// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

//! This example demonstrates how to use the Azure Device Registry (ADR) file mount
//! observation feature of the Azure IoT Operations Connector.
//!
//! To use the example, set the `ADR_RESOURCES_NAME_MOUNT_PATH` environment variable to the
//! directory where the file mount will be created. The example will create
//! a directory structure in that location, and simulate the addition and removal
//! of device endpoints and assets.
//!
//! NOTE: Make sure that the environment variable folder exists and is empty before running the example.

use std::{collections::HashMap, fs, path::PathBuf, time::Duration};

use azure_iot_operations_connector::filemount::{
    azure_device_registry::{
        AssetRef, DeviceEndpointCreateObservation, DeviceEndpointRef, get_mount_path,
    },
    connector_config::ConnectorConfiguration,
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
    let connector_config = ConnectorConfiguration::new_from_deployment()?;
    let mqtt_connection_settings = connector_config.to_mqtt_connection_settings("0")?;
    // let mqtt_connection_settings =
    //     azure_iot_operations_mqtt::MqttConnectionSettings::try_from(connector_config.clone())?;
    // let mqtt_connection_settings = MqttConnectionSettingsBuilder::default()
    //     .client_id("mounted-connector-template-2-instance-statefulset")
    //     .hostname("localhost")
    //     .tcp_port(1883u16)
    //     .use_tls(false)
    //     .build()?;

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
        // adr_client_tasks(azure_device_registry_client, session.create_exit_handle()),
        // observation_runner(device_creation_observation),
        run_program(device_creation_observation, azure_device_registry_client),
        // operator_simulator(), // TODO: remove once using real mounted config
        session.run(),
    );
    r.1?;
    Ok(())

    // Creating tasks to run the observation runner and the operator simulator
    // let observation_runner_task = tokio::spawn(async {
    //     observation_runner(device_creation_observation).await;
    // });
    // let operator_simulator_task = tokio::spawn(async {
    //     operator_simulator().await;
    // });

    // // Wait for the tasks to finish
    // // The operator simulator task will finish when all device endpoints and assets
    // // have been added and removed.
    // tokio::select! {
    //     _ = operator_simulator_task => {
    //         log::info!("Operator simulator task finished");
    //     }
    //     _ = observation_runner_task => {
    //         panic!("Observation runner task failed");
    //     }
    // }

    // log::info!("ADR file mount observation example finished");

    // Ok(())
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
                        for (endpoint_name, endpoint) in device.specification.endpoints.inbound {
                            if endpoint.endpoint_type == "rest-thermostat"
                                || endpoint.endpoint_type == "coap-thermostat"
                            {
                                log::info!("Endpoint '{endpoint_name}' accepted");
                                // adding endpoint to status hashmap with None ConfigError to show that we accept the endpoint with no errors
                                endpoint_statuses.insert(endpoint_name, None);
                            } else {
                                any_errors = true;
                                // if we don't support the endpoint type, then we can report that error
                                log::warn!(
                                    "Endpoint '{endpoint_name}' not accepted. Endpoint type '{}' not supported.",
                                    endpoint.endpoint_type
                                );
                                endpoint_statuses.insert(
                                    endpoint_name,
                                    Some(azure_device_registry::ConfigError {
                                        message: Some("endpoint type is not supported".to_string()),
                                        ..azure_device_registry::ConfigError::default()
                                    }),
                                );
                            }
                        }
                        let status = azure_device_registry::DeviceStatus {
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
                                                    dataset_statuses.push(azure_device_registry::AssetDatasetEventStreamStatus {
                                                    error: None,
                                                    message_schema_reference: None,
                                                    name: dataset.name,
                                                });
                                                }
                                                let updated_status = azure_device_registry::AssetStatus {
                                                config: Some(azure_device_registry::StatusConfig {
                                                    version: asset.specification.version,
                                                    ..azure_device_registry::StatusConfig::default()
                                                }),
                                                datasets_schema: Some(dataset_statuses),
                                                ..azure_device_registry::AssetStatus::default()
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

// ~~~~~~~~~~~~~~~~~ Operator Simulation Helper Structs and Functions ~~~~~~~~~~~~~~~~~~~~~

// This is a simulation of the operator's actions. It creates and removes device endpoints
// and assets in the file mount.
// async fn operator_simulator() {
//     let file_mount_manager = FileMountManager::new(get_mount_path().unwrap().to_str().unwrap());

//     // ADDING DEVICE 1 ENDPOINT 1 WITH ASSETS 1 AND 2

//     let (device1_endpoint1, device1_endpoint1_assets) = (
//         DeviceEndpointRef {
//             device_name: "my-thermostat".to_string(),
//             inbound_endpoint_name: "my-rest-endpoint".to_string(),
//         },
//         vec![
//             AssetRef {
//                 name: "my-rest-thermostat-asset".to_string(),
//                 device_name: "my-thermostat".to_string(),
//                 inbound_endpoint_name: "my-rest-endpoint".to_string(),
//             },
//             AssetRef {
//                 name: "my-rest-smart-thermostat-asset".to_string(),
//                 device_name: "my-thermostat".to_string(),
//                 inbound_endpoint_name: "my-rest-endpoint".to_string(),
//             },
//         ],
//     );

//     file_mount_manager.add_device_endpoint(&device1_endpoint1, &device1_endpoint1_assets);

//     tokio::time::sleep(DEBOUNCE_DURATION).await;

//     // ADDING DEVICE 2 ENDPOINT 2 WITH ASSETS 3 AND 4

//     let (device2_endpoint2, device2_endpoint2_assets) = (
//         DeviceEndpointRef {
//             device_name: "my-thermostat".to_string(),
//             inbound_endpoint_name: "my-coap-endpoint".to_string(),
//         },
//         vec![
//             AssetRef {
//                 name: "my-coap-thermostat-asset".to_string(),
//                 device_name: "my-thermostat".to_string(),
//                 inbound_endpoint_name: "my-coap-endpoint".to_string(),
//             },
//             AssetRef {
//                 name: "my-coap-smart-thermostat-asset".to_string(),
//                 device_name: "my-thermostat".to_string(),
//                 inbound_endpoint_name: "my-coap-endpoint".to_string(),
//             },
//         ],
//     );

//     file_mount_manager.add_device_endpoint(&device2_endpoint2, &device2_endpoint2_assets);

//     tokio::time::sleep(DEBOUNCE_DURATION).await;

//     // REMOVING ALL ASSETS FROM DEVICE 1 ENDPOINT 1

//     for asset in &device1_endpoint1_assets {
//         file_mount_manager.remove_asset(&device1_endpoint1, asset);
//     }

//     tokio::time::sleep(DEBOUNCE_DURATION).await;

//     // ADDING ASSET 5 TO DEVICE 2 ENDPOINT 2

//     let asset5 = AssetRef {
//         name: "my-coap-simple-thermostat-asset".to_string(),
//         device_name: "my-thermostat".to_string(),
//         inbound_endpoint_name: "my-coap-endpoint".to_string(),
//     };

//     file_mount_manager.add_asset(&device2_endpoint2, &asset5);

//     tokio::time::sleep(DEBOUNCE_DURATION).await;

//     // REMOVING DEVICE 2 ENDPOINT 2

//     file_mount_manager.remove_device_endpoint(&device2_endpoint2);

//     tokio::time::sleep(DEBOUNCE_DURATION).await;

//     // REMOVIUNG DEVICE 1 ENDPOINT 1

//     file_mount_manager.remove_device_endpoint(&device1_endpoint1);

//     // Wait for the observation runner to process the removal and exit
//     tokio::time::sleep(DEBOUNCE_DURATION * 2).await;
// }

// This struct manages the file mount directory and provides methods to add and remove
// device endpoints and assets. It creates a file for each device endpoint, and stores
// the asset names in the file. The file is created in the directory specified by
// the ADR_RESOURCES_NAME_MOUNT_PATH environment variable.
// struct FileMountManager {
//     dir: PathBuf,
// }

// impl FileMountManager {
//     fn new(dir_name: &str) -> Self {
//         Self {
//             dir: PathBuf::from(dir_name),
//         }
//     }

//     fn add_device_endpoint(&self, device_endpoint: &DeviceEndpointRef, asset_names: &[AssetRef]) {
//         let file_path = self.dir.as_path().join(device_endpoint.to_string());
//         let content: Vec<_> = asset_names.iter().map(|asset| asset.name.clone()).collect();
//         let content = content.join(";");
//         fs::write(file_path, content).unwrap();
//     }

//     fn remove_device_endpoint(&self, device_endpoint: &DeviceEndpointRef) {
//         let file_path = self.dir.as_path().join(device_endpoint.to_string());
//         fs::remove_file(file_path).unwrap();
//     }

//     fn add_asset(&self, device_endpoint: &DeviceEndpointRef, asset: &AssetRef) {
//         let file_path = self.dir.as_path().join(device_endpoint.to_string());
//         let mut content = fs::read_to_string(&file_path).unwrap();

//         // Make sure the asset name is not already present
//         if content.contains(asset.name.as_str()) {
//             return;
//         }
//         // Append the asset name to the file
//         if !content.is_empty() {
//             content.push(';');
//         }
//         content.push_str(asset.name.as_str());
//         fs::write(file_path, content).unwrap();
//     }

//     fn remove_asset(&self, device_endpoint: &DeviceEndpointRef, asset: &AssetRef) {
//         let file_path = self.dir.as_path().join(device_endpoint.to_string());
//         let mut content = fs::read_to_string(&file_path).unwrap();

//         // Remove the asset name from the file
//         content = content
//             .split(';')
//             .filter(|&name| name != asset.name.as_str())
//             .collect::<Vec<_>>()
//             .join(";");

//         fs::write(file_path, content).unwrap();
//     }
// }
