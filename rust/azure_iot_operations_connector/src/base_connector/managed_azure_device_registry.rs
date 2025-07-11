// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

//! Types for Azure IoT Operations Connectors.

use std::{collections::HashMap, path::PathBuf, sync::Arc};

use azure_iot_operations_services::{
    azure_device_registry::{
        self,
        models::{self as adr_models},
    },
    schema_registry,
};
use chrono::{DateTime, Utc};
use thiserror::Error;
use tokio::sync::mpsc::{self, UnboundedReceiver, UnboundedSender};
use tokio_retry2::{Retry, RetryError};
use tokio_util::sync::CancellationToken;

use crate::{
    AdrConfigError, Data, DatasetRef, MessageSchema,
    base_connector::ConnectorContext,
    deployment_artifacts::{
        self,
        azure_device_registry::{AssetRef, DeviceEndpointRef},
    },
    destination_endpoint,
};

/// Used as the strategy when using [`tokio_retry2::Retry`]
const RETRY_STRATEGY: tokio_retry2::strategy::ExponentialFactorBackoff =
    tokio_retry2::strategy::ExponentialFactorBackoff::from_millis(500, 2.0);

/// Notifications that can be received for a Client
pub enum ClientNotification<T> {
    /// Indicates that the Client's specification has been updated in place
    Updated,
    /// Indicates that the Client has been deleted.
    Deleted,
    /// Indicates that there is a new `T` for this Client, which is included in the notification
    Created(T),
}

/// An Observation for device endpoint creation events that uses
/// multiple underlying clients to get full device endpoint information.
pub struct DeviceEndpointClientCreationObservation {
    connector_context: Arc<ConnectorContext>,
    device_endpoint_create_observation:
        deployment_artifacts::azure_device_registry::DeviceEndpointCreateObservation,
}
impl DeviceEndpointClientCreationObservation {
    /// Creates a new [`DeviceEndpointClientCreationObservation`] that uses the given [`ConnectorContext`]
    pub(crate) fn new(connector_context: Arc<ConnectorContext>) -> Self {
        // TODO: handle unwrap in a better way
        let device_endpoint_create_observation =
            deployment_artifacts::azure_device_registry::DeviceEndpointCreateObservation::new(
                connector_context.debounce_duration,
            )
            .unwrap();

        Self {
            connector_context,
            device_endpoint_create_observation,
        }
    }

