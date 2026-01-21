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
//! use azure_iot_operations_services::azure_device_registry::health_reporter::{
//!     DeviceEndpointHealthReporter, HealthReporterOptions, new_health_reporter,
//! };
//!
//! let reporter = DeviceEndpointHealthReporter::new(
//!     client.clone(),
//!     "device-name".to_string(),
//!     "endpoint-name".to_string(),
//!     Duration::from_secs(30),
//! );
//!
//! let sender = new_health_reporter(
//!     reporter,
//!     HealthReporterOptions { report_interval: Duration::from_secs(60) },
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
    DatasetRuntimeHealthEvent, EventRuntimeHealthEvent, ManagementActionRuntimeHealthEvent,
    StreamRuntimeHealthEvent,
};
use super::{Client, Error, RuntimeHealth};

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

/// Configuration options for the health reporter background task.
#[derive(Clone, Debug)]
pub struct HealthReporterOptions {
    /// Interval for re-reporting steady-state health when no changes occur.
    pub report_interval: Duration,
}

/// Handle to send health events to the background reporter task.
///
/// This handle is cloneable, allowing multiple tasks to share the same reporter.
/// The background task handles deduplication and periodic re-reporting automatically.
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
/// * `options` - Configuration options for the background task.
/// * `cancellation_token` - Token to signal cancellation of the background task.
///
/// Returns a [`HealthReporterSender`] handle. The background task runs until:
/// - The cancellation token is cancelled, OR
/// - All senders are dropped (channel closes)
#[must_use]
pub fn new_health_reporter<R: HealthReporter>(
    reporter: R,
    options: HealthReporterOptions,
    cancellation_token: CancellationToken,
) -> HealthReporterSender {
    let (tx, rx) = tokio::sync::mpsc::unbounded_channel();

    tokio::spawn(health_reporter_task(
        reporter,
        options,
        rx,
        cancellation_token,
    ));

    HealthReporterSender { tx }
}

