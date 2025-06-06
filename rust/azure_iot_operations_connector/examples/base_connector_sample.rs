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

use std::time::Duration;

use azure_iot_operations_connector::{
    AdrConfigError, Data,
    base_connector::{
        BaseConnector,
        managed_azure_device_registry::{
            AssetClient, AssetClientCreationObservation, DatasetClient,
            DatasetClientCreationObservation, DeviceEndpointClient,
            DeviceEndpointClientCreationObservation,
        },
    },
    data_processor::derived_json,
};
use azure_iot_operations_protocol::application::ApplicationContextBuilder;

#[tokio::main(flavor = "current_thread")]
async fn main() -> Result<(), Box<dyn std::error::Error>> {
    env_logger::Builder::new()
        .filter_level(log::LevelFilter::max())
        .format_timestamp(None)
        .filter_module("rumqttc", log::LevelFilter::Warn)
        .filter_module("azure_iot_operations_mqtt", log::LevelFilter::Warn)
        .filter_module("azure_iot_operations_protocol", log::LevelFilter::Warn)
        .filter_module("azure_iot_operations_services", log::LevelFilter::Warn)
        .filter_module("azure_iot_operations_connector", log::LevelFilter::Info)
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
    while let Some((device_endpoint_client, asset_creation_observation)) =
        device_creation_observation.recv_notification().await
    {
        log::info!("Device created: {device_endpoint_client:?}");

        // now we should update the status of the device
        let endpoint_status = generate_endpoint_status(&device_endpoint_client);

        device_endpoint_client
            .report_status(Ok(()), endpoint_status)
            .await;

        // Start handling the assets for this device endpoint
        // if we didn't accept the inbound endpoint, then we still want to run this to wait for updates
        tokio::task::spawn(run_device(
            device_endpoint_client,
            asset_creation_observation,
        ));
    }
}

// This function runs in a loop, waiting for asset creation notifications.
async fn run_device(
    mut device_endpoint_client: DeviceEndpointClient,
    mut asset_creation_observation: AssetClientCreationObservation,
) {
    loop {
        tokio::select! {
            biased;
            // Listen for a device update notifications
            res = device_endpoint_client.recv_update() => {
                if let Some(()) = res {
                    log::info!("Device updated: {device_endpoint_client:?}");
                    // now we should update the status of the device
                    let endpoint_status = generate_endpoint_status(&device_endpoint_client);

                    device_endpoint_client
                        .report_status(Ok(()), endpoint_status)
                        .await;
                } else {
                    log::error!("No more Device Endpoint updates will be received");
                    break;
                }
            }
            // Listen for a asset creation notifications
            res = asset_creation_observation.recv_notification() => {
                if let Some((asset_client, _asset_deletion_token, dataset_creation_observation)) = res {
                    log::info!("Asset created: {asset_client:?}");

                    // now we should update the status of the asset
                    let asset_status = generate_asset_status(&asset_client);

                    asset_client.report_status(asset_status).await;

                    // Start handling the datasets for this asset
                    // if we didn't accept the asset, then we still want to run this to wait for updates
                    tokio::task::spawn(run_asset(
                        asset_client,
                        dataset_creation_observation,
                    ));
                } else {
                    log::error!("asset_creation_observer has been dropped");
                    break;
                }
            }
        }
    }
    panic!("asset_creation_observer has been dropped");
}

// This function runs in a loop, waiting for dataset creation notifications.
async fn run_asset(
    mut asset_client: AssetClient,
    mut dataset_creation_observation: DatasetClientCreationObservation,
) {
    loop {
        tokio::select! {
            biased;
            // Listen for a asset update notifications
            res = asset_client.recv_update() => {
                if let Some(()) = res {
                    log::info!("asset updated: {asset_client:?}");
                    // now we should update the status of the asset
                    let asset_status = generate_asset_status(&asset_client);

                    asset_client
                        .report_status(asset_status)
                        .await;
                } else {
                    log::error!("No more Asset updates will be received");
                    break;
                }
            }
            // Listen for a dataset creation notifications
            res = dataset_creation_observation.recv_notification() => {
                if let Some(dataset_client) = res {
                    tokio::task::spawn(run_dataset(dataset_client));
                } else {
                    log::error!("asset_creation_observer has been dropped");
                    break;
                }
            }
        }
    }
    panic!("asset_creation_observer has been dropped");
}

async fn run_dataset(mut dataset_client: DatasetClient) {
    log::info!("new Dataset: {dataset_client:?}");

    // now we should update the status of the dataset and report the message schema
    dataset_client.report_status(Ok(())).await;

    let sample_data = mock_received_data(0);

    let (_, message_schema) =
        derived_json::transform(sample_data, dataset_client.dataset_definition()).unwrap();
    match dataset_client.report_message_schema(message_schema).await {
        Ok(message_schema_reference) => {
            log::info!("Message Schema reported, reference returned: {message_schema_reference:?}");
        }
        Err(e) => {
            log::error!("Error reporting message schema: {e}");
        }
    }
    let mut count = 0;
    // Timer will trigger the sampling of data
    let mut timer = tokio::time::interval(Duration::from_secs(10));
    loop {
        tokio::select! {
            biased;
            // Listen for a dataset update notifications
            res = dataset_client.recv_update() => {
                if let Some(()) = res {
                    log::info!("dataset updated: {dataset_client:?}");
                    // now we should update the status of the dataset and report the message schema
                    dataset_client.report_status(Ok(())).await;

                    let sample_data = mock_received_data(0);

                    let (_, message_schema) =
                        derived_json::transform(sample_data, dataset_client.dataset_definition()).unwrap();
                    match dataset_client.report_message_schema(message_schema).await {
                        Ok(message_schema_reference) => {
                            log::info!("Message Schema reported, reference returned: {message_schema_reference:?}");
                        }
                        Err(e) => {
                            log::error!("Error reporting message schema: {e}");
                        }
                    }
                } else {
                    log::error!("Dataset has been deleted. No more dataset updates will be received");
                    break;
                }
            }
            _ = timer.tick() => {
                let sample_data = mock_received_data(count);
                let (transformed_data, _) =
                    derived_json::transform(sample_data.clone(), dataset_client.dataset_definition())
                        .unwrap();
                match dataset_client.forward_data(transformed_data).await {
                    Ok(()) => {
                        log::info!(
                            "data {count} for {} forwarded",
                            dataset_client.dataset_ref().dataset_name
                        );
                        count += 1;
                    }
                    Err(e) => log::error!("error forwarding data: {e}"),
                }
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
