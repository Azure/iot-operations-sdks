// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

//! Types for Azure IoT Operations Connectors.

use std::{sync::Arc, time::Duration};

use azure_iot_operations_mqtt::session::{
    Session, SessionError, SessionManagedClient, SessionOptionsBuilder,
    reconnect_policy::ExponentialBackoffWithJitter, reconnect_policy::ReconnectPolicy,
};
use azure_iot_operations_protocol::application::ApplicationContext;
use azure_iot_operations_services::{
    azure_device_registry::{self, health_reporter::ReportInterval},
    schema_registry, state_store,
};
use derive_builder::Builder;
use managed_azure_device_registry::DeviceEndpointClientCreationObservation;
use thiserror::Error;
use tokio::sync::mpsc;

use crate::{deployment_artifacts::connector::ConnectorArtifacts, readiness_probe::ReadinessProbe};

pub mod adr_discovery;
pub mod managed_azure_device_registry;

/// Error describing why a [`BaseConnector`] run ended
#[derive(Debug, Error)]
#[error(transparent)]
pub struct ConnectorError(#[from] ConnectorErrorRepr);

#[derive(Debug, Error)]
pub(crate) enum ConnectorErrorRepr {
    #[error("Session error: {0}")]
    Session(#[from] SessionError),
    #[error("Unrecoverable error: {0}")]
    Unrecoverable(String),
}

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
    /// Health status reporting interval
    pub(crate) health_report_interval: ReportInterval,
    /// Clients used to perform connector operations
    azure_device_registry_client: azure_device_registry::Client,
    pub(crate) state_store_client: Arc<state_store::Client>,
    schema_registry_client: schema_registry::Client,
    /// Channel for signaling that the connector requires a restart
    pub(crate) connector_restart_tx: mpsc::Sender<String>,
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

/// Options for configuring a new [`BaseConnector`]
#[derive(Builder)]
#[builder(pattern = "owned")] // Keep for when we have more options like reconnect policy
pub struct Options {
    // Timeouts for underlying service operations
    /// Timeout for Azure Device Registry operations
    #[builder(default = "Duration::from_secs(10)")]
    azure_device_registry_timeout: Duration,
    // NOTE (2025-09-12): Schema Registry has an issue with scale causing throttling,
    // so this value has been set very high. This is probably not ideal.
    /// Timeout for Schema Registry operations
    #[builder(default = "Duration::from_secs(90)")]
    schema_registry_timeout: Duration,
    /// Timeout for State Store operations
    #[builder(default = "Duration::from_secs(10)")]
    state_store_timeout: Duration,

    /// Health Status reporting interval
    #[builder(default = "ReportInterval::default()")]
    health_report_interval: ReportInterval,

    /// Debounce duration for filemount operations for the connector
    #[builder(default = "Duration::from_secs(5)")]
    filemount_debounce_duration: Duration,

    /// Reconnect policy used by the MQTT Session.
    #[builder(default = "Box::new(ExponentialBackoffWithJitter::default())")]
    reconnect_policy: Box<dyn ReconnectPolicy>,

    /// Optional readiness probe implementation to use for the connector.
    #[builder(default = "None")]
    readiness_probe: Option<Box<dyn ReadinessProbe>>,
}

/// Base Connector for Azure IoT Operations
pub struct BaseConnector {
    connector_context: Arc<ConnectorContext>,
    session: Session,
    connector_restart_rx: mpsc::Receiver<String>,
    readiness_probe: Option<Box<dyn ReadinessProbe>>,
}

impl BaseConnector {
    /// Creates a new [`BaseConnector`] and all required clients/etc needed to run connector operations.
    ///
    /// # Errors
    /// Returns a String error if any of the setup fails, detailing the cause.
    #[allow(clippy::needless_pass_by_value)]
    pub fn new(
        application_context: ApplicationContext,
        connector_artifacts: ConnectorArtifacts,
        base_connector_options: Options,
    ) -> Result<Self, String> {
        // Create Session
        let mqtt_connection_settings = connector_artifacts
            .to_mqtt_connection_settings("0")
            .map_err(|e| e.clone())?;
        let session_options = SessionOptionsBuilder::default()
            .connection_settings(mqtt_connection_settings)
            .reconnect_policy(base_connector_options.reconnect_policy)
            .build()
            .map_err(|e| e.to_string())?;
        let session = Session::new(session_options).map_err(|e| e.to_string())?;

        let (connector_restart_tx, connector_restart_rx) = mpsc::channel(1);

        // Create clients
        // Create Azure Device Registry Client
        let azure_device_registry_client = azure_device_registry::Client::new(
            application_context.clone(),
            session.create_managed_client(),
            azure_device_registry::ClientOptionsBuilder::default()
                .build()
                .map_err(|e| e.to_string())?,
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
            session.create_session_monitor(),
            state_store::ClientOptionsBuilder::default()
                .build()
                .map_err(|e| e.to_string())?,
        )
        .map_err(|e| e.to_string())?;

        Ok(Self {
            connector_context: Arc::new(ConnectorContext {
                debounce_duration: base_connector_options.filemount_debounce_duration,
                azure_device_registry_timeout: base_connector_options.azure_device_registry_timeout,
                schema_registry_timeout: base_connector_options.schema_registry_timeout,
                state_store_timeout: base_connector_options.state_store_timeout,
                health_report_interval: base_connector_options.health_report_interval,
                application_context,
                managed_client: session.create_managed_client(),
                connector_artifacts,
                azure_device_registry_client,
                schema_registry_client,
                state_store_client: Arc::new(state_store_client),
                connector_restart_tx,
            }),
            session,
            connector_restart_rx,
            readiness_probe: base_connector_options.readiness_probe,
        })
    }

    /// Runs the MQTT Session that allows all Connector Operations to be performed.
    /// Returns if the session ends. If this happens, the base connector will need to be recreated
    ///
    /// # Errors
    /// Returns a [`ConnectorError`] if the session encounters a fatal error and ends, or if
    /// the connector encounters an error that requires a restart.
    ///
    /// # Panics
    /// Panics if the restart channel is closed, which should never happen since the [`BaseConnector`]
    /// itself holds the sender side of the channel.
    pub async fn run(mut self) -> Result<(), ConnectorError> {
        if let Some(readiness_probe) = self.readiness_probe {
            let session_monitor = self.session.create_session_monitor();

            tokio::task::spawn(async move {
                loop {
                    session_monitor.connected().await;
                    readiness_probe.set_ready();
                    session_monitor.disconnected().await;
                    readiness_probe.set_not_ready();
                }
            });
        }

        tokio::select! {
            session_result = self.session.run() => {
                session_result.map_err(|e| ConnectorError::from(ConnectorErrorRepr::from(e)))
            }
            restart_reason = self.connector_restart_rx.recv() => {
                Err(ConnectorErrorRepr::Unrecoverable(restart_reason.expect("Base connector holds sender, so this should never fail")).into())
            }
        }
    }

    /// Creates a new [`DeviceEndpointClientCreationObservation`] to allow for Azure Device Registry operations
    ///
    /// # Errors
    /// Returns a `String` error if the underlying file mount observation cannot be created.
    pub fn create_device_endpoint_client_create_observation(
        &self,
    ) -> Result<DeviceEndpointClientCreationObservation, String> {
        DeviceEndpointClientCreationObservation::new(self.connector_context.clone())
    }

    /// Creates a handle to use the [`BaseConnector`]'s Azure Device Registry client for discovery operations.
    pub fn discovery_client(&self) -> adr_discovery::Client {
        adr_discovery::Client::new(self.connector_context.clone())
    }
}
