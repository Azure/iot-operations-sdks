// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
use std::{collections::HashMap, marker::PhantomData, str::FromStr};

use azure_iot_operations_mqtt::{
    control_packet::QoS,
    interface::{self, ManagedClient, PubReceiver},
};
use chrono::{DateTime, Utc};
use tokio::{sync::oneshot, task::JoinSet};

use crate::{
    common::{
        aio_protocol_error::{AIOProtocolError, Value},
        hybrid_logical_clock::HybridLogicalClock,
        is_invalid_utf8,
        payload_serialize::PayloadSerialize,
        topic_processor::TopicPattern,
        user_properties::UserProperty,
    },
    DEFAULT_AIO_PROTOCOL_VERSION,
};
use crate::{
    telemetry::cloud_event::{CloudEventFields, DEFAULT_CLOUD_EVENT_SPEC_VERSION},
    ProtocolVersion,
};

const SUPPORTED_PROTOCOL_VERSIONS: &[u16] = &[1];

/// Cloud Event struct
///
/// Implements the cloud event spec 1.0 for the telemetry receiver.
/// See [CloudEvents Spec](https://github.com/cloudevents/spec/blob/main/cloudevents/spec.md).
#[derive(Builder, Clone, Debug)]
#[builder(setter(into), build_fn(validate = "Self::validate"))]
pub struct CloudEvent {
    /// Identifies the event. Producers MUST ensure that source + id is unique for each distinct
    /// event. If a duplicate event is re-sent (e.g. due to a network error) it MAY have the same
    /// id. Consumers MAY assume that Events with identical source and id are duplicates.
    pub id: String,
    /// Identifies the context in which an event happened. Often this will include information such
    /// as the type of the event source, the organization publishing the event or the process that
    /// produced the event. The exact syntax and semantics behind the data encoded in the URI is
    /// defined by the event producer.
    pub source: String,
    /// The version of the cloud events specification which the event uses. This enables the
    /// interpretation of the context. Compliant event producers MUST use a value of 1.0 when
    /// referring to this version of the specification.
    pub spec_version: String,
    /// Contains a value describing the type of event related to the originating occurrence. Often
    /// this attribute is used for routing, observability, policy enforcement, etc. The format of
    /// this is producer defined and might include information such as the version of the type.
    pub event_type: String,
    /// Identifies the subject of the event in the context of the event producer (identified by
    /// source). In publish-subscribe scenarios, a subscriber will typically subscribe to events
    /// emitted by a source, but the source identifier alone might not be sufficient as a qualifier
    /// for any specific event if the source context has internal sub-structure.
    #[builder(default = "None")]
    pub subject: Option<String>,
    /// Identifies the schema that data adheres to. Incompatible changes to the schema SHOULD be
    /// reflected by a different URI.
    #[builder(default = "None")]
    pub data_schema: Option<String>,
    /// Content type of data value. This attribute enables data to carry any type of content,
    /// whereby format and encoding might differ from that of the chosen event format.
    #[builder(default = "None")]
    pub data_content_type: Option<String>,
    /// Timestamp of when the occurrence happened. If the time of the occurrence cannot be
    /// determined then this attribute MAY be set to some other time (such as the current time) by
    /// the cloud event producer, however all producers for the same source MUST be consistent in
    /// this respect. In other words, either they all use the actual time of the occurrence or they
    /// all use the same algorithm to determine the value used.
    #[builder(default = "None")]
    pub time: Option<DateTime<Utc>>,
}

