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
    telemetry::telemetry_receiver::{TelemetryReceiver, TelemetryReceiverOptionsBuilder},
};
use azure_iot_operations_services::state_store::{self, SetOptions};
use chrono::{DateTime, Utc};
use serde::{Deserialize, Serialize};
use tokio::io::join;

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
            .client_id("EventDrivenApp-input")
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

    let incoming_sensor_data = Arc::new(Mutex::new(Vec::new()));

    let receive_telemetry_handle = tokio::task::spawn(receive_telemetry(
        application_context.clone(),
        session.create_managed_client(),
        incoming_sensor_data.clone(),
        session.create_exit_handle(),
    ));

    let process_sensor_data_handle = tokio::task::spawn(process_sensor_data(
        application_context.clone(),
        session.create_managed_client(),
        session.create_connection_monitor(),
        incoming_sensor_data.clone(),
        session.create_exit_handle(),
    )); // ASK: Do we even need exit handles?

    // FIN: log::info!("Connecting to: {:?}", connection_settings);
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
    incoming_sensor_data: Arc<Mutex<Vec<SensorData>>>, // FIN: Choose where to put this
    exit_handle: SessionExitHandle,                    // FIN: Do we need this here?
) {
    let receiver_options = TelemetryReceiverOptionsBuilder::default()
        .topic_pattern("sensor/data".to_string())
        .build()
        .unwrap();

    let mut telemetry_receiver: TelemetryReceiver<SensorData, _> =
        TelemetryReceiver::new(application_context, client, receiver_options).unwrap();

    // Start the telemetry receiver
    // FIN: probably a way to simplify this while loop
    while let Some(message) = telemetry_receiver.recv().await {
        match message {
            Ok((message, _ack_token)) => {
                incoming_sensor_data.lock().unwrap().push(message.payload);
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
    exit_handle: SessionExitHandle,
) {
    //  ASK: Are state store clients supposed to get torn down and reused?
    let state_store_client = state_store::Client::new(
        application_context,
        client,
        connection_monitor,
        state_store::ClientOptionsBuilder::default()
            .build()
            .unwrap(),
    )
    .unwrap();

    // Fetch historical sensor data from the state store
    loop {
        let response = state_store_client
            .get(STATE_STORE_SENSOR_KEY.into(), Duration::from_secs(10))
            .await;
        match response {
            Ok(data) => {
                let mut data: Vec<SensorData> = match data.response {
                    Some(data) => match serde_json::from_slice(&data) {
                        Ok(data) => data,
                        Err(e) => {
                            log::error!(
                                "Unable to deserialize state store data, deleting the key: {e:?}"
                            );
                            state_store_client
                                .del(STATE_STORE_SENSOR_KEY.into(), None, Duration::from_secs(10))
                                .await
                                .unwrap();
                            continue;
                        }
                    },
                    None => Vec::new(),
                };

                // Drain the incoming queue
                incoming_sensor_data
                    .lock()
                    .unwrap()
                    .drain(..)
                    .for_each(|d| {
                        data.push(d);
                    });

                // Discard old data
                data.retain(|d| Utc::now() - d.timestamp < chrono::Duration::seconds(WINDOW_SIZE));

                // Push the sensor data back to the state store
                let data = serde_json::to_vec(&data).unwrap();

                // ASK: If the set operation fails what do we do?
                match state_store_client
                    .set(
                        STATE_STORE_SENSOR_KEY.into(),
                        serde_json::to_vec(&data).unwrap(),
                        Duration::from_secs(10),
                        None,
                        SetOptions::default(),
                    )
                    .await
                {
                    Ok(_) => { /* Success */ }
                    Err(e) => log::error!("Failed to set state store data: {e:?}"), // Incoming sensor data is lost
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
        todo!()
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
