// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

use std::sync::{Arc, Mutex};
use std::time::Duration;

use azure_iot_operations_mqtt::session::{
    Session, SessionExitHandle, SessionManagedClient, SessionOptionsBuilder,
};
use azure_iot_operations_mqtt::MqttConnectionSettingsBuilder;
use envoy::common_types::common_options::CommonOptionsBuilder;
use envoy::dtmi_com_example_Counter__1::service::{
    IncrementCommandExecutor, IncrementResponseBuilder, IncrementResponsePayload,
    ReadCounterCommandExecutor, ReadCounterResponseBuilder, ReadCounterResponsePayload,
};

#[tokio::main(flavor = "current_thread")]
async fn main() {
    env_logger::Builder::new()
        .filter_level(log::LevelFilter::max())
        .format_timestamp(None)
        .filter_module("rumqttc", log::LevelFilter::Warn)
        .init();

    // Create a session
    let connection_settings = MqttConnectionSettingsBuilder::from_environment()
        .build()
        .unwrap();
    let session_options = SessionOptionsBuilder::default()
        .connection_settings(connection_settings)
        .build()
        .unwrap();
    let mut session = Session::new(session_options).unwrap();

    // The counter value for the server
    let counter = Arc::new(Mutex::new(0));

    // Spawn tasks for the server features
    tokio::spawn(read_counter_executor(
        session.create_managed_client(),
        counter.clone(),
    ));
    tokio::spawn(increment_executor(
        session.create_managed_client(),
        counter.clone(),
    ));
    tokio::spawn(exit_timer(
        session.create_exit_handle(),
        Duration::from_secs(120),
    ));

    // Run the session
    session.run().await.unwrap();
}

/// Run an executor that responds to requests to read the counter value.
async fn read_counter_executor(client: SessionManagedClient, counter: Arc<Mutex<i32>>) {
    // Create executor
    let options = CommonOptionsBuilder::default().build().unwrap();
    let mut read_counter_executor = ReadCounterCommandExecutor::new(client, &options);

    // Respond to each read request with the current counter value
    loop {
        let request = read_counter_executor.recv().await.unwrap();
        let response_payload = ReadCounterResponsePayload {
            counter_response: *counter.lock().unwrap(),
        };
        let response = ReadCounterResponseBuilder::default()
            .payload(&response_payload)
            .unwrap()
            .build()
            .unwrap();
        request.complete(response).unwrap();
    }
}

/// Run an executor that responds to requests to increment the counter value.
async fn increment_executor(client: SessionManagedClient, counter: Arc<Mutex<i32>>) {
    // Create executor
    let options = CommonOptionsBuilder::default().build().unwrap();
    let mut increment_executor = IncrementCommandExecutor::new(client, &options);

    // Respond to each increment request by incrementing the counter value and responding with the new value
    loop {
        let request = increment_executor.recv().await.unwrap();
        // Increment
        let mut counter_guard = counter.lock().unwrap();
        *counter_guard += 1;
        // Respond
        let response_payload = IncrementResponsePayload {
            counter_response: *counter_guard,
        };
        let response = IncrementResponseBuilder::default()
            .payload(&response_payload)
            .unwrap()
            .build()
            .unwrap();
        request.complete(response).unwrap();
    }
}

/// Exit the session after a delay.
async fn exit_timer(exit_handle: SessionExitHandle, exit_after: Duration) {
    tokio::time::sleep(exit_after).await;
    exit_handle.try_exit().await.unwrap();
}