impl CloudEventBuilder {
    fn validate(&self) -> Result<(), String> {
        let mut spec_version = DEFAULT_CLOUD_EVENT_SPEC_VERSION.to_string();

        if let Some(sv) = &self.spec_version {
            CloudEventFields::SpecVersion.validate(sv, &spec_version)?;
            spec_version = sv.to_string();
        }

        if let Some(id) = &self.id {
            CloudEventFields::Id.validate(id, &spec_version)?;
        }

        if let Some(source) = &self.source {
            CloudEventFields::Source.validate(source, &spec_version)?;
        }

        if let Some(event_type) = &self.event_type {
            CloudEventFields::EventType.validate(event_type, &spec_version)?;
        }

        if let Some(Some(subject)) = &self.subject {
            CloudEventFields::Subject.validate(subject, &spec_version)?;
        }

        if let Some(Some(data_schema)) = &self.data_schema {
            CloudEventFields::DataSchema.validate(data_schema, &spec_version)?;
        }

        if let Some(Some(data_content_type)) = &self.data_content_type {
            CloudEventFields::DataContentType.validate(data_content_type, &spec_version)?;
        }

        Ok(())
    }
}

/// Acknowledgement token used to acknowledge a telemetry message.
/// Used by the [`TelemetryReceiver`].
///
/// When dropped without calling [`AckToken::ack`], the message is automatically acknowledged.
pub struct AckToken {
    ack_tx: oneshot::Sender<()>,
}

impl AckToken {
    /// Consumes the [`AckToken`] and acknowledges the telemetry message.
    pub fn ack(self) {
        match self.ack_tx.send(()) {
            Ok(()) => { /* Success */ }
            Err(()) => {
                log::error!("Ack error");
            }
        }
    }
}

/// Telemetry message struct
/// Used by the telemetry receiver.
pub struct TelemetryMessage<T: PayloadSerialize> {
    /// Payload of the telemetry message. Must implement [`PayloadSerialize`].
    pub payload: T,
    /// Custom user data set as custom MQTT User Properties on the telemetry message.
    pub custom_user_data: Vec<(String, String)>,
    /// If present, contains the client ID of the sender of the telemetry message.
    pub sender_id: Option<String>,
    /// Timestamp of the telemetry message.
    pub timestamp: Option<HybridLogicalClock>,
    /// Cloud event of the telemetry message.
    pub cloud_event: Option<CloudEvent>,
    /// Resolved topic tokens from the incoming message's topic.
    pub topic_tokens: HashMap<String, String>,
}

/// Telemetry Receiver Options struct
#[derive(Builder, Clone)]
#[builder(setter(into, strip_option))]
pub struct TelemetryReceiverOptions {
    /// Topic pattern for the telemetry message
    /// Must align with [topic-structure.md](https://github.com/microsoft/mqtt-patterns/blob/main/docs/specs/topic-structure.md)
    topic_pattern: String,
    /// Optional Topic namespace to be prepended to the topic pattern
    #[builder(default = "None")]
    topic_namespace: Option<String>,
    /// Topic token keys/values to be permanently replaced in the topic pattern
    #[builder(default)]
    topic_token_map: HashMap<String, String>,
    /// If true, telemetry messages are auto-acknowledged
    #[builder(default = "true")]
    auto_ack: bool,
    /// Service group ID
    #[allow(unused)]
    #[builder(default = "None")]
    service_group_id: Option<String>,
}

/// Telemetry Receiver struct
/// # Example
/// ```
/// # use tokio_test::block_on;
/// # use azure_iot_operations_mqtt::MqttConnectionSettingsBuilder;
/// # use azure_iot_operations_mqtt::session::{Session, SessionOptionsBuilder};
/// # use azure_iot_operations_protocol::telemetry::telemetry_receiver::{TelemetryReceiver, TelemetryReceiverOptionsBuilder};
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
/// #     .hostname("mqtt://localhost")
/// #     .tcp_port(1883u16)
/// #     .build().unwrap();
/// # let mut session_options = SessionOptionsBuilder::default()
/// #     .connection_settings(connection_settings)
/// #     .build().unwrap();
/// # let mut mqtt_session = Session::new(session_options).unwrap();
/// let receiver_options = TelemetryReceiverOptionsBuilder::default()
///  .topic_pattern("test/telemetry")
///  .build().unwrap();
/// let mut telemetry_receiver: TelemetryReceiver<SamplePayload, _> = TelemetryReceiver::new(mqtt_session.create_managed_client(), receiver_options).unwrap();
/// // let telemetry_message = telemetry_receiver.recv().await.unwrap();
/// ```
pub struct TelemetryReceiver<T, C>
where
    T: PayloadSerialize + Send + Sync + 'static,
    C: ManagedClient + Clone + Send + Sync + 'static,
    C::PubReceiver: Send + Sync + 'static,
{
    // Static properties of the receiver
    mqtt_client: C,
    mqtt_receiver: C::PubReceiver,
    telemetry_topic: String,
    topic_pattern: TopicPattern,
    message_payload_type: PhantomData<T>,
    // Describes state
    is_subscribed: bool,
    // Information to manage state
    pending_acks: JoinSet<interface::AckToken>, // TODO: Remove need for this
}

