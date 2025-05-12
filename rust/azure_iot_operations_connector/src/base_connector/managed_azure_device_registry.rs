// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

//! Types for Azure IoT Operations Connectors.

use std::{collections::HashMap, sync::Arc};

use azure_iot_operations_mqtt::interface::AckToken;
use azure_iot_operations_services::azure_device_registry::{
    self, Asset, AssetUpdateObservation, ConfigError, Dataset, Device, DeviceUpdateObservation,
    MessageSchemaReference,
};
use tokio_retry2::{Retry, RetryError};

use crate::{
    Data, MessageSchema,
    base_connector::ConnectorContext,
    data_transformer::{DataTransformer, DatasetDataTransformer},
    destination_endpoint::Forwarder,
    filemount::azure_device_registry::{
        AssetCreateObservation, AssetDeletionToken, DeviceEndpointCreateObservation,
    },
};

/// Used as the strategy when using [`tokio_retry2::Retry`]
const RETRY_STRATEGY: tokio_retry2::strategy::ExponentialBackoff =
    tokio_retry2::strategy::ExponentialBackoff::from_millis(100);

/// An Observation for device endpoint creation events that uses
/// multiple underlying clients to get full information for a
/// [`ProtocolTranslator`] to use.
pub struct DeviceEndpointClientCreationObservation<T: DataTransformer> {
    connector_context: Arc<ConnectorContext<T>>,
    device_endpoint_create_observation: DeviceEndpointCreateObservation,
}
impl<T> DeviceEndpointClientCreationObservation<T>
where
    T: DataTransformer,
{
    /// Creates a new [`DeviceEndpointClientCreationObservation`] that uses the given [`ConnectorContext`]
    pub(crate) fn new(connector_context: Arc<ConnectorContext<T>>) -> Self {
        let device_endpoint_create_observation =
            DeviceEndpointCreateObservation::new(connector_context.debounce_duration).unwrap();

        Self {
            connector_context,
            device_endpoint_create_observation,
        }
    }

    /// Receives a notification for a newly created device endpoint or [`None`]
    /// if there will be no more notifications. This notification includes the
    /// [`DeviceEndpointClient`], a [`DeviceEndpointClientUpdateObservation`]
    /// to observe for updates on the new Device, and a [`AssetClientCreationObservation`]
    ///  to observe for newly created Assets related to this Device
    pub async fn recv_notification(
        &mut self,
    ) -> Option<(
        DeviceEndpointClient<T>,
        DeviceEndpointClientUpdateObservation<T>,
        /*DeviceDeleteToken,*/ AssetClientCreationObservation<T>,
    )> {
        loop {
            // Get the notification
            let (device_endpoint_ref, asset_create_observation) = self
                .device_endpoint_create_observation
                .recv_notification()
                .await?;

            // Turn AssetCreateObservation into an AssetClientCreationObservation
            let asset_client_creation_observation = AssetClientCreationObservation {
                asset_create_observation,
                connector_context: self.connector_context.clone(),
            };

            // and then get device update observation as well and turn it into a DeviceEndpointClientUpdateObservation
            let device_endpoint_client_update_observation =  match Retry::spawn(RETRY_STRATEGY, async || -> Result<DeviceUpdateObservation, RetryError<azure_device_registry::Error>> {
                self.connector_context
                    .azure_device_registry_client
                    .observe_device_update_notifications(
                        device_endpoint_ref.device_name.clone(),
                        device_endpoint_ref.inbound_endpoint_name.clone(),
                        self.connector_context.default_timeout,
                    )
                    // retry on network errors, otherwise don't retry on config/dev errors
                    .await.map_err(observe_error_into_retry_error)
            }).await {
                Ok(device_update_observation) => {
                    DeviceEndpointClientUpdateObservation {
                        device_update_observation,
                        connector_context: self.connector_context.clone(),
                    }
                },
                Err(e) => {
                  log::error!("Failed to observe for device update notifications after retries: {e}");
                  log::error!("Dropping device endpoint create notification: {device_endpoint_ref:?}");
                  continue;
                },
            };

            // get the device definition
            let device = match Retry::spawn(
                RETRY_STRATEGY,
                async || -> Result<Device, RetryError<azure_device_registry::Error>> {
                    self.connector_context
                        .azure_device_registry_client
                        .get_device(
                            device_endpoint_ref.device_name.clone(),
                            device_endpoint_ref.inbound_endpoint_name.clone(),
                            self.connector_context.default_timeout,
                        )
                        .await
                        .map_err(|e| {
                            match e.kind() {
                                // network/retriable
                                azure_device_registry::ErrorKind::AIOProtocolError(_) => {
                                    RetryError::transient(e)
                                }
                                // indicates an error in the configuration, so we want to get a new notification instead of retrying this operation
                                azure_device_registry::ErrorKind::ServiceError(_) => {
                                    RetryError::permanent(e)
                                }
                                _ => {
                                    // InvalidRequestArgument shouldn't be possible since timeout is already validated
                                    // ValidationError, ObservationError, DuplicateObserve, and ShutdownError aren't possible for this fn to return
                                    unreachable!()
                                }
                            }
                        })
                },
            )
            .await
            {
                Ok(device) => device,
                Err(e) => {
                    log::error!("Failed to get Device definition after retries: {e}");
                    log::error!(
                        "Dropping device endpoint create notification: {device_endpoint_ref:?}"
                    );
                    // unobserve as cleanup
                    let _ = Retry::spawn(
                        RETRY_STRATEGY,
                        async || -> Result<(), RetryError<azure_device_registry::Error>> {
                            self.connector_context
                                .azure_device_registry_client
                                .unobserve_device_update_notifications(
                                    device_endpoint_ref.device_name.clone(),
                                    device_endpoint_ref.inbound_endpoint_name.clone(),
                                    self.connector_context.default_timeout,
                                )
                                // retry on network errors, otherwise don't retry on config/dev errors
                                .await
                                .map_err(observe_error_into_retry_error)
                        },
                    )
                    .await
                    .inspect_err(|e| {
                        log::error!(
                            "Failed to unobserve device update notifications after retries: {e}"
                        );
                    });
                    continue;
                }
            };

            // turn the device definition into a DeviceEndpointClient
            let device_endpoint_client = match DeviceEndpointClient::new(
                device,
                device_endpoint_ref.inbound_endpoint_name.clone(),
                self.connector_context.clone(),
            ) {
                Ok(managed_device) => managed_device,
                Err(e) => {
                    // the device definition didn't include the inbound_endpoint, so it likely no longer exists
                    // TODO: This won't be a possible failure point in the future once the service returns errors
                    log::error!("{e}");
                    log::error!(
                        "Dropping device endpoint create notification: {device_endpoint_ref:?}"
                    );
                    // unobserve
                    let _ = Retry::spawn(
                        RETRY_STRATEGY,
                        async || -> Result<(), RetryError<azure_device_registry::Error>> {
                            self.connector_context
                                .azure_device_registry_client
                                .unobserve_device_update_notifications(
                                    device_endpoint_ref.device_name.clone(),
                                    device_endpoint_ref.inbound_endpoint_name.clone(),
                                    self.connector_context.default_timeout,
                                )
                                // retry on network errors, otherwise don't retry on config/dev errors
                                .await
                                .map_err(observe_error_into_retry_error)
                        },
                    )
                    .await
                    .inspect_err(|e| {
                        log::error!(
                            "Failed to unobserve device update notifications after retries: {e}"
                        );
                    });
                    continue;
                }
            };

            return Some((
                device_endpoint_client,
                device_endpoint_client_update_observation,
                asset_client_creation_observation,
            ));
        }
    }
}

