// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

use std::{collections::HashMap, marker::PhantomData, str::FromStr, sync::Arc, time::Duration};

use azure_iot_operations_mqtt::control_packet::{Publish, PublishProperties, QoS};
use azure_iot_operations_mqtt::interface::{MqttAck, MqttProvider, MqttPubReceiver, MqttPubSub};
use bytes::Bytes;
use tokio::{
    sync::{
        broadcast::{error::RecvError, Sender},
        Mutex,
    },
    task, time,
};
use tokio_util::sync::CancellationToken;
use uuid::Uuid;

use super::StatusCode;
use crate::common::{
    aio_protocol_error::{AIOProtocolError, AIOProtocolErrorKind, Value},
    hybrid_logical_clock::HybridLogicalClock,
    is_invalid_utf8,
    payload_serialize::{FormatIndicator, PayloadSerialize},
    topic_processor::{self, contains_invalid_char, TopicPattern},
    user_properties::{self, validate_user_properties, UserProperty},
};

/// Command Request struct.
/// Used by the [`CommandInvoker`]
#[derive(Builder, Clone, Debug)]
#[builder(setter(into), build_fn(validate = "Self::validate"))]
pub struct CommandRequest<TReq>
where
    TReq: PayloadSerialize,
{
    /// Payload of the command request.
    #[builder(setter(custom))]
    payload: Vec<u8>,
    /// Strongly link `CommandRequest` with type `TReq`
    #[builder(private)]
    request_payload_type: PhantomData<TReq>,
    /// User data that will be set as custom MQTT User Properties on the Request message.
    /// Can be used to pass additional metadata to the executor.
    /// Default is an empty vector.
    #[builder(default)]
    custom_user_data: Vec<(String, String)>,
    /// Optional Fencing Token of the command request.
    #[builder(default = "None")]
    fencing_token: Option<HybridLogicalClock>,
    /// Executor ID to use if required in the request topic
    #[builder(default = "None")]
    executor_id: Option<String>,
    /// Timeout for the command. Will also be used as the `message_expiry_interval` to give the executor information on when the invoke request might expire.
    timeout: Duration,
}

impl<TReq: PayloadSerialize> CommandRequestBuilder<TReq> {
    /// Add a payload to the command request. Validates successful serialization of the payload.
    ///
    /// # Errors
    /// Returns a [`PayloadSerialize::Error`] if serialization of the payload fails
    pub fn payload(&mut self, payload: &TReq) -> Result<&mut Self, TReq::Error> {
        let serialized_payload = payload.serialize()?;
        self.payload = Some(serialized_payload);
        self.request_payload_type = Some(PhantomData);
        Ok(self)
    }

    /// Validate the command request.
    ///
    /// # Errors
    /// Returns a `String` describing the error if
    ///     - any of `custom_user_data`'s keys start with the [`RESERVED_PREFIX`](user_properties::RESERVED_PREFIX)
    ///     - any of `custom_user_data`'s keys or values are invalid utf-8
    ///     - timeout is < 1 ms or > `u32::max`
    fn validate(&self) -> Result<(), String> {
        if let Some(custom_user_data) = &self.custom_user_data {
            return validate_user_properties(custom_user_data);
        }
        if let Some(timeout) = &self.timeout {
            if timeout.as_millis() < 1 {
                return Err("Timeout must be at least 1 ms".to_string());
            }
            match <u64 as TryInto<u32>>::try_into(timeout.as_secs()) {
                Ok(_) => {}
                Err(_) => {
                    return Err("Timeout in seconds must be less than or equal to u32::max to be used as message_expiry_interval".to_string());
                }
            }
        }
        Ok(())
    }
}

/// Command Response struct.
/// Used by the [`CommandInvoker`]
#[derive(Debug)]
pub struct CommandResponse<TResp>
where
    TResp: PayloadSerialize,
{
    /// Payload of the command response. Must implement [`PayloadSerialize`].
    pub payload: TResp,
    /// User data that will be set as custom MQTT User Properties on the Response message.
    pub custom_user_data: Vec<(String, String)>,
    /// Timestamp of the command response.
    pub timestamp: Option<HybridLogicalClock>,
}

/// Command Invoker Options struct
#[derive(Builder, Clone)]
#[builder(setter(into))]
pub struct CommandInvokerOptions {
    /// Topic pattern for the command request
    /// Must align with [topic-structure.md](https://github.com/microsoft/mqtt-patterns/blob/main/docs/specs/topic-structure.md)
    request_topic_pattern: String,
    /// Topic pattern for the command response
    /// Must align with [topic-structure.md](https://github.com/microsoft/mqtt-patterns/blob/main/docs/specs/topic-structure.md)
    #[builder(default = "None")]
    response_topic_pattern: Option<String>,
    /// Command name
    command_name: String,
    /// Model ID if required by the topic patterns
    #[builder(default = "None")]
    model_id: Option<String>,
    /// Optional Topic namespace to be prepended to the topic patterns
    #[builder(default = "None")]
    topic_namespace: Option<String>,
    /// Custom topic token keys/values to be replaced in the topic patterns
    #[builder(default)]
    custom_topic_token_map: HashMap<String, String>,
    /// Prefix for the response topic
    #[builder(default = "Some(\"clients/{invokerClientId}\".to_string())")]
    response_topic_prefix: Option<String>,
    /// Suffix for the response topic
    #[builder(default = "None")]
    response_topic_suffix: Option<String>,
}

/// Command Invoker struct
/// # Example
/// ```
/// # use std::{collections::HashMap, time::Duration};
/// # use tokio_test::block_on;
/// # use azure_iot_operations_mqtt::MqttConnectionSettingsBuilder;
/// # use azure_iot_operations_mqtt::session::{Session, SessionOptionsBuilder};
/// # use azure_iot_operations_protocol::rpc::command_invoker::{CommandInvoker, CommandInvokerOptionsBuilder, CommandRequestBuilder, CommandResponse};
/// # use azure_iot_operations_protocol::common::payload_serialize::{PayloadSerialize, SerializerError, FormatIndicator};
/// # #[derive(Clone, Debug)]
/// # pub struct SamplePayload { }
/// # impl PayloadSerialize for SamplePayload {
/// #   fn content_type() -> &'static str { "application/json" }
/// #   fn format_indicator() -> FormatIndicator { FormatIndicator::Utf8EncodedCharacterData }
/// #   fn serialize(&self) -> Result<Vec<u8>, SerializerError> { Ok(Vec::new()) }
/// #   fn deserialize(payload: &[u8]) -> Result<Self, SerializerError> { Ok(SamplePayload {}) }
/// # }
/// # let mut connection_settings = MqttConnectionSettingsBuilder::default()
/// #     .client_id("test_client")
/// #     .host_name("mqtt://localhost")
/// #     .tcp_port(1883u16)
/// #     .build().unwrap();
/// # let mut session_options = SessionOptionsBuilder::default()
/// #     .connection_settings(connection_settings)
/// #     .build().unwrap();
/// # let mut mqtt_session = Session::new(session_options).unwrap();
/// let invoker_options = CommandInvokerOptionsBuilder::default()
///   .request_topic_pattern("test/request")
///   .response_topic_pattern("test/response".to_string())
///   .command_name("test_command")
///   .model_id("test_model".to_string())
///   .topic_namespace("test_namespace".to_string())
///   .custom_topic_token_map(HashMap::new())
///   .response_topic_prefix("custom/{invokerClientId}".to_string())
///   .build().unwrap();
/// # tokio_test::block_on(async {
/// let command_invoker: CommandInvoker<SamplePayload, SamplePayload, _> = CommandInvoker::new(&mut mqtt_session, invoker_options).unwrap();
/// let request = CommandRequestBuilder::default()
///   .payload(&SamplePayload {}).unwrap()
///   .timeout(Duration::from_secs(2))
///   .executor_id(Some("test_executor".to_string()))
///   .build().unwrap();
/// let result = command_invoker.invoke(request);
/// //let response: CommandResponse<SamplePayload> = result.await.unwrap();
/// # })
/// ```
#[allow(unused)] // TODO: remove once drop is implemented
pub struct CommandInvoker<TReq, TResp, PS>
where
    TReq: PayloadSerialize,
    TResp: PayloadSerialize,
    PS: MqttPubSub + Clone + Send + Sync,
{
    // static properties of the invoker
    mqtt_pub_sub: PS,
    command_name: String,
    client_id: String,
    request_topic_pattern: TopicPattern,
    response_topic_pattern: TopicPattern,
    request_payload_type: PhantomData<TReq>,
    response_payload_type: PhantomData<TResp>,
    // Describes state
    is_subscribed_mutex: Arc<Mutex<bool>>,
    // Used to send information to manage state
    recv_cancellation_token: CancellationToken,
    response_tx: Sender<Publish>,
}

