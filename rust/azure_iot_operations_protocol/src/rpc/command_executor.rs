// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

use std::str::FromStr;
use std::{collections::HashMap, marker::PhantomData, time::Duration};

use azure_iot_operations_mqtt::control_packet::{Publish, PublishProperties, QoS};
use azure_iot_operations_mqtt::interface::{ManagedClient, MqttAck, PubReceiver};
use bytes::Bytes;
use tokio::time::{timeout, Instant};
use tokio::{sync::oneshot, task::JoinSet};
use tokio_util::sync::CancellationToken;

use super::StatusCode;
use crate::common::{
    aio_protocol_error::{AIOProtocolError, Value},
    hybrid_logical_clock::HybridLogicalClock,
    payload_serialize::{FormatIndicator, PayloadSerialize},
    topic_processor::{contains_invalid_char, is_valid_replacement, TopicPattern, WILDCARD},
    user_properties::{validate_user_properties, UserProperty, RESERVED_PREFIX},
};

/// Default message expiry interval only for when the message expiry interval is not present
const DEFAULT_MESSAGE_EXPIRY_INTERVAL: u64 = 10;

/// Message for when expiration time is unable to be calculated, internal logic error
const INTERNAL_LOGIC_EXPIRATION_ERROR: &str =
    "Internal logic error, unable to calculate command expiration time";

/// Struct to hold response arguments
struct ResponseArguments {
    command_name: String,
    response_topic: String,
    correlation_data: Option<Bytes>,
    status_code: StatusCode,
    status_message: Option<String>,
    is_application_error: bool,
    invalid_property_name: Option<String>,
    invalid_property_value: Option<String>,
    command_expiration_time: Option<Instant>,
}

/// Command Request struct.
/// Used by the [`CommandExecutor`]
///
/// If dropped, executor will send an error response to the invoker
pub struct CommandRequest<TReq, TResp>
where
    TReq: PayloadSerialize,
    TResp: PayloadSerialize,
{
    /// Payload of the command request.
    pub payload: TReq,
    /// Custom user data set as custom MQTT User Properties on the request message.
    pub custom_user_data: Vec<(String, String)>,
    /// Fencing token of the command request.
    pub fencing_token: Option<HybridLogicalClock>,
    /// Timestamp of the command request.
    pub timestamp: Option<HybridLogicalClock>,
    /// Client ID of the invoker.
    pub invoker_id: String,

    // Internal fields
    response_tx: oneshot::Sender<Result<CommandResponse<TResp>, String>>,
}

impl<TReq, TResp> CommandRequest<TReq, TResp>
where
    TReq: PayloadSerialize,
    TResp: PayloadSerialize,
{
    /// Consumes the command request and completes it with a response.
    ///
    /// # Arguments
    /// * `response` - The [`CommandResponse`] to send.
    ///
    /// Returns Ok(()) on success, otherwise returns the [`CommandResponse`] response.
    ///
    /// # Errors
    /// Returns the [`CommandResponse`] if the response is no longer expected because of a
    /// timeout or dropped executor.
    pub fn complete(self, response: CommandResponse<TResp>) -> Result<(), CommandResponse<TResp>> {
        match self.response_tx.send(Ok(response)) {
            Ok(()) => Ok(()),
            Err(e) => match e {
                Ok(resp) => Err(resp),
                Err(_) => unreachable!(), // The response channel is sending a command response, receiving an error message on failure is impossible
            },
        }
    }

    /// Consumes the command request and completes it with an error message.
    ///
    /// # Arguments
    /// * `error` - The error message to send.
    ///
    /// Returns Ok(()) on success, otherwise returns the error message.
    ///
    /// # Errors
    /// Returns the error message if the response channel is no longer expected because of a
    /// timeout or dropped executor.
    pub fn error(self, error: String) -> Result<(), String> {
        match self.response_tx.send(Err(error)) {
            Ok(()) => Ok(()),
            Err(e) => match e {
                Ok(_) => unreachable!(), // The response channel is sending an error message, receiving a response on failure is impossible
                Err(e_msg) => Err(e_msg),
            },
        }
    }

    /// Check if the command response is no longer expected.
    ///
    /// Returns true if the response is no longer expected, otherwise returns false.
    pub fn is_cancelled(&self) -> bool {
        self.response_tx.is_closed()
    }
}

/// Command Response struct.
/// Used by the [`CommandExecutor`]
#[derive(Builder, Clone, Debug)]
#[builder(setter(into, strip_option), build_fn(validate = "Self::validate"))]
pub struct CommandResponse<TResp>
where
    TResp: PayloadSerialize,
{
    /// Payload of the command response.
    #[builder(setter(custom))]
    payload: Vec<u8>,
    /// Strongly link `CommandResponse` with type `TResp`
    #[builder(private)]
    response_payload_type: PhantomData<TResp>,
    /// Custom user data set as custom MQTT User Properties on the response message.
    /// Used to pass additional metadata to the invoker.
    /// Default is an empty vector.
    #[builder(default)]
    custom_user_data: Vec<(String, String)>,
}

impl<TResp: PayloadSerialize> CommandResponseBuilder<TResp> {
    /// Add a payload to the command response. Validates successful serialization of the payload.
    ///
    /// # Errors
    /// Returns a [`PayloadSerialize::Error`] if serialization of the payload fails
    pub fn payload(&mut self, payload: &TResp) -> Result<&mut Self, TResp::Error> {
        let serialized_payload = payload.serialize()?;
        self.payload = Some(serialized_payload);
        self.response_payload_type = Some(PhantomData);
        Ok(self)
    }

