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

use std::{fs, path::PathBuf, time::Duration};

use azure_iot_operations_connector::file_mount_azure_device_registry::{
    AssetRef, DeviceEndpointCreateObservation, DeviceEndpointRef, get_mount_path,
};
use env_logger::Builder;

// This example uses a 5-second debounce duration for the file mount observation.
const DEBOUNCE_DURATION: Duration = Duration::from_secs(5);

#[tokio::main(flavor = "current_thread")]
async fn main() -> Result<(), Box<dyn std::error::Error>> {
    Builder::new()
        .filter_level(log::LevelFilter::max())
        .format_timestamp(None)
        .filter_module("notify_debouncer_full", log::LevelFilter::Off)
        .filter_module("notify::inotify", log::LevelFilter::Off)
        .init();

    // Create the observation for device endpoint creation
    let device_creation_observation =
        DeviceEndpointCreateObservation::new(DEBOUNCE_DURATION).unwrap();

    // Creating tasks to run the observation runner and the operator simulator
    let observation_runner_task = tokio::spawn(async {
        observation_runner(device_creation_observation).await;
    });
    let operator_simulator_task = tokio::spawn(async {
        operator_simulator().await;
    });

    // Wait for the tasks to finish
    // The operator simulator task will finish when all device endpoints and assets
    // have been added and removed.
    tokio::select! {
        _ = operator_simulator_task => {
            log::info!("Operator simulator task finished");
        }
        _ = observation_runner_task => {
            panic!("Observation runner task failed");
        }
    }

    log::info!("ADR file mount observation example finished");

    Ok(())
}

// This function runs in a loop, waiting for device creation notifications.
async fn observation_runner(mut device_creation_observation: DeviceEndpointCreateObservation) {
    loop {
        // Wait for a device creation notification
        match device_creation_observation.recv_notification().await {
            Some((device_ref, mut asset_creation_observation)) => {
                log::info!("Device created: {device_ref:?}");

                // Spawn a new task to handle asset creation notifications
                tokio::spawn(async move {
                    loop {
                        // Wait for an asset creation notification
                        if let Some((asset_ref, asset_deletion_token)) =
                            asset_creation_observation.recv_notification().await
                        {
                            log::info!("Asset created: {asset_ref:?}");
                            // Spawn a new task to handle asset deletion
                            tokio::spawn(async move {
                                // Wait for the asset deletion token to be triggered
                                asset_deletion_token.await;
                                log::info!("Asset removed: {asset_ref:?}");
                            });
                        } else {
                            // The asset creation observation has been dropped
                            log::info!("Device removed: {device_ref:?}");
                            break;
                        }
                    }
                });
            }
            None => panic!("device_creation_observer has been dropped"),
        }
    }
}

// ~~~~~~~~~~~~~~~~~ Operator Simulation Helper Structs and Functions ~~~~~~~~~~~~~~~~~~~~~

