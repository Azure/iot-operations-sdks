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
//! - Convenience reporters for common components:
//!   - [`DeviceEndpointHealthReporter`]
//!   - [`DatasetHealthReporter`]
//!   - [`EventHealthReporter`]
//!   - [`StreamHealthReporter`]
//!   - [`ManagementActionHealthReporter`]
//!
//! # Example
//!
//! ```ignore
//! use azure_iot_operations_services::azure_device_registry::{
//!     health_reporter::{DeviceEndpointHealthReporter, new_health_reporter},
//!     models::DeviceRef,
//! };
//!
//! let device_ref = DeviceRef {
//!     device_name: "device-name".to_string(),
//!     endpoint_name: "endpoint-name".to_string(),
//! };
//!
//! let reporter = DeviceEndpointHealthReporter::new(
//!     client.clone(),
//!     device_ref,
//!     Duration::from_secs(30), // timeout
//! );
//!
//! let sender = new_health_reporter(
//!     reporter,
//!     Duration::from_secs(60), // report_interval
//!     cancellation_token,
//! );
//!
//! // Report health status - the background task handles deduplication
//! sender.report(RuntimeHealth {
//!     version: 1,
//!     status: HealthStatus::Available,
//!     message: Some("Connected".to_string()),
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
    /// coalesced until the next reporting interval.
    ///
    /// # Arguments
    /// * `status` - The runtime health status to report.
    pub fn report(&self, status: RuntimeHealth) {
        let _ = self.tx.send(Some(status));
    }

    /// Pauses background reporting until a new event is reported.
    ///
    /// Use this during reconfiguration when the previous health state may no longer
    /// be valid. The background task will not re-report until a new status is sent.
    pub fn pause(&self) {
        let _ = self.tx.send(None);
    }
}

/// Spawns a background health reporter task.
///
/// The background task handles:
/// - Deduplication of identical health events
/// - Periodic re-reporting at the configured interval
/// - Coalescing rapid updates
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
    report_interval: Duration,
    cancellation_token: CancellationToken,
) -> HealthReporterSender {
    let (tx, rx) = tokio::sync::mpsc::unbounded_channel();

    tokio::spawn(health_reporter_task(
        reporter,
        report_interval,
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
                log::debug!("Health reporter task cancelled");
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
                    log::debug!("Reported health event: {status:?}");
                    // Setting to current time rather than current_status time in case the receiver
                    // is backed up - if we set to current_status time, the next report might trigger
                    // sooner than the health interval requires, causing the backup to worsen
                    last_reported_time = Some(Utc::now());
                }
                Err(e) => {
                    log::warn!("Failed to report health event: {e:?}");
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
#[derive(Clone)]
pub struct DeviceEndpointHealthReporter {
    client: Client,
    device_ref: DeviceRef,
    timeout: Duration,
}

impl DeviceEndpointHealthReporter {
    /// Creates a new [`DeviceEndpointHealthReporter`].
    ///
    /// # Arguments
    /// * `client` - The Azure Device Registry client.
    /// * `device_ref` - Reference to the device and endpoint.
    /// * `timeout` - The duration until the client stops waiting for a response, rounded up to the nearest second.
    #[must_use]
    pub fn new(client: Client, device_ref: DeviceRef, timeout: Duration) -> Self {
        Self {
            client,
            device_ref,
            timeout,
        }
    }
}

impl HealthReporter for DeviceEndpointHealthReporter {
    async fn report(&self, status: RuntimeHealth) -> Result<(), Error> {
        self.client
            .report_device_endpoint_runtime_health_event(
                self.device_ref.device_name.clone(),
                self.device_ref.endpoint_name.clone(),
                status,
                self.timeout,
            )
            .await
    }
}

/// Health reporter for a dataset.
///
/// Reports runtime health status for a specific dataset within an asset to the
/// Azure Device Registry service.
#[derive(Clone)]
pub struct DatasetHealthReporter {
    client: Client,
    asset_ref: AssetRef,
    dataset_name: String,
    timeout: Duration,
}

impl DatasetHealthReporter {
    /// Creates a new [`DatasetHealthReporter`].
    ///
    /// # Arguments
    /// * `client` - The Azure Device Registry client.
    /// * `asset_ref` - Reference to the asset containing the dataset.
    /// * `dataset_name` - The name of the dataset.
    /// * `timeout` - The duration until the client stops waiting for a response, rounded up to the nearest second.
    #[must_use]
    pub fn new(
        client: Client,
        asset_ref: AssetRef,
        dataset_name: String,
        timeout: Duration,
    ) -> Self {
        Self {
            client,
            asset_ref,
            dataset_name,
            timeout,
        }
    }
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
                self.timeout,
            )
            .await
    }
}

/// Health reporter for an event.
///
/// Reports runtime health status for a specific event within an asset to the
/// Azure Device Registry service.
#[derive(Clone)]
pub struct EventHealthReporter {
    client: Client,
    asset_ref: AssetRef,
    event_group_name: String,
    event_name: String,
    timeout: Duration,
}

impl EventHealthReporter {
    /// Creates a new [`EventHealthReporter`].
    ///
    /// # Arguments
    /// * `client` - The Azure Device Registry client.
    /// * `asset_ref` - Reference to the asset containing the event.
    /// * `event_group_name` - The name of the event group.
    /// * `event_name` - The name of the event.
    /// * `timeout` - The duration until the client stops waiting for a response, rounded up to the nearest second.
    #[must_use]
    pub fn new(
        client: Client,
        asset_ref: AssetRef,
        event_group_name: String,
        event_name: String,
        timeout: Duration,
    ) -> Self {
        Self {
            client,
            asset_ref,
            event_group_name,
            event_name,
            timeout,
        }
    }
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
                self.timeout,
            )
            .await
    }
}

