// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

use std::{collections::HashMap, marker::PhantomData, str::FromStr, sync::Arc, time::Duration};

use azure_iot_operations_mqtt::control_packet::{Publish, PublishProperties, QoS};
use azure_iot_operations_mqtt::interface::{ManagedClient, PubReceiver};
use bytes::Bytes;
use iso8601_duration;
use tokio::{
    sync::{
        Mutex, Notify,
        broadcast::{Sender, error::RecvError},
    },
    task::{self, JoinHandle},
    time,
};
use tokio_util::sync::CancellationToken;
use uuid::Uuid;

use crate::common::user_properties::{PARTITION_KEY, validate_invoker_user_properties};
use crate::{
    ProtocolVersion,
    application::{ApplicationContext, ApplicationHybridLogicalClock},
    common::{
        aio_protocol_error::{AIOProtocolError, AIOProtocolErrorKind, Value},
        hybrid_logical_clock::HybridLogicalClock,
        is_invalid_utf8,
        payload_serialize::{
            DeserializationError, FormatIndicator, PayloadSerialize, SerializedPayload,
        },
        topic_processor::{TopicPattern, contains_invalid_char},
        user_properties::UserProperty,
    },
    parse_supported_protocol_major_versions,
    rpc_command::{
        DEFAULT_RPC_COMMAND_PROTOCOL_VERSION, RPC_COMMAND_PROTOCOL_VERSION, StatusCode,
        StatusCodeParseError,
    },
};

const SUPPORTED_PROTOCOL_VERSIONS: &[u16] = &[1];

/// Command Request struct.
/// Used by the [`Invoker`]
#[derive(Builder, Clone, Debug)]
#[builder(setter(into), build_fn(validate = "Self::validate"))]
pub struct Request<TReq>
where
    TReq: PayloadSerialize,
{
    /// Payload of the command request.
    #[builder(setter(custom))]
    serialized_payload: SerializedPayload,
    /// Strongly link `Request` with type `TReq`
    #[builder(private)]
    request_payload_type: PhantomData<TReq>,
    /// User data that will be set as custom MQTT User Properties on the Request message.
    /// Can be used to pass additional metadata to the executor.
    /// Default is an empty vector.
    #[builder(default)]
    custom_user_data: Vec<(String, String)>,
    /// Topic token keys/values to be replaced into the publish topic of the request.
    #[builder(default)]
    topic_tokens: HashMap<String, String>,
    /// Timeout for the command, must be one second or greater. Will also be used as the `message_expiry_interval`
    /// to give the executor information on when the invoke request might expire.
    #[builder(setter(custom))]
    timeout: Duration,
}
impl<TReq: PayloadSerialize> RequestBuilder<TReq> {
    /// Add a payload to the command request. Validates successful serialization of the payload.
    ///
    /// # Errors
    /// [`AIOProtocolError`] of kind [`PayloadInvalid`](crate::common::aio_protocol_error::AIOProtocolErrorKind::PayloadInvalid) if serialization of the payload fails
    ///
    /// [`AIOProtocolError`] of kind [`ConfigurationInvalid`](crate::common::aio_protocol_error::AIOProtocolErrorKind::ConfigurationInvalid) if the content type is not valid utf-8
    pub fn payload(&mut self, payload: TReq) -> Result<&mut Self, AIOProtocolError> {
        match payload.serialize() {
            Err(e) => Err(AIOProtocolError::new_payload_invalid_error(
                true,
                false,
                Some(e.into()),
                Some("Payload serialization error".to_string()),
                None,
            )),
            Ok(serialized_payload) => {
                // Validate content type of command request is valid UTF-8
                if is_invalid_utf8(&serialized_payload.content_type) {
                    return Err(AIOProtocolError::new_configuration_invalid_error(
                        None,
                        "content_type",
                        Value::String(serialized_payload.content_type.to_string()),
                        Some(format!(
                            "Content type '{}' of command request is not valid UTF-8",
                            serialized_payload.content_type
                        )),
                        None,
                    ));
                }
                self.serialized_payload = Some(serialized_payload);
                self.request_payload_type = Some(PhantomData);
                Ok(self)
            }
        }
    }

    /// Set the timeout for the command
    ///
    /// Note: Will be rounded up to the nearest second.
    pub fn timeout(&mut self, timeout: Duration) -> &mut Self {
        self.timeout = Some(if timeout.subsec_nanos() != 0 {
            Duration::from_secs(timeout.as_secs().saturating_add(1))
        } else {
            timeout
        });

        self
    }

