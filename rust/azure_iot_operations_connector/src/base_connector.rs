// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

//! Types for Azure IoT Operations Connectors.

use std::time::Duration;

use azure_iot_operations_mqtt::session::{Session, SessionManagedClient, SessionOptionsBuilder};
use azure_iot_operations_protocol::application::ApplicationContext;
use azure_iot_operations_services::azure_device_registry;

use crate::filemount::connector_config::ConnectorConfiguration;

pub struct ConnectorContext {
    application_context: ApplicationContext,
    connector_config: ConnectorConfiguration,
    azure_device_registry_client: azure_device_registry::Client<SessionManagedClient>,
}

pub async fn initialize_and_start(application_context: ApplicationContext) {
    // if any of these operations fail, wait and try again in case connector configuration has changed
    let (base_connector, session) =
        operation_with_retries::<(ConnectorContext, Session), String>(|| {
            // Get Connector Configuration
            let connector_config =
                ConnectorConfiguration::new_from_deployment().map_err(|e| e.to_string())?;

            // Create Session
            let mqtt_connection_settings = connector_config
                .clone()
                .to_mqtt_connection_settings("0")
                .map_err(|e| e.to_string())?;
            let session_options = SessionOptionsBuilder::default()
                .connection_settings(mqtt_connection_settings.clone())
                // TODO: reconnect policy
                // TODO: outgoing_max
                .build()
                .map_err(|e| e.to_string())?;
            let session = Session::new(session_options).map_err(|e| e.to_string())?;

            // Create clients
            // Create Azure Device Registry Client
            let azure_device_registry_client = azure_device_registry::Client::new(
                application_context.clone(),
                session.create_managed_client(),
                azure_device_registry::ClientOptions::default(),
            ).map_err(|e| e.to_string())?;

            Ok((
                ConnectorContext {
                    application_context: application_context.clone(),
                    connector_config,
                    azure_device_registry_client
                },
                session,
            ))
        });

    // Run the Session and Connector Operations
    // TODO: make this a part of operation_with_retries to restart the connector if anything fails?
    tokio::try_join!(
        connector_tasks(base_connector),
        async move { session.run().await.map_err(|e| e.to_string()) },
    )
    .unwrap(); // TODO: no unwrap
}

#[allow(clippy::unused_async)]
async fn connector_tasks(connector_context: ConnectorContext) -> Result<(), String> {
    log::info!("Starting connector tasks with context: {:?}", connector_context.connector_config);
    Ok(())
}

fn operation_with_retries<T, E: std::fmt::Debug>(operation: impl Fn() -> Result<T, E>) -> T {
    let mut retry_duration = Duration::from_secs(1);
    loop {
        match operation() {
            Ok(result) => return result,
            Err(e) => {
                log::error!("Operation failed, retrying: {:?}", e);
                retry_duration = retry_duration.saturating_mul(2);
                std::thread::sleep(retry_duration);
            }
        }
    }
}
