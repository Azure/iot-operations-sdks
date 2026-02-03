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
            AssetClient, AssetComponentClient, ClientNotification, DataOperationClient,
            DataOperationNotification, DeviceEndpointClient,
            DeviceEndpointClientCreationObservation, ManagementActionClient,
            ManagementActionNotification, RuntimeHealthEvent, SchemaModifyResult,
        },
    },
    data_processor::derived_json,
    deployment_artifacts::connector::ConnectorArtifacts,
    management_action_executor::{
        ManagementActionApplicationError, ManagementActionExecutor, ManagementActionRequest,
        ManagementActionResponseBuilder,
    },
};
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

#[tokio::main(flavor = "current_thread")]
async fn main() -> Result<(), Box<dyn std::error::Error>> {
    env_logger::Builder::new()
        .filter_level(log::LevelFilter::Warn)
        .format_timestamp(None)
        .filter_module("azure_iot_operations_connector", log::LevelFilter::Info)
        .filter_module("base_connector_sample", log::LevelFilter::Info)
        .init();

    // Create the connector artifacts from the deployment
    let connector_artifacts = ConnectorArtifacts::new_from_deployment()?;

    // Create an ApplicationContext
    let application_context = ApplicationContextBuilder::default().build()?;

    // Create options for the base connector
    let base_connector_options = base_connector::OptionsBuilder::default().build()?;

    // Create the BaseConnector
    let base_connector = BaseConnector::new(
        application_context,
        connector_artifacts,
        base_connector_options,
    )?;

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
    };

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
    let mut device_endpoint_reporter = device_endpoint_client.get_status_reporter();

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

    // Report initial health event after successfully validating and reporting endpoint status
    device_endpoint_reporter.report_health_event(RuntimeHealthEvent::Available);

    loop {
        match device_endpoint_client.recv_notification().await {
            ClientNotification::Deleted => {
                log::warn!("{log_identifier} Device Endpoint deleted");
                break;
            }
            ClientNotification::Updated => {
                // Pause reporting and refresh to the new version before processing the update
                device_endpoint_reporter.pause_and_refresh_health_version();
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
                // Report health event after successfully processing the update
                device_endpoint_reporter.report_health_event(RuntimeHealthEvent::Available);
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
    }

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
            ClientNotification::Created(AssetComponentClient::DataOperation((
                data_operation_client,
                initial_status,
            ))) => {
                let data_operation_log_identifier = format!(
                    "{asset_log_identifier}[{}]",
                    data_operation_client
                        .data_operation_ref()
                        .data_operation_name
                );
                log::info!(
                    "{data_operation_log_identifier} Data Operation Created: {data_operation_client:?}"
                );
                if let DataOperationKind::Dataset = data_operation_client.kind() {
                    tokio::task::spawn(run_dataset(
                        data_operation_log_identifier,
                        data_operation_client,
                        initial_status,
                    ));
                } else {
                    tokio::task::spawn(handle_unsupported_data_operation(
                        data_operation_log_identifier,
                        data_operation_client,
                    ));
                }
            }
            ClientNotification::Created(AssetComponentClient::ManagementAction((
                management_action_client,
                initial_executor,
            ))) => {
                let management_action_log_identifier = format!(
                    "{asset_log_identifier}[Action: {}]",
                    management_action_client
                        .management_action_ref()
                        .management_action_name
                );
                log::info!("{management_action_log_identifier} Management Action Created");
                tokio::task::spawn(run_management_action(
                    management_action_log_identifier,
                    management_action_client,
                    initial_executor,
                ));
            }
            ClientNotification::Created(_) => {}
        }
    }
}