/// Azure Device Registry Device Endpoint that includes additional functionality to report status
pub struct DeviceEndpointClient<T: DataTransformer> {
    /// The 'name' Field.
    pub device_name: String,
    /// The 'endpointName' Field.
    pub inbound_endpoint_name: String, // needed for easy status reporting?
    /// The 'specification' Field.
    pub specification: DeviceSpecification,
    /// The 'status' Field.
    pub status: Option<DeviceEndpointStatus>,
    connector_context: Arc<ConnectorContext<T>>,
}
impl<T> DeviceEndpointClient<T>
where
    T: DataTransformer,
{
    pub(crate) fn new(
        device: azure_device_registry::Device,
        inbound_endpoint_name: String,
        connector_context: Arc<ConnectorContext<T>>,
        // TODO: This won't need to return an error once the service properly sends errors if the endpoint doesn't exist
    ) -> Result<Self, String> {
        Ok(DeviceEndpointClient {
            device_name: device.name,
            // TODO: get device_endpoint_credentials_mount_path from connector config
            specification: DeviceSpecification::new(
                device.specification,
                "/etc/akri/secrets/device_endpoint_auth",
                &inbound_endpoint_name,
            )?,
            status: device.status.map(|recvd_status| {
                DeviceEndpointStatus::new(recvd_status, &inbound_endpoint_name)
            }),
            inbound_endpoint_name,
            connector_context,
        })
    }

    /// Used to report the status of a device and endpoint together,
    /// and then updates the [`Device`] with the new status returned
    pub async fn report_status(
        &mut self,
        device_status: Result<(), ConfigError>,
        endpoint_status: Result<(), ConfigError>,
    ) {
        // Create status
        let status = azure_device_registry::DeviceStatus {
            config: Some(azure_device_registry::StatusConfig {
                version: self.specification.version,
                error: device_status.err(),
                last_transition_time: None, // this field will be removed, so we don't need to worry about it for now
            }),
            // inserts the inbound endpoint name with None if there's no error, or Some(ConfigError) if there is
            endpoints: HashMap::from([(self.inbound_endpoint_name.clone(), endpoint_status.err())]),
        };

        // send status update to the service
        self.internal_report_status(status).await;
    }

    /// Used to report the status of just the device,
    /// and then updates the [`Device`] with the new status returned
    pub async fn report_device_status(&mut self, device_status: Result<(), ConfigError>) {
        // Create status with empty endpoint status
        let status = azure_device_registry::DeviceStatus {
            config: Some(azure_device_registry::StatusConfig {
                version: self.specification.version,
                error: device_status.err(),
                last_transition_time: None, // this field will be removed, so we don't need to worry about it for now
            }),
            // inserts the inbound endpoint name with None if there's no error, or Some(ConfigError) if there is
            endpoints: HashMap::new(),
        };

        // send status update to the service
        self.internal_report_status(status).await;
    }

    /// Used to report the status of just the endpoint,
    /// and then updates the [`Device`] with the new status returned
    pub async fn report_endpoint_status(&mut self, endpoint_status: Result<(), ConfigError>) {
        // Create status with empty device status
        let status = azure_device_registry::DeviceStatus {
            config: None,
            // inserts the inbound endpoint name with None if there's no error, or Some(ConfigError) if there is
            endpoints: HashMap::from([(self.inbound_endpoint_name.clone(), endpoint_status.err())]),
        };

        // send status update to the service
        self.internal_report_status(status).await;
    }

    /// Reports an already built status to the service, with retries, and then updates the device with the new status returned
    async fn internal_report_status(
        &mut self,
        adr_device_status: azure_device_registry::DeviceStatus,
    ) {
        // send status update to the service
        match Retry::spawn(
            RETRY_STRATEGY,
            async || -> Result<Device, RetryError<azure_device_registry::Error>> {
                self.connector_context
                    .azure_device_registry_client
                    .update_device_plus_endpoint_status(
                        self.device_name.clone(),
                        self.inbound_endpoint_name.clone(),
                        adr_device_status.clone(),
                        self.connector_context.default_timeout,
                    )
                    .await
                    .map_err(|e| {
                        match e.kind() {
                            // network/retriable
                            azure_device_registry::ErrorKind::AIOProtocolError(_) => {
                                RetryError::transient(e)
                            }
                            // indicates an error in the configuration, might be transient in the future depending on what it can indicate
                            azure_device_registry::ErrorKind::ServiceError(_) => {
                                RetryError::permanent(e)
                            }
                            _ => {
                                // InvalidRequestArgument shouldn't be possible since timeout is already validated
                                // ValidationError, ObservationError, DuplicateObserve, and ShutdownError aren't possible for this fn to return
                                unreachable!()
                            }
                        }
                    })
            },
        )
        .await
        {
            Ok(updated_device) => {
                // update self with new returned status
                self.status = updated_device.status.map(|recvd_status| {
                    DeviceEndpointStatus::new(recvd_status, &self.inbound_endpoint_name)
                });
                // NOTE: There may be updates present on the device specification, but even if that is the case,
                // we won't update them here and instead wait for the device update notification (finding out
                // first here is a race condition, the update will always be received imminently)
            }
            Err(e) => {
                // TODO: return an error for this scenario? Largely shouldn't be possible
                log::error!("Failed to Update Device Status: {e}");
            }
        };
    }
}

