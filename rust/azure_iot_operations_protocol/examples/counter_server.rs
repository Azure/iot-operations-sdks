// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

use std::{num::ParseIntError, str::Utf8Error, time::Duration};

use env_logger::Builder;
use thiserror::Error;
use tokio::time::Instant;

use azure_iot_operations_mqtt::session::{
    Session, SessionExitHandle, SessionOptionsBuilder, SessionPubReceiver, SessionPubSub,
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

    let connection_settings = MqttConnectionSettingsBuilder::from_environment()
        .build()
        .unwrap();

    let session_options = SessionOptionsBuilder::default()
        .connection_settings(connection_settings)
        .build()
        .unwrap();

    let mut session = Session::new(session_options).unwrap();
    let exit_handle = session.get_session_exit_handle();

    let rpc_read_executor_options = CommandExecutorOptionsBuilder::default()
        .request_topic_pattern(REQUEST_TOPIC_PATTERN)
        .command_name("readCounter")
        .build()
        .unwrap();
    let rpc_read_executor: CommandExecutor<CounterRequest, CounterResponse, _, _> =
        CommandExecutor::new(&mut session, rpc_read_executor_options).unwrap();

    let rpc_incr_executor_options = CommandExecutorOptionsBuilder::default()
        .request_topic_pattern(REQUEST_TOPIC_PATTERN)
        .command_name("increment")
        .build()
        .unwrap();
    let rpc_incr_executor: CommandExecutor<CounterRequest, CounterResponse, _, _> =
        CommandExecutor::new(&mut session, rpc_incr_executor_options).unwrap();

    tokio::task::spawn(rpc_loop(rpc_read_executor, rpc_incr_executor, exit_handle));

    session.run().await.unwrap();
}

async fn rpc_loop(
    mut rpc_read_executor: CommandExecutor<
        CounterRequest,
        CounterResponse,
        SessionPubSub,
        SessionPubReceiver,
    >,
    mut rpc_incr_executor: CommandExecutor<
        CounterRequest,
        CounterResponse,
        SessionPubSub,
        SessionPubReceiver,
    >,
    exit_handle: SessionExitHandle,
) {
    log::info!("Starting counter executor");

    // Create timer to expect responses for two minutes
    let exit_time = Instant::now() + Duration::from_secs(120);

    let mut counter = 0;
    loop {
        if !exit_time.elapsed().is_zero() {
            log::info!("Exiting counter executor");
            exit_handle.try_exit().await.unwrap();
            break;
        }
        tokio::select! {
            read_request = rpc_read_executor.recv() => {
                let request = read_request.unwrap();
                let response = CounterResponse {
                    counter_response: counter,
                };
                tokio::time::sleep(Duration::from_secs(1)).await;
                let response = CommandResponseBuilder::default()
                    .payload(&response)
                    .unwrap()
                    .build()
                    .unwrap();
                request.complete(response).unwrap();
            },
            incr_request = rpc_incr_executor.recv() => {
                let request = incr_request.unwrap();
                counter += 1;
                let response = CounterResponse {
                    counter_response: counter,
                };
                let response = CommandResponseBuilder::default()
                    .payload(&response)
                    .unwrap()
                    .build()
                    .unwrap();
                request.complete(response).unwrap();
            },
        }
    }
}

#[derive(Clone, Debug, Default)]
pub struct CounterRequest {}

#[derive(Clone, Debug, Default)]
pub struct CounterResponse {
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

impl PayloadSerialize for CounterRequest {
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

    fn deserialize(_payload: &[u8]) -> Result<CounterRequest, CounterSerializerError> {
        Ok(CounterRequest {})
    }
}

impl PayloadSerialize for CounterResponse {
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

    fn deserialize(payload: &[u8]) -> Result<CounterResponse, CounterSerializerError> {
        log::info!("payload: {:?}", std::str::from_utf8(payload).unwrap());
        if payload.starts_with(b"{\"CounterResponse\":") && payload.ends_with(b"}") {
            match std::str::from_utf8(&payload[19..payload.len() - 1]) {
                Ok(s) => {
                    log::info!("s: {:?}", s);
                    match s.parse::<u64>() {
                        Ok(n) => Ok(CounterResponse {
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
