// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

//! Background health reporting for Azure Device Registry components.

use std::future::Future;
use std::ops::Add;
use std::time::Duration;

use chrono::{DateTime, Utc};
use tokio::sync::mpsc::{UnboundedReceiver, UnboundedSender};
use tokio_util::sync::CancellationToken;

use super::{Client, Error, RuntimeHealth};
use super::models::{
    DatasetRuntimeHealthEvent, EventRuntimeHealthEvent, 
    ManagementActionRuntimeHealthEvent, StreamRuntimeHealthEvent,
};

/// Trait for components that can report health events to the ADR service.
///
/// Implement this trait for your component type, then use [`new_health_reporter`]
/// to create a background task that handles periodic re-reporting and deduplication.
pub trait HealthReporter: Send + Sync + 'static {
    /// Reports a health status to the ADR service.
    fn report(
        &self,
        status: RuntimeHealth,
    ) -> impl Future<Output = Result<(), Error>> + Send;
}

/// Configuration for the health reporter background task.
#[derive(Clone, Debug)]
pub struct HealthReporterOptions {
    /// Interval for re-reporting steady-state health.
    pub report_interval: Duration,
}

/// Handle to send health events to the background reporter.
/// 
/// Cloneable so multiple tasks can share it.
#[derive(Clone, Debug)]
pub struct HealthReporterSender {
    tx: UnboundedSender<Option<RuntimeHealth>>,
}

impl HealthReporterSender {
    /// Send a health event to be reported.
    /// The background task will handle deduplication and periodic re-reporting.
    pub fn report(&self, status: RuntimeHealth) {
        let _ = self.tx.send(Some(status));
    }

    /// Pause background reporting until a new event is reported.
    /// Use this during reconfiguration when the previous health state may no longer be valid.
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
/// Returns a [`HealthReporterSender`] handle. The background task runs until:
/// - The cancellation token is cancelled, OR
/// - All senders are dropped (channel closes)
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

/// Reports health for a device endpoint.
#[derive(Clone)]
pub struct DeviceEndpointHealthReporter {
    client: Client,
    device_name: String,
    inbound_endpoint_name: String,
    timeout: Duration,
}

impl DeviceEndpointHealthReporter {
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

/// Reports health for a dataset.
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

/// Reports health for an event.
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

/// Reports health for a stream.
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

/// Reports health for a management action.
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
