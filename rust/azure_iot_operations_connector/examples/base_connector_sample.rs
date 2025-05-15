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

use azure_iot_operations_connector::base_connector::{
    BaseConnector,
    managed_azure_device_registry::{
        AssetClientCreationObservation, DatasetClient, DeviceEndpointClientCreationObservation,
    },
};
use azure_iot_operations_protocol::application::ApplicationContextBuilder;
use azure_iot_operations_services::{azure_device_registry, schema_registry};

#[tokio::main(flavor = "current_thread")]
async fn main() -> Result<(), Box<dyn std::error::Error>> {
    env_logger::Builder::new()
        .filter_level(log::LevelFilter::max())
        .format_timestamp(None)
        .filter_module("rumqttc", log::LevelFilter::Warn)
        .filter_module("azure_iot_operations_mqtt", log::LevelFilter::Warn)
        .filter_module("azure_iot_operations_protocol", log::LevelFilter::Warn)
        .filter_module("notify_debouncer_full", log::LevelFilter::Off)
        .filter_module("notify::inotify", log::LevelFilter::Off)
        .init();

    // Create an ApplicationContext
    let application_context = ApplicationContextBuilder::default().build()?;

    let base_connector = BaseConnector::new(application_context);

    let device_creation_observation =
        base_connector.create_device_endpoint_client_create_observation();

    // Run the Session and the Azure Device Registry operations concurrently
    let r = tokio::join!(
        run_program(device_creation_observation),
        base_connector.run(),
    );
    r.1?;
    Ok(())
}

// This function runs in a loop, waiting for device creation notifications.
async fn run_program(mut device_creation_observation: DeviceEndpointClientCreationObservation) {
    // Wait for a device creation notification
    while let Some((
        mut device_endpoint_client,
        _device_endpoint_update_observation,
        asset_creation_observation,
    )) = device_creation_observation.recv_notification().await
    {
        log::info!("Device created: {device_endpoint_client:?}");

        // now we should update the status of the device
        let endpoint_status = match device_endpoint_client
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
                Err(azure_device_registry::ConfigError {
                    message: Some("endpoint type is not supported".to_string()),
                    ..azure_device_registry::ConfigError::default()
                })
            }
        };

        // Start handling the assets for this device endpoint
        // if we didn't accept the inbound endpoint, then there's no reason to manage the assets
        if endpoint_status.is_ok() {
            tokio::task::spawn(run_assets(asset_creation_observation));
        }

        device_endpoint_client
            .report_status(Ok(()), endpoint_status)
            .await;
    }
    panic!("device_creation_observer has been dropped");
}

// This function runs in a loop, waiting for asset creation notifications.
async fn run_assets(mut asset_creation_observation: AssetClientCreationObservation) {
    // Wait for a device creation notification
    while let Some((asset_client, _asset_update_observation, _asset_deletion_token)) =
        asset_creation_observation.recv_notification().await
    {
        log::info!("Asset created: {asset_client:?}");

        // now we should update the status of the asset
        let asset_status = match asset_client.specification().manufacturer.as_deref() {
            Some("Contoso") | None => Ok(()),
            Some(m) => {
                log::warn!(
                    "Asset '{}' not accepted. Manufacturer '{m}' not supported.",
                    asset_client.asset_ref().name
                );
                Err(azure_device_registry::ConfigError {
                    message: Some("asset manufacturer type is not supported".to_string()),
                    ..azure_device_registry::ConfigError::default()
                })
            }
        };

        asset_client.report_status(asset_status).await;

        for dataset in asset_client.datasets() {
            tokio::task::spawn({
                let dataset_clone = dataset.clone();
                async move {
                    run_dataset(dataset_clone).await;
                }
            });
        }
    }
    panic!("asset_creation_observer has been dropped");
}

async fn run_dataset(dataset_client: DatasetClient) {
    log::info!("new Dataset: {dataset_client:?}");

    // now we should update the status of the dataset and report the message schema
    dataset_client.report_status(Ok(())).await;
    match dataset_client
        .report_message_schema(
            schema_registry::PutRequestBuilder::default()
                .content(
                    r#"
{
  "$schema": "http://json-schema.org/draft-07/schema#",
  "type": "object",
  "properties": {
    "currentTemperature": {
      "type": "number"
    },
    "desiredTemperature": {
      "type": "number"
    }
  }
}
"#,
                )
                .format(schema_registry::Format::JsonSchemaDraft07)
                .build()
                .unwrap(),
        )
        .await
    {
        Ok(message_schema_reference) => {
            log::info!("Message Schema reported, reference returned: {message_schema_reference:?}");
        }
        Err(e) => {
            log::error!("Error reporting message schema: {e}");
        }
    }
}
