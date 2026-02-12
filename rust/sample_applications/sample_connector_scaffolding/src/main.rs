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
//! - Each handler (device, asset, dataset) is responsible for reporting its **configuration status** back to Azure IoT Operations.
//! - Configuration status indicates whether the entity's configuration is valid (Ok) or has errors (Err with details).
//! - Use `report_device_status_if_modified`, `report_endpoint_status_if_modified`, and `report_status_if_modified` (for an asset and its components).
//!
//! ### Health Event Reporting
//! - Device and asset component (datasets, events, etc.) handlers report **runtime health events** indicating operational availability.
//! - Use `report_health_event(RuntimeHealthEvent::Available)` when operations succeed.
//! - Use `report_health_event(RuntimeHealthEvent::Unavailable { message, reason_code })` when operations fail.
//! - Use `pause_and_refresh_health_version()` when configuration updates occur to avoid reporting stale health.
//!
//! ### When to Use Configuration Status vs Health Events
//! A good rule for deciding whether to report something as configuration status vs health status:
//! - **Configuration status**: Report errors here if they can ONLY be fixed by a definition update.
//!   Examples: invalid URL format, missing required fields, unsupported protocol.
//! - **Health events**: Report errors here if there's any possibility the operation could succeed
//!   in the future without a definition update. Examples: network timeouts, connection refused,
//!   external service unavailable, transient failures.
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
        self, BaseConnector,
        managed_azure_device_registry::{
            AssetClient, ClientNotification, DataOperationClient, DataOperationNotification,
            DeviceEndpointClient, DeviceEndpointClientCreationObservation, ModifyResult,
            RuntimeHealthEvent, SchemaModifyResult,
        },
    },
    data_processor::derived_json,
    deployment_artifacts::connector::ConnectorArtifacts,
};
use azure_iot_operations_protocol::{
    application::ApplicationContextBuilder, common::hybrid_logical_clock::HybridLogicalClock,
};
use tokio::sync::watch;

const DEFAULT_SAMPLING_INTERVAL: Duration = Duration::from_millis(10000); // Default sampling interval in milliseconds

/// Macro that generates closures for reporting status with one-way transitions.
///
/// A one-way transition means that we can only go from None to Ok, None to Err, and from Ok to Err.
///
/// Reports Ok only if status is None
/// Reports Err if status is None or Ok (errors are sticky once set)
macro_rules! report_status_one_way {
    ($new_status:expr) => {
        |status| {
            let should_report = match (&status, &$new_status) {
                // Report Ok only if current status is None
                (None, Ok(())) => true,
                // Report Err if current status is None or Ok
                (None, Err(_)) => true,
                (Some(Ok(())), Err(_)) => true,
                // Don't report anything else
                _ => false,
            };

            if should_report {
                Some($new_status)
            } else {
                None
            }
        }
    };
}

/// Macro that generates closures for reporting status any time it's different than the current status
///
/// This means that unless the statuses match exactly, the new status will be reported
///
/// Reports Ok in any case where status is not Ok
/// Reports Err in any case where the status is not the same exact Err
macro_rules! report_status_if_different {
    ($new_status:expr) => {
        |status| {
            let should_report = match (&status, &$new_status) {
                // Report Ok(()) in all cases unless it's already Ok
                (Some(Ok(())), Ok(())) => false,
                // If the current status is Err, only don't report if they match
                (Some(Err(curr_err)), Err(new_err)) => *curr_err != new_err,
                // Report anything else
                _ => true,
            };
            if should_report {
                Some($new_status)
            } else {
                None
            }
        }
    };
}

