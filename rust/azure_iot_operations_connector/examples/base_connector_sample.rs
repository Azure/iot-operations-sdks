// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

//! This example demonstrates how to use the base connector SDK to initialize
//! a deployed Connector and get [`DeviceEndpointClients`] and [`AssetClients`].
//!
//! This sample simply logs the device information received - a real
//! connector would then use these to connect to the device/inbound endpoints
//! and start operations defined in the assets.
//!
//! To deploy and test this example, see instructions in `rust/azure_iot_operations_connector/README.md`

use std::{collections::HashMap, time::Duration};

use azure_iot_operations_connector::{
    AdrConfigError, Data, DataOperationKind,
    base_connector::{
        self, BaseConnector,
        managed_azure_device_registry::{
            AssetClient, ClientNotification, DataOperationClient, DataOperationNotification,
            DeviceEndpointClient, DeviceEndpointClientCreationObservation, StatusReported,
        },
    },
    data_processor::derived_json,
    deployment_artifacts::connector::ConnectorArtifacts,
};
use azure_iot_operations_otel::Otel;
use azure_iot_operations_protocol::application::ApplicationContextBuilder;
use azure_iot_operations_services::azure_device_registry;

const OTEL_TAG: &str = "base_connector_sample_logs";
const DEFAULT_LOG_LEVEL: &str =
    "warn,base_connector_sample=info,azure_iot_operations_connector=info";

#[tokio::main(flavor = "current_thread")]
async fn main() -> Result<(), Box<dyn std::error::Error>> {
    // Create the connector artifacts from the deployment
    let connector_artifacts = ConnectorArtifacts::new_from_deployment()?;

    // Initialize the OTEL logger / exporter
    let otel_config = connector_artifacts.to_otel_config(OTEL_TAG, DEFAULT_LOG_LEVEL);
    let mut otel_exporter = Otel::new(otel_config);
    let otel_task = otel_exporter.run();

    // Create an ApplicationContext
    let application_context = ApplicationContextBuilder::default().build()?;

    // Create the BaseConnector
    let base_connector = BaseConnector::new(application_context, connector_artifacts)?;

    // Create a device endpoint client creation observation
    let device_creation_observation =
        base_connector.create_device_endpoint_client_create_observation();

    // Create a discovery client
    let adr_discovery_client = base_connector.discovery_client();

    // Run the Session and the Azure Device Registry operations concurrently
    let res = tokio::select! {

        (r1, r2) = async {
            tokio::join!(
                run_program(device_creation_observation),
                run_discovery(adr_discovery_client),
            )
        } => {
            match r2 {
                Ok(()) => log::info!("Discovery finished successfully"),
                Err(e) => {
                    log::error!("Discovery failed: {e}");
                    Err(e)?;
                },
            }
            Ok(r1)
        }
        r = base_connector.run() => {
            match r {
                Ok(()) => {
                    log::info!("Base connector finished successfully");
                    Ok(())
                }
                Err(e) => {
                    log::error!("Base connector failed: {e}");
                    Err(Box::new(e))?
                }
            }
        },
        r = otel_task => {
            match r {
                Ok(()) => {
                    log::info!("OTEL task finished successfully");
                    Ok(())
                }
                Err(e) => {
                    log::error!("OTEL task failed: {e}");
                    Err(Box::new(e))?
                }
            }
        },
    };

    otel_exporter.shutdown().await;

    res
}

// This function runs in a loop, waiting for device creation notifications.
async fn run_program(mut device_creation_observation: DeviceEndpointClientCreationObservation) {
    // Wait for a device creation notification
    loop {
        let mut device_endpoint_client = device_creation_observation.recv_notification().await;
        log::info!("Device created: {device_endpoint_client:?}");

        // now we should update the status of the device
        if let Err(e) = device_endpoint_client
            .report_device_status_if_modified(report_status_if_changed(Ok(())))
            .await
        {
            log::error!("Error reporting device status: {e}");
        }

        // Generate endpoint status outside closure to avoid borrowing issues
        let endpoint_status = generate_endpoint_status(&device_endpoint_client);

        if let Err(e) = device_endpoint_client
            .report_endpoint_status_if_modified(report_status_if_changed(endpoint_status))
            .await
        {
            log::error!("Error reporting device endpoint status: {e}");
        }

        // Start handling the assets for this device endpoint
        // if we didn't accept the inbound endpoint, then we still want to run this to wait for updates
        tokio::task::spawn(run_device(device_endpoint_client));
    }
}

