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
            DeviceEndpointClient, DeviceEndpointClientCreationObservation, SchemaModifyResult,
        },
    },
    data_processor::derived_json,
    deployment_artifacts::connector::ConnectorArtifacts,
};
use azure_iot_operations_otel::Otel;
use azure_iot_operations_protocol::application::ApplicationContextBuilder;
use azure_iot_operations_services::azure_device_registry;

/// Only reports status on first time (None) and when changing from OK to Error.
/// Skips reporting when status has already been reported and hasn't changed.
macro_rules! report_status_if_changed {
    ($log_identifier:expr, $new_status:expr) => {
        |current_status| match current_status {
            None => {
                log::info!("{} reporting status", $log_identifier);
                Some($new_status.clone())
            }
            Some(Ok(())) => {
                // Status was OK, report if we now have an error
                if $new_status.is_err() {
                    log::info!("{} reporting error on ok status", $log_identifier);
                    Some($new_status.clone())
                } else {
                    // Still OK, no need to report again
                    log::debug!("{} reporting no change", $log_identifier);
                    None
                }
            }
            Some(Err(_)) => {
                // Status was an error, we leave it as is
                log::debug!("{} reporting no change", $log_identifier);
                None
            }
        }
    };
}

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
        let device_endpoint_client = device_creation_observation.recv_notification().await;
        let log_identifier = format!("[{}]", device_endpoint_client.device_endpoint_ref());
        log::info!("Device created: {device_endpoint_client:?}");

        // Start handling the assets for this device endpoint
        // if we didn't accept the inbound endpoint, then we still want to run this to wait for updates
        tokio::task::spawn(run_device(log_identifier, device_endpoint_client));
    }
}

// This function runs in a loop, waiting for asset creation notifications.
async fn run_device(log_identifier: String, mut device_endpoint_client: DeviceEndpointClient) {
    // Get the status reporter for this device endpoint - create once and reuse
    let device_endpoint_reporter = device_endpoint_client.get_status_reporter();

    // Update the status of the device
    if let Err(e) = device_endpoint_reporter
        .report_device_status_if_modified(report_status_if_changed!(
            &log_identifier,
            Ok::<(), AdrConfigError>(())
        ))
        .await
    {
        log::error!("{log_identifier} Error reporting device status: {e}");
    }

    // Update the status of the endpoint
    if let Err(e) = device_endpoint_reporter
        .report_endpoint_status_if_modified(report_status_if_changed!(
            &log_identifier,
            generate_endpoint_status(&device_endpoint_client)
        ))
        .await
    {
        log::error!("{log_identifier} Error reporting endpoint status: {e}");
    }

    loop {
        match device_endpoint_client.recv_notification().await {
            ClientNotification::Deleted => {
                log::warn!("{log_identifier} Device Endpoint deleted");
                break;
            }
            ClientNotification::Updated => {
                log::info!("{log_identifier} Device updated: {device_endpoint_client:?}");

                // Update device status - usually only on first report or error changes
                if let Err(e) = device_endpoint_reporter
                    .report_device_status_if_modified(report_status_if_changed!(
                        &log_identifier,
                        Ok::<(), AdrConfigError>(())
                    ))
                    .await
                {
                    log::error!("{log_identifier} Error reporting device status: {e}");
                }

                // Update the status of the endpoint
                if let Err(e) = device_endpoint_reporter
                    .report_endpoint_status_if_modified(report_status_if_changed!(
                        &log_identifier,
                        generate_endpoint_status(&device_endpoint_client)
                    ))
                    .await
                {
                    log::error!("{log_identifier} Error reporting endpoint status: {e}");
                }
            }
            ClientNotification::Created(asset_client) => {
                let asset_log_identifier =
                    format!("{log_identifier}[{}]", asset_client.asset_ref().name);
                log::info!("{asset_log_identifier} Asset created: {asset_client:?}");

                // Start handling the datasets for this asset
                // if we didn't accept the asset, then we still want to run this to wait for updates
                tokio::task::spawn(run_asset(asset_log_identifier, asset_client));
            }
        }
    }
}

