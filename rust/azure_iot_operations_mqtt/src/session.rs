// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

//! MQTT client providing a managed connection with automatic reconnection across a single MQTT session.

mod dispatcher;
#[doc(hidden)]
pub mod internal; // TODO: Make this private and accessible via compile flags
mod pub_tracker;
pub mod reconnect_policy;
mod wrapper;

use thiserror::Error;

use crate::error::{ConnectionError, ClientError};
use crate::rumqttc_adapter as adapter;
pub use wrapper::*;

/// Error type for [`Session`]. The type of error is specified by the value of [`SessionErrorKind`].
#[derive(Debug, Error)]
#[error(transparent)]
pub struct SessionError(#[from] SessionErrorKind);

//TODO: arguably, ConfigError and ExitError types should be a part of a separate enum
/// Error kind for [`SessionError`].
#[derive(Error, Debug)]
pub enum SessionErrorKind {
    /// Invalid configuration options provided to the [`Session`].
    #[error("invalid configuration: {0}")]
    ConfigError(#[from] adapter::ConnectionSettingsAdapterError),
    /// MQTT session was lost due to a connection error.
    #[error("session state not present on broker after reconnect")]
    SessionLost,
    /// MQTT session was ended due to an unrecoverable connection error
    #[error(transparent)]
    ConnectionError(#[from] ConnectionError),
    /// Reconnect attempts were halted by the reconnect policy, ending the MQTT session
    #[error("reconnection halted by reconnect policy")]
    ReconnectHalted,
    /// The [`Session`] was ended by a user-initiated force exit. The broker may still retain the MQTT session.
    #[error("session ended by force exit")]
    ForceExit,
    /// The [`Session`] ended up in an invalid state.
    #[error("{0}")]
    InvalidState(String),
}

/// Error type for exiting a [`Session`] using the [`SessionExitHandle`].
#[derive(Error, Debug)]
pub enum SessionExitError {
    /// Session was dropped before it could be exited.
    #[error("session dropped")]
    Dropped(#[from] ClientError),
    /// Session is not currently able to contact the broker for graceful exit.
    #[error("cannot gracefully exit session while disconnected from broker - issued attempt = {attempted}")]
    BrokerUnavailable{ 
        /// Indicates if a disconnect attempt was made.
        attempted: bool
    },
    /// Attempt to exit the Session gracefully timed out.
    #[error("exit attempt timed out")]
    Timeout(#[from] tokio::time::error::Elapsed),
}