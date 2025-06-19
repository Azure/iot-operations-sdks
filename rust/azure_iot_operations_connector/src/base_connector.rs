// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

//! Types for Azure IoT Operations Connectors.

use std::{sync::Arc, time::Duration};

use azure_iot_operations_mqtt::session::{
    Session, SessionError, SessionManagedClient, SessionOptionsBuilder,
};
use azure_iot_operations_protocol::application::ApplicationContext;
use azure_iot_operations_services::{azure_device_registry, schema_registry, state_store};
use managed_azure_device_registry::DeviceEndpointClientCreationObservation;

use crate::deployment_artifacts::connector::ConnectorArtifacts;

pub mod adr_discovery;
pub mod managed_azure_device_registry;

/// Context required to run the base connector operations
pub(crate) struct ConnectorContext {
    /// Application context used for creating new clients and envoys
    pub(crate) application_context: ApplicationContext,
    /// Used to create new envoys
    pub(crate) managed_client: SessionManagedClient,
    /// Connector artifacts if needed by any dependent operations
    connector_artifacts: ConnectorArtifacts,
    /// Debounce duration for filemount operations for the connector
    debounce_duration: Duration,
    /// Default timeout for connector operations
    pub(crate) default_timeout: Duration,
    /// Clients used to perform connector operations
    azure_device_registry_client: azure_device_registry::Client<SessionManagedClient>,
    pub(crate) state_store_client: Arc<state_store::Client<SessionManagedClient>>,
    schema_registry_client: schema_registry::Client<SessionManagedClient>,
}

#[allow(clippy::missing_fields_in_debug)]
impl std::fmt::Debug for ConnectorContext {
    fn fmt(&self, f: &mut std::fmt::Formatter<'_>) -> std::fmt::Result {
        f.debug_struct("ConnectorContext")
            .field("connector_artifacts", &self.connector_artifacts)
            .field("debounce_duration", &self.debounce_duration)
            .field("default_timeout", &self.default_timeout)
            .finish()
    }
}

/// Base Connector for Azure IoT Operations
pub struct BaseConnector {
    connector_context: Arc<ConnectorContext>,
    session: Session,
}

impl BaseConnector {
    /// Creates a new [`BaseConnector`] and all required clients/etc needed to run connector operations.
    /// On any failures, will log the error and then retry getting the connector configuration
    /// with exponential backoff. This allows for new configuration to be deployed to fix any
    /// errors without needing to restart the connector.
    #[must_use]
    pub fn new(application_context: ApplicationContext) -> Self {
        // if any of these operations fail, wait and try again in case connector configuration has changed
        let (
            connector_artifacts,
            azure_device_registry_client,
            schema_registry_client,
            state_store_client,
            session,
        ) = operation_with_retries::<(_, _, _, _, _), String>(|| {
            // Get Connector Artifacts
            let connector_artifacts =
                ConnectorArtifacts::new_from_deployment().map_err(|e| e.to_string())?;

            // Create Session
            let mqtt_connection_settings = connector_artifacts
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
            )
            .map_err(|e| e.to_string())?;

            // Create Schema Registry Client
            let schema_registry_client = schema_registry::Client::new(
                application_context.clone(),
                &session.create_managed_client(),
            );

            // Create State Store Client
            let state_store_client = state_store::Client::new(
                application_context.clone(),
                session.create_managed_client(),
                session.create_connection_monitor(),
                state_store::ClientOptionsBuilder::default()
                    .build()
                    .map_err(|e| e.to_string())?,
            )
            .map_err(|e| e.to_string())?;

            Ok((
                connector_artifacts,
                azure_device_registry_client,
                schema_registry_client,
                state_store_client,
                session,
            ))
        });
        Self {
            connector_context: Arc::new(ConnectorContext {
                // TODO: validate these timeouts here once they come from somewhere
                debounce_duration: Duration::from_secs(5), // TODO: come from somewhere
                default_timeout: Duration::from_secs(10),  // TODO: come from somewhere
                application_context,
                managed_client: session.create_managed_client(),
                connector_artifacts,
                azure_device_registry_client,
                schema_registry_client,
                state_store_client: Arc::new(state_store_client),
            }),
            session,
        }
    }

    /// Runs the MQTT Session that allows all Connector Operations to be performed
    /// Returns if the session ends. If this happens, the base connector will need to be recreated
    ///
    /// # Errors
    /// Returns a [`SessionError`] if the session encounters a fatal error and ends.
    pub async fn run(self) -> Result<(), SessionError> {
        // Run the Session and Connector Operations
        // TODO: make this a part of operation_with_retries to restart the connector if anything fails?
        self.session.run().await
    }

    /// Creates a new [`DeviceEndpointClientCreationObservation`] to allow for all Azure Device Registry operations
    pub fn create_device_endpoint_client_create_observation(
        &self,
    ) -> DeviceEndpointClientCreationObservation {
        DeviceEndpointClientCreationObservation::new(self.connector_context.clone())
    }

    /// Creates a handle to use the [`BaseConnector`]'s Azure Device Registry client for discovery operations.
    pub fn discovery_client(&self) -> adr_discovery::Client {
        adr_discovery::Client::new(self.connector_context.clone())
    }

    /// Returns a copy of the connector artifacts
    pub fn connector_artifacts(&self) -> ConnectorArtifacts {
        self.connector_context.connector_artifacts.clone()
    }
}

/// Helper function to perform any operation with retries.
fn operation_with_retries<T, E: std::fmt::Debug>(operation: impl Fn() -> Result<T, E>) -> T {
    let mut retry_duration = Duration::from_secs(1);
    loop {
        match operation() {
            Ok(result) => return result,
            Err(e) => {
                log::error!("Operation failed, retrying: {e:?}");
                retry_duration = retry_duration.saturating_mul(2);
                std::thread::sleep(retry_duration);
            }
        }
    }
}