    /// Validate the command response.
    ///
    /// # Errors
    /// Returns a `String` describing the error if
    ///     - any of `custom_user_data`'s keys start with the [`RESERVED_PREFIX`]
    ///     - any of `custom_user_data`'s keys or values are invalid utf-8
    fn validate(&self) -> Result<(), String> {
        if let Some(custom_user_data) = &self.custom_user_data {
            return validate_user_properties(custom_user_data);
        }
        Ok(())
    }
}

/// Command Executor Options struct
#[allow(unused)]
#[derive(Builder, Clone)]
#[builder(setter(into, strip_option))]
pub struct CommandExecutorOptions {
    /// Topic pattern for the command request
    /// Must align with [topic-structure.md](https://github.com/microsoft/mqtt-patterns/blob/main/docs/specs/topic-structure.md)
    request_topic_pattern: String,
    /// Command name if required by the topic pattern
    command_name: String,
    /// Executor ID if required by the topic pattern
    #[builder(default = "None")]
    executor_id: Option<String>,
    /// Model ID if required by the topic pattern
    #[builder(default = "None")]
    model_id: Option<String>,
    /// Optional Topic namespace to be prepended to the topic pattern
    #[builder(default = "None")]
    topic_namespace: Option<String>,
    /// Custom topic token keys/values to be replaced in the topic pattern
    #[builder(default)]
    custom_topic_token_map: HashMap<String, String>,
    /// Duration to cache the command response
    #[builder(default = "Duration::from_secs(0)")]
    cacheable_duration: Duration,
    /// Denotes if commands are idempotent
    #[builder(default = "false")]
    is_idempotent: bool,
    /// Service group ID
    #[builder(default = "None")]
    service_group_id: Option<String>,
}

/// Command Executor struct
/// # Example
/// ```
/// # use std::{collections::HashMap, time::Duration};
/// # use tokio_test::block_on;
/// # use azure_iot_operations_mqtt::MqttConnectionSettingsBuilder;
/// # use azure_iot_operations_mqtt::session::{Session, SessionOptionsBuilder};
/// # use azure_iot_operations_protocol::rpc::command_executor::{CommandExecutor, CommandExecutorOptionsBuilder, CommandResponse, CommandResponseBuilder, CommandRequest};
/// # use azure_iot_operations_protocol::common::payload_serialize::{PayloadSerialize, FormatIndicator};
/// # #[derive(Clone, Debug)]
/// # pub struct SamplePayload { }
/// # impl PayloadSerialize for SamplePayload {
/// #   type Error = String;
/// #   fn content_type() -> &'static str { "application/json" }
/// #   fn format_indicator() -> FormatIndicator { FormatIndicator::Utf8EncodedCharacterData }
/// #   fn serialize(&self) -> Result<Vec<u8>, String> { Ok(Vec::new()) }
/// #   fn deserialize(payload: &[u8]) -> Result<Self, String> { Ok(SamplePayload {}) }
/// # }
/// # let mut connection_settings = MqttConnectionSettingsBuilder::default()
/// #     .client_id("test_server")
/// #     .host_name("mqtt://localhost")
/// #     .tcp_port(1883u16)
/// #     .build().unwrap();
/// # let mut session_options = SessionOptionsBuilder::default()
/// #     .connection_settings(connection_settings)
/// #     .build().unwrap();
/// # let mut mqtt_session = Session::new(session_options).unwrap();
/// let executor_options = CommandExecutorOptionsBuilder::default()
///   .command_name("test_command")
///   .request_topic_pattern("test/request")
///   .build().unwrap();
/// # tokio_test::block_on(async {
/// let mut command_executor: CommandExecutor<SamplePayload, SamplePayload, _> = CommandExecutor::new(mqtt_session.create_managed_client(), executor_options).unwrap();
/// // command_executor.start().await.unwrap();
/// // let request = command_executor.recv().await.unwrap();
/// // let response = CommandResponseBuilder::default()
///  // .payload(SamplePayload {})
///  // .build().unwrap();
/// // let request.complete(response).unwrap();
/// # });
/// ```
#[allow(unused)]
pub struct CommandExecutor<TReq, TResp, C>
where
    TReq: PayloadSerialize + Send + 'static,
    TResp: PayloadSerialize + Send + 'static,
    C: ManagedClient + Clone + Send + Sync + 'static,
    C::PubReceiver: Send + Sync + 'static,
{
    // Static properties of the executor
    mqtt_client: C,
    mqtt_receiver: C::PubReceiver,
    is_idempotent: bool,
    request_topic: String,
    command_name: String,
    cacheable_duration: Duration,
    request_payload_type: PhantomData<TReq>,
    response_payload_type: PhantomData<TResp>,
    // Describes state
    is_subscribed: bool,
    // Information to manage state
    pending_pubs: JoinSet<Publish>, // TODO: Consider using FuturesUnordered
    recv_cancellation_token: CancellationToken,
}

