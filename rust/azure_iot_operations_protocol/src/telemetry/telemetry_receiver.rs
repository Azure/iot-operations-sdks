// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
use std::{collections::HashMap, marker::PhantomData, str::FromStr};

use azure_iot_operations_mqtt::{
    control_packet::{Publish, QoS},
    interface::{ManagedClient, MqttAck, PubReceiver},
};
use chrono::DateTime;
use tokio::{sync::oneshot, task::JoinSet};

use crate::{
    common::{
        aio_protocol_error::{AIOProtocolError, Value},
        hybrid_logical_clock::HybridLogicalClock,
        payload_serialize::PayloadSerialize,
        topic_processor::{TopicPattern, WILDCARD},
        user_properties::{UserProperty, RESERVED_PREFIX},
    },
    telemetry::{CloudEvent, CloudEventBuilder},
};

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
    /// Client ID of the sender of the telemetry message.
    pub sender_id: String,
    /// Timestamp of the telemetry message.
    pub timestamp: Option<HybridLogicalClock>,
    /// Cloud event of the telemetry message.
    pub cloud_event: Option<CloudEvent>,
}

/// Telemetry Receiver Options struct
#[derive(Builder, Clone)]
#[builder(setter(into, strip_option))]
pub struct TelemetryReceiverOptions {
    /// Topic pattern for the telemetry message
    /// Must align with [topic-structure.md](https://github.com/microsoft/mqtt-patterns/blob/main/docs/specs/topic-structure.md)
    topic_pattern: String,
    /// Telemetry name if required by the topic pattern
    #[builder(default = "None")]
    telemetry_name: Option<String>,
    /// Model ID if required by the topic pattern
    #[builder(default = "None")]
    model_id: Option<String>,
    /// Optional Topic namespace to be prepended to the topic pattern
    #[builder(default = "None")]
    topic_namespace: Option<String>,
    /// Custom topic token keys/values to be replaced in the topic pattern
    #[builder(default)]
    custom_topic_token_map: HashMap<String, String>,
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
/// #     .host_name("mqtt://localhost")
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
    auto_ack: bool,
    // Describes state
    is_subscribed: bool,
    // Information to manage state
    pending_pubs: JoinSet<Publish>, // TODO: Remove need for this
}

