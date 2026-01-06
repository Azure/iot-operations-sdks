// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

//! Utilities to handle health status reporting logic for Azure Device Registry components.

use std::ops::Add;
use std::sync::Arc;

use azure_iot_operations_services::azure_device_registry::{self, HealthStatus, RuntimeHealth};
use chrono::{DateTime, Utc};
use tokio::sync::mpsc::{UnboundedReceiver, UnboundedSender};

use crate::{
    DataOperationName, DataOperationRef, base_connector::ConnectorContext,
    deployment_artifacts::azure_device_registry::DeviceEndpointRef,
};

pub(crate) trait HealthComponent: Clone + Send + Sync + 'static {
    fn report_health_status(
        &self,
        connector_context: &Arc<ConnectorContext>,
        status: RuntimeHealth,
    ) -> impl std::future::Future<Output = Result<(), azure_device_registry::Error>> + std::marker::Send;
}

pub(crate) fn new_health_sender<T: HealthComponent>(
    connector_context: Arc<ConnectorContext>,
    component: T,
) -> UnboundedSender<Option<RuntimeHealth>> {
    let (health_tx, health_rx) = tokio::sync::mpsc::unbounded_channel();

    // TODO: need cancellation token?
    tokio::task::spawn(health_sender_run(connector_context, health_rx, component));
    health_tx
}
// TODO: spawn this as a task in new? Or find some way to link this to baseConnector::run()
async fn health_sender_run<T: HealthComponent>(
    connector_context: Arc<ConnectorContext>,
    mut health_rx: UnboundedReceiver<Option<RuntimeHealth>>,
    component: T,
) {
    let mut current_status = None;
    let mut last_reported_time = None;
    loop {
        tokio::select! {
            biased;
            recv_result = health_recv(&mut health_rx, &mut current_status, last_reported_time.map(|t: DateTime<Utc>| t.add(connector_context.health_report_interval))) => {
                match recv_result {
                    // TODO: need another way to end this task when the component is deleted as well
                    None => break, // Channel closed, TODO: handle this case properly
                    Some(new_status) => current_status = new_status,
                }
            }
            _ = tokio::time::sleep(connector_context.health_report_interval) => {
                if let Some(curr_status) = &mut current_status {
                    // update time to report steady state
                    curr_status.last_update_time = chrono::Utc::now();
                }
            }
        }
        // report current or new status
        // TODO: tokio::task?
        if let Some(ref curr_status) = current_status {
            match component
                .report_health_status(&connector_context, curr_status.clone())
                .await
            {
                Ok(_) => {
                    log::info!("Reported health status: {:?}", curr_status);
                    // Setting to current time rather than current_status time in case the
                    // receiver is backed up - if we set to current_status time,
                    // the next report might trigger sooner than the health interval requires, causing the backup to worsen
                    last_reported_time = Some(chrono::Utc::now());
                }
                Err(e) => {
                    // Handle error (e.g., log it)
                    log::warn!("Failed to report health status: {:?}", e);
                    // TODO: retry? Retries now done by not saving this as the last_reported_time - should this factor into the sleep time?
                }
            }
        } else {
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
        let new_status = match health_rx.try_recv() {
            Ok(status) => status,
            // If there aren't any pending messages, it's okay if the timeout branch of the select completes
            Err(tokio::sync::mpsc::error::TryRecvError::Empty) => health_rx.recv().await?,
            Err(tokio::sync::mpsc::error::TryRecvError::Disconnected) => return None,
        };
        let new_status = match new_status {
            Some(status) => status,
            None => return Some(None),
        };
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
                        vec![(name.clone(), status)],
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
                    .report_event_runtime_health_event(
                        self.device_name.clone(),
                        self.inbound_endpoint_name.clone(),
                        self.asset_name.clone(),
                        event_group_name.clone(),
                        name.clone(),
                        status,
                        connector_context.azure_device_registry_timeout,
                    )
                    .await
            }
            DataOperationName::Stream { name } => {
                connector_context
                    .azure_device_registry_client
                    .report_stream_runtime_health_event(
                        self.device_name.clone(),
                        self.inbound_endpoint_name.clone(),
                        self.asset_name.clone(),
                        name.clone(),
                        status,
                        connector_context.azure_device_registry_timeout,
                    )
                    .await
            }
        }
    }
}

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