/// Implementation of Command Invoker.
impl<TReq, TResp, PS> CommandInvoker<TReq, TResp, PS>
where
    TReq: PayloadSerialize,
    TResp: PayloadSerialize,
    PS: MqttPubSub + Clone + Send + Sync,
{
    /// Creates a new [`CommandInvoker`].
    ///
    /// Returns Ok([`CommandInvoker`]) on success, otherwise returns [`AIOProtocolError`].
    /// # Errors
    /// [`AIOProtocolError`] of kind [`ConfigurationInvalid`](AIOProtocolErrorKind::ConfigurationInvalid)
    /// - [`command_name`](CommandInvokerOptions::command_name) is empty, whitespace or invalid
    /// - [`request_topic_pattern`](CommandInvokerOptions::request_topic_pattern) is empty or whitespace
    /// - [`response_topic_pattern`](CommandInvokerOptions::response_topic_pattern) is Some and empty or whitespace
    ///     - [`response_topic_pattern`](CommandInvokerOptions::response_topic_pattern) is None and
    ///         [`response_topic_prefix`](CommandInvokerOptions::response_topic_prefix) or
    ///         [`response_topic_suffix`](CommandInvokerOptions::response_topic_suffix) are Some and empty or whitespace
    /// - [`request_topic_pattern`](CommandInvokerOptions::request_topic_pattern),
    ///     [`response_topic_pattern`](CommandInvokerOptions::response_topic_pattern),
    ///     [`topic_namespace`](CommandInvokerOptions::topic_namespace),
    ///     [`response_topic_prefix`](CommandInvokerOptions::response_topic_prefix),
    ///     [`response_topic_suffix`](CommandInvokerOptions::response_topic_suffix),
    ///     or [`model_id`](CommandInvokerOptions::model_id)
    ///     are Some and invalid or contain a token with no valid replacement
    /// - [`custom_topic_token_map`](CommandInvokerOptions::custom_topic_token_map) isn't empty and contains invalid key(s)/token(s)
    pub fn new<PR: MqttPubReceiver + MqttAck + Send + Sync + 'static>(
        mqtt_provider: &mut impl MqttProvider<PS, PR>,
        invoker_options: CommandInvokerOptions,
    ) -> Result<Self, AIOProtocolError> {
        // Validate function parameters. request_topic_pattern will be validated by topic parser
        if invoker_options.command_name.is_empty()
            || contains_invalid_char(&invoker_options.command_name)
        {
            return Err(AIOProtocolError::new_configuration_invalid_error(
                None,
                "command_name",
                Value::String(invoker_options.command_name.clone()),
                None,
                Some(invoker_options.command_name),
            ));
        }

        // If no response_topic_pattern is specified, generate one based on the request_topic_pattern, response_topic_prefix, and response_topic_suffix
        let mut response_topic_pattern;
        if let Some(pattern) = invoker_options.response_topic_pattern {
            response_topic_pattern = pattern;
        } else {
            response_topic_pattern = invoker_options.request_topic_pattern.clone();
            if let Some(prefix) = invoker_options.response_topic_prefix {
                // Validity check will be done within the topic processor
                response_topic_pattern = prefix + "/" + &response_topic_pattern;
            }
            if let Some(suffix) = invoker_options.response_topic_suffix {
                // Validity check will be done within the topic processor
                response_topic_pattern = response_topic_pattern + "/" + &suffix;
            }
        }
        // Generate the request and response topics
        let request_topic_pattern = TopicPattern::new_command_pattern(
            &invoker_options.request_topic_pattern,
            &invoker_options.command_name,
            topic_processor::WILDCARD,
            mqtt_provider.client_id(),
            invoker_options.model_id.as_deref(),
            invoker_options.topic_namespace.as_deref(),
            &invoker_options.custom_topic_token_map,
        )?;

        let response_topic_pattern = TopicPattern::new_command_pattern(
            &response_topic_pattern,
            &invoker_options.command_name,
            topic_processor::WILDCARD,
            mqtt_provider.client_id(),
            invoker_options.model_id.as_deref(),
            invoker_options.topic_namespace.as_deref(),
            &invoker_options.custom_topic_token_map,
        )?;

        // Create mutex to track subscription state
        let is_subscribed_mutex = Arc::new(Mutex::new(false));

        // Get the pub sub and receiver from the session
        let mqtt_pub_sub = mqtt_provider.pub_sub();
        let mqtt_receiver = match mqtt_provider
            .filtered_pub_receiver(&response_topic_pattern.as_subscribe_topic(), false)
        {
            Ok(receiver) => receiver,
            Err(e) => {
                return Err(AIOProtocolError::new_configuration_invalid_error(
                    Some(Box::new(e)),
                    "response_topic_pattern",
                    Value::String(response_topic_pattern.as_subscribe_topic()),
                    Some("Could not parse response topic pattern".to_string()),
                    Some(invoker_options.command_name),
                ));
            }
        };

        // Create the channel to send responses on
        let response_tx = Sender::new(5);

        // Create the cancellation token for the receiver loop
        let recv_cancellation_token = CancellationToken::new();

        // Start the receive response loop
        task::spawn({
            let response_tx_clone = response_tx.clone();
            let recv_cancellation_token_clone = recv_cancellation_token.clone();
            let command_name_clone = invoker_options.command_name.clone();
            async move {
                Self::receive_response_loop(
                    mqtt_receiver,
                    response_tx_clone,
                    recv_cancellation_token_clone,
                    command_name_clone,
                )
                .await;
            }
        });

        Ok(Self {
            mqtt_pub_sub,
            command_name: invoker_options.command_name,
            client_id: mqtt_provider.client_id().to_string(),
            request_topic_pattern,
            response_topic_pattern,
            request_payload_type: PhantomData,
            response_payload_type: PhantomData,
            is_subscribed_mutex,
            recv_cancellation_token,
            response_tx,
        })
    }

    /// Invokes a command.
    ///
    /// Returns Ok([`CommandResponse`]) on success, otherwise returns [`AIOProtocolError`].
    /// # Arguments
    /// * `request` - [`CommandRequest`] to invoke
    /// # Errors
    ///
    /// [`AIOProtocolError`] of kind [`ConfigurationInvalid`](AIOProtocolErrorKind::ConfigurationInvalid) if
    /// - `executor_id` is Some and invalid
    ///
    /// [`AIOProtocolError`] of kind [`PayloadInvalid`](AIOProtocolErrorKind::PayloadInvalid) if
    /// - [`request_payload`][CommandRequest::payload]'s content type isn't valid utf-8
    /// - [`response_payload`][CommandResponse::payload] deserialization fails
    /// - The response has a [`UserProperty::Status`] of [`StatusCode::NoContent`] but the payload isn't empty
    /// - The response has a [`UserProperty::Status`] of [`StatusCode::BadRequest`] and there is no [`UserProperty::InvalidPropertyName`] or [`UserProperty::InvalidPropertyValue`] specified
    ///
    /// [`AIOProtocolError`] of kind [`Timeout`](AIOProtocolErrorKind::Timeout) if
    /// - Command invoke timed out
    /// - The response has a [`UserProperty::Status`] of [`StatusCode::RequestTimeout`]
    ///
    /// [`AIOProtocolError`] of kind [`ClientError`](AIOProtocolErrorKind::ClientError) if
    /// - The subscribe fails
    /// - The suback reason code doesn't indicate success.
    /// - The publish fails
    /// - The puback reason code doesn't indicate success.
    ///
    /// [`AIOProtocolError`] of kind [`Cancellation`](AIOProtocolErrorKind::Cancellation) if the [`CommandInvoker`] has been dropped
    ///
    /// [`AIOProtocolError`] of kind [`HeaderInvalid`](AIOProtocolErrorKind::HeaderInvalid) if
    /// - The response's `content_type` doesn't match the `content_type` of [`TResp`](PayloadSerialize)
    /// - The response's `format_indicator` is `Utf8EncodedCharacterData` and doesn't match the `format_indicator` of [`TResp`](PayloadSerialize)
    /// - The response has a [`UserProperty::Timestamp`] that is malformed
    /// - The response has a [`UserProperty::Status`] that can't be parsed as an integer
    /// - The response has a [`UserProperty::Status`] of [`StatusCode::BadRequest`] and a [`UserProperty::InvalidPropertyValue`] is specified
    /// - The response has a [`UserProperty::Status`] of [`StatusCode::UnsupportedMediaType`]
    ///
    /// [`AIOProtocolError`] of kind [`HeaderMissing`](AIOProtocolErrorKind::HeaderMissing) if
    /// - The response has a [`UserProperty::Status`] of [`StatusCode::BadRequest`] and [`UserProperty::InvalidPropertyName`] is specified, but [`UserProperty::InvalidPropertyValue`] isn't specified
    /// - The response doesn't specify a [`UserProperty::Status`]
    ///
    /// [`AIOProtocolError`] of kind [`UnknownError`](AIOProtocolErrorKind::UnknownError) if
    /// - The response has a [`UserProperty::Status`] that isn't one of [`StatusCode`]
    /// - The response has a [`UserProperty::Status`] of [`StatusCode::InternalServerError`], the [`UserProperty::IsApplicationError`] is false, and a [`UserProperty::InvalidPropertyName`] isn't provided
    ///
    /// [`AIOProtocolError`] of kind [`InvocationException`](AIOProtocolErrorKind::InvocationException) if the response has a [`UserProperty::Status`] of [`StatusCode::UnprocessableContent`]
    ///
    /// [`AIOProtocolError`] of kind [`ExecutionException`](AIOProtocolErrorKind::ExecutionException) if the response has a [`UserProperty::Status`] of [`StatusCode::InternalServerError`] and the [`UserProperty::IsApplicationError`] is true
    ///
    /// [`AIOProtocolError`] of kind [`InternalLogicError`](AIOProtocolErrorKind::InternalLogicError) if the response has a [`UserProperty::Status`] of [`StatusCode::InternalServerError`], the [`UserProperty::IsApplicationError`] is false, and a [`UserProperty::InvalidPropertyName`] is provided
    ///
    /// [`AIOProtocolError`] of kind [`StateInvalid`](AIOProtocolErrorKind::StateInvalid) if the response has a [`UserProperty::Status`] of [`StatusCode::ServiceUnavailable`]
    pub async fn invoke(
        &self,
        request: CommandRequest<TReq>,
    ) -> Result<CommandResponse<TResp>, AIOProtocolError> {
        // Get the timeout duration to use
        let command_timeout = request.timeout;

        // Call invoke, wrapped within a timeout
        let invoke_result = time::timeout(request.timeout, self.invoke_internal(request)).await;

        // Return the timeout error or the result from the command invocation.
        match invoke_result {
            Ok(result) => match result {
                Ok(response) => Ok(response),
                Err(e) => Err(e),
            },
            Err(e) => {
                log::error!(
                    "[{command_name}] Command invoke timed out after {command_timeout:?}",
                    command_name = self.command_name,
                );
                Err(AIOProtocolError::new_timeout_error(
                    false,
                    Some(Box::new(e)),
                    None,
                    &self.command_name,
                    command_timeout,
                    None,
                    Some(self.command_name.clone()),
                ))
            }
        }
    }

    /// Subscribes to the response topic filter.
    ///
    /// Returns `Ok()` on success, otherwise returns [`AIOProtocolError`].
    /// # Errors
    /// [`AIOProtocolError`] of kind [`ClientError`](AIOProtocolErrorKind::ClientError) if the subscribe fails or if the suback reason code doesn't indicate success.
    async fn subscribe_to_response_filter(&self) -> Result<(), AIOProtocolError> {
        let response_subscribe_topic = self.response_topic_pattern.as_subscribe_topic();
        // Send subscribe
        let subscribe_result = self
            .mqtt_pub_sub
            .subscribe(response_subscribe_topic, QoS::AtLeastOnce)
            .await;
        match subscribe_result {
            Ok(suback) => {
                // Wait for suback
                match suback.wait().await {
                    Ok(()) => { /* Success */ }
                    Err(e) => {
                        log::error!("[ERROR] suback error: {e}");
                        return Err(AIOProtocolError::new_mqtt_error(
                            Some("MQTT Error on command invoker suback".to_string()),
                            Box::new(e),
                            Some(self.command_name.clone()),
                        ));
                    }
                }
            }
            Err(e) => {
                log::error!("[ERROR] subscribe error: {e}");
                return Err(AIOProtocolError::new_mqtt_error(
                    Some("MQTT Error on command invoker subscribe".to_string()),
                    Box::new(e),
                    Some(self.command_name.clone()),
                ));
            }
        }
        Ok(())
    }

    async fn invoke_internal(
        &self,
        mut request: CommandRequest<TReq>,
    ) -> Result<CommandResponse<TResp>, AIOProtocolError> {
        // Validate parameters. Custom user data, timeout, and payload serialization have already been validated in CommandRequestBuilder
        // Validate message expiry interval
        let message_expiry_interval: u32 = match request.timeout.as_secs().try_into() {
            Ok(val) => val,
            Err(_) => {
                // should be validated in CommandRequestBuilder
                unreachable!();
            }
        };

        // Get request topic. Validates executor_id
        let request_topic = self
            .request_topic_pattern
            .as_publish_topic(request.executor_id.as_deref())?;
        // Get response topic. Validates executor_id
        let response_topic = self
            .response_topic_pattern
            .as_publish_topic(request.executor_id.as_deref())?;
        // Get and validate content_type
        let content_type = TReq::content_type();
        if is_invalid_utf8(content_type) {
            return Err(AIOProtocolError::new_payload_invalid_error(
                true,
                false,
                None,
                None,
                Some(format!(
                    "The payload's content type '{content_type}' isn't valid utf-8"
                )),
                Some(self.command_name.clone()),
            ));
        }

        // Create correlation id
        let correlation_id = Uuid::new_v4();
        let correlation_data = Bytes::from(correlation_id.as_bytes().to_vec());

        // Add internal user properties
        request.custom_user_data.push((
            UserProperty::CommandInvokerId.to_string(),
            self.client_id.clone(),
        ));
        request.custom_user_data.push((
            UserProperty::Timestamp.to_string(),
            HybridLogicalClock::new().to_string(),
        ));
        if let Some(fencing_token) = request.fencing_token {
            request.custom_user_data.push((
                UserProperty::FencingToken.to_string(),
                fencing_token.to_string(),
            ));
        }

        // Create MQTT Properties
        let publish_properties = PublishProperties {
            correlation_data: Some(correlation_data.clone()),
            response_topic: Some(response_topic),
            payload_format_indicator: Some(TReq::format_indicator() as u8),
            content_type: Some(content_type.to_string()),
            message_expiry_interval: Some(message_expiry_interval),
            user_properties: request.custom_user_data,
            topic_alias: None,
            subscription_identifiers: Vec::new(),
        };

        // Subscribe to the response topic if we're not already subscribed
        {
            let mut is_subscribed = self.is_subscribed_mutex.lock().await;
            if !*is_subscribed {
                self.subscribe_to_response_filter().await?;
                *is_subscribed = true;
            }
            // Allow other concurrent invoke commands to acquire the is_subscribed lock
        }

        // Create receiver for response
        let mut response_rx = self.response_tx.subscribe();

        // Send publish
        let publish_result = self
            .mqtt_pub_sub
            .publish_with_properties(
                request_topic,
                QoS::AtLeastOnce,
                false,
                request.payload,
                publish_properties,
            )
            .await;

        match publish_result {
            Ok(publish_completion_token) => {
                // Wait for and handle the puback
                match publish_completion_token.wait().await {
                    // if puback is Ok, continue and wait for the response
                    Ok(()) => {}
                    Err(e) => {
                        log::error!("[ERROR] puback error: {e}");
                        return Err(AIOProtocolError::new_mqtt_error(
                            Some("MQTT Error on command invoke puback".to_string()),
                            Box::new(e),
                            Some(self.command_name.clone()),
                        ));
                    }
                }
            }
            Err(e) => {
                log::error!("[ERROR] publish error: {e}");
                return Err(AIOProtocolError::new_mqtt_error(
                    Some("MQTT Error on command invoke publish".to_string()),
                    Box::new(e),
                    Some(self.command_name.clone()),
                ));
            }
        }

        // Wait for a response where the correlation id matches
        loop {
            // wait for incoming pub
            match response_rx.recv().await {
                Ok(rsp_pub) => {
                    // check correlation id for match, otherwise loop again
                    if let Some(ref rsp_properties) = rsp_pub.properties {
                        if let Some(ref response_correlation_data) = rsp_properties.correlation_data
                        {
                            if *response_correlation_data == correlation_data {
                                // This is implicit validation of the correlation data - if it's malformed it won't match the request
                                // This is the response for this request, validate and parse it and send it back to the application
                                return validate_and_parse_response(
                                    self.command_name.clone(),
                                    &rsp_pub.payload,
                                    rsp_properties.clone(),
                                );
                            }
                        }
                    }
                    // If the publish doesn't have properties, correlation_data, or the correlation data doesn't match, keep waiting for the next one
                }
                Err(RecvError::Lagged(e)) => {
                    log::error!("[ERROR] Invoker response receiver lagged. Response may not be received: {e}");
                    // Keep waiting for response even though it may have gotten overwritten.
                    continue;
                }
                Err(RecvError::Closed) => {
                    log::error!("[ERROR] MQTT Receiver has been cleaned up and will no longer send a response");
                    return Err(AIOProtocolError::new_cancellation_error(
                        false,
                        None,
                        None,
                        Some(
                            "MQTT Receiver has been cleaned up and will no longer send a response"
                                .to_string(),
                        ),
                        Some(self.command_name.clone()),
                    ));
                }
            };
        }
    }

    async fn receive_response_loop<PR: MqttPubReceiver + MqttAck + Send + Sync + 'static>(
        mut mqtt_receiver: PR,
        response_tx: Sender<Publish>,
        recv_cancellation_token: CancellationToken,
        command_name: String,
    ) {
        loop {
            tokio::select! {
                  // on drop, this cancellation token will be called so this loop can exit
                  () = recv_cancellation_token.cancelled() => {
                    log::info!("[{command_name}] Receive response loop cancelled");
                    break;
                  },
                  msg = mqtt_receiver.recv() => {
                    if let Some(m) = msg {
                        // Send to pending command listeners
                        match response_tx.send(m.clone()) {
                            Ok(_) => { },
                            Err(e) => {
                                log::debug!("[{command_name}] Message ignored, no pending commands: {e}, {:?}", m.topic);
                            }
                        }
                        // Manually ack
                        match mqtt_receiver.ack(&m).await {
                            Ok(()) => { },
                            Err(e) => {
                                log::error!("[{command_name}] Error acking message: {e}");
                            }
                        }
                    } else {
                        log::error!("[{command_name}] MqttReceiver closed");
                        break;
                    }
                }
            }
        }
    }
}

