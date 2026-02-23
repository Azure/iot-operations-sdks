// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

use std::{collections::HashMap, time::Duration};

use azure_iot_operations_mqtt::{
    aio::connection_settings::MqttConnectionSettingsBuilder,
    session::{Session, SessionExitHandle, SessionOptionsBuilder},
};
use azure_iot_operations_protocol::application::ApplicationContextBuilder;
use azure_iot_operations_services::azure_device_registry::{
    self, HealthStatus, RuntimeHealth, health_reporter::ReportInterval, models, models::DeviceRef,
};
use tokio_util::sync::CancellationToken;

// Replace these values with the actual values for your device, inbound endpoint, and asset.
// They must be present in the Azure Device Registry Service for the example to work correctly.
const DEVICE_NAME: &str = "my-thermostat";
const INBOUND_ENDPOINT_NAME: &str = "my-rest-endpoint";
const ASSET_NAME: &str = "my-rest-thermostat-asset";
const VALID_ENDPOINT_TYPE: &str = "rest-thermostat";
const TIMEOUT: Duration = Duration::from_secs(10);

#[tokio::main(flavor = "current_thread")]
async fn main() -> Result<(), Box<dyn std::error::Error>> {
    env_logger::Builder::new()
        .filter_level(log::LevelFilter::Info)
        .format_timestamp(None)
        .filter_module("azure_mqtt", log::LevelFilter::Warn)
        .init();

    // Create a Session
    let connection_settings = MqttConnectionSettingsBuilder::default()
        .client_id("adr-client-app")
        .hostname("localhost")
        .tcp_port(1883u16)
        .use_tls(false)
        .build()?;
    let session_options = SessionOptionsBuilder::default()
        .connection_settings(connection_settings)
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

    // Run the Session and the Azure Device Registry operations concurrently
    let r = tokio::join!(
        azure_device_registry_operations(
            azure_device_registry_client,
            session.create_exit_handle()
        ),
        session.run(),
    );
    r.1?;
    Ok(())
}

async fn azure_device_registry_operations(
    azure_device_registry_client: azure_device_registry::Client,
    exit_handle: SessionExitHandle,
) {
    // Create a cancellation token for health reporting
    let health_cancellation = CancellationToken::new();

    // Create a background health reporter for the device endpoint
    let device_ref = DeviceRef {
        device_name: DEVICE_NAME.to_string(),
        endpoint_name: INBOUND_ENDPOINT_NAME.to_string(),
    };
    let endpoint_reporter = azure_device_registry_client.new_device_endpoint_health_reporter(
        device_ref,
        TIMEOUT,
        ReportInterval::default(),
        health_cancellation.clone(),
    );

    // observe for updates for our Device + Inbound Endpoint
    match azure_device_registry_client
        .observe_device_update_notifications(
            DEVICE_NAME.to_string(),
            INBOUND_ENDPOINT_NAME.to_string(),
            TIMEOUT,
        )
        .await
    {
        Ok(mut observation) => {
            log::info!("Device observed successfully");
            tokio::task::spawn({
                async move {
                    while let Some((notification, _)) = observation.recv_notification().await {
                        log::info!("device updated: {notification:?}");
                    }
                    log::info!("device notification receiver closed");
                }
            });
        }
        Err(e) => {
            log::error!("Observing for device updates failed: {e}");
        }
    }

    // observe for updates for our Asset
    match azure_device_registry_client
        .observe_asset_update_notifications(
            DEVICE_NAME.to_string(),
            INBOUND_ENDPOINT_NAME.to_string(),
            ASSET_NAME.to_string(),
            TIMEOUT,
        )
        .await
    {
        Ok(mut observation) => {
            log::info!("Asset observed successfully");
            tokio::task::spawn({
                async move {
                    while let Some((notification, _)) = observation.recv_notification().await {
                        log::info!("asset updated: {notification:?}");
                    }
                    log::info!("asset notification receiver closed");
                }
            });
        }
        Err(e) => {
            log::error!("Observing for asset updates failed: {e}");
        }
    }

    // run device operations and log any errors
    match device_operations(&azure_device_registry_client, &endpoint_reporter).await {
        Ok(()) => {
            log::info!("Device operations completed successfully");
        }
        Err(e) => {
            log::error!("Device operations failed: {e}");
        }
    }

    // run asset operations and log any errors
    match asset_operations(&azure_device_registry_client).await {
        Ok(()) => {
            log::info!("Asset operations completed successfully");
        }
        Err(e) => {
            log::error!("Asset operations failed: {e}");
        }
    }

    // Unobserve must be called on clean-up to prevent getting notifications for this in the future
    match azure_device_registry_client
        .unobserve_device_update_notifications(
            DEVICE_NAME.to_string(),
            INBOUND_ENDPOINT_NAME.to_string(),
            TIMEOUT,
        )
        .await
    {
        Ok(()) => {
            log::info!("Device unobserved successfully");
        }
        Err(e) => {
            log::error!("Unobserving for device updates failed: {e}");
        }
    }

    // Unobserve must be called on clean-up to prevent getting notifications for this in the future
    match azure_device_registry_client
        .unobserve_asset_update_notifications(
            DEVICE_NAME.to_string(),
            INBOUND_ENDPOINT_NAME.to_string(),
            ASSET_NAME.to_string(),
            TIMEOUT,
        )
        .await
    {
        Ok(()) => {
            log::info!("Asset unobserved successfully");
        }
        Err(e) => {
            log::error!("Unobserving for Asset updates failed: {e}");
        }
    }

    // Cancel health reporting before shutdown
    log::info!("Cancelling health reporter");
    health_cancellation.cancel();

    match azure_device_registry_client.shutdown().await {
        Ok(()) => {
            log::info!("azure_device_registry_client shutdown successfully");
        }
        Err(e) => {
            log::warn!(
                "Error shutting down azure_device_registry_client. Retry may be desired. {e}"
            );
        }
    }

    log::info!("Exiting session");
    match exit_handle.try_exit() {
        Ok(()) => log::info!("Session exited gracefully"),
        Err(e) => {
            log::error!("Graceful session exit failed: {e}");
            log::warn!("Forcing session exit");
            exit_handle.force_exit();
        }
    }
}

