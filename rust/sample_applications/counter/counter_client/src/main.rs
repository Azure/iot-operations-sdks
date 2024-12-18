// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

use core::panic;
use std::{env, time::Duration};

use azure_iot_operations_mqtt::session::{
    Session, SessionExitHandle, SessionManagedClient, SessionOptionsBuilder,
};
use azure_iot_operations_mqtt::MqttConnectionSettingsBuilder;
use envoy::common_types::common_options::{CommandOptionsBuilder, TelemetryOptionsBuilder};
use envoy::dtmi_com_example_Counter__1::client::{
    IncrementCommandInvoker, IncrementRequestBuilder, IncrementRequestPayloadBuilder,
    ReadCounterCommandInvoker, ReadCounterRequestBuilder, TelemetryCollectionReceiver,
};

use tokio::sync::oneshot;
use tokio::time::sleep;

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

    let (telemetry_done_tx, telemetry_done_rx) = oneshot::channel::<()>();

    // Use the managed client to run telemetry checks in another task
    tokio::task::spawn(counter_telemetry_check(
        session.create_managed_client(),
        telemetry_done_tx,
    ));

    // Use the managed client to run command invocations in another task
    tokio::task::spawn(increment_and_check(
        session.create_managed_client(),
        session.create_exit_handle(),
        telemetry_done_rx,
    ));

    // Run the session
    session.run().await.unwrap();
}

/// Wait for the associated telemetry
async fn counter_telemetry_check(
    client: SessionManagedClient,
    telemetry_done_tx: oneshot::Sender<()>,
) {
    // Create receiver
    let mut counter_value_receiver = TelemetryCollectionReceiver::new(
        client,
        &TelemetryOptionsBuilder::default()
            .auto_ack(false)
            .build()
            .unwrap(),
    );

    log::info!("Waiting for associated telemetry");
    let mut telemetry_count = 0;

    loop {
        tokio::select! {
            telemetry_res = counter_value_receiver.recv() => {
                let (message, ack_token) = telemetry_res.unwrap().unwrap();

                log::info!("Telemetry reported counter value: {:?}", message.payload);

                // Acknowledge the message
                if let Some(ack_token) = ack_token {
                    ack_token.ack();
                }

                telemetry_count += 1;
            },
            () = sleep(Duration::from_secs(5))=> {
                if telemetry_count >= 14 {
                    telemetry_done_tx.send(()).unwrap();
                    break;
                }
                panic!("Telemetry not finished");
            }
        }
    }

    counter_value_receiver.shutdown().await.unwrap();
}

/// Send a read request, 15 increment requests, and another read request and wait for their responses. Then exit the session.
async fn increment_and_check(
    client: SessionManagedClient,
    exit_handle: SessionExitHandle,
    telemetry_done_rx: oneshot::Receiver<()>,
) {
    // Create invokers
    let options = CommandOptionsBuilder::default().build().unwrap();
    let increment_invoker = IncrementCommandInvoker::new(client.clone(), &options);
    let read_counter_invoker = ReadCounterCommandInvoker::new(client.clone(), &options);

    // Get the target executor ID from the environment
    let target_executor_id = env::var("COUNTER_SERVER_ID").unwrap();

    // Initial counter read from the server
    log::info!("Calling readCounter");
    let read_counter_request = ReadCounterRequestBuilder::default()
        .timeout(Duration::from_secs(10))
        .executor_id(target_executor_id.clone())
        .build()
        .unwrap();
    let read_counter_response = read_counter_invoker
        .invoke(read_counter_request)
        .await
        .unwrap();
    log::info!(
        "Counter value: {:?}",
        read_counter_response.payload.counter_response
    );

    // Increment the counter 15 times on the server
    for _ in 1..15 {
        log::info!("Calling increment");
        let increment_request = IncrementRequestBuilder::default()
            .timeout(Duration::from_secs(10))
            .executor_id(target_executor_id.clone())
            .payload(
                &IncrementRequestPayloadBuilder::default()
                    .increment_value(1)
                    .build()
                    .unwrap(),
            )
            .unwrap()
            .build()
            .unwrap();
        let increment_response = increment_invoker.invoke(increment_request).await.unwrap();
        log::info!(
            "Counter value after increment:: {:?}",
            increment_response.payload.counter_response
        );
    }

    // Final counter read from the server
    log::info!("Calling readCounter");
    let read_counter_request = ReadCounterRequestBuilder::default()
        .timeout(Duration::from_secs(10))
        .executor_id(target_executor_id)
        .build()
        .unwrap();
    let read_counter_response = read_counter_invoker
        .invoke(read_counter_request)
        .await
        .unwrap();
    log::info!(
        "Counter value: {:?}",
        read_counter_response.payload.counter_response
    );

    log::info!("Waiting for associated telemetry");
    telemetry_done_rx.await.unwrap();
    log::info!("Telemetry done");

    read_counter_invoker.shutdown().await.unwrap();
    increment_invoker.shutdown().await.unwrap();

    // Exit the session now that we're done
    exit_handle.try_exit().await.unwrap();
}