/// needed otherwise the compiler complains about T not being debug even though it doesn't need to be
#[allow(clippy::missing_fields_in_debug)]
impl<T> std::fmt::Debug for DeviceEndpointClient<T>
where
    T: DataTransformer,
{
    fn fmt(&self, f: &mut std::fmt::Formatter<'_>) -> std::fmt::Result {
        f.debug_struct("DeviceEndpointClient")
            .field("device_name", &self.device_name)
            .field("inbound_endpoint_name", &self.inbound_endpoint_name)
            .field("specification", &self.specification)
            .field("status", &self.status)
            .field("connector_context", &self.connector_context)
            .finish()
    }
}

/// An Observation for device endpoint update events that uses
/// multiple underlying clients to get full information for a
/// [`ProtocolTranslator`] to use.
/// TODO: maybe move this to be on the [`DeviceEndpointClient`]?
#[allow(dead_code)]
pub struct DeviceEndpointClientUpdateObservation<T: DataTransformer> {
    device_update_observation: DeviceUpdateObservation,
    connector_context: Arc<ConnectorContext<T>>,
}
impl<T> DeviceEndpointClientUpdateObservation<T>
where
    T: DataTransformer,
{
    /// Receives an updated [`DeviceEndpointClient`] or [`None`] if there will be no more notifications.
    ///
    /// If there are notifications:
    /// - Returns Some([`DeviceEndpointClient`], [`Option<AckToken>`]) on success
    ///     - If auto ack is disabled, the [`AckToken`] should be used or dropped when you want the ack to occur. If auto ack is enabled, you may use ([`DeviceEndpointClient`], _) to ignore the [`AckToken`].
    ///
    /// A received notification can be acknowledged via the [`AckToken`] by calling [`AckToken::ack`] or dropping the [`AckToken`].
    #[allow(clippy::unused_async)]
    pub async fn recv_notification(&self) -> Option<(DeviceEndpointClient<T>, Option<AckToken>)> {
        // handle the notification
        // convert into DeviceEndpointClient
        None
    }
}