// This function runs in a loop, waiting for dataset creation notifications.
async fn run_asset(asset_log_identifier: String, mut asset_client: AssetClient) {
    // Get the status reporter for this asset - create once and reuse
    let asset_reporter = asset_client.get_status_reporter();

    if let Err(e) = asset_reporter
        .report_status_if_modified(report_status_if_changed!(
            &asset_log_identifier,
            generate_asset_status(&asset_client)
        ))
        .await
    {
        log::error!("{asset_log_identifier} Error reporting asset status: {e}");
    };

    loop {
        match asset_client.recv_notification().await {
            ClientNotification::Updated => {
                log::info!("{asset_log_identifier} Asset updated");

                if let Err(e) = asset_reporter
                    .report_status_if_modified(report_status_if_changed!(
                        &asset_log_identifier,
                        generate_asset_status(&asset_client)
                    ))
                    .await
                {
                    log::error!("{asset_log_identifier} Error reporting asset status: {e}");
                }
            }
            ClientNotification::Deleted => {
                log::warn!("{asset_log_identifier} Asset has been deleted");
                break;
            }
            ClientNotification::Created(data_operation_client) => {
                let data_operation_log_identifier = format!(
                    "{asset_log_identifier}[{}]",
                    data_operation_client
                        .data_operation_ref()
                        .data_operation_name
                );
                log::info!(
                    "{data_operation_log_identifier} Data Operation Created: {data_operation_client:?}"
                );
                if let DataOperationKind::Dataset = data_operation_client
                    .data_operation_ref()
                    .data_operation_kind
                {
                    tokio::task::spawn(run_dataset(
                        data_operation_log_identifier,
                        data_operation_client,
                    ));
                } else {
                    tokio::task::spawn(handle_unsupported_data_operation(
                        data_operation_log_identifier,
                        data_operation_client,
                    ));
                }
            }
        }
    }
}

/// Note, this function takes in a `DataOperationClient`, but we know it is specifically a `Dataset`
/// because we already filtered out non-dataset `DataOperationClient`s in the `run_asset` function.
async fn run_dataset(log_identifier: String, mut data_operation_client: DataOperationClient) {
    // Get the status reporter for this data operation - create once and reuse
    let data_operation_reporter = data_operation_client.get_status_reporter();

    // now we should update the status of the dataset and report the message schema
    if let Err(e) = data_operation_reporter
        .report_status_if_modified(report_status_if_changed!(
            &log_identifier,
            Ok::<(), AdrConfigError>(())
        ))
        .await
    {
        log::error!("{log_identifier} Error reporting dataset status: {e}");
    }

    let sample_data = mock_received_data(0);

    let mut local_message_schema = derived_json::create_schema(&sample_data).unwrap();
    let mut local_schema_reference = match data_operation_client
        .report_message_schema_if_modified(|_current_schema| Some(local_message_schema.clone()))
        .await
    {
        Ok(status_reported) => match status_reported {
            SchemaModifyResult::Reported(schema_ref) => {
                log::info!("{log_identifier} Message Schema reported successfully: {schema_ref:?}");
                Some(schema_ref)
            }
            SchemaModifyResult::NotModified => {
                log::info!("{log_identifier} Message Schema already exists, not reporting");
                unreachable!(); // Always report the first time
            }
        },
        Err(e) => {
            log::error!("{log_identifier} Error reporting message schema: {e}");
            None
        }
    };
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
                        log::info!("{log_identifier} Dataset updated: {data_operation_client:?}");

                        // now we should update the status of the dataset and report the message schema
                        if let Err(e) = data_operation_reporter
                            .report_status_if_modified(report_status_if_changed!(&log_identifier, Ok::<(), AdrConfigError>(())))
                            .await
                        {
                            log::error!("{log_identifier} Error reporting dataset status: {e}");
                        }

                        // Update the local schema reference
                        local_schema_reference = data_operation_client.message_schema_reference().await;


                        dataset_valid = true;
                    },
                    DataOperationNotification::UpdatedInvalid => {
                        log::warn!("{log_identifier} Dataset has invalid update. Wait for new dataset update.");
                        dataset_valid = false;
                    },
                    DataOperationNotification::Deleted => {
                        log::warn!("{log_identifier} Dataset has been deleted. No more dataset updates will be received");
                        break;
                    }
                }
            },
            _ = timer.tick(), if dataset_valid => {
                let sample_data = mock_received_data(count);

                let current_message_schema =
                    derived_json::create_schema(&sample_data).unwrap();
                // Report schema only if there isn't already one
                match data_operation_client
                    .report_message_schema_if_modified(|current_schema_reference| {
                        // Only report schema if there isn't already one, or if schema has changed
                        if local_schema_reference.is_none()
                            || current_schema_reference.is_none()
                            || current_schema_reference != local_schema_reference.as_ref()
                            || current_message_schema != local_message_schema
                        {
                            Some(current_message_schema.clone())
                        } else {
                            None
                        }
                    })
                    .await
                {
                    Ok(status_reported) => {
                        match status_reported {
                            SchemaModifyResult::Reported(schema_ref) => {
                                local_message_schema = current_message_schema;
                                local_schema_reference = Some(schema_ref);

                                log::info!("{log_identifier} Message Schema reported successfully: {local_schema_reference:?}");
                            }
                            SchemaModifyResult::NotModified => {
                                log::info!("{log_identifier} Message Schema already exists, not reporting");
                            }
                        }
                    }
                    Err(e) => {
                        log::error!("{log_identifier} Error reporting message schema: {e}");
                        continue; // Can't forward data without a schema reported
                    }
                }

                match data_operation_client.forward_data(sample_data).await {
                    Ok(()) => {
                        log::info!(
                            "{log_identifier} data {count} for {} forwarded",
                            data_operation_client.data_operation_ref().data_operation_name
                        );
                        count += 1;
                    }
                    Err(e) => log::error!("{log_identifier} error forwarding data: {e}"),
                }
            }
        }
    }
}

