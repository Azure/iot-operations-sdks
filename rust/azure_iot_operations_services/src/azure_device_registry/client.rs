// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

//! Client for Azure Device Registry operations.
//!
//! To use this client, the `azure_device_registry` feature must be enabled.

use derive_builder::Builder;
use std::{collections::HashMap, sync::Arc, time::Duration};

use azure_iot_operations_mqtt::interface::AckToken;
use azure_iot_operations_mqtt::interface::ManagedClient;
use azure_iot_operations_protocol::application::ApplicationContext;
use tokio::{sync::Notify, task};

use crate::azure_device_registry::device_name_gen::adr_base_service::client as adr_name_gen;
use crate::azure_device_registry::{
    AssetStatus, AssetUpdateObservation, Device, DeviceStatus, DeviceUpdateObservation, Error,
    ErrorKind,
    device_name_gen::{
        adr_base_service::client::GetDeviceRequestBuilder,
        common_types::options::CommandInvokerOptionsBuilder,
        common_types::options::TelemetryReceiverOptionsBuilder,
    },
};
use crate::common::dispatcher::{DispatchError, Dispatcher};

use super::Asset;

/// Options for the Azure Device Registry client.
#[derive(Builder, Clone, Default)]
#[builder(setter(into))]
pub struct ClientOptions {
    /// If true, update notifications are auto-acknowledged
    #[builder(default = "true")]
    notification_auto_ack: bool,
}

/// Azure Device Registry client implementation.
#[derive(Clone)]
pub struct Client<C>
where
    C: ManagedClient + Clone + Send + Sync + 'static,
    C::PubReceiver: Send + Sync,
{
    get_device_command_invoker: Arc<adr_name_gen::GetDeviceCommandInvoker<C>>,
    update_device_status_command_invoker: Arc<adr_name_gen::UpdateDeviceStatusCommandInvoker<C>>,
    _notify_on_device_update_command_invoker:
        Arc<adr_name_gen::SetNotificationPreferenceForDeviceUpdatesCommandInvoker<C>>,
    get_asset_command_invoker: Arc<adr_name_gen::GetAssetCommandInvoker<C>>,
    update_asset_status_command_invoker: Arc<adr_name_gen::UpdateAssetStatusCommandInvoker<C>>,
    notify_on_asset_update_command_invoker:
        Arc<adr_name_gen::SetNotificationPreferenceForAssetUpdatesCommandInvoker<C>>,
    asset_update_event_telemetry_dispatcher: Arc<Dispatcher<(Asset, Option<AckToken>)>>,
}