/// An Observation for asset creation events that uses
/// multiple underlying clients to get full information for a
/// [`ProtocolTranslator`] to use.
#[allow(dead_code)]
pub struct AssetClientCreationObservation<T: DataTransformer> {
    asset_create_observation: AssetCreateObservation,
    connector_context: Arc<ConnectorContext<T>>,
}
impl<T> AssetClientCreationObservation<T>
where
    T: DataTransformer,
{
    /// Receives a notification for a newly created asset or [`None`] if there
    /// will be no more notifications. This notification includes the [`AssetClient`],
    /// a [`AssetClientUpdateObservation`] to observe for updates on the new Asset,
    /// and a [`AssetDeletionToken`] to observe for deletion of this Asset
    #[allow(clippy::unused_async)]
    pub async fn recv_notification(
        &self,
    ) -> Option<(
        AssetClient<T>,
        AssetClientUpdateObservation<T>,
        AssetDeletionToken,
    )> {
        // handle the notification
        // add asset update observation
        // create copy of asset with asset and dataset connector functionality (status reporting, data forwarding, create data transformers)
        None
    }
}

/// An Observation for asset update events that uses
/// multiple underlying clients to get full information for a
/// [`ProtocolTranslator`] to use.
pub struct AssetClientUpdateObservation<T: DataTransformer> {
    _asset_update_observation: AssetUpdateObservation,
    _data_transformer: Arc<T>,
}
impl<T> AssetClientUpdateObservation<T>
where
    T: DataTransformer,
{
    /// Receives an updated [`AssetClient`] or [`None`] if there will be no more notifications.
    ///
    /// If there are notifications:
    /// - Returns Some([`AssetClient`], [`Option<AckToken>`]) on success
    ///     - If auto ack is disabled, the [`AckToken`] should be used or dropped when you want the ack to occur. If auto ack is enabled, you may use ([`AssetClient`], _) to ignore the [`AckToken`].
    ///
    /// A received notification can be acknowledged via the [`AckToken`] by calling [`AckToken::ack`] or dropping the [`AckToken`].
    #[allow(clippy::unused_async)]
    pub async fn recv_notification(&self) -> Option<(AssetClient<T>, Option<AckToken>)> {
        // handle the notification
        None
    }
}

