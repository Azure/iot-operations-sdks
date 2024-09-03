// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
use std::time::Duration;

use env_logger::Builder;

use azure_iot_operations_mqtt::session::{
    Session, SessionExitHandle, SessionOptionsBuilder, SessionPubSub,
};
use azure_iot_operations_mqtt::MqttConnectionSettingsBuilder;
use azure_iot_operations_protocol::common::payload_serialize::{
    FormatIndicator, PayloadSerialize, SerializerError,
};
use azure_iot_operations_protocol::rpc::command_invoker::{
    CommandInvoker, CommandInvokerOptionsBuilder, CommandRequestBuilder,
};

const CLIENT_ID: &str = "<client id>";
const HOST: &str = "<broker host>";
const PORT: u16 = 1883;
const REQUEST_TOPIC_PATTERN: &str = "<request topic>";
const RESPONSE_TOPIC_PATTERN: &str = "<response topic>";

#[tokio::main(flavor = "current_thread")]
async fn main() {
    Builder::new()
        .filter_level(log::LevelFilter::max())
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
            .executor_id(Some("SampleServer".to_string()))
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
pub struct IncrResponse {}

impl PayloadSerialize for IncrRequest {
    fn content_type() -> &'static str {
        "application/json"
    }

    fn format_indicator() -> FormatIndicator {
        FormatIndicator::Utf8EncodedCharacterData
    }

    fn serialize(&self) -> Result<Vec<u8>, SerializerError> {
        Ok(String::new().into())
    }

    fn deserialize(_payload: &[u8]) -> Result<IncrRequest, SerializerError> {
        Ok(IncrRequest {})
    }
}

impl PayloadSerialize for IncrResponse {
    fn content_type() -> &'static str {
        "application/json"
    }

    fn format_indicator() -> FormatIndicator {
        FormatIndicator::Utf8EncodedCharacterData
    }
    fn serialize(&self) -> Result<Vec<u8>, SerializerError> {
        Ok(String::new().into())
    }

    fn deserialize(_payload: &[u8]) -> Result<IncrResponse, SerializerError> {
        Ok(IncrResponse {})
    }
}