/// Implementation of Command Executor.
impl<TReq, TResp, C> CommandExecutor<TReq, TResp, C>
where
    TReq: PayloadSerialize + Send + 'static,
    TResp: PayloadSerialize + Send + 'static,
    C: ManagedClient + Clone + Send + Sync + 'static,
    C::PubReceiver: Send + Sync + 'static,
{
    /// Create a new [`CommandExecutor`].
    ///
    /// # Arguments
    /// * `client` - The MQTT client to use for communication
    /// * `executor_options` - Configuration options
    ///
    /// Returns Ok([`CommandExecutor`]) on success, otherwise returns [`AIOProtocolError`].
    ///
    /// # Errors
    /// [`AIOProtocolError`] of kind [`ConfigurationInvalid`](crate::common::aio_protocol_error::AIOProtocolErrorKind::ConfigurationInvalid)
    /// - [`command_name`](CommandExecutorOptions::command_name) is empty, whitespace or invalid
    /// - [`request_topic_pattern`](CommandExecutorOptions::request_topic_pattern),
    ///     [`executor_id`](CommandExecutorOptions::executor_id),
    ///     [`model_id`](CommandExecutorOptions::model_id) or
    ///     [`topic_namespace`](CommandExecutorOptions::topic_namespace)
    ///     are Some and invalid or contain a token with no valid replacement
    /// - [`custom_topic_token_map`](CommandExecutorOptions::custom_topic_token_map) is not empty and contains invalid key(s) and/or token(s)
    /// - [`is_idempotent`](CommandExecutorOptions::is_idempotent) is false and [`cacheable_duration`](CommandExecutorOptions::cacheable_duration) is not zero
    pub fn new(
        client: C,
        executor_options: CommandExecutorOptions,
    ) -> Result<Self, AIOProtocolError> {
        // Validate function parameters, validation for topic pattern and related options done in
        // TopicPattern::new_command_pattern
        if executor_options.command_name.is_empty()
            || contains_invalid_char(&executor_options.command_name)
        {
            return Err(AIOProtocolError::new_configuration_invalid_error(
                None,
                "command_name",
                Value::String(executor_options.command_name.clone()),
                None,
                Some(executor_options.command_name),
            ));
        }
        if !executor_options.is_idempotent && !executor_options.cacheable_duration.is_zero() {
            return Err(AIOProtocolError::new_configuration_invalid_error(
                None,
                "is_idempotent",
                Value::Boolean(executor_options.is_idempotent),
                None,
                Some(executor_options.command_name),
            ));
        }

        // If executor_id is not provided, use the client_id
        let executor_id = executor_options
            .executor_id
            .as_deref()
            .unwrap_or(client.client_id());

        // Create a new Command Pattern, validates topic pattern and options
        let request_topic_pattern = TopicPattern::new_command_pattern(
            &executor_options.request_topic_pattern,
            &executor_options.command_name,
            executor_id,
            WILDCARD,
            executor_options.model_id.as_deref(),
            executor_options.topic_namespace.as_deref(),
            &executor_options.custom_topic_token_map,
        )?;

        // Get the request topic
        let request_topic = request_topic_pattern.as_subscribe_topic();

        // Create cancellation token for the request receive loop
        let recv_cancellation_token = CancellationToken::new();

        // Get pub sub and receiver from the mqtt session
        let mqtt_receiver = match client
            .create_filtered_pub_receiver(&request_topic_pattern.as_subscribe_topic(), false)
        {
            Ok(receiver) => receiver,
            Err(e) => {
                return Err(AIOProtocolError::new_configuration_invalid_error(
                    Some(Box::new(e)),
                    "request_topic_pattern",
                    Value::String(request_topic_pattern.as_subscribe_topic()),
                    Some("Could not parse request topic pattern".to_string()),
                    Some(executor_options.command_name),
                ));
            }
        };

        // Create Command executor
        Ok(CommandExecutor {
            mqtt_client: client,
            mqtt_receiver,
            is_idempotent: executor_options.is_idempotent,
            request_topic,
            command_name: executor_options.command_name,
            cacheable_duration: executor_options.cacheable_duration,
            request_payload_type: PhantomData,
            response_payload_type: PhantomData,
            is_subscribed: false,
            pending_pubs: JoinSet::new(),
            recv_cancellation_token,
        })
    }

    // TODO: Finish implementing shutdown logic
    /// Shutdown the [`CommandExecutor`]. Unsubscribes from the request topic.
    ///
    /// Returns Ok(()) on success, otherwise returns [`AIOProtocolError`].
    /// # Errors
    /// [`AIOProtocolError`] of kind [`ClientError`](crate::common::aio_protocol_error::AIOProtocolErrorKind::ClientError) if the unsubscribe fails or if the unsuback reason code doesn't indicate success.
    pub async fn shutdown(&mut self) -> Result<(), AIOProtocolError> {
        let unsubscribe_result = self.mqtt_client.unsubscribe(&self.request_topic).await;

        match unsubscribe_result {
            Ok(unsub_ct) => {
                match unsub_ct.wait().await {
                    Ok(()) => { /* Success */ }
                    Err(e) => {
                        log::error!("[{}] Unsuback error: {e}", self.command_name);
                        return Err(AIOProtocolError::new_mqtt_error(
                            Some("MQTT error on command executor unsuback".to_string()),
                            Box::new(e),
                            Some(self.command_name.clone()),
                        ));
                    }
                }
            }
            Err(e) => {
                log::error!(
                    "[{}] Client error while unsubscribing: {e}",
                    self.command_name
                );
                return Err(AIOProtocolError::new_mqtt_error(
                    Some("Client error on command executor unsubscribe".to_string()),
                    Box::new(e),
                    Some(self.command_name.clone()),
                ));
            }
        }
        log::info!("[{}] Stopped", self.command_name);
        Ok(())
    }

    /// Subscribe to the request topic if not already subscribed.
    ///
    /// Returns Ok(()) on success, otherwise returns [`AIOProtocolError`].
    /// # Errors
    /// [`AIOProtocolError`] of kind [`ClientError`](crate::common::aio_protocol_error::AIOProtocolErrorKind::ClientError) if the subscribe fails or if the suback reason code doesn't indicate success.
    async fn try_subscribe(&mut self) -> Result<(), AIOProtocolError> {
        if !self.is_subscribed {
            let subscribe_result = self
                .mqtt_client
                .subscribe(&self.request_topic, QoS::AtLeastOnce)
                .await;

            match subscribe_result {
                Ok(sub_ct) => match sub_ct.wait().await {
                    Ok(()) => {
                        self.is_subscribed = true;
                    }
                    Err(e) => {
                        log::error!("[{}] Suback error: {e}", self.command_name);
                        return Err(AIOProtocolError::new_mqtt_error(
                            Some("MQTT error on command executor suback".to_string()),
                            Box::new(e),
                            Some(self.command_name.clone()),
                        ));
                    }
                },
                Err(e) => {
                    log::error!(
                        "[{}] Client error while subscribing: {e}",
                        self.command_name
                    );
                    return Err(AIOProtocolError::new_mqtt_error(
                        Some("Client error on command executor subscribe".to_string()),
                        Box::new(e),
                        Some(self.command_name.clone()),
                    ));
                }
            }
        }
        Ok(())
    }

    /// Receive a command request.
    ///
    /// Will also subscribe to the request topic if not already subscribed.
    ///
    /// Returns Ok([`CommandRequest`]) on success, otherwise returns [`AIOProtocolError`].
    /// # Errors
    /// [`AIOProtocolError`] of kind [`UnknownError`](crate::common::aio_protocol_error::AIOProtocolErrorKind::UnknownError) if an error occurs while receiving the message.
    /// [`AIOProtocolError`] of kind [`ClientError`](crate::common::aio_protocol_error::AIOProtocolErrorKind::ClientError) if the subscribe fails or if the suback reason code doesn't indicate success.
    /// [`AIOProtocolError`] of kind [`InternalLogicError`](crate::common::aio_protocol_error::AIOProtocolErrorKind::InternalLogicError) if the command expiration time cannot be calculated.
    pub async fn recv(&mut self) -> Result<CommandRequest<TReq, TResp>, AIOProtocolError> {
        // Subscribe to the request topic if not already subscribed
        self.try_subscribe().await?;

        loop {
            tokio::select! {
                // TODO: BUG, if recv() is not called, pending_pubs will never be processed
                Some(pending_pub) = self.pending_pubs.join_next() => {
                    match pending_pub {
                        Ok(pending_pub) => {
                            match self.mqtt_receiver.ack(&pending_pub).await {
                                Ok(()) => { /* Success */ }
                                Err(e) => {
                                    log::error!("[{}][pkid: {}] Ack error: {e}", self.command_name, pending_pub.pkid);
                                }
                            }
                        }
                        Err(e) => {
                            // Unreachable: Occurs when the task failed to execute to completion by
                            // panicking or cancelling.
                            log::error!("[{}] Failure to process command response: {e}", self.command_name);
                        }
                    }
                },
                request = self.mqtt_receiver.recv() => {
                    // Process the request
                    if let Some(m) = request {
                        log::info!("[{}][pkid: {}] Received request", self.command_name, m.pkid);
                        let message_received_time = Instant::now();

                        // Clone properties
                        let properties = match &m.properties {
                            Some(properties) => properties.clone(),
                            None => {
                                log::error!("[{}][pkid: {}] Properties missing", self.command_name, m.pkid);
                                self.pending_pubs.spawn(async move { m });
                                continue;
                            }
                        };

                        // Get response topic
                        let response_topic = if let Some(rt) = properties.response_topic {
                            if !is_valid_replacement(&rt) {
                                log::error!("[{}][pkid: {}] Response topic invalid, command response will not be published", self.command_name, m.pkid);
                                self.pending_pubs.spawn(async move { m });
                                continue;
                            }
                            rt
                        } else {
                            log::error!("[{}][pkid: {}] Response topic missing", self.command_name, m.pkid);
                            self.pending_pubs.spawn(async move { m });
                            continue;
                        };

                        let mut command_expiration_time_calculated = false;
                        let mut response_arguments = ResponseArguments {
                            command_name: self.command_name.clone(),
                            response_topic,
                            correlation_data: None,
                            status_code: StatusCode::Ok,
                            status_message: None,
                            is_application_error: false,
                            invalid_property_name: None,
                            invalid_property_value: None,
                            command_expiration_time: None,
                        };

                        // Get message expiry interval
                        let command_expiration_time = if let Some(ct) = properties.message_expiry_interval {
                            message_received_time.checked_add(Duration::from_secs(ct.into()))
                        } else {
                            message_received_time.checked_add(Duration::from_secs(DEFAULT_MESSAGE_EXPIRY_INTERVAL))
                        };

                        // Check if there was an error calculating the command expiration time
                        // if not, set the command expiration time
                        if let Some(command_expiration_time) = command_expiration_time{
                            response_arguments.command_expiration_time = Some(command_expiration_time);
                            command_expiration_time_calculated = true;
                        }

                        // TODO: Use once shutdown is implemented
                        let _execution_cancellation_token = CancellationToken::new();
                        'process_request: {
                            // Get correlation data
                            if let Some(correlation_data) = properties.correlation_data {
                                if correlation_data.len() != 16 {
                                    response_arguments.status_code = StatusCode::BadRequest;
                                    response_arguments.status_message = Some("Correlation data bytes do not conform to a GUID".to_string());
                                    response_arguments.invalid_property_name = Some("Correlation Data".to_string());
                                    if let Ok(correlation_data_str) = String::from_utf8(correlation_data.to_vec()) {
                                        response_arguments.invalid_property_value = Some(correlation_data_str);
                                    } else { /* Ignore */ }
                                    response_arguments.correlation_data = Some(correlation_data);
                                    break 'process_request;
                                }
                                response_arguments.correlation_data = Some(correlation_data);
                            } else {
                                response_arguments.status_code = StatusCode::BadRequest;
                                response_arguments.status_message = Some("Correlation data missing".to_string());
                                response_arguments.invalid_property_name = Some("Correlation Data".to_string());
                                break 'process_request;
                            };

                            // Checking if command expiration time was calculated after correlation
                            // to provide a more accurate response to the invoker.
                            let Some(command_expiration_time) = command_expiration_time else {
                                response_arguments.status_code = StatusCode::InternalServerError;
                                response_arguments.status_message = Some(INTERNAL_LOGIC_EXPIRATION_ERROR.to_string());
                                break 'process_request;
                            };

                            // Check if message expiry interval is present
                            if properties.message_expiry_interval.is_none() {
                                response_arguments.status_code = StatusCode::BadRequest;
                                response_arguments.status_message = Some("Message expiry interval missing".to_string());
                                response_arguments.invalid_property_name = Some("Message Expiry Interval".to_string());
                                break 'process_request;
                            }

                            // Get payload format indicator (underlying mqtt client should validate that format indicator is 0 or 1)
                            if let Some(format_indicator) = properties.payload_format_indicator {
                                if format_indicator != FormatIndicator::Utf8EncodedCharacterData as u8 && format_indicator != TReq::format_indicator() as u8 {
                                    response_arguments.status_code = StatusCode::UnsupportedMediaType;
                                    response_arguments.status_message = Some(format!("Format indicator {format_indicator} is not appropriate for {} content", TReq::content_type()));
                                    response_arguments.invalid_property_name = Some("Payload Format Indicator".to_string());
                                    response_arguments.invalid_property_value = Some(format_indicator.to_string());
                                    break 'process_request;
                                }
                            };

                            // Get content type
                            if let Some(content_type) = properties.content_type {
                                if TReq::content_type() != content_type {
                                    response_arguments.status_code = StatusCode::UnsupportedMediaType;
                                    response_arguments.status_message = Some(format!("Content type {content_type} is not supported by this implementation; only {} is accepted", TReq::content_type()));
                                    response_arguments.invalid_property_name = Some("Content Type".to_string());
                                    response_arguments.invalid_property_value = Some(content_type);
                                    break 'process_request;
                                }
                            };

                            let mut user_data = Vec::new();
                            let mut timestamp = None;
                            let mut invoker_id = None;
                            for (key,value) in properties.user_properties {
                                match UserProperty::from_str(&key) {
                                    Ok(UserProperty::Timestamp) => {
                                        match HybridLogicalClock::from_str(&value) {
                                            Ok(ts) => {
                                                timestamp = Some(ts);
                                            },
                                            Err(e) => {
                                                response_arguments.status_code = StatusCode::BadRequest;
                                                response_arguments.status_message = Some(format!("Timestamp invalid: {e}"));
                                                response_arguments.invalid_property_name = Some(UserProperty::Timestamp.to_string());
                                                response_arguments.invalid_property_value = Some(value);
                                                break 'process_request;
                                            }
                                        }
                                    },
                                    Ok(UserProperty::CommandInvokerId) => {
                                        invoker_id = Some(value);
                                    },
                                    Err(()) => {
                                        if key.starts_with(RESERVED_PREFIX) {
                                            // Don't return error, although these properties shouldn't be present on a request
                                            log::error!("Invalid request user data property '{}' starts with reserved prefix '{}'. Value is '{}'", key, RESERVED_PREFIX, value);
                                        } else {
                                            user_data.push((key, value));
                                        }
                                    }
                                    _ => {
                                        /* UserProperty::Status, UserProperty::StatusMessage, UserProperty::IsApplicationError, UserProperty::InvalidPropertyName, UserProperty::InvalidPropertyValue */
                                        // Don't return error, although above properties shouldn't be in the request
                                        // TODO: Add validation for Fencing Token
                                        log::error!("Request should not contain MQTT user property {key}. Value is {value}");
                                    }
                                }
                            }

                            let Some(invoker_id) = invoker_id else {
                                 response_arguments.status_code = StatusCode::BadRequest;
                                 response_arguments.status_message = Some(format!("No invoker client id ({}) property present", UserProperty::CommandInvokerId));
                                 response_arguments.invalid_property_name = Some(UserProperty::CommandInvokerId.to_string());
                                 break 'process_request;
                             };

                            // Deserialize payload
                            let payload = match TReq::deserialize(&m.payload) {
                                Ok(payload) => payload,
                                Err(e) => {
                                    response_arguments.status_code = StatusCode::BadRequest;
                                    response_arguments.status_message = Some(format!("Error deserializing payload: {e:?}"));
                                    break 'process_request;
                                }
                            };

                            let (response_tx, response_rx) = oneshot::channel();

                            let command_request = CommandRequest {
                                payload,
                                custom_user_data: user_data,
                                fencing_token: None, // TODO: Add fencing token
                                timestamp,
                                invoker_id,
                                response_tx,
                            };

                            // Check the command has not expired, if it has, we do not respond to the invoker.
                            if command_expiration_time.elapsed().is_zero() { // Elapsed returns zero if the time has not passed
                                self.pending_pubs.spawn({
                                    let client_clone = self.mqtt_client.clone();
                                    let recv_cancellation_token_clone = self.recv_cancellation_token.clone();
                                    let pkid = m.pkid;
                                    async move {
                                        tokio::select! {
                                            () = recv_cancellation_token_clone.cancelled() => { /* Receive loop cancelled */},
                                            () = Self::process_command(
                                                    client_clone,
                                                    pkid,
                                                    response_arguments,
                                                    Some(response_rx),
                                            ) => { /* Finished processing command */},
                                        }
                                        m
                                    }
                                });
                                return Ok(command_request);
                            }
                        }

                        // Checking that command expiration time was calculated and has not
                        // expired. If it has, we do not respond to the invoker.
                        if let Some(command_expiration_time) = command_expiration_time {
                            if !command_expiration_time.elapsed().is_zero() {
                                continue;
                            }
                        }

                        self.pending_pubs.spawn({
                            let client_clone = self.mqtt_client.clone();
                            let recv_cancellation_token_clone = self.recv_cancellation_token.clone();
                            let pkid = m.pkid;
                            async move {
                                tokio::select! {
                                    () = recv_cancellation_token_clone.cancelled() => { /* Receive loop cancelled */},
                                    () = Self::process_command(
                                        client_clone,
                                        pkid,
                                        response_arguments,
                                        None,
                                    ) => { /* Finished processing command */},
                                }
                                m
                            }
                        });

                        if !command_expiration_time_calculated {
                            return Err(AIOProtocolError::new_internal_logic_error(
                                true,
                                false,
                                None,
                                None,
                                "command_expiration_time",
                                None,
                                Some(INTERNAL_LOGIC_EXPIRATION_ERROR.to_string()),
                                Some(self.command_name.clone())));
                        }
                    } else {
                        // TODO: Change the signature to return Option.
                        log::error!("MqttReceiver Closed");
                        return Err(AIOProtocolError::new_unknown_error(false, false, None, None, None, Some(self.command_name.clone())));
                    }
                }
            }
        }
    }

    async fn process_command(
        client: C,
        pkid: u16,
        mut response_arguments: ResponseArguments,
        response_rx: Option<oneshot::Receiver<Result<CommandResponse<TResp>, String>>>,
    ) {
        let mut user_properties: Vec<(String, String)> = Vec::new();
        let mut payload = Vec::new();
        'process_response: {
            let Some(command_expiration_time) = response_arguments.command_expiration_time else {
                break 'process_response;
            };
            if let Some(response_rx) = response_rx {
                // Wait for response
                let response = if let Ok(response_timer) = timeout(
                    command_expiration_time.duration_since(Instant::now()),
                    response_rx,
                )
                .await
                {
                    if let Ok(response_app) = response_timer {
                        match response_app {
                            Ok(response) => response,
                            Err(e) => {
                                response_arguments.status_code = StatusCode::InternalServerError;
                                response_arguments.status_message = Some(e);
                                response_arguments.is_application_error = true;
                                break 'process_response;
                            }
                        }
                    } else {
                        // Happens when the sender is dropped by the application.
                        response_arguments.status_code = StatusCode::InternalServerError;
                        response_arguments.status_message =
                            Some("Request has been dropped by the application".to_string());
                        response_arguments.is_application_error = true;
                        break 'process_response;
                    }
                } else {
                    log::error!(
                        "[{}][pkid: {}] Request timed out",
                        response_arguments.command_name,
                        pkid
                    );
                    return;
                };

                user_properties = response.custom_user_data;

                // Serialize payload
                payload = response.payload;

                if payload.is_empty() {
                    response_arguments.status_code = StatusCode::NoContent;
                }
            } else { /* Error */
            }
        }

        if response_arguments.status_code != StatusCode::Ok
            || response_arguments.status_code != StatusCode::NoContent
        {
            user_properties.push((
                UserProperty::IsApplicationError.to_string(),
                response_arguments.is_application_error.to_string(),
            ));
        }

        user_properties.push((
            UserProperty::Status.to_string(),
            (response_arguments.status_code as u16).to_string(),
        ));

        if let Some(status_message) = response_arguments.status_message {
            log::error!(
                "[{}][pkid: {}] {}",
                response_arguments.command_name,
                pkid,
                status_message
            );
            user_properties.push((UserProperty::StatusMessage.to_string(), status_message));
        }

        if let Some(name) = response_arguments.invalid_property_name {
            user_properties.push((
                UserProperty::InvalidPropertyName.to_string(),
                name.to_string(),
            ));
        }

        if let Some(value) = response_arguments.invalid_property_value {
            user_properties.push((
                UserProperty::InvalidPropertyValue.to_string(),
                value.to_string(),
            ));
        }

        let message_expiry_interval =
            if let Some(command_expiration_time) = response_arguments.command_expiration_time {
                command_expiration_time.saturating_duration_since(Instant::now())
            } else {
                // Happens when the command expiration time was not able to be calculated.
                Duration::from_secs(DEFAULT_MESSAGE_EXPIRY_INTERVAL)
            };

        if message_expiry_interval.is_zero() {
            log::error!(
                "[{}][pkid: {}] Request timed out",
                response_arguments.command_name,
                pkid
            );
            return;
        }

        let Ok(message_expiry_interval) = message_expiry_interval.as_secs().try_into() else {
            // Unreachable, will be smaller than u32::MAX
            log::error!(
                "[{}][pkid: {}] Message expiry interval is too large",
                response_arguments.command_name,
                pkid
            );
            return;
        };

        // Create publish properties
        let publish_properties = PublishProperties {
            payload_format_indicator: Some(TResp::format_indicator() as u8),
            message_expiry_interval: Some(message_expiry_interval),
            topic_alias: None,
            response_topic: None,
            correlation_data: response_arguments.correlation_data,
            user_properties,
            subscription_identifiers: Vec::new(),
            content_type: Some(TResp::content_type().to_string()),
        };

        // Try to publish
        match client
            .publish_with_properties(
                response_arguments.response_topic,
                QoS::AtLeastOnce,
                false,
                payload,
                publish_properties,
            )
            .await
        {
            Ok(publish_completion_token) => {
                // Wait and handle puback
                match publish_completion_token.wait().await {
                    Ok(()) => {}
                    Err(e) => {
                        log::error!(
                            "[{}][pkid: {}] Puback error: {e}",
                            response_arguments.command_name,
                            pkid
                        );
                    }
                }
            }
            Err(e) => {
                log::error!(
                    "[{}][pkid: {}] Client error on command executor response publish: {e}",
                    response_arguments.command_name,
                    pkid
                );
            }
        }
    }
}

