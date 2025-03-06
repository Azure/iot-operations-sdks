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


// #[derive(Error, Debug)]
// #[error(transparent)]
// pub struct SessionExitError(#[from] SessionExitErrorRepr);

// impl SessionExitError {
//     // should retry?

//     // pub fn is_fatal(&self) -> bool {
//     //     matches!(self.0, SessionExitErrorRepr::Dropped(_))
//     // }

//     pub fn should_retry(&self) -> bool {
//         matches!(self.0, SessionExitErrorRepr::BrokerUnavailable { .. })
//     }
// }


// /// Error type for exiting a [`Session`] using the [`SessionExitHandle`].
// #[derive(Error, Debug)]
// pub enum SessionExitErrorRepr {
//     /// Session was dropped before it could be exited.
//     #[error("session dropped")]
//     Dropped(#[from] DisconnectError),
//     /// Session is not currently able to contact the broker for graceful exit.
//     #[error("cannot gracefully exit session while disconnected from broker - issued attempt = {attempted}")]
//     BrokerUnavailable {
//         /// Indicates if a disconnect attempt was made.
//         attempted: bool,
//     },
//     /// Attempt to exit the Session gracefully timed out.
//     #[error("exit attempt timed out")]
//     Timeout(#[from] tokio::time::error::Elapsed),
// }

#[derive(Error, Debug)]
#[error("{kind} (network attempt = {attempted})")]
pub struct SessionExitError {
    attempted: bool,
    kind: SessionExitErrorKind,
}

impl SessionExitError {
    pub fn kind(&self) -> SessionExitErrorKind {
        self.kind
    }

    pub fn attempted(&self) -> bool {
        self.attempted
    }
}

#[derive(Debug, Clone, Copy, Eq, PartialEq)]
pub enum SessionExitErrorKind {
    Detached,
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