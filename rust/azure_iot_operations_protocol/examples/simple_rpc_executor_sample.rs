// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
use std::time::Duration;

use env_logger::Builder;

use azure_iot_operations_mqtt::session::{
    Session, SessionOptionsBuilder, SessionPubReceiver, SessionPubSub,
};
use azure_iot_operations_mqtt::MqttConnectionSettingsBuilder;
use azure_iot_operations_protocol::common::payload_serialize::{
    FormatIndicator, PayloadSerialize, SerializerError,
};
use azure_iot_operations_protocol::rpc::command_executor::{
    CommandExecutor, CommandExecutorOptionsBuilder, CommandResponseBuilder,
};

const CLIENT_ID: &str = "aio_example_executor_client";
const HOST: &str = "localhost";
const PORT: u16 = 1883;
const REQUEST_TOPIC_PATTERN: &str = "topic/for/request";

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

    let rpc_incr_executor_options = CommandExecutorOptionsBuilder::default()
        .request_topic_pattern(REQUEST_TOPIC_PATTERN)
        .command_name("increment")
        .build()
        .unwrap();
    let rpc_incr_executor: CommandExecutor<IncrRequest, IncrResponse, _, _> =
        CommandExecutor::new(&mut session, rpc_incr_executor_options).unwrap();

    tokio::task::spawn(incr_loop(rpc_incr_executor));

    session.run().await.unwrap();
}

async fn incr_loop(
    mut rpc_executor: CommandExecutor<IncrRequest, IncrResponse, SessionPubSub, SessionPubReceiver>,
) {
    let mut counter = 0;
    loop {
        // TODO: Show how to use other parameters
        let request = rpc_executor.recv().await.unwrap();
        counter += 1;
        let response = IncrResponse {
            counter_response: counter,
        };
        let response = CommandResponseBuilder::default()
            .payload(&response)
            .unwrap()
            .build()
            .unwrap();
        request.complete(response).unwrap();
    }
}

#[derive(Clone, Debug, Default)]
pub struct IncrRequest {}

#[derive(Clone, Debug, Default)]
pub struct IncrResponse {
    pub counter_response: i32,
}

impl PayloadSerialize for IncrRequest {
    fn content_type() -> &'static str {
        "application/json"
    }

    fn format_indicator() -> FormatIndicator {
        FormatIndicator::Utf8EncodedCharacterData
    }

    fn serialize(&self) -> Result<Vec<u8>, SerializerError> {
        let payload = String::from("{}");
        Ok(payload.into_bytes())
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
        let payload = format!("{{\"CounterResponse\":{}}}", self.counter_response);
        Ok(payload.into_bytes())
    }

    fn deserialize(payload: &[u8]) -> Result<IncrResponse, SerializerError> {
        let payload = String::from_utf8(payload.to_vec()).unwrap();
        let counter_response = payload.parse::<i32>().unwrap();
        Ok(IncrResponse { counter_response })
    }
}