fn validate_and_parse_response<TResp: PayloadSerialize>(
    command_name: String,
    response_payload: &Bytes,
    response_properties: PublishProperties,
) -> Result<CommandResponse<TResp>, AIOProtocolError> {
    // validate headers
    // content type matches serializer content type. Content type being None isn't an error
    if let Some(content_type) = response_properties.content_type {
        if content_type != TResp::content_type() {
            return Err(AIOProtocolError::new_header_invalid_error(
                "Content Type",
                &content_type,
                false,
                None,
                Some(format!(
                    "Content type '{}' is not supported by this implementation; only '{}' is accepted.",
                    content_type,
                    TResp::content_type()
                )),
                Some(command_name)
            ));
        }
    }

    // If format indicator is Utf8EncodedCharacterData, it matches the response type format indicator.
    // If it's UnspecifiedBytes, it doesn't matter whether it matches or not
    if let Some(format_indicator) = response_properties.payload_format_indicator {
        if format_indicator != FormatIndicator::UnspecifiedBytes as u8
            && format_indicator != TResp::format_indicator() as u8
        {
            return Err(AIOProtocolError::new_header_invalid_error(
                "Payload Format Indicator",
                &format_indicator.to_string(),
                false,
                None,
                Some(format!(
                    "Format indicator '{}' is not appropriate for '{}' content.",
                    format_indicator,
                    TResp::content_type()
                )),
                Some(command_name),
            ));
        }
    }

    // Create a default response error so we can update details as we parse
    let mut response_error = AIOProtocolError {
        kind: AIOProtocolErrorKind::UnknownError, // should be overwritten
        message: None,                            // should be overwritten
        in_application: false,                    // for most scenarios, this will be false
        is_shallow: false,                        // this is always false here
        is_remote: true,                          // this should be overwritten as needed
        nested_error: None,
        http_status_code: None,
        header_name: None,
        header_value: None,
        timeout_name: None,
        timeout_value: None,
        property_name: None,
        property_value: None,
        command_name: Some(command_name.clone()), // correct for all errors here
    };

    // parse user properties
    let mut response_custom_user_data = vec![];

    let mut status: Option<StatusCode> = None;
    let mut invalid_property_name: Option<String> = None;
    let mut invalid_property_value: Option<String> = None;

    let mut unknown_status_error: Option<AIOProtocolError> = None;

    let mut timestamp = None;
    for (key, value) in response_properties.user_properties {
        match UserProperty::from_str(&key) {
            Ok(UserProperty::Timestamp) => {
                match HybridLogicalClock::from_str(&value) {
                    Ok(ts) => {
                        timestamp = Some(ts);
                    }
                    Err(mut e) => {
                        // update error to include more specific header name
                        e.header_name = Some(key);
                        e.command_name = Some(command_name);
                        return Err(e);
                    }
                }
            }
            Ok(UserProperty::Status) => {
                // validate that status is one of valid values. Must be present
                match StatusCode::from_str(&value) {
                    Ok(code) => {
                        response_error.http_status_code = Some(code as u16);
                        status = Some(code);
                    }
                    Err(mut e) => {
                        e.command_name = Some(command_name.clone());
                        if e.kind == AIOProtocolErrorKind::UnknownError {
                            // if the error is that the status code isn't recognized, we want to include the status message before returning it to the application
                            unknown_status_error = Some(e);
                        } else {
                            // any other parsing errors can be returned immediately
                            return Err(e);
                        }
                    }
                }
            }
            Ok(UserProperty::StatusMessage) => {
                // Nothing to validate, but save info
                response_error.message = Some(value);
            }
            Ok(UserProperty::IsApplicationError) => {
                // Nothing to validate, but save info
                // IsApplicationError is interpreted as false if the property is omitted, or has no value, or has a value that case-insensitively equals "false". Otherwise, the property is interpreted as true.
                response_error.in_application = value.eq_ignore_ascii_case("true");
            }
            Ok(UserProperty::InvalidPropertyName) => {
                // Nothing to validate, but save info
                invalid_property_name = Some(value);
            }
            Ok(UserProperty::InvalidPropertyValue) => {
                // Nothing to validate, but save info
                invalid_property_value = Some(value);
            }
            Ok(_) => {
                // UserProperty::FencingToken or UserProperty::CommandInvokerId
                // Don't return error, although these properties shouldn't be present on a response
                log::error!(
                    "Response should not contain MQTT user property '{}'. Value is '{}'",
                    key,
                    value
                );
            }
            Err(()) => {
                if key.starts_with(user_properties::RESERVED_PREFIX) {
                    // Don't return error, although these properties shouldn't be present on a response
                    log::error!("Invalid response user data property '{}' starts with reserved prefix '{}'. Value is '{}'", key, user_properties::RESERVED_PREFIX, value);
                } else {
                    response_custom_user_data.push((key, value));
                }
            }
        }
    }

    // If status isn't one of the `StatusCode` enums, return an error
    if let Some(mut e) = unknown_status_error {
        if let Some(m) = response_error.message {
            e.message = Some(m);
        }
        return Err(e);
    }

    'block: {
        // status is present
        if let Some(status_code) = status {
            // if status code isn't ok or no content, form `AIOProtocolError` about response
            match status_code {
                StatusCode::Ok => {
                    // Continue to form success CommandResponse
                    break 'block;
                }
                StatusCode::NoContent => {
                    // If status code is no content, an error will be returned if there is content
                    if response_payload.is_empty() {
                        // continue to form success CommandResponse
                        break 'block;
                    }
                    // If there is content, return an error
                    response_error.kind = AIOProtocolErrorKind::PayloadInvalid;
                    response_error.is_remote = false;
                    response_error.message =
                        Some("Status code 204 (No Content) should not have a payload".to_string());
                }
                StatusCode::BadRequest => {
                    if let Some(property_value) = invalid_property_value {
                        response_error.kind = AIOProtocolErrorKind::HeaderInvalid;
                        response_error.header_name = invalid_property_name;
                        response_error.header_value = Some(property_value);
                    } else if let Some(property_name) = invalid_property_name {
                        response_error.kind = AIOProtocolErrorKind::HeaderMissing;
                        response_error.header_name = Some(property_name);
                    } else {
                        response_error.kind = AIOProtocolErrorKind::PayloadInvalid;
                    }
                }
                StatusCode::RequestTimeout => {
                    response_error.kind = AIOProtocolErrorKind::Timeout;
                    response_error.timeout_name = invalid_property_name;
                    response_error.timeout_value =
                        invalid_property_value.and_then(|timeout| match timeout.parse::<u64>() {
                            Ok(val) => Some(Duration::from_secs(val)),
                            Err(_) => None,
                        });
                }
                StatusCode::UnsupportedMediaType => {
                    response_error.kind = AIOProtocolErrorKind::HeaderInvalid;
                    response_error.header_name = invalid_property_name;
                    response_error.header_value = invalid_property_value;
                }
                StatusCode::UnprocessableContent => {
                    response_error.kind = AIOProtocolErrorKind::InvocationException;
                    response_error.in_application = true;
                    response_error.property_name = invalid_property_name;
                    response_error.property_value = invalid_property_value.map(Value::String);
                }
                StatusCode::InternalServerError => {
                    response_error.property_value = invalid_property_value.map(Value::String);
                    response_error.property_name = invalid_property_name;
                    if response_error.in_application {
                        response_error.kind = AIOProtocolErrorKind::ExecutionException;
                    } else if response_error.property_name.is_some() {
                        response_error.kind = AIOProtocolErrorKind::InternalLogicError;
                    } else {
                        response_error.kind = AIOProtocolErrorKind::UnknownError;
                        // Should be None anyways, but clearing it just to be safe
                        response_error.property_value = None;
                    }
                }
                StatusCode::ServiceUnavailable => {
                    response_error.kind = AIOProtocolErrorKind::StateInvalid;
                    response_error.is_remote = false;
                    response_error.property_name = invalid_property_name;
                    response_error.property_value = invalid_property_value.map(Value::String);
                }
            }
            response_error.ensure_error_message();
            return Err(response_error);
        }
        // status is not present
        return Err(AIOProtocolError::new_header_missing_error(
            "Status",
            false,
            None,
            Some(format!(
                "Response missing MQTT user property '{}'",
                UserProperty::Status
            )),
            Some(command_name),
        ));
    }

    // response payload deserialization
    let deserialized_response_payload = match TResp::deserialize(response_payload) {
        Ok(payload) => payload,
        Err(e) => {
            return Err(AIOProtocolError::new_payload_invalid_error(
                false,
                false,
                Some(e.into()),
                None,
                None,
                Some(command_name),
            ));
        }
    };

    Ok(CommandResponse {
        payload: deserialized_response_payload,
        custom_user_data: response_custom_user_data,
        timestamp,
    })
}

