// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

use std::str::FromStr;
use std::sync::{Arc, Mutex};
use std::{collections::HashMap, marker::PhantomData, time::Duration};

use azure_iot_operations_mqtt::{
    aio::cloud_event as aio_cloud_event,
    session::{SessionManagedClient, SessionPubReceiver},
};
use azure_iot_operations_mqtt::{
    control_packet::{PublishProperties, QoS, TopicFilter, TopicName},
    token::AckToken,
};
use bytes::Bytes;
use chrono::{DateTime, Utc};
use tokio::sync::oneshot;
use tokio::time::{Instant, timeout};
use tokio_util::sync::{CancellationToken, DropGuard};

use crate::{
    ProtocolVersion,
    application::{ApplicationContext, ApplicationHybridLogicalClock},
    common::{
        aio_protocol_error::{AIOProtocolError, Value},
        cloud_event as protocol_cloud_event,
        hybrid_logical_clock::{HLCErrorKind, HybridLogicalClock},
        is_invalid_utf8,
        payload_serialize::{
            DeserializationError, FormatIndicator, PayloadSerialize, SerializedPayload,
        },
        topic_processor::{TopicPattern, contains_invalid_char, is_valid_replacement},
        user_properties::{PARTITION_KEY, UserProperty, validate_user_properties},
    },
    rpc_command::{
        DEFAULT_RPC_COMMAND_PROTOCOL_VERSION, DEFAULT_RPC_RESPONSE_CLOUD_EVENT_EVENT_TYPE,
        RPC_COMMAND_PROTOCOL_VERSION, StatusCode,
    },
    supported_protocol_major_versions_to_string,
};

/// Default message expiry interval only for when the message expiry interval is not present
const DEFAULT_MESSAGE_EXPIRY_INTERVAL_SECONDS: u32 = 10;

/// Additional time in seconds to extend cache entry expiration beyond the command expiration time
const CACHE_EXPIRY_BUFFER_SECONDS: u64 = 60;

/// Message for when expiration time is unable to be calculated, internal logic error
const INTERNAL_LOGIC_EXPIRATION_ERROR: &str =
    "Internal logic error, unable to calculate command expiration time";

const SUPPORTED_PROTOCOL_VERSIONS: &[u16] = &[1];

/// Struct to hold response arguments
struct ResponseArguments {
    command_name: String,
    response_topic: TopicName,
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
    cached_key: Option<CacheKey>,
    cache_lookup_result: CacheLookupResult,
}

/// Command Executor Request struct.
/// Used by the [`Executor`]
///
/// If dropped, executor will send an error response to the invoker
pub struct Request<TReq, TResp>
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
    response_tx: oneshot::Sender<Response<TResp>>,
    publish_completion_rx: oneshot::Receiver<Result<(), AIOProtocolError>>,
}

impl<TReq, TResp> Request<TReq, TResp>
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
    /// * `response` - The [`Response`] to send.
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
    pub async fn complete(self, response: Response<TResp>) -> Result<(), AIOProtocolError> {
        // We can ignore the error here. If the receiver of the response is dropped it may be
        // because the executor is shutting down in which case the receive below will fail.
        // If the executor is not shutting down, the receive below will succeed and we'll receive a
        // timeout error since that is the only possible error at this point.
        let _ = self.response_tx.send(response);

        self.publish_completion_rx
            .await
            .map_err(|_| Self::create_cancellation_error(self.command_name))?
    }

    fn create_cancellation_error(command_name: String) -> AIOProtocolError {
        AIOProtocolError::new_cancellation_error(
            false,
            None,
            Some(
                "Command Executor has been shutdown and can no longer respond to commands"
                    .to_string(),
            ),
            Some(command_name),
        )
    }

    /// Check if the command response is no longer expected.
    ///
    /// Returns true if the response is no longer expected, otherwise returns false.
    pub fn is_cancelled(&self) -> bool {
        self.response_tx.is_closed()
    }
}

/// Cloud Event struct derived from the Command Request.
pub type RequestCloudEvent = aio_cloud_event::CloudEvent;
/// Error when parsing a Cloud Event from a Request
pub type RequestCloudEventParseError = aio_cloud_event::CloudEventParseError;

/// Parse a [`RequestCloudEvent`] from a [`Request`].
/// Note that this will return an error if the [`Request`] does not contain the required fields for a [`RequestCloudEvent`].
///
/// # Errors
/// [`RequestCloudEventParseError`] if
/// - the [`Request`] does not contain the required fields for a [`RequestCloudEvent`].
/// - any of the field values are not valid for a [`RequestCloudEvent`].
pub fn cloud_event_from_request<TReq: PayloadSerialize, TResp: PayloadSerialize>(
    request: &Request<TReq, TResp>,
) -> Result<RequestCloudEvent, RequestCloudEventParseError> {
    RequestCloudEvent::try_from((&request.custom_user_data, request.content_type.as_deref()))
}

/// Command Executor Response struct.
/// Used by the [`Executor`]
#[derive(Builder, Clone, Debug)]
#[builder(setter(into), build_fn(validate = "Self::validate"))]
pub struct Response<TResp>
where
    TResp: PayloadSerialize,
{
    /// Payload of the command response.
    #[builder(setter(custom))]
    serialized_payload: SerializedPayload,
    /// Strongly link `Response` with type `TResp`
    #[builder(private)]
    payload_type: PhantomData<TResp>,
    /// Custom user data set as custom MQTT User Properties on the response message.
    /// Used to pass additional metadata to the invoker.
    /// Default is an empty vector.
    #[builder(default)]
    custom_user_data: Vec<(String, String)>,
    /// Cloud event of the response.
    #[builder(default = "None")]
    cloud_event: Option<ResponseCloudEvent>,
}

/// Cloud Event struct used for the Command Response.
///
/// Implements the Cloud Events spec 1.0 for the command executor.
/// See [CloudEvents Spec](https://github.com/cloudevents/spec/blob/main/cloudevents/spec.md).
#[derive(Clone, Debug)]
pub struct ResponseCloudEvent(protocol_cloud_event::CloudEvent);

/// Builder for [`ResponseCloudEvent`].
#[derive(Clone)]
pub struct ResponseCloudEventBuilder(protocol_cloud_event::CloudEventBuilder);