impl<TReq, TResp, C> Drop for CommandExecutor<TReq, TResp, C>
where
    TReq: PayloadSerialize + Send + 'static,
    TResp: PayloadSerialize + Send + 'static,
    C: ManagedClient + Clone + Send + Sync + 'static,
    C::PubReceiver: Send + Sync + 'static,
{
    fn drop(&mut self) {}
}

#[cfg(test)]
mod tests {
    use azure_iot_operations_mqtt::session::{Session, SessionOptionsBuilder};
    use test_case::test_case;
    // TODO: This dependency on MqttConnectionSettingsBuilder should be removed in lieu of using a true mock
    use azure_iot_operations_mqtt::MqttConnectionSettingsBuilder;

    use super::*;
    use crate::common::{aio_protocol_error::AIOProtocolErrorKind, payload_serialize::MockPayload};

    // TODO: This should return a mock ManagedClient instead.
    // Until that's possible, need to return a Session so that the Session doesn't go out of
    // scope and render the ManagedClient unable to to be used correctly.
    fn create_session() -> Session {
        let connection_settings = MqttConnectionSettingsBuilder::default()
            .host_name("localhost")
            .client_id("test_server")
            .build()
            .unwrap();
        let session_options = SessionOptionsBuilder::default()
            .connection_settings(connection_settings)
            .build()
            .unwrap();
        Session::new(session_options).unwrap()
    }

