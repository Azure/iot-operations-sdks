// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
use std::time::Duration;

use env_logger::Builder;

use azure_iot_operations_mqtt::session::{
    Session, SessionExitHandle, SessionOptionsBuilder, SessionPubSub,
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
    let exit_handle = session.get_session_exit_handle();

    let rpc_incr_invoker_options = CommandInvokerOptionsBuilder::default()
        .request_topic_pattern(REQUEST_TOPIC_PATTERN)
        .response_topic_pattern(RESPONSE_TOPIC_PATTERN.to_string())
        .command_name("increment")
        .build()
        .unwrap();
    let rpc_incr_invoker: CommandInvoker<IncrRequest, IncrResponse, _> =
        CommandInvoker::new(&mut session, rpc_incr_invoker_options).unwrap();

    tokio::task::spawn(rpc_loop(rpc_incr_invoker, exit_handle));

    session.run().await.unwrap();
}

/// Send 10 increment command requests and wait for their responses, then disconnect
async fn rpc_loop(
    rpc_invoker: CommandInvoker<IncrRequest, IncrResponse, SessionPubSub>,
    exit_handle: SessionExitHandle,
) {
    for i in 1..10 {
        let payload = CommandRequestBuilder::default()
            .payload(&IncrRequest::default())
            .unwrap()
            .timeout(Duration::from_secs(2))
            .executor_id(None)
            .build()
            .unwrap();
        let response = rpc_invoker.invoke(payload).await;
        log::info!("Response {}: {:?}", i, response);
    }

    exit_handle.exit_session().await.unwrap();
}

#[derive(Clone, Debug, Default)]
pub struct IncrRequest {}

#[derive(Clone, Debug, Default)]
pub struct IncrResponse {
    pub counter_response: i32,
}

impl PayloadSerialize for IncrRequest {
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

    fn deserialize(_payload: &[u8]) -> Result<IncrRequest, String> {
        Ok(IncrRequest {})
    }
}

impl PayloadSerialize for IncrResponse {
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

    fn deserialize(payload: &[u8]) -> Result<IncrResponse, String> {
        let payload = String::from_utf8(payload.to_vec()).unwrap();
        let counter_response = payload.parse::<i32>().unwrap();
        Ok(IncrResponse { counter_response })
    }
}
