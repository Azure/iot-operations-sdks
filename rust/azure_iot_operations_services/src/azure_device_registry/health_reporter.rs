// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

//! Background health reporting for Azure Device Registry components.

use std::ops::Add;
use std::time::Duration;

use chrono::{DateTime, Utc};
use tokio::sync::mpsc::{UnboundedReceiver, UnboundedSender};
use tokio_util::sync::DropGuard;

use super::models::{DatasetRuntimeHealthEvent, EventRuntimeHealthEvent, StreamRuntimeHealthEvent};
use super::{Client, HealthStatus, RuntimeHealth};

/// Configuration for health reporters.
#[derive(Clone, Debug)]
pub struct HealthReporterOptions {
    /// Interval for re-reporting steady-state health.
    pub report_interval: Duration,
    /// Timeout for ADR operations.
    pub timeout: Duration,
}

/// A health event without version/timestamp (managed by the reporter).
#[derive(Clone, Debug)]
pub struct HealthEvent {
    pub message: Option<String>,
    pub reason_code: Option<String>,
    pub status: HealthStatus,
}

// Internal message sent through the channel
#[derive(Clone, Debug)]
struct HealthMessage {
    event: HealthEvent,
    version: u64,
    timestamp: DateTime<Utc>,
}

// ============= Device Endpoint =============

/// Manages background health reporting for a device endpoint.
/// Dropping this stops the background task.
#[derive(Debug)]
pub struct DeviceEndpointHealthReporter {
    tx: UnboundedSender<Option<HealthMessage>>,
    _drop_guard: DropGuard,
}

impl DeviceEndpointHealthReporter {
    pub fn new(
        client: Client,
        device_name: String,
        inbound_endpoint_name: String,
        options: HealthReporterOptions,
    ) -> Self {
        let (tx, rx) = tokio::sync::mpsc::unbounded_channel();
        let cancellation_token = tokio_util::sync::CancellationToken::new();

        tokio::spawn(device_endpoint_health_task(
            client,
            device_name,
            inbound_endpoint_name,
            options,
            rx,
            cancellation_token.clone(),
        ));

        Self {
            tx,
            _drop_guard: cancellation_token.drop_guard(),
        }
    }

    pub fn get_sender(&self) -> DeviceEndpointHealthSender {
        DeviceEndpointHealthSender {
            tx: self.tx.clone(),
        }
    }
}

#[derive(Clone, Debug)]
pub struct DeviceEndpointHealthSender {
    tx: UnboundedSender<Option<HealthMessage>>,
}

impl DeviceEndpointHealthSender {
    pub fn report(&self, event: HealthEvent, version: u64) {
        let _ = self.tx.send(Some(HealthMessage {
            event,
            version,
            timestamp: Utc::now(),
        }));
    }

    pub fn pause(&self) {
        let _ = self.tx.send(None);
    }
}

async fn device_endpoint_health_task(
    client: Client,
    device_name: String,
    inbound_endpoint_name: String,
    options: HealthReporterOptions,
    mut rx: UnboundedReceiver<Option<HealthMessage>>,
    cancellation_token: tokio_util::sync::CancellationToken,
) {
    let mut current_status: Option<RuntimeHealth> = None;
    let mut last_reported_time: Option<DateTime<Utc>> = None;

    loop {
        tokio::select! {
            biased;
            () = cancellation_token.cancelled() => break,
            recv_result = health_recv(&mut rx, &mut current_status, last_reported_time.map(|t| t.add(chrono::Duration::from_std(options.report_interval).unwrap_or(chrono::Duration::seconds(60))))) => {
                match recv_result {
                    None => break,
                    Some(new_status) => current_status = new_status,
                }
            }
            () = tokio::time::sleep(options.report_interval) => {
                if let Some(ref mut status) = current_status {
                    status.last_update_time = Utc::now();
                }
            }
        }

        if let Some(ref status) = current_status {
            match client
                .report_device_endpoint_runtime_health_event(
                    device_name.clone(),
                    inbound_endpoint_name.clone(),
                    status.clone(),
                    options.timeout,
                )
                .await
            {
                Ok(()) => {
                    last_reported_time = Some(Utc::now());
                }
                Err(e) => {
                    log::warn!("Failed to report device endpoint health: {e:?}");
                }
            }
        } else {
            last_reported_time = None;
        }
    }
}

// ============= Dataset =============

#[derive(Debug)]
pub struct DatasetHealthReporter {
    tx: UnboundedSender<Option<HealthMessage>>,
    _drop_guard: DropGuard,
}

impl DatasetHealthReporter {
    pub fn new(
        client: Client,
        device_name: String,
        inbound_endpoint_name: String,
        asset_name: String,
        dataset_name: String,
        options: HealthReporterOptions,
    ) -> Self {
        let (tx, rx) = tokio::sync::mpsc::unbounded_channel();
        let cancellation_token = tokio_util::sync::CancellationToken::new();

        tokio::spawn(dataset_health_task(
            client,
            device_name,
            inbound_endpoint_name,
            asset_name,
            dataset_name,
            options,
            rx,
            cancellation_token.clone(),
        ));

        Self {
            tx,
            _drop_guard: cancellation_token.drop_guard(),
        }
    }

