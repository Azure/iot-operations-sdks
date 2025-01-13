// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
use std::time::Duration;

use env_logger::Builder;
use thiserror::Error;

use azure_iot_operations_mqtt::session::{Session, SessionManagedClient, SessionOptionsBuilder};
use azure_iot_operations_mqtt::MqttConnectionSettingsBuilder;
use azure_iot_operations_protocol::common::payload_serialize::{FormatIndicator, PayloadSerialize};
use azure_iot_operations_protocol::rpc::command_executor::{
    CommandExecutor, CommandExecutorOptionsBuilder, CommandResponseBuilder,
};

const CLIENT_ID: &str = "aio_example_executor_client";
const HOSTNAME: &str = "localhost";
const PORT: u16 = 1883;
const REQUEST_TOPIC_PATTERN: &str = "topic/for/request";

#[tokio::main(flavor = "current_thread")]
async fn main() {
    Builder::new()
        .filter_level(log::LevelFilter::Warn)
        .format_timestamp(None)
        .filter_module("rumqttc", log::LevelFilter::Warn)
        .init();

    // Create a session
    let connection_settings = MqttConnectionSettingsBuilder::default()
        .client_id(CLIENT_ID)
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
    let mut session = Session::new(session_options).unwrap();

    // Use the managed client to run a a command executor in another task
    tokio::task::spawn(executor_loop(session.create_managed_client()));

    // Run the session
    session.run().await.unwrap();
}

/// Handle incoming increment command requests
async fn executor_loop(client: SessionManagedClient) {
    // Create a command executor for the increment command
    let incr_executor_options = CommandExecutorOptionsBuilder::default()
        .request_topic_pattern(REQUEST_TOPIC_PATTERN)
        .command_name("increment")
        .build()
        .unwrap();
    let mut incr_executor: CommandExecutor<IncrRequestPayload, IncrResponsePayload, _> =
        CommandExecutor::new(client, incr_executor_options).unwrap();

    // Counter to increment
    let mut counter = 0;

    // Increment the counter for each incoming request
    loop {
        // TODO: Show how to use other parameters
        let request = incr_executor.recv().await.unwrap();
        counter += 1;
        let response = IncrResponsePayload {
            counter_response: counter,
        };
        let response = CommandResponseBuilder::default()
            .payload(response)
            .unwrap()
            .build()
            .unwrap();
        request.complete(response).unwrap();
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
    fn content_type() -> &'static str {
        "application/json"
    }

    fn format_indicator() -> FormatIndicator {
        FormatIndicator::Utf8EncodedCharacterData
    }

    fn serialize(self) -> Result<Vec<u8>, IncrSerializerError> {
        // This is a request payload, executor does not need to serialize it
        unimplemented!()
    }

    fn deserialize(_payload: &[u8]) -> Result<IncrRequestPayload, IncrSerializerError> {
        Ok(IncrRequestPayload {})
    }
}

impl PayloadSerialize for IncrResponsePayload {
    type Error = IncrSerializerError;
    fn content_type() -> &'static str {
        "application/json"
    }

    fn format_indicator() -> FormatIndicator {
        FormatIndicator::Utf8EncodedCharacterData
    }

    fn serialize(self) -> Result<Vec<u8>, IncrSerializerError> {
        let payload = format!("{{\"CounterResponse\":{}}}", self.counter_response);
        Ok(payload.into_bytes())
    }

    fn deserialize(_payload: &[u8]) -> Result<IncrResponsePayload, IncrSerializerError> {
        // This is a response payload, executor does not need to deserialize it
        unimplemented!()
    }
}