    #[tokio::test]
    async fn test_new_defaults() {
        let session = create_session();
        let managed_client = session.create_managed_client();
        let executor_options = CommandExecutorOptionsBuilder::default()
            .request_topic_pattern("test/{commandName}/{executorId}/request")
            .command_name("test_command_name")
            .build()
            .unwrap();

        let command_executor: CommandExecutor<MockPayload, MockPayload, _> =
            CommandExecutor::new(managed_client, executor_options).unwrap();

        assert_eq!(
            command_executor.request_topic,
            "test/test_command_name/test_server/request"
        );

        assert!(!command_executor.is_idempotent);
        // Since idempotent is false by default, cacheable_duration should be 0
        assert_eq!(command_executor.cacheable_duration, Duration::from_secs(0));
    }

    #[tokio::test]
    async fn test_new_override_defaults() {
        let session = create_session();
        let managed_client = session.create_managed_client();
        let executor_options = CommandExecutorOptionsBuilder::default()
            .request_topic_pattern("test/{commandName}/{executorId}/{modelId}/request")
            .command_name("test_command_name")
            .executor_id("test_executor_id")
            .model_id("test_model_id")
            .topic_namespace("test_namespace")
            .custom_topic_token_map(HashMap::new())
            .cacheable_duration(Duration::from_secs(10))
            .is_idempotent(true)
            .build()
            .unwrap();

        let command_executor: CommandExecutor<MockPayload, MockPayload, _> =
            CommandExecutor::new(managed_client, executor_options).unwrap();

        assert_eq!(
            command_executor.request_topic,
            "test_namespace/test/test_command_name/test_executor_id/test_model_id/request"
        );

        assert!(command_executor.is_idempotent);
        assert_eq!(command_executor.cacheable_duration, Duration::from_secs(10));
    }

