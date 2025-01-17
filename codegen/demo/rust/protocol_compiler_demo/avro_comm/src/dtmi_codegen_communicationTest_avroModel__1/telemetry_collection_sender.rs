/* Code generated by Azure.Iot.Operations.ProtocolCompiler v0.6.0.0; DO NOT EDIT. */

use std::collections::HashMap;
use std::time::Duration;

use azure_iot_operations_mqtt::control_packet::QoS;
use azure_iot_operations_mqtt::interface::ManagedClient;
use azure_iot_operations_protocol::common::aio_protocol_error::AIOProtocolError;
use azure_iot_operations_protocol::common::payload_serialize::PayloadSerialize;
use azure_iot_operations_protocol::telemetry::telemetry_sender::{
    CloudEvent, TelemetryMessage, TelemetryMessageBuilder, TelemetryMessageBuilderError,
    TelemetrySender, TelemetrySenderOptionsBuilder,
};

use super::super::common_types::common_options::TelemetryOptions;
use super::telemetry_collection::TelemetryCollection;
use super::MODEL_ID;
use super::TELEMETRY_TOPIC_PATTERN;

pub type TelemetryCollectionMessage = TelemetryMessage<TelemetryCollection>;
pub type TelemetryCollectionMessageBuilderError = TelemetryMessageBuilderError;

/// Builder for [`TelemetryCollectionMessage`]
#[derive(Default)]
pub struct TelemetryCollectionMessageBuilder {
    inner_builder: TelemetryMessageBuilder<TelemetryCollection>,
}

impl TelemetryCollectionMessageBuilder {
    /// Quality of Service of the telemetry message. Can only be `AtMostOnce` or `AtLeastOnce`.
    pub fn qos(&mut self, qos: QoS) -> &mut Self {
        self.inner_builder.qos(qos);
        self
    }

    /// Custom user data to set on the message
    pub fn custom_user_data(&mut self, custom_user_data: Vec<(String, String)>) -> &mut Self {
        self.inner_builder.custom_user_data(custom_user_data);
        self
    }

    /// Time before message expires
    pub fn message_expiry(&mut self, message_expiry: Duration) -> &mut Self {
        self.inner_builder.message_expiry(message_expiry);
        self
    }

    /// Cloud event for the message
    pub fn cloud_event(&mut self, cloud_event: Option<CloudEvent>) -> &mut Self {
        self.inner_builder.cloud_event(cloud_event);
        self
    }

    /// Payload of the message
    ///
    /// # Errors
    /// If the payload cannot be serialized
    pub fn payload(
        &mut self,
        payload: TelemetryCollection,
    ) -> Result<&mut Self, <TelemetryCollection as PayloadSerialize>::Error> {
        self.inner_builder.payload(payload)?;
        Ok(self)
    }

    /// Builds a new `TelemetryCollectionMessage`
    ///
    /// # Errors
    /// If a required field has not been initialized
    pub fn build(
        &mut self,
    ) -> Result<TelemetryCollectionMessage, TelemetryCollectionMessageBuilderError> {
        self.inner_builder.build()
    }
}

/// Telemetry Sender for `TelemetryCollection`
pub struct TelemetryCollectionSender<C>(TelemetrySender<TelemetryCollection, C>)
where
    C: ManagedClient + Send + Sync + 'static;

impl<C> TelemetryCollectionSender<C>
where
    C: ManagedClient + Send + Sync + 'static,
{
    /// Creates a new [`TelemetryCollectionSender`]
    ///
    /// # Panics
    /// If the DTDL that generated this code was invalid
    pub fn new(client: C, options: &TelemetryOptions) -> Self {
        let mut sender_options_builder = TelemetrySenderOptionsBuilder::default();
        if let Some(topic_namespace) = &options.topic_namespace {
            sender_options_builder.topic_namespace(topic_namespace.clone());
        }

        let mut topic_token_map: HashMap<String, String> = options
            .topic_token_map
            .clone()
            .into_iter()
            .map(|(k, v)| (format!("ex:{k}"), v))
            .collect();

        topic_token_map.insert("modelId".to_string(), MODEL_ID.to_string());
        topic_token_map.insert("senderId".to_string(), client.client_id().to_string());

        let sender_options = sender_options_builder
            .topic_pattern(TELEMETRY_TOPIC_PATTERN)
            .topic_token_map(topic_token_map)
            .build()
            .expect("DTDL schema generated invalid arguments");

        Self(
            TelemetrySender::new(client, sender_options)
                .expect("DTDL schema generated invalid arguments"),
        )
    }

    /// Sends a [`TelemetryCollectionMessage`]
    ///
    /// # Error
    /// [`AIOProtocolError`] if there is a failure sending the message
    pub async fn send(&self, message: TelemetryCollectionMessage) -> Result<(), AIOProtocolError> {
        self.0.send(message).await
    }
}
