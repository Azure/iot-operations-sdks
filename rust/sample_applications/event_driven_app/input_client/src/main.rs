// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

//! This sample application demonstrates how to create an event-driven application that receives
//! incoming sensor data and stores it in a state store.

use std::{
    sync::{Arc, Mutex},
    time::Duration,
};

use azure_iot_operations_mqtt::{
    session::{Session, SessionConnectionMonitor, SessionManagedClient, SessionOptionsBuilder},
    MqttConnectionSettingsBuilder,
};
use azure_iot_operations_protocol::{
    application::{ApplicationContext, ApplicationContextBuilder},
    common::payload_serialize::{
        DeserializationError, FormatIndicator, PayloadSerialize, SerializedPayload,
    },
    telemetry::telemetry_receiver::{TelemetryReceiver, TelemetryReceiverOptionsBuilder},
};
use azure_iot_operations_services::state_store::{self, SetOptions};
use chrono::{DateTime, Utc};
use serde::{Deserialize, Serialize};
use tokio::sync::Notify;

const STATE_STORE_SENSOR_KEY: &str = "event_app_sample";
const WINDOW_SIZE: i64 = 60;
const DEFAULT_STATE_STORE_OPERATION_TIMEOUT: Duration = Duration::from_secs(10);
const SENSOR_DATA_TOPIC: &str = "sensor/data";

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

    // Create a shared vector to store incoming sensor data
    let incoming_sensor_data = Arc::new(Mutex::new(Vec::new()));
    let incoming_sensor_data_notify = Arc::new(Notify::new());

    // Spawn a task to receive telemetry from a sensor
    let receive_telemetry_handle = tokio::task::spawn(receive_telemetry(
        application_context.clone(),
        session.create_managed_client(),
        incoming_sensor_data.clone(),
        incoming_sensor_data_notify.clone(),
    ));

    // Spawn a task to collect sensor data and store it in the state store
    let process_sensor_data_handle = tokio::task::spawn(process_sensor_data(
        application_context.clone(),
        session.create_managed_client(),
        session.create_connection_monitor(),
        incoming_sensor_data.clone(),
        incoming_sensor_data_notify.clone(),
    ));

    assert!(tokio::try_join!(
        async move { session.run().await.map_err(|e| { e.to_string() }) },
        async move { receive_telemetry_handle.await.map_err(|e| e.to_string()) },
        async move { process_sensor_data_handle.await.map_err(|e| e.to_string()) }
    )
    .is_ok());
}

async fn receive_telemetry(
    application_context: ApplicationContext,
    client: SessionManagedClient,
    incoming_sensor_data: Arc<Mutex<Vec<SensorData>>>,
    incoming_sensor_data_notify: Arc<Notify>,
) {
    let receiver_options = TelemetryReceiverOptionsBuilder::default()
        .topic_pattern(SENSOR_DATA_TOPIC.to_string())
        .build()
        .expect("Telemetry receiver options should not fail");

    let mut telemetry_receiver: TelemetryReceiver<SensorData, _> =
        TelemetryReceiver::new(application_context, client, receiver_options)
            .expect("Telemetry receiver creation should not fail");

    // Start the telemetry receiver
    while let Some(message) = telemetry_receiver.recv().await {
        match message {
            Ok((message, _ack_token)) => {
                incoming_sensor_data
                    .lock()
                    .expect("incoming_sensor_data lock is handled properly")
                    .push(message.payload);
                incoming_sensor_data_notify.notify_one();
            }
            Err(e) => {
                log::error!("Failed to receive telemetry: {:?}", e);
            }
        }
        log::info!("Received sensor data");
    }
}

async fn process_sensor_data(
    application_context: ApplicationContext,
    client: SessionManagedClient,
    connection_monitor: SessionConnectionMonitor,
    incoming_sensor_data: Arc<Mutex<Vec<SensorData>>>,
    incoming_sensor_data_notify: Arc<Notify>,
) {
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
        // Wait for incoming sensor data
        incoming_sensor_data_notify.notified().await;

        // Fetch historical sensor data from the state store
        let get_result = state_store_client
            .get(
                STATE_STORE_SENSOR_KEY.into(),
                DEFAULT_STATE_STORE_OPERATION_TIMEOUT,
            )
            .await;
        match get_result {
            Ok(get_response) => {
                // Deserialize the historical sensor data
                let mut sensor_data: Vec<SensorData> = match get_response.response {
                    Some(serialized_data) => match serde_json::from_slice(&serialized_data) {
                        Ok(sensor_data) => sensor_data,
                        Err(e) => {
                            // If we can't deserialize the data, delete the key
                            log::error!(
                                "Unable to deserialize state store data, deleting the key: {e:?}"
                            );
                            match state_store_client
                                .del(
                                    STATE_STORE_SENSOR_KEY.into(),
                                    None,
                                    DEFAULT_STATE_STORE_OPERATION_TIMEOUT,
                                )
                                .await
                            {
                                Ok(_) => { /* Success */ }
                                Err(e) => {
                                    log::error!("Failed to delete state store data: {e:?}");
                                }
                            }
                            continue;
                        }
                    },
                    None => Vec::new(), // No data in the state store
                };

                // Drain the incoming sensor data and append it to the historical sensor data
                incoming_sensor_data
                    .lock()
                    .expect("incoming_sensor_data lock is handled properly")
                    .drain(..)
                    .for_each(|d| {
                        sensor_data.push(d);
                    });

                // Discard old data
                sensor_data
                    .retain(|d| Utc::now() - d.timestamp < chrono::Duration::seconds(WINDOW_SIZE));

                // Push the sensor data back to the state store
                match state_store_client
                    .set(
                        STATE_STORE_SENSOR_KEY.into(),
                        serde_json::to_vec(&sensor_data)
                            .expect("sensor_data was previously deserialized"),
                        DEFAULT_STATE_STORE_OPERATION_TIMEOUT,
                        None,
                        SetOptions::default(),
                    )
                    .await
                {
                    Ok(_) => { /* Success */ }
                    Err(e) => log::error!(
                        "Failed to set state store data, incoming sensor data lost: {e:?}"
                    ),
                }
            }
            Err(e) => {
                log::error!("Failed to fetch state store data: {e:?}");
            }
        };
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
