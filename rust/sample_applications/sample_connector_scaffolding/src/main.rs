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
            AssetClient, AssetComponentClient, AssetSpecification, ClientNotification,
            DataOperationClient, DataOperationNotification, DeviceEndpointClient,
            DeviceEndpointClientCreationObservation, ManagementActionClient,
            ManagementActionNotification, ModifyResult, RuntimeHealthEvent, SchemaModifyResult,
            UnsupportedComponentClient, UnsupportedComponentNotification,
        },
    },
    data_processor::derived_json,
    deployment_artifacts::connector::ConnectorArtifacts,
    management_action_executor::{
        ManagementActionApplicationError, ManagementActionExecutor, ManagementActionRequest,
        ManagementActionResponseBuilder,
    },
};
use azure_iot_operations_protocol::{
    application::ApplicationContextBuilder, common::hybrid_logical_clock::HybridLogicalClock,
};
use azure_iot_operations_services::azure_device_registry::models::ActionType;
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

    // Receive asset updates and dataset creation notifications
    loop {
        match asset_client.recv_notification().await {
            ClientNotification::Updated => {
                log::info!("{asset_log_identifier} Asset update notification received");

                // IMPLEMENT: Add custom asset update/validation logic here

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
            }
            ClientNotification::Created(AssetComponentClient::DataOperation((
                data_operation_client,
                initial_data_operation_status,
            ))) => {
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
                            initial_data_operation_status,
                            device_endpoint_ready_watcher_rx.clone(),
                        ));
                    }
                    azure_iot_operations_connector::DataOperationKind::Event
                    | azure_iot_operations_connector::DataOperationKind::Stream => {
                        // Handle the new stream / event
                        // For this scaffolding, they are not supported. A similar implementation
                        // could be added for handling these types of data operations.
                        tokio::task::spawn(handle_unsupported_component(
                            data_operation_log_identifier,
                            format!("{:?}", data_operation_client.kind()),
                            data_operation_client,
                        ));
                    }
                }
            }
            ClientNotification::Created(AssetComponentClient::ManagementAction((
                management_action_client,
                initial_executor,
            ))) => {
                let management_action_log_identifier = format!(
                    "{asset_log_identifier}[{}]",
                    management_action_client.management_action_ref().name()
                );
                log::info!("{management_action_log_identifier} Management Action created");
                // Handle the new management action
                tokio::task::spawn(handle_management_action(
                    management_action_log_identifier,
                    management_action_client,
                    initial_executor,
                    device_endpoint_ready_watcher_rx.clone(),
                ));
            }
            // IMPLEMENT: If management actions are not supported, this arm can be uncommented and used instead
            // ClientNotification::Created(AssetComponentClient::ManagementAction((
            //     management_action_client,
            //     _initial_executor,
            // ))) => {
            //     let management_action_log_identifier = format!(
            //         "{asset_log_identifier}[{}]",
            //         management_action_client.management_action_ref().name()
            //     );
            //     log::info!("{management_action_log_identifier} Management Action created");
            //     // Handle the new management action
            //     tokio::task::spawn(handle_unsupported_component(
            //         management_action_log_identifier,
            //         "Management Action".to_string(),
            //         management_action_client,
            //     ));
            // }
            ClientNotification::Created(_) => {
                log::warn!(
                    "{asset_log_identifier} Unsupported asset component created, this component will not be handled"
                );
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
/// * `initial_data_operation_status` - Whether the SDK detected an initial error with the dataset.
/// * `device_endpoint_ready_watcher_rx` - A watcher for the device endpoint readiness state.
async fn handle_dataset(
    dataset_log_identifier: String,
    mut data_operation_client: DataOperationClient,
    initial_data_operation_status: Result<(), AdrConfigError>,
    mut device_endpoint_ready_watcher_rx: watch::Receiver<bool>,
) {
    // Get the status reporter for the data operation
    let mut data_operation_status_reporter = data_operation_client.get_status_reporter();

    // Here is one thing that should be validated for most connectors, although it won't be a config error if it's not enabled
    let mut is_asset_ready = data_operation_client
        .asset_specification()
        .enabled
        .is_none_or(|enabled| enabled);
    let mut is_device_endpoint_ready = *device_endpoint_ready_watcher_rx.borrow_and_update();
    // This boolean tracks if the dataset is ready to be sampled.
    let mut is_dataset_ready;
    // This variable keeps track of the latest reported schema.
    let mut last_reported_schema = None;
    // This variable keeps track of the latest reported schema reference.
    let mut last_reported_schema_reference = None;

    // Extract the dataset definition from the dataset client
    let mut _local_dataset_definition = data_operation_client.definition().clone();
    // These variables keep track of the latest reported dataset status
    let mut is_sdk_error_causing_invalid_state = initial_data_operation_status.is_err();
    let mut last_reported_dataset_status = match initial_data_operation_status {
        // IMPLEMENT: If the sdk didn't detect an initial error, verify whether the dataset definition is OK.
        // For this example, we will assume that no additional validation is needed
        Ok(()) => Ok(()),
        Err(e) => Err(e),
    };
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
                // Pause health reporting until we validate the new configuration and successfully
                // complete a sampling cycle. This prevents reporting stale health status from
                // the previous configuration. The "refresh" part snapshots the new specification
                // version so future health events are tagged with the correct version.
                data_operation_status_reporter.pause_and_refresh_health_version();
                // on all updates, check whether the asset is now enabled or not
                is_asset_ready = data_operation_client
                    .asset_specification()
                    .enabled
                    .is_none_or(|enabled| enabled);
                // Match the data operation notification to handle updates, deletions, or invalid updates
                match data_operation_notification {
                    DataOperationNotification::Updated(Ok(())) => {
                        // If we receive an `Ok(())` update from the SDK, then the SDK is not currently detecting any errors with the dataset definition.
                        is_sdk_error_causing_invalid_state = false;
                        log::info!("{dataset_log_identifier} Dataset update notification received. Current Asset ready state is {is_asset_ready}.");

                        // Update the local dataset definition
                        _local_dataset_definition = data_operation_client.definition().clone();

                        // IMPLEMENT: Verify the dataset specification is OK and send an error report if needed
                        last_reported_dataset_status = Ok(());
                    },
                    DataOperationNotification::AssetUpdated(Ok(())) => {
                        log::info!("{dataset_log_identifier} Asset update notification received. Current Asset ready state is {is_asset_ready}.");
                        if is_sdk_error_causing_invalid_state {
                            // IMPLEMENT: If the data operation wasn't valid because of an error detected from the SDK, re-evaluate the definition to see if it's valid now
                            last_reported_dataset_status = Ok(());
                        }
                        // If we receive an `Ok(())` update from the SDK, then the SDK is not currently detecting any errors with the dataset definition.
                        is_sdk_error_causing_invalid_state = false;
                    },
                    DataOperationNotification::Updated(Err(e)) | DataOperationNotification::AssetUpdated(Err(e))=> {
                        is_sdk_error_causing_invalid_state = true;
                        log::error!("{dataset_log_identifier} Dataset update notification received with invalid configuration: {e}");
                        last_reported_dataset_status = Err(e);
                    },
                    DataOperationNotification::Deleted => {
                        // The dataset client has been deleted, we need to end the dataset handler
                        log::info!("{dataset_log_identifier} Dataset deleted notification received, ending dataset handler");
                        break;
                    }
                }
                // Update the dataset readiness state based on the new status
                is_dataset_ready = last_reported_dataset_status.is_ok();

                // Report/re-report the dataset status based on validation.
                match data_operation_status_reporter.report_status_if_modified(report_status_one_way!(
                        last_reported_dataset_status.clone()))
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

/// Handles executions of management action requests.
///
/// # Arguments
/// * `management_action_log_identifier` - A string identifier for the management action, used for logging.
/// * `management_action_client` - The management action client.
/// * `initial_executor` - The initial executor.
/// * `device_endpoint_ready_watcher_rx` - A watcher for the device endpoint readiness state.
async fn handle_management_action(
    management_action_log_identifier: String,
    mut management_action_client: ManagementActionClient,
    initial_executor: Result<ManagementActionExecutor, AdrConfigError>,
    mut device_endpoint_ready_watcher_rx: watch::Receiver<bool>,
) {
    // Get the status reporter for the management action
    let mut management_action_status_reporter = management_action_client.get_status_reporter();

    let (mut current_executor, initial_sdk_management_action_config_status) = match initial_executor
    {
        Ok(executor) => (Some(executor), Ok(())),
        Err(e) => (None, Err(e)),
    };
    let mut action_state = ActionState::new(
        initial_sdk_management_action_config_status,
        &management_action_client.asset_specification(),
        *device_endpoint_ready_watcher_rx.borrow_and_update(),
    );

    if action_state.last_reported_config_status.is_ok() {
        // IMPLEMENT: If the sdk didn't detect an initial error, verify whether the management action definition is OK.
        // For this example, we will assume that no additional validation is needed
        action_state.last_reported_config_status = Ok(());
    }

    // Report the management action status based on validation.
    match management_action_status_reporter
        .report_status_if_modified(report_status_one_way!(
            action_state.last_reported_config_status.clone()
        ))
        .await
    {
        Ok(ModifyResult::Reported) => {
            log::info!("{management_action_log_identifier} Management action status reported");
        }
        Ok(ModifyResult::NotModified) => {} // No change, do nothing
        Err(e) => {
            log::error!(
                "{management_action_log_identifier} Failed to report management action status: {e}"
            );
        }
    }

    // IMPLEMENT: If the request and response message schemas are known, this would be a good place
    // to report them using `report_request_message_schema_if_modified` and `report_response_message_schema_if_modified`

    if action_state.is_ready() {
        // Report healthy if the asset/device is enabled and if definitions are valid
        // because we don't know how long it will be until an actual action request is
        // received, but if the definition is valid, there are no known issues
        management_action_status_reporter.report_health_event(RuntimeHealthEvent::Available);
    }

    loop {
        tokio::select! {
            // When receiving requests at high frequency, multiple requests may occur before updates are handled.
            // Using 'biased;' ensures updates are prioritized over requests.
            biased;
            // Monitor for device endpoint readiness changes
            res = device_endpoint_ready_watcher_rx.changed() => {
                if res.is_err() {
                    // While this signals that the device endpoint has been deleted, the associated management action will be deleted momentarily as well.
                    log::info!("{management_action_log_identifier} Device Endpoint deleted notification received, ending management action handler");
                    // Drain any queued requests from the executor and respond with a clear error
                    if let Some(old_executor) = current_executor.take() {
                        tokio::task::spawn(drain_executor(
                            old_executor,
                            management_action_log_identifier.clone(),
                            "DeviceEndpointDeleted",
                            "Device Endpoint was deleted while this request was queued",
                        ));
                    }
                    break;
                }
                // Update our local device endpoint readiness state
                let previous_device_ready_state = action_state.is_device_endpoint_ready;
                action_state.is_device_endpoint_ready = *device_endpoint_ready_watcher_rx.borrow_and_update();

                // NOTE: If the management action's health depends on device endpoint configuration (e.g., connection
                // settings, authentication), you may want to pause management action health reporting here
                // until the new configuration is validated:
                // management_action_status_reporter.pause_health_reporting();

                log::debug!("{management_action_log_identifier} Device endpoint ready state changed to {}", action_state.is_device_endpoint_ready);
                if !action_state.is_ready() {
                    // If the action state is now not ready, pause health reporting until it is.
                    // If it already wasn't ready, this will be a no-op
                    management_action_status_reporter.pause_health_reporting();
                } else if !previous_device_ready_state {
                    // If we previously weren't ready because of the device endpoint readiness and
                    // that has now changed, report healthy if the asset/device is enabled and if
                    // definitions are valid because we don't know how long it will be until an
                    // actual action request is received, but if the definition is valid, there
                    // are no known issues. If the device was previously ready, then whatever health
                    // was last reported should continue to be reported, so health reporting doesn't
                    // need to be paused or have Available sent again.
                    management_action_status_reporter.report_health_event(RuntimeHealthEvent::Available);
                }
            },
            action_notification = management_action_client.recv_notification() => {
                // Pause health reporting until we evaluate the new definition.
                // This prevents reporting stale health status from the previous
                // configuration. The "refresh" part snapshots the new specification
                // version so future health events are tagged with the correct version.
                management_action_status_reporter.pause_and_refresh_health_version();
                // On all updates, check whether the asset is now enabled or not
                action_state.update_asset_ready(&management_action_client.asset_specification());
                // Match the management action notification to handle updates, deletions, or invalid updates
                match action_notification {
                    ManagementActionNotification::UpdatedWithNewExecutor(new_executor) => {
                        log::info!("{management_action_log_identifier} Management Action executor update notification received.");
                        // Set the last reported config status based on the new executor's result
                        action_state.last_reported_config_status = new_executor.as_ref().map(|_| ()).map_err(Clone::clone);
                        action_state.is_sdk_error_causing_invalid_state = action_state.last_reported_config_status.is_err();
                        if action_state.last_reported_config_status.is_ok() {
                            // IMPLEMENT: If the sdk didn't detect an error, verify whether the management action specification is OK.
                            action_state.last_reported_config_status = Ok(());
                        }
                        // Drain any queued requests from the old executor and respond with an error,
                        // since they were queued against a definition that is no longer active.
                        // NOTE: Depending on the connector's needs, it may be desirable to instead attempt
                        // to execute the queued requests against the old definition, but for this scaffolding
                        // we will just respond with an error.
                        if let Some(old_executor) = current_executor.take() {
                            tokio::task::spawn(drain_executor(
                                old_executor,
                                management_action_log_identifier.clone(),
                                "ManagementActionDefinitionOutdated",
                                "Management action definition was updated while this request was queued",
                            ));
                        }
                        current_executor = new_executor.ok();
                    },
                    ManagementActionNotification::Updated(Ok(())) => {
                        // If we receive an `Ok(())` update from the SDK, then the SDK is not currently detecting any errors with the management action definition.
                        action_state.is_sdk_error_causing_invalid_state = false;
                        log::info!("{management_action_log_identifier} Management Action update notification received");

                        // IMPLEMENT: Verify the management action specification is OK and send an error report if needed
                        action_state.last_reported_config_status = Ok(());
                    },
                    ManagementActionNotification::AssetUpdated(Ok(())) => {
                        log::info!("{management_action_log_identifier} Asset update notification received.");
                        // If the previous Err was detected by the SDK, then this Asset(Ok()) indicates the invalid state has been resolved
                        if action_state.is_sdk_error_causing_invalid_state {
                            // IMPLEMENT: If the management action wasn't valid because of an error detected from the SDK, re-evaluate the definition to see if it's valid now
                            action_state.last_reported_config_status = Ok(());
                        }
                        // If we receive an `Ok(())` update from the SDK, then the SDK is not currently detecting any errors with the management action definition.
                        action_state.is_sdk_error_causing_invalid_state = false;
                    },
                    ManagementActionNotification::Updated(Err(e)) | ManagementActionNotification::AssetUpdated(Err(e)) => {
                        // The management action update is invalid, we need to wait for a valid update
                        log::error!("{management_action_log_identifier} Management Action update notification received with invalid configuration: {e}");
                        action_state.is_sdk_error_causing_invalid_state = true;
                        action_state.last_reported_config_status = Err(e);

                        // Continue to wait for a valid update
                    },
                    ManagementActionNotification::Deleted => {
                        // The management action client has been deleted, we need to end the management action handler
                        log::info!("{management_action_log_identifier} Management action deleted notification received, ending management action handler");
                        // Drain any queued requests from the executor and respond with a clear error
                        if let Some(old_executor) = current_executor.take() {
                            tokio::task::spawn(drain_executor(
                                old_executor,
                                management_action_log_identifier.clone(),
                                "ManagementActionDeleted",
                                "Management action was deleted while this request was queued",
                            ));
                        }
                        break;
                    },
                }

                // Report/re-report the management action status based on validation.
                match management_action_status_reporter
                    .report_status_if_modified(report_status_one_way!(action_state.last_reported_config_status.clone()))
                    .await
                {
                    Ok(ModifyResult::Reported) => {
                        log::info!("{management_action_log_identifier} Management action status reported");
                    }
                    Ok(ModifyResult::NotModified) => {} // No change, do nothing
                    Err(e) => {
                        log::error!("{management_action_log_identifier} Failed to report management action status: {e}");
                    }
                }

                // IMPLEMENT: If the request and response message schemas are known, this would be a good place
                // to report them using `report_request_message_schema_if_modified` and `report_response_message_schema_if_modified`.
                // Note that they must be re-reported on any definition update even if they haven't changed

                if action_state.is_ready() {
                    // Report healthy if the asset is enabled and if definitions are valid because we don't know
                    // how long it will be until an actual action request is received, but if
                    // the definition is valid, there are no known issues
                    management_action_status_reporter.report_health_event(RuntimeHealthEvent::Available);
                }
            },
            request_res = recv_request(&mut current_executor), if current_executor.is_some() => {
                if let Some(request) = request_res {
                    log::debug!("{management_action_log_identifier} Management action request received");
                    // Process, execute, and complete the request in a spawned task so we don't block this
                    // loop from processing other updates or requests.
                    tokio::task::spawn({
                        let management_action_log_identifier = management_action_log_identifier.clone();
                        let management_action_status_reporter = management_action_status_reporter.clone();
                        let action_type = management_action_client.definition().action_type.clone();
                        let action_ready = action_state.is_ready_or_err();
                        async move {
                            let response = if let Err(reason) = action_ready {
                                // If the management action is not ready, we respond with an application error
                                log::warn!("{management_action_log_identifier} Management action request received but action is not ready: {reason}");
                                Err(ManagementActionApplicationError {
                                    application_error_code: "ActionNotReady".to_string(),
                                    application_error_payload: reason,
                                })
                            } else {
                                // IMPLEMENT: Implement handling different action types based on your connector's capabilities.
                                // Note that all requests should be responded to, even if they aren't supported. This example shows
                                // a connector that supports Read and Write requests, but not Call requests
                                match action_type {
                                    ActionType::Call => {
                                        log::warn!("{management_action_log_identifier} Call action type is not supported for this connector scaffolding");
                                        Err(ManagementActionApplicationError {
                                            application_error_code: "UnsupportedActionType".to_string(),
                                            application_error_payload: "Call action type is not supported by this connector scaffolding".to_string(),
                                        })
                                    },
                                    ActionType::Read | ActionType::Write => {
                                        // IMPLEMENT: This should be replaced with the actual execution logic for the action type.
                                        match mock_execute_action() {
                                            Ok(response) => {
                                                log::debug!("{management_action_log_identifier} Action executed successfully");
                                                management_action_status_reporter.report_health_event(RuntimeHealthEvent::Available);
                                                // IMPLEMENT: Depending on the structure of the connector, it may also be necessary to report a health event for the device endpoint here as well
                                                // device_endpoint_status_reporter_clone.report_health_event(RuntimeHealthEvent::Available);
                                                Ok(response)
                                            },
                                            Err(e) => {
                                                log::error!("{management_action_log_identifier} Action execution failed: {e:?}");
                                                // NOTE: reason_codes for unavailable health events should include the Connector Name as a prefix
                                                management_action_status_reporter.report_health_event(RuntimeHealthEvent::Unavailable {
                                                    message: Some(e.application_error_payload.clone()),
                                                    reason_code: Some("ScaffoldingConnectorExecutionFailed".to_string()),
                                                });
                                                // IMPLEMENT: If there are errors executing the request, a device endpoint health event may need to be reported as well
                                                // device_endpoint_status_reporter_clone.report_health_event(RuntimeHealthEvent::Unavailable { ... });
                                                Err(e)
                                            }
                                        }
                                    },
                                }
                            };
                            complete_management_action_request(request, management_action_log_identifier, response).await;
                        }
                    });
                } else {
                    // This will occur when the the executor has been shutdown because it's no longer valid
                    log::info!("{management_action_log_identifier} Management action executor shut down, no more requests will be received");
                    current_executor = None;
                }
            }
        }
    }
}

/// Helper function to receive a management action request from the executor, if the executor exists.
/// Returns `None` if there will be no more requests from this executor or if there is no executor,
/// which could be the case if the configuration is invalid.
async fn recv_request(
    executor: &mut Option<ManagementActionExecutor>,
) -> Option<ManagementActionRequest> {
    if let Some(ex) = executor {
        ex.recv_request().await
    } else {
        None
    }
}

/// Drains all pending requests from a superseded or deleted executor
/// Intended to be spawned so the caller is not blocked.
async fn drain_executor(
    mut executor: ManagementActionExecutor,
    log_identifier: String,
    error_code: &'static str,
    error_payload: &'static str,
) {
    while let Some(stale_request) = executor.recv_request().await {
        log::warn!(
            "{log_identifier} Draining stale request from executor, responding with {error_code} error"
        );
        tokio::task::spawn(complete_management_action_request(
            stale_request,
            log_identifier.clone(),
            Err(ManagementActionApplicationError {
                application_error_code: error_code.to_string(),
                application_error_payload: error_payload.to_string(),
            }),
        ));
    }
}

/// Builds and completes a management action request.
/// Intended to be spawned so the caller is not blocked on the response round-trip.
///
/// # Arguments
/// * `request` - The management action request to complete
/// * `management_action_log_identifier` - A string identifier for the management action, used for logging
/// * `result` - `Err(error)` to include an application error with the response; `Ok(payload)` for a success response (may be `vec![]`)
async fn complete_management_action_request(
    request: ManagementActionRequest,
    management_action_log_identifier: String,
    result: Result<Vec<u8>, ManagementActionApplicationError>,
) {
    let mut response_builder = ManagementActionResponseBuilder::default();
    // IMPLEMENT: Change the content type and cloud event implementations as needed - for
    // some implementations it may make sense to pass them in as function arguments
    response_builder
        .content_type("application/json".to_string())
        .cloud_event(None);
    match result {
        Ok(success_response_payload) => {
            response_builder.payload(success_response_payload);
        }
        Err(e) => {
            // IMPLEMENT: some connectors may desire to pass an error payload as well - this function
            // can be modified to support passing that in
            response_builder.payload(vec![]);
            response_builder.application_error(e);
        }
    }

    match response_builder.build() {
        Ok(response) => {
            if let Err(e) = request.complete(response).await {
                log::error!(
                    "{management_action_log_identifier} Error completing management action request: {e}"
                );
            } else {
                log::debug!(
                    "{management_action_log_identifier} Management action request completed"
                );
            }
        }
        Err(e) => {
            log::error!(
                "{management_action_log_identifier} Failed to build management action response: {e}"
            );
            // Drop request, which will send an error response to the invoker
        }
    }
}

fn mock_execute_action() -> Result<Vec<u8>, ManagementActionApplicationError> {
    // IMPLEMENT: This function is a mock for executing an action, it should be replaced with the actual execution logic.
    // For now, it returns a simple JSON object as a byte vector.
    serde_json::to_vec(&serde_json::json!({
        "execution": "success",
        "newValue": 45.0,
    }))
    // IMPLEMENT: Replace this with mapping from actual execution errors to appropriately named error codes and payloads
    .map_err(|e| ManagementActionApplicationError {
        application_error_code: "ActionExecutionFailed".to_string(),
        application_error_payload: format!("Action execution failed: {e}."),
    })
}

/// A struct to track the readiness state of a management action based on its asset
/// specification, device endpoint readiness, and any configuration errors detected
/// from the SDK or validation. Additional state and validations can be added here.
struct ActionState {
    /// Whether the asset is enabled or not
    is_asset_ready: bool,
    /// Whether the device endpoint is enabled or not
    pub is_device_endpoint_ready: bool,
    /// The last reported configuration status for this management action.
    /// This is used to determine what status to report to ADR and whether
    /// the management action is ready for requests.
    pub last_reported_config_status: Result<(), AdrConfigError>,
    /// Whether the SDK has reported the error that causes the management action config status to be an error
    pub is_sdk_error_causing_invalid_state: bool,
}

impl ActionState {
    fn new(
        initial_sdk_management_action_config_status: Result<(), AdrConfigError>,
        asset_specification: &AssetSpecification,
        is_device_endpoint_ready: bool,
    ) -> Self {
        Self {
            is_asset_ready: asset_specification.enabled.is_none_or(|enabled| enabled),
            is_device_endpoint_ready,
            is_sdk_error_causing_invalid_state: initial_sdk_management_action_config_status
                .is_err(),
            last_reported_config_status: initial_sdk_management_action_config_status,
        }
    }

    fn update_asset_ready(&mut self, asset_specification: &AssetSpecification) {
        self.is_asset_ready = asset_specification.enabled.is_none_or(|enabled| enabled);
    }

    fn is_ready(&self) -> bool {
        self.is_asset_ready
            && self.is_device_endpoint_ready
            && self.last_reported_config_status.is_ok()
    }

    /// Returns an error payload describing why the action isn't ready
    fn is_ready_or_err(&self) -> Result<(), String> {
        if self.is_ready() {
            Ok(())
        } else if let Err(e) = &self.last_reported_config_status {
            Err(format!("Management action configuration error: {e}"))
        } else if !self.is_asset_ready {
            Err("Asset is not enabled".to_string())
        } else if !self.is_device_endpoint_ready {
            Err("Device endpoint is not enabled".to_string())
        } else {
            Err("Unknown reason".to_string())
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

/// Small handler to indicate lack of support for a component in this scaffolding
///
/// Will report errors for this component on updates
///
/// # Arguments
/// * `log_identifier` - A string identifier for the component, used for logging.
/// * `component_name` - The name of the kind of component.
/// * `unsupported_client` - The client for the unsupported component.
async fn handle_unsupported_component<T: UnsupportedComponentClient>(
    log_identifier: String,
    component_name: String,
    mut unsupported_client: T,
) {
    // Get the status reporter for the unsupported component
    let status_reporter = unsupported_client.get_status_reporter();

    log::warn!("{log_identifier} {component_name}s are not supported for this scaffolding");

    let adr_config_error = Err(AdrConfigError {
        message: Some(format!(
            "{component_name}s are not supported for this scaffolding"
        )),
        ..Default::default()
    });

    // Report invalid definition to adr
    match status_reporter
        .report_status_if_modified(report_status_one_way!(adr_config_error.clone()))
        .await
    {
        Ok(ModifyResult::Reported) => {
            log::debug!("{log_identifier} {component_name} status reported as error");
        }
        Ok(ModifyResult::NotModified) => {} // No change, do nothing
        Err(e) => {
            log::error!("{log_identifier} Failed to report {component_name} status: {e}");
        }
    }

    loop {
        match unsupported_client.recv_notification().await {
            UnsupportedComponentNotification::Updated => {
                log::warn!(
                    "{log_identifier} {component_name} update notification received. {component_name}s are not supported for this scaffolding"
                );

                match status_reporter
                    .report_status_if_modified(report_status_one_way!(adr_config_error.clone()))
                    .await
                {
                    Ok(ModifyResult::Reported) => {
                        log::debug!("{log_identifier} {component_name} status reported as error");
                    }
                    Ok(ModifyResult::NotModified) => {} // No change, do nothing
                    Err(e) => {
                        log::error!(
                            "{log_identifier} Failed to report {component_name} status: {e}"
                        );
                    }
                }
            }
            UnsupportedComponentNotification::Deleted => {
                log::info!("{log_identifier} {component_name} deleted notification received");
                break;
            }
        }
    }
}
