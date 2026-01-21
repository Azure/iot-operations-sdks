// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

//! Utilities to handle health event reporting logic for Azure Device Registry components.

use std::sync::Arc;

use azure_iot_operations_services::azure_device_registry::{
    HealthStatus,
    health_reporter::HealthReporterSender,
    models::DeviceRef,
};
use tokio_util::sync::CancellationToken;

use crate::{
    DataOperationName, DataOperationRef, base_connector::ConnectorContext,
    deployment_artifacts::azure_device_registry::DeviceEndpointRef,
};

/// Represents the runtime health of a resource.
#[derive(Debug, Clone)]
pub struct RuntimeHealthEvent {
    /// A human-readable message describing the last transition.
    pub message: Option<String>,
    /// Unique, CamelCase reason code describing the cause of the last health state transition.
    pub reason_code: Option<String>,
    /// The current health status of the resource.
    pub status: HealthStatus,
}

// ASK: Maybe not necessary, we could just use the new_device_endpoint_health_reporter directly?
/// Creates a health reporter sender for a device endpoint.
///
/// The background task is managed by the services layer and handles deduplication
/// and periodic re-reporting automatically.
///
/// The `cancellation_token` is used to stop the background task when the component is deleted
/// or when the parent connector shuts down.
pub(crate) fn new_device_endpoint_health_sender(
    connector_context: &Arc<ConnectorContext>,
    device_endpoint_ref: &DeviceEndpointRef,
    cancellation_token: CancellationToken,
) -> HealthReporterSender {
    let device_ref = DeviceRef {
        device_name: device_endpoint_ref.device_name.clone(),
        endpoint_name: device_endpoint_ref.inbound_endpoint_name.clone(),
    };
    connector_context
        .azure_device_registry_client
        .new_device_endpoint_health_reporter(
            device_ref,
            connector_context.azure_device_registry_timeout,
            connector_context.health_report_interval,
            cancellation_token,
        )
}

/// Creates a health reporter sender for a data operation.
///
/// The background task is managed by the services layer and handles deduplication
/// and periodic re-reporting automatically.
///
/// The `cancellation_token` is used to stop the background task when the component is deleted
/// or when the parent connector shuts down.
pub(crate) fn new_data_operation_health_sender(
    connector_context: &Arc<ConnectorContext>,
    data_operation_ref: &DataOperationRef,
    cancellation_token: CancellationToken,
) -> HealthReporterSender {
    let asset_ref = azure_iot_operations_services::azure_device_registry::AssetRef {
        device_name: data_operation_ref.device_name.clone(),
        inbound_endpoint_name: data_operation_ref.inbound_endpoint_name.clone(),
        name: data_operation_ref.asset_name.clone(),
    };
    match &data_operation_ref.data_operation_name {
        DataOperationName::Dataset { name } => connector_context
            .azure_device_registry_client
            .new_dataset_health_reporter(
                asset_ref,
                name.clone(),
                connector_context.azure_device_registry_timeout,
                connector_context.health_report_interval,
                cancellation_token,
            ),
        DataOperationName::Event {
            name,
            event_group_name,
        } => connector_context
            .azure_device_registry_client
            .new_event_health_reporter(
                asset_ref,
                event_group_name.clone(),
                name.clone(),
                connector_context.azure_device_registry_timeout,
                connector_context.health_report_interval,
                cancellation_token,
            ),
        DataOperationName::Stream { name } => connector_context
            .azure_device_registry_client
            .new_stream_health_reporter(
                asset_ref,
                name.clone(),
                connector_context.azure_device_registry_timeout,
                connector_context.health_report_interval,
                cancellation_token,
            ),
    }
}
