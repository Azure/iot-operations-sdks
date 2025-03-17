// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

use std::str::FromStr;
use std::sync::Arc;
use std::time::SystemTime;
use std::{collections::HashMap, marker::PhantomData, time::Duration};

use azure_iot_operations_mqtt::control_packet::{PublishProperties, QoS};
use azure_iot_operations_mqtt::interface::ManagedClient;
use bytes::Bytes;
use chrono::{DateTime, SecondsFormat, Utc};
use uuid::Uuid;

use crate::{
    application::{ApplicationContext, ApplicationHybridLogicalClock},
    common::{
        aio_protocol_error::{AIOProtocolError, Value},
        is_invalid_utf8,
        payload_serialize::{PayloadSerialize, SerializedPayload},
        topic_processor::TopicPattern,
        user_properties::{validate_user_properties, UserProperty},
    },
    telemetry::{
        cloud_event::{
            CloudEventFields, DEFAULT_CLOUD_EVENT_EVENT_TYPE, DEFAULT_CLOUD_EVENT_SPEC_VERSION,
        },
        TELEMETRY_PROTOCOL_VERSION,
    },
};

/// Cloud Event struct used by the [`TelemetrySender`].
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
    /// Identifies the event. Producers MUST ensure that source + id is unique for each distinct
    /// event. If a duplicate event is re-sent (e.g. due to a network error) it MAY have the same
    /// id. Consumers MAY assume that Events with identical source and id are duplicates.
    #[builder(default = "Uuid::new_v4().to_string()")]
    id: String,
    /// Timestamp of when the occurrence happened. If the time of the occurrence cannot be
    /// determined then this attribute MAY be set to some other time (such as the current time) by
    /// the cloud event producer, however all producers for the same source MUST be consistent in
    /// this respect. In other words, either they all use the actual time of the occurrence or they
    /// all use the same algorithm to determine the value used.
    #[builder(default = "Some(DateTime::<Utc>::from(SystemTime::now()))")]
    time: Option<DateTime<Utc>>,
    /// Identifies the subject of the event in the context of the event producer (identified by
    /// source). In publish-subscribe scenarios, a subscriber will typically subscribe to events
    /// emitted by a source, but the source identifier alone might not be sufficient as a qualifier
    /// for any specific event if the source context has internal sub-structure.
    #[builder(default = "CloudEventSubject::TelemetryTopic")]
    subject: CloudEventSubject,
}

/// Enum representing the different values that the [`subject`](CloudEvent::subject) field of a [`CloudEvent`] can take.
#[derive(Clone, Debug)]
pub enum CloudEventSubject {
    /// The telemetry topic should be used as the subject when the [`CloudEvent`] is sent across the wire
    TelemetryTopic,
    /// A custom (provided) `String` should be used for the `subject` of the [`CloudEvent`]
    Custom(String),
    /// No subject should be included on the [`CloudEvent`]
    None,
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

        if let Some(id) = &self.id {
            CloudEventFields::Id.validate(id, &spec_version)?;
        }

        if let Some(CloudEventSubject::Custom(subject)) = &self.subject {
            CloudEventFields::Subject.validate(subject, &spec_version)?;
        }

        // time does not need to be validated because converting it to an rfc3339 compliant string will always succeed

        Ok(())
    }
}

impl CloudEvent {
    /// Get [`CloudEvent`] as headers for an MQTT message
    #[must_use]
    fn into_headers(self, telemetry_topic: &str) -> Vec<(String, String)> {
        let mut headers = vec![
            (CloudEventFields::Id.to_string(), self.id),
            (CloudEventFields::Source.to_string(), self.source),
            (CloudEventFields::SpecVersion.to_string(), self.spec_version),
            (CloudEventFields::EventType.to_string(), self.event_type),
        ];
        match self.subject {
            CloudEventSubject::Custom(subject) => {
                headers.push((CloudEventFields::Subject.to_string(), subject));
            }
            CloudEventSubject::TelemetryTopic => {
                headers.push((
                    CloudEventFields::Subject.to_string(),
                    telemetry_topic.to_string(),
                ));
            }
            CloudEventSubject::None => {}
        }
        if let Some(time) = self.time {
            headers.push((
                CloudEventFields::Time.to_string(),
                time.to_rfc3339_opts(SecondsFormat::Secs, true),
            ));
        }
        if let Some(data_schema) = self.data_schema {
            headers.push((
                CloudEventFields::DataSchema.to_string(),
                data_schema.to_string(),
            ));
        }
        headers
    }
}