/// Implementation of a Telemetry Sender
impl<T, C> TelemetryReceiver<T, C>
where
    T: PayloadSerialize + Send + Sync + 'static,
    C: ManagedClient + Clone + Send + Sync + 'static,
    C::PubReceiver: Send + Sync + 'static,
{
    /// Creates a new [`TelemetryReceiver`].
    ///
    /// # Arguments
    /// * `client` - [`ManagedClient`] to use for telemetry communication.
    /// * `receiver_options` - [`TelemetryReceiverOptions`] to configure the telemetry receiver.
    ///
    /// Returns Ok([`TelemetryReceiver`]) on success, otherwise returns[`AIOProtocolError`].
    ///
    /// # Errors
    /// [`AIOProtocolError`] of kind [`ConfigurationInvalid`](crate::common::aio_protocol_error::AIOProtocolErrorKind::ConfigurationInvalid)
    /// - [`topic_pattern`](TelemetryReceiverOptions::topic_pattern),
    ///   [`topic_namespace`](TelemetryReceiverOptions::topic_namespace), are Some and and invalid
    ///   or contain a token with no valid replacement
    /// - [`topic_token_map`](TelemetryReceiverOptions::topic_token_map) is not empty
    ///   and contains invalid key(s) and/or token(s)
    /// - Content type of the telemetry message is not valid utf-8
    #[allow(clippy::needless_pass_by_value)]
    pub fn new(
        client: C,
        receiver_options: TelemetryReceiverOptions,
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
        // Validation for topic pattern and related options done in
        // [`TopicPattern::new`]
        let topic_pattern = TopicPattern::new(
            "receiver_options.topic_pattern",
            &receiver_options.topic_pattern,
            receiver_options.topic_namespace.as_deref(),
            &receiver_options.topic_token_map,
        )?;

        // Get the telemetry topic
        let telemetry_topic = topic_pattern.as_subscribe_topic();

        let mqtt_receiver = match client
            .create_filtered_pub_receiver(&telemetry_topic, receiver_options.auto_ack)
        {
            Ok(receiver) => receiver,
            Err(e) => {
                return Err(AIOProtocolError::new_configuration_invalid_error(
                    Some(Box::new(e)),
                    "topic_pattern",
                    Value::String(telemetry_topic),
                    Some("Could not parse subscription topic pattern".to_string()),
                    None,
                ));
            }
        };

        Ok(Self {
            mqtt_client: client,
            mqtt_receiver,
            telemetry_topic,
            topic_pattern,
            message_payload_type: PhantomData,
            is_subscribed: false,
            pending_acks: JoinSet::new(),
        })
    }

    // TODO: Finish implementing shutdown logic
    /// Shutdown the [`TelemetryReceiver`]. Unsubscribes from the telemetry topic if subscribed.
    ///
    /// Note: If this method is called, the [`TelemetryReceiver`] should not be used again.
    /// If the method returns an error, it may be called again to attempt the unsubscribe again.
    ///
    /// Returns Ok(()) on success, otherwise returns [`AIOProtocolError`].
    /// # Errors
    /// [`AIOProtocolError`] of kind [`ClientError`](crate::common::aio_protocol_error::AIOProtocolErrorKind::ClientError) if the unsubscribe fails or if the unsuback reason code doesn't indicate success.
    pub async fn shutdown(&mut self) -> Result<(), AIOProtocolError> {
        if self.is_subscribed {
            let unsubscribe_result = self.mqtt_client.unsubscribe(&self.telemetry_topic).await;

            match unsubscribe_result {
                Ok(unsub_ct) => {
                    match unsub_ct.await {
                        Ok(()) => { /* Success */ }
                        Err(e) => {
                            log::error!("Unsuback error: {e}");
                            return Err(AIOProtocolError::new_mqtt_error(
                                Some("MQTT error on telemetry receiver unsuback".to_string()),
                                Box::new(e),
                                None,
                            ));
                        }
                    }
                }
                Err(e) => {
                    log::error!("Client error while unsubscribing: {e}");
                    return Err(AIOProtocolError::new_mqtt_error(
                        Some("Client error on telemetry receiver unsubscribe".to_string()),
                        Box::new(e),
                        None,
                    ));
                }
            }
        }
        log::info!("Shutdown");
        Ok(())
    }

    /// Subscribe to the telemetry topic if not already subscribed.
    ///
    /// Returns Ok(()) on success, otherwise returns [`AIOProtocolError`].
    /// # Errors
    /// [`AIOProtocolError`] of kind [`ClientError`](crate::common::aio_protocol_error::AIOProtocolErrorKind::ClientError) if the subscribe fails or if the suback reason code doesn't indicate success.
    async fn try_subscribe(&mut self) -> Result<(), AIOProtocolError> {
        let subscribe_result = self
            .mqtt_client
            .subscribe(&self.telemetry_topic, QoS::AtLeastOnce)
            .await;

        match subscribe_result {
            Ok(sub_ct) => match sub_ct.await {
                Ok(()) => {
                    self.is_subscribed = true;
                }
                Err(e) => {
                    log::error!("Suback error: {e}");
                    return Err(AIOProtocolError::new_mqtt_error(
                        Some("MQTT error on telemetry receiver suback".to_string()),
                        Box::new(e),
                        None,
                    ));
                }
            },
            Err(e) => {
                log::error!("Client error while subscribing: {e}");
                return Err(AIOProtocolError::new_mqtt_error(
                    Some("Client error on telemetry receiver subscribe".to_string()),
                    Box::new(e),
                    None,
                ));
            }
        }
        Ok(())
    }

    /// Receives a telemetry message or [`None`] if there will be no more changes.
    /// Receives a telemetry message or [`None`] if there will be no more messages.
    /// If there are messages:
    /// - Returns Ok([`TelemetryMessage`], [`Option<AckToken>`]) on success
    ///     - If the message is received with Quality of Service 1 an [`AckToken`] is returned.
    /// - Returns [`AIOProtocolError`] on error.
    ///
    /// A received message can be acknowledged via the [`AckToken`] by calling [`AckToken::ack`] or dropping the [`AckToken`].
    ///
    /// # Errors
    /// [`AIOProtocolError`] of kind [`ClientError`](crate::common::aio_protocol_error::AIOProtocolErrorKind::ClientError) if the subscribe fails or if the suback reason code doesn't indicate success.
    pub async fn recv(
        &mut self,
    ) -> Option<Result<(TelemetryMessage<T>, Option<AckToken>), AIOProtocolError>> {
        // Subscribe to the telemetry topic if not already subscribed
        if !self.is_subscribed {
            match self.try_subscribe().await {
                Ok(()) => {
                    /* Success */
                    log::info!("Subscribed to telemetry topic");
                }
                Err(e) => {
                    return Some(Err(e));
                }
            }
        }

        loop {
            tokio::select! {
                // TODO: BUG, if recv() is not called, pending_acks will never be processed
                Some(inner_ack_token_result) = self.pending_acks.join_next() => {

                    match inner_ack_token_result {
                        Ok(inner_ack_token) => {
                            match inner_ack_token.ack().await {
                                Ok(_) => { /* Acked */ }
                                Err(e) => {
                                    log::error!("Ack error: {e}");
                                }
                            }

                        }
                        Err(e) => {
                            // Unreachable: Occurs when the task failed to execute to completion by
                            // panicking or cancelling.
                            log::error!("Failure to process ack: {e}");
                        }
                    }
                },
                recv_result = self.mqtt_receiver.recv() => {
                    // Process the received message
                    if let Some((m, inner_ack_token)) = recv_result {
                        log::info!("[pkid: {}] Received message", m.pkid);

                        'process_message: {
                            // Clone properties

                            let properties = m.properties.clone();

                            let mut custom_user_data = Vec::new();
                            let mut timestamp = None;
                            let mut cloud_event = None;
                            let mut sender_id = None;

                            if let Some(properties) = properties {
                                // Get content type
                                if let Some(content_type) = &properties.content_type {
                                    if T::content_type() != content_type {
                                        log::error!(
                                            "[pkid: {}] Content type {content_type} is not supported by this implementation; only {} is accepted", m.pkid, T::content_type()
                                        );
                                        break 'process_message;
                                    }
                                }

                                // unused beyond validation, but may be used in the future to determine how to handle other fields.
                                let mut message_protocol_version = DEFAULT_AIO_PROTOCOL_VERSION; // assume default version if none is provided
                                if let Some((_, protocol_version)) = properties.user_properties.iter().find(|(key, _)| UserProperty::from_str(key) == Ok(UserProperty::ProtocolVersion)) {
                                    if let Some(message_version) = ProtocolVersion::parse_protocol_version(protocol_version) {
                                        message_protocol_version = message_version;
                                    } else {
                                        log::error!("[pkid: {}] Unparsable protocol version value provided: {protocol_version}.",
                                            m.pkid
                                        );
                                        break 'process_message;
                                    }
                                }
                                // Check that the version (or the default version if one isn't provided) is supported
                                if !message_protocol_version.is_supported(SUPPORTED_PROTOCOL_VERSIONS) {
                                    log::error!("[pkid: {}] Unsupported Protocol Version '{message_protocol_version}'. Only major protocol versions '{SUPPORTED_PROTOCOL_VERSIONS:?}' are supported.",
                                        m.pkid
                                    );
                                    break 'process_message;
                                }

                                let mut cloud_event_present = false;
                                let mut cloud_event_builder = CloudEventBuilder::default();
                                let mut cloud_event_time = None;
                                for (key, value) in properties.user_properties {
                                    match UserProperty::from_str(&key) {
                                        Ok(UserProperty::Timestamp) => {
                                            match HybridLogicalClock::from_str(&value) {
                                                Ok(ts) => {
                                                    timestamp = Some(ts);
                                                }
                                                Err(e) => {
                                                    log::error!(
                                                        "[pkid: {}] Invalid timestamp {value}: {e}",
                                                        m.pkid
                                                    );
                                                    break 'process_message;
                                                }
                                            }
                                        },
                                        Ok(UserProperty::ProtocolVersion) => {
                                            // skip, already processed
                                        },
                                        Ok(UserProperty::SourceId) => {
                                            sender_id = Some(value);
                                        },
                                        Err(()) => {
                                            match CloudEventFields::from_str(&key) {
                                                Ok(CloudEventFields::Id) => {
                                                    cloud_event_present = true;
                                                    cloud_event_builder.id(value);
                                                },
                                                Ok(CloudEventFields::Source) => {
                                                    cloud_event_present = true;
                                                    cloud_event_builder.source(value);
                                                },
                                                Ok(CloudEventFields::SpecVersion) => {
                                                    cloud_event_present = true;
                                                    cloud_event_builder.spec_version(value);
                                                },
                                                Ok(CloudEventFields::EventType) => {
                                                    cloud_event_present = true;
                                                    cloud_event_builder.event_type(value);
                                                },
                                                Ok(CloudEventFields::Subject) => {
                                                    cloud_event_present = true;
                                                    cloud_event_builder.subject(value);
                                                },
                                                Ok(CloudEventFields::DataSchema) => {
                                                    cloud_event_present = true;
                                                    cloud_event_builder.data_schema(Some(value));
                                                },
                                                Ok(CloudEventFields::DataContentType) => {
                                                    cloud_event_present = true;
                                                    cloud_event_builder.data_content_type(value);
                                                },
                                                Ok(CloudEventFields::Time) => {
                                                    cloud_event_present = true;
                                                    cloud_event_time = Some(value);
                                                },
                                                Err(()) => {
                                                    custom_user_data.push((key, value));
                                                }
                                            }
                                        }
                                        _ => {
                                            log::warn!("[pkid: {}] Telemetry message should not contain MQTT user property {key}. Value is {value}", m.pkid);
                                            custom_user_data.push((key, value));
                                        }
                                    }
                                }
                                if cloud_event_present {
                                    if let Ok(mut ce) = cloud_event_builder.build() {
                                        if let Some(ce_time) = cloud_event_time {
                                            match DateTime::parse_from_rfc3339(&ce_time) {
                                                Ok(time) => {
                                                    let time = time.with_timezone(&Utc);
                                                    ce.time = Some(time);
                                                    cloud_event = Some(ce);
                                                },
                                                Err(e) => {
                                                    log::error!("[pkid: {}] Invalid cloud event time {ce_time}: {e}", m.pkid);
                                                }
                                            }
                                        } else {
                                            cloud_event = Some(ce);
                                        }
                                    } else {
                                        log::error!("[pkid: {}] Telemetry received invalid cloud event", m.pkid);
                                    }
                                }
                            }

                            let topic = match std::str::from_utf8(&m.topic) {
                                Ok(topic) => topic,
                                Err(e) => {
                                    // This should never happen as the topic is always a valid UTF-8 string from the MQTT client
                                    log::error!("[pkid: {}] Topic deserialization error: {e:?}", m.pkid);
                                    break 'process_message;
                                }
                            };

                            let topic_tokens = self.topic_pattern.parse_tokens(topic);

                            // Deserialize payload
                            let payload = match T::deserialize(&m.payload) {
                                Ok(p) => p,
                                Err(e) => {
                                    log::error!("[pkid: {}] Payload deserialization error: {e:?}", m.pkid);
                                    break 'process_message;
                                }
                            };

                            let telemetry_message = TelemetryMessage {
                                payload,
                                custom_user_data,
                                sender_id,
                                timestamp,
                                cloud_event,
                                topic_tokens,
                            };

                            // If the telemetry message needs ack, return telemetry message with ack token
                            if let Some(inner_ack_token) = inner_ack_token {
                                let (ack_tx, ack_rx) = oneshot::channel();
                                let ack_token = AckToken { ack_tx };

                                self.pending_acks.spawn({
                                    async move {
                                        match ack_rx.await {
                                            Ok(()) => { /* Ack token used */ },
                                            Err(_) => {
                                                log::error!("[pkid: {}] Ack channel closed, acking", m.pkid);
                                            }
                                        }
                                        inner_ack_token
                                    }
                                });

                                return Some(Ok((telemetry_message, Some(ack_token))));
                            }

                            return Some(Ok((telemetry_message, None)));
                        }

                        // Occurs on an error processing the message, ack to prevent redelivery
                        if let Some(inner_ack_token) = inner_ack_token {
                            match inner_ack_token.ack().await {
                                Ok(_) => { /* Success */ }
                                Err(e) => {
                                    log::error!("[pkid: {}] Ack error {e}", m.pkid);
                                }
                            };
                        }
                    } else {
                        // There will be no more messages
                        return None;
                    }
                }
            }
        }
    }
}

