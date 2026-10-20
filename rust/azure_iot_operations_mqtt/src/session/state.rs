// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

//! Types for tracking the state of a [`crate::session::Session`].

use std::fmt;
use std::sync::RwLock;

use tokio::sync::Notify;

use crate::azure_mqtt::packet::ConnAck;

/// Information used to track the state of the Session.
pub struct SessionState {
    /// CONNACK of the current connection; `None` while disconnected
    connack: RwLock<Option<ConnAck>>,
    /// Notifier indicating a state change
    state_change: Notify,
}

impl SessionState {
    /// Return true if the Session is currently connected (to the best of knowledge)
    pub fn is_connected(&self) -> bool {
        self.connack.read().unwrap().is_some()
    }

    /// Return the CONNACK of the current connection, or `None` if the Session is not currently
    /// connected (to the best of knowledge)
    pub fn connack(&self) -> Option<ConnAck> {
        self.connack.read().unwrap().clone()
    }

    /// Wait until the Session is connected.
    /// Returns immediately if the Session is already connected.
    pub async fn condition_connected(&self) {
        loop {
            if self.is_connected() {
                break;
            }
            self.state_change.notified().await;
        }
    }

    /// Wait until the Session is disconnected.
    /// Returns immediately if the Session is already disconnected.
    pub async fn condition_disconnected(&self) {
        loop {
            if !self.is_connected() {
                break;
            }
            self.state_change.notified().await;
        }
    }

    /// Update the state to reflect a connection
    pub fn transition_connected(&self, connack: ConnAck) {
        // Acquire write lock for duration of method to ensure correctness of logging
        let mut connected = self.connack.write().unwrap();

        if connected.is_some() {
            // NOTE: This should never happen.
            log::warn!("Duplicate connection");
        } else {
            *connected = Some(connack);
            log::info!("Connected!");
            self.state_change.notify_waiters();
        }
        log::debug!("{:?}", connected.is_some());
    }

    /// Update the state to reflect a disconnection
    pub fn transition_disconnected(&self) {
        // Acquire write lock for duration of method to ensure correctness of logging
        let mut connected = self.connack.write().unwrap();

        if connected.is_some() {
            *connected = None;
            self.state_change.notify_waiters();
        }
        log::debug!("{:?}", connected.is_some());
    }
}

impl Default for SessionState {
    /// Create a new `SessionState` with default values.
    fn default() -> Self {
        Self {
            connack: RwLock::new(None),
            state_change: Notify::new(),
        }
    }
}

// NOTE: Do NOT log SessionState directly within it's own internal methods, at least those that
// have acquired write locks or you will deadlock. Instead, log the InnerSessionState directly.
impl fmt::Debug for SessionState {
    fn fmt(&self, f: &mut fmt::Formatter<'_>) -> fmt::Result {
        f.debug_struct("SessionState")
            .field("connected", &self.is_connected())
            .finish()
    }
}
