// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

use std::{env, num::ParseIntError, str::Utf8Error, time::Duration};

use env_logger::Builder;
use thiserror::Error;

use azure_iot_operations_mqtt::session::{
    Session, SessionExitHandle, SessionManagedClient, SessionOptionsBuilder,
};
use azure_iot_operations_mqtt::MqttConnectionSettingsBuilder;
use azure_iot_operations_protocol::common::payload_serialize::{FormatIndicator, PayloadSerialize};
use azure_iot_operations_protocol::rpc::command_invoker::{
    CommandInvoker, CommandInvokerOptionsBuilder, CommandRequestBuilder,
};

const REQUEST_TOPIC_PATTERN: &str = "rpc/command-samples/{executorId}/{commandName}";

#[tokio::main(flavor = "current_thread")]
async fn main() {
    Builder::new()
        .filter_level(log::LevelFilter::max())
        .format_timestamp(None)
        .filter_module("rumqttc", log::LevelFilter::Warn)
        .init();

    // Create a session
    let connection_settings = MqttConnectionSettingsBuilder::from_environment()
        .build()
        .unwrap();
    let session_options = SessionOptionsBuilder::default()
        .connection_settings(connection_settings)
        .build()
        .unwrap();
    let mut session = Session::new(session_options).unwrap();

    // Use the managed client to run command invocations in another task
    tokio::task::spawn(increment_and_check(
        session.create_managed_client(),
        session.create_exit_handle(),
    ));

    // Run the session
    session.run().await.unwrap();
}

/// Send a read request, 15 increment command requests, and another read request and wait for their responses, then disconnect
async fn increment_and_check(client: SessionManagedClient, exit_handle: SessionExitHandle) {
    // Create a command invoker for the readCounter command
    let read_invoker_options = CommandInvokerOptionsBuilder::default()
        .request_topic_pattern(REQUEST_TOPIC_PATTERN)
        .command_name("readCounter")
        .build()
        .unwrap();
    let read_invoker: CommandInvoker<CounterRequestPayload, CounterResponsePayload, _> =
        CommandInvoker::new(client.clone(), read_invoker_options).unwrap();

    // Create a command invoker for the increment command
    let incr_invoker_options = CommandInvokerOptionsBuilder::default()
        .request_topic_pattern(REQUEST_TOPIC_PATTERN)
        .command_name("increment")
        .build()
        .unwrap();
    let incr_invoker: CommandInvoker<CounterRequestPayload, CounterResponsePayload, _> =
        CommandInvoker::new(client, incr_invoker_options).unwrap();

    // Get the target executor ID from the environment
    let executor_id = env::var("COUNTER_SERVER_ID").ok();

    // Initial counter read from the server
    log::info!("Calling readCounter");
    let read_payload = CommandRequestBuilder::default()
        .payload(&CounterRequestPayload::default())
        .unwrap()
        .executor_id(executor_id.clone())
        .timeout(Duration::from_secs(10))
        .build()
        .unwrap();
    let read_response = read_invoker.invoke(read_payload).await.unwrap();
    log::info!("Counter value: {:?}", read_response);

    // Increment the counter 15 times on the server
    for _ in 1..15 {
        log::info!("Calling increment");
        let incr_payload = CommandRequestBuilder::default()
            .payload(&CounterRequestPayload::default())
            .unwrap()
            .timeout(Duration::from_secs(10))
            .executor_id(executor_id.clone())
            .build()
            .unwrap();
        let incr_response = incr_invoker.invoke(incr_payload).await;
        log::info!("Counter value after increment:: {:?}", incr_response);
    }

    // Final counter read from the server
    log::info!("Calling readCounter");
    let read_payload = CommandRequestBuilder::default()
        .payload(&CounterRequestPayload::default())
        .unwrap()
        .executor_id(executor_id)
        .timeout(Duration::from_secs(10))
        .build()
        .unwrap();
    let read_response = read_invoker.invoke(read_payload).await.unwrap();
    log::info!("Counter value: {}", read_response.payload.counter_response);

    // Exit the session now that we're done
    exit_handle.try_exit().await.unwrap();
}

#[derive(Clone, Debug, Default)]
pub struct CounterRequestPayload {}

#[derive(Clone, Debug, Default)]
pub struct CounterResponsePayload {
    counter_response: u64,
}

#[derive(Debug, Error)]
pub enum CounterSerializerError {
    #[error("invalid payload: {0:?}")]
    InvalidPayload(Vec<u8>),
    #[error(transparent)]
    ParseIntError(#[from] ParseIntError),
    #[error(transparent)]
    Utf8Error(#[from] Utf8Error),
}

impl PayloadSerialize for CounterRequestPayload {
    type Error = CounterSerializerError;
    fn content_type() -> &'static str {
        "application/json"
    }

    fn format_indicator() -> FormatIndicator {
        FormatIndicator::UnspecifiedBytes
    }

    fn serialize(&self) -> Result<Vec<u8>, CounterSerializerError> {
        Ok(String::new().into())
    }

    fn deserialize(_payload: &[u8]) -> Result<CounterRequestPayload, CounterSerializerError> {
        Ok(CounterRequestPayload {})
    }
}

impl PayloadSerialize for CounterResponsePayload {
    type Error = CounterSerializerError;
    fn content_type() -> &'static str {
        "application/json"
    }

    fn format_indicator() -> FormatIndicator {
        FormatIndicator::Utf8EncodedCharacterData
    }

    fn serialize(&self) -> Result<Vec<u8>, CounterSerializerError> {
        // This is a response payload, client does not need to serialize it
        unimplemented!()
    }

    fn deserialize(payload: &[u8]) -> Result<CounterResponsePayload, CounterSerializerError> {
        let payload = match std::str::from_utf8(payload) {
            Ok(p) => {
                log::info!("payload: {:?}", p);
                p
            }
            Err(e) => return Err(CounterSerializerError::Utf8Error(e)),
        };

        let start_str = "{\"CounterResponse\":";

        if payload.starts_with(start_str) && payload.ends_with('}') {
            match payload[start_str.len()..payload.len() - 1].parse::<u64>() {
                Ok(n) => Ok(CounterResponsePayload {
                    counter_response: n,
                }),
                Err(e) => Err(CounterSerializerError::ParseIntError(e)),
            }
        } else {
            Err(CounterSerializerError::InvalidPayload(payload.into()))
        }
    }
}
