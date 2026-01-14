// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

//! Error types for the MQTT client library.

use thiserror::Error;

pub use crate::azure_mqtt::client::token::completion::CompletionError; // Re-export to have all errors in one place

/// Indicates a failure in the MQTT client before any operation takes place
/// or the state is affected.
#[derive(Debug, Clone, Error)]
#[error("Communication channels have been closed")]
pub struct DetachedError {}

/// Indicates that the MQTT operation did not complete successfully
/// NOTE: Does NOT contain the reason code as an enum, as it must be agnostic to the operation type.
#[derive(Debug, Clone, Error)]
pub struct OperationFailure {
    pub reason: String,
}

impl From<String> for OperationFailure {
    fn from(value: String) -> Self {
        OperationFailure { reason: value }
    }
}

impl std::fmt::Display for OperationFailure {
    fn fmt(&self, f: &mut std::fmt::Formatter<'_>) -> std::fmt::Result {
        write!(f, "Operation failed: {}", self.reason)
    }
}

/// Indicates a failure to connect to an MQTT server.
#[derive(Debug, Error)]
pub enum ConnectError {
    #[error(transparent)]
    Protocol(#[from] ProtocolError),
    #[error("I/O error: {0}")]
    Io(
        #[from]
        #[source]
        std::io::Error,
    ),
    #[error("connection rejected by server: {0:?}")]
    Rejected(crate::azure_mqtt::packet::ConnAck),
    #[error("timed out waiting for response packet")]
    ResponseTimeout,
}

/// Indicates a protocol violation of the MQTT specification
#[derive(Debug, Error)]
#[error(transparent)]
pub struct ProtocolError(#[from] ProtocolErrorRepr);

#[derive(Debug, Error)]
pub(crate) enum ProtocolErrorRepr {
    #[error("protocol violation: malformed packet: {0}")]
    MalformedPacket(
        #[from]
        #[source]
        crate::azure_mqtt::mqtt_proto::DecodeError,
    ),
    #[error("protocol violation: unexpected packet")]
    UnexpectedPacket,
}
