// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

use std::str::FromStr;
use std::time::SystemTime;
use std::{collections::HashMap, marker::PhantomData, time::Duration};

use azure_iot_operations_mqtt::control_packet::{PublishProperties, QoS};
use azure_iot_operations_mqtt::interface::ManagedClient;
use bytes::Bytes;
use chrono::{DateTime, Utc};
use uuid::Uuid;

use crate::telemetry::cloud_event::{
    CloudEventFields, DEFAULT_CLOUD_EVENT_EVENT_TYPE, DEFAULT_CLOUD_EVENT_SPEC_VERSION,
};
use crate::{
    common::{
        aio_protocol_error::{AIOProtocolError, Value},
        hybrid_logical_clock::HybridLogicalClock,
        is_invalid_utf8,
        payload_serialize::PayloadSerialize,
        topic_processor::TopicPattern,
        user_properties::{validate_user_properties, UserProperty},
    },
    AIO_PROTOCOL_VERSION,
};

/// Cloud Event struct
///
/// Implements the cloud event spec 1.0 for the telemetry sender.
/// See [CloudEvents Spec](https://github.com/cloudevents/spec/blob/main/cloudevents/spec.md).
#[derive(Builder, Clone, Debug)]
#[builder(setter(into), build_fn(validate = "Self::validate"))]
pub struct CloudEvent {
    /// Identifies the context in which an event happened. Often this will include information such
    /// as the type of the event source, the organization publishing the event or the process that
    /// produced the event. The exact syntax and semantics behind the data encoded in the URI is
    /// defined by the event producer.
    source: String,
    /// The version of the cloud events specification which the event uses. This enables the
    /// interpretation of the context. Compliant event producers MUST use a value of 1.0 when
    /// referring to this version of the specification.
    #[builder(default = "DEFAULT_CLOUD_EVENT_SPEC_VERSION.to_string()")]
    spec_version: String,
    /// Contains a value describing the type of event related to the originating occurrence. Often
    /// this attribute is used for routing, observability, policy enforcement, etc. The format of
    /// this is producer defined and might include information such as the version of the type.
    #[builder(default = "DEFAULT_CLOUD_EVENT_EVENT_TYPE.to_string()")]
    event_type: String,
    /// Identifies the schema that data adheres to. Incompatible changes to the schema SHOULD be
    /// reflected by a different URI.
    #[builder(default = "None")]
    data_schema: Option<String>,
}

impl CloudEventBuilder {
    fn validate(&self) -> Result<(), String> {
        let mut spec_version = DEFAULT_CLOUD_EVENT_SPEC_VERSION.to_string();

        if let Some(sv) = &self.spec_version {
            CloudEventFields::SpecVersion.validate(sv, &spec_version)?;
            spec_version = sv.to_string();
        }

        if let Some(source) = &self.source {
            CloudEventFields::Source.validate(source, &spec_version)?;
        }

        if let Some(event_type) = &self.event_type {
            CloudEventFields::EventType.validate(event_type, &spec_version)?;
        }

        if let Some(Some(data_schema)) = &self.data_schema {
            CloudEventFields::DataSchema.validate(data_schema, &spec_version)?;
        }

        Ok(())
    }
}

impl CloudEvent {
    /// Get [`CloudEvent`] as headers for an MQTT message
    #[must_use]
    fn into_headers(self, subject: &str, content_type: &str) -> Vec<(String, String)> {
        let mut headers = vec![
            (CloudEventFields::Id.to_string(), Uuid::new_v4().to_string()),
            (CloudEventFields::Source.to_string(), self.source),
            (CloudEventFields::SpecVersion.to_string(), self.spec_version),
            (CloudEventFields::EventType.to_string(), self.event_type),
            (CloudEventFields::Subject.to_string(), subject.to_string()),
            (
                CloudEventFields::DataContentType.to_string(),
                content_type.to_string(),
            ),
            (
                CloudEventFields::Time.to_string(),
                DateTime::<Utc>::from(SystemTime::now()).to_rfc3339(),
            ),
        ];
        if let Some(data_schema) = self.data_schema {
            headers.push((CloudEventFields::DataSchema.to_string(), data_schema));
        }
        headers
    }
}

