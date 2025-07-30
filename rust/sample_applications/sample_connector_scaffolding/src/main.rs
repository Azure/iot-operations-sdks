// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

//! # Connector Scaffolding Template
//!
//! This crate provides a scaffolding template for building edge applications targeting Azure IoT Operations.
//! It demonstrates how to structure device, asset, and dataset handlers, focusing on the lifecycle of creation,
//! updating, deletion and status reporting. The sample logic assumes periodic sampling of an endpoint at a fixed interval.
//!
//! See `IMPLEMENT` comments in the code for areas that need to be implemented or customized.
//! See 'NOTE' comments for areas that may need to be considered.
//!
//! ## Major Components and Flow
//!
//! ### Device Handler
//! - Responsible for orchestrating asset creation by spawning new asset handlers.
//! - On startup or configuration change, the device handler creates asset handlers for each asset defined in the configuration.
//! - Handles updates to itself and propagates changes.
//!
//! ### Asset Handler
//! - Responsible for orchestrating dataset creation by spawning new dataset handlers.
//! - On creation, the asset handler sets up dataset handlers for each dataset associated with the asset.
//! - Handles updates to its configuration or state and propagates changes to its datasets.
//!
//! ### Dataset Handler
//! - Handles the ingestion, transformation, and forwarding of data samples collected by the asset handler.
//! - Can report status for itself, the asset, or device endpoint.
//!
//! ### Status Reporting
//! - Each handler (device, asset, dataset) is responsible for reporting its status back to Azure IoT Operations.
//!
//! ## Extending the Scaffold
//! - Implement custom sampling logic in the dataset handler.
//! - Extend dataset handlers for custom data processing or integration.
//! - Integrate additional status reporting as needed.
//!
//! For more details, refer to the the [Azure IoT Operations SDK documentation](https://github.com/Azure/iot-operations-sdks).

use std::time::Duration;

use azure_iot_operations_connector::{
    Data,
    base_connector::{
        BaseConnector,
        managed_azure_device_registry::{
            AssetClient, ClientNotification, DatasetClient, DatasetNotification,
            DeviceEndpointClient, DeviceEndpointClientCreationObservation,
        },
    },
    data_processor::derived_json,
    deployment_artifacts::connector::ConnectorArtifacts,
};
use azure_iot_operations_otel::Otel;
use azure_iot_operations_protocol::{
    application::ApplicationContextBuilder, common::hybrid_logical_clock::HybridLogicalClock,
};
use tokio::sync::watch;

const DEFAULT_SAMPLING_INTERVAL: Duration = Duration::from_millis(10000); // Default sampling interval in milliseconds
const OTEL_TAG: &str = "connector_scaffolding_template"; // IMPLEMENT: Change this to a unique tag for your connector
const DEFAULT_LOG_LEVEL: &str =
    "warn,sample_connector_scaffolding=info,azure_iot_operations_connector=info"; // IMPLEMENT: Change this to a unique log level for your connector and change the tag to match the crate name

#[tokio::main]
async fn main() -> Result<(), Box<dyn std::error::Error>> {
    // Initialize the logger
    // TODO: Use a more sophisticated logger configuration in production
    env_logger::Builder::new()
        .filter_level(log::LevelFilter::Debug)
        .format_timestamp(None)
        .filter_module("rumqttc", log::LevelFilter::Warn)
        .filter_module("reqwest", log::LevelFilter::Warn)
        .filter_module("azure_iot_operations_mqtt", log::LevelFilter::Warn)
        .filter_module("azure_iot_operations_protocol", log::LevelFilter::Warn)
        .filter_module("azure_iot_operations_services", log::LevelFilter::Warn)
        .filter_module("azure_iot_operations_connector", log::LevelFilter::Warn)
        .filter_module("notify_debouncer_full", log::LevelFilter::Off)
        .filter_module("notify::inotify", log::LevelFilter::Off)
        .init();

    log::info!("Starting connector");

    // Create the connector artifacts from the deployment, IMPLEMENT: Use them as needed
    let connector_artifacts = ConnectorArtifacts::new_from_deployment()?;

    // Initialize the OTEL logger / exporter
    let otel_config = connector_artifacts.to_otel_config(OTEL_TAG, DEFAULT_LOG_LEVEL);
    let mut otel_exporter = Otel::new(otel_config);
    let otel_task = otel_exporter.run();

    // Create the appplication context used by the AIO SDK
    let application_context = ApplicationContextBuilder::default().build()?;

    // Create the Base Connector to handle device endpoints, assets, and datasets creation, update and deletion notifications plus status reporting.
    let base_connector = BaseConnector::new(application_context, connector_artifacts)?;

    // Create a device endpoint client creation observation
    let device_endpoint_client_creation_observation =
        base_connector.create_device_endpoint_client_create_observation();

    // Run the session and the base connector concurrently, ending the application if either end (both should run forever unless there are fatal errors)
    tokio::select! {
        () = receive_device_endpoints(device_endpoint_client_creation_observation) => {
            log::warn!("Connector Application tasks ended");
            Ok(())
        },
        res = base_connector.run() => {
            match res {
                Ok(()) => {
                    log::info!("Base connector run completed successfully");
                    Ok(())
                }
                Err(e) => {
                    log::error!("Base connector run failed: {e}");
                    Err(Box::new(e))?
                }
            }
        }
        res = otel_task => {
            match res {
                Ok(()) => {
                    log::info!("OTEL run finished successfully");
                    Ok(())
                }
                Err(e) => {
                    log::error!("OTEL run failed: {e}");
                    Err(Box::new(e))?
                }
            }
        }
    }
}

