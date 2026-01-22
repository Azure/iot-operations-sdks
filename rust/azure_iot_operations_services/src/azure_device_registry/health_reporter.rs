// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

//! Background health reporting for Azure Device Registry components.
//!
//! This module provides infrastructure for reporting runtime health status of various
//! Azure Device Registry components (device endpoints, datasets, events, streams, and
//! management actions) with automatic deduplication and periodic re-reporting.
//!
//! # Overview
//!
//! The health reporting system consists of:
//! - [`HealthReporter`] trait - Implement this for custom health reporting components.
//! - [`new_health_reporter`] - Spawns a background task that handles health reporting.
//! - [`HealthReporterSender`] - Handle to send health events to the background task.
//! - Convenience methods on [`Client`](super::Client) for creating health reporters:
//!   - [`Client::new_device_endpoint_health_reporter`](super::Client::new_device_endpoint_health_reporter)
//!   - [`Client::new_dataset_health_reporter`](super::Client::new_dataset_health_reporter)
//!   - [`Client::new_event_health_reporter`](super::Client::new_event_health_reporter)
//!   - [`Client::new_stream_health_reporter`](super::Client::new_stream_health_reporter)
//!   - [`Client::new_management_action_health_reporter`](super::Client::new_management_action_health_reporter)
//!
//! # Example
//!
//! ```ignore
//! use azure_iot_operations_services::azure_device_registry::{
//!     Client, HealthStatus, RuntimeHealth,
//!     health_reporter::ReportInterval,
//!     models::DeviceRef,
//! };
//! use tokio_util::sync::CancellationToken;
//! use std::time::Duration;
//!
//! let device_ref = DeviceRef {
//!     device_name: "device-name".to_string(),
//!     endpoint_name: "endpoint-name".to_string(),
//! };
//!
//! let cancellation_token = CancellationToken::new();
//!
//! // Create a background health reporter using the Client convenience method
//! let sender = client.new_device_endpoint_health_reporter(
//!     device_ref,
//!     Duration::from_secs(30), // message_expiry
//!     ReportInterval::default(), // report_interval (10 minutes)
//!     cancellation_token,
//! );
//!
//! // Report health status - the background task handles deduplication.
//! // When available, neither message nor reason_code should be set.
//! sender.report(RuntimeHealth {
//!     version: 1,
//!     status: HealthStatus::Available,
//!     message: None,
//!     reason_code: None,
//!     last_update_time: chrono::Utc::now(),
//! });
//! ```

use std::future::Future;
use std::ops::Add;
use std::time::Duration;

use chrono::{DateTime, Utc};
use tokio::sync::mpsc::{UnboundedReceiver, UnboundedSender};
use tokio_util::sync::CancellationToken;

use super::models::{
    DatasetRuntimeHealthEvent, DeviceRef, EventRuntimeHealthEvent,
    ManagementActionRuntimeHealthEvent, StreamRuntimeHealthEvent,
};
use super::{AssetRef, Client, Error, RuntimeHealth};

// ============= ReportInterval Type =============

/// Interval for periodic health re-reporting.
///
/// Must be at least 1 minute ([`ReportInterval::MIN`]). Defaults to 10 minutes.
///
/// # Example
///
/// ```
/// use std::time::Duration;
/// use azure_iot_operations_services::azure_device_registry::health_reporter::ReportInterval;
///
/// // Use the default (10 minutes)
/// let interval = ReportInterval::default();
///
/// // Create a custom interval (must be >= 1 minute)
/// let interval = ReportInterval::new(Duration::from_secs(120)).unwrap();
///
/// // Panics if interval is below minimum
/// let interval = ReportInterval::new_unchecked(Duration::from_secs(120));
/// ```
#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub struct ReportInterval(Duration);

impl ReportInterval {
    /// Minimum allowed interval (1 minute).
    pub const MIN: Duration = Duration::from_secs(60);

    /// Default interval (10 minutes).
    const DEFAULT: Duration = Duration::from_secs(600);