// This is a simulation of the operator's actions. It creates and removes device endpoints
// and assets in the file mount.
async fn operator_simulator() {
    let file_mount_manager = FileMountManager::new(get_mount_path().unwrap().to_str().unwrap());

    // ADDING DEVICE 1 ENDPOINT 1 WITH ASSETS 1 AND 2

    let (device1_endpoint1, device1_endpoint1_assets) = (
        DeviceEndpointRef {
            device_name: "device1".to_string(),
            endpoint_name: "endpoint1".to_string(),
        },
        vec![
            AssetRef {
                name: "asset1".to_string(),
                device_name: "device1".to_string(),
                endpoint_name: "endpoint1".to_string(),
            },
            AssetRef {
                name: "asset2".to_string(),
                device_name: "device1".to_string(),
                endpoint_name: "endpoint1".to_string(),
            },
        ],
    );

    file_mount_manager.add_device_endpoint(&device1_endpoint1, &device1_endpoint1_assets);

    tokio::time::sleep(DEBOUNCE_DURATION).await;

    // ADDING DEVICE 2 ENDPOINT 2 WITH ASSETS 3 AND 4

    let (device2_endpoint2, device2_endpoint2_assets) = (
        DeviceEndpointRef {
            device_name: "device2".to_string(),
            endpoint_name: "endpoint2".to_string(),
        },
        vec![
            AssetRef {
                name: "asset3".to_string(),
                device_name: "device2".to_string(),
                endpoint_name: "endpoint2".to_string(),
            },
            AssetRef {
                name: "asset4".to_string(),
                device_name: "device2".to_string(),
                endpoint_name: "endpoint2".to_string(),
            },
        ],
    );

    file_mount_manager.add_device_endpoint(&device2_endpoint2, &device2_endpoint2_assets);

    tokio::time::sleep(DEBOUNCE_DURATION).await;

    // REMOVING ALL ASSETS FROM DEVICE 1 ENDPOINT 1

    for asset in &device1_endpoint1_assets {
        file_mount_manager.remove_asset(&device1_endpoint1, asset);
    }

    tokio::time::sleep(DEBOUNCE_DURATION).await;

    // ADDING ASSET 5 TO DEVICE 2 ENDPOINT 2

    let asset5 = AssetRef {
        name: "asset5".to_string(),
        device_name: "device2".to_string(),
        endpoint_name: "endpoint2".to_string(),
    };

    file_mount_manager.add_asset(&device2_endpoint2, &asset5);

    tokio::time::sleep(DEBOUNCE_DURATION).await;

    // REMOVING DEVICE 2 ENDPOINT 2

    file_mount_manager.remove_device_endpoint(&device2_endpoint2);

    tokio::time::sleep(DEBOUNCE_DURATION).await;

    // REMOVIUNG DEVICE 1 ENDPOINT 1

    file_mount_manager.remove_device_endpoint(&device1_endpoint1);

    // Wait for the observation runner to process the removal and exit
    tokio::time::sleep(DEBOUNCE_DURATION * 2).await;
}

// This struct manages the file mount directory and provides methods to add and remove
// device endpoints and assets. It creates a file for each device endpoint, and stores
// the asset names in the file. The file is created in the directory specified by
// the ADR_RESOURCES_NAME_MOUNT_PATH environment variable.
struct FileMountManager {
    dir: PathBuf,
}

impl FileMountManager {
    fn new(dir_name: &str) -> Self {
        Self {
            dir: PathBuf::from(dir_name),
        }
    }

    fn add_device_endpoint(&self, device_endpoint: &DeviceEndpointRef, asset_names: &[AssetRef]) {
        let file_path = self.dir.as_path().join(device_endpoint.to_string());
        let content: Vec<_> = asset_names.iter().map(|asset| asset.name.clone()).collect();
        let content = content.join(";");
        fs::write(file_path, content).unwrap();
    }

    fn remove_device_endpoint(&self, device_endpoint: &DeviceEndpointRef) {
        let file_path = self.dir.as_path().join(device_endpoint.to_string());
        fs::remove_file(file_path).unwrap();
    }

    fn add_asset(&self, device_endpoint: &DeviceEndpointRef, asset: &AssetRef) {
        let file_path = self.dir.as_path().join(device_endpoint.to_string());
        let mut content = fs::read_to_string(&file_path).unwrap();

        // Make sure the asset name is not already present
        if content.contains(asset.name.as_str()) {
            return;
        }
        // Append the asset name to the file
        if !content.is_empty() {
            content.push(';');
        }
        content.push_str(asset.name.as_str());
        fs::write(file_path, content).unwrap();
    }

    fn remove_asset(&self, device_endpoint: &DeviceEndpointRef, asset: &AssetRef) {
        let file_path = self.dir.as_path().join(device_endpoint.to_string());
        let mut content = fs::read_to_string(&file_path).unwrap();

        // Remove the asset name from the file
        content = content
            .split(';')
            .filter(|&name| name != asset.name.as_str())
            .collect::<Vec<_>>()
            .join(";");

        fs::write(file_path, content).unwrap();
    }
}
