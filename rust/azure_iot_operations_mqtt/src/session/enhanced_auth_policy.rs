// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

//! Enhanced authentication policies for a [`Session`](crate::session::Session).

use std::path::PathBuf;
use std::sync::{Arc, Mutex};
use std::time::Duration;

use bytes::Bytes;
use notify::RecommendedWatcher;
use notify_debouncer_full::{DebounceEventResult, Debouncer, RecommendedCache, new_debouncer};
use thiserror::Error;
use tokio::sync::Notify;

use crate::control_packet::{Auth, AuthenticationInfo};

/// Used as the authentication method for the MQTT client when using K8S-SAT.
const K8S_SAT_AUTHENTICATION_METHOD: &str = "K8S-SAT";

/// Trait defining interface for authentication policies for MQTT enhanced authentication.
#[async_trait::async_trait]
pub trait EnhancedAuthPolicy: Send + Sync {
    /// Return the `AuthenticationInfo` to use for connecting with MQTT enhanced authentication.
    fn authentication_info(&self) -> AuthenticationInfo;

    /// Return the response authentication data to an AUTH challenge from the server.
    fn auth_challenge(&self, auth: &Auth) -> Option<Bytes>;

    /// Await notification that reauthentication should occur, returning the authentication data
    /// to send to the server.
    async fn reauth_notified(&self) -> Option<Bytes>;
}

