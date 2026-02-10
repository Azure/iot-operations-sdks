// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
use chrono::prelude::*;
use std::time::Duration;

use env_logger::Builder;
use thiserror::Error;

use azure_iot_operations_mqtt::aio::connection_settings::MqttConnectionSettingsBuilder;
use azure_iot_operations_mqtt::session::{Session, SessionOptionsBuilder};
use azure_iot_operations_protocol::application::ApplicationContextBuilder;
use azure_iot_operations_protocol::common::payload_serialize::{
    DeserializationError, FormatIndicator, PayloadSerialize, SerializedPayload,
};
use azure_iot_operations_protocol::rpc_command;

const CLIENT_ID: &str = "aio_example_executor_client";
const HOSTNAME: &str = "localhost";
const PORT: u16 = 1883;
const REQUEST_TOPIC_PATTERN: &str = "topic/for/request";

#[tokio::main(flavor = "current_thread")]
async fn main() -> Result<(), Box<dyn std::error::Error>> {
    Builder::new()
        .filter_level(log::LevelFilter::Info)
        .format_timestamp(None)
        .filter_module("azure_mqtt", log::LevelFilter::Warn)
        .init();

    // Create a Session
    let connection_settings = MqttConnectionSettingsBuilder::default()
        .client_id(CLIENT_ID)
        .hostname(HOSTNAME)
        .tcp_port(PORT)
        .keep_alive(Duration::from_secs(5))
        .use_tls(false)
        .build()?;
    let session_options = SessionOptionsBuilder::default()
        .connection_settings(connection_settings)
        .build()?;
    let session = Session::new(session_options).unwrap();

    // Create an ApplicationContext
    let application_context = ApplicationContextBuilder::default().build().unwrap();

    // Create an RPC command Executor for the 'increment' command
    let incr_executor_options = rpc_command::executor::OptionsBuilder::default()
        .request_topic_pattern(REQUEST_TOPIC_PATTERN)
        .command_name("increment")
        .build()
        .unwrap();
    let incr_executor: rpc_command::Executor<IncrRequestPayload, IncrResponsePayload> =
        rpc_command::Executor::new(
            application_context,
            session.create_managed_client(),
            incr_executor_options,
        )?;

    // Run the Session and the Executor loop concurrently
    tokio::select! {
        r1 = increment_executor_loop(incr_executor) => r1.map_err(|e| e as Box<dyn std::error::Error>)?,
        r2 = session.run() => r2?,
    }

    Ok(())
}

/// Handle incoming increment command requests
async fn increment_executor_loop(
    mut executor: rpc_command::Executor<IncrRequestPayload, IncrResponsePayload>,
) -> Result<(), Box<dyn std::error::Error + Send + Sync>> {
    // Counter to increment
    let mut counter = 0;

    // Increment the counter for each incoming request
    while let Some(recv_result) = executor.recv().await {
        let utcBefore: DateTime<Utc> = Utc::now();
        println!("Received invocation: {}", utcBefore);

        let request = recv_result?;

        // Parse cloud event if present
        match rpc_command::executor::cloud_event_from_request(&request) {
            Ok(cloud_event) => {
                log::info!("{cloud_event}");
            }
            Err(e) => {
                // If a cloud event is not present, this error is expected
                log::warn!("Error parsing cloud event: {e}");
            }
        }

        // Update the counter
        counter += 1;
        log::info!("Counter incremented to: {counter}");
        // Create the response
        let response = IncrResponsePayload {
            counter_response: counter,
        };
        let cloud_event = rpc_command::executor::ResponseCloudEventBuilder::default()
            .source("aio://increment/executor/sample")
            .build()
            .unwrap();
        let response = rpc_command::executor::ResponseBuilder::default()
            .payload(response)
            .unwrap()
            .cloud_event(cloud_event)
            .build()
            .unwrap();
        // Send the response
        match request.complete(response).await {
            Ok(()) => {
                let utcAfter: DateTime<Utc> = Utc::now();
                println!("Sent response: {}", utcAfter);
                log::info!("Sent response to 'increment' command request");
            }
            Err(e) => {
                log::error!("Error sending response to 'increment' command request: {e}");
            }
        }
    }

    // Shut down if there are no more requests
    executor.shutdown().await?;

    Ok(())
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
