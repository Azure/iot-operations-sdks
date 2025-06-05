// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

//! This example demonstrates how to use the combination of the file mounted
//! connector config, the Azure Device Registry (ADR) file mount observation
//! feature, and the ADR Client to initialize a deployed Connector (from the
//! deployed connector config artifacts), get Device and Asset names (from the
//! deployed ADR file mounted artifacts), and then get the full definitions
//! for these Devices and Assets from the ADR service (using the ADR client).
//!
//! This sample simply logs the device and asset information received - a real
//! connector would then use these to connect to the device/inbound endpoints
//! and start operations defined in the assets.
//!
//! To deploy and test this example, see instructions in `rust/azure_iot_operations_connector/README.md`

use std::{collections::HashMap, time::Duration};

use azure_iot_operations_connector::filemount::{
    azure_device_registry::DeviceEndpointCreateObservation, connector_artifacts::ConnectorArtifacts,
};
use azure_iot_operations_mqtt::session::{Session, SessionManagedClient, SessionOptionsBuilder};
use azure_iot_operations_protocol::application::ApplicationContextBuilder;
use azure_iot_operations_services::azure_device_registry;
use env_logger::Builder;

// This example uses a 5-second debounce duration for the file mount observation.
const DEBOUNCE_DURATION: Duration = Duration::from_secs(5);

#[tokio::main(flavor = "current_thread")]
async fn main() -> Result<(), Box<dyn std::error::Error>> {
    Builder::new()
        .filter_level(log::LevelFilter::max())
        .format_timestamp(None)
        .filter_module("rumqttc", log::LevelFilter::Warn)
        .filter_module("azure_iot_operations_mqtt", log::LevelFilter::Warn)
        .filter_module("azure_iot_operations_protocol", log::LevelFilter::Warn)
        .filter_module("notify_debouncer_full", log::LevelFilter::Off)
        .filter_module("notify::inotify", log::LevelFilter::Off)
        .init();

    // Get Connector Configuration
    let connector_config = ConnectorArtifacts::new_from_deployment()?;
    let mqtt_connection_settings = connector_config.to_mqtt_connection_settings("0")?;

    // Create Session
    let session_options = SessionOptionsBuilder::default()
        .connection_settings(mqtt_connection_settings)
        .build()?;
    let session = Session::new(session_options)?;

    // Create an ApplicationContext
    let application_context = ApplicationContextBuilder::default().build()?;

    // Create an Azure Device Registry Client
    let azure_device_registry_client = azure_device_registry::Client::new(
        application_context,
        session.create_managed_client(),
        azure_device_registry::ClientOptions::default(),
    )?;

    // Create the observation for device endpoint creation
    let device_creation_observation =
        DeviceEndpointCreateObservation::new(DEBOUNCE_DURATION).unwrap();

    // Run the Session and the Azure Device Registry operations concurrently
    let r = tokio::join!(
        run_program(device_creation_observation, azure_device_registry_client),
        session.run(),
    );
    r.1?;
    Ok(())
}

