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

const CLIENT_ID: &str = "myClient";
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

    // Create a telemetry Sender
    let sender_options = telemetry::sender::OptionsBuilder::default()
        .topic_pattern(TOPIC)
        .build()?;
    let telemetry_sender: telemetry::Sender<SampleTelemetry, _> = telemetry::Sender::new(
        application_context,
        session.create_managed_client(),
        sender_options,
    )?;

    // Run the session and the telemetry loop concurrently
    tokio::select! {
        r1 = telemetry_loop(telemetry_sender) => r1,
        r2 = session.run() => r2?,
    };

    Ok(())
}

/// Indefinitely send Telemetry
async fn telemetry_loop(
    telemetry_sender: telemetry::Sender<SampleTelemetry, SessionManagedClient>,
) {
    loop {
        let cloud_event = telemetry::sender::CloudEventBuilder::default()
            .source("aio://oven/sample")
            .build()
            .unwrap();
        let message = telemetry::sender::MessageBuilder::default()
            .payload(SampleTelemetry {
                external_temperature: 100,
                internal_temperature: 200,
            })
            .unwrap()
            .topic_tokens(HashMap::from([(
                "modelId".to_string(),
                MODEL_ID.to_string(),
            )]))
            .message_expiry(Duration::from_secs(2))
            .cloud_event(cloud_event)
            .build()
            .unwrap();
        match telemetry_sender.send(message).await {
            Ok(()) => log::info!("Sent telemetry successfully"),
            Err(e) => log::error!("Error sending telemetry: {e}"),
        }
        tokio::time::sleep(Duration::from_secs(1)).await;
    }
}

#[derive(Clone, Debug, Default)]
pub struct SampleTelemetry {
    pub external_temperature: i32,
    pub internal_temperature: i32,
}

impl PayloadSerialize for SampleTelemetry {
    type Error = String;

    fn serialize(self) -> Result<SerializedPayload, String> {
        Ok(SerializedPayload {
            payload: format!(
                "{{\"externalTemperature\":{},\"internalTemperature\":{}}}",
                self.external_temperature, self.internal_temperature
            )
            .into(),
            content_type: "application/json".to_string(),
            format_indicator: FormatIndicator::Utf8EncodedCharacterData,
        })
    }

    fn deserialize(
        _payload: &[u8],
        _content_type: Option<&String>,
        _format_indicator: &FormatIndicator,
    ) -> Result<SampleTelemetry, DeserializationError<String>> {
        // Not used in this example
        unimplemented!()
    }
}