    #[test_case(""; "empty command name")]
    #[test_case(" "; "whitespace command name")]
    #[tokio::test]
    async fn test_new_empty_and_whitespace_command_name(command_name: &str) {
        let session = create_session();
        let managed_client = session.create_managed_client();

        let executor_options = CommandExecutorOptionsBuilder::default()
            .request_topic_pattern("test/{commandName}/request")
            .command_name(command_name.to_string())
            .build()
            .unwrap();

        let executor: Result<CommandExecutor<MockPayload, MockPayload, _>, AIOProtocolError> =
            CommandExecutor::new(managed_client, executor_options);

        match executor {
            Err(e) => {
                assert_eq!(e.kind, AIOProtocolErrorKind::ConfigurationInvalid);
                assert!(!e.in_application);
                assert!(e.is_shallow);
                assert!(!e.is_remote);
                assert_eq!(e.http_status_code, None);
                assert_eq!(e.property_name, Some("command_name".to_string()));
                assert!(e.property_value == Some(Value::String(command_name.to_string())));
            }
            Ok(_) => {
                panic!("Expected error");
            }
        }
    }

    #[test_case(""; "empty request topic pattern")]
    #[test_case(" "; "whitespace request topic pattern")]
    #[test_case("test/{commandName}/\u{0}/request"; "invalid request topic pattern")]
    #[tokio::test]
    async fn test_invalid_request_topic_string(request_topic: &str) {
        let session = create_session();
        let managed_client = session.create_managed_client();

        let executor_options = CommandExecutorOptionsBuilder::default()
            .request_topic_pattern(request_topic.to_string())
            .command_name("test_command_name")
            .build()
            .unwrap();

        let executor: Result<CommandExecutor<MockPayload, MockPayload, _>, AIOProtocolError> =
            CommandExecutor::new(managed_client, executor_options);

        match executor {
            Err(e) => {
                assert_eq!(e.kind, AIOProtocolErrorKind::ConfigurationInvalid);
                assert!(!e.in_application);
                assert!(e.is_shallow);
                assert!(!e.is_remote);
                assert_eq!(e.http_status_code, None);
                assert_eq!(e.property_name, Some("pattern".to_string()));
                assert!(e.property_value == Some(Value::String(request_topic.to_string())));
            }
            Ok(_) => {
                panic!("Expected error");
            }
        }
    }