impl<TReq, TResp, PS> Drop for CommandInvoker<TReq, TResp, PS>
where
    TReq: PayloadSerialize,
    TResp: PayloadSerialize,
    PS: MqttPubSub + Clone + Send + Sync,
{
    fn drop(&mut self) {}
}

#[cfg(test)]
mod tests {
    use std::sync::Mutex;

    use test_case::test_case;
    // TODO: This dependency on MqttConnectionSettingsBuilder should be removed in lieu of using a true mock
    use azure_iot_operations_mqtt::session::{Session, SessionOptionsBuilder};
    use azure_iot_operations_mqtt::MqttConnectionSettingsBuilder;

    use super::*;
    use crate::common::{
        aio_protocol_error::AIOProtocolErrorKind,
        payload_serialize::{FormatIndicator, MockPayload},
    };

    /// Mutex needed to check mock calls of static methods `PayloadSerialize::deserialize`, `PayloadSerialize::content_type`, and `PayloadSerialize::format_indicator`
    static DESERIALIZE_MTX: Mutex<()> = Mutex::new(());
    static CONTENT_TYPE_MTX: Mutex<()> = Mutex::new(());
    static FORMAT_INDICATOR_MTX: Mutex<()> = Mutex::new(());

    // TODO: This should return a mock MqttProvider instead
    fn get_mqtt_provider() -> Session {
        // TODO: Make a real mock that implements MqttProvider
        let connection_settings = MqttConnectionSettingsBuilder::default()
            .host_name("localhost")
            .client_id("test_client")
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
        let mut mqtt_provider = get_mqtt_provider();
        let invoker_options = CommandInvokerOptionsBuilder::default()
            .request_topic_pattern("test/{commandName}/{executorId}/request")
            .command_name("test_command_name")
            .build()
            .unwrap();

        let command_invoker: CommandInvoker<MockPayload, MockPayload, _> =
            CommandInvoker::new(&mut mqtt_provider, invoker_options).unwrap();
        assert!(command_invoker
            .request_topic_pattern
            .is_match("test/test_command_name/some_executor/request"));
        assert!(command_invoker
            .response_topic_pattern
            .is_match("clients/test_client/test/test_command_name/some_executor/request"));
        assert_eq!(
            command_invoker.response_topic_pattern.as_subscribe_topic(),
            "clients/test_client/test/test_command_name/+/request"
        );
    }

