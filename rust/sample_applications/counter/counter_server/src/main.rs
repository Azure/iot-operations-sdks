// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

use std::sync::{Arc, Mutex};
use std::time::Duration;

use azure_iot_operations_mqtt::session::{
    Session, SessionExitHandle, SessionManagedClient, SessionOptionsBuilder,
};
use azure_iot_operations_mqtt::MqttConnectionSettingsBuilder;
use azure_iot_operations_protocol::application::{ApplicationContext, ApplicationContextBuilder};
use envoy::common_types::common_options::{CommandOptionsBuilder, TelemetryOptionsBuilder};
use envoy::counter::service::{
    IncrementCommandExecutor, IncrementResponseBuilder, IncrementResponsePayload,
    ReadCounterCommandExecutor, ReadCounterResponseBuilder, ReadCounterResponsePayload,
    TelemetryCollectionBuilder, TelemetryMessageBuilder, TelemetrySender,
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
        .unwrap()
        .build()
        .unwrap();
    let session_options = SessionOptionsBuilder::default()
        .connection_settings(connection_settings)
        .build()
        .unwrap();
    let session = Session::new(session_options).unwrap();

    let application_context = ApplicationContextBuilder::default().build().unwrap();

    // The counter value for the server
    let counter = Arc::new(Mutex::new(0));

    // Spawn tasks for the server features
    tokio::spawn(read_counter_executor(
        application_context.clone(),
        session.create_managed_client(),
        counter.clone(),
    ));
    tokio::spawn(increment_counter_and_publish(
        application_context,
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
async fn read_counter_executor(
    application_context: ApplicationContext,
    client: SessionManagedClient,
    counter: Arc<Mutex<i32>>,
) {
    // Create executor
    let options = CommandOptionsBuilder::default().build().unwrap();
    let mut read_counter_executor =
        ReadCounterCommandExecutor::new(application_context, client, &options);

    // Respond to each read request with the current counter value
    loop {
        let request = read_counter_executor.recv().await.unwrap().unwrap();
        let response_payload = ReadCounterResponsePayload {
            counter_response: *counter.lock().unwrap(),
        };
        let response = ReadCounterResponseBuilder::default()
            .payload(response_payload)
            .unwrap()
            .build()
            .unwrap();
        request.complete(response).await.unwrap();
    }
}

/// Run an executor that responds to requests to increment the counter value and a sender that sends
/// telemetry messages with the new counter value.
async fn increment_counter_and_publish(
    application_context: ApplicationContext,
    client: SessionManagedClient,
    counter: Arc<Mutex<i32>>,
) {
    // Create executor
    let options = CommandOptionsBuilder::default().build().unwrap();
    let mut increment_executor =
        IncrementCommandExecutor::new(application_context.clone(), client.clone(), &options);

    // Create sender
    let counter_sender = TelemetrySender::new(
        application_context,
        client,
        &TelemetryOptionsBuilder::default().build().unwrap(),
    );

    // Respond to each increment request by incrementing the counter value and responding with the new value
    loop {
        let request = increment_executor.recv().await.unwrap().unwrap();

        let updated_counter = {
            // Increment
            let mut counter_guard = counter.lock().unwrap();
            *counter_guard += request.payload.increment_value;
            *counter_guard
        };

        // Create telemetry message using the new counter value
        let telemetry_message = TelemetryMessageBuilder::default()
            .payload(
                TelemetryCollectionBuilder::default()
                    .counter_value(Some(updated_counter))
                    .build()
                    .unwrap(),
            )
            .unwrap()
            .build()
            .unwrap();

        // Send associated telemetry
        counter_sender.send(telemetry_message).await.unwrap();

        // Respond
        let response_payload = IncrementResponsePayload {
            counter_response: updated_counter,
        };

        // Respond to the increment request
        let response = IncrementResponseBuilder::default()
            .payload(response_payload)
            .unwrap()
            .build()
            .unwrap();
        request.complete(response).await.unwrap();
    }
}

/// Exit the session after a delay.
async fn exit_timer(exit_handle: SessionExitHandle, exit_after: Duration) {
    tokio::time::sleep(exit_after).await;
    exit_handle.try_exit().await.unwrap();
}
