// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

use std::path::PathBuf;
use std::sync::{Arc, Mutex};
use std::time::Duration;

use bytes::Bytes;
use notify::RecommendedWatcher;
use notify_debouncer_full::{DebounceEventResult, Debouncer, RecommendedCache, new_debouncer};
use thiserror::Error;
use tokio::sync::Notify;

use crate::control_packet::{Auth, AuthenticationInfo};

/// Used as the authentication method for the MQTT client when using SAT.
const SAT_AUTHENTICATION_METHOD: &str = "K8S-SAT";

#[async_trait::async_trait]
pub trait AuthPolicy: Send + Sync {
    /// Return the `AuthenticationInfo` to use for connecting with MQTT enhanced authentication.
    fn authentication_info(&self) -> AuthenticationInfo;

    /// Return the response authentication data to an AUTH challenge from the server.
    fn auth_challenge(&self, auth: &Auth) -> Option<Bytes>;

    /// Await notification that reauthentication should occur, returning the authentication data
    /// to send to the server.
    async fn reauth_notified(&self) -> Option<Bytes>;
}

#[derive(Debug, Error)]
pub enum SatAuthError {
    #[error("provided SAT file path is invalid")]
    InvalidPath,
    #[error("failed to read SAT file: {0}")]
    IoError(#[from] std::io::Error),
    #[error("failed to watch SAT file directory: {0}")]
    WatcherError(#[from] notify::Error),
}

pub struct SatAuthFileMonitor {
    /// The latest SAT file auth data
    latest_data: Arc<Mutex<Bytes>>,
    /// Notify indicating that the SAT file directory has changed
    dir_watch_notify: Arc<Notify>,
    /// SAT file directory watcher, held to keep the watcher alive
    #[allow(dead_code)]
    watcher: Debouncer<RecommendedWatcher, RecommendedCache>,
}

impl SatAuthFileMonitor {
    pub fn new(file_path: PathBuf) -> Result<Self, SatAuthError> {
        if !file_path.is_file() {
            Err(SatAuthError::InvalidPath)?;
        }
        let dir_path = file_path
            .parent()
            .ok_or(SatAuthError::InvalidPath)?
            .to_path_buf();

        let latest_data = Arc::new(Mutex::new(Bytes::from(std::fs::read_to_string(
            &file_path,
        )?)));
        let latest_data_c = latest_data.clone();
        let dir_watch_notify = Arc::new(Notify::new());
        let dir_watch_notify_c = dir_watch_notify.clone();

        let mut watcher = new_debouncer(
            Duration::from_secs(10),
            None,
            move |res: DebounceEventResult| {
                match res {
                    Ok(events) => {
                        if events.iter().any(|e| {
                            // Only notify on non-open events
                            !matches!(
                                e.event.kind,
                                notify::EventKind::Access(notify::event::AccessKind::Open(_))
                            )
                        }) {
                            log::debug!("SAT file change detected, updating authentication data.");
                            let new_data = match std::fs::read_to_string(&file_path) {
                                Ok(data) => Bytes::from(data),
                                Err(e) => {
                                    log::warn!("Error reading updated SAT file: {e}");
                                    log::warn!(
                                        "SAT file reading will be retried on next change/connection attempt."
                                    );
                                    return;
                                }
                            };
                            *latest_data_c.lock().unwrap() = new_data;
                            // Notify that reauthentication should occur
                            // NOTE: We use `notify_waiters` here because we only want to wake up
                            // a waiter that is currently waiting. Reauth can only happen when
                            // connected to the MQTT broker, so if the SAT file updates while the
                            // client is disconnected, nobody is listening, and the information
                            // will be irrelevant by the time someone is listening, because the
                            // next connection would already be made with this updated value.
                            dir_watch_notify_c.notify_waiters();
                        }
                    }
                    Err(e) => {
                        log::warn!("Error(s) on SAT file directory debounce event: {e:?}");
                        log::warn!(
                            "SAT file reading will be retried on next change/connection attempt."
                        );
                    }
                }
            },
        )?;
        watcher.watch(dir_path, notify::RecursiveMode::NonRecursive)?;

        Ok(Self {
            latest_data,
            dir_watch_notify,
            watcher,
        })
    }
}

#[async_trait::async_trait]
impl AuthPolicy for SatAuthFileMonitor {
    fn authentication_info(&self) -> AuthenticationInfo {
        AuthenticationInfo {
            method: SAT_AUTHENTICATION_METHOD.to_string(),
            data: Some(self.latest_data.lock().unwrap().clone()),
        }
    }

    fn auth_challenge(&self, _auth: &Auth) -> Option<Bytes> {
        log::warn!("Received unexpected AUTH challenge from broker during SAT authentication.");
        log::warn!("Responding to unexpected AUTH challenge with the same SAT token.");
        Some(self.latest_data.lock().unwrap().clone())
    }

    async fn reauth_notified(&self) -> Option<Bytes> {
        self.dir_watch_notify.notified().await;
        Some(self.latest_data.lock().unwrap().clone())
    }
}