async fn device_operations(
    azure_device_registry_client: &azure_device_registry::Client,
    health_sender: &azure_device_registry::health_reporter::HealthReporterSender,
) -> Result<(), Box<dyn std::error::Error>> {
    // Get Device + Inbound Endpoint details and send status update
    let device = azure_device_registry_client
        .get_device(
            DEVICE_NAME.to_string(),
            INBOUND_ENDPOINT_NAME.to_string(),
            TIMEOUT,
        )
        .await?;
    log::info!("Device details: {device:?}");

    // Report healthy status for the device endpoint
    // The background task handles deduplication and periodic re-reporting
    let _ = health_sender.report(RuntimeHealth {
        version: device.version.unwrap_or(0),
        status: HealthStatus::Available,
        message: Some("Connected and operational".to_string()),
        reason_code: None,
        last_update_time: chrono::Utc::now(),
    });
    log::info!("Reported healthy status for device endpoint");

    // get the current status so that we can update it in place
    let mut device_status = azure_device_registry_client
        .get_device_status(
            DEVICE_NAME.to_string(),
            INBOUND_ENDPOINT_NAME.to_string(),
            TIMEOUT,
        )
        .await?;
    log::info!("Device status: {device_status:?}");

    // if config isn't present on status or the version is out of date, then we should update/add it
    if device_status
        .config
        .as_ref()
        .is_none_or(|config| config.version != device.version)
    {
        device_status.config = Some(azure_device_registry::ConfigStatus {
            version: device.version,
            error: None,
            last_transition_time: Some(chrono::Utc::now()),
        });
    }

    // build the statuses for the endpoints
    let mut endpoint_statuses = HashMap::new();
    for (endpoint_name, endpoint) in device.endpoints.unwrap().inbound {
        if endpoint.endpoint_type == VALID_ENDPOINT_TYPE {
            log::info!("Endpoint '{endpoint_name}' accepted");
            // adding endpoint to status hashmap with None ConfigError to show that we accept the endpoint with no errors
            endpoint_statuses.insert(endpoint_name, None);
        } else {
            // if we don't support the endpoint type, then we can report that error
            log::warn!(
                "Endpoint '{endpoint_name}' not accepted. Endpoint type '{}' not supported.",
                endpoint.endpoint_type
            );
            endpoint_statuses.insert(
                endpoint_name,
                Some(azure_device_registry::ConfigError {
                    message: Some("endpoint type is not supported".to_string()),
                    ..Default::default()
                }),
            );
        }
    }
    device_status.endpoints = endpoint_statuses;

    // send the fully updated status to Azure Device Registry
    log::info!("Device status to send as update: {device_status:?}");
    let updated_device_status = azure_device_registry_client
        .update_device_plus_endpoint_status(
            DEVICE_NAME.to_string(),
            INBOUND_ENDPOINT_NAME.to_string(),
            device_status,
            TIMEOUT,
        )
        .await?;
    log::info!("Updated Device status: {updated_device_status:?}");

    Ok(())
}

async fn asset_operations(
    azure_device_registry_client: &azure_device_registry::Client,
) -> Result<(), Box<dyn std::error::Error>> {
    // Get Asset details and send status update
    let asset = azure_device_registry_client
        .get_asset(
            DEVICE_NAME.to_string(),
            INBOUND_ENDPOINT_NAME.to_string(),
            ASSET_NAME.to_string(),
            TIMEOUT,
        )
        .await?;
    log::info!("Asset details: {asset:?}");

    // get the current status so that we can update it in place
    let mut asset_status = azure_device_registry_client
        .get_asset_status(
            DEVICE_NAME.to_string(),
            INBOUND_ENDPOINT_NAME.to_string(),
            ASSET_NAME.to_string(),
            TIMEOUT,
        )
        .await?;
    log::info!("Asset status: {asset_status:?}");

    // if config isn't present on status or the version is out of date, then we should update/add it
    if asset_status
        .config
        .as_ref()
        .is_none_or(|config| config.version != asset.version)
    {
        asset_status.config = Some(azure_device_registry::ConfigStatus {
            version: asset.version,
            error: None,
            last_transition_time: Some(chrono::Utc::now()),
        });
    }

    let mut dataset_statuses = Vec::new();
    for dataset in asset.datasets {
        dataset_statuses.push(models::DatasetEventStreamStatus {
            error: None,
            message_schema_reference: None,
            name: dataset.name,
        });
    }
    asset_status.datasets = Some(dataset_statuses);
    log::info!("Asset status to send as update: {asset_status:?}");
    let updated_asset_status = azure_device_registry_client
        .update_asset_status(
            DEVICE_NAME.to_string(),
            INBOUND_ENDPOINT_NAME.to_string(),
            ASSET_NAME.to_string(),
            asset_status,
            TIMEOUT,
        )
        .await?;
    log::info!("Updated Asset status: {updated_asset_status:?}");

    Ok(())
}
