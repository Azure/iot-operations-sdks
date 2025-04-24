// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

use std::{collections::HashMap, time::Duration};

use azure_iot_operations_mqtt::{
    MqttConnectionSettingsBuilder,
    session::{Session, SessionExitHandle, SessionManagedClient, SessionOptionsBuilder},
};
use azure_iot_operations_protocol::application::ApplicationContextBuilder;
use azure_iot_operations_services::azure_device_registry;

use env_logger::Builder;

#[tokio::main(flavor = "current_thread")]
async fn main() -> Result<(), Box<dyn std::error::Error>> {
    Builder::new()
        .filter_level(log::LevelFilter::max())
        .format_timestamp(None)
        .filter_module("rumqttc", log::LevelFilter::Warn)
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
    azure_device_registry_client: azure_device_registry::Client<SessionManagedClient>,
    exit_handle: SessionExitHandle,
) {
    let device_name = "my-thermostat".to_string();
    let inbound_endpoint_name = "my-rest-endpoint".to_string();
    let asset_name = "my-rest-thermostat-asset".to_string();
    let timeout = Duration::from_secs(10);

    // observe for updates for our Device + Inbound Endpoint
    match azure_device_registry_client
        .observe_device_update_notifications(
            device_name.clone(),
            inbound_endpoint_name.clone(),
            timeout,
        )
        .await
    {
        Ok(mut observation) => {
            log::info!("Device observed successfully");
            tokio::task::spawn({
                async move {
                    while let Some((notification, _)) = observation.recv_notification().await {
                        log::info!("device updated: {notification:#?}");
                    }
                    log::info!("device notification receiver closed");
                }
            });
        }
        Err(e) => {
            log::error!("Observing for device updates failed: {e}");
        }
    };

    // Get Device + Inbound Endpoint details and send status update
    match azure_device_registry_client
        .observe_asset_update_notifications(
            device_name.clone(),
            inbound_endpoint_name.clone(),
            asset_name.clone(),
            timeout,
        )
        .await
    {
        Ok(mut observation) => {
            log::info!("Asset observed successfully");
            tokio::task::spawn({
                async move {
                    while let Some((notification, _)) = observation.recv_notification().await {
                        log::info!("asset updated: {notification:#?}");
                    }
                    log::info!("asset notification receiver closed");
                }
            });
        }
        Err(e) => {
            log::error!("Observing for asset updates failed: {e}");
        }
    };

    match azure_device_registry_client
        .get_device(device_name.clone(), inbound_endpoint_name.clone(), timeout)
        .await
    {
        Ok(device) => {
            log::info!("Device details: {device:#?}");
            // now we should update the status of the device
            let mut endpoint_statuses = HashMap::new();
            for (endpoint_name, endpoint) in device.specification.endpoints.inbound {
                if endpoint.endpoint_type == "rest-thermostat" {
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
                    device_name.clone(),
                    inbound_endpoint_name.clone(),
                    status,
                    timeout,
                )
                .await
            {
                Ok(updated_device) => {
                    log::info!("Updated Device details: {updated_device:#?}");
                }
                Err(e) => {
                    log::error!("Update device status request failed: {e}");
                }
            };
        }
        Err(e) => {
            log::error!("Get device request failed: {e}");
        }
    };

    match azure_device_registry_client
        .get_asset(
            device_name.clone(),
            inbound_endpoint_name.clone(),
            asset_name.clone(),
            Duration::from_secs(10),
        )
        .await
    {
        Ok(asset) => {
            log::info!("Asset details: {asset:#?}");
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
            match azure_device_registry_client
                .update_asset_status(
                    device_name.clone(),
                    inbound_endpoint_name.clone(),
                    asset_name.clone(),
                    updated_status,
                    Duration::from_secs(10),
                )
                .await
            {
                Ok(updated_asset) => {
                    log::info!("Updated Asset details: {updated_asset:#?}");
                }
                Err(e) => {
                    log::error!("Update asset status request failed: {e}");
                }
            }
        }
        Err(e) => {
            log::error!("Get asset request failed: {e}");
        }
    }

    // Unobserve must be called on clean-up to prevent getting notifications for this in the future
    match azure_device_registry_client
        .unobserve_device_update_notifications(
            device_name.clone(),
            inbound_endpoint_name.clone(),
            timeout,
        )
        .await
    {
        Ok(()) => {
            log::info!("Device unobserved successfully");
        }
        Err(e) => {
            log::error!("Unobserving for device updates failed: {e}");
        }
    };

    // Unobserve must be called on clean-up to prevent getting notifications for this in the future
    match azure_device_registry_client
        .unobserve_asset_update_notifications(
            device_name.clone(),
            inbound_endpoint_name.clone(),
            asset_name.clone(),
            timeout,
        )
        .await
    {
        Ok(()) => {
            log::info!("Asset unobserved successfully");
        }
        Err(e) => {
            log::error!("Unobserving for Asset updates failed: {e}");
        }
    };

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
    match exit_handle.try_exit().await {
        Ok(()) => log::error!("Session exited gracefully"),
        Err(e) => {
            log::error!("Graceful session exit failed: {e}");
            log::error!("Forcing session exit");
            exit_handle.exit_force().await;
        }
    };
}