/// Azure Device Registry Asset that includes additional functionality
/// to report status, translate data, and send data to the destination
#[allow(dead_code)]
pub struct AssetClient<T: DataTransformer> {
    // re-export of adr::Asset, but Dataset/Event/etc structs are of type ConnectorDataset/etc
    /// Asset Definition
    pub asset_definition: Asset,
    data_transformer: Arc<T>,
    /// datasets associated with the asset. Will be part of [`AssetClient`] struct in future, but for now this creates the right dependencies
    pub datasets: Vec<DatasetClient<T>>,
}
impl<T> AssetClient<T>
where
    T: DataTransformer,
{
    /// Used to report the status of an Asset
    pub fn report_status(_status: Result<(), ConfigError>) {}
}

/// Azure Device Registry Dataset that includes additional functionality
/// to report status, translate data, and send data to the destination
pub struct DatasetClient<T: DataTransformer> {
    /// Dataset Definition
    pub dataset_definition: Dataset,
    dataset_data_transformer: T::MyDatasetDataTransformer,
    reporter: Arc<Reporter>,
}
#[allow(dead_code)]
impl<T> DatasetClient<T>
where
    T: DataTransformer,
{
    pub(crate) fn new(dataset_definition: Dataset, data_transformer: &T) -> Self {
        // Create a new dataset
        let forwarder = Forwarder::new(dataset_definition.clone());
        let reporter = Arc::new(Reporter::new(dataset_definition.clone()));
        let dataset_data_transformer = data_transformer.new_dataset_data_transformer(
            dataset_definition.clone(),
            forwarder,
            reporter.clone(),
        );
        Self {
            dataset_definition,
            dataset_data_transformer,
            reporter,
        }
    }
    /// Used to report the status and/or [`MessageSchema`] of an dataset
    /// # Errors
    /// TODO
    pub fn report_status(
        &self,
        status: Result<Option<MessageSchema>, ConfigError>,
    ) -> Result<Option<MessageSchemaReference>, String> {
        // Report the status of the dataset
        self.reporter.report_status(status)
    }

    /// Used to send sampled data to the [`DataTransformer`], which will then send
    /// the transformed data to the destination
    /// # Errors
    /// TODO
    pub async fn add_sampled_data(&self, data: Data) -> Result<(), String> {
        // Add sampled data to the dataset
        self.dataset_data_transformer.add_sampled_data(data).await
    }
}