/// Telemetry Message struct
/// Used by the telemetry sender.
#[derive(Builder, Clone, Debug)]
#[builder(setter(into), build_fn(validate = "Self::validate"))]
pub struct TelemetryMessage<T: PayloadSerialize> {
    /// Payload of the telemetry message. Must implement [`PayloadSerialize`].
    #[builder(setter(custom))]
    payload: Vec<u8>,
    /// Strongly link `TelemetryMessage` with type `T`
    #[builder(private)]
    message_payload_type: PhantomData<T>,
    /// Quality of Service of the telemetry message. Can only be `AtMostOnce` or `AtLeastOnce`.
    #[builder(default = "QoS::AtLeastOnce")]
    qos: QoS,
    /// User data that will be set as custom MQTT User Properties on the telemetry message.
    /// Can be used to pass additional metadata to the receiver.
    /// Default is an empty `Vec`.
    #[builder(default)]
    custom_user_data: Vec<(String, String)>,
    /// Topic token keys/values to be replaced into the publish topic of the telemetry message.
    #[builder(default)]
    topic_tokens: HashMap<String, String>,
    /// Message expiry for the message. Will be used as the `message_expiry_interval` in the MQTT
    /// properties. Default is 10 seconds.
    #[builder(default = "Duration::from_secs(10)")]
    message_expiry: Duration,
    /// Cloud event of the telemetry message.
    #[builder(default = "None")]
    cloud_event: Option<CloudEvent>,
}

impl<T: PayloadSerialize> TelemetryMessageBuilder<T> {
    /// Add a payload to the telemetry message. Validates successful serialization of the payload.
    ///
    /// # Errors
    /// Returns a [`PayloadSerialize::Error`] if serialization of the payload fails
    pub fn payload(&mut self, payload: &T) -> Result<&mut Self, T::Error> {
        let serialized_payload = payload.serialize()?;
        self.payload = Some(serialized_payload);
        self.message_payload_type = Some(PhantomData);
        Ok(self)
    }

    /// Validate the telemetry message.
    ///
    /// # Errors
    /// Returns a `String` describing the error if
    ///     - any of `custom_user_data's` keys is a reserved Cloud Event key
    ///     - any of `custom_user_data`'s keys start with the [`RESERVED_PREFIX`](user_properties::RESERVED_PREFIX)
    ///     - any of `custom_user_data`'s keys or values are invalid utf-8
    ///     - `message_expiry` is not zero and < 1 ms or > `u32::max`
    ///     - Quality of Service is not `AtMostOnce` or `AtLeastOnce`
    fn validate(&self) -> Result<(), String> {
        if let Some(custom_user_data) = &self.custom_user_data {
            for (key, _) in custom_user_data {
                if CloudEventFields::from_str(key).is_ok() {
                    return Err(format!(
                        "Invalid user data property '{key}' is a reserved Cloud Event key"
                    ));
                }
            }
            validate_user_properties(custom_user_data)?;
        }
        if let Some(timeout) = &self.message_expiry {
            // If timeout is set, it must be at least 1 ms. If zero, message will never expire.
            if !timeout.is_zero() && timeout.as_millis() < 1 {
                return Err("Timeout must be at least 1 ms if it is greater than 0".to_string());
            }
            match <u64 as TryInto<u32>>::try_into(timeout.as_secs()) {
                Ok(_) => {}
                Err(_) => {
                    return Err("Timeout in seconds must be less than or equal to u32::max to be used as message_expiry_interval".to_string());
                }
            }
        }
        if let Some(qos) = &self.qos {
            if *qos != QoS::AtMostOnce && *qos != QoS::AtLeastOnce {
                return Err("QoS must be AtMostOnce or AtLeastOnce".to_string());
            }
        }
        Ok(())
    }
}

/// Telemetry Sender Options struct
#[derive(Builder, Clone)]
#[builder(setter(into, strip_option))]
pub struct TelemetrySenderOptions {
    // TODO: Update topic-structure link to the correct one once available.
    /// Topic pattern for the telemetry message
    /// Must align with [topic-structure.md](https://github.com/microsoft/mqtt-patterns/blob/main/docs/specs/topic-structure.md)
    topic_pattern: String,
    /// Optional Topic namespace to be prepended to the topic pattern
    #[builder(default = "None")]
    topic_namespace: Option<String>,
    /// Topic token keys/values to be permanently replaced in the topic pattern
    #[builder(default)]
    topic_token_map: HashMap<String, String>,
}

