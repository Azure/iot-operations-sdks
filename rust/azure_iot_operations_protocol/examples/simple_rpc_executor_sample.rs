// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
use std::time::Duration;

use env_logger::Builder;
use thiserror::Error;

use azure_iot_operations_mqtt::session::{Session, SessionManagedClient, SessionOptionsBuilder};
use azure_iot_operations_mqtt::MqttConnectionSettingsBuilder;
use azure_iot_operations_protocol::application::{ApplicationContext, ApplicationContextBuilder};
use azure_iot_operations_protocol::common::payload_serialize::{
    DeserializationError, FormatIndicator, PayloadSerialize, SerializedPayload,
};
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
    let session = Session::new(session_options).unwrap();

    let application_context = ApplicationContextBuilder::default().build().unwrap();

    // Use the managed client to run a a command executor in another task
    tokio::task::spawn(executor_loop(
        application_context,
        session.create_managed_client(),
    ));

    // Run the session
    session.run().await.unwrap();
}

/// Handle incoming increment command requests
async fn executor_loop(application_context: ApplicationContext, client: SessionManagedClient) {
    // Create a command executor for the increment command
    let incr_executor_options = CommandExecutorOptionsBuilder::default()
        .request_topic_pattern(REQUEST_TOPIC_PATTERN)
        .command_name("increment")
        .build()
        .unwrap();
    let mut incr_executor: CommandExecutor<IncrRequestPayload, IncrResponsePayload, _> =
        CommandExecutor::new(application_context, client, incr_executor_options).unwrap();

    // Counter to increment
    let mut counter = 0;

    // Increment the counter for each incoming request
    while let Some(request) = incr_executor.recv().await {
        match request {
            Ok(request) => {
                counter += 1;
                let response = IncrResponsePayload {
                    counter_response: counter,
                };
                let response = CommandResponseBuilder::default()
                    .payload(response)
                    .unwrap()
                    .build()
                    .unwrap();
                request.complete(response).await.unwrap();
            }
            Err(err) => {
                println!("Error receiving request: {err}");
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