    #[tokio::test]
    async fn test_new_override_defaults() {
        let mut mqtt_provider = get_mqtt_provider();
        let invoker_options = CommandInvokerOptionsBuilder::default()
            .request_topic_pattern("test/{commandName}/{modelId}/{executorId}/request")
            .response_topic_pattern(
                "test/{commandName}/{modelId}/{executorId}/response".to_string(),
            )
            .command_name("test_command_name")
            .model_id("test_model_id".to_string())
            .topic_namespace("test_namespace".to_string())
            .custom_topic_token_map(HashMap::new())
            .response_topic_prefix("custom/{invokerClientId}".to_string())
            .response_topic_suffix("custom/response".to_string())
            .build()
            .unwrap();

        let command_invoker: CommandInvoker<MockPayload, MockPayload, _> =
            CommandInvoker::new(&mut mqtt_provider, invoker_options).unwrap();
        assert!(command_invoker
            .request_topic_pattern
            .is_match("test_namespace/test/test_command_name/test_model_id/some_executor/request"));
        // prefix and suffix should be ignored if response_topic_pattern is provided
        assert!(command_invoker.response_topic_pattern.is_match(
            "test_namespace/test/test_command_name/test_model_id/some_executor/response"
        ));
        assert_eq!(
            command_invoker.response_topic_pattern.as_subscribe_topic(),
            "test_namespace/test/test_command_name/test_model_id/+/response"
        );
    }