/// Telemetry Message struct.
/// Used by the [`TelemetrySender`].
#[derive(Builder, Clone, Debug)]
#[builder(setter(into), build_fn(validate = "Self::validate"))]
pub struct TelemetryMessage<T: PayloadSerialize> {
    /// Payload of the telemetry message. Must implement [`PayloadSerialize`].
    #[builder(setter(custom))]
    serialized_payload: SerializedPayload,
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
    #[builder(setter(custom))]
    message_expiry: Duration,
    /// Cloud event of the telemetry message.
    #[builder(default = "None")]
    cloud_event: Option<CloudEvent>,
}

impl<T: PayloadSerialize> TelemetryMessageBuilder<T> {
    /// Add a payload to the telemetry message. Validates successful serialization of the payload.
    ///
    /// # Errors
    /// [`AIOProtocolError`] of kind [`PayloadInvalid`](crate::common::aio_protocol_error::AIOProtocolErrorKind::PayloadInvalid) if serialization of the payload fails
    ///
    /// [`AIOProtocolError`] of kind [`ConfigurationInvalid`](crate::common::aio_protocol_error::AIOProtocolErrorKind::ConfigurationInvalid) if the content type is not valid utf-8
    pub fn payload(&mut self, payload: T) -> Result<&mut Self, AIOProtocolError> {
        match payload.serialize() {
            Err(e) => Err(AIOProtocolError::new_payload_invalid_error(
                true,
                false,
                Some(e.into()),
                Some("Payload serialization error".to_string()),
                None,
            )),
            Ok(serialized_payload) => {
                // Validate content type of telemetry message is valid UTF-8
                if is_invalid_utf8(&serialized_payload.content_type) {
                    return Err(AIOProtocolError::new_configuration_invalid_error(
                        None,
                        "content_type",
                        Value::String(serialized_payload.content_type.to_string()),
                        Some(format!(
                            "Content type '{}' of telemetry message type is not valid UTF-8",
                            serialized_payload.content_type
                        )),
                        None,
                    ));
                }
                self.serialized_payload = Some(serialized_payload);
                self.message_payload_type = Some(PhantomData);
                Ok(self)
            }
        }
    }

    /// Set the message expiry for the telemetry.
    ///
    /// Note: Will be rounded up to the nearest second.
    pub fn message_expiry(&mut self, message_expiry: Duration) -> &mut Self {
        self.message_expiry = Some(if message_expiry.subsec_nanos() != 0 {
            Duration::from_secs(message_expiry.as_secs().saturating_add(1))
        } else {
            message_expiry
        });

        self
    }

    /// Validate the telemetry message.
    ///
    /// # Errors
    /// Returns a `String` describing the error if
    ///     - any of `custom_user_data's` keys is a reserved Cloud Event key
    ///     - any of `custom_user_data`'s keys or values are invalid utf-8
    ///     - `message_expiry` is > `u32::max`
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
        // If there's a cloud event, make sure the content type is valid for the cloud event spec version
        if let Some(Some(cloud_event)) = &self.cloud_event {
            if let Some(serialized_payload) = &self.serialized_payload {
                CloudEventFields::DataContentType
                    .validate(&serialized_payload.content_type, &cloud_event.spec_version)?;
            }
        }
        Ok(())
    }
}