    /// Creates a new [`ReportInterval`] with the specified duration.
    ///
    /// Returns `None` if duration is less than [`Self::MIN`].
    ///
    /// # Arguments
    /// * `duration` - The interval duration.
    #[must_use]
    pub fn new(duration: Duration) -> Option<Self> {
        if duration < Self::MIN {
            None
        } else {
            Some(Self(duration))
        }
    }

    /// Creates a new [`ReportInterval`] with the specified duration, panicking if invalid.
    ///
    /// # Panics
    /// Panics if duration is less than [`Self::MIN`].
    ///
    /// # Arguments
    /// * `duration` - The interval duration.
    #[must_use]
    pub fn new_unchecked(duration: Duration) -> Self {
        Self::new(duration).expect("report interval must be at least 1 minute")
    }
}

impl Default for ReportInterval {
    fn default() -> Self {
        Self(Self::DEFAULT)
    }
}

impl From<ReportInterval> for Duration {
    fn from(interval: ReportInterval) -> Duration {
        interval.0
    }
}

// ============= HealthReporterError Type =============

/// Error returned when sending to the health reporter fails.
///
/// This occurs when the background health reporter task has stopped,
/// either due to cancellation or because all senders were dropped.
#[derive(Debug)]
pub struct HealthReporterError(tokio::sync::mpsc::error::SendError<Option<RuntimeHealth>>);

impl std::fmt::Display for HealthReporterError {
    fn fmt(&self, f: &mut std::fmt::Formatter<'_>) -> std::fmt::Result {
        write!(f, "health reporter channel closed")
    }
}

impl std::error::Error for HealthReporterError {
    fn source(&self) -> Option<&(dyn std::error::Error + 'static)> {
        Some(&self.0)
    }
}

/// Trait for components that can report health events to the Azure Device Registry service.
///
/// Implement this trait for your component type, then use [`new_health_reporter`]
/// to create a background task that handles periodic re-reporting and deduplication.
pub trait HealthReporter: Send + Sync + 'static {
    /// Reports a health status to the Azure Device Registry service.
    ///
    /// # Arguments
    /// * `status` - The runtime health status to report.
    ///
    /// # Errors
    /// Returns an error if the health report fails to be sent.
    fn report(&self, status: RuntimeHealth) -> impl Future<Output = Result<(), Error>> + Send;

    /// Returns a descriptive name for this component, used in log messages.
    fn component_name(&self) -> String;
}

/// Handle to send health events to the background reporter task.
#[derive(Clone, Debug)]
pub struct HealthReporterSender {
    tx: UnboundedSender<Option<RuntimeHealth>>,
}

impl HealthReporterSender {
    /// Sends a health event to be reported.
    ///
    /// The background task will handle deduplication and periodic re-reporting.
    /// Duplicate events (same version, status, message, and reason code) are
    /// skipped until the next reporting interval.
    ///
    /// # Arguments
    /// * `status` - The runtime health status to report.
    ///
    /// # Errors
    /// Returns [`HealthReporterError`] if the background task has stopped.
    pub fn report(&self, status: RuntimeHealth) -> Result<(), HealthReporterError> {
        self.tx.send(Some(status)).map_err(HealthReporterError)
    }

    /// Pauses background reporting until a new event is reported.
    ///
    /// Use this during reconfiguration when the previous health state may no longer
    /// be valid. The background task will not re-report until a new status is sent.
    ///
    /// # Errors
    /// Returns [`HealthReporterError`] if the background task has stopped.
    pub fn pause(&self) -> Result<(), HealthReporterError> {
        self.tx.send(None).map_err(HealthReporterError)
    }
}

/// Spawns a background health reporter task.
///
/// The background task handles:
/// - Deduplication of identical health events
/// - Periodic re-reporting at the configured interval
///
/// # Arguments
/// * `reporter` - The health reporter implementation to use.
/// * `report_interval` - Interval for re-reporting steady-state health when no changes occur.
/// * `cancellation_token` - Token to signal cancellation of the background task.
///
/// Returns a [`HealthReporterSender`] handle. The background task runs until:
/// - The cancellation token is cancelled, OR
/// - All senders are dropped (channel closes)
#[must_use]
pub fn new_health_reporter<R: HealthReporter>(
    reporter: R,
    report_interval: ReportInterval,
    cancellation_token: CancellationToken,
) -> HealthReporterSender {
    let (tx, rx) = tokio::sync::mpsc::unbounded_channel();

    tokio::spawn(health_reporter_task(
        reporter,
        report_interval.into(),
        rx,
        cancellation_token,
    ));

    HealthReporterSender { tx }
}

