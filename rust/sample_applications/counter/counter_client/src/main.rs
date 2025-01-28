// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

use core::panic;
use std::{env, time::Duration};

use azure_iot_operations_mqtt::session::{
    Session, SessionExitHandle, SessionManagedClient, SessionOptionsBuilder,
};
use azure_iot_operations_mqtt::MqttConnectionSettingsBuilder;
use azure_iot_operations_protocol::application::{
    ApplicationContext, ApplicationContextOptionsBuilder,
};
use envoy::common_types::common_options::{CommandOptionsBuilder, TelemetryOptionsBuilder};
use envoy::dtmi_com_example_Counter__1::client::{
    IncrementCommandInvoker, IncrementRequestBuilder, IncrementRequestPayloadBuilder,
    ReadCounterCommandInvoker, ReadCounterRequestBuilder, TelemetryCollectionReceiver,
};

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

    let application_context =
        ApplicationContext::new(ApplicationContextOptionsBuilder::default().build().unwrap());

    // Use the managed client to run telemetry checks in another task
    let counter_telemetry_check_handle = tokio::task::spawn(counter_telemetry_check(
        application_context.clone(),
        session.create_managed_client(),
        session.create_exit_handle(),
    ));

    // Use the managed client to run command invocations in another task
    let increment_and_check_handle = tokio::task::spawn(increment_and_check(
        application_context,
        session.create_managed_client(),
    ));

    // Wait for all tasks to finish and run the session, if any of the tasks fail, the program will panic
    assert!(tokio::try_join!(
        async move {
            counter_telemetry_check_handle
                .await
                .map_err(|e| e.to_string())
        },
        async move { increment_and_check_handle.await.map_err(|e| e.to_string()) },
        async move { session.run().await.map_err(|e| { e.to_string() }) }
    )
    .is_ok());
}

/// Wait for the associated telemetry. Then exit the session.
async fn counter_telemetry_check(
    application_context: ApplicationContext,
    client: SessionManagedClient,
    exit_handle: SessionExitHandle,
) {
    // Create receiver
    let mut counter_value_receiver = TelemetryCollectionReceiver::new(
        application_context,
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
                    ack_token.ack().await.unwrap();
                }

                telemetry_count += 1;
            },
            () = sleep(Duration::from_secs(5))=> {
                if telemetry_count >= 15 {
                    break;
                }
                panic!("Telemetry not finished");
            }
        }
    }

    log::info!("Telemetry finished");
    counter_value_receiver.shutdown().await.unwrap();

    // Exit the session now that we're done
    match exit_handle.try_exit().await {
        Ok(()) => { /* Successfully exited */ }
        Err(e) => {
            if let azure_iot_operations_mqtt::session::SessionExitError::BrokerUnavailable {
                attempted,
            } = e
            {
                // Because of a current race condition, we need to ignore this as it isn't indicative of a real error
                assert!(attempted, "{}", e.to_string());
            } else {
                panic!("{}", e.to_string())
            }
        }
    }
}

/// Send a read request, 15 increment requests, and another read request and wait for their responses.
async fn increment_and_check(
    application_context: ApplicationContext,
    client: SessionManagedClient,
) {
    // Create invokers
    let options = CommandOptionsBuilder::default().build().unwrap();
    let increment_invoker =
        IncrementCommandInvoker::new(application_context.clone(), client.clone(), &options);
    let read_counter_invoker =
        ReadCounterCommandInvoker::new(application_context, client, &options);

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
    for _ in 0..15 {
        log::info!("Calling increment");
        let increment_request = IncrementRequestBuilder::default()
            .timeout(Duration::from_secs(10))
            .executor_id(target_executor_id.clone())
            .payload(
                IncrementRequestPayloadBuilder::default()
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

    read_counter_invoker.shutdown().await.unwrap();
    increment_invoker.shutdown().await.unwrap();
}
