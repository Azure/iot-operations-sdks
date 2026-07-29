// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

//! Types for tracking the state of a [`crate::session::Session`].

use std::fmt;
use std::sync::RwLock;

use tokio::sync::Notify;

/// Information used to track the state of the Session.
pub struct SessionState {
    /// State information locked for concurrency protection
    connected: RwLock<bool>,
    /// Notifier indicating a state change
    state_change: Notify,
}

impl SessionState {
    /// Return true if the Session is currently connected (to the best of knowledge)
    pub fn is_connected(&self) -> bool {
        *self.connected.read().unwrap()
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
    pub fn transition_connected(&self) {
        // Acquire write lock for duration of method to ensure correctness of logging
        let mut connected = self.connected.write().unwrap();

        if *connected {
            // NOTE: This should never happen.
            log::warn!("Duplicate connection");
        } else {
            *connected = true;
            log::info!("Connected!");
            self.state_change.notify_waiters();
        }
        log::debug!("{:?}", *connected);
    }

    /// Update the state to reflect a disconnection
    pub fn transition_disconnected(&self) {
        // Acquire write lock for duration of method to ensure correctness of logging
        let mut connected = self.connected.write().unwrap();

        if *connected {
            *connected = false;
            self.state_change.notify_waiters();
        }
        log::debug!("{:?}", *connected);
    }
}

impl Default for SessionState {
    /// Create a new `SessionState` with default values.
    fn default() -> Self {
        Self {
            connected: RwLock::new(false),
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
