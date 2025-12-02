// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

#![allow(dead_code)] // TODO: remove

//! Types for tracking the state of a [`crate::session::Session`].

use std::fmt;
use std::sync::RwLock;

use tokio::sync::Notify;

/// Information used to track the state of the Session.
pub struct SessionState {
    /// State information locked for concurrency protection
    //state: RwLock<InnerSessionState>,
    connected: RwLock<bool>,
    /// Notifier indicating a state change
    state_change: Notify,
}

// /// The inner state containing the actual state data.
// struct InnerSessionState {
//     /// Indicates the part of the lifecycle the Session is currently in.
//     lifecycle_status: LifecycleStatus,
//     /// Indicates whether or not the Session is currently connected.
//     /// Note that this is best-effort information - it may not be accurate.
//     connected: bool,
//     /// Indicates if a Session exit is desired, and if so, by whom.
//     desire_exit: DesireExit,
// }

// NOTE: There could be more methods implemented here, but they would not be used yet,
// so they are omitted for now. Add as necessary.
impl SessionState {
    /// Return true if the Session is currently connected (to the best of knowledge)
    pub fn is_connected(&self) -> bool {
        *self.connected.read().unwrap()
    }

    /// Wait until the Session is connected.
    /// Returns immediately if the Session is already connected.
    #[allow(dead_code)]
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
            match state.desire_exit {
                DesireExit::No => log::info!("Connection lost."),
                DesireExit::User => log::info!("Disconnected due to user-initiated Session exit"),
                DesireExit::SessionLogic => {
                    log::info!("Disconnected due to session-initiated Session exit");
                }
            }
            self.state_change.notify_waiters();
        }
        log::debug!("{state:?}");
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
        // Acquire lock on inner state, and then just defer to the Debug implementation there.
        let state = self.state.read().unwrap();
        fmt::Debug::fmt(&state, f)
    }
}

impl fmt::Debug for InnerSessionState {
    fn fmt(&self, f: &mut fmt::Formatter<'_>) -> fmt::Result {
        // Even though this is the inner state, represent it as the outer state, since this is
        // the state for all intents and purposes.
        f.debug_struct("SessionState")
            .field("lifecycle_status", &self.lifecycle_status)
            .field("connected", &self.connected)
            .field("desire_exit", &self.desire_exit)
            .finish()
    }
}

/// Enum indicating the part of the lifecycle the Session is currently in.
#[derive(Debug)]
enum LifecycleStatus {
    /// Indicates the Session has not yet started.
    NotStarted,
    /// Indicates the Session is currently running.
    Running,
    /// Indicates the Session has exited.
    Exited,
}

#[derive(Debug)]
/// Enum indicating if and why the Session should end from the client-side.
enum DesireExit {
    /// Indicates no desire for Session exit.
    No,
    /// Indicates the user has requested Session exit.
    User,
    /// Indicates the Session logic has requested Session exit.
    SessionLogic,
}