/// Implementation of a Telemetry Sender
#[allow(clippy::needless_pass_by_value)] // TODO: Remove, in other envoys, options are passed by value
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
    /// - [`telemetry_topic_pattern`](TelemetryReceiverOptions::telemetry_topic_pattern),
    ///   [`telemetry_name`](TelemetryReceiverOptions::telemetry_name),
    ///   [`model_id`](TelemetryReceiverOptions::model_id),
    ///   [`topic_namespace`](TelemetryReceiverOptions::topic_namespace), are Some and and invalid
    ///   or contain a token with no valid replacement
    /// - [`custom_topic_token_map`](TelemetryReceiverOptions::custom_topic_token_map) is not empty
    ///   and contains invalid key(s) and/or token(s)
    pub fn new(
        client: C,
        receiver_options: TelemetryReceiverOptions,
    ) -> Result<Self, AIOProtocolError> {
        // Validation for topic pattern and related options done in
        // [`TopicPattern::new_telemetry_pattern`]
        let topic_pattern = TopicPattern::new_telemetry_pattern(
            &receiver_options.topic_pattern,
            WILDCARD,
            receiver_options.telemetry_name.as_deref(),
            receiver_options.model_id.as_deref(),
            receiver_options.topic_namespace.as_deref(),
            &receiver_options.custom_topic_token_map,
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
            auto_ack: receiver_options.auto_ack,
            is_subscribed: false,
            pending_pubs: JoinSet::new(),
        })
    }

    // TODO: Finish implementing shutdown logic
    /// Shutdown the [`TelemetryReceiver`]. Unsubscribes from the telemetry topic.
    ///
    /// Returns Ok(()) on success, otherwise returns [`AIOProtocolError`].
    /// # Errors
    /// [`AIOProtocolError`] of kind [`ClientError`](crate::common::aio_protocol_error::AIOProtocolErrorKind::ClientError) if the unsubscribe fails or if the unsuback reason code doesn't indicate success.
    pub async fn shutdown(&mut self) -> Result<(), AIOProtocolError> {
        let unsubscribe_result = self.mqtt_client.unsubscribe(&self.telemetry_topic).await;

        match unsubscribe_result {
            Ok(unsub_ct) => {
                match unsub_ct.wait().await {
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
        log::info!("Stopped");
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
            Ok(sub_ct) => match sub_ct.wait().await {
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
                // TODO: BUG, if recv() is not called, pending_pubs will never be processed
                Some(pending_pub) = self.pending_pubs.join_next() => {
                    match pending_pub {
                        Ok(pending_pub) => {
                            match self.mqtt_receiver.ack(&pending_pub).await {
                                Ok(()) => { log::info!("[pkid: {}] Acked", pending_pub.pkid); }
                                Err(e) => {
                                    log::error!("[pkid: {}] Ack error: {e}", pending_pub.pkid);
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
                message = self.mqtt_receiver.recv() => {
                    // Process the received message
                    if let Some(m) = message {
                        log::info!("[pkid: {}] Received message", m.pkid);

                        'process_message: {
                            // Clone properties

                            let properties = m.properties.clone();

                            let mut custom_user_data = Vec::new();
                            let mut timestamp = None;
                            let mut cloud_event = None;

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

                                let mut cloud_event_present = false;
                                let mut cloud_event_builder = CloudEventBuilder::default();
                                let mut cloud_event_time_valid = true;
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
                                        Ok(UserProperty::ProtocolVersion | UserProperty::SupportedMajorVersions) => {
                                            // TODO: Implement protocol version check
                                        },
                                        Ok(UserProperty::CloudEventId) => {
                                            cloud_event_present = true;
                                            cloud_event_builder.id(value);
                                        },
                                        Ok(UserProperty::CloudEventSource) => {
                                            cloud_event_present = true;
                                            cloud_event_builder.source(value);
                                        },
                                        Ok(UserProperty::CloudEventSpecVersion) => {
                                            cloud_event_present = true;
                                            cloud_event_builder.spec_version(value);
                                        },
                                        Ok(UserProperty::CloudEventType) => {
                                            cloud_event_present = true;
                                            cloud_event_builder.event_type(value);
                                        },
                                        Ok(UserProperty::CloudEventSubject) => {
                                            cloud_event_present = true;
                                            cloud_event_builder.subject(value);
                                        },
                                        Ok(UserProperty::CloudEventDataSchema) => {
                                            cloud_event_present = true;
                                            cloud_event_builder.data_schema(Some(value));
                                        },
                                        Ok(UserProperty::CloudEventDataContentType) => {
                                            cloud_event_present = true;
                                            cloud_event_builder.data_content_type(value);
                                        },
                                        Ok(UserProperty::CloudEventTime) => {
                                            cloud_event_present = true;
                                            match DateTime::parse_from_rfc3339(&value) {
                                                Ok(time) => {
                                                    cloud_event_builder.time(time);
                                                },
                                                Err(e) => {
                                                    log::error!("[pkid: {}] Invalid cloud event time {value}: {e}", m.pkid);
                                                    cloud_event_time_valid = false;
                                                }
                                            }
                                        },
                                        Err(()) => {
                                            if key.starts_with(RESERVED_PREFIX) {
                                                log::error!("[pkid: {}] Invalid telemetry user data property '{}' starts with reserved prefix '{}'. Value is '{}'", m.pkid, key, RESERVED_PREFIX, value);
                                            } else {
                                                custom_user_data.push((key, value));
                                            }
                                        }
                                        _ => {
                                            log::error!("[pkid: {}] Telemetry message should not contain MQTT user property {key}. Value is {value}", m.pkid);
                                        }
                                    }
                                }
                                if cloud_event_present && cloud_event_time_valid {
                                    if let Ok(ce) = cloud_event_builder.build() {
                                        cloud_event = Some(ce);
                                    } else {
                                        log::error!("[pkid: {}] Telemetry received invalid cloud event", m.pkid);
                                    }
                                }
                            }

                            // Parse the sender ID from the topic
                            let Ok(received_topic) = String::from_utf8(m.topic.to_vec()) else {
                                log::error!("[pkid: {}] Invalid telemetry topic", m.pkid);
                                break 'process_message;
                            };
                            let Some(sender_id) = self.topic_pattern.parse_wildcard(&received_topic)
                            else {
                                log::error!("[pkid: {}] Sender ID not found in telemetry topic", m.pkid);
                                break 'process_message;
                            };

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
                            };

                            // If the telemetry message needs ack, return telemetry message with ack token
                            if !self.auto_ack && !matches!(m.qos, QoS::AtMostOnce)  {
                                let (ack_tx, ack_rx) = oneshot::channel();
                                let ack_token = AckToken { ack_tx };

                                self.pending_pubs.spawn({
                                    async move {
                                        match ack_rx.await {
                                            Ok(()) => { /* Ack token used */ },
                                            Err(_) => {
                                                log::error!("[pkid: {}] Ack channel closed, acking", m.pkid);
                                            }
                                        }
                                        m
                                    }
                                });

                                return Some(Ok((telemetry_message, Some(ack_token))));
                            }

                            return Some(Ok((telemetry_message, None)));
                        }

                        // Occurs on an error processing the message, ack to prevent redelivery
                        if !self.auto_ack && !matches!(m.qos, QoS::AtMostOnce) {
                            match self.mqtt_receiver.ack(&m).await {
                                Ok(()) => { /* Success */ }
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
        common::{aio_protocol_error::AIOProtocolErrorKind, payload_serialize::MockPayload},
        telemetry::telemetry_receiver::{TelemetryReceiver, TelemetryReceiverOptionsBuilder},
    };
    use azure_iot_operations_mqtt::{
        session::{Session, SessionOptionsBuilder},
        MqttConnectionSettingsBuilder,
    };

    const MODEL_ID: &str = "test_model";

    // TODO: This should return a mock Session instead
    fn get_session() -> Session {
        // TODO: Make a real mock that implements Session
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
        let session = get_session();
        let receiver_options = TelemetryReceiverOptionsBuilder::default()
            .topic_pattern("test/{senderId}/receiver")
            .build()
            .unwrap();

        let telemetry_receiver: TelemetryReceiver<MockPayload, _> =
            TelemetryReceiver::new(session.create_managed_client(), receiver_options).unwrap();

        assert!(telemetry_receiver
            .topic_pattern
            .is_match("test/test_sender/receiver"));
    }

    #[tokio::test]
    async fn test_new_override_defaults() {
        let session = get_session();
        let custom_token_map = HashMap::from([("customToken".to_string(), "123".to_string())]);
        let receiver_options = TelemetryReceiverOptionsBuilder::default()
            .topic_pattern("test/{senderId}/{ex:customToken}/{modelId}/receiver")
            .telemetry_name("test_telemetry")
            .model_id("test_model")
            .topic_namespace("test_namespace")
            .custom_topic_token_map(custom_token_map)
            .build()
            .unwrap();
        let telemetry_receiver: TelemetryReceiver<MockPayload, _> =
            TelemetryReceiver::new(session.create_managed_client(), receiver_options).unwrap();

        assert!(telemetry_receiver
            .topic_pattern
            .is_match(format!("test_namespace/test/test_sender/123/{MODEL_ID}/receiver").as_str()));
    }

    #[test_case(""; "new_empty_topic_pattern")]
    #[test_case(" "; "new_whitespace_topic_pattern")]
    fn test_new_empty_topic_pattern(topic_pattern: &str) {
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
                assert_eq!(e.property_name, Some("pattern".to_string()));
                assert_eq!(
                    e.property_value,
                    Some(Value::String(topic_pattern.to_string()))
                );
            }
        }
    }
}

// Test cases for recv telemetry
// Tests success:
//   recv() is called and a telemetry message is received by the application with sender_id
//   if cloud event properties are present, they are successfully parsed
//   if user properties are present, they don't start with reserved prefix
//   if timestamp is present, it is successfully parsed
//   if telemetry message is ackable (QoS 1) and auto-ack is disabled, an ack token is returned
//   if telemetry message is ackable (QoS 1) and auto-ack is enabled, no ack token is returned
//   if telemetry message is not ackable (QoS 0) and auto-ack is disabled, no ack token is returned
//   if telemetry message is not ackable (QoS 0) and auto-ack is enabled, no ack token is returned
// Tests failure:
//   if properties are missing, the message is not processed and is acked
//   if content type is not supported by the payload type, the message is not processed and is acked
//   if timestamp is invalid, the message is not processed and is acked
//   if payload deserialization fails, the message is not processed and is acked
//
// Test cases for telemetry message processing
// Tests success:
//   QoS 1 message is processed and AckToken is used, message is acked