    /// Receives a notification for a newly created device endpoint. This
    /// notification includes the [`DeviceEndpointClient`], which can be used
    /// to receive Assets related to this Device Endpoint
    ///
    /// # Panics
    /// If the `device_endpoint_create_observation` channel is closed, which should not be possible
    pub async fn recv_notification(&mut self) -> DeviceEndpointClient {
        loop {
            // Get the notification
            let (device_endpoint_ref, asset_create_observation) = self
                .device_endpoint_create_observation
                .recv_notification()
                .await.expect("Device Endpoint Create Observation should never return None because the device_endpoint_create_observation struct holds the sending side of the channel");

            // and then get device update observation as well
            let device_endpoint_update_observation =  match Retry::spawn(RETRY_STRATEGY.map(tokio_retry2::strategy::jitter), async || -> Result<azure_device_registry::DeviceUpdateObservation, RetryError<azure_device_registry::Error>> {
                self.connector_context
                    .azure_device_registry_client
                    .observe_device_update_notifications(
                        device_endpoint_ref.device_name.clone(),
                        device_endpoint_ref.inbound_endpoint_name.clone(),
                        self.connector_context.default_timeout,
                    )
                    // retry on network errors, otherwise don't retry on config/dev errors
                    .await
                    .map_err(|e| adr_error_into_retry_error(e, "Observe for Device Updates"))
            }).await {
                Ok(device_update_observation) => device_update_observation,
                Err(e) => {
                  log::error!("Dropping device endpoint create notification: {device_endpoint_ref:?}. Failed to observe for device update notifications after retries: {e}");
                  continue;
                },
            };

            // get the device definition
            let device = match Retry::spawn(
                RETRY_STRATEGY.map(tokio_retry2::strategy::jitter),
                async || -> Result<adr_models::Device, RetryError<azure_device_registry::Error>> {
                    self.connector_context
                        .azure_device_registry_client
                        .get_device(
                            device_endpoint_ref.device_name.clone(),
                            device_endpoint_ref.inbound_endpoint_name.clone(),
                            self.connector_context.default_timeout,
                        )
                        .await
                        .map_err(|e| adr_error_into_retry_error(e, "Get Device Definition"))
                },
            )
            .await
            {
                Ok(device) => device,
                Err(e) => {
                    log::error!(
                        "Dropping device endpoint create notification: {device_endpoint_ref:?}. Failed to get Device definition after retries: {e}"
                    );
                    // unobserve as cleanup
                    DeviceEndpointClient::unobserve_device(
                        &self.connector_context,
                        &device_endpoint_ref,
                    )
                    .await;
                    continue;
                }
            };

            // get the device status
            let device_status = match Retry::spawn(
                RETRY_STRATEGY.map(tokio_retry2::strategy::jitter),
                async || -> Result<adr_models::DeviceStatus, RetryError<azure_device_registry::Error>> {
                    self.connector_context
                        .azure_device_registry_client
                        .get_device_status(
                            device_endpoint_ref.device_name.clone(),
                            device_endpoint_ref.inbound_endpoint_name.clone(),
                            self.connector_context.default_timeout,
                        )
                        .await
                        .map_err(|e| adr_error_into_retry_error(e, "Get Device Status"))
                },
            )
            .await
            {
                Ok(device_status) => device_status,
                Err(e) => {
                    log::error!("Dropping device endpoint create notification: {device_endpoint_ref:?}. Failed to get Device Status after retries: {e}");
                    // unobserve as cleanup
                    DeviceEndpointClient::unobserve_device(&self.connector_context, &device_endpoint_ref).await;
                    continue;
                }
            };

            // turn the device definition into a DeviceEndpointClient
            return match DeviceEndpointClient::new(
                device,
                device_status,
                device_endpoint_ref.clone(),
                device_endpoint_update_observation,
                asset_create_observation,
                self.connector_context.clone(),
            ) {
                Ok(managed_device) => managed_device,
                Err(e) => {
                    // the device definition didn't include the inbound_endpoint, so it likely no longer exists
                    // TODO: This won't be a possible failure point in the future once the service returns errors
                    log::error!(
                        "Dropping device endpoint create notification: {device_endpoint_ref:?}. {e}"
                    );
                    // unobserve as cleanup
                    DeviceEndpointClient::unobserve_device(
                        &self.connector_context,
                        &device_endpoint_ref,
                    )
                    .await;
                    continue;
                }
            };
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
    specification: Arc<std::sync::RwLock<DeviceSpecification>>,
    /// The 'status' Field.
    #[getter(skip)]
    status: Arc<std::sync::RwLock<DeviceEndpointStatus>>,
    // Internally used fields
    /// The internal observation for updates
    #[getter(skip)]
    device_update_observation: azure_device_registry::DeviceUpdateObservation,
    #[getter(skip)]
    asset_create_observation: deployment_artifacts::azure_device_registry::AssetCreateObservation,
    #[getter(skip)]
    connector_context: Arc<ConnectorContext>,
}
impl DeviceEndpointClient {
    pub(crate) fn new(
        device: adr_models::Device,
        device_status: adr_models::DeviceStatus,
        device_endpoint_ref: DeviceEndpointRef,
        device_update_observation: azure_device_registry::DeviceUpdateObservation,
        asset_create_observation: deployment_artifacts::azure_device_registry::AssetCreateObservation,
        connector_context: Arc<ConnectorContext>,
        // TODO: This won't need to return an error once the service properly sends errors if the endpoint doesn't exist
    ) -> Result<Self, String> {
        Ok(DeviceEndpointClient {
            specification: Arc::new(std::sync::RwLock::new(DeviceSpecification::new(
                device,
                connector_context
                    .connector_artifacts
                    .device_endpoint_credentials_mount
                    .as_ref(),
                &device_endpoint_ref.inbound_endpoint_name,
            )?)),
            status: Arc::new(std::sync::RwLock::new(DeviceEndpointStatus::new(
                device_status,
                &device_endpoint_ref.inbound_endpoint_name,
            ))),
            device_endpoint_ref,
            device_update_observation,
            asset_create_observation,
            connector_context,
        })
    }

    /// Used to report the status of a device and endpoint together,
    /// and then updates the `self.status` with the new status returned
    ///
    /// # Errors
    /// [`azure_device_registry::Error`] of kind [`AIOProtocolError`](azure_device_registry::ErrorKind::AIOProtocolError) if
    /// there are any underlying errors from the AIO RPC protocol. This error will be retried
    /// 10 times with exponential backoff and jitter and only returned if it still is failing.
    ///
    /// [`azure_device_registry::Error`] of kind [`ServiceError`](azure_device_registry::ErrorKind::ServiceError) if an error is returned
    /// by the Azure Device Registry service.
    ///
    /// # Panics
    /// if the specification mutex has been poisoned, which should not be possible
    pub async fn report_status(
        &self,
        device_status: Result<(), AdrConfigError>,
        endpoint_status: Result<(), AdrConfigError>,
    ) -> Result<(), azure_device_registry::Error> {
        // Create status
        let version = self.specification.read().unwrap().version;
        let status = adr_models::DeviceStatus {
            config: Some(azure_device_registry::ConfigStatus {
                version,
                error: device_status.err(),
                last_transition_time: Some(chrono::Utc::now()),
            }),
            // inserts the inbound endpoint name with None if there's no error, or Some(AdrConfigError) if there is
            endpoints: HashMap::from([(
                self.device_endpoint_ref.inbound_endpoint_name.clone(),
                endpoint_status.err(),
            )]),
        };

        log::debug!(
            "Reporting device endpoint status from app for {:?}",
            self.device_endpoint_ref
        );
        // send status update to the service
        self.internal_report_status(status).await
    }

    /// Used to report the status of just the device,
    /// and then updates the [`DeviceEndpointClient`] with the new status returned
    ///
    /// # Errors
    /// [`azure_device_registry::Error`] of kind [`AIOProtocolError`](azure_device_registry::ErrorKind::AIOProtocolError) if
    /// there are any underlying errors from the AIO RPC protocol. This error will be retried
    /// 10 times with exponential backoff and jitter and only returned if it still is failing.
    ///
    /// [`azure_device_registry::Error`] of kind [`ServiceError`](azure_device_registry::ErrorKind::ServiceError) if an error is returned
    /// by the Azure Device Registry service.
    ///
    /// # Panics
    /// if the status or specification mutexes have been poisoned, which should not be possible
    pub async fn report_device_status(
        &self,
        device_status: Result<(), AdrConfigError>,
    ) -> Result<(), azure_device_registry::Error> {
        // Create status with maintained endpoint status
        let version = self.specification.read().unwrap().version;
        let current_endpoints = self
            .status
            .read()
            .unwrap()
            .adr_endpoints(version, &self.device_endpoint_ref.inbound_endpoint_name);
        let status = adr_models::DeviceStatus {
            config: Some(azure_device_registry::ConfigStatus {
                version,
                error: device_status.err(),
                last_transition_time: Some(chrono::Utc::now()),
            }),
            // Endpoints are merged on the service, so sending an empty map won't update anything
            endpoints: current_endpoints,
        };

        log::debug!(
            "Reporting device status from app for {:?}",
            self.device_endpoint_ref
        );
        // send status update to the service
        self.internal_report_status(status).await
    }

    /// Used to report the status of just the endpoint,
    /// and then updates the [`DeviceEndpointClient`] with the new status returned
    ///
    /// # Errors
    /// [`azure_device_registry::Error`] of kind [`AIOProtocolError`](azure_device_registry::ErrorKind::AIOProtocolError) if
    /// there are any underlying errors from the AIO RPC protocol. This error will be retried
    /// 10 times with exponential backoff and jitter and only returned if it still is failing.
    ///
    /// [`azure_device_registry::Error`] of kind [`ServiceError`](azure_device_registry::ErrorKind::ServiceError) if an error is returned
    /// by the Azure Device Registry service.
    ///
    /// # Panics
    /// if the status or specification mutexes have been poisoned, which should not be possible
    pub async fn report_endpoint_status(
        &self,
        endpoint_status: Result<(), AdrConfigError>,
    ) -> Result<(), azure_device_registry::Error> {
        // If the version of the current status config matches the current version, then include the existing config.
        // If there's no current config or the version doesn't match, don't report a status since the status for this version hasn't been reported yet
        let current_config = {
            let current_status = self.status.read().unwrap();
            if current_status
                .config
                .as_ref()
                .and_then(|config| config.version)
                == self.specification.read().unwrap().version
            {
                current_status.config.clone()
            } else {
                None
            }
        };
        // Create status without updating the device status
        let status = adr_models::DeviceStatus {
            config: current_config,
            // inserts the inbound endpoint name with None if there's no error, or Some(AdrConfigError) if there is
            endpoints: HashMap::from([(
                self.device_endpoint_ref.inbound_endpoint_name.clone(),
                endpoint_status.err(),
            )]),
        };

        log::debug!(
            "Reporting endpoint status from app for {:?}",
            self.device_endpoint_ref
        );
        // send status update to the service
        self.internal_report_status(status).await
    }

    /// Used to receive notifications related to the Device/Inbound Endpoint
    /// from the Azure Device Registry Service.
    ///
    /// Returns [`ClientNotification::Updated`] if the Device Endpoint
    /// Specification has been updated in place.
    ///
    /// Returns [`ClientNotification::Deleted`] if the Device Endpoint has been
    /// deleted. The [`DeviceEndpointClient`] should not be used after this
    /// point, and no more notifications will be received.
    ///
    /// Returns [`ClientNotification::Created`] with a new [`AssetClient`] if a new
    /// Asset has been created.
    ///
    /// # Panics
    /// If the Azure Device Registry Service provides a notification that isn't for this Device Endpoint. This should not be possible.
    ///
    /// If the specification mutex has been poisoned, which should not be possible
    pub async fn recv_notification(&mut self) -> ClientNotification<AssetClient> {
        loop {
            tokio::select! {
                biased;
                update = self.device_update_observation.recv_notification() => {
                    // handle the notification
                    // We set auto ack to true, so there's never an ack here to deal with. If we restart, then we'll implicitly
                    // get the update again because we'll pull the latest definition on the restart, so we don't need to get
                    // the notification again.
                    let Some((updated_device, _)) = update else {
                        // if the update notification is None, then the device endpoint has been deleted
                        // unobserve as cleanup
                        Self::unobserve_device(&self.connector_context, &self.device_endpoint_ref).await;
                        return ClientNotification::Deleted;
                    };
                    // update self with updated specification
                    let mut unlocked_specification = self.specification.write().unwrap(); // unwrap can't fail unless lock is poisoned
                    *unlocked_specification = DeviceSpecification::new(
                        updated_device,
                        self.connector_context
                            .connector_artifacts
                            .device_endpoint_credentials_mount
                            .as_ref(),
                        &self.device_endpoint_ref.inbound_endpoint_name,
                    ).expect("Device Update Notification should never provide a device that doesn't have the inbound endpoint");
                    return ClientNotification::Updated;
                },
                create_notification = self.asset_create_observation.recv_notification() => {
                    let Some((asset_ref, asset_deletion_token)) = create_notification else {
                        // if the create notification is None, then the device endpoint has been deleted
                        log::debug!("Device Endpoint Deletion detected, stopping device update observation for {:?}", self.device_endpoint_ref);
                        // unobserve as cleanup
                        Self::unobserve_device(&self.connector_context, &self.device_endpoint_ref).await;
                        return ClientNotification::Deleted;
                    };
                    // Get asset update observation as well
                    let asset_update_observation =  match Retry::spawn(
                        RETRY_STRATEGY.map(tokio_retry2::strategy::jitter),
                        async || -> Result<azure_device_registry::AssetUpdateObservation, RetryError<azure_device_registry::Error>> {
                            self.connector_context
                                .azure_device_registry_client
                                .observe_asset_update_notifications(
                                    asset_ref.device_name.clone(),
                                    asset_ref.inbound_endpoint_name.clone(),
                                    asset_ref.name.clone(),
                                    self.connector_context.default_timeout,
                                )
                                // retry on network errors, otherwise don't retry on config/dev errors
                                .await
                                .map_err(|e| adr_error_into_retry_error(e, "Observe for Asset Updates"))
                    }).await {
                        Ok(asset_update_observation) => asset_update_observation,
                        Err(e) => {
                            log::error!("Dropping asset create notification: {asset_ref:?}. Failed to observe for asset update notifications after retries: {e}");
                            continue;
                        },
                    };

                    // get the asset definition
                    let asset_client = match Retry::spawn(
                        RETRY_STRATEGY.map(tokio_retry2::strategy::jitter),
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
                                .map_err(|e| adr_error_into_retry_error(e, "Get Asset Definition"))
                        },
                    )
                    .await
                    {
                        Ok(asset) => {
                            // get the asset status
                            match Retry::spawn(
                                RETRY_STRATEGY.map(tokio_retry2::strategy::jitter),
                                async || -> Result<adr_models::AssetStatus, RetryError<azure_device_registry::Error>> {
                                    self.connector_context
                                        .azure_device_registry_client
                                        .get_asset_status(
                                            asset_ref.device_name.clone(),
                                            asset_ref.inbound_endpoint_name.clone(),
                                            asset_ref.name.clone(),
                                            self.connector_context.default_timeout,
                                        )
                                        .await
                                        .map_err(|e| adr_error_into_retry_error(e, "Get Asset Status"))
                                },
                            )
                            .await {
                                Ok(asset_status) => {
                                    AssetClient::new(
                                        asset,
                                        asset_status,
                                        asset_ref,
                                        self.specification.clone(),
                                        self.status.clone(),
                                        asset_update_observation,
                                        asset_deletion_token,
                                        self.connector_context.clone(),
                                    )
                                    .await
                                },
                                Err(e) => {
                                    log::error!("Dropping asset create notification: {asset_ref:?}. Failed to get Asset status after retries: {e}");
                                    // unobserve as cleanup
                                    AssetClient::unobserve_asset(&self.connector_context, &asset_ref).await;
                                    continue;
                                },
                            }
                        }
                        Err(e) => {
                            log::error!("Dropping asset create notification: {asset_ref:?}. Failed to get Asset definition after retries: {e}");
                            // unobserve as cleanup
                            AssetClient::unobserve_asset(&self.connector_context, &asset_ref).await;
                            continue;
                        }
                    };
                    return ClientNotification::Created(asset_client);
                }
            }
        }
    }

    // Returns a clone of the current device status
    /// # Panics
    /// if the status mutex has been poisoned, which should not be possible
    #[must_use]
    pub fn status(&self) -> DeviceEndpointStatus {
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
    async fn internal_report_status(
        &self,
        adr_device_status: adr_models::DeviceStatus,
    ) -> Result<(), azure_device_registry::Error> {
        // send status update to the service
        let updated_device_status = Retry::spawn(
            RETRY_STRATEGY.map(tokio_retry2::strategy::jitter).take(10),
            async || -> Result<adr_models::DeviceStatus, RetryError<azure_device_registry::Error>> {
                self.connector_context
                    .azure_device_registry_client
                    .update_device_plus_endpoint_status(
                        self.device_endpoint_ref.device_name.clone(),
                        self.device_endpoint_ref.inbound_endpoint_name.clone(),
                        adr_device_status.clone(),
                        self.connector_context.default_timeout,
                    )
                    .await
                    .map_err(|e| adr_error_into_retry_error(e, "Update Device Status"))
            },
        )
        .await?;
        // update self with new returned status
        let mut unlocked_status = self.status.write().unwrap(); // unwrap can't fail unless lock is poisoned
        *unlocked_status = DeviceEndpointStatus::new(
            updated_device_status,
            &self.device_endpoint_ref.inbound_endpoint_name,
        );
        Ok(())
    }

    /// Internal convenience function to unobserve from a device's update notifications for cleanup
    async fn unobserve_device(
        connector_context: &Arc<ConnectorContext>,
        device_endpoint_ref: &DeviceEndpointRef,
    ) {
        let _ = Retry::spawn(
            RETRY_STRATEGY.map(tokio_retry2::strategy::jitter),
            async || -> Result<(), RetryError<azure_device_registry::Error>> {
                connector_context
                    .azure_device_registry_client
                    .unobserve_device_update_notifications(
                        device_endpoint_ref.device_name.clone(),
                        device_endpoint_ref.inbound_endpoint_name.clone(),
                        connector_context.default_timeout,
                    )
                    // retry on network errors, otherwise don't retry on config/dev errors
                    .await
                    .map_err(|e| adr_error_into_retry_error(e, "Unobserve for Device Updates"))
            },
        )
        .await
        .inspect_err(|e| {
            log::error!("Failed to unobserve device update notifications for {device_endpoint_ref:?} after retries: {e}");
        });
    }
}

/// Azure Device Registry Asset that includes additional functionality
/// to report status and receive Asset updates
#[derive(Debug, Getters)]
pub struct AssetClient {
    /// Asset, device, and inbound endpoint names
    asset_ref: AssetRef,
    /// Specification for the Asset
    #[getter(skip)]
    specification: Arc<std::sync::RwLock<AssetSpecification>>,
    /// Status for the Asset
    #[getter(skip)]
    status: Arc<tokio::sync::RwLock<adr_models::AssetStatus>>,
    /// Specification of the device that this Asset is tied to
    #[getter(skip)]
    device_specification: Arc<std::sync::RwLock<DeviceSpecification>>,
    /// Status of the device that this Asset is tied to
    #[getter(skip)]
    device_status: Arc<std::sync::RwLock<DeviceEndpointStatus>>,
    // Internally used fields
    /// Internal `CancellationToken` for when the Asset is deleted. Surfaced to the user through the receive update flow
    #[getter(skip)]
    asset_deletion_token: CancellationToken,
    /// The internal observation for updates
    #[getter(skip)]
    asset_update_observation: azure_device_registry::AssetUpdateObservation,
    /// Internal sender for when new datasets are created
    #[getter(skip)]
    dataset_creation_tx: UnboundedSender<DatasetClient>,
    /// Internal channel for receiving notifications about dataset creation events.
    #[getter(skip)]
    dataset_creation_rx: UnboundedReceiver<DatasetClient>,
    /// Internal watch sender for releasing dataset create/update notifications
    #[getter(skip)]
    release_dataset_notifications_tx: tokio::sync::watch::Sender<()>,
    /// hashmap of current dataset names to their current definition and a sender to send dataset updates
    #[getter(skip)]
    dataset_hashmap: HashMap<
        String,
        (
            adr_models::Dataset,
            UnboundedSender<DatasetUpdateNotification>,
        ),
    >,
    #[getter(skip)]
    connector_context: Arc<ConnectorContext>,
}

impl AssetClient {
    #[allow(clippy::too_many_arguments)]
    pub(crate) async fn new(
        asset: adr_models::Asset,
        asset_status: adr_models::AssetStatus,
        asset_ref: AssetRef,
        device_specification: Arc<std::sync::RwLock<DeviceSpecification>>,
        device_status: Arc<std::sync::RwLock<DeviceEndpointStatus>>,
        asset_update_observation: azure_device_registry::AssetUpdateObservation,
        asset_deletion_token: CancellationToken,
        connector_context: Arc<ConnectorContext>,
    ) -> Self {
        let dataset_definitions = asset.datasets.clone();
        let specification = AssetSpecification::from(asset);
        let specification_version = specification.version;
        let (dataset_creation_tx, dataset_creation_rx) = mpsc::unbounded_channel();

        // Create the AssetClient so that we can use the same helper functions for processing the datasets as we do during the update flow
        let mut asset_client = AssetClient {
            asset_ref,
            specification: Arc::new(std::sync::RwLock::new(specification)),
            status: Arc::new(tokio::sync::RwLock::new(asset_status)),
            device_specification,
            device_status,
            asset_update_observation,
            dataset_creation_tx,
            dataset_creation_rx,
            dataset_hashmap: HashMap::new(),
            connector_context,
            release_dataset_notifications_tx: tokio::sync::watch::Sender::new(()),
            asset_deletion_token,
        };

        {
            // lock the status write guard so that no other threads can modify the status while we update it
            // (not possible in new, but allows use of Self:: helper fns)
            let mut status_write_guard = asset_client.status.write().await;
            // if there are any config errors when parsing the asset, collect them all so we can report them at once
            let mut new_status: adr_models::AssetStatus =
                Self::current_status_to_modify(&status_write_guard, specification_version);
            let mut status_updated = false;

            // Create the default dataset destinations from the asset definition
            let default_dataset_destinations: Vec<Arc<destination_endpoint::Destination>> =
                match destination_endpoint::Destination::new_dataset_destinations(
                    &asset_client
                        .specification
                        .read()
                        .unwrap()
                        .default_datasets_destinations,
                    &asset_client.asset_ref.inbound_endpoint_name,
                    &asset_client.connector_context,
                ) {
                    Ok(res) => res.into_iter().map(Arc::new).collect(),
                    Err(e) => {
                        log::error!(
                            "Invalid default dataset destination for Asset {:?}: {e:?}",
                            asset_client.asset_ref
                        );
                        // Add this to the status to be reported to ADR
                        new_status.config = Some(azure_device_registry::ConfigStatus {
                            version: specification_version,
                            error: Some(e),
                            last_transition_time: Some(chrono::Utc::now()),
                        });
                        status_updated = true;
                        // set this to None because if all datasets have a destination specified, this might not cause the asset to be unusable
                        vec![]
                    }
                };

            // create the DatasetClients for each dataset in the definition, add them
            // to our tracking for handling updates, and send the create notification
            // to the dataset creation observation
            for dataset_definition in dataset_definitions {
                let (dataset_update_tx, dataset_update_rx) = mpsc::unbounded_channel();
                match DatasetClient::new(
                    dataset_definition.clone(),
                    dataset_update_rx,
                    &default_dataset_destinations,
                    asset_client.asset_ref.clone(),
                    asset_client.status.clone(),
                    asset_client.specification.clone(),
                    asset_client.device_specification.clone(),
                    asset_client.device_status.clone(),
                    asset_client.connector_context.clone(),
                ) {
                    Ok(new_dataset_client) => {
                        // insert the dataset client into the hashmap so we can handle updates
                        asset_client.dataset_hashmap.insert(
                            dataset_definition.name.clone(),
                            (dataset_definition, dataset_update_tx),
                        );

                        // error is not possible since the receiving side of the channel is owned by the AssetClient
                        let _ = asset_client.dataset_creation_tx.send(new_dataset_client);
                    }
                    Err(e) => {
                        // Add the error to the status to be reported to ADR, and then continue to process
                        // other datasets even if one isn't valid. Don't give this one to
                        // the application since we can't forward data on it. If there's an update to the
                        // definition, they'll get the create notification for it at that point if it's valid
                        DatasetClient::update_dataset_status(
                            &mut new_status,
                            &dataset_definition.name,
                            Err(e),
                        );
                        status_updated = true;
                    }
                };
            }

            // if there were any config errors, report them to the ADR service together
            if status_updated {
                log::debug!(
                    "Reporting error asset status on new for {:?}",
                    asset_client.asset_ref
                );
                if let Err(e) = Self::internal_report_status(
                    new_status,
                    &asset_client.connector_context,
                    &asset_client.asset_ref,
                    &mut status_write_guard,
                    "AssetClient::new",
                )
                .await
                {
                    log::error!(
                        "Failed to report error Asset status for new Asset {:?}: {e}",
                        asset_client.asset_ref
                    );
                }
            }
        }

        asset_client
    }

    /// Used to report the status of an Asset,
    /// and then updates the `self.status` with the new status returned
    ///
    /// # Errors
    /// [`azure_device_registry::Error`] of kind [`AIOProtocolError`](azure_device_registry::ErrorKind::AIOProtocolError) if
    /// there are any underlying errors from the AIO RPC protocol. This error will be retried
    /// 10 times with exponential backoff and jitter and only returned if it still is failing.
    ///
    /// [`azure_device_registry::Error`] of kind [`ServiceError`](azure_device_registry::ErrorKind::ServiceError) if an error is returned
    /// by the Azure Device Registry service.
    ///
    /// # Panics
    /// if the specification mutex has been poisoned, which should not be possible
    pub async fn report_status(
        &self,
        status: Result<(), AdrConfigError>,
    ) -> Result<(), azure_device_registry::Error> {
        let mut status_write_guard = self.status.write().await;
        let version = self.specification.read().unwrap().version;
        // get current or cleared (if it's out of date) asset status as our base to modify only what we're explicitly trying to set
        let mut new_status = Self::current_status_to_modify(&status_write_guard, version);
        // no matter whether we kept other fields or not, we will always fully replace the config status
        new_status.config = Some(azure_device_registry::ConfigStatus {
            version,
            error: status.err(),
            last_transition_time: Some(chrono::Utc::now()),
        });

        log::debug!("Reporting asset status from app for {:?}", self.asset_ref);
        // send status update to the service
        Self::internal_report_status(
            new_status,
            &self.connector_context,
            &self.asset_ref,
            &mut status_write_guard,
            "AssetClient::report_status",
        )
        .await
    }

    /// Used to receive notifications related to the Asset from the Azure Device
    /// Registry Service.
    ///
    /// Returns [`ClientNotification::Updated`] if the Asset Specification has
    /// been updated in place.
    ///
    /// Returns [`ClientNotification::Deleted`] if the Asset has been deleted.
    /// The [`AssetClient`] should not be used after this point, and no more
    /// notifications will be received.
    ///
    /// Returns [`ClientNotification::Created`] with a new [`DatasetClient`] if a
    /// new Dataset has been created.
    ///
    /// Receiving an update will also trigger update/deletion notifications for datasets that
    /// are linked to this asset. To ensure the asset update is received before dataset notifications,
    /// dataset notifications won't be released until this function is polled again after receiving an
    /// update.
    ///
    /// # Panics
    /// If the specification mutex has been poisoned, which should not be possible
    pub async fn recv_notification(&mut self) -> ClientNotification<DatasetClient> {
        // release any pending dataset create/update notifications
        self.release_dataset_notifications_tx.send_modify(|()| ());
        tokio::select! {
            biased;
            () = self.asset_deletion_token.cancelled() => {
                log::debug!("Asset deletion token received, stopping asset update observation for {:?}", self.asset_ref);
                // unobserve as cleanup
                Self::unobserve_asset(&self.connector_context, &self.asset_ref).await;
                ClientNotification::Deleted
            },
            notification = self.asset_update_observation.recv_notification() => {
                // handle the notification
                // We set auto ack to true, so there's never an ack here to deal with. If we restart, then we'll implicitly
                // get the update again because we'll pull the latest definition on the restart, so we don't need to get
                // the notification again as an MQTT message.
                let Some((updated_asset, _)) = notification else {
                    // unobserve as cleanup
                    Self::unobserve_asset(&self.connector_context, &self.asset_ref).await;
                    return ClientNotification::Deleted;
                };

                // lock the status write guard so that no other threads can modify the status while we update it
                let mut status_write_guard = self.status.write().await;
                // if there are any config errors when parsing the asset, collect them all so we can report them at once
                let mut new_status: adr_models::AssetStatus = Self::current_status_to_modify(&status_write_guard, updated_asset.version);
                let mut status_updated = false;

                // update datasets
                // remove the datasets that are no longer present in the new asset definition.
                // This triggers deletion notification since this drops the update sender.
                self.dataset_hashmap.retain(|dataset_name, _| {
                    updated_asset
                        .datasets
                        .iter()
                        .any(|dataset| dataset.name == *dataset_name)
                });

                // Get the new default dataset destination and track whether it's different or not from the current one
                let default_dataset_destination_updated = updated_asset.default_datasets_destinations
                    != self
                        .specification
                        .read()
                        .unwrap()
                        .default_datasets_destinations;
                let default_dataset_destinations =
                    match destination_endpoint::Destination::new_dataset_destinations(
                        &updated_asset.default_datasets_destinations,
                        &self.asset_ref.inbound_endpoint_name,
                        &self.connector_context,
                    ) {
                        Ok(res) => res.into_iter().map(Arc::new).collect(),
                        Err(e) => {
                            log::error!(
                                "Invalid default dataset destination for Asset {:?}: {e:?}",
                                self.asset_ref
                            );
                            // Add this to the status to be reported to ADR
                            new_status.config = Some(azure_device_registry::ConfigStatus {
                                version: updated_asset.version,
                                error: Some(e),
                                last_transition_time: Some(chrono::Utc::now()),
                            });
                            status_updated = true;
                            // set this to None because if all datasets have a destination specified, this might not cause the asset to be unusable
                            vec![]
                        }
                    };

                // For all received datasets, check if the existing dataset needs an update or if a new one needs to be created
                for received_dataset_definition in &updated_asset.datasets {
                    // it already exists
                    if let Some((dataset_definition, dataset_update_tx)) = self
                        .dataset_hashmap
                        .get_mut(&received_dataset_definition.name)
                    {
                        // if the default destination has changed, update all datasets. TODO: might be able to track whether a dataset uses a default to reduce updates needed here
                        // otherwise, only send an update if the dataset definition has changed
                        if default_dataset_destination_updated
                            || received_dataset_definition != dataset_definition
                        {
                            // we need to make sure we have the updated definition for comparing next time
                            *dataset_definition = received_dataset_definition.clone();
                            // send update to the dataset
                            let _ = dataset_update_tx
                                .send((
                                    received_dataset_definition.clone(),
                                    default_dataset_destinations.clone(),
                                    self.release_dataset_notifications_tx.subscribe(),
                                )).inspect_err(|tokio::sync::mpsc::error::SendError((e_dataset_definition, _,_))| {
                                    // TODO: should this trigger the datasetClient create flow, or is this just indicative of an application bug?
                                    log::warn!(
                                        "Update received for dataset {} on asset {:?}, but DatasetClient has been dropped",
                                        e_dataset_definition.name,
                                        self.asset_ref
                                    );
                                });
                        } else {
                            // TODO: copy over the existing dataset status for a new status report? (other bug)
                        }
                    }
                    // it needs to be created
                    else {
                        let (dataset_update_tx, dataset_update_rx) = mpsc::unbounded_channel();
                        match DatasetClient::new(
                            received_dataset_definition.clone(),
                            dataset_update_rx,
                            &default_dataset_destinations,
                            self.asset_ref.clone(),
                            self.status.clone(),
                            self.specification.clone(),
                            self.device_specification.clone(),
                            self.device_status.clone(),
                            self.connector_context.clone(),
                        ) {
                            Ok(new_dataset_client) => {
                                // insert the dataset client into the hashmap so we can handle updates
                                self.dataset_hashmap.insert(
                                    received_dataset_definition.name.clone(),
                                    (received_dataset_definition.clone(), dataset_update_tx),
                                );

                                // error is not possible since the receiving side of the channel is owned by the AssetClient
                                let _ = self.dataset_creation_tx.send(new_dataset_client);
                            },
                            Err(e) => {
                                // Add the error to the status to be reported to ADR, and then continue to process
                                // other datasets even if one isn't valid. Don't give this one to
                                // the application since we can't forward data on it. If there's an update to the
                                // definition, they'll get the create notification for it at that point if it's valid
                                DatasetClient::update_dataset_status(&mut new_status, &received_dataset_definition.name, Err(e));
                                status_updated = true;
                            },
                        };
                    }
                }

                // if there were any config errors, report them to the ADR service together
                if status_updated {
                    log::debug!("Reporting error asset status on recv_notification for {:?}", self.asset_ref);
                    if let Err(e) = Self::internal_report_status(
                        new_status,
                        &self.connector_context,
                        &self.asset_ref,
                        &mut status_write_guard,
                        "AssetClient::recv_notification",
                    )
                    .await {
                        log::error!("Failed to report error Asset status for updated Asset {:?}: {e}", self.asset_ref);
                    }
                }

                // update specification
                let mut unlocked_specification = self.specification.write().unwrap(); // unwrap can't fail unless lock is poisoned
                *unlocked_specification = AssetSpecification::from(updated_asset);

                ClientNotification::Updated
            },
            create_notification = self.dataset_creation_rx.recv() => {
                let Some(dataset_client) = create_notification else {
                    // unobserve as cleanup
                    Self::unobserve_asset(&self.connector_context, &self.asset_ref).await;
                    return ClientNotification::Deleted;
                };
                ClientNotification::Created(dataset_client)
            },
        }
    }

    // Returns a clone of the current asset specification
    /// # Panics
    /// if the specification mutex has been poisoned, which should not be possible
    #[must_use]
    pub fn specification(&self) -> AssetSpecification {
        (*self.specification.read().unwrap()).clone()
    }

    /// Returns a clone of the current asset status
    /// Note that this is the value of the last reported status (or the status
    /// from when the asset was first received if one hasn't been reported yet),
    /// so it is possible that this is not the latest value that the ADR service has
    #[must_use]
    pub async fn status(&self) -> adr_models::AssetStatus {
        (*self.status.read().await).clone()
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
    pub fn device_status(&self) -> DeviceEndpointStatus {
        (*self.device_status.read().unwrap()).clone()
    }

    /// Internal helper to get an `adr_models::AssetStatus` that can be used as a starting place
    /// to modify the current status with whatever new things we want to report
    ///
    /// If the config is present and the version doesn't match, clear everything, since it's all from an old version.
    /// Otherwise, if the config status doesn't exist, or the version matches, keep everything.
    /// Then, the caller of this function can modify only what they are explicitly reporting, and all other fields will
    /// be either maintained or will be their default values if this is the first time reporting for a new version.
    fn current_status_to_modify(
        current_status: &adr_models::AssetStatus,
        version: Option<u64>,
    ) -> adr_models::AssetStatus {
        if let Some(config) = &current_status.config {
            // version matches
            if config.version == version {
                current_status.clone()
            } else {
                // config out of date, clear everything
                adr_models::AssetStatus::default()
            }
        } else {
            // config not reported, assume anything reported so far is for this version
            current_status.clone()
        }
    }

    pub(crate) async fn internal_report_status(
        adr_asset_status: adr_models::AssetStatus,
        connector_context: &ConnectorContext,
        asset_ref: &AssetRef,
        asset_status_ref: &mut adr_models::AssetStatus,
        log_identifier: &str,
    ) -> Result<(), azure_device_registry::Error> {
        // send status update to the service
        let updated_asset_status = Retry::spawn(
            RETRY_STRATEGY.map(tokio_retry2::strategy::jitter).take(10),
            async || -> Result<adr_models::AssetStatus, RetryError<azure_device_registry::Error>> {
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
                    .map_err(|e| adr_error_into_retry_error(e, &format!("Update Asset Status for {log_identifier}")))
            },
        )
        .await?;
        // update self with new returned status
        *asset_status_ref = updated_asset_status;
        Ok(())
    }

    /// Internal convenience function to unobserve from an asset's update notifications for cleanup
    async fn unobserve_asset(connector_context: &Arc<ConnectorContext>, asset_ref: &AssetRef) {
        // unobserve as cleanup
        let _ = Retry::spawn(
            RETRY_STRATEGY.map(tokio_retry2::strategy::jitter),
            async || -> Result<(), RetryError<azure_device_registry::Error>> {
                connector_context
                    .azure_device_registry_client
                    .unobserve_asset_update_notifications(
                        asset_ref.device_name.clone(),
                        asset_ref.inbound_endpoint_name.clone(),
                        asset_ref.name.clone(),
                        connector_context.default_timeout,
                    )
                    // retry on network errors, otherwise don't retry on config/dev errors
                    .await
                    .map_err(|e| adr_error_into_retry_error(e, "Unobserve for Asset Updates"))
            },
        )
        .await
        .inspect_err(|e| {
            log::error!("Failed to unobserve asset update notifications for {asset_ref:?} after retries: {e}");
        });
    }
}

/// Errors that can be returned when reporting a message schema for a dataset
#[derive(Error, Debug)]
pub enum MessageSchemaError {
    /// An error occurred while putting the Schema in the Schema Registry
    #[error(transparent)]
    PutSchemaError(#[from] schema_registry::Error),
    /// An error occurred while reporting the Schema to the Azure Device Registry Service.
    #[error(transparent)]
    AzureDeviceRegistryError(#[from] azure_device_registry::Error),
}

type DatasetUpdateNotification = (
    adr_models::Dataset,                         // new Dataset definition
    Vec<Arc<destination_endpoint::Destination>>, // new default dataset destinations
    tokio::sync::watch::Receiver<()>, // watch receiver for when the update notification should be released to the application
);

/// Notifications that can be received for a Dataset
pub enum DatasetNotification {
    /// Indicates that the Datasets's definition has been updated in place
    Updated,
    /// Indicates that the Dataset has been deleted.
    Deleted,
    /// Indicates that the Dataset received an update, but the update was not valid.
    /// The definition is still updated in place, but the dataset should not be used until
    /// there is a new update, otherwise the out of date definition will be used for
    /// sending data to the destination.
    UpdatedInvalid,
}

/// Azure Device Registry Dataset that includes additional functionality
/// to report status, report message schema, receive Dataset updates,
/// and send data to the destination
#[derive(Debug, Getters)]
pub struct DatasetClient {
    /// Dataset, asset, device, and inbound endpoint names
    dataset_ref: DatasetRef,
    /// Dataset Definition
    dataset_definition: adr_models::Dataset,
    /// Current status for the Asset
    #[getter(skip)]
    asset_status: Arc<tokio::sync::RwLock<adr_models::AssetStatus>>,
    /// Current specification for the Asset
    #[getter(skip)]
    asset_specification: Arc<std::sync::RwLock<AssetSpecification>>,
    /// Specification of the device that this dataset is tied to
    #[getter(skip)]
    device_specification: Arc<std::sync::RwLock<DeviceSpecification>>,
    /// Status of the device that this dataset is tied to
    #[getter(skip)]
    device_status: Arc<std::sync::RwLock<DeviceEndpointStatus>>,
    // Internally used fields
    /// Internal [`Forwarder`] that handles forwarding data to the destination defined in the dataset definition
    #[getter(skip)]
    forwarder: destination_endpoint::Forwarder,
    #[getter(skip)]
    connector_context: Arc<ConnectorContext>,
    /// Asset reference for internal use
    #[getter(skip)]
    asset_ref: AssetRef,
    /// Internal channel for receiving notifications about dataset updates.
    #[getter(skip)]
    dataset_update_rx: UnboundedReceiver<DatasetUpdateNotification>,
}

impl DatasetClient {
    #[allow(clippy::too_many_arguments)]
    pub(crate) fn new(
        dataset_definition: adr_models::Dataset,
        dataset_update_rx: UnboundedReceiver<DatasetUpdateNotification>,
        default_destinations: &[Arc<destination_endpoint::Destination>],
        asset_ref: AssetRef,
        asset_status: Arc<tokio::sync::RwLock<adr_models::AssetStatus>>,
        asset_specification: Arc<std::sync::RwLock<AssetSpecification>>,
        device_specification: Arc<std::sync::RwLock<DeviceSpecification>>,
        device_status: Arc<std::sync::RwLock<DeviceEndpointStatus>>,
        connector_context: Arc<ConnectorContext>,
    ) -> Result<Self, AdrConfigError> {
        // Create a new dataset
        let forwarder = destination_endpoint::Forwarder::new_dataset_forwarder(
            &dataset_definition.destinations,
            &asset_ref.inbound_endpoint_name,
            default_destinations,
            connector_context.clone(),
        )?;
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
            dataset_update_rx,
            connector_context,
        })
    }

    /// Used to report the status of a dataset
    ///
    /// # Errors
    /// [`azure_device_registry::Error`] of kind [`AIOProtocolError`](azure_device_registry::ErrorKind::AIOProtocolError) if
    /// there are any underlying errors from the AIO RPC protocol. This error will be retried
    /// 10 times with exponential backoff and jitter and only returned if it still is failing.
    ///
    /// [`azure_device_registry::Error`] of kind [`ServiceError`](azure_device_registry::ErrorKind::ServiceError) if an error is returned
    /// by the Azure Device Registry service.
    ///
    /// # Panics
    /// if the asset specification mutex has been poisoned, which should not be possible
    pub async fn report_status(
        &self,
        status: Result<(), AdrConfigError>,
    ) -> Result<(), azure_device_registry::Error> {
        // get current or cleared (if it's out of date) asset status as our base to modify only what we're explicitly trying to set
        let mut status_write_guard = self.asset_status.write().await;
        let mut new_status = AssetClient::current_status_to_modify(
            &status_write_guard,
            self.asset_specification.read().unwrap().version,
        );

        // if dataset is already in the current status, then update the existing dataset with the new error
        // Otherwise if the dataset isn't present, or no datasets have been reported yet, then add it with the new error
        Self::update_dataset_status(&mut new_status, &self.dataset_ref.dataset_name, status);

        // send status update to the service
        log::debug!("reporting dataset {:?} status from app", self.dataset_ref);
        AssetClient::internal_report_status(
            new_status,
            &self.connector_context,
            &self.asset_ref,
            &mut status_write_guard,
            "DatasetClient::report_status",
        )
        .await
    }

    /// Used to report the message schema of a dataset
    ///
    /// # Errors
    /// [`MessageSchemaError`] of kind [`SchemaRegistryError::InvalidArgument`](schema_registry::ErrorKind::InvalidArgument)
    /// if the content of the [`MessageSchema`] is empty or there is an error building the request
    ///
    /// [`MessageSchemaError`] of kind [`SchemaRegistryError::ServiceError`](schema_registry::ErrorKind::ServiceError)
    /// if there is an error returned by the Schema Registry Service.
    ///
    /// [`MessageSchemaError`] of kind [`AzureDeviceRegistryError::AIOProtocolError`](azure_device_registry::ErrorKind::AIOProtocolError) if
    /// there are any underlying errors from the AIO RPC protocol. This error will be retried
    /// 10 times with exponential backoff and jitter and only returned if it still is failing.
    ///
    /// [`MessageSchemaError`] of kind [`AzureDeviceRegistryError::ServiceError`](azure_device_registry::ErrorKind::ServiceError) if an error is returned
    /// by the Azure Device Registry service.
    ///
    /// # Panics
    /// If the Schema Registry Service returns a schema without required values. This should get updated
    /// to be validated by the Schema Registry API surface in the future
    ///
    /// If the asset specification mutex has been poisoned, which should not be possible
    pub async fn report_message_schema(
        &mut self,
        message_schema: MessageSchema,
    ) -> Result<adr_models::MessageSchemaReference, MessageSchemaError> {
        // TODO: save message schema provided with message schema uri so it can be compared
        // send message schema to schema registry service
        let message_schema_reference = Retry::spawn(
            RETRY_STRATEGY.map(tokio_retry2::strategy::jitter),
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
                                log::warn!(
                                    "Reporting message schema failed for {:?}. Retrying: {e}",
                                    self.dataset_ref
                                );
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

        // get current or cleared (if it's out of date) asset status as our base to modify only what we're explicitly trying to set
        let mut status_write_guard = self.asset_status.write().await;
        let mut new_status = AssetClient::current_status_to_modify(
            &status_write_guard,
            self.asset_specification.read().unwrap().version,
        );

        // if dataset is already in the current status, then update the existing dataset with the new message schema
        // Otherwise if the dataset isn't present, or no datasets have been reported yet, then add it with the new message schema
        if let Some(dataset_status) = new_status.datasets.as_mut().and_then(|datasets| {
            datasets
                .iter_mut()
                .find(|dataset| dataset.name == self.dataset_ref.dataset_name)
        }) {
            // If the dataset already has a status, update the existing dataset with the new message schema
            dataset_status.message_schema_reference = Some(message_schema_reference.clone());
        } else {
            // If the dataset doesn't exist in the current status, then add it
            new_status.datasets.get_or_insert_with(Vec::new).push(
                adr_models::DatasetEventStreamStatus {
                    name: self.dataset_ref.dataset_name.clone(),
                    message_schema_reference: Some(message_schema_reference.clone()),
                    error: None,
                },
            );
        }

        // send status update to the service
        log::debug!(
            "reporting dataset {:?} message schema from app",
            self.dataset_ref
        );
        AssetClient::internal_report_status(
            new_status,
            &self.connector_context,
            &self.asset_ref,
            &mut status_write_guard,
            "DatasetClient::report_message_schema",
        )
        .await?;

        self.forwarder
            .update_message_schema_reference(Some(message_schema_reference.clone()));

        Ok(message_schema_reference)
    }

    /// Used to send transformed data to the destination
    /// Returns once the message has been sent successfully
    ///
    /// # Errors
    /// [`destination_endpoint::Error`] of kind [`MissingMessageSchema`](destination_endpoint::ErrorKind::MissingMessageSchema)
    /// if the [`MessageSchema`] has not been reported yet. This is required before forwarding any data
    ///
    /// [`destination_endpoint::Error`] of kind [`DataValidationError`](destination_endpoint::ErrorKind::MqttTelemetryError)
    /// if the [`Data`] isn't valid.
    ///
    /// [`destination_endpoint::Error`] of kind [`BrokerStateStoreError`](destination_endpoint::ErrorKind::BrokerStateStoreError)
    /// if the destination is `BrokerStateStore` and there are any errors setting the data with the service
    ///
    /// [`destination_endpoint::Error`] of kind [`MqttTelemetryError`](destination_endpoint::ErrorKind::MqttTelemetryError)
    /// if the destination is `Mqtt` and there are any errors sending the message to the broker
    pub async fn forward_data(&self, data: Data) -> Result<(), destination_endpoint::Error> {
        self.forwarder.send_data(data).await
    }

    /// Used to receive notifications about the Dataset from the Azure Device Registry Service.
    ///
    /// Returns [`DatasetNotification::Updated`] if the Dataset's definition has been updated in place.
    ///
    /// Returns [`DatasetNotification::UpdatedInvalid`] if the Dataset received an update, but the update was not valid.
    /// The definition is still updated in place, but the dataset should not be used until
    /// there is a new update, otherwise the out of date definition will be used for
    /// sending data to the destination.
    ///
    /// Returns [`DatasetNotification::Deleted`] if the Dataset has been deleted. The [`DatasetClient`]
    /// should not be used after this point, and no more notifications will be received.
    pub async fn recv_notification(&mut self) -> DatasetNotification {
        let Some((updated_dataset, default_destinations, mut watch_receiver)) =
            self.dataset_update_rx.recv().await
        else {
            return DatasetNotification::Deleted;
        };
        // wait until the update has been released. If the watch sender has been dropped, this means the Asset has been deleted/dropped
        if watch_receiver.changed().await.is_err() {
            return DatasetNotification::Deleted;
        }
        // create new forwarder, in case destination has changed
        self.forwarder = match destination_endpoint::Forwarder::new_dataset_forwarder(
            &updated_dataset.destinations,
            &self.asset_ref.inbound_endpoint_name,
            &default_destinations,
            self.connector_context.clone(),
        ) {
            Ok(forwarder) => forwarder,
            Err(e) => {
                log::error!(
                    "Invalid dataset destination for updated dataset: {:?} {e:?}",
                    self.dataset_ref
                );

                if let Err(e) = self.report_status(Err(e)).await {
                    log::error!(
                        "Failed to report status for updated dataset {:?}: {e}",
                        self.dataset_ref
                    );
                }
                // notify the application to not use this dataset until a new update is received
                self.dataset_definition = updated_dataset;
                return DatasetNotification::UpdatedInvalid;
            }
        };
        self.dataset_definition = updated_dataset;
        DatasetNotification::Updated
    }

    /// Returns a clone of this dataset's [`adr_models::MessageSchemaReference`] from
    /// the `AssetStatus`, if it exists
    #[must_use]
    pub async fn message_schema_reference(&self) -> Option<adr_models::MessageSchemaReference> {
        // unwrap can't fail unless lock is poisoned
        self.asset_status
            .read()
            .await
            .datasets
            .as_ref()?
            .iter()
            .find(|dataset| dataset.name == self.dataset_ref.dataset_name)?
            .message_schema_reference
            .clone()
    }

    /// Returns a clone of the current asset specification
    /// # Panics
    /// if the asset specification mutex has been poisoned, which should not be possible
    #[must_use]
    pub fn asset_specification(&self) -> AssetSpecification {
        (*self.asset_specification.read().unwrap()).clone()
    }

    /// Returns a clone of the current asset status, if it exists
    #[must_use]
    pub async fn asset_status(&self) -> adr_models::AssetStatus {
        (*self.asset_status.read().await).clone()
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
    pub fn device_status(&self) -> DeviceEndpointStatus {
        (*self.device_status.read().unwrap()).clone()
    }

    /// Helper function to update the specific dataset status within the asset status
    fn update_dataset_status(
        asset_status_to_update: &mut adr_models::AssetStatus,
        dataset_name: &str,
        dataset_status: Result<(), AdrConfigError>,
    ) {
        if let Some(curr_dataset_status) =
            asset_status_to_update
                .datasets
                .as_mut()
                .and_then(|datasets| {
                    datasets
                        .iter_mut()
                        .find(|dataset| dataset.name == dataset_name)
                })
        {
            // If the dataset already has a status, update the existing dataset with the new error
            curr_dataset_status.error = dataset_status.err();
        } else {
            // If the dataset doesn't exist in the current status, then add it
            asset_status_to_update
                .datasets
                .get_or_insert_with(Vec::new)
                .push(adr_models::DatasetEventStreamStatus {
                    name: dataset_name.to_string(),
                    message_schema_reference: None,
                    error: dataset_status.err(),
                });
        }
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
        device_specification: adr_models::Device,
        device_endpoint_credentials_mount_path: Option<&PathBuf>,
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

        // update authentication to include the full file path for the credentials
        let authentication = match recvd_inbound.authentication {
            adr_models::Authentication::Anonymous => Authentication::Anonymous,
            adr_models::Authentication::Certificate {
                certificate_secret_name,
            } => Authentication::Certificate {
                certificate_path: device_endpoint_credentials_mount_path.expect("device_endpoint_credentials_mount_path must be present if Authentication is Certificate").as_path().join(certificate_secret_name),
            },
            adr_models::Authentication::UsernamePassword {
                password_secret_name,
                username_secret_name,
            } => Authentication::UsernamePassword {
                password_path: device_endpoint_credentials_mount_path.expect("device_endpoint_credentials_mount_path must be present if Authentication is UsernamePassword").as_path().join(password_secret_name),
                username_path: device_endpoint_credentials_mount_path.expect("device_endpoint_credentials_mount_path must be present if Authentication is UsernamePassword").as_path().join(username_secret_name),
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
            outbound: recvd_endpoints.outbound,
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
    pub outbound: Option<adr_models::OutboundEndpoints>,
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
        certificate_path: PathBuf, // different from adr
    },
    /// Represents authentication using a username and password.
    UsernamePassword {
        /// The 'passwordSecretName' Field.
        password_path: PathBuf, // different from adr
        /// The 'usernameSecretName' Field.
        username_path: PathBuf, // different from adr
    },
}

#[derive(Clone, Debug, Default)] //, PartialEq)]
/// Represents the observed status of a Device and endpoint in the ADR Service.
pub struct DeviceEndpointStatus {
    /// Defines the status for the Device.
    pub config: Option<azure_device_registry::ConfigStatus>,
    /// Defines the status for the inbound endpoint.
    /// Specifies whether the inbound endpoint status has been reported, and if so, whether it was successful or not.
    pub inbound_endpoint_status: Option<Result<(), AdrConfigError>>, // different from adr
}

impl DeviceEndpointStatus {
    pub(crate) fn new(recvd_status: adr_models::DeviceStatus, inbound_endpoint_name: &str) -> Self {
        let inbound_endpoint_status = recvd_status.endpoints.get(inbound_endpoint_name).cloned();
        DeviceEndpointStatus {
            config: recvd_status.config,
            // inbound_endpoint_status: inbound_endpoint_status.map(|status| status.map(|e| Err(e))),
            inbound_endpoint_status: inbound_endpoint_status.map(|status| match status {
                Some(e) => Err(e),
                None => Ok(()),
            }),
        }
    }

    /// Convenience function to turn [`DeviceEndpointStatus`] back into the
    /// `adr_models::DeviceStatus` endpoints format
    pub(crate) fn adr_endpoints(
        &self,
        current_version: Option<u64>,
        inbound_endpoint_name: &str,
    ) -> HashMap<String, Option<AdrConfigError>> {
        // If the version of the status config matches the current version, or the status config
        // hasn't been reported yet, then include the existing endpoint status (if it hasn't been reported, maintain that state).
        // If the version doesn't match, then clear the endpoint status since it hasn't been reported for this version yet

        // if the endpoint status has been set, create a hashmap with it, otherwise represent it as a new hashmap
        let current_endpoint_status = self.inbound_endpoint_status.clone().map_or(
            HashMap::new(),
            |current_inbound_endpoint_status| {
                HashMap::from([(
                    inbound_endpoint_name.to_string(),
                    current_inbound_endpoint_status.err(),
                )])
            },
        );
        if let Some(config) = &self.config {
            // version matches
            if config.version == current_version {
                current_endpoint_status
            } else {
                // config out of date, clear everything
                HashMap::new()
            }
        } else {
            // config not reported, assume anything reported so far is for this version
            current_endpoint_status
        }
    }
}

/// Represents the specification of an Asset in the Azure Device Registry service.
#[derive(Debug, Clone)]
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

impl From<adr_models::Asset> for AssetSpecification {
    fn from(value: adr_models::Asset) -> Self {
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

fn adr_error_into_retry_error(
    e: azure_device_registry::Error,
    operation_for_log: &str,
) -> RetryError<azure_device_registry::Error> {
    match e.kind() {
        // network/retriable
        azure_device_registry::ErrorKind::AIOProtocolError(_) => {
            log::warn!("{operation_for_log} failed. Retrying: {e}");
            RetryError::transient(e)
        }
        // indicates an error in the configuration, might be transient in the future depending on what it can indicate
        azure_device_registry::ErrorKind::ServiceError(_)
        // DuplicateObserve indicates an sdk bug where we called observe more than once. Only possible for unobserve calls.
        // Because we unobserve on deletion, this should not happen, but because unobserve can possibly fail, don't panic on this
        | azure_device_registry::ErrorKind::DuplicateObserve(_) => RetryError::permanent(e),
        _ => {
            // InvalidRequestArgument shouldn't be possible since timeout is already validated
            // ValidationError shouldn't be possible since we should never have an empty asset name. It's not possible to be returned for device calls.
            // ShutdownError isn't possible for this fn to return
            unreachable!()
        }
    }
}

#[cfg(test)]
mod tests {
    use test_case::test_case;

    use super::*;

    /// test that a received device status can be converted to our
    /// DeviceEndpointStatus and then used properly when reporting
    /// a new device config status
    #[test_case(&adr_models::DeviceStatus {
        config: None,
        endpoints: HashMap::from([(
            "my_endpoint".to_string(),
            Some(AdrConfigError { message: Some("test message".to_string()), ..Default::default()}),
        )]),
    }, "my_endpoint", Some(1), true; "no_config")]
    #[test_case(&adr_models::DeviceStatus {
        config: Some(azure_device_registry::ConfigStatus {
            version: Some(1),
            ..Default::default()
        }),
        endpoints: HashMap::from([(
            "my_endpoint".to_string(),
            Some(AdrConfigError { message: Some("test message".to_string()), ..Default::default()}),
        )]),
    }, "my_endpoint", Some(2), false; "version_mismatch")]
    #[test_case(&adr_models::DeviceStatus {
        config: Some(azure_device_registry::ConfigStatus {
            version: Some(1),
            ..Default::default()
        }),
        endpoints: HashMap::from([(
            "my_endpoint".to_string(),
            Some(AdrConfigError { message: Some("test message".to_string()), ..Default::default()}),
        )]),
    }, "my_endpoint", Some(1), true; "version_match")]
    #[test_case(&adr_models::DeviceStatus {
        config: Some(azure_device_registry::ConfigStatus {
            version: Some(1),
            ..Default::default()
        }),
        endpoints: HashMap::new(),
    }, "my_endpoint", Some(1), true; "no_endpoint_statuses_reported")]
    fn device_endpoint_status(
        recvd_status: &adr_models::DeviceStatus,
        inbound_endpoint_name: &str,
        spec_version: Option<u64>,
        expect_keep_received: bool,
    ) {
        let our_status = DeviceEndpointStatus::new(recvd_status.clone(), inbound_endpoint_name);
        let adr_endpoints = our_status.adr_endpoints(spec_version, inbound_endpoint_name);
        if expect_keep_received {
            assert_eq!(recvd_status.endpoints, adr_endpoints);
        } else {
            assert!(
                adr_endpoints.is_empty(),
                "Expected endpoints to be cleared, but got: {adr_endpoints:?}"
            );
        }
    }

    #[test_case(None, Some(1), true; "no_config")]
    #[test_case(Some(azure_device_registry::ConfigStatus {
            version: Some(1),
            ..Default::default()
        }), Some(2), false; "version_mismatch")]
    #[test_case(Some(azure_device_registry::ConfigStatus {
            version: Some(1),
            ..Default::default()
        }), Some(1), true; "version_match")]
    fn current_asset_status_to_modify(
        current_config_status: Option<azure_device_registry::ConfigStatus>,
        spec_version: Option<u64>,
        expect_keep_received: bool,
    ) {
        // put some dataset status in just to make the input never the default AssetStatus
        let current_status = adr_models::AssetStatus {
            config: current_config_status,
            datasets: Some(vec![adr_models::DatasetEventStreamStatus {
                name: "test_dataset".to_string(),
                message_schema_reference: None,
                error: None,
            }]),
            ..Default::default()
        };
        let new_status_base =
            AssetClient::current_status_to_modify(&current_status.clone(), spec_version);
        if expect_keep_received {
            assert_eq!(new_status_base, current_status);
        } else {
            assert_eq!(new_status_base, adr_models::AssetStatus::default());
        }
    }
}
