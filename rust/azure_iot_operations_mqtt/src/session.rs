// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

//! MQTT client providing a managed connection with automatic reconnection across a single MQTT session.
//!
//! This module provides several key components for using an MQTT session:
//! * [`Session`] - Manages the lifetime of the MQTT session
//! * [`SessionManagedClient`] - Sends MQTT messages to the broker
//! * [`SessionPubReceiver`] - Receives MQTT messages from the broker
//! * [`SessionConnectionMonitor`] - Provides information about MQTT connection state
//! * [`SessionExitHandle`] - Allows the user to exit the session gracefully
//!
//! # [`Session`] lifespan
//! Each instance of [`Session`] is single use - after configuring a [`Session`], and creating any
//! other necessary components from it, calling the [`run`](crate::session::Session::run) method
//! will consume the [`Session`] and block (asynchronously) until the MQTT session shared between
//! client and broker ends. Note that a MQTT session can span multiple connects and disconnects to
//! the broker.
//!
//! The MQTT session can be ended one of three ways:
//! 1. The MQTT broker ends the MQTT session
//! 2. The [`ReconnectPolicy`](crate::session::reconnect_policy::ReconnectPolicy) configured on the
//!    [`Session`] halts reconnection attempts, causing the [`Session`] to end the MQTT session.
//! 3. The user uses the [`SessionExitHandle`] to end the MQTT session.
//!    <div class="warning">The SessionExitHandle currently only causes the exit of the Session client
//!    not the end of the MQTT session shared with the broker. This limitation will be fixed in future
//!    updates.</div>
//!
//! # Sending and receiving data over MQTT
//! A [`Session`] can be used to create a [`SessionManagedClient`] for sending data (i.e. outgoing
//! MQTT PUBLISH, MQTT SUBSCRIBE, MQTT UNSUBSCRIBE), and can in turn be used to create a
//! [`SessionPubReceiver`] for receiving incoming data (i.e. incoming MQTT PUBLISH).
//!
//! [`SessionPubReceiver`]s can be either filtered or unfiltered - a filtered receiver will only
//! receive messages that match a specific topic filter, while an unfiltered receiver will receive
//! all messages that do not match another existing filter.
//!
//! Note that in order to receive incoming data, you must both subscribe to the topic filter of
//! interest using the [`SessionManagedClient`] and create a [`SessionPubReceiver`] (filtered or
//! unfiltered). If an incoming message is received that
//! does not match any [`SessionPubReceiver`]s, it will be acknowledged to the MQTT broker and
//! discarded. Thus, in order to guarantee that messages will not be lost, you should create the
//! [`SessionPubReceiver`] *before* subscribing to the topic filter.

pub mod managed_client; // TODO: This really ought be private, but we need it public for testing
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