/// Receives a device endpoint and creates a device handler for it.
///
/// # Arguments
/// * `device_endpoint_client_creation_observation` - The device endpoint client creation observation.
async fn receive_device_endpoints(
    mut device_endpoint_client_creation_observation: DeviceEndpointClientCreationObservation,
) {
    loop {
        let device_endpoint_client = device_endpoint_client_creation_observation
            .recv_notification()
            .await;

        // The log identifier for the device endpoint is used for logging purposes.
        let device_endpoint_log_identifier = {
            let device_endpoint_ref = device_endpoint_client.device_endpoint_ref();
            format!(
                "[DE: {}_{}]",
                device_endpoint_ref.device_name, device_endpoint_ref.inbound_endpoint_name
            )
        };
        log::info!("{device_endpoint_log_identifier} Device endpoint created");

        tokio::task::spawn(device_handler(
            device_endpoint_log_identifier,
            device_endpoint_client,
        ));
    }
}

/// Handles the device endpoint and receives the asset creation notifications with which it will
/// create asset handlers.
///
/// # Arguments
/// * `device_endpoint_log_identifier` - A string identifier for the device endpoint, used for logging.
/// * `device_endpoint_client` - The device endpoint client.
async fn device_handler(
    device_endpoint_log_identifier: String,
    mut device_endpoint_client: DeviceEndpointClient,
) {
    // This watcher is used to notify the dataset handler whether the device endpoint is healthy and sampling should happen
    let device_endpoint_ready_watcher_tx = watch::Sender::new(false);

    // IMPLEMENT: Reject endpoint types that are not this connector's type.

    // IMPLEMENT: Validate the device endpoint specification and report any errors if there are any.

    // Here is one thing that should be validated for most connectors, although it won't be a config error if it's not enabled
    if device_endpoint_client
        .specification()
        .enabled
        .is_some_and(|enabled| !enabled)
    {
        log::warn!(
            "{device_endpoint_log_identifier} Device endpoint is disabled, waiting for update"
        );
        // Notify any lower components that the device endpoint is not in a ready state
        device_endpoint_ready_watcher_tx.send_if_modified(send_if_modified_fn(false));
    }
    // Only perform connection if the device endpoint is enabled and validated
    else {
        // IMPLEMENT: Connection logic may be handled at this level if this Connector Application maintains a persistent connection to the device endpoint.
        // If knowledge of a successful connection can't be determined at this level, communication will need to be added from the location of
        // the connection logic to this level to report the device and endpoint statuses.

        // Notify any lower components that the device endpoint is in a ready state
        device_endpoint_ready_watcher_tx.send_if_modified(send_if_modified_fn(true));
        // If there was an error, notify any lower components that the device endpoint is not in a ready state while we wait for an update
        // device_endpoint_ready_watcher_tx.send_if_modified(send_if_modified_fn(false));
    }

    // If the connection is successful or the device wasn't enabled and there weren't configuration errors, report the device and endpoint statuses.
    // Modify this to report any errors if there are any
    match device_endpoint_client.report_status(Ok(()), Ok(())).await {
        Ok(()) => {
            log::debug!("{device_endpoint_log_identifier} Endpoint status reported as OK");
        }
        Err(e) => {
            log::error!("{device_endpoint_log_identifier} Failed to report endpoint status: {e}");
        }
    }

    // Listen for DeviceEndpointClient updates/deletion and new AssetClients
    loop {
        match device_endpoint_client.recv_notification().await {
            ClientNotification::Updated => {
                log::info!(
                    "{device_endpoint_log_identifier} Device endpoint update notification received"
                );

                // IMPLEMENT: Add custom device endpoint update logic here (all items at the beginning of `device_handler` would apply here as well)

                // Here is one thing that should be validated for most connectors, although it won't be a config error if it's not enabled
                if device_endpoint_client
                    .specification()
                    .enabled
                    .is_some_and(|enabled| !enabled)
                {
                    log::warn!(
                        "{device_endpoint_log_identifier} Device endpoint is disabled, waiting for update"
                    );
                    // Notify any lower components that the device endpoint is not in a ready state
                    device_endpoint_ready_watcher_tx.send_if_modified(send_if_modified_fn(false));
                } else {
                    // Notify any lower components that the device endpoint is in a ready state (this notification will only be sent if that wasn't already true)
                    device_endpoint_ready_watcher_tx.send_if_modified(send_if_modified_fn(true));
                }

                // For this example, we will assume that the device endpoint specification is OK. Modify this to report any errors
                match device_endpoint_client.report_status(Ok(()), Ok(())).await {
                    Ok(()) => {
                        log::debug!(
                            "{device_endpoint_log_identifier} Endpoint status reported as OK"
                        );
                    }
                    Err(e) => {
                        log::error!(
                            "{device_endpoint_log_identifier} Failed to report endpoint status: {e}"
                        );
                    }
                }
            }
            ClientNotification::Created(asset_client) => {
                let asset_log_identifier = {
                    let asset_ref = asset_client.asset_ref();
                    format!("{device_endpoint_log_identifier}[A: {}]", asset_ref.name)
                };
                log::info!("{asset_log_identifier} Asset created");

                // Handle asset creation
                tokio::task::spawn(asset_handler(
                    asset_log_identifier,
                    asset_client,
                    device_endpoint_ready_watcher_tx.subscribe(),
                ));
            }
            ClientNotification::Deleted => {
                log::info!(
                    "{device_endpoint_log_identifier} Device endpoint deleted notification received, ending device handler"
                );
                // The device endpoint ready state does not need to be updated here because all lower components will also get deleted
                break;
            }
        }
    }
}