/// Small handler to indicate lack of stream/event support in this connector
async fn handle_unsupported_data_operation(
    log_identifier: String,
    mut data_operation_client: DataOperationClient,
) {
    let data_operation_kind = data_operation_client
        .data_operation_ref()
        .data_operation_kind;
    let data_operation_name = data_operation_client
        .data_operation_ref()
        .data_operation_name
        .clone();
    log::warn!(
        "{log_identifier} Data Operation kind {data_operation_kind:?} not supported for this connector"
    );

    // Get the status reporter for this data operation - create once and reuse
    let data_operation_reporter = data_operation_client.get_status_reporter();

    // Report invalid definition to adr
    let error_status = Err(AdrConfigError {
        message: Some(format!(
            "Data Operation kind {data_operation_kind:?} not supported for this connector",
        )),
        ..Default::default()
    });

    if let Err(e) = data_operation_reporter
        .report_status_if_modified(report_status_if_changed!(&log_identifier, &error_status))
        .await
    {
        log::error!(
            "{log_identifier} Error reporting {data_operation_kind:?} {data_operation_name} status: {e}"
        );
    }

    // While the unsupported data operation client is active, we should keep polling for updates
    // to handle cases where it is deleted and to continue reporting errors if it is
    // incorrectly updated.
    loop {
        match data_operation_client.recv_notification().await {
            DataOperationNotification::Updated => {
                log::warn!(
                    "{log_identifier} {data_operation_name} update notification received. {data_operation_kind:?} is not supported for the this Connector",
                );

                let error_status = Err(AdrConfigError {
                    message: Some(format!(
                        "Data Operation kind {data_operation_kind:?} not supported for this connector",
                    )),
                    ..Default::default()
                });

                if let Err(e) = data_operation_reporter
                    .report_status_if_modified(report_status_if_changed!(
                        &log_identifier,
                        error_status
                    ))
                    .await
                {
                    log::error!(
                        "{log_identifier} Error reporting {data_operation_kind:?} {data_operation_name} status: {e}"
                    );
                }
            }
            DataOperationNotification::UpdatedInvalid => {
                log::info!(
                    "{log_identifier} {data_operation_kind:?} {data_operation_name} update invalid notification received"
                );
            }
            DataOperationNotification::Deleted => {
                log::info!(
                    "{log_identifier} {data_operation_kind:?} {data_operation_name} deleted notification received"
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
