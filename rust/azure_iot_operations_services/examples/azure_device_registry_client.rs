// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

use std::{collections::HashMap, time::Duration};

use azure_iot_operations_mqtt::MqttConnectionSettingsBuilder;
use azure_iot_operations_mqtt::session::{
    Session, SessionExitHandle, SessionManagedClient, SessionOptionsBuilder,
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
        &session.create_managed_client(),
        azure_device_registry::ClientOptions::default(),
    )?;

    // Run the Session and the Azure Device Registry operations concurrently
    let r = tokio::join!(
        run_program(azure_device_registry_client, session.create_exit_handle()),
        session.run(),
    );
    r.1?;
    Ok(())
}

async fn run_program(
    azure_device_registry_client: azure_device_registry::Client<SessionManagedClient>,
    exit_handle: SessionExitHandle,
) {
    let device_name = "my-thermostat".to_string();
    let inbound_endpoint_name = "my-rest-endpoint".to_string();
    let asset_name = "my-rest-thermostat-asset".to_string();
    let timeout = Duration::from_secs(5);

    match azure_device_registry_client
        .observe_device_update_notifications(
            device_name.clone(),
            inbound_endpoint_name.clone(),
            timeout,
        )
        .await
    {
        Ok(mut observation) => {
            tokio::task::spawn({
                async move {
                    while let Some((notification, _)) = observation.recv_notification().await {
                        log::info!("device updated! {notification:?}");
                    }
                    log::info!("device notification receiver closed");
                }
            });
        }
        Err(e) => {
            log::error!("Observing for device updates failed: {e}");
        }
    };

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
            tokio::task::spawn({
                async move {
                    while let Some((notification, _)) = observation.recv_notification().await {
                        log::info!("asset updated! {notification:?}");
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
            log::info!("Device details: {device:?}");
            // now we should update the status of the device
            let status = azure_device_registry::DeviceStatus {
                config: None,
                endpoints: HashMap::new(),
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
                    log::info!("Updated Device details: {updated_device:?}");
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
            log::info!("Asset details: {asset:?}");
            let mut updated_datasets = Vec::new();
            for ds in asset.specification.datasets.unwrap() {
                updated_datasets.push(azure_device_registry::AssetDatasetEventStream {
                    error: None,
                    message_schema_reference: None,
                    name: format!("{}_updated", ds.name),
                });
            }
            // // now we should update the status of the asset
            let updated_status = azure_device_registry::AssetStatus {
                config: None,
                datasets_schema: Some(updated_datasets), // Use the updated datasets here
                events_schema: None,
                management_groups: None,
                streams: None,
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
                    log::info!("Updated Asset details: {updated_asset:?}");
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
    // allow time to update Device in ADR service
    // tokio::time::sleep(Duration::from_secs(20)).await;

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