// This function runs in a loop, waiting for device creation notifications.
async fn run_program(
    mut device_creation_observation: DeviceEndpointCreateObservation,
    azure_device_registry_client: azure_device_registry::Client<SessionManagedClient>,
) {
    let timeout = Duration::from_secs(10);

    loop {
        // Wait for a device creation notification
        match device_creation_observation.recv_notification().await {
            Some((device_ref, mut asset_creation_observation)) => {
                log::info!("Device created: {device_ref:?}");

                // spawn a new task to handle device + endpoint update notifications
                match azure_device_registry_client
                    .observe_device_update_notifications(
                        device_ref.device_name.clone(),
                        device_ref.inbound_endpoint_name.clone(),
                        timeout,
                    )
                    .await
                {
                    Ok(mut observation) => {
                        log::debug!("Device observed successfully");
                        tokio::task::spawn({
                            async move {
                                while let Some((notification, _)) =
                                    observation.recv_notification().await
                                {
                                    log::info!("device updated: {notification:?}");
                                }
                                log::info!("device notification receiver closed");
                            }
                        });
                    }
                    Err(e) => {
                        log::error!("Observing for device updates failed: {e}");
                    }
                };

                // Get device + endpoint details from ADR Service and send status update
                match azure_device_registry_client
                    .get_device(
                        device_ref.device_name.clone(),
                        device_ref.inbound_endpoint_name.clone(),
                        timeout,
                    )
                    .await
                {
                    Err(e) => {
                        log::error!("Get device request failed: {e}");
                    }
                    Ok(device) => {
                        log::info!("Device details: {device:?}");
                        // now we should update the status of the device
                        let mut endpoint_statuses = HashMap::new();
                        let mut any_errors = false;
                        for (inbound_endpoint_name, endpoint) in
                            device.specification.endpoints.unwrap().inbound
                        {
                            if endpoint.endpoint_type == "rest-thermostat"
                                || endpoint.endpoint_type == "coap-thermostat"
                            {
                                log::info!("Endpoint '{inbound_endpoint_name}' accepted");
                                // adding endpoint to status hashmap with None ConfigError to show that we accept the endpoint with no errors
                                endpoint_statuses.insert(inbound_endpoint_name, None);
                            } else {
                                any_errors = true;
                                // if we don't support the endpoint type, then we can report that error
                                log::warn!(
                                    "Endpoint '{inbound_endpoint_name}' not accepted. Endpoint type '{}' not supported.",
                                    endpoint.endpoint_type
                                );
                                endpoint_statuses.insert(
                                    inbound_endpoint_name,
                                    Some(azure_device_registry::ConfigError {
                                        message: Some("endpoint type is not supported".to_string()),
                                        ..azure_device_registry::ConfigError::default()
                                    }),
                                );
                            }
                        }
                        let status = azure_device_registry::models::DeviceStatus {
                            config: Some(azure_device_registry::StatusConfig {
                                version: device.specification.version,
                                ..azure_device_registry::StatusConfig::default()
                            }),
                            endpoints: endpoint_statuses,
                        };
                        match azure_device_registry_client
                            .update_device_plus_endpoint_status(
                                device_ref.device_name.clone(),
                                device_ref.inbound_endpoint_name.clone(),
                                status,
                                timeout,
                            )
                            .await
                        {
                            Ok(updated_device) => {
                                log::info!(
                                    "Device returned after status update: {updated_device:?}"
                                );
                            }
                            Err(e) => {
                                log::error!("Update device status request failed: {e}");
                            }
                        };
                        // if we didn't accept the inbound endpoint, then no reason to manage the assets
                        if !any_errors {
                            // Spawn a new task to handle asset creation notifications
                            let azure_device_registry_client_clone =
                                azure_device_registry_client.clone();
                            tokio::spawn(async move {
                                loop {
                                    // Wait for an asset creation notification
                                    if let Some((asset_ref, asset_deletion_token)) =
                                        asset_creation_observation.recv_notification().await
                                    {
                                        log::info!("Asset created: {asset_ref:?}");
                                        // TODO: check if deletion token is already deleted

                                        // spawn a new task to handle asset update notifications
                                        match azure_device_registry_client_clone
                                            .observe_asset_update_notifications(
                                                asset_ref.device_name.clone(),
                                                asset_ref.inbound_endpoint_name.clone(),
                                                asset_ref.name.clone(),
                                                timeout,
                                            )
                                            .await
                                        {
                                            Ok(mut observation) => {
                                                log::info!("Asset observed successfully");
                                                tokio::task::spawn({
                                                    async move {
                                                        // TODO: combine this task with the one observing for delete
                                                        while let Some((notification, _)) =
                                                            observation.recv_notification().await
                                                        {
                                                            log::info!(
                                                                "asset updated: {notification:?}"
                                                            );
                                                        }
                                                        log::info!(
                                                            "asset notification receiver closed"
                                                        );
                                                    }
                                                });
                                            }
                                            Err(e) => {
                                                log::error!(
                                                    "Observing for asset updates failed: {e}"
                                                );
                                            }
                                        };

                                        // Get asset details from ADR Service and send status update
                                        match azure_device_registry_client_clone
                                            .get_asset(
                                                asset_ref.device_name.clone(),
                                                asset_ref.inbound_endpoint_name.clone(),
                                                asset_ref.name.clone(),
                                                timeout,
                                            )
                                            .await
                                        {
                                            Ok(asset) => {
                                                log::info!("Asset details: {asset:?}");
                                                // now we should update the status of the asset
                                                let mut dataset_statuses = Vec::new();
                                                for dataset in asset.specification.datasets {
                                                    dataset_statuses.push(azure_device_registry::models::DatasetEventStreamStatus {
                                                    error: None,
                                                    message_schema_reference: None,
                                                    name: dataset.name,
                                                });
                                                }
                                                let updated_status = azure_device_registry::models::AssetStatus {
                                                config: Some(azure_device_registry::StatusConfig {
                                                    version: asset.specification.version,
                                                    ..azure_device_registry::StatusConfig::default()
                                                }),
                                                datasets: Some(dataset_statuses),
                                                ..azure_device_registry::models::AssetStatus::default()
                                            };
                                                match azure_device_registry_client_clone
                                                    .update_asset_status(
                                                        asset_ref.device_name.clone(),
                                                        asset_ref.inbound_endpoint_name.clone(),
                                                        asset_ref.name.clone(),
                                                        updated_status,
                                                        timeout,
                                                    )
                                                    .await
                                                {
                                                    Ok(updated_asset) => {
                                                        log::info!(
                                                            "Asset returned after status update: {updated_asset:?}"
                                                        );
                                                    }
                                                    Err(e) => {
                                                        log::error!(
                                                            "Update asset status request failed: {e}"
                                                        );
                                                    }
                                                }
                                            }
                                            Err(e) => {
                                                log::error!("Get asset request failed: {e}");
                                            }
                                        }

                                        // Spawn a new task to handle asset deletion
                                        let azure_device_registry_client_clone_2 =
                                            azure_device_registry_client_clone.clone();
                                        tokio::spawn(async move {
                                            // Wait for the asset deletion token to be triggered
                                            asset_deletion_token.await;
                                            log::info!("Asset removed: {asset_ref:?}");
                                            // Unobserve must be called on clean-up to prevent getting notifications for this in the future
                                            match azure_device_registry_client_clone_2
                                                .unobserve_asset_update_notifications(
                                                    asset_ref.device_name.clone(),
                                                    asset_ref.inbound_endpoint_name.clone(),
                                                    asset_ref.name.clone(),
                                                    timeout,
                                                )
                                                .await
                                            {
                                                Ok(()) => {
                                                    log::info!("Asset unobserved successfully");
                                                }
                                                Err(e) => {
                                                    log::error!(
                                                        "Unobserving for Asset updates failed: {e}"
                                                    );
                                                }
                                            };
                                        });
                                    } else {
                                        // The asset creation observation has been dropped
                                        log::info!("Device removed: {device_ref:?}");
                                        // Unobserve must be called on clean-up to prevent getting notifications for this in the future
                                        match azure_device_registry_client_clone
                                            .unobserve_device_update_notifications(
                                                device_ref.device_name.clone(),
                                                device_ref.inbound_endpoint_name.clone(),
                                                timeout,
                                            )
                                            .await
                                        {
                                            Ok(()) => {
                                                log::info!("Device unobserved successfully");
                                            }
                                            Err(e) => {
                                                log::error!(
                                                    "Unobserving for device updates failed: {e}"
                                                );
                                            }
                                        };
                                        break;
                                    }
                                }
                            });
                        }
                    }
                };
            }
            None => panic!("device_creation_observer has been dropped"),
        }
    }
    // this loop never ends, so no cleanup is necessary (otherwise we'd call adr client shutdown and session exit)
}
