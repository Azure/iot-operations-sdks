// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
use std::time::Duration;
use std::{num::ParseIntError, str::Utf8Error};

use env_logger::Builder;
use thiserror::Error;

use azure_iot_operations_mqtt::MqttConnectionSettingsBuilder;
use azure_iot_operations_mqtt::session::{Session, SessionManagedClient, SessionOptionsBuilder};
use azure_iot_operations_protocol::application::ApplicationContextBuilder;
use azure_iot_operations_protocol::common::payload_serialize::{
    DeserializationError, FormatIndicator, PayloadSerialize, SerializedPayload,
};
use azure_iot_operations_protocol::rpc_command;

const CLIENT_ID: &str = "aio_example_invoker_client";
const HOSTNAME: &str = "localhost";
const PORT: u16 = 1883;
const REQUEST_TOPIC_PATTERN: &str = "topic/for/request";
const RESPONSE_TOPIC_PATTERN: &str = "topic/for/response";

#[tokio::main(flavor = "current_thread")]
async fn main() -> Result<(), Box<dyn std::error::Error>> {
    Builder::new()
        .filter_level(log::LevelFilter::Info)
        .format_timestamp(None)
        .filter_module("rumqttc", log::LevelFilter::Warn)
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

    // Create an RPC command Invoker for the 'increment' command
    let incr_invoker_options = rpc_command::invoker::OptionsBuilder::default()
        .request_topic_pattern(REQUEST_TOPIC_PATTERN)
        .response_topic_pattern(RESPONSE_TOPIC_PATTERN.to_string())
        .command_name("increment")
        .build()
        .unwrap();
    let incr_invoker: rpc_command::Invoker<IncrRequestPayload, IncrResponsePayload, _> =
        rpc_command::Invoker::new(
            application_context,
            session.create_managed_client(),
            incr_invoker_options,
        )?;

    // Run the Session and and the 'increment' command invoker concurrently
    tokio::select! {
        () = increment_invoke_loop(incr_invoker) => (),
        sr = session.run() => sr?,
    }
    Ok(())
}

/// Indefinitely send 'increment' command requests
async fn increment_invoke_loop(
    invoker: rpc_command::Invoker<IncrRequestPayload, IncrResponsePayload, SessionManagedClient>,
) {
    loop {
        let payload = rpc_command::invoker::RequestBuilder::default()
            .payload(IncrRequestPayload::default())
            .unwrap()
            .timeout(Duration::from_secs(2))
            .build()
            .unwrap();
        log::info!("Sending 'increment' command request...");
        match invoker.invoke(payload).await {
            Ok(response) => {
                log::info!("Response: {response:?}");
            }
            Err(e) => {
                log::error!("Error invoking 'increment' command: {e}");
            }
        }
        tokio::time::sleep(Duration::from_secs(5)).await;
    }
}

#[derive(Clone, Debug, Default)]
pub struct IncrRequestPayload {}

#[derive(Clone, Debug, Default)]
pub struct IncrResponsePayload {
    pub counter_response: i32,
}

#[derive(Debug, Error)]
pub enum IncrSerializerError {
    #[error("invalid payload: {0:?}")]
    InvalidPayload(Vec<u8>),
    #[error(transparent)]
    ParseIntError(#[from] ParseIntError),
    #[error(transparent)]
    Utf8Error(#[from] Utf8Error),
}

impl PayloadSerialize for IncrRequestPayload {
    type Error = IncrSerializerError;
    fn serialize(self) -> Result<SerializedPayload, IncrSerializerError> {
        Ok(SerializedPayload {
            payload: Vec::new(),
            content_type: "application/json".to_string(),
            format_indicator: FormatIndicator::Utf8EncodedCharacterData,
        })
    }
    fn deserialize(
        _payload: &[u8],
        _content_type: Option<&String>,
        _format_indicator: &FormatIndicator,
    ) -> Result<IncrRequestPayload, DeserializationError<IncrSerializerError>> {
        // This is a request payload, invoker does not need to deserialize it
        unimplemented!()
    }
}

impl PayloadSerialize for IncrResponsePayload {
    type Error = IncrSerializerError;
    fn serialize(self) -> Result<SerializedPayload, IncrSerializerError> {
        // This is a response payload, invoker does not need to serialize it
        unimplemented!()
    }

    fn deserialize(
        payload: &[u8],
        content_type: Option<&String>,
        _format_indicator: &FormatIndicator,
    ) -> Result<IncrResponsePayload, DeserializationError<IncrSerializerError>> {
        if let Some(content_type) = content_type {
            if content_type != "application/json" {
                return Err(DeserializationError::UnsupportedContentType(format!(
                    "Invalid content type: '{content_type:?}'. Must be 'application/json'"
                )));
            }
        }
        let payload = match std::str::from_utf8(payload) {
            Ok(p) => p,
            Err(e) => {
                return Err(DeserializationError::InvalidPayload(
                    IncrSerializerError::Utf8Error(e),
                ));
            }
        };

        let start_str = "{\"CounterResponse\":";

        if payload.starts_with(start_str) && payload.ends_with('}') {
            let counter_str = &payload[start_str.len()..payload.len() - 1];
            match counter_str.parse::<i32>() {
                Ok(counter_response) => Ok(IncrResponsePayload { counter_response }),
                Err(e) => Err(DeserializationError::InvalidPayload(
                    IncrSerializerError::ParseIntError(e),
                )),
            }
        } else {
            Err(DeserializationError::InvalidPayload(
                IncrSerializerError::InvalidPayload(payload.into()),
            ))
        }
    }
}