    pub fn get_sender(&self) -> DatasetHealthSender {
        DatasetHealthSender {
            tx: self.tx.clone(),
        }
    }
}

#[derive(Clone, Debug)]
pub struct DatasetHealthSender {
    tx: UnboundedSender<Option<HealthMessage>>,
}

impl DatasetHealthSender {
    pub fn report(&self, event: HealthEvent, version: u64) {
        let _ = self.tx.send(Some(HealthMessage {
            event,
            version,
            timestamp: Utc::now(),
        }));
    }

    pub fn pause(&self) {
        let _ = self.tx.send(None);
    }
}

async fn dataset_health_task(
    client: Client,
    device_name: String,
    inbound_endpoint_name: String,
    asset_name: String,
    dataset_name: String,
    options: HealthReporterOptions,
    mut rx: UnboundedReceiver<Option<HealthMessage>>,
    cancellation_token: tokio_util::sync::CancellationToken,
) {
    let mut current_status: Option<RuntimeHealth> = None;
    let mut last_reported_time: Option<DateTime<Utc>> = None;

    loop {
        tokio::select! {
            biased;
            () = cancellation_token.cancelled() => break,
            recv_result = health_recv(&mut rx, &mut current_status, last_reported_time.map(|t| t.add(chrono::Duration::from_std(options.report_interval).unwrap_or(chrono::Duration::seconds(60))))) => {
                match recv_result {
                    None => break,
                    Some(new_status) => current_status = new_status,
                }
            }
            () = tokio::time::sleep(options.report_interval) => {
                if let Some(ref mut status) = current_status {
                    status.last_update_time = Utc::now();
                }
            }
        }

        if let Some(ref status) = current_status {
            match client
                .report_dataset_runtime_health_events(
                    device_name.clone(),
                    inbound_endpoint_name.clone(),
                    asset_name.clone(),
                    vec![DatasetRuntimeHealthEvent {
                        dataset_name: dataset_name.clone(),
                        runtime_health: status.clone(),
                    }],
                    options.timeout,
                )
                .await
            {
                Ok(()) => {
                    last_reported_time = Some(Utc::now());
                }
                Err(e) => {
                    log::warn!("Failed to report dataset health: {e:?}");
                }
            }
        } else {
            last_reported_time = None;
        }
    }
}

// ============= Event =============

#[derive(Debug)]
pub struct EventHealthReporter {
    tx: UnboundedSender<Option<HealthMessage>>,
    _drop_guard: DropGuard,
}

impl EventHealthReporter {
    pub fn new(
        client: Client,
        device_name: String,
        inbound_endpoint_name: String,
        asset_name: String,
        event_group_name: String,
        event_name: String,
        options: HealthReporterOptions,
    ) -> Self {
        let (tx, rx) = tokio::sync::mpsc::unbounded_channel();
        let cancellation_token = tokio_util::sync::CancellationToken::new();

        tokio::spawn(event_health_task(
            client,
            device_name,
            inbound_endpoint_name,
            asset_name,
            event_group_name,
            event_name,
            options,
            rx,
            cancellation_token.clone(),
        ));

        Self {
            tx,
            _drop_guard: cancellation_token.drop_guard(),
        }
    }

    pub fn get_sender(&self) -> EventHealthSender {
        EventHealthSender {
            tx: self.tx.clone(),
        }
    }
}

#[derive(Clone, Debug)]
pub struct EventHealthSender {
    tx: UnboundedSender<Option<HealthMessage>>,
}

impl EventHealthSender {
    pub fn report(&self, event: HealthEvent, version: u64) {
        let _ = self.tx.send(Some(HealthMessage {
            event,
            version,
            timestamp: Utc::now(),
        }));
    }

    pub fn pause(&self) {
        let _ = self.tx.send(None);
    }
}

async fn event_health_task(
    client: Client,
    device_name: String,
    inbound_endpoint_name: String,
    asset_name: String,
    event_group_name: String,
    event_name: String,
    options: HealthReporterOptions,
    mut rx: UnboundedReceiver<Option<HealthMessage>>,
    cancellation_token: tokio_util::sync::CancellationToken,
) {
    let mut current_status: Option<RuntimeHealth> = None;
    let mut last_reported_time: Option<DateTime<Utc>> = None;

    loop {
        tokio::select! {
            biased;
            () = cancellation_token.cancelled() => break,
            recv_result = health_recv(&mut rx, &mut current_status, last_reported_time.map(|t| t.add(chrono::Duration::from_std(options.report_interval).unwrap_or(chrono::Duration::seconds(60))))) => {
                match recv_result {
                    None => break,
                    Some(new_status) => current_status = new_status,
                }
            }
            () = tokio::time::sleep(options.report_interval) => {
                if let Some(ref mut status) = current_status {
                    status.last_update_time = Utc::now();
                }
            }
        }

        if let Some(ref status) = current_status {
            match client
                .report_event_runtime_health_events(
                    device_name.clone(),
                    inbound_endpoint_name.clone(),
                    asset_name.clone(),
                    vec![EventRuntimeHealthEvent {
                        event_group_name: event_group_name.clone(),
                        event_name: event_name.clone(),
                        runtime_health: status.clone(),
                    }],
                    options.timeout,
                )
                .await
            {
                Ok(()) => {
                    last_reported_time = Some(Utc::now());
                }
                Err(e) => {
                    log::warn!("Failed to report event health: {e:?}");
                }
            }
        } else {
            last_reported_time = None;
        }
    }
}