/// Note, this function takes in a `DataOperationClient`, but we know it is specifically a `Dataset`
/// because we already filtered out non-dataset `DataOperationClient`s in the `run_asset` function.
async fn run_dataset(
    log_identifier: String,
    mut data_operation_client: DataOperationClient,
    initial_status: Result<(), AdrConfigError>,
) {
    // Get the status reporter for this data operation - create once and reuse
    let mut data_operation_reporter = data_operation_client.get_status_reporter();

    // now we should update the status of the dataset, using the initial_status since there's nothing else we need to validate ourselves
    if let Err(e) = data_operation_reporter
        .report_status_if_modified(report_status_if_changed!(&log_identifier, initial_status))
        .await
    {
        log::error!("{log_identifier} Error reporting dataset status: {e}");
    }

    let mut local_message_schema = None;
    let mut local_schema_reference = None;
    let mut count = 0;
    // Timer will trigger the sampling of data
    let mut timer = tokio::time::interval(Duration::from_secs(10));
    let mut is_sdk_error_causing_invalid_state = initial_status.is_err();
    let mut last_reported_dataset_status = initial_status;
    let mut dataset_valid = last_reported_dataset_status.is_ok();
    loop {
        tokio::select! {
            biased;
            // Listen for a dataset update notifications
            res = data_operation_client.recv_notification() => {
                // Pause reporting and refresh to the new version before processing the update
                data_operation_reporter.pause_and_refresh_health_version();
                match res {
                    DataOperationNotification::AssetUpdated(Ok(())) => {
                        log::info!("{log_identifier} Asset updated for {:?}", data_operation_client.data_operation_ref());
                        // If the previous Err was detected by the SDK, then this Asset(Ok()) indicates the invalid state has been resolved
                        if is_sdk_error_causing_invalid_state {
                            last_reported_dataset_status = Ok(());
                            dataset_valid = last_reported_dataset_status.is_ok();
                            is_sdk_error_causing_invalid_state = false;
                        }
                        // Update the local schema reference, since it will have been cleared by the version update
                        local_schema_reference = None;
                    },
                    DataOperationNotification::Updated(Ok(())) => {
                        log::info!("{log_identifier} Dataset updated: {data_operation_client:?}");

                        is_sdk_error_causing_invalid_state = false;
                        last_reported_dataset_status = Ok(());
                        dataset_valid = last_reported_dataset_status.is_ok();
                        // Update the local schema reference, since it will have been cleared by the version update
                        local_schema_reference = None;
                    },
                    DataOperationNotification::AssetUpdated(Err(e)) |
                    DataOperationNotification::Updated(Err(e)) => {
                        log::warn!("{log_identifier} Dataset has invalid update. Wait for new dataset update. {e}");
                        is_sdk_error_causing_invalid_state = true;
                        last_reported_dataset_status = Err(e);
                        dataset_valid = false;
                    },
                    DataOperationNotification::Deleted => {
                        log::warn!("{log_identifier} Dataset has been deleted. No more dataset updates will be received");
                        break;
                    }
                }
                // Report the new dataset status (needed for all cases other than deletion)
                if let Err(e) = data_operation_reporter
                    .report_status_if_modified(report_status_if_changed!(&log_identifier, last_reported_dataset_status))
                    .await
                {
                    log::error!("{log_identifier} Error reporting dataset status: {e}");
                }
            },
            _ = timer.tick(), if dataset_valid => {
                let sample_data = mock_received_data(count);

                let current_message_schema =
                    derived_json::create_schema(&sample_data).ok();
                // Report schema only if there isn't already one
                match data_operation_client
                    .report_message_schema_if_modified(|current_schema_reference| {
                        // Only report schema if there isn't already one, or if schema has changed
                        if local_schema_reference.is_none()
                            || current_schema_reference.is_none()
                            || current_schema_reference != local_schema_reference.as_ref()
                            || current_message_schema != local_message_schema
                        {
                            current_message_schema.clone()
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
                        data_operation_reporter.report_health_event(RuntimeHealthEvent::Unavailable {
                            message: Some(format!("Failed to report message schema: {e}")),
                            reason_code: Some("SampleConnectorSchemaReportFailed".to_string()),
                        });
                        continue; // Can't forward data without a schema reported
                    }
                }

                match data_operation_client.forward_data(sample_data).await {
                    Ok(()) => {
                        log::info!(
                            "{log_identifier} data {count} forwarded"
                        );
                        count += 1;
                        data_operation_reporter.report_health_event(RuntimeHealthEvent::Available);
                    }
                    Err(e) => {
                        log::error!("{log_identifier} error forwarding data: {e}");
                        data_operation_reporter.report_health_event(RuntimeHealthEvent::Unavailable {
                            message: Some(format!("Failed to forward data to broker: {e}")),
                            reason_code: Some("SampleConnectorDataForwardFailed".to_string()),
                        });
                    },
                }
            }
        }
    }
}

async fn run_management_action(
    log_identifier: String,
    mut management_action_client: ManagementActionClient,
    initial_executor: Result<ManagementActionExecutor, AdrConfigError>,
) {
    // Get the status reporter for this management action
    let mut management_action_reporter = management_action_client.get_status_reporter();
    let (mut current_executor, mut last_reported_management_action_status) = match initial_executor
    {
        Ok(executor) => (Some(executor), Ok(())),
        Err(e) => (None, Err(e)),
    };
    let mut stale_executor: Option<ManagementActionExecutor> = None;
    let mut is_sdk_error_causing_invalid_state = last_reported_management_action_status.is_err();
    let mut management_action_valid = last_reported_management_action_status.is_ok();
    // now we should update the status of the management action, using the initial_status since there's nothing else we need to validate ourselves
    if let Err(e) = management_action_reporter
        .report_status_if_modified(report_status_if_changed!(
            &log_identifier,
            last_reported_management_action_status
        ))
        .await
    {
        log::error!("{log_identifier} Error reporting management action status: {e}");
    }

    loop {
        tokio::select! {
            biased;
            // drain any pending requests from the out of date executor definition. Bias this above receiving new notifications
            // so that we don't have to ever store more than one stale executor at a time
            request_res = recv_request(&mut stale_executor), if stale_executor.is_some() => {
                // TODO: this branch needs to know the old definition and the old management_action_valid value
                // TODO: maybe draining is a rare case because usually there wouldn't be anything in this queue if commands are processed in another task as they should be
                if let Some(request) = request_res {
                    // WARNING: DON'T REPORT STATUS/HEALTH/MESSAGE SCHEMAS HERE - THIS IS FOR A STALE DEFINITION
                    log::info!("{log_identifier} Management action request received: {:?}", request.raw_payload());

                    let response = if management_action_valid {
                        // Here we would process the management action request
                        // For this example, we simply log it and respond that it succeeded
                        ManagementActionResponseBuilder::default()
                            .payload(vec![])
                            .content_type("application/json".to_string())
                            .cloud_event(None)
                            .build().unwrap()
                    } else {
                        // If the management action is not valid, we respond with an application error
                        ManagementActionResponseBuilder::default()
                            .application_error(ManagementActionApplicationError {
                                application_error_code: "ManagementActionInvalidState".to_string(),
                                application_error_payload: "The management action is in an invalid state and cannot process requests.".to_string(),
                            })
                            .payload(vec![])
                            .content_type("application/json".to_string())
                            .cloud_event(None)
                            .build().unwrap()
                    };

                    if let Err(e) = request.complete(response).await {
                        log::error!("{log_identifier} Error completing management action request: {e}");
                    } else {
                        log::info!("{log_identifier} Management action request completed");
                    }
                }
                else {
                    log::info!("{log_identifier} Old management action executor completed");
                    stale_executor = None;
                }
            },
            // Listen for a management action update notifications
            res = management_action_client.recv_notification() => {
                // Pause reporting and refresh to the new version before processing the update
                management_action_reporter.pause_and_refresh_health_version();
                match res {
                    ManagementActionNotification::AssetUpdated(Ok(())) => {
                        log::info!("{log_identifier} Asset updated for {:?}", management_action_client.management_action_ref());
                        // If the previous Err was detected by the SDK, then this Asset(Ok()) indicates the invalid state has been resolved
                        if is_sdk_error_causing_invalid_state {
                            last_reported_management_action_status = Ok(());
                            management_action_valid = last_reported_management_action_status.is_ok();
                            is_sdk_error_causing_invalid_state = false;
                        }
                    },
                    ManagementActionNotification::Updated(Ok(())) => {
                        log::info!("{log_identifier} Management action updated");

                        is_sdk_error_causing_invalid_state = false;
                        last_reported_management_action_status = Ok(());
                        management_action_valid = last_reported_management_action_status.is_ok();
                    },
                    ManagementActionNotification::UpdatedWithNewExecutor(new_executor) => {
                        log::info!("{log_identifier} Management action updated with new executor");

                        is_sdk_error_causing_invalid_state = false;
                        last_reported_management_action_status = Ok(());
                        management_action_valid = last_reported_management_action_status.is_ok();
                        // move the current executor to the stale marker so we can drain any pending requests
                        // TODO: rather than having a branch for the stale executor, should we just drain it here?
                        // If the application is executing in a separate task, there really shouldn't be a build up to drain anyways
                        stale_executor = current_executor.take();
                        current_executor = new_executor.ok();
                    },
                    ManagementActionNotification::AssetUpdated(Err(e)) |
                    ManagementActionNotification::Updated(Err(e)) => {
                        log::warn!("{log_identifier} Management action has invalid update. Wait for new management action update. {e}");
                        is_sdk_error_causing_invalid_state = true;
                        last_reported_management_action_status = Err(e);
                        management_action_valid = false;
                    },
                    ManagementActionNotification::Deleted => {
                        log::warn!("{log_identifier} Management action has been deleted. No more management action updates will be received");
                        break;
                    }
                }
                // Report the new management action status (needed for all cases other than deletion)
                if let Err(e) = management_action_reporter
                    .report_status_if_modified(report_status_if_changed!(&log_identifier, last_reported_management_action_status))
                    .await
                {
                    log::error!("{log_identifier} Error reporting management action status: {e}");
                }
            },
            request_res = recv_request(&mut current_executor), if current_executor.is_some() => {
                if let Some(request) = request_res {
                    log::info!("{log_identifier} Management action request received: {:?}", request.raw_payload());

                    let response = if management_action_valid {
                        // Here we would process the management action request in another task if it has any async work to do
                        // For this example, we simply log it and respond that it succeeded
                        ManagementActionResponseBuilder::default()
                            .payload(vec![])
                            .content_type("application/json".to_string())
                            .cloud_event(None)
                            .build().unwrap()
                    } else {
                        // If the management action is not valid, we respond with an application error
                        ManagementActionResponseBuilder::default()
                            .application_error(ManagementActionApplicationError {
                                application_error_code: "ManagementActionInvalidState".to_string(),
                                application_error_payload: "The management action is in an invalid state and cannot process requests.".to_string(),
                            })
                            .payload(vec![])
                            .content_type("application/json".to_string())
                            .cloud_event(None)
                            .build().unwrap()
                    };

                    if let Err(e) = request.complete(response).await {
                        log::error!("{log_identifier} Error completing management action request: {e}");
                    } else {
                        log::info!("{log_identifier} Management action request completed");
                        management_action_reporter.report_health_event(RuntimeHealthEvent::Available);
                    }
                } else {
                    log::warn!("{log_identifier} Management action executor closed");
                    current_executor = None;
                }
            },
        }
    }
}

async fn recv_request(
    executor: &mut Option<ManagementActionExecutor>,
) -> Option<ManagementActionRequest> {
    if let Some(ex) = executor {
        ex.recv_request().await
    } else {
        None
    }
}

/// Small handler to indicate lack of stream/event support in this connector
async fn handle_unsupported_data_operation(
    log_identifier: String,
    mut data_operation_client: DataOperationClient,
) {
    let data_operation_kind = data_operation_client.kind();
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
        log::error!("{log_identifier} Error reporting status: {e}");
    }

    // While the unsupported data operation client is active, we should keep polling for updates
    // to handle cases where it is deleted and to continue reporting errors if it is
    // incorrectly updated.
    loop {
        match data_operation_client.recv_notification().await {
            // it doesn't matter if the update is Err or Ok, we can always report unsupported
            DataOperationNotification::AssetUpdated(_) | DataOperationNotification::Updated(_) => {
                log::warn!(
                    "{log_identifier} update notification received. {data_operation_kind:?} is not supported for the this Connector",
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
                    log::error!("{log_identifier} Error reporting status: {e}");
                }
            }
            DataOperationNotification::Deleted => {
                log::info!("{log_identifier} deleted notification received");
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