/// Telemetry Sender struct
/// # Example
/// ```
/// # use std::{collections::HashMap, time::Duration};
/// # use tokio_test::block_on;
/// # use azure_iot_operations_mqtt::control_packet::QoS;
/// # use azure_iot_operations_mqtt::MqttConnectionSettingsBuilder;
/// # use azure_iot_operations_mqtt::session::{Session, SessionOptionsBuilder};
/// # use azure_iot_operations_protocol::telemetry::telemetry_sender::{TelemetrySender, TelemetryMessageBuilder, TelemetrySenderOptionsBuilder};
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
/// #     .client_id("test_client")
/// #     .hostname("mqtt://localhost")
/// #     .tcp_port(1883u16)
/// #     .build().unwrap();
/// # let mut session_options = SessionOptionsBuilder::default()
/// #     .connection_settings(connection_settings)
/// #     .build().unwrap();
/// # let mut mqtt_session = Session::new(session_options).unwrap();
/// let sender_options = TelemetrySenderOptionsBuilder::default()
///   .topic_pattern("test/telemetry")
///   .topic_namespace("test_namespace")
///   .topic_token_map(HashMap::new())
///   .build().unwrap();
/// let telemetry_sender: TelemetrySender<SamplePayload, _> = TelemetrySender::new(mqtt_session.create_managed_client(), sender_options).unwrap();
/// let telemetry_message = TelemetryMessageBuilder::default()
///   .payload(&SamplePayload {}).unwrap()
///   .qos(QoS::AtLeastOnce)
///   .build().unwrap();
/// # tokio_test::block_on(async {
/// // let result = telemetry_sender.send(telemetry_message).await.unwrap();
/// # })
/// ```
///
pub struct TelemetrySender<T, C>
where
    T: PayloadSerialize,
    C: ManagedClient + Send + Sync + 'static,
{
    mqtt_client: C,
    message_payload_type: PhantomData<T>,
    topic_pattern: TopicPattern,
}

/// Implementation of Telemetry Sender
impl<T, C> TelemetrySender<T, C>
where
    T: PayloadSerialize,
    C: ManagedClient + Send + Sync + 'static,
{
    /// Creates a new [`TelemetrySender`].
    ///
    /// Returns Ok([`TelemetrySender`]) on success, otherwise returns [`AIOProtocolError`].
    /// # Errors
    /// [`AIOProtocolError`] of kind [`ConfigurationInvalid`](crate::common::aio_protocol_error::AIOProtocolErrorKind::ConfigurationInvalid) if
    /// - [`topic_pattern`](TelemetrySenderOptions::topic_pattern) is empty or whitespace
    /// - [`topic_pattern`](TelemetrySenderOptions::topic_pattern),
    ///     [`topic_namespace`](TelemetrySenderOptions::topic_namespace),
    ///     are Some and invalid or contain a token with no valid replacement
    /// - [`topic_token_map`](TelemetrySenderOptions::topic_token_map) isn't empty and contains invalid key(s)/token(s)
    /// - Content type of the telemetry message is not valid utf-8
    #[allow(clippy::needless_pass_by_value)]
    pub fn new(
        client: C,
        sender_options: TelemetrySenderOptions,
    ) -> Result<Self, AIOProtocolError> {
        // Validate content type of telemetry message is valid UTF-8
        if is_invalid_utf8(T::content_type()) {
            return Err(AIOProtocolError::new_configuration_invalid_error(
                None,
                "content_type",
                Value::String(T::content_type().to_string()),
                Some(format!(
                    "Content type '{}' of telemetry message type is not valid UTF-8",
                    T::content_type()
                )),
                None,
            ));
        }
        // Validate parameters
        let topic_pattern = TopicPattern::new(
            &sender_options.topic_pattern,
            sender_options.topic_namespace.as_deref(),
            &sender_options.topic_token_map,
        )?;

        Ok(Self {
            mqtt_client: client,
            message_payload_type: PhantomData,
            topic_pattern,
        })
    }

    /// Sends a [`TelemetryMessage`].
    ///
    /// Returns `Ok(())` on success, otherwise returns [`AIOProtocolError`].
    /// # Arguments
    /// * `message` - [`TelemetryMessage`] to send
    /// # Errors
    /// [`AIOProtocolError`] of kind [`PayloadInvalid`](crate::common::aio_protocol_error::AIOProtocolErrorKind::PayloadInvalid) if
    /// - [`payload`][TelemetryMessage::payload]'s content type isn't valid utf-8
    ///
    /// [`AIOProtocolError`] of kind [`MqttError`](crate::common::aio_protocol_error::AIOProtocolErrorKind::ClientError) if
    /// - The publish fails
    /// - The puback reason code doesn't indicate success.
    pub async fn send(&self, mut message: TelemetryMessage<T>) -> Result<(), AIOProtocolError> {
        // Validate parameters. Custom user data, timeout, QoS, and payload serialization have already been validated in TelemetryMessageBuilder
        let message_expiry_interval: u32 = match message.message_expiry.as_secs().try_into() {
            Ok(val) => val,
            Err(_) => {
                // should be validated in TelemetryMessageBuilder
                unreachable!();
            }
        };

        // Get topic.
        let message_topic = self.topic_pattern.as_publish_topic(&message.topic_tokens)?;

        // Create timestamp
        let timestamp = HybridLogicalClock::new();

        // Create correlation id
        let correlation_id = Uuid::new_v4();
        let correlation_data = Bytes::from(correlation_id.as_bytes().to_vec());

        // Cloud Events headers
        if let Some(cloud_event) = message.cloud_event {
            let cloud_event_headers = cloud_event.into_headers(&message_topic, T::content_type());
            for (key, value) in cloud_event_headers {
                message.custom_user_data.push((key, value));
            }
        }

        // Add internal user properties
        message
            .custom_user_data
            .push((UserProperty::Timestamp.to_string(), timestamp.to_string()));

        message.custom_user_data.push((
            UserProperty::ProtocolVersion.to_string(),
            AIO_PROTOCOL_VERSION.to_string(),
        ));

        message.custom_user_data.push((
            UserProperty::SourceId.to_string(),
            self.mqtt_client.client_id().to_string(),
        ));

        // Create MQTT Properties
        let publish_properties = PublishProperties {
            correlation_data: Some(correlation_data),
            response_topic: None,
            payload_format_indicator: Some(T::format_indicator() as u8),
            content_type: Some(T::content_type().to_string()),
            message_expiry_interval: Some(message_expiry_interval),
            user_properties: message.custom_user_data,
            topic_alias: None,
            subscription_identifiers: Vec::new(),
        };

        // Send publish
        let publish_result = self
            .mqtt_client
            .publish_with_properties(
                message_topic,
                message.qos,
                false,
                message.payload,
                publish_properties,
            )
            .await;

        match publish_result {
            Ok(publish_completion_token) => {
                // Wait for and handle the puback
                match publish_completion_token.await {
                    Ok(()) => Ok(()),
                    Err(e) => {
                        log::error!("Puback error: {e}");
                        Err(AIOProtocolError::new_mqtt_error(
                            Some("MQTT Error on telemetry send puback".to_string()),
                            Box::new(e),
                            None,
                        ))
                    }
                }
            }
            Err(e) => {
                log::error!("Publish error: {e}");
                Err(AIOProtocolError::new_mqtt_error(
                    Some("MQTT Error on telemetry send publish".to_string()),
                    Box::new(e),
                    None,
                ))
            }
        }
    }
}

