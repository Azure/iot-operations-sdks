// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
use std::time::Duration;

use env_logger::Builder;

use azure_iot_operations_mqtt::session::{
    Session, SessionExitHandle, SessionManagedClient, SessionOptionsBuilder,
};
use azure_iot_operations_mqtt::MqttConnectionSettingsBuilder;
use azure_iot_operations_protocol::common::payload_serialize::{BypassPayload, FormatIndicator};
use azure_iot_operations_protocol::rpc::command_invoker::{
    CommandInvoker, CommandInvokerOptionsBuilder, CommandRequestBuilder,
};

const CLIENT_ID: &str = "aio_example_invoker_client";
const HOSTNAME: &str = "localhost";
const PORT: u16 = 1883;
const REQUEST_TOPIC_PATTERN: &str = "topic/for/request";
const RESPONSE_TOPIC_PATTERN: &str = "topic/for/response";

#[tokio::main(flavor = "current_thread")]
async fn main() {
    Builder::new()
        .filter_level(log::LevelFilter::Info)
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

    // Use the managed client to run command invocations in another task
    tokio::task::spawn(invoke_loop(
        session.create_managed_client(),
        session.create_exit_handle(),
    ));

    // Run the session
    session.run().await.unwrap();
}

/// Send 10 file transfer command requests and wait for their responses, then disconnect
async fn invoke_loop(client: SessionManagedClient, exit_handle: SessionExitHandle) {
    // Create a command invoker for the file transfer command
    let file_transfer_invoker_options = CommandInvokerOptionsBuilder::default()
        .request_topic_pattern(REQUEST_TOPIC_PATTERN)
        .response_topic_pattern(RESPONSE_TOPIC_PATTERN.to_string())
        .command_name("file_transfer")
        .build()
        .unwrap();
    let file_transfer_invoker: CommandInvoker<BypassPayload, Vec<u8>, _> =
        CommandInvoker::new(client, file_transfer_invoker_options).unwrap();

    // Send 10 file transfer requests
    for i in 1..6 {
        let payload = CommandRequestBuilder::default()
            .payload(BypassPayload {
                payload: b"fruit,count\napple,2\norange,3".to_vec(),
                content_type: "text/csv".to_string(),
                format_indicator: FormatIndicator::Utf8EncodedCharacterData,
            })
            .unwrap()
            .timeout(Duration::from_secs(2))
            .build()
            .unwrap();
        let response = file_transfer_invoker.invoke(payload).await;
        log::info!("Response {}: {:?}", i, response);
    }
    for i in 6..11 {
        let payload = CommandRequestBuilder::default()
            .payload(BypassPayload {
                payload: "Hello, World!".to_string().into_bytes(),
                content_type: "text/plain".to_string(),
                format_indicator: FormatIndicator::Utf8EncodedCharacterData,
            })
            .unwrap()
            .timeout(Duration::from_secs(2))
            .build()
            .unwrap();
        let response = file_transfer_invoker.invoke(payload).await;
        log::info!("Response {}: {:?}", i, response);
    }

    file_transfer_invoker.shutdown().await.unwrap();

    // End the session
    exit_handle.try_exit().await.unwrap();
}
