// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

//! Types for Azure IoT Operations Connectors.

use std::{borrow::Cow, collections::HashMap, hash::Hash, path::PathBuf, sync::Arc};

use azure_iot_operations_services::{
    azure_device_registry::{
        self,
        models::{self as adr_models, Asset},
    },
    schema_registry,
};
use chrono::{DateTime, Utc};
use thiserror::Error;
use tokio::sync::mpsc::{self, UnboundedReceiver, UnboundedSender};
use tokio::sync::watch;
use tokio_retry2::{Retry, RetryError};
use tokio_util::sync::CancellationToken;

use crate::{
    AdrConfigError, Data, DataOperationKind, DataOperationName, DataOperationRef, MessageSchema,
    MessageSchemaReference,
    base_connector::ConnectorContext,
    deployment_artifacts::{
        self, FileMount,
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

/// Represents the result of a network modification
pub enum ModifyResult {
    /// Indicates that the modification was reported
    Reported,
    /// Indicates that the modification did not occur
    NotModified,
}

/// A cloneable status reporter for Device and Endpoint status reporting.
///
/// This provides a way to report Device and Endpoint status changes from outside the [`DeviceEndpointClient`].
#[derive(Clone, Debug)]
pub struct DeviceEndpointStatusReporter {
    connector_context: Arc<ConnectorContext>,
    device_endpoint_status: Arc<tokio::sync::RwLock<DeviceEndpointStatus>>,
    device_endpoint_specification: Arc<std::sync::RwLock<DeviceSpecification>>,
    device_endpoint_ref: DeviceEndpointRef,
}

impl DeviceEndpointStatusReporter {
    /// Used to conditionally report the device status and then updates the device with the new status returned.
    ///
    /// The `modify` function is called with the current device status (if any) and should return:
    /// - `Some(new_status)` if the status should be updated and reported
    /// - `None` if no update is needed
    ///
    /// # Returns
    /// - [`ModifyResult::Reported`] if the status was updated and successfully reported
    /// - [`ModifyResult::NotModified`] if no modification was needed or the version changed during processing
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
    pub async fn report_device_status_if_modified<F>(
        &self,
        modify: F,
    ) -> Result<ModifyResult, azure_device_registry::Error>
    where
        F: Fn(Option<Result<(), &AdrConfigError>>) -> Option<Result<(), AdrConfigError>>,
    {
        // Get the current version of the device endpoint specification
        let cached_version = self.device_endpoint_specification.read().unwrap().version;

        {
            // Get the current device status
            let status_read_guard = self.device_endpoint_status.read().await;
            let current_device_endpoint_status =
                status_read_guard.get_current_device_endpoint_status(cached_version);

            let modify_input = Self::get_device_modify_input(&current_device_endpoint_status);

            // We do not use the result since the status once we acquire the read lock might change
            // and we will have to re-evaluate
            let Some(_modify_result) = modify(modify_input) else {
                // If no modification is needed, return Ok
                return Ok(ModifyResult::NotModified);
            };
        }

        // To modify, we need to acquire the write lock
        let mut status_write_guard = self.device_endpoint_status.write().await;

        if cached_version != self.device_endpoint_specification.read().unwrap().version {
            // Our modify is no longer valid
            log::debug!(
                "Device endpoint specification is out-of-date for {:?}; skipping modification",
                self.device_endpoint_ref
            );
            return Ok(ModifyResult::NotModified);
        }

        // We can continue here because the device endpoint status will not change. The specification might
        // update but we will be reporting for an old version.

        // Get the current device endpoint status in case it has changed
        let current_device_endpoint_status =
            status_write_guard.get_current_device_endpoint_status(cached_version);

        let modify_result = {
            let modify_input = Self::get_device_modify_input(&current_device_endpoint_status);

            let Some(modify_result) = modify(modify_input) else {
                // If no modification is needed, return Ok
                return Ok(ModifyResult::NotModified);
            };

            modify_result
        };

        let device_endpoint_status_to_report = current_device_endpoint_status.into_owned();

        let endpoints = {
            if let Some(inbound_endpoint_status) =
                device_endpoint_status_to_report.inbound_endpoint_status
            {
                // If the inbound endpoint status is present, include it in the report
                HashMap::from([(
                    self.device_endpoint_ref.inbound_endpoint_name.clone(),
                    inbound_endpoint_status.err(),
                )])
            } else {
                // If the inbound endpoint status is not present, we still have to include it in the report
                HashMap::from([(self.device_endpoint_ref.inbound_endpoint_name.clone(), None)])
            }
        };

        // Create a device status
        let device_status_to_report = adr_models::DeviceStatus {
            config: Some(azure_device_registry::ConfigStatus {
                version: cached_version,
                error: modify_result.err(),
                last_transition_time: Some(Utc::now()),
            }),
            endpoints,
        };

        log::debug!(
            "Reporting device status from app for {:?}",
            self.device_endpoint_ref
        );

        Self::internal_report_status(
            &self.connector_context,
            device_status_to_report,
            &mut status_write_guard,
            &self.device_endpoint_ref,
        )
        .await?;

        Ok(ModifyResult::Reported)
    }

    fn get_device_modify_input(
        current_status: &DeviceEndpointStatus,
    ) -> Option<Result<(), &AdrConfigError>> {
        current_status
            .config
            .as_ref()
            .map(|config| match &config.error {
                Some(e) => Err(e),
                None => Ok(()),
            })
    }

    /// Used to conditionally report the endpoint status and then updates the device with the new status returned.
    ///
    /// The `modify` function is called with the current endpoint status (if any) and should return:
    /// - `Some(new_status)` if the status should be updated and reported
    /// - `None` if no update is needed
    ///
    /// # Returns
    /// - [`ModifyResult::Reported`] if the status was updated and successfully reported
    /// - [`ModifyResult::NotModified`] if no modification was needed or the version changed during processing
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
    pub async fn report_endpoint_status_if_modified<F>(
        &self,
        modify: F,
    ) -> Result<ModifyResult, azure_device_registry::Error>
    where
        F: Fn(Option<Result<(), &AdrConfigError>>) -> Option<Result<(), AdrConfigError>>,
    {
        // Get the current version of the device endpoint specification
        let cached_version = self.device_endpoint_specification.read().unwrap().version;

        {
            // Get the current endpoint status
            let status_read_guard = self.device_endpoint_status.read().await;
            let current_device_endpoint_status =
                status_read_guard.get_current_device_endpoint_status(cached_version);

            let modify_input = Self::get_endpoint_modify_input(&current_device_endpoint_status);

            // We do not use the result since the status once we acquire the read lock might change
            // and we will have to re-evaluate
            let Some(_modify_result) = modify(modify_input) else {
                // If no modification is needed, return Ok
                return Ok(ModifyResult::NotModified);
            };
        }

        // To modify, we need to acquire the write lock
        let mut status_write_guard = self.device_endpoint_status.write().await;

        if cached_version != self.device_endpoint_specification.read().unwrap().version {
            // Our modify is no longer valid
            log::debug!(
                "Device endpoint specification is out-of-date for {:?}; skipping modification",
                self.device_endpoint_ref
            );
            return Ok(ModifyResult::NotModified);
        }

        // We can continue here because the device endpoint status will not change. The specification might
        // update but we will be reporting for an old version.

        // Get the current device endpoint status in case it has changed
        let current_device_endpoint_status =
            status_write_guard.get_current_device_endpoint_status(cached_version);

        let modify_result = {
            let modify_input = Self::get_endpoint_modify_input(&current_device_endpoint_status);

            let Some(modify_result) = modify(modify_input) else {
                // If no modification is needed, return Ok
                return Ok(ModifyResult::NotModified);
            };

            modify_result
        };

        let device_endpoint_status_to_report = current_device_endpoint_status.into_owned();

        // If the config is None, we need to create a new one to report along with the endpoint status
        let mut device_config_status = device_endpoint_status_to_report.config.unwrap_or_default();

        device_config_status.version = cached_version;
        device_config_status.last_transition_time = Some(Utc::now());

        // Create a device status
        let device_status_to_report = adr_models::DeviceStatus {
            config: Some(device_config_status),
            endpoints: HashMap::from([(
                self.device_endpoint_ref.inbound_endpoint_name.clone(),
                modify_result.clone().err(),
            )]),
        };

        log::debug!(
            "Reporting endpoint status from app for {:?}",
            self.device_endpoint_ref
        );

        // Report and update status
        Self::internal_report_status(
            &self.connector_context,
            device_status_to_report,
            &mut status_write_guard,
            &self.device_endpoint_ref,
        )
        .await?;

        Ok(ModifyResult::Reported)
    }

    fn get_endpoint_modify_input(
        current_status: &DeviceEndpointStatus,
    ) -> Option<Result<(), &AdrConfigError>> {
        current_status
            .inbound_endpoint_status
            .as_ref()
            .map(|status| match status {
                Ok(()) => Ok(()),
                Err(e) => Err(e),
            })
    }

    /// Reports an already built status to the service, with retries, and then updates the device with the new status returned
    async fn internal_report_status(
        connector_context: &Arc<ConnectorContext>,
        adr_device_status: adr_models::DeviceStatus,
        adr_device_status_ref: &mut DeviceEndpointStatus,
        device_endpoint_ref: &DeviceEndpointRef,
    ) -> Result<(), azure_device_registry::Error> {
        // send status update to the service
        let updated_device_status = Retry::spawn(
            RETRY_STRATEGY.map(tokio_retry2::strategy::jitter).take(10),
            async || -> Result<adr_models::DeviceStatus, RetryError<azure_device_registry::Error>> {
                connector_context
                    .azure_device_registry_client
                    .update_device_plus_endpoint_status(
                        device_endpoint_ref.device_name.clone(),
                        device_endpoint_ref.inbound_endpoint_name.clone(),
                        adr_device_status.clone(),
                        connector_context.azure_device_registry_timeout,
                    )
                    .await
                    .map_err(|e| adr_error_into_retry_error(e, "Update Device Status"))
            },
        )
        .await?;

        // update self with new returned status
        *adr_device_status_ref = DeviceEndpointStatus::new(
            updated_device_status,
            &device_endpoint_ref.inbound_endpoint_name,
        );
        Ok(())
    }
}

/// An Observation for device endpoint creation events that uses
/// multiple underlying clients to get full device endpoint information.
pub struct DeviceEndpointClientCreationObservation {
    connector_context: Arc<ConnectorContext>,
    device_endpoint_create_observation:
        deployment_artifacts::azure_device_registry::DeviceEndpointCreateObservation,
    /// Flag to track if device creation is in progress
    pending_device_creation: bool,
    /// Channels for sending and receiving completed device endpoint clients
    /// This is used to ensure that we only process one device creation at a time
    device_completion_rx: mpsc::Receiver<Option<DeviceEndpointClient>>,
    device_completion_tx: mpsc::Sender<Option<DeviceEndpointClient>>,
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

        let (device_completion_tx, device_completion_rx) = mpsc::channel(1);

        Self {
            connector_context,
            device_endpoint_create_observation,
            pending_device_creation: false,
            device_completion_rx,
            device_completion_tx,
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
            tokio::select! {
                // Check for completed device creation
                Some(device_client_option) = self.device_completion_rx.recv() => {
                    self.pending_device_creation = false;
                    if let Some(device_client) = device_client_option {
                        return device_client;
                    }
                    // If device_client_option is None, creation failed, continue loop
                },
                // Get new device creation notifications only if not already processing one
                create_notification = self.device_endpoint_create_observation.recv_notification(), if !self.pending_device_creation => {
                    let (device_endpoint_ref, asset_create_observation) =
                        create_notification.expect("Device Endpoint Create Observation should never return None because the device_endpoint_create_observation struct holds the sending side of the channel");

                    // Start device creation task
                    self.pending_device_creation = true;
                    let connector_context = self.connector_context.clone();
                    let device_completion_tx = self.device_completion_tx.clone();

                    tokio::task::spawn(async move {
                        let device_client = Self::create_device_endpoint_client(
                            connector_context,
                            device_endpoint_ref,
                            asset_create_observation,
                        ).await;

                        // Always send the result (Some or None) to unblock the receiver
                        let _ = device_completion_tx.send(device_client).await;
                    });
                }
            }
        }
    }

    /// Internal helper to create a [`DeviceEndpointClient`]
    async fn create_device_endpoint_client(
        connector_context: Arc<ConnectorContext>,
        device_endpoint_ref: DeviceEndpointRef,
        asset_create_observation: deployment_artifacts::azure_device_registry::AssetCreateObservation,
    ) -> Option<DeviceEndpointClient> {
        // Obtain the device update observation
        let device_endpoint_update_observation =  match Retry::spawn(
            RETRY_STRATEGY.map(tokio_retry2::strategy::jitter),
            async || -> Result<azure_device_registry::DeviceUpdateObservation, RetryError<azure_device_registry::Error>> {
                connector_context
                    .azure_device_registry_client
                    .observe_device_update_notifications(
                        device_endpoint_ref.device_name.clone(),
                        device_endpoint_ref.inbound_endpoint_name.clone(),
                        connector_context.azure_device_registry_timeout,
                    )
                    // retry on network errors, otherwise don't retry on config/dev errors
                    .await
                    .map_err(|e| adr_error_into_retry_error(e, "Observe for Device Updates"))
        }).await {
            Ok(device_update_observation) => device_update_observation,
            Err(e) => {
              log::error!("Dropping device endpoint create notification: {device_endpoint_ref:?}. Failed to observe for device update notifications after retries: {e}");
              return None;
            },
        };

        // Get the device definition
        let device = match Retry::spawn(
            RETRY_STRATEGY.map(tokio_retry2::strategy::jitter),
            async || -> Result<adr_models::Device, RetryError<azure_device_registry::Error>> {
                connector_context
                    .azure_device_registry_client
                    .get_device(
                        device_endpoint_ref.device_name.clone(),
                        device_endpoint_ref.inbound_endpoint_name.clone(),
                        connector_context.azure_device_registry_timeout,
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
                    &connector_context,
                    &device_endpoint_ref,
                    false,
                )
                .await;
                return None;
            }
        };

        // Get the device status
        let device_status = match Retry::spawn(
            RETRY_STRATEGY.map(tokio_retry2::strategy::jitter),
            async || -> Result<adr_models::DeviceStatus, RetryError<azure_device_registry::Error>> {
                connector_context
                    .azure_device_registry_client
                    .get_device_status(
                        device_endpoint_ref.device_name.clone(),
                        device_endpoint_ref.inbound_endpoint_name.clone(),
                        connector_context.azure_device_registry_timeout,
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
                DeviceEndpointClient::unobserve_device(
                    &connector_context,
                    &device_endpoint_ref,
                    false)
                .await;
                return None;
            }
        };

        // turn the device definition into a DeviceEndpointClient
        match DeviceEndpointClient::new(
            device,
            device_status,
            device_endpoint_ref.clone(),
            device_endpoint_update_observation,
            asset_create_observation,
            connector_context.clone(),
        ) {
            Ok(managed_device) => Some(managed_device),
            Err(e) => {
                // the device definition didn't include the inbound_endpoint, so it likely no longer exists
                // TODO: This won't be a possible failure point in the future once the service returns errors
                log::error!(
                    "Dropping device endpoint create notification: {device_endpoint_ref:?}. {e}"
                );
                // unobserve as cleanup
                DeviceEndpointClient::unobserve_device(
                    &connector_context,
                    &device_endpoint_ref,
                    false,
                )
                .await;
                None
            }
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
    status: Arc<tokio::sync::RwLock<DeviceEndpointStatus>>,
    // Internally used fields
    /// The internal observation for updates
    #[getter(skip)]
    device_update_observation: azure_device_registry::DeviceUpdateObservation,
    #[getter(skip)]
    asset_create_observation: deployment_artifacts::azure_device_registry::AssetCreateObservation,
    /// Flag to track if asset creation is in progress
    #[getter(skip)]
    pending_asset_creation: bool,
    /// Channels for sending and receiving completed asset clients.
    /// This is used to ensure that we only process one asset creation at a time
    #[getter(skip)]
    asset_completion_rx: mpsc::UnboundedReceiver<Option<AssetClient>>,
    #[getter(skip)]
    asset_completion_tx: mpsc::UnboundedSender<Option<AssetClient>>,
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
        let (asset_completion_tx, asset_completion_rx) = mpsc::unbounded_channel();

        Ok(DeviceEndpointClient {
            specification: Arc::new(std::sync::RwLock::new(DeviceSpecification::new(
                device,
                connector_context
                    .connector_artifacts
                    .device_endpoint_credentials_mount
                    .as_ref(),
                &device_endpoint_ref.inbound_endpoint_name,
            )?)),
            status: Arc::new(tokio::sync::RwLock::new(DeviceEndpointStatus::new(
                device_status,
                &device_endpoint_ref.inbound_endpoint_name,
            ))),
            device_endpoint_ref,
            device_update_observation,
            asset_create_observation,
            pending_asset_creation: false,
            asset_completion_rx,
            asset_completion_tx,
            connector_context,
        })
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
                        // Spawn a new task to prevent a possible cancellation and ensure the deleted
                        // notification reaches the application.
                        tokio::task::spawn(
                            {
                                let connector_context_clone = self.connector_context.clone();
                                let device_endpoint_ref_clone = self.device_endpoint_ref.clone();
                                async move {
                                    Self::unobserve_device(&connector_context_clone, &device_endpoint_ref_clone, true).await;
                                }
                            }
                        );
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
                // Check for completed asset creation
                Some(asset_client_option) = self.asset_completion_rx.recv() => {
                    self.pending_asset_creation = false;
                    if let Some(asset_client) = asset_client_option {
                        return ClientNotification::Created(asset_client);
                    }
                    // If asset_client_option is None, creation failed, continue loop
                },
                create_notification = self.asset_create_observation.recv_notification(), if !self.pending_asset_creation => {
                    let Some((asset_ref, asset_deletion_token)) = create_notification else {
                        // if the create notification is None, then the device endpoint has been deleted
                        log::info!("Device Endpoint Deletion detected, stopping device update observation for {:?}", self.device_endpoint_ref);
                        // unobserve as cleanup
                        // Spawn a new task to prevent a possible cancellation and ensure the deleted
                        // notification reaches the application.
                        tokio::task::spawn(
                            {
                                let connector_context_clone = self.connector_context.clone();
                                let device_endpoint_ref_clone = self.device_endpoint_ref.clone();
                                async move {
                                    Self::unobserve_device(&connector_context_clone, &device_endpoint_ref_clone, true).await;
                                }
                            }
                        );
                        return ClientNotification::Deleted;
                    };

                    // Start asset creation task
                    self.pending_asset_creation = true;
                    let connector_context = self.connector_context.clone();
                    let specification = self.specification.clone();
                    let status = self.status.clone();
                    let asset_completion_tx = self.asset_completion_tx.clone();

                    tokio::task::spawn(async move {
                        let asset_client = Self::create_asset_client(
                            connector_context,
                            asset_ref,
                            asset_deletion_token,
                            specification,
                            status,
                        ).await;

                        // Always send the result (Some or None) to unblock the receiver
                        let _ = asset_completion_tx.send(asset_client);
                    });
                }
            }
        }
    }

    /// Creates a new status reporter for this [`DeviceEndpointClient`].
    #[must_use]
    pub fn get_status_reporter(&self) -> DeviceEndpointStatusReporter {
        DeviceEndpointStatusReporter {
            connector_context: self.connector_context.clone(),
            device_endpoint_status: self.status.clone(),
            device_endpoint_specification: self.specification.clone(),
            device_endpoint_ref: self.device_endpoint_ref.clone(),
        }
    }

    /// Internal helper to create an [`AssetClient`]
    async fn create_asset_client(
        connector_context: Arc<ConnectorContext>,
        asset_ref: AssetRef,
        asset_deletion_token: CancellationToken,
        specification: Arc<std::sync::RwLock<DeviceSpecification>>,
        status: Arc<tokio::sync::RwLock<DeviceEndpointStatus>>,
    ) -> Option<AssetClient> {
        // Get asset update observation
        let asset_update_observation = match Retry::spawn(
            RETRY_STRATEGY.map(tokio_retry2::strategy::jitter),
            async || -> Result<azure_device_registry::AssetUpdateObservation, RetryError<azure_device_registry::Error>> {
                connector_context
                    .azure_device_registry_client
                    .observe_asset_update_notifications(
                        asset_ref.device_name.clone(),
                        asset_ref.inbound_endpoint_name.clone(),
                        asset_ref.name.clone(),
                        connector_context.azure_device_registry_timeout,
                    )
                    // retry on network errors, otherwise don't retry on config/dev errors
                    .await
                    .map_err(|e| adr_error_into_retry_error(e, "Observe for Asset Updates"))
        }).await {
            Ok(asset_update_observation) => asset_update_observation,
            Err(e) => {
                log::error!("Dropping asset create notification: {asset_ref:?}. Failed to observe for asset update notifications after retries: {e}");
                return None;
            },
        };

        // Get the asset definition
        let asset = match Retry::spawn(
            RETRY_STRATEGY.map(tokio_retry2::strategy::jitter),
            async || -> Result<adr_models::Asset, RetryError<azure_device_registry::Error>> {
                connector_context
                    .azure_device_registry_client
                    .get_asset(
                        asset_ref.device_name.clone(),
                        asset_ref.inbound_endpoint_name.clone(),
                        asset_ref.name.clone(),
                        connector_context.azure_device_registry_timeout,
                    )
                    .await
                    .map_err(|e| adr_error_into_retry_error(e, "Get Asset Definition"))
            },
        )
        .await
        {
            Ok(asset) => asset,
            Err(e) => {
                log::error!(
                    "Dropping asset create notification: {asset_ref:?}. Failed to get Asset definition after retries: {e}"
                );
                // unobserve as cleanup
                AssetClient::unobserve_asset(&connector_context, &asset_ref, false).await;
                return None;
            }
        };

        // Get the asset status
        let asset_status = match Retry::spawn(
            RETRY_STRATEGY.map(tokio_retry2::strategy::jitter),
            async || -> Result<adr_models::AssetStatus, RetryError<azure_device_registry::Error>> {
                connector_context
                    .azure_device_registry_client
                    .get_asset_status(
                        asset_ref.device_name.clone(),
                        asset_ref.inbound_endpoint_name.clone(),
                        asset_ref.name.clone(),
                        connector_context.azure_device_registry_timeout,
                    )
                    .await
                    .map_err(|e| adr_error_into_retry_error(e, "Get Asset Status"))
            },
        )
        .await {
            Ok(asset_status) => asset_status,
            Err(e) => {
                log::error!("Dropping asset create notification: {asset_ref:?}. Failed to get Asset status after retries: {e}");
                // unobserve as cleanup
                AssetClient::unobserve_asset(&connector_context, &asset_ref, false).await;
                return None;
            },
        };

        // Create the AssetClient
        Some(
            AssetClient::new(
                asset,
                asset_status,
                asset_ref,
                specification,
                status,
                asset_update_observation,
                asset_deletion_token,
                connector_context,
            )
            .await,
        )
    }

    // Returns a clone of the current device status
    /// # Panics
    /// if the status mutex has been poisoned, which should not be possible
    #[must_use]
    pub async fn status(&self) -> DeviceEndpointStatus {
        (*self.status.read().await).clone()
    }

    // Returns a clone of the current device specification
    /// # Panics
    /// if the specification mutex has been poisoned, which should not be possible
    #[must_use]
    pub fn specification(&self) -> DeviceSpecification {
        (*self.specification.read().unwrap()).clone()
    }

    /// Internal convenience function to unobserve from a device's update notifications for cleanup
    async fn unobserve_device(
        connector_context: &Arc<ConnectorContext>,
        device_endpoint_ref: &DeviceEndpointRef,
        is_device_endpoint_deleted: bool,
    ) {
        let _ = Retry::spawn(
            RETRY_STRATEGY.map(tokio_retry2::strategy::jitter),
            async || -> Result<(), RetryError<azure_device_registry::Error>> {
                connector_context
                    .azure_device_registry_client
                    .unobserve_device_update_notifications(
                        device_endpoint_ref.device_name.clone(),
                        device_endpoint_ref.inbound_endpoint_name.clone(),
                        connector_context.azure_device_registry_timeout,
                    )
                    // retry on network errors, otherwise don't retry on config/dev errors
                    .await
                    .map_err(|e| adr_error_into_retry_error(e, "Unobserve for Device Updates"))
            },
        )
        .await
        .inspect_err(|e| {
            // If this unobserve is happening because the device_endpoint has been deleted, the unobserve call isn't necessary and will likely return
            // an error indicating that the device_endpoint can't be found. In the future, we may have a service change to allow us to not need to
            // call unobserve in these cases to clean up our local state, but for now this call is needed and the error should be ignored.
            if is_device_endpoint_deleted {
                match e.kind() {
                    azure_device_registry::ErrorKind::ServiceError(_) => {
                        log::debug!("Expected failure unobserving device update notifications for {device_endpoint_ref:?}. Since the device/endpoint has been deleted, this is expected and not an error if the error indicates that the device/endpoint is not found: {e}");
                    },
                    _ => {
                        log::warn!("Failed to unobserve device update notifications for deleted {device_endpoint_ref:?} after retries. If error indicates that the device/endpoint is not found, this is expected and not a true error: {e}");
                    }
                }
            } else {
                log::error!("Failed to unobserve device update notifications for {device_endpoint_ref:?} after retries. If error indicates that the device/endpoint is not found, this may be expected and not a true error: {e}");
            }
        });
    }
}

/// A cloneable status reporter for Asset status reporting.
///
/// This provides a way to report Asset status changes from outside the [`AssetClient`].
#[derive(Clone, Debug)]
pub struct AssetStatusReporter {
    connector_context: Arc<ConnectorContext>,
    asset_status: Arc<tokio::sync::RwLock<adr_models::AssetStatus>>,
    asset_specification: Arc<std::sync::RwLock<AssetSpecification>>,
    asset_ref: AssetRef,
}

impl AssetStatusReporter {
    /// Used to conditionally report the asset status and then updates the asset with the new status returned.
    ///
    /// The `modify` function is called with the current asset status (if any) and should return:
    /// - `Some(new_status)` if the status should be updated and reported
    /// - `None` if no update is needed
    ///
    /// # Returns
    /// - [`ModifyResult::Reported`] if the status was updated and successfully reported
    /// - [`ModifyResult::NotModified`] if no modification was needed or the version changed during processing
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
    pub async fn report_status_if_modified<F>(
        &self,
        modify: F,
    ) -> Result<ModifyResult, azure_device_registry::Error>
    where
        F: Fn(Option<Result<(), &AdrConfigError>>) -> Option<Result<(), AdrConfigError>>,
    {
        // Get the current version of the asset specification
        let cached_version = self.asset_specification.read().unwrap().version;

        {
            // Get the current asset status
            let status_read_guard = self.asset_status.read().await;
            let current_asset_status =
                AssetClient::get_current_asset_status(&status_read_guard, cached_version);

            let modify_input = Self::get_modify_input(&current_asset_status);

            // We do not use the result since the status once we acquire the read lock might change
            // and we will have to re-evaluate
            let Some(_modify_result) = modify(modify_input) else {
                // If no modification is needed, return Ok
                return Ok(ModifyResult::NotModified);
            };
        }

        // To modify, we need to acquire the write lock
        let mut status_write_guard = self.asset_status.write().await;

        if cached_version != self.asset_specification.read().unwrap().version {
            // Our modify is no longer valid
            log::debug!(
                "Asset specification is out-of-date for {:?}; skipping modification",
                self.asset_ref
            );
            return Ok(ModifyResult::NotModified);
        }

        // We can continue here because the asset status will not change. The specification might
        // update but we will be reporting for an old version.

        // Get the current asset status in case it has changed
        let current_asset_status =
            AssetClient::get_current_asset_status(&status_write_guard, cached_version);

        let modify_result = {
            let modify_input = Self::get_modify_input(&current_asset_status);

            let Some(modify_result) = modify(modify_input) else {
                // If no modification is needed, return Ok
                return Ok(ModifyResult::NotModified);
            };

            modify_result
        };

        let mut asset_status_to_report = current_asset_status.into_owned();

        // Update the config status
        asset_status_to_report.config = Some(azure_device_registry::ConfigStatus {
            version: cached_version,
            error: modify_result.clone().err(),
            last_transition_time: Some(chrono::Utc::now()),
        });

        log::debug!("Reporting asset status from app for {:?}", self.asset_ref);

        Self::internal_report_status(
            asset_status_to_report,
            &self.connector_context,
            &self.asset_ref,
            &mut status_write_guard,
            "AssetClient::report_status_if_modified",
        )
        .await?;

        Ok(ModifyResult::Reported)
    }

    fn get_modify_input(
        current_status: &adr_models::AssetStatus,
    ) -> Option<Result<(), &AdrConfigError>> {
        current_status
            .config
            .as_ref()
            .map(|config| match &config.error {
                Some(err) => Err(err),
                None => Ok(()),
            })
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
                        connector_context.azure_device_registry_timeout,
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
}

/// Struct used to hold the updates for an Asset's data operations
/// until all data operation kinds have been processed and the function
/// using this struct is beyond the point where it needs to worry about
/// cancel safety.
struct AssetDataOperationUpdates {
    new_status: adr_models::AssetStatus,
    status_updated: bool,
    data_operation_updates: Vec<(
        watch::Sender<DataOperationUpdateNotification>,
        DataOperationUpdateNotification,
    )>,
    new_data_operation_clients: Vec<DataOperationClient>,
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
    device_status: Arc<tokio::sync::RwLock<DeviceEndpointStatus>>,
    // Internally used fields
    /// Internal `CancellationToken` for when the Asset is deleted. Surfaced to the user through the receive update flow
    #[getter(skip)]
    asset_deletion_token: CancellationToken,
    /// The internal observation for updates
    #[getter(skip)]
    asset_update_observation: azure_device_registry::AssetUpdateObservation,
    /// Internal watcher receiver that holds a snapshot of the latest update and whether it has
    /// been fully processed or not
    #[getter(skip)]
    asset_update_watcher_rx: watch::Receiver<Asset>,
    /// Internal watcher sender that sends the latest update
    #[getter(skip)]
    asset_update_watcher_tx: watch::Sender<Asset>,
    /// Internal sender for when new data operations are created
    #[getter(skip)]
    data_operation_creation_tx: UnboundedSender<DataOperationClient>,
    /// Internal channel for receiving notifications about data operation creation events.
    #[getter(skip)]
    data_operation_creation_rx: UnboundedReceiver<DataOperationClient>,
    /// Internal watch sender for releasing data operation create/update notifications
    #[getter(skip)]
    release_data_operation_notifications_tx: watch::Sender<()>,
    /// hashmap of current dataset names to their current definition and a sender to send dataset updates
    #[getter(skip)]
    dataset_hashmap: HashMap<
        String,
        (
            adr_models::Dataset,
            watch::Sender<DataOperationUpdateNotification>,
        ),
    >,
    /// hashmap of current event/event group names to their current definitions and a sender to send event updates
    #[getter(skip)]
    event_hashmap: HashMap<
        (String, String), // (EventGroup name, Event name)
        (
            EventSpecification,
            watch::Sender<DataOperationUpdateNotification>,
        ),
    >,
    /// hashmap of current stream names to their current definition and a sender to send stream updates
    #[getter(skip)]
    stream_hashmap: HashMap<
        String,
        (
            adr_models::Stream,
            watch::Sender<DataOperationUpdateNotification>,
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
        device_status: Arc<tokio::sync::RwLock<DeviceEndpointStatus>>,
        asset_update_observation: azure_device_registry::AssetUpdateObservation,
        asset_deletion_token: CancellationToken,
        connector_context: Arc<ConnectorContext>,
    ) -> Self {
        let (asset_update_watcher_tx, asset_update_watcher_rx) = watch::channel(asset.clone());
        let specification = AssetSpecification::from(asset.clone());
        let specification_version = specification.version;
        let (data_operation_creation_tx, data_operation_creation_rx) = mpsc::unbounded_channel();

        // Create the AssetClient so that we can use the same helper functions for processing the data_operations as we do during the update flow
        let mut asset_client = AssetClient {
            asset_ref,
            specification: Arc::new(std::sync::RwLock::new(specification)),
            status: Arc::new(tokio::sync::RwLock::new(asset_status)),
            device_specification,
            device_status,
            asset_update_observation,
            asset_update_watcher_rx,
            asset_update_watcher_tx,
            data_operation_creation_tx,
            data_operation_creation_rx,
            dataset_hashmap: HashMap::new(),
            event_hashmap: HashMap::new(),
            stream_hashmap: HashMap::new(),
            connector_context,
            release_data_operation_notifications_tx: watch::Sender::new(()),
            asset_deletion_token,
        };

        {
            // lock the status write guard so that no other threads can modify the status while we update it
            // (not possible in new, but allows use of Self:: helper fns)
            let mut status_write_guard = asset_client.status.write().await;
            // if there are any config errors when parsing the asset, collect them all so we can report them at once
            let mut updates = AssetDataOperationUpdates {
                new_status: Self::get_current_asset_status(
                    &status_write_guard,
                    specification_version,
                )
                .into_owned(),
                status_updated: false,
                data_operation_updates: Vec::new(),
                new_data_operation_clients: Vec::new(),
            };
            // Handle "updates" for each type of data operation. Since we don't currently have any data
            // operations tracked yet, everything in the definition will be treated as a new data operation.
            let mut temp_dataset_hashmap = asset_client.dataset_hashmap.clone();
            // asset_client.dataset_hashmap will be empty, so all datasets will be treated as new (as it should be).
            // Note that I could use vec::new() for temp_dataset_hashmap, but for extra safety, I'll clone the asset's dataset hashmap instead
            asset_client.handle_data_operation_kind_updates(
                &mut temp_dataset_hashmap,
                &asset,
                &asset.datasets,
                &mut updates,
            );
            asset_client.dataset_hashmap = temp_dataset_hashmap;

            let mut temp_event_hashmap = asset_client.event_hashmap.clone();
            asset_client.handle_event_group_updates(
                &mut temp_event_hashmap,
                &asset,
                &asset.event_groups,
                &mut updates,
            );
            asset_client.event_hashmap = temp_event_hashmap;

            let mut temp_stream_hashmap = asset_client.stream_hashmap.clone();
            asset_client.handle_data_operation_kind_updates(
                &mut temp_stream_hashmap,
                &asset,
                &asset.streams,
                &mut updates,
            );
            asset_client.stream_hashmap = temp_stream_hashmap;

            // if there were any config errors, report them to the ADR service together
            if updates.status_updated {
                log::debug!(
                    "Reporting error asset status on new for {:?}",
                    asset_client.asset_ref
                );
                if let Err(e) = AssetStatusReporter::internal_report_status(
                    updates.new_status,
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

            // Send all new data operation create notifications
            for new_data_operation_client in updates.new_data_operation_clients {
                // error is not possible since the receiving side of the channel is owned by the AssetClient
                let _ = asset_client
                    .data_operation_creation_tx
                    .send(new_data_operation_client);
            }
            // Note, updates.data_operation_updates is not used because there will be no updates on new
        }

        asset_client
    }

    /// Helper function to handle updates for event groups on an Asset
    /// Transforms a list of `adr_models::EventGroup`s into a list of
    /// [`EventSpecification`]s per Event and calls `handle_data_operation_kind_updates`
    fn handle_event_group_updates(
        &self,
        event_hashmap: &mut HashMap<
            (String, String), // (eg name, event name)
            (
                EventSpecification,
                watch::Sender<DataOperationUpdateNotification>,
            ),
        >,
        updated_asset: &Asset,
        updated_asset_event_groups: &[adr_models::EventGroup],
        updates: &mut AssetDataOperationUpdates,
    ) {
        let mut updated_asset_events = Vec::new();
        for event_group in updated_asset_event_groups {
            // Creates an [`EventSpecification`] for each event in the event group
            for event in &event_group.events {
                updated_asset_events.push((event_group.clone(), event.clone()).into());
            }
        }

        // Do the actual updates now that we have the correct data format
        self.handle_data_operation_kind_updates(
            event_hashmap,
            updated_asset,
            &updated_asset_events,
            updates,
        );
    }

    /// Helper function to handle updates for all of a type of data operations on an Asset
    /// This reduces duplicate code for each data operation kind - instead this function is called once for each
    ///
    /// Parses and validates all Asset updates pertaining to this data operation kind
    ///     Detects any deleted, updated, and new data operations
    ///     Parses the default destination for that data operation
    /// Modifies `updates` and `data_operation_hashmap` in place:
    /// Modifies `updates.new_status` with any validation errors found
    /// Adds any Data Operation updates to `updates.data_operation_updates` that can be sent after the update task can't be cancelled
    /// Adds any new Data Operation Clients to `updates.new_data_operation_clients` that can be sent after the update task can't be cancelled
    /// Removes any deleted Data Operations from the `data_operation_hashmap` that can be applied after the update task can't be cancelled
    fn handle_data_operation_kind_updates<T: Clone + DataOperation + PartialEq>(
        &self,
        data_operation_hashmap: &mut HashMap<
            T::HashName,
            (T, watch::Sender<DataOperationUpdateNotification>),
        >,
        updated_asset: &Asset,
        updated_asset_data_operations: &[T],
        updates: &mut AssetDataOperationUpdates,
    ) {
        // remove the data operations that are no longer present in the new asset definition.
        // This triggers deletion notification since this drops the update sender.
        data_operation_hashmap.retain(|data_operation_name, _| {
            updated_asset_data_operations
                .iter()
                .any(|data_operation| data_operation.hash_name() == data_operation_name)
        });
        // Get the new default data operation destinations and track whether they're different or not from the current one
        let default_data_operation_destination_updated = match T::kind() {
            DataOperationKind::Dataset => {
                updated_asset.default_datasets_destinations
                    != self
                        .specification
                        .read()
                        .unwrap()
                        .default_datasets_destinations
            }
            DataOperationKind::Event => {
                updated_asset.default_events_destinations
                    != self
                        .specification
                        .read()
                        .unwrap()
                        .default_events_destinations
            }
            DataOperationKind::Stream => {
                updated_asset.default_streams_destinations
                    != self
                        .specification
                        .read()
                        .unwrap()
                        .default_streams_destinations
            }
        };

        let default_destinations_result = match T::kind() {
            DataOperationKind::Dataset => {
                destination_endpoint::Destination::new_dataset_destinations(
                    &updated_asset.default_datasets_destinations,
                    &self.asset_ref,
                    updated_asset.uuid.as_ref(),
                    updated_asset.external_asset_id.as_ref(),
                    &self.connector_context,
                )
            }
            DataOperationKind::Event => {
                // TODO: To support default destinations on the event group in the future,
                // we can create a hashmap of eventGroups to their default destinations here.
                // However, any failures need to be reported only on an event that would actually
                // use that bad configuration
                destination_endpoint::Destination::new_event_stream_destinations(
                    &updated_asset.default_events_destinations,
                    &self.asset_ref,
                    updated_asset.uuid.as_ref(),
                    updated_asset.external_asset_id.as_ref(),
                    &self.connector_context,
                )
            }
            DataOperationKind::Stream => {
                destination_endpoint::Destination::new_event_stream_destinations(
                    &updated_asset.default_streams_destinations,
                    &self.asset_ref,
                    updated_asset.uuid.as_ref(),
                    updated_asset.external_asset_id.as_ref(),
                    &self.connector_context,
                )
            }
        };
        let default_data_operation_destinations = match default_destinations_result {
            Ok(res) => res.into_iter().map(Arc::new).collect(),
            Err(e) => {
                log::error!(
                    "Invalid default data operation destination for Asset {:?}: {e:?}",
                    self.asset_ref
                );
                // Add this to the status to be reported to ADR
                updates.new_status.config = Some(azure_device_registry::ConfigStatus {
                    version: updated_asset.version,
                    error: Some(e),
                    last_transition_time: Some(chrono::Utc::now()),
                });
                updates.status_updated = true;
                // set this to an empty vec instead of skipping parsing the rest of the asset because if all
                // data operations have a destination specified, this might not cause the asset to be unusable
                vec![]
            }
        };

        // For all received data operations, check if the existing data operation needs an update or if a new one needs to be created
        for received_data_operation in updated_asset_data_operations {
            // it already exists
            if let Some((data_operation, data_operation_update_tx)) =
                data_operation_hashmap.get_mut(received_data_operation.hash_name())
            {
                // TODO: To support default destinations on the event group in the future,
                // we can check the previous event group default destinations hashmap here
                // and overwrite the asset event default destination if it's present before
                // sending the update.
                // To support splitting updates for the eventGroup as a separate notification
                // from updates on an event, that logic would need to be added here as well.

                // if the default destination has changed, update all data operations. TODO: might be able to track whether a data operation uses a default to reduce updates needed here
                // otherwise, only send an update if the data operation definition has changed
                if default_data_operation_destination_updated
                    || received_data_operation != data_operation
                {
                    // we need to make sure we have the updated definition for comparing next time
                    *data_operation = received_data_operation.clone();

                    // save update to send to the data operation after the task can't get cancelled
                    updates.data_operation_updates.push((
                        data_operation_update_tx.clone(),
                        (
                            received_data_operation.clone().into(),
                            default_data_operation_destinations.clone(),
                            self.release_data_operation_notifications_tx.subscribe(),
                        ),
                    ));
                }
            }
            // it needs to be created
            else {
                // TODO: To support default destinations on the event group in the future,
                // here we also need to determine whether to pass in the asset or eventGroup
                // default event destination

                let data_operation_definition = received_data_operation.clone().into();
                let (data_operation_update_watcher_tx, data_operation_update_watcher_rx) =
                    watch::channel((
                        data_operation_definition.clone(),
                        default_data_operation_destinations.clone(),
                        self.release_data_operation_notifications_tx.subscribe(),
                    ));
                match DataOperationClient::new(
                    data_operation_definition,
                    data_operation_update_watcher_rx,
                    &default_data_operation_destinations,
                    self.asset_ref.clone(),
                    self.status.clone(),
                    self.specification.clone(),
                    self.device_specification.clone(),
                    self.device_status.clone(),
                    self.connector_context.clone(),
                ) {
                    Ok(new_data_operation_client) => {
                        // insert the data operation client into the hashmap so we can handle updates
                        data_operation_hashmap.insert(
                            received_data_operation.hash_name().clone(),
                            (
                                received_data_operation.clone(),
                                data_operation_update_watcher_tx,
                            ),
                        );

                        // save new data operation client to be sent on self.data_operation_creation_tx after the task can't get cancelled
                        updates
                            .new_data_operation_clients
                            .push(new_data_operation_client);
                    }
                    Err(e) => {
                        // Add the error to the status to be reported to ADR, and then continue to process
                        // other data operations even if one isn't valid. Don't give this one to
                        // the application since we can't forward data on it. If there's an update to the
                        // definition, they'll get the create notification for it at that point if it's valid
                        match received_data_operation.data_operation_name() {
                            DataOperationName::Dataset {
                                name: ref dataset_name,
                            } => {
                                DataOperationClient::update_dataset_status(
                                    &mut updates.new_status,
                                    dataset_name,
                                    Err(e),
                                );
                            }
                            DataOperationName::Event {
                                name: ref event_name,
                                ref event_group_name,
                            } => {
                                DataOperationClient::update_event_status(
                                    &mut updates.new_status,
                                    event_group_name,
                                    event_name,
                                    Err(e),
                                );
                            }
                            DataOperationName::Stream {
                                name: ref stream_name,
                            } => {
                                DataOperationClient::update_stream_status(
                                    &mut updates.new_status,
                                    stream_name,
                                    Err(e),
                                );
                            }
                        }
                        updates.status_updated = true;
                    }
                }
            }
        }
    }

    /// Helper function to handle an asset update
    async fn handle_update(
        &mut self,
        updated_asset: Asset,
    ) -> ClientNotification<DataOperationClient> {
        // lock the status write guard so that no other threads can modify the status while we update it
        let mut status_write_guard = self.status.write().await;

        let mut adr_asset_status =
            AssetClient::get_current_asset_status(&status_write_guard, updated_asset.version)
                .into_owned();

        // Update the config status
        adr_asset_status.config = match adr_asset_status.config {
            Some(mut config) => {
                config.last_transition_time = Some(Utc::now());
                Some(config)
            }
            None => {
                // If the config is None, we need to create a new one to report along with the data operation status
                Some(azure_device_registry::ConfigStatus {
                    version: updated_asset.version,
                    last_transition_time: Some(Utc::now()),
                    ..Default::default()
                })
            }
        };

        // if there are any config errors when parsing the asset, collect them all so we can report them at once
        // track all data_operations to update and save notifications for once the task can't be cancelled
        let mut updates = AssetDataOperationUpdates {
            new_status: adr_asset_status,
            status_updated: false,
            data_operation_updates: Vec::new(),
            new_data_operation_clients: Vec::new(),
        };

        // Handle updates for each type of data operation
        // make changes to copies of the data operation hashmaps so that this function is cancel safe
        let mut temp_dataset_hashmap = self.dataset_hashmap.clone();
        self.handle_data_operation_kind_updates(
            &mut temp_dataset_hashmap,
            &updated_asset,
            &updated_asset.datasets,
            &mut updates,
        );
        let mut temp_event_hashmap = self.event_hashmap.clone();
        self.handle_event_group_updates(
            &mut temp_event_hashmap,
            &updated_asset,
            &updated_asset.event_groups,
            &mut updates,
        );
        let mut temp_stream_hashmap = self.stream_hashmap.clone();
        self.handle_data_operation_kind_updates(
            &mut temp_stream_hashmap,
            &updated_asset,
            &updated_asset.streams,
            &mut updates,
        );

        // if there were any config errors, report them to the ADR service together
        if updates.status_updated {
            log::debug!(
                "Reporting error asset status on recv_notification for {:?}",
                self.asset_ref
            );
            if let Err(e) = AssetStatusReporter::internal_report_status(
                updates.new_status,
                &self.connector_context,
                &self.asset_ref,
                &mut status_write_guard,
                "AssetClient::recv_notification",
            )
            .await
            {
                log::error!(
                    "Failed to report error Asset status for updated Asset {:?}: {e}",
                    self.asset_ref
                );
            }
        }

        // update specification
        let mut unlocked_specification = self.specification.write().unwrap(); // unwrap can't fail unless lock is poisoned
        *unlocked_specification = AssetSpecification::from(updated_asset);

        // update data operation hashmaps now that this task can't be cancelled
        self.dataset_hashmap = temp_dataset_hashmap;
        self.event_hashmap = temp_event_hashmap;
        self.stream_hashmap = temp_stream_hashmap;

        // send all notifications associated with this asset update
        for (data_operation_update_tx, data_operation_update_notification) in
            updates.data_operation_updates
        {
            // send update to the data operation
            let _ = data_operation_update_tx.send(data_operation_update_notification).inspect_err(|tokio::sync::watch::error::SendError((e_data_operation_definition, _,_))| {
                // TODO: should this trigger the DataOperationClient create flow, or is this just indicative of an application bug?
                log::warn!(
                    "Update received for data operation {} on asset {:?}, but DataOperationClient has been dropped",
                    e_data_operation_definition.name(),
                    self.asset_ref
                );
            });
        }
        for new_data_operation_client in updates.new_data_operation_clients {
            // error is not possible since the receiving side of the channel is owned by the AssetClient
            let _ = self
                .data_operation_creation_tx
                .send(new_data_operation_client);
        }

        // Asset update has been fully processed, mark as seen.
        self.asset_update_watcher_rx.mark_unchanged();
        ClientNotification::Updated
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
    /// Returns [`ClientNotification::Created`] with a new [`DataOperationClient`] if a
    /// new Data Operation has been created.
    ///
    /// Receiving an update will also trigger update/deletion notifications for data operations that
    /// are linked to this asset. To ensure the asset update is received before data operation notifications,
    /// data operation notifications won't be released until this function is polled again after receiving an
    /// update.
    ///
    /// # Cancel safety
    /// This method is cancel safe. If you use it as the event in a `tokio::select!` statement and some other branch
    /// completes first, then it is guaranteed that no asset notifications will be lost, and the asset will not
    /// be updated without a notification being returned.
    ///
    /// # Panics
    /// If the specification mutex has been poisoned, which should not be possible
    pub async fn recv_notification(&mut self) -> ClientNotification<DataOperationClient> {
        // release any pending data_operation create/update notifications
        self.release_data_operation_notifications_tx
            .send_modify(|()| ());
        tokio::select! {
            biased;
            () = self.asset_deletion_token.cancelled() => {
                log::info!("Asset deletion token received, stopping asset update observation for {:?}", self.asset_ref);
                // unobserve as cleanup
                // Spawn a new task to prevent a possible cancellation and ensure the deleted
                // notification reaches the application.
                tokio::task::spawn(
                    {
                        let connector_context_clone = self.connector_context.clone();
                        let asset_ref_clone = self.asset_ref.clone();
                        async move {
                            Self::unobserve_asset(&connector_context_clone, &asset_ref_clone, true).await;
                        }
                    }
                );
                ClientNotification::Deleted
            },
            notification = self.asset_update_observation.recv_notification() => {
                // We set auto ack to true, so there's never an ack here to deal with. If we restart, then we'll implicitly
                // get the update again because we'll pull the latest definition on the restart, so we don't need to get
                // the notification again as an MQTT message.
                if let Some((updated_asset, _)) = notification {
                    // The asset client holds the receiving end so `send` will never return an error,
                    // we are saving the updated asset in the watcher in case `handle_update` is
                    // cancelled
                    let _ = self.asset_update_watcher_tx.send(updated_asset.clone());
                    self.handle_update(updated_asset).await
                } else {
                    // unobserve as cleanup
                    // Spawn a new task to prevent a possible cancellation and ensure the deleted
                    // notification reaches the application.
                    tokio::task::spawn(
                        {
                            let connector_context_clone = self.connector_context.clone();
                            let asset_ref_clone = self.asset_ref.clone();
                            async move {
                                Self::unobserve_asset(&connector_context_clone, &asset_ref_clone, true).await;
                            }
                        }
                    );
                    // Asset update has been fully processed, mark as seen.
                    self.asset_update_watcher_rx.mark_unchanged();
                    ClientNotification::Deleted
                }
            },
            // An error here is not possible since the asset client holds the sender end
            Ok(()) = self.asset_update_watcher_rx.changed() => {
                // We need to mark as changed in case `handle_update` is cancelled so we can enter
                // this branch again
                self.asset_update_watcher_rx.mark_changed();
                let updated_asset = self.asset_update_watcher_rx.borrow().clone();

                self.handle_update(updated_asset).await
            }
            create_notification = self.data_operation_creation_rx.recv() => {
                let Some(data_operation_client) = create_notification else {
                    // unobserve as cleanup
                    // Spawn a new task to prevent a possible cancellation and ensure the deleted
                    // notification reaches the application.
                    tokio::task::spawn(
                        {
                            let connector_context_clone = self.connector_context.clone();
                            let asset_ref_clone = self.asset_ref.clone();
                            async move {
                                Self::unobserve_asset(&connector_context_clone, &asset_ref_clone, true).await;
                            }
                        }
                    );
                    return ClientNotification::Deleted;
                };
                ClientNotification::Created(data_operation_client)
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
    pub async fn device_status(&self) -> DeviceEndpointStatus {
        (*self.device_status.read().await).clone()
    }

    /// Creates a new status reporter for this [`AssetClient`]
    #[must_use]
    pub fn get_status_reporter(&self) -> AssetStatusReporter {
        AssetStatusReporter {
            connector_context: self.connector_context.clone(),
            asset_status: self.status.clone(),
            asset_specification: self.specification.clone(),
            asset_ref: self.asset_ref.clone(),
        }
    }

    /// Internal helper to get an [`adr_models::AssetStatus`] that can be used as a starting place
    /// to modify the current status with whatever new things we want to report.
    ///
    /// Note that it returns a `Cow`. The reason is that most of the times that we are reporting
    /// a status we will not end up modifying it. `Cow` allows us to only clone when we are going to
    /// modify.
    pub(crate) fn get_current_asset_status(
        current_status: &adr_models::AssetStatus,
        adr_version: Option<u64>,
    ) -> Cow<'_, adr_models::AssetStatus> {
        match &current_status.config {
            Some(config) => {
                if config.version == adr_version {
                    // If the version in our config matches the one in ADR we return our current
                    // asset status
                    Cow::Borrowed(current_status)
                } else {
                    // If the version doesn't match, the config version we are holding is stale so
                    // we pass a cleared version
                    Cow::Owned(adr_models::AssetStatus::default())
                }
            }
            None => {
                // If there is no config, then the status is not set, we can pass our current asset
                // status
                Cow::Borrowed(current_status)
            }
        }
    }

    /// Internal convenience function to unobserve from an asset's update notifications for cleanup
    async fn unobserve_asset(
        connector_context: &Arc<ConnectorContext>,
        asset_ref: &AssetRef,
        is_asset_deleted: bool,
    ) {
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
                        connector_context.azure_device_registry_timeout,
                    )
                    // retry on network errors, otherwise don't retry on config/dev errors
                    .await
                    .map_err(|e| adr_error_into_retry_error(e, "Unobserve for Asset Updates"))
            },
        )
        .await
        .inspect_err(|e| {
            // If this unobserve is happening because the asset has been deleted, the unobserve call isn't necessary and will likely return
            // an error indicating that the asset can't be found. In the future, we may have a service change to allow us to not need to
            // call unobserve in these cases to clean up our local state, but for now this call is needed and the error should be ignored.
            if is_asset_deleted {
                match e.kind() {
                    azure_device_registry::ErrorKind::ServiceError(_) => {
                        log::debug!("Expected failure unobserving asset update notifications for {asset_ref:?}. Since the asset has been deleted, this is expected and not an error if the error indicates that the asset is not found: {e}");
                    },
                    _ => {
                        log::warn!("Failed to unobserve asset update notifications for deleted {asset_ref:?} after retries. If error indicates that the asset is not found, this is expected and not a true error: {e}");
                    }
                }
            } else {
                log::error!("Failed to unobserve asset update notifications for {asset_ref:?} after retries. If error indicates that the asset is not found, this may be expected and not a true error: {e}");
            }
        });
    }
}

/// Errors that can be returned when reporting a message schema for a data operation
#[derive(Error, Debug)]
pub enum MessageSchemaError {
    /// An error occurred while putting the Schema in the Schema Registry
    #[error(transparent)]
    PutSchemaError(#[from] schema_registry::Error),
    /// An error occurred while reporting the Schema to the Azure Device Registry Service.
    #[error(transparent)]
    AzureDeviceRegistryError(#[from] azure_device_registry::Error),
}

type DataOperationUpdateNotification = (
    DataOperationDefinition,                     // new data operation definition
    Vec<Arc<destination_endpoint::Destination>>, // new default data operation destinations
    watch::Receiver<()>, // watch receiver for when the update notification should be released to the application
);

/// Notifications that can be received for a Data Operation
pub enum DataOperationNotification {
    /// Indicates that the Data Operation's definition has been updated in place
    Updated,
    /// Indicates that the Data Operation has been deleted.
    Deleted,
    /// Indicates that the Data Operation received an update, but the update was not valid.
    /// The definition is still updated in place, but the [`DataOperationClient`] should not be used until
    /// there is a new update, otherwise the out of date definition will be used for
    /// sending data to the destination.
    UpdatedInvalid,
}

/// Result of a schema modification attempt
pub enum SchemaModifyResult {
    /// Indicates that the schema was reported successfully and status was modified
    Reported(MessageSchemaReference),
    /// Indicates that the schema or status were not modified
    NotModified,
}

/// A cloneable status reporter for Data Operation status reporting.
///
/// This provides a way to report Data Operation status changes from outside the [`DataOperationClient`].
#[derive(Debug, Clone)]
pub struct DataOperationStatusReporter {
    connector_context: Arc<ConnectorContext>,
    asset_status: Arc<tokio::sync::RwLock<adr_models::AssetStatus>>,
    asset_specification: Arc<std::sync::RwLock<AssetSpecification>>,
    data_operation_ref: DataOperationRef,
    asset_ref: AssetRef,
}

impl DataOperationStatusReporter {
    /// Used to conditionally report the data operation status and then updates the asset with the new status returned.
    ///
    /// The `modify` function is called with the current data operation status (if any) and should return:
    /// - `Some(new_status)` if the status should be updated and reported
    /// - `None` if no update is needed
    ///
    /// # Returns
    /// - [`ModifyResult::Reported`] if the status was updated and successfully reported
    /// - [`ModifyResult::NotModified`] if no modification was needed or the version changed during processing
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
    pub async fn report_status_if_modified<F>(
        &self,
        modify: F,
    ) -> Result<ModifyResult, azure_device_registry::Error>
    where
        F: Fn(Option<Result<(), &AdrConfigError>>) -> Option<Result<(), AdrConfigError>>,
    {
        // Get the current version of the asset specification
        let cached_version = self.asset_specification.read().unwrap().version;

        {
            // Get the current asset status
            let status_read_guard = self.asset_status.read().await;
            let current_asset_status =
                AssetClient::get_current_asset_status(&status_read_guard, cached_version);

            let modify_input = self.get_modify_input(&current_asset_status);

            // We do not use the result since the status once we acquire the read lock might change
            // and we will have to re-evaluate
            let Some(_modify_result) = modify(modify_input) else {
                // If no modification is needed, return Ok
                return Ok(ModifyResult::NotModified);
            };
        }

        // To modify, we need to acquire the write lock
        let mut status_write_guard = self.asset_status.write().await;

        if cached_version != self.asset_specification.read().unwrap().version {
            // Our modify is no longer valid
            log::debug!(
                "Asset specification is out-of-date for data operation {:?}; skipping modification",
                self.data_operation_ref
            );
            return Ok(ModifyResult::NotModified);
        }

        // We can continue here because the asset status will not change. The specification might
        // update but we will be reporting for an old version.

        // Get the current asset status in case it has changed
        let current_asset_status =
            AssetClient::get_current_asset_status(&status_write_guard, cached_version);

        let modify_result = {
            let modify_input = self.get_modify_input(&current_asset_status);

            let Some(modify_result) = modify(modify_input) else {
                // If no modification is needed, return Ok
                return Ok(ModifyResult::NotModified);
            };

            modify_result
        };

        let mut asset_status_to_report = current_asset_status.into_owned();

        // Update the config status
        asset_status_to_report.config = match asset_status_to_report.config {
            Some(mut config) => {
                config.last_transition_time = Some(Utc::now());
                Some(config)
            }
            None => {
                // If the config is None, we need to create a new one to report along with the
                // data operations status
                Some(azure_device_registry::ConfigStatus {
                    version: cached_version,
                    last_transition_time: Some(Utc::now()),
                    ..Default::default()
                })
            }
        };

        Self::internal_report_status(
            &self.connector_context,
            &self.asset_ref,
            asset_status_to_report,
            &mut status_write_guard,
            &self.data_operation_ref,
            modify_result,
            "DataOperationStatusReporter::report_data_operation_status_if_modified",
        )
        .await?;

        Ok(ModifyResult::Reported)
    }

    fn get_modify_input<'a>(
        &self,
        current_status: &'a adr_models::AssetStatus,
    ) -> Option<Result<(), &'a AdrConfigError>> {
        match self.data_operation_ref.data_operation_name {
            DataOperationName::Dataset {
                name: ref dataset_name,
            } => current_status
                .datasets
                .as_ref()
                .and_then(|datasets| {
                    datasets
                        .iter()
                        .find(|ds_status| ds_status.name == *dataset_name)
                })
                .map(|ds_status| ds_status.error.as_ref().map_or(Ok(()), Err)),
            DataOperationName::Event {
                name: ref event_name,
                ref event_group_name,
            } => current_status
                .event_groups
                .as_ref()
                .and_then(|event_groups| {
                    event_groups
                        .iter()
                        .find(|eg_status| eg_status.name == *event_group_name)
                })
                .and_then(|eg_status| {
                    eg_status.events.as_ref().and_then(|events| {
                        events.iter().find(|e_status| e_status.name == *event_name)
                    })
                })
                .map(|e_status| e_status.error.as_ref().map_or(Ok(()), Err)),
            DataOperationName::Stream {
                name: ref stream_name,
            } => current_status
                .streams
                .as_ref()
                .and_then(|streams| {
                    streams
                        .iter()
                        .find(|s_status| s_status.name == *stream_name)
                })
                .map(|s_status| s_status.error.as_ref().map_or(Ok(()), Err)),
        }
    }

    async fn internal_report_status(
        connector_context: &Arc<ConnectorContext>,
        asset_ref: &AssetRef,
        mut adr_asset_status: adr_models::AssetStatus,
        asset_status_write_guard: &mut adr_models::AssetStatus,
        data_operation_ref: &DataOperationRef,
        desired_data_operation_status: Result<(), AdrConfigError>,
        log_identifier: &str,
    ) -> Result<(), azure_device_registry::Error> {
        match data_operation_ref.data_operation_name {
            DataOperationName::Dataset {
                name: ref dataset_name,
            } => DataOperationClient::update_dataset_status(
                &mut adr_asset_status,
                dataset_name,
                desired_data_operation_status,
            ),
            DataOperationName::Event {
                name: ref event_name,
                ref event_group_name,
            } => DataOperationClient::update_event_status(
                &mut adr_asset_status,
                event_group_name,
                event_name,
                desired_data_operation_status,
            ),
            DataOperationName::Stream {
                name: ref stream_name,
            } => DataOperationClient::update_stream_status(
                &mut adr_asset_status,
                stream_name,
                desired_data_operation_status,
            ),
        }

        log::debug!("Reporting data operation {data_operation_ref:?} status from app");

        AssetStatusReporter::internal_report_status(
            adr_asset_status,
            connector_context,
            asset_ref,
            asset_status_write_guard,
            log_identifier,
        )
        .await
    }
}

/// Azure Device Registry Data Operation Client represents either a Dataset, Event,
/// or Stream and includes additional functionality
/// to report status, report message schema, receive updates,
/// and send data to the destination
#[derive(Debug, Getters)]
pub struct DataOperationClient {
    /// Data operation kind and data operation, asset, device, and inbound endpoint names
    data_operation_ref: DataOperationRef,
    // Data operation Definition
    definition: DataOperationDefinition,
    /// Current status for the Asset
    #[getter(skip)]
    asset_status: Arc<tokio::sync::RwLock<adr_models::AssetStatus>>,
    /// Current specification for the Asset
    #[getter(skip)]
    asset_specification: Arc<std::sync::RwLock<AssetSpecification>>,
    /// Specification of the device that this data operation is tied to
    #[getter(skip)]
    device_specification: Arc<std::sync::RwLock<DeviceSpecification>>,
    /// Status of the device that this data operation is tied to
    #[getter(skip)]
    device_status: Arc<tokio::sync::RwLock<DeviceEndpointStatus>>,
    // Internally used fields
    /// Internal [`Forwarder`] that handles forwarding data to the destination defined in the data operation definition
    #[getter(skip)]
    forwarder: destination_endpoint::Forwarder,
    #[getter(skip)]
    connector_context: Arc<ConnectorContext>,
    /// Asset reference for internal use
    #[getter(skip)]
    asset_ref: AssetRef,
    /// Internal watcher receiver that holds a snapshot of the latest update and whether it has been
    /// fully processed or not.
    #[getter(skip)]
    data_operation_update_watcher_rx: watch::Receiver<DataOperationUpdateNotification>,
}

impl DataOperationClient {
    #[allow(clippy::too_many_arguments)]
    pub(crate) fn new(
        definition: DataOperationDefinition,
        data_operation_update_watcher_rx: watch::Receiver<DataOperationUpdateNotification>,
        default_destinations: &[Arc<destination_endpoint::Destination>],
        asset_ref: AssetRef,
        asset_status: Arc<tokio::sync::RwLock<adr_models::AssetStatus>>,
        asset_specification: Arc<std::sync::RwLock<AssetSpecification>>,
        device_specification: Arc<std::sync::RwLock<DeviceSpecification>>,
        device_status: Arc<tokio::sync::RwLock<DeviceEndpointStatus>>,
        connector_context: Arc<ConnectorContext>,
    ) -> Result<Self, AdrConfigError> {
        // Create a new data_operation
        let data_operation_ref = DataOperationRef {
            data_operation_name: definition.name(),
            asset_name: asset_ref.name.clone(),
            device_name: asset_ref.device_name.clone(),
            inbound_endpoint_name: asset_ref.inbound_endpoint_name.clone(),
        };
        // technically because these fields are under the lock, if they change, we won't update them unless there's a data operation update
        // however, uuids will always be set and can't change and external ids can only change once from None to set, so this is acceptable
        let (device_uuid, device_external_device_id) = {
            let device_spec = device_specification.read().unwrap();
            (
                device_spec.uuid.clone(),
                device_spec.external_device_id.clone(),
            )
        };
        let (asset_uuid, asset_external_asset_id) = {
            let asset_spec = asset_specification.read().unwrap();
            (
                asset_spec.uuid.clone(),
                asset_spec.external_asset_id.clone(),
            )
        };
        let forwarder = match definition {
            DataOperationDefinition::Dataset(ref dataset) => {
                destination_endpoint::Forwarder::new_dataset_forwarder(
                    dataset,
                    default_destinations,
                    &asset_ref,
                    device_uuid,
                    device_external_device_id,
                    asset_uuid.as_ref(),
                    asset_external_asset_id.as_ref(),
                    connector_context.clone(),
                )
            }
            DataOperationDefinition::Event(ref event) => {
                destination_endpoint::Forwarder::new_event_stream_forwarder(
                    &event.destinations,
                    default_destinations,
                    &data_operation_ref,
                    event.data_source.clone(),
                    event.type_ref.clone(),
                    device_uuid,
                    device_external_device_id,
                    asset_uuid.as_ref(),
                    asset_external_asset_id.as_ref(),
                    connector_context.clone(),
                )
            }
            DataOperationDefinition::Stream(ref stream) => {
                destination_endpoint::Forwarder::new_event_stream_forwarder(
                    &stream.destinations,
                    default_destinations,
                    &data_operation_ref,
                    None,
                    stream.type_ref.clone(),
                    device_uuid,
                    device_external_device_id,
                    asset_uuid.as_ref(),
                    asset_external_asset_id.as_ref(),
                    connector_context.clone(),
                )
            }
        }
        .inspect_err(|e| {
            log::error!("Invalid destination for data_operation: {data_operation_ref:?} {e:?}");
        })?;
        Ok(Self {
            data_operation_ref,
            definition,
            asset_status,
            asset_specification,
            device_specification,
            device_status,
            forwarder,
            connector_context,
            asset_ref,
            data_operation_update_watcher_rx,
        })
    }

    /// Returns the kind of data operation this client represents
    #[must_use]
    pub fn kind(&self) -> DataOperationKind {
        self.definition.kind()
    }

    /// Used to conditionally report the message schema of a data operation as an existing schema reference
    ///
    /// The `modify` function is called with the current message schema reference (if any) and should return:
    /// - `Some(new_message_schema_reference)` if the schema should be updated and reported
    /// - `None` if no update is needed
    ///
    /// # Returns
    /// - [`SchemaModifyResult::Reported`] if the schema was updated and successfully reported, containing the reported [`MessageSchemaReference`]
    /// - [`SchemaModifyResult::NotModified`] if no modification was needed or the version changed during processing
    ///
    /// # Errors
    /// [`MessageSchemaError`] of kind [`AzureDeviceRegistryError::AIOProtocolError`](azure_device_registry::ErrorKind::AIOProtocolError) if
    /// there are any underlying errors from the AIO RPC protocol. This error will be retried
    /// 10 times with exponential backoff and jitter and only returned if it still is failing.
    ///
    /// [`MessageSchemaError`] of kind [`AzureDeviceRegistryError::ServiceError`](azure_device_registry::ErrorKind::ServiceError) if an error is returned
    /// by the Azure Device Registry service.
    ///
    /// # Panics
    /// If the asset specification mutex has been poisoned, which should not be possible
    pub async fn report_message_schema_reference_if_modified<F>(
        &mut self,
        modify: F,
    ) -> Result<SchemaModifyResult, MessageSchemaError>
    where
        F: Fn(Option<&MessageSchemaReference>) -> Option<MessageSchemaReference>,
    {
        // Get the current version of the asset specification
        let cached_version = self.asset_specification.read().unwrap().version;

        {
            // Get the current asset status
            let status_read_guard = self.asset_status.read().await;
            let current_asset_status =
                AssetClient::get_current_asset_status(&status_read_guard, cached_version);

            let modify_input = self.get_schema_reference_modify_input(&current_asset_status);

            match modify(modify_input) {
                Some(_) => {
                    // A modification was made, we proceed to report schema
                }
                None => {
                    // No modification was made, so no need to report schema
                    return Ok(SchemaModifyResult::NotModified);
                }
            }
        };

        // To modify, we need to acquire the write lock
        let mut status_write_guard = self.asset_status.write().await;

        // We can continue here because the asset status will not change. The specification might
        // update but we will be reporting for an old version.

        if cached_version != self.asset_specification.read().unwrap().version {
            // Our modify is no longer valid
            log::debug!(
                "Reporting for an out-of-date asset specification from a Data Operation client, will not modify"
            );
            return Ok(SchemaModifyResult::NotModified);
        }

        // Get the current asset status in case it has changed
        let current_asset_status =
            AssetClient::get_current_asset_status(&status_write_guard, cached_version);

        let modify_input = self.get_schema_reference_modify_input(&current_asset_status);

        let Some(new_message_schema_reference) = modify(modify_input) else {
            // No modification was made, so no need to report schema
            return Ok(SchemaModifyResult::NotModified);
        };

        let mut asset_status_to_report = current_asset_status.into_owned();

        asset_status_to_report.config = match asset_status_to_report.config {
            Some(mut config) => {
                config.last_transition_time = Some(Utc::now());
                Some(config)
            }
            None => {
                // If the config is None, we need to create a new one to report along with the asset status
                Some(azure_device_registry::ConfigStatus {
                    version: cached_version,
                    last_transition_time: Some(Utc::now()),
                    ..Default::default()
                })
            }
        };

        Self::internal_report_message_schema_reference(
            &self.connector_context,
            &self.asset_ref,
            &self.data_operation_ref,
            &mut self.forwarder,
            asset_status_to_report,
            &mut status_write_guard,
            &new_message_schema_reference,
            "DataOperationClient::report_message_schema_reference_if_modified",
        )
        .await?;

        Ok(SchemaModifyResult::Reported(new_message_schema_reference))
    }

    /// Used to conditionally report the message schema of a data operation
    ///
    /// The `modify` function is called with the current message schema reference (if any) and should return:
    /// - `Some(new_message_schema)` if the schema should be updated and reported
    /// - `None` if no update is needed
    ///
    /// # Returns
    /// - [`SchemaModifyResult::Reported`] if the schema was updated and successfully reported, containing the reported [`MessageSchemaReference`]
    /// - [`SchemaModifyResult::NotModified`] if no modification was needed or the version changed during processing
    ///
    /// # Errors
    /// [`MessageSchemaError`] of kind [`SchemaRegistryError::InvalidRequestArgument`](schema_registry::ErrorKind::InvalidRequestArgument)
    /// if the content of the [`MessageSchema`] is empty or there is an error building the request
    ///
    /// [`MessageSchemaError`] of kind [`SchemaRegistryError::ServiceError`](schema_registry::ErrorKind::ServiceError)
    /// if there is an error returned by the Schema Registry Service. This error will be retried 10
    /// times with exponential backoff and jitter if it is an internal error and only returned if
    /// it still is failing.
    ///
    /// [`MessageSchemaError`] of kind [`AzureDeviceRegistryError::AIOProtocolError`](azure_device_registry::ErrorKind::AIOProtocolError) if
    /// there are any underlying errors from the AIO RPC protocol. This error will be retried
    /// 10 times with exponential backoff and jitter and only returned if it still is failing.
    ///
    /// [`MessageSchemaError`] of kind [`AzureDeviceRegistryError::ServiceError`](azure_device_registry::ErrorKind::ServiceError) if
    /// an error is returned by the Azure Device Registry service.
    ///
    /// # Panics
    /// If the asset specification mutex has been poisoned, which should not be possible
    pub async fn report_message_schema_if_modified<F>(
        &mut self,
        modify: F,
    ) -> Result<SchemaModifyResult, MessageSchemaError>
    where
        F: Fn(Option<&MessageSchemaReference>) -> Option<MessageSchema>,
    {
        // Get the current version of the asset specification
        let cached_version = self.asset_specification.read().unwrap().version;

        {
            // Get the current asset status
            let status_read_guard = self.asset_status.read().await;
            let current_asset_status =
                AssetClient::get_current_asset_status(&status_read_guard, cached_version);

            let modify_input = self.get_schema_reference_modify_input(&current_asset_status);

            if modify(modify_input).is_some() {
                // A modification was made, we proceed to report schema
            } else {
                // No modification was made, so no need to report schema
                return Ok(SchemaModifyResult::NotModified);
            }
        };

        // To modify, we need to acquire the write lock
        let mut status_write_guard = self.asset_status.write().await;

        // We can continue here because the asset status will not change. The specification might
        // update but we will be reporting for an old version.

        if cached_version != self.asset_specification.read().unwrap().version {
            // Our modify is no longer valid
            log::debug!(
                "Reporting for an out-of-date asset specification from a Data Operation client, will not modify"
            );
            return Ok(SchemaModifyResult::NotModified);
        }

        // Get the current asset status in case it has changed
        let current_asset_status =
            AssetClient::get_current_asset_status(&status_write_guard, cached_version);

        let modify_input = self.get_schema_reference_modify_input(&current_asset_status);

        let Some(new_message_schema) = modify(modify_input) else {
            // No modification was made, so no need to report schema
            return Ok(SchemaModifyResult::NotModified);
        };

        let mut asset_status_to_report = current_asset_status.into_owned();

        asset_status_to_report.config = match asset_status_to_report.config {
            Some(mut config) => {
                config.last_transition_time = Some(Utc::now());
                Some(config)
            }
            None => {
                // If the config is None, we need to create a new one to report along with the asset status
                Some(azure_device_registry::ConfigStatus {
                    version: cached_version,
                    last_transition_time: Some(Utc::now()),
                    ..Default::default()
                })
            }
        };

        // First put the schema in the schema registry
        let message_schema_reference = Retry::spawn(
            RETRY_STRATEGY.map(tokio_retry2::strategy::jitter),
            async || -> Result<schema_registry::Schema, RetryError<schema_registry::Error>> {
                self.connector_context
                    .schema_registry_client
                    .put(
                        new_message_schema.clone(),
                        self.connector_context.schema_registry_timeout,
                    )
                    .await
                    .map_err(|e| {
                        match e.kind() {
                            // network/retriable
                            schema_registry::ErrorKind::AIOProtocolError(_) => {
                                log::warn!(
                                    "Reporting message schema failed for {:?}. Retrying: {e}",
                                    self.data_operation_ref
                                );
                                RetryError::transient(e)
                            }
                            schema_registry::ErrorKind::ServiceError(service_error) => {
                                if let schema_registry::ErrorCode::InternalError =
                                    service_error.code
                                {
                                    log::warn!(
                                        "Reporting message schema failed for {:?}. Retrying: {e}",
                                        self.data_operation_ref
                                    );
                                    RetryError::transient(e)
                                } else {
                                    RetryError::permanent(e)
                                }
                            }
                            // indicates an error in the provided message schema, return to caller so they can fix
                            schema_registry::ErrorKind::InvalidRequestArgument(_) => {
                                RetryError::permanent(e)
                            }
                        }
                    })
            },
        )
        .await
        .map(|schema| MessageSchemaReference {
            name: schema.name,
            version: schema.version,
            registry_namespace: schema.namespace,
        })?;

        Self::internal_report_message_schema_reference(
            &self.connector_context,
            &self.asset_ref,
            &self.data_operation_ref,
            &mut self.forwarder,
            asset_status_to_report,
            &mut status_write_guard,
            &message_schema_reference,
            "DataOperationClient::report_message_schema_if_modified",
        )
        .await?;

        Ok(SchemaModifyResult::Reported(message_schema_reference))
    }

    #[allow(clippy::too_many_arguments)]
    async fn internal_report_message_schema_reference(
        connector_context: &Arc<ConnectorContext>,
        asset_ref: &AssetRef,
        data_operation_ref: &DataOperationRef,
        forwarder: &mut destination_endpoint::Forwarder,
        asset_status_to_report: adr_models::AssetStatus,
        status_write_guard: &mut adr_models::AssetStatus,
        message_schema_reference: &MessageSchemaReference,
        log_identifier: &str,
    ) -> Result<(), MessageSchemaError> {
        // Use the provided asset_status_to_report instead of creating a new one
        let mut new_status = asset_status_to_report;

        // if data operation is already in the current status, then update the existing data operation with the new message schema
        // Otherwise if the data operation isn't present, or no data operations of that kind have been reported yet, then add it with the new message schema
        match data_operation_ref.data_operation_name {
            DataOperationName::Dataset {
                name: ref dataset_name,
            } => {
                if let Some(dataset_status) = new_status.datasets.as_mut().and_then(|datasets| {
                    datasets
                        .iter_mut()
                        .find(|dataset| dataset.name == *dataset_name)
                }) {
                    // If the dataset already has a status, update the existing dataset with the new message schema
                    dataset_status.message_schema_reference =
                        Some(message_schema_reference.clone());
                } else {
                    // If the dataset doesn't exist in the current status, then add it
                    new_status.datasets.get_or_insert_with(Vec::new).push(
                        adr_models::DatasetEventStreamStatus {
                            name: dataset_name.clone(),
                            message_schema_reference: Some(message_schema_reference.clone()),
                            error: None,
                        },
                    );
                }
            }
            DataOperationName::Event {
                name: ref event_name,
                ref event_group_name,
            } => {
                if let Some(event_group_status) =
                    new_status.event_groups.as_mut().and_then(|event_groups| {
                        event_groups
                            .iter_mut()
                            .find(|event_group| event_group.name == *event_group_name)
                    })
                {
                    if let Some(event_status) =
                        event_group_status.events.as_mut().and_then(|events| {
                            events.iter_mut().find(|event| event.name == *event_name)
                        })
                    {
                        // If the event already has a status, update the existing event with the new message schema
                        event_status.message_schema_reference =
                            Some(message_schema_reference.clone());
                    } else {
                        // If the event doesn't exist in the current status, then add it
                        event_group_status.events.get_or_insert_with(Vec::new).push(
                            adr_models::DatasetEventStreamStatus {
                                name: event_name.clone(),
                                message_schema_reference: Some(message_schema_reference.clone()),
                                error: None,
                            },
                        );
                    }
                } else {
                    // If the event group doesn't exist in the current status, then add it
                    new_status.event_groups.get_or_insert_with(Vec::new).push(
                        adr_models::EventGroupStatus {
                            name: event_group_name.clone(),
                            events: Some(vec![adr_models::DatasetEventStreamStatus {
                                name: event_name.clone(),
                                message_schema_reference: Some(message_schema_reference.clone()),
                                error: None,
                            }]),
                        },
                    );
                }
            }
            DataOperationName::Stream {
                name: ref stream_name,
            } => {
                if let Some(stream_status) = new_status.streams.as_mut().and_then(|streams| {
                    streams
                        .iter_mut()
                        .find(|stream| stream.name == *stream_name)
                }) {
                    // If the stream already has a status, update the existing stream with the new message schema
                    stream_status.message_schema_reference = Some(message_schema_reference.clone());
                } else {
                    // If the stream doesn't exist in the current status, then add it
                    new_status.streams.get_or_insert_with(Vec::new).push(
                        adr_models::DatasetEventStreamStatus {
                            name: stream_name.clone(),
                            message_schema_reference: Some(message_schema_reference.clone()),
                            error: None,
                        },
                    );
                }
            }
        }

        // send status update to the service
        log::debug!("Reporting data operation {data_operation_ref:?} message schema from app");
        AssetStatusReporter::internal_report_status(
            new_status,
            connector_context,
            asset_ref,
            status_write_guard,
            log_identifier,
        )
        .await?;

        forwarder.update_message_schema_reference(Some(message_schema_reference.clone()));

        Ok(())
    }

    fn get_schema_reference_modify_input<'a>(
        &self,
        asset_status: &'a adr_models::AssetStatus,
    ) -> Option<&'a adr_models::MessageSchemaReference> {
        match self.data_operation_ref.data_operation_name {
            DataOperationName::Dataset {
                name: ref dataset_name,
            } => asset_status
                .datasets
                .as_ref()
                .and_then(|datasets| {
                    datasets
                        .iter()
                        .find(|ds_status| ds_status.name == *dataset_name)
                })
                .and_then(|ds_status| ds_status.message_schema_reference.as_ref()),
            DataOperationName::Event {
                name: ref event_name,
                ref event_group_name,
            } => asset_status
                .event_groups
                .as_ref()
                .and_then(|event_groups| {
                    event_groups
                        .iter()
                        .find(|eg_status| eg_status.name == *event_group_name)
                })
                .and_then(|eg_status| {
                    eg_status.events.as_ref().and_then(|events| {
                        events.iter().find(|e_status| e_status.name == *event_name)
                    })
                })
                .and_then(|e_status| e_status.message_schema_reference.as_ref()),
            DataOperationName::Stream {
                name: ref stream_name,
            } => asset_status
                .streams
                .as_ref()
                .and_then(|streams| {
                    streams
                        .iter()
                        .find(|s_status| s_status.name == *stream_name)
                })
                .and_then(|s_status| s_status.message_schema_reference.as_ref()),
        }
    }

    /// Used to send transformed data to the destination
    /// Returns once the message has been sent successfully
    /// Use `forward_data_provide_protocol_specific_identifier` if it is desired to
    /// provide a Protocol Specific Identifier to be used on the Cloud Event `source`
    /// header used if the destination is `MQTT`. If this fn is used, the Cloud Event Header
    /// will default to using either the device external device id or the device name.
    ///
    /// # Errors
    /// [`destination_endpoint::Error`] of kind [`MissingMessageSchema`](destination_endpoint::ErrorKind::MissingMessageSchema)
    /// if the [`MessageSchema`] has not been reported yet. This is required before forwarding any data
    ///
    /// [`destination_endpoint::Error`] of kind [`ValidationError`](destination_endpoint::ErrorKind::ValidationError)
    /// if the [`Data`] isn't valid.
    ///
    /// [`destination_endpoint::Error`] of kind [`BrokerStateStoreError`](destination_endpoint::ErrorKind::BrokerStateStoreError)
    /// if the destination is `BrokerStateStore` and there are any errors setting the data with the service
    ///
    /// [`destination_endpoint::Error`] of kind [`MqttTelemetryError`](destination_endpoint::ErrorKind::MqttTelemetryError)
    /// if the destination is `Mqtt` and there are any errors sending the message to the broker
    pub async fn forward_data(&self, data: Data) -> Result<(), destination_endpoint::Error> {
        self.forwarder.send_data(data, None).await
    }

    /// Used to send transformed data to the destination
    /// Returns once the message has been sent successfully.
    /// `protocol_specific_identifier` will be used on the Cloud Event
    /// `source` header used if the destination is `MQTT`. If `forward_data` is used instead of this fn,
    /// the Cloud Event Header will default to using either the device external device id or the device name.
    /// The inbound endpoint address is a recommended value for this field.
    ///
    /// # Errors
    /// [`destination_endpoint::Error`] of kind [`MissingMessageSchema`](destination_endpoint::ErrorKind::MissingMessageSchema)
    /// if the [`MessageSchema`] has not been reported yet. This is required before forwarding any data
    ///
    /// [`destination_endpoint::Error`] of kind [`ValidationError`](destination_endpoint::ErrorKind::ValidationError)
    /// if the [`Data`] isn't valid.
    ///
    /// [`destination_endpoint::Error`] of kind [`BrokerStateStoreError`](destination_endpoint::ErrorKind::BrokerStateStoreError)
    /// if the destination is `BrokerStateStore` and there are any errors setting the data with the service
    ///
    /// [`destination_endpoint::Error`] of kind [`MqttTelemetryError`](destination_endpoint::ErrorKind::MqttTelemetryError)
    /// if the destination is `Mqtt` and there are any errors sending the message to the broker
    pub async fn forward_data_provide_protocol_specific_identifier(
        &self,
        data: Data,
        protocol_specific_identifier: &str,
    ) -> Result<(), destination_endpoint::Error> {
        self.forwarder
            .send_data(data, Some(protocol_specific_identifier))
            .await
    }

    /// Used to receive notifications about the Data Operation from the Azure Device Registry Service.
    ///
    /// Returns [`DataOperationNotification::Updated`] if the Data Operation's definition has been updated in place.
    ///
    /// Returns [`DataOperationNotification::UpdatedInvalid`] if the Data Operation received an update, but the update was not valid.
    /// The definition is still updated in place, but the [`DataOperationClient`] should not be used until
    /// there is a new update, otherwise the out of date definition will be used for
    /// sending data to the destination.
    ///
    /// Returns [`DataOperationNotification::Deleted`] if the Data Operation has been deleted. The [`DataOperationClient`]
    /// should not be used after this point, and no more notifications will be received.
    ///
    /// # Panics
    /// If the asset specification mutex has been poisoned, which should not be possible
    ///
    /// # Cancel safety
    /// This method is cancel safe. If you use it as the event in a `tokio::select!` statement and some other branch
    /// completes first, then it is guaranteed that no data operation notifications will be lost, and the data operation will not
    /// be updated without a notification being returned.
    pub async fn recv_notification(&mut self) -> DataOperationNotification {
        if self
            .data_operation_update_watcher_rx
            .changed()
            .await
            .is_err()
        {
            return DataOperationNotification::Deleted;
        }
        // In case this function gets cancelled the next time it is called we will process the update again.
        self.data_operation_update_watcher_rx.mark_changed();
        let (updated_data_operation, default_destinations, mut watch_receiver) =
            self.data_operation_update_watcher_rx.borrow().clone();

        // wait until the update has been released. If the watch sender has been dropped, this means the Asset has been deleted/dropped
        if watch_receiver.changed().await.is_err() {
            self.data_operation_update_watcher_rx.mark_unchanged();
            return DataOperationNotification::Deleted;
        }
        // create new forwarder, in case destination has changed
        // technically because these fields are under the lock, if they change, we won't update them unless there's a data operation update
        // however, uuids will always be set and can't change and external ids can only change once from None to set, so this is acceptable
        let (device_uuid, device_external_device_id) = {
            let device_spec = self.device_specification.read().unwrap();
            (
                device_spec.uuid.clone(),
                device_spec.external_device_id.clone(),
            )
        };
        let (asset_uuid, asset_external_asset_id) = {
            let asset_spec = self.asset_specification.read().unwrap();
            (
                asset_spec.uuid.clone(),
                asset_spec.external_asset_id.clone(),
            )
        };
        let forwarder_result = match updated_data_operation {
            DataOperationDefinition::Dataset(ref updated_dataset) => {
                destination_endpoint::Forwarder::new_dataset_forwarder(
                    updated_dataset,
                    &default_destinations,
                    &self.asset_ref,
                    device_uuid,
                    device_external_device_id,
                    asset_uuid.as_ref(),
                    asset_external_asset_id.as_ref(),
                    self.connector_context.clone(),
                )
            }
            DataOperationDefinition::Event(ref updated_event) => {
                destination_endpoint::Forwarder::new_event_stream_forwarder(
                    &updated_event.destinations,
                    &default_destinations,
                    &self.data_operation_ref,
                    updated_event.data_source.clone(),
                    updated_event.type_ref.clone(),
                    device_uuid,
                    device_external_device_id,
                    asset_uuid.as_ref(),
                    asset_external_asset_id.as_ref(),
                    self.connector_context.clone(),
                )
            }
            DataOperationDefinition::Stream(ref updated_stream) => {
                destination_endpoint::Forwarder::new_event_stream_forwarder(
                    &updated_stream.destinations,
                    &default_destinations,
                    &self.data_operation_ref,
                    None,
                    updated_stream.type_ref.clone(),
                    device_uuid,
                    device_external_device_id,
                    asset_uuid.as_ref(),
                    asset_external_asset_id.as_ref(),
                    self.connector_context.clone(),
                )
            }
        };
        self.forwarder = match forwarder_result {
            Ok(forwarder) => forwarder,
            Err(e) => {
                log::error!(
                    "Invalid data_operation destination for updated data_operation: {:?} {e:?}",
                    self.data_operation_ref
                );

                tokio::task::spawn({
                    let asset_status_mutex_clone = self.asset_status.clone();
                    let asset_specification_mutex_clone = self.asset_specification.clone();
                    let data_operation_ref_clone = self.data_operation_ref.clone();
                    let connector_context = self.connector_context.clone();
                    let asset_ref = self.asset_ref.clone();
                    async move {
                        log::debug!(
                            "Reporting data operation {data_operation_ref_clone:?} status from recv_notification"
                        );
                        let mut status_write_guard = asset_status_mutex_clone.write().await;
                        let adr_version = asset_specification_mutex_clone.read().unwrap().version;
                        let mut adr_asset_status =
                            AssetClient::get_current_asset_status(&status_write_guard, adr_version)
                                .into_owned();
                        // Update the config status
                        adr_asset_status.config = match adr_asset_status.config {
                            Some(mut config) => {
                                config.last_transition_time = Some(Utc::now());
                                Some(config)
                            }
                            None => {
                                // If the config is None, we need to create a new one to report along
                                // with the data operations status
                                Some(azure_device_registry::ConfigStatus {
                                    version: adr_version,
                                    last_transition_time: Some(Utc::now()),
                                    ..Default::default()
                                })
                            }
                        };
                        if let Err(e) = DataOperationStatusReporter::internal_report_status(
                            &connector_context,
                            &asset_ref,
                            adr_asset_status,
                            &mut status_write_guard,
                            &data_operation_ref_clone,
                            Err(e),
                            "DataOperationClient::recv_notification",
                        )
                        .await
                        {
                            log::error!(
                                "Failed to report status for updated data_operation {data_operation_ref_clone:?}: {e}"
                            );
                        }
                    }
                });
                // notify the application to not use this data_operation until a new update is received
                self.definition = updated_data_operation;
                self.data_operation_update_watcher_rx.mark_unchanged();
                return DataOperationNotification::UpdatedInvalid;
            }
        };
        self.definition = updated_data_operation;
        // Once the data_operation definition has been updated we can mark the value in the watcher as seen
        self.data_operation_update_watcher_rx.mark_unchanged();
        DataOperationNotification::Updated
    }

    /// Creates a new status reporter for this [`DataOperationClient`]
    #[must_use]
    pub fn get_status_reporter(&self) -> DataOperationStatusReporter {
        DataOperationStatusReporter {
            connector_context: self.connector_context.clone(),
            asset_status: self.asset_status.clone(),
            asset_specification: self.asset_specification.clone(),
            data_operation_ref: self.data_operation_ref.clone(),
            asset_ref: self.asset_ref.clone(),
        }
    }

    /// Returns a clone of this Data Operation's [`MessageSchemaReference`] from
    /// the `AssetStatus`, if it exists
    #[must_use]
    pub async fn message_schema_reference(&self) -> Option<MessageSchemaReference> {
        // unwrap can't fail unless lock is poisoned
        match self.data_operation_ref.data_operation_name {
            DataOperationName::Dataset {
                name: ref dataset_name,
            } => self
                .asset_status
                .read()
                .await
                .datasets
                .as_ref()?
                .iter()
                .find(|dataset| dataset.name == *dataset_name)?
                .message_schema_reference
                .clone(),
            DataOperationName::Event {
                name: ref event_name,
                ref event_group_name,
            } => self
                .asset_status
                .read()
                .await
                .event_groups
                .as_ref()?
                .iter()
                .find(|event_group| event_group.name == *event_group_name)?
                .events
                .as_ref()?
                .iter()
                .find(|event| event.name == *event_name)?
                .message_schema_reference
                .clone(),
            DataOperationName::Stream {
                name: ref stream_name,
            } => self
                .asset_status
                .read()
                .await
                .streams
                .as_ref()?
                .iter()
                .find(|stream| stream.name == *stream_name)?
                .message_schema_reference
                .clone(),
        }
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
    pub async fn device_status(&self) -> DeviceEndpointStatus {
        (*self.device_status.read().await).clone()
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

    /// Helper function to update the specific event status within the asset status
    fn update_event_status(
        asset_status_to_update: &mut adr_models::AssetStatus,
        event_group_name: &str,
        event_name: &str,
        event_status: Result<(), AdrConfigError>,
    ) {
        if let Some(curr_event_group_status) = asset_status_to_update
            .event_groups
            .as_mut()
            .and_then(|event_groups| {
                event_groups
                    .iter_mut()
                    .find(|event_group| event_group.name == event_group_name)
            })
        {
            if let Some(curr_event_status) = curr_event_group_status
                .events
                .as_mut()
                .and_then(|events| events.iter_mut().find(|event| event.name == event_name))
            {
                // If the event already has a status, update the existing event with the new error
                curr_event_status.error = event_status.err();
            } else {
                // If the event doesn't exist in the current event group status, then add it
                curr_event_group_status
                    .events
                    .get_or_insert_with(Vec::new)
                    .push(adr_models::DatasetEventStreamStatus {
                        name: event_name.to_string(),
                        message_schema_reference: None,
                        error: event_status.err(),
                    });
            }
        } else {
            // If the event group doesn't exist in the current status, then add it
            asset_status_to_update
                .event_groups
                .get_or_insert_with(Vec::new)
                .push(adr_models::EventGroupStatus {
                    name: event_group_name.to_string(),
                    events: Some(vec![adr_models::DatasetEventStreamStatus {
                        name: event_name.to_string(),
                        message_schema_reference: None,
                        error: event_status.err(),
                    }]),
                });
        }
    }

    /// Helper function to update the specific stream status within the asset status
    fn update_stream_status(
        asset_status_to_update: &mut adr_models::AssetStatus,
        stream_name: &str,
        stream_status: Result<(), AdrConfigError>,
    ) {
        if let Some(curr_stream_status) = asset_status_to_update
            .streams
            .as_mut()
            .and_then(|streams| streams.iter_mut().find(|stream| stream.name == stream_name))
        {
            // If the stream already has a status, update the existing stream with the new error
            curr_stream_status.error = stream_status.err();
        } else {
            // If the stream doesn't exist in the current status, then add it
            asset_status_to_update
                .streams
                .get_or_insert_with(Vec::new)
                .push(adr_models::DatasetEventStreamStatus {
                    name: stream_name.to_string(),
                    message_schema_reference: None,
                    error: stream_status.err(),
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
        device_endpoint_credentials_mount_path: Option<&FileMount>,
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
                intermediate_certificates_secret_name,
                key_secret_name,
            } => Authentication::Certificate {
                certificate_path: device_endpoint_credentials_mount_path.expect("device_endpoint_credentials_mount_path must be present if Authentication is Certificate").as_path().join(certificate_secret_name),
                intermediate_certificates_path: intermediate_certificates_secret_name.map(|intermediate_cert_secret_name| {
                    device_endpoint_credentials_mount_path.expect("device_endpoint_credentials_mount_path must be present if Authentication is Certificate").as_path().join(intermediate_cert_secret_name)
                }),
                key_path: key_secret_name.map(|key_secret_name| {
                    device_endpoint_credentials_mount_path.expect("device_endpoint_credentials_mount_path must be present if Authentication is Certificate").as_path().join(key_secret_name)
                }),

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
        /// The path to a file containing containing the client certificate in PEM format.
        certificate_path: PathBuf, // different from adr
        /// The path to a file containing the combined intermediate certificates in PEM format (if any).
        intermediate_certificates_path: Option<PathBuf>, // different from adr
        /// The path to a file containing the private key in PEM or DER format.
        key_path: Option<PathBuf>, // different from adr
    },
    /// Represents authentication using a username and password.
    UsernamePassword {
        /// The 'passwordSecretName' Field.
        password_path: PathBuf, // different from adr
        /// The 'usernameSecretName' Field.
        username_path: PathBuf, // different from adr
    },
}

#[derive(Clone, Debug, Default, PartialEq)]
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

    /// Internal helper to get an [`DeviceEndpointStatus`] that can be used as a starting place
    /// to modify the current status with whatever new things we want to report.
    ///
    /// Note that it returns a `Cow`. The reason is that most of the times that we are reporting
    /// a status we will not end up modifying it. `Cow` allows us to only clone when we are going to
    /// modify.
    pub(crate) fn get_current_device_endpoint_status(
        &self,
        adr_version: Option<u64>,
    ) -> Cow<'_, Self> {
        match &self.config {
            Some(config) => {
                if config.version == adr_version {
                    // If the version in our config matches the one in ADR we return our current
                    // device endpoint status
                    Cow::Borrowed(self)
                } else {
                    // If the version doesn't match, the config version we are holding is stale so
                    // we pass a cleared version
                    Cow::Owned(DeviceEndpointStatus::default())
                }
            }
            None => {
                // If there is no config, then the status is not set, we can pass our current device
                // endpoint status
                Cow::Borrowed(self)
            }
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
            uuid: value.uuid,
            version: value.version,
        }
    }
}

/// Represents the specification of an Event and its Event Group in the Azure Device Registry service.
#[derive(Debug, Clone, PartialEq)]
pub struct EventSpecification {
    /// Destinations for an event.
    pub destinations: Vec<adr_models::EventStreamDestination>, // if None on generated model, we can represent as empty vec. Can currently only be length of 1
    /// Stringified JSON that contains connector-specific configuration for the specific event.
    pub event_configuration: Option<String>,
    /// Reference to a data source for a given event.
    pub data_source: Option<String>,
    /// The name of the event.
    pub name: String,
    /// URI or type definition ID.
    pub type_ref: Option<String>,
    /// The Event Group this event belongs to.
    pub event_group: EventGroupSpecification,
    name_tuple: (String, String),
}

impl EventSpecification {
    pub(crate) fn hash_name(&self) -> &(String, String) {
        &self.name_tuple
    }
}

impl From<(adr_models::EventGroup, adr_models::Event)> for EventSpecification {
    fn from(val: (adr_models::EventGroup, adr_models::Event)) -> Self {
        EventSpecification {
            name_tuple: (val.0.name.clone(), val.1.name.clone()),
            destinations: val.1.destinations,
            event_configuration: val.1.event_configuration,
            data_source: val.1.data_source,
            name: val.1.name,
            type_ref: val.1.type_ref,
            event_group: EventGroupSpecification {
                default_events_destinations: val.0.default_events_destinations,
                event_group_configuration: val.0.event_group_configuration,
                data_source: val.0.data_source,
                name: val.0.name,
                type_ref: val.0.type_ref,
            },
        }
    }
}

/// Represents the specification of an Event Group in the Azure Device Registry service.
#[derive(Debug, Clone, PartialEq)]
pub struct EventGroupSpecification {
    /// Default destinations for an event on this Event Group.
    pub default_events_destinations: Vec<adr_models::EventStreamDestination>, // if None on generated model, we can represent as empty vec. Can currently only be length of 1
    /// Stringified JSON that contains connector-specific configuration for the specific event group.
    pub event_group_configuration: Option<String>,
    /// The address of the notifier of the event in the asset (e.g. URL) so that a client can access the notifier on the asset.
    pub data_source: Option<String>,
    /// The name of the event group.
    pub name: String,
    /// URI or type definition ID.
    pub type_ref: Option<String>,
}

/// Holds the `DataOperation`'s definition, regardless of the type
#[derive(Debug, Clone)]
pub enum DataOperationDefinition {
    /// Dataset definition
    Dataset(adr_models::Dataset),
    /// Event definition
    Event(EventSpecification),
    /// Stream definition
    Stream(adr_models::Stream),
}

impl DataOperationDefinition {
    /// Returns the [`DataOperationName`] of the data operation
    #[must_use]
    pub fn name(&self) -> DataOperationName {
        match self {
            DataOperationDefinition::Dataset(dataset) => DataOperationName::Dataset {
                name: dataset.name.clone(),
            },
            DataOperationDefinition::Event(event) => DataOperationName::Event {
                name: event.name.clone(),
                event_group_name: event.event_group.name.clone(),
            },
            DataOperationDefinition::Stream(stream) => DataOperationName::Stream {
                name: stream.name.clone(),
            },
        }
    }

    /// Returns the [`DataOperationKind`] of the data operation definition
    #[must_use]
    pub fn kind(&self) -> DataOperationKind {
        match self {
            DataOperationDefinition::Dataset(_) => DataOperationKind::Dataset,
            DataOperationDefinition::Event(_) => DataOperationKind::Event,
            DataOperationDefinition::Stream(_) => DataOperationKind::Stream,
        }
    }
}

/// A trait for handling different types of data operations generically.
///
/// The `DataOperation` trait provides a way to interact with data operations
/// (datasets, events, and streams) without needing to know their specific type.
/// This abstraction is useful for scenarios where operations need to be performed
/// uniformly across different data operation types.
///
/// Unlike directly implementing methods on the `DataOperationDefinition` enum,
/// this trait allows individual data operation types (e.g., `Dataset`, `Event`, `Stream`)
/// to define their own behavior while still conforming to a common interface.
trait DataOperation: Into<DataOperationDefinition> {
    type HashName: PartialEq + Eq + Hash + Clone + std::fmt::Debug;
    fn kind() -> DataOperationKind;
    fn hash_name(&self) -> &Self::HashName;
    fn data_operation_name(&self) -> DataOperationName;
}

impl From<adr_models::Dataset> for DataOperationDefinition {
    fn from(val: adr_models::Dataset) -> Self {
        DataOperationDefinition::Dataset(val)
    }
}
impl DataOperation for adr_models::Dataset {
    type HashName = String;
    fn kind() -> DataOperationKind {
        DataOperationKind::Dataset
    }
    fn hash_name(&self) -> &Self::HashName {
        &self.name
    }
    fn data_operation_name(&self) -> DataOperationName {
        DataOperationName::Dataset {
            name: self.name.clone(),
        }
    }
}

impl From<EventSpecification> for DataOperationDefinition {
    fn from(val: EventSpecification) -> Self {
        DataOperationDefinition::Event(val)
    }
}

impl DataOperation for EventSpecification {
    type HashName = (String, String);
    fn kind() -> DataOperationKind {
        DataOperationKind::Event
    }
    fn hash_name(&self) -> &Self::HashName {
        self.hash_name()
    }

    fn data_operation_name(&self) -> DataOperationName {
        DataOperationName::Event {
            name: self.name.clone(),
            event_group_name: self.event_group.name.clone(),
        }
    }
}

impl From<adr_models::Stream> for DataOperationDefinition {
    fn from(val: adr_models::Stream) -> Self {
        DataOperationDefinition::Stream(val)
    }
}
impl DataOperation for adr_models::Stream {
    type HashName = String;
    fn kind() -> DataOperationKind {
        DataOperationKind::Stream
    }
    fn hash_name(&self) -> &Self::HashName {
        &self.name
    }
    fn data_operation_name(&self) -> DataOperationName {
        DataOperationName::Stream {
            name: self.name.clone(),
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

    #[test_case(None, 1, false, true; "new")]
    #[test_case(Some(azure_device_registry::ConfigStatus {
            version: Some(2),
            ..Default::default()
        }), 2, false, true; "version_match")]
    #[test_case(Some(azure_device_registry::ConfigStatus {
            version: Some(1),
            ..Default::default()
        }), 2,
        false,
        false; "version_mismatch")]
    #[test_case(Some(azure_device_registry::ConfigStatus {
            version: Some(2),
            ..Default::default()
        }), 2, true, true; "version_match_with_dataset")]
    #[test_case(Some(azure_device_registry::ConfigStatus {
            version: Some(1),
            ..Default::default()
        }), 2,
        true,
        false; "version_mismatch_with_dataset")]
    fn get_current_asset_status_asset(
        config: Option<azure_device_registry::ConfigStatus>,
        new_spec_version: u64,
        datasets_set: bool,
        expect_keep_received: bool,
    ) {
        let current_status = if datasets_set {
            adr_models::AssetStatus {
                config,
                datasets: Some(vec![adr_models::DatasetEventStreamStatus {
                    name: "test".to_string(),
                    message_schema_reference: None,
                    error: None,
                }]),
                ..Default::default()
            }
        } else {
            adr_models::AssetStatus {
                config,
                ..Default::default()
            }
        };

        let new_status_base =
            AssetClient::get_current_asset_status(&current_status, Some(new_spec_version))
                .into_owned();
        if expect_keep_received {
            assert_eq!(new_status_base, current_status);
        } else {
            assert_eq!(new_status_base, adr_models::AssetStatus::default());
        }
    }

    #[test_case(None, 1, Some(1), true; "no_data_operation")]
    #[test_case(Some(adr_models::DatasetEventStreamStatus {
            name: "test".to_string(),
            message_schema_reference: None,
            error: None,
    }), 1, Some(1), true; "version_match")]
    #[test_case(Some(adr_models::DatasetEventStreamStatus {
            name: "test".to_string(),
            message_schema_reference: None,
            error: None,
    }), 1, Some(2), false; "version_mismatch")]
    fn get_current_asset_status_data_operation(
        data_operation_status: Option<adr_models::DatasetEventStreamStatus>,
        current_version: u64,
        new_spec_version: Option<u64>,
        expect_keep_received: bool,
    ) {
        let datasets = data_operation_status.map(|status| vec![status]);
        let current_status = adr_models::AssetStatus {
            config: Some(azure_device_registry::ConfigStatus {
                version: Some(current_version),
                ..Default::default()
            }),
            datasets,
            ..Default::default()
        };

        let new_status_base =
            AssetClient::get_current_asset_status(&current_status, new_spec_version).into_owned();

        if expect_keep_received {
            assert_eq!(new_status_base, current_status);
        } else {
            assert_eq!(new_status_base, adr_models::AssetStatus::default());
        }
    }

    #[test_case(None, None, true; "new")]
    #[test_case(Some(azure_device_registry::ConfigStatus {
            version: Some(1),
            ..Default::default()
        }), Some(1), true; "version_match")]
    #[test_case(Some(azure_device_registry::ConfigStatus {
            version: Some(1),
            ..Default::default()
        }), Some(2), false; "version_mismatch")]
    fn get_current_device_endpoint_status_device(
        config: Option<azure_device_registry::ConfigStatus>,
        adr_version: Option<u64>,
        expect_keep_received: bool,
    ) {
        let current_status = DeviceEndpointStatus {
            config,
            inbound_endpoint_status: None,
        };

        let new_status_base = current_status
            .get_current_device_endpoint_status(adr_version)
            .into_owned();

        if expect_keep_received {
            assert_eq!(new_status_base, current_status);
        } else {
            assert_eq!(new_status_base, DeviceEndpointStatus::default());
        }
    }

    #[test_case(None, 1, Some(1), true; "no_endpoint")]
    #[test_case(Some(Ok(())), 1, Some(1), true; "version_match")]
    #[test_case(Some(Ok(())), 1, Some(2), false; "version_mismatch")]
    fn get_current_device_endpoint_status_endpoint(
        endpoint_status: Option<Result<(), AdrConfigError>>,
        current_version: u64,
        new_spec_version: Option<u64>,
        expect_keep_received: bool,
    ) {
        let current_status = DeviceEndpointStatus {
            config: Some(azure_device_registry::ConfigStatus {
                version: Some(current_version),
                ..Default::default()
            }),
            inbound_endpoint_status: endpoint_status,
        };

        let new_status_base = current_status
            .get_current_device_endpoint_status(new_spec_version)
            .into_owned();

        if expect_keep_received {
            assert_eq!(new_status_base, current_status);
        } else {
            assert_eq!(new_status_base, DeviceEndpointStatus::default());
        }
    }
}