/// Handles the asset and spawns dataset handlers for each dataset.
///
/// # Arguments
/// * `asset_log_identifier` - A string identifier for the asset, used for logging.
/// * `asset_client` - The asset client.
/// * `device_endpoint_ready_watcher_rx` - A watcher for the device endpoint readiness state.
async fn asset_handler(
    asset_log_identifier: String,
    mut asset_client: AssetClient,
    device_endpoint_ready_watcher_rx: watch::Receiver<bool>,
) {
    // This watcher is used to notify the dataset handler whether the asset is healthy and sampling should happen
    let asset_ready_watcher_tx = watch::Sender::new(false);
    // IMPLEMENT: add any Asset validation here and report errors or Ok status
    // If the asset specification has a default_dataset_configuration, then the asset status
    // may not be able to be reported until the dataset level can validate this field.

    // For this example, we will assume that the asset specification is OK. Modify this to report any errors
    match asset_client.report_status(Ok(())).await {
        Ok(()) => {
            log::debug!("{asset_log_identifier} Asset status reported as OK");
        }
        Err(e) => {
            log::error!("{asset_log_identifier} Failed to report asset status: {e}");
        }
    }
    // Notify any datasets that the asset is in a ready state
    asset_ready_watcher_tx.send_if_modified(send_if_modified_fn(true));
    // If there was an error, notify any lower components that the asset is not in a ready state while we wait for an update
    // asset_ready_watcher_tx.send_if_modified(send_if_modified_fn(false));

    // Receive asset updates and dataset creation notifications
    loop {
        match asset_client.recv_notification().await {
            ClientNotification::Updated => {
                log::info!("{asset_log_identifier} Asset update notification received");

                // IMPLEMENT: Add custom asset update/validation logic here

                // For this example, we will assume that the asset specification is OK. Modify this to report any errors
                match asset_client.report_status(Ok(())).await {
                    Ok(()) => {
                        log::debug!("{asset_log_identifier} Asset status reported as OK");
                    }
                    Err(e) => {
                        log::error!("{asset_log_identifier} Failed to report asset status: {e}");
                    }
                }
                // Notify any datasets that the asset is in a ready state (this notification will only be sent if that wasn't already true)
                asset_ready_watcher_tx.send_if_modified(send_if_modified_fn(true));
                // If there was an error, notify any lower components that the asset is not in a ready state while we wait for another update
                // asset_ready_watcher_tx.send_if_modified(send_if_modified_fn(false));
            }
            ClientNotification::Created(dataset_client) => {
                let dataset_log_identifier = {
                    let dataset_ref = dataset_client.dataset_ref();
                    format!("{asset_log_identifier}[DS: {}]", dataset_ref.dataset_name)
                };
                log::info!("{dataset_log_identifier} Dataset created");

                // Handle the new dataset
                tokio::task::spawn(handle_dataset(
                    dataset_log_identifier,
                    dataset_client,
                    asset_ready_watcher_tx.subscribe(),
                    device_endpoint_ready_watcher_rx.clone(),
                ));
            }
            ClientNotification::Deleted => {
                log::info!(
                    "{asset_log_identifier} Asset deleted notification received, ending asset handler"
                );
                // The asset ready state does not need to be updated here because all datasets will also get deleted
                break;
            }
        }
    }
}

