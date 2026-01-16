// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

//! Utilities to handle health status reporting logic for Azure Device Registry components.

use std::ops::Add;
use std::sync::Arc;

use azure_iot_operations_services::azure_device_registry::{
    self, HealthStatus, RuntimeHealth,
    models::{DatasetRuntimeHealthEvent, EventRuntimeHealthEvent, StreamRuntimeHealthEvent},
};
use chrono::{DateTime, Utc};
use tokio::sync::mpsc::{UnboundedReceiver, UnboundedSender};
use tokio_util::sync::CancellationToken;

use crate::{
    DataOperationName, DataOperationRef, base_connector::ConnectorContext,
    deployment_artifacts::azure_device_registry::DeviceEndpointRef,
};

/// Represents the runtime health of a resource.
#[derive(Debug, Clone)]
pub struct RuntimeHealthStatus {
    /// A human-readable message describing the last transition.
    pub message: Option<String>,
    /// Unique, CamelCase reason code describing the cause of the last health state transition.
    pub reason_code: Option<String>,
    /// The current health status of the resource.
    pub status: HealthStatus,
}

/// Trait for how each component reports health status to the ADR service
/// General practice is to implement this trait for the `*Ref` types
pub(crate) trait HealthComponent: Clone + Send + Sync + 'static {
    fn report_health_status(
        &self,
        connector_context: &Arc<ConnectorContext>,
        status: RuntimeHealth,
    ) -> impl std::future::Future<Output = Result<(), azure_device_registry::Error>> + std::marker::Send;
}

/// Creates the Unbounded Sender to report health status for the given component
/// and spawns the task to handle sending the health status reports.
///
/// The `cancellation_token` is used to stop the background task when the component is deleted
/// or when the parent connector shuts down.
pub(crate) fn new_health_sender<T: HealthComponent>(
    connector_context: Arc<ConnectorContext>,
    component: T,
    cancellation_token: CancellationToken,
) -> UnboundedSender<Option<RuntimeHealth>> {
    let (health_tx, health_rx) = tokio::sync::mpsc::unbounded_channel();

    tokio::task::spawn(health_sender_run(
        connector_context,
        health_rx,
        component,
        cancellation_token,
    ));
    health_tx
}

async fn health_sender_run<T: HealthComponent>(
    connector_context: Arc<ConnectorContext>,
    mut health_rx: UnboundedReceiver<Option<RuntimeHealth>>,
    component: T,
    cancellation_token: CancellationToken,
) {
    // Latest status from the application (whether reported or not). None if background reporting
    // shouldn't be happening.
    let mut current_status = None;
    // Time of the last successfully reported status, or None if background reporting is disabled
    let mut last_reported_time = None;
    loop {
        tokio::select! {
            biased;
            // Check for cancellation first (highest priority)
            () = cancellation_token.cancelled() => {
                log::debug!("Health sender task cancelled for component");
                break;
            }
            // passes in the next time that a report should happen in case this doesn't free up to allow the sleep branch to complete
            recv_result = health_recv(&mut health_rx, &mut current_status, last_reported_time.map(|t: DateTime<Utc>| t.add(connector_context.health_report_interval))) => {
                match recv_result {
                    None => break, // Channel closed
                    Some(new_status) => current_status = new_status,
                }
            }
            () = tokio::time::sleep(connector_context.health_report_interval) => {
                // if current_status is None, it means that background reporting shouldn't happen
                if let Some(curr_status) = &mut current_status {
                    // update time to report updated steady state
                    curr_status.last_update_time = chrono::Utc::now();
                }
            }
        }
        // report current or new status
        if let Some(ref curr_status) = current_status {
            match component
                .report_health_status(&connector_context, curr_status.clone())
                .await
            {
                Ok(()) => {
                    log::debug!("Reported health status: {curr_status:?}");
                    // Setting to current time rather than current_status time in case the
                    // receiver is backed up - if we set to current_status time,
                    // the next report might trigger sooner than the health interval requires, causing the backup to worsen
                    last_reported_time = Some(chrono::Utc::now());
                }
                Err(e) => {
                    log::warn!("Failed to report health status: {e:?}");
                }
            }
        } else {
            // If current_status is None, also reset last_reported_time to None to avoid "background reporting" from the health_recv task
            last_reported_time = None;
        }
    }
}

async fn health_recv(
    health_rx: &mut UnboundedReceiver<Option<RuntimeHealth>>,
    curr_status: &mut Option<RuntimeHealth>,
    next_fallback_report_time: Option<DateTime<Utc>>,
) -> Option<Option<RuntimeHealth>> {
    loop {
        // use try_recv to avoid an await point if there are pending messages.
        // Any actual message from the application should be prioritized over the last cached message that
        // would be reported from the timeout branch of the select.
        let new_status = match health_rx.try_recv() {
            Ok(status) => status,
            // If there aren't any pending messages, it's okay if the timeout branch of the select completes
            Err(tokio::sync::mpsc::error::TryRecvError::Empty) => health_rx.recv().await?,
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
                // if we've been getting continuous reports that don't allow the the timeout branch of the tokio select to complete, trigger the
                // steady state status reporting from here
                && next_fallback_report_time.is_some_and(|next_fallback_report_time| new_status.last_update_time < next_fallback_report_time)
            {
                // Override the existing_status to have the latest timestamp
                *existing_status = new_status;
                continue;
            }
        }
        return Some(Some(new_status));
    }
}

impl HealthComponent for DeviceEndpointRef {
    async fn report_health_status(
        &self,
        connector_context: &Arc<ConnectorContext>,
        status: RuntimeHealth,
    ) -> Result<(), azure_device_registry::Error> {
        connector_context
            .azure_device_registry_client
            .report_device_endpoint_runtime_health_event(
                self.device_name.clone(),
                self.inbound_endpoint_name.clone(),
                status,
                connector_context.azure_device_registry_timeout,
            )
            .await
    }
}

impl HealthComponent for DataOperationRef {
    async fn report_health_status(
        &self,
        connector_context: &Arc<ConnectorContext>,
        status: RuntimeHealth,
    ) -> Result<(), azure_device_registry::Error> {
        match &self.data_operation_name {
            DataOperationName::Dataset { name } => {
                connector_context
                    .azure_device_registry_client
                    .report_dataset_runtime_health_events(
                        self.device_name.clone(),
                        self.inbound_endpoint_name.clone(),
                        self.asset_name.clone(),
                        vec![DatasetRuntimeHealthEvent {
                            dataset_name: name.clone(),
                            runtime_health: status,
                        }],
                        connector_context.azure_device_registry_timeout,
                    )
                    .await
            }
            DataOperationName::Event {
                name,
                event_group_name,
            } => {
                connector_context
                    .azure_device_registry_client
                    .report_event_runtime_health_events(
                        self.device_name.clone(),
                        self.inbound_endpoint_name.clone(),
                        self.asset_name.clone(),
                        vec![EventRuntimeHealthEvent {
                            event_group_name: event_group_name.clone(),
                            event_name: name.clone(),
                            runtime_health: status,
                        }],
                        connector_context.azure_device_registry_timeout,
                    )
                    .await
            }
            DataOperationName::Stream { name } => {
                connector_context
                    .azure_device_registry_client
                    .report_stream_runtime_health_events(
                        self.device_name.clone(),
                        self.inbound_endpoint_name.clone(),
                        self.asset_name.clone(),
                        vec![StreamRuntimeHealthEvent {
                            stream_name: name.clone(),
                            runtime_health: status,
                        }],
                        connector_context.azure_device_registry_timeout,
                    )
                    .await
            }
        }
    }
}