/// The background task that handles health reporting.
async fn health_reporter_task<R: HealthReporter>(
    reporter: R,
    report_interval: Duration,
    mut rx: UnboundedReceiver<Option<RuntimeHealth>>,
    cancellation_token: CancellationToken,
) {
    // Latest status from the application (whether reported or not). None if background reporting
    // shouldn't be happening
    let mut current_status: Option<RuntimeHealth> = None;
    // Time of the last successfully reported status, or None if background reporting is paused
    let mut last_reported_time: Option<DateTime<Utc>> = None;

    loop {
        tokio::select! {
            biased;
            // Check for cancellation first (highest priority)
            () = cancellation_token.cancelled() => {
                log::debug!("Health reporter task cancelled for {}", reporter.component_name());
                break;
            }
            // passes in the next time that a report should happen in case this doesn't free up to
            // allow the sleep branch to complete
            recv_result = health_recv(
                &mut rx,
                &mut current_status,
                last_reported_time.map(|t| t.add(
                    chrono::Duration::from_std(report_interval)
                        .unwrap_or(chrono::Duration::seconds(60))
                ))
            ) => {
                match recv_result {
                    None => break, // Channel closed
                    Some(new_status) => current_status = new_status,
                }
            }
            () = tokio::time::sleep(report_interval) => {
                // if current_status is None, it means the background reporting shouldn't happen
                if let Some(ref mut status) = current_status {
                    // update time to report updated steady state
                    status.last_update_time = Utc::now();
                }
            }
        }

        // Report current status if we have one
        if let Some(ref status) = current_status {
            match reporter.report(status.clone()).await {
                Ok(()) => {
                    log::debug!(
                        "Reported health event for {}: {status:?}",
                        reporter.component_name()
                    );
                    // Setting to current time rather than current_status time in case the receiver
                    // is backed up - if we set to current_status time, the next report might trigger
                    // sooner than the health interval requires, causing the backup to worsen
                    last_reported_time = Some(Utc::now());
                }
                Err(e) => {
                    log::warn!(
                        "Failed to report health event for {}: {e:?}",
                        reporter.component_name()
                    );
                }
            }
        } else {
            // If paused, reset last_reported_time
            last_reported_time = None;
        }
    }
}

/// Helper to receive and deduplicate health messages.
async fn health_recv(
    rx: &mut UnboundedReceiver<Option<RuntimeHealth>>,
    curr_status: &mut Option<RuntimeHealth>,
    next_fallback_report_time: Option<DateTime<Utc>>,
) -> Option<Option<RuntimeHealth>> {
    loop {
        // use try_recv to avoid an await point if there are pending messages.
        // Any actual message from the application should be prioritized over the last cached message that
        // would be reported from the timeout branch of the select.
        let new_status = match rx.try_recv() {
            Ok(status) => status,
            // If there aren't any pending messages, it's okay if the timeout branch of the select completes
            Err(tokio::sync::mpsc::error::TryRecvError::Empty) => rx.recv().await?,
            Err(tokio::sync::mpsc::error::TryRecvError::Disconnected) => return None,
        };

        // If the application sent None, propagate that to indicate background reporting should stop
        let Some(new_status) = new_status else {
            return Some(None);
        };

        // If background reporting is on, check if this new status is more recent/different than the current status
        if let Some(existing_status) = curr_status {
            // if new status is more stale than the current status, ignore it
            if new_status.version < existing_status.version
                || new_status.last_update_time < existing_status.last_update_time
            {
                continue;
            }

            // if status is exactly the same other than the timestamp, don't report, but update curr_status
            if new_status.version == existing_status.version
                && new_status.status == existing_status.status
                && new_status.message == existing_status.message
                && new_status.reason_code == existing_status.reason_code
                && next_fallback_report_time.is_some_and(|t| new_status.last_update_time < t)
            {
                // Override the existing_status to have the latest timestamp
                *existing_status = new_status;
                continue;
            }
        }

        return Some(Some(new_status));
    }
}

