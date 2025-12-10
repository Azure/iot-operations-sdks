// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

//! Common error types

/// Indicates a component has become detached from the Session.
pub type DetachedError = crate::azure_mqtt::error::DetachedError;
/// Indicates that an MQTT operation has failed to complete successfully.
pub type CompletionError = crate::azure_mqtt::client::token::completion::CompletionError;
/// Error connecting to an MQTT server.
pub type ConnectError = crate::azure_mqtt::error::ConnectError;
/// Error indicating a violation of the MQTT protocol.
pub type ProtocolError = crate::azure_mqtt::error::ProtocolError;
/// Error related to MQTT topic names or filters.
pub type TopicError = crate::azure_mqtt::topic::TopicError;

pub use crate::session::{
    SessionConfigError, SessionError, SessionErrorKind, SessionExitError, SessionExitErrorKind,
};
