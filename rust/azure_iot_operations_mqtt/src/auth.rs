// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

//! Internal module for handling authentication in the Azure IoT Operations MQTT library.

use std::sync::Arc;
use std::{path::Path, time::Duration};

use notify::RecommendedWatcher;
use notify_debouncer_full::{RecommendedCache, new_debouncer};
use rumqttc::v5::mqttbytes::v5::{AuthProperties, AuthReasonCode};
use thiserror::Error;
use tokio::sync::Notify;

use crate::error::ReauthError;
use crate::interface::MqttClient;

/// Used as the authentication method for the MQTT client when using SAT.
pub const SAT_AUTHENTICATION_METHOD: &str = "K8S-SAT";

/// Error type for initializing the SAT auth context. The type of error is specified by the value of [`SatAuthContextInitError`].
#[derive(Debug, Error)]
pub enum SatAuthContextInitError {
    /// I/O error occurred while reading the SAT token file.
    #[error("{0}")]
    IoError(#[from] std::io::Error),
    /// Error occurred while creating the SAT auth context.
    #[error("{0}")]
    WatcherError(#[from] notify::Error),
    /// No SAT file found.
    #[error("No SAT file found")]
    NoSatFile,
}

/// Error type for reauthenticating the client using the SAT token. The type of error is specified by the value of [`SatReauthError`].
#[derive(Debug, Error)]
pub enum SatReauthError {
    /// I/O error occurred while reading the SAT token file.
    #[error("{0}")]
    IoError(#[from] std::io::Error),
    /// Reauth timed out.
    #[error("Reauth timed out")]
    Timeout,
    /// Reauth was not successful.
    #[error("Reauth failed with reason: {0:?}")]
    ReauthUnsuccessful(AuthReasonCode),
    /// Error occurred while reauthenticating the client.
    #[error("{0}")]
    ClientReauthError(#[from] ReauthError),
    /// Reauth channel closed.
    #[error("Auth watcher channel closed")]
    AuthWatcherClosed,
}

/// Context for maintaining SAT token authentication.
pub struct SatAuthContext {
    /// File path to the SAT token
    file_location: String,
    /// SAT file's directory watcher, held to keep the watcher alive
    #[allow(dead_code)]
    watcher: Option<notify_debouncer_full::Debouncer<RecommendedWatcher, RecommendedCache>>,
    /// Notifier for changes in the SAT file's directory
    pub directory_watcher_notify: Arc<Notify>,
    /// Channel for receiving auth change notifications
    auth_watcher_rx: tokio::sync::mpsc::UnboundedReceiver<AuthReasonCode>,
}

impl SatAuthContext {
    /// Create a new SAT auth context.
    ///
    /// Returns a [`SatAuthContext`] instance. If an error occurs, a [`SatAuthContextInitError`] is returned.
    pub fn new(
        file_location: String,
        auth_watcher_rx: tokio::sync::mpsc::UnboundedReceiver<AuthReasonCode>,
    ) -> Result<Self, SatAuthContextInitError> {
        let file_location_path = Path::new(&file_location);

        // Check that the specified SAT token file exists
        if !file_location_path.is_file() {
            return Err(SatAuthContextInitError::NoSatFile);
        }

        let Some(parent_path) = Path::new(&file_location).parent() else {
            // Should never happen, as we already checked that the file exists
            return Err(SatAuthContextInitError::NoSatFile);
        };

        // Create a watcher notifier
        let directory_watcher_notify = Arc::new(Notify::new());
        let directory_watcher_notify_clone = directory_watcher_notify.clone();

        // Create a SAT directory watcher
        let watcher = match new_debouncer(
            Duration::from_secs(10),
            None,
            move |res: Result<Vec<notify_debouncer_full::DebouncedEvent>, Vec<notify::Error>>| {
                match res {
                    Ok(events) => {
                        if events.iter().any(|e| {
                            // Only notify on non-open events
                            !matches!(
                                e.event.kind,
                                notify::EventKind::Access(notify::event::AccessKind::Open(_))
                            )
                        }) {
                            directory_watcher_notify_clone.notify_one();
                        }
                    }
                    Err(err) => {
                        log::error!("Error reading SAT directory: {err:?}");
                    }
                }
            },
        ) {
            Ok(mut debouncer) => {
                // Start watching the SAT file's parent directory
                debouncer
                    .watch(Path::new(&parent_path), notify::RecursiveMode::NonRecursive)
                    .map_err(SatAuthContextInitError::from)?;
                Some(debouncer)
            }
            Err(e) => {
                log::error!("Error creating SAT file's directory watcher: {e:?}");
                return Err(SatAuthContextInitError::from(e));
            }
        };

        Ok(Self {
            file_location,
            watcher,
            directory_watcher_notify,
            auth_watcher_rx,
        })
    }

    /// Wait for changes in SAT file's directory.
    pub async fn notified(&self) {
        self.directory_watcher_notify.notified().await;
    }
    /// Drain the SAT file's directory watcher notification.
    pub async fn drain_notify(&self) {
        self.directory_watcher_notify.notify_one();
        self.directory_watcher_notify.notified().await;
    }

    /// Re-authenticate the client using the SAT token.
    ///
    /// Returns `Ok(())` if re-authentication is successful. If an error occurs or reauthentication was unsuccessful, a [`SatReauthError`] is returned.
    pub async fn reauth(
        &mut self,
        timeout: Duration,
        client: &impl MqttClient,
    ) -> Result<(), SatReauthError> {
        // Get SAT token
        let sat_token =
            std::fs::read_to_string(&self.file_location).map_err(SatReauthError::from)?;

        let props = AuthProperties {
            method: Some(SAT_AUTHENTICATION_METHOD.to_string()),
            data: Some(sat_token.into()),
            reason: None,
            user_properties: Vec::new(),
        };

        // Re-authenticate the client
        client.reauth(props).await.map_err(SatReauthError::from)?;

        // Wait for next auth change
        tokio::select! {
            auth = self.auth_watcher_rx.recv() => {
                match auth {
                    Some(AuthReasonCode::Success) => Ok(()),
                    Some(rc) => Err(SatReauthError::ReauthUnsuccessful(rc)),
                    None => Err(SatReauthError::AuthWatcherClosed),
                }
            }
            () = tokio::time::sleep(timeout) => Err(SatReauthError::Timeout),
        }
    }
}
