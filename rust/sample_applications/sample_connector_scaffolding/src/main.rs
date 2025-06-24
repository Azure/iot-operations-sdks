// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

//! # Connector Scaffolding Template
//!
//! This crate provides a scaffolding template for building edge applications targeting Azure IoT Operations.
//! It demonstrates how to structure device, asset, and dataset handlers, focusing on the lifecycle of creation,
//! updating, deletion and status reporting. The sample logic assumes periodic sampling of an endpoint at a fixed interval.
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
    AdrConfigError, Data,
    base_connector::{
        BaseConnector,
        managed_azure_device_registry::{
            AssetClient, ClientNotification, DatasetClient, DatasetNotification,
            DeviceEndpointClient, DeviceEndpointClientCreationObservation,
        },
    },
    data_processor::derived_json,
};
use azure_iot_operations_protocol::{
    application::ApplicationContextBuilder, common::hybrid_logical_clock::HybridLogicalClock,
};
use tokio::{
    select,
    sync::{mpsc, watch},
};
use tokio_util::sync::CancellationToken;

const DEFAULT_SAMPLING_INTERVAL: Duration = Duration::from_millis(10000); // Default sampling interval in milliseconds

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

    // Create the appplication context used by the AIO SDK
    let application_context = ApplicationContextBuilder::default().build()?;

    // Create the Base Connector to handle device endpoints, assets, and datasets creation, update and deletion notifications plus status reporting.
    let base_connector = BaseConnector::new(application_context);

    // Get the connector artifacts from the base connector, TODO: Use them as needed
    let _connector_artifacts = base_connector.connector_artifacts();

    // Create a device endpoint client creation observation
    let device_endpoint_client_creation_observation =
        base_connector.create_device_endpoint_client_create_observation();

    // Create a cancellation token for the connector
    let cancellation_token = CancellationToken::new();
    let cancellation_token_child = cancellation_token.child_token();

    // When the task ends the cancellation token will be cancelled
    let _cancellation_token_drop_guard = cancellation_token.drop_guard();

    // Run the session and the base connector concurrently
    let r = tokio::join!(
        async move {
            select! {
                biased;
                () = cancellation_token_child.cancelled() => {
                    log::info!("Connector is no longer available, ending device endpoint receiver");
                },
                () = receive_device_endpoints(device_endpoint_client_creation_observation, cancellation_token_child.clone()) => {
                    // Logs handled in the receive_device_endpoints function
                }
            }
        },
        base_connector.run()
    );
    // TODO: A retry mechanism should be implemented here.
    r.1?;
    Ok(())
}

/// Receives a device endpoint and creates a device handler for it.
///
/// # Arguments
/// * `device_endpoint_client_creation_observation` - The device endpoint client creation observation.
/// * `cancellation_token` - A cancellation token which triggers when the connector is cancelled.
async fn receive_device_endpoints(
    mut device_endpoint_client_creation_observation: DeviceEndpointClientCreationObservation,
    cancellation_token: CancellationToken,
) {
    // This cancellation token is used to cancel the task when the connector is cancelled.
    let _cancellation_token_drop_guard = cancellation_token.clone().drop_guard();

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

        // TODO: Reject endpoint types that are not this connector's type.

        let cancellation_token_child = cancellation_token.child_token();

        tokio::task::spawn(async move {
            tokio::select! {
                biased;
                () = cancellation_token_child.cancelled() => {
                    log::info!("{device_endpoint_log_identifier} Connector is no longer available, ending device handler");
                },
                () = device_handler(
                        device_endpoint_log_identifier.clone(),
                        device_endpoint_client,
                        cancellation_token_child.clone()
                    ) => {
                    // Logs handled in the device_handler function
                }
            }
        });
    }
}