    #[test_case(""; "empty topic namespace")]
    #[test_case(" "; "whitespace topic namespace")]
    #[test_case("test/\u{0}"; "invalid topic namespace")]
    #[tokio::test]
    async fn test_invalid_topic_namespace(topic_namespace: &str) {
        let session = create_session();
        let managed_client = session.create_managed_client();
        let executor_options = CommandExecutorOptionsBuilder::default()
            .request_topic_pattern("test/{commandName}/request")
            .command_name("test_command_name")
            .topic_namespace(topic_namespace.to_string())
            .build()
            .unwrap();

        let executor: Result<CommandExecutor<MockPayload, MockPayload, _>, AIOProtocolError> =
            CommandExecutor::new(managed_client, executor_options);
        match executor {
            Err(e) => {
                assert_eq!(e.kind, AIOProtocolErrorKind::ConfigurationInvalid);
                assert!(!e.in_application);
                assert!(e.is_shallow);
                assert!(!e.is_remote);
                assert_eq!(e.http_status_code, None);
                assert_eq!(e.property_name, Some("topic_namespace".to_string()));
                assert!(e.property_value == Some(Value::String(topic_namespace.to_string())));
            }
            Ok(_) => {
                panic!("Expected error");
            }
        }
    }

    #[test_case(Duration::from_secs(0); "cacheable duration zero")]
    #[test_case(Duration::from_secs(60); "cacheable duration positive")]
    #[tokio::test]
    async fn test_idempotent_command_with_cacheable_duration(cacheable_duration: Duration) {
        let session = create_session();
        let managed_client = session.create_managed_client();
        let executor_options = CommandExecutorOptionsBuilder::default()
            .request_topic_pattern("test/{commandName}/request")
            .command_name("test_command_name")
            .cacheable_duration(cacheable_duration)
            .is_idempotent(true)
            .build()
            .unwrap();

        let command_executor =
            CommandExecutor::<MockPayload, MockPayload, _>::new(managed_client, executor_options);
        assert!(command_executor.is_ok());
    }

