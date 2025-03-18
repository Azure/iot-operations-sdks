// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

//! This sample application demonstrates how to create an event-driven application that gets sensor
//! data from a state store, aggregates it into windows, and publishes the window data.

use std::time::Duration;

use azure_iot_operations_mqtt::{
    session::{Session, SessionConnectionMonitor, SessionManagedClient, SessionOptionsBuilder},
    MqttConnectionSettingsBuilder,
};
use azure_iot_operations_protocol::{
    application::{ApplicationContext, ApplicationContextBuilder},
    common::payload_serialize::{
        DeserializationError, FormatIndicator, PayloadSerialize, SerializedPayload,
    },
    telemetry::telemetry_sender::{
        TelemetryMessageBuilder, TelemetrySender, TelemetrySenderOptionsBuilder,
    },
};
use azure_iot_operations_services::state_store::{self};
use chrono::{DateTime, Utc};
use derive_builder::Builder;
use serde::{Deserialize, Serialize};

const PUBLISH_INTERVAL: Duration = Duration::from_secs(10);
const STATE_STORE_SENSOR_KEY: &str = "event_app_sample";
const WINDOW_SIZE: i64 = 60;
const DEFAULT_STATE_STORE_OPERATION_TIMEOUT: Duration = Duration::from_secs(10);
const WINDOW_DATA_TOPIC: &str = "sensor/window_data";

#[tokio::main(flavor = "current_thread")]
async fn main() {
    env_logger::Builder::new()
        .filter_level(log::LevelFilter::max())
        .format_timestamp(None)
        .filter_module("rumqttc", log::LevelFilter::Warn)
        .init();

    // Create a session
    let connection_settings = MqttConnectionSettingsBuilder::from_environment()
        .unwrap()
        .build()
        .unwrap();
    let session_options = SessionOptionsBuilder::default()
        .connection_settings(connection_settings)
        .build()
        .unwrap();
    let session = Session::new(session_options).unwrap();

    // Create application context
    let application_context = ApplicationContextBuilder::default().build().unwrap();

    // Spawn a task to process sensor data, aggregate it into windows, and publish the window data
    let process_window_task = tokio::task::spawn(process_window(
        application_context.clone(),
        session.create_managed_client(),
        session.create_connection_monitor(),
    ));

    tokio::try_join!(
        async move { session.run().await.map_err(|e| { e.to_string() }) },
        async move { process_window_task.await.map_err(|e| e.to_string()) },
    )
    .unwrap();
}

