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
    /// Timeout for Azure Device Registry operations
    pub(crate) azure_device_registry_timeout: Duration,
    /// Timeout for Schema Registry operations
    pub(crate) schema_registry_timeout: Duration,
    /// Timeout for State Store operations
    pub(crate) state_store_timeout: Duration,
    /// Clients used to perform connector operations
    azure_device_registry_client: azure_device_registry::Client<SessionManagedClient>,
    pub(crate) state_store_client: Arc<state_store::Client<SessionManagedClient>>,
    schema_registry_client: schema_registry::Client<SessionManagedClient>,
}

#[allow(clippy::missing_fields_in_debug)]
impl std::fmt::Debug for ConnectorContext {
    fn fmt(&self, f: &mut std::fmt::Formatter<'_>) -> std::fmt::Result {
        f.debug_struct("ConnectorContext")
            .field("debounce_duration", &self.debounce_duration)
            .field(
                "azure_device_registry_timeout",
                &self.azure_device_registry_timeout,
            )
            .field("schema_registry_timeout", &self.schema_registry_timeout)
            .field("state_store_timeout", &self.state_store_timeout)
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
    ///
    /// # Errors
    /// Returns a String error if any of the setup fails, detailing the cause.
    pub fn new(
        application_context: ApplicationContext,
        connector_artifacts: ConnectorArtifacts,
    ) -> Result<Self, String> {
        // Create Session
        let mqtt_connection_settings = connector_artifacts
            .to_mqtt_connection_settings("0")
            .map_err(|e| e.to_string())?;
        let session_options = SessionOptionsBuilder::default()
            .connection_settings(mqtt_connection_settings)
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

        Ok(Self {
            connector_context: Arc::new(ConnectorContext {
                // TODO: These timeouts should come from somewhere, specifically, probably the artifacts.
                // These will need to be configured by the connector deployer, not the connector author,
                // so exposing them through API is not the correct solution.
                debounce_duration: Duration::from_secs(5),
                azure_device_registry_timeout: Duration::from_secs(10),
                // NOTE (2025-09-12): Schema Registry has an issue with scale causing throttling,
                // so this value has been set very high. This is probably not ideal.
                schema_registry_timeout: Duration::from_secs(90),
                state_store_timeout: Duration::from_secs(10),
                application_context,
                managed_client: session.create_managed_client(),
                connector_artifacts,
                azure_device_registry_client,
                schema_registry_client,
                state_store_client: Arc::new(state_store_client),
            }),
            session,
        })
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
}
