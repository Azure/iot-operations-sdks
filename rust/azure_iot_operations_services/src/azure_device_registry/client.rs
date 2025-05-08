// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

//! Client for Azure Device Registry operations.
//!
//! To use this client, the `azure_device_registry` feature must be enabled.

use std::{collections::HashMap, sync::Arc, time::Duration};

use azure_iot_operations_mqtt::interface::{AckToken, ManagedClient};
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
    /// Create a new Azure Device Registry Client.
    ///
    /// # Errors
    /// [`struct@Error`] of kind [`ValidationError`](ErrorKind::ValidationError)
    /// if the Client Id of the [`ManagedClient`] isn't valid as a topic token.
    /// # Panics
    /// Panics if the options for the underlying command invokers or receivers cannot be built. Not possible since
    /// the options are statically generated.
    #[allow(clippy::needless_pass_by_value)]
    pub fn new(
        application_context: ApplicationContext,
        client: C,
        options: ClientOptions,
    ) -> Result<Self, Error> {
        // THIS IS A TEMPORARY FIX. WORKAROUND FOR THE FACT THAT CODEGEN PANICS ON INVALID CLIENT ID
        // INSTEAD OF RETURNING AN ERROR
        if !Self::is_valid_replacement(client.client_id()) {
            return Err(Error(ErrorKind::ValidationError(format!(
                "Client id {} is invalid",
                client.client_id()
            ))));
        }

        let command_options = CommandInvokerOptionsBuilder::default()
            .topic_token_map(HashMap::from([(
                "connectorClientId".to_string(),
                client.client_id().to_string(),
            )]))
            .build()
            .expect("Builder cannot fail as there is no validation function");

        let telemetry_options = TelemetryReceiverOptionsBuilder::default()
            .topic_token_map(HashMap::from([(
                "connectorClientId".to_string(),
                client.client_id().to_string(),
            )]))
            .auto_ack(options.notification_auto_ack)
            .build()
            .expect("Builder cannot fail as there is no validation function");

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

    /// Convenience function to get all observed device & inbound endpoint names to quickly unobserve all of them before cleaning up
    #[must_use]
    pub fn get_all_observed_device_endpoints(&self) -> Vec<(String, String)> {
        let mut device_endpoints = Vec::new();
        for device_receiver_id in self
            .device_update_notification_dispatcher
            .get_all_receiver_ids()
        {
            // best effort, if the id can't be parsed, then skip it
            if let Some((device_name, inbound_endpoint_name)) =
                Self::unhash_device_endpoint(&device_receiver_id)
            {
                device_endpoints.push((device_name, inbound_endpoint_name));
            }
        }
        device_endpoints
    }

    /// Convenience function to get all observed asset names to quickly unobserve all of them before cleaning up
    #[must_use]
    pub fn get_all_observed_assets(&self) -> Vec<(String, String, String)> {
        let mut assets = Vec::new();
        for receiver_id in self
            .asset_update_notification_dispatcher
            .get_all_receiver_ids()
        {
            // best effort, if the id can't be parsed, then skip it
            if let Some((device_name, inbound_endpoint_name, asset_name)) =
                Self::unhash_device_endpoint_asset(&receiver_id)
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

        let (result1, result2, result3, result4, result5, result6) = tokio::join!(
            self.get_device_command_invoker.shutdown(),
            self.update_device_status_command_invoker.shutdown(),
            self.notify_on_device_update_command_invoker.shutdown(),
            self.get_asset_command_invoker.shutdown(),
            self.update_asset_status_command_invoker.shutdown(),
            self.notify_on_asset_update_command_invoker.shutdown()
        );

        for result in [result1, result2, result3, result4, result5, result6] {
            if let Err(e) = result {
                errors.push(e);
            }
        }

        if errors.is_empty() {
            log::info!("Shutdown done gracefully");
            Ok(())
        } else {
            Err(Error(ErrorKind::ShutdownError(errors)))
        }
    }

    /// Helper function to get the topic tokens for a device and inbound endpoint.
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

    /// Determine whether a string is valid for use as a replacement string in a custom replacement map
    /// or a topic namespace based on [topic-structure.md](https://github.com/Azure/iot-operations-sdks/blob/main/doc/reference/topic-structure.md)
    ///
    /// Returns true if the string is not empty, does not contain invalid characters, does not start or
    /// end with '/', and does not contain "//"
    ///
    /// # Arguments
    /// * `s` - A string slice to check for validity
    #[must_use]
    fn is_valid_replacement(s: &str) -> bool {
        !(s.is_empty()
            || Self::contains_invalid_char(s)
            || s.starts_with('/')
            || s.ends_with('/')
            || s.contains("//"))
    }

    /// Check if a string contains invalid characters specified in [topic-structure.md](https://github.com/Azure/iot-operations-sdks/blob/main/doc/reference/topic-structure.md)
    ///
    /// Returns true if the string contains any of the following:
    /// - Non-ASCII characters
    /// - Characters outside the range of '!' to '~'
    /// - Characters '+', '#', '{', '}'
    ///
    /// # Arguments
    /// * `s` - A string slice to check for invalid characters
    #[must_use]
    fn contains_invalid_char(s: &str) -> bool {
        s.chars().any(|c| {
            !c.is_ascii()
                || !('!'..='~').contains(&c)
                || c == '+'
                || c == '#'
                || c == '{'
                || c == '}'
        })
    }

    /// It receives update notifications from the Azure Device Registry service.
    async fn receive_update_notification_loop(
        shutdown_notifier: Arc<Notify>,
        mut device_update_telemetry_receiver: adr_name_gen::DeviceUpdateEventTelemetryReceiver<C>,
        device_update_notification_dispatcher: Arc<Dispatcher<(Device, Option<AckToken>)>>,
        mut asset_update_telemetry_receiver: adr_name_gen::AssetUpdateEventTelemetryReceiver<C>,
        asset_update_notification_dispatcher: Arc<Dispatcher<(Asset, Option<AckToken>)>>,
    ) {
        let max_attempt = 3;
        let mut device_shutdown_attempt_count = 0;
        let mut asset_shutdown_attempt_count = 0;

        let device_shutdown_notifier = Arc::new(Notify::new());
        let asset_shutdown_notifier = Arc::new(Notify::new());

        let mut device_receiver_closed = false;
        let mut asset_receiver_closed = false;

        loop {
            tokio::select! {
                () = shutdown_notifier.notified() => {
                    if device_shutdown_attempt_count < max_attempt {
                        device_shutdown_notifier.notify_one();
                    }
                    if asset_shutdown_attempt_count < max_attempt {
                        asset_shutdown_notifier.notify_one();
                    }
                },
                // Device shutdown handler
                () = device_shutdown_notifier.notified() => {
                    match device_update_telemetry_receiver.shutdown().await {
                        Ok(()) => {
                            log::info!("DeviceUpdateEventTelemetryReceiver shutdown");
                        }
                        Err(e) => {
                            log::error!("Error shutting down DeviceUpdateEventTelemetryReceiver: {e}");
                            // try shutdown again, but not indefinitely
                            if device_shutdown_attempt_count < max_attempt {
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
                            if asset_shutdown_attempt_count < max_attempt {
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
                            if device_shutdown_attempt_count < max_attempt {
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
                                log::error!("Asset Update Notification missing {INBOUND_ENDPOINT_NAME_RECEIVED_TOPIC_TOKEN} topic token.");
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
                                Err(DispatchError::NotFound((receiver_id, (payload, _)))) => {
                                    log::warn!("Asset is not being observed. Received Asset Update Notification: {payload:#?} for {receiver_id:?}",);
                                }
                            }
                        },
                        Some(Err(e))=> {
                            // This should only happen on errors subscribing, but it's likely not recoverable
                            log::error!("Error receiving Asset Update Notification Telemetry: {e}. Shutting down AssetUpdateEventTelemetryReceiver.");
                            // try to shutdown telemetry receiver, but not indefinitely
                            if asset_shutdown_attempt_count < max_attempt {
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

    /// Retrieves a [`Device`] from the Azure Device Registry service.
    ///
    /// # Arguments
    /// * `device_name` - The name of the device.
    /// * `inbound_endpoint_name` - The name of the inbound endpoint.
    /// * `timeout` - The duration until the client stops waiting for a response to the request, it is rounded up to the nearest second.
    ///
    /// Returns a [`Device`] if the device was found.
    ///
    /// # Errors
    /// [`struct@Error`] of kind [`InvalidRequestArgument`](ErrorKind::InvalidRequestArgument)
    /// if timeout is 0 or > `u32::max`.
    ///
    /// [`struct@Error`] of kind [`AIOProtocolError`](ErrorKind::AIOProtocolError) if:
    /// - device or inbound endpoint names are invalid.
    /// - there are any underlying errors from the AIO RPC protocol.
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

    /// Updates a [`Device`]'s status in the Azure Device Registry service.
    ///
    /// # Arguments
    /// * `device_name` - The name of the device.
    /// * `inbound_endpoint_name` - The name of the inbound endpoint.
    /// * `status` - A [`DeviceStatus`] containing all status information for the device.
    /// * `timeout` - The duration until the client stops waiting for a response to the request, it is rounded up to the nearest second.
    ///
    /// Returns the updated [`Device`] once updated.
    ///
    /// # Errors
    /// [`struct@Error`] of kind [`InvalidRequestArgument`](ErrorKind::InvalidRequestArgument)
    /// if timeout is 0 or > `u32::max`.
    ///
    /// [`struct@Error`] of kind [`AIOProtocolError`](ErrorKind::AIOProtocolError) if:
    /// - device or inbound endpoint names are invalid.
    /// - there are any underlying errors from the AIO RPC protocol.
    pub async fn update_device_plus_endpoint_status(
        &self,
        device_name: String,
        inbound_endpoint_name: String,
        status: DeviceStatus,
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

    /// Starts observation of a [`Device`]'s updates from the Azure Device Registry service.
    ///
    /// Note: On cleanup, unobserve should always be called so that the service knows to stop sending notifications.
    ///
    /// # Arguments
    /// * `device_name` - The name of the device.
    /// * `inbound_endpoint_name` - The name of the inbound endpoint.
    /// * `timeout` - The duration until the client stops waiting for a response to the request, it is rounded up to the nearest second.
    ///
    /// Returns the [`DeviceUpdateObservation`] if the observation was started successfully or [`struct@Error`].
    ///
    /// # Errors
    /// [`struct@Error`] of kind [`DuplicateObserve`](ErrorKind::DuplicateObserve)
    /// if the [`Device`] is already being observed.
    ///
    /// [`struct@Error`] of kind [`InvalidRequestArgument`](ErrorKind::InvalidRequestArgument)
    /// if timeout is 0 or > `u32::max`.
    ///
    /// [`struct@Error`] of kind [`AIOProtocolError`](ErrorKind::AIOProtocolError) if:
    /// - device or inbound endpoint names are invalid.
    /// - there are any underlying errors from the AIO RPC protocol.
    ///
    /// [`struct@Error`] of kind [`ObservationError`](ErrorKind::ObservationError)
    /// if the observation was not accepted by the service.
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
                        Ok(DeviceUpdateObservation(rx))
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

    /// Stops observation of a [`Device`]'s updates from the Azure Device Registry service.
    ///
    /// # Arguments
    /// * `device_name` - The name of the device.
    /// * `inbound_endpoint_name` - The name of the inbound endpoint.
    /// * `timeout` - The duration until the client stops waiting for a response to the request, it is rounded up to the nearest second.
    ///
    /// Returns `Ok(())` if the device updates are no longer being observed.
    ///
    /// # Errors
    /// [`struct@Error`] of kind [`InvalidRequestArgument`](ErrorKind::InvalidRequestArgument)
    /// if timeout is 0 or > `u32::max`.
    ///
    /// [`struct@Error`] of kind [`AIOProtocolError`](ErrorKind::AIOProtocolError) if:
    /// - device or inbound endpoint names are invalid.
    /// - there are any underlying errors from the AIO RPC protocol.
    ///
    /// [`struct@Error`] of kind [`ObservationError`](ErrorKind::ObservationError)
    /// if the unobservation was not accepted by the service.
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

    /// Hashes the device name and inbound endpoint name to create a single string.
    fn hash_device_endpoint(device_name: &str, inbound_endpoint_name: &str) -> String {
        // `~`` can't be in a topic token, so this will never collide with another device + inbound endpoint name combo
        format!("{device_name}~{inbound_endpoint_name}")
    }

    /// Unhashes the device name and inbound endpoint name from a single string.
    fn unhash_device_endpoint(hashed_device_endpoint: &str) -> Option<(String, String)> {
        hashed_device_endpoint
            .split_once('~')
            .map(|(device_name, inbound_endpoint_name)| {
                (device_name.to_string(), inbound_endpoint_name.to_string())
            })
    }

    // ~~~~~~~~~~~~~~~~~ Asset APIs ~~~~~~~~~~~~~~~~~~~~~

    /// Retrieves an [`Asset`] from the Azure Device Registry service.
    ///
    /// # Arguments
    /// * `device_name` - The name of the device.
    /// * `inbound_endpoint_name` - The name of the inbound endpoint.
    /// * `asset_name` - The name of the asset.
    /// * `timeout` - The duration until the client stops waiting for a response to the request, it is rounded up to the nearest second.
    ///
    /// Returns an [`Asset`] if the the asset was found.
    ///
    /// # Errors
    /// [`struct@Error`] of kind [`InvalidRequestArgument`](ErrorKind::InvalidRequestArgument)
    /// if timeout is 0 or > `u32::max`.
    ///
    /// [`struct@Error`] of kind [`AIOProtocolError`](ErrorKind::AIOProtocolError) if:
    /// - device or inbound endpoint names are invalid.
    /// - there are any underlying errors from the AIO RPC protocol.
    ///
    /// [`struct@Error`] of kind [`ValidationError`](ErrorKind::ValidationError)
    /// if the asset name is empty.
    pub async fn get_asset(
        &self,
        device_name: String,
        inbound_endpoint_name: String,
        asset_name: String,
        timeout: Duration,
    ) -> Result<Asset, Error> {
        if asset_name.trim().is_empty() {
            return Err(Error(ErrorKind::ValidationError(
                "asset_name must not be empty".to_string(),
            )));
        }
        let payload = adr_name_gen::GetAssetRequestPayload { asset_name };
        let command_request = adr_name_gen::GetAssetRequestBuilder::default()
            .payload(payload)
            .map_err(ErrorKind::from)?
            .timeout(timeout)
            .topic_tokens(Self::get_topic_tokens(device_name, inbound_endpoint_name))
            .build()
            .map_err(ErrorKind::from)?;

        let response = self
            .get_asset_command_invoker
            .invoke(command_request)
            .await
            .map_err(ErrorKind::from)?;

        Ok(response.payload.asset.into())
    }

    /// Updates the status of an [`Asset`] in the Azure Device Registry service.
    ///
    /// # Arguments
    /// * `device_name` - The name of the device.
    /// * `inbound_endpoint_name` - The name of the inbound endpoint.
    /// * `asset_name` - The name of the asset.
    /// * `status` - An [`AssetStatus`] containing the status of an asset for the update.
    /// * `timeout` - The duration until the client stops waiting for a response to the request, it is rounded up to the nearest second.
    ///
    /// Returns the updated [`Asset`] once updated.
    ///
    /// # Errors
    /// [`struct@Error`] of kind [`InvalidRequestArgument`](ErrorKind::InvalidRequestArgument)
    /// if timeout is 0 or > `u32::max`.
    ///
    /// [`struct@Error`] of kind [`AIOProtocolError`](ErrorKind::AIOProtocolError) if:
    /// - device or inbound endpoint names are invalid.
    /// - there are any underlying errors from the AIO RPC protocol.
    ///
    /// [`struct@Error`] of kind [`ValidationError`](ErrorKind::ValidationError)
    /// if the asset name is empty.
    pub async fn update_asset_status(
        &self,
        device_name: String,
        inbound_endpoint_name: String,
        asset_name: String,
        status: AssetStatus,
        timeout: Duration,
    ) -> Result<Asset, Error> {
        if asset_name.trim().is_empty() {
            return Err(Error(ErrorKind::ValidationError(
                "asset_name must not be empty".to_string(),
            )));
        }

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

    /// Starts observation of an [`Asset`]'s updates from the Azure Device Registry service.
    ///
    /// Note: On cleanup, unobserve should always be called so that the service knows to stop sending notifications.
    ///
    /// # Arguments
    /// * `device_name` - The name of the device.
    /// * `inbound_endpoint_name` - The name of the inbound endpoint.
    /// * `asset_name` - The name of the asset.
    /// * `timeout` - The duration until the client stops waiting for a response to the request, it is rounded up to the nearest second.
    ///
    /// Returns an [`AssetUpdateObservation`] if the observation was started successfully or [`struct@Error`].
    ///
    /// # Errors
    /// [`struct@Error`] of kind [`DuplicateObserve`](ErrorKind::DuplicateObserve)
    /// if the [`Asset`] is already being observed.
    ///
    /// [`struct@Error`] of kind [`InvalidRequestArgument`](ErrorKind::InvalidRequestArgument)
    /// if timeout is 0 or > `u32::max`.
    ///
    /// [`struct@Error`] of kind [`AIOProtocolError`](ErrorKind::AIOProtocolError) if:
    /// - device or inbound endpoint names are invalid.
    /// - there are any underlying errors from the AIO RPC protocol.
    ///
    /// [`struct@Error`] of kind [`ObservationError`](ErrorKind::ObservationError)
    /// if the observation was not accepted by the service.
    ///
    /// [`struct@Error`] of kind [`ValidationError`](ErrorKind::ValidationError)
    /// if the asset name is empty.
    pub async fn observe_asset_update_notifications(
        &self,
        device_name: String,
        inbound_endpoint_name: String,
        asset_name: String,
        timeout: Duration,
    ) -> Result<AssetUpdateObservation, Error> {
        if asset_name.trim().is_empty() {
            return Err(Error(ErrorKind::ValidationError(
                "asset_name must not be empty".to_string(),
            )));
        }

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
                    Ok(AssetUpdateObservation(rx))
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

    /// Stops observation of an [`Asset`]'s updates from the Azure Device Registry service.
    ///
    /// # Arguments
    /// * `device_name` - The name of the device.
    /// * `inbound_endpoint_name` - The name of the inbound endpoint.
    /// * `asset_name` - The name of the asset.
    /// * `timeout` - The duration until the client stops waiting for a response to the request, it is rounded up to the nearest second.
    ///
    /// Returns `Ok(())` if the asset updates are no longer being observed.
    ///
    /// # Errors
    /// [`struct@Error`] of kind [`InvalidRequestArgument`](ErrorKind::InvalidRequestArgument)
    /// if timeout is 0 or > `u32::max`.
    ///
    /// [`struct@Error`] of kind [`AIOProtocolError`](ErrorKind::AIOProtocolError) if:
    /// - device or inbound endpoint names are invalid.
    /// - there are any underlying errors from the AIO RPC protocol.
    ///
    /// [`struct@Error`] of kind [`ObservationError`](ErrorKind::ObservationError)
    /// if the unobservation was not accepted by the service.
    ///
    /// [`struct@Error`] of kind [`ValidationError`](ErrorKind::ValidationError)
    /// if the asset name is empty.
    pub async fn unobserve_asset_update_notifications(
        &self,
        device_name: String,
        inbound_endpoint_name: String,
        asset_name: String,
        timeout: Duration,
    ) -> Result<(), Error> {
        if asset_name.trim().is_empty() {
            return Err(Error(ErrorKind::ValidationError(
                "asset_name must not be empty".to_string(),
            )));
        }

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

    /// Hashes the device name and inbound endpoint name into a single string.
    fn hash_device_endpoint_asset(
        device_name: &str,
        inbound_endpoint_name: &str,
        asset_name: &str,
    ) -> String {
        // `~`` can't be in a topic token, so this will never collide with another device + inbound endpoint + asset name combo
        format!("{device_name}~{inbound_endpoint_name}~{asset_name}")
    }

    /// Unhashes the device name, inbound endpoint name, and asset name from a single string.
    fn unhash_device_endpoint_asset(
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

#[cfg(test)]
mod tests {
    use super::*;
    use azure_iot_operations_mqtt::MqttConnectionSettingsBuilder;
    use azure_iot_operations_mqtt::session::SessionManagedClient;
    use azure_iot_operations_mqtt::session::{Session, SessionOptionsBuilder};
    use azure_iot_operations_protocol::application::ApplicationContextBuilder;
    use azure_iot_operations_protocol::common::aio_protocol_error::AIOProtocolErrorKind;
    use test_case::test_case;

    const DEVICE_NAME: &str = "test-device";
    const INBOUND_ENDPOINT_NAME: &str = "test-endpoint";
    const ASSET_NAME: &str = "test-asset";
    const DURATION: Duration = Duration::from_secs(10);

    fn create_session() -> Session {
        let connection_settings = MqttConnectionSettingsBuilder::default()
            .hostname("localhost")
            .client_id("test_client")
            .build()
            .unwrap();
        let session_options = SessionOptionsBuilder::default()
            .connection_settings(connection_settings)
            .build()
            .unwrap();
        Session::new(session_options).unwrap()
    }

    fn create_adr_client() -> Client<SessionManagedClient> {
        let session = create_session();
        let managed_client = session.create_managed_client();

        super::Client::new(
            ApplicationContextBuilder::default().build().unwrap(),
            managed_client,
            super::ClientOptionsBuilder::default().build().unwrap(),
        )
        .unwrap()
    }

    #[test]
    fn test_new_client() {
        let connection_settings = MqttConnectionSettingsBuilder::default()
            .hostname("localhost")
            .client_id("+++")
            .build()
            .unwrap();
        let session_options = SessionOptionsBuilder::default()
            .connection_settings(connection_settings)
            .build()
            .unwrap();
        let session = Session::new(session_options).unwrap();
        let managed_client = session.create_managed_client();
        let result = Client::new(
            ApplicationContextBuilder::default().build().unwrap(),
            managed_client,
            super::ClientOptionsBuilder::default().build().unwrap(),
        );

        assert!(result.is_err());
        assert!(matches!(
            result,
            Err(e) if matches!(&e.0, ErrorKind::ValidationError(_))
        ));
    }

    #[tokio::test]
    async fn test_get_asset_empty_asset_name() {
        let adr_client = create_adr_client();
        let result = adr_client
            .get_asset(
                DEVICE_NAME.to_string(),
                INBOUND_ENDPOINT_NAME.to_string(),
                String::new(),
                DURATION,
            )
            .await;

        assert!(matches!(
            result.unwrap_err(),
            Error(ErrorKind::ValidationError(_))
        ));
    }

    #[test_case("", INBOUND_ENDPOINT_NAME)]
    #[test_case(DEVICE_NAME, "")]
    #[tokio::test]
    async fn test_get_asset_invalid_topic_tokens(device_name: &str, endpoint_name: &str) {
        let adr_client: Client<SessionManagedClient> = create_adr_client();
        let result = adr_client
            .get_asset(
                device_name.to_string(),
                endpoint_name.to_string(),
                ASSET_NAME.to_string(),
                DURATION,
            )
            .await;

        assert!(matches!(
            result.unwrap_err().0,
            ErrorKind::AIOProtocolError(ref e) if matches!(e.kind, AIOProtocolErrorKind::ConfigurationInvalid)
        ));
    }

    #[tokio::test]
    async fn test_get_asset_zero_timeout() {
        let adr_client = create_adr_client();
        let result = adr_client
            .get_asset(
                DEVICE_NAME.to_string(),
                INBOUND_ENDPOINT_NAME.to_string(),
                ASSET_NAME.to_string(),
                Duration::from_secs(0),
            )
            .await;
        assert!(matches!(
            result.unwrap_err(),
            Error(ErrorKind::InvalidRequestArgument(_))
        ));
    }

    #[tokio::test]
    async fn test_update_asset_status_empty_asset_name() {
        let adr_client = create_adr_client();
        let result = adr_client
            .update_asset_status(
                DEVICE_NAME.to_string(),
                INBOUND_ENDPOINT_NAME.to_string(),
                String::new(),
                AssetStatus::default(),
                DURATION,
            )
            .await;

        assert!(matches!(
            result.unwrap_err(),
            Error(ErrorKind::ValidationError(_))
        ));
    }

    #[test_case("", INBOUND_ENDPOINT_NAME)]
    #[test_case(DEVICE_NAME, "")]
    #[tokio::test]
    async fn test_update_asset_status_invalid_topic_tokens(device_name: &str, endpoint_name: &str) {
        let adr_client = create_adr_client();
        let result = adr_client
            .update_asset_status(
                device_name.to_string(),
                endpoint_name.to_string(),
                ASSET_NAME.to_string(),
                AssetStatus::default(),
                DURATION,
            )
            .await;

        assert!(matches!(
            result.unwrap_err().0,
            ErrorKind::AIOProtocolError(ref e) if matches!(e.kind, AIOProtocolErrorKind::ConfigurationInvalid)
        ));
    }

    #[tokio::test]
    async fn test_update_asset_status_zero_timeout() {
        let adr_client = create_adr_client();
        let result = adr_client
            .update_asset_status(
                DEVICE_NAME.to_string(),
                INBOUND_ENDPOINT_NAME.to_string(),
                ASSET_NAME.to_string(),
                AssetStatus::default(),
                Duration::from_secs(0),
            )
            .await;
        assert!(matches!(
            result.unwrap_err().kind(),
            ErrorKind::InvalidRequestArgument(_)
        ));
    }

    #[tokio::test]
    async fn test_observe_asset_update_empty_asset_name() {
        let adr_client = create_adr_client();
        let result = adr_client
            .observe_asset_update_notifications(
                DEVICE_NAME.to_string(),
                INBOUND_ENDPOINT_NAME.to_string(),
                String::new(),
                DURATION,
            )
            .await;

        assert!(matches!(
            result.unwrap_err(),
            Error(ErrorKind::ValidationError(_))
        ));
    }

    #[test_case("", INBOUND_ENDPOINT_NAME)]
    #[test_case(DEVICE_NAME, "")]
    #[tokio::test]
    async fn test_observe_asset_update_invalid_topic_tokens(
        device_name: &str,
        endpoint_name: &str,
    ) {
        let adr_client = create_adr_client();
        let result = adr_client
            .observe_asset_update_notifications(
                device_name.to_string(),
                endpoint_name.to_string(),
                ASSET_NAME.to_string(),
                DURATION,
            )
            .await;

        assert!(matches!(
            result.unwrap_err().0,
            ErrorKind::AIOProtocolError(ref e) if matches!(e.kind, AIOProtocolErrorKind::ConfigurationInvalid)
        ));
    }

    #[tokio::test]
    async fn test_observe_asset_update_zero_timeout() {
        let adr_client = create_adr_client();
        let result = adr_client
            .observe_asset_update_notifications(
                DEVICE_NAME.to_string(),
                INBOUND_ENDPOINT_NAME.to_string(),
                ASSET_NAME.to_string(),
                Duration::from_secs(0),
            )
            .await;
        assert!(matches!(
            result.unwrap_err().kind(),
            ErrorKind::InvalidRequestArgument(_)
        ));
    }

    #[tokio::test]
    async fn test_unobserve_asset_update_empty_asset_name() {
        let adr_client = create_adr_client();
        let result = adr_client
            .observe_asset_update_notifications(
                DEVICE_NAME.to_string(),
                INBOUND_ENDPOINT_NAME.to_string(),
                String::new(),
                DURATION,
            )
            .await;

        assert!(matches!(
            result.unwrap_err(),
            Error(ErrorKind::ValidationError(_))
        ));
    }

    #[test_case("", INBOUND_ENDPOINT_NAME)]
    #[test_case(DEVICE_NAME, "")]
    #[tokio::test]
    async fn test_unobserve_asset_update_invalid_topic_tokens(
        device_name: &str,
        endpoint_name: &str,
    ) {
        let adr_client = create_adr_client();
        let result = adr_client
            .unobserve_asset_update_notifications(
                device_name.to_string(),
                endpoint_name.to_string(),
                ASSET_NAME.to_string(),
                DURATION,
            )
            .await;

        assert!(matches!(
            result.unwrap_err().0,
            ErrorKind::AIOProtocolError(ref e) if matches!(e.kind, AIOProtocolErrorKind::ConfigurationInvalid)
        ));
    }

    #[tokio::test]
    async fn test_unobserve_asset_update_zero_timeout() {
        let adr_client = create_adr_client();
        let result = adr_client
            .unobserve_asset_update_notifications(
                DEVICE_NAME.to_string(),
                INBOUND_ENDPOINT_NAME.to_string(),
                ASSET_NAME.to_string(),
                Duration::from_secs(0),
            )
            .await;
        assert!(matches!(
            result.unwrap_err().kind(),
            ErrorKind::InvalidRequestArgument(_)
        ));
    }

    #[test_case("", INBOUND_ENDPOINT_NAME)]
    #[test_case(DEVICE_NAME, "")]
    #[tokio::test]
    async fn test_get_device_invalid_topic_tokens(device_name: &str, endpoint_name: &str) {
        let adr_client = create_adr_client();
        let result = adr_client
            .get_device(device_name.to_string(), endpoint_name.to_string(), DURATION)
            .await;

        assert!(matches!(
            result.unwrap_err().0,
            ErrorKind::AIOProtocolError(ref e) if matches!(e.kind, AIOProtocolErrorKind::ConfigurationInvalid)
        ));
    }

    #[tokio::test]
    async fn test_get_device_zero_timeout() {
        let adr_client = create_adr_client();
        let result = adr_client
            .get_device(
                DEVICE_NAME.to_string(),
                INBOUND_ENDPOINT_NAME.to_string(),
                Duration::from_secs(0),
            )
            .await;

        assert!(matches!(
            result.unwrap_err().kind(),
            ErrorKind::InvalidRequestArgument(_)
        ));
    }

    #[test_case("", INBOUND_ENDPOINT_NAME)]
    #[test_case(DEVICE_NAME, "")]
    #[tokio::test]
    async fn test_update_device_plus_endpoint_invalid_topic_tokens(
        device_name: &str,
        endpoint_name: &str,
    ) {
        let adr_client = create_adr_client();
        let result = adr_client
            .update_device_plus_endpoint_status(
                device_name.to_string(),
                endpoint_name.to_string(),
                DeviceStatus::default(),
                DURATION,
            )
            .await;

        assert!(matches!(
            result.unwrap_err().0,
            ErrorKind::AIOProtocolError(ref e) if matches!(e.kind, AIOProtocolErrorKind::ConfigurationInvalid)
        ));
    }

    #[tokio::test]
    async fn test_update_device_plus_endpoint_zero_timeout() {
        let adr_client = create_adr_client();
        let result = adr_client
            .update_device_plus_endpoint_status(
                DEVICE_NAME.to_string(),
                INBOUND_ENDPOINT_NAME.to_string(),
                DeviceStatus::default(),
                Duration::from_secs(0),
            )
            .await;

        assert!(matches!(
            result.unwrap_err().kind(),
            ErrorKind::InvalidRequestArgument(_)
        ));
    }

    #[test_case("", INBOUND_ENDPOINT_NAME)]
    #[test_case(DEVICE_NAME, "")]
    #[tokio::test]
    async fn test_observe_device_invalid_topic_tokens(device_name: &str, endpoint_name: &str) {
        let adr_client = create_adr_client();
        let result = adr_client
            .observe_device_update_notifications(
                device_name.to_string(),
                endpoint_name.to_string(),
                DURATION,
            )
            .await;

        assert!(matches!(
            result.unwrap_err().0,
            ErrorKind::AIOProtocolError(ref e) if matches!(e.kind, AIOProtocolErrorKind::ConfigurationInvalid)
        ));
    }

    #[tokio::test]
    async fn test_observe_device_update_zero_timeout() {
        let adr_client = create_adr_client();
        let result = adr_client
            .observe_device_update_notifications(
                DEVICE_NAME.to_string(),
                INBOUND_ENDPOINT_NAME.to_string(),
                Duration::from_secs(0),
            )
            .await;
        assert!(matches!(
            result.unwrap_err().kind(),
            ErrorKind::InvalidRequestArgument(_)
        ));
    }

    #[test_case("", INBOUND_ENDPOINT_NAME)]
    #[test_case(DEVICE_NAME, "")]
    #[tokio::test]
    async fn test_unobserve_device_invalid_topic_tokens(device_name: &str, endpoint_name: &str) {
        let adr_client = create_adr_client();
        let result = adr_client
            .unobserve_device_update_notifications(
                device_name.to_string(),
                endpoint_name.to_string(),
                DURATION,
            )
            .await;

        assert!(matches!(
            result.unwrap_err().0,
            ErrorKind::AIOProtocolError(ref e) if matches!(e.kind, AIOProtocolErrorKind::ConfigurationInvalid)
        ));
    }

    #[tokio::test]
    async fn test_unobserve_device_update_zero_timeout() {
        let adr_client = create_adr_client();
        let result = adr_client
            .unobserve_device_update_notifications(
                DEVICE_NAME.to_string(),
                INBOUND_ENDPOINT_NAME.to_string(),
                Duration::from_secs(0),
            )
            .await;
        assert!(matches!(
            result.unwrap_err().kind(),
            ErrorKind::InvalidRequestArgument(_)
        ));
    }

    #[test]
    fn test_client_options_builder_default_auto_ack() {
        let options = ClientOptionsBuilder::default().build().unwrap();
        assert!(options.notification_auto_ack);
    }

    #[test]
    fn test_client_options_builder_custom_auto_ack() {
        let options = ClientOptionsBuilder::default()
            .notification_auto_ack(false)
            .build()
            .unwrap();

        assert!(!options.notification_auto_ack);
    }

    #[test]
    fn test_get_topic_tokens() {
        let device_name = "test-device".to_string();
        let inbound_endpoint_name = "test-endpoint".to_string();

        let topic_tokens = Client::<SessionManagedClient>::get_topic_tokens(
            device_name.clone(),
            inbound_endpoint_name.clone(),
        );

        assert_eq!(topic_tokens.len(), 2);
        assert_eq!(
            topic_tokens.get(DEVICE_NAME_TOPIC_TOKEN),
            Some(&device_name)
        );
        assert_eq!(
            topic_tokens.get(INBOUND_ENDPOINT_NAME_TOPIC_TOKEN),
            Some(&inbound_endpoint_name)
        );
        assert!(topic_tokens.keys().all(|key| {
            key == DEVICE_NAME_TOPIC_TOKEN || key == INBOUND_ENDPOINT_NAME_TOPIC_TOKEN
        }));
    }

    #[test]
    fn test_hash_and_unhash_device_endpoint() {
        let device_name = "device1";
        let endpoint_name = "endpoint1";

        let hashed =
            Client::<SessionManagedClient>::hash_device_endpoint(device_name, endpoint_name);
        assert_eq!(hashed, "device1~endpoint1");

        let unhashed = Client::<SessionManagedClient>::unhash_device_endpoint(&hashed);
        assert!(unhashed.is_some());

        let (unhashed_device, unhashed_endpoint) = unhashed.unwrap();
        assert_eq!(unhashed_device, device_name);
        assert_eq!(unhashed_endpoint, endpoint_name);
    }

    #[test]
    fn test_hash_and_unhash_device_endpoint_asset() {
        let device_name = "device1";
        let endpoint_name = "endpoint1";
        let aseet_name = "asset1";

        let hashed = Client::<SessionManagedClient>::hash_device_endpoint_asset(
            device_name,
            endpoint_name,
            aseet_name,
        );
        assert_eq!(hashed, "device1~endpoint1~asset1");

        let unhashed = Client::<SessionManagedClient>::unhash_device_endpoint_asset(&hashed);
        assert!(unhashed.is_some());

        let (unhashed_device, unhashed_endpoint, unhashed_asset) = unhashed.unwrap();
        assert_eq!(unhashed_device, device_name);
        assert_eq!(unhashed_endpoint, endpoint_name);
        assert_eq!(unhashed_asset, aseet_name);
    }

    #[test]
    fn test_hash_and_unhash_invalid_device_endpoint_asset() {
        let invalid_hash = "device1";
        let unhashed_invalid =
            Client::<SessionManagedClient>::unhash_device_endpoint_asset(invalid_hash);
        assert!(unhashed_invalid.is_none());
    }
    #[test]
    fn test_hash_and_unhash_invalid_device_endpoint() {
        // Test unhashing with invalid input
        let invalid_hash = "device1";
        let unhashed_invalid = Client::<SessionManagedClient>::unhash_device_endpoint(invalid_hash);
        assert!(unhashed_invalid.is_none());
    }
}
