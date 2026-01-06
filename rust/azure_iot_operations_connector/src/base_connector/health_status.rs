// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

#![allow(missing_docs)]

use std::sync::Arc;

use azure_iot_operations_services::azure_device_registry::{self, HealthStatus, RuntimeHealth};
use tokio::sync::mpsc::{UnboundedReceiver, UnboundedSender};

use crate::{
    base_connector::ConnectorContext,
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
) -> UnboundedSender<RuntimeHealth> {
    let (health_tx, health_rx) = tokio::sync::mpsc::unbounded_channel();

    // TODO: need cancellation token?
    tokio::task::spawn(health_sender_run(connector_context, health_rx, component));
    health_tx
}
// TODO: spawn this as a task in new? Or find some way to link this to baseConnector::run()
async fn health_sender_run<T: HealthComponent>(
    connector_context: Arc<ConnectorContext>,
    mut health_rx: UnboundedReceiver<RuntimeHealth>,
    component: T,
) {
    let mut current_status = health_rx.recv().await.unwrap();
    loop {
        // report current or new status
        // TODO: tokio::task?
        match component
            .report_health_status(&connector_context, current_status.clone())
            .await
        {
            Ok(_) => {
                log::info!("Reported health status: {:?}", current_status);
            }
            Err(e) => {
                // Handle error (e.g., log it)
                log::warn!("Failed to report health status: {:?}", e);
                // TODO: retry?
            }
        }
        tokio::select! {
            biased;
            recv_result = health_recv(&mut health_rx, &current_status) => {
                match recv_result {
                    // TODO: need another way to end this task when the component is deleted as well
                    None => break, // Channel closed, TODO: handle this case properly
                    Some(new_status) => current_status = new_status,
                }
            }
            _ = tokio::time::sleep(connector_context.health_report_interval) => {
                // if let Some(status) = &current_status {
                // Report the current health status to Azure IoT Operations Services
                // (Implementation of reporting logic goes here)
                // }
            }
        }
    }
}
async fn health_recv(
    health_rx: &mut UnboundedReceiver<RuntimeHealth>,
    curr_status: &RuntimeHealth,
) -> Option<RuntimeHealth> {
    loop {
        let new_status = health_rx.recv().await?;
        if new_status.last_update_time < curr_status.last_update_time {
            continue;
        }
        if new_status.version < curr_status.version {
            continue;
        }
        if new_status.status == curr_status.status {
            // TODO: do we need to update last_update_time to compare against in this case?
            continue;
        }
        return Some(new_status);
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