// ============= Stream =============

#[derive(Debug)]
pub struct StreamHealthReporter {
    tx: UnboundedSender<Option<HealthMessage>>,
    _drop_guard: DropGuard,
}

impl StreamHealthReporter {
    pub fn new(
        client: Client,
        device_name: String,
        inbound_endpoint_name: String,
        asset_name: String,
        stream_name: String,
        options: HealthReporterOptions,
    ) -> Self {
        let (tx, rx) = tokio::sync::mpsc::unbounded_channel();
        let cancellation_token = tokio_util::sync::CancellationToken::new();

        tokio::spawn(stream_health_task(
            client,
            device_name,
            inbound_endpoint_name,
            asset_name,
            stream_name,
            options,
            rx,
            cancellation_token.clone(),
        ));

        Self {
            tx,
            _drop_guard: cancellation_token.drop_guard(),
        }
    }

    pub fn get_sender(&self) -> StreamHealthSender {
        StreamHealthSender {
            tx: self.tx.clone(),
        }
    }
}

#[derive(Clone, Debug)]
pub struct StreamHealthSender {
    tx: UnboundedSender<Option<HealthMessage>>,
}

impl StreamHealthSender {
    pub fn report(&self, event: HealthEvent, version: u64) {
        let _ = self.tx.send(Some(HealthMessage {
            event,
            version,
            timestamp: Utc::now(),
        }));
    }

    pub fn pause(&self) {
        let _ = self.tx.send(None);
    }
}

async fn stream_health_task(
    client: Client,
    device_name: String,
    inbound_endpoint_name: String,
    asset_name: String,
    stream_name: String,
    options: HealthReporterOptions,
    mut rx: UnboundedReceiver<Option<HealthMessage>>,
    cancellation_token: tokio_util::sync::CancellationToken,
) {
    let mut current_status: Option<RuntimeHealth> = None;
    let mut last_reported_time: Option<DateTime<Utc>> = None;

    loop {
        tokio::select! {
            biased;
            () = cancellation_token.cancelled() => break,
            recv_result = health_recv(&mut rx, &mut current_status, last_reported_time.map(|t| t.add(chrono::Duration::from_std(options.report_interval).unwrap_or(chrono::Duration::seconds(60))))) => {
                match recv_result {
                    None => break,
                    Some(new_status) => current_status = new_status,
                }
            }
            () = tokio::time::sleep(options.report_interval) => {
                if let Some(ref mut status) = current_status {
                    status.last_update_time = Utc::now();
                }
            }
        }

        if let Some(ref status) = current_status {
            match client
                .report_stream_runtime_health_events(
                    device_name.clone(),
                    inbound_endpoint_name.clone(),
                    asset_name.clone(),
                    vec![StreamRuntimeHealthEvent {
                        stream_name: stream_name.clone(),
                        runtime_health: status.clone(),
                    }],
                    options.timeout,
                )
                .await
            {
                Ok(()) => {
                    last_reported_time = Some(Utc::now());
                }
                Err(e) => {
                    log::warn!("Failed to report stream health: {e:?}");
                }
            }
        } else {
            last_reported_time = None;
        }
    }
}

// ============= Shared Helper =============

async fn health_recv(
    rx: &mut UnboundedReceiver<Option<HealthMessage>>,
    curr_status: &mut Option<RuntimeHealth>,
    next_fallback_report_time: Option<DateTime<Utc>>,
) -> Option<Option<RuntimeHealth>> {
    loop {
        let msg = match rx.try_recv() {
            Ok(msg) => msg,
            Err(tokio::sync::mpsc::error::TryRecvError::Empty) => rx.recv().await?,
            Err(tokio::sync::mpsc::error::TryRecvError::Disconnected) => return None,
        };

        let Some(msg) = msg else {
            return Some(None); // Pause signal
        };

        let new_status = RuntimeHealth {
            last_update_time: msg.timestamp,
            message: msg.event.message,
            reason_code: msg.event.reason_code,
            status: msg.event.status,
            version: msg.version,
        };

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
                && next_fallback_report_time
                    .is_some_and(|t| new_status.last_update_time < t)
            {
                *existing = new_status;
                continue;
            }
        }

        return Some(Some(new_status));
    }
}
