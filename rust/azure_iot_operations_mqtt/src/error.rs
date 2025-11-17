// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

//! Common error types

use std::fmt;

use thiserror::Error;

pub type ClientError = azure_mqtt::error::ClientError;
pub type CompletionError = azure_mqtt::client::token::completion::CompletionError;
pub type TopicError = azure_mqtt::topic::TopicError;

/// Error executing an MQTT publish
#[derive(Debug, Error)]
#[error("{kind}")]
pub struct ConnectionError {
    kind: ConnectionErrorKind,
}

impl ConnectionError {
    /// Create a new [`ConnectionError`]
    #[must_use]
    pub fn new(kind: ConnectionErrorKind) -> Self {
        Self { kind }
    }

    /// Return the corresponding [`ConnectionErrorKind`] for this error
    #[must_use]
    pub fn kind(&self) -> &ConnectionErrorKind {
        &self.kind
    }
}

/// An enumeration of categories of [`ConnectionError`]
#[derive(Debug)]
pub enum ConnectionErrorKind {
    Disconnected(azure_mqtt::client::DisconnectedEvent),
    Timeout,
    ConnectFailure(azure_mqtt::error::ConnectError),
}

impl fmt::Display for ConnectionErrorKind {
    fn fmt(&self, f: &mut fmt::Formatter<'_>) -> fmt::Result {
        match self {
            ConnectionErrorKind::Disconnected(event) => {
                write!(f, "client is disconnected: {event:?}")
            }
            ConnectionErrorKind::Timeout => write!(f, "connection attempt timed out"),
            ConnectionErrorKind::ConnectFailure(conn_ack) => {
                write!(f, "connection attempt failed: {:?}", conn_ack)
            }
        }
    }
}
