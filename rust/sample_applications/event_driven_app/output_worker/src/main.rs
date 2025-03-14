// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

use std::{
    sync::{Arc, Mutex},
    time::Duration,
};

use azure_iot_operations_mqtt::{
    session::{
        self, Session, SessionConnectionMonitor, SessionExitHandle, SessionManagedClient,
        SessionOptionsBuilder,
    },
    MqttConnectionSettingsBuilder,
};
use azure_iot_operations_protocol::{
    application::{ApplicationContext, ApplicationContextBuilder},
    common::payload_serialize::{
        DeserializationError, FormatIndicator, PayloadSerialize, SerializedPayload,
    },
    telemetry::{
        telemetry_receiver::{TelemetryReceiver, TelemetryReceiverOptionsBuilder},
        telemetry_sender::{
            TelemetryMessageBuilder, TelemetrySender, TelemetrySenderOptionsBuilder,
        },
    },
};
use azure_iot_operations_services::state_store::{self, SetOptions};
use chrono::{DateTime, Utc};
use derive_builder::Builder;
use serde::{Deserialize, Serialize};
use tokio::io::join;

const PUBLISH_INTERVAL: Duration = Duration::from_secs(10);
const IS_IN_CLUSTER: bool = false;
const STATE_STORE_SENSOR_KEY: &str = "event_app_sample";
const WINDOW_SIZE: i64 = 60;

#[tokio::main(flavor = "current_thread")]
async fn main() {
    env_logger::Builder::new()
        .filter_level(log::LevelFilter::max())
        .format_timestamp(None)
        .filter_module("rumqttc", log::LevelFilter::Warn)
        .init();

    // ASK: How do I set this? Idea, maybe there is a k8 environment variable?
    let connection_settings = if IS_IN_CLUSTER {
        // If running in a cluster, read from environment variables
        log::info!("Running in cluster, load config from environment");
        MqttConnectionSettingsBuilder::from_environment()
            .unwrap()
            .build()
            .unwrap()
    } else {
        // If running locally, use default settings
        log::info!("Running locally, setting config directly");
        MqttConnectionSettingsBuilder::default()
            .hostname("localhost")
            .client_id("EventDrivenApp-output")
            .tcp_port(8884u16)
            .use_tls(true)
            .ca_file("../../../.session/broker-ca.crt".to_string())
            .sat_file("../../../.session/token.txt".to_string())
            .clean_start(true)
            .build()
            .unwrap()
    };

    let session_options = SessionOptionsBuilder::default()
        .connection_settings(connection_settings)
        .build()
        .unwrap();
    let session = Session::new(session_options).unwrap();

    let application_context = ApplicationContextBuilder::default().build().unwrap();

    let process_window_task = tokio::task::spawn(process_window(
        application_context.clone(),
        session.create_managed_client(),
        session.create_connection_monitor(),
    ));

    // FIN: log::info!("Connecting to: {:?}", connection_settings);
    assert!(tokio::try_join!(
        async move { session.run().await.map_err(|e| { e.to_string() }) },
        async move { process_window_task.await.map_err(|e| e.to_string()) },
    )
    .is_ok());
}

async fn process_window(
    application_context: ApplicationContext,
    client: SessionManagedClient,
    connection_monitor: SessionConnectionMonitor,
) {
    // Create sender
    let sender_options = TelemetrySenderOptionsBuilder::default()
        .topic_pattern("sensor/window_data")
        .build()
        .unwrap();

    let sender =
        TelemetrySender::new(application_context.clone(), client.clone(), sender_options).unwrap();

    let state_store_client = state_store::Client::new(
        application_context,
        client,
        connection_monitor,
        state_store::ClientOptionsBuilder::default()
            .build()
            .unwrap(),
    )
    .unwrap();

    loop {
        // Get the past sensor data from the state store // ASK:Any specific limits for state store timeouts?
        let response = state_store_client
            .get(STATE_STORE_SENSOR_KEY.into(), Duration::from_secs(10))
            .await;

        match response {
            Ok(data) => {
                match data.response {
                    Some(data) => match serde_json::from_slice::<Vec<SensorData>>(&data) {
                        Ok(mut data) => {
                            data.retain(|d| {
                                Utc::now() - d.timestamp < chrono::Duration::seconds(WINDOW_SIZE)
                            });
                            if data.is_empty() {
                                continue;
                            }

                            let output_data = WindowDataBuilder::default()
                                .timestamp(Utc::now())
                                .window_size(WINDOW_SIZE)
                                .temperature(&data)
                                .pressure(&data)
                                .vibration(&data)
                                .build()
                                .unwrap();
                            let output_data_clone = output_data.clone();

                            let message = TelemetryMessageBuilder::default()
                                .payload(output_data)
                                .unwrap()
                                .build()
                                .unwrap();

                            match sender.send(message).await {
                                Ok(_) => {
                                    log::info!(
                                        "Published window data: {}",
                                        serde_json::to_string(&output_data_clone)
                                            .expect("Failed to serialize payload")
                                    );
                                }
                                Err(e) => {
                                    log::error!("{e:?}");
                                    continue;
                                }
                            }

                            // Wait before processing the next window
                            tokio::time::sleep(PUBLISH_INTERVAL).await;
                        }
                        Err(e) => {
                            log::error!("{e:?}");
                            continue;
                        }
                    },
                    None => {
                        log::info!("Sensor data not found in state store");
                        continue;
                    }
                };
            }
            Err(e) => log::error!("{e:?}"),
        }
    }
}