/// Error type for [`ResponseCloudEventBuilder`]
#[derive(Debug)]
#[non_exhaustive]
pub enum ResponseCloudEventBuilderError {
    /// Uninitialized field
    UninitializedField(&'static str),
    /// Custom validation error
    ValidationError(String),
}

impl std::error::Error for ResponseCloudEventBuilderError {}
impl std::fmt::Display for ResponseCloudEventBuilderError {
    fn fmt(&self, f: &mut std::fmt::Formatter<'_>) -> std::fmt::Result {
        match self {
            ResponseCloudEventBuilderError::UninitializedField(field_name) => {
                write!(f, "Uninitialized field: {field_name}")
            }
            ResponseCloudEventBuilderError::ValidationError(err_msg) => {
                write!(f, "Validation error: {err_msg}")
            }
        }
    }
}

impl From<protocol_cloud_event::CloudEventBuilderError> for ResponseCloudEventBuilderError {
    fn from(value: protocol_cloud_event::CloudEventBuilderError) -> Self {
        match value {
            protocol_cloud_event::CloudEventBuilderError::UninitializedField(field_name) => {
                ResponseCloudEventBuilderError::UninitializedField(field_name)
            }
            protocol_cloud_event::CloudEventBuilderError::ValidationError(err_msg) => {
                ResponseCloudEventBuilderError::ValidationError(err_msg)
            }
        }
    }
}
impl Default for ResponseCloudEventBuilder {
    fn default() -> Self {
        Self(protocol_cloud_event::CloudEventBuilder::new(
            DEFAULT_RPC_RESPONSE_CLOUD_EVENT_EVENT_TYPE.to_string(),
        ))
    }
}

impl ResponseCloudEventBuilder {
    /// Builds a new [`ResponseCloudEvent`].
    /// # Errors
    /// If a required field has not been initialized.
    pub fn build(&self) -> Result<ResponseCloudEvent, ResponseCloudEventBuilderError> {
        Ok(ResponseCloudEvent(
            protocol_cloud_event::CloudEventBuilder::build(&self.0)?,
        ))
    }
    /// Identifies the context in which an event happened. Often this will include information such
    /// as the type of the event source, the organization publishing the event or the process that
    /// produced the event. The exact syntax and semantics behind the data encoded in the URI is
    /// defined by the event producer.
    pub fn source<VALUE: Into<String>>(&mut self, value: VALUE) -> &mut Self {
        self.0.source(value);
        self
    }
    /// The version of the cloud events specification which the event uses. This enables the
    /// interpretation of the context. Compliant event producers MUST use a value of 1.0 when
    /// referring to this version of the specification.
    pub fn spec_version<VALUE: Into<String>>(&mut self, value: VALUE) -> &mut Self {
        self.0.spec_version(value);
        self
    }
    /// Contains a value describing the type of event related to the originating occurrence. Often
    /// this attribute is used for routing, observability, policy enforcement, etc. The format of
    /// this is producer defined and might include information such as the version of the type.
    pub fn event_type<VALUE: Into<String>>(&mut self, value: VALUE) -> &mut Self {
        self.0.event_type(value);
        self
    }
    /// Identifies the schema that data adheres to. Incompatible changes to the schema SHOULD be
    /// reflected by a different URI.
    pub fn data_schema<VALUE: Into<Option<String>>>(&mut self, value: VALUE) -> &mut Self {
        self.0.data_schema(value);
        self
    }
    /// Identifies the event. Producers MUST ensure that source + id is unique for each distinct
    /// event. If a duplicate event is re-sent (e.g. due to a network error) it MAY have the same
    /// id. Consumers MAY assume that Events with identical source and id are duplicates.
    pub fn id<VALUE: Into<String>>(&mut self, value: VALUE) -> &mut Self {
        self.0.id(value);
        self
    }
    /// Timestamp of when the occurrence happened. If the time of the occurrence cannot be
    /// determined then this attribute MAY be set to some other time (such as the current time) by
    /// the cloud event producer, however all producers for the same source MUST be consistent in
    /// this respect. In other words, either they all use the actual time of the occurrence or they
    /// all use the same algorithm to determine the value used.
    pub fn time<VALUE: Into<Option<DateTime<Utc>>>>(&mut self, value: VALUE) -> &mut Self {
        self.0.time(value);
        self
    }
    /// Identifies the subject of the event in the context of the event producer (identified by
    /// source). In publish-subscribe scenarios, a subscriber will typically subscribe to events
    /// emitted by a source, but the source identifier alone might not be sufficient as a qualifier
    /// for any specific event if the source context has internal sub-structure.
    pub fn subject<VALUE: Into<protocol_cloud_event::CloudEventSubject>>(
        &mut self,
        value: VALUE,
    ) -> &mut Self {
        self.0.subject(value);
        self
    }
}

impl<TResp: PayloadSerialize> ResponseBuilder<TResp> {
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
                Some("Payload serialization error".to_string()),
                None,
            )),
            Ok(serialized_payload) => {
                // Validate content type of command response is valid UTF-8
                if is_invalid_utf8(&serialized_payload.content_type) {
                    return Err(AIOProtocolError::new_configuration_invalid_error(
                        None,
                        "content_type",
                        Value::String(serialized_payload.content_type.clone()),
                        Some(format!(
                            "Content type '{}' of command response is not valid UTF-8",
                            serialized_payload.content_type
                        )),
                        None,
                    ));
                }
                self.serialized_payload = Some(serialized_payload);
                self.payload_type = Some(PhantomData);
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
            for (key, _) in custom_user_data {
                if aio_cloud_event::CloudEventFields::from_str(key).is_ok() {
                    return Err(format!(
                        "Invalid user data property '{key}' is a reserved Cloud Event key"
                    ));
                }
            }
            validate_user_properties(custom_user_data)?;
        }
        // If there's a cloud event, make sure the content type is valid for the cloud event spec version
        if let Some(Some(cloud_event)) = &self.cloud_event
            && let Some(serialized_payload) = &self.serialized_payload
        {
            aio_cloud_event::CloudEventFields::DataContentType.validate(
                &serialized_payload.content_type,
                &cloud_event.0.spec_version,
            )?;
        }
        Ok(())
    }
}

/// Helper function to add user-defined application error headers to a [`Vec<(String, String)>`] to be used as the `custom_user_data` on the [`Response`].
///
/// `custom_user_data` can be an empty Vec or an existing Vec of custom user data. This function will add the application error headers to this Vec.
/// `application_error_code` required to be a non-empty `String`.
/// `application_error_payload` is optional and can be an empty `String`, in which case it is ignored and not added to `custom_user_data`. It is conventionally, but not necessarily, a stringified JSON object/value/array.
///
/// Returns `Ok(())` if the properties are added to `custom_user_data`. If an error is returned, `custom_user_data` won't get modified.
///
/// # Errors
/// Returns an Error with the `String` "`application_error_code` cannot be empty" if `application_error_code` is an empty string.
pub fn application_error_headers(
    custom_user_data: &mut Vec<(String, String)>,
    application_error_code: String,
    application_error_payload: String,
) -> Result<(), String> {
    const APPLICATION_ERROR_CODE_HEADER: &str = "AppErrCode";
    const APPLICATION_ERROR_PAYLOAD_HEADER: &str = "AppErrPayload";

    if application_error_code.trim().is_empty() {
        return Err("application_error_code cannot be empty".into());
    }

    custom_user_data.push((APPLICATION_ERROR_CODE_HEADER.into(), application_error_code));

    if !application_error_payload.trim().is_empty() {
        custom_user_data.push((
            APPLICATION_ERROR_PAYLOAD_HEADER.into(),
            application_error_payload,
        ));
    }

    Ok(())
}

/// Command Executor Cache Key struct.
///
/// Used to uniquely identify a command request.
#[derive(Eq, Hash, PartialEq, Clone)]
struct CacheKey {
    response_topic: TopicName,
    correlation_data: Bytes,
}

/// Command Executor Cache Entry struct.
#[derive(Clone, Debug)]
#[allow(clippy::large_enum_variant)]
enum CacheEntry {
    /// Indicates that the response is completed and cached
    Cached {
        serialized_payload: SerializedPayload,
        properties: PublishProperties,
        expiration_time: Instant,
    },
    /// Indicates that the request is in progress
    InProgress {
        /// Used for deduped requests to await the completion of the original request in order to ack
        processing_cancellation_token: CancellationToken,
    },
}

/// Command Executor Cache Lookup Result enum
///
/// Used to indicate the status of a cache entry.
#[derive(Debug)]
enum CacheLookupResult {
    /// The cache entry is cached and has not expired
    Cached {
        serialized_payload: SerializedPayload,
        properties: PublishProperties,
        response_message_expiry_interval: u32,
    },
    /// The cache entry is in progress
    InProgress(CancellationToken),
    /// The cache entry is not found
    NotFound,
}

/// The Command Executor Cache struct.
///
/// Used to cache command responses and determine if a command request is a duplicate.
#[derive(Clone)]
struct Cache(Arc<Mutex<HashMap<CacheKey, CacheEntry>>>);

impl Cache {
    /// Get the status of a cache entry from the [`Cache`].
    ///
    /// # Arguments
    /// `key` - The cache key to look for.
    ///
    /// Returns a [`CacheLookupResult`] indicating the result of the get.
    fn get(&self, key: &CacheKey) -> CacheLookupResult {
        let cache = self.0.lock().unwrap();

        match cache.get(key) {
            Some(entry) => {
                match entry {
                    CacheEntry::Cached {
                        serialized_payload,
                        properties,
                        expiration_time,
                    } => {
                        let response_message_expiry_interval =
                            get_response_message_expiry_interval(*expiration_time);

                        // Check if the entry has expired, return the entry if not
                        if let Some(response_message_expiry_interval) =
                            response_message_expiry_interval
                        {
                            CacheLookupResult::Cached {
                                serialized_payload: serialized_payload.clone(),
                                properties: properties.clone(),
                                response_message_expiry_interval,
                            }
                        } else {
                            // Entry has expired, return not found
                            CacheLookupResult::NotFound
                        }
                    }
                    CacheEntry::InProgress {
                        processing_cancellation_token,
                    } => {
                        // If the entry is in progress, it means it has not been completed yet
                        // any duplicate requests should await the cancellation token to complete
                        // their acknowledgement
                        CacheLookupResult::InProgress(processing_cancellation_token.clone())
                    }
                }
            }
            None => CacheLookupResult::NotFound,
        }
    }

