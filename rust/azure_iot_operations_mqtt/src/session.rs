// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

//! MQTT client providing a managed connection with automatic reconnection across a single MQTT session.

pub mod managed_client;
pub(crate) mod receiver;
pub mod reconnect_policy;
#[doc(hidden)]
#[allow(clippy::module_inception)]
// This isn't ideal naming, but it'd be inconsistent otherwise.
pub mod session; // TODO: Make this private and accessible via compile flags
mod state;
mod wrapper;

use std::fmt;

use thiserror::Error;

use crate::auth::SatAuthContextInitError;
use crate::error::{ConnectionError, DisconnectError};
use crate::rumqttc_adapter as adapter;
pub use wrapper::*;

/// Error describing why a [`Session`] ended prematurely
#[derive(Debug, Error)]
#[error(transparent)]
pub struct SessionError(#[from] SessionErrorRepr);

/// Internal error for [`Session`] runs.
#[derive(Error, Debug)]
enum SessionErrorRepr {
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
    /// The [`Session`] was ended by an IO error.
    #[error("{0}")]
    IoError(#[from] std::io::Error),
    /// The [`Session`] was ended by an error in the SAT auth context.
    #[error("{0}")]
    SatAuthError(#[from] SatAuthContextInitError),
}

/// Error configuring a [`Session`].
#[derive(Error, Debug)]
#[error(transparent)]
pub struct SessionConfigError(#[from] adapter::MqttAdapterError);

/// Error type for exiting a [`Session`] using the [`SessionExitHandle`].
#[derive(Error, Debug)]
#[error("{kind} (network attempt = {attempted})")]
pub struct SessionExitError {
    attempted: bool,
    kind: SessionExitErrorKind,
}

impl SessionExitError {
    /// Return the corresponding [`SessionExitErrorKind`] for this error
    #[must_use]
    pub fn kind(&self) -> SessionExitErrorKind {
        self.kind
    }

    /// Return whether a network attempt was made before the error occurred
    #[must_use]
    pub fn attempted(&self) -> bool {
        self.attempted
    }
}

impl From<DisconnectError> for SessionExitError {
    fn from(_: DisconnectError) -> Self {
        Self {
            attempted: true,
            kind: SessionExitErrorKind::Detached,
        }
    }
}

/// An enumeration of categories of [`SessionExitError`]
#[derive(Debug, Clone, Copy, Eq, PartialEq)]
pub enum SessionExitErrorKind {
    /// The exit handle was detached from the session
    Detached,
    /// The broker could not be reached
    BrokerUnavailable,
}

impl fmt::Display for SessionExitErrorKind {
    fn fmt(&self, f: &mut fmt::Formatter<'_>) -> fmt::Result {
        match self {
            SessionExitErrorKind::Detached => {
                write!(f, "Detached from Session")
            }
            SessionExitErrorKind::BrokerUnavailable => write!(f, "Could not contact broker"),
        }
    }
}
