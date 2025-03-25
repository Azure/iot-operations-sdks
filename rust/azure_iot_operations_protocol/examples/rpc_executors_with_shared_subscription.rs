// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

//! This example demonstrates how to use shared subscriptions to run multiple command executors
//! for the same command on different clients. The example creates two clients, each running a
//! command executor for the "increment" command. The command executor increments a shared counter
//! for each incoming request.
//!
//! To test, run against the `simple_rpc_invoker_sample.rs` example.

use std::sync::{Arc, Mutex};
use std::time::Duration;

use env_logger::Builder;
use thiserror::Error;

use azure_iot_operations_mqtt::MqttConnectionSettingsBuilder;
use azure_iot_operations_mqtt::session::{
    Session, SessionExitHandle, SessionManagedClient, SessionOptionsBuilder,
};
use azure_iot_operations_protocol::application::{ApplicationContext, ApplicationContextBuilder};
use azure_iot_operations_protocol::common::payload_serialize::{
    DeserializationError, FormatIndicator, PayloadSerialize, SerializedPayload,
};
use azure_iot_operations_protocol::rpc_command;

const CLIENT_ID_1: &str = "aio_example_client_1";
const CLIENT_ID_2: &str = "aio_example_client_2";
const HOSTNAME: &str = "localhost";
const PORT: u16 = 1883;
const REQUEST_TOPIC_PATTERN: &str = "topic/for/request";
const SERVICE_GROUP_ID: &str = "example_service_group";

#[tokio::main(flavor = "current_thread")]
async fn main() -> Result<(), Box<dyn std::error::Error>> {
    Builder::new()
        .filter_level(log::LevelFilter::Info)
        .format_timestamp(None)
        .filter_module("rumqttc", log::LevelFilter::Warn)
        .init();

    // Create two Sessions and their exit handles
    let session1 = create_session(CLIENT_ID_1)?;
    let session2 = create_session(CLIENT_ID_2)?;
    let exit_handle1 = session1.create_exit_handle();
    let exit_handle2 = session2.create_exit_handle();

    // Create an Application Context
    let application_context = ApplicationContextBuilder::default().build()?;

    // Creating shared counter
    let counter = Arc::new(Mutex::new(0));

    // Create Executors
    let executor1 = create_increment_executor(
        application_context.clone(),
        session1.create_managed_client(),
    )?;
    let executor2 =
        create_increment_executor(application_context, session2.create_managed_client())?;

    // Run both Sessions and their Executor loops concurrently
    let counter_clone = counter.clone();
    let results = tokio::join!(
        async {
            // Run Executor loop
            let result = increment_executor_loop(executor1, CLIENT_ID_1, counter_clone).await;
            // Exit Session if done
            exit(exit_handle1, CLIENT_ID_1).await;
            result
        },
        async {
            // Run Executor loop
            let result = increment_executor_loop(executor2, CLIENT_ID_2, counter).await;
            // Exit Session if done
            exit(exit_handle2, CLIENT_ID_2).await;
            result
        },
        session1.run(),
        session2.run(),
    );

    // Report any failures
    results.0.map_err(|e| e as Box<dyn std::error::Error>)?;
    results.1.map_err(|e| e as Box<dyn std::error::Error>)?;
    results.2?;
    results.3?;
    Ok(())
}

fn create_session(client_id: &str) -> Result<Session, Box<dyn std::error::Error>> {
    let connection_settings = MqttConnectionSettingsBuilder::default()
        .client_id(client_id)
        .hostname(HOSTNAME)
        .tcp_port(PORT)
        .keep_alive(Duration::from_secs(5))
        .use_tls(false)
        .build()
        .unwrap();
    let session_options = SessionOptionsBuilder::default()
        .connection_settings(connection_settings)
        .build()
        .unwrap();
    Ok(Session::new(session_options)?)
}

fn create_increment_executor(
    application_context: ApplicationContext,
    managed_client: SessionManagedClient,
) -> Result<
    rpc_command::Executor<IncrRequestPayload, IncrResponsePayload, SessionManagedClient>,
    Box<dyn std::error::Error>,