    #[test_case("command_name", ""; "new_empty_command_name")]
    #[test_case("command_name", " "; "new_whitespace_command_name")]
    #[test_case("request_topic_pattern", ""; "new_empty_request_topic_pattern")]
    #[test_case("request_topic_pattern", " "; "new_whitespace_request_topic_pattern")]
    #[test_case("response_topic_pattern", ""; "new_empty_response_topic_pattern")]
    #[test_case("response_topic_pattern", " "; "new_whitespace_response_topic_pattern")]
    #[test_case("response_topic_prefix", ""; "new_empty_response_topic_prefix")]
    #[test_case("response_topic_prefix", " "; "new_whitespace_response_topic_prefix")]
    #[test_case("response_topic_suffix", ""; "new_empty_response_topic_suffix")]
    #[test_case("response_topic_suffix", " "; "new_whitespace_response_topic_suffix")]
    #[tokio::test]
    async fn test_new_empty_args(property_name: &str, property_value: &str) {
        let mut mqtt_provider = get_mqtt_provider();

        let mut command_name = "test_command_name".to_string();
        let mut request_topic_pattern = "test/req/topic".to_string();
        let mut response_topic_pattern = None;
        let mut response_topic_prefix = "custom/prefix".to_string();
        let mut response_topic_suffix = "custom/suffix".to_string();

        let mut error_property_name = "pattern";
        let mut error_property_value = property_value.to_string();

        match property_name {
            "command_name" => {
                command_name = property_value.to_string();
                error_property_name = "command_name";
            }
            "request_topic_pattern" => request_topic_pattern = property_value.to_string(),
            "response_topic_pattern" => response_topic_pattern = Some(property_value.to_string()),
            "response_topic_prefix" => {
                response_topic_prefix = property_value.to_string();
                error_property_value.push_str("/test/req/topic/custom/suffix");
            }
            "response_topic_suffix" => {
                response_topic_suffix = property_value.to_string();
                error_property_value = "custom/prefix/test/req/topic/".to_string();
                error_property_value.push_str(&response_topic_suffix);
            }
            _ => panic!("Invalid error_property_name"),
        }

        let invoker_options = CommandInvokerOptionsBuilder::default()
            .request_topic_pattern(request_topic_pattern)
            .response_topic_pattern(response_topic_pattern)
            .response_topic_prefix(response_topic_prefix)
            .response_topic_suffix(response_topic_suffix)
            .command_name(command_name)
            .build()
            .unwrap();
        let command_invoker: Result<CommandInvoker<MockPayload, MockPayload, _>, AIOProtocolError> =
            CommandInvoker::new(&mut mqtt_provider, invoker_options);
        match command_invoker {
            Ok(_) => panic!("Expected error"),
            Err(e) => {
                assert_eq!(e.kind, AIOProtocolErrorKind::ConfigurationInvalid);
                assert!(!e.in_application);
                assert!(e.is_shallow);
                assert!(!e.is_remote);
                assert_eq!(e.http_status_code, None);
                assert_eq!(e.property_name, Some(error_property_name.to_string()));
                assert!(e.property_value == Some(Value::String(error_property_value.to_string())));
            }
        }
    }

    // Happy scenario tests for response topic prefix and suffix
    // For a null response topic, valid provided and null prefixes and suffixes all are okay
    #[test_case(Some("custom/prefix".to_string()), Some("custom/suffix".to_string()), "custom/prefix/test/req/topic/custom/suffix"; "new_response_topic_prefix_and_suffix")]
    #[test_case(None, Some("custom/suffix".to_string()), "test/req/topic/custom/suffix"; "new_none_response_topic_prefix")]
    #[test_case(Some("custom/prefix".to_string()), None, "custom/prefix/test/req/topic"; "new_none_response_topic_suffix")]
    #[test_case(None, None, "test/req/topic"; "new_none_response_topic_prefix_and_suffix")]
    #[tokio::test]
    async fn test_new_response_pattern_prefix_suffix_args(
        response_topic_prefix: Option<String>,
        response_topic_suffix: Option<String>,
        expected_response_topic_subscribe_pattern: &str,
    ) {
        let mut mqtt_provider = get_mqtt_provider();

        let command_name = "test_command_name".to_string();
        let request_topic_pattern = "test/req/topic".to_string();

        let invoker_options = CommandInvokerOptionsBuilder::default()
            .request_topic_pattern(request_topic_pattern)
            .command_name(command_name)
            .response_topic_prefix(response_topic_prefix)
            .response_topic_suffix(response_topic_suffix)
            .build()
            .unwrap();
        let command_invoker: Result<CommandInvoker<MockPayload, MockPayload, _>, AIOProtocolError> =
            CommandInvoker::new(&mut mqtt_provider, invoker_options);
        assert!(command_invoker.is_ok());
        assert_eq!(
            command_invoker
                .unwrap()
                .response_topic_pattern
                .as_subscribe_topic(),
            expected_response_topic_subscribe_pattern
        );
    }

    // If response pattern suffix is not specified, the default is used
    #[tokio::test]
    async fn test_new_response_pattern_default_prefix() {
        let mut mqtt_provider = get_mqtt_provider();

        let command_name = "test_command_name";
        let request_topic_pattern = "test/req/topic";

        let invoker_options = CommandInvokerOptionsBuilder::default()
            .request_topic_pattern(request_topic_pattern)
            .command_name(command_name)
            .build()
            .unwrap();
        let command_invoker: Result<CommandInvoker<MockPayload, MockPayload, _>, AIOProtocolError> =
            CommandInvoker::new(&mut mqtt_provider, invoker_options);
        assert!(command_invoker.is_ok());
        assert_eq!(
            command_invoker
                .unwrap()
                .response_topic_pattern
                .as_subscribe_topic(),
            "clients/test_client/test/req/topic"
        );
    }

