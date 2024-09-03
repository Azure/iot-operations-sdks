// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

mod dispatcher;
pub mod internal; // TODO: Make this private and accessible via compile flags
mod pub_tracker;
pub mod reconnect_policy;
mod wrapper;

pub use thiserror::Error;

use crate::error::ConnectionError;
use crate::rumqttc_adapter as adapter;
pub use wrapper::*;

/// Error type for sessions
/// TODO: Flesh this out. This is a placeholder for now.
#[derive(Debug, Error)]
#[error(transparent)]
pub struct SessionError(#[from] SessionErrorKind);

#[derive(Error, Debug)]
pub enum SessionErrorKind {
    #[error("invalid configuration: {0}")]
    ConfigError(#[from] adapter::ConnectionSettingsAdapterError),
    #[error("session state not present on broker after reconnect")]
    SessionLost,
    #[error(transparent)]
    ConnectionError(#[from] ConnectionError),
    #[error("reconnection halted by reconnect policy")]
    ReconnectHalted,
    #[error("{0}")]
    InvalidState(String),
}