    /// Validate the command request.
    ///
    /// # Errors
    /// Returns a `String` describing the error if
    ///     - any of `custom_user_data`'s keys or values are invalid utf-8 or the key is reserved
    ///     - timeout is zero or > `u32::max`
    fn validate(&self) -> Result<(), String> {
        if let Some(custom_user_data) = &self.custom_user_data {
            validate_invoker_user_properties(custom_user_data)?;
        }
        if let Some(timeout) = &self.timeout {
            if timeout.as_secs() == 0 {
                return Err("Timeout must not be 0".to_string());
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
/// Used by the [`Invoker`]
#[derive(Debug)]
pub struct Response<TResp>
where
    TResp: PayloadSerialize,
{
    /// Payload of the command response. Must implement [`PayloadSerialize`].
    pub payload: TResp,
    /// Content Type of the command response.
    pub content_type: Option<String>,
    /// Format Indicator of the command response.
    pub format_indicator: FormatIndicator,
    /// Custom user data set as custom MQTT User Properties on the Response message.
    pub custom_user_data: Vec<(String, String)>,
    /// Timestamp of the command response.
    pub timestamp: Option<HybridLogicalClock>,
}

/// Helper function to return the application error code and payload, if present in `custom_user_data`.
///
/// Returns a [`(Option<String>, Option<String>)`] tuple where:
/// - the first element is the application error code (or [`None`] if not present), and
/// - the second element is the application error payload (or [`None`] if not present).
#[must_use]
pub fn application_error_headers(
    custom_user_data: &Vec<(String, String)>,
) -> (Option<String>, Option<String>) {
    const APPLICATION_ERROR_CODE_HEADER: &str = "AppErrCode";
    const APPLICATION_ERROR_PAYLOAD_HEADER: &str = "AppErrPayload";

    let mut app_error_code: Option<String> = None;
    let mut app_error_payload: Option<String> = None;

    for (key, value) in custom_user_data {
        if key == APPLICATION_ERROR_CODE_HEADER {
            app_error_code = Some(value.clone());
        }

        if key == APPLICATION_ERROR_PAYLOAD_HEADER {
            app_error_payload = Some(value.clone());
        }
    }

    (app_error_code, app_error_payload)
}

/// Represents an error reported by a remote executor
#[derive(thiserror::Error, Debug, Clone)]
#[error("Remote Error status code: {status_code:?}")]
pub struct RemoteError {
    /// Status code received from a remote service that detected the error
    status_code: StatusCode,
    /// Protocol version of data received from a remote service
    protocol_version: ProtocolVersion,
    /// The message received with the error
    status_message: Option<String>,
    /// Indicates if the error was detected in the user-application
    is_application_error: bool,
    /// The name of the property that was invalid
    invalid_property_name: Option<String>,
    /// The value of the property that was invalid
    invalid_property_value: Option<String>,
    /// List of supported major protocol versions
    supported_protocol_major_versions: Option<Vec<u16>>,
    /// The timestamp of the error
    timestamp: Option<HybridLogicalClock>,
}

impl From<RemoteError> for AIOProtocolError {
    fn from(value: RemoteError) -> Self {
        // NOTE: We do not use the AIOProtocolError constructor functions, as they would
        // result in additional allocations (String -> &str -> String). Because there is no
        // Default implementation for AIOProtocolError (nor can there be), we will initialize a
        // "default" instance here, and update fields as necessary.
        let remote_error_clone = value.clone();
        let mut aio_error = AIOProtocolError {
            kind: AIOProtocolErrorKind::UnknownError,
            message: value.status_message,
            is_shallow: false, // Always false because a RemoteError implies network activity
            is_remote: true,   // Always true because it is a RemoteError
            nested_error: Some(Box::new(remote_error_clone)),
            header_name: None,
            header_value: None,
            timeout_name: None,
            timeout_value: None,
            property_name: None,
            property_value: None,
            command_name: None, // Will need to update this after return
            protocol_version: Some(value.protocol_version.to_string()),
            supported_protocol_major_versions: value.supported_protocol_major_versions,
        };

        match value.status_code {
            StatusCode::Ok | StatusCode::NoContent => {
                // NOTE: Could remove this by defining a subset of the StatusCode enums
                // e.g. FailureStatusCode, but that might be overkill
                unreachable!("Invalid status code for RemoteError")
            }
            StatusCode::BadRequest => {
                if value.invalid_property_name.is_some() && value.invalid_property_value.is_some() {
                    aio_error.kind = AIOProtocolErrorKind::HeaderInvalid;
                    aio_error.header_name = value.invalid_property_name;
                    aio_error.header_value = value.invalid_property_value;
                } else if value.invalid_property_name.is_some() {
                    aio_error.kind = AIOProtocolErrorKind::HeaderMissing;
                    aio_error.header_name = value.invalid_property_name;
                } else {
                    aio_error.kind = AIOProtocolErrorKind::PayloadInvalid;
                }
            }
            StatusCode::RequestTimeout => {
                aio_error.kind = AIOProtocolErrorKind::Timeout;
                aio_error.timeout_name = value.invalid_property_name;
                aio_error.timeout_value = value.invalid_property_value.and_then(|timeout_s| {
                    match timeout_s.parse::<iso8601_duration::Duration>() {
                        Ok(d) => d.to_std(),
                        Err(_) => None,
                    }
                });
            }
            StatusCode::UnsupportedMediaType => {
                aio_error.kind = AIOProtocolErrorKind::HeaderInvalid;
                aio_error.header_name = value.invalid_property_name;
                aio_error.header_value = value.invalid_property_value;
            }
            StatusCode::InternalServerError => {
                // TODO: We may want to narrow this logic a bit to cover only valid cases.
                // but for now, this is the same logic from prior iterations. When revisiting
                // errors, this may be able to be cleaned up more.
                if value.is_application_error {
                    aio_error.kind = AIOProtocolErrorKind::ExecutionException;
                    aio_error.property_name = value.invalid_property_name;
                    aio_error.property_value = value.invalid_property_value.map(Value::String);
                } else if value.invalid_property_name.is_some() {
                    aio_error.kind = AIOProtocolErrorKind::InternalLogicError;
                    aio_error.property_name = value.invalid_property_name;
                    aio_error.property_value = value.invalid_property_value.map(Value::String);
                } else {
                    aio_error.kind = AIOProtocolErrorKind::UnknownError;
                    // It is expected that value.invalid_property_name and
                    // value.invalid_property_value are None, but they are not set
                    // in order to ensure a None value.
                    // Not sure that this is entirely desirable.
                }
            }
            StatusCode::ServiceUnavailable => {
                aio_error.kind = AIOProtocolErrorKind::StateInvalid;
                aio_error.property_name = value.invalid_property_name;
                aio_error.property_value = value.invalid_property_value.map(Value::String);
            }
            StatusCode::VersionNotSupported => {
                aio_error.kind = AIOProtocolErrorKind::UnsupportedVersion;
            }
        }
        aio_error
    }
}

/// Internal enum representing a result returned over the network
enum CommandResult<TResp>
where
    TResp: PayloadSerialize,
{
    /// Indicates a successful response reported over the network
    Ok(Response<TResp>),
    /// Indicates a protocol failure reported over the network
    Err(RemoteError),
}

impl<TResp> TryFrom<Publish> for CommandResult<TResp>
where
    TResp: PayloadSerialize,
{
    // NOTE: The `command_name` field will need to be added to the AIOProtocolError AFTER it is
    // returned in the case of failure, since, of course, the command name is not included in the
    // Publish.
    // NOTE 2: This of course would make moving to other error models more complex, but the
    // eventual implementation of a ChunkBuffer could get us around this by including the command
    // name on the buffer.
    type Error = AIOProtocolError;

    fn try_from(value: Publish) -> Result<CommandResult<TResp>, Self::Error> {
        // NOTE: User properties are parsed out into a new HashMap because:
        // 1) It makes the code more readable/maintanable to do HashMap lookups
        // 2) When this logic is extracted to a ChunkBuffer, it will be more memory efficient as
        //  we won't want to keep entire copies of all Publishes, so we will just copy the
        //  properties once.

        let publish_properties =
            value
                .properties
                .ok_or(AIOProtocolError::new_header_missing_error(
                    "Properties",
                    false,
                    Some("Properties missing from MQTT message".to_string()),
                    None,
                ))?;

        // Parse user properties
        let expected_aio_properties = [
            UserProperty::Timestamp,
            UserProperty::Status,
            UserProperty::StatusMessage,
            UserProperty::IsApplicationError,
            UserProperty::InvalidPropertyName,
            UserProperty::InvalidPropertyValue,
            UserProperty::ProtocolVersion,
            UserProperty::SupportedMajorVersions,
            UserProperty::RequestProtocolVersion,
        ];
        let mut response_custom_user_data = vec![];
        let mut response_aio_data = HashMap::new();
        for (key, value) in publish_properties.user_properties {
            match UserProperty::from_str(&key) {
                Ok(p) if expected_aio_properties.contains(&p) => {
                    response_aio_data.insert(p, value);
                }
                Ok(_) => {
                    log::warn!(
                        "Response should not contain MQTT user property '{key}'. Value is '{value}'"
                    );
                    response_custom_user_data.push((key, value));
                }
                Err(()) => {
                    response_custom_user_data.push((key, value));
                }
            }
        }

        // Check the protocol version.
        // If the protocol version is not supported, or cannot be parsed, all bets are off
        // regarding what anything else even means, so this *must* be done first
        let protocol_version = {
            match response_aio_data.get(&UserProperty::ProtocolVersion) {
                Some(protocol_version) => {
                    if let Some(version) = ProtocolVersion::parse_protocol_version(protocol_version)
                    {
                        version
                    } else {
                        return Err(AIOProtocolError::new_unsupported_version_error(
                            Some(format!(
                                "Received a response with an unparsable protocol version number: {protocol_version}"
                            )),
                            protocol_version.to_string(),
                            SUPPORTED_PROTOCOL_VERSIONS.to_vec(),
                            None,
                            false,
                            false,
                        ));
                    }
                }
                None => DEFAULT_RPC_COMMAND_PROTOCOL_VERSION,
            }
        };
        if !protocol_version.is_supported(SUPPORTED_PROTOCOL_VERSIONS) {
            return Err(AIOProtocolError::new_unsupported_version_error(
                None,
                protocol_version.to_string(),
                SUPPORTED_PROTOCOL_VERSIONS.to_vec(),
                None,
                false,
                false,
            ));
        }

        // Check the status code.
        // We will use this to determine which data format to serialize to.
        let status_code = {
            match response_aio_data.get(&UserProperty::Status) {
                Some(s) => match StatusCode::from_str(s) {
                    Ok(code) => code,
                    Err(StatusCodeParseError::UnparsableStatusCode(s)) => {
                        return Err(AIOProtocolError::new_header_invalid_error(
                            &UserProperty::Status.to_string(),
                            &s,
                            false,
                            Some(format!(
                                "Could not parse status in response '{s}' as an integer"
                            )),
                            None,
                        ));
                    }
                    Err(StatusCodeParseError::UnknownStatusCode(_)) => {
                        let status_message = response_aio_data
                            .remove(&UserProperty::StatusMessage)
                            .unwrap_or(String::from("Unknown"));
                        let mut unknown_err = AIOProtocolError::new_unknown_error(
                            true,
                            false,
                            None,
                            Some(status_message),
                            None,
                        );
                        // Add any invalid properties that might be included for extra information
                        unknown_err.property_name =
                            response_aio_data.remove(&UserProperty::InvalidPropertyName);
                        unknown_err.property_value = response_aio_data
                            .remove(&UserProperty::InvalidPropertyValue)
                            .map(Value::String);
                        return Err(unknown_err);
                    }
                },
                None => {
                    return Err(AIOProtocolError::new_header_missing_error(
                        &UserProperty::Status.to_string(),
                        false,
                        Some(format!(
                            "Response missing MQTT user property '{}'",
                            UserProperty::Status
                        )),
                        None,
                    ));
                }
            }
        };

        // Get HLC here since we will need it no matter what type of result we are processing
        let timestamp = response_aio_data
            .get(&UserProperty::Timestamp)
            .map(|s| HybridLogicalClock::from_str(s))
            .transpose()?;

        // Process result based on status code
        let command_result = match status_code {
            // Response with payload
            StatusCode::Ok | StatusCode::NoContent => {
                let content_type = publish_properties.content_type;
                let format_indicator = publish_properties.payload_format_indicator.try_into().unwrap_or_else(|e| {
                    log::error!("Received invalid payload format indicator: {e}. This should not be possible to receive from the broker. Using default.");
                    FormatIndicator::default()
                });

                if matches!(status_code, StatusCode::NoContent) && !value.payload.is_empty() {
                    return Err(AIOProtocolError::new_payload_invalid_error(
                        false,
                        false,
                        None,
                        Some("Status code 204 (No Content) should not have a payload".to_string()),
                        None,
                    ));
                }

                let payload = match TResp::deserialize(
                    &value.payload,
                    content_type.as_ref(),
                    &format_indicator,
                ) {
                    Ok(payload) => payload,
                    Err(DeserializationError::InvalidPayload(e)) => {
                        return Err(AIOProtocolError::new_payload_invalid_error(
                            false,
                            false,
                            Some(e.into()),
                            None,
                            None,
                        ));
                    }
                    Err(DeserializationError::UnsupportedContentType(message)) => {
                        return Err(AIOProtocolError::new_header_invalid_error(
                            "Content Type",
                            &content_type.unwrap_or("None".to_string()),
                            false,
                            Some(message),
                            None,
                        ));
                    }
                };

                Self::Ok(Response {
                    payload,
                    content_type,
                    format_indicator,
                    custom_user_data: response_custom_user_data,
                    timestamp,
                })
            }
            // RemoteError
            _ => Self::Err(RemoteError {
                status_code,
                protocol_version,
                status_message: response_aio_data.remove(&UserProperty::StatusMessage),
                is_application_error: response_aio_data
                    .get(&UserProperty::IsApplicationError)
                    .is_some_and(|v| v == "true"),
                invalid_property_name: response_aio_data.remove(&UserProperty::InvalidPropertyName),
                invalid_property_value: response_aio_data
                    .remove(&UserProperty::InvalidPropertyValue),
                timestamp,
                supported_protocol_major_versions: response_aio_data
                    .get(&UserProperty::SupportedMajorVersions)
                    .map(|s| parse_supported_protocol_major_versions(s)),
            }),
        };
        Ok(command_result)
    }
}

/// Command Invoker Options struct
#[derive(Builder, Clone)]
#[builder(setter(into))]
pub struct Options {
    /// Topic pattern for the command request.
    /// Must align with [topic-structure.md](https://github.com/Azure/iot-operations-sdks/blob/main/doc/reference/topic-structure.md)
    request_topic_pattern: String,
    /// Topic pattern for the command response.
    /// Must align with [topic-structure.md](https://github.com/Azure/iot-operations-sdks/blob/main/doc/reference/topic-structure.md).
    /// If all response topic options are `None`, the response topic will be generated
    /// based on the request topic in the form: `clients/<client_id>/<request_topic>`
    #[builder(default = "None")]
    response_topic_pattern: Option<String>,
    /// Command name
    command_name: String,
    /// Optional Topic namespace to be prepended to the topic patterns
    #[builder(default = "None")]
    topic_namespace: Option<String>,
    /// Topic token keys/values to be permanently replaced in the topic pattern
    #[builder(default)]
    topic_token_map: HashMap<String, String>,
    /// Prefix for the response topic.
    /// If all response topic options are `None`, the response topic will be generated
    /// based on the request topic in the form: `clients/<client_id>/<request_topic>`
    #[builder(default = "None")]
    response_topic_prefix: Option<String>,
    /// Suffix for the response topic.
    /// If all response topic options are `None`, the response topic will be generated
    /// based on the request topic in the form: `clients/<client_id>/<request_topic>`
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
/// # use azure_iot_operations_protocol::rpc_command;
/// # use azure_iot_operations_protocol::application::ApplicationContextBuilder;
/// # let mut connection_settings = MqttConnectionSettingsBuilder::default()
/// #     .client_id("test_client")
/// #     .hostname("mqtt://localhost")
/// #     .tcp_port(1883u16)
/// #     .build().unwrap();
/// # let mut session_options = SessionOptionsBuilder::default()
/// #     .connection_settings(connection_settings)
/// #     .build().unwrap();
/// # let mqtt_session = Session::new(session_options).unwrap();
/// # let application_context = ApplicationContextBuilder::default().build().unwrap();;
/// let invoker_options = rpc_command::invoker::OptionsBuilder::default()
///   .request_topic_pattern("test/request")
///   .response_topic_pattern("test/response".to_string())
///   .command_name("test_command")
///   .topic_namespace("test_namespace".to_string())
///   .topic_token_map(HashMap::from([("invokerClientId".to_string(), "test_client".to_string())]))
///   .response_topic_prefix("custom/{invokerClientId}".to_string())
///   .build().unwrap();
/// # tokio_test::block_on(async {
/// let invoker: rpc_command::Invoker<Vec<u8>, Vec<u8>, _> = rpc_command::Invoker::new(application_context, mqtt_session.create_managed_client(), invoker_options).unwrap();
/// let request = rpc_command::invoker::RequestBuilder::default()
///   .payload(Vec::new()).unwrap()
///   .timeout(Duration::from_secs(2))
///   .topic_tokens(HashMap::from([("executorId".to_string(), "test_executor".to_string())]))
///   .build().unwrap();
/// let result = invoker.invoke(request);
/// //let response: Response<Vec<u8>> = result.await.unwrap();
/// # })
/// ```
pub struct Invoker<TReq, TResp, C>
where
    TReq: PayloadSerialize + 'static,
    TResp: PayloadSerialize + 'static,
    C: ManagedClient + Clone + Send + Sync + 'static,
    C::PubReceiver: Send + Sync + 'static,
{
    // static properties of the invoker
    application_hlc: Arc<ApplicationHybridLogicalClock>,
    mqtt_client: C,
    command_name: String,
    request_topic_pattern: TopicPattern,
    response_topic_pattern: TopicPattern,
    request_payload_type: PhantomData<TReq>,
    response_payload_type: PhantomData<TResp>,
    // Describes state
    invoker_state_mutex: Arc<Mutex<State>>,
    // Used to send information to manage state
    shutdown_notifier: Arc<Notify>,
    response_tx: Sender<Option<Publish>>,
}

/// Describes state of invoker to know whether to subscribe/unsubscribe/reject invokes
enum State {
    New,
    Subscribed,
    ShutdownInitiated,
    ShutdownSuccessful,
}

/// Implementation of Command Invoker.
impl<TReq, TResp, C> Invoker<TReq, TResp, C>
where
    TReq: PayloadSerialize + 'static,
    TResp: PayloadSerialize + 'static,
    C: ManagedClient + Clone + Send + Sync + 'static,
    C::PubReceiver: Send + Sync + 'static,
{
    /// Creates a new [`Invoker`].
    ///
    /// # Arguments
    /// * `application_context` - [`ApplicationContext`] that the command invoker is part of.
    /// * `client` - The MQTT client to use for communication.
    /// * `invoker_options` - Configuration options.
    ///
    /// Returns Ok([`Invoker`]) on success, otherwise returns [`AIOProtocolError`].
    /// # Errors
    /// [`AIOProtocolError`] of kind [`ConfigurationInvalid`](AIOProtocolErrorKind::ConfigurationInvalid) if:
    /// - [`command_name`](OptionsBuilder::command_name) is empty, whitespace or invalid
    /// - [`request_topic_pattern`](OptionsBuilder::request_topic_pattern) is empty or whitespace
    /// - [`response_topic_pattern`](OptionsBuilder::response_topic_pattern) is Some and empty or whitespace
    ///     - [`response_topic_pattern`](OptionsBuilder::response_topic_pattern) is None and
    ///         [`response_topic_prefix`](OptionsBuilder::response_topic_prefix) or
    ///         [`response_topic_suffix`](OptionsBuilder::response_topic_suffix) are Some and empty or whitespace
    /// - [`request_topic_pattern`](OptionsBuilder::request_topic_pattern),
    ///     [`response_topic_pattern`](OptionsBuilder::response_topic_pattern),
    ///     [`topic_namespace`](OptionsBuilder::topic_namespace),
    ///     [`response_topic_prefix`](OptionsBuilder::response_topic_prefix),
    ///     [`response_topic_suffix`](OptionsBuilder::response_topic_suffix),
    ///     are Some and invalid or contain a token with no valid replacement
    /// - [`topic_token_map`](OptionsBuilder::topic_token_map) isn't empty and contains invalid key(s)/token(s)
    pub fn new(
        application_context: ApplicationContext,
        client: C,
        invoker_options: Options,
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

            // If no options around the response topic have been specified
            // (no response topic or prefix/suffix), default to a well-known
            // pattern of clients/<client_id>/<request_topic>. This ensures
            // that the response topic is different from the request topic and lets
            // us document this pattern for auth configuration. Note that this does
            // not use any topic tokens, since we cannot guarantee their existence.
            if invoker_options.response_topic_prefix.is_none()
                && invoker_options.response_topic_suffix.is_none()
            {
                response_topic_pattern =
                    "clients/".to_owned() + client.client_id() + "/" + &response_topic_pattern;
            } else {
                if let Some(prefix) = invoker_options.response_topic_prefix {
                    // Validity check will be done within the topic processor
                    response_topic_pattern = prefix + "/" + &response_topic_pattern;
                }
                if let Some(suffix) = invoker_options.response_topic_suffix {
                    // Validity check will be done within the topic processor
                    response_topic_pattern = response_topic_pattern + "/" + &suffix;
                }
            }
        }

        // Generate the request and response topics
        let request_topic_pattern = TopicPattern::new(
            &invoker_options.request_topic_pattern,
            None,
            invoker_options.topic_namespace.as_deref(),
            &invoker_options.topic_token_map,
        )
        .map_err(|e| {
            AIOProtocolError::config_invalid_from_topic_pattern_error(
                e,
                "invoker_options.request_topic_pattern",
            )
        })?;

        let response_topic_pattern = TopicPattern::new(
            &response_topic_pattern,
            None,
            invoker_options.topic_namespace.as_deref(),
            &invoker_options.topic_token_map,
        )
        .map_err(|e| {
            AIOProtocolError::config_invalid_from_topic_pattern_error(e, "response_topic_pattern")
        })?;

        // Create mutex to track invoker state
        let invoker_state_mutex = Arc::new(Mutex::new(State::New));

        // Create a filtered receiver from the Managed Client
        let mqtt_receiver = match client
            .create_filtered_pub_receiver(&response_topic_pattern.as_subscribe_topic())
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

        // Create the shutdown notifier for the receiver loop
        let shutdown_notifier = Arc::new(Notify::new());

        // Start the receive response loop
        task::spawn({
            let response_tx_clone = response_tx.clone();
            let shutdown_notifier_clone = shutdown_notifier.clone();
            let command_name_clone = invoker_options.command_name.clone();
            async move {
                Self::receive_response_loop(
                    mqtt_receiver,
                    response_tx_clone,
                    shutdown_notifier_clone,
                    command_name_clone,
                )
                .await;
            }
        });

        Ok(Self {
            application_hlc: application_context.application_hlc,
            mqtt_client: client,
            command_name: invoker_options.command_name,
            request_topic_pattern,
            response_topic_pattern,
            request_payload_type: PhantomData,
            response_payload_type: PhantomData,
            invoker_state_mutex,
            shutdown_notifier,
            response_tx,
        })
    }

    /// Invokes a command.
    ///
    /// Returns Ok([`Response`]) on success, otherwise returns [`AIOProtocolError`].
    /// # Arguments
    /// * `request` - [`Request`] to invoke
    /// # Errors
    ///
    /// [`AIOProtocolError`] of kind [`ConfigurationInvalid`](AIOProtocolErrorKind::ConfigurationInvalid) if
    /// - any [`topic_tokens`](RequestBuilder::topic_tokens) are invalid
    ///
    /// [`AIOProtocolError`] of kind [`PayloadInvalid`](AIOProtocolErrorKind::PayloadInvalid) if
    /// - [`response_payload`][Response::payload] deserialization fails
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
    /// [`AIOProtocolError`] of kind [`Cancellation`](AIOProtocolErrorKind::Cancellation) if the [`Invoker`] has been dropped
    ///
    /// [`AIOProtocolError`] of kind [`HeaderInvalid`](AIOProtocolErrorKind::HeaderInvalid) if
    /// - The response's `content_type` isn't supported
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
    /// [`AIOProtocolError`] of kind [`ExecutionException`](AIOProtocolErrorKind::ExecutionException) if the response has a [`UserProperty::Status`] of [`StatusCode::InternalServerError`] and the [`UserProperty::IsApplicationError`] is true
    ///
    /// [`AIOProtocolError`] of kind [`InternalLogicError`](AIOProtocolErrorKind::InternalLogicError) if
    /// - the [`ApplicationHybridLogicalClock`]'s counter would be incremented and overflow beyond [`u64::MAX`]
    /// - the response has a [`UserProperty::Status`] of [`StatusCode::InternalServerError`], the [`UserProperty::IsApplicationError`] is false, and a [`UserProperty::InvalidPropertyName`] is provided
    ///
    /// [`AIOProtocolError`] of kind [`StateInvalid`](AIOProtocolErrorKind::StateInvalid) if
    /// - the [`ApplicationHybridLogicalClock`] or the received timestamp on the response is too far in the future
    /// - the response has a [`UserProperty::Status`] of [`StatusCode::ServiceUnavailable`]
    pub async fn invoke(
        &self,
        request: Request<TReq>,
    ) -> Result<Response<TResp>, AIOProtocolError> {
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
            .mqtt_client
            .subscribe(response_subscribe_topic, QoS::AtLeastOnce)
            .await;
        match subscribe_result {
            Ok(suback) => {
                // Wait for suback
                match suback.await {
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
                log::error!("[ERROR] client error while subscribing: {e}");
                return Err(AIOProtocolError::new_mqtt_error(
                    Some("Client error on command invoker subscribe".to_string()),
                    Box::new(e),
                    Some(self.command_name.clone()),
                ));
            }
        }
        Ok(())
    }

    async fn invoke_internal(
        &self,
        mut request: Request<TReq>,
    ) -> Result<Response<TResp>, AIOProtocolError> {
        // cancellation token to clean up spawned tasks if the invoke times out
        let cancellation_token = CancellationToken::new();
        let _drop_guard = cancellation_token.clone().drop_guard();
        // Validate parameters. Custom user data, timeout, and payload serialization have already been validated in RequestBuilder
        // Validate message expiry interval
        let message_expiry_interval: u32 = match request.timeout.as_secs().try_into() {
            Ok(val) => val,
            Err(_) => {
                // should be validated in RequestBuilder
                unreachable!();
            }
        };

        // Get request topic. Validates dynamic topic tokens
        let request_topic = self
            .request_topic_pattern
            .as_publish_topic(&request.topic_tokens)
            .map_err(|e| {
                AIOProtocolError::config_invalid_from_topic_pattern_error(
                    e,
                    "request_topic_pattern",
                )
            })?;
        // Get response topic. Validates dynamic topic tokens
        let response_topic = self
            .response_topic_pattern
            .as_publish_topic(&request.topic_tokens)
            .map_err(|e| {
                AIOProtocolError::config_invalid_from_topic_pattern_error(
                    e,
                    "response_topic_pattern",
                )
            })?;

        // Create correlation id
        let correlation_id = Uuid::new_v4();
        let correlation_data = Bytes::from(correlation_id.as_bytes().to_vec());

        // Get updated timestamp
        let timestamp_str = self.application_hlc.update_now()?;

        // Add internal user properties
        request.custom_user_data.push((
            UserProperty::SourceId.to_string(),
            self.mqtt_client.client_id().to_string(),
        ));
        request
            .custom_user_data
            .push((UserProperty::Timestamp.to_string(), timestamp_str));
        request.custom_user_data.push((
            UserProperty::ProtocolVersion.to_string(),
            RPC_COMMAND_PROTOCOL_VERSION.to_string(),
        ));
        request.custom_user_data.push((
            PARTITION_KEY.to_string(),
            self.mqtt_client.client_id().to_string(),
        ));

        // Create MQTT Properties
        let publish_properties = PublishProperties {
            correlation_data: Some(correlation_data.clone()),
            response_topic: Some(response_topic),
            payload_format_indicator: Some(request.serialized_payload.format_indicator as u8),
            content_type: Some(request.serialized_payload.content_type.to_string()),
            message_expiry_interval: Some(message_expiry_interval),
            user_properties: request.custom_user_data,
            topic_alias: None,
            subscription_identifiers: Vec::new(),
        };

        // Subscribe to the response topic if we're not already subscribed and the invoker hasn't been shutdown
        {
            let mut invoker_state = self.invoker_state_mutex.lock().await;
            match *invoker_state {
                State::New => {
                    self.subscribe_to_response_filter().await?;
                    *invoker_state = State::Subscribed;
                }
                State::Subscribed => { /* No-op, already subscribed */ }
                State::ShutdownInitiated | State::ShutdownSuccessful => {
                    return Err(AIOProtocolError::new_cancellation_error(
                        false,
                        None,
                        Some(
                            "Command Invoker has been shutdown and can no longer invoke commands"
                                .to_string(),
                        ),
                        Some(self.command_name.clone()),
                    ));
                }
            }
            // Allow other concurrent invoke commands to acquire the invoker_state lock
        }

        // Create receiver for response
        let mut response_rx = self.response_tx.subscribe();

        // Send publish
        let publish_result = self
            .mqtt_client
            .publish_with_properties(
                request_topic,
                QoS::AtLeastOnce,
                false,
                request.serialized_payload.payload,
                publish_properties,
            )
            .await;

        // Await for publish to complete in a task that concurrently polls the response_rx
        // so that the response_tx won't lag if the puback takes long to return
        // TODO: this could be fixed more elegantly by using a dispatcher instead of a broadcast channel for the response_tx/rx
        let pub_task = tokio::task::spawn({
            let command_name = self.command_name.clone();
            let ct = cancellation_token.clone();
            async move {
                match publish_result {
                    Ok(publish_completion_token) => {
                        // Wait for and handle the puback
                        tokio::select! {
                            () = ct.cancelled() => {
                                // This error won't actually be returned as this only happens if the invoke has already returned a timeout error
                                // This branch is just here to make sure this task ends
                                Err(AIOProtocolError::new_timeout_error(
                                    false,
                                    None,
                                    &command_name,
                                    request.timeout,
                                    None,
                                    Some(command_name.clone()),
                                ))
                            },
                            puback = publish_completion_token => {
                                match puback {
                                    // if puback is Ok, continue and wait for the response
                                    Ok(()) => Ok(()),
                                    Err(e) => {
                                        log::error!("[ERROR] puback error: {e}");
                                        Err(AIOProtocolError::new_mqtt_error(
                                            Some("MQTT Error on command invoke puback".to_string()),
                                            Box::new(e),
                                            Some(command_name),
                                        ))
                                    }
                                }
                            }
                        }
                    }
                    Err(e) => {
                        log::error!("[ERROR] client error while publishing: {e}");
                        Err(AIOProtocolError::new_mqtt_error(
                            Some("Client error on command invoker request publish".to_string()),
                            Box::new(e),
                            Some(command_name),
                        ))
                    }
                }
            }
        });
        // task to receive incoming responses and check for the one that is for this request
        let response_task = tokio::task::spawn({
            let command_name = self.command_name.clone();
            let ct = cancellation_token.clone();
            async move {
                loop {
                    // wait for incoming pub
                    tokio::select! {
                        () = ct.cancelled() => {
                            // This error won't be returned as this only happens if the invoke has already returned a timeout error
                            // This branch is just here to make sure this task ends
                            return Err(AIOProtocolError::new_timeout_error(
                                false,
                                None,
                                &command_name,
                                request.timeout,
                                None,
                                Some(command_name.clone()),
                            ));
                        },
                        res = response_rx.recv() => {
                            match res {
                                Ok(rsp_pub) => {
                                    if let Some(rsp_pub) = rsp_pub {
                                        // check correlation id for match, otherwise loop again
                                        if let Some(ref rsp_properties) = rsp_pub.properties {
                                            if let Some(ref response_correlation_data) =
                                                rsp_properties.correlation_data
                                            {
                                                if *response_correlation_data == correlation_data {
                                                    // This is implicit validation of the correlation data - if it's malformed it won't match the request
                                                    // This is the response for this request, stop listening for more responses and validate and parse it and send it back to the application
                                                    return Ok(rsp_pub);
                                                }
                                            }
                                        }
                                    } else {
                                        log::error!(
                                            "Command Invoker has been shutdown and will no longer receive a response"
                                        );
                                        return Err(AIOProtocolError::new_cancellation_error(
                                            false,
                                            None,
                                            Some(
                                                "Command Invoker has been shutdown and will no longer receive a response"
                                                    .to_string(),
                                            ),
                                            Some(command_name),
                                        ));
                                    }
                                    // If the publish doesn't have properties, correlation_data, or the correlation data doesn't match, keep waiting for the next one
                                }
                                Err(RecvError::Lagged(e)) => {
                                    log::error!(
                                        "[ERROR] Invoker response receiver lagged. Response may not be received. Number of skipped messages: {e}"
                                    );
                                    // Keep waiting for response even though it may have gotten overwritten.
                                    continue;
                                }
                                Err(RecvError::Closed) => {
                                    log::error!(
                                        "[ERROR] MQTT Receiver has been cleaned up and will no longer send a response"
                                    );
                                    return Err(AIOProtocolError::new_cancellation_error(
                                        false,
                                        None,
                                        Some(
                                            "MQTT Receiver has been cleaned up and will no longer send a response"
                                                .to_string(),
                                        ),
                                        Some(command_name),
                                    ));
                                }
                            } else {
                                log::error!(
                                    "Command Invoker has been shutdown and will no longer receive a response"
                                );
                                return Err(AIOProtocolError::new_cancellation_error(
                                    false,
                                    None,
                                    Some(
                                        "Command Invoker has been shutdown and will no longer receive a response"
                                            .to_string(),
                                    ),
                                    Some(command_name),
                                ));
                            }
                            // If the publish doesn't have properties, correlation_data, or the correlation data doesn't match, keep waiting for the next one
                        }
                        Err(RecvError::Lagged(e)) => {
                            log::error!(
                                "[ERROR] Invoker response receiver lagged. Response may not be received. Number of skipped messages: {e}"
                            );
                            // Keep waiting for response even though it may have gotten overwritten.
                            continue;
                        }
                        Err(RecvError::Closed) => {
                            log::error!(
                                "[ERROR] MQTT Receiver has been cleaned up and will no longer send a response"
                            );
                            return Err(AIOProtocolError::new_cancellation_error(
                                false,
                                None,
                                Some(
                                    "MQTT Receiver has been cleaned up and will no longer send a response"
                                        .to_string(),
                                ),
                                Some(command_name),
                            ));
                        }
                    }
                }
            }
        });

        // wait for pub to be completed and response to be received, immediately returning any errors returned.
        let rsp_pub = match tokio::try_join!(flatten(pub_task), flatten(response_task)) {
            Ok(((), rsp_pub)) => rsp_pub,
            // Return any error that occurs
            Err(e) => {
                return Err(e);
            }
        };

        // validate and parse the response pub that is for this request
        let command_result: CommandResult<TResp> =
            rsp_pub.try_into().map_err(|mut e: AIOProtocolError| {
                // Add command name to the error
                e.command_name = Some(self.command_name.clone());
                e
            })?;

        match command_result {
            CommandResult::Ok(response) => {
                // Update application HLC
                if let Some(hlc) = &response.timestamp {
                    self.application_hlc.update(hlc).map_err(|e| {
                        let mut aio_error: AIOProtocolError = e.into();
                        aio_error.command_name = Some(self.command_name.clone());
                        aio_error
                    })?;
                }
                Ok(response)
            }
            CommandResult::Err(remote_e) => {
                // Update application HLC
                if let Some(hlc) = &remote_e.timestamp {
                    self.application_hlc.update(hlc).map_err(|e| {
                        let mut aio_error: AIOProtocolError = e.into();
                        aio_error.command_name = Some(self.command_name.clone());
                        aio_error
                    })?;
                }
                // Convert into AIOProtocolError and return
                let mut aio_e: AIOProtocolError = remote_e.into();
                aio_e.command_name = Some(self.command_name.clone());
                Err(aio_e)
            }
        }
    }

    async fn receive_response_loop(
        mut mqtt_receiver: C::PubReceiver,
        response_tx: Sender<Option<Publish>>,
        shutdown_notifier: Arc<Notify>,
        command_name: String,
    ) {
        loop {
            tokio::select! {
                  // on shutdown/drop, we will be notified so that we can stop receiving any more messages
                  // The loop will continue to receive any more publishes that are already in the queue
                  () = shutdown_notifier.notified() => {
                    mqtt_receiver.close();
                    log::info!("[{command_name}] MQTT Receiver closed");
                  },
                  recv_result = mqtt_receiver.recv_manual_ack() => {
                    if let Some((m, ack_token)) = recv_result {
                        // Send to pending command listeners
                        match response_tx.send(Some(m)) {
                            Ok(_) => { },
                            Err(e) => {
                                log::debug!("[{command_name}] Message ignored, no pending commands: {e}");
                            }
                        }
                        // Manually ack
                        if let Some(ack_token) = ack_token {
                            // Ack in separate task to avoid blocking the receiver loop
                            tokio::task::spawn({
                                let command_name_clone = command_name.clone();
                                async move {
                                    match ack_token.ack().await {
                                        Ok(_) => { },
                                        Err(e) => {
                                            log::error!("[{command_name_clone}] Error acking message: {e}");
                                        }
                                    }
                                }
                            });
                        }
                    } else {
                        // if this fails, it's just because there are no more pending commands, which is fine
                        _ = response_tx.send(None);
                        log::info!("[{command_name}] No more command responses will be received.");
                        break;
                    }
                }
            }
        }
    }

    /// Shutdown the [`Invoker`]. Unsubscribes from the response topic and closes the
    /// MQTT receiver to stop receiving messages.
    ///
    /// Note: If this method is called, the [`Invoker`] should not be used again.
    /// If the method returns an error, it may be called again to attempt the unsubscribe again.
    ///
    /// Returns Ok(()) on success, otherwise returns [`AIOProtocolError`].
    /// # Errors
    /// [`AIOProtocolError`] of kind [`ClientError`](crate::common::aio_protocol_error::AIOProtocolErrorKind::ClientError) if the unsubscribe fails or if the unsuback reason code doesn't indicate success.
    pub async fn shutdown(&self) -> Result<(), AIOProtocolError> {
        // Notify the receiver loop to close the MQTT Receiver
        self.shutdown_notifier.notify_one();

        let mut invoker_state_mutex_guard = self.invoker_state_mutex.lock().await;
        match *invoker_state_mutex_guard {
            State::New | State::ShutdownSuccessful => {
                /* If we didn't call subscribe or shutdown has already been called successfully, skip unsubscribing */
            }
            State::ShutdownInitiated | State::Subscribed => {
                // if anything causes this to fail, we should still consider the invoker shutdown, but unsuccessfully, so that no more invocations can be made
                *invoker_state_mutex_guard = State::ShutdownInitiated;
                let unsubscribe_result = self
                    .mqtt_client
                    .unsubscribe(self.response_topic_pattern.as_subscribe_topic())
                    .await;

                match unsubscribe_result {
                    Ok(unsub_completion_token) => {
                        match unsub_completion_token.await {
                            Ok(()) => { /* Success */ }
                            Err(e) => {
                                log::error!("[{}] Unsuback error: {e}", self.command_name);
                                return Err(AIOProtocolError::new_mqtt_error(
                                    Some("MQTT error on command invoker unsuback".to_string()),
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
                            Some("Client error on command invoker unsubscribe".to_string()),
                            Box::new(e),
                            Some(self.command_name.clone()),
                        ));
                    }
                }
            }
        }

        log::info!("[{}] Shutdown", self.command_name);
        // If we successfully unsubscribed or did not need to, we can consider the invoker successfully shutdown
        *invoker_state_mutex_guard = State::ShutdownSuccessful;
        Ok(())
    }
}

impl<TReq, TResp, C> Drop for Invoker<TReq, TResp, C>
where
    TReq: PayloadSerialize + 'static,
    TResp: PayloadSerialize + 'static,
    C: ManagedClient + Clone + Send + Sync + 'static,
    C::PubReceiver: Send + Sync + 'static,
{
    fn drop(&mut self) {
        // drop can't be async, but we can spawn a task to unsubscribe
        tokio::spawn({
            let invoker_state_mutex = self.invoker_state_mutex.clone();
            let unsubscribe_filter = self.response_topic_pattern.as_subscribe_topic();
            let mqtt_client = self.mqtt_client.clone();
            async move { drop_unsubscribe(mqtt_client, invoker_state_mutex, unsubscribe_filter).await }
        });

        // Notify the receiver loop to close the MQTT receiver
        self.shutdown_notifier.notify_one();
        log::info!("[{}] Invoker has been dropped", self.command_name);
    }
}

async fn drop_unsubscribe<C: ManagedClient + Clone + Send + Sync + 'static>(
    mqtt_client: C,
    invoker_state_mutex: Arc<Mutex<State>>,
    unsubscribe_filter: String,
) {
    let mut invoker_state_mutex_guard = invoker_state_mutex.lock().await;
    match *invoker_state_mutex_guard {
        State::New | State::ShutdownSuccessful => {
            /* If we didn't call subscribe or shutdown has already been called successfully, skip unsubscribing */
        }
        State::ShutdownInitiated | State::Subscribed => {
            // if anything causes this to fail, we should still consider the invoker shutdown, but unsuccessfully, so that no more invocations can be made
            *invoker_state_mutex_guard = State::ShutdownInitiated;
            match mqtt_client.unsubscribe(unsubscribe_filter.clone()).await {
                Ok(_) => {
                    log::debug!(
                        "Unsubscribe sent on topic {unsubscribe_filter}. Unsuback may still be pending."
                    );
                }
                Err(e) => {
                    log::error!("Unsubscribe error on topic {unsubscribe_filter}: {e}");
                }
            }
        }
    }

    // If we successfully unsubscribed or did not need to, we can consider the invoker successfully shutdown
    *invoker_state_mutex_guard = State::ShutdownSuccessful;
}

/// convenience fn to flatten the result of a `JoinHandle`
async fn flatten<T>(
    handle: JoinHandle<Result<T, AIOProtocolError>>,
) -> Result<T, AIOProtocolError> {
    match handle.await {
        Ok(Ok(result)) => Ok(result),
        Ok(Err(e)) => Err(e),
        Err(e) => {
            // tasks can't panic
            log::error!("Join Error: {e}");
            unreachable!()
        }
    }
}

#[cfg(test)]
mod tests {
    use test_case::test_case;
    // TODO: This dependency on MqttConnectionSettingsBuilder should be removed in lieu of using a true mock
    use azure_iot_operations_mqtt::MqttConnectionSettingsBuilder;
    use azure_iot_operations_mqtt::session::{Session, SessionOptionsBuilder};

    use super::*;
    use crate::application::ApplicationContextBuilder;
    use crate::common::{
        aio_protocol_error::AIOProtocolErrorKind,
        payload_serialize::{DESERIALIZE_MTX, FormatIndicator, MockPayload},
    };

    // TODO: This should return a mock ManagedClient instead.
    // Until that's possible, need to return a Session so that the Session doesn't go out of
    // scope and render the ManagedClient unable to to be used correctly.
    fn create_session() -> Session {
        let connection_settings = MqttConnectionSettingsBuilder::default()
            .hostname("localhost")
            .client_id("test_client")
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
            ("commandName".to_string(), "test_command_name".to_string()),
            ("invokerClientId".to_string(), "test_client".to_string()),
        ])
    }

    #[tokio::test]
    async fn test_new_defaults() {
        let session = create_session();
        let managed_client = session.create_managed_client();
        let invoker_options = OptionsBuilder::default()
            .request_topic_pattern("test/{commandName}/{executorId}/request")
            .command_name("test_command_name")
            .topic_token_map(create_topic_tokens())
            .build()
            .unwrap();

        let invoker: Invoker<MockPayload, MockPayload, _> = Invoker::new(
            ApplicationContextBuilder::default().build().unwrap(),
            managed_client,
            invoker_options,
        )
        .unwrap();
        assert_eq!(
            invoker.response_topic_pattern.as_subscribe_topic(),
            "clients/test_client/test/test_command_name/+/request"
        );
    }

    #[tokio::test]
    async fn test_new_override_defaults() {
        let session = create_session();
        let managed_client = session.create_managed_client();
        let invoker_options = OptionsBuilder::default()
            .request_topic_pattern("test/{commandName}/{executorId}/request")
            .response_topic_pattern("test/{commandName}/{executorId}/response".to_string())
            .command_name("test_command_name")
            .topic_namespace("test_namespace".to_string())
            .topic_token_map(create_topic_tokens())
            .response_topic_prefix("custom/{invokerClientId}".to_string())
            .response_topic_suffix("custom/response".to_string())
            .build()
            .unwrap();

        let invoker: Invoker<MockPayload, MockPayload, _> = Invoker::new(
            ApplicationContextBuilder::default().build().unwrap(),
            managed_client,
            invoker_options,
        )
        .unwrap();
        // prefix and suffix should be ignored if response_topic_pattern is provided
        assert_eq!(
            invoker.response_topic_pattern.as_subscribe_topic(),
            "test_namespace/test/test_command_name/+/response"
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
        let session = create_session();
        let managed_client = session.create_managed_client();

        let mut command_name = "test_command_name".to_string();
        let mut request_topic_pattern = "test/req/topic".to_string();
        let mut response_topic_pattern = None;
        let mut response_topic_prefix = "custom/prefix".to_string();
        let mut response_topic_suffix = "custom/suffix".to_string();

        let error_property_name;
        let mut error_property_value = property_value.to_string();

        match property_name {
            "command_name" => {
                command_name = property_value.to_string();
                error_property_name = "command_name";
            }
            "request_topic_pattern" => {
                request_topic_pattern = property_value.to_string();
                error_property_name = "invoker_options.request_topic_pattern";
            }
            "response_topic_pattern" => {
                response_topic_pattern = Some(property_value.to_string());
                error_property_name = "response_topic_pattern";
            }
            "response_topic_prefix" => {
                response_topic_prefix = property_value.to_string();
                error_property_name = "response_topic_pattern";
                error_property_value.push_str("/test/req/topic/custom/suffix");
            }
            "response_topic_suffix" => {
                response_topic_suffix = property_value.to_string();
                error_property_name = "response_topic_pattern";
                error_property_value = "custom/prefix/test/req/topic/".to_string();
                error_property_value.push_str(&response_topic_suffix);
            }
            _ => panic!("Invalid property_name"),
        }

        let invoker_options = OptionsBuilder::default()
            .request_topic_pattern(request_topic_pattern)
            .response_topic_pattern(response_topic_pattern)
            .response_topic_prefix(response_topic_prefix)
            .response_topic_suffix(response_topic_suffix)
            .command_name(command_name)
            .build()
            .unwrap();

        let invoker: Result<Invoker<MockPayload, MockPayload, _>, AIOProtocolError> = Invoker::new(
            ApplicationContextBuilder::default().build().unwrap(),
            managed_client,
            invoker_options,
        );
        match invoker {
            Ok(_) => panic!("Expected error"),
            Err(e) => {
                assert_eq!(e.kind, AIOProtocolErrorKind::ConfigurationInvalid);
                assert!(e.is_shallow);
                assert!(!e.is_remote);
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
    #[test_case(None, None, "clients/test_client/test/req/topic"; "new_none_response_topic_prefix_and_suffix")]
    #[tokio::test]
    async fn test_new_response_pattern_prefix_suffix_args(
        response_topic_prefix: Option<String>,
        response_topic_suffix: Option<String>,
        expected_response_topic_subscribe_pattern: &str,
    ) {
        let session = create_session();
        let managed_client = session.create_managed_client();

        let command_name = "test_command_name".to_string();
        let request_topic_pattern = "test/req/topic".to_string();

        let invoker_options = OptionsBuilder::default()
            .request_topic_pattern(request_topic_pattern)
            .command_name(command_name)
            .response_topic_prefix(response_topic_prefix)
            .response_topic_suffix(response_topic_suffix)
            .build()
            .unwrap();

        let invoker: Result<Invoker<MockPayload, MockPayload, _>, AIOProtocolError> = Invoker::new(
            ApplicationContextBuilder::default().build().unwrap(),
            managed_client,
            invoker_options,
        );
        assert!(invoker.is_ok());
        assert_eq!(
            invoker.unwrap().response_topic_pattern.as_subscribe_topic(),
            expected_response_topic_subscribe_pattern
        );
    }

    // If response pattern prefix/suffix are not specified, the default response topic prefix is used
    #[tokio::test]
    async fn test_new_response_pattern_default_prefix() {
        let session = create_session();
        let managed_client = session.create_managed_client();
        let command_name = "test_command_name";
        let request_topic_pattern = "test/req/topic";

        let invoker_options = OptionsBuilder::default()
            .request_topic_pattern(request_topic_pattern)
            .command_name(command_name)
            .topic_token_map(create_topic_tokens())
            .build()
            .unwrap();
        let invoker: Result<Invoker<MockPayload, MockPayload, _>, AIOProtocolError> = Invoker::new(
            ApplicationContextBuilder::default().build().unwrap(),
            managed_client,
            invoker_options,
        );
        assert!(invoker.is_ok());
        assert_eq!(
            invoker.unwrap().response_topic_pattern.as_subscribe_topic(),
            "clients/test_client/test/req/topic"
        );
    }

    // If response pattern suffix is specified, there is no prefix added
    #[tokio::test]
    async fn test_new_response_pattern_only_suffix() {
        let session = create_session();
        let managed_client = session.create_managed_client();
        let command_name = "test_command_name";
        let request_topic_pattern = "test/req/topic";
        let response_topic_suffix = "custom/suffix";

        let invoker_options = OptionsBuilder::default()
            .request_topic_pattern(request_topic_pattern)
            .command_name(command_name)
            .topic_token_map(create_topic_tokens())
            .response_topic_suffix(response_topic_suffix.to_string())
            .build()
            .unwrap();
        let invoker: Result<Invoker<MockPayload, MockPayload, _>, AIOProtocolError> = Invoker::new(
            ApplicationContextBuilder::default().build().unwrap(),
            managed_client,
            invoker_options,
        );
        assert!(invoker.is_ok());
        assert_eq!(
            invoker.unwrap().response_topic_pattern.as_subscribe_topic(),
            "test/req/topic/custom/suffix"
        );
    }

    /// Tests success: Timeout specified on invoke and there is no error
    #[tokio::test]
    #[ignore] // test ignored because waiting for the suback hangs forever. Leaving the test for now until we have a full testing framework
    async fn test_invoke_timeout_parameter() {
        // Get mutexes for checking static PayloadSerialize calls
        let _deserialize_mutex = DESERIALIZE_MTX.lock();
        let session = create_session();
        let managed_client = session.create_managed_client();
        let invoker_options = OptionsBuilder::default()
            .request_topic_pattern("test/req/topic")
            .command_name("test_command_name")
            .topic_token_map(create_topic_tokens())
            .build()
            .unwrap();

        let invoker: Invoker<MockPayload, MockPayload, _> = Invoker::new(
            ApplicationContextBuilder::default().build().unwrap(),
            managed_client,
            invoker_options,
        )
        .unwrap();

        let mut mock_request_payload = MockPayload::new();
        mock_request_payload
            .expect_serialize()
            .returning(|| {
                Ok(SerializedPayload {
                    payload: Vec::new(),
                    content_type: "application/json".to_string(),
                    format_indicator: FormatIndicator::Utf8EncodedCharacterData,
                })
            })
            .times(1);

        // TODO: Check for
        //      sub sent (suback received)?
        //      pub sent (puback received)
        //      pub received (puback sent)

        // Mock context to track deserialize calls
        let mock_payload_deserialize_ctx = MockPayload::deserialize_context();
        // Deserialize should be called on the incoming response payload
        mock_payload_deserialize_ctx
            .expect()
            .returning(|_, _, _| {
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
        let mut invoker_state = invoker.invoker_state_mutex.lock().await;
        *invoker_state = State::Subscribed;
        drop(invoker_state);

        let response = invoker
            .invoke(
                RequestBuilder::default()
                    .payload(mock_request_payload)
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
        let session = create_session();
        let managed_client = session.create_managed_client();
        let invoker_options = OptionsBuilder::default()
            .request_topic_pattern("test/req/topic")
            .command_name("test_command_name")
            .topic_token_map(create_topic_tokens())
            .build()
            .unwrap();

        let invoker: Invoker<MockPayload, MockPayload, _> = Invoker::new(
            ApplicationContextBuilder::default().build().unwrap(),
            managed_client,
            invoker_options,
        )
        .unwrap();

        let mut mock_request_payload = MockPayload::new();
        mock_request_payload
            .expect_serialize()
            .returning(|| {
                Ok(SerializedPayload {
                    payload: Vec::new(),
                    content_type: "application/json".to_string(),
                    format_indicator: FormatIndicator::Utf8EncodedCharacterData,
                })
            })
            .times(1);

        // TODO: Check for
        //      sub sent (suback received)
        //      pub sent (puback received)
        //      pub not received

        let response = invoker
            .invoke(
                RequestBuilder::default()
                    .payload(mock_request_payload)
                    .unwrap()
                    .timeout(Duration::from_secs(1))
                    .build()
                    .unwrap(),
            )
            .await;
        match response {
            Ok(_) => panic!("Expected error"),
            Err(e) => {
                assert_eq!(e.kind, AIOProtocolErrorKind::Timeout);
                assert!(!e.is_shallow);
                assert!(!e.is_remote);
                assert_eq!(e.timeout_name, Some("test_command_name".to_string()));
                assert!(e.timeout_value == Some(Duration::from_secs(1)));
            }
        }
    }

    // Tests failure: Invocation times out (valid timeout value less than a second but not zero specified on invoke)
    // and a `Timeout` error is returned
    #[tokio::test]
    async fn test_invoke_times_out_timeout_rounded() {
        let session = create_session();
        let managed_client = session.create_managed_client();
        let invoker_options = OptionsBuilder::default()
            .request_topic_pattern("test/req/topic")
            .command_name("test_command_name")
            .topic_token_map(create_topic_tokens())
            .build()
            .unwrap();

        let invoker: Invoker<MockPayload, MockPayload, _> = Invoker::new(
            ApplicationContextBuilder::default().build().unwrap(),
            managed_client,
            invoker_options,
        )
        .unwrap();

        let mut mock_request_payload = MockPayload::new();
        mock_request_payload
            .expect_serialize()
            .returning(|| {
                Ok(SerializedPayload {
                    payload: Vec::new(),
                    content_type: "application/json".to_string(),
                    format_indicator: FormatIndicator::Utf8EncodedCharacterData,
                })
            })
            .times(1);

        // TODO: Check for
        //      sub sent (suback received)
        //      pub sent (puback received)
        //      pub not received

        let response = invoker
            .invoke(
                RequestBuilder::default()
                    .payload(mock_request_payload)
                    .unwrap()
                    .timeout(Duration::from_nanos(1))
                    .build()
                    .unwrap(),
            )
            .await;
        match response {
            Ok(_) => panic!("Expected error"),
            Err(e) => {
                assert_eq!(e.kind, AIOProtocolErrorKind::Timeout);
                assert!(!e.is_shallow);
                assert!(!e.is_remote);
                assert_eq!(e.timeout_name, Some("test_command_name".to_string()));
                assert!(e.timeout_value == Some(Duration::from_secs(1)));
            }
        }
    }

    #[tokio::test]
    #[ignore] // test ignored because waiting for the suback hangs forever. Leaving the test for now until we have a full testing framework
    async fn test_invoke_deserialize_error() {
        // Get mutexes for checking static PayloadSerialize calls
        let _deserialize_mutex = DESERIALIZE_MTX.lock();

        let session = create_session();
        let managed_client = session.create_managed_client();
        let invoker_options = OptionsBuilder::default()
            .request_topic_pattern("test/req/topic")
            .command_name("test_command_name")
            .topic_token_map(create_topic_tokens())
            .build()
            .unwrap();

        let invoker: Invoker<MockPayload, MockPayload, _> = Invoker::new(
            ApplicationContextBuilder::default().build().unwrap(),
            managed_client,
            invoker_options,
        )
        .unwrap();

        let mut mock_request_payload = MockPayload::new();
        mock_request_payload
            .expect_serialize()
            .returning(|| {
                Ok(SerializedPayload {
                    payload: Vec::new(),
                    content_type: "application/json".to_string(),
                    format_indicator: FormatIndicator::Utf8EncodedCharacterData,
                })
            })
            .times(1);

        // TODO: Check for
        //      sub sent (suback received)?
        //      pub sent (puback received)
        //      pub received (puback sent)

        // Mock context to track deserialize calls
        let mock_payload_deserialize_ctx = MockPayload::deserialize_context();
        // Deserialize should be called on the incoming response payload
        mock_payload_deserialize_ctx
            .expect()
            .returning(|_, _, _| {
                Err(DeserializationError::InvalidPayload(
                    "dummy error".to_string(),
                ))
            })
            .once();

        // Mock invoker being subscribed already so we don't wait for suback
        let mut invoker_state = invoker.invoker_state_mutex.lock().await;
        *invoker_state = State::Subscribed;
        drop(invoker_state);

        let response = invoker
            .invoke(
                RequestBuilder::default()
                    .payload(mock_request_payload)
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
                assert!(!e.is_shallow);
                assert!(!e.is_remote);
                assert!(e.nested_error.is_some());
            }
        }
    }

    #[tokio::test]
    async fn test_invoke_executor_id_invalid_value() {
        let session = create_session();
        let managed_client = session.create_managed_client();
        let invoker_options = OptionsBuilder::default()
            .request_topic_pattern("test/req/{executorId}/topic")
            .command_name("test_command_name")
            .topic_token_map(create_topic_tokens())
            .build()
            .unwrap();

        let invoker: Invoker<MockPayload, MockPayload, _> = Invoker::new(
            ApplicationContextBuilder::default().build().unwrap(),
            managed_client,
            invoker_options,
        )
        .unwrap();
        let mut mock_request_payload = MockPayload::new();
        mock_request_payload
            .expect_serialize()
            .returning(|| {
                Ok(SerializedPayload {
                    payload: Vec::new(),
                    content_type: "application/json".to_string(),
                    format_indicator: FormatIndicator::Utf8EncodedCharacterData,
                })
            })
            .times(1);

        let response = invoker
            .invoke(
                RequestBuilder::default()
                    .payload(mock_request_payload)
                    .unwrap()
                    .timeout(Duration::from_secs(2))
                    .topic_tokens(HashMap::from([(
                        "executorId".to_string(),
                        "+++".to_string(),
                    )]))
                    .build()
                    .unwrap(),
            )
            .await;
        match response {
            Ok(_) => panic!("Expected error"),
            Err(e) => {
                assert_eq!(e.kind, AIOProtocolErrorKind::ConfigurationInvalid);
                assert!(e.is_shallow);
                assert!(!e.is_remote);
                assert_eq!(e.property_name, Some("executorId".to_string()));
                assert!(e.property_value == Some(Value::String("+++".to_string())));
            }
        }
    }

    #[tokio::test]
    async fn test_invoke_missing_token() {
        let session = create_session();
        let managed_client = session.create_managed_client();
        let invoker_options = OptionsBuilder::default()
            .request_topic_pattern("test/req/{executorId}/topic")
            .command_name("test_command_name")
            .topic_token_map(create_topic_tokens())
            .build()
            .unwrap();

        let invoker: Invoker<MockPayload, MockPayload, _> = Invoker::new(
            ApplicationContextBuilder::default().build().unwrap(),
            managed_client,
            invoker_options,
        )
        .unwrap();
        let mut mock_request_payload = MockPayload::new();
        mock_request_payload
            .expect_serialize()
            .returning(|| {
                Ok(SerializedPayload {
                    payload: Vec::new(),
                    content_type: "application/json".to_string(),
                    format_indicator: FormatIndicator::Utf8EncodedCharacterData,
                })
            })
            .times(1);

        let response = invoker
            .invoke(
                RequestBuilder::default()
                    .payload(mock_request_payload)
                    .unwrap()
                    .timeout(Duration::from_secs(2))
                    .topic_tokens(HashMap::new())
                    .build()
                    .unwrap(),
            )
            .await;

        match response {
            Ok(_) => panic!("Expected error"),
            Err(e) => {
                assert_eq!(e.kind, AIOProtocolErrorKind::ConfigurationInvalid);
                assert!(e.is_shallow);
                assert!(!e.is_remote);
                assert_eq!(e.property_name, Some("executorId".to_string()));
                assert_eq!(e.property_value, Some(Value::String(String::new())));
            }
        }
    }

    #[test]
    fn test_request_serialization_error() {
        let mut mock_request_payload = MockPayload::new();
        mock_request_payload
            .expect_serialize()
            .returning(|| Err("dummy error".to_string()))
            .times(1);

        let mut binding = RequestBuilder::default();
        let req_builder = binding.payload(mock_request_payload);
        match req_builder {
            Err(e) => {
                assert_eq!(e.kind, AIOProtocolErrorKind::PayloadInvalid);
            }
            Ok(_) => {
                panic!("Expected error");
            }
        }
    }

    #[test]
    fn test_request_serialization_bad_content_type_error() {
        let mut mock_request_payload = MockPayload::new();
        mock_request_payload
            .expect_serialize()
            .returning(|| {
                Ok(SerializedPayload {
                    payload: Vec::new(),
                    content_type: "application/json\u{0000}".to_string(),
                    format_indicator: FormatIndicator::Utf8EncodedCharacterData,
                })
            })
            .times(1);

        let mut binding = RequestBuilder::default();
        let req_builder = binding.payload(mock_request_payload);
        match req_builder {
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

    /// Tests failure: Timeout specified as 0 (invalid value) on invoke and an `ArgumentInvalid` error is returned
    #[test_case(Duration::from_secs(0); "invoke_timeout_0")]
    /// Tests failure: Timeout specified as > u32::max (invalid value) on invoke and an `ArgumentInvalid` error is returned
    #[test_case(Duration::from_secs(u64::from(u32::MAX) + 1); "invoke_timeout_u32_max")]
    fn test_request_timeout_invalid_value(timeout: Duration) {
        let mut mock_request_payload = MockPayload::new();
        mock_request_payload
            .expect_serialize()
            .returning(|| {
                Ok(SerializedPayload {
                    payload: Vec::new(),
                    content_type: "application/json".to_string(),
                    format_indicator: FormatIndicator::Utf8EncodedCharacterData,
                })
            })
            .times(1);

        let request_builder_result = RequestBuilder::default()
            .payload(mock_request_payload)
            .unwrap()
            .timeout(timeout)
            .build();

        assert!(request_builder_result.is_err());
    }

    /// Tests success: application_error_headers() returns no Application Error Code and Payload since custom_user_data has none.
    #[tokio::test]
    async fn test_no_app_error_code_and_payload() {
        let user_data: Vec<(String, String)> = Vec::new();

        let (application_error_code, application_error_payload) =
            application_error_headers(&user_data);
        assert!(application_error_code.is_none());
        assert!(application_error_payload.is_none());
    }

    /// Tests success: custom_user_data contains both Application Error Code and Payload.
    #[tokio::test]
    async fn test_response_with_app_error_code_and_payload() {
        let error_code_content = "5888";
        let error_payload_content = "5888 is a fictitious error code";

        let custom_user_data = vec![
            ("AppErrCode".into(), error_code_content.into()),
            ("AppErrPayload".into(), error_payload_content.into()),
        ];

        assert_eq!(custom_user_data.len(), 2);

        let (application_error_code, application_error_payload) =
            application_error_headers(&custom_user_data);
        assert_eq!(application_error_code, Some(error_code_content.into()));
        assert_eq!(
            application_error_payload,
            Some(error_payload_content.into())
        );
    }

    /// Tests success: custom_user_data contains Application Error Code, but no Payload.
    #[tokio::test]
    async fn test_response_with_app_error_code_but_no_payload() {
        let error_code_content = "5888";

        let custom_user_data = vec![("AppErrCode".into(), error_code_content.into())];

        assert_eq!(custom_user_data.len(), 1);

        let (application_error_code, application_error_payload) =
            application_error_headers(&custom_user_data);
        assert_eq!(application_error_code, Some(error_code_content.into()));
        assert!(application_error_payload.is_none());
    }
}

// Command Request tests
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
//     shutdown notifier is notified and the MQTT receiver is closed
//     mqtt_receiver sends a lagged error
//     mqtt_receiver sends a closed error
// Tests failure:
//     should never fail
//
// validation
// Tests success:
//     content type isn't present
//     format indicator not present, 0, or (1 and TResp format indicator is 1)
//     valid timestamp present and is returned on the Command Response
//     no timestamp is present
//     status is a number and is one of StatusCode's enum values
//     in_application is interpreted as false if it is anything other than "true" and there are no errors parsing this
//     custom user properties are correctly passed to the application.
//     status code is no content and the payload is empty
//     test matrix for different statuses and fields present for an error response - see possible return values from invoke for full list
//     response payload deserializes successfully
// Tests failure:
//     content type isn't supported. 'HeaderInvalid' error returned
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