    /// Tests success: Timeout specified on invoke and there is no error
    #[tokio::test]
    #[ignore] // test ignored because waiting for the suback hangs forever. Leaving the test for now until we have a full testing framework
    async fn test_invoke_timeout_parameter() {
        // Get mutexes for checking static PayloadSerialize calls
        let _deserialize_mutex = DESERIALIZE_MTX.lock();
        let _content_type_mutex = CONTENT_TYPE_MTX.lock();
        let _format_indicator_mutex = FORMAT_INDICATOR_MTX.lock();

        let mut mqtt_provider = get_mqtt_provider();
        let invoker_options = CommandInvokerOptionsBuilder::default()
            .request_topic_pattern("test/req/topic")
            .command_name("test_command_name")
            .build()
            .unwrap();

        let command_invoker: CommandInvoker<MockPayload, MockPayload, _> =
            CommandInvoker::new(&mut mqtt_provider, invoker_options).unwrap();

        let mut mock_request_payload = MockPayload::new();
        mock_request_payload
            .expect_serialize()
            .returning(|| Ok(String::new().into()))
            .times(1);

        // Mock context to track content_type calls
        let mock_payload_content_type_ctx = MockPayload::content_type_context();
        // content_type should be called on the outgoing request payload
        mock_payload_content_type_ctx
            .expect()
            .returning(|| "application/json")
            .once();

        // Mock context to track format_indicator calls
        let mock_payload_format_indicator_ctx = MockPayload::format_indicator_context();
        // content_type should be called on the outgoing request payload
        mock_payload_format_indicator_ctx
            .expect()
            .returning(|| FormatIndicator::UnspecifiedBytes)
            .once();

        // TODO: Check for
        //      sub sent (suback received)?
        //      pub sent (puback received)
        //      pub received (puback sent)

        // Mock context to track deserialize calls
        let mock_payload_deserialize_ctx = MockPayload::deserialize_context();
        // Deserialize should be called on the incoming response payload
        mock_payload_deserialize_ctx
            .expect()
            .returning(|_| {
                let mut mock_response_payload = MockPayload::default();
                // The deserialized payload should be cloned to return to the application
                mock_response_payload
                    .expect_clone()
                    .returning(MockPayload::default)
                    .times(1);
                Ok(mock_response_payload)
            })
            .once();

        // Mock invoker being subscribed already so we don't wait for suback
        let mut is_subscribed = command_invoker.is_subscribed_mutex.lock().await;
        *is_subscribed = true;
        drop(is_subscribed);

        let response = command_invoker
            .invoke(
                CommandRequestBuilder::default()
                    .payload(&mock_request_payload)
                    .unwrap()
                    .timeout(Duration::from_secs(5))
                    .build()
                    .unwrap(),
            )
            .await;
        assert!(response.is_ok());
    }

    // Tests failure: Invocation times out (valid timeout value specified on invoke) and a `Timeout` error is returned
    #[tokio::test]
    async fn test_invoke_times_out() {
        // Get mutexes for checking static PayloadSerialize calls
        let _content_type_mutex = CONTENT_TYPE_MTX.lock();
        let _format_indicator_mutex = FORMAT_INDICATOR_MTX.lock();

        let mut mqtt_provider = get_mqtt_provider();
        let invoker_options = CommandInvokerOptionsBuilder::default()
            .request_topic_pattern("test/req/topic")
            .command_name("test_command_name")
            .build()
            .unwrap();

        let command_invoker: CommandInvoker<MockPayload, MockPayload, _> =
            CommandInvoker::new(&mut mqtt_provider, invoker_options).unwrap();

        let mut mock_request_payload = MockPayload::new();
        mock_request_payload
            .expect_serialize()
            .returning(|| Ok(String::new().into()))
            .times(1);

        // Mock context to track content_type calls
        let mock_payload_content_type_ctx = MockPayload::content_type_context();
        // content_type should be called on the outgoing request payload
        mock_payload_content_type_ctx
            .expect()
            .returning(|| "application/json")
            .once();

        // Mock context to track format_indicator calls
        let mock_payload_format_indicator_ctx = MockPayload::format_indicator_context();
        // content_type should be called on the outgoing request payload
        mock_payload_format_indicator_ctx
            .expect()
            .returning(|| FormatIndicator::UnspecifiedBytes)
            .once();

        // TODO: Check for
        //      sub sent (suback received)
        //      pub sent (puback received)
        //      pub not received

        let response = command_invoker
            .invoke(
                CommandRequestBuilder::default()
                    .payload(&mock_request_payload)
                    .unwrap()
                    .timeout(Duration::from_millis(2))
                    .build()
                    .unwrap(),
            )
            .await;
        match response {
            Ok(_) => panic!("Expected error"),
            Err(e) => {
                assert_eq!(e.kind, AIOProtocolErrorKind::Timeout);
                assert!(!e.in_application);
                assert!(!e.is_shallow);
                assert!(!e.is_remote);
                assert_eq!(e.http_status_code, None);
                assert_eq!(e.timeout_name, Some("test_command_name".to_string()));
                assert!(e.timeout_value == Some(Duration::from_millis(2)));
            }
        }
    }

    #[tokio::test]
    #[ignore] // test ignored because waiting for the suback hangs forever. Leaving the test for now until we have a full testing framework
    async fn test_invoke_deserialize_error() {
        // Get mutexes for checking static PayloadSerialize calls
        let _deserialize_mutex = DESERIALIZE_MTX.lock();
        let _content_type_mutex = CONTENT_TYPE_MTX.lock();
        let _format_indicator_mutex = FORMAT_INDICATOR_MTX.lock();

        let mut mqtt_provider = get_mqtt_provider();
        let invoker_options = CommandInvokerOptionsBuilder::default()
            .request_topic_pattern("test/req/topic")
            .command_name("test_command_name")
            .build()
            .unwrap();

        let command_invoker: CommandInvoker<MockPayload, MockPayload, _> =
            CommandInvoker::new(&mut mqtt_provider, invoker_options).unwrap();

        let mut mock_request_payload = MockPayload::new();
        mock_request_payload
            .expect_serialize()
            .returning(|| Ok(String::new().into()))
            .times(1);

        // Mock context to track content_type calls
        let mock_payload_content_type_ctx = MockPayload::content_type_context();
        // content_type should be called on the outgoing request payload
        mock_payload_content_type_ctx
            .expect()
            .returning(|| "application/json")
            .once();

        // Mock context to track format_indicator calls
        let mock_payload_format_indicator_ctx = MockPayload::format_indicator_context();
        // content_type should be called on the outgoing request payload
        mock_payload_format_indicator_ctx
            .expect()
            .returning(|| FormatIndicator::UnspecifiedBytes)
            .once();

        // TODO: Check for
        //      sub sent (suback received)?
        //      pub sent (puback received)
        //      pub received (puback sent)

        // Mock context to track deserialize calls
        let mock_payload_deserialize_ctx = MockPayload::deserialize_context();
        // Deserialize should be called on the incoming response payload
        mock_payload_deserialize_ctx
            .expect()
            .returning(|_| Err("dummy error".to_string()))
            .once();

        // Mock invoker being subscribed already so we don't wait for suback
        let mut is_subscribed = command_invoker.is_subscribed_mutex.lock().await;
        *is_subscribed = true;
        drop(is_subscribed);

        let response = command_invoker
            .invoke(
                CommandRequestBuilder::default()
                    .payload(&mock_request_payload)
                    .unwrap()
                    .timeout(Duration::from_millis(2))
                    .build()
                    .unwrap(),
            )
            .await;
        match response {
            Ok(_) => panic!("Expected error"),
            Err(e) => {
                assert_eq!(e.kind, AIOProtocolErrorKind::PayloadInvalid);
                assert!(!e.in_application);
                assert!(!e.is_shallow);
                assert!(!e.is_remote);
                assert_eq!(e.http_status_code, None);
                assert!(e.nested_error.is_some());
            }
        }
    }