#[cfg(test)]
mod tests {
    use std::{collections::HashMap, time::Duration};

    use test_case::test_case;

    use crate::{
        common::{
            aio_protocol_error::{AIOProtocolError, AIOProtocolErrorKind, Value},
            payload_serialize::{FormatIndicator, MockPayload, PayloadSerialize, CONTENT_TYPE_MTX},
        },
        telemetry::telemetry_sender::{
            TelemetryMessageBuilder, TelemetrySender, TelemetrySenderOptionsBuilder,
        },
    };
    use azure_iot_operations_mqtt::{
        session::{Session, SessionOptionsBuilder},
        MqttConnectionSettingsBuilder,
    };

    // Payload that has an invalid content type for testing
    struct InvalidContentTypePayload {}
    impl Clone for InvalidContentTypePayload {
        fn clone(&self) -> Self {
            unimplemented!()
        }
    }
    impl PayloadSerialize for InvalidContentTypePayload {
        type Error = String;
        fn content_type() -> &'static str {
            "application/json\u{0000}"
        }
        fn format_indicator() -> FormatIndicator {
            unimplemented!()
        }
        fn serialize(&self) -> Result<Vec<u8>, String> {
            unimplemented!()
        }
        fn deserialize(_payload: &[u8]) -> Result<Self, String> {
            unimplemented!()
        }
    }

    // TODO: This should return a mock MqttProvider instead
    fn get_session() -> Session {
        // TODO: Make a real mock that implements MqttProvider
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

    #[test]
    fn test_new_defaults() {
        // Get mutex lock for content type
        let _content_type_mutex = CONTENT_TYPE_MTX.lock();
        // Mock context to track content_type calls
        let mock_payload_content_type_ctx = MockPayload::content_type_context();
        let _mock_payload_content_type = mock_payload_content_type_ctx
            .expect()
            .returning(|| "application/json");

        let session = get_session();
        let sender_options = TelemetrySenderOptionsBuilder::default()
            .topic_pattern("test/test_telemetry")
            .build()
            .unwrap();

        TelemetrySender::<MockPayload, _>::new(session.create_managed_client(), sender_options)
            .unwrap();
    }

    #[test]
    fn test_new_override_defaults() {
        // Get mutex lock for content type
        let _content_type_mutex = CONTENT_TYPE_MTX.lock();
        // Mock context to track content_type calls
        let mock_payload_content_type_ctx = MockPayload::content_type_context();
        let _mock_payload_content_type = mock_payload_content_type_ctx
            .expect()
            .returning(|| "application/json");

        let session = get_session();
        let sender_options = TelemetrySenderOptionsBuilder::default()
            .topic_pattern("test/{telemetryName}")
            .topic_namespace("test_namespace")
            .topic_token_map(HashMap::from([(
                "telemetryName".to_string(),
                "test_telemetry".to_string(),
            )]))
            .build()
            .unwrap();

        TelemetrySender::<MockPayload, _>::new(session.create_managed_client(), sender_options)
            .unwrap();
    }

    #[test_case(""; "new_empty_topic_pattern")]
    #[test_case(" "; "new_whitespace_topic_pattern")]
    fn test_new_empty_topic_pattern(property_value: &str) {
        // Get mutex lock for content type
        let _content_type_mutex = CONTENT_TYPE_MTX.lock();
        // Mock context to track content_type calls
        let mock_payload_content_type_ctx = MockPayload::content_type_context();
        let _mock_payload_content_type = mock_payload_content_type_ctx
            .expect()
            .returning(|| "application/json");

        let session = get_session();

        let sender_options = TelemetrySenderOptionsBuilder::default()
            .topic_pattern(property_value)
            .build()
            .unwrap();

        let telemetry_sender: Result<TelemetrySender<MockPayload, _>, _> =
            TelemetrySender::new(session.create_managed_client(), sender_options);
        match telemetry_sender {
            Ok(_) => panic!("Expected error"),
            Err(e) => {
                assert_eq!(e.kind, AIOProtocolErrorKind::ConfigurationInvalid);
                assert!(!e.in_application);
                assert!(e.is_shallow);
                assert!(!e.is_remote);
                assert_eq!(e.http_status_code, None);
                assert_eq!(e.property_name, Some("pattern".to_string()));
                assert!(e.property_value == Some(Value::String(property_value.to_string())));
            }
        }
    }

    #[tokio::test]
    async fn test_send_serializer_invalid_content_type() {
        let session = get_session();
        let sender_options = TelemetrySenderOptionsBuilder::default()
            .topic_pattern("test/test_telemetry")
            .build()
            .unwrap();

        let telemetry_sender: Result<
            TelemetrySender<InvalidContentTypePayload, _>,
            AIOProtocolError,
        > = TelemetrySender::new(session.create_managed_client(), sender_options);

        match telemetry_sender {
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

    /// Tests failure: Timeout specified as > u32::max (invalid value) on send and an `ArgumentInvalid` error is returned
    #[test_case(Duration::from_secs(u64::from(u32::MAX) + 1); "send_timeout_u32_max")]
    /// Tests failure: Timeout specified as < 1ms (invalid value) on send and an `ArgumentInvalid` error is returned
    #[test_case(Duration::from_nanos(50); "send_timeout_less_1_ms")]
    fn test_send_timeout_invalid_value(timeout: Duration) {
        let mut mock_telemetry_payload = MockPayload::new();
        mock_telemetry_payload
            .expect_serialize()
            .returning(|| Ok(String::new().into()))
            .times(1);

        let message_builder_result = TelemetryMessageBuilder::default()
            .payload(&mock_telemetry_payload)
            .unwrap()
            .message_expiry(timeout)
            .build();

        assert!(message_builder_result.is_err());
    }

    #[test]
    fn test_send_qos_invalid_value() {
        let mut mock_telemetry_payload = MockPayload::new();
        mock_telemetry_payload
            .expect_serialize()
            .returning(|| Ok(String::new().into()))
            .times(1);

        let message_builder_result = TelemetryMessageBuilder::default()
            .payload(&mock_telemetry_payload)
            .unwrap()
            .qos(azure_iot_operations_mqtt::control_packet::QoS::ExactlyOnce)
            .build();

        assert!(message_builder_result.is_err());
    }

    #[test]
    fn test_send_invalid_custom_user_data_cloud_event_header() {
        let mut mock_telemetry_payload = MockPayload::new();
        mock_telemetry_payload
            .expect_serialize()
            .returning(|| Ok(String::new().into()))
            .times(1);

        let message_builder_result = TelemetryMessageBuilder::default()
            .payload(&mock_telemetry_payload)
            .unwrap()
            .custom_user_data(vec![("source".to_string(), "test".to_string())])
            .build();

        assert!(message_builder_result.is_err());
    }
}
