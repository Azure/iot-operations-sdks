// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

//! Types for Azure IoT Operations Connectors.

use std::{
    collections::HashMap,
    sync::{Arc, RwLock},
};

use azure_iot_operations_mqtt::interface::AckToken;
use azure_iot_operations_services::{
    azure_device_registry::{
        self, AssetUpdateObservation, DeviceUpdateObservation,
        models::{self as adr_models},
    },
    schema_registry,
};
use chrono::{DateTime, Utc};
use tokio_retry2::{Retry, RetryError};

use crate::{
    AdrConfigError, Data, DatasetRef, MessageSchema,
    base_connector::ConnectorContext,
    destination_endpoint::{self, Forwarder},
    filemount::azure_device_registry::{
        AssetCreateObservation, AssetDeletionToken, AssetRef, DeviceEndpointCreateObservation,
        DeviceEndpointRef,
    },
};

/// Used as the strategy when using [`tokio_retry2::Retry`]
const RETRY_STRATEGY: tokio_retry2::strategy::ExponentialFactorBackoff =
    tokio_retry2::strategy::ExponentialFactorBackoff::from_millis(500, 2.0);

/// An Observation for device endpoint creation events that uses
/// multiple underlying clients to get full device endpoint information.
pub struct DeviceEndpointClientCreationObservation {
    connector_context: Arc<ConnectorContext>,
    device_endpoint_create_observation: DeviceEndpointCreateObservation,
}
impl DeviceEndpointClientCreationObservation {
    /// Creates a new [`DeviceEndpointClientCreationObservation`] that uses the given [`ConnectorContext`]
    pub(crate) fn new(connector_context: Arc<ConnectorContext>) -> Self {
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
        DeviceEndpointClient,
        /*DeviceDeleteToken,*/ AssetClientCreationObservation,
    )> {
        loop {
            // Get the notification
            let (device_endpoint_ref, asset_create_observation) = self
                .device_endpoint_create_observation
                .recv_notification()
                .await?;

            // and then get device update observation as well and turn it into a DeviceEndpointClientUpdateObservation
            let device_endpoint_client_update_observation =  match Retry::spawn(RETRY_STRATEGY.take(10), async || -> Result<DeviceUpdateObservation, RetryError<azure_device_registry::Error>> {
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
                Ok(device_update_observation) => device_update_observation,
                Err(e) => {
                  log::error!("Failed to observe for device update notifications after retries: {e}");
                  log::error!("Dropping device endpoint create notification: {device_endpoint_ref:?}");
                  continue;
                },
            };

            // get the device definition
            let device = match Retry::spawn(
                RETRY_STRATEGY,
                async || -> Result<adr_models::Device, RetryError<azure_device_registry::Error>> {
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
                                    log::warn!("Get device definition failed. Retrying: {e}");
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
                        RETRY_STRATEGY.take(10),
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
                device_endpoint_ref.clone(),
                device_endpoint_client_update_observation,
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
                        RETRY_STRATEGY.take(10),
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

            // Turn AssetCreateObservation into an AssetClientCreationObservation
            let asset_client_creation_observation = AssetClientCreationObservation {
                asset_create_observation,
                connector_context: self.connector_context.clone(),
                device_specification: device_endpoint_client.specification.clone(),
                device_status: device_endpoint_client.status.clone(),
            };

            return Some((device_endpoint_client, asset_client_creation_observation));
        }
    }
}

/// Azure Device Registry Device Endpoint that includes additional functionality to report status and receive updates
#[derive(Debug, Getters)]
pub struct DeviceEndpointClient {
    /// The names of the Device and Inbound Endpoint
    device_endpoint_ref: DeviceEndpointRef,
    /// The 'specification' Field.
    #[getter(skip)]
    specification: Arc<RwLock<DeviceSpecification>>,
    /// The 'status' Field.
    #[getter(skip)]
    status: Arc<RwLock<Option<DeviceEndpointStatus>>>,
    /// The internal observation for updates
    #[getter(skip)]
    device_update_observation: DeviceUpdateObservation,
    #[getter(skip)]
    connector_context: Arc<ConnectorContext>,
}
impl DeviceEndpointClient {
    pub(crate) fn new(
        device: adr_models::Device,
        device_endpoint_ref: DeviceEndpointRef,
        device_update_observation: DeviceUpdateObservation,
        connector_context: Arc<ConnectorContext>,
        // TODO: This won't need to return an error once the service properly sends errors if the endpoint doesn't exist
    ) -> Result<Self, String> {
        Ok(DeviceEndpointClient {
            // TODO: get device_endpoint_credentials_mount_path from connector config
            specification: Arc::new(RwLock::new(DeviceSpecification::new(
                device.specification,
                "/etc/akri/secrets/device_endpoint_auth",
                &device_endpoint_ref.inbound_endpoint_name,
            )?)),
            status: Arc::new(RwLock::new(device.status.map(|recvd_status| {
                DeviceEndpointStatus::new(recvd_status, &device_endpoint_ref.inbound_endpoint_name)
            }))),
            device_endpoint_ref,
            device_update_observation,
            connector_context,
        })
    }

    /// Used to report the status of a device and endpoint together,
    /// and then updates the `self.status` with the new status returned
    ///
    /// # Panics
    /// if the specification mutex has been poisoned, which should not be possible
    pub async fn report_status(
        &self,
        device_status: Result<(), AdrConfigError>,
        endpoint_status: Result<(), AdrConfigError>,
    ) {
        // Create status
        let version = self.specification.read().unwrap().version;
        let status = adr_models::DeviceStatus {
            config: Some(azure_device_registry::StatusConfig {
                version,
                error: device_status.err(),
                last_transition_time: None, // this field will be removed, so we don't need to worry about it for now
            }),
            // inserts the inbound endpoint name with None if there's no error, or Some(AdrConfigError) if there is
            endpoints: HashMap::from([(
                self.device_endpoint_ref.inbound_endpoint_name.clone(),
                endpoint_status.err(),
            )]),
        };

        // send status update to the service
        self.internal_report_status(status).await;
    }

    /// Used to report the status of just the device,
    /// and then updates the [`Device`] with the new status returned
    ///
    /// # Panics
    /// if the specification mutex has been poisoned, which should not be possible
    pub async fn report_device_status(&self, device_status: Result<(), AdrConfigError>) {
        // Create status with empty endpoint status
        let version = self.specification.read().unwrap().version;
        let status = adr_models::DeviceStatus {
            config: Some(azure_device_registry::StatusConfig {
                version,
                error: device_status.err(),
                last_transition_time: None, // this field will be removed, so we don't need to worry about it for now
            }),
            // Endpoints are merged on the service, so sending an empty map won't update anything
            endpoints: HashMap::new(),
        };

        // send status update to the service
        self.internal_report_status(status).await;
    }

    /// Used to report the status of just the endpoint,
    /// and then updates the [`Device`] with the new status returned
    /// # Panics
    /// if the status or specification mutexes have been poisoned, which should not be possible
    pub async fn report_endpoint_status(&self, endpoint_status: Result<(), AdrConfigError>) {
        // If the version of the current status config matches the current version, then include the existing config.
        // If there's no current config or the version doesn't match, don't report a status since the status for this version hasn't been reported yet
        let current_config = self.status.read().unwrap().as_ref().and_then(|status| {
            if status.config.as_ref().and_then(|config| config.version)
                == self.specification.read().unwrap().version
            {
                status.config.clone()
            } else {
                None
            }
        });
        // Create status without updating the device status
        let status = adr_models::DeviceStatus {
            config: current_config,
            // inserts the inbound endpoint name with None if there's no error, or Some(AdrConfigError) if there is
            endpoints: HashMap::from([(
                self.device_endpoint_ref.inbound_endpoint_name.clone(),
                endpoint_status.err(),
            )]),
        };

        // send status update to the service
        self.internal_report_status(status).await;
    }

    /// Used to receive updates for the Device/Inbound Endpoint from the Azure Device Registry Service.
    /// This function returning `Some(())` indicates that the device specification and status have been
    /// updated in place. The function returns [`None`] if there will be no more notifications.
    ///
    /// # Panics
    /// If the Azure Device Registry Service provides a notification that isn't for this Device Endpoint. This should not be possible.
    ///
    /// If the status or specification mutexes have been poisoned, which should not be possible
    pub async fn recv_update(&mut self) -> Option<()> {
        // handle the notification
        // We set auto ack to true, so there's never an ack here to deal with. If we restart, then we'll implicitly
        // get the update again because we'll pull the latest definition on the restart, so we don't need to get
        // the notification again.
        let (updated_device, _) = self.device_update_observation.recv_notification().await?;
        // update self with updated specification and status
        let mut unlocked_specification = self.specification.write().unwrap(); // unwrap can't fail unless lock is poisoned
        *unlocked_specification = DeviceSpecification::new(
                updated_device.specification,
                // TODO: get device_endpoint_credentials_mount_path from connector config
                "/etc/akri/secrets/device_endpoint_auth",
                &self.device_endpoint_ref.inbound_endpoint_name,
            ).expect("Device Update Notification should never provide a device that doesn't have the inbound endpoint");

        let mut unlocked_status = self.status.write().unwrap(); // unwrap can't fail unless lock is poisoned
        *unlocked_status = updated_device.status.map(|recvd_status| {
            DeviceEndpointStatus::new(
                recvd_status,
                &self.device_endpoint_ref.inbound_endpoint_name,
            )
        });
        Some(())
    }

    // Returns a clone of the current device status
    /// # Panics
    /// if the status mutex has been poisoned, which should not be possible
    #[must_use]
    pub fn status(&self) -> Option<DeviceEndpointStatus> {
        (*self.status.read().unwrap()).clone()
    }

    // Returns a clone of the current device specification
    /// # Panics
    /// if the specification mutex has been poisoned, which should not be possible
    #[must_use]
    pub fn specification(&self) -> DeviceSpecification {
        (*self.specification.read().unwrap()).clone()
    }

    /// Reports an already built status to the service, with retries, and then updates the device with the new status returned
    async fn internal_report_status(&self, adr_device_status: adr_models::DeviceStatus) {
        // send status update to the service
        match Retry::spawn(
            RETRY_STRATEGY.take(10),
            async || -> Result<adr_models::Device, RetryError<azure_device_registry::Error>> {
                self.connector_context
                    .azure_device_registry_client
                    .update_device_plus_endpoint_status(
                        self.device_endpoint_ref.device_name.clone(),
                        self.device_endpoint_ref.inbound_endpoint_name.clone(),
                        adr_device_status.clone(),
                        self.connector_context.default_timeout,
                    )
                    .await
                    .map_err(|e| {
                        match e.kind() {
                            // network/retriable
                            azure_device_registry::ErrorKind::AIOProtocolError(_) => {
                                log::warn!("Update device status failed. Retrying: {e}");
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
                let mut unlocked_status = self.status.write().unwrap(); // unwrap can't fail unless lock is poisoned
                *unlocked_status = updated_device.status.map(|recvd_status| {
                    DeviceEndpointStatus::new(
                        recvd_status,
                        &self.device_endpoint_ref.inbound_endpoint_name,
                    )
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

/// An Observation for asset creation events that uses
/// multiple underlying clients to get full asset information.
pub struct AssetClientCreationObservation {
    asset_create_observation: AssetCreateObservation,
    connector_context: Arc<ConnectorContext>,
    device_specification: Arc<RwLock<DeviceSpecification>>,
    device_status: Arc<RwLock<Option<DeviceEndpointStatus>>>,
}
impl AssetClientCreationObservation {
    /// Receives a notification for a newly created asset or [`None`] if there
    /// will be no more notifications. This notification includes the [`AssetClient`],
    /// an [`AssetClientUpdateObservation`] to observe for updates on the new Asset,
    /// and an [`AssetDeletionToken`] to observe for deletion of this Asset
    pub async fn recv_notification(
        &mut self,
    ) -> Option<(
        AssetClient,
        AssetClientUpdateObservation,
        AssetDeletionToken,
    )> {
        loop {
            // Get the notification
            let (asset_ref, asset_deletion_token) =
                self.asset_create_observation.recv_notification().await?;

            // Get asset update observation as well and turn it into a AssetClientUpdateObservation
            let asset_client_update_observation =  match Retry::spawn(RETRY_STRATEGY.take(10), async || -> Result<AssetUpdateObservation, RetryError<azure_device_registry::Error>> {
                self.connector_context
                    .azure_device_registry_client
                    .observe_asset_update_notifications(
                        asset_ref.device_name.clone(),
                        asset_ref.inbound_endpoint_name.clone(),
                        asset_ref.name.clone(),
                        self.connector_context.default_timeout,
                    )
                    // retry on network errors, otherwise don't retry on config/dev errors
                    .await.map_err(observe_error_into_retry_error)
            }).await {
                Ok(asset_update_observation) => {
                    AssetClientUpdateObservation {
                        asset_update_observation,
                        connector_context: self.connector_context.clone(),
                    }
                },
                Err(e) => {
                    log::error!("Failed to observe for asset update notifications after retries: {e}");
                    log::error!("Dropping asset create notification: {asset_ref:?}");
                    continue;
                },
            };

            // get the asset definition
            let asset_client = match Retry::spawn(
                RETRY_STRATEGY,
                async || -> Result<adr_models::Asset, RetryError<azure_device_registry::Error>> {
                    self.connector_context
                        .azure_device_registry_client
                        .get_asset(
                            asset_ref.device_name.clone(),
                            asset_ref.inbound_endpoint_name.clone(),
                            asset_ref.name.clone(),
                            self.connector_context.default_timeout,
                        )
                        .await
                        .map_err(|e| {
                            match e.kind() {
                                // network/retriable
                                azure_device_registry::ErrorKind::AIOProtocolError(_) => {
                                    log::warn!("Get asset definition failed. Retrying: {e}");
                                    RetryError::transient(e)
                                }
                                // indicates an error in the configuration, so we want to get a new notification instead of retrying this operation
                                azure_device_registry::ErrorKind::ServiceError(_) => {
                                    RetryError::permanent(e)
                                }
                                _ => {
                                    // InvalidRequestArgument shouldn't be possible since timeout is already validated
                                    // ValidationError shouldn't be possible since we shouldn't receive a notification with an empty asset name
                                    // ObservationError, DuplicateObserve, and ShutdownError aren't possible for this fn to return
                                    unreachable!()
                                }
                            }
                        })
                },
            )
            .await
            {
                Ok(asset) => {
                    AssetClient::new(
                        asset,
                        asset_ref,
                        self.device_specification.clone(),
                        self.device_status.clone(),
                        self.connector_context.clone(),
                    )
                    .await
                }
                Err(e) => {
                    log::error!("Failed to get Asset definition after retries: {e}");
                    log::error!("Dropping asset create notification: {asset_ref:?}");
                    // unobserve as cleanup
                    let _ = Retry::spawn(
                        RETRY_STRATEGY.take(10),
                        async || -> Result<(), RetryError<azure_device_registry::Error>> {
                            self.connector_context
                                .azure_device_registry_client
                                .unobserve_asset_update_notifications(
                                    asset_ref.device_name.clone(),
                                    asset_ref.inbound_endpoint_name.clone(),
                                    asset_ref.name.clone(),
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
                            "Failed to unobserve asset update notifications after retries: {e}"
                        );
                    });
                    continue;
                }
            };
            return Some((
                asset_client,
                asset_client_update_observation,
                asset_deletion_token,
            ));
        }
    }
}

/// An Observation for asset update events that uses
/// multiple underlying clients to get full asset update information.
#[allow(dead_code)]
pub struct AssetClientUpdateObservation {
    asset_update_observation: AssetUpdateObservation,
    connector_context: Arc<ConnectorContext>,
}
impl AssetClientUpdateObservation {
    /// Receives an updated [`AssetClient`] or [`None`] if there will be no more notifications.
    ///
    /// If there are notifications:
    /// - Returns Some([`AssetClient`], [`Option<AckToken>`]) on success
    ///     - If auto ack is disabled, the [`AckToken`] should be used or dropped when you want the ack to occur. If auto ack is enabled, you may use ([`AssetClient`], _) to ignore the [`AckToken`].
    ///
    /// A received notification can be acknowledged via the [`AckToken`] by calling [`AckToken::ack`] or dropping the [`AckToken`].
    #[allow(clippy::unused_async)]
    pub async fn recv_notification(&self) -> Option<(AssetClient, Option<AckToken>)> {
        // handle the notification
        None
    }
}

/// Azure Device Registry Asset that includes additional functionality
/// to report status, translate data, and send data to the destination
#[derive(Debug, Getters)]
pub struct AssetClient {
    /// Asset, device, and inbound endpoint names
    asset_ref: AssetRef,
    /// Specification for the Asset
    specification: Arc<AssetSpecification>, // TODO: will need to be Arc<RwLock> once update is supported
    /// Status for the Asset
    #[getter(skip)]
    status: Arc<RwLock<Option<adr_models::AssetStatus>>>,
    /// Datasets on this Asset
    datasets: Vec<DatasetClient>, // TODO: might need to change this model once the dataset definition can get updated from an update
    // TODO: events, streams, and management groups as well
    /// Specification of the device that this Asset is tied to
    #[getter(skip)]
    device_specification: Arc<RwLock<DeviceSpecification>>,
    /// Status of the device that this Asset is tied to
    #[getter(skip)]
    device_status: Arc<RwLock<Option<DeviceEndpointStatus>>>,
    #[getter(skip)]
    connector_context: Arc<ConnectorContext>,
}
impl AssetClient {
    pub(crate) async fn new(
        asset: adr_models::Asset,
        asset_ref: AssetRef,
        device_specification: Arc<RwLock<DeviceSpecification>>,
        device_status: Arc<RwLock<Option<DeviceEndpointStatus>>>,
        connector_context: Arc<ConnectorContext>,
    ) -> Self {
        let status = Arc::new(RwLock::new(asset.status));
        let dataset_definitions = asset.specification.datasets.clone();
        let specification = Arc::new(AssetSpecification::from(asset.specification));
        let default_dataset_destinations =
            match destination_endpoint::Destination::new_dataset_destinations(
                &specification.default_datasets_destinations,
                &asset_ref.inbound_endpoint_name,
                &connector_context,
            ) {
                Ok(res) => res.into_iter().map(Arc::new).collect(),
                Err(e) => {
                    log::error!(
                        "Invalid default dataset destination for Asset {}: {e:?}",
                        asset_ref.name
                    );
                    let adr_asset_status = adr_models::AssetStatus {
                        config: Some(azure_device_registry::StatusConfig {
                            version: specification.version,
                            error: Some(e),
                            last_transition_time: None, // this field will be removed, so we don't need to worry about it for now
                        }),
                        ..adr_models::AssetStatus::default()
                    };
                    // send status update to the service
                    Self::internal_report_status(
                        adr_asset_status,
                        &connector_context,
                        &asset_ref,
                        &status,
                    )
                    .await;
                    // set this to None because if all datasets have a destination specified, this might not cause the asset to be unusable
                    vec![]
                }
            };
        let mut dataset_config_errors = Vec::new();
        let datasets = dataset_definitions
            .into_iter()
            // leave out any datasets that have config errors - they will be provided by an update notification if they get fixed, otherwise they can't be used at this point
            // TODO: should we instead include it with a forwarder that doesn't work?
            .filter_map(|dataset| {
                let dataset_name = dataset.name.clone();
                match DatasetClient::new(
                    dataset,
                    &default_dataset_destinations,
                    asset_ref.clone(),
                    status.clone(),
                    specification.clone(),
                    device_specification.clone(),
                    device_status.clone(),
                    connector_context.clone(),
                ) {
                    Ok(dataset_client) => Some(dataset_client),
                    Err(e) => {
                        log::error!(
                            "Invalid dataset destination for dataset: {dataset_name} {e:?}"
                        );
                        // Get current message schema reference if there is one, so that it isn't overwritten
                        let message_schema_reference = status
                            .read()
                            .unwrap()
                            .as_ref()?
                            .datasets
                            .as_ref()?
                            .iter()
                            .find(|dataset| dataset.name == dataset_name)?
                            .message_schema_reference
                            .clone();
                        dataset_config_errors.push(adr_models::DatasetEventStreamStatus {
                            name: dataset_name,
                            message_schema_reference,
                            error: Some(e),
                        });
                        None
                    }
                }
            })
            .collect();
        if !dataset_config_errors.is_empty() {
            // If the version of the current status config matches the current version, then include the existing config.
            // If there's no current config or the version doesn't match, don't report a status since the status for this version hasn't been reported yet
            let current_asset_config = status.read().unwrap().as_ref().and_then(|status| {
                if status.config.as_ref().and_then(|config| config.version) == specification.version
                {
                    status.config.clone()
                } else {
                    None
                }
            });
            let adr_asset_status = adr_models::AssetStatus {
                config: current_asset_config,
                datasets: Some(dataset_config_errors),
                ..adr_models::AssetStatus::default()
            };
            // send status update to the service
            AssetClient::internal_report_status(
                adr_asset_status,
                &connector_context,
                &asset_ref,
                &status,
            )
            .await;
        }
        AssetClient {
            asset_ref,
            specification,
            status,
            datasets,
            device_specification,
            device_status,
            connector_context,
        }
    }

    /// Used to report the status of an Asset
    pub async fn report_status(&self, status: Result<(), AdrConfigError>) {
        let adr_asset_status = adr_models::AssetStatus {
            config: Some(azure_device_registry::StatusConfig {
                version: self.specification.version,
                error: status.err(),
                last_transition_time: None, // this field will be removed, so we don't need to worry about it for now
            }),
            ..adr_models::AssetStatus::default()
        };

        // send status update to the service
        Self::internal_report_status(
            adr_asset_status,
            &self.connector_context,
            &self.asset_ref,
            &self.status,
        )
        .await;
    }

    /// Returns a clone of the current asset status
    /// # Panics
    /// if the status mutex has been poisoned, which should not be possible
    #[must_use]
    pub fn status(&self) -> Option<adr_models::AssetStatus> {
        (*self.status.read().unwrap()).clone()
    }

    // Returns a clone of the current device specification
    /// # Panics
    /// if the device specification mutex has been poisoned, which should not be possible
    #[must_use]
    pub fn device_specification(&self) -> DeviceSpecification {
        (*self.device_specification.read().unwrap()).clone()
    }

    // Returns a clone of the current device status
    /// # Panics
    /// if the device status mutex has been poisoned, which should not be possible
    #[must_use]
    pub fn device_status(&self) -> Option<DeviceEndpointStatus> {
        (*self.device_status.read().unwrap()).clone()
    }

    pub(crate) async fn internal_report_status(
        adr_asset_status: adr_models::AssetStatus,
        connector_context: &ConnectorContext,
        asset_ref: &AssetRef,
        asset_status_ref: &Arc<RwLock<Option<adr_models::AssetStatus>>>,
    ) {
        // send status update to the service
        match Retry::spawn(
            RETRY_STRATEGY.take(10),
            async || -> Result<adr_models::Asset, RetryError<azure_device_registry::Error>> {
                connector_context
                    .azure_device_registry_client
                    .update_asset_status(
                        asset_ref.device_name.clone(),
                        asset_ref.inbound_endpoint_name.clone(),
                        asset_ref.name.clone(),
                        adr_asset_status.clone(),
                        connector_context.default_timeout,
                    )
                    .await
                    .map_err(|e| {
                        match e.kind() {
                            // network/retriable
                            azure_device_registry::ErrorKind::AIOProtocolError(_) => {
                                log::warn!("Update asset status failed. Retrying: {e}");
                                RetryError::transient(e)
                            }
                            // indicates an error in the configuration, might be transient in the future depending on what it can indicate
                            azure_device_registry::ErrorKind::ServiceError(_) => {
                                RetryError::permanent(e)
                            }
                            _ => {
                                // InvalidRequestArgument shouldn't be possible since timeout is already validated
                                // ValidationError shouldn't be possible since we shouldn't have an asset with an empty asset name
                                // ObservationError, DuplicateObserve, and ShutdownError aren't possible for this fn to return
                                unreachable!()
                            }
                        }
                    })
            },
        )
        .await
        {
            Ok(updated_asset) => {
                // update self with new returned status
                let mut unlocked_status = asset_status_ref.write().unwrap(); // unwrap can't fail unless lock is poisoned
                *unlocked_status = updated_asset.status;
                // NOTE: There may be updates present on the asset specification, but even if that is the case,
                // we won't update them here and instead wait for the asset update notification (finding out
                // first here is a race condition, the update will always be received imminently)
            }
            Err(e) => {
                // TODO: return an error for this scenario? Largely shouldn't be possible
                log::error!("Failed to Update Asset Status: {e}");
            }
        };
    }
}

/// Azure Device Registry Dataset that includes additional functionality
/// to report status, translate data, and send data to the destination
#[derive(Debug, Getters, Clone)]
pub struct DatasetClient {
    /// Dataset, asset, device, and inbound endpoint names
    dataset_ref: DatasetRef,
    /// Dataset Definition
    dataset_definition: adr_models::Dataset,
    /// Current status for the Asset
    #[getter(skip)]
    asset_status: Arc<RwLock<Option<adr_models::AssetStatus>>>,
    /// Current specification for the Asset
    asset_specification: Arc<AssetSpecification>,
    /// Specification of the device that this dataset is tied to
    #[getter(skip)]
    device_specification: Arc<RwLock<DeviceSpecification>>,
    /// Status of the device that this dataset is tied to
    #[getter(skip)]
    device_status: Arc<RwLock<Option<DeviceEndpointStatus>>>,
    #[getter(skip)]
    forwarder: Arc<Forwarder>,
    #[getter(skip)]
    connector_context: Arc<ConnectorContext>,
    /// Asset reference for internal use
    #[getter(skip)]
    asset_ref: AssetRef,
}

impl DatasetClient {
    #[allow(clippy::too_many_arguments)]
    pub(crate) fn new(
        dataset_definition: adr_models::Dataset,
        default_destinations: &[Arc<destination_endpoint::Destination>],
        asset_ref: AssetRef,
        asset_status: Arc<RwLock<Option<adr_models::AssetStatus>>>,
        asset_specification: Arc<AssetSpecification>,
        device_specification: Arc<RwLock<DeviceSpecification>>,
        device_status: Arc<RwLock<Option<DeviceEndpointStatus>>>,
        connector_context: Arc<ConnectorContext>,
    ) -> Result<Self, AdrConfigError> {
        // Create a new dataset
        let forwarder = Arc::new(Forwarder::new_dataset_forwarder(
            &dataset_definition.destinations,
            &asset_ref.inbound_endpoint_name,
            default_destinations,
            connector_context.clone(),
        )?);
        Ok(Self {
            dataset_ref: DatasetRef {
                dataset_name: dataset_definition.name.clone(),
                asset_name: asset_ref.name.clone(),
                device_name: asset_ref.device_name.clone(),
                inbound_endpoint_name: asset_ref.inbound_endpoint_name.clone(),
            },
            asset_ref,
            dataset_definition,
            asset_status,
            asset_specification,
            device_specification,
            device_status,
            forwarder,
            connector_context,
        })
    }

    /// Used to report the status of a dataset
    /// # Panics
    /// if the asset status mutex has been poisoned, which should not be possible
    pub async fn report_status(&self, status: Result<(), AdrConfigError>) {
        // If the version of the current status config matches the current version, then include the existing config.
        // If there's no current config or the version doesn't match, don't report a status since the status for this version hasn't been reported yet
        let current_asset_config = self
            .asset_status
            .read()
            .unwrap()
            .as_ref()
            .and_then(|status| {
                if status.config.as_ref().and_then(|config| config.version)
                    == self.asset_specification.version
                {
                    status.config.clone()
                } else {
                    None
                }
            });
        // Get current message schema reference, so that it isn't overwritten
        let current_message_schema_reference = self.message_schema_reference();
        let adr_asset_status = adr_models::AssetStatus {
            config: current_asset_config,
            datasets: Some(vec![adr_models::DatasetEventStreamStatus {
                name: self.dataset_ref.dataset_name.clone(),
                message_schema_reference: current_message_schema_reference,
                error: status.err(),
            }]),
            ..adr_models::AssetStatus::default()
        };

        // send status update to the service
        AssetClient::internal_report_status(
            adr_asset_status,
            &self.connector_context,
            &self.asset_ref,
            &self.asset_status,
        )
        .await;
    }

    /// Used to report the message schema of a dataset
    ///
    /// # Errors
    /// [`schema_registry::Error`] of kind [`InvalidArgument`](schema_registry::ErrorKind::InvalidArgument)
    /// if the content of the [`MessageSchema`] is empty or there is an error building the request
    ///
    /// [`schema_registry::Error`] of kind [`ServiceError`](schema_registry::ErrorKind::ServiceError)
    /// if there is an error returned by the Schema Registry Service.
    ///
    /// # Panics
    /// If the Schema Registry Service returns a schema without required values. This should get updated
    /// to be validated by the Schema Registry API surface in the future
    ///
    /// If the asset status mutex has been poisoned, which should not be possible
    pub async fn report_message_schema(
        &self,
        message_schema: MessageSchema,
    ) -> Result<adr_models::MessageSchemaReference, schema_registry::Error> {
        // TODO: save message schema provided with message schema uri so it can be compared
        // send message schema to schema registry service
        let message_schema_reference = Retry::spawn(
            RETRY_STRATEGY,
            async || -> Result<schema_registry::Schema, RetryError<schema_registry::Error>> {
                self.connector_context
                    .schema_registry_client
                    .put(
                        message_schema.clone(),
                        self.connector_context.default_timeout,
                    )
                    .await
                    .map_err(|e| {
                        match e.kind() {
                            // network/retriable
                            schema_registry::ErrorKind::AIOProtocolError(_) => {
                                log::warn!("Reporting message schema failed. Retrying: {e}");
                                RetryError::transient(e)
                            }
                            // indicates an error in the provided message schema, return to caller so they can fix
                            schema_registry::ErrorKind::ServiceError(_)
                            | schema_registry::ErrorKind::InvalidArgument(_) => {
                                RetryError::permanent(e)
                            }
                            // SerializationError shouldn't be possible since any [`MessageSchema`] should be serializable
                            schema_registry::ErrorKind::SerializationError(_) => {
                                unreachable!()
                            }
                        }
                    })
            },
        )
        .await
        .map(|schema| {
            adr_models::MessageSchemaReference {
                name: schema
                    .name
                    .expect("schema name will always be present since sent in PUT"),
                version: schema
                    .version
                    .expect("schema version will always be present since sent in PUT"),
                registry_namespace: schema
                    .namespace
                    .expect("schema namespace will always be present."), // waiting on change to service DTDL for this to be guaranteed in code
            }
        })?;
        // If the version of the current status config matches the current version, then include the existing config.
        // If there's no current config or the version doesn't match, don't report a status since the status for this version hasn't been reported yet
        let current_asset_config = self
            .asset_status
            .read()
            .unwrap()
            .as_ref()
            .and_then(|status| {
                if status.config.as_ref().and_then(|config| config.version)
                    == self.asset_specification.version
                {
                    status.config.clone()
                } else {
                    None
                }
            });
        // Get the current dataset config error, if it exists, so that it isn't overwritten
        let current_dataset_config_error =
            self.asset_status
                .read()
                .unwrap()
                .as_ref()
                .and_then(|status| {
                    status.datasets.as_ref().and_then(|datasets| {
                        datasets
                            .iter()
                            .find(|dataset| dataset.name == self.dataset_ref.dataset_name)
                            .and_then(|dataset| dataset.error.clone())
                    })
                });
        let adr_asset_status = adr_models::AssetStatus {
            config: current_asset_config,
            datasets: Some(vec![adr_models::DatasetEventStreamStatus {
                name: self.dataset_ref.dataset_name.clone(),
                message_schema_reference: Some(message_schema_reference.clone()),
                error: current_dataset_config_error,
            }]),
            ..adr_models::AssetStatus::default()
        };

        // send status update to the service
        AssetClient::internal_report_status(
            adr_asset_status,
            &self.connector_context,
            &self.asset_ref,
            &self.asset_status,
        )
        .await;

        self.forwarder
            .update_message_schema_reference(Some(message_schema_reference.clone()));

        Ok(message_schema_reference)
    }

    /// Used to send transformed data to the destination
    /// # Errors
    /// TODO
    pub async fn forward_data(&self, data: Data) -> Result<(), destination_endpoint::Error> {
        self.forwarder.send_data(data).await
    }

    /// Returns a clone of this dataset's [`MessageSchemaReference`] from
    /// the [`AssetStatus`], if it exists
    ///
    /// # Panics
    /// if the asset status mutex has been poisoned, which should not be possible
    #[must_use]
    pub fn message_schema_reference(&self) -> Option<adr_models::MessageSchemaReference> {
        // unwrap can't fail unless lock is poisoned
        self.asset_status
            .read()
            .unwrap()
            .as_ref()?
            .datasets
            .as_ref()?
            .iter()
            .find(|dataset| dataset.name == self.dataset_ref.dataset_name)?
            .message_schema_reference
            .clone()
    }

    /// Returns a clone of the current asset status, if it exists
    /// # Panics
    /// if the asset status mutex has been poisoned, which should not be possible
    #[must_use]
    pub fn asset_status(&self) -> Option<adr_models::AssetStatus> {
        (*self.asset_status.read().unwrap()).clone()
    }

    // Returns a clone of the current device specification
    /// # Panics
    /// if the device specification mutex has been poisoned, which should not be possible
    #[must_use]
    pub fn device_specification(&self) -> DeviceSpecification {
        (*self.device_specification.read().unwrap()).clone()
    }

    // Returns a clone of the current device status
    /// # Panics
    /// if the device status mutex has been poisoned, which should not be possible
    #[must_use]
    pub fn device_status(&self) -> Option<DeviceEndpointStatus> {
        (*self.device_status.read().unwrap()).clone()
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
    pub last_transition_time: Option<DateTime<Utc>>,
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
        device_specification: adr_models::DeviceSpecification,
        device_endpoint_credentials_mount_path: &str,
        inbound_endpoint_name: &str,
    ) -> Result<Self, String> {
        // convert the endpoints to the new format with only the one specified inbound endpoint
        // if the inbound endpoint isn't in the specification, return an error
        let recvd_endpoints = device_specification
            .endpoints
            .ok_or("Endpoints not found on Device specification")?;

        let recvd_inbound = recvd_endpoints
            .inbound
            .get(inbound_endpoint_name)
            .cloned()
            .ok_or("Inbound endpoint not found on Device specification")?;
        let recvd_outbound = recvd_endpoints
            .outbound
            // TODO: more elegant way to handle this
            .unwrap_or(adr_models::OutboundEndpoints {
                assigned: HashMap::new(),
                unassigned: HashMap::new(),
            });

        // update authentication to include the full file path for the credentials
        let authentication = match recvd_inbound.authentication {
            adr_models::Authentication::Anonymous => Authentication::Anonymous,
            adr_models::Authentication::Certificate {
                certificate_secret_name,
            } => Authentication::Certificate {
                certificate_path: format!("path/{certificate_secret_name}"),
            },
            adr_models::Authentication::UsernamePassword {
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
            outbound_assigned: recvd_outbound.assigned,
            outbound_unassigned: recvd_outbound.unassigned,
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
    pub outbound_assigned: HashMap<String, adr_models::OutboundEndpoint>,
    /// The 'outboundUnassigned' Field.
    pub outbound_unassigned: HashMap<String, adr_models::OutboundEndpoint>,
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
    pub trust_settings: Option<adr_models::TrustSettings>,
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
    pub inbound_endpoint_error: Option<AdrConfigError>, // different from adr
}

impl DeviceEndpointStatus {
    pub(crate) fn new(recvd_status: adr_models::DeviceStatus, inbound_endpoint_name: &str) -> Self {
        let inbound_endpoint_error = recvd_status.endpoints.get(inbound_endpoint_name).cloned();
        DeviceEndpointStatus {
            config: recvd_status.config,
            inbound_endpoint_error: inbound_endpoint_error.unwrap_or_default(),
        }
    }
}

/// Represents the specification of an Asset in the Azure Device Registry service.
#[derive(Debug)]
pub struct AssetSpecification {
    /// URI or type definition ids.
    pub asset_type_refs: Vec<String>, // if None, we can represent as empty vec. Can currently only be length of 1
    /// A set of key-value pairs that contain custom attributes
    pub attributes: HashMap<String, String>, // if None, we can represent as empty hashmap
    /// Array of datasets that are part of the asset.
    // pub datasets: Vec<DatasetClient>, // if None, we can represent as empty vec. Different from adr
    /// Default configuration for datasets.
    pub default_datasets_configuration: Option<String>,
    /// Default destinations for datasets.
    pub default_datasets_destinations: Vec<adr_models::DatasetDestination>, // if None, we can represent as empty vec.  Can currently only be length of 1
    /// Default configuration for events.
    pub default_events_configuration: Option<String>,
    /// Default destinations for events.
    pub default_events_destinations: Vec<adr_models::EventStreamDestination>, // if None, we can represent as empty vec.  Can currently only be length of 1
    /// Default configuration for management groups.
    pub default_management_groups_configuration: Option<String>,
    /// Default configuration for streams.
    pub default_streams_configuration: Option<String>,
    /// Default destinations for streams.
    pub default_streams_destinations: Vec<adr_models::EventStreamDestination>, // if None, we can represent as empty vec. Can currently only be length of 1
    /// The description of the asset.
    pub description: Option<String>,
    /// A reference to the Device and Endpoint within the device
    pub device_ref: adr_models::DeviceRef,
    /// Reference to a list of discovered assets
    pub discovered_asset_refs: Vec<String>, // if None, we can represent as empty vec
    /// The display name of the asset.
    pub display_name: Option<String>,
    /// Reference to the documentation.
    pub documentation_uri: Option<String>,
    /// Enabled/Disabled status of the asset.
    pub enabled: Option<bool>, // TODO: just bool?
    ///  Array of events that are part of the asset. TODO: `EventClient`
    pub events: Vec<adr_models::Event>, // if None, we can represent as empty vec
    /// Asset id provided by the customer.
    pub external_asset_id: Option<String>,
    /// Revision number of the hardware.
    pub hardware_revision: Option<String>,
    /// The last time the asset has been modified.
    pub last_transition_time: Option<DateTime<Utc>>,
    /// Array of management groups that are part of the asset. TODO: `ManagementGroupClient`
    pub management_groups: Vec<adr_models::ManagementGroup>, // if None, we can represent as empty vec
    /// The name of the manufacturer.
    pub manufacturer: Option<String>,
    /// The URI of the manufacturer.
    pub manufacturer_uri: Option<String>,
    /// The model of the asset.
    pub model: Option<String>,
    /// The product code of the asset.
    pub product_code: Option<String>,
    /// The revision number of the software.
    pub serial_number: Option<String>,
    /// The revision number of the software.
    pub software_revision: Option<String>,
    /// Array of streams that are part of the asset. TODO: `StreamClient`
    pub streams: Vec<adr_models::Stream>, // if None, we can represent as empty vec
    ///  Globally unique, immutable, non-reusable id.
    pub uuid: Option<String>,
    /// The version of the asset.
    pub version: Option<u64>,
}

impl From<adr_models::AssetSpecification> for AssetSpecification {
    fn from(value: adr_models::AssetSpecification) -> Self {
        AssetSpecification {
            asset_type_refs: value.asset_type_refs,
            attributes: value.attributes,
            // datasets,
            default_datasets_configuration: value.default_datasets_configuration,
            default_datasets_destinations: value.default_datasets_destinations,
            default_events_configuration: value.default_events_configuration,
            default_events_destinations: value.default_events_destinations,
            default_management_groups_configuration: value.default_management_groups_configuration,
            default_streams_configuration: value.default_streams_configuration,
            default_streams_destinations: value.default_streams_destinations,
            description: value.description,
            device_ref: value.device_ref,
            discovered_asset_refs: value.discovered_asset_refs,
            display_name: value.display_name,
            documentation_uri: value.documentation_uri,
            enabled: value.enabled,
            events: value.events,
            external_asset_id: value.external_asset_id,
            hardware_revision: value.hardware_revision,
            last_transition_time: value.last_transition_time,
            management_groups: value.management_groups,
            manufacturer: value.manufacturer,
            manufacturer_uri: value.manufacturer_uri,
            model: value.model,
            product_code: value.product_code,
            serial_number: value.serial_number,
            software_revision: value.software_revision,
            streams: value.streams,
            uuid: value.uuid,
            version: value.version,
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
            // ValidationError shouldn't be possible since we should never have an empty asset name. It's not possible to be returned for device observe calls.
            // ShutdownError isn't possible for this fn to return
            unreachable!()
        }
    }
}