// This function runs in a loop, waiting for asset creation notifications.
async fn run_device(mut device_endpoint_client: DeviceEndpointClient) {
    loop {
        match device_endpoint_client.recv_notification().await {
            ClientNotification::Deleted => {
                log::warn!("Device Endpoint deleted");
                break;
            }
            ClientNotification::Updated => {
                log::info!("Device updated: {device_endpoint_client:?}");
                // Update device status - usually only on first report or error changes
                if let Err(e) = device_endpoint_client
                    .report_device_status_if_modified(report_status_if_changed(Ok(())))
                    .await
                {
                    log::error!("Error reporting device status: {e}");
                }

                // Update endpoint status - check for changes
                let endpoint_status = generate_endpoint_status(&device_endpoint_client);

                if let Err(e) = device_endpoint_client
                    .report_endpoint_status_if_modified(report_status_if_changed(endpoint_status))
                    .await
                {
                    log::error!("Error reporting endpoint status: {e}");
                }
            }
            ClientNotification::Created(mut asset_client) => {
                log::info!("Asset created: {asset_client:?}");

                // Generate status outside the closure to avoid borrowing issues
                let asset_status = generate_asset_status(&asset_client);

                if let Err(e) = asset_client
                    .report_status_if_modified(report_status_if_changed(asset_status))
                    .await
                {
                    log::error!("Error reporting asset status: {e}");
                }

                // if let Err(e) = asset_client.report_status(asset_status).await {
                //     log::error!("Error reporting asset status: {e}");
                // }

                // Start handling the datasets for this asset
                // if we didn't accept the asset, then we still want to run this to wait for updates
                tokio::task::spawn(run_asset(asset_client));
            }
        }
    }
}

// This function runs in a loop, waiting for dataset creation notifications.
async fn run_asset(mut asset_client: AssetClient) {
    loop {
        match asset_client.recv_notification().await {
            ClientNotification::Updated => {
                log::info!("asset updated: {asset_client:?}");
                // Generate status outside the closure to avoid borrowing issues
                let asset_status = generate_asset_status(&asset_client);

                if let Err(e) = asset_client
                    .report_status_if_modified(report_status_if_changed(asset_status))
                    .await
                {
                    log::error!("Error reporting asset status: {e}");
                }
            }
            ClientNotification::Deleted => {
                log::warn!("Asset has been deleted");
                break;
            }
            ClientNotification::Created(data_operation_client) => {
                log::info!("Data Operation Created: {data_operation_client:?}");
                if let DataOperationKind::Dataset = data_operation_client
                    .data_operation_ref()
                    .data_operation_kind
                {
                    tokio::task::spawn(run_dataset(data_operation_client));
                } else {
                    tokio::task::spawn(handle_unsupported_data_operation(data_operation_client));
                }
            }
        }
    }
}

