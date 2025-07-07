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
    AdrConfigError, Data,
    base_connector::{
        self, BaseConnector,
        managed_azure_device_registry::{
            AssetClient, ClientNotification, DatasetClient, DatasetNotification,
            DeviceEndpointClient, DeviceEndpointClientCreationObservation,
        },
    },
    data_processor::derived_json,
};
use azure_iot_operations_protocol::application::ApplicationContextBuilder;
use azure_iot_operations_services::azure_device_registry;

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

    let adr_discovery_client = base_connector.discovery_client();

    // Run the Session and the Azure Device Registry operations concurrently
    let r = tokio::join!(
        run_program(device_creation_observation),
        run_discovery(adr_discovery_client),
        base_connector.run(),
    );
    r.1?;
    r.2?;
    Ok(())
}

// This function runs in a loop, waiting for device creation notifications.
async fn run_program(mut device_creation_observation: DeviceEndpointClientCreationObservation) {
    // Wait for a device creation notification
    loop {
        let device_endpoint_client = device_creation_observation.recv_notification().await;
        log::info!("Device created: {device_endpoint_client:?}");

        // now we should update the status of the device
        let endpoint_status = generate_endpoint_status(&device_endpoint_client);

        if let Err(e) = device_endpoint_client
            .report_status(Ok(()), endpoint_status)
            .await
        {
            log::error!("Error reporting device endpoint status: {e}");
        }

        // Start handling the assets for this device endpoint
        // if we didn't accept the inbound endpoint, then we still want to run this to wait for updates
        tokio::task::spawn(run_device(device_endpoint_client));
    }
}

// This function runs in a loop, waiting for asset creation notifications.
async fn run_device(mut device_endpoint_client: DeviceEndpointClient) {
    loop {
        match device_endpoint_client.recv_notification().await {
            ClientNotification::Deleted => {
                log::warn!("Device Endpoint deleted");
                break;
            }
            ClientNotification::Updated => {
                log::info!("Device updated: {device_endpoint_client:?}");
                // now we should update the status of the device
                let endpoint_status = generate_endpoint_status(&device_endpoint_client);

                if let Err(e) = device_endpoint_client
                    .report_status(Ok(()), endpoint_status)
                    .await
                {
                    log::error!("Error reporting device endpoint status: {e}");
                }
            }
            ClientNotification::Created(asset_client) => {
                log::info!("Asset created: {asset_client:?}");

                // now we should update the status of the asset
                let asset_status = generate_asset_status(&asset_client);

                if let Err(e) = asset_client.report_status(asset_status).await {
                    log::error!("Error reporting asset status: {e}");
                }

                // Start handling the datasets for this asset
                // if we didn't accept the asset, then we still want to run this to wait for updates
                tokio::task::spawn(run_asset(asset_client));
            }
        }
    }
}

// This function runs in a loop, waiting for dataset creation notifications.
async fn run_asset(mut asset_client: AssetClient) {
    loop {
        match asset_client.recv_notification().await {
            ClientNotification::Updated => {
                log::info!("asset updated: {asset_client:?}");
                // now we should update the status of the asset
                let asset_status = generate_asset_status(&asset_client);

                if let Err(e) = asset_client.report_status(asset_status).await {
                    log::error!("Error reporting asset status: {e}");
                }
            }
            ClientNotification::Deleted => {
                log::warn!("Asset has been deleted");
                break;
            }
            ClientNotification::Created(dataset_client) => {
                log::info!("Dataset Created: {dataset_client:?}");
                tokio::task::spawn(run_dataset(dataset_client));
            }
        }
    }
}

async fn run_dataset(mut dataset_client: DatasetClient) {
    // now we should update the status of the dataset and report the message schema
    if let Err(e) = dataset_client.report_status(Ok(())).await {
        log::error!("Error reporting dataset status: {e}");
    }

    let sample_data = mock_received_data(0);

    let message_schema = derived_json::create_schema(&sample_data).unwrap();
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
    let mut dataset_valid = true;
    loop {
        tokio::select! {
            biased;
            // Listen for a dataset update notifications
            res = dataset_client.recv_notification() => {
                match res {
                    DatasetNotification::Updated => {
                        log::info!("dataset updated: {dataset_client:?}");
                        // now we should update the status of the dataset and report the message schema
                        if let Err(e) = dataset_client.report_status(Ok(())).await {
                            log::error!("Error reporting dataset status: {e}");
                        }

                        let sample_data = mock_received_data(0);

                        let message_schema =
                            derived_json::create_schema(&sample_data).unwrap();
                        match dataset_client.report_message_schema(message_schema).await {
                            Ok(message_schema_reference) => {
                                log::info!("Message Schema reported, reference returned: {message_schema_reference:?}");
                            }
                            Err(e) => {
                                log::error!("Error reporting message schema: {e}");
                            }
                        }
                        dataset_valid = true;
                    },
                    DatasetNotification::UpdatedInvalid => {
                        log::warn!("Dataset has invalid update. Wait for new dataset update.");
                        dataset_valid = false;
                    },
                    DatasetNotification::Deleted => {
                        log::warn!("Dataset has been deleted. No more dataset updates will be received");
                        break;
                    }
                }
            },
            _ = timer.tick(), if dataset_valid => {
                let sample_data = mock_received_data(count);
                match dataset_client.forward_data(sample_data).await {
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