/// Convenience struct to manage reporting the status of a dataset
pub struct Reporter {
    message_schema_uri: Option<MessageSchemaReference>,
    _message_schema: Option<MessageSchema>,
}
#[allow(dead_code)]
impl Reporter {
    pub(crate) fn new(_dataset_definition: Dataset) -> Self {
        // Create a new forwarder
        Self {
            message_schema_uri: None,
            _message_schema: None,
        }
    }
    /// Used to report the status and/or [`MessageSchema`] of an dataset
    /// # Errors
    /// TODO
    pub fn report_status(
        &self,
        _status: Result<Option<MessageSchema>, ConfigError>,
    ) -> Result<Option<MessageSchemaReference>, String> {
        // Report the status of the dataset
        Ok(None)
    }

    /// Returns the current message schema URI
    #[must_use]
    pub fn get_current_message_schema_uri(&self) -> Option<MessageSchemaReference> {
        // Get the current message schema URI
        self.message_schema_uri.clone()
    }
}

// Client Structs
// Device

#[derive(Debug, Clone)]
/// Represents the specification of a device in the Azure Device Registry service.
pub struct DeviceSpecification {
    /// The 'attributes' Field.
    pub attributes: HashMap<String, String>,
    /// The 'discoveredDeviceRef' Field.
    pub discovered_device_ref: Option<String>,
    /// The 'enabled' Field.
    pub enabled: Option<bool>,
    /// The 'endpoints' Field.
    pub endpoints: DeviceEndpoints, // different from adr
    /// The 'externalDeviceId' Field.
    pub external_device_id: Option<String>,
    /// The 'lastTransitionTime' Field.
    pub last_transition_time: Option<String>, // TODO DateTime?
    /// The 'manufacturer' Field.
    pub manufacturer: Option<String>,
    /// The 'model' Field.
    pub model: Option<String>,
    /// The 'operatingSystem' Field.
    pub operating_system: Option<String>,
    /// The 'operatingSystemVersion' Field.
    pub operating_system_version: Option<String>,
    /// The 'uuid' Field.
    pub uuid: Option<String>,
    /// The 'version' Field.
    pub version: Option<u64>,
}

impl DeviceSpecification {
    pub(crate) fn new(
        device_specification: azure_device_registry::DeviceSpecification,
        device_endpoint_credentials_mount_path: &str,
        inbound_endpoint_name: &str,
    ) -> Result<Self, String> {
        // convert the endpoints to the new format with only the one specified inbound endpoint
        // if the inbound endpoint isn't in the specification, return an error
        let recvd_inbound = device_specification
            .endpoints
            .inbound
            .get(inbound_endpoint_name)
            .cloned()
            .ok_or("Inbound endpoint not found on Device specification")?;
        // update authentication to include the full file path for the credentials
        let authentication = match recvd_inbound.authentication {
            azure_device_registry::Authentication::Anonymous => Authentication::Anonymous,
            azure_device_registry::Authentication::Certificate {
                certificate_secret_name,
            } => Authentication::Certificate {
                certificate_path: format!("path/{certificate_secret_name}"),
            },
            azure_device_registry::Authentication::UsernamePassword {
                password_secret_name,
                username_secret_name,
            } => Authentication::UsernamePassword {
                password_path: format!(
                    "{device_endpoint_credentials_mount_path}/{password_secret_name}"
                ),
                username_path: format!(
                    "{device_endpoint_credentials_mount_path}/{username_secret_name}"
                ),
            },
        };
        let endpoints = DeviceEndpoints {
            inbound: InboundEndpoint {
                name: inbound_endpoint_name.to_string(),
                additional_configuration: recvd_inbound.additional_configuration,
                address: recvd_inbound.address,
                authentication,
                endpoint_type: recvd_inbound.endpoint_type,
                trust_settings: recvd_inbound.trust_settings,
                version: recvd_inbound.version,
            },
            outbound_assigned: device_specification.endpoints.outbound_assigned,
            outbound_unassigned: device_specification.endpoints.outbound_unassigned,
        };

        Ok(DeviceSpecification {
            attributes: device_specification.attributes,
            discovered_device_ref: device_specification.discovered_device_ref,
            enabled: device_specification.enabled,
            endpoints,
            external_device_id: device_specification.external_device_id,
            last_transition_time: device_specification.last_transition_time,
            manufacturer: device_specification.manufacturer,
            model: device_specification.model,
            operating_system: device_specification.operating_system,
            operating_system_version: device_specification.operating_system_version,
            uuid: device_specification.uuid,
            version: device_specification.version,
        })
    }
}