#[tokio::main]
async fn main() -> Result<(), Box<dyn std::error::Error>> {
    env_logger::Builder::new()
        .filter_level(log::LevelFilter::Warn)
        .format_timestamp(None)
        .filter_module("azure_iot_operations_connector", log::LevelFilter::Info)
        .filter_module("sample_connector_scaffolding", log::LevelFilter::Info)
        .init();

    // Create the connector artifacts from the deployment, IMPLEMENT: Use them as needed
    let connector_artifacts = ConnectorArtifacts::new_from_deployment()?;

    log::info!("Starting connector");

    // Create the application context used by the AIO SDK
    let application_context = ApplicationContextBuilder::default().build()?;

    // Create options for the base connector, IMPLEMENT: Customize as needed
    let base_connector_options = base_connector::OptionsBuilder::default().build()?;

    // Create the Base Connector to handle device endpoints, assets, and datasets creation, update and deletion notifications plus status reporting.
    let base_connector = BaseConnector::new(
        application_context,
        connector_artifacts,
        base_connector_options,
    )?;

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
    // Get the status reporter for the device endpoint
    let mut device_endpoint_status_reporter = device_endpoint_client.get_status_reporter();

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
        // IMPLEMENT: Connection logic may be handled at this level if this Connector Application
        // maintains a persistent connection to the device endpoint. If knowledge of a successful connection
        // can't be determined at this level, communication will need to be added from the location of
        // the connection logic to this level to report the device and endpoint statuses and health events.

        // Notify any lower components that the device endpoint is in a ready state
        device_endpoint_ready_watcher_tx.send_if_modified(send_if_modified_fn(true));
        // If there was an error, notify any lower components that the device endpoint is not in a ready state while we wait for an update
        // device_endpoint_ready_watcher_tx.send_if_modified(send_if_modified_fn(false));

        // Report runtime health events based on connection status.
        // Runtime health events indicate the operational health of the connector at runtime,
        // separate from configuration status.
        //
        // Report Available when the connection to the device endpoint succeeds:
        device_endpoint_status_reporter.report_health_event(RuntimeHealthEvent::Available);
        //
        // IMPLEMENT: Report Unavailable with details when the connection fails:
        // device_endpoint_status_reporter.report_health_event(RuntimeHealthEvent::Unavailable {
        //     message: Some("Failed to connect: <error details>".to_string()),
        //     reason_code: Some("SampleConnectionFailure".to_string()),
        // });
    }

    // If the connection is successful or the device wasn't enabled and there weren't configuration errors, report the device and endpoint statuses.
    // Modify this to report any configuration errors if there are any
    // Report device status
    match device_endpoint_status_reporter
        .report_device_status_if_modified(report_status_one_way!(Ok::<(), AdrConfigError>(())))
        .await
    {
        Ok(ModifyResult::Reported) => {
            log::info!("{device_endpoint_log_identifier} Device status reported as OK");
        }
        Ok(ModifyResult::NotModified) => {} // No change, do nothing
        Err(e) => {
            log::error!("{device_endpoint_log_identifier} Failed to report Device status: {e}");
        }
    }

    // Report endpoint status
    match device_endpoint_status_reporter
        .report_endpoint_status_if_modified(report_status_one_way!(Ok::<(), AdrConfigError>(())))
        .await
    {
        Ok(ModifyResult::Reported) => {
            log::info!("{device_endpoint_log_identifier} Endpoint status reported as OK");
        }
        Ok(ModifyResult::NotModified) => {} // No change, do nothing
        Err(e) => {
            log::error!("{device_endpoint_log_identifier} Failed to report Endpoint status: {e}");
        }
    }

    // Listen for DeviceEndpointClient updates/deletion and new AssetClients
    loop {
        match device_endpoint_client.recv_notification().await {
            ClientNotification::Updated => {
                // Pause health reporting until we re-validate and re-establish connection.
                // The "refresh" snapshots the new specification version for future health events.
                device_endpoint_status_reporter.pause_and_refresh_health_version();
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

                    // After re-validating the updated configuration and re-establishing connection,
                    // report runtime health events as shown in the initial device_handler setup above.
                    device_endpoint_status_reporter
                        .report_health_event(RuntimeHealthEvent::Available);
                }

                // For this example, we will assume that the device endpoint specification is OK. Modify this to report any errors
                // Report device status
                match device_endpoint_status_reporter
                    .report_device_status_if_modified(report_status_one_way!(Ok::<
                        (),
                        AdrConfigError,
                    >(
                        ()
                    )))
                    .await
                {
                    Ok(ModifyResult::Reported) => {
                        log::info!("{device_endpoint_log_identifier} Device status reported as OK");
                    }
                    Ok(ModifyResult::NotModified) => {} // No change, do nothing
                    Err(e) => {
                        log::error!(
                            "{device_endpoint_log_identifier} Failed to report Device status: {e}"
                        );
                    }
                }

                // Report endpoint status
                match device_endpoint_status_reporter
                    .report_endpoint_status_if_modified(report_status_one_way!(Ok::<
                        (),
                        AdrConfigError,
                    >(
                        ()
                    )))
                    .await
                {
                    Ok(ModifyResult::Reported) => {
                        log::info!(
                            "{device_endpoint_log_identifier} Endpoint status reported as OK"
                        );
                    }
                    Ok(ModifyResult::NotModified) => {} // No change, do nothing
                    Err(e) => {
                        log::error!(
                            "{device_endpoint_log_identifier} Failed to report Endpoint status: {e}"
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
    // Get the status reporter for the asset
    let asset_status_reporter = asset_client.get_status_reporter();

    // IMPLEMENT: add any Asset validation here and report errors or Ok status
    // If the asset specification has a default_dataset_configuration, then the asset status
    // may not be able to be reported until the dataset level can validate this field.

    // Here is one thing that should be validated for most connectors, although it won't be a config error if it's not enabled
    let mut is_asset_ready = if asset_client
        .specification()
        .enabled
        .is_some_and(|enabled| !enabled)
    {
        log::warn!("{asset_log_identifier} Asset is disabled, waiting for update");
        false
    } else {
        true
    };

    // For this example, we will assume that the asset specification is OK. Modify this to report any errors
    match asset_status_reporter
        .report_status_if_modified(report_status_one_way!(Ok::<(), AdrConfigError>(())))
        .await
    {
        Ok(ModifyResult::Reported) => {
            log::info!("{asset_log_identifier} Asset status reported as OK");
        }
        Ok(ModifyResult::NotModified) => {} // No change, do nothing
        Err(e) => {
            log::error!("{asset_log_identifier} Failed to report asset status: {e}");
        }
    }

    // This watcher is used to notify the data operation handlers whether the asset is healthy and sampling should happen or if there are any updates
    // Initialize it with the initial readiness state
    let asset_update_watcher_tx = watch::Sender::new(is_asset_ready);

    // Receive asset updates and dataset creation notifications
    loop {
        match asset_client.recv_notification().await {
            ClientNotification::Updated => {
                log::info!("{asset_log_identifier} Asset update notification received");

                // IMPLEMENT: Add custom asset update/validation logic here

                // Update asset ready state for data operations
                is_asset_ready = if asset_client
                    .specification()
                    .enabled
                    .is_some_and(|enabled| !enabled)
                {
                    log::warn!("{asset_log_identifier} Asset is disabled, waiting for update");
                    false
                } else {
                    true
                };

                // For this example, we will assume that the asset specification is OK. Modify this to report any errors
                match asset_status_reporter
                    .report_status_if_modified(report_status_one_way!(Ok::<(), AdrConfigError>(())))
                    .await
                {
                    Ok(ModifyResult::Reported) => {
                        log::info!("{asset_log_identifier} Asset status reported as OK");
                    }
                    Ok(ModifyResult::NotModified) => {} // No change, do nothing
                    Err(e) => {
                        log::error!("{asset_log_identifier} Failed to report asset status: {e}");
                    }
                }

                // Notify any data operations that the asset has been updated and its ready state
                asset_update_watcher_tx.send_modify(|asset_ready| *asset_ready = is_asset_ready);
            }
            ClientNotification::Created(data_operation_client) => {
                let data_operation_ref = data_operation_client.data_operation_ref();
                let data_operation_log_identifier = {
                    format!(
                        "{asset_log_identifier}[{}]",
                        data_operation_ref.data_operation_name
                    )
                };
                log::info!("{data_operation_log_identifier} Data Operation created");

                // Handle the new data operation
                // IMPLEMENT: For this scaffolding a dataset handler is provided, other
                // data operation handlers should be implemented as needed.
                match data_operation_client.kind() {
                    azure_iot_operations_connector::DataOperationKind::Dataset => {
                        // Handle the new dataset
                        tokio::task::spawn(handle_dataset(
                            data_operation_log_identifier,
                            data_operation_client,
                            asset_update_watcher_tx.subscribe(),
                            device_endpoint_ready_watcher_rx.clone(),
                        ));
                    }
                    azure_iot_operations_connector::DataOperationKind::Event
                    | azure_iot_operations_connector::DataOperationKind::Stream => {
                        // Handle the new stream / event
                        // For this scaffolding, they are not supported. A similar implementation
                        // could be added for handling these types of data operations.
                        tokio::task::spawn(handle_unsupported_data_operation(
                            data_operation_log_identifier,
                            data_operation_client,
                            asset_update_watcher_tx.subscribe(),
                        ));
                    }
                }
            }
            ClientNotification::Deleted => {
                log::info!(
                    "{asset_log_identifier} Asset deleted notification received, ending asset handler"
                );
                // The asset ready state does not need to be updated here because all data operations will also get deleted
                break;
            }
        }
    }
}

/// Handles sampling of data from the dataset.
///
/// # Arguments
/// * `dataset_log_identifier` - A string identifier for the dataset, used for logging.
/// * `data_operation_client` - The data operation client we use for operations related to the dataset.
/// * `asset_update_watcher_rx` - A watcher for asset updates and readiness state.
/// * `device_endpoint_ready_watcher_rx` - A watcher for the device endpoint readiness state.
async fn handle_dataset(
    dataset_log_identifier: String,
    mut data_operation_client: DataOperationClient,
    mut asset_update_watcher_rx: watch::Receiver<bool>,
    mut device_endpoint_ready_watcher_rx: watch::Receiver<bool>,
) {
    // Get the status reporter for the data operation
    let mut data_operation_status_reporter = data_operation_client.get_status_reporter();

    let mut is_asset_ready = *asset_update_watcher_rx.borrow_and_update();
    let mut is_device_endpoint_ready = *device_endpoint_ready_watcher_rx.borrow_and_update();
    // This boolean tracks if the dataset is ready to be sampled.
    let mut is_dataset_ready;
    // This variable keeps track of the latest reported schema.
    let mut last_reported_schema = None;
    // This variable keeps track of the latest reported schema reference.
    let mut last_reported_schema_reference = None;

    // Extract the dataset definition from the dataset client
    let mut _local_dataset_definition = data_operation_client.definition().clone();
    // IMPLEMENT: Verify the dataset definition is OK. For this example, we will assume that the dataset definition is Ok
    // This variable keeps track of the latest reported dataset status.
    let mut last_reported_dataset_status = Ok(());
    is_dataset_ready = last_reported_dataset_status.is_ok();

    // Report the dataset status based on validation.
    match data_operation_status_reporter
        .report_status_if_modified(report_status_one_way!(last_reported_dataset_status.clone()))
        .await
    {
        Ok(ModifyResult::Reported) => {
            log::info!("{dataset_log_identifier} Dataset status reported");
        }
        Ok(ModifyResult::NotModified) => {} // No change, do nothing
        Err(e) => {
            log::error!("{dataset_log_identifier} Failed to report Dataset status: {e}");
        }
    }

    // NOTE: This could be read from the dataset_configuration instead if it's desired to be configurable
    let mut timer = tokio::time::interval(DEFAULT_SAMPLING_INTERVAL);

    // If the timer misses a tick, the next one will be immediate and the following one will be one sampling interval (in time) after that.
    timer.set_missed_tick_behavior(tokio::time::MissedTickBehavior::Delay);
    loop {
        tokio::select! {
            // When sampling at high frequency, multiple samples may occur before updates are handled.
            // Using 'biased;' ensures updates are prioritized over sampling.
            biased;
            // Monitor for device endpoint readiness changes
            res = device_endpoint_ready_watcher_rx.changed() => {
                if res.is_err() {
                    // While this signals that the device endpoint has been deleted, the associated dataset will be deleted momentarily as well.
                    log::info!("{dataset_log_identifier} Device Endpoint deleted notification received, ending dataset handler");
                    break;
                }
                // Update our local device endpoint readiness state
                is_device_endpoint_ready = *device_endpoint_ready_watcher_rx.borrow_and_update();

                // NOTE: If the dataset's health depends on device endpoint configuration (e.g., connection
                // settings, authentication), you may want to pause data operation health reporting here
                // until the new configuration is validated:
                // data_operation_status_reporter.pause_health_reporting();

                log::debug!("{dataset_log_identifier} Device endpoint ready state changed to {is_device_endpoint_ready}");
            },
            data_operation_notification = data_operation_client.recv_notification() => {
                // Match the data operation notification to handle updates, deletions, or invalid updates
                match data_operation_notification {
                    DataOperationNotification::Updated => {
                        // Pause health reporting until we validate the new configuration and successfully
                        // complete a sampling cycle. This prevents reporting stale health status from
                        // the previous configuration. The "refresh" part snapshots the new specification
                        // version so future health events are tagged with the correct version.
                        data_operation_status_reporter.pause_and_refresh_health_version();
                        log::info!("{dataset_log_identifier} Dataset update notification received");

                        // Update the local dataset definition
                        _local_dataset_definition = data_operation_client.definition().clone();

                        // IMPLEMENT: Verify the dataset specification is OK and send an error report if needed
                        last_reported_dataset_status = Ok(());
                        is_dataset_ready = last_reported_dataset_status.is_ok();

                        // Report the dataset status based on validation.
                        match data_operation_status_reporter
                            .report_status_if_modified(report_status_if_different!(
                                last_reported_dataset_status.clone()
                            ))
                            .await
                        {
                            Ok(ModifyResult::Reported) => {
                                log::info!("{dataset_log_identifier} Dataset status reported");
                            }
                            Ok(ModifyResult::NotModified) => {} // No change, do nothing
                            Err(e) => {
                                log::error!(
                                    "{dataset_log_identifier} Failed to report Dataset status: {e}"
                                );
                            }
                        }
                    },
                    DataOperationNotification::Deleted => {
                        // The dataset client has been deleted, we need to end the dataset handler
                        log::info!("{dataset_log_identifier} Dataset deleted notification received, ending dataset handler");
                        break;
                    },
                    DataOperationNotification::UpdatedInvalid => {
                        // The dataset update is invalid, we need to wait for a valid update
                        log::info!("{dataset_log_identifier} Dataset invalid update notification received, waiting for a valid update");
                        is_dataset_ready = false;
                        // Continue to wait for a valid update
                    },
                }
            },
            // Monitor for asset updates and readiness changes
            res = asset_update_watcher_rx.changed() => {
                if res.is_err() {
                    // The asset client has been deleted, we need to end the data operation handler
                    log::info!("{dataset_log_identifier} Asset deleted notification received, ending dataset handler");
                    break;
                }
                // Pause and refresh health version because we have a new version of the asset
                // and want to report health for that new version after we validate the new configuration.
                data_operation_status_reporter.pause_and_refresh_health_version();

                // Update our local asset readiness state
                is_asset_ready = *asset_update_watcher_rx.borrow_and_update();
                log::debug!("{dataset_log_identifier} Asset update notification received. Current ready state is {is_asset_ready}");

                // Re-report dataset status as status has been cleared
                match data_operation_status_reporter
                    .report_status_if_modified(report_status_one_way!(
                        last_reported_dataset_status.clone()
                    ))
                    .await
                {
                    Ok(ModifyResult::Reported) => {
                        log::info!("{dataset_log_identifier} Dataset status reported");
                    }
                    Ok(ModifyResult::NotModified) => {} // No change, do nothing
                    Err(e) => {
                        log::error!(
                            "{dataset_log_identifier} Failed to report Dataset status: {e}"
                        );
                    }
                }
            },
            _ = timer.tick(), if is_dataset_ready && is_asset_ready && is_device_endpoint_ready => {
                log::debug!("{dataset_log_identifier} Sampling!");

                // IMPLEMENT: This should be replaced with the actual sampling logic.
                let bytes = match mock_sample() {
                    Ok(bytes) => bytes,
                    Err(e) => {
                        log::error!("{dataset_log_identifier} Sampling failed: {e}");
                        // Report Unavailable when sampling fails
                        data_operation_status_reporter.report_health_event(RuntimeHealthEvent::Unavailable {
                            message: Some(format!("Sampling failed: {e}")),
                            reason_code: Some("SampleConnectorSamplingFailure".to_string()),
                        });
                        continue;
                    }
                };

                // IMPLEMENT: If there are any configuration related errors found while sampling those should be
                // reported to ADR on the appropriate level (e.g., device endpoint, asset, dataset). Status reporters
                // for higher levels can be cloned and passed down to use on this level

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

                    // If we fail to create the message schema, we will not be able to report it or forward data.
                    // NOTE: Failing to create the message schema could be due to malformed data, so waiting for
                    // a dataset definition update on this failure is not desirable.
                    data_operation_status_reporter.report_health_event(RuntimeHealthEvent::Unavailable {
                        message: Some("Failed to create message schema. Response data may be malformed or in an unexpected format.".to_string()),
                        reason_code: Some("SampleConnectorSchemaGenerationFailure".to_string()),
                    });
                    continue;
                };

                // Report the message schema if needed
                match data_operation_client.report_message_schema_if_modified(|schema_ref| {
                    // Report unless we've already reported this exact schema with the same reference
                    if let (Some(schema_ref), Some(last_reported_ref), Some(last_reported_schema)) = (schema_ref, &last_reported_schema_reference, last_reported_schema.as_ref()) {
                        if schema_ref == last_reported_ref && message_schema == *last_reported_schema {
                            // Already reported this exact schema
                            None
                        } else {
                            Some(message_schema.clone())
                        }
                    } else {
                        Some(message_schema.clone()) // Always report if we don't have the complete state
                    }
                }).await {
                    Ok(SchemaModifyResult::Reported(new_schema_reference)) => {
                        log::info!("{dataset_log_identifier} Message schema reported");
                        last_reported_schema = Some(message_schema);
                        last_reported_schema_reference = Some(new_schema_reference);
                    }
                    Ok(SchemaModifyResult::NotModified) => {} // No change, do nothing
                    Err(e) => {
                        log::error!("{dataset_log_identifier} Failed to report message schema: {e}");
                        // If we fail to report the message schema, we will not be able to forward the data
                        continue;
                    }
                }

                // Forward the data using the data operation client
                log::info!("{dataset_log_identifier} Forwarding data");

                // IMPLEMENT: Handle errors forwarding the data.
                match data_operation_client.forward_data(data).await {
                    Ok(()) => {
                        // For this connector, report dataset healthy after successful data forwarding.
                        // This indicates the full sampling cycle completed successfully.
                        data_operation_status_reporter.report_health_event(RuntimeHealthEvent::Available);
                    }
                    Err(e) => {
                        log::error!("{dataset_log_identifier} Failed to forward data: {e}");
                    }
                }
            }
        }
    }
}

fn mock_sample() -> Result<Vec<u8>, String> {
    // IMPLEMENT: This function is a mock for sampling data, it should be replaced with the actual sampling logic.
    // For now, it returns a simple JSON object as a byte vector.
    serde_json::to_vec(&serde_json::json!({
        "temperature": 22.5,
        "humidity": 45.0,
    }))
    .map_err(|e| e.to_string())
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

/// Small handler to indicate lack of stream/event support in this scaffolding
///
/// Will report errors for this data operation on updates
async fn handle_unsupported_data_operation(
    data_operation_log_identifier: String,
    mut data_operation_client: DataOperationClient,
    mut asset_update_watcher_rx: watch::Receiver<bool>,
) {
    // Get the status reporter for the unsupported data operation
    let data_operation_status_reporter = data_operation_client.get_status_reporter();

    let data_operation_kind = data_operation_client.kind();
    log::warn!(
        "{data_operation_log_identifier} Data Operation kind {data_operation_kind:?} not supported for this scaffolding"
    );

    let error_status = Err(AdrConfigError {
        message: Some(format!(
            "Data Operation kind {data_operation_kind:?} not supported for this scaffolding",
        )),
        ..Default::default()
    });

    // Report invalid definition to adr
    match data_operation_status_reporter
        .report_status_if_modified(report_status_one_way!(error_status.clone()))
        .await
    {
        Ok(ModifyResult::Reported) => {
            log::debug!("{data_operation_log_identifier} Status reported");
        }
        Ok(ModifyResult::NotModified) => {} // No change, do nothing
        Err(e) => {
            log::error!("{data_operation_log_identifier} Failed to report status: {e}");
        }
    }

    loop {
        tokio::select! {
            biased;
            // Monitor for asset updates to trigger re-reporting the status
            _ = asset_update_watcher_rx.changed() => {
                // Re-report the data operation status as status has been cleared
                match data_operation_status_reporter
                    .report_status_if_modified(report_status_one_way!(error_status.clone()))
                    .await
                {
                    Ok(ModifyResult::Reported) => {
                        log::debug!("{data_operation_log_identifier} Status reported");
                    }
                    Ok(ModifyResult::NotModified) => {} // No change, do nothing
                    Err(e) => {
                        log::error!("{data_operation_log_identifier} Failed to report status: {e}");
                    }
                }
            }
            data_operation_notification = data_operation_client.recv_notification() => {
                match data_operation_notification {
                    DataOperationNotification::Updated => {
                        log::warn!(
                            "{data_operation_log_identifier} update notification received. {data_operation_kind:?} is not supported for this scaffolding",
                        );
                        match data_operation_status_reporter
                            .report_status_if_modified(report_status_one_way!(error_status.clone()))
                            .await
                        {
                            Ok(ModifyResult::Reported) => {
                                log::debug!("{data_operation_log_identifier} Status reported");
                            }
                            Ok(ModifyResult::NotModified) => {} // No change, do nothing
                            Err(e) => {
                                log::error!("{data_operation_log_identifier} Failed to report status: {e}");
                            }
                        }
                    }
                    DataOperationNotification::UpdatedInvalid => {
                        log::info!("{data_operation_log_identifier} Update invalid notification received");
                    }
                    DataOperationNotification::Deleted => {
                        log::info!("{data_operation_log_identifier} Deleted notification received");
                        break;
                    }
                }
            }
        }
    }
}