// TODO: Wrap error from this module.
#[derive(Debug, Error)]
/// Error configuring [`K8sSatFileMonitor`] file monitoring authentication policy.
pub enum K8sSatConfigError {
    /// The provided SAT file path is invalid.
    #[error("provided SAT file path is invalid")]
    InvalidPath,
    /// Failed to read the SAT file.
    #[error("failed to read SAT file: {0}")]
    IoError(#[from] std::io::Error),
    /// Failed to create the file watcher.
    #[error("failed to watch SAT file directory: {0}")]
    WatcherError(#[from] notify::Error),
}

/// An authentication policy that reads SAT tokens from a file in a Kubernetes pod and monitors for
/// changes.
pub struct K8sSatFileMonitor {
    /// The latest SAT file auth data
    latest_data: Arc<Mutex<Bytes>>,
    /// Notify indicating that the SAT file directory has changed
    dir_watch_notify: Arc<Notify>,
    /// SAT file directory watcher, held to keep the watcher alive
    #[allow(dead_code)]
    watcher: Debouncer<RecommendedWatcher, RecommendedCache>,
}

impl K8sSatFileMonitor {
    /// Create a new [`K8sSatFileMonitor`] that monitors the specified SAT `file_path`.
    /// `aggregation_window` specifies the aggregation window for file change events.
    ///
    /// # Errors
    /// Returns `K8sSatConfigError` if the file monitor cannot be configured
    #[allow(clippy::missing_panics_doc)] // Cannot actually panic on new
    pub fn new(
        file_path: PathBuf,
        aggregation_window: Duration,
    ) -> Result<Self, K8sSatConfigError> {
        if !file_path.is_file() {
            Err(K8sSatConfigError::InvalidPath)?;
        }
        let dir_path = file_path
            .parent()
            .ok_or(K8sSatConfigError::InvalidPath)?
            .to_path_buf();

        let latest_data = Arc::new(Mutex::new(Bytes::from(std::fs::read_to_string(
            &file_path,
        )?)));
        let latest_data_c = latest_data.clone();
        let dir_watch_notify = Arc::new(Notify::new());
        let dir_watch_notify_c = dir_watch_notify.clone();

        let mut watcher = new_debouncer(
            aggregation_window,
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
                            // connected to the MQTT server, so if the SAT file updates while the
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
impl EnhancedAuthPolicy for K8sSatFileMonitor {
    fn authentication_info(&self) -> AuthenticationInfo {
        AuthenticationInfo {
            method: K8S_SAT_AUTHENTICATION_METHOD.to_string(),
            data: Some(self.latest_data.lock().unwrap().clone()),
        }
    }

    fn auth_challenge(&self, _auth: &Auth) -> Option<Bytes> {
        log::warn!("Received unexpected AUTH challenge from server during K8S-SAT authentication.");
        log::warn!("Responding to unexpected AUTH challenge with the same SAT token.");
        Some(self.latest_data.lock().unwrap().clone())
    }

    async fn reauth_notified(&self) -> Option<Bytes> {
        self.dir_watch_notify.notified().await;
        Some(self.latest_data.lock().unwrap().clone())
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use crate::control_packet::{AuthProperties, AuthReason};
    use crate::test_utils::MockSatFile;
    use std::fs;
    use tokio_test::{assert_pending, assert_ready};

    /// Validate that the `K8sSatFileMonitor::authentication_info()` correctly returns the file contents (after aggregation window)
    #[tokio::test]
    async fn k8s_authentication_info() {
        // Set up SAT file monitor
        let mock_sat_file = MockSatFile::new();
        let aggregation_window = Duration::from_secs(1);
        let file_monitor =
            K8sSatFileMonitor::new(mock_sat_file.path().to_path_buf(), aggregation_window).unwrap();

        // Get the expected authentication info
        let contents_t1 = fs::read(mock_sat_file.path()).unwrap();
        let expected_auth_info = AuthenticationInfo {
            method: "K8S-SAT".to_string(),
            data: Some(contents_t1.clone().into()),
        };
        assert_eq!(
            file_monitor.authentication_info(),
            expected_auth_info,
            "AuthenticationInfo did not match file contents at T1."
        );

        // Update the SAT file, and wait for the aggregation window to pass
        mock_sat_file.update_contents();
        let contents_t2 = fs::read(mock_sat_file.path()).unwrap();
        assert!(
            contents_t1 != contents_t2,
            "SAT file contents should have changed after update."
        );
        tokio::time::sleep(aggregation_window + Duration::from_secs(1)).await;

        // New authentication info should reflect updated SAT file contents
        let expected_auth_info = AuthenticationInfo {
            method: "K8S-SAT".to_string(),
            data: Some(contents_t2.clone().into()),
        };
        assert_eq!(
            file_monitor.authentication_info(),
            expected_auth_info,
            "AuthenticationInfo did not match file contents at T2."
        );
    }

    /// Validate that the `K8sSatFileMonitor::reauth_notified()` notifies on file changes (after aggregation window)
    #[tokio::test]
    async fn k8s_reauth_notified() {
        // Set up SAT file monitor
        let mock_sat_file = MockSatFile::new();
        let aggregation_window = Duration::from_secs(3);
        let file_monitor =
            K8sSatFileMonitor::new(mock_sat_file.path().to_path_buf(), aggregation_window).unwrap();

        let contents_t1 = fs::read(mock_sat_file.path()).unwrap();

        // Create future to await reauth notification
        let mut reauth_notified_f = tokio_test::task::spawn(file_monitor.reauth_notified());
        // Initially, the future should be pending
        assert_pending!(reauth_notified_f.poll());

        // Update the SAT file
        mock_sat_file.update_contents();
        let contents_t2 = fs::read(mock_sat_file.path()).unwrap();
        assert!(
            contents_t1 != contents_t2,
            "SAT file contents should have changed after update"
        );

        // Reauth notification triggers with updated data
        // Should occur after aggregation window
        let start = tokio::time::Instant::now();
        let data = reauth_notified_f.await;
        let elapsed = start.elapsed();
        assert!(
            elapsed >= aggregation_window,
            "Reauth notification should not have triggered until the aggregation window passed."
        );
        assert!(
            elapsed < aggregation_window + Duration::from_secs(1),
            "Reauth notification took too long."
        );
        assert_eq!(
            data.unwrap(),
            Bytes::from(contents_t2),
            "Reauth data did not match updated SAT file contents."
        );
    }

    /// Validate that the `K8sSatFileMonitor::auth_challenge()` returns the current file contents
    #[tokio::test]
    async fn k8s_auth_challenge() {
        // Set up SAT file monitor
        let mock_sat_file = MockSatFile::new();
        let aggregation_window = Duration::from_secs(1);
        let file_monitor =
            K8sSatFileMonitor::new(mock_sat_file.path().to_path_buf(), aggregation_window).unwrap();

        // Get the expected auth data
        let contents_t1 = fs::read(mock_sat_file.path()).unwrap();
        let expected_data = Some(contents_t1.clone().into());
        let auth = Auth {
            reason: AuthReason::ContinueAuthentication,
            authentication_info: None,
            properties: AuthProperties::default(),
        };
        assert_eq!(
            file_monitor.auth_challenge(&auth),
            expected_data,
            "Authentication data did not match file contents at T1."
        );

        // Update the SAT file, and wait for the aggregation window to pass
        mock_sat_file.update_contents();
        let contents_t2 = fs::read(mock_sat_file.path()).unwrap();
        assert!(
            contents_t1 != contents_t2,
            "SAT file contents should have changed after update."
        );
        tokio::time::sleep(aggregation_window + Duration::from_secs(1)).await;

        // New authentication data should reflect updated SAT file contents
        let expected_data = Some(contents_t2.clone().into());
        assert_eq!(
            file_monitor.auth_challenge(&auth),
            expected_data,
            "Authentication data did not match file contents at T2."
        );
    }

    // Validate that multiple SAT file updates within the aggregation window are aggregated into a
    // single update.
    #[tokio::test]
    async fn k8s_aggregation_window() {
        // Set up SAT file monitor
        let mock_sat_file = MockSatFile::new();
        let aggregation_window = Duration::from_secs(3);
        let file_monitor =
            K8sSatFileMonitor::new(mock_sat_file.path().to_path_buf(), aggregation_window).unwrap();

        // Create future to await reauth notification
        let mut reauth_notified_f = tokio_test::task::spawn(file_monitor.reauth_notified());

        // Update the SAT file multiple times within the aggregation window
        // Show that each update changes the contents of the file, but the reauth notification is
        // not triggered until after the aggregation window passes, nor does the result of
        // authentication_info() or auth_challenge() change until after the aggregation window..
        let contents_t1 = fs::read(mock_sat_file.path()).unwrap();
        assert_pending!(reauth_notified_f.poll());
        let expected_authentication_info = AuthenticationInfo {
            method: "K8S-SAT".to_string(),
            data: Some(contents_t1.clone().into()),
        };
        let auth = Auth {
            reason: AuthReason::ContinueAuthentication,
            authentication_info: None,
            properties: AuthProperties::default(),
        };
        let expected_data = Some(contents_t1.clone().into());

        tokio::time::sleep(Duration::from_secs(1)).await;
        mock_sat_file.update_contents();
        let contents_t2 = fs::read(mock_sat_file.path()).unwrap();
        assert_ne!(contents_t1, contents_t2);
        assert_eq!(
            file_monitor.authentication_info(),
            expected_authentication_info
        );
        assert_eq!(file_monitor.auth_challenge(&auth), expected_data);
        assert_pending!(reauth_notified_f.poll());

        tokio::time::sleep(Duration::from_secs(1)).await;
        mock_sat_file.update_contents();
        let contents_t3 = fs::read(mock_sat_file.path()).unwrap();
        assert_ne!(contents_t2, contents_t3);
        assert_eq!(
            file_monitor.authentication_info(),
            expected_authentication_info
        );
        assert_eq!(file_monitor.auth_challenge(&auth), expected_data);
        assert_pending!(reauth_notified_f.poll());

        tokio::time::sleep(Duration::from_secs(1)).await;
        mock_sat_file.update_contents();
        let contents_t4 = fs::read(mock_sat_file.path()).unwrap();
        assert_ne!(contents_t3, contents_t4);
        assert_eq!(
            file_monitor.authentication_info(),
            expected_authentication_info
        );
        assert_eq!(file_monitor.auth_challenge(&auth), expected_data);
        assert_pending!(reauth_notified_f.poll());

        tokio::time::sleep(Duration::from_secs(2)).await;
        let data = assert_ready!(reauth_notified_f.poll());
        assert_eq!(
            data,
            Some(Bytes::from(contents_t4.clone())),
            "Reauth data did not match final SAT file contents after aggregation window."
        );
        assert_eq!(
            file_monitor.authentication_info(),
            AuthenticationInfo {
                method: "K8S-SAT".to_string(),
                data: Some(contents_t4.clone().into()),
            },
            "AuthenticationInfo did not match final SAT file contents after aggregation window."
        );
        assert_eq!(
            file_monitor.auth_challenge(&auth),
            Some(contents_t4.into()),
            "Authentication data did not match final SAT file contents after aggregation window."
        );
    }
}