    #[tokio::test]
    async fn test_invoke_executor_id_invalid_value() {
        let mut mqtt_provider = get_mqtt_provider();
        let invoker_options = CommandInvokerOptionsBuilder::default()
            .request_topic_pattern("test/req/{executorId}/topic")
            .command_name("test_command_name")
            .build()
            .unwrap();

        let command_invoker: CommandInvoker<MockPayload, MockPayload, _> =
            CommandInvoker::new(&mut mqtt_provider, invoker_options).unwrap();
        let mut mock_request_payload = MockPayload::new();
        mock_request_payload
            .expect_serialize()
            .returning(|| Ok(String::new().into()))
            .times(1);

        let response = command_invoker
            .invoke(
                CommandRequestBuilder::default()
                    .payload(&mock_request_payload)
                    .unwrap()
                    .timeout(Duration::from_secs(2))
                    .executor_id(Some("+++".to_string()))
                    .build()
                    .unwrap(),
            )
            .await;
        match response {
            Ok(_) => panic!("Expected error"),
            Err(e) => {
                assert_eq!(e.kind, AIOProtocolErrorKind::ConfigurationInvalid);
                assert!(!e.in_application);
                assert!(e.is_shallow);
                assert!(!e.is_remote);
                assert_eq!(e.http_status_code, None);
                assert_eq!(e.property_name, Some("executorId".to_string()));
                assert!(e.property_value == Some(Value::String("+++".to_string())));
            }
        }
    }

    #[tokio::test]
    async fn test_invoke_serializer_invalid_content_type() {
        // Get mutexes for checking static PayloadSerialize calls
        let _content_type_mutex = CONTENT_TYPE_MTX.lock();

        let mut mqtt_provider = get_mqtt_provider();
        let invoker_options = CommandInvokerOptionsBuilder::default()
            .request_topic_pattern("test/req/topic")
            .command_name("test_command_name")
            .build()
            .unwrap();

        let command_invoker: CommandInvoker<MockPayload, MockPayload, _> =
            CommandInvoker::new(&mut mqtt_provider, invoker_options).unwrap();

        let mut mock_request_payload = MockPayload::new();
        mock_request_payload
            .expect_serialize()
            .returning(|| Ok(String::new().into()))
            .times(1);

        // Mock context to track content_type calls
        let mock_payload_content_type_ctx = MockPayload::content_type_context();
        // content_type should be called on the outgoing request payload
        mock_payload_content_type_ctx
            .expect()
            .returning(|| "abc\ndef")
            .once();

        let response = command_invoker
            .invoke(
                CommandRequestBuilder::default()
                    .payload(&mock_request_payload)
                    .unwrap()
                    .timeout(Duration::from_secs(2))
                    .build()
                    .unwrap(),
            )
            .await;
        match response {
            Ok(_) => panic!("Expected error"),
            Err(e) => {
                assert_eq!(e.kind, AIOProtocolErrorKind::PayloadInvalid);
                assert!(!e.in_application);
                assert!(e.is_shallow);
                assert!(!e.is_remote);
                assert_eq!(e.http_status_code, None);
                assert!(e.nested_error.is_none());
            }
        }
    }

    // Test cases for subscribe
    // Tests success:
    //    Subscribe is called on first invoke request and suback is received
    //    Subscribe is not called on subsequent invoke requests
    //    Subscribe fails on first invoke request, so subscribe is called again on second invoke request
    // Tests failure:
    //    subscribe call fails (invalid filter or failure on sending outbound sub async) and an `ClientError` error is returned (first error scenario should not be possible with topic parsing checks)
    //    suback returns bad reason code and an `ClientError` error is returned

    // Test cases for invoke create publish, publish, get puback
    // Tests success:
    //    Publish is called on invoke request and puback is received
    //    custom user data property doesn't start with __ and is added to the publish properties
    //    invalid executor id when it's not in either topic pattern
    //    payload is successfully serialized
    //    invoker client id is correctly added to user properties on message
    //    fencing_token is provided and added to user properties
    //    fencing_token isn't provided and isn't added to user properties
    // Tests failure:
    //     x timeout is > u32::max (invalid value) and an `ArgumentInvalid` error is returned
    //     x custom user data property starts with __
    //     x custom user data property key or value is malformed utf-8
    //     x invalid executor id
    //     x payload fails serialization
    //     x serializer sets invalid content_type
    //     puback returns bad reason code and an `ClientError` error is returned
    //     publish call fails (invalid topic or failure on sending outbound pub async) and an `ClientError` error is returned (first error scenario should not be possible with topic parsing checks)

    // Test cases for invoke receive
    // Receive response loop
    // Tests success:
    //     publish received on the correct topic, gets sent to pending command requests
    //     publish received on a different topic, message ignored
    //     publish received on the correct topic, no pending commands. Message ignored
    //     receive cancellation token is called and the loop exits
    //     mqtt_receiver sends a lagged error
    //     mqtt_receiver sends a closed error
    // Tests failure:
    //     should never fail
    //
    // validation
    // Tests success:
    //     content type matches TReq content type
    //     content type isn't present
    //     format indicator not present, 0, or (1 and TResp format indicator is 1)
    //     valid timestamp present and is returned on the CommandResponse
    //     no timestamp is present
    //     status is a number and is one of StatusCode's enum values
    //     in_application is interpreted as false if it is anything other than "true" and there are no errors parsing this
    //     custom user properties are correctly passed to the application. Any starting with the reserved prefix '__' that aren't ours are logged and ignored
    //     status code is no content and the payload is empty
    //     test matrix for different statuses and fields present for an error response - see possible return values from invoke for full list
    //     response payload deserializes successfully
    // Tests failure:
    //     content type doesn't match TResp content type. 'HeaderInvalid' error returned
    //     format indicator is 1 and TResp format indicator is 0. 'HeaderInvalid' error returned
    //     timestamp cannot be parsed (more or less than 3 sections, invalid section values for each of the three sections)
    //     status can't be parsed as a number
    //     status isn't one of StatusCode's enum values
    //     status is missing
    //     status code is no content, but the payload isn't empty
    //     failure deserializing response payload
    //
    // Wait for/match response
    // Tests success:
    //    response with correct correlation_id received. Parsing begins
    //    response with incorrect correlation_id received. Message ignored
    //    response with missing properties received. Message ignored
    //    response with missing correlation data received. Message ignored
    //    response_rx returns lagged error. Error logged and continue waiting for the response
    // Tests failure:
    //    response_rx returns closed error. Returns cancellation error

    // CommandRequest tests

    #[test]
    fn test_request_serialization_error() {
        let mut mock_request_payload = MockPayload::new();
        mock_request_payload
            .expect_serialize()
            .returning(|| Err("dummy error".to_string()))
            .times(1);

        let mut binding = CommandRequestBuilder::default();
        let req_builder = binding.payload(&mock_request_payload);
        assert!(req_builder.is_err());
    }

    /// Tests failure: Timeout specified as 0 (invalid value) on invoke and an `ArgumentInvalid` error is returned
    #[test_case(Duration::from_secs(0); "invoke_timeout_0")]
    /// Tests failure: Timeout specified as > u32::max (invalid value) on invoke and an `ArgumentInvalid` error is returned
    #[test_case(Duration::from_secs(u64::from(u32::MAX) + 1); "invoke_timeout_u32_max")]
    /// Tests failure: Timeout specified as < 1ms (invalid value) on invoke and an `ArgumentInvalid` error is returned
    #[test_case(Duration::from_nanos(50); "invoke_timeout_less_1_ms")]
    fn test_request_timeout_invalid_value(timeout: Duration) {
        let mut mock_request_payload = MockPayload::new();
        mock_request_payload
            .expect_serialize()
            .returning(|| Ok(String::new().into()))
            .times(1);

        let request_builder_result = CommandRequestBuilder::default()
            .payload(&mock_request_payload)
            .unwrap()
            .timeout(timeout)
            .build();

        assert!(request_builder_result.is_err());
    }
}
