// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

use std::sync::{Arc, Mutex};
use std::{num::ParseIntError, str::Utf8Error, time::Duration};

use env_logger::Builder;
use thiserror::Error;

use azure_iot_operations_mqtt::session::{
    Session, SessionExitHandle, SessionManagedClient, SessionOptionsBuilder,
};
use azure_iot_operations_mqtt::MqttConnectionSettingsBuilder;
use azure_iot_operations_protocol::common::payload_serialize::{FormatIndicator, PayloadSerialize};
use azure_iot_operations_protocol::rpc::command_executor::{
    CommandExecutor, CommandExecutorOptionsBuilder, CommandResponseBuilder,
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

    // The counter value for the server
    let counter = Arc::new(Mutex::new(0));

    // Spawn tasks for the server features
    tokio::spawn(read_executor(
        session.create_managed_client(),
        counter.clone(),
    ));
    tokio::spawn(increment_executor(
        session.create_managed_client(),
        counter.clone(),
    ));
    tokio::spawn(exit_timer(
        session.create_exit_handle(),
        Duration::from_secs(120),
    ));

    // Run the session
    session.run().await.unwrap();
}

/// Run an RPC command executor that responds to requests to read the counter value.
async fn read_executor(client: SessionManagedClient, counter: Arc<Mutex<u64>>) {
    // Create a command executor for the readCounter command
    let read_executor_options = CommandExecutorOptionsBuilder::default()
        .request_topic_pattern(REQUEST_TOPIC_PATTERN)
        .command_name("readCounter")
        .build()
        .unwrap();
    let mut read_executor: CommandExecutor<CounterRequestPayload, CounterResponsePayload, _> =
        CommandExecutor::new(client, read_executor_options).unwrap();

    // Loop to handle requests
    loop {
        let request = read_executor.recv().await.unwrap();
        let response = CounterResponsePayload {
            counter_response: *counter.lock().unwrap(),
        };
        tokio::time::sleep(Duration::from_secs(1)).await;
        let response = CommandResponseBuilder::default()
            .payload(&response)
            .unwrap()
            .build()
            .unwrap();
        request.complete(response).unwrap();
    }
}

/// Run an RPC command executor that responds to requests to increment the counter value.
async fn increment_executor(client: SessionManagedClient, counter: Arc<Mutex<u64>>) {
    // Create a command executor for the increment command
    let incr_executor_options = CommandExecutorOptionsBuilder::default()
        .request_topic_pattern(REQUEST_TOPIC_PATTERN)
        .command_name("increment")
        .build()
        .unwrap();
    let mut incr_executor: CommandExecutor<CounterRequestPayload, CounterResponsePayload, _> =
        CommandExecutor::new(client, incr_executor_options).unwrap();

    // Loop to handle requests
    loop {
        let request = incr_executor.recv().await.unwrap();
        *counter.lock().unwrap() += 1;
        let response = CounterResponsePayload {
            counter_response: *counter.lock().unwrap(),
        };
        let response = CommandResponseBuilder::default()
            .payload(&response)
            .unwrap()
            .build()
            .unwrap();
        request.complete(response).unwrap();
    }
}

/// Exit the session after a delay.
async fn exit_timer(exit_handle: SessionExitHandle, exit_after: Duration) {
    tokio::time::sleep(exit_after).await;
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
        Ok(format!("{{\"CounterResponse\":{}}}", self.counter_response).into())
    }

    fn deserialize(payload: &[u8]) -> Result<CounterResponsePayload, CounterSerializerError> {
        log::info!("payload: {:?}", std::str::from_utf8(payload).unwrap());
        if payload.starts_with(b"{\"CounterResponse\":") && payload.ends_with(b"}") {
            match std::str::from_utf8(&payload[19..payload.len() - 1]) {
                Ok(s) => {
                    log::info!("s: {:?}", s);
                    match s.parse::<u64>() {
                        Ok(n) => Ok(CounterResponsePayload {
                            counter_response: n,
                        }),
                        Err(e) => Err(CounterSerializerError::ParseIntError(e)),
                    }
                }
                Err(e) => Err(CounterSerializerError::Utf8Error(e)),
            }
        } else {
            Err(CounterSerializerError::InvalidPayload(payload.into()))
        }
    }
}