/// Handles sampling of data from the dataset.
///
/// # Arguments
/// * `dataset_log_identifier` - A string identifier for the dataset, used for logging.
/// * `dataset_client` - The dataset client.
/// * `asset_ready_watcher_rx` - A watcher for the asset readiness state.
/// * `device_endpoint_ready_watcher_rx` - A watcher for the device endpoint readiness state.
async fn handle_dataset(
    dataset_log_identifier: String,
    mut dataset_client: DatasetClient,
    mut asset_ready_watcher_rx: watch::Receiver<bool>,
    mut device_endpoint_ready_watcher_rx: watch::Receiver<bool>,
) {
    let mut is_asset_ready = *asset_ready_watcher_rx.borrow_and_update();
    let mut is_device_endpoint_ready = *device_endpoint_ready_watcher_rx.borrow_and_update();
    // This boolean tracks if the dataset is ready to be sampled.
    let mut is_dataset_ready;
    // This boolean tracks if the status for the dataset has been reported.
    let mut is_dataset_reported = false;

    // Extract the dataset definition from the dataset client
    let mut _local_dataset_definition = dataset_client.dataset_definition().clone();
    // IMPLEMENT: Verify the dataset definition is OK

    // For this example, we will assume that the dataset definition is OK, see below for how to handle a bad definition.
    is_dataset_ready = true;
    // // If the dataset definition is not OK, report it and await for a new one
    // match dataset_client.report_status(Err(e)).await {
    //     Ok(()) => {
    //         log::info!("{dataset_log_identifier} Dataset status reported as error");
    //     }
    //     Err(e) => {
    //         log::error!("{dataset_log_identifier} Failed to report dataset status: {e}");
    //     }
    // }
    // is_dataset_ready = false;

    // This variable keeps track of the latest reported schema.
    let mut current_schema = None;

    // NOTE: This could be read from the dataset_configuration instead if it's desired to be configurable
    let mut timer = tokio::time::interval(DEFAULT_SAMPLING_INTERVAL);

    // If the timer misses a tick, the next one will be immediate and the following one will be one sampling interval (in time) after that.
    timer.set_missed_tick_behavior(tokio::time::MissedTickBehavior::Delay);
    loop {
        tokio::select! {
            // Monitor for device endpoint readiness changes
            _ = device_endpoint_ready_watcher_rx.changed() => {
                // Update our local device endpoint readiness state
                is_device_endpoint_ready = *device_endpoint_ready_watcher_rx.borrow_and_update();

                log::info!("{dataset_log_identifier} Device endpoint ready state changed to {is_device_endpoint_ready}");
            },
            // Monitor for device endpoint readiness changes
            _ = asset_ready_watcher_rx.changed() => {
                // Update our local asset readiness state
                is_asset_ready = *asset_ready_watcher_rx.borrow_and_update();

                log::info!("{dataset_log_identifier} Asset ready state changed to {is_asset_ready}");
            },
            dataset_notification = dataset_client.recv_notification() => {
                // Match the dataset notification to handle updates, deletions, or invalid updates
                match dataset_notification {
                    DatasetNotification::Updated => {
                        log::info!("{dataset_log_identifier} Dataset update notification received");

                        // IMPLEMENT: Verify the dataset specification is OK and send an error report if needed
                        // If the dataset specification is not OK, we will set the boolean to false
                        is_dataset_ready = true;

                        // Update the local dataset definition
                        _local_dataset_definition = dataset_client.dataset_definition().clone();

                        // Reset the dataset reported flag
                        is_dataset_reported = false;
                    },
                    DatasetNotification::Deleted => {
                        // The dataset client has been deleted, we need to end the dataset handler
                        log::info!("{dataset_log_identifier} Dataset deleted notification received, ending dataset handler");
                        break;
                    },
                    DatasetNotification::UpdatedInvalid => {
                        // The dataset update is invalid, we need to wait for a valid update
                        log::info!("{dataset_log_identifier} Dataset invalid update notification received, waiting for a valid update");
                        is_dataset_ready = false;
                        // Continue to wait for a valid update
                    },
                }
            },
            _ = timer.tick(), if is_dataset_ready && is_asset_ready && is_device_endpoint_ready => {
                log::debug!("{dataset_log_identifier} Sampling!");

                // IMPLEMENT: This should be replaced with the actual sampling logic.
                let bytes = mock_sample();

                // IMPLEMENT: If there are any configuration related errors while sampling those should be
                // reported to ADR on the appropriate level (e.g., device endpoint, asset, dataset).

                // Create a data structure with the sampled data
                let data = Data {
                    payload: bytes,
                    content_type: "application/json".to_string(),
                    custom_user_data: vec![],
                    timestamp: Some(HybridLogicalClock::new()),
                };

                // Infer the message schema using the derived_json module. This works for JSON data only.
                let Ok(message_schema) = derived_json::create_schema(&data) else {
                    log::error!("{dataset_log_identifier} Failed to create message schema");

                    // If we fail to create the message schema, we will not be able to report it or forward data
                    // NOTE: Failing to create the message schema could be due to malformed data, so waiting for
                    // a dataset definition update on this failure is not desirable.
                    continue;
                };

                // If we've already reported the dataset status, we will not report it again until an update occurs.
                if !is_dataset_reported {
                    log::info!("{dataset_log_identifier} Reporting dataset status as OK");
                    match dataset_client.report_status(Ok(())).await {
                        Ok(()) => {
                            log::debug!("{dataset_log_identifier} Dataset status reported as OK");
                            is_dataset_reported = true;
                        }
                        Err(e) => {
                            log::error!("{dataset_log_identifier} Failed to report dataset status as OK, attempting in next sampling interval: {e}");
                        }
                    }
                }

                // If the current schema is None or different from the message schema, we will report the message schema.
                if current_schema.is_none() || current_schema.as_ref() != Some(&message_schema) {
                    // Note, this operation already retries internally.
                    log::info!("{dataset_log_identifier} Reporting message schema");
                    match dataset_client.report_message_schema(message_schema.clone()).await {
                        Ok(_) => {
                            log::debug!("{dataset_log_identifier} Successfully reported message schema");
                            current_schema = Some(message_schema);
                        }
                        Err(e) => {
                            log::error!("{dataset_log_identifier} Failed to report message schema, attempting in next interval: {e}");
                            // If we fail to report the message schema, we will not be able to forward the data
                            continue;
                        }
                    }
                }

                // Forward the data using the dataset client
                log::info!("{dataset_log_identifier} Forwarding data");

                // IMPLEMENT: This should handle errors forwarding the data.
                let _ = dataset_client.forward_data(data).await;
            }
        }
    }
}

fn mock_sample() -> Vec<u8> {
    // This function is a mock for sampling data, it should be replaced with the actual sampling logic.
    // For now, it returns a simple JSON object as a byte vector.
    serde_json::to_vec(&serde_json::json!({
        "temperature": 22.5,
        "humidity": 45.0,
    }))
    .unwrap()
}

/// Helper function to create a closure that sends an update if the desired state is different from the current state.
fn send_if_modified_fn(desired_state: bool) -> impl FnOnce(&mut bool) -> bool {
    move |curr| {
        // If the desired state is the same as the current state, don't send an update
        if *curr == desired_state {
            false
        } else {
            // Otherwise, update the current state to the desired state and return true to indicate that an update should be sent
            *curr = desired_state;
            true
        }
    }
}