// Sensor Data
#[derive(Debug, Clone, Default, Serialize, Deserialize)]
pub struct SensorData {
    pub sensor_id: String,
    pub timestamp: DateTime<Utc>,
    pub temperature: f64,
    pub pressure: f64,
    pub vibration: f64,
    pub msg_number: i64,
}

impl PayloadSerialize for SensorData {
    type Error = String;

    fn serialize(self) -> Result<SerializedPayload, Self::Error> {
        unreachable!()
    }

    fn deserialize(
        payload: &[u8],
        content_type: &Option<String>,
        _format_indicator: &FormatIndicator,
    ) -> Result<Self, DeserializationError<Self::Error>> {
        // ASK: Ask if the message is being sent with content type like this
        if let Some(content_type) = content_type {
            if content_type != "application/json" {
                return Err(DeserializationError::UnsupportedContentType(format!(
                    "Invalid content type: '{content_type:?}'. Must be 'application/json'"
                )));
            }
        }

        let payload = serde_json::from_slice(payload).map_err(|e| {
            DeserializationError::InvalidPayload(format!("Failed to deserialize payload: {e}"))
        })?;

        Ok(payload)
    }
}

#[derive(Debug, Clone, Default, Serialize, Deserialize)]
pub struct WindowSensorData {
    pub min: f64,
    pub max: f64,
    pub mean: f64,
    pub median: f64,
    pub count: i64,
}

// Window Data
#[derive(Debug, Clone, Default, Serialize, Deserialize, Builder)]
#[builder(setter(into))]
pub struct WindowData {
    pub timestamp: DateTime<Utc>,
    pub window_size: i64,
    #[builder(setter(custom))]
    pub temperature: WindowSensorData,
    #[builder(setter(custom))]
    pub pressure: WindowSensorData,
    #[builder(setter(custom))]
    pub vibration: WindowSensorData,
}

impl WindowDataBuilder {
    fn aggregate(&mut self, data: &Vec<f64>) -> WindowSensorData {
        let mut data = data.clone();

        // ASK: Should we be bubbling an error up?
        data.sort_by(|a, b| a.partial_cmp(b).expect("f64 comparison failed"));
        let count = data.len() as i64;
        let min = *data.first().expect("No data found");
        let max = *data.last().expect("No data found");
        let mean = data.iter().sum::<f64>() / count as f64;
        let median = if count % 2 == 0 {
            (data[count as usize / 2] + data[count as usize / 2 - 1]) / 2.0
        } else {
            data[count as usize / 2]
        };

        WindowSensorData {
            min,
            max,
            mean,
            median,
            count,
        }
    }

    pub fn temperature(&mut self, input_data: &Vec<SensorData>) -> &mut Self {
        let data = input_data.iter().map(|d| d.temperature).collect();
        self.temperature = Some(self.aggregate(&data));

        self
    }

    pub fn pressure(&mut self, input_data: &Vec<SensorData>) -> &mut Self {
        let data = input_data.iter().map(|d| d.pressure).collect();
        self.pressure = Some(self.aggregate(&data));

        self
    }

    pub fn vibration(&mut self, input_data: &Vec<SensorData>) -> &mut Self {
        let data = input_data.iter().map(|d| d.vibration).collect();
        self.vibration = Some(self.aggregate(&data));

        self
    }
}

impl PayloadSerialize for WindowData {
    type Error = String;

    fn serialize(self) -> Result<SerializedPayload, Self::Error> {
        Ok(SerializedPayload {
            // ASK: Is this the correct content type and format indicator to use?
            payload: serde_json::to_vec(&self).expect("Failed to serialize payload"),
            content_type: "application/json".to_string(),
            format_indicator: FormatIndicator::Utf8EncodedCharacterData,
        })
    }

    fn deserialize(
        _payload: &[u8],
        _content_type: &Option<String>,
        _format_indicator: &FormatIndicator,
    ) -> Result<Self, DeserializationError<Self::Error>> {
        unreachable!()
    }
}