async fn process_window(
    application_context: ApplicationContext,
    client: SessionManagedClient,
    connection_monitor: SessionConnectionMonitor,
) {
    // Create sender
    let sender_options = TelemetrySenderOptionsBuilder::default()
        .topic_pattern(WINDOW_DATA_TOPIC)
        .build()
        .expect("Telemetry sender options should not fail");
    let sender = TelemetrySender::new(application_context.clone(), client.clone(), sender_options)
        .expect("Telemetry sender creation should not fail");

    // Create state store client
    let state_store_client = state_store::Client::new(
        application_context,
        client,
        connection_monitor,
        state_store::ClientOptionsBuilder::default()
            .build()
            .expect("default state store options should not fail"),
    )
    .expect("state store client creation with default options should not fail");

    loop {
        // Wait before processing the next window
        tokio::time::sleep(PUBLISH_INTERVAL).await;

        // Get the past sensor data from the state store
        let get_result = state_store_client
            .get(
                STATE_STORE_SENSOR_KEY.into(),
                DEFAULT_STATE_STORE_OPERATION_TIMEOUT,
            )
            .await;

        match get_result {
            Ok(get_response) => {
                if let Some(serialized_data) = get_response.response {
                    // Deserialize the historical sensor data
                    match serde_json::from_slice::<Vec<SensorData>>(&serialized_data) {
                        Ok(mut sensor_data) => {
                            // Filter out old data
                            sensor_data.retain(|d| {
                                Utc::now() - d.timestamp < chrono::Duration::seconds(WINDOW_SIZE)
                            });

                            // If there is no data, skip the window
                            if sensor_data.is_empty() {
                                continue;
                            }

                            // Aggregate the sensor data into a window
                            let output_window_data = WindowDataBuilder::default()
                                .timestamp(Utc::now())
                                .window_size(WINDOW_SIZE)
                                .temperature(&sensor_data)
                                .pressure(&sensor_data)
                                .vibration(&sensor_data)
                                .build()
                                .expect("output_window_data should contain all fields");
                            let output_data_clone = output_window_data.clone();

                            let message = TelemetryMessageBuilder::default()
                                .payload(output_window_data)
                                .expect("output_window_data is a valid payload")
                                .build()
                                .expect("message should contain all fields");

                            match sender.send(message).await {
                                Ok(()) => {
                                    log::info!(
                                        "Published window data: {}",
                                        serde_json::to_string(&output_data_clone)
                                            .expect("output_data_clone should serialize")
                                    );
                                }
                                Err(e) => {
                                    // Error while sending telemetry
                                    log::error!("{e:?}");
                                    continue;
                                }
                            }
                        }
                        Err(e) => {
                            // Deserialization error
                            log::error!("{e:?}");
                            continue;
                        }
                    }
                } else {
                    log::info!("Sensor data not found in state store");
                    continue;
                };
            }
            // Error while fetching data from state store
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
        unreachable!("This method should not be called");
    }

    fn deserialize(
        payload: &[u8],
        content_type: &Option<String>,
        _format_indicator: &FormatIndicator,
    ) -> Result<Self, DeserializationError<Self::Error>> {
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

// Struct representing the aggregated sensor data for one sensor type in a window
#[derive(Debug, Clone, Default, Serialize, Deserialize)]
pub struct WindowSensorData {
    pub min: f64,
    pub max: f64,
    pub mean: f64,
    pub median: f64,
    pub count: i64,
}

/// Struct representing the aggregated sensor data for a window
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
    /// Helper function to aggregate sensor data into a window
    fn aggregate(sensor_data: &[f64]) -> WindowSensorData {
        let mut sensor_data: Vec<f64> = sensor_data.to_vec();

        sensor_data.sort_by(|a, b| a.partial_cmp(b).expect("f64 comparison should not fail"));
        let count: i64 = sensor_data
            .len()
            .try_into()
            .expect("usize to i64 conversion should not fail");
        let min = *sensor_data
            .first()
            .expect("data always contains at least one element");
        let max = *sensor_data
            .last()
            .expect("data always contains at least one element");
        let mean = sensor_data.iter().sum::<f64>()
            / f64::from(u32::try_from(sensor_data.len()).expect("element count should fit in u32"));
        let median = if count % 2 == 0 {
            (sensor_data[sensor_data.len() / 2] + sensor_data[sensor_data.len() / 2 - 1]) / 2.0
        } else {
            sensor_data[sensor_data.len() / 2]
        };

        WindowSensorData {
            min,
            max,
            mean,
            median,
            count,
        }
    }

    pub fn temperature(&mut self, input_data: &[SensorData]) -> &mut Self {
        let data: Vec<f64> = input_data.iter().map(|d| d.temperature).collect();
        self.temperature = Some(Self::aggregate(&data));

        self
    }

    pub fn pressure(&mut self, input_data: &[SensorData]) -> &mut Self {
        let data: Vec<f64> = input_data.iter().map(|d| d.pressure).collect();
        self.pressure = Some(Self::aggregate(&data));

        self
    }

    pub fn vibration(&mut self, input_data: &[SensorData]) -> &mut Self {
        let data: Vec<f64> = input_data.iter().map(|d| d.vibration).collect();
        self.vibration = Some(Self::aggregate(&data));

        self
    }
}

impl PayloadSerialize for WindowData {
    type Error = String;

    fn serialize(self) -> Result<SerializedPayload, Self::Error> {
        Ok(SerializedPayload {
            payload: serde_json::to_vec(&self).expect("A valid payload should serialize"),
            content_type: "application/json".to_string(),
            format_indicator: FormatIndicator::Utf8EncodedCharacterData,
        })
    }

    fn deserialize(
        _payload: &[u8],
        _content_type: &Option<String>,
        _format_indicator: &FormatIndicator,
    ) -> Result<Self, DeserializationError<Self::Error>> {
        unreachable!("This method should not be called");
    }
}