    /// Set a cache entry in the cache. Also removes expired cache entries.
    ///
    /// # Arguments
    /// `key` - The cache key to set the cache entry for.
    /// `entry` - The cache entry to set.
    fn set(&self, key: CacheKey, entry: CacheEntry) {
        let mut cache = self.0.lock().unwrap();
        cache.retain(|_, entry| {
            match entry {
                CacheEntry::Cached {
                    expiration_time, ..
                } => {
                    // Retain only non-expired entries
                    expiration_time.elapsed().is_zero()
                }
                CacheEntry::InProgress {
                    processing_cancellation_token,
                } => {
                    // If an entry is in progress and its processing cancellation token is cancelled
                    // it means it timed out or the application dropped it, so it can be safely
                    // removed. If it didn't time out it would have been converted to a Cached entry.
                    !processing_cancellation_token.is_cancelled()
                }
            }
        });
        cache.insert(key, entry);
    }
}

/// Command Executor Options struct
#[allow(unused)]
#[derive(Builder, Clone)]
#[builder(setter(into, strip_option))]
pub struct Options {
    /// Topic pattern for the command request.
    /// Must align with [topic-structure.md](https://github.com/Azure/iot-operations-sdks/blob/main/doc/reference/topic-structure.md)
    request_topic_pattern: String,
    /// Command name
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
/// # use azure_iot_operations_mqtt::aio::connection_settings::MqttConnectionSettingsBuilder;
/// # use azure_iot_operations_mqtt::session::{Session, SessionOptionsBuilder};
/// # use azure_iot_operations_protocol::rpc_command;
/// # use azure_iot_operations_protocol::application::ApplicationContextBuilder;
/// # let mut connection_settings = MqttConnectionSettingsBuilder::default()
/// #     .client_id("test_server")
/// #     .hostname("localhost")
/// #     .tcp_port(1883u16)
/// #     .build().unwrap();
/// # let mut session_options = SessionOptionsBuilder::default()
/// #     .connection_settings(connection_settings)
/// #     .build().unwrap();
/// # let mqtt_session = Session::new(session_options).unwrap();
/// # let application_context = ApplicationContextBuilder::default().build().unwrap();;
/// let executor_options = rpc_command::executor::OptionsBuilder::default()
///   .command_name("test_command")
///   .request_topic_pattern("test/request")
///   .build().unwrap();
/// # tokio_test::block_on(async {
/// let mut executor: rpc_command::Executor<Vec<u8>, Vec<u8>> = rpc_command::Executor::new(application_context, mqtt_session.create_managed_client(), executor_options).unwrap();
/// // let request = executor.recv().await.unwrap();
/// // let response = rpc_command::executor::ResponseBuilder::default()
///  // .payload(Vec::new()).unwrap()
///  // .build().unwrap();
/// // let request.complete(response).await.unwrap();
/// # });
/// ```
#[allow(unused)]
pub struct Executor<TReq, TResp>
where
    TReq: PayloadSerialize + Send + 'static,
    TResp: PayloadSerialize + Send + 'static,
{
    // Static properties of the executor
    application_hlc: Arc<ApplicationHybridLogicalClock>,
    mqtt_client: SessionManagedClient,
    mqtt_receiver: SessionPubReceiver,
    is_idempotent: bool,
    request_topic_pattern: TopicPattern,
    request_topic_filter: TopicFilter,
    command_name: String,
    request_payload_type: PhantomData<TReq>,
    response_payload_type: PhantomData<TResp>,
    cache: Cache,
    // Describes state
    state: State,
    // Information to manage state
    cancellation_token: CancellationToken,
}

/// Describes state of executor
#[derive(PartialEq)]
enum State {
    New,
    Subscribed,
    ShutdownSuccessful,
}

/// Implementation of Command Executor.
impl<TReq, TResp> Executor<TReq, TResp>
where
    TReq: PayloadSerialize + Send + 'static,
    TResp: PayloadSerialize + Send + 'static,
{
    /// Create a new [`Executor`].
    ///
    /// # Arguments
    /// * `application_context` - [`ApplicationContext`] that the command executor is part of.
    /// * `client` - The MQTT client to use for communication.
    /// * `executor_options` - Configuration options.
    ///
    /// Returns Ok([`Executor`]) on success, otherwise returns [`AIOProtocolError`].
    ///
    /// # Errors
    /// [`AIOProtocolError`] of kind [`ConfigurationInvalid`](crate::common::aio_protocol_error::AIOProtocolErrorKind::ConfigurationInvalid) if:
    /// - [`command_name`](OptionsBuilder::command_name) is empty, whitespace or invalid
    /// - [`request_topic_pattern`](OptionsBuilder::request_topic_pattern),
    ///   [`topic_namespace`](OptionsBuilder::topic_namespace)
    ///   are Some and invalid or contain a token with no valid replacement
    /// - [`topic_token_map`](OptionsBuilder::topic_token_map) is not empty and contains invalid key(s) and/or token(s)
    pub fn new(
        application_context: ApplicationContext,
        client: SessionManagedClient,
        executor_options: Options,
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

        let request_topic_filter = request_topic_pattern.as_subscribe_topic().map_err(|e| {
            AIOProtocolError::config_invalid_from_topic_pattern_error(
                e,
                "executor_options.request_topic_pattern",
            )
        })?;

        // Get pub sub and receiver from the mqtt session
        let mqtt_receiver = client.create_filtered_pub_receiver(request_topic_filter.clone());

        // Create Command executor
        Ok(Executor {
            application_hlc: application_context.application_hlc,
            mqtt_client: client,
            mqtt_receiver,
            is_idempotent: executor_options.is_idempotent,
            request_topic_pattern,
            request_topic_filter,
            command_name: executor_options.command_name,
            request_payload_type: PhantomData,
            response_payload_type: PhantomData,
            cache: Cache(Arc::new(Mutex::new(HashMap::new()))),
            state: State::New,
            cancellation_token: CancellationToken::new(),
        })
    }

    /// Shutdown the [`Executor`]. Unsubscribes from the request topic.
    ///
    /// Note: If this method is called, the [`Executor`] will no longer receive commands
    /// from the MQTT client, any command requests that have not been processed can still be received
    /// by the executor. If the method returns an error, it may be called again to attempt the unsubscribe again.
    ///
    /// Returns Ok(()) on success, otherwise returns [`AIOProtocolError`].
    /// # Errors
    /// [`AIOProtocolError`] of kind [`ClientError`](crate::common::aio_protocol_error::AIOProtocolErrorKind::ClientError) if the unsubscribe fails or if the unsuback reason code doesn't indicate success.
    pub async fn shutdown(&mut self) -> Result<(), AIOProtocolError> {
        // Close the receiver, no longer receive messages
        self.mqtt_receiver.close();

        match self.state {
            State::New | State::ShutdownSuccessful => {
                // If subscribe has not been called or shutdown was successful, do not unsubscribe
                self.state = State::ShutdownSuccessful;
            }
            State::Subscribed => {
                let unsubscribe_result = self
                    .mqtt_client
                    .unsubscribe(
                        self.request_topic_filter.clone(),
                        azure_iot_operations_mqtt::control_packet::UnsubscribeProperties::default(),
                    )
                    .await;

                match unsubscribe_result {
                    Ok(unsub_ct) => match unsub_ct.await {
                        Ok(unsuback) => match unsuback.as_result() {
                            Ok(()) => {
                                self.state = State::ShutdownSuccessful;
                            }
                            Err(e) => {
                                log::error!(
                                    "[{}] Executor unsuback error: {unsuback:?}",
                                    self.command_name
                                );
                                return Err(AIOProtocolError::new_mqtt_error(
                                    Some("MQTT error on command executor unsuback".to_string()),
                                    Box::new(e),
                                    Some(self.command_name.clone()),
                                ));
                            }
                        },
                        Err(e) => {
                            log::error!(
                                "[{}] Executor unsubscribe completion error: {e}",
                                self.command_name
                            );
                            return Err(AIOProtocolError::new_mqtt_error(
                                Some("MQTT error on command executor unsubscribe".to_string()),
                                Box::new(e),
                                Some(self.command_name.clone()),
                            ));
                        }
                    },
                    Err(e) => {
                        log::error!(
                            "[{}] Client error while unsubscribing in Executor: {e}",
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
        log::info!("[{}] Executor Shutdown", self.command_name);
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
                self.request_topic_filter.clone(),
                QoS::AtLeastOnce,
                false,
                azure_iot_operations_mqtt::control_packet::RetainOptions::default(),
                azure_iot_operations_mqtt::control_packet::SubscribeProperties::default(),
            )
            .await;

        match subscribe_result {
            Ok(sub_ct) => match sub_ct.await {
                Ok(suback) => {
                    suback.as_result().map_err(|e| {
                        log::error!("[{}] Executor suback error: {suback:?}", self.command_name);
                        AIOProtocolError::new_mqtt_error(
                            Some("MQTT error on command executor suback".to_string()),
                            Box::new(e),
                            Some(self.command_name.clone()),
                        )
                    })?;
                }
                Err(e) => {
                    log::error!(
                        "[{}] Executor subscribe completion error: {e}",
                        self.command_name
                    );
                    return Err(AIOProtocolError::new_mqtt_error(
                        Some("MQTT error on command executor subscribe".to_string()),
                        Box::new(e),
                        Some(self.command_name.clone()),
                    ));
                }
            },
            Err(e) => {
                log::error!(
                    "[{}] Client error while subscribing in Executor: {e}",
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
    /// - Returns Ok([`Request`]) on success
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
    pub async fn recv(&mut self) -> Option<Result<Request<TReq, TResp>, AIOProtocolError>> {
        // Subscribe to the request topic if not already subscribed
        if State::New == self.state {
            if let Err(e) = self.try_subscribe().await {
                return Some(Err(e));
            }
            self.state = State::Subscribed;
        }

        loop {
            match self.mqtt_receiver.recv_manual_ack().await {
                Some((m, ack_token)) => {
                    let Some(ack_token) = ack_token else {
                        // No ack token, ignore the message. This should never happen as the executor
                        // should always receive QoS 1 messages that have an ack token.
                        log::warn!(
                            "[{}] Received command request without ack token",
                            self.command_name
                        );
                        continue;
                    };
                    let pkid = match m.qos {
                        azure_iot_operations_mqtt::control_packet::DeliveryQoS::AtMostOnce
                        | azure_iot_operations_mqtt::control_packet::DeliveryQoS::ExactlyOnce(_) => {
                            // This should never happen as the executor should always receive QoS 1 messages
                            log::warn!(
                                "[{}] Received non QoS 1 command request",
                                self.command_name
                            );
                            continue;
                        }
                        azure_iot_operations_mqtt::control_packet::DeliveryQoS::AtLeastOnce(
                            delivery_info,
                        ) => delivery_info.packet_identifier.get(),
                    };
                    // Process the request
                    log::debug!("[{}][pkid: {}] Received request", self.command_name, pkid);
                    let message_received_time = Instant::now();

                    // Create a cancellation token to be used to track cache entry state, once the
                    // entry has gone from InProgress to Cached or removed, this token will be cancelled
                    let processing_cancellation_token = CancellationToken::new();
                    let processing_cancellation_token_clone = processing_cancellation_token.clone();
                    // This drop guard will stay alive until the request processing is complete,
                    // whether a timeout occurs, or we have an ok or error response.
                    let processing_drop_guard = processing_cancellation_token.drop_guard();

                    // Clone properties
                    let properties = m.properties;

                    // Get response topic
                    let response_topic = if let Some(rt) = properties.response_topic {
                        if !is_valid_replacement(rt.as_str()) {
                            log::warn!(
                                "[{}][pkid: {}] Response topic invalid, command response will not be published",
                                self.command_name,
                                pkid
                            );
                            tokio::task::spawn({
                                let executor_cancellation_token_clone =
                                    self.cancellation_token.clone();
                                async move {
                                    handle_ack(ack_token, executor_cancellation_token_clone, pkid)
                                        .await;
                                }
                            });
                            continue;
                        }
                        rt
                    } else {
                        log::warn!(
                            "[{}][pkid: {}] Response topic missing, command response will not be published",
                            self.command_name,
                            pkid
                        );
                        tokio::task::spawn({
                            let executor_cancellation_token_clone = self.cancellation_token.clone();
                            async move {
                                handle_ack(ack_token, executor_cancellation_token_clone, pkid)
                                    .await;
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
                        cache_lookup_result: CacheLookupResult::NotFound,
                    };

                    // Get message expiry interval
                    let command_expiration_time = match properties.message_expiry_interval {
                        Some(ct) => {
                            response_arguments.message_expiry_interval = Some(ct);
                            message_received_time.checked_add(Duration::from_secs(ct.into()))
                        }
                        _ => message_received_time.checked_add(Duration::from_secs(u64::from(
                            DEFAULT_MESSAGE_EXPIRY_INTERVAL_SECONDS,
                        ))),
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
                            response_arguments.cached_key = Some(CacheKey {
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
                                response_arguments.invalid_property_value =
                                    Some(correlation_data_str);
                            } else { /* Ignore */
                            }
                            response_arguments.correlation_data = Some(correlation_data);
                        }
                    } else {
                        response_arguments.status_code = StatusCode::BadRequest;
                        response_arguments.status_message =
                            Some("Correlation data missing".to_string());
                        response_arguments.invalid_property_name =
                            Some("Correlation Data".to_string());
                    }

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
                        response_arguments.cache_lookup_result = self.cache.get(cache_key);

                        if !matches!(
                            response_arguments.cache_lookup_result,
                            CacheLookupResult::NotFound
                        ) {
                            // This means there is an entry for this correlation ID so either we
                            // have a cached response or the request is already being processed.
                            break 'process_request;
                        }

                        // If there is no entry for this correlation ID we register it as in progress
                        self.cache.set(
                            cache_key.clone(),
                            CacheEntry::InProgress {
                                processing_cancellation_token: processing_cancellation_token_clone,
                            },
                        );

                        // unused beyond validation, but may be used in the future to determine how to handle other fields. Can be moved higher in the future if needed.
                        let mut request_protocol_version = DEFAULT_RPC_COMMAND_PROTOCOL_VERSION; // assume default version if none is provided
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
                                    Some(protocol_version.clone());
                                break 'process_request;
                            }
                        }
                        // Check that the version (or the default version if one isn't provided) is supported
                        if !request_protocol_version.is_supported(SUPPORTED_PROTOCOL_VERSIONS) {
                            response_arguments.status_code = StatusCode::VersionNotSupported;
                            response_arguments.status_message = Some(format!(
                                "The command executor that received the request only supports major protocol versions '{SUPPORTED_PROTOCOL_VERSIONS:?}', but '{request_protocol_version}' was sent on the request."
                            ));
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
                                                response_arguments.status_message = Some(format!(
                                                    "Failure updating application HLC against {value}: {e}"
                                                ));
                                                response_arguments.invalid_property_name =
                                                    Some(UserProperty::Timestamp.to_string());
                                                response_arguments.invalid_property_value =
                                                    Some(value);
                                                match e.kind() {
                                                    HLCErrorKind::ClockDrift => {
                                                        response_arguments.status_code =
                                                            StatusCode::ServiceUnavailable;
                                                    }
                                                    HLCErrorKind::OverflowWarning => {
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
                                    log::warn!(
                                        "[{}] Command request should not contain MQTT user property {key}. Value is {value}",
                                        self.command_name
                                    );
                                    user_data.push((key, value));
                                }
                            }
                        }

                        let topic_tokens = self
                            .request_topic_pattern
                            .parse_tokens(m.topic_name.as_str());

                        // Deserialize payload
                        let format_indicator = properties.payload_format_indicator.into();
                        let payload = match TReq::deserialize(
                            &m.payload,
                            properties.content_type.as_ref(),
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
                                    response_arguments.status_code =
                                        StatusCode::UnsupportedMediaType;
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

                        let command_request = Request {
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
                                    self.cancellation_token.clone();
                                async move {
                                    tokio::select! {
                                        () = executor_cancellation_token_clone.cancelled() => { /* executor dropped */},
                                        () = Self::process_command(
                                            app_hlc_clone,
                                            client_clone,
                                            pkid,
                                            response_arguments,
                                            (Some(response_rx), Some(publish_completion_tx)),
                                            cache_clone,
                                            processing_drop_guard,
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

                    // Checking that command expiration time was calculated, if it has not we do not
                    // respond to the invoker.
                    let Some(command_expiration_time) = command_expiration_time else {
                        continue;
                    };

                    match response_arguments.cache_lookup_result {
                        CacheLookupResult::Cached {
                            serialized_payload,
                            properties,
                            response_message_expiry_interval,
                        } => {
                            // Process the duplicate command
                            tokio::task::spawn({
                                let client_clone = self.mqtt_client.clone();
                                let executor_cancellation_token_clone =
                                    self.cancellation_token.clone();
                                async move {
                                    tokio::select! {
                                        () = executor_cancellation_token_clone.cancelled() => { /* executor dropped */},
                                        () = Self::process_duplicate_command(
                                            client_clone,
                                            response_arguments.response_topic,
                                            serialized_payload,
                                            properties,
                                            response_message_expiry_interval,
                                            response_arguments.command_name,
                                            pkid,
                                        ) => {
                                            // Finished processing duplicate command
                                            handle_ack(ack_token, executor_cancellation_token_clone, pkid).await;
                                        },
                                    }
                                }
                            });
                        }
                        CacheLookupResult::InProgress(cancellation_token) => {
                            // If the command is currently in progress that means this message
                            // is a dupe and we need to wait for the original to complete before
                            // acking.
                            tokio::task::spawn(handle_in_progress_duplicate_ack(
                                ack_token,
                                cancellation_token.clone(),
                                self.cancellation_token.clone(),
                                pkid,
                            ));
                        }
                        CacheLookupResult::NotFound => {
                            // Indicates the command should be processed as an error

                            // Check the command has not expired, if it has, we do not respond to the invoker.
                            if command_expiration_time.elapsed().is_zero() {
                                tokio::task::spawn({
                                    let app_hlc_clone = self.application_hlc.clone();
                                    let client_clone = self.mqtt_client.clone();
                                    let cache_clone = self.cache.clone();
                                    let executor_cancellation_token_clone =
                                        self.cancellation_token.clone();
                                    async move {
                                        tokio::select! {
                                            () = executor_cancellation_token_clone.cancelled() => { /* executor dropped */},
                                            () = Self::process_command(
                                                app_hlc_clone,
                                                client_clone,
                                                pkid,
                                                response_arguments,
                                                (None, None),
                                                cache_clone,
                                                processing_drop_guard,
                                            ) => {
                                                // Finished processing command
                                                handle_ack(ack_token, executor_cancellation_token_clone, pkid).await;
                                            },
                                        }
                                    }
                                });
                            }
                        }
                    }

                    if !command_expiration_time_calculated {
                        return Some(Err(AIOProtocolError::new_internal_logic_error(
                            true,
                            false,
                            None,
                            "command_expiration_time",
                            None,
                            Some(INTERNAL_LOGIC_EXPIRATION_ERROR.to_string()),
                            Some(self.command_name.clone()),
                        )));
                    }
                }
                _ => {
                    // There will be no more requests
                    return None;
                }
            }
        }
    }

    /// Process a duplicate command by sending the cached response.
    async fn process_duplicate_command(
        client: SessionManagedClient,
        response_topic: TopicName,
        serialized_payload: SerializedPayload,
        mut publish_properties: PublishProperties,
        response_message_expiry_interval: u32,
        command_name: String,
        pkid: u16,
    ) {
        log::debug!(
            "[{command_name}][pkid: {pkid}] Duplicate request, responding with cached response"
        );

        publish_properties.message_expiry_interval = Some(response_message_expiry_interval);

        match client
            .publish_qos1(
                response_topic,
                false,
                serialized_payload.payload,
                publish_properties,
            )
            .await
        {
            Ok(publish_completion_token) => {
                // Wait and handle puback
                match publish_completion_token.await {
                    Ok(puback) => {
                        if !puback.is_success() {
                            log::warn!(
                                "[{command_name}][pkid: {pkid}] Puback reported failure for cached command response: {puback:?}"
                            );
                        }
                    }
                    Err(e) => {
                        log::warn!(
                            "[{command_name}][pkid: {pkid}] Publish completion error for cached command response: {e}"
                        );
                    }
                }
            }
            Err(e) => {
                log::warn!(
                    "[{command_name}][pkid: {pkid}] Client error on cached command response publish: {e}"
                );
            }
        }
    }

    /// Process a command request, finish building the response and send it.
    #[allow(clippy::type_complexity)]
    async fn process_command(
        application_hlc: Arc<ApplicationHybridLogicalClock>,
        client: SessionManagedClient,
        pkid: u16,
        mut response_arguments: ResponseArguments,
        application_channels: (
            Option<oneshot::Receiver<Response<TResp>>>,
            Option<oneshot::Sender<Result<(), AIOProtocolError>>>,
        ), // TODO: Once simplified, remove this complex type
        cache: Cache,
        _processing_drop_guard: DropGuard,
    ) {
        let (response_rx, completion_tx) = application_channels;
        let mut serialized_payload = SerializedPayload::default();
        let mut publish_properties = PublishProperties::default();

        let mut user_properties: Vec<(String, String)> = Vec::new();
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
                        response_app
                    } else {
                        // Happens when the sender is dropped by the application.
                        response_arguments.status_code = StatusCode::InternalServerError;
                        response_arguments.status_message =
                            Some("Request has been dropped by the application".to_string());
                        response_arguments.is_application_error = true;
                        break 'process_response;
                    }
                } else {
                    log::warn!(
                        "[{}][pkid: {}] Command request timed out",
                        response_arguments.command_name,
                        pkid
                    );
                    // Notify the application that a timeout occurred
                    if let Some(completion_tx) = completion_tx {
                        let _ = completion_tx.send(Err(AIOProtocolError::new_timeout_error(
                            false,
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

                // Cloud Events headers
                if let Some(cloud_event) = response.cloud_event {
                    let cloud_event_headers = cloud_event
                        .0
                        .into_headers(response_arguments.response_topic.as_str());
                    for (key, value) in cloud_event_headers {
                        user_properties.push((key, value));
                    }
                }

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
            RPC_COMMAND_PROTOCOL_VERSION.to_string(),
        ));

        user_properties.push((
            UserProperty::SourceId.to_string(),
            client.client_id().to_string(),
        ));

        // Update HLC and use as the timestamp.
        // If there are errors updating the HLC (unlikely when updating against now),
        // the timestamp will not be added.
        if let Ok(timestamp_str) = application_hlc.update_now() {
            user_properties.push((UserProperty::Timestamp.to_string(), timestamp_str));
        }

        if let Some(status_message) = response_arguments.status_message {
            log::warn!(
                "[{}][pkid: {}] sending error reponse to invoker: {}",
                response_arguments.command_name,
                pkid,
                status_message
            );
            user_properties.push((UserProperty::StatusMessage.to_string(), status_message));
        }

        if let Some(name) = response_arguments.invalid_property_name {
            user_properties.push((UserProperty::InvalidPropertyName.to_string(), name));
        }

        if let Some(value) = response_arguments.invalid_property_value {
            user_properties.push((UserProperty::InvalidPropertyValue.to_string(), value));
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
        publish_properties.payload_format_indicator = serialized_payload.format_indicator.into();
        publish_properties.topic_alias = None;
        publish_properties.response_topic = None;
        publish_properties.correlation_data = response_arguments.correlation_data;
        publish_properties.user_properties = user_properties;
        publish_properties.subscription_identifiers = Vec::new();
        publish_properties.content_type = Some(serialized_payload.content_type.clone());

        match response_arguments.command_expiration_time {
            Some(command_expiration_time) => {
                let response_message_expiry_interval =
                    get_response_message_expiry_interval(command_expiration_time);

                // Check if the entry has expired
                if let Some(response_message_expiry_interval) = response_message_expiry_interval {
                    publish_properties.message_expiry_interval =
                        Some(response_message_expiry_interval);
                } else {
                    log::warn!(
                        "[{}][pkid: {}] Command request timed out",
                        response_arguments.command_name,
                        pkid
                    );
                    // Notify the application that a timeout occurred
                    if let Some(completion_tx) = completion_tx {
                        let _ = completion_tx.send(Err(AIOProtocolError::new_timeout_error(
                            false,
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

                // Store cache, even if the response is an error
                if let Some(cached_key) = response_arguments.cached_key {
                    let cache_entry = CacheEntry::Cached {
                        serialized_payload: serialized_payload.clone(),
                        properties: publish_properties.clone(),
                        expiration_time: command_expiration_time
                            + Duration::from_secs(CACHE_EXPIRY_BUFFER_SECONDS),
                    };
                    log::debug!(
                        "[{}][pkid: {}] Caching response",
                        response_arguments.command_name,
                        pkid
                    );
                    cache.set(cached_key, cache_entry);
                }
            }
            _ => {
                // Happens when the command expiration time was not able to be calculated.
                // We don't cache the response in this case. Note that we did not set a in progress
                // entry for this case since it requires a valid expiration time.
                publish_properties.message_expiry_interval =
                    Some(DEFAULT_MESSAGE_EXPIRY_INTERVAL_SECONDS);
            }
        }

        // Try to publish
        match client
            .publish_qos1(
                response_arguments.response_topic,
                false,
                serialized_payload.payload,
                publish_properties,
            )
            .await
        {
            Ok(publish_completion_token) => {
                // Wait and handle puback
                match publish_completion_token.await {
                    Ok(puback) => {
                        match puback.as_result() {
                            Ok(()) => {
                                if let Some(completion_tx) = completion_tx {
                                    // We ignore the error as the receiver may have been dropped indicating that the
                                    // application is not interested in the completion of the publish.
                                    let _ = completion_tx.send(Ok(()));
                                }
                            }
                            Err(e) => {
                                log::error!(
                                    "[{}][pkid: {}] Command response Puback error: {puback:?}",
                                    response_arguments.command_name,
                                    pkid
                                );
                                if let Some(completion_tx) = completion_tx {
                                    // Ignore error as receiver may have been dropped
                                    let _ =
                                        completion_tx.send(Err(AIOProtocolError::new_mqtt_error(
                                            Some(
                                                "MQTT error on command executor response puback"
                                                    .to_string(),
                                            ),
                                            Box::new(e),
                                            Some(response_arguments.command_name.clone()),
                                        )));
                                }
                            }
                        }
                    }
                    Err(e) => {
                        log::error!(
                            "[{}][pkid: {}] Command response Publish completion error: {e}",
                            response_arguments.command_name,
                            pkid
                        );
                        if let Some(completion_tx) = completion_tx {
                            // Ignore error as receiver may have been dropped
                            let _ = completion_tx.send(Err(AIOProtocolError::new_mqtt_error(
                                Some("MQTT error on command executor response publish".to_string()),
                                Box::new(e),
                                Some(response_arguments.command_name.clone()),
                            )));
                        }
                    }
                }
            }
            Err(e) => {
                log::error!(
                    "[{}][pkid: {}] Client error on command executor response publish: {e}",
                    response_arguments.command_name,
                    pkid
                );
                // Notify error publishing
                if let Some(completion_tx) = completion_tx {
                    // Ignore error as receiver may have been dropped
                    let _ = completion_tx.send(Err(AIOProtocolError::new_mqtt_error(
                        Some("MQTT error on command executor response publish".to_string()),
                        Box::new(e),
                        Some(response_arguments.command_name.clone()),
                    )));
                }
            }
        }
    }
}

impl<TReq, TResp> Drop for Executor<TReq, TResp>
where
    TReq: PayloadSerialize + Send + 'static,
    TResp: PayloadSerialize + Send + 'static,
{
    fn drop(&mut self) {
        // Cancel all tasks awaiting responses
        self.cancellation_token.cancel();
        // Close the receiver, once dropped all remaining messages are automatically ack'd
        self.mqtt_receiver.close();

        // If the executor has not been unsubscribed, attempt to unsubscribe
        if State::Subscribed == self.state {
            tokio::spawn({
                let request_topic = self.request_topic_filter.clone();
                let mqtt_client = self.mqtt_client.clone();
                async move {
                    match mqtt_client
                        .unsubscribe(
                            request_topic.clone(),
                            azure_iot_operations_mqtt::control_packet::UnsubscribeProperties::default(),
                        )
                        .await
                    {
                        Ok(_) => {
                            log::debug!(
                                "Executor Unsubscribe sent on topic {request_topic}. Unsuback may still be pending."
                            );
                        }
                        Err(e) => {
                            log::warn!("Executor Unsubscribe error on topic {request_topic}: {e}");
                        }
                    }
                }
            });
        }

        log::info!("[{}] Command Executor has been dropped", self.command_name);
    }
}

fn get_response_message_expiry_interval(command_expiration_time: Instant) -> Option<u32> {
    // Calculate the remaining time until the command expires
    let response_message_expiry_interval =
        command_expiration_time.saturating_duration_since(Instant::now());

    // Check if the entry has expired
    if response_message_expiry_interval.is_zero() {
        // Don't return zero as returning a message expiry interval of zero means the message
        // never expires.
        None
    } else {
        // Rounding remaining expiration time up to the nearest second
        let response_message_expiry_interval =
            if response_message_expiry_interval.subsec_nanos() != 0 {
                // NOTE: We should always be able to add 1 since the seconds portion of the
                // response_message_expiry_interval is always at least one less than its initial
                // value when received in this block.
                // NOTE: Rounding up to the nearest second to ensure the invoker will time out
                // at or before the response expires.
                response_message_expiry_interval.as_secs().saturating_add(1)
            } else {
                response_message_expiry_interval.as_secs()
            };

        match response_message_expiry_interval.try_into() {
            Ok(interval) => Some(interval),
            Err(_) => unreachable!(), // Unreachable, will be less than or equal to u32::MAX, see unit test "test_get_response_message_expiry_interval_at_limit"
        }
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
                Ok(ack_ct) => {
                    match ack_ct.await {
                        Ok(()) => log::debug!("[pkid: {pkid}] Command Request Acknowledged"),
                        Err(e) => {
                            match e {
                                azure_iot_operations_mqtt::error::CompletionError::Detached => {
                                    log::warn!("[pkid: {pkid}] Command Request Ack error: {e}");
                                },
                                azure_iot_operations_mqtt::error::CompletionError::Canceled(_) => {
                                    // This means the executor will receive a future ack from the
                                    // session once the dupe comes in.
                                    log::warn!("[pkid: {pkid}] Command Request ack cancelled due to disconnect, request will be redelivered");
                                },
                            }
                         }
                    }
                },
                Err(e) => {
                    log::warn!("[pkid: {pkid}] Command Request Ack error: {e}");
                }
            }
        }
    }
}

async fn handle_in_progress_duplicate_ack(
    ack_token: AckToken,
    in_progress_cancellation_token: CancellationToken,
    executor_cancellation_token: CancellationToken,
    pkid: u16,
) {
    tokio::select! {
        () = executor_cancellation_token.cancelled() => { /* executor dropped */ },
        () = in_progress_cancellation_token.cancelled() => {
            // This means the in progress command has finished processing, we can now ack the
            // duplicate ack
            handle_ack(ack_token, executor_cancellation_token, pkid).await;
        }
    }
}

#[cfg(test)]
mod tests {
    use azure_iot_operations_mqtt::session::{Session, SessionOptionsBuilder};
    use test_case::test_case;
    // TODO: This dependency on MqttConnectionSettingsBuilder should be removed in lieu of using a true mock
    use azure_iot_operations_mqtt::aio::connection_settings::MqttConnectionSettingsBuilder;

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
        let executor_options = OptionsBuilder::default()
            .request_topic_pattern("test/{commandName}/{executorId}/request")
            .command_name("test_command_name")
            .topic_token_map(create_topic_tokens())
            .build()
            .unwrap();

        let executor: Executor<MockPayload, MockPayload> = Executor::new(
            ApplicationContextBuilder::default().build().unwrap(),
            managed_client,
            executor_options,
        )
        .unwrap();

        assert_eq!(
            executor
                .request_topic_pattern
                .as_subscribe_topic()
                .unwrap()
                .as_str(),
            "test/test_command_name/test_executor_id/request"
        );

        assert!(!executor.is_idempotent);
    }

    #[tokio::test]
    async fn test_new_override_defaults() {
        let session = create_session();
        let managed_client = session.create_managed_client();
        let executor_options = OptionsBuilder::default()
            .request_topic_pattern("test/{commandName}/{executorId}/request")
            .command_name("test_command_name")
            .topic_namespace("test_namespace")
            .topic_token_map(create_topic_tokens())
            .is_idempotent(true)
            .build()
            .unwrap();

        let executor: Executor<MockPayload, MockPayload> = Executor::new(
            ApplicationContextBuilder::default().build().unwrap(),
            managed_client,
            executor_options,
        )
        .unwrap();

        assert_eq!(
            executor
                .request_topic_pattern
                .as_subscribe_topic()
                .unwrap()
                .as_str(),
            "test_namespace/test/test_command_name/test_executor_id/request"
        );

        assert!(executor.is_idempotent);
    }

    #[test_case(""; "empty command name")]
    #[test_case(" "; "whitespace command name")]
    #[tokio::test]
    async fn test_new_empty_and_whitespace_command_name(command_name: &str) {
        let session = create_session();
        let managed_client = session.create_managed_client();

        let executor_options = OptionsBuilder::default()
            .request_topic_pattern("test/{commandName}/request")
            .command_name(command_name.to_string())
            .topic_token_map(create_topic_tokens())
            .build()
            .unwrap();

        let executor: Result<Executor<MockPayload, MockPayload>, AIOProtocolError> = Executor::new(
            ApplicationContextBuilder::default().build().unwrap(),
            managed_client,
            executor_options,
        );

        match executor {
            Err(e) => {
                assert_eq!(e.kind, AIOProtocolErrorKind::ConfigurationInvalid);
                assert!(e.is_shallow);
                assert!(!e.is_remote);
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

        let executor_options = OptionsBuilder::default()
            .request_topic_pattern(request_topic.to_string())
            .command_name("test_command_name")
            .topic_token_map(create_topic_tokens())
            .build()
            .unwrap();

        let executor: Result<Executor<MockPayload, MockPayload>, AIOProtocolError> = Executor::new(
            ApplicationContextBuilder::default().build().unwrap(),
            managed_client,
            executor_options,
        );

        match executor {
            Err(e) => {
                assert_eq!(e.kind, AIOProtocolErrorKind::ConfigurationInvalid);
                assert!(e.is_shallow);
                assert!(!e.is_remote);
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
        let executor_options = OptionsBuilder::default()
            .request_topic_pattern("test/{commandName}/request")
            .command_name("test_command_name")
            .topic_namespace(topic_namespace.to_string())
            .topic_token_map(create_topic_tokens())
            .build()
            .unwrap();

        let executor: Result<Executor<MockPayload, MockPayload>, AIOProtocolError> = Executor::new(
            ApplicationContextBuilder::default().build().unwrap(),
            managed_client,
            executor_options,
        );
        match executor {
            Err(e) => {
                assert_eq!(e.kind, AIOProtocolErrorKind::ConfigurationInvalid);
                assert!(e.is_shallow);
                assert!(!e.is_remote);
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
        let executor_options = OptionsBuilder::default()
            .request_topic_pattern("test/request")
            .command_name("test_command_name")
            .build()
            .unwrap();
        let mut executor: Executor<MockPayload, MockPayload> = Executor::new(
            ApplicationContextBuilder::default().build().unwrap(),
            session.create_managed_client(),
            executor_options,
        )
        .unwrap();
        assert!(executor.shutdown().await.is_ok());
    }

    // Command Response tests
    #[test]
    fn test_response_serialization_error() {
        let mut mock_response_payload = MockPayload::new();
        mock_response_payload
            .expect_serialize()
            .returning(|| Err("dummy error".to_string()))
            .times(1);

        let mut binding = ResponseBuilder::default();
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

        let mut binding = ResponseBuilder::default();
        let resp_builder = binding.payload(mock_response_payload);
        match resp_builder {
            Err(e) => {
                assert_eq!(e.kind, AIOProtocolErrorKind::ConfigurationInvalid);
                assert!(e.is_shallow);
                assert!(!e.is_remote);
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

    #[test]
    fn test_response_invalid_custom_user_data_cloud_event_header() {
        let mut mock_response_payload = MockPayload::new();
        mock_response_payload
            .expect_serialize()
            .returning(|| {
                Ok(SerializedPayload {
                    payload: Vec::new(),
                    content_type: "application/json".to_string(),
                    format_indicator: FormatIndicator::Utf8EncodedCharacterData,
                })
            })
            .times(1);

        let response_builder_result = ResponseBuilder::default()
            .payload(mock_response_payload)
            .unwrap()
            .custom_user_data(vec![("source".to_string(), "test".to_string())])
            .build();

        assert!(response_builder_result.is_err());
    }

    #[test]
    fn test_response_defaults() {
        let mut mock_response_payload = MockPayload::new();
        mock_response_payload
            .expect_serialize()
            .returning(|| {
                Ok(SerializedPayload {
                    payload: Vec::new(),
                    content_type: "application/json".to_string(),
                    format_indicator: FormatIndicator::Utf8EncodedCharacterData,
                })
            })
            .times(1);

        let response_builder_result = ResponseBuilder::default()
            .payload(mock_response_payload)
            .unwrap()
            .build();

        let r = response_builder_result.unwrap();

        assert!(r.custom_user_data.is_empty());
        assert!(r.cloud_event.is_none());
        assert!(r.serialized_payload.payload.is_empty());
    }

    #[tokio::test]
    async fn test_cache_not_found() {
        let cache = Cache(Arc::new(Mutex::new(HashMap::new())));
        let key = CacheKey {
            response_topic: TopicName::new("test_response_topic").unwrap(),
            correlation_data: Bytes::from("test_correlation_data"),
        };
        let status = cache.get(&key);
        assert!(matches!(status, CacheLookupResult::NotFound));
    }

    #[test]
    fn test_cache_found_complete() {
        let cache = Cache(Arc::new(Mutex::new(HashMap::new())));
        let key = CacheKey {
            response_topic: TopicName::new("test_response_topic").unwrap(),
            correlation_data: Bytes::from("test_correlation_data"),
        };
        let entered_serialized_payload = SerializedPayload {
            payload: Bytes::from("test_payload").to_vec(),
            content_type: "application/json".to_string(),
            format_indicator: FormatIndicator::Utf8EncodedCharacterData,
        };
        let entry = CacheEntry::Cached {
            serialized_payload: entered_serialized_payload.clone(),
            properties: PublishProperties::default(),
            expiration_time: Instant::now() + Duration::from_secs(60),
        };
        cache.set(key.clone(), entry.clone());
        let status = cache.get(&key);
        match status {
            CacheLookupResult::Cached {
                serialized_payload,
                properties,
                response_message_expiry_interval,
            } => {
                assert_eq!(serialized_payload, entered_serialized_payload);
                assert_eq!(properties, PublishProperties::default());
                // The expiry interval should be between 1 and 60 seconds, 60 seconds should not pass
                // before this check is made and we will always round up to the nearest second.
                let range = 1..=60;
                assert!(range.contains(&response_message_expiry_interval));
            }
            _ => {
                panic!("Expected cached entry");
            }
        }
    }

    #[test]
    fn test_cache_found_in_progress() {
        let cache = Cache(Arc::new(Mutex::new(HashMap::new())));
        let key = CacheKey {
            response_topic: TopicName::new("test_response_topic").unwrap(),
            correlation_data: Bytes::from("test_correlation_data"),
        };
        let entry = CacheEntry::InProgress {
            processing_cancellation_token: CancellationToken::new(),
        };
        cache.set(key.clone(), entry.clone());

        match cache.get(&key) {
            CacheLookupResult::InProgress(_) => { /* Success */ }
            _ => {
                panic!("Expected in progress entry");
            }
        }
    }

    #[test]
    fn test_cache_expired_entry_not_found() {
        let cache = Cache(Arc::new(Mutex::new(HashMap::new())));
        let key = CacheKey {
            response_topic: TopicName::new("test_response_topic").unwrap(),
            correlation_data: Bytes::from("test_correlation_data"),
        };
        let entry = CacheEntry::Cached {
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
        assert!(matches!(status, CacheLookupResult::NotFound));

        // Set a new entry and check if the expired entry is deleted
        let new_serialized_payload = SerializedPayload {
            payload: Bytes::from("new_test_payload").to_vec(),
            content_type: "application/json".to_string(),
            format_indicator: FormatIndicator::Utf8EncodedCharacterData,
        };
        let new_entry = CacheEntry::Cached {
            serialized_payload: new_serialized_payload.clone(),
            properties: PublishProperties::default(),
            expiration_time: Instant::now() + Duration::from_secs(60),
        };
        // The cache should never see another entry with the same key, this is for testing purposes only.
        cache.set(key.clone(), new_entry.clone());

        let new_status = cache.get(&key);
        match new_status {
            CacheLookupResult::Cached {
                serialized_payload,
                properties,
                response_message_expiry_interval,
            } => {
                assert_eq!(serialized_payload, new_serialized_payload);
                assert_eq!(properties, PublishProperties::default());
                // The expiry interval should be between 1 and 60 seconds, 60 seconds should not pass
                // before this check is made and we will always round up to the nearest second.
                let range = 1..=60;
                assert!(range.contains(&response_message_expiry_interval));
            }
            _ => {
                panic!("Expected cached entry");
            }
        }
    }

    #[test]
    fn test_cache_expired_entry_not_found_with_different_key_set() {
        let cache = Cache(Arc::new(Mutex::new(HashMap::new())));
        let old_key = CacheKey {
            response_topic: TopicName::new("test_response_topic").unwrap(),
            correlation_data: Bytes::from("test_correlation_data"),
        };
        let old_entry = CacheEntry::Cached {
            serialized_payload: SerializedPayload {
                payload: Bytes::from("test_payload").to_vec(),
                content_type: "application/json".to_string(),
                format_indicator: FormatIndicator::Utf8EncodedCharacterData,
            },
            properties: PublishProperties::default(),
            expiration_time: Instant::now() - Duration::from_secs(60),
        };
        cache.set(old_key.clone(), old_entry);
        let status = cache.get(&old_key);
        assert!(matches!(status, CacheLookupResult::NotFound));

        // Set a new entry with a different key and check if the expired entry is deleted
        let new_key = CacheKey {
            response_topic: TopicName::new("new_test_response_topic").unwrap(),
            correlation_data: Bytes::from("new_test_correlation_data"),
        };
        let new_serialized_payload = SerializedPayload {
            payload: Bytes::from("new_test_payload").to_vec(),
            content_type: "application/json".to_string(),
            format_indicator: FormatIndicator::Utf8EncodedCharacterData,
        };
        let new_entry = CacheEntry::Cached {
            serialized_payload: new_serialized_payload.clone(),
            properties: PublishProperties::default(),
            expiration_time: Instant::now() + Duration::from_secs(60),
        };
        cache.set(new_key.clone(), new_entry.clone());

        let old_status = cache.get(&old_key);
        assert!(matches!(old_status, CacheLookupResult::NotFound));
        let new_status = cache.get(&new_key);

        match new_status {
            CacheLookupResult::Cached {
                serialized_payload,
                properties,
                response_message_expiry_interval,
            } => {
                assert_eq!(serialized_payload, new_serialized_payload);
                assert_eq!(properties, PublishProperties::default());
                // The expiry interval should be between 1 and 60 seconds, 60 seconds should not pass
                // before this check is made and we will always round up to the nearest second.
                let range = 1..=60;
                assert!(range.contains(&response_message_expiry_interval));
            }
            _ => {
                panic!("Expected cached entry");
            }
        }
    }

    #[test]
    fn test_cache_in_progress_found_with_different_key_set() {
        let cache = Cache(Arc::new(Mutex::new(HashMap::new())));
        let old_key = CacheKey {
            response_topic: TopicName::new("test_response_topic").unwrap(),
            correlation_data: Bytes::from("test_correlation_data"),
        };
        let old_entry = CacheEntry::InProgress {
            processing_cancellation_token: CancellationToken::new(),
        };
        cache.set(old_key.clone(), old_entry);
        let status = cache.get(&old_key);
        // While in progress, the entry can't expire.
        assert!(matches!(status, CacheLookupResult::InProgress(..)));

        // Set a new entry with a different key and check if the in progress entry is still present
        let new_key = CacheKey {
            response_topic: TopicName::new("new_test_response_topic").unwrap(),
            correlation_data: Bytes::from("new_test_correlation_data"),
        };
        let new_serialized_payload = SerializedPayload {
            payload: Bytes::from("new_test_payload").to_vec(),
            content_type: "application/json".to_string(),
            format_indicator: FormatIndicator::Utf8EncodedCharacterData,
        };
        let new_entry = CacheEntry::Cached {
            serialized_payload: new_serialized_payload.clone(),
            properties: PublishProperties::default(),
            expiration_time: Instant::now() + Duration::from_secs(60),
        };
        cache.set(new_key.clone(), new_entry.clone());

        let old_status = cache.get(&old_key);
        assert!(matches!(old_status, CacheLookupResult::InProgress(..)));
        let new_status = cache.get(&new_key);

        match new_status {
            CacheLookupResult::Cached {
                serialized_payload,
                properties,
                response_message_expiry_interval,
            } => {
                assert_eq!(serialized_payload, new_serialized_payload);
                assert_eq!(properties, PublishProperties::default());
                // The expiry interval should be between 1 and 60 seconds, 60 seconds should not pass
                // before this check is made and we will always round up to the nearest second.
                let range = 1..=60;
                assert!(range.contains(&response_message_expiry_interval));
            }
            _ => {
                panic!("Expected cached entry");
            }
        }
    }

    #[test]
    fn test_cache_in_progress_notified_completion() {
        // This tests the verified flow of registering to completion in case a dupe comes in
        let cache = Cache(Arc::new(Mutex::new(HashMap::new())));
        let processing_cancellation_token = CancellationToken::new();
        let key = CacheKey {
            response_topic: TopicName::new("test_response_topic").unwrap(),
            correlation_data: Bytes::from("test_correlation_data"),
        };
        let entry = CacheEntry::InProgress {
            processing_cancellation_token: processing_cancellation_token.clone(),
        };
        cache.set(key.clone(), entry.clone());

        {
            // This simulates processing the command
            let _processing_drop_guard = processing_cancellation_token.drop_guard();

            match cache.get(&key) {
                CacheLookupResult::InProgress(_) => { /* Success */ }
                _ => {
                    panic!("Expected in progress entry");
                }
            }
        }

        // If a dupe comes in, and checks the cancellation token it should know it can ack
        match cache.get(&key) {
            CacheLookupResult::InProgress(cancellation_token) => {
                // Since the command processing simulation ended, the cancellation token should be cancelled
                assert!(cancellation_token.is_cancelled());
            }
            _ => {
                panic!("Expected in progress entry");
            }
        }
    }

    #[test]
    fn test_response_add_empty_error_payload_success() {
        let mut mock_response_payload = MockPayload::new();
        mock_response_payload
            .expect_serialize()
            .returning(|| {
                Ok(SerializedPayload {
                    payload: Vec::new(),
                    content_type: "application/json".to_string(),
                    format_indicator: FormatIndicator::Utf8EncodedCharacterData,
                })
            })
            .times(1);

        let mut custom_user_data = Vec::new();
        assert!(
            application_error_headers(&mut custom_user_data, "500".into(), "  ".into()).is_ok()
        );

        let response = ResponseBuilder::default()
            .custom_user_data(custom_user_data)
            .payload(mock_response_payload)
            .unwrap()
            .build()
            .unwrap();

        assert_eq!(response.custom_user_data.len(), 1);

        let mut app_error_code_header_found = false;
        let mut app_error_payload_header_found = false;
        for (key, value) in response.custom_user_data {
            if key == "AppErrCode" {
                app_error_code_header_found = true;
                assert_eq!(value, "500");
            }
            if key == "AppErrPayload" {
                app_error_payload_header_found = true;
            }
        }

        assert!(app_error_code_header_found);
        assert!(!app_error_payload_header_found);
    }

    #[test]
    fn test_response_add_empty_error_code_error() {
        let mut custom_user_data = Vec::new();

        assert!(
            application_error_headers(&mut custom_user_data, " ".into(), "Some error".into())
                .is_err()
        );

        assert_eq!(custom_user_data.len(), 0);
    }

    #[test]
    fn test_get_response_message_expiry_interval_not_expired() {
        let response_message_expiry_interval =
            get_response_message_expiry_interval(Instant::now() + Duration::from_secs(10));

        let range = 1..=10;
        // Should be between 1 and 10 seconds
        assert!(range.contains(&response_message_expiry_interval.unwrap()));
    }

    #[test]
    fn test_get_response_message_expiry_inteval_expired() {
        let response_message_expiry_interval =
            get_response_message_expiry_interval(Instant::now() - Duration::from_secs(10));

        assert!(response_message_expiry_interval.is_none());
    }

    #[test]
    fn test_get_response_message_expiry_interval_at_limit() {
        // When the message_expiry_interval property is received it is bounded to a u32 meaning that the
        // maximum value is u32::MAX seconds. We test that the function correctly handles this upper limit.
        let response_message_expiry_interval = get_response_message_expiry_interval(
            Instant::now() + Duration::from_secs(u64::from(u32::MAX)),
        );

        let range = 1..=u32::MAX; // note, range is memory efficient
        assert!(range.contains(&response_message_expiry_interval.unwrap()));
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
