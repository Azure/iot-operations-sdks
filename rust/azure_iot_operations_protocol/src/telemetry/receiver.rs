// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
use std::{collections::HashMap, marker::PhantomData, str::FromStr, sync::Arc};

use azure_iot_operations_mqtt::{
    aio::cloud_event as aio_cloud_event,
    control_packet::{Publish, QoS, TopicFilter},
    session::{SessionManagedClient, SessionPubReceiver},
    token::AckToken,
};
use tokio_util::sync::CancellationToken;

use crate::{
    ProtocolVersion,
    application::{ApplicationContext, ApplicationHybridLogicalClock},
    common::{
        aio_protocol_error::AIOProtocolError,
        hybrid_logical_clock::HybridLogicalClock,
        payload_serialize::{FormatIndicator, PayloadSerialize},
        topic_processor::TopicPattern,
        user_properties::UserProperty,
    },
    telemetry::DEFAULT_TELEMETRY_PROTOCOL_VERSION,
};

const SUPPORTED_PROTOCOL_VERSIONS: &[u16] = &[1];

/// Cloud Event struct derived from a received Telemetry Message.
pub type CloudEvent = aio_cloud_event::CloudEvent;
// TODO: pub type the error too once we have the right name

/// Parse a [`CloudEvent`] from a [`Message`].
/// Note that this will return an error if the [`Message`] does not contain the required fields for a [`CloudEvent`].
///
/// # Errors
/// [`CloudEventBuilderError::UninitializedField`] if the [`Message`] does not contain the required fields for a [`CloudEvent`].
///
/// [`CloudEventBuilderError::ValidationError`] if any of the field values are not valid for a [`CloudEvent`].
pub fn cloud_event_from_telemetry<T: PayloadSerialize>(
    telemetry: &Message<T>,
) -> Result<CloudEvent, aio_cloud_event::CloudEventBuilderError> {
    CloudEvent::try_from((
        &telemetry.custom_user_data,
        telemetry.content_type.as_deref(),
    ))
}

/// Telemetry message struct.
/// Used by the [`Receiver`].
#[derive(Debug)]
pub struct Message<T: PayloadSerialize> {
    /// Payload of the telemetry message. Must implement [`PayloadSerialize`].
    pub payload: T,
    /// Content Type of the telemetry message.
    pub content_type: Option<String>,
    /// Format Indicator of the telemetry message.
    pub format_indicator: FormatIndicator,
    /// Custom user data set as custom MQTT User Properties on the telemetry message.
    pub custom_user_data: Vec<(String, String)>,
    /// If present, contains the client ID of the sender of the telemetry message.
    pub sender_id: Option<String>,
    /// Timestamp of the telemetry message.
    pub timestamp: Option<HybridLogicalClock>,
    /// Resolved static and dynamic topic tokens from the incoming message's topic.
    pub topic_tokens: HashMap<String, String>,
    /// Incoming message topic
    pub topic: String,
    /// Indicates if the message is a duplicate delivery if QoS 1 (DUP flag in MQTT publish)
    pub duplicate: Option<bool>,
}

