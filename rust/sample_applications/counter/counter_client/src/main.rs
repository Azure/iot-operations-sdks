// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

use std::{env, time::Duration};

use azure_iot_operations_mqtt::session::{
    Session, SessionExitHandle, SessionManagedClient, SessionOptionsBuilder,
};
use azure_iot_operations_mqtt::MqttConnectionSettingsBuilder;
use envoy::common_types::common_options::CommonOptionsBuilder;
use envoy::dtmi_com_example_Counter__1::client::{
    IncrementCommandInvoker, IncrementRequestBuilder, ReadCounterCommandInvoker,
    ReadCounterRequestBuilder,
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

    // Use the managed client to run command invocations in another task
    tokio::task::spawn(increment_and_check(
        session.create_managed_client(),
        session.create_exit_handle(),
    ));

    // Run the session
    session.run().await.unwrap();
}

/// Send a read request, 15 increment requests, and another read request and wait for their responses, then disconnect
async fn increment_and_check(client: SessionManagedClient, exit_handle: SessionExitHandle) {
    // Create invokers
    let options = CommonOptionsBuilder::default().build().unwrap();
    let increment_invoker = IncrementCommandInvoker::new(client.clone(), &options);
    let read_counter_invoker = ReadCounterCommandInvoker::new(client, &options);

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

    // Exit the session now that we're done
    exit_handle.try_exit().await.unwrap();
}
