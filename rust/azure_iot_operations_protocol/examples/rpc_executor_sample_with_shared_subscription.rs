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

use azure_iot_operations_mqtt::session::{Session, SessionManagedClient, SessionOptionsBuilder};
use azure_iot_operations_mqtt::MqttConnectionSettingsBuilder;
use azure_iot_operations_protocol::application::{ApplicationContext, ApplicationContextBuilder};
use azure_iot_operations_protocol::common::payload_serialize::{
    DeserializationError, FormatIndicator, PayloadSerialize, SerializedPayload,
};
use azure_iot_operations_protocol::rpc_command;

const EXECUTOR_CLIENT_ID_1: &str = "aio_example_executor_client_1";
const EXECUTOR_CLIENT_ID_2: &str = "aio_example_executor_client_2";
const HOSTNAME: &str = "localhost";
const PORT: u16 = 4883;
const REQUEST_TOPIC_PATTERN: &str = "topic/for/request";
const SERVICE_GROUP_ID: &str = "example_service_group";

#[tokio::main(flavor = "current_thread")]
async fn main() {
    Builder::new()
        .filter_level(log::LevelFilter::Warn)
        .format_timestamp(None)
        .filter_module("rumqttc", log::LevelFilter::Warn)
        .init();

    // Create a session for client 1
    let connection_settings_client_1 = MqttConnectionSettingsBuilder::default()
        .client_id(EXECUTOR_CLIENT_ID_1)
        .hostname(HOSTNAME)
        .tcp_port(PORT)
        .keep_alive(Duration::from_secs(5))
        .use_tls(false)
        .build()
        .unwrap();

    let session_options_client_1 = SessionOptionsBuilder::default()
        .connection_settings(connection_settings_client_1)
        .build()
        .unwrap();
    let session_client_1 = Session::new(session_options_client_1).unwrap();
    let application_context_client_1 = ApplicationContextBuilder::default().build().unwrap();

    // Create a session for client 2
    let connection_settings_client_2 = MqttConnectionSettingsBuilder::default()
        .client_id(EXECUTOR_CLIENT_ID_2)
        .hostname(HOSTNAME)
        .tcp_port(PORT)
        .keep_alive(Duration::from_secs(5))
        .use_tls(false)
        .build()
        .unwrap();

    let session_options_client_2 = SessionOptionsBuilder::default()
        .connection_settings(connection_settings_client_2)
        .build()
        .unwrap();
    let session_client_2 = Session::new(session_options_client_2).unwrap();
    let application_context_client_2 = ApplicationContextBuilder::default().build().unwrap();

    // Creating shared counter
    let counter = Arc::new(Mutex::new(0));

    // Use the managed client to run command executor 1 in another task
    tokio::task::spawn(executor_loop(
        application_context_client_1,
        session_client_1.create_managed_client(),
        EXECUTOR_CLIENT_ID_1,
        counter.clone(),
    ));

    // Use the managed client to run command executor 2 in another task
    tokio::task::spawn(executor_loop(
        application_context_client_2,
        session_client_2.create_managed_client(),
        EXECUTOR_CLIENT_ID_2,
        counter,
    ));

    // Run the sessions
    assert!(tokio::try_join!(
        async move { session_client_1.run().await.map_err(|e| { e.to_string() }) },
        async move { session_client_2.run().await.map_err(|e| { e.to_string() }) }
    )
    .is_ok());
}

/// Handle incoming increment command requests
async fn executor_loop(
    application_context: ApplicationContext,
    client: SessionManagedClient,
    executor_client_id: &str,
    counter: Arc<Mutex<i32>>,
) {
    println!("{executor_client_id}: Starting executor loop");
    // Create a command executor for the increment command
    let incr_executor_options = rpc_command::executor::OptionsBuilder::default()
        .request_topic_pattern(REQUEST_TOPIC_PATTERN)
        .service_group_id(SERVICE_GROUP_ID)
        .command_name("increment")
        .build()
        .unwrap();
    let mut incr_executor: rpc_command::Executor<IncrRequestPayload, IncrResponsePayload, _> =
        rpc_command::Executor::new(application_context, client, incr_executor_options).unwrap();

    // Increment the counter for each incoming request
    while let Some(request) = incr_executor.recv().await {
        match request {
            Ok(request) => {
                println!("{executor_client_id}: Received request");
                let updated_counter = {
                    let mut counter = counter.lock().unwrap();
                    *counter += 1;
                    *counter
                };
                let response = IncrResponsePayload {
                    counter_response: updated_counter,
                };
                let response = rpc_command::executor::ResponseBuilder::default()
                    .payload(response)
                    .unwrap()
                    .build()
                    .unwrap();
                request.complete(response).await.unwrap();
                println!(
                    "{executor_client_id}: Completed request with counter value: {updated_counter}"
                );
            }
            Err(err) => {
                println!("{executor_client_id}: Error receiving request: {err}");
                return;
            }
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
        _content_type: &Option<String>,
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
        _content_type: &Option<String>,
        _format_indicator: &FormatIndicator,
    ) -> Result<IncrResponsePayload, DeserializationError<IncrSerializerError>> {
        // This is a response payload, executor does not need to deserialize it
        unimplemented!()
    }
}