impl<T> TryFrom<Publish> for Message<T>
where
    T: PayloadSerialize,
{
    type Error = String;

    fn try_from(value: Publish) -> Result<Message<T>, Self::Error> {
        // NOTE: User properties are parsed out into a new HashMap because:
        // 1) It makes the code more readable/maintanable to do HashMap lookups
        // 2) When this logic is extracted to a ChunkBuffer, it will be more memory efficient as
        //  we won't want to keep entire copies of all Publishes, so we will just copy the
        //  properties once.

        let publish_properties = value.properties;

        // Parse user properties
        let expected_aio_properties = [
            UserProperty::Timestamp,
            UserProperty::ProtocolVersion,
            UserProperty::SourceId,
        ];
        let mut telemetry_custom_user_data = vec![];
        let mut telemetry_aio_data = HashMap::new();
        for (key, value) in publish_properties.user_properties {
            match UserProperty::from_str(&key) {
                Ok(p) if expected_aio_properties.contains(&p) => {
                    telemetry_aio_data.insert(p, value);
                }
                Ok(_) => {
                    log::warn!(
                        "Telemetry should not contain MQTT user property '{key}'. Value is '{value}'"
                    );
                    telemetry_custom_user_data.push((key, value));
                }
                Err(()) => {
                    telemetry_custom_user_data.push((key, value));
                }
            }
        }

        // Check the protocol version.
        // If the protocol version is not supported, or cannot be parsed, all bets are off
        // regarding what anything else even means, so this *must* be done first
        let protocol_version = {
            match telemetry_aio_data.get(&UserProperty::ProtocolVersion) {
                Some(protocol_version) => {
                    if let Some(version) = ProtocolVersion::parse_protocol_version(protocol_version)
                    {
                        version
                    } else {
                        return Err(format!(
                            "Received a telemetry with an unparsable protocol version number: {protocol_version}"
                        ));
                    }
                }
                None => DEFAULT_TELEMETRY_PROTOCOL_VERSION,
            }
        };
        if !protocol_version.is_supported(SUPPORTED_PROTOCOL_VERSIONS) {
            return Err(format!(
                "Unsupported protocol version '{protocol_version}'. Only major protocol versions '{SUPPORTED_PROTOCOL_VERSIONS:?}' are supported"
            ));
        }

        // Format HLC timestamp
        let timestamp = telemetry_aio_data
            .get(&UserProperty::Timestamp)
            .map(|s| HybridLogicalClock::from_str(s))
            .transpose()
            .map_err(|e| e.to_string())?;

        // Deserialize payload
        let format_indicator = publish_properties.payload_format_indicator.into();

        let content_type = publish_properties.content_type;
        let payload = T::deserialize(&value.payload, content_type.as_ref(), &format_indicator)
            .map_err(|e| format!("{e:?}"))?;
        let duplicate = match value.qos {
            azure_iot_operations_mqtt::control_packet::DeliveryQoS::AtMostOnce => None,
            azure_iot_operations_mqtt::control_packet::DeliveryQoS::AtLeastOnce(delivery_info) => {
                Some(delivery_info.dup)
            }
            azure_iot_operations_mqtt::control_packet::DeliveryQoS::ExactlyOnce(_) => {
                // Before conversion, a check is done to prevent any QoS 2 messages from being processed
                unreachable!()
            }
        };

        let telemetry_message = Message {
            payload,
            content_type,
            format_indicator,
            custom_user_data: telemetry_custom_user_data,
            sender_id: telemetry_aio_data.remove(&UserProperty::SourceId),
            timestamp,
            // NOTE: Topic Tokens cannot be created from just a Publish, they need additional information
            topic_tokens: HashMap::default(),
            topic: value.topic_name.as_str().to_string(),
            duplicate,
        };
        Ok(telemetry_message)
    }
}