#[derive(Debug, Clone)]
/// Represents the endpoints of a device in the Azure Device Registry service.
pub struct DeviceEndpoints {
    /// The 'inbound' Field.
    pub inbound: InboundEndpoint, // different from adr
    /// The 'outbound' Field.
    pub outbound_assigned: HashMap<String, azure_device_registry::OutboundEndpoint>,
    /// The 'outboundUnassigned' Field.
    pub outbound_unassigned: HashMap<String, azure_device_registry::OutboundEndpoint>,
}
/// Represents an inbound endpoint of a device in the Azure Device Registry service.
#[derive(Debug, Clone)]
pub struct InboundEndpoint {
    /// name
    pub name: String,
    /// The 'additionalConfiguration' Field.
    pub additional_configuration: Option<String>,
    /// The 'address' Field.
    pub address: String,
    /// The 'authentication' Field.
    pub authentication: Authentication, // different from adr
    /// The 'endpointType' Field.
    pub endpoint_type: String,
    /// The 'trustSettings' Field.
    pub trust_settings: Option<azure_device_registry::TrustSettings>,
    /// The 'version' Field.
    pub version: Option<String>,
}

#[derive(Debug, Clone, Default)]
/// Represents the authentication method for an endpoint.
pub enum Authentication {
    #[default]
    /// Represents anonymous authentication.
    Anonymous,
    /// Represents authentication using a certificate.
    Certificate {
        /// The 'certificateSecretName' Field.
        certificate_path: String, // different from adr
    },
    /// Represents authentication using a username and password.
    UsernamePassword {
        /// The 'passwordSecretName' Field.
        password_path: String, // different from adr
        /// The 'usernameSecretName' Field.
        username_path: String, // different from adr
    },
}

#[derive(Clone, Debug, Default)] //, PartialEq)]
/// Represents the observed status of a Device and endpoint in the ADR Service.
pub struct DeviceEndpointStatus {
    /// Defines the status for the Device.
    pub config: Option<azure_device_registry::StatusConfig>,
    /// Defines the status for the inbound endpoint.
    pub inbound_endpoint_error: Option<azure_device_registry::ConfigError>, // different from adr
}

impl DeviceEndpointStatus {
    pub(crate) fn new(
        recvd_status: azure_device_registry::DeviceStatus,
        inbound_endpoint_name: &str,
    ) -> Self {
        let inbound_endpoint_error = recvd_status.endpoints.get(inbound_endpoint_name).cloned();
        DeviceEndpointStatus {
            config: recvd_status.config,
            inbound_endpoint_error: inbound_endpoint_error.unwrap_or_default(),
        }
    }
}

fn observe_error_into_retry_error(
    e: azure_device_registry::Error,
) -> RetryError<azure_device_registry::Error> {
    match e.kind() {
        // network/retriable
        azure_device_registry::ErrorKind::AIOProtocolError(_)
        | azure_device_registry::ErrorKind::ObservationError => {
            // not sure what causes ObservationError yet, so let's treat it as transient for now
            RetryError::transient(e)
        }
        // indicates an error in the configuration, so we want to get a new notification instead of retrying this operation
        azure_device_registry::ErrorKind::ServiceError(_)
        // DuplicateObserve indicates an sdk bug where we called observe more than once. Not possible for unobserves.
        // This should be moved to unreachable!() once we add logic for calling unobserve on deletion
        | azure_device_registry::ErrorKind::DuplicateObserve(_) => RetryError::permanent(e),
        _ => {
            // InvalidRequestArgument shouldn't be possible since timeout is already validated
            // ValidationError and ShutdownError aren't possible for this fn to return
            unreachable!()
        }
    }
}
