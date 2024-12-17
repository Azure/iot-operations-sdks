// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

use std::collections::HashMap;
use std::time::Duration;

use env_logger::Builder;

use azure_iot_operations_mqtt::session::{
    Session, SessionExitHandle, SessionManagedClient, SessionOptionsBuilder,
};
use azure_iot_operations_mqtt::MqttConnectionSettingsBuilder;
use azure_iot_operations_protocol::telemetry::telemetry_sender::{
    CloudEventBuilder, TelemetrySender,
};
use azure_iot_operations_protocol::{
    common::payload_serialize::{FormatIndicator, PayloadSerialize},
    telemetry::telemetry_sender::{TelemetryMessageBuilder, TelemetrySenderOptionsBuilder},
};

const CLIENT_ID: &str = "myClient";
const HOSTNAME: &str = "localhost";
const PORT: u16 = 1883;
const TOPIC: &str = "akri/samples/{modelId}/new";
const MODEL_ID: &str = "dtmi:akri:samples:oven;1";

#[tokio::main(flavor = "current_thread")]
async fn main() {
    Builder::new()
        .filter_level(log::LevelFilter::max())
        .format_timestamp(None)
        .filter_module("rumqttc", log::LevelFilter::Warn)
        .init();

    let connection_settings = MqttConnectionSettingsBuilder::default()
        .client_id(CLIENT_ID)
        .hostname(HOSTNAME)
        .tcp_port(PORT)
        .keep_alive(Duration::from_secs(5))
        .use_tls(false)
        .build()
        .unwrap();

    let session_options = SessionOptionsBuilder::default()
        .connection_settings(connection_settings)
        .build()
        .unwrap();

    let mut session = Session::new(session_options).unwrap();
    let exit_handle = session.create_exit_handle();

    let sender_options = TelemetrySenderOptionsBuilder::default()
        .topic_pattern(TOPIC)
        .build()
        .unwrap();
    let telemetry_sender: TelemetrySender<SampleTelemetry, _> =
        TelemetrySender::new(session.create_managed_client(), sender_options).unwrap();

    tokio::task::spawn(telemetry_loop(telemetry_sender, exit_handle));

    session.run().await.unwrap();
}

/// Send 10 telemetry messages, then disconnect
async fn telemetry_loop(
    telemetry_sender: TelemetrySender<SampleTelemetry, SessionManagedClient>,
    exit_handle: SessionExitHandle,
) {
    for i in 1..10 {
        let cloud_event = CloudEventBuilder::default()
            .source("aio://oven/sample")
            .build()
            .unwrap();
        let message = TelemetryMessageBuilder::default()
            .payload(&SampleTelemetry {
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
        let result = telemetry_sender.send(message).await;
        log::info!("Result {}: {:?}", i, result);
    }

    exit_handle.try_exit().await.unwrap();
}

#[derive(Clone, Debug, Default)]
pub struct SampleTelemetry {
    pub external_temperature: i32,
    pub internal_temperature: i32,
}

impl PayloadSerialize for SampleTelemetry {
    type Error = String;
    fn content_type() -> &'static str {
        "application/json"
    }

    fn format_indicator() -> FormatIndicator {
        FormatIndicator::Utf8EncodedCharacterData
    }

    fn serialize(&self) -> Result<Vec<u8>, String> {
        Ok(format!(
            "{{\"externalTemperature\":{},\"internalTemperature\":{}}}",
            self.external_temperature, self.internal_temperature
        )
        .into())
    }

    fn deserialize(_payload: &[u8]) -> Result<SampleTelemetry, String> {
        Ok(SampleTelemetry::default())
    }
}