/// Health reporter for a stream.
///
/// Reports runtime health status for a specific stream within an asset to the
/// Azure Device Registry service.
#[derive(Clone)]
pub struct StreamHealthReporter {
    client: Client,
    asset_ref: AssetRef,
    stream_name: String,
    timeout: Duration,
}

impl StreamHealthReporter {
    /// Creates a new [`StreamHealthReporter`].
    ///
    /// # Arguments
    /// * `client` - The Azure Device Registry client.
    /// * `asset_ref` - Reference to the asset containing the stream.
    /// * `stream_name` - The name of the stream.
    /// * `timeout` - The duration until the client stops waiting for a response, rounded up to the nearest second.
    #[must_use]
    pub fn new(
        client: Client,
        asset_ref: AssetRef,
        stream_name: String,
        timeout: Duration,
    ) -> Self {
        Self {
            client,
            asset_ref,
            stream_name,
            timeout,
        }
    }
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
                self.timeout,
            )
            .await
    }
}

/// Health reporter for a management action.
///
/// Reports runtime health status for a specific management action within an asset to the
/// Azure Device Registry service.
#[derive(Clone)]
pub struct ManagementActionHealthReporter {
    client: Client,
    asset_ref: AssetRef,
    management_group_name: String,
    management_action_name: String,
    timeout: Duration,
}

impl ManagementActionHealthReporter {
    /// Creates a new [`ManagementActionHealthReporter`].
    ///
    /// # Arguments
    /// * `client` - The Azure Device Registry client.
    /// * `asset_ref` - Reference to the asset containing the management action.
    /// * `management_group_name` - The name of the management group.
    /// * `management_action_name` - The name of the management action.
    /// * `timeout` - The duration until the client stops waiting for a response, rounded up to the nearest second.
    #[must_use]
    pub fn new(
        client: Client,
        asset_ref: AssetRef,
        management_group_name: String,
        management_action_name: String,
        timeout: Duration,
    ) -> Self {
        Self {
            client,
            asset_ref,
            management_group_name,
            management_action_name,
            timeout,
        }
    }
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
                self.timeout,
            )
            .await
    }
}
