// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

use std::collections::HashMap;
use std::time::Duration;

use azure_iot_operations_mqtt::session::{
    Session, SessionExitHandle, SessionManagedClient, SessionOptionsBuilder,
};
use azure_iot_operations_mqtt::MqttConnectionSettingsBuilder;
use complex_envoy::common_types::common_options::{CommandOptionsBuilder, TelemetryOptionsBuilder};
use complex_envoy::dtmi_example_Complex__1::client::{
    Enum_Test_Result__1, Object_GetTemperatures_Response_ElementSchema,
};
use complex_envoy::dtmi_example_Complex__1::service::{
    GetTemperaturesCommandExecutor, GetTemperaturesResponseBuilder, GetTemperaturesResponsePayload,
    TelemetryCollectionBuilder, TelemetryCollectionMessageBuilder, TelemetryCollectionSender,
};

#[tokio::main(flavor = "current_thread")]
async fn main() {
    // Initialize the logger
    env_logger::Builder::new()
        .filter_level(log::LevelFilter::max())
        .format_timestamp(None)
        .filter_module("rumqttc", log::LevelFilter::Warn)
        .init();

    // Create a session with connection settings from the environment
    let connection_settings = MqttConnectionSettingsBuilder::from_environment()
        .build()
        .unwrap();
    let session_options = SessionOptionsBuilder::default()
        .connection_settings(connection_settings)
        .build()
        .unwrap();
    let mut session = Session::new(session_options).unwrap();

    // Spawn tasks for handling commands and telemetry
    tokio::spawn(get_temperatures_and_publish(
        session.create_managed_client(),
    ));
    tokio::spawn(exit_timer(
        session.create_exit_handle(),
        Duration::from_secs(120),
    ));

    // Run the session
    session.run().await.unwrap();
}

// Task to handle get temperatures command and publish telemetry
async fn get_temperatures_and_publish(client: SessionManagedClient) {
    // Create command executor
    let options = CommandOptionsBuilder::default().build().unwrap();
    let mut get_temperatures_executor =
        GetTemperaturesCommandExecutor::new(client.clone(), &options);

    // Create telemetry sender
    let temperature_map_sender = TelemetryCollectionSender::new(
        client,
        &TelemetryOptionsBuilder::default().build().unwrap(),
    );

    // Sample temperature data
    let temperatures = vec![
        Object_GetTemperatures_Response_ElementSchema {
            temperature: Some(70.0),
            city: Some("Seattle".to_string()),
            result: Some(Enum_Test_Result__1::Success),
        },
        Object_GetTemperatures_Response_ElementSchema {
            temperature: Some(72.0),
            city: Some("Portland".to_string()),
            result: Some(Enum_Test_Result__1::Failure),
        },
        Object_GetTemperatures_Response_ElementSchema {
            temperature: Some(74.0),
            city: Some("San Francisco".to_string()),
            result: Some(Enum_Test_Result__1::Success),
        },
    ];

    // Sample telemetry data
    let telemetry_map = Some(HashMap::from([
        ("Seattle".to_string(), 70.0),
        ("Portland".to_string(), 72.0),
        ("San Francisco".to_string(), 74.0),
    ]));

    // Loop to handle incoming get temperature requests and publish telemetry
    loop {
        // Receive and handle get temperatures command
        let request = get_temperatures_executor.recv().await.unwrap().unwrap();

        let response_payload = GetTemperaturesResponsePayload {
            temperatures: temperatures.clone(),
        };

        let response = GetTemperaturesResponseBuilder::default()
            .payload(response_payload)
            .unwrap()
            .build()
            .unwrap();
        request.complete(response).unwrap();

        // Build and send telemetry message
        let telemetry_message = TelemetryCollectionMessageBuilder::default()
            .payload(
                TelemetryCollectionBuilder::default()
                    .temperatures(telemetry_map.clone())
                    .build()
                    .unwrap(),
            )
            .unwrap()
            .build()
            .unwrap();

        temperature_map_sender
            .send(telemetry_message)
            .await
            .unwrap();
    }
}

// Task to exit the session after a specified delay
async fn exit_timer(exit_handle: SessionExitHandle, exit_after: Duration) {
    tokio::time::sleep(exit_after).await;
    exit_handle.try_exit().await.unwrap();
}