> {
    let incr_executor_options = rpc_command::executor::OptionsBuilder::default()
        .request_topic_pattern(REQUEST_TOPIC_PATTERN)
        .service_group_id(SERVICE_GROUP_ID)
        .command_name("increment")
        .build()?;
    Ok(rpc_command::Executor::new(
        application_context,
        managed_client,
        incr_executor_options,
    )?)
}

async fn increment_executor_loop(
    mut executor: rpc_command::Executor<
        IncrRequestPayload,
        IncrResponsePayload,
        SessionManagedClient,
    >,
    executor_client_id: &str,
    counter: Arc<Mutex<i32>>,
) -> Result<(), Box<dyn std::error::Error + Send + Sync>> {
    // Increment the counter for each incoming request
    while let Some(recv_result) = executor.recv().await {
        let request = recv_result?;
        log::info!("{executor_client_id}: Received request");
        // Increment the counter
        let updated_counter = {
            let mut counter = counter.lock().unwrap();
            *counter += 1;
            log::info!("Counter incremented to: {counter}");
            *counter
        };
        // Create the response
        let response = IncrResponsePayload {
            counter_response: updated_counter,
        };
        let response = rpc_command::executor::ResponseBuilder::default()
            .payload(response)
            .unwrap()
            .build()
            .unwrap();
        // Send the response
        match request.complete(response).await {
            Ok(()) => {
                log::info!(
                    "{executor_client_id}: Responded to 'increment' request with counter value: {updated_counter}"
                );
            }
            Err(err) => {
                log::error!(
                    "{executor_client_id}: Error sending response to 'increment' command request: {err}"
                );
                return Err(err.into());
            }
        }
    }

    // Shut down if there are no more requests
    log::info!("{executor_client_id}: No more requests. Shutting down executor");
    executor.shutdown().await?;

    Ok(())
}

// Exit the Session
async fn exit(exit_handle: SessionExitHandle, client_id: &str) {
    log::info!("{client_id}: Exiting session");
    match exit_handle.try_exit().await {
        Ok(()) => log::info!("{client_id}: Session exited gracefully"),
        Err(e) => {
            log::error!("{client_id}: Graceful session exit failed: {e}");
            log::warn!("{client_id}: Forcing session exit");
            exit_handle.exit_force().await;
        }
    }
}

#[derive(Clone, Debug, Default)]
pub struct IncrRequestPayload {}

#[derive(Clone, Debug, Default)]
pub struct IncrResponsePayload {
    pub counter_response: i32,
}

#[derive(Debug, Error)]
pub enum IncrSerializerError {}

impl PayloadSerialize for IncrRequestPayload {
    type Error = IncrSerializerError;
    fn serialize(self) -> Result<SerializedPayload, IncrSerializerError> {
        // This is a request payload, executor does not need to serialize it
        unimplemented!()
    }
    fn deserialize(
        _payload: &[u8],
        _content_type: Option<&String>,
        _format_indicator: &FormatIndicator,
    ) -> Result<IncrRequestPayload, DeserializationError<IncrSerializerError>> {
        Ok(IncrRequestPayload {})
    }
}

impl PayloadSerialize for IncrResponsePayload {
    type Error = IncrSerializerError;

    fn serialize(self) -> Result<SerializedPayload, IncrSerializerError> {
        let payload = format!("{{\"CounterResponse\":{}}}", self.counter_response);
        Ok(SerializedPayload {
            payload: payload.into_bytes(),
            content_type: "application/json".to_string(),
            format_indicator: FormatIndicator::Utf8EncodedCharacterData,
        })
    }

    fn deserialize(
        _payload: &[u8],
        _content_type: Option<&String>,
        _format_indicator: &FormatIndicator,
    ) -> Result<IncrResponsePayload, DeserializationError<IncrSerializerError>> {
        // This is a response payload, executor does not need to deserialize it
        unimplemented!()
    }
}
