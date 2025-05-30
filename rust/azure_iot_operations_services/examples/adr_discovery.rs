// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

use std::{collections::HashMap, time::Duration};

use azure_iot_operations_mqtt::{
    MqttConnectionSettingsBuilder,
    session::{Session, SessionExitHandle, SessionManagedClient, SessionOptionsBuilder},
};
use azure_iot_operations_protocol::application::ApplicationContextBuilder;
use azure_iot_operations_services::azure_device_registry::{self, models};

use env_logger::Builder;

const HOSTNAME: &str = "localhost";
const PORT: u16 = 1883;

#[tokio::main(flavor = "current_thread")]
async fn main() -> Result<(), Box<dyn std::error::Error>> {
    Builder::new()
        .filter_level(log::LevelFilter::Warn)
        .format_timestamp(None)
        .filter_module("rumqttc", log::LevelFilter::Warn)
        .init();

    // Create a Session
    let connection_settings = MqttConnectionSettingsBuilder::default()
        .client_id("adr-client-app")
        .hostname(HOSTNAME)
        .tcp_port(PORT)
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
    let results = tokio::join!(
        run_program(azure_device_registry_client, session.create_exit_handle()),
        session.run()
    );
    // Return errors or the program result
    results.1?;
    results.0
}

async fn run_program(
    client: azure_device_registry::Client<SessionManagedClient>,
    exit_handle: SessionExitHandle,
) -> Result<(), Box<dyn std::error::Error>> {
    let r = do_discovery(client).await;
    match exit_handle.try_exit().await {
        Ok(()) => {
            println!("Session exit requested successfully.");
        }
        Err(e) => {
            eprintln!("Error requesting session exit: {e}");
        }
    }
    r
}

/// Perform ADR discovery operations.
async fn do_discovery(
    client: azure_device_registry::Client<SessionManagedClient>,
) -> Result<(), Box<dyn std::error::Error>> {
    let device_name = "my-device-name".to_string();

    let discovered_inbound_endpoints = HashMap::from([(
        "inbound_endpoint1".to_string(),
        models::DiscoveredInboundEndpoint {
            address: "tcp://inbound/endpoint1".to_string(),
            endpoint_type: "myEndpointType".to_string(),
            supported_authentication_methods: vec![],
            version: Some("1.0.0".to_string()),
            additional_configuration: None,
        },
    )]);
    let discovered_outbound_assigned_endpoints = HashMap::from([
        (
            "outbound_endpoint".to_string(),
            models::OutboundEndpoint {
                address: "tcp://outbound/endpoint".to_string(),
                endpoint_type: Some("myEndpointType".to_string()),
            },
        ),
        (
            "another_outbound_endpoint".to_string(),
            models::OutboundEndpoint {
                address: "tcp://another/outbound/endpoint".to_string(),
                endpoint_type: Some("anotherEndpointType".to_string()),
            },
        ),
    ]);
    let device_spec = models::DiscoveredDeviceSpecification {
        attributes: HashMap::default(),
        endpoints: Some(models::DiscoveredDeviceEndpoints {
            inbound: discovered_inbound_endpoints,
            outbound: Some(models::DiscoveredOutboundEndpoints {
                assigned: discovered_outbound_assigned_endpoints,
            }),
        }),
        external_device_id: Some("my-device-id".to_string()),
        manufacturer: Some("Contoso".to_string()),
        model: Some("Device Model".to_string()),
        operating_system: Some("MyOS".to_string()),
        operating_system_version: Some("1.0.0".to_string()),
    };

    match client
        .create_or_update_discovered_device(
            device_name,
            device_spec,
            "myEndpointType".to_string(),
            Duration::from_secs(10),
        )
        .await
    {
        Ok(response) => {
            println!("Discovered device created or updated successfully: {response:?}");
            Ok(())
        }
        Err(e) => {
            println!("Error creating or updating discovered device");
            Err(Box::new(e))
        }
    }
}