impl<C> Client<C>
where
    C: ManagedClient + Clone + Send + Sync + 'static,
    C::PubReceiver: Send + Sync,
{
    // ~~~~~~~~~~~~~~~~~ General APIs ~~~~~~~~~~~~~~~~~~~~~
    // Create a new Azure Device Registry Client.
    /// # Errors
    /// TODO
    /// # Panics
    /// Panics if the options for the underlying command invokers and telemetry receivers cannot be built. Not possible since
    /// the options are statically generated.
    pub fn new(
        application_context: ApplicationContext,
        client: &C,
        options: &ClientOptions,
    ) -> Result<Self, Error> {
        let command_options = CommandInvokerOptionsBuilder::default()
            .topic_token_map(HashMap::from([(
                "connectorClientId".to_string(),
                client.client_id().to_string(),
            )]))
            .build()
            .map_err(ErrorKind::from)?;

        let telemetry_options = TelemetryReceiverOptionsBuilder::default()
            .topic_token_map(HashMap::from([(
                "connectorClientId".to_string(),
                client.client_id().to_string(),
            )]))
            .auto_ack(options.notification_auto_ack)
            .build()
            .expect("DTDL schema generated invalid arguments");

        let asset_shutdown_notifier = Arc::new(Notify::new());
        let asset_update_event_telemetry_dispatcher = Arc::new(Dispatcher::new());
        let asset_update_event_telemetry_dispatcher_clone =
            asset_update_event_telemetry_dispatcher.clone();

        // Start the update notification loop
        task::spawn({
            let asset_update_event_telemetry_receiver =
                adr_name_gen::AssetUpdateEventTelemetryReceiver::new(
                    application_context.clone(),
                    client.clone(),
                    &telemetry_options,
                );

            async move {
                Self::receive_update_event_telemetry_loop(
                    asset_shutdown_notifier,
                    asset_update_event_telemetry_receiver,
                    asset_update_event_telemetry_dispatcher,
                )
                .await;
            }
        });

        Ok(Self {
            get_device_command_invoker: Arc::new(adr_name_gen::GetDeviceCommandInvoker::new(
                application_context.clone(),
                client.clone(),
                &command_options,
            )),
            update_device_status_command_invoker: Arc::new(
                adr_name_gen::UpdateDeviceStatusCommandInvoker::new(
                    application_context.clone(),
                    client.clone(),
                    &command_options,
                ),
            ),
            _notify_on_device_update_command_invoker: Arc::new(
                adr_name_gen::SetNotificationPreferenceForDeviceUpdatesCommandInvoker::new(
                    application_context.clone(),
                    client.clone(),
                    &command_options,
                ),
            ),
            get_asset_command_invoker: Arc::new(adr_name_gen::GetAssetCommandInvoker::new(
                application_context.clone(),
                client.clone(),
                &command_options,
            )),
            update_asset_status_command_invoker: Arc::new(
                adr_name_gen::UpdateAssetStatusCommandInvoker::new(
                    application_context.clone(),
                    client.clone(),
                    &command_options,
                ),
            ),
            notify_on_asset_update_command_invoker: Arc::new(
                adr_name_gen::SetNotificationPreferenceForAssetUpdatesCommandInvoker::new(
                    application_context,
                    client.clone(),
                    &command_options,
                ),
            ),
            asset_update_event_telemetry_dispatcher: asset_update_event_telemetry_dispatcher_clone,
        })
    }

    /// Shutdown the [`Client`]. Shuts down the underlying command invokers.
    ///
    /// Note: If this method is called, the [`Client`] should not be used again.
    /// If the method returns an error, it may be called again to re-attempt unsubscribing.
    ///
    /// Returns Ok(()) on success, otherwise returns [`struct@Error`].
    /// # Errors
    /// [`struct@Error`] of kind [`AIOProtocolError`](ErrorKind::AIOProtocolError)
    /// if the unsubscribe fails or if the unsuback reason code doesn't indicate success.
    #[allow(clippy::unused_async)]
    pub async fn shutdown(&self) -> Result<(), Error> {
        Err(Error(ErrorKind::PlaceholderError))
    }

    // ~~~~~~~~~~~~~~~~~ Device APIs ~~~~~~~~~~~~~~~~~~~~~

    /// Retrieves a Device from a Azure Device Registry service.
    ///
    /// # Arguments
    /// * `device_name` - The name of the Device.
    /// * `inbound_endpoint_name` - The name of the inbound endpoint.
    /// * `timeout` - The duration until the Client stops waiting for a response to the request, it is rounded up to the nearest second.
    ///
    /// Returns a [`Device`] if the device was found.
    ///
    /// # Errors
    /// TODO
    pub async fn get_device(
        &self,
        device_name: String,
        inbound_endpoint_name: String,
        timeout: Duration,
    ) -> Result<Device, Error> {
        let get_device_request = GetDeviceRequestBuilder::default()
            .topic_tokens(HashMap::from([
                ("deviceName".to_string(), device_name),
                ("inboundEndpointName".to_string(), inbound_endpoint_name),
            ]))
            .timeout(timeout)
            .build()
            .map_err(ErrorKind::from)?;

        let response = self
            .get_device_command_invoker
            .invoke(get_device_request)
            .await
            .map_err(ErrorKind::from)?;
        Ok(response.payload.device.into())
    }

    /// Updates a Device's status in the Azure Device Registry service.
    ///
    /// # Arguments
    /// * `device_name` - The name of the Device.
    /// * `inbound_endpoint_name` - The name of the inbound endpoint.
    /// * [`DeviceStatus`] - All status information for the device.
    /// * `timeout` - The duration until the Client stops waiting for a response to the request, it is rounded up to the nearest second.
    ///
    /// Returns the updated [`Device`] once updated.
    ///
    /// # Errors
    /// TODO
    pub async fn update_device_plus_endpoint_status(
        &self,
        device_name: String,
        inbound_endpoint_name: String,
        status: DeviceStatus, // TODO: should this be DeviceEndpointStatus that doesn't have hashmap of endpoints?
        timeout: Duration,
    ) -> Result<Device, Error> {
        let status_payload = adr_name_gen::UpdateDeviceStatusRequestPayload {
            device_status_update: status.into(),
        };
        let update_device_status_request =
            adr_name_gen::UpdateDeviceStatusRequestBuilder::default()
                .payload(status_payload)
                .map_err(ErrorKind::from)?
                .topic_tokens(HashMap::from([
                    ("deviceName".to_string(), device_name),
                    ("inboundEndpointName".to_string(), inbound_endpoint_name),
                ]))
                .timeout(timeout)
                .build()
                .map_err(ErrorKind::from)?;
        let response = self
            .update_device_status_command_invoker
            .invoke(update_device_status_request)
            .await
            .map_err(ErrorKind::from)?;
        Ok(response.payload.updated_device.into())
    }

    /// Starts observation of any Device updates from the Azure Device Registry service.
    ///
    /// # Arguments
    /// * `device_name` - The name of the Device.
    /// * `inbound_endpoint_name` - The name of the inbound endpoint.
    /// * `timeout` - The duration until the Client stops waiting for a response to the request, it is rounded up to the nearest second.
    ///
    /// Returns the [`DeviceUpdateObservation`] if the observation was started successfully or [`Error`].
    ///
    /// # Errors
    /// TODO
    #[allow(clippy::unused_async)]
    pub async fn observe_device_update_notifications(
        &self,
        _device_name: String,
        _inbound_endpoint_name: String,
        _timeout: Duration,
    ) -> Result<DeviceUpdateObservation, Error> {
        Err(Error(ErrorKind::PlaceholderError))
    }

    /// Stops observation of any Device updates from the Azure Device Registry service.
    ///
    /// # Arguments
    /// * `device_name` - The name of the Device.
    /// * `inbound_endpoint_name` - The name of the inbound endpoint.
    /// * `timeout` - The duration until the Client stops waiting for a response to the request, it is rounded up to the nearest second.
    ///
    /// Returns `Ok(())` if the Device Updates are no longer being observed.
    ///
    /// # Errors
    /// TODO
    #[allow(clippy::unused_async)]
    pub async fn unobserve_device_update_notifications(
        &self,
        _device_name: String,
        _inbound_endpoint_name: String,
        _timeout: Duration,
    ) -> Result<(), Error> {
        Err(Error(ErrorKind::PlaceholderError))
    }

    // ~~~~~~~~~~~~~~~~~ Asset APIs ~~~~~~~~~~~~~~~~~~~~~

    /// Retrieves an asset from a Azure Device Registry service.
    ///
    /// # Arguments
    /// * `device_name` - The name of the Device.
    /// * `inbound_endpoint_name` - The name of the inbound endpoint.
    /// * `asset_name` - The name of the asset.
    /// * `timeout` - The duration until the Client stops waiting for a response to the request, it is rounded up to the nearest second.
    ///
    /// Returns a [`Asset`] if the the asset was found.
    ///
    /// # Errors
    /// TODO
    pub async fn get_asset(
        &self,
        device_name: String,
        inbound_endpoint_name: String,
        asset_name: String,
        timeout: Duration,
    ) -> Result<Asset, Error> {
        let payload = adr_name_gen::GetAssetRequestPayload { asset_name };
        let command_request = adr_name_gen::GetAssetRequestBuilder::default()
            .payload(payload)
            .map_err(ErrorKind::from)?
            .topic_tokens(HashMap::from([
                ("deviceName".to_string(), device_name),
                ("inboundEndpointName".to_string(), inbound_endpoint_name),
            ]))
            .timeout(timeout)
            .build()
            .map_err(ErrorKind::from)?;

        let response = self
            .get_asset_command_invoker
            .invoke(command_request)
            .await
            .map_err(ErrorKind::from)?;

        Ok(response.payload.asset.into())
    }

    /// Updates the status of an asset in the Azure Device Registry service.
    ///
    /// # Arguments
    /// * `device_name` - The name of the Device.
    /// * `inbound_endpoint_name` - The name of the inbound endpoint.
    /// * `asset_name` - The name of the asset.
    /// * [`AssetStatus`] - The status of an asset for the update.
    /// * `timeout` - The duration until the Client stops waiting for a response to the request, it is rounded up to the nearest second.
    ///
    /// Returns the updated [`Asset`] once updated.
    ///
    /// # Errors
    /// TODO
    #[allow(clippy::unused_async)]
    pub async fn update_asset_status(
        &self,
        device_name: String,
        inbound_endpoint_name: String,
        asset_name: String,
        status: AssetStatus,
        timeout: Duration,
    ) -> Result<Asset, Error> {
        let payload = adr_name_gen::UpdateAssetStatusRequestPayload {
            asset_status_update: adr_name_gen::UpdateAssetStatusRequestSchema {
                asset_name: asset_name.clone(),
                asset_status: status.into(),
            },
        };
        let command_request = adr_name_gen::UpdateAssetStatusRequestBuilder::default()
            .payload(payload)
            .map_err(ErrorKind::from)?
            .topic_tokens(HashMap::from([
                ("deviceName".to_string(), device_name),
                ("inboundEndpointName".to_string(), inbound_endpoint_name),
            ]))
            .timeout(timeout)
            .build()
            .map_err(ErrorKind::from)?;

        let response = self
            .update_asset_status_command_invoker
            .invoke(command_request)
            .await
            .map_err(ErrorKind::from)?;

        Ok(response.payload.updated_asset.into())
    }

    /// Starts observation of any Asset updates from the Azure Device Registry service.
    ///
    /// # Arguments
    /// * `device_name` - The name of the Device.
    /// * `inbound_endpoint_name` - The name of the inbound endpoint.
    /// * `asset_name` - The name of the asset.
    /// * `timeout` - The duration until the Client stops waiting for a response to the request, it is rounded up to the nearest second.
    ///
    /// Returns a [`AssetUpdateObservation`].
    ///
    /// # Errors
    /// TODO
    pub async fn observe_asset_update_notifications(
        &self,
        device_name: String,
        inbound_endpoint_name: String,
        asset_name: String,
        timeout: Duration,
    ) -> Result<AssetUpdateObservation, Error> {
        // TODO Right now using aep_name + asset_name as the key for the dispatcher, consider using tuple
        let receiver_id = format!("{device_name}~{inbound_endpoint_name}~{asset_name}");

        let rx = self
            .asset_update_event_telemetry_dispatcher
            .register_receiver(receiver_id.clone())
            .map_err(|_| Error(ErrorKind::DuplicateObserve))?;

        let payload = adr_name_gen::SetNotificationPreferenceForAssetUpdatesRequestPayload {
            notification_preference_request:
                adr_name_gen::SetNotificationPreferenceForAssetUpdatesRequestSchema {
                    asset_name: asset_name.clone(),
                    notification_preference: adr_name_gen::NotificationPreference::On,
                },
        };

        let command_request =
            adr_name_gen::SetNotificationPreferenceForAssetUpdatesRequestBuilder::default()
                .payload(payload)
                .map_err(ErrorKind::from)?
                .topic_tokens(HashMap::from([
                    ("deviceName".to_string(), device_name),
                    ("inboundEndpointName".to_string(), inbound_endpoint_name),
                ]))
                .timeout(timeout)
                .build()
                .map_err(ErrorKind::from)?;

        let result = self
            .notify_on_asset_update_command_invoker
            .invoke(command_request)
            .await;

        match result {
            Ok(response) => {
                if let adr_name_gen::NotificationPreferenceResponse::Accepted =
                    response.payload.notification_preference_response
                {
                    Ok(AssetUpdateObservation {
                        name: asset_name.clone(),
                        receiver: rx,
                    })
                } else {
                    Err(Error(ErrorKind::ObservationError(asset_name)))
                }
            }
            Err(e) => {
                // If the observe request wasn't successful, remove it from our dispatcher
                if self
                    .asset_update_event_telemetry_dispatcher
                    .unregister_receiver(&receiver_id)
                {
                    log::debug!(
                        "Device , Endpoint and Asset combination removed from observed list: {receiver_id:?}"
                    );
                } else {
                    log::debug!(
                        "Device , Endpoint and Asset combination not in observed list: {receiver_id:?}"
                    );
                }
                Err(Error(ErrorKind::AIOProtocolError(e)))
            }
        }
    }

    /// Stops observation of any Asset updates from the Azure Device Registry service.
    ///
    /// # Arguments
    /// * `device_name` - The name of the Device.
    /// * `inbound_endpoint_name` - The name of the inbound endpoint.
    /// * `asset_name` - The name of the asset.
    /// * `timeout` - The duration until the Client stops waiting for a response to the request, it is rounded up to the nearest second.
    ///
    /// Returns `Ok(())` if the Device Updates are no longer being observed.
    ///
    /// # Errors
    /// TODO
    #[allow(clippy::unused_async)]
    pub async fn unobserve_asset_update_notifications(
        &self,
        _device_name: String,
        _inbound_endpoint_name: String,
        _asset_name: String,
        _timeout: Duration,
    ) -> Result<(), Error> {
        Err(Error(ErrorKind::PlaceholderError))
    }

    #[allow(clippy::unused_async)]
    async fn receive_update_event_telemetry_loop(
        asset_shutdown_notifier: Arc<Notify>, // Separate notifier for Asset
        mut asset_update_event_telemetry_receiver: adr_name_gen::AssetUpdateEventTelemetryReceiver<
            C,
        >,
        asset_update_event_telemetry_dispatcher: Arc<Dispatcher<(Asset, Option<AckToken>)>>,
    ) {
        let mut asset_shutdown_attempt_count = 0;

        loop {
            tokio::select! {
                // Asset shutdown handler
                () = asset_shutdown_notifier.notified() => {
                    match asset_update_event_telemetry_receiver.shutdown().await {
                        Ok(()) => {
                            log::info!("AssetUpdateEventTelemetryReceiver shutdown");
                        }
                        Err(e) => {
                            log::error!("Error shutting down AssetUpdateEventTelemetryReceiver: {e}");
                            // try shutdown again, but not indefinitely
                            if asset_shutdown_attempt_count < 3 {
                                asset_shutdown_attempt_count += 1;
                                asset_shutdown_notifier.notify_one();
                            }
                        }
                    }
                },
                asset_msg = asset_update_event_telemetry_receiver.recv() => {
                    if let Some(m) = asset_msg {
                        match m {
                            Ok((asset_update_event_telemetry, ack_token)) => {
                                let Some(device_name) = asset_update_event_telemetry.topic_tokens.get("ex:deviceName") else {
                                    log::error!("AssetUpdateEventTelemetry missing ex:aepName topic token.");
                                    continue;
                                };
                                let Some(inbound_endpoint_name) = asset_update_event_telemetry.topic_tokens.get("ex:inboundEndpointName") else {
                                    log::error!("AssetUpdateEventTelemetry missing ex:inboundEndpointName topic token.");
                                    continue;
                                };
                                // TODO Consider making the receiver id a tuple in the dispatcher
                                let dispatch_receiver_id = format!("{}~{}~{}", device_name, inbound_endpoint_name, asset_update_event_telemetry.payload.asset_update_event.asset_name);

                                match asset_update_event_telemetry_dispatcher.dispatch(&dispatch_receiver_id, (asset_update_event_telemetry.payload.asset_update_event.asset.into(), ack_token)) {
                                    Ok(()) => {
                                        log::debug!("AssetUpdateEventTelemetry dispatched for aep and asset: {dispatch_receiver_id:?}");
                                    }
                                    Err(DispatchError::SendError(payload)) => {
                                        log::warn!("AssetUpdateEventTelemetryReceiver has been dropped. Received Telemetry: {payload:?}",);
                                    }
                                    Err(DispatchError::NotFound(payload)) => {
                                        log::warn!("Asset is not being observed. Received AssetUpdateEventTelemetry: {payload:?}",);
                                    }
                                }
                            }
                            Err(e) => {
                                // This should only happen on errors subscribing, but it's likely not recoverable
                                log::error!("Error receiving AssetUpdateEventTelemetry: {e}. Shutting down AssetUpdateEventTelemetryReceiver.");
                                // try to shutdown telemetry receiver, but not indefinitely
                                if asset_shutdown_attempt_count < 3 {
                                    asset_shutdown_notifier.notify_one();
                                }
                            }
                        }
                    } else {
                        log::info!("AssetUpdateEventTelemetryReceiver closed, no more AssetUpdateEventTelemetry will be received");
                        // Unregister all receivers, closing the associated channels
                        asset_update_event_telemetry_dispatcher.unregister_all();
                        break;
                    }
                }
            }
        }
    }
}
