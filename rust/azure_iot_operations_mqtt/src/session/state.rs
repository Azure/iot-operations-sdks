// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

//! Types for tracking the state of a [`crate::session::Session`].

use std::fmt;
use std::sync::RwLock;

use tokio::sync::Notify;

/// Information used to track the state of the Session.
pub struct SessionState {
    /// State information locked for concurrency protection
    state: RwLock<InnerSessionState>,
    /// Notifier indicating a state change
    state_change: Notify,
}

/// The inner state containing the actual state data.
struct InnerSessionState {
    /// Indicates the part of the lifecycle the Session is currently in.
    lifecycle_status: LifecycleStatus,
    /// Indicates whether or not the Session is currently connected.
    /// Note that this is best-effort information - it may not be accurate.
    connected: bool,
    /// Indicates if a Session exit is desired, and if so, by whom.
    desire_exit: DesireExit,
}

// NOTE: There could be more methods implemented here, but they would not be used yet,
// so they are omitted for now. Add as necessary.
impl SessionState {
    /// Return true if the Session has exited.
    pub fn has_exited(&self) -> bool {
        matches!(
            self.state.read().unwrap().lifecycle_status,
            LifecycleStatus::Exited
        )
    }

    /// Return true if the Session is currently connected (to the best of knowledge)
    pub fn is_connected(&self) -> bool {
        self.state.read().unwrap().connected
    }

    /// Return true if the a Session exit is desired
    pub fn desire_exit(&self) -> bool {
        !matches!(self.state.read().unwrap().desire_exit, DesireExit::No)
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

    /// Wait until the Session has exited.
    /// Returns immediately if the Session has already exited.
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
        // Acquire write lock for duration of method to ensure correctness of logging
        let mut state = self.state.write().unwrap();

        if state.connected {
            // NOTE: I don't think this is possible, but just in case, log it.
            log::warn!("Duplicate connection");
        } else {
            state.connected = true;
            log::info!("Connected!");
            self.state_change.notify_waiters();
        }
        log::debug!("{state:?}");
    }

    /// Update the state to reflect a disconnection
    pub fn transition_disconnected(&self) {
        // Acquire write lock for duration of method to ensure correctness of logging
        let mut state = self.state.write().unwrap();

        if state.connected {
            state.connected = false;
            match state.desire_exit {
                DesireExit::No => log::info!("Connection lost."),
                DesireExit::User => log::info!("Disconnected due to user-initiated Session exit"),
                DesireExit::SessionLogic => {
                    log::info!("Disconnected due to Session-initiated Session exit");
                }
            }
            self.state_change.notify_waiters();
        }
        log::debug!("{state:?}");
    }

    /// Update the state to reflect the Session is running
    pub fn transition_running(&self) {
        let mut state = self.state.write().unwrap();
        state.lifecycle_status = LifecycleStatus::Running;
        self.state_change.notify_waiters();
        log::info!("Session started");
        log::debug!("{state:?}");
    }

    /// Update the state to reflect the Session has exited
    pub fn transition_exited(&self) {
        let mut state = self.state.write().unwrap();
        state.lifecycle_status = LifecycleStatus::Exited;
        self.state_change.notify_waiters();
        log::info!("Session exited");
        log::debug!("{state:?}");
    }

    /// Update the state to reflect the user desires a Session exit
    pub fn transition_user_desire_exit(&self) {
        let mut state = self.state.write().unwrap();
        state.desire_exit = DesireExit::User;
        self.state_change.notify_waiters();
        log::info!("User initiated Session exit process");
        log::debug!("{state:?}");
    }

    /// Update the state to reflect the Session logic desires a Session exit
    pub fn transition_session_desire_exit(&self) {
        let mut state = self.state.write().unwrap();
        state.desire_exit = DesireExit::SessionLogic;
        self.state_change.notify_waiters();
        log::info!("Session initiated Session exit process");
        log::debug!("{state:?}");
    }
}

impl Default for SessionState {
    /// Create a new `SessionState` with default values.
    fn default() -> Self {
        Self {
            state: RwLock::new(InnerSessionState::default()),
            state_change: Notify::new(),
        }
    }
}

impl Default for InnerSessionState {
    fn default() -> Self {
        Self {
            lifecycle_status: LifecycleStatus::NotStarted,
            connected: false,
            desire_exit: DesireExit::No,
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