/// Telemetry Receiver Options struct
#[derive(Builder, Clone)]
#[builder(setter(into, strip_option))]
pub struct Options {
    /// Topic pattern for the telemetry message.
    /// Must align with [topic-structure.md](https://github.com/Azure/iot-operations-sdks/blob/main/doc/reference/topic-structure.md)
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
/// # use azure_iot_operations_mqtt::aio::connection_settings::MqttConnectionSettingsBuilder;
/// # use azure_iot_operations_mqtt::session::{Session, SessionOptionsBuilder};
/// # use azure_iot_operations_protocol::telemetry;
/// # use azure_iot_operations_protocol::application::ApplicationContextBuilder;
/// # let mut connection_settings = MqttConnectionSettingsBuilder::default()
/// #     .client_id("test_server")
/// #     .hostname("mqtt://localhost")
/// #     .tcp_port(1883u16)
/// #     .build().unwrap();
/// # let mut session_options = SessionOptionsBuilder::default()
/// #     .connection_settings(connection_settings)
/// #     .build().unwrap();
/// # let mqtt_session = Session::new(session_options).unwrap();
/// # let application_context = ApplicationContextBuilder::default().build().unwrap();;
/// let receiver_options = telemetry::receiver::OptionsBuilder::default()
///  .topic_pattern("test/telemetry")
///  .build().unwrap();
/// let mut receiver: telemetry::Receiver<Vec<u8>> = telemetry::Receiver::new(application_context, mqtt_session.create_managed_client(), receiver_options).unwrap();
/// // let telemetry_message = receiver.recv().await.unwrap();
/// ```
pub struct Receiver<T>
where
    T: PayloadSerialize + Send + Sync + 'static,
{
    // Static properties of the receiver
    application_hlc: Arc<ApplicationHybridLogicalClock>,
    mqtt_client: SessionManagedClient,
    #[allow(clippy::struct_field_names)]
    mqtt_receiver: SessionPubReceiver,
    telemetry_topic: TopicFilter,
    topic_pattern: TopicPattern,
    message_payload_type: PhantomData<T>,
    // Describes state
    state: State,
    // Information to manage state
    cancellation_token: CancellationToken,
    // User autoack setting
    auto_ack: bool,
}

/// Describes state of receiver
#[derive(PartialEq)]
enum State {
    New,
    Subscribed,
    ShutdownSuccessful,
}

/// Implementation of a Telemetry Sender
impl<T> Receiver<T>
where
    T: PayloadSerialize + Send + Sync + 'static,
{
    /// Creates a new [`Receiver`].
    ///
    /// # Arguments
    /// * `application_context` - [`ApplicationContext`] that the telemetry receiver is part of.
    /// * `client` - [`SessionManagedClient`] to use for telemetry communication.
    /// * `receiver_options` - [`Options`] to configure the telemetry receiver.
    ///
    /// Returns Ok([`Receiver`]) on success, otherwise returns[`AIOProtocolError`].
    ///
    /// # Errors
    /// [`AIOProtocolError`] of kind [`ConfigurationInvalid`](crate::common::aio_protocol_error::AIOProtocolErrorKind::ConfigurationInvalid)
    /// - [`topic_pattern`](OptionsBuilder::topic_pattern),
    ///   [`topic_namespace`](OptionsBuilder::topic_namespace), are Some and invalid
    ///   or contain a token with no valid replacement
    /// - [`topic_token_map`](OptionsBuilder::topic_token_map) is not empty
    ///   and contains invalid key(s) and/or token(s)
    #[allow(clippy::needless_pass_by_value)]
    pub fn new(
        application_context: ApplicationContext,
        client: SessionManagedClient,
        receiver_options: Options,
    ) -> Result<Self, AIOProtocolError> {
        // Validation for topic pattern and related options done in
        // [`TopicPattern::new`]
        let topic_pattern = TopicPattern::new(
            &receiver_options.topic_pattern,
            None,
            receiver_options.topic_namespace.as_deref(),
            &receiver_options.topic_token_map,
        )
        .map_err(|e| {
            AIOProtocolError::config_invalid_from_topic_pattern_error(
                e,
                "receiver_options.topic_pattern",
            )
        })?;

        // Get the telemetry topic
        let telemetry_topic = topic_pattern.as_subscribe_topic().map_err(|e| {
            AIOProtocolError::config_invalid_from_topic_pattern_error(
                e,
                "receiver_options.topic_pattern",
            )
        })?;

        let mqtt_receiver = client.create_filtered_pub_receiver(telemetry_topic.clone());

        Ok(Self {
            application_hlc: application_context.application_hlc,
            mqtt_client: client,
            mqtt_receiver,
            telemetry_topic,
            topic_pattern,
            message_payload_type: PhantomData,
            state: State::New,
            cancellation_token: CancellationToken::new(),
            auto_ack: receiver_options.auto_ack,
        })
    }

    /// Shutdown the [`Receiver`]. Unsubscribes from the telemetry topic if subscribed.
    ///
    /// Note: If this method is called, the [`Receiver`] will no longer receive telemetry messages
    /// from the MQTT client, any messages that have not been processed can still be received by the
    /// receiver. If the method returns an error, it may be called again to attempt the unsubscribe again.
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
                        self.telemetry_topic.clone(),
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
                                log::error!("Telemetry Receiver Unsuback error: {unsuback:?}");
                                return Err(AIOProtocolError::new_mqtt_error(
                                    Some("MQTT error on telemetry receiver unsuback".to_string()),
                                    Box::new(e),
                                    None,
                                ));
                            }
                        },
                        Err(e) => {
                            log::error!("Telemetry Receiver Unsubscribe completion error: {e}");
                            return Err(AIOProtocolError::new_mqtt_error(
                                Some("MQTT error on telemetry receiver unsubscribe".to_string()),
                                Box::new(e),
                                None,
                            ));
                        }
                    },
                    Err(e) => {
                        log::error!("Client error while unsubscribing in Telemetry Receiver: {e}");
                        return Err(AIOProtocolError::new_mqtt_error(
                            Some("Client error on telemetry receiver unsubscribe".to_string()),
                            Box::new(e),
                            None,
                        ));
                    }
                }
            }
        }
        log::info!("Telemetry receiver shutdown");
        Ok(())
    }

    /// Subscribe to the telemetry topic.
    ///
    /// Returns Ok(()) on success, otherwise returns [`AIOProtocolError`].
    /// # Errors
    /// [`AIOProtocolError`] of kind [`ClientError`](crate::common::aio_protocol_error::AIOProtocolErrorKind::ClientError) if the subscribe fails or if the suback reason code doesn't indicate success.
    async fn try_subscribe(&mut self) -> Result<(), AIOProtocolError> {
        let subscribe_result = self
            .mqtt_client
            .subscribe(
                self.telemetry_topic.clone(),
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
                        log::error!("Telemetry Receiver Suback error: {suback:?}");
                        AIOProtocolError::new_mqtt_error(
                            Some("MQTT error on telemetry receiver suback".to_string()),
                            Box::new(e),
                            None,
                        )
                    })?;
                }
                Err(e) => {
                    log::error!("Telemetry Receiver Subscribe completion error: {e}");
                    return Err(AIOProtocolError::new_mqtt_error(
                        Some("MQTT error on telemetry receiver subscribe".to_string()),
                        Box::new(e),
                        None,
                    ));
                }
            },
            Err(e) => {
                log::error!("Client error while subscribing in Telemetry Receiver: {e}");
                return Err(AIOProtocolError::new_mqtt_error(
                    Some("Client error on telemetry receiver subscribe".to_string()),
                    Box::new(e),
                    None,
                ));
            }
        }
        Ok(())
    }

    /// Receives a telemetry message or [`None`] if there will be no more messages.
    /// If there are messages:
    /// - Returns Ok([`Message`], [`Option<AckToken>`]) on success
    ///     - If the message is received with Quality of Service 1 an [`AckToken`] is returned.
    /// - Returns [`AIOProtocolError`] on error.
    ///
    /// A received message can be acknowledged via the [`AckToken`] by calling [`AckToken::ack`] or dropping the [`AckToken`].
    /// If successful [`AckToken::ack`] will return a completion token that can be awaited to ensure the acknowledgement
    /// was delivered on the wire. The acknowledgement may fail to be delivered because of a network disconnection
    /// at which point a duplicate message may be received once the connection is re-established. The [`Message`]
    /// contains a [`duplicate`](Message::duplicate) field that indicates if the message is a duplicate delivery. It is
    /// left up to the application to handle duplicate messages appropriately.
    ///
    /// Will also subscribe to the telemetry topic if not already subscribed.
    ///
    /// # Errors
    /// [`AIOProtocolError`] of kind [`ClientError`](crate::common::aio_protocol_error::AIOProtocolErrorKind::ClientError) if the subscribe fails or if the suback reason code doesn't indicate success.
    pub async fn recv(
        &mut self,
    ) -> Option<Result<(Message<T>, Option<AckToken>), AIOProtocolError>> {
        // Subscribe to the telemetry topic if not already subscribed
        if self.state == State::New {
            if let Err(e) = self.try_subscribe().await {
                return Some(Err(e));
            }
            self.state = State::Subscribed;
        }

        loop {
            match self.mqtt_receiver.recv_manual_ack().await {
                Some((m, mut ack_token)) => {
                    // Drop the ack token if the user does not desire it
                    // TODO: change API around this receive to simplify
                    if self.auto_ack {
                        // Replace the token with None (if Some)
                        ack_token.take();
                    }

                    // Get pkid for logging
                    let pkid = match m.qos {
                        azure_iot_operations_mqtt::control_packet::DeliveryQoS::AtMostOnce => {
                            // CONSIDER: maybe we should log with something else, but this matches old behavior
                            // QoS0 doesn't have a packet id, but 0 isn't a valid packet id, and rumqttc used to use 0
                            0
                        }
                        azure_iot_operations_mqtt::control_packet::DeliveryQoS::AtLeastOnce(
                            delivery_info,
                        ) => delivery_info.packet_identifier.get(),
                        azure_iot_operations_mqtt::control_packet::DeliveryQoS::ExactlyOnce(_) => {
                            // This should never happen as the telemetry receiver should always receive QoS 1 messages
                            log::warn!("Received QoS 2 telemetry message");
                            continue;
                        }
                    };

                    // Process the received message
                    log::debug!("[pkid: {pkid}] Received message");

                    match TryInto::<Message<T>>::try_into(m) {
                        Ok(mut message) => {
                            // Update the topic tokens
                            // NOTE: Tokens can't be added as part of the try_into conversion, as
                            // it requires knowledge from the Receiver.
                            message
                                .topic_tokens
                                .extend(self.topic_pattern.parse_tokens(&message.topic));

                            // Update application HLC
                            if let Some(hlc) = &message.timestamp
                                && let Err(e) = self.application_hlc.update(hlc)
                            {
                                log::warn!(
                                    "[pkid: {pkid}]: Failure updating application HLC against received telemetry HLC {hlc}: {e}"
                                );
                            }
                            return Some(Ok((message, ack_token)));
                        }
                        Err(e_string) => {
                            log::warn!("[pkid: {pkid}] {e_string}");

                            // Ack on error to prevent redelivery
                            if let Some(ack_token) = ack_token {
                                tokio::spawn({
                                    let receiver_cancellation_token_clone =
                                        self.cancellation_token.clone();
                                    async move {
                                        tokio::select! {
                                            () = receiver_cancellation_token_clone.cancelled() => { /* Received loop cancelled */ },
                                            ack_res = ack_token.ack() => {
                                                match ack_res {
                                                    Ok(_) => { /* Success */ }
                                                    Err(e) => {
                                                        log::warn!("[pkid: {pkid}] Telemetry Receiver Ack error {e}");
                                                    }
                                                }
                                            }
                                        }
                                    }
                                });
                            }
                        }
                    }
                }
                _ => {
                    // There will be no more messages
                    return None;
                }
            }
        }
    }
}

