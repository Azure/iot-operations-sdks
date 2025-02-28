// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

use std::str::FromStr;
use std::sync::{Arc, Mutex};
use std::{collections::HashMap, marker::PhantomData, time::Duration};

use azure_iot_operations_mqtt::control_packet::{PublishProperties, QoS};
use azure_iot_operations_mqtt::interface::{AckToken, ManagedClient, PubReceiver};
use bytes::Bytes;
use tokio::sync::oneshot;
use tokio::time::{timeout, Instant};
use tokio_util::sync::CancellationToken;

use crate::{
    application::{ApplicationContext, ApplicationHybridLogicalClock},
    common::{
        aio_protocol_error::{AIOProtocolError, AIOProtocolErrorKind, Value},
        hybrid_logical_clock::HybridLogicalClock,
        is_invalid_utf8,
        payload_serialize::{
            DeserializationError, FormatIndicator, PayloadSerialize, SerializedPayload,
        },
        topic_processor::{contains_invalid_char, is_valid_replacement, TopicPattern},
        user_properties::{validate_user_properties, UserProperty, PARTITION_KEY},
    },
    rpc::{StatusCode, DEFAULT_RPC_PROTOCOL_VERSION, RPC_PROTOCOL_VERSION},
    supported_protocol_major_versions_to_string, ProtocolVersion,
};

/// Default message expiry interval only for when the message expiry interval is not present
const DEFAULT_MESSAGE_EXPIRY_INTERVAL_SECONDS: u32 = 10;

/// Message for when expiration time is unable to be calculated, internal logic error
const INTERNAL_LOGIC_EXPIRATION_ERROR: &str =
    "Internal logic error, unable to calculate command expiration time";

const SUPPORTED_PROTOCOL_VERSIONS: &[u16] = &[1];

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
    message_expiry_interval: Option<u32>,
    supported_protocol_major_versions: Option<Vec<u16>>,
    request_protocol_version: Option<String>,
    cached_key: Option<CommandExecutorCacheKey>,
    cached_entry_status: CommandExecutorCacheEntryStatus,
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
    /// Content Type of the command request.
    pub content_type: Option<String>,
    /// Format Indicator of the command request.
    pub format_indicator: FormatIndicator,
    /// Custom user data set as custom MQTT User Properties on the request message.
    pub custom_user_data: Vec<(String, String)>,
    /// Timestamp of the command request.
    pub timestamp: Option<HybridLogicalClock>,
    /// If present, contains the client ID of the invoker of the command.
    pub invoker_id: Option<String>,
    /// Resolved static and dynamic topic tokens from the incoming request's topic.
    pub topic_tokens: HashMap<String, String>,
    // Internal fields
    command_name: String,
    response_tx: oneshot::Sender<Result<CommandResponse<TResp>, String>>,
    publish_completion_rx: oneshot::Receiver<Result<(), AIOProtocolError>>,
}