/// Note, this function takes in a `DataOperationClient`, but we know it is specifically a `Dataset`
/// because we already filtered out non-dataset `DataOperationClient`s in the `run_asset` function.
async fn run_dataset(mut data_operation_client: DataOperationClient) {
    // now we should update the status of the dataset and report the message schema
    if let Err(e) = data_operation_client
        .report_status_if_modified(report_status_if_changed(Ok(())))
        .await
    {
        log::error!("Error reporting dataset status: {e}");
    }

    let sample_data = mock_received_data(0);

    let message_schema = derived_json::create_schema(&sample_data).unwrap();
    match data_operation_client
        .report_message_schema_if_modified(|current_schema| {
            // Only report schema if there isn't already one
            if current_schema.is_none() {
                Some(message_schema.clone())
            } else {
                None
            }
        })
        .await
    {
        Ok(status_reported) => {
            match status_reported {
                StatusReported::Success => {
                    log::info!("Message Schema reported successfully");
                }
                StatusReported::NotModified => {
                    log::info!("Message Schema already exists, not reporting");
                }
            }
        }
        Err(e) => {
            log::error!("Error reporting message schema: {e}");
        }
    }
    let mut count = 0;
    // Timer will trigger the sampling of data
    let mut timer = tokio::time::interval(Duration::from_secs(10));
    let mut dataset_valid = true;
    loop {
        tokio::select! {
            biased;
            // Listen for a dataset update notifications
            res = data_operation_client.recv_notification() => {
                match res {
                    DataOperationNotification::Updated => {
                        log::info!("dataset updated: {data_operation_client:?}");
                        // now we should update the status of the dataset and report the message schema
                        if let Err(e) = data_operation_client
                            .report_status_if_modified(report_status_if_changed(Ok(())))
                            .await
                        {
                            log::error!("Error reporting dataset status: {e}");
                        }

                        let sample_data = mock_received_data(0);

                        let message_schema =
                            derived_json::create_schema(&sample_data).unwrap();
                        // Report schema only if there isn't already one
                        match data_operation_client
                            .report_message_schema_if_modified(|current_schema| {
                                // Only report schema if there isn't already one
                                if current_schema.is_none() {
                                    Some(message_schema.clone())
                                } else {
                                    None
                                }
                            })
                            .await
                        {
                            Ok(status_reported) => {
                                match status_reported {
                                    StatusReported::Success => {
                                        log::info!("Message Schema reported successfully");
                                    }
                                    StatusReported::NotModified => {
                                        log::info!("Message Schema already exists, not reporting");
                                    }
                                }
                            }
                            Err(e) => {
                                log::error!("Error reporting message schema: {e}");
                            }
                        }
                        dataset_valid = true;
                    },
                    DataOperationNotification::UpdatedInvalid => {
                        log::warn!("Dataset has invalid update. Wait for new dataset update.");
                        dataset_valid = false;
                    },
                    DataOperationNotification::Deleted => {
                        log::warn!("Dataset has been deleted. No more dataset updates will be received");
                        break;
                    }
                }
            },
            _ = timer.tick(), if dataset_valid => {
                let sample_data = mock_received_data(count);
                match data_operation_client.forward_data(sample_data).await {
                    Ok(()) => {
                        log::info!(
                            "data {count} for {} forwarded",
                            data_operation_client.data_operation_ref().data_operation_name
                        );
                        count += 1;
                    }
                    Err(e) => log::error!("error forwarding data: {e}"),
                }
            }
        }
    }
}

/// Small handler to indicate lack of stream/event support in this connector
async fn handle_unsupported_data_operation(mut data_operation_client: DataOperationClient) {
    let data_operation_kind = data_operation_client
        .data_operation_ref()
        .data_operation_kind;
    let data_operation_name = data_operation_client
        .data_operation_ref()
        .data_operation_name
        .clone();
    log::warn!("Data Operation kind {data_operation_kind:?} not supported for this connector");
    // Report invalid definition to adr
    let error_status = Err(AdrConfigError {
        message: Some(format!(
            "Data Operation kind {data_operation_kind:?} not supported for this connector",
        )),
        ..Default::default()
    });

    if let Err(e) = data_operation_client
        .report_status_if_modified(report_status_if_changed(error_status))
        .await
    {
        log::error!("Error reporting {data_operation_kind:?} {data_operation_name} status: {e}");
    }

    loop {
        match data_operation_client.recv_notification().await {
            DataOperationNotification::Updated => {
                log::warn!(
                    "{data_operation_name} update notification received. {data_operation_kind:?} is not supported for the this Connector",
                );
                let error_status = Err(AdrConfigError {
                    message: Some(format!(
                        "Data Operation kind {data_operation_kind:?} not supported for this connector",
                    )),
                    ..Default::default()
                });

                if let Err(e) = data_operation_client
                    .report_status_if_modified(report_status_if_changed(error_status))
                    .await
                {
                    log::error!(
                        "Error reporting {data_operation_kind:?} {data_operation_name} status: {e}"
                    );
                }
            }
            DataOperationNotification::UpdatedInvalid => {
                log::info!(
                    "{data_operation_kind:?} {data_operation_name} update invalid notification received"
                );
            }
            DataOperationNotification::Deleted => {
                log::info!(
                    "{data_operation_kind:?} {data_operation_name} deleted notification received"
                );
                break;
            }
        }
    }
}

