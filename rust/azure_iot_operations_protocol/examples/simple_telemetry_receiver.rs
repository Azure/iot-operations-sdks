// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

use std::collections::HashMap;
use std::time::Duration;

use env_logger::Builder;

use azure_iot_operations_mqtt::MqttConnectionSettingsBuilder;
use azure_iot_operations_mqtt::session::{Session, SessionManagedClient, SessionOptionsBuilder};
use azure_iot_operations_protocol::{
    application::ApplicationContextBuilder,
    common::payload_serialize::{
        DeserializationError, FormatIndicator, PayloadSerialize, SerializedPayload,
    },
    telemetry,
};

const CLIENT_ID: &str = "myReceiver";
const HOSTNAME: &str = "localhost";
const PORT: u16 = 1883;
const TOPIC: &str = "akri/samples/{modelId}/new";
const MODEL_ID: &str = "dtmi:akri:samples:oven;1";

#[tokio::main(flavor = "current_thread")]
async fn main() -> Result<(), Box<dyn std::error::Error>> {
    Builder::new()
        .filter_level(log::LevelFilter::Info)
        .format_timestamp(None)
        .filter_module("rumqttc", log::LevelFilter::Warn)
        .init();

    // Create a Session
    let connection_settings = MqttConnectionSettingsBuilder::default()
        .client_id(CLIENT_ID)
        .hostname(HOSTNAME)
        .tcp_port(PORT)
        .keep_alive(Duration::from_secs(5))
        .use_tls(false)
        .build()?;
    let session_options = SessionOptionsBuilder::default()
        .connection_settings(connection_settings)
        .build()?;
    let session = Session::new(session_options)?;

    // Create an ApplicationContext
    let application_context = ApplicationContextBuilder::default().build()?;

    // Create a telemetry Receiver
    let receiver_options = telemetry::receiver::OptionsBuilder::default()
        .topic_pattern(TOPIC)
        .topic_token_map(HashMap::from([(
            "modelId".to_string(),
            MODEL_ID.to_string(),
        )]))
        .auto_ack(false)
        .build()?;
    let receiver: telemetry::Receiver<SampleTelemetry, _> = telemetry::Receiver::new(
        application_context,
        session.create_managed_client(),
        receiver_options,
    )?;

    // Run the Session and the telemetry loop concurrently
    tokio::select! {
        r1 = telemetry_loop(receiver) => r1.map_err(|e| e as Box<dyn std::error::Error>)?,
        r2 = session.run() => r2?,
    }

    Ok(())
}

// Handle incoming telemetry messages
async fn telemetry_loop(
    mut telemetry_receiver: telemetry::Receiver<SampleTelemetry, SessionManagedClient>,
) -> Result<(), Box<dyn std::error::Error>> {
    while let Some(msg_result) = telemetry_receiver.recv().await {
        let (message, ack_token) = msg_result?;
        // Handle the telemetry message. If no acknowledgement is needed, ack_token will be None
        log::info!(
            "Sender {:?} sent temperature reading: {:?}",
            message.sender_id,
            message.payload
        );

        // Parse cloud event
        match telemetry::receiver::CloudEvent::from_telemetry(&message) {
            Ok(cloud_event) => {
                log::info!("{cloud_event}");
            }
            Err(e) => {
                // If a cloud event is not present, this error is expected
                log::warn!("Error parsing cloud event: {e}");
            }
        }

        // Acknowledge the message if ack_token is present
        if let Some(ack_token) = ack_token {
            let completion_token = ack_token.ack().await?;
            match completion_token.await {
                Ok(()) => log::info!("Acknowledged message"),
                Err(e) => log::error!("Error acknowledging message: {e}"),
            }
        }
    }

    // Shut down if there are no more telemetry messages
    telemetry_receiver.shutdown().await?;

    Ok(())
}

#[derive(Clone, Debug, Default)]
pub struct SampleTelemetry {
    pub external_temperature: f64,
    pub internal_temperature: f64,
}

impl PayloadSerialize for SampleTelemetry {
    type Error = String;

    fn serialize(self) -> Result<SerializedPayload, String> {
        // Not used in this example
        unimplemented!()
    }

    fn deserialize(
        payload: &[u8],
        content_type: Option<&String>,
        _format_indicator: &FormatIndicator,
    ) -> Result<SampleTelemetry, DeserializationError<String>> {
        if let Some(content_type) = content_type {
            if content_type != "application/json" {
                return Err(DeserializationError::UnsupportedContentType(format!(
                    "Invalid content type: '{content_type:?}'. Must be 'application/json'"
                )));
            }
        }

        let payload = match String::from_utf8(payload.to_vec()) {
            Ok(p) => p,
            Err(e) => {
                return Err(DeserializationError::InvalidPayload(format!(
                    "Error while deserializing telemetry: {e}"
                )));
            }
        };
        let payload = payload.split(',').collect::<Vec<&str>>();

        let external_temperature = match payload[0]
            .trim_start_matches("{\"externalTemperature\":")
            .parse::<f64>()
        {
            Ok(ext_temp) => ext_temp,
            Err(e) => {
                return Err(DeserializationError::InvalidPayload(format!(
                    "Error while deserializing telemetry: {e}"
                )));
            }
        };
        let internal_temperature = match payload[1]
            .trim_start_matches("\"internalTemperature\":")
            .trim_end_matches('}')
            .parse::<f64>()
        {
            Ok(int_temp) => int_temp,
            Err(e) => {
                return Err(DeserializationError::InvalidPayload(format!(
                    "Error while deserializing telemetry: {e}"
                )));
            }
        };

        Ok(SampleTelemetry {
            external_temperature,
            internal_temperature,
        })
    }
}
