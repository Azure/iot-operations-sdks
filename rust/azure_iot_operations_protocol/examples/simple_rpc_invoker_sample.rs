// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
use std::time::Duration;

use env_logger::Builder;

use azure_iot_operations_mqtt::session::{
    Session, SessionExitHandle, SessionManagedClient, SessionOptionsBuilder,
};
use azure_iot_operations_mqtt::MqttConnectionSettingsBuilder;
use azure_iot_operations_protocol::common::payload_serialize::{FormatIndicator, PayloadSerialize};
use azure_iot_operations_protocol::rpc::command_invoker::{
    CommandInvoker, CommandInvokerOptionsBuilder, CommandRequestBuilder,
};

const CLIENT_ID: &str = "aio_example_invoker_client";
const HOST: &str = "localhost";
const PORT: u16 = 1883;
const REQUEST_TOPIC_PATTERN: &str = "topic/for/request";
const RESPONSE_TOPIC_PATTERN: &str = "topic/for/response";

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
        .host_name(HOST)
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

    // Use the managed client to run command invocations in another task
    tokio::task::spawn(invoke_loop(
        session.create_managed_client(),
        session.create_exit_handle(),
    ));

    // Run the session
    session.run().await.unwrap();
}

/// Send 10 increment command requests and wait for their responses, then disconnect
async fn invoke_loop(client: SessionManagedClient, exit_handle: SessionExitHandle) {
    // Create a command invoker for the increment command
    let incr_invoker_options = CommandInvokerOptionsBuilder::default()
        .request_topic_pattern(REQUEST_TOPIC_PATTERN)
        .response_topic_pattern(RESPONSE_TOPIC_PATTERN.to_string())
        .command_name("increment")
        .build()
        .unwrap();
    let incr_invoker: CommandInvoker<IncrRequestPayload, IncrResponsePayload, _> =
        CommandInvoker::new(client, incr_invoker_options).unwrap();

    // Send 10 increment requests
    for i in 1..10 {
        let payload = CommandRequestBuilder::default()
            .payload(&IncrRequestPayload::default())
            .unwrap()
            .timeout(Duration::from_secs(2))
            .executor_id(None)
            .build()
            .unwrap();
        let response = incr_invoker.invoke(payload).await;
        log::info!("Response {}: {:?}", i, response);
    }

    // End the session
    exit_handle.try_exit().await.unwrap();
}

#[derive(Clone, Debug, Default)]
pub struct IncrRequestPayload {}

#[derive(Clone, Debug, Default)]
pub struct IncrResponsePayload {
    pub counter_response: i32,
}

impl PayloadSerialize for IncrRequestPayload {
    type Error = String;
    fn content_type() -> &'static str {
        "application/json"
    }

    fn format_indicator() -> FormatIndicator {
        FormatIndicator::Utf8EncodedCharacterData
    }

    fn serialize(&self) -> Result<Vec<u8>, String> {
        Ok(String::new().into())
    }

    fn deserialize(_payload: &[u8]) -> Result<IncrRequestPayload, String> {
        Ok(IncrRequestPayload {})
    }
}

impl PayloadSerialize for IncrResponsePayload {
    type Error = String;
    fn content_type() -> &'static str {
        "application/json"
    }

    fn format_indicator() -> FormatIndicator {
        FormatIndicator::Utf8EncodedCharacterData
    }
    fn serialize(&self) -> Result<Vec<u8>, String> {
        Ok(String::new().into())
    }

    fn deserialize(payload: &[u8]) -> Result<IncrResponsePayload, String> {
        let payload = String::from_utf8(payload.to_vec()).unwrap();
        let counter_response = payload.parse::<i32>().unwrap();
        Ok(IncrResponsePayload { counter_response })
    }
}
