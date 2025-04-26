// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

//! Client for Azure Device Registry operations.
//!
//! To use this client, the `azure_device_registry` feature must be enabled.

use std::{collections::HashMap, sync::Arc, time::Duration};

use azure_iot_operations_mqtt::interface::AckToken;
use azure_iot_operations_mqtt::interface::ManagedClient;
use azure_iot_operations_protocol::application::ApplicationContext;
use derive_builder::Builder;
use tokio::sync::Notify;

use crate::azure_device_registry::device_name_gen::adr_base_service::client as adr_name_gen;
use crate::azure_device_registry::{
    Asset, AssetStatus, AssetUpdateObservation, Device, DeviceStatus, DeviceUpdateObservation,
    Error, ErrorKind,
    device_name_gen::{
        common_types::options::CommandInvokerOptionsBuilder,
        common_types::options::TelemetryReceiverOptionsBuilder,
    },
};
use crate::common::dispatcher::{DispatchError, Dispatcher};

const DEVICE_NAME_TOPIC_TOKEN: &str = "deviceName";
const DEVICE_NAME_RECEIVED_TOPIC_TOKEN: &str = "ex:deviceName";
const INBOUND_ENDPOINT_NAME_TOPIC_TOKEN: &str = "inboundEndpointName";
const INBOUND_ENDPOINT_NAME_RECEIVED_TOPIC_TOKEN: &str = "ex:inboundEndpointName";

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
    // general
    shutdown_notifier: Arc<Notify>,
    // device
    get_device_command_invoker: Arc<adr_name_gen::GetDeviceCommandInvoker<C>>,
    update_device_status_command_invoker: Arc<adr_name_gen::UpdateDeviceStatusCommandInvoker<C>>,
    notify_on_device_update_command_invoker:
        Arc<adr_name_gen::SetNotificationPreferenceForDeviceUpdatesCommandInvoker<C>>,
    device_update_notification_dispatcher: Arc<Dispatcher<(Device, Option<AckToken>)>>,
    // asset
    get_asset_command_invoker: Arc<adr_name_gen::GetAssetCommandInvoker<C>>,
    update_asset_status_command_invoker: Arc<adr_name_gen::UpdateAssetStatusCommandInvoker<C>>,
    notify_on_asset_update_command_invoker:
        Arc<adr_name_gen::SetNotificationPreferenceForAssetUpdatesCommandInvoker<C>>,
    asset_update_notification_dispatcher: Arc<Dispatcher<(Asset, Option<AckToken>)>>,
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
    #[allow(clippy::needless_pass_by_value)]
    pub fn new(
        application_context: ApplicationContext,
        client: C,
        options: ClientOptions,
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
            .map_err(ErrorKind::from)?;

        // Create the shutdown notifier for the receiver loop
        let shutdown_notifier = Arc::new(Notify::new());

        // Create dispatchers for devices/assets being observed to send their update notifications to
        let device_update_notification_dispatcher = Arc::new(Dispatcher::new());
        let asset_update_notification_dispatcher = Arc::new(Dispatcher::new());

        // Start the update device and assets notification loop
        tokio::task::spawn({
            // clones
            let shutdown_notifier_clone = shutdown_notifier.clone();
            let device_update_notification_dispatcher_clone =
                device_update_notification_dispatcher.clone();
            let asset_update_notification_dispatcher_clone =
                asset_update_notification_dispatcher.clone();

            // telemetry receivers
            let device_update_telemetry_receiver =
                adr_name_gen::DeviceUpdateEventTelemetryReceiver::new(
                    application_context.clone(),
                    client.clone(),
                    &telemetry_options,
                );
            let asset_update_telemetry_receiver =
                adr_name_gen::AssetUpdateEventTelemetryReceiver::new(
                    application_context.clone(),
                    client.clone(),
                    &telemetry_options,
                );

            async move {
                Self::receive_update_notification_loop(
                    shutdown_notifier_clone,
                    device_update_telemetry_receiver,
                    device_update_notification_dispatcher_clone,
                    asset_update_telemetry_receiver,
                    asset_update_notification_dispatcher_clone,
                )
                .await;
            }
        });

        Ok(Self {
            shutdown_notifier,
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
            notify_on_device_update_command_invoker: Arc::new(
                adr_name_gen::SetNotificationPreferenceForDeviceUpdatesCommandInvoker::new(
                    application_context.clone(),
                    client.clone(),
                    &command_options,
                ),
            ),
            device_update_notification_dispatcher,
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
                    client,
                    &command_options,
                ),
            ),
            asset_update_notification_dispatcher,
        })
    }

    /// Convenience function to get all observed device endpoint device & inbound endpoint names to quickly unobserve all of them before cleaning up
    #[must_use]
    #[allow(dead_code)]
    pub fn get_all_observed_device_endpoints(&self) -> Vec<(String, String)> {
        let mut device_endpoints = Vec::new();
        for device_receiver_id in self
            .device_update_notification_dispatcher
            .get_all_receiver_ids()
        {
            // best effort, if the id can't be parsed, then skip it
            if let Some((device_name, inbound_endpoint_name)) =
                Self::un_hash_device_endpoint(&device_receiver_id)
            {
                device_endpoints.push((device_name, inbound_endpoint_name));
            }
        }
        device_endpoints
    }

    /// Convenience function to get all observed asset names to quickly unobserve all of them before cleaning up
    #[must_use]
    #[allow(dead_code)]
    pub fn get_all_observed_assets(&self) -> Vec<(String, String, String)> {
        let mut assets = Vec::new();
        for receiver_id in self
            .asset_update_notification_dispatcher
            .get_all_receiver_ids()
        {
            // best effort, if the id can't be parsed, then skip it
            if let Some((device_name, inbound_endpoint_name, asset_name)) =
                Self::un_hash_device_endpoint_asset(&receiver_id)
            {
                assets.push((device_name, inbound_endpoint_name, asset_name));
            }
        }
        assets
    }

    /// Shutdown the [`Client`]. Shuts down the underlying command invokers.
    ///
    /// Note: If this method is called, the [`Client`] should not be used again.
    /// If the method returns an error, it may be called again to re-attempt unsubscribing.
    ///
    /// Returns Ok(()) on success, otherwise returns [`struct@Error`].
    /// # Errors
    /// [`struct@Error`] of kind [`ShutdownError`](ErrorKind::ShutdownError)
    /// if any of the invoker unsubscribes fail or if the unsuback reason code doesn't indicate success.
    /// This will be a vector of any shutdown errors, all invokers will attempt to be shutdown.
    pub async fn shutdown(&self) -> Result<(), Error> {
        // Notify the receiver loop to shutdown the telemetry receivers
        self.shutdown_notifier.notify_one();

        // Shut down invokers
        let mut errors = Vec::new();

        // device invokers
        let _ = self
            .get_device_command_invoker
            .shutdown()
            .await
            .map_err(|e| errors.push(e));
        let _ = self
            .update_device_status_command_invoker
            .shutdown()
            .await
            .map_err(|e| errors.push(e));
        let _ = self
            .notify_on_device_update_command_invoker
            .shutdown()
            .await
            .map_err(|e| errors.push(e));

        // asset invokers
        let _ = self
            .get_asset_command_invoker
            .shutdown()
            .await
            .map_err(|e| errors.push(e));
        let _ = self
            .update_asset_status_command_invoker
            .shutdown()
            .await
            .map_err(|e| errors.push(e));
        let _ = self
            .notify_on_asset_update_command_invoker
            .shutdown()
            .await
            .map_err(|e| errors.push(e));

        if errors.is_empty() {
            log::info!("Shutdown");
            Ok(())
        } else {
            Err(Error(ErrorKind::ShutdownError(errors)))
        }
    }

    fn get_topic_tokens(
        device_name: String,
        inbound_endpoint_name: String,
    ) -> HashMap<String, String> {
        HashMap::from([
            (DEVICE_NAME_TOPIC_TOKEN.to_string(), device_name),
            (
                INBOUND_ENDPOINT_NAME_TOPIC_TOKEN.to_string(),
                inbound_endpoint_name,
            ),
        ])
    }

    async fn receive_update_notification_loop(
        shutdown_notifier: Arc<Notify>,
        mut device_update_telemetry_receiver: adr_name_gen::DeviceUpdateEventTelemetryReceiver<C>,
        device_update_notification_dispatcher: Arc<Dispatcher<(Device, Option<AckToken>)>>,
        mut asset_update_telemetry_receiver: adr_name_gen::AssetUpdateEventTelemetryReceiver<C>,
        asset_update_notification_dispatcher: Arc<Dispatcher<(Asset, Option<AckToken>)>>,
    ) {
        let mut device_shutdown_attempt_count = 0;
        let mut asset_shutdown_attempt_count = 0;

        let device_shutdown_notifier = Arc::new(Notify::new());
        let asset_shutdown_notifier = Arc::new(Notify::new());

        let mut device_receiver_closed = false;
        let mut asset_receiver_closed = false;

        loop {
            tokio::select! {
                () = shutdown_notifier.notified() => {
                    if device_shutdown_attempt_count < 3 {
                        device_shutdown_attempt_count += 1;
                        device_shutdown_notifier.notify_one();
                    }
                    if asset_shutdown_attempt_count < 3 {
                        asset_shutdown_attempt_count += 1;
                        asset_shutdown_notifier.notify_one();
                    }
                },
                // AEP shutdown handler
                () = device_shutdown_notifier.notified() => {
                    match device_update_telemetry_receiver.shutdown().await {
                        Ok(()) => {
                            log::info!("DeviceUpdateEventTelemetryReceiver shutdown");
                        }
                        Err(e) => {
                            log::error!("Error shutting down DeviceUpdateEventTelemetryReceiver: {e}");
                            // try shutdown again, but not indefinitely
                            if device_shutdown_attempt_count < 3 {
                                device_shutdown_attempt_count += 1;
                                device_shutdown_notifier.notify_one();
                            }
                        }
                    }
                },
                // Asset shutdown handler
                () = asset_shutdown_notifier.notified() => {
                    match asset_update_telemetry_receiver.shutdown().await {
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
                device_update_message = device_update_telemetry_receiver.recv() => {
                    match device_update_message {
                        Some(Ok((device_update_telemetry, ack_token))) => {
                            let Some(device_name) = device_update_telemetry.topic_tokens.get(DEVICE_NAME_RECEIVED_TOPIC_TOKEN) else {
                                log::error!("Device Update Notification missing {DEVICE_NAME_RECEIVED_TOPIC_TOKEN} topic token.");
                                continue;
                            };
                            let Some(inbound_endpoint_name) = device_update_telemetry.topic_tokens.get(INBOUND_ENDPOINT_NAME_RECEIVED_TOPIC_TOKEN) else {
                                log::error!("Device Update Notification missing {INBOUND_ENDPOINT_NAME_RECEIVED_TOPIC_TOKEN} topic token.");
                                continue;
                            };

                            // Try to send the notification to the associated receiver
                            let receiver_id = Self::hash_device_endpoint(
                                device_name,
                                inbound_endpoint_name,
                            );
                            match device_update_notification_dispatcher.dispatch(&receiver_id, (device_update_telemetry.payload.into(), ack_token)) {
                                Ok(()) => {
                                    log::debug!("Device Update Notification dispatched for device {device_name:?} and inbound endpoint {inbound_endpoint_name:?}");
                                }
                                Err(DispatchError::SendError(payload)) => {
                                    log::warn!("Device Update Observation has been dropped. Received Device Update Notification: {payload:#?}");
                                }
                                Err(DispatchError::NotFound((receiver_id, (payload, _)))) => {
                                    log::warn!("Device Endpoint is not being observed. Received Device Update Notification: {payload:#?} for {receiver_id:?}");
                                }
                            }
                        },
                        Some(Err(e)) => {
                            // This should only happen on errors subscribing, but it's likely not recoverable
                            log::error!("Error receiving Device Update Notification Telemetry: {e}. Shutting down DeviceUpdateEventTelemetryReceiver.");
                            // try to shutdown telemetry receiver, but not indefinitely
                            if device_shutdown_attempt_count < 3 {
                                device_shutdown_notifier.notify_one();
                            }
                        },
                        None => {
                            device_receiver_closed = true;
                            log::info!("DeviceUpdateEventTelemetryReceiver closed, no more Device Update Notifications will be received");
                            // Unregister all receivers, closing the associated channels
                            device_update_notification_dispatcher.unregister_all();
                            if device_receiver_closed && asset_receiver_closed {
                                // only break if both telemetry receivers won't receive any more messages
                                break;
                            }
                        }
                    }
                },
                asset_update_message = asset_update_telemetry_receiver.recv() => {
                    match asset_update_message {
                        Some(Ok((asset_update_telemetry, ack_token))) => {
                            let Some(device_name) = asset_update_telemetry.topic_tokens.get(DEVICE_NAME_RECEIVED_TOPIC_TOKEN) else {
                                log::error!("Asset Update Notification missing {DEVICE_NAME_RECEIVED_TOPIC_TOKEN} topic token.");
                                continue;
                            };
                            let Some(inbound_endpoint_name) = asset_update_telemetry.topic_tokens.get(INBOUND_ENDPOINT_NAME_RECEIVED_TOPIC_TOKEN) else {
                                log::error!("AssetUpdateEventTelemetry missing {INBOUND_ENDPOINT_NAME_RECEIVED_TOPIC_TOKEN} topic token.");
                                continue;
                            };

                            // Try to send the notification to the associated receiver
                            let receiver_id = Self::hash_device_endpoint_asset(device_name, inbound_endpoint_name, &asset_update_telemetry.payload.asset_update_event.asset_name);
                            match asset_update_notification_dispatcher.dispatch(&receiver_id, (asset_update_telemetry.payload.asset_update_event.asset.into(), ack_token)) {
                                Ok(()) => {
                                    log::debug!("Asset Update Notification dispatched for device {device_name:?}, inbound endpoint {inbound_endpoint_name:?}, and asset {:?}", asset_update_telemetry.payload.asset_update_event.asset_name);
                                }
                                Err(DispatchError::SendError(payload)) => {
                                    log::warn!("Asset Update Observation has been dropped. Received Asset Update Notification: {payload:?}",);
                                }
                                Err(DispatchError::NotFound(payload)) => {
                                    log::warn!("Asset is not being observed. Received Asset Update Notification: {payload:?}",);
                                }
                            }
                        },
                        Some(Err(e))=> {
                            // This should only happen on errors subscribing, but it's likely not recoverable
                            log::error!("Error receiving Asset Update Notification Telemetry: {e}. Shutting down AssetUpdateEventTelemetryReceiver.");
                            // try to shutdown telemetry receiver, but not indefinitely
                            if asset_shutdown_attempt_count < 3 {
                                asset_shutdown_notifier.notify_one();
                            }
                        },
                        None => {
                            asset_receiver_closed = true;
                            log::info!("AssetUpdateEventTelemetryReceiver closed, no more Asset Update Notifications will be received");
                            // Unregister all receivers, closing the associated channels
                            asset_update_notification_dispatcher.unregister_all();
                            if device_receiver_closed && asset_receiver_closed {
                                // only break if both telemetry receivers won't receive any more messages
                                break;
                            }
                        }
                    }
                }
            }
        }
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
        let get_device_request = adr_name_gen::GetDeviceRequestBuilder::default()
            .topic_tokens(Self::get_topic_tokens(device_name, inbound_endpoint_name))
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
                .topic_tokens(Self::get_topic_tokens(device_name, inbound_endpoint_name))
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
    /// Note: On cleanup, unobserve should always be called so that the service knows to stop sending notifications.
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
    pub async fn observe_device_update_notifications(
        &self,
        device_name: String,
        inbound_endpoint_name: String,
        timeout: Duration,
    ) -> Result<DeviceUpdateObservation, Error> {
        let receiver_id = Self::hash_device_endpoint(&device_name, &inbound_endpoint_name);
        let rx = self
            .device_update_notification_dispatcher
            .register_receiver(receiver_id.clone())
            .map_err(ErrorKind::from)?;

        let observe_payload =
            adr_name_gen::SetNotificationPreferenceForDeviceUpdatesRequestPayload {
                notification_preference_request: adr_name_gen::NotificationPreference::On,
            };

        let observe_request =
            adr_name_gen::SetNotificationPreferenceForDeviceUpdatesRequestBuilder::default()
                .payload(observe_payload)
                .map_err(ErrorKind::from)?
                .topic_tokens(Self::get_topic_tokens(
                    device_name.clone(),
                    inbound_endpoint_name.clone(),
                ))
                .timeout(timeout)
                .build()
                .map_err(ErrorKind::from)?;

        match self
            .notify_on_device_update_command_invoker
            .invoke(observe_request)
            .await
        {
            Ok(response) => {
                match response.payload.notification_preference_response {
                    adr_name_gen::NotificationPreferenceResponse::Accepted => {
                        Ok(DeviceUpdateObservation { receiver: rx })
                    }
                    adr_name_gen::NotificationPreferenceResponse::Failed => {
                        // If the observe request wasn't successful, remove it from our dispatcher
                        if self
                            .device_update_notification_dispatcher
                            .unregister_receiver(&receiver_id)
                        {
                            log::debug!(
                                "Device `{device_name:?}` with inbound endpoint `{inbound_endpoint_name:?}` removed from observed list"
                            );
                        } else {
                            log::debug!(
                                "Device `{device_name:?}` with inbound endpoint `{inbound_endpoint_name:?}` not in observed list"
                            );
                        }
                        Err(Error(ErrorKind::ObservationError))
                    }
                }
            }
            Err(e) => {
                // If the observe request wasn't successful, remove it from our dispatcher
                if self
                    .device_update_notification_dispatcher
                    .unregister_receiver(&receiver_id)
                {
                    log::debug!(
                        "Device `{device_name:?}` with inbound endpoint `{inbound_endpoint_name:?}` removed from observed list"
                    );
                } else {
                    log::debug!(
                        "Device `{device_name:?}` with inbound endpoint `{inbound_endpoint_name:?}` not in observed list"
                    );
                }
                Err(Error(ErrorKind::from(e)))
            }
        }
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
    pub async fn unobserve_device_update_notifications(
        &self,
        device_name: String,
        inbound_endpoint_name: String,
        timeout: Duration,
    ) -> Result<(), Error> {
        let unobserve_payload =
            adr_name_gen::SetNotificationPreferenceForDeviceUpdatesRequestPayload {
                notification_preference_request: adr_name_gen::NotificationPreference::Off,
            };

        let unobserve_request =
            adr_name_gen::SetNotificationPreferenceForDeviceUpdatesRequestBuilder::default()
                .payload(unobserve_payload)
                .map_err(ErrorKind::from)?
                .topic_tokens(Self::get_topic_tokens(
                    device_name.clone(),
                    inbound_endpoint_name.clone(),
                ))
                .timeout(timeout)
                .build()
                .map_err(ErrorKind::from)?;
        let response = self
            .notify_on_device_update_command_invoker
            .invoke(unobserve_request)
            .await
            .map_err(ErrorKind::from)?;
        match response.payload.notification_preference_response {
            adr_name_gen::NotificationPreferenceResponse::Accepted => {
                let receiver_id = Self::hash_device_endpoint(&device_name, &inbound_endpoint_name);
                // Remove it from our dispatcher
                if self
                    .device_update_notification_dispatcher
                    .unregister_receiver(&receiver_id)
                {
                    log::debug!(
                        "Device `{device_name:?}` with inbound endpoint `{inbound_endpoint_name:?}` removed from observed list"
                    );
                } else {
                    log::debug!(
                        "Device `{device_name:?}` with inbound endpoint `{inbound_endpoint_name:?}` not in observed list"
                    );
                }
                Ok(())
            }
            adr_name_gen::NotificationPreferenceResponse::Failed => {
                Err(Error(ErrorKind::ObservationError))
            }
        }
    }

    fn hash_device_endpoint(device_name: &str, inbound_endpoint_name: &str) -> String {
        // `~`` can't be in a topic token, so this will never collide with another device + inbound endpoint name combo
        format!("{device_name}~{inbound_endpoint_name}")
    }

    fn un_hash_device_endpoint(hashed_device_endpoint: &str) -> Option<(String, String)> {
        hashed_device_endpoint
            .split_once('~')
            .map(|(device_name, inbound_endpoint_name)| {
                (device_name.to_string(), inbound_endpoint_name.to_string())
            })
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
            .topic_tokens(Self::get_topic_tokens(device_name, inbound_endpoint_name))
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
                asset_name,
                asset_status: status.into(),
            },
        };
        let command_request = adr_name_gen::UpdateAssetStatusRequestBuilder::default()
            .payload(payload)
            .map_err(ErrorKind::from)?
            .topic_tokens(Self::get_topic_tokens(device_name, inbound_endpoint_name))
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
    /// Note: On cleanup, unobserve should always be called so that the service knows to stop sending notifications.
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
        // TODO Right now using device name + asset_name as the key for the dispatcher, consider using tuple
        let receiver_id =
            Self::hash_device_endpoint_asset(&device_name, &inbound_endpoint_name, &asset_name);

        let rx = self
            .asset_update_notification_dispatcher
            .register_receiver(receiver_id.clone())
            .map_err(ErrorKind::from)?;

        let payload = adr_name_gen::SetNotificationPreferenceForAssetUpdatesRequestPayload {
            notification_preference_request:
                adr_name_gen::SetNotificationPreferenceForAssetUpdatesRequestSchema {
                    asset_name,
                    notification_preference: adr_name_gen::NotificationPreference::On,
                },
        };

        let command_request =
            adr_name_gen::SetNotificationPreferenceForAssetUpdatesRequestBuilder::default()
                .payload(payload)
                .map_err(ErrorKind::from)?
                .topic_tokens(Self::get_topic_tokens(device_name, inbound_endpoint_name))
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
                    Ok(AssetUpdateObservation { receiver: rx })
                } else {
                    // If the observe request wasn't successful, remove it from our dispatcher
                    if self
                        .asset_update_notification_dispatcher
                        .unregister_receiver(&receiver_id)
                    {
                        log::debug!(
                            "Device, Endpoint and Asset combination removed from observed list: {receiver_id:?}"
                        );
                    } else {
                        log::debug!(
                            "Device, Endpoint and Asset combination not in observed list: {receiver_id:?}"
                        );
                    }
                    Err(Error(ErrorKind::ObservationError))
                }
            }
            Err(e) => {
                // If the observe request wasn't successful, remove it from our dispatcher
                if self
                    .asset_update_notification_dispatcher
                    .unregister_receiver(&receiver_id)
                {
                    log::debug!(
                        "Device, Endpoint and Asset combination removed from observed list: {receiver_id:?}"
                    );
                } else {
                    log::debug!(
                        "Device, Endpoint and Asset combination not in observed list: {receiver_id:?}"
                    );
                }
                Err(Error(ErrorKind::from(e)))
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
    pub async fn unobserve_asset_update_notifications(
        &self,
        device_name: String,
        inbound_endpoint_name: String,
        asset_name: String,
        timeout: Duration,
    ) -> Result<(), Error> {
        let payload = adr_name_gen::SetNotificationPreferenceForAssetUpdatesRequestPayload {
            notification_preference_request:
                adr_name_gen::SetNotificationPreferenceForAssetUpdatesRequestSchema {
                    asset_name: asset_name.clone(),
                    notification_preference: adr_name_gen::NotificationPreference::Off,
                },
        };

        let command_request =
            adr_name_gen::SetNotificationPreferenceForAssetUpdatesRequestBuilder::default()
                .payload(payload)
                .map_err(ErrorKind::from)?
                .topic_tokens(Self::get_topic_tokens(
                    device_name.clone(),
                    inbound_endpoint_name.clone(),
                ))
                .timeout(timeout)
                .build()
                .map_err(ErrorKind::from)?;

        let response = self
            .notify_on_asset_update_command_invoker
            .invoke(command_request)
            .await
            .map_err(ErrorKind::from)?;

        match response.payload.notification_preference_response {
            adr_name_gen::NotificationPreferenceResponse::Accepted => {
                let receiver_id = Self::hash_device_endpoint_asset(
                    &device_name,
                    &inbound_endpoint_name,
                    &asset_name,
                );
                // Remove it from our dispatcher
                if self
                    .asset_update_notification_dispatcher
                    .unregister_receiver(&receiver_id)
                {
                    log::debug!(
                        "Device, Endpoint and Asset combination removed from observed list: {receiver_id:?}"
                    );
                } else {
                    log::debug!(
                        "Device, Endpoint and Asset combination not in observed list: {receiver_id:?}"
                    );
                }
                Ok(())
            }
            adr_name_gen::NotificationPreferenceResponse::Failed => {
                Err(Error(ErrorKind::ObservationError))
            }
        }
    }

    fn hash_device_endpoint_asset(
        device_name: &str,
        inbound_endpoint_name: &str,
        asset_name: &str,
    ) -> String {
        // `~`` can't be in a topic token, so this will never collide with another device + inbound endpoint + asset name combo
        format!("{device_name}~{inbound_endpoint_name}~{asset_name}")
    }

    fn un_hash_device_endpoint_asset(
        hashed_device_endpoint_asset: &str,
    ) -> Option<(String, String, String)> {
        let pieces: Vec<&str> = hashed_device_endpoint_asset.split('~').collect();
        if pieces.len() >= 3 {
            Some((
                pieces[0].to_string(),
                pieces[1].to_string(),
                pieces[2].to_string(),
            ))
        } else {
            None
        }
    }
}