/// Handles the device endpoint and receives the asset creation notifications with which it will
/// create asset handlers.
///
/// # Arguments
///  * `device_endpoint_log_identifier` - A string identifier for the device endpoint, used for logging.
/// * `device_endpoint_client` - The device endpoint client.
/// * `cancellation_token` - A cancellation token which triggers when the connector is cancelled.
async fn device_handler(
    device_endpoint_log_identifier: String,
    mut device_endpoint_client: DeviceEndpointClient,
    cancellation_token: CancellationToken,
) {
    // These channels are used to report the status of the device endpoint. When there is an update
    // to the device endpoint, we will drop these channels and create new ones. This will prevent
    // the device endpoint handler from receiving stale updates.
    let (mut device_endpoint_status_reporter_tx, mut device_endpoint_status_reporter_rx) =
        mpsc::unbounded_channel::<Result<(), AdrConfigError>>();
    // This watcher is used to notify the dataset handler of the current status reporter so that it can
    // report the status of the device endpoint.
    let (device_endpoint_status_reporter_watcher_tx, device_endpoint_status_reporter_watcher_rx) =
        watch::channel(device_endpoint_status_reporter_tx.clone());
    let mut device_endpoint_status_reported = false;

    loop {
        select! {
            biased;
            // This must be the first select branch because we want to prioritize reporting
            Some(status_report) = device_endpoint_status_reporter_rx.recv(), if !device_endpoint_status_reported => {
                match status_report {
                    Ok(()) => {
                        log::info!(
                            "{device_endpoint_log_identifier} Reporting endpoint and device status as OK"
                        );
                        // TODO: Reporting Ok here for both device and endpoint status may not be the desired behavior.
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
                    Err(adr_config_error) => {
                        log::error!(
                            "{device_endpoint_log_identifier} Reporting endpoint status error, awaiting device endpoint update: {:?}", &adr_config_error.message
                        );

                        // The next time we obtain a report, it will be the next version so our current
                        // senders and receivers need to be invalidated and updated.
                        (
                            #[allow(unused_assignments)]
                            device_endpoint_status_reporter_tx,
                            device_endpoint_status_reporter_rx,
                        ) = mpsc::unbounded_channel();
                        // Report the current version of the device endpoint
                        match device_endpoint_client
                            .report_endpoint_status(Err(adr_config_error))
                            .await
                        {
                            Ok(()) => {
                                log::info!(
                                    "{device_endpoint_log_identifier} Endpoint status reported as error"
                                );
                            }
                            Err(e) => {
                                log::error!(
                                    "{device_endpoint_log_identifier} Failed to report endpoint status: {e}"
                                );
                            }
                        }
                    }
                }
                // Since the status is now reported, we will not run through this branch again until we need to report
                device_endpoint_status_reported = true;
            }
            notification = device_endpoint_client.recv_notification() => match notification {
                ClientNotification::Updated => {
                    log::info!("{device_endpoint_log_identifier} Device endpoint update notification received");
                    // The current version of the device endpoint has been updated, we need to invalidate the current
                    // senders and receivers and update the watcher
                    (
                        device_endpoint_status_reporter_tx,
                        device_endpoint_status_reporter_rx,
                    ) = mpsc::unbounded_channel();
                    device_endpoint_status_reported = false; // Reset the status reported flag

                    // TODO: Add custom device endpoint update logic here

                    // Indicates a device endpoint update
                    let _ = device_endpoint_status_reporter_watcher_tx
                        .send(device_endpoint_status_reporter_tx.clone());
                }
                ClientNotification::Created(asset_client) => {
                    let asset_log_identifier = {
                        let asset_ref = asset_client.asset_ref();
                        format!(
                            "{device_endpoint_log_identifier}[A: {}]",
                            asset_ref.name
                        )
                    };
                    log::info!("{asset_log_identifier} Asset created");

                    let cancellation_token_child = cancellation_token.child_token();
                    let device_endpoint_status_reporter_watcher_rx_clone =
                        device_endpoint_status_reporter_watcher_rx.clone();

                    // Handle asset creation
                    tokio::task::spawn(async move {
                        tokio::select! {
                            biased;
                            () = cancellation_token_child.cancelled() => {
                                log::info!("{asset_log_identifier} Connector is no longer available, ending asset handler");
                            },
                            () = asset_handler(
                                    asset_log_identifier.clone(),
                                    asset_client,
                                    device_endpoint_status_reporter_watcher_rx_clone,
                                    cancellation_token_child.clone(),
                            ) => {
                                // Logs handled in the asset_handler function
                            }
                        }
                    });
                }
                ClientNotification::Deleted => {
                    log::info!(
                        "{device_endpoint_log_identifier} Device endpoint deleted notification received, ending device handler"
                    );
                    break;
                }
            }
        }
    }
}

/// Handles the asset and spawns dataset handlers for each dataset.
///
/// # Arguments
/// * `asset_log_identifier` - A string identifier for the asset, used for logging.
/// * `asset_client` - The asset client.
/// * `device_endpoint_status_reporter_watcher_rx` - A watcher for the device endpoint status reporter sender.
/// * `cancellation_token` - A cancellation token which triggers when the connector is cancelled or the device endpoint is deleted.
async fn asset_handler(
    asset_log_identifier: String,
    mut asset_client: AssetClient,
    device_endpoint_status_reporter_watcher_rx: watch::Receiver<
        mpsc::UnboundedSender<Result<(), AdrConfigError>>,
    >,
    cancellation_token: CancellationToken,
) {
    // These channels are used to report the status of the asset. When there is an update
    // to the asset, we will drop these channels and create new ones. This will prevent
    // the asset handler from receiving stale updates.
    let (mut asset_status_reporter_tx, mut asset_status_reporter_rx) =
        mpsc::unbounded_channel::<Result<(), AdrConfigError>>();
    // This watcher is used to notify the dataset handler of the current status reporter so that it can
    // report the status of the asset.
    let (asset_status_reporter_watcher_tx, asset_status_reporter_watcher_rx) =
        watch::channel(asset_status_reporter_tx.clone());
    let mut asset_status_reported = false;
    loop {
        select! {
            biased;
            // This must be the first select branch because we want to prioritize reporting
            Some(status_report) = asset_status_reporter_rx.recv(), if !asset_status_reported => {
                match status_report {
                    Ok(()) => {
                        log::info!("{asset_log_identifier} Reporting asset status as OK");
                        match asset_client.report_status(Ok(())).await {
                            Ok(()) => {
                                log::debug!("{asset_log_identifier} Asset status reported as OK");
                            }
                            Err(e) => {
                                log::error!("{asset_log_identifier} Failed to report asset status: {e}");
                            }
                        }
                    }
                    Err(adr_config_error) => {
                        log::error!("{asset_log_identifier} Reporting asset status error, awaiting asset update: {:?}", &adr_config_error.message);
                        // The next time we obtain a report, it will be the next version so our
                        // current senders and receivers need to be invalidated.
                        // We don't use or send the asset_status_reporter_tx here, but we need to create it to prevent datasets from sending stale updates.
                        (
                            #[allow(unused_assignments)]
                            asset_status_reporter_tx,
                            asset_status_reporter_rx
                        ) =
                            mpsc::unbounded_channel::<Result<(), AdrConfigError>>();
                        // Report the current version of the asset
                        match asset_client.report_status(Err(adr_config_error)).await {
                            Ok(()) => {
                                log::info!("{asset_log_identifier} Asset status reported as error");
                            }
                            Err(e) => {
                                log::error!("{asset_log_identifier} Failed to report asset status: {e}");
                            }
                        }
                    }
                }
                // Since the status is now reported, we will not run through this branch again
                asset_status_reported = true;
            }
            notification = asset_client.recv_notification() => match notification {
                ClientNotification::Updated => {
                    log::info!("{asset_log_identifier} Asset update notification received");

                    // TODO: Add custom asset update logic here

                    // The current version of the asset has been updated, we need to invalidate the
                    // current senders and receivers and update the watcher.
                    (asset_status_reporter_tx, asset_status_reporter_rx) = mpsc::unbounded_channel();
                    asset_status_reported = false;
                    // Indicates an asset update
                    let _ = asset_status_reporter_watcher_tx.send(asset_status_reporter_tx.clone());
                }
                ClientNotification::Created(dataset_client) => {
                    let dataset_log_identifier = {
                        let dataset_ref = dataset_client.dataset_ref();
                        format!(
                            "{asset_log_identifier}[DS: {}]",
                            dataset_ref.dataset_name
                        )
                    };
                    log::info!("{dataset_log_identifier} Dataset created");

                    let cancellation_token_child = cancellation_token.child_token();
                    let device_endpoint_status_watcher_rx_clone =
                        device_endpoint_status_reporter_watcher_rx.clone();
                    let asset_status_watcher_rx_clone = asset_status_reporter_watcher_rx.clone();

                    // Handle the new dataset
                    tokio::task::spawn(async move {
                        tokio::select! {
                            biased;
                            () = cancellation_token_child.cancelled() => {
                                log::info!("{dataset_log_identifier} Connector is no longer available, ending dataset handler");
                            },
                            () = handle_dataset(
                                    dataset_log_identifier.clone(),
                                    dataset_client,
                                    device_endpoint_status_watcher_rx_clone,
                                    asset_status_watcher_rx_clone,
                                ) => {
                                // Logs handled in the handle_dataset function
                            }
                        }
                    });
                }
                ClientNotification::Deleted => {
                    log::info!(
                        "{asset_log_identifier} Asset deleted notification received, ending asset handler"
                    );
                    break;
                }
            }
        }
    }
}

/// Handles sampling of data from the dataset.
///
/// # Arguments
/// * `dataset_log_identifier` - A string identifier for the dataset, used for logging.
/// * `dataset_client` - The dataset client.
/// * `device_endpoint_status_reporter_watcher_rx` - A watcher for the device endpoint status reporter sender.
/// * `asset_status_reporter_watcher_rx` - A watcher for the asset status reporter sender.
#[allow(unused_assignments)] // TODO: Remove once variables are being used
#[allow(clippy::never_loop)] // TODO: Remove once loop is being used
async fn handle_dataset(
    dataset_log_identifier: String,
    mut dataset_client: DatasetClient,
    mut device_endpoint_status_reporter_watcher_rx: watch::Receiver<
        mpsc::UnboundedSender<Result<(), AdrConfigError>>,
    >,
    mut asset_status_reporter_watcher_rx: watch::Receiver<
        mpsc::UnboundedSender<Result<(), AdrConfigError>>,
    >,
) {
    // This boolean tracks if the endpoint is ready to be sampled.
    let mut is_device_endpoint_ready = true;

    // Extract the status reporter from the device endpoint watcher
    let mut device_endpoint_status_reporter_tx = device_endpoint_status_reporter_watcher_rx
        .borrow_and_update()
        .clone();
    // Extract a copy of the dataset client
    #[allow(clippy::never_loop)]
    let mut _local_device_endpoint_specification = loop {
        let local_device_endpoint_specification = dataset_client.device_specification();

        // TODO: Verify the device endpoint specification is OK

        // For this example, we will assume that the device endpoint specification is OK, see below for how to handle a bad update.
        break local_device_endpoint_specification;

        // // If the device specification is not OK await for a new one
        // let _ = device_endpoint_status_reporter_watcher_rx.changed().await;
        // // Update the status reporter for the new device endpoint specification
        // device_endpoint_status_reporter_tx = device_endpoint_status_reporter_watcher_rx
        //     .borrow_and_update()
        //     .clone();
    };

    // Extract the asset status reporter from the asset watcher
    let mut _asset_status_reporter_tx =
        asset_status_reporter_watcher_rx.borrow_and_update().clone();

    #[allow(clippy::never_loop)]
    let mut _local_asset_specification = loop {
        let local_asset_specification = dataset_client.asset_specification();

        // TODO: Verify the asset specification is OK

        // For this example, we will assume that the asset specification is OK, see below for how to handle a bad update.
        break local_asset_specification;

        // // If the asset specification is not OK await for a new one
        // let _ = asset_status_reporter_watcher_rx.changed().await;
        // // Update the status reporter for the new asset specification
        // asset_status_reporter_tx = asset_status_reporter_watcher_rx.borrow_and_update().clone();
    };

    // This boolean tracks if the dataset and asset are ready to be sampled.
    let mut is_dataset_asset_ready = true;
    // This boolean tracks if the status for the dataset has been reported.
    let mut is_dataset_reported = false;
    // Extract the dataset definition from the dataset client
    #[allow(clippy::never_loop)]
    let mut _local_dataset_definition = loop {
        let local_dataset_definition = dataset_client.dataset_definition().clone();

        // TODO: Verify the dataset definition is OK

        // For this example, we will assume that the dataset definition is OK, see below for how to handle a bad update.
        break local_dataset_definition;

        // // If the dataset definition is not OK await for a new one
        // match dataset_client.recv_notification().await {
        //     DatasetNotification::Updated => {
        //         log::info!("{dataset_log_identifier} Dataset definition updated");
        //     }
        //     DatasetNotification::Deleted => {
        //         log::info!("{dataset_log_identifier} Dataset deleted, ending dataset handler");
        //         return;
        //     }
        //     DatasetNotification::UpdatedInvalid => {
        //         log::info!("{dataset_log_identifier} Dataset definition invalid, waiting for a valid update");
        //         loop {
        //             // Wait for a valid update
        //             match dataset_client.recv_notification().await {
        //                 DatasetNotification::Updated => {
        //                     log::info!("{dataset_log_identifier} Dataset definition updated");
        //                     break;
        //                 }
        //                 DatasetNotification::Deleted => {
        //                     log::info!("{dataset_log_identifier} Dataset deleted, ending dataset handler");
        //                     return;
        //                 }
        //                 DatasetNotification::UpdatedInvalid => {
        //                     log::info!("{dataset_log_identifier} Dataset definition invalid, waiting for a valid update");
        //                 }
        //             }
        //         }
        //     }
        // }
    };

    // This variable keeps track of the latest reported schema.
    let mut current_schema = None;

    let mut timer = tokio::time::interval(DEFAULT_SAMPLING_INTERVAL);

    // If the timer misses a tick, the next one will be immediate and the following one will be one sampling interval (in time) after that.
    timer.set_missed_tick_behavior(tokio::time::MissedTickBehavior::Delay);
    loop {
        select! {
            // Monitor for device endpoint updates
            _ = device_endpoint_status_reporter_watcher_rx.changed() => {
                log::info!("{dataset_log_identifier} Device endpoint update notification received");
                let new_device_endpoint_status_reporter_tx =
                    device_endpoint_status_reporter_watcher_rx.borrow_and_update();

                // TODO: Verify the device endpoint specification is OK and optionally send a report if needed

                // If any issues, set the corresponding boolean to false
                is_device_endpoint_ready = true;

                // Update the local device endpoint specification
                _local_device_endpoint_specification = dataset_client.device_specification();

                // Update the status reporter for the updated device endpoint
                device_endpoint_status_reporter_tx =
                    new_device_endpoint_status_reporter_tx.clone();
            },
            // Monitor for asset updates
            _ = asset_status_reporter_watcher_rx.changed() => {
                log::info!("{dataset_log_identifier} Asset update notification received");
                let new_asset_status_reporter_tx =
                    asset_status_reporter_watcher_rx.borrow_and_update();

                // TODO: Verify the asset specification is OK and optionally send a report if needed

                // If any issues, set the corresponding boolean to false
                is_dataset_asset_ready = true;

                // Update the local asset specification
                _local_asset_specification = dataset_client.asset_specification();

                // Update the status reporter for the updated asset
                _asset_status_reporter_tx = new_asset_status_reporter_tx.clone();
            },
            dataset_notification = dataset_client.recv_notification() => {
                // Match the dataset notification to handle updates, deletions, or invalid updates
                match dataset_notification {
                    DatasetNotification::Updated => {
                        log::info!("{dataset_log_identifier} Dataset update notification received");

                        // TODO: Verify the dataset specification is OK and optionally send a report if needed
                        // If the dataset specification is not OK, we will set the boolean to false
                        is_dataset_asset_ready = true;

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
                        is_dataset_asset_ready = false;
                        // Continue to wait for a valid update
                    },
                }
            },
            _ = timer.tick(), if is_dataset_asset_ready && is_device_endpoint_ready => {
                log::debug!("{dataset_log_identifier} Sampling!");

                // TODO: This should be replaced with the actual sampling logic.
                let bytes = mock_sample();

                // TODO: If there are any errors while sampling those should be returned and statuses reported to appropriate channels (e.g., device endpoint, asset, dataset).

                // Report Ok for the device endpoint status reporter if the sampling was successful (will differ depending on the sampling logic)
                let _ = device_endpoint_status_reporter_tx.send(Ok(()));

                // Create a data structure with the sampled data
                let data = Data {
                    payload: bytes,
                    content_type: "application/json".to_string(),
                    custom_user_data: vec![],
                    timestamp: Some(HybridLogicalClock::new()),
                };

                // Transform the data and infer the message schema using the derived_json module. This works for JSON data only.
                let Ok(message_schema) = derived_json::create_schema(&data) else {
                    log::error!("{dataset_log_identifier} Failed to transform data");

                    // Report a status error of the dataset.
                    match dataset_client.report_status(Err(AdrConfigError {
                        message: Some("Failed to transform data".to_string()),
                        ..Default::default()
                    })).await {
                        Ok(()) => log::debug!("{dataset_log_identifier} Dataset status reported as error"),
                        Err(e) => log::error!("{dataset_log_identifier} Failed to report dataset status: {e}"),
                    }
                    // If we fail to report the message schema, we will not be able to forward it
                    is_dataset_asset_ready = false;
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

                // Forward the data to the dataset client
                log::info!("{dataset_log_identifier} Forwarding data");

                // TODO: This should handle errors forwarding the data.
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