// ============= Convenience Reporter Implementations =============

/// Health reporter for a device endpoint.
///
/// Reports runtime health status for a specific device endpoint to the
/// Azure Device Registry service.
///
/// Use [`Client::new_device_endpoint_health_reporter`](super::Client::new_device_endpoint_health_reporter)
/// to create instances.
#[derive(Clone)]
pub(super) struct DeviceEndpointHealthReporter {
    pub(super) client: Client,
    pub(super) device_ref: DeviceRef,
    pub(super) message_expiry: Duration,
}

impl HealthReporter for DeviceEndpointHealthReporter {
    async fn report(&self, status: RuntimeHealth) -> Result<(), Error> {
        self.client
            .report_device_endpoint_runtime_health_event(
                self.device_ref.device_name.clone(),
                self.device_ref.endpoint_name.clone(),
                status,
                self.message_expiry,
            )
            .await
    }

    fn component_name(&self) -> String {
        format!(
            "device endpoint {}/{}",
            self.device_ref.device_name, self.device_ref.endpoint_name
        )
    }
}

/// Health reporter for a dataset.
///
/// Reports runtime health status for a specific dataset within an asset to the
/// Azure Device Registry service.
///
/// Use [`Client::new_dataset_health_reporter`](super::Client::new_dataset_health_reporter)
/// to create instances.
#[derive(Clone)]
pub(super) struct DatasetHealthReporter {
    pub(super) client: Client,
    pub(super) asset_ref: AssetRef,
    pub(super) dataset_name: String,
    pub(super) message_expiry: Duration,
}

impl HealthReporter for DatasetHealthReporter {
    async fn report(&self, status: RuntimeHealth) -> Result<(), Error> {
        self.client
            .report_dataset_runtime_health_events(
                self.asset_ref.device_name.clone(),
                self.asset_ref.inbound_endpoint_name.clone(),
                self.asset_ref.name.clone(),
                vec![DatasetRuntimeHealthEvent {
                    dataset_name: self.dataset_name.clone(),
                    runtime_health: status,
                }],
                self.message_expiry,
            )
            .await
    }

    fn component_name(&self) -> String {
        format!(
            "dataset {}/{}/{}/{}",
            self.asset_ref.device_name,
            self.asset_ref.inbound_endpoint_name,
            self.asset_ref.name,
            self.dataset_name
        )
    }
}

/// Health reporter for an event.
///
/// Reports runtime health status for a specific event within an asset to the
/// Azure Device Registry service.
///
/// Use [`Client::new_event_health_reporter`](super::Client::new_event_health_reporter)
/// to create instances.
#[derive(Clone)]
pub(super) struct EventHealthReporter {
    pub(super) client: Client,
    pub(super) asset_ref: AssetRef,
    pub(super) event_group_name: String,
    pub(super) event_name: String,
    pub(super) message_expiry: Duration,
}

impl HealthReporter for EventHealthReporter {
    async fn report(&self, status: RuntimeHealth) -> Result<(), Error> {
        self.client
            .report_event_runtime_health_events(
                self.asset_ref.device_name.clone(),
                self.asset_ref.inbound_endpoint_name.clone(),
                self.asset_ref.name.clone(),
                vec![EventRuntimeHealthEvent {
                    event_group_name: self.event_group_name.clone(),
                    event_name: self.event_name.clone(),
                    runtime_health: status,
                }],
                self.message_expiry,
            )
            .await
    }

    fn component_name(&self) -> String {
        format!(
            "event {}/{}/{}/{}/{}",
            self.asset_ref.device_name,
            self.asset_ref.inbound_endpoint_name,
            self.asset_ref.name,
            self.event_group_name,
            self.event_name
        )
    }
}

