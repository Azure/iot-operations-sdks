// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
use std::{collections::HashMap, fmt::Display, marker::PhantomData, str::FromStr, sync::Arc};

use azure_iot_operations_mqtt::{
    control_packet::{Publish, QoS},
    interface::{AckToken, ManagedClient, PubReceiver},
};
use chrono::{DateTime, Utc};
use tokio_util::sync::CancellationToken;

use crate::{
    ProtocolVersion,
    application::{ApplicationContext, ApplicationHybridLogicalClock},
    common::{
        aio_protocol_error::{AIOProtocolError, Value},
        hybrid_logical_clock::HybridLogicalClock,
        payload_serialize::{FormatIndicator, PayloadSerialize},
        topic_processor::TopicPattern,
        user_properties::UserProperty,
    },
    telemetry::{
        DEFAULT_TELEMETRY_PROTOCOL_VERSION,
        cloud_event::{CloudEventFields, DEFAULT_CLOUD_EVENT_SPEC_VERSION},
    },
};

const SUPPORTED_PROTOCOL_VERSIONS: &[u16] = &[1];

/// Cloud Event struct used by the [`Receiver`].
///
/// Implements the cloud event spec 1.0 for the telemetry receiver.
/// See [CloudEvents Spec](https://github.com/cloudevents/spec/blob/main/cloudevents/spec.md).
#[derive(Builder, Clone)]
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
    #[builder(setter(skip))]
    pub time: Option<DateTime<Utc>>,
    /// time as a string so that it can be validated during build
    #[builder(default = "None")]
    builder_time: Option<String>,
}

impl CloudEventBuilder {
    // now that spec version is known, all fields can be validated against that spec version
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

        if let Some(Some(builder_time)) = &self.builder_time {
            CloudEventFields::Time.validate(builder_time, &spec_version)?;
        }

        Ok(())
    }
}

// implementing display because debug prints private fields
impl Display for CloudEvent {
    fn fmt(&self, f: &mut std::fmt::Formatter<'_>) -> std::fmt::Result {
        write!(
            f,
            "CloudEvent {{ id: {id}, source: {source}, spec_version: {spec_version}, event_type: {event_type}, subject: {subject}, data_schema: {data_schema}, data_content_type: {data_content_type}, time: {time:?} }}",
            id = self.id,
            source = self.source,
            spec_version = self.spec_version,
            event_type = self.event_type,
            subject = self.subject.as_deref().unwrap_or("None"),
            data_schema = self.data_schema.as_deref().unwrap_or("None"),
            data_content_type = self.data_content_type.as_deref().unwrap_or("None"),
            time = self.time,
        )
    }
}

impl CloudEvent {
    /// Parse a [`CloudEvent`] from a [`Message`].
    /// Note that this will return an error if the [`Message`] does not contain the required fields for a [`CloudEvent`].
    ///
    /// # Errors
    /// [`CloudEventBuilderError::UninitializedField`] if the [`Message`] does not contain the required fields for a [`CloudEvent`].
    ///
    /// [`CloudEventBuilderError::ValidationError`] if any of the field values are not valid for a [`CloudEvent`].
    pub fn from_telemetry<T: PayloadSerialize>(
        telemetry: &Message<T>,
    ) -> Result<Self, CloudEventBuilderError> {
        // use builder so that all fields can be validated together
        let mut cloud_event_builder = CloudEventBuilder::default();
        if let Some(content_type) = &telemetry.content_type {
            cloud_event_builder.data_content_type(content_type.clone());
        }

        for (key, value) in &telemetry.custom_user_data {
            match CloudEventFields::from_str(key) {
                Ok(CloudEventFields::Id) => {
                    cloud_event_builder.id(value);
                }
                Ok(CloudEventFields::Source) => {
                    cloud_event_builder.source(value);
                }
                Ok(CloudEventFields::SpecVersion) => {
                    cloud_event_builder.spec_version(value);
                }
                Ok(CloudEventFields::EventType) => {
                    cloud_event_builder.event_type(value);
                }
                Ok(CloudEventFields::Subject) => {
                    cloud_event_builder.subject(Some(value.into()));
                }
                Ok(CloudEventFields::DataSchema) => {
                    cloud_event_builder.data_schema(Some(value.into()));
                }
                Ok(CloudEventFields::Time) => {
                    cloud_event_builder.builder_time(Some(value.into()));
                }
                _ => {}
            }
        }
        let mut cloud_event = cloud_event_builder.build()?;
        // now that everything is validated, update the time field to its correct typing
        // NOTE: If the spec_version changes in the future, that may need to be taken into account here.
        // For now, the builder validates spec version 1.0
        if let Some(ref time_str) = cloud_event.builder_time {
            match DateTime::parse_from_rfc3339(time_str) {
                Ok(parsed_time) => {
                    let time = parsed_time.with_timezone(&Utc);
                    cloud_event.time = Some(time);
                }
                Err(_) => {
                    // Builder should have already caught this error
                    unreachable!()
                }
            }
        }
        Ok(cloud_event)
    }
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

