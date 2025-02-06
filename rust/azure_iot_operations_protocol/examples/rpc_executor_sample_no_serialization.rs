// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
use std::time::Duration;

use azure_iot_operations_protocol::application::{
    ApplicationContext, ApplicationContextOptionsBuilder,
};
use env_logger::Builder;

use azure_iot_operations_mqtt::session::{Session, SessionManagedClient, SessionOptionsBuilder};
use azure_iot_operations_mqtt::MqttConnectionSettingsBuilder;
use azure_iot_operations_protocol::common::payload_serialize::BypassPayload;
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

    let application_context =
        ApplicationContext::new(ApplicationContextOptionsBuilder::default().build().unwrap());

    // Use the managed client to run a command executor in another task
    tokio::task::spawn(executor_loop(
        application_context,
        session.create_managed_client(),
    ));

    // Run the session
    session.run().await.unwrap();
}

/// Handle incoming file transfer command requests
async fn executor_loop(application_context: ApplicationContext, client: SessionManagedClient) {
    // Create a command executor for the file transfer command
    let file_transfer_executor_options = CommandExecutorOptionsBuilder::default()
        .request_topic_pattern(REQUEST_TOPIC_PATTERN)
        .command_name("file_transfer")
        .build()
        .unwrap();
    let mut file_transfer_executor: CommandExecutor<BypassPayload, Vec<u8>, _> =
        CommandExecutor::new(application_context, client, file_transfer_executor_options).unwrap();

    // Save the file for each incoming request
    loop {
        let request = file_transfer_executor.recv().await.unwrap().unwrap();
        match request.payload.content_type.as_str() {
            "text/csv" => {
                // save csv file implementation would go here
                log::info!("CSV file saved!");

                let response = CommandResponseBuilder::default()
                    .payload(b"CSV File Saved".to_vec())
                    .unwrap()
                    .build()
                    .unwrap();
                request.complete(response).await.unwrap();
            }
            "text/plain" => {
                // save txt file implementation would go here
                log::info!("txt file saved!");

                let response = CommandResponseBuilder::default()
                    .payload(b"txt File Saved".to_vec())
                    .unwrap()
                    .build()
                    .unwrap();
                request.complete(response).await.unwrap();
            }
            _ => {
                log::warn!("Ignored file");
                request.error("Ignored File".to_string()).await.unwrap();
            }
        }
    }
}