/// The background task that handles health reporting.
async fn health_reporter_task<R: HealthReporter>(
    reporter: R,
    options: HealthReporterOptions,
    mut rx: UnboundedReceiver<Option<RuntimeHealth>>,
    cancellation_token: CancellationToken,
) {
    let mut current_status: Option<RuntimeHealth> = None;
    let mut last_reported_time: Option<DateTime<Utc>> = None;

    loop {
        tokio::select! {
            biased;
            () = cancellation_token.cancelled() => {
                log::debug!("Health reporter task cancelled");
                break;
            }
            recv_result = health_recv(
                &mut rx,
                &mut current_status,
                last_reported_time.map(|t| t.add(
                    chrono::Duration::from_std(options.report_interval)
                        .unwrap_or(chrono::Duration::seconds(60))
                ))
            ) => {
                match recv_result {
                    None => break, // Channel closed
                    Some(new_status) => current_status = new_status,
                }
            }
            () = tokio::time::sleep(options.report_interval) => {
                // Update timestamp for steady-state re-reporting
                if let Some(ref mut status) = current_status {
                    status.last_update_time = Utc::now();
                }
            }
        }

        // Report current status if we have one
        if let Some(ref status) = current_status {
            match reporter.report(status.clone()).await {
                Ok(()) => {
                    log::debug!("Reported health event: {status:?}");
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
        let msg = match rx.try_recv() {
            Ok(msg) => msg,
            Err(tokio::sync::mpsc::error::TryRecvError::Empty) => rx.recv().await?,
            Err(tokio::sync::mpsc::error::TryRecvError::Disconnected) => return None,
        };

        // Pause signal
        let Some(new_status) = msg else {
            return Some(None);
        };

        // Check against existing status for deduplication
        if let Some(existing) = curr_status {
            // Skip stale messages
            if new_status.version < existing.version
                || new_status.last_update_time < existing.last_update_time
            {
                continue;
            }

            // Skip duplicates unless we need to report for steady-state
            if new_status.version == existing.version
                && new_status.status == existing.status
                && new_status.message == existing.message
                && new_status.reason_code == existing.reason_code
                && next_fallback_report_time.is_some_and(|t| new_status.last_update_time < t)
            {
                // Update timestamp but don't trigger report
                *existing = new_status;
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
    device_name: String,
    inbound_endpoint_name: String,
    timeout: Duration,
}

impl DeviceEndpointHealthReporter {
    /// Creates a new [`DeviceEndpointHealthReporter`].
    ///
    /// # Arguments
    /// * `client` - The Azure Device Registry client.
    /// * `device_name` - The name of the device.
    /// * `inbound_endpoint_name` - The name of the inbound endpoint.
    /// * `timeout` - The duration until the client stops waiting for a response, rounded up to the nearest second.
    #[must_use]
    pub fn new(
        client: Client,
        device_name: String,
        inbound_endpoint_name: String,
        timeout: Duration,
    ) -> Self {
        Self {
            client,
            device_name,
            inbound_endpoint_name,
            timeout,
        }
    }
}

impl HealthReporter for DeviceEndpointHealthReporter {
    async fn report(&self, status: RuntimeHealth) -> Result<(), Error> {
        self.client
            .report_device_endpoint_runtime_health_event(
                self.device_name.clone(),
                self.inbound_endpoint_name.clone(),
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
    device_name: String,
    inbound_endpoint_name: String,
    asset_name: String,
    dataset_name: String,
    timeout: Duration,
}

impl DatasetHealthReporter {
    /// Creates a new [`DatasetHealthReporter`].
    ///
    /// # Arguments
    /// * `client` - The Azure Device Registry client.
    /// * `device_name` - The name of the device.
    /// * `inbound_endpoint_name` - The name of the inbound endpoint.
    /// * `asset_name` - The name of the asset containing the dataset.
    /// * `dataset_name` - The name of the dataset.
    /// * `timeout` - The duration until the client stops waiting for a response, rounded up to the nearest second.
    #[must_use]
    pub fn new(
        client: Client,
        device_name: String,
        inbound_endpoint_name: String,
        asset_name: String,
        dataset_name: String,
        timeout: Duration,
    ) -> Self {
        Self {
            client,
            device_name,
            inbound_endpoint_name,
            asset_name,
            dataset_name,
            timeout,
        }
    }
}

impl HealthReporter for DatasetHealthReporter {
    async fn report(&self, status: RuntimeHealth) -> Result<(), Error> {
        self.client
            .report_dataset_runtime_health_events(
                self.device_name.clone(),
                self.inbound_endpoint_name.clone(),
                self.asset_name.clone(),
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
    device_name: String,
    inbound_endpoint_name: String,
    asset_name: String,
    event_group_name: String,
    event_name: String,
    timeout: Duration,
}

impl EventHealthReporter {
    /// Creates a new [`EventHealthReporter`].
    ///
    /// # Arguments
    /// * `client` - The Azure Device Registry client.
    /// * `device_name` - The name of the device.
    /// * `inbound_endpoint_name` - The name of the inbound endpoint.
    /// * `asset_name` - The name of the asset containing the event.
    /// * `event_group_name` - The name of the event group.
    /// * `event_name` - The name of the event.
    /// * `timeout` - The duration until the client stops waiting for a response, rounded up to the nearest second.
    #[must_use]
    pub fn new(
        client: Client,
        device_name: String,
        inbound_endpoint_name: String,
        asset_name: String,
        event_group_name: String,
        event_name: String,
        timeout: Duration,
    ) -> Self {
        Self {
            client,
            device_name,
            inbound_endpoint_name,
            asset_name,
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
                self.device_name.clone(),
                self.inbound_endpoint_name.clone(),
                self.asset_name.clone(),
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
    device_name: String,
    inbound_endpoint_name: String,
    asset_name: String,
    stream_name: String,
    timeout: Duration,
}

impl StreamHealthReporter {
    /// Creates a new [`StreamHealthReporter`].
    ///
    /// # Arguments
    /// * `client` - The Azure Device Registry client.
    /// * `device_name` - The name of the device.
    /// * `inbound_endpoint_name` - The name of the inbound endpoint.
    /// * `asset_name` - The name of the asset containing the stream.
    /// * `stream_name` - The name of the stream.
    /// * `timeout` - The duration until the client stops waiting for a response, rounded up to the nearest second.
    #[must_use]
    pub fn new(
        client: Client,
        device_name: String,
        inbound_endpoint_name: String,
        asset_name: String,
        stream_name: String,
        timeout: Duration,
    ) -> Self {
        Self {
            client,
            device_name,
            inbound_endpoint_name,
            asset_name,
            stream_name,
            timeout,
        }
    }
}

impl HealthReporter for StreamHealthReporter {
    async fn report(&self, status: RuntimeHealth) -> Result<(), Error> {
        self.client
            .report_stream_runtime_health_events(
                self.device_name.clone(),
                self.inbound_endpoint_name.clone(),
                self.asset_name.clone(),
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
    device_name: String,
    inbound_endpoint_name: String,
    asset_name: String,
    management_group_name: String,
    management_action_name: String,
    timeout: Duration,
}

impl ManagementActionHealthReporter {
    /// Creates a new [`ManagementActionHealthReporter`].
    ///
    /// # Arguments
    /// * `client` - The Azure Device Registry client.
    /// * `device_name` - The name of the device.
    /// * `inbound_endpoint_name` - The name of the inbound endpoint.
    /// * `asset_name` - The name of the asset containing the management action.
    /// * `management_group_name` - The name of the management group.
    /// * `management_action_name` - The name of the management action.
    /// * `timeout` - The duration until the client stops waiting for a response, rounded up to the nearest second.
    #[must_use]
    pub fn new(
        client: Client,
        device_name: String,
        inbound_endpoint_name: String,
        asset_name: String,
        management_group_name: String,
        management_action_name: String,
        timeout: Duration,
    ) -> Self {
        Self {
            client,
            device_name,
            inbound_endpoint_name,
            asset_name,
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
                self.device_name.clone(),
                self.inbound_endpoint_name.clone(),
                self.asset_name.clone(),
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