/// Telemetry Sender Options struct
#[derive(Builder, Clone)]
#[builder(setter(into, strip_option))]
pub struct TelemetrySenderOptions {
    /// Topic pattern for the telemetry message.
    /// Must align with [topic-structure.md](https://github.com/Azure/iot-operations-sdks/blob/main/doc/reference/topic-structure.md)
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
/// let sender_options = TelemetrySenderOptionsBuilder::default()
///   .topic_pattern("test/telemetry")
///   .topic_namespace("test_namespace")
///   .topic_token_map(HashMap::new())
///   .build().unwrap();
/// let telemetry_sender: TelemetrySender<Vec<u8>, _> = TelemetrySender::new(application_context, mqtt_session.create_managed_client(), sender_options).unwrap();
/// let telemetry_message = TelemetryMessageBuilder::default()
///   .payload(Vec::new()).unwrap()
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
    application_hlc: Arc<ApplicationHybridLogicalClock>,
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
    /// # Arguments
    /// * `application_context` - [`ApplicationContext`] that the telemetry sender is part of.
    /// * `client` - The MQTT client to use for telemetry communication.
    /// * `sender_options` - Configuration options.
    ///
    /// Returns Ok([`TelemetrySender`]) on success, otherwise returns [`AIOProtocolError`].
    /// # Errors
    /// [`AIOProtocolError`] of kind [`ConfigurationInvalid`](crate::common::aio_protocol_error::AIOProtocolErrorKind::ConfigurationInvalid) if
    /// - [`topic_pattern`](TelemetrySenderOptions::topic_pattern) is empty or whitespace
    /// - [`topic_pattern`](TelemetrySenderOptions::topic_pattern),
    ///     [`topic_namespace`](TelemetrySenderOptions::topic_namespace),
    ///     are Some and invalid or contain a token with no valid replacement
    /// - [`topic_token_map`](TelemetrySenderOptions::topic_token_map) isn't empty and contains invalid key(s)/token(s)
    #[allow(clippy::needless_pass_by_value)]
    pub fn new(
        application_context: ApplicationContext,
        client: C,
        sender_options: TelemetrySenderOptions,
    ) -> Result<Self, AIOProtocolError> {
        // Validate parameters
        let topic_pattern = TopicPattern::new(
            &sender_options.topic_pattern,
            None,
            sender_options.topic_namespace.as_deref(),
            &sender_options.topic_token_map,
        )
        .map_err(|e| {
            AIOProtocolError::config_invalid_from_topic_pattern_error(
                e,
                "sender_options.topic_pattern",
            )
        })?;

        Ok(Self {
            application_hlc: application_context.application_hlc,
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
    /// [`AIOProtocolError`] of kind [`MqttError`](crate::common::aio_protocol_error::AIOProtocolErrorKind::ClientError) if
    /// - The publish fails
    /// - The puback reason code doesn't indicate success.
    ///
    /// [`AIOProtocolError`] of kind [`InternalLogicError`](crate::common::aio_protocol_error::AIOProtocolErrorKind::InternalLogicError) if
    /// - the [`ApplicationHybridLogicalClock`]'s counter would be incremented and overflow beyond [`u64::MAX`] when preparing the timestamp for the message
    ///
    /// [`AIOProtocolError`] of kind [`StateInvalid`](crate::common::aio_protocol_error::AIOProtocolErrorKind::StateInvalid) if
    /// - the [`ApplicationHybridLogicalClock`]'s timestamp is too far in the future
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
        let message_topic = self
            .topic_pattern
            .as_publish_topic(&message.topic_tokens)
            .map_err(|e| {
                AIOProtocolError::config_invalid_from_topic_pattern_error(e, "message_topic")
            })?;

        // Get updated timestamp
        let timestamp_str = self.application_hlc.update_now()?;

        // Create correlation id
        let correlation_id = Uuid::new_v4();
        let correlation_data = Bytes::from(correlation_id.as_bytes().to_vec());

        // Cloud Events headers
        if let Some(cloud_event) = message.cloud_event {
            let cloud_event_headers = cloud_event.into_headers(&message_topic);
            for (key, value) in cloud_event_headers {
                message.custom_user_data.push((key, value));
            }
        }

        // Add internal user properties
        message
            .custom_user_data
            .push((UserProperty::Timestamp.to_string(), timestamp_str));

        message.custom_user_data.push((
            UserProperty::ProtocolVersion.to_string(),
            TELEMETRY_PROTOCOL_VERSION.to_string(),
        ));

        message.custom_user_data.push((
            UserProperty::SourceId.to_string(),
            self.mqtt_client.client_id().to_string(),
        ));

        // Create MQTT Properties
        let publish_properties = PublishProperties {
            correlation_data: Some(correlation_data),
            response_topic: None,
            payload_format_indicator: Some(message.serialized_payload.format_indicator as u8),
            content_type: Some(message.serialized_payload.content_type.to_string()),
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
                message.serialized_payload.payload,
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
        application::ApplicationContextBuilder,
        common::{
            aio_protocol_error::{AIOProtocolErrorKind, Value},
            payload_serialize::{FormatIndicator, MockPayload, SerializedPayload},
        },
        telemetry::telemetry_sender::{
            TelemetryMessageBuilder, TelemetrySender, TelemetrySenderOptionsBuilder,
        },
    };
    use azure_iot_operations_mqtt::{
        session::{Session, SessionOptionsBuilder},
        MqttConnectionSettingsBuilder,
    };

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
        let session = get_session();
        let sender_options = TelemetrySenderOptionsBuilder::default()
            .topic_pattern("test/test_telemetry")
            .build()
            .unwrap();

        TelemetrySender::<MockPayload, _>::new(
            ApplicationContextBuilder::default().build().unwrap(),
            session.create_managed_client(),
            sender_options,
        )
        .unwrap();
    }

    #[test]
    fn test_new_override_defaults() {
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

        TelemetrySender::<MockPayload, _>::new(
            ApplicationContextBuilder::default().build().unwrap(),
            session.create_managed_client(),
            sender_options,
        )
        .unwrap();
    }

    #[test_case(""; "new_empty_topic_pattern")]
    #[test_case(" "; "new_whitespace_topic_pattern")]
    fn test_new_empty_topic_pattern(property_value: &str) {
        let session = get_session();

        let sender_options = TelemetrySenderOptionsBuilder::default()
            .topic_pattern(property_value)
            .build()
            .unwrap();

        let telemetry_sender: Result<TelemetrySender<MockPayload, _>, _> = TelemetrySender::new(
            ApplicationContextBuilder::default().build().unwrap(),
            session.create_managed_client(),
            sender_options,
        );
        match telemetry_sender {
            Ok(_) => panic!("Expected error"),
            Err(e) => {
                assert_eq!(e.kind, AIOProtocolErrorKind::ConfigurationInvalid);
                assert!(e.is_shallow);
                assert!(!e.is_remote);
                assert_eq!(
                    e.property_name,
                    Some("sender_options.topic_pattern".to_string())
                );
                assert!(e.property_value == Some(Value::String(property_value.to_string())));
            }
        }
    }

    #[test]
    fn test_message_serialization_error() {
        let mut mock_telemetry_payload = MockPayload::new();
        mock_telemetry_payload
            .expect_serialize()
            .returning(|| Err("dummy error".to_string()))
            .times(1);

        let mut binding = TelemetryMessageBuilder::default();
        let message_builder = binding.payload(mock_telemetry_payload);
        match message_builder {
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
        let mut mock_telemetry_payload = MockPayload::new();
        mock_telemetry_payload
            .expect_serialize()
            .returning(|| {
                Ok(SerializedPayload {
                    payload: Vec::new(),
                    content_type: "application/json\u{0000}".to_string(),
                    format_indicator: FormatIndicator::Utf8EncodedCharacterData,
                })
            })
            .times(1);

        let mut binding = TelemetryMessageBuilder::default();
        let message_builder = binding.payload(mock_telemetry_payload);
        match message_builder {
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

    /// Tests failure: Timeout specified as > u32::max (invalid value) on send and an `ArgumentInvalid` error is returned
    #[test_case(Duration::from_secs(u64::from(u32::MAX) + 1); "send_timeout_u32_max")]
    fn test_send_timeout_invalid_value(timeout: Duration) {
        let mut mock_telemetry_payload = MockPayload::new();
        mock_telemetry_payload
            .expect_serialize()
            .returning(|| {
                Ok(SerializedPayload {
                    payload: String::new().into(),
                    content_type: "application/json".to_string(),
                    format_indicator: FormatIndicator::Utf8EncodedCharacterData,
                })
            })
            .times(1);

        let message_builder_result = TelemetryMessageBuilder::default()
            .payload(mock_telemetry_payload)
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
            .returning(|| {
                Ok(SerializedPayload {
                    payload: String::new().into(),
                    content_type: "application/json".to_string(),
                    format_indicator: FormatIndicator::Utf8EncodedCharacterData,
                })
            })
            .times(1);

        let message_builder_result = TelemetryMessageBuilder::default()
            .payload(mock_telemetry_payload)
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
            .returning(|| {
                Ok(SerializedPayload {
                    payload: String::new().into(),
                    content_type: "application/json".to_string(),
                    format_indicator: FormatIndicator::Utf8EncodedCharacterData,
                })
            })
            .times(1);

        let message_builder_result = TelemetryMessageBuilder::default()
            .payload(mock_telemetry_payload)
            .unwrap()
            .custom_user_data(vec![("source".to_string(), "test".to_string())])
            .build();

        assert!(message_builder_result.is_err());
    }
}