/// Health reporter for a stream.
///
/// Reports runtime health status for a specific stream within an asset to the
/// Azure Device Registry service.
///
/// Use [`Client::new_stream_health_reporter`](super::Client::new_stream_health_reporter)
/// to create instances.
#[derive(Clone)]
pub(super) struct StreamHealthReporter {
    pub(super) client: Client,
    pub(super) asset_ref: AssetRef,
    pub(super) stream_name: String,
    pub(super) message_expiry: Duration,
}

impl HealthReporter for StreamHealthReporter {
    async fn report(&self, status: RuntimeHealth) -> Result<(), Error> {
        self.client
            .report_stream_runtime_health_events(
                self.asset_ref.device_name.clone(),
                self.asset_ref.inbound_endpoint_name.clone(),
                self.asset_ref.name.clone(),
                vec![StreamRuntimeHealthEvent {
                    stream_name: self.stream_name.clone(),
                    runtime_health: status,
                }],
                self.message_expiry,
            )
            .await
    }

    fn component_name(&self) -> String {
        format!(
            "stream {}/{}/{}/{}",
            self.asset_ref.device_name,
            self.asset_ref.inbound_endpoint_name,
            self.asset_ref.name,
            self.stream_name
        )
    }
}

/// Health reporter for a management action.
///
/// Reports runtime health status for a specific management action within an asset to the
/// Azure Device Registry service.
///
/// Use [`Client::new_management_action_health_reporter`](super::Client::new_management_action_health_reporter)
/// to create instances.
#[derive(Clone)]
pub(super) struct ManagementActionHealthReporter {
    pub(super) client: Client,
    pub(super) asset_ref: AssetRef,
    pub(super) management_group_name: String,
    pub(super) management_action_name: String,
    pub(super) message_expiry: Duration,
}

impl HealthReporter for ManagementActionHealthReporter {
    async fn report(&self, status: RuntimeHealth) -> Result<(), Error> {
        self.client
            .report_management_action_runtime_health_events(
                self.asset_ref.device_name.clone(),
                self.asset_ref.inbound_endpoint_name.clone(),
                self.asset_ref.name.clone(),
                vec![ManagementActionRuntimeHealthEvent {
                    management_group_name: self.management_group_name.clone(),
                    management_action_name: self.management_action_name.clone(),
                    runtime_health: status,
                }],
                self.message_expiry,
            )
            .await
    }

