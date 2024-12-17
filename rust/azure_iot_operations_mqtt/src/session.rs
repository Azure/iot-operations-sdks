// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

//! MQTT client providing a managed connection with automatic reconnection across a single MQTT session.

mod dispatcher;
mod managed_client;
pub(crate) mod pub_tracker; //TODO: This should not be pub. It's needed for a stopgap AckToken implementation currently.
pub mod reconnect_policy;
#[doc(hidden)]
#[allow(clippy::module_inception)]
// This isn't ideal naming, but it'd be inconsistent otherwise.
pub mod session; // TODO: Make this private and accessible via compile flags
mod state;
mod wrapper;

use thiserror::Error;

use crate::auth::SatAuthContextInitError;
use crate::error::{ConnectionError, DisconnectError};
use crate::rumqttc_adapter as adapter;
pub use wrapper::*;

/// Error type for [`Session`]. The type of error is specified by the value of [`SessionErrorKind`].
#[derive(Debug, Error)]
#[error(transparent)]
pub struct SessionError(#[from] SessionErrorKind);

impl SessionError {
    /// Returns the [`SessionErrorKind`] of the error.
    #[must_use]
    pub fn kind(&self) -> &SessionErrorKind {
        &self.0
    }
}

/// Error kind for [`SessionError`].
#[derive(Error, Debug)]
pub enum SessionErrorKind {
    /// Invalid configuration options provided to the [`Session`].
    // TODO: Revisit how this config err is designed. Matching is strange due to the adapter error not being exposed (must use _ in match).
    // TODO: Should this be an adapter error? Would it be better to generalize it for more config errors other than just the MqttAdapterError?
    // Ideally, inner value should not be accessible, although this might not be the worst thing either, it's not uncommon for libraries to do this.
    // Also arguably, should be on a different error type entirely since it's pre-run validation.
    #[error("invalid configuration: {0}")]
    ConfigError(#[from] adapter::MqttAdapterError),
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
    /// The [`Session`] was ended by an IO error.
    #[error("{0}")]
    IoError(#[from] std::io::Error),
    /// The [`Session`] was ended by an error in the SAT auth context.
    #[error("{0}")]
    SatAuthError(#[from] SatAuthContextInitError),
}

/// Error type for exiting a [`Session`] using the [`SessionExitHandle`].
#[derive(Error, Debug)]
pub enum SessionExitError {
    /// Session was dropped before it could be exited.
    #[error("session dropped")]
    Dropped(#[from] DisconnectError),
    /// Session is not currently able to contact the broker for graceful exit.
    #[error("cannot gracefully exit session while disconnected from broker - issued attempt = {attempted}")]
    BrokerUnavailable {
        /// Indicates if a disconnect attempt was made.
        attempted: bool,
    },
    /// Attempt to exit the Session gracefully timed out.
    #[error("exit attempt timed out")]
    Timeout(#[from] tokio::time::error::Elapsed),
}
