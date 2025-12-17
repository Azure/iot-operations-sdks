// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

use std::{sync::Arc, time::Duration};

use azure_iot_operations_services::azure_device_registry::{self, RuntimeHealth};
use tokio::sync::mpsc::{UnboundedReceiver, UnboundedSender};

use crate::{
    base_connector::ConnectorContext,
    deployment_artifacts::azure_device_registry::DeviceEndpointRef,
};

pub trait HealthComponent {
    async fn report_health_status(
        &self,
        connector_context: &Arc<ConnectorContext>,
        status: RuntimeHealth,
    ) -> Result<(), azure_device_registry::Error>;
}

pub struct HealthStatusClient<T: HealthComponent> {
    connector_context: Arc<ConnectorContext>,
    // current_status: Option<RuntimeHealth>,
    report_interval: Duration,
    health_rx: UnboundedReceiver<RuntimeHealth>,
    component: T,
}

impl<T: HealthComponent> HealthStatusClient<T> {
    pub fn new(
        connector_context: Arc<ConnectorContext>,
        component: T,
        report_interval: Duration,
    ) -> (Self, UnboundedSender<RuntimeHealth>) {
        let (health_tx, health_rx) = tokio::sync::mpsc::unbounded_channel();
        (
            Self {
                connector_context,
                // current_status: None,
                report_interval,
                health_rx,
                component,
            },
            health_tx,
        )
    }
    // TODO: spawn this as a task in new? Or find some way to link this to baseConnector::run()
    pub async fn run(&mut self) {
        let mut current_status = self.health_rx.recv().await.unwrap();
        let report_interval = self.report_interval;
        loop {
            tokio::select! {
                biased;
                recv_result = self.health_recv(&current_status) => {
                    match recv_result {
                        // TODO: need another way to end this task when the component is deleted as well
                        None => break, // Channel closed, TODO: handle this case properly
                        Some(new_status) => current_status = new_status,
                    }
                }
                _ = tokio::time::sleep(report_interval) => {
                    // if let Some(status) = &current_status {
                    // Report the current health status to Azure IoT Operations Services
                    // (Implementation of reporting logic goes here)
                    // }
                }
            }
            // report current or new status
            // TODO: tokio::task?
            match self
                .component
                .report_health_status(&self.connector_context, current_status.clone())
                .await
            {
                Ok(_) => {}
                Err(e) => {
                    // Handle error (e.g., log it)
                    log::warn!("Failed to report health status: {:?}", e);
                    // TODO: retry?
                }
            }
        }
    }
    async fn health_recv(&mut self, curr_status: &RuntimeHealth) -> Option<RuntimeHealth> {
        loop {
            let new_status = self.health_rx.recv().await?;
            if new_status.last_update_time < curr_status.last_update_time {
                continue;
            }
            if new_status.version < curr_status.version {
                continue;
            }
            if new_status.status == curr_status.status {
                continue;
            }
            return Some(new_status);
        }
    }

    // async fn report_health_status(&self, status: &RuntimeHealth) {
    //     let _ = self.connector_context.azure_device_registry_client
    //         .report_device_endpoint_runtime_health_event(status)
    //         .await;
    // }
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