#[must_use]
pub fn mock_received_data(count: u32) -> Data {
    Data {
        // temp and newTemp
        payload: format!(
            r#"{{
            "temp": {count},
            "newTemp": {}
        }}"#,
            count * 2
        )
        .into(),
        content_type: "application/json".to_string(),
        custom_user_data: Vec::new(),
        timestamp: None,
    }
}

/// Helper function for consistent status reporting behavior across all `*_if_modified` calls.
/// Only reports status on first time (None) or when changing from OK to Error.
/// Skips reporting when status has already been reported and hasn't changed.
fn report_status_if_changed(
    new_status: Result<(), AdrConfigError>,
) -> impl Fn(Option<Result<(), &AdrConfigError>>) -> Option<Result<(), AdrConfigError>> {
    move |current_status| match current_status {
        None => {
            // First time reporting status
            Some(new_status.clone())
        }
        Some(Ok(())) => {
            // Status was OK, report if we now have an error
            if new_status.is_err() {
                Some(new_status.clone())
            } else {
                // Still OK, no need to report again
                None
            }
        }
        Some(Err(_)) => {
            // Status was error, report if we now have OK
            if new_status.is_ok() {
                Some(new_status.clone())
            } else {
                // Still error, no need to report again
                None
            }
        }
    }
}

fn generate_endpoint_status(
    device_endpoint_client: &DeviceEndpointClient,
) -> Result<(), AdrConfigError> {
    // now we should update the status of the device
    match device_endpoint_client
        .specification()
        .endpoints
        .inbound
        .endpoint_type
        .as_str()
    {
        "rest-thermostat" | "coap-thermostat" => Ok(()),
        unsupported_endpoint_type => {
            // if we don't support the endpoint type, then we can report that error
            log::warn!(
                "Endpoint '{}' not accepted. Endpoint type '{}' not supported.",
                device_endpoint_client
                    .specification()
                    .endpoints
                    .inbound
                    .name,
                unsupported_endpoint_type
            );
            Err(AdrConfigError {
                message: Some("endpoint type is not supported".to_string()),
                ..Default::default()
            })
        }
    }
}

fn generate_asset_status(asset_client: &AssetClient) -> Result<(), AdrConfigError> {
    match asset_client.specification().manufacturer.as_deref() {
        Some("Contoso") | None => Ok(()),
        Some(m) => {
            log::warn!(
                "Asset '{}' not accepted. Manufacturer '{m}' not supported.",
                asset_client.asset_ref().name
            );
            Err(AdrConfigError {
                message: Some("asset manufacturer type is not supported".to_string()),
                ..Default::default()
            })
        }
    }
}

/// NOTE: This is just showing that running discovery concurrently works. In a real world solution,
/// this should be run in a loop and create the discovered devices through an actual disovery process
/// instead of being hard-coded
async fn run_discovery(
    discovery_client: base_connector::adr_discovery::Client,
) -> Result<(), Box<dyn std::error::Error>> {
    let device_name = "my-thermostat".to_string();

    let discovered_inbound_endpoints = HashMap::from([(
        "inbound_endpoint1".to_string(),
        azure_device_registry::models::DiscoveredInboundEndpoint {
            address: "tcp://inbound/endpoint1".to_string(),
            endpoint_type: "rest-thermostat".to_string(),
            supported_authentication_methods: vec![],
            version: Some("1.0.0".to_string()),
            last_updated_on: Some(chrono::Utc::now()),
            additional_configuration: None,
        },
    )]);
    let device = azure_device_registry::models::DiscoveredDevice {
        attributes: HashMap::default(),
        endpoints: Some(azure_device_registry::models::DiscoveredDeviceEndpoints {
            inbound: discovered_inbound_endpoints,
            outbound: None,
        }),
        external_device_id: None,
        manufacturer: Some("Contoso".to_string()),
        model: Some("Device Model".to_string()),
        operating_system: Some("MyOS".to_string()),
        operating_system_version: Some("1.0.0".to_string()),
    };

    match discovery_client
        .create_or_update_discovered_device(device_name, device, "rest-thermostat".to_string())
        .await
    {
        Ok(response) => {
            log::info!("Discovered device created or updated successfully: {response:?}");
            Ok(())
        }
        Err(e) => {
            log::error!("Error creating or updating discovered device: {e}");
            Err(Box::new(e))
        }
    }
}