impl<T> Drop for Receiver<T>
where
    T: PayloadSerialize + Send + Sync + 'static,
{
    fn drop(&mut self) {
        // Cancel all tasks awaiting responses
        self.cancellation_token.cancel();
        // Close the receiver
        self.mqtt_receiver.close();

        // If the receiver has not unsubscribed, attempt to unsubscribe
        if State::Subscribed == self.state {
            tokio::spawn({
                let telemetry_topic = self.telemetry_topic.clone();
                let mqtt_client = self.mqtt_client.clone();
                async move {
                    match mqtt_client
                        .unsubscribe(
                            telemetry_topic.clone(),
                            azure_iot_operations_mqtt::control_packet::UnsubscribeProperties::default(),
                        )
                        .await
                    {
                        Ok(_) => {
                            log::debug!(
                                "Telemetry Receiver Unsubscribe sent on topic {telemetry_topic}. Unsuback may still be pending."
                            );
                        }
                        Err(e) => {
                            log::warn!("Telemetry Receiver Unsubscribe error on topic {telemetry_topic}: {e}");
                        }
                    }
                }
            });
        }

        log::info!("Telemetry receiver dropped");
    }
}

#[cfg(test)]
mod tests {
    use test_case::test_case;

    use super::*;
    use crate::{
        application::ApplicationContextBuilder,
        common::{
            aio_protocol_error::{AIOProtocolErrorKind, Value},
            payload_serialize::MockPayload,
        },
        telemetry::receiver::{OptionsBuilder, Receiver},
    };
    use azure_iot_operations_mqtt::{
        aio::connection_settings::MqttConnectionSettingsBuilder,
        session::{Session, SessionOptionsBuilder},
    };

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
        let session = get_session();
        let receiver_options = OptionsBuilder::default()
            .topic_pattern("test/receiver")
            .build()
            .unwrap();