impl<TReq, TResp> CommandRequest<TReq, TResp>
where
    TReq: PayloadSerialize,
    TResp: PayloadSerialize,
{
    /// Consumes the command request and reports the response to the executor. An attempt is made to
    /// send the response to the invoker.
    ///
    /// Returns Ok(()) on success, otherwise returns [`AIOProtocolError`].
    ///
    /// # Arguments
    /// * `response` - The [`CommandResponse`] to send.
    ///
    /// # Errors
    ///
    /// [`AIOProtocolError`] of kind [`Timeout`](crate::common::aio_protocol_error::AIOProtocolErrorKind::Timeout) if the command request
    /// has expired.
    ///
    /// [`AIOProtocolError`] of kind [`ClientError`](crate::common::aio_protocol_error::AIOProtocolErrorKind::ClientError) if the response
    /// acknowledgement returns an error.
    ///
    /// [`AIOProtocolError`] of kind [`Cancellation`](crate::common::aio_protocol_error::AIOProtocolErrorKind::Cancellation) if the
    /// executor is dropped.
    ///
    /// [`AIOProtocolError`] of kind [`InternalLogicError`](crate::common::aio_protocol_error::AIOProtocolErrorKind::InternalLogicError)
    /// if the response publish completion fails. This should not happen.
    pub async fn complete(self, response: CommandResponse<TResp>) -> Result<(), AIOProtocolError> {
        self.send_response(Ok(response)).await
    }

    /// Consumes the command request and reports an error to the executor. An attempt is made to
    /// send the error to the invoker.
    ///
    /// Returns Ok(()) on success, otherwise returns [`AIOProtocolError`].
    ///
    /// # Arguments
    /// * `error` - The error message to send.
    ///
    /// # Errors
    ///
    /// [`AIOProtocolError`] of kind [`Timeout`](crate::common::aio_protocol_error::AIOProtocolErrorKind::Timeout) if the command request
    /// has expired.
    ///
    /// [`AIOProtocolError`] of kind [`ClientError`](crate::common::aio_protocol_error::AIOProtocolErrorKind::ClientError) if the error
    /// acknowledgement returns an error.
    ///
    /// [`AIOProtocolError`] of kind [`Cancellation`](crate::common::aio_protocol_error::AIOProtocolErrorKind::Cancellation) if the
    /// executor is dropped.
    ///
    /// [`AIOProtocolError`] of kind [`InternalLogicError`](crate::common::aio_protocol_error::AIOProtocolErrorKind::InternalLogicError)
    /// if the error publish completion fails. This should not happen.
    pub async fn error(self, error: String) -> Result<(), AIOProtocolError> {
        self.send_response(Err(error)).await
    }

    fn create_cancellation_error(command_name: String) -> AIOProtocolError {
        AIOProtocolError::new_cancellation_error(
            false,
            None,
            None,
            Some(
                "Command Executor has been shutdown and can no longer respond to commands"
                    .to_string(),
            ),
            Some(command_name),
        )
    }

    async fn send_response(
        self,
        response: Result<CommandResponse<TResp>, String>,
    ) -> Result<(), AIOProtocolError> {
        // We can ignore the error here. If the receiver of the response is dropped it may be
        // because the executor is shutting down in which case the receive below will fail.
        // If the executor is not shutting down, the receive below will succeed and we'll receive a
        // timeout error since that is the only possible error at this point.
        let _ = self.response_tx.send(response);

        self.publish_completion_rx
            .await
            .map_err(|_| Self::create_cancellation_error(self.command_name))?
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
    serialized_payload: SerializedPayload,
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
    /// [`AIOProtocolError`] of kind [`PayloadInvalid`](crate::common::aio_protocol_error::AIOProtocolErrorKind::PayloadInvalid) if serialization of the payload fails
    ///
    /// [`AIOProtocolError`] of kind [`ConfigurationInvalid`](crate::common::aio_protocol_error::AIOProtocolErrorKind::ConfigurationInvalid) if the content type is not valid utf-8
    pub fn payload(&mut self, payload: TResp) -> Result<&mut Self, AIOProtocolError> {
        match payload.serialize() {
            Err(e) => Err(AIOProtocolError::new_payload_invalid_error(
                true,
                false,
                Some(e.into()),
                None,
                Some("Payload serialization error".to_string()),
                None,
            )),
            Ok(serialized_payload) => {
                // Validate content type of command response is valid UTF-8
                if is_invalid_utf8(&serialized_payload.content_type) {
                    return Err(AIOProtocolError::new_configuration_invalid_error(
                        None,
                        "content_type",
                        Value::String(serialized_payload.content_type.to_string()),
                        Some(format!(
                            "Content type '{}' of command response is not valid UTF-8",
                            serialized_payload.content_type
                        )),
                        None,
                    ));
                }
                self.serialized_payload = Some(serialized_payload);
                self.response_payload_type = Some(PhantomData);
                Ok(self)
            }
        }
    }

    /// Validate the command response.
    ///
    /// # Errors
    /// Returns a `String` describing the error if any of `custom_user_data`'s keys or values are invalid utf-8
    /// or the reserved [`PARTITION_KEY`] key is used.
    fn validate(&self) -> Result<(), String> {
        if let Some(custom_user_data) = &self.custom_user_data {
            validate_user_properties(custom_user_data)?;
        }
        Ok(())
    }
}

/// Command Executor Cache Key struct.
///
/// Used to uniquely identify a command request.
#[derive(Eq, Hash, PartialEq, Clone)]
struct CommandExecutorCacheKey {
    response_topic: String,
    correlation_data: Bytes,
}

/// Command Executor Cache Entry struct.
#[derive(Clone, PartialEq, Debug)]
struct CommandExecutorCacheEntry {
    serialized_payload: SerializedPayload,
    properties: PublishProperties,
    expiration_time: Instant,
}

/// Command Executor Cache Entry Status enum.
///
/// Used to indicate the status of a cache entry.
///
/// Note: It is not possible for a cache entry to be in progress due to the nature of the underlying
/// session. If a command request is received, the session will drop duplicates while the original
/// request is being processed.
#[derive(PartialEq, Debug)]
enum CommandExecutorCacheEntryStatus {
    /// The cache entry is cached and has not expired
    Cached(CommandExecutorCacheEntry),
    /// The cache entry is expired
    Expired,
    /// The cache entry is not found
    NotFound,
}

/// The Command Executor Cache struct.
///
/// Used to cache command responses and determine if a command request is a duplicate.
#[derive(Clone)]
struct CommandExecutorCache(
    Arc<Mutex<HashMap<CommandExecutorCacheKey, CommandExecutorCacheEntry>>>,
);

impl CommandExecutorCache {
    /// Get a cache entry from the [`CommandExecutorCache`].
    ///
    /// # Arguments
    /// `key` - The cache key to get the cache entry for.
    ///
    /// Returns a [`CommandExecutorCacheEntryStatus`] indicating the status of the cache entry.
    fn get(&self, key: &CommandExecutorCacheKey) -> CommandExecutorCacheEntryStatus {
        let cache = self.0.lock().unwrap();
        cache
            .get(key)
            .map_or(CommandExecutorCacheEntryStatus::NotFound, |entry| {
                if entry.expiration_time.elapsed().is_zero() {
                    CommandExecutorCacheEntryStatus::Cached(entry.clone())
                } else {
                    CommandExecutorCacheEntryStatus::Expired
                }
            })
    }

    /// Set a cache entry in the cache. Also removes expired cache entries.
    ///
    /// # Arguments
    /// `key` - The cache key to set the cache entry for.
    /// `entry` - The cache entry to set.
    fn set(&self, key: CommandExecutorCacheKey, entry: CommandExecutorCacheEntry) {
        let mut cache = self.0.lock().unwrap();
        cache.retain(|_, entry| entry.expiration_time.elapsed().is_zero());
        cache.insert(key, entry);
    }
}

/// Command Executor Options struct
#[allow(unused)]
#[derive(Builder, Clone)]
#[builder(setter(into, strip_option))]
pub struct CommandExecutorOptions {
    /// Topic pattern for the command request.
    /// Must align with [topic-structure.md](https://github.com/Azure/iot-operations-sdks/blob/main/doc/reference/topic-structure.md)
    request_topic_pattern: String,
    /// Command name if required by the topic pattern
    command_name: String,
    /// Optional Topic namespace to be prepended to the topic pattern
    #[builder(default = "None")]
    topic_namespace: Option<String>,
    /// Topic token keys/values to be permanently replaced in the topic pattern
    #[builder(default)]
    topic_token_map: HashMap<String, String>,
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
/// # use azure_iot_operations_protocol::application::ApplicationContextBuilder;
/// # let mut connection_settings = MqttConnectionSettingsBuilder::default()
/// #     .client_id("test_server")
/// #     .hostname("localhost")
/// #     .tcp_port(1883u16)
/// #     .build().unwrap();
/// # let mut session_options = SessionOptionsBuilder::default()
/// #     .connection_settings(connection_settings)
/// #     .build().unwrap();
/// # let mut mqtt_session = Session::new(session_options).unwrap();
/// # let application_context = ApplicationContextBuilder::default().build().unwrap();;
/// let executor_options = CommandExecutorOptionsBuilder::default()
///   .command_name("test_command")
///   .request_topic_pattern("test/request")
///   .build().unwrap();
/// # tokio_test::block_on(async {
/// let mut command_executor: CommandExecutor<Vec<u8>, Vec<u8>, _> = CommandExecutor::new(application_context, mqtt_session.create_managed_client(), executor_options).unwrap();
/// // let request = command_executor.recv().await.unwrap();
/// // let response = CommandResponseBuilder::default()
///  // .payload(Vec::new()).unwrap()
///  // .build().unwrap();
/// // let request.complete(response).await.unwrap();
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
    application_hlc: Arc<ApplicationHybridLogicalClock>,
    mqtt_client: C,
    mqtt_receiver: C::PubReceiver,
    is_idempotent: bool,
    request_topic_pattern: TopicPattern,
    command_name: String,
    request_payload_type: PhantomData<TReq>,
    response_payload_type: PhantomData<TResp>,
    cache: CommandExecutorCache,
    // Describes state
    executor_state: CommandExecutorState,
    // Information to manage state
    executor_cancellation_token: CancellationToken,
}

/// Describes state of executor
#[derive(PartialEq)]
enum CommandExecutorState {
    New,
    Subscribed,
    ShutdownSuccessful,
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
    /// * `application_context` - [`ApplicationContext`] that the command executor is part of.
    /// * `client` - The MQTT client to use for communication.
    /// * `executor_options` - Configuration options.
    ///
    /// Returns Ok([`CommandExecutor`]) on success, otherwise returns [`AIOProtocolError`].
    ///
    /// # Errors
    /// [`AIOProtocolError`] of kind [`ConfigurationInvalid`](crate::common::aio_protocol_error::AIOProtocolErrorKind::ConfigurationInvalid) if:
    /// - [`command_name`](CommandExecutorOptions::command_name) is empty, whitespace or invalid
    /// - [`request_topic_pattern`](CommandExecutorOptions::request_topic_pattern),
    ///     [`topic_namespace`](CommandExecutorOptions::topic_namespace)
    ///     are Some and invalid or contain a token with no valid replacement
    /// - [`topic_token_map`](CommandExecutorOptions::topic_token_map) is not empty and contains invalid key(s) and/or token(s)
    pub fn new(
        application_context: ApplicationContext,
        client: C,
        executor_options: CommandExecutorOptions,
    ) -> Result<Self, AIOProtocolError> {
        // Validate function parameters, validation for topic pattern and related options done in
        // TopicPattern::new
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

        // Create a new Command Pattern, validates topic pattern and options
        let request_topic_pattern = TopicPattern::new(
            &executor_options.request_topic_pattern,
            executor_options.service_group_id,
            executor_options.topic_namespace.as_deref(),
            &executor_options.topic_token_map,
        )
        .map_err(|e| {
            AIOProtocolError::config_invalid_from_topic_pattern_error(
                e,
                "executor_options.request_topic_pattern",
            )
        })?;

        // Get pub sub and receiver from the mqtt session
        let mqtt_receiver = match client
            .create_filtered_pub_receiver(&request_topic_pattern.as_subscribe_topic())
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
            application_hlc: application_context.application_hlc,
            mqtt_client: client,
            mqtt_receiver,
            is_idempotent: executor_options.is_idempotent,
            request_topic_pattern,
            command_name: executor_options.command_name,
            request_payload_type: PhantomData,
            response_payload_type: PhantomData,
            cache: CommandExecutorCache(Arc::new(Mutex::new(HashMap::new()))),
            executor_state: CommandExecutorState::New,
            executor_cancellation_token: CancellationToken::new(),
        })
    }

    /// Shutdown the [`CommandExecutor`]. Unsubscribes from the request topic.
    ///
    /// Note: If this method is called, the [`CommandExecutor`] will no longer receive commands
    /// from the MQTT client, any command requests that have not been processed can still be received
    /// by the executor. If the method returns an error, it may be called again to attempt the unsubscribe again.
    ///
    /// Returns Ok(()) on success, otherwise returns [`AIOProtocolError`].
    /// # Errors
    /// [`AIOProtocolError`] of kind [`ClientError`](crate::common::aio_protocol_error::AIOProtocolErrorKind::ClientError) if the unsubscribe fails or if the unsuback reason code doesn't indicate success.
    pub async fn shutdown(&mut self) -> Result<(), AIOProtocolError> {
        // Close the receiver, no longer receive messages
        self.mqtt_receiver.close();

        match self.executor_state {
            CommandExecutorState::New | CommandExecutorState::ShutdownSuccessful => {
                // If subscribe has not been called or shutdown was successful, do not unsubscribe
                self.executor_state = CommandExecutorState::ShutdownSuccessful;
            }
            CommandExecutorState::Subscribed => {
                let unsubscribe_result = self
                    .mqtt_client
                    .unsubscribe(self.request_topic_pattern.as_subscribe_topic())
                    .await;

                match unsubscribe_result {
                    Ok(unsub_ct) => match unsub_ct.await {
                        Ok(()) => {
                            self.executor_state = CommandExecutorState::ShutdownSuccessful;
                        }
                        Err(e) => {
                            log::error!("[{}] Unsuback error: {e}", self.command_name);
                            return Err(AIOProtocolError::new_mqtt_error(
                                Some("MQTT error on command executor unsuback".to_string()),
                                Box::new(e),
                                Some(self.command_name.clone()),
                            ));
                        }
                    },
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
            }
        }
        log::info!("[{}] Shutdown", self.command_name);
        Ok(())
    }

    /// Subscribe to the request topic.
    ///
    /// Returns Ok(()) on success, otherwise returns [`AIOProtocolError`].
    /// # Errors
    /// [`AIOProtocolError`] of kind [`ClientError`](crate::common::aio_protocol_error::AIOProtocolErrorKind::ClientError) if the subscribe fails or if the suback reason code doesn't indicate success.
    async fn try_subscribe(&mut self) -> Result<(), AIOProtocolError> {
        let subscribe_result = self
            .mqtt_client
            .subscribe(
                self.request_topic_pattern.as_subscribe_topic(),
                QoS::AtLeastOnce,
            )
            .await;

        match subscribe_result {
            Ok(sub_ct) => match sub_ct.await {
                Ok(()) => { /* Success */ }
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
        Ok(())
    }

    /// Receive a command request or [`None`] if there will be no more requests.
    ///
    /// If there are messages:
    /// - Returns Ok([`CommandRequest`]) on success
    /// - Returns [`AIOProtocolError`] on error.
    ///
    /// Will also subscribe to the request topic if not already subscribed.
    ///
    /// # Errors
    /// [`AIOProtocolError`] of kind [`UnknownError`](crate::common::aio_protocol_error::AIOProtocolErrorKind::UnknownError) if an error occurs while receiving the message.
    ///
    /// [`AIOProtocolError`] of kind [`ClientError`](crate::common::aio_protocol_error::AIOProtocolErrorKind::ClientError) if the subscribe fails or if the suback reason code doesn't indicate success.
    ///
    /// [`AIOProtocolError`] of kind [`InternalLogicError`](crate::common::aio_protocol_error::AIOProtocolErrorKind::InternalLogicError) if the command expiration time cannot be calculated.
    pub async fn recv(&mut self) -> Option<Result<CommandRequest<TReq, TResp>, AIOProtocolError>> {
        // Subscribe to the request topic if not already subscribed
        if CommandExecutorState::New == self.executor_state {
            if let Err(e) = self.try_subscribe().await {
                return Some(Err(e));
            }
            self.executor_state = CommandExecutorState::Subscribed;
        }

        loop {
            if let Some((m, ack_token)) = self.mqtt_receiver.recv_manual_ack().await {
                let Some(ack_token) = ack_token else {
                    // No ack token, ignore the message. This should never happen as the executor
                    // should always receive QoS 1 messages that have an ack token.
                    log::warn!("[{}] Received message without ack token", self.command_name);
                    continue;
                };
                // Process the request
                log::info!("[{}][pkid: {}] Received request", self.command_name, m.pkid);
                let message_received_time = Instant::now();

                // Clone properties
                let properties = match &m.properties {
                    Some(properties) => properties.clone(),
                    None => {
                        log::error!(
                            "[{}][pkid: {}] Properties missing",
                            self.command_name,
                            m.pkid
                        );
                        tokio::task::spawn({
                            let executor_cancellation_token_clone =
                                self.executor_cancellation_token.clone();
                            async move {
                                handle_ack(ack_token, executor_cancellation_token_clone, m.pkid)
                                    .await;
                            }
                        });
                        continue;
                    }
                };

                // Get response topic
                let response_topic = if let Some(rt) = properties.response_topic {
                    if !is_valid_replacement(&rt) {
                        log::error!("[{}][pkid: {}] Response topic invalid, command response will not be published", self.command_name, m.pkid);
                        tokio::task::spawn({
                            let executor_cancellation_token_clone =
                                self.executor_cancellation_token.clone();
                            async move {
                                handle_ack(ack_token, executor_cancellation_token_clone, m.pkid)
                                    .await;
                            }
                        });
                        continue;
                    }
                    rt
                } else {
                    log::error!(
                        "[{}][pkid: {}] Response topic missing",
                        self.command_name,
                        m.pkid
                    );
                    tokio::task::spawn({
                        let executor_cancellation_token_clone =
                            self.executor_cancellation_token.clone();
                        async move {
                            handle_ack(ack_token, executor_cancellation_token_clone, m.pkid).await;
                        }
                    });
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
                    message_expiry_interval: None,
                    command_expiration_time: None,
                    supported_protocol_major_versions: None,
                    request_protocol_version: None,
                    cached_key: None,
                    cached_entry_status: CommandExecutorCacheEntryStatus::NotFound,
                };

                // Get message expiry interval
                let command_expiration_time = if let Some(ct) = properties.message_expiry_interval {
                    response_arguments.message_expiry_interval = Some(ct);
                    message_received_time.checked_add(Duration::from_secs(ct.into()))
                } else {
                    message_received_time.checked_add(Duration::from_secs(u64::from(
                        DEFAULT_MESSAGE_EXPIRY_INTERVAL_SECONDS,
                    )))
                };

                // Check if there was an error calculating the command expiration time
                // if not, set the command expiration time
                if let Some(command_expiration_time) = command_expiration_time {
                    response_arguments.command_expiration_time = Some(command_expiration_time);
                    command_expiration_time_calculated = true;
                }

                // Get correlation data
                if let Some(correlation_data) = properties.correlation_data {
                    if correlation_data.len() == 16 {
                        response_arguments.correlation_data = Some(correlation_data.clone());
                        response_arguments.cached_key = Some(CommandExecutorCacheKey {
                            response_topic: response_arguments.response_topic.clone(),
                            correlation_data,
                        });
                    } else {
                        response_arguments.status_code = StatusCode::BadRequest;
                        response_arguments.status_message =
                            Some("Correlation data bytes do not conform to a GUID".to_string());
                        response_arguments.invalid_property_name =
                            Some("Correlation Data".to_string());
                        if let Ok(correlation_data_str) =
                            String::from_utf8(correlation_data.to_vec())
                        {
                            response_arguments.invalid_property_value = Some(correlation_data_str);
                        } else { /* Ignore */
                        }
                        response_arguments.correlation_data = Some(correlation_data);
                    }
                } else {
                    response_arguments.status_code = StatusCode::BadRequest;
                    response_arguments.status_message =
                        Some("Correlation data missing".to_string());
                    response_arguments.invalid_property_name = Some("Correlation Data".to_string());
                };

                'process_request: {
                    // If the cache key was not created it means the correlation data was invalid
                    let Some(cache_key) = &response_arguments.cached_key else {
                        break 'process_request;
                    };

                    // Checking if command expiration time was calculated after correlation
                    // to provide a more accurate response to the invoker.
                    let Some(command_expiration_time) = command_expiration_time else {
                        response_arguments.status_code = StatusCode::InternalServerError;
                        response_arguments.status_message =
                            Some(INTERNAL_LOGIC_EXPIRATION_ERROR.to_string());
                        break 'process_request;
                    };

                    // Check if message expiry interval is present
                    if properties.message_expiry_interval.is_none() {
                        response_arguments.status_code = StatusCode::BadRequest;
                        response_arguments.status_message =
                            Some("Message expiry interval missing".to_string());
                        response_arguments.invalid_property_name =
                            Some("Message Expiry".to_string());
                        break 'process_request;
                    }

                    // Check cache
                    response_arguments.cached_entry_status = self.cache.get(cache_key);

                    // If the cache entry is not found, continue processing the request
                    if response_arguments.cached_entry_status
                        != CommandExecutorCacheEntryStatus::NotFound
                    {
                        break 'process_request;
                    }

                    // unused beyond validation, but may be used in the future to determine how to handle other fields. Can be moved higher in the future if needed.
                    let mut request_protocol_version = DEFAULT_RPC_PROTOCOL_VERSION; // assume default version if none is provided
                    if let Some((_, protocol_version)) =
                        properties.user_properties.iter().find(|(key, _)| {
                            UserProperty::from_str(key) == Ok(UserProperty::ProtocolVersion)
                        })
                    {
                        if let Some(request_version) =
                            ProtocolVersion::parse_protocol_version(protocol_version)
                        {
                            request_protocol_version = request_version;
                        } else {
                            response_arguments.status_code = StatusCode::VersionNotSupported;
                            response_arguments.status_message = Some(format!(
                                "Unparsable protocol version value provided: {protocol_version}."
                            ));
                            response_arguments.supported_protocol_major_versions =
                                Some(SUPPORTED_PROTOCOL_VERSIONS.to_vec());
                            response_arguments.request_protocol_version =
                                Some(protocol_version.to_string());
                            break 'process_request;
                        }
                    }
                    // Check that the version (or the default version if one isn't provided) is supported
                    if !request_protocol_version.is_supported(SUPPORTED_PROTOCOL_VERSIONS) {
                        response_arguments.status_code = StatusCode::VersionNotSupported;
                        response_arguments.status_message = Some(format!("The command executor that received the request only supports major protocol versions '{SUPPORTED_PROTOCOL_VERSIONS:?}', but '{request_protocol_version}' was sent on the request."));
                        response_arguments.supported_protocol_major_versions =
                            Some(SUPPORTED_PROTOCOL_VERSIONS.to_vec());
                        response_arguments.request_protocol_version =
                            Some(request_protocol_version.to_string());
                        break 'process_request;
                    }

                    let mut user_data = Vec::new();
                    let mut timestamp = None;
                    let mut invoker_id = None;
                    for (key, value) in properties.user_properties {
                        match UserProperty::from_str(&key) {
                            Ok(UserProperty::Timestamp) => {
                                match HybridLogicalClock::from_str(&value) {
                                    Ok(ts) => {
                                        // Update application HLC against received __ts
                                        if let Err(e) = self.application_hlc.update(&ts) {
                                            response_arguments.status_message = Some(format!("Failure updating application HLC against {value}: {e}"));
                                            response_arguments.invalid_property_name =
                                                Some(UserProperty::Timestamp.to_string());
                                            response_arguments.invalid_property_value = Some(value);
                                            match e.kind {
                                                AIOProtocolErrorKind::StateInvalid => {
                                                    response_arguments.status_code =
                                                        StatusCode::ServiceUnavailable;
                                                }
                                                _ => {
                                                    // AIOProtocolErrorKind::InternalLogicError should route here,
                                                    // but anything unexpected should also be classified as InternalServerError
                                                    response_arguments.status_code =
                                                        StatusCode::InternalServerError;
                                                }
                                            }
                                            break 'process_request;
                                        }
                                        timestamp = Some(ts);
                                    }
                                    Err(e) => {
                                        response_arguments.status_code = StatusCode::BadRequest;
                                        response_arguments.status_message =
                                            Some(format!("Timestamp invalid: {e}"));
                                        response_arguments.invalid_property_name =
                                            Some(UserProperty::Timestamp.to_string());
                                        response_arguments.invalid_property_value = Some(value);
                                        break 'process_request;
                                    }
                                }
                            }
                            Ok(UserProperty::SourceId) => {
                                invoker_id = Some(value);
                            }
                            Ok(UserProperty::ProtocolVersion) => {
                                // skip, already processed
                            }
                            Err(()) => {
                                if key == PARTITION_KEY {
                                    // Ignore partition key, it is meant for the broker
                                    continue;
                                }
                                user_data.push((key, value));
                            }
                            _ => {
                                /* UserProperty::Status, UserProperty::StatusMessage, UserProperty::IsApplicationError, UserProperty::InvalidPropertyName, UserProperty::InvalidPropertyValue */
                                // Don't return error, although above properties shouldn't be in the request
                                log::warn!("Request should not contain MQTT user property {key}. Value is {value}");
                                user_data.push((key, value));
                            }
                        }
                    }

                    let topic = match std::str::from_utf8(&m.topic) {
                        Ok(topic) => topic,
                        Err(e) => {
                            // This should never happen as the topic is always a valid UTF-8 string from the MQTT client
                            response_arguments.status_code = StatusCode::BadRequest;
                            response_arguments.status_message =
                                Some(format!("Error deserializing topic: {e:?}"));
                            break 'process_request;
                        }
                    };

                    let topic_tokens = self.request_topic_pattern.parse_tokens(topic);

                    // Deserialize payload
                    let format_indicator = match properties.payload_format_indicator.try_into() {
                        Ok(format_indicator) => format_indicator,
                        Err(e) => {
                            log::error!(
                                "[pkid: {}] Received invalid payload format indicator: {e}. This should not be possible to receive from the broker.",
                                m.pkid
                            );
                            // Use default format indicator
                            FormatIndicator::default()
                        }
                    };
                    let payload = match TReq::deserialize(
                        &m.payload,
                        &properties.content_type,
                        &format_indicator,
                    ) {
                        Ok(payload) => payload,
                        Err(e) => match e {
                            DeserializationError::InvalidPayload(deserialization_e) => {
                                response_arguments.status_code = StatusCode::BadRequest;
                                response_arguments.status_message = Some(format!(
                                    "Error deserializing payload: {deserialization_e:?}"
                                ));
                                break 'process_request;
                            }
                            DeserializationError::UnsupportedContentType(message) => {
                                response_arguments.status_code = StatusCode::UnsupportedMediaType;
                                response_arguments.status_message = Some(message);
                                response_arguments.invalid_property_name =
                                    Some("Content Type".to_string());
                                response_arguments.invalid_property_value =
                                    Some(properties.content_type.unwrap_or("None".to_string()));
                                break 'process_request;
                            }
                        },
                    };

                    let (response_tx, response_rx) = oneshot::channel();
                    let (publish_completion_tx, publish_completion_rx) = oneshot::channel();

                    let command_request = CommandRequest {
                        payload,
                        content_type: properties.content_type,
                        format_indicator,
                        custom_user_data: user_data,
                        timestamp,
                        invoker_id,
                        topic_tokens,
                        command_name: self.command_name.clone(),
                        response_tx,
                        publish_completion_rx,
                    };

                    // Check the command has not expired, if it has, we do not respond to the invoker.
                    if command_expiration_time.elapsed().is_zero() {
                        // Elapsed returns zero if the time has not passed
                        tokio::task::spawn({
                            let app_hlc_clone = self.application_hlc.clone();
                            let client_clone = self.mqtt_client.clone();
                            let cache_clone = self.cache.clone();
                            let executor_cancellation_token_clone =
                                self.executor_cancellation_token.clone();
                            let pkid = m.pkid;
                            async move {
                                tokio::select! {
                                    () = executor_cancellation_token_clone.cancelled() => { /* executor dropped */},
                                    () = Self::process_command(
                                        app_hlc_clone,
                                        client_clone,
                                        pkid,
                                        response_arguments,
                                        Some(response_rx),
                                        Some(publish_completion_tx),
                                        cache_clone,

                                    ) => {
                                        // Finished processing command
                                        handle_ack(ack_token, executor_cancellation_token_clone, pkid).await;
                                    },
                                }
                            }
                        });
                        return Some(Ok(command_request));
                    }
                }

                // Checking that command expiration time was calculated and has not
                // expired. If it has, we do not respond to the invoker.
                if let Some(command_expiration_time) = command_expiration_time {
                    if !command_expiration_time.elapsed().is_zero() {
                        continue;
                    }
                }

                // If the command has expired, we do not respond to the invoker.
                match response_arguments.cached_entry_status {
                    CommandExecutorCacheEntryStatus::Expired => {
                        log::debug!(
                            "[{}][pkid: {}] Duplicate request has expired",
                            self.command_name,
                            m.pkid
                        );
                        continue;
                    }
                    _ => {
                        tokio::task::spawn({
                            let app_hlc_clone = self.application_hlc.clone();
                            let client_clone = self.mqtt_client.clone();
                            let cache_clone = self.cache.clone();
                            let executor_cancellation_token_clone =
                                self.executor_cancellation_token.clone();
                            let pkid = m.pkid;
                            async move {
                                tokio::select! {
                                    () = executor_cancellation_token_clone.cancelled() => { /* executor dropped */},
                                    () = Self::process_command(
                                        app_hlc_clone,
                                        client_clone,
                                        pkid,
                                        response_arguments,
                                        None,
                                        None,
                                        cache_clone,
                                    ) => {
                                        // Finished processing command
                                        handle_ack(ack_token, executor_cancellation_token_clone, pkid).await;
                                    },
                                }
                            }
                        });
                    }
                }

                if !command_expiration_time_calculated {
                    return Some(Err(AIOProtocolError::new_internal_logic_error(
                        true,
                        false,
                        None,
                        None,
                        "command_expiration_time",
                        None,
                        Some(INTERNAL_LOGIC_EXPIRATION_ERROR.to_string()),
                        Some(self.command_name.clone()),
                    )));
                }
            } else {
                // There will be no more requests
                return None;
            }
        }
    }

    async fn process_command(
        application_hlc: Arc<ApplicationHybridLogicalClock>,
        client: C,
        pkid: u16,
        mut response_arguments: ResponseArguments,
        response_rx: Option<oneshot::Receiver<Result<CommandResponse<TResp>, String>>>,
        completion_tx: Option<oneshot::Sender<Result<(), AIOProtocolError>>>,
        cache: CommandExecutorCache,
    ) {
        let mut serialized_payload = SerializedPayload::default();
        let mut publish_properties = PublishProperties::default();
        let cache_not_found =
            response_arguments.cached_entry_status == CommandExecutorCacheEntryStatus::NotFound;

        if let CommandExecutorCacheEntryStatus::Cached(entry) =
            response_arguments.cached_entry_status
        {
            // The command has already been processed, we can respond with the cached response
            log::debug!(
                "[{}][pkid: {}] Duplicate request, responding with cached response",
                response_arguments.command_name,
                pkid
            );
            publish_properties = entry.properties;
            serialized_payload = entry.serialized_payload;
        } else {
            let mut user_properties: Vec<(String, String)> = Vec::new();
            'process_response: {
                let Some(command_expiration_time) = response_arguments.command_expiration_time
                else {
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
                                    response_arguments.status_code =
                                        StatusCode::InternalServerError;
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
                        // Notify the application that a timeout occurred
                        if let Some(completion_tx) = completion_tx {
                            let _ = completion_tx.send(Err(AIOProtocolError::new_timeout_error(
                                false,
                                None,
                                None,
                                &response_arguments.command_name,
                                Duration::from_secs(
                                    response_arguments
                                        .message_expiry_interval
                                        .unwrap_or_default()
                                        .into(),
                                ),
                                None,
                                Some(response_arguments.command_name.clone()),
                            )));
                        }
                        return;
                    };

                    user_properties = response.custom_user_data;

                    // Serialize payload
                    serialized_payload = response.serialized_payload;

                    if serialized_payload.payload.is_empty() {
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

            user_properties.push((
                UserProperty::ProtocolVersion.to_string(),
                RPC_PROTOCOL_VERSION.to_string(),
            ));

            // Update HLC and use as the timestamp.
            // If there are errors updating the HLC (unlikely when updating against now),
            // the timestamp will not be added.
            if let Ok(timestamp_str) = application_hlc.update_now() {
                user_properties.push((UserProperty::Timestamp.to_string(), timestamp_str));
            }

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

            if let Some(supported_protocol_major_versions) =
                response_arguments.supported_protocol_major_versions
            {
                user_properties.push((
                    UserProperty::SupportedMajorVersions.to_string(),
                    supported_protocol_major_versions_to_string(&supported_protocol_major_versions),
                ));
            }

            if let Some(request_protocol_version) = response_arguments.request_protocol_version {
                user_properties.push((
                    UserProperty::RequestProtocolVersion.to_string(),
                    request_protocol_version,
                ));
            }

            // Create publish properties
            publish_properties.payload_format_indicator =
                Some(serialized_payload.format_indicator.clone() as u8);
            publish_properties.topic_alias = None;
            publish_properties.response_topic = None;
            publish_properties.correlation_data = response_arguments.correlation_data;
            publish_properties.user_properties = user_properties;
            publish_properties.subscription_identifiers = Vec::new();
            publish_properties.content_type = Some(serialized_payload.content_type.to_string());
        };

        if let Some(command_expiration_time) = response_arguments.command_expiration_time {
            // Calculating remaining time until the command expires
            let response_message_expiry_interval =
                command_expiration_time.saturating_duration_since(Instant::now());
            if response_message_expiry_interval.is_zero() {
                log::error!(
                    "[{}][pkid: {}] Request timed out",
                    response_arguments.command_name,
                    pkid
                );
                // Notify the application that a timeout occurred
                if let Some(completion_tx) = completion_tx {
                    let _ = completion_tx.send(Err(AIOProtocolError::new_timeout_error(
                        false,
                        None,
                        None,
                        &response_arguments.command_name,
                        Duration::from_secs(
                            response_arguments
                                .message_expiry_interval
                                .unwrap_or_default()
                                .into(),
                        ),
                        None,
                        Some(response_arguments.command_name.clone()),
                    )));
                }
                return;
            }

            // Rounding remaining expiration time up to the nearest second
            let response_message_expiry_interval =
                if response_message_expiry_interval.as_nanos() != 0 {
                    // NOTE: We should always be able to add 1 since the seconds portion of the
                    // response_message_expiry_interval is always at least one less than its initial
                    // value when received in this block.
                    // NOTE: Rounding up to the nearest second to ensure the invoker will time out
                    // at or before the response expires.
                    response_message_expiry_interval.as_secs().saturating_add(1)
                } else {
                    response_message_expiry_interval.as_secs()
                };

            let Ok(response_message_expiry_interval) = response_message_expiry_interval.try_into()
            else {
                // Unreachable, will be smaller than u32::MAX
                log::error!(
                    "[{}][pkid: {}] Message expiry interval is too large",
                    response_arguments.command_name,
                    pkid
                );
                return;
            };

            publish_properties.message_expiry_interval = Some(response_message_expiry_interval);

            // Store cache, even if the response is an error
            if cache_not_found {
                if let Some(cached_key) = response_arguments.cached_key {
                    let cache_entry = CommandExecutorCacheEntry {
                        properties: publish_properties.clone(),
                        serialized_payload: serialized_payload.clone(),
                        expiration_time: command_expiration_time,
                    };
                    log::info!(
                        "[{}][pkid: {}] Caching response",
                        response_arguments.command_name,
                        pkid
                    );
                    cache.set(cached_key, cache_entry);
                }
            }
        } else {
            // Happens when the command expiration time was not able to be calculated.
            // We don't cache the response in this case.
            publish_properties.message_expiry_interval =
                Some(DEFAULT_MESSAGE_EXPIRY_INTERVAL_SECONDS);
        }

        // Try to publish
        match client
            .publish_with_properties(
                response_arguments.response_topic,
                QoS::AtLeastOnce,
                false,
                serialized_payload.payload,
                publish_properties,
            )
            .await
        {
            Ok(publish_completion_token) => {
                // Wait and handle puback
                match publish_completion_token.await {
                    Ok(()) => {
                        if let Some(completion_tx) = completion_tx {
                            // We ignore the error as the receiver may have been dropped indicating that the
                            // application is not interested in the completion of the publish.
                            let _ = completion_tx.send(Ok(()));
                        }
                    }
                    Err(e) => {
                        log::error!(
                            "[{}][pkid: {}] Puback error: {e}",
                            response_arguments.command_name,
                            pkid
                        );
                        if let Some(completion_tx) = completion_tx {
                            // Ignore error as receiver may have been dropped
                            let _ = completion_tx.send(Err(AIOProtocolError::new_mqtt_error(
                                Some("MQTT error on command executor response puback".to_string()),
                                Box::new(e),
                                Some(response_arguments.command_name.clone()),
                            )));
                        }
                    }
                }
            }
            Err(e) => {
                // Unreachable, we control the topic
                log::error!(
                    "[{}][pkid: {}] Client error on command executor response publish: {e}",
                    response_arguments.command_name,
                    pkid
                );
                // Notify error publishing
                if let Some(completion_tx) = completion_tx {
                    // Ignore error as receiver may have been dropped
                    let _ = completion_tx.send(Err(AIOProtocolError::new_internal_logic_error(
                        false,
                        false,
                        Some(Box::new(e)),
                        None,
                        "response_publish",
                        None,
                        Some("Error publishing response".to_string()),
                        Some(response_arguments.command_name.clone()),
                    )));
                }
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
    fn drop(&mut self) {
        // Cancel all tasks awaiting responses
        self.executor_cancellation_token.cancel();
        // Close the receiver, once dropped all remaining messages are automatically ack'd
        self.mqtt_receiver.close();

        // If the executor has not been unsubscribed, attempt to unsubscribe
        if CommandExecutorState::Subscribed == self.executor_state {
            tokio::spawn({
                let request_topic = self.request_topic_pattern.as_subscribe_topic();
                let mqtt_client = self.mqtt_client.clone();
                async move {
                    match mqtt_client.unsubscribe(request_topic.clone()).await {
                        Ok(_) => {
                            log::debug!("Unsubscribe sent on topic {request_topic}. Unsuback may still be pending.");
                        }
                        Err(e) => {
                            log::error!("Unsubscribe error on topic {request_topic}: {e}");
                        }
                    }
                }
            });
        }

        log::info!("[{}] Executor has been dropped", self.command_name);
    }
}

/// Wait on an [`AckToken`] ack to complete, if the [`CancellationToken`] is cancelled, the ack is dropped.
/// # Arguments
/// * `ack_token` - [`AckToken`] ack to wait on
/// * `executor_cancellation_token` - Cancellation token to check if the ack should be dropped
/// * `pkid` - Packet identifier of the message
async fn handle_ack(
    ack_token: AckToken,
    executor_cancellation_token: CancellationToken,
    pkid: u16,
) {
    tokio::select! {
        () = executor_cancellation_token.cancelled() => { /* executor dropped */ },
        ack_res = ack_token.ack() => {
            match ack_res {
                Ok(_) => {
                    log::info!("[pkid: {}] Acknowledged", pkid);
                },
                Err(e) => {
                    log::error!("[pkid: {}] Ack error: {e}", pkid);
                }
            }
        }
    }
}

#[cfg(test)]
mod tests {
    use azure_iot_operations_mqtt::session::{Session, SessionOptionsBuilder};
    use test_case::test_case;
    // TODO: This dependency on MqttConnectionSettingsBuilder should be removed in lieu of using a true mock
    use azure_iot_operations_mqtt::MqttConnectionSettingsBuilder;

    use super::*;
    use crate::application::ApplicationContextBuilder;
    use crate::common::{aio_protocol_error::AIOProtocolErrorKind, payload_serialize::MockPayload};

    // TODO: This should return a mock ManagedClient instead.
    // Until that's possible, need to return a Session so that the Session doesn't go out of
    // scope and render the ManagedClient unable to to be used correctly.
    fn create_session() -> Session {
        let connection_settings = MqttConnectionSettingsBuilder::default()
            .hostname("localhost")
            .client_id("test_server")
            .build()
            .unwrap();
        let session_options = SessionOptionsBuilder::default()
            .connection_settings(connection_settings)
            .build()
            .unwrap();
        Session::new(session_options).unwrap()
    }

    fn create_topic_tokens() -> HashMap<String, String> {
        HashMap::from([
            ("executorId".to_string(), "test_executor_id".to_string()),
            ("commandName".to_string(), "test_command_name".to_string()),
        ])
    }

    #[tokio::test]
    async fn test_new_defaults() {
        let session = create_session();
        let managed_client = session.create_managed_client();
        let executor_options = CommandExecutorOptionsBuilder::default()
            .request_topic_pattern("test/{commandName}/{executorId}/request")
            .command_name("test_command_name")
            .topic_token_map(create_topic_tokens())
            .build()
            .unwrap();

        let command_executor: CommandExecutor<MockPayload, MockPayload, _> = CommandExecutor::new(
            ApplicationContextBuilder::default().build().unwrap(),
            managed_client,
            executor_options,
        )
        .unwrap();

        assert_eq!(
            command_executor.request_topic_pattern.as_subscribe_topic(),
            "test/test_command_name/test_executor_id/request"
        );

        assert!(!command_executor.is_idempotent);
    }

    #[tokio::test]
    async fn test_new_override_defaults() {
        let session = create_session();
        let managed_client = session.create_managed_client();
        let executor_options = CommandExecutorOptionsBuilder::default()
            .request_topic_pattern("test/{commandName}/{executorId}/request")
            .command_name("test_command_name")
            .topic_namespace("test_namespace")
            .topic_token_map(create_topic_tokens())
            .is_idempotent(true)
            .build()
            .unwrap();

        let command_executor: CommandExecutor<MockPayload, MockPayload, _> = CommandExecutor::new(
            ApplicationContextBuilder::default().build().unwrap(),
            managed_client,
            executor_options,
        )
        .unwrap();

        assert_eq!(
            command_executor.request_topic_pattern.as_subscribe_topic(),
            "test_namespace/test/test_command_name/test_executor_id/request"
        );

        assert!(command_executor.is_idempotent);
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
            .topic_token_map(create_topic_tokens())
            .build()
            .unwrap();

        let executor: Result<CommandExecutor<MockPayload, MockPayload, _>, AIOProtocolError> =
            CommandExecutor::new(
                ApplicationContextBuilder::default().build().unwrap(),
                managed_client,
                executor_options,
            );

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
            .topic_token_map(create_topic_tokens())
            .build()
            .unwrap();

        let executor: Result<CommandExecutor<MockPayload, MockPayload, _>, AIOProtocolError> =
            CommandExecutor::new(
                ApplicationContextBuilder::default().build().unwrap(),
                managed_client,
                executor_options,
            );

        match executor {
            Err(e) => {
                assert_eq!(e.kind, AIOProtocolErrorKind::ConfigurationInvalid);
                assert!(!e.in_application);
                assert!(e.is_shallow);
                assert!(!e.is_remote);
                assert_eq!(e.http_status_code, None);
                assert_eq!(
                    e.property_name,
                    Some("executor_options.request_topic_pattern".to_string())
                );
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
            .topic_token_map(create_topic_tokens())
            .build()
            .unwrap();

        let executor: Result<CommandExecutor<MockPayload, MockPayload, _>, AIOProtocolError> =
            CommandExecutor::new(
                ApplicationContextBuilder::default().build().unwrap(),
                managed_client,
                executor_options,
            );
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

    #[tokio::test]
    async fn test_shutdown_without_subscribe() {
        let session = create_session();
        let executor_options = CommandExecutorOptionsBuilder::default()
            .request_topic_pattern("test/request")
            .command_name("test_command_name")
            .build()
            .unwrap();
        let mut command_executor: CommandExecutor<MockPayload, MockPayload, _> =
            CommandExecutor::new(
                ApplicationContextBuilder::default().build().unwrap(),
                session.create_managed_client(),
                executor_options,
            )
            .unwrap();
        assert!(command_executor.shutdown().await.is_ok());
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
        let resp_builder = binding.payload(mock_response_payload);
        match resp_builder {
            Err(e) => {
                assert_eq!(e.kind, AIOProtocolErrorKind::PayloadInvalid);
            }
            Ok(_) => {
                panic!("Expected error");
            }
        }
    }
    #[test]
    fn test_response_serialization_bad_content_type_error() {
        let mut mock_response_payload = MockPayload::new();
        mock_response_payload
            .expect_serialize()
            .returning(|| {
                Ok(SerializedPayload {
                    payload: Vec::new(),
                    content_type: "application/json\u{0000}".to_string(),
                    format_indicator: FormatIndicator::Utf8EncodedCharacterData,
                })
            })
            .times(1);

        let mut binding = CommandResponseBuilder::default();
        let resp_builder = binding.payload(mock_response_payload);
        match resp_builder {
            Err(e) => {
                assert_eq!(e.kind, AIOProtocolErrorKind::ConfigurationInvalid);
                assert!(!e.in_application);
                assert!(e.is_shallow);
                assert!(!e.is_remote);
                assert_eq!(e.http_status_code, None);
                assert_eq!(e.property_name, Some("content_type".to_string()));
                assert!(
                    e.property_value == Some(Value::String("application/json\u{0000}".to_string()))
                );
            }
            Ok(_) => {
                panic!("Expected error");
            }
        }
    }

    #[tokio::test]
    async fn test_cache_not_found() {
        let cache = CommandExecutorCache(Arc::new(Mutex::new(HashMap::new())));
        let key = CommandExecutorCacheKey {
            response_topic: String::from("test_response_topic"),
            correlation_data: Bytes::from("test_correlation_data"),
        };
        let status = cache.get(&key);
        assert_eq!(status, CommandExecutorCacheEntryStatus::NotFound);
    }

    #[tokio::test]
    async fn test_cache_found() {
        let cache = CommandExecutorCache(Arc::new(Mutex::new(HashMap::new())));
        let key = CommandExecutorCacheKey {
            response_topic: String::from("test_response_topic"),
            correlation_data: Bytes::from("test_correlation_data"),
        };
        let entry = CommandExecutorCacheEntry {
            serialized_payload: SerializedPayload {
                payload: Bytes::from("test_payload").to_vec(),
                content_type: "application/json".to_string(),
                format_indicator: FormatIndicator::Utf8EncodedCharacterData,
            },
            properties: PublishProperties::default(),
            expiration_time: Instant::now() + Duration::from_secs(60),
        };
        cache.set(key.clone(), entry.clone());
        let status = cache.get(&key);
        assert_eq!(status, CommandExecutorCacheEntryStatus::Cached(entry));
    }

    #[tokio::test]
    async fn test_cache_expired() {
        let cache = CommandExecutorCache(Arc::new(Mutex::new(HashMap::new())));
        let key = CommandExecutorCacheKey {
            response_topic: String::from("test_response_topic"),
            correlation_data: Bytes::from("test_correlation_data"),
        };
        let entry = CommandExecutorCacheEntry {
            serialized_payload: SerializedPayload {
                payload: Bytes::from("test_payload").to_vec(),
                content_type: "application/json".to_string(),
                format_indicator: FormatIndicator::Utf8EncodedCharacterData,
            },
            properties: PublishProperties::default(),
            expiration_time: Instant::now() - Duration::from_secs(60),
        };
        cache.set(key.clone(), entry);
        let status = cache.get(&key);
        assert_eq!(status, CommandExecutorCacheEntryStatus::Expired);

        // Set a new entry and check if the expired entry is deleted
        let new_entry = CommandExecutorCacheEntry {
            serialized_payload: SerializedPayload {
                payload: Bytes::from("new_test_payload").to_vec(),
                content_type: "application/json".to_string(),
                format_indicator: FormatIndicator::Utf8EncodedCharacterData,
            },
            properties: PublishProperties::default(),
            expiration_time: Instant::now() + Duration::from_secs(60),
        };
        // The cache should never see another entry with the same key, this is for testing purposes only.
        cache.set(key.clone(), new_entry.clone());

        let new_status = cache.get(&key);
        assert_eq!(
            new_status,
            CommandExecutorCacheEntryStatus::Cached(new_entry)
        );
    }

    #[tokio::test]
    async fn test_cache_expired_with_different_key_set() {
        let cache = CommandExecutorCache(Arc::new(Mutex::new(HashMap::new())));
        let key = CommandExecutorCacheKey {
            response_topic: String::from("test_response_topic"),
            correlation_data: Bytes::from("test_correlation_data"),
        };
        let entry = CommandExecutorCacheEntry {
            serialized_payload: SerializedPayload {
                payload: Bytes::from("test_payload").to_vec(),
                content_type: "application/json".to_string(),
                format_indicator: FormatIndicator::Utf8EncodedCharacterData,
            },
            properties: PublishProperties::default(),
            expiration_time: Instant::now() - Duration::from_secs(60),
        };
        cache.set(key.clone(), entry);
        let status = cache.get(&key);
        assert_eq!(status, CommandExecutorCacheEntryStatus::Expired);

        // Set a new entry with a different key and check if the expired entry is deleted
        let new_key = CommandExecutorCacheKey {
            response_topic: String::from("new_test_response_topic"),
            correlation_data: Bytes::from("new_test_correlation_data"),
        };
        let new_entry = CommandExecutorCacheEntry {
            serialized_payload: SerializedPayload {
                payload: Bytes::from("new_test_payload").to_vec(),
                content_type: "application/json".to_string(),
                format_indicator: FormatIndicator::Utf8EncodedCharacterData,
            },
            properties: PublishProperties::default(),
            expiration_time: Instant::now() + Duration::from_secs(60),
        };
        cache.set(new_key.clone(), new_entry.clone());

        let status = cache.get(&key);
        assert_eq!(status, CommandExecutorCacheEntryStatus::NotFound);
        let status = cache.get(&new_key);
        assert_eq!(status, CommandExecutorCacheEntryStatus::Cached(new_entry));
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
//
// Tests failure:
//   if an error response is published, the original request is acked
//   response topic is invalid and command response is not published and original request is acked
//   correlation data, invoker id, or payload are missing and error response is published and original request is acked
//   if payload format indicator, content type, and timestamp are present and invalid, error response is published and original request is acked
//
// Test cases for response processing
// Tests success:
//    a command response is received and successfully published, the original request is acked
//    response payload is serialized and published
//    an empty response payload has a status code of NoContent
//
// Tests failure:
//    an error occurs while processing the command response, an error response is sent and the original request is acked
//    response payload is not serialized and an error response is sent and the original request is acked
//
// Test cases for timeout
// Tests success:
//   a command request is received and a response is published before the command expiration time, the original request is acked
//   a command request is received and a response is not published after the command expiration time, the original request is acked
// Tests failure:
//   a command request is received and the command expiration time cannot be calculated, an error response is sent to the invoker and executor application and the original request is acked