    #[tokio::test]
    async fn test_non_idempotent_command_with_positive_cacheable_duration() {
        let session = create_session();
        let managed_client = session.create_managed_client();
        let executor_options = CommandExecutorOptionsBuilder::default()
            .request_topic_pattern("test/{commandName}/{executorId}/request")
            .command_name("test_command_name")
            .cacheable_duration(Duration::from_secs(10))
            .build()
            .unwrap();

        let command_executor: Result<
            CommandExecutor<MockPayload, MockPayload, _>,
            AIOProtocolError,
        > = CommandExecutor::new(managed_client, executor_options);

        match command_executor {
            Err(e) => {
                assert_eq!(e.kind, AIOProtocolErrorKind::ConfigurationInvalid);
                assert!(!e.in_application);
                assert!(e.is_shallow);
                assert!(!e.is_remote);
                assert_eq!(e.http_status_code, None);
                assert_eq!(e.property_name, Some("is_idempotent".to_string()));
                assert!(e.property_value == Some(Value::Boolean(false)));
            }
            Ok(_) => {
                panic!("Expected error");
            }
        }
    }

    // CommandResponse tests

    #[test]
    fn test_response_serialization_error() {
        let mut mock_response_payload = MockPayload::new();
        mock_response_payload
            .expect_serialize()
            .returning(|| Err("dummy error".to_string()))
            .times(1);

        let mut binding = CommandResponseBuilder::default();
        let resp_builder = binding.payload(&mock_response_payload);
        assert!(resp_builder.is_err());
    }
}

// Test cases for subscribe
// Tests success:
//   start() is called and successfully receives suback
//   stop() is called and successfully receives unsuback
// Tests failure:
//   start() is called and receives a suback with a bad rc
//   stop() is called and receives an unsuback with a bad rc
//   start() is called and the subscribe call fails (invalid filter or failure on sending outbound sub async)
//   stop() is called and the unsubscribe call fails (invalid filter or failure on sending outbound unsub async)
//
// Test cases for recv request
// Tests success:
//   recv() is called and successfully sends a command request to the application
//   response topic, correlation data, invoker id, and payload are valid and successfully received
//   if payload format indicator, content type, and timestamp are present, they are validated successfully
//   if user properties are present, they don't start with reserved prefix
//
// Tests failure:
//   if an error response is published, the original request is acked
//   response topic is invalid and command response is not published and original request is acked
//   correlation data, invoker id, or payload are missing and error response is published and original request is acked
//   if payload format indicator, content type, and timestamp are present and invalid, error response is published and original request is acked
//   if user properties are present and start with reserved prefix, error response is published and original request is acked
//
// Test cases for response processing
// Tests success:
//    a command response is received and successfully published, the original request is acked
//    response user properties do not start with reserved prefix
//    response payload is serialized and published
//    an empty response payload has a status code of NoContent
//
// Tests failure:
//    an error occurs while processing the command response, an error response is sent and the original request is acked
//    response user properties start with reserved prefix, an error response is sent and the original request is acked
//    response payload is not serialized and an error response is sent and the original request is acked
//
// Test cases for timeout
// Tests success:
//   a command request is received and a response is published before the command expiration time, the original request is acked
//   a command request is received and a response is not published after the command expiration time, the original request is acked
// Tests failure:
//   a command request is received and the command expiration time cannot be calculated, an error response is sent to the invoker and executor application and the original request is acked