        let publish_properties = value.properties.ok_or("Publish contains no properties")?;

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

        // Parse topic
        let topic = std::str::from_utf8(&value.topic)
            .map_err(|e| e.to_string())?
            .to_string();

        // Deserialize payload
        let format_indicator = publish_properties.payload_format_indicator.try_into().unwrap_or_else(|e| {
            log::error!("Received invalid payload format indicator: {e}. This should not be possible to receive from the broker. Using default.");
            FormatIndicator::default()
        });
        let content_type = publish_properties.content_type;
        let payload = T::deserialize(&value.payload, content_type.as_ref(), &format_indicator)
            .map_err(|e| format!("{e:?}"))?;

        let telemetry_message = Message {
            payload,
            content_type,
            format_indicator,
            custom_user_data: telemetry_custom_user_data,
            sender_id: telemetry_aio_data.remove(&UserProperty::SourceId),
            timestamp,
            // NOTE: Topic Tokens cannot be created from just a Publish, they need additional information
            topic_tokens: HashMap::default(),
            topic,
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
/// # use azure_iot_operations_mqtt::MqttConnectionSettingsBuilder;
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
/// let mut receiver: telemetry::Receiver<Vec<u8>, _> = telemetry::Receiver::new(application_context, mqtt_session.create_managed_client(), receiver_options).unwrap();
/// // let telemetry_message = receiver.recv().await.unwrap();
/// ```
pub struct Receiver<T, C>
where
    T: PayloadSerialize + Send + Sync + 'static,
    C: ManagedClient + Clone + Send + Sync + 'static,
    C::PubReceiver: Send + Sync + 'static,
{
    // Static properties of the receiver
    application_hlc: Arc<ApplicationHybridLogicalClock>,
    mqtt_client: C,
    mqtt_receiver: C::PubReceiver,
    telemetry_topic: String,
    topic_pattern: TopicPattern,
    message_payload_type: PhantomData<T>,
    // Describes state
    receiver_state: State,
    // Information to manage state
    receiver_cancellation_token: CancellationToken,
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
impl<T, C> Receiver<T, C>
where
    T: PayloadSerialize + Send + Sync + 'static,
    C: ManagedClient + Clone + Send + Sync + 'static,
    C::PubReceiver: Send + Sync + 'static,
{
    /// Creates a new [`Receiver`].
    ///
    /// # Arguments
    /// * `application_context` - [`ApplicationContext`] that the telemetry receiver is part of.
    /// * `client` - [`ManagedClient`] to use for telemetry communication.
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
        client: C,
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
        let telemetry_topic = topic_pattern.as_subscribe_topic();

        let mqtt_receiver = match client.create_filtered_pub_receiver(&telemetry_topic) {
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
            application_hlc: application_context.application_hlc,
            mqtt_client: client,
            mqtt_receiver,
            telemetry_topic,
            topic_pattern,
            message_payload_type: PhantomData,
            receiver_state: State::New,
            receiver_cancellation_token: CancellationToken::new(),
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

        match self.receiver_state {
            State::New | State::ShutdownSuccessful => {
                // If subscribe has not been called or shutdown was successful, do not unsubscribe
                self.receiver_state = State::ShutdownSuccessful;
            }
            State::Subscribed => {
                let unsubscribe_result = self.mqtt_client.unsubscribe(&self.telemetry_topic).await;

                match unsubscribe_result {
                    Ok(unsub_ct) => match unsub_ct.await {
                        Ok(()) => {
                            self.receiver_state = State::ShutdownSuccessful;
                        }
                        Err(e) => {
                            log::error!("Unsuback error: {e}");
                            return Err(AIOProtocolError::new_mqtt_error(
                                Some("MQTT error on telemetry receiver unsuback".to_string()),
                                Box::new(e),
                                None,
                            ));
                        }
                    },
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
            .subscribe(&self.telemetry_topic, QoS::AtLeastOnce)
            .await;

        match subscribe_result {
            Ok(sub_ct) => match sub_ct.await {
                Ok(()) => { /* Success */ }
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

    /// Receives a telemetry message or [`None`] if there will be no more messages.
    /// If there are messages:
    /// - Returns Ok([`Message`], [`Option<AckToken>`]) on success
    ///     - If the message is received with Quality of Service 1 an [`AckToken`] is returned.
    /// - Returns [`AIOProtocolError`] on error.
    ///
    /// A received message can be acknowledged via the [`AckToken`] by calling [`AckToken::ack`] or dropping the [`AckToken`].
    ///
    /// Will also subscribe to the telemetry topic if not already subscribed.
    ///
    /// # Errors
    /// [`AIOProtocolError`] of kind [`ClientError`](crate::common::aio_protocol_error::AIOProtocolErrorKind::ClientError) if the subscribe fails or if the suback reason code doesn't indicate success.
    pub async fn recv(
        &mut self,
    ) -> Option<Result<(Message<T>, Option<AckToken>), AIOProtocolError>> {
        // Subscribe to the telemetry topic if not already subscribed
        if self.receiver_state == State::New {
            if let Err(e) = self.try_subscribe().await {
                return Some(Err(e));
            }
            self.receiver_state = State::Subscribed;
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
                    let pkid = m.pkid;

                    // Process the received message
                    log::info!("[pkid: {pkid}] Received message");

                    match TryInto::<Message<T>>::try_into(m) {
                        Ok(mut message) => {
                            // Update the topic tokens
                            // NOTE: Tokens can't be added as part of the try_into conversion, as
                            // it requires knowledge from the Receiver.
                            message
                                .topic_tokens
                                .extend(self.topic_pattern.parse_tokens(&message.topic));

                            // Update application HLC
                            if let Some(hlc) = &message.timestamp {
                                if let Err(e) = self.application_hlc.update(hlc) {
                                    log::error!(
                                        "[pkid: {pkid}]: Failure updating application HLC against {hlc}: {e}"
                                    );
                                }
                            }
                            return Some(Ok((message, ack_token)));
                        }
                        Err(e_string) => {
                            log::error!("[pkid: {pkid}] {e_string}");

                            // Ack on error to prevent redelivery
                            if let Some(ack_token) = ack_token {
                                tokio::spawn({
                                    let receiver_cancellation_token_clone =
                                        self.receiver_cancellation_token.clone();
                                    async move {
                                        tokio::select! {
                                            () = receiver_cancellation_token_clone.cancelled() => { /* Received loop cancelled */ },
                                            ack_res = ack_token.ack() => {
                                                match ack_res {
                                                    Ok(_) => { /* Success */ }
                                                    Err(e) => {
                                                        log::error!("[pkid: {pkid}] Ack error {e}");
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

impl<T, C> Drop for Receiver<T, C>
where
    T: PayloadSerialize + Send + Sync + 'static,
    C: ManagedClient + Clone + Send + Sync + 'static,
    C::PubReceiver: Send + Sync + 'static,
{
    fn drop(&mut self) {
        // Cancel all tasks awaiting responses
        self.receiver_cancellation_token.cancel();
        // Close the receiver
        self.mqtt_receiver.close();

        // If the receiver has not unsubscribed, attempt to unsubscribe
        if State::Subscribed == self.receiver_state {
            tokio::spawn({
                let telemetry_topic = self.telemetry_topic.clone();
                let mqtt_client = self.mqtt_client.clone();
                async move {
                    match mqtt_client.unsubscribe(telemetry_topic.clone()).await {
                        Ok(_) => {
                            log::debug!(
                                "Unsubscribe sent on topic {telemetry_topic}. Unsuback may still be pending."
                            );
                        }
                        Err(e) => {
                            log::error!("Unsubscribe error on topic {telemetry_topic}: {e}");
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
        common::{aio_protocol_error::AIOProtocolErrorKind, payload_serialize::MockPayload},
        telemetry::receiver::{OptionsBuilder, Receiver},
    };
    use azure_iot_operations_mqtt::{
        MqttConnectionSettingsBuilder,
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

        Receiver::<MockPayload, _>::new(
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

        Receiver::<MockPayload, _>::new(
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

        let result: Result<Receiver<MockPayload, _>, _> = Receiver::new(
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

        let mut receiver: Receiver<MockPayload, _> = Receiver::new(
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