    fn component_name(&self) -> String {
        format!(
            "management action {}/{}/{}/{}/{}",
            self.asset_ref.device_name,
            self.asset_ref.inbound_endpoint_name,
            self.asset_ref.name,
            self.management_group_name,
            self.management_action_name
        )
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use std::sync::{Arc, Mutex};

    /// A mock health reporter that records all reported health events.
    #[derive(Clone)]
    struct MockHealthReporter {
        reported_events: Arc<Mutex<Vec<RuntimeHealth>>>,
    }

    impl MockHealthReporter {
        fn new() -> Self {
            Self {
                reported_events: Arc::new(Mutex::new(Vec::new())),
            }
        }

        fn get_reported_events(&self) -> Vec<RuntimeHealth> {
            self.reported_events.lock().unwrap().clone()
        }

        fn get_call_count(&self) -> usize {
            self.reported_events.lock().unwrap().len()
        }
    }

    impl HealthReporter for MockHealthReporter {
        async fn report(&self, status: RuntimeHealth) -> Result<(), Error> {
            self.reported_events.lock().unwrap().push(status);
            Ok(())
        }

        fn component_name(&self) -> String {
            "mock component".to_string()
        }
    }

    fn create_available_health_status(version: u64) -> RuntimeHealth {
        RuntimeHealth {
            version,
            status: super::super::HealthStatus::Available,
            message: None,
            reason_code: None,
            last_update_time: chrono::Utc::now(),
        }
    }

    fn create_unavailable_health_status(version: u64) -> RuntimeHealth {
        RuntimeHealth {
            version,
            status: super::super::HealthStatus::Unavailable,
            message: Some("Unavailable".to_string()),
            reason_code: Some("UnavailableResource".to_string()),
            last_update_time: chrono::Utc::now(),
        }
    }

    #[tokio::test]
    async fn test_background_task_reports_initial_event() {
        let mock_reporter = MockHealthReporter::new();
        let cancellation_token = CancellationToken::new();

        let sender = new_health_reporter(
            mock_reporter.clone(),
            ReportInterval::default(),
            cancellation_token.clone(),
        );

        // Send a health event
        let status = create_available_health_status(1);
        sender.report(status.clone()).unwrap();

        // Give the background task time to process
        tokio::time::sleep(Duration::from_millis(50)).await;

        // Verify the event was reported
        let events = mock_reporter.get_reported_events();
        assert_eq!(events.len(), 1);
        assert_eq!(events[0].version, 1);
        assert_eq!(mock_reporter.get_call_count(), 1);

        // Cleanup
        cancellation_token.cancel();
    }

    #[tokio::test]
    async fn test_background_task_deduplicates_identical_events() {
        let mock_reporter = MockHealthReporter::new();
        let cancellation_token = CancellationToken::new();

        let sender = new_health_reporter(
            mock_reporter.clone(),
            ReportInterval::default(), // Long interval so no periodic re-report
            cancellation_token.clone(),
        );

        // Send the same health event multiple times rapidly, with only the timestamp changing
        let mut status = create_available_health_status(1);
        sender.report(status.clone()).unwrap();
        tokio::time::sleep(Duration::from_millis(10)).await;
        status.last_update_time = Utc::now();
        sender.report(status.clone()).unwrap();
        tokio::time::sleep(Duration::from_millis(10)).await;
        status.last_update_time = Utc::now();
        sender.report(status).unwrap();

        // Give the background task time to process
        tokio::time::sleep(Duration::from_millis(100)).await;

        // Verify only one event was reported (duplicates were deduplicated)
        assert_eq!(mock_reporter.get_call_count(), 1);

        // Cleanup
        cancellation_token.cancel();
    }

    #[tokio::test]
    async fn test_background_task_reports_changed_status_to_unavailable() {
        let mock_reporter = MockHealthReporter::new();
        let cancellation_token = CancellationToken::new();

        let sender = new_health_reporter(
            mock_reporter.clone(),
            ReportInterval::default(),
            cancellation_token.clone(),
        );

        // Send initial event available
        let status1 = create_available_health_status(1);
        sender.report(status1).unwrap();
        tokio::time::sleep(Duration::from_millis(50)).await;

        // Send event with changed status to unavailable (same version)
        let status2 = create_unavailable_health_status(1);
        sender.report(status2).unwrap();
        tokio::time::sleep(Duration::from_millis(50)).await;

        // Verify both events were reported
        let events = mock_reporter.get_reported_events();
        assert_eq!(events.len(), 2);
        assert_eq!(events[0].status, super::super::HealthStatus::Available);
        assert_eq!(events[1].status, super::super::HealthStatus::Unavailable);

        // Cleanup
        cancellation_token.cancel();
    }

    #[tokio::test]
    async fn test_background_task_reports_changed_status_to_available() {
        let mock_reporter = MockHealthReporter::new();
        let cancellation_token = CancellationToken::new();

        let sender = new_health_reporter(
            mock_reporter.clone(),
            ReportInterval::default(),
            cancellation_token.clone(),
        );

        // Send initial event unavailable
        let status1 = create_unavailable_health_status(1);
        sender.report(status1).unwrap();
        tokio::time::sleep(Duration::from_millis(50)).await;

        // Send event with changed status to available (same version)
        let status2 = create_available_health_status(1);
        sender.report(status2).unwrap();
        tokio::time::sleep(Duration::from_millis(50)).await;

        // Verify both events were reported
        let events = mock_reporter.get_reported_events();
        assert_eq!(events.len(), 2);
        assert_eq!(events[0].status, super::super::HealthStatus::Unavailable);
        assert_eq!(events[1].status, super::super::HealthStatus::Available);

        // Cleanup
        cancellation_token.cancel();
    }

    #[tokio::test]
    async fn test_background_task_reports_changed_version() {
        let mock_reporter = MockHealthReporter::new();
        let cancellation_token = CancellationToken::new();

        let sender = new_health_reporter(
            mock_reporter.clone(),
            ReportInterval::default(),
            cancellation_token.clone(),
        );

        // Send initial event
        let status1 = create_available_health_status(1);
        sender.report(status1).unwrap();
        tokio::time::sleep(Duration::from_millis(50)).await;

        // Send same event with new version
        let status2 = create_available_health_status(2);
        sender.report(status2).unwrap();
        tokio::time::sleep(Duration::from_millis(50)).await;

        // Verify both events were reported
        let events = mock_reporter.get_reported_events();
        assert_eq!(events.len(), 2);
        assert_eq!(events[0].version, 1);
        assert_eq!(events[1].version, 2);

        // Cleanup
        cancellation_token.cancel();
    }

    #[tokio::test]
    async fn test_background_task_reports_changed_message() {
        let mock_reporter = MockHealthReporter::new();
        let cancellation_token = CancellationToken::new();

        let sender = new_health_reporter(
            mock_reporter.clone(),
            ReportInterval::default(),
            cancellation_token.clone(),
        );

        // Send initial event
        let mut status1 = create_unavailable_health_status(1);
        status1.message = Some("message1".to_string());
        sender.report(status1).unwrap();
        tokio::time::sleep(Duration::from_millis(50)).await;

        // Send event with changed message (same version)
        let mut status2 = create_unavailable_health_status(1);
        status2.message = Some("message2".to_string());
        sender.report(status2).unwrap();
        tokio::time::sleep(Duration::from_millis(50)).await;

        // Verify both events were reported
        let events = mock_reporter.get_reported_events();
        assert_eq!(events.len(), 2);
        assert_eq!(events[0].message, Some("message1".to_string()));
        assert_eq!(events[1].message, Some("message2".to_string()));

        // Cleanup
        cancellation_token.cancel();
    }

    #[tokio::test]
    async fn test_background_task_periodic_rereporting() {
        let mock_reporter = MockHealthReporter::new();
        let cancellation_token = CancellationToken::new();

        // Use a short interval for testing (bypass validation via direct construction)
        let report_interval = ReportInterval(Duration::from_millis(100));
        let sender = new_health_reporter(
            mock_reporter.clone(),
            report_interval,
            cancellation_token.clone(),
        );

        // Send initial event
        let status = create_available_health_status(1);
        let mut last_reported_time = status.last_update_time;
        sender.report(status).unwrap();

        // Wait for initial report
        tokio::time::sleep(Duration::from_millis(50)).await;
        assert_eq!(mock_reporter.get_call_count(), 1);
        assert_eq!(
            mock_reporter.get_reported_events()[0].last_update_time,
            last_reported_time
        );

        // Wait past first interval - should re-report
        tokio::time::sleep(Duration::from(report_interval) + Duration::from_millis(50)).await;
        assert!(mock_reporter.get_call_count() >= 2);
        assert!(mock_reporter.get_reported_events()[1].last_update_time > last_reported_time);
        last_reported_time = mock_reporter.get_reported_events()[1].last_update_time;

        // Wait past second interval - should re-report again
        tokio::time::sleep(Duration::from(report_interval) + Duration::from_millis(50)).await;
        assert!(mock_reporter.get_call_count() >= 3);
        assert!(mock_reporter.get_reported_events()[2].last_update_time > last_reported_time);

        // Cleanup
        cancellation_token.cancel();
    }

    #[tokio::test]
    async fn test_background_task_stops_on_cancellation() {
        let mock_reporter = MockHealthReporter::new();
        let cancellation_token = CancellationToken::new();

        let sender = new_health_reporter(
            mock_reporter.clone(),
            ReportInterval::default(),
            cancellation_token.clone(),
        );

        // Send initial event
        let status = create_available_health_status(1);
        sender.report(status).unwrap();
        tokio::time::sleep(Duration::from_millis(50)).await;

        assert_eq!(mock_reporter.get_call_count(), 1);

        // Cancel the token
        cancellation_token.cancel();
        tokio::time::sleep(Duration::from_millis(50)).await;

        // Send another event - should fail since task is cancelled
        let status2 = create_available_health_status(2);
        assert!(sender.report(status2).is_err());

        // Verify no additional events were reported
        assert_eq!(mock_reporter.get_call_count(), 1);
    }

    #[tokio::test]
    async fn test_background_task_stops_on_sender_drop() {
        let mock_reporter = MockHealthReporter::new();
        let cancellation_token = CancellationToken::new();

        let sender = new_health_reporter(
            mock_reporter.clone(),
            ReportInterval(Duration::from_millis(100)), // Short interval for testing
            cancellation_token.clone(),
        );

        // Send initial event
        let status = create_available_health_status(1);
        sender.report(status).unwrap();
        tokio::time::sleep(Duration::from_millis(50)).await;

        assert_eq!(mock_reporter.get_call_count(), 1);

        // Wait for one interval to ensure background reporting is working
        tokio::time::sleep(Duration::from_millis(100)).await;
        assert!(mock_reporter.get_call_count() > 1);

        // Drop the sender
        drop(sender);
        tokio::time::sleep(Duration::from_millis(100)).await;

        // Task should have stopped - verify by checking it doesn't panic
        // and the call count remains the same
        assert_eq!(mock_reporter.get_call_count(), 2);

        // Cleanup
        cancellation_token.cancel();
    }

    #[tokio::test]
    async fn test_background_task_pause_stops_reporting() {
        let mock_reporter = MockHealthReporter::new();
        let cancellation_token = CancellationToken::new();

        let sender = new_health_reporter(
            mock_reporter.clone(),
            ReportInterval(Duration::from_millis(100)), // Short interval for testing
            cancellation_token.clone(),
        );

        // Send initial event
        let status = create_available_health_status(1);
        sender.report(status).unwrap();
        tokio::time::sleep(Duration::from_millis(50)).await;
        assert_eq!(mock_reporter.get_call_count(), 1);

        // Pause reporting
        sender.pause().unwrap();
        tokio::time::sleep(Duration::from_millis(150)).await;

        // Verify no periodic re-reporting happened while paused
        let count_after_pause = mock_reporter.get_call_count();

        // Wait another interval - should still not report
        tokio::time::sleep(Duration::from_millis(150)).await;
        assert_eq!(mock_reporter.get_call_count(), count_after_pause);

        // Resume by sending a new event
        let status2 = create_available_health_status(2);
        sender.report(status2).unwrap();
        tokio::time::sleep(Duration::from_millis(50)).await;

        // Verify reporting resumed
        assert!(mock_reporter.get_call_count() == count_after_pause + 1);

        // Verify periodic reporting resumes
        tokio::time::sleep(Duration::from_millis(150)).await;
        assert!(mock_reporter.get_call_count() > count_after_pause + 1);

        // Cleanup
        cancellation_token.cancel();
    }

    #[tokio::test]
    async fn test_background_task_skips_stale_events() {
        let mock_reporter = MockHealthReporter::new();
        let cancellation_token = CancellationToken::new();

        let sender = new_health_reporter(
            mock_reporter.clone(),
            ReportInterval::default(),
            cancellation_token.clone(),
        );

        // Send event with version 2 first
        let status2 = create_available_health_status(2);
        sender.report(status2).unwrap();
        tokio::time::sleep(Duration::from_millis(50)).await;

        // Send stale event with version 1 - should be skipped
        let status1 = create_available_health_status(1);
        sender.report(status1).unwrap();
        tokio::time::sleep(Duration::from_millis(50)).await;

        // Verify only one event was reported (the stale one was skipped)
        let events = mock_reporter.get_reported_events();
        assert_eq!(events.len(), 1);
        assert_eq!(events[0].version, 2);

        // Cleanup
        cancellation_token.cancel();
    }
}
