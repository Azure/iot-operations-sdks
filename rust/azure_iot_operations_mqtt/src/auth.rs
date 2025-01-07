// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

//! Internal module for handling authentication in the Azure IoT Operations MQTT library.

use std::sync::Arc;
use std::{path::Path, time::Duration};

use notify::RecommendedWatcher;
use notify_debouncer_full::{new_debouncer, RecommendedCache};
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
    /// SAT file watcher, held to keep the watcher alive
    #[allow(dead_code)]
    watcher: Option<notify_debouncer_full::Debouncer<RecommendedWatcher, RecommendedCache>>,
    /// Notifier for SAT file changes
    pub file_watcher_notify: Arc<Notify>,
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
        // Create a watcher notifier
        let file_watcher_notify = Arc::new(Notify::new());
        let file_watcher_notify_clone = file_watcher_notify.clone();

        // Create a SAT file watcher
        let watcher = match new_debouncer(
            Duration::from_secs(10),
            None,
            move |res: Result<Vec<notify_debouncer_full::DebouncedEvent>, Vec<notify::Error>>| {
                match res {
                    Ok(events) => {
                        if events.iter().any(|e| {
                            // Only notify on events that are not file open events
                            !matches!(
                                e.event.kind,
                                notify::EventKind::Access(notify::event::AccessKind::Open(_))
                            )
                        }) {
                            file_watcher_notify_clone.notify_one();
                        }
                    }
                    Err(err) => {
                        log::error!("Error reading SAT file: {err:?}");
                    }
                }
            },
        ) {
            Ok(mut debouncer) => {
                // Start watching the SAT file
                debouncer
                    .watch(
                        Path::new(&file_location),
                        notify::RecursiveMode::NonRecursive,
                    )
                    .map_err(SatAuthContextInitError::from)?;
                Some(debouncer)
            }
            Err(e) => {
                log::error!("Error creating SAT file watcher: {e:?}");
                return Err(SatAuthContextInitError::from(e));
            }
        };

        Ok(Self {
            file_location,
            watcher,
            file_watcher_notify,
            auth_watcher_rx,
        })
    }

    /// Wait for SAT file changes.
    pub async fn notified(&self) {
        self.file_watcher_notify.notified().await;
    }

    /// Drain the SAT file watcher notification.
    pub async fn drain_notify(&self) {
        self.file_watcher_notify.notify_one();
        self.file_watcher_notify.notified().await;
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
