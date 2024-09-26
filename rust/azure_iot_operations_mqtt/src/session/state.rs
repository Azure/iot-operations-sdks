// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

//! Types for tracking the state of a [`crate::session::Session`].

use std::fmt;
use std::sync::RwLock;

use tokio::sync::Notify;

/// Information used to track the state of the Session.
pub struct SessionState {
    /// Indicates the part of the lifecycle the Session is currently in.
    lifecycle_status: RwLock<LifecycleStatus>,
    /// Indicates whether or not the Session is currently connected.
    /// Note that this is best-effort information - it may not be accurate.
    connected: RwLock<bool>,
    /// Indicates if a Session exit is desired, and if so, by whom.
    desire_exit: RwLock<DesireExit>,
    /// Notifier indicating a state change
    state_change: Notify,
}

// NOTE: There could be more methods implemented here, but they would not be used yet,
// so they are omitted for now. Add as necessary.
impl SessionState {
    /// Return true if the Session has exited.
    pub fn has_exited(&self) -> bool {
        matches!(
            *self.lifecycle_status.read().unwrap(),
            LifecycleStatus::Exited
        )
    }

    /// Return true if the Session is currently connected (to the best of knowledge)
    pub fn is_connected(&self) -> bool {
        *self.connected.read().unwrap()
    }

    /// Return true if the a Session exit is desired
    pub fn desire_exit(&self) -> bool {
        !matches!(*self.desire_exit.read().unwrap(), DesireExit::No)
    }

    /// Wait until the Session is connected.
    /// Returns immediately if the Session is already connected.
    #[allow(dead_code)]
    pub async fn condition_connected(&self) {
        loop {
            if *self.connected.read().unwrap() {
                break;
            }
            self.state_change.notified().await;
        }
    }

    /// Wait until the Session is disconnected.
    /// Returns immediately if the Session is already disconnected.
    pub async fn condition_disconnected(&self) {
        loop {
            if !*self.connected.read().unwrap() {
                break;
            }
            self.state_change.notified().await;
        }
    }

    /// Wait until the Session has exited.
    /// Returns immediately if the Session has already exited.6
    pub async fn condition_exited(&self) {
        loop {
            if self.has_exited() {
                break;
            }
            self.state_change.notified().await;
        }
    }

    /// Update the state to reflect a connection
    pub fn transition_connected(&self) {
        if self.is_connected() {
            // NOTE: I don't think this is possible, but just in case, log it.
            log::warn!("Duplicate connection");
        } else {
            *self.connected.write().unwrap() = true;
            log::info!("Connected!");
            self.state_change.notify_waiters();
        }
        log::debug!("{self}");
    }

    /// Update the state to reflect a disconnection
    pub fn transition_disconnected(&self) {
        if self.is_connected() {
            *self.connected.write().unwrap() = false;
            match *self.desire_exit.read().unwrap() {
                DesireExit::No => log::info!("Connection lost."),
                DesireExit::User => log::info!("Disconnected due to user-initiated Session exit"),
                DesireExit::SessionLogic => {
                    log::info!("Disconnected due to Session-initiated Session exit");
                }
            }
            self.state_change.notify_waiters();
        }
        log::debug!("{self}");
    }

    /// Update the state to reflect the Session is running
    pub fn transition_running(&self) {
        *self.lifecycle_status.write().unwrap() = LifecycleStatus::Running;
        self.state_change.notify_waiters();
        log::info!("Session started");
        log::debug!("{self}");
    }

    /// Update the state to reflect the Session has exited
    pub fn transition_exited(&self) {
        *self.lifecycle_status.write().unwrap() = LifecycleStatus::Exited;
        self.state_change.notify_waiters();
        log::info!("Session exited");
        log::debug!("{self}");
    }

    /// Update the state to reflect the user desires a Session exit
    pub fn transition_user_desire_exit(&self) {
        *self.desire_exit.write().unwrap() = DesireExit::User;
        self.state_change.notify_waiters();
        log::info!("User initiated Session exit process");
        log::debug!("{self}");
    }

    /// Update the state to reflect the Session logic desires a Session exit
    pub fn transition_session_desire_exit(&self) {
        *self.desire_exit.write().unwrap() = DesireExit::SessionLogic;
        self.state_change.notify_waiters();
        log::info!("Session initiated Session exit process");
        log::debug!("{self}");
    }
}

impl Default for SessionState {
    /// Create a new `SessionState` with default values.
    fn default() -> Self {
        Self {
            lifecycle_status: RwLock::new(LifecycleStatus::NotStarted),
            connected: RwLock::new(false),
            desire_exit: RwLock::new(DesireExit::No),
            state_change: Notify::new(),
        }
    }
}

impl fmt::Display for SessionState {
    fn fmt(&self, f: &mut fmt::Formatter<'_>) -> fmt::Result {
        write!(
            f,
            "SessionState {{ lifecycle_status: {:?}, connected: {:?}, desire_exit: {:?} }}",
            *self.lifecycle_status.read().unwrap(),
            *self.connected.read().unwrap(),
            *self.desire_exit.read().unwrap()
        )
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
