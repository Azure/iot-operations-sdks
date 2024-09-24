// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

use std::{env, num::ParseIntError, str::Utf8Error, time::Duration};

use env_logger::Builder;
use thiserror::Error;

use azure_iot_operations_mqtt::session::{
    Session, SessionExitHandle, SessionOptionsBuilder, SessionPubSub,
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

    let connection_settings = MqttConnectionSettingsBuilder::from_environment()
        .build()
        .unwrap();

    let session_options = SessionOptionsBuilder::default()
        .connection_settings(connection_settings)
        .build()
        .unwrap();

    let mut session = Session::new(session_options).unwrap();
    let exit_handle = session.get_session_exit_handle();

    let rpc_read_invoker_options = CommandInvokerOptionsBuilder::default()
        .request_topic_pattern(REQUEST_TOPIC_PATTERN)
        .command_name("readCounter")
        .build()
        .unwrap();
    let rpc_read_invoker: CommandInvoker<CounterRequest, CounterResponse, _> =
        CommandInvoker::new(&mut session, rpc_read_invoker_options).unwrap();

    let rpc_incr_invoker_options = CommandInvokerOptionsBuilder::default()
        .request_topic_pattern(REQUEST_TOPIC_PATTERN)
        .command_name("increment")
        .build()
        .unwrap();
    let rpc_incr_invoker: CommandInvoker<CounterRequest, CounterResponse, _> =
        CommandInvoker::new(&mut session, rpc_incr_invoker_options).unwrap();

    tokio::task::spawn(rpc_loop(rpc_read_invoker, rpc_incr_invoker, exit_handle));

    session.run().await.unwrap();
}

/// Send a read request, 15 increment command requests, and another read request and wait for their responses, then disconnect
async fn rpc_loop(
    rpc_read_invoker: CommandInvoker<CounterRequest, CounterResponse, SessionPubSub>,
    rpc_incr_invoker: CommandInvoker<CounterRequest, CounterResponse, SessionPubSub>,
    exit_handle: SessionExitHandle,
) {
    let executor_id = env::var("COUNTER_SERVER_ID").ok();
    log::info!("Calling readCounter");
    let read_payload = CommandRequestBuilder::default()
        .payload(&CounterRequest::default())
        .unwrap()
        .executor_id(executor_id.clone())
        .timeout(Duration::from_secs(10))
        .build()
        .unwrap();
    let read_response = rpc_read_invoker.invoke(read_payload).await.unwrap();
    log::info!("Counter value: {:?}", read_response);

    for _ in 1..15 {
        log::info!("Calling increment");
        let incr_payload = CommandRequestBuilder::default()
            .payload(&CounterRequest::default())
            .unwrap()
            .timeout(Duration::from_secs(10))
            .executor_id(executor_id.clone())
            .build()
            .unwrap();
        let incr_response = rpc_incr_invoker.invoke(incr_payload).await;
        log::info!("Counter value after increment:: {:?}", incr_response);
    }

    log::info!("Calling readCounter");
    let read_payload = CommandRequestBuilder::default()
        .payload(&CounterRequest::default())
        .unwrap()
        .executor_id(executor_id)
        .timeout(Duration::from_secs(10))
        .build()
        .unwrap();
    let read_response = rpc_read_invoker.invoke(read_payload).await.unwrap();
    log::info!("Counter value: {:?}", read_response);

    exit_handle.exit_session().await.unwrap();
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
                Ok(s) => match s.parse::<u64>() {
                    Ok(n) => Ok(CounterResponse {
                        counter_response: n,
                    }),
                    Err(e) => Err(CounterSerializerError::ParseIntError(e)),
                },
                Err(e) => Err(CounterSerializerError::Utf8Error(e)),
            }
        } else {
            Err(CounterSerializerError::InvalidPayload(payload.into()))
        }
    }
}