impl<T, C> Drop for TelemetryReceiver<T, C>
where
    T: PayloadSerialize + Send + Sync + 'static,
    C: ManagedClient + Clone + Send + Sync + 'static,
    C::PubReceiver: Send + Sync + 'static,
{
    fn drop(&mut self) {}
}

#[cfg(test)]
mod tests {
    use test_case::test_case;

    use super::*;
    use crate::{
        common::{
            aio_protocol_error::AIOProtocolErrorKind,
            payload_serialize::{FormatIndicator, MockPayload, CONTENT_TYPE_MTX},
        },
        telemetry::telemetry_receiver::{TelemetryReceiver, TelemetryReceiverOptionsBuilder},
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

    // TODO: This should return a mock Session instead
    fn get_session() -> Session {
        // TODO: Make a real mock that implements Session
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
        HashMap::from([("telemetryName".to_string(), "test_telemetry".to_string())])
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
        let receiver_options = TelemetryReceiverOptionsBuilder::default()
            .topic_pattern("test/receiver")
            .build()
            .unwrap();

        TelemetryReceiver::<MockPayload, _>::new(session.create_managed_client(), receiver_options)
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
        let receiver_options = TelemetryReceiverOptionsBuilder::default()
            .topic_pattern("test/{telemetryName}/receiver")
            .topic_namespace("test_namespace")
            .topic_token_map(create_topic_tokens())
            .build()
            .unwrap();

        TelemetryReceiver::<MockPayload, _>::new(session.create_managed_client(), receiver_options)
            .unwrap();
    }

    #[test]
    fn test_invalid_telemetry_content_type() {
        let session = get_session();
        let receiver_options = TelemetryReceiverOptionsBuilder::default()
            .topic_pattern("test/receiver")
            .build()
            .unwrap();

        let telemetry_receiver: Result<
            TelemetryReceiver<InvalidContentTypePayload, _>,
            AIOProtocolError,
        > = TelemetryReceiver::new(session.create_managed_client(), receiver_options);

        match telemetry_receiver {
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

    #[test_case(""; "new_empty_topic_pattern")]
    #[test_case(" "; "new_whitespace_topic_pattern")]
    fn test_new_empty_topic_pattern(topic_pattern: &str) {
        // Get mutex lock for content type
        let _content_type_mutex = CONTENT_TYPE_MTX.lock();
        // Mock context to track content_type calls
        let mock_payload_content_type_ctx = MockPayload::content_type_context();
        let _mock_payload_content_type = mock_payload_content_type_ctx
            .expect()
            .returning(|| "application/json");

        let session = get_session();
        let receiver_options = TelemetryReceiverOptionsBuilder::default()
            .topic_pattern(topic_pattern)
            .build()
            .unwrap();

        let result: Result<TelemetryReceiver<MockPayload, _>, _> =
            TelemetryReceiver::new(session.create_managed_client(), receiver_options);
        match result {
            Ok(_) => panic!("Expected error"),
            Err(e) => {
                assert_eq!(e.kind, AIOProtocolErrorKind::ConfigurationInvalid);
                assert!(!e.in_application);
                assert!(e.is_shallow);
                assert!(!e.is_remote);
                assert_eq!(e.http_status_code, None);
                assert_eq!(
                    e.property_name,
                    Some("receiver_options.topic_pattern".to_string())
                );
                assert_eq!(
                    e.property_value,
                    Some(Value::String(topic_pattern.to_string()))
                );
            }
        }
    }

    #[tokio::test]
    async fn test_shutdown_without_subscribe() {
        // Get mutex lock for content type
        let _content_type_mutex = CONTENT_TYPE_MTX.lock();
        // Mock context to track content_type calls
        let mock_payload_content_type_ctx = MockPayload::content_type_context();
        let _mock_payload_content_type = mock_payload_content_type_ctx
            .expect()
            .returning(|| "application/json");
        let session = get_session();
        let receiver_options = TelemetryReceiverOptionsBuilder::default()
            .topic_pattern("test/receiver")
            .build()
            .unwrap();

        let mut telemetry_receiver: TelemetryReceiver<MockPayload, _> =
            TelemetryReceiver::new(session.create_managed_client(), receiver_options).unwrap();
        assert!(telemetry_receiver.shutdown().await.is_ok());
    }
}

// Test cases for recv telemetry
// Tests failure:
//   if properties are missing, the message is not processed and is acked
//   if content type is not supported by the payload type, the message is not processed and is acked
//   if timestamp is invalid, the message is not processed and is acked
//   if payload deserialization fails, the message is not processed and is acked
//
// Test cases for telemetry message processing
// Tests success:
//   QoS 1 message is processed and AckToken is used, message is acked