        Receiver::<MockPayload>::new(
            ApplicationContextBuilder::default().build().unwrap(),
            session.create_managed_client(),
            receiver_options,
        )
        .unwrap();
    }

    #[test]
    fn test_new_override_defaults() {
        let session = get_session();
        let receiver_options = OptionsBuilder::default()
            .topic_pattern("test/{telemetryName}/receiver")
            .topic_namespace("test_namespace")
            .topic_token_map(create_topic_tokens())
            .build()
            .unwrap();

        Receiver::<MockPayload>::new(
            ApplicationContextBuilder::default().build().unwrap(),
            session.create_managed_client(),
            receiver_options,
        )
        .unwrap();
    }

    #[test_case(""; "new_empty_topic_pattern")]
    #[test_case(" "; "new_whitespace_topic_pattern")]
    fn test_new_empty_topic_pattern(topic_pattern: &str) {
        let session = get_session();
        let receiver_options = OptionsBuilder::default()
            .topic_pattern(topic_pattern)
            .build()
            .unwrap();

        let result: Result<Receiver<MockPayload>, _> = Receiver::new(
            ApplicationContextBuilder::default().build().unwrap(),
            session.create_managed_client(),
            receiver_options,
        );
        match result {
            Ok(_) => panic!("Expected error"),
            Err(e) => {
                assert_eq!(e.kind, AIOProtocolErrorKind::ConfigurationInvalid);
                assert!(e.is_shallow);
                assert!(!e.is_remote);
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
        let session = get_session();
        let receiver_options = OptionsBuilder::default()
            .topic_pattern("test/receiver")
            .build()
            .unwrap();

        let mut receiver: Receiver<MockPayload> = Receiver::new(
            ApplicationContextBuilder::default().build().unwrap(),
            session.create_managed_client(),
            receiver_options,
        )
        .unwrap();
        assert!(receiver.shutdown().await.is_ok());
    }
}

// Test cases for recv telemetry
// Tests failure:
//   if properties are missing, the message is not processed and is acked
//   if content type is not supported, the message is not processed and is acked
//   if timestamp is invalid, the message is not processed and is acked
//   if payload deserialization fails, the message is not processed and is acked
//
// Test cases for telemetry message processing
// Tests success:
//   QoS 1 message is processed and AckToken is used, message is acked
