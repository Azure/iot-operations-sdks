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

use azure_iot_operations_connector::{
    base_connector::{
        BaseConnector, managed_azure_device_registry::DeviceEndpointClientCreationObservation,
    },
    data_transformer::PassthroughDataTransformer,
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
        .filter_module("notify_debouncer_full", log::LevelFilter::Off)
        .filter_module("notify::inotify", log::LevelFilter::Off)
        .init();

    // Create an ApplicationContext
    let application_context = ApplicationContextBuilder::default().build()?;

    let base_connector = BaseConnector::new(application_context, PassthroughDataTransformer {});

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
async fn run_program(
    mut device_creation_observation: DeviceEndpointClientCreationObservation<
        PassthroughDataTransformer,
    >,
) {
    // Wait for a device creation notification
    while let Some((
        mut device_endpoint_client,
        _device_endpoint_update_observation,
        _asset_creation_observation,
    )) = device_creation_observation.recv_notification().await
    {
        log::info!("Device created: {device_endpoint_client:?}");

        // now we should update the status of the device
        let endpoint_status = match device_endpoint_client
            .specification
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
                    device_endpoint_client.specification.endpoints.inbound.name,
                    unsupported_endpoint_type
                );
                Err(azure_device_registry::ConfigError {
                    message: Some("endpoint type is not supported".to_string()),
                    ..azure_device_registry::ConfigError::default()
                })
            }
        };

        device_endpoint_client
            .report_status(Ok(()), endpoint_status)
            .await;
    }
    panic!("device_creation_observer has been dropped");
}
