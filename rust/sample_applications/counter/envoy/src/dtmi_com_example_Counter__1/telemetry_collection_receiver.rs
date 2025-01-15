/* Code generated by Azure.Iot.Operations.ProtocolCompiler v0.6.0.0; DO NOT EDIT. */

use std::collections::HashMap;

use azure_iot_operations_mqtt::interface::{AckToken, ManagedClient};
use azure_iot_operations_protocol::common::aio_protocol_error::AIOProtocolError;
use azure_iot_operations_protocol::telemetry::telemetry_receiver::{
    TelemetryMessage, TelemetryReceiver, TelemetryReceiverOptionsBuilder,
};
use azure_iot_operations_protocol::ApplicationContext;

use super::super::common_types::common_options::TelemetryOptions;
use super::telemetry_collection::TelemetryCollection;
use super::MODEL_ID;
use super::TELEMETRY_TOPIC_PATTERN;

pub type TelemetryCollectionMessage = TelemetryMessage<TelemetryCollection>;

/// Telemetry Receiver for `TelemetryCollection`
pub struct TelemetryCollectionReceiver<C>(TelemetryReceiver<TelemetryCollection, C>)
where
    C: ManagedClient + Clone + Send + Sync + 'static,
    C::PubReceiver: Send + Sync + 'static;

impl<C> TelemetryCollectionReceiver<C>
where
    C: ManagedClient + Clone + Send + Sync + 'static,
    C::PubReceiver: Send + Sync + 'static,
{
    /// Creates a new [`TelemetryCollectionReceiver`]
    ///
    /// # Panics
    /// If the DTDL that generated this code was invalid
    pub fn new(
        client: C,
        application_context: ApplicationContext,
        options: &TelemetryOptions,
    ) -> Self {
        let mut receiver_options_builder = TelemetryReceiverOptionsBuilder::default();
        if let Some(topic_namespace) = &options.topic_namespace {
            receiver_options_builder.topic_namespace(topic_namespace.clone());
        }

        let mut topic_token_map: HashMap<String, String> = options
            .topic_token_map
            .clone()
            .into_iter()
            .map(|(k, v)| (format!("ex:{k}"), v))
            .collect();

        topic_token_map.insert("modelId".to_string(), MODEL_ID.to_string());

        let receiver_options = receiver_options_builder
            .topic_pattern(TELEMETRY_TOPIC_PATTERN)
            .topic_token_map(topic_token_map)
            .auto_ack(options.auto_ack)
            .build()
            .expect("DTDL schema generated invalid arguments");

        Self(
            TelemetryReceiver::new(client, application_context, receiver_options)
                .expect("DTDL schema generated invalid arguments"),
        )
    }

    /// Shut down the [`TelemetryCollectionReceiver`]
    ///
    /// # Errors
    /// [`AIOProtocolError`] if there is a failure in graceful shutdown
    pub async fn shutdown(&mut self) -> Result<(), AIOProtocolError> {
        self.0.shutdown().await
    }

    /// Receive the next [`TelemetryCollectionMessage`]
    ///
    /// # Errors
    /// [`AIOProtocolError`] if there is a failure receiving a message
    pub async fn recv(
        &mut self,
    ) -> Option<Result<(TelemetryCollectionMessage, Option<AckToken>), AIOProtocolError>> {
        self.0.recv().await
    }
}
