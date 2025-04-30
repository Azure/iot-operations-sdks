// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

//! Types for Azure IoT Operations Connectors.

use std::time::Duration;

use azure_iot_operations_mqtt::session::{Session, SessionManagedClient, SessionOptionsBuilder};
use azure_iot_operations_protocol::application::ApplicationContext;
use azure_iot_operations_services::azure_device_registry;
use managed_azure_device_registry::ManagedDeviceCreateObservation;

use crate::filemount::connector_config::ConnectorConfiguration;

pub mod managed_azure_device_registry;

/// Context required to run the base connector operations
#[derive(Clone)]
struct ConnectorContext {
    /// Application context used for creating new clients and envoys
    application_context: ApplicationContext,
    /// Connector configuration if needed by any dependent operations
    connector_config: ConnectorConfiguration,
    /// Debounce duration for filemount operations for the connector
    debounce_duration: Duration,
    /// Clients used to perform connector operations
    azure_device_registry_client: azure_device_registry::Client<SessionManagedClient>,
    // state_store_client: Arc<state_store::Client<SessionManagedClient>>,
    // schema_registry_client: schema_registry::Client<SessionManagedClient>,
    // etc
}

pub struct BaseConnector {
  connector_context: ConnectorContext,
  session: Session
}

impl BaseConnector {
  pub fn new(application_context: ApplicationContext) -> Self {
    // if any of these operations fail, wait and try again in case connector configuration has changed
    operation_with_retries::<Self, String>(|| {
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

        Ok(Self {
            connector_context: ConnectorContext {
              debounce_duration: Duration::from_secs(5), // TODO: come from somewhere
                application_context: application_context.clone(),
                connector_config,
                azure_device_registry_client
            },
            session,
          }
        )
    })
  }

  pub async fn run(self) {
    // Run the Session and Connector Operations
    // TODO: make this a part of operation_with_retries to restart the connector if anything fails?
    self.session.run().await.unwrap();
  }

  pub fn create_managed_azure_device_registry_client(&self) -> ManagedDeviceCreateObservation {
    ManagedDeviceCreateObservation::new(self.connector_context.clone())
  }
}


#[allow(clippy::unused_async)]
async fn connector_tasks(connector_context: ConnectorContext) -> Result<(), String> {
    log::info!("Starting connector tasks with context: {:?}", connector_context.connector_config);
    Ok(())
}

/// Helper function to perform any operation with retries.
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
