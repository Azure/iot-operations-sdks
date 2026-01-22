// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

//! Client for Azure Device Registry operations.
//!
//! To use this client, the `azure_device_registry` feature must be enabled.

use std::{collections::HashMap, sync::Arc, time::Duration};

use azure_iot_operations_mqtt::{session::SessionManagedClient, token::AckToken};
use azure_iot_operations_protocol::application::ApplicationContext;
use derive_builder::Builder;
use tokio::sync::Notify;

use crate::azure_device_registry::models::{
    Asset, AssetStatus, DatasetRuntimeHealthEvent, Device, DeviceRef, DeviceStatus,
    DiscoveredAsset, DiscoveredDevice, EventRuntimeHealthEvent, ManagementActionRuntimeHealthEvent,
    StreamRuntimeHealthEvent,
};
use crate::azure_device_registry::{
    AssetRef, AssetUpdateObservation, DeviceUpdateObservation, Error, ErrorKind, RuntimeHealth,
    health_reporter,
};
use crate::azure_device_registry::{
    adr_base_gen::adr_base_service::client as base_client_gen,
    adr_base_gen::adr_base_service::service as base_service_gen,
    adr_base_gen::common_types::options as base_options_gen,
    device_discovery_gen::common_types::options as discovery_options_gen,
    device_discovery_gen::device_discovery_service::client as discovery_client_gen,
};
use crate::common::dispatcher::{DispatchError, DispatchErrorKind, Dispatcher};

const DEVICE_NAME_TOPIC_TOKEN: &str = "deviceName";
const DEVICE_NAME_RECEIVED_TOPIC_TOKEN: &str = "ex:deviceName";
const INBOUND_ENDPOINT_NAME_TOPIC_TOKEN: &str = "inboundEndpointName";
const INBOUND_ENDPOINT_NAME_RECEIVED_TOPIC_TOKEN: &str = "ex:inboundEndpointName";
const INBOUND_ENDPOINT_TYPE_TOPIC_TOKEN: &str = "inboundEndpointType";

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
pub struct Client {
    // general
    shutdown_notifier: Arc<Notify>,
    // device
    get_device_command_invoker: Arc<base_client_gen::GetDeviceCommandInvoker>,
    get_device_status_command_invoker: Arc<base_client_gen::GetDeviceStatusCommandInvoker>,
    update_device_status_command_invoker: Arc<base_client_gen::UpdateDeviceStatusCommandInvoker>,
    device_endpoint_health_telemetry_sender:
        Arc<base_service_gen::DeviceEndpointRuntimeHealthEventTelemetrySender>,
    notify_on_device_update_command_invoker:
        Arc<base_client_gen::SetNotificationPreferenceForDeviceUpdatesCommandInvoker>,
    create_or_update_discovered_device_command_invoker:
        Arc<discovery_client_gen::CreateOrUpdateDiscoveredDeviceCommandInvoker>,
    device_update_notification_dispatcher: Arc<Dispatcher<(Device, Option<AckToken>), DeviceRef>>,
    // asset
    get_asset_command_invoker: Arc<base_client_gen::GetAssetCommandInvoker>,
    get_asset_status_command_invoker: Arc<base_client_gen::GetAssetStatusCommandInvoker>,
    update_asset_status_command_invoker: Arc<base_client_gen::UpdateAssetStatusCommandInvoker>,
    dataset_health_telemetry_sender:
        Arc<base_service_gen::DatasetRuntimeHealthEventTelemetrySender>,
    event_health_telemetry_sender: Arc<base_service_gen::EventRuntimeHealthEventTelemetrySender>,
    stream_health_telemetry_sender: Arc<base_service_gen::StreamRuntimeHealthEventTelemetrySender>,
    management_action_health_telemetry_sender:
        Arc<base_service_gen::ManagementActionRuntimeHealthEventTelemetrySender>,
    notify_on_asset_update_command_invoker:
        Arc<base_client_gen::SetNotificationPreferenceForAssetUpdatesCommandInvoker>,
    create_or_update_discovered_asset_command_invoker:
        Arc<base_client_gen::CreateOrUpdateDiscoveredAssetCommandInvoker>,
    asset_update_notification_dispatcher: Arc<Dispatcher<(Asset, Option<AckToken>), AssetRef>>,
}

impl Client {
    // ~~~~~~~~~~~~~~~~~ General APIs ~~~~~~~~~~~~~~~~~~~~~
    /// Create a new Azure Device Registry Client.
    ///
    /// # Errors
    /// [`struct@Error`] of kind [`ValidationError`](ErrorKind::ValidationError)
    /// if the Client Id of the [`SessionManagedClient`] isn't valid as a topic token.
    /// # Panics
    /// Panics if the options for the underlying command invokers or receivers cannot be built. Not possible since
    /// the options are statically generated.
    #[allow(clippy::needless_pass_by_value)]
    pub fn new(
        application_context: ApplicationContext,
        client: SessionManagedClient,
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

        let command_options_base = base_options_gen::CommandInvokerOptionsBuilder::default()
            .topic_token_map(HashMap::from([(
                "connectorClientId".to_string(),
                client.client_id().to_string(),
            )]))
            .build()
            .expect("Builder cannot fail as there is no validation function");
        let command_options_discovery =
            discovery_options_gen::CommandInvokerOptionsBuilder::default()
                .topic_token_map(HashMap::from([(
                    "discoveryClientId".to_string(),
                    client.client_id().to_string(),
                )]))
                .build()
                .expect("Builder cannot fail as there is no validation function");

        let telemetry_receiver_options =
            base_options_gen::TelemetryReceiverOptionsBuilder::default()
                .topic_token_map(HashMap::from([(
                    "connectorClientId".to_string(),
                    client.client_id().to_string(),
                )]))
                .auto_ack(options.notification_auto_ack)
                .build()
                .expect("Builder cannot fail as there is no validation function");

        let telemetry_sender_options = base_options_gen::TelemetrySenderOptionsBuilder::default()
            .topic_token_map(HashMap::from([(
                "connectorClientId".to_string(),
                client.client_id().to_string(),
            )]))
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
                base_client_gen::DeviceUpdateEventTelemetryReceiver::new(
                    application_context.clone(),
                    client.clone(),
                    &telemetry_receiver_options,
                );
            let asset_update_telemetry_receiver =
                base_client_gen::AssetUpdateEventTelemetryReceiver::new(
                    application_context.clone(),
                    client.clone(),
                    &telemetry_receiver_options,
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
            get_device_command_invoker: Arc::new(base_client_gen::GetDeviceCommandInvoker::new(
                application_context.clone(),
                client.clone(),
                &command_options_base,
            )),
            get_device_status_command_invoker: Arc::new(
                base_client_gen::GetDeviceStatusCommandInvoker::new(
                    application_context.clone(),
                    client.clone(),
                    &command_options_base,
                ),
            ),
            update_device_status_command_invoker: Arc::new(
                base_client_gen::UpdateDeviceStatusCommandInvoker::new(
                    application_context.clone(),
                    client.clone(),
                    &command_options_base,
                ),
            ),
            device_endpoint_health_telemetry_sender: Arc::new(
                base_service_gen::DeviceEndpointRuntimeHealthEventTelemetrySender::new(
                    application_context.clone(),
                    client.clone(),
                    &telemetry_sender_options,
                ),
            ),
            notify_on_device_update_command_invoker: Arc::new(
                base_client_gen::SetNotificationPreferenceForDeviceUpdatesCommandInvoker::new(
                    application_context.clone(),
                    client.clone(),
                    &command_options_base,
                ),
            ),
            create_or_update_discovered_device_command_invoker: Arc::new(
                discovery_client_gen::CreateOrUpdateDiscoveredDeviceCommandInvoker::new(
                    application_context.clone(),
                    client.clone(),
                    &command_options_discovery,
                ),
            ),
            device_update_notification_dispatcher,
            get_asset_command_invoker: Arc::new(base_client_gen::GetAssetCommandInvoker::new(
                application_context.clone(),
                client.clone(),
                &command_options_base,
            )),
            get_asset_status_command_invoker: Arc::new(
                base_client_gen::GetAssetStatusCommandInvoker::new(
                    application_context.clone(),
                    client.clone(),
                    &command_options_base,
                ),
            ),
            update_asset_status_command_invoker: Arc::new(
                base_client_gen::UpdateAssetStatusCommandInvoker::new(
                    application_context.clone(),
                    client.clone(),
                    &command_options_base,
                ),
            ),
            dataset_health_telemetry_sender: Arc::new(
                base_service_gen::DatasetRuntimeHealthEventTelemetrySender::new(
                    application_context.clone(),
                    client.clone(),
                    &telemetry_sender_options,
                ),
            ),
            event_health_telemetry_sender: Arc::new(
                base_service_gen::EventRuntimeHealthEventTelemetrySender::new(
                    application_context.clone(),
                    client.clone(),
                    &telemetry_sender_options,
                ),
            ),
            stream_health_telemetry_sender: Arc::new(
                base_service_gen::StreamRuntimeHealthEventTelemetrySender::new(
                    application_context.clone(),
                    client.clone(),
                    &telemetry_sender_options,
                ),
            ),
            management_action_health_telemetry_sender: Arc::new(
                base_service_gen::ManagementActionRuntimeHealthEventTelemetrySender::new(
                    application_context.clone(),
                    client.clone(),
                    &telemetry_sender_options,
                ),
            ),
            notify_on_asset_update_command_invoker: Arc::new(
                base_client_gen::SetNotificationPreferenceForAssetUpdatesCommandInvoker::new(
                    application_context.clone(),
                    client.clone(),
                    &command_options_base,
                ),
            ),
            create_or_update_discovered_asset_command_invoker: Arc::new(
                base_client_gen::CreateOrUpdateDiscoveredAssetCommandInvoker::new(
                    application_context,
                    client,
                    &command_options_base,
                ),
            ),
            asset_update_notification_dispatcher,
        })
    }

    /// Convenience function to get all observed device & inbound endpoint names to quickly unobserve all of them before cleaning up
    #[must_use]
    pub fn get_all_observed_device_endpoints(&self) -> Vec<DeviceRef> {
        self.device_update_notification_dispatcher
            .get_all_receiver_ids()
    }

    /// Convenience function to get all observed asset names to quickly unobserve all of them before cleaning up
    #[must_use]
    pub fn get_all_observed_assets(&self) -> Vec<AssetRef> {
        self.asset_update_notification_dispatcher
            .get_all_receiver_ids()
    }

    /// Shutdown the [`Client`]. Shuts down the underlying command invokers and telemetry receivers.
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

        let (result1, result2, result3, result4, result5, result6, result7, result8) = tokio::join!(
            self.get_device_command_invoker.shutdown(),
            self.get_device_status_command_invoker.shutdown(),
            self.update_device_status_command_invoker.shutdown(),
            self.notify_on_device_update_command_invoker.shutdown(),
            self.get_asset_command_invoker.shutdown(),
            self.get_asset_status_command_invoker.shutdown(),
            self.update_asset_status_command_invoker.shutdown(),
            self.notify_on_asset_update_command_invoker.shutdown()
        );

        for result in [
            result1, result2, result3, result4, result5, result6, result7, result8,
        ] {
            if let Err(e) = result {
                errors.push(e);
            }
        }

        if errors.is_empty() {
            log::info!("Azure Device Registry Client shutdown done gracefully");
            Ok(())
        } else {
            Err(Error(ErrorKind::ShutdownError(errors)))
        }
    }

    /// Helper function to get the topic tokens for a device and inbound endpoint.
    /// Used for the base service.
    fn get_base_service_topic_tokens(
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

    /// Helper function to get the topic tokens for an inbound endpoint type.
    /// Used for the discovery service.
    fn get_discovery_service_topic_tokens(
        inbound_endpoint_type: String,
    ) -> HashMap<String, String> {
        HashMap::from([(
            INBOUND_ENDPOINT_TYPE_TOPIC_TOKEN.to_string(),
            inbound_endpoint_type,
        )])
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
        mut device_update_telemetry_receiver: base_client_gen::DeviceUpdateEventTelemetryReceiver,
        device_update_notification_dispatcher: Arc<
            Dispatcher<(Device, Option<AckToken>), DeviceRef>,
        >,
        mut asset_update_telemetry_receiver: base_client_gen::AssetUpdateEventTelemetryReceiver,
        asset_update_notification_dispatcher: Arc<Dispatcher<(Asset, Option<AckToken>), AssetRef>>,
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
                                log::warn!("Device Update Notification missing {DEVICE_NAME_RECEIVED_TOPIC_TOKEN} topic token.");
                                continue;
                            };
                            let Some(inbound_endpoint_name) = device_update_telemetry.topic_tokens.get(INBOUND_ENDPOINT_NAME_RECEIVED_TOPIC_TOKEN) else {
                                log::warn!("Device Update Notification missing {INBOUND_ENDPOINT_NAME_RECEIVED_TOPIC_TOKEN} topic token.");
                                continue;
                            };

                            // Try to send the notification to the associated receiver
                            let receiver_id = DeviceRef {
                                device_name: device_name.clone(),
                                endpoint_name: inbound_endpoint_name.clone(),
                            };
                            match device_update_notification_dispatcher.dispatch(&receiver_id, (device_update_telemetry.payload.device_update_event.device.into(), ack_token)) {
                                Ok(()) => {
                                    log::debug!("Device Update Notification dispatched for {receiver_id:?}");
                                }
                                Err(DispatchError { data: (_, _), kind: DispatchErrorKind::SendError }) => {
                                    log::warn!("Device Update Observation has been dropped. Received Device Update Notification for {receiver_id:?}");
                                }
                                Err(DispatchError { data: (_, _), kind: DispatchErrorKind::NotFound(receiver_id) }) => {
                                    log::warn!("Device Endpoint is not being observed. Received Device Update Notification for {receiver_id:?}");
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
                                log::warn!("Asset Update Notification missing {DEVICE_NAME_RECEIVED_TOPIC_TOKEN} topic token.");
                                continue;
                            };
                            let Some(inbound_endpoint_name) = asset_update_telemetry.topic_tokens.get(INBOUND_ENDPOINT_NAME_RECEIVED_TOPIC_TOKEN) else {
                                log::warn!("Asset Update Notification missing {INBOUND_ENDPOINT_NAME_RECEIVED_TOPIC_TOKEN} topic token.");
                                continue;
                            };

                            // Try to send the notification to the associated receiver
                            let receiver_id = AssetRef {
                                device_name: device_name.clone(),
                                inbound_endpoint_name: inbound_endpoint_name.clone(),
                                name: asset_update_telemetry.payload.asset_update_event.asset_name.clone(),
                            };
                            match asset_update_notification_dispatcher.dispatch(&receiver_id, (asset_update_telemetry.payload.asset_update_event.asset.into(), ack_token)) {
                                Ok(()) => {
                                    log::debug!("Asset Update Notification dispatched for {receiver_id:?}");
                                }
                                Err(DispatchError { data: (_, _), kind: DispatchErrorKind::SendError }) => {
                                    log::warn!("Asset Update Observation has been dropped. Received Asset Update Notification for {receiver_id:?}");
                                }
                                Err(DispatchError { data: (_, _), kind: DispatchErrorKind::NotFound(receiver_id) }) => {
                                    log::warn!("Asset is not being observed. Received Asset Update Notification for {receiver_id:?}");
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
    /// [`struct@Error`] of kind [`ValidationError`](ErrorKind::ValidationError) if timeout is 0
    /// or > `u32::max`.
    ///
    /// [`struct@Error`] of kind [`AIOProtocolError`](ErrorKind::AIOProtocolError) if:
    /// - device or inbound endpoint names are invalid.
    /// - there are any underlying errors from the AIO RPC protocol.
    ///
    /// [`struct@Error`] of kind [`ServiceError`](ErrorKind::ServiceError) if an error is returned
    /// by the Azure Device Registry service.
    pub async fn get_device(
        &self,
        device_name: String,
        inbound_endpoint_name: String,
        timeout: Duration,
    ) -> Result<Device, Error> {
        let get_device_request = base_client_gen::GetDeviceRequestBuilder::default()
            .topic_tokens(Self::get_base_service_topic_tokens(
                device_name,
                inbound_endpoint_name,
            ))
            .timeout(timeout)
            .build()
            .map_err(ErrorKind::from)?;

        let response = self
            .get_device_command_invoker
            .invoke(get_device_request)
            .await
            .map_err(ErrorKind::from)?
            .map_err(ErrorKind::from)?;
        Ok(response.payload.device.into())
    }

    /// Retrieves a [`DeviceStatus`] from the Azure Device Registry service.
    ///
    /// # Arguments
    /// * `device_name` - The name of the device.
    /// * `inbound_endpoint_name` - The name of the inbound endpoint.
    /// * `timeout` - The duration until the client stops waiting for a response to the request, it is rounded up to the nearest second.
    ///
    /// Returns the [`DeviceStatus`] if the device was found.
    ///
    /// # Errors
    /// [`struct@Error`] of kind [`ValidationError`](ErrorKind::ValidationError) if timeout is 0
    /// or > `u32::max`.
    ///
    /// [`struct@Error`] of kind [`AIOProtocolError`](ErrorKind::AIOProtocolError) if:
    /// - device or inbound endpoint names are invalid.
    /// - there are any underlying errors from the AIO RPC protocol.
    ///
    /// [`struct@Error`] of kind [`ServiceError`](ErrorKind::ServiceError) if an error is returned
    /// by the Azure Device Registry service.
    pub async fn get_device_status(
        &self,
        device_name: String,
        inbound_endpoint_name: String,
        timeout: Duration,
    ) -> Result<DeviceStatus, Error> {
        let get_device_status_request = base_client_gen::GetDeviceStatusRequestBuilder::default()
            .topic_tokens(Self::get_base_service_topic_tokens(
                device_name,
                inbound_endpoint_name,
            ))
            .timeout(timeout)
            .build()
            .map_err(ErrorKind::from)?;

        let response = self
            .get_device_status_command_invoker
            .invoke(get_device_status_request)
            .await
            .map_err(ErrorKind::from)?
            .map_err(ErrorKind::from)?;
        Ok(response.payload.device_status.into())
    }

    /// Updates a Device's status in the Azure Device Registry service.
    ///
    /// # Arguments
    /// * `device_name` - The name of the device.
    /// * `inbound_endpoint_name` - The name of the inbound endpoint.
    /// * `status` - A [`DeviceStatus`] containing all status information for the device.
    /// * `timeout` - The duration until the client stops waiting for a response to the request, it is rounded up to the nearest second.
    ///
    /// Returns the updated [`DeviceStatus`] once updated.
    ///
    /// # Errors
    /// [`struct@Error`] of kind [`ValidationError`](ErrorKind::ValidationError) if timeout is 0
    /// or > `u32::max`.
    ///
    /// [`struct@Error`] of kind [`AIOProtocolError`](ErrorKind::AIOProtocolError) if:
    /// - device or inbound endpoint names are invalid.
    /// - there are any underlying errors from the AIO RPC protocol.
    ///
    /// [`struct@Error`] of kind [`ServiceError`](ErrorKind::ServiceError) if an error is returned
    /// by the Azure Device Registry service.
    pub async fn update_device_plus_endpoint_status(
        &self,
        device_name: String,
        inbound_endpoint_name: String,
        status: DeviceStatus,
        timeout: Duration,
    ) -> Result<DeviceStatus, Error> {
        let status_payload = base_client_gen::UpdateDeviceStatusRequestPayload {
            device_status_update: status.into(),
        };
        let update_device_status_request =
            base_client_gen::UpdateDeviceStatusRequestBuilder::default()
                .payload(status_payload)
                .map_err(ErrorKind::from)?
                .topic_tokens(Self::get_base_service_topic_tokens(
                    device_name,
                    inbound_endpoint_name,
                ))
                .timeout(timeout)
                .build()
                .map_err(ErrorKind::from)?;
        let response = self
            .update_device_status_command_invoker
            .invoke(update_device_status_request)
            .await
            .map_err(ErrorKind::from)?
            .map_err(ErrorKind::from)?;
        Ok(response.payload.updated_device_status.into())
    }

    /// Reports a Device Endpoint's runtime health status to the Azure Device Registry service.
    ///
    /// # Arguments
    /// * `device_name` - The name of the device.
    /// * `inbound_endpoint_name` - The name of the inbound endpoint.
    /// * `runtime_health` - A [`RuntimeHealth`] containing all runtime health information for the device endpoint.
    /// * `message_expiry` - The duration for which the message will be attempted to be given to the service, it is rounded up to the nearest second.
    ///
    /// # Errors
    /// [`struct@Error`] of kind [`ValidationError`](ErrorKind::ValidationError)
    /// if `message_expiry` is > `u32::max`.
    ///
    /// [`struct@Error`] of kind [`AIOProtocolError`](ErrorKind::AIOProtocolError) if:
    /// - device or inbound endpoint names are invalid.
    /// - there are any underlying errors from the AIO Telemetry protocol.
    pub async fn report_device_endpoint_runtime_health_event(
        &self,
        device_name: String,
        inbound_endpoint_name: String,
        runtime_health: RuntimeHealth,
        message_expiry: Duration,
    ) -> Result<(), Error> {
        let health_status_message =
            base_service_gen::DeviceEndpointRuntimeHealthEventTelemetryMessageBuilder::default()
                .payload(runtime_health.into())
                .map_err(ErrorKind::from)?
                .topic_tokens(Self::get_base_service_topic_tokens(
                    device_name,
                    inbound_endpoint_name,
                ))
                .message_expiry(message_expiry)
                .build()
                .map_err(ErrorKind::from)?;
        self.device_endpoint_health_telemetry_sender
            .send(health_status_message)
            .await
            .map_err(|e| Error::from(ErrorKind::from(e)))
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
    /// [`struct@Error`] of kind [`ValidationError`](ErrorKind::ValidationError) if timeout is 0
    /// or > `u32::max`.
    ///
    /// [`struct@Error`] of kind [`AIOProtocolError`](ErrorKind::AIOProtocolError) if:
    /// - device or inbound endpoint names are invalid.
    /// - there are any underlying errors from the AIO RPC protocol.
    ///
    /// [`struct@Error`] of kind [`ServiceError`](ErrorKind::ServiceError) if an error is returned
    /// by the Azure Device Registry service.
    pub async fn observe_device_update_notifications(
        &self,
        device_name: String,
        inbound_endpoint_name: String,
        timeout: Duration,
    ) -> Result<DeviceUpdateObservation, Error> {
        let receiver_id = DeviceRef {
            device_name: device_name.clone(),
            endpoint_name: inbound_endpoint_name.clone(),
        };
        let rx = self
            .device_update_notification_dispatcher
            .register_receiver(receiver_id.clone())
            .map_err(ErrorKind::from)?;

        let observe_payload =
            base_client_gen::SetNotificationPreferenceForDeviceUpdatesRequestPayload {
                notification_preference_request: base_client_gen::NotificationPreference::On,
            };

        let observe_request =
            base_client_gen::SetNotificationPreferenceForDeviceUpdatesRequestBuilder::default()
                .payload(observe_payload)
                .map_err(ErrorKind::from)?
                .topic_tokens(Self::get_base_service_topic_tokens(
                    device_name,
                    inbound_endpoint_name,
                ))
                .timeout(timeout)
                .build()
                .map_err(ErrorKind::from)?;

        match self
            .notify_on_device_update_command_invoker
            .invoke(observe_request)
            .await
        {
            Ok(Ok(_)) => Ok(DeviceUpdateObservation(rx)),
            error_branches => {
                if self
                    .device_update_notification_dispatcher
                    .unregister_receiver(&receiver_id)
                {
                    log::debug!(
                        "Removed device with receiver ID `{receiver_id:?}` from observed list"
                    );
                } else {
                    log::debug!("Device with receiver ID `{receiver_id:?}` not in observed list");
                }

                match error_branches {
                    Ok(Err(e)) => Err(Error(ErrorKind::from(e))),
                    Err(e) => Err(Error(ErrorKind::from(e))),
                    Ok(Ok(_)) => unreachable!(), // This case should not happen, as we already checked this in the first match branch
                }
            }
        }
    }

    /// Stops observation of a [`Device`]'s updates from the Azure Device Registry service.
    /// Once this request has been sent, no more notifications will be received, regardless
    /// of whether the service successfully processes it or not. However, if the request
    /// is not sent due to input validation errors, the observation will continue because
    /// the request could not be sent.
    ///
    /// # Arguments
    /// * `device_name` - The name of the device.
    /// * `inbound_endpoint_name` - The name of the inbound endpoint.
    /// * `timeout` - The duration until the client stops waiting for a response to the request, it is rounded up to the nearest second.
    ///
    /// Returns `Ok(())` if the device updates are no longer being observed.
    ///
    /// # Errors
    /// [`struct@Error`] of kind [`ValidationError`](ErrorKind::ValidationError) if timeout is 0
    /// or > `u32::max`.
    ///
    /// [`struct@Error`] of kind [`AIOProtocolError`](ErrorKind::AIOProtocolError) if:
    /// - device or inbound endpoint names are invalid.
    /// - there are any underlying errors from the AIO RPC protocol.
    ///
    /// [`struct@Error`] of kind [`ServiceError`](ErrorKind::ServiceError) if an error is returned
    /// by the Azure Device Registry service.
    pub async fn unobserve_device_update_notifications(
        &self,
        device_name: String,
        inbound_endpoint_name: String,
        timeout: Duration,
    ) -> Result<(), Error> {
        let unobserve_payload =
            base_client_gen::SetNotificationPreferenceForDeviceUpdatesRequestPayload {
                notification_preference_request: base_client_gen::NotificationPreference::Off,
            };

        let unobserve_request =
            base_client_gen::SetNotificationPreferenceForDeviceUpdatesRequestBuilder::default()
                .payload(unobserve_payload)
                .map_err(ErrorKind::from)?
                .topic_tokens(Self::get_base_service_topic_tokens(
                    device_name.clone(),
                    inbound_endpoint_name.clone(),
                ))
                .timeout(timeout)
                .build()
                .map_err(ErrorKind::from)?;
        let result = self
            .notify_on_device_update_command_invoker
            .invoke(unobserve_request)
            .await;

        // Remove this device from our dispatcher whether or not the unobserve was successful to clear this from the dispatcher so that a new observation can be created later
        let receiver_id = DeviceRef {
            device_name,
            endpoint_name: inbound_endpoint_name,
        };
        if self
            .device_update_notification_dispatcher
            .unregister_receiver(&receiver_id)
        {
            log::debug!("Removed `{receiver_id:?}` from observed list");
        } else {
            log::debug!("`{receiver_id:?}` not in observed list");
        }
        // still return any errors from executing the request after removing from dispatcher
        result.map_err(ErrorKind::from)?.map_err(ErrorKind::from)?;
        Ok(())
    }

    /// Creates or updates a discovered device in the Azure Device Registry service.
    ///
    /// If the specified discovered device does not yet exist, it will be created.
    /// If it already exists, it will be replaced.
    ///
    /// # Arguments
    /// * `device_name` - The name of the discovered device.
    /// * `device` - The specification of the discovered device.
    /// * `inbound_endpoint_type` - The type of the inbound endpoint.
    /// * `timeout` - The duration until the client stops waiting for a response to the request, it is rounded up to the nearest second.
    ///
    /// Returns tuple containing the discovery ID and version of the discovered device.
    ///
    /// # Errors
    /// [`struct@Error`] of kind [`ValidationError`](ErrorKind::ValidationError) if timeout is 0
    /// or > `u32::max` or if the device name is empty.
    ///
    /// [`struct@Error`] of kind [`AIOProtocolError`](ErrorKind::AIOProtocolError) if:
    /// - inbound endpoint type is invalid for the topic.
    /// - there are any underlying errors from the AIO RPC protocol.
    ///
    /// [`struct@Error`] of kind [`ServiceError`](ErrorKind::ServiceError) if an error is returned
    /// by the Azure Device Registry service.
    pub async fn create_or_update_discovered_device(
        &self,
        device_name: String,
        device: DiscoveredDevice,
        inbound_endpoint_type: String,
        timeout: Duration,
    ) -> Result<(String, u64), Error> {
        if device_name.trim().is_empty() {
            return Err(Error(ErrorKind::ValidationError(
                "device_name must not be empty".to_string(),
            )));
        }
        let payload = discovery_client_gen::CreateOrUpdateDiscoveredDeviceRequestPayload {
            discovered_device_request:
                discovery_client_gen::CreateOrUpdateDiscoveredDeviceRequestSchema {
                    discovered_device: device.into(),
                    discovered_device_name: device_name,
                },
        };
        let request = discovery_client_gen::CreateOrUpdateDiscoveredDeviceRequestBuilder::default()
            .payload(payload)
            .map_err(ErrorKind::from)?
            .topic_tokens(Self::get_discovery_service_topic_tokens(
                inbound_endpoint_type,
            ))
            .timeout(timeout)
            .build()
            .map_err(ErrorKind::from)?;
        let response = self
            .create_or_update_discovered_device_command_invoker
            .invoke(request)
            .await
            .map_err(ErrorKind::from)?
            .map_err(ErrorKind::from)?;

        let discovery_id = response.payload.discovered_device_response.discovery_id;
        let version = response.payload.discovered_device_response.version;
        Ok((discovery_id, version))
    }

    /// Creates a new background health reporter for a device endpoint.
    ///
    /// Spawns a background task that handles deduplication and periodic re-reporting
    /// of health status for the specified device endpoint.
    ///
    /// # Arguments
    /// * `device_ref` - Reference to the device and endpoint.
    /// * `message_expiry` - The duration for which the message will be attempted to be given to the service, it is rounded up to the nearest second.
    /// * `report_interval` - Interval for re-reporting steady-state health when no changes occur.
    /// * `cancellation_token` - Token to signal cancellation of the background task. Should be triggered on device endpoint deletion.
    ///
    /// Returns a [`HealthReporterSender`](health_reporter::HealthReporterSender) that can be used to send health events.
    #[must_use]
    pub fn new_device_endpoint_health_reporter(
        &self,
        device_ref: DeviceRef,
        message_expiry: Duration,
        report_interval: health_reporter::ReportInterval,
        cancellation_token: tokio_util::sync::CancellationToken,
    ) -> health_reporter::HealthReporterSender {
        let reporter = health_reporter::DeviceEndpointHealthReporter {
            client: self.clone(),
            device_ref,
            message_expiry,
        };
        health_reporter::new_health_reporter(reporter, report_interval, cancellation_token)
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
    /// [`struct@Error`] of kind [`ValidationError`](ErrorKind::ValidationError) if timeout is 0
    /// or > `u32::max` or if the asset name is empty.
    ///
    /// [`struct@Error`] of kind [`AIOProtocolError`](ErrorKind::AIOProtocolError) if:
    /// - device or inbound endpoint names are invalid.
    /// - there are any underlying errors from the AIO RPC protocol.
    ///
    /// [`struct@Error`] of kind [`ServiceError`](ErrorKind::ServiceError) if an error is returned
    /// by the Azure Device Registry service.
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
        let payload = base_client_gen::GetAssetRequestPayload { asset_name };
        let command_request = base_client_gen::GetAssetRequestBuilder::default()
            .payload(payload)
            .map_err(ErrorKind::from)?
            .timeout(timeout)
            .topic_tokens(Self::get_base_service_topic_tokens(
                device_name,
                inbound_endpoint_name,
            ))
            .build()
            .map_err(ErrorKind::from)?;

        let response = self
            .get_asset_command_invoker
            .invoke(command_request)
            .await
            .map_err(ErrorKind::from)?
            .map_err(ErrorKind::from)?;

        Ok(response.payload.asset.into())
    }

    /// Retrieves an [`AssetStatus`] from the Azure Device Registry service.
    ///
    /// # Arguments
    /// * `device_name` - The name of the device.
    /// * `inbound_endpoint_name` - The name of the inbound endpoint.
    /// * `asset_name` - The name of the asset.
    /// * `timeout` - The duration until the client stops waiting for a response to the request, it is rounded up to the nearest second.
    ///
    /// Returns an [`AssetStatus`] if the the asset was found.
    ///
    /// # Errors
    /// [`struct@Error`] of kind [`ValidationError`](ErrorKind::ValidationError) if timeout is 0
    /// or > `u32::max` or if the asset name is empty.
    ///
    /// [`struct@Error`] of kind [`AIOProtocolError`](ErrorKind::AIOProtocolError) if:
    /// - device or inbound endpoint names are invalid.
    /// - there are any underlying errors from the AIO RPC protocol.
    ///
    /// [`struct@Error`] of kind [`ServiceError`](ErrorKind::ServiceError) if an error is returned
    /// by the Azure Device Registry service.
    pub async fn get_asset_status(
        &self,
        device_name: String,
        inbound_endpoint_name: String,
        asset_name: String,
        timeout: Duration,
    ) -> Result<AssetStatus, Error> {
        if asset_name.trim().is_empty() {
            return Err(Error(ErrorKind::ValidationError(
                "asset_name must not be empty".to_string(),
            )));
        }
        let get_status_request_payload =
            base_client_gen::GetAssetStatusRequestPayload { asset_name };
        let get_status_request = base_client_gen::GetAssetStatusRequestBuilder::default()
            .payload(get_status_request_payload)
            .map_err(ErrorKind::from)?
            .timeout(timeout)
            .topic_tokens(Self::get_base_service_topic_tokens(
                device_name,
                inbound_endpoint_name,
            ))
            .build()
            .map_err(ErrorKind::from)?;

        let response = self
            .get_asset_status_command_invoker
            .invoke(get_status_request)
            .await
            .map_err(ErrorKind::from)?
            .map_err(ErrorKind::from)?;

        Ok(response.payload.asset_status.into())
    }

    /// Updates the status of an Asset in the Azure Device Registry service.
    ///
    /// # Arguments
    /// * `device_name` - The name of the device.
    /// * `inbound_endpoint_name` - The name of the inbound endpoint.
    /// * `asset_name` - The name of the asset.
    /// * `status` - An [`AssetStatus`] containing the status of an asset for the update.
    /// * `timeout` - The duration until the client stops waiting for a response to the request, it is rounded up to the nearest second.
    ///
    /// Returns the updated [`AssetStatus`] once updated.
    ///
    /// # Errors
    /// [`struct@Error`] of kind [`ValidationError`](ErrorKind::ValidationError) if timeout is 0
    /// or > `u32::max` or if the asset name is empty.
    ///
    /// [`struct@Error`] of kind [`AIOProtocolError`](ErrorKind::AIOProtocolError) if:
    /// - device or inbound endpoint names are invalid.
    /// - there are any underlying errors from the AIO RPC protocol.
    ///
    /// [`struct@Error`] of kind [`ServiceError`](ErrorKind::ServiceError) if an error is returned
    /// by the Azure Device Registry service.
    pub async fn update_asset_status(
        &self,
        device_name: String,
        inbound_endpoint_name: String,
        asset_name: String,
        status: AssetStatus,
        timeout: Duration,
    ) -> Result<AssetStatus, Error> {
        if asset_name.trim().is_empty() {
            return Err(Error(ErrorKind::ValidationError(
                "asset_name must not be empty".to_string(),
            )));
        }

        let payload = base_client_gen::UpdateAssetStatusRequestPayload {
            asset_status_update: base_client_gen::UpdateAssetStatusRequestSchema {
                asset_name,
                asset_status: status.into(),
            },
        };
        let command_request = base_client_gen::UpdateAssetStatusRequestBuilder::default()
            .payload(payload)
            .map_err(ErrorKind::from)?
            .topic_tokens(Self::get_base_service_topic_tokens(
                device_name,
                inbound_endpoint_name,
            ))
            .timeout(timeout)
            .build()
            .map_err(ErrorKind::from)?;

        let response = self
            .update_asset_status_command_invoker
            .invoke(command_request)
            .await
            .map_err(ErrorKind::from)?
            .map_err(ErrorKind::from)?;

        Ok(response.payload.updated_asset_status.into())
    }

    /// Reports Datasets' runtime health statuses to the Azure Device Registry service.
    /// Note: Reporting multiple dataset statuses in a single call has the same effect
    /// as reporting them individually, but reduced network calls. Duplicate dataset names
    /// in the `runtime_healths` vector will result in the latest (by version and timestamp) entry being used
    ///
    /// # Arguments
    /// * `device_name` - The name of the device.
    /// * `inbound_endpoint_name` - The name of the inbound endpoint.
    /// * `asset_name` - The name of the asset.
    /// * `runtime_healths` - A vector of [`DatasetRuntimeHealthEvent`] containing all runtime health information for the datasets.
    /// * `message_expiry` - The duration for which the message will be attempted to be given to the service, it is rounded up to the nearest second.
    ///
    /// # Errors
    /// [`struct@Error`] of kind [`ValidationError`](ErrorKind::ValidationError)
    /// if `message_expiry` is > `u32::max` or if the asset or any dataset name is empty.
    ///
    /// [`struct@Error`] of kind [`AIOProtocolError`](ErrorKind::AIOProtocolError) if:
    /// - device or inbound endpoint names are invalid.
    /// - there are any underlying errors from the AIO Telemetry protocol.
    pub async fn report_dataset_runtime_health_events(
        &self,
        device_name: String,
        inbound_endpoint_name: String,
        asset_name: String,
        runtime_healths: Vec<DatasetRuntimeHealthEvent>,
        message_expiry: Duration,
    ) -> Result<(), Error> {
        if asset_name.trim().is_empty() {
            return Err(Error(ErrorKind::ValidationError(
                "asset_name must not be empty".to_string(),
            )));
        }
        // If there are no health events to report, this is a no-op
        if runtime_healths.is_empty() {
            return Ok(());
        }

        let dataset_health_events = runtime_healths
            .into_iter()
            .map(|runtime_health_event| {
                if runtime_health_event.dataset_name.trim().is_empty() {
                    return Err(Error(ErrorKind::ValidationError(
                        "dataset_name must not be empty".to_string(),
                    )));
                }
                Ok(runtime_health_event.into())
            })
            .collect::<Result<Vec<_>, _>>()?;

        let payload = base_service_gen::DatasetRuntimeHealthEventTelemetry {
            dataset_runtime_health_event: base_service_gen::DatasetRuntimeHealthEventSchema {
                asset_name,
                datasets: dataset_health_events,
            },
        };

        let health_status_message =
            base_service_gen::DatasetRuntimeHealthEventTelemetryMessageBuilder::default()
                .payload(payload)
                .map_err(ErrorKind::from)?
                .topic_tokens(Self::get_base_service_topic_tokens(
                    device_name,
                    inbound_endpoint_name,
                ))
                .message_expiry(message_expiry)
                .build()
                .map_err(ErrorKind::from)?;
        self.dataset_health_telemetry_sender
            .send(health_status_message)
            .await
            .map_err(|e| Error::from(ErrorKind::from(e)))
    }

    /// Reports Events' runtime health statuses to the Azure Device Registry service.
    /// Note: Reporting multiple event statuses in a single call has the same effect
    /// as reporting them individually, but reduced network calls. Duplicate event names
    /// in the `runtime_healths` vector will result in the latest (by version and timestamp) entry being used
    ///
    /// # Arguments
    /// * `device_name` - The name of the device.
    /// * `inbound_endpoint_name` - The name of the inbound endpoint.
    /// * `asset_name` - The name of the asset.
    /// * `runtime_healths` - A vector of [`EventRuntimeHealthEvent`] containing all runtime health information for the events.
    /// * `message_expiry` - The duration for which the message will be attempted to be given to the service, it is rounded up to the nearest second.
    ///
    /// # Errors
    /// [`struct@Error`] of kind [`ValidationError`](ErrorKind::ValidationError)
    /// if `message_expiry` is > `u32::max` or if the asset or any event group or event name is
    /// empty.
    ///
    /// [`struct@Error`] of kind [`AIOProtocolError`](ErrorKind::AIOProtocolError) if:
    /// - device or inbound endpoint names are invalid.
    /// - there are any underlying errors from the AIO Telemetry protocol.
    pub async fn report_event_runtime_health_events(
        &self,
        device_name: String,
        inbound_endpoint_name: String,
        asset_name: String,
        runtime_healths: Vec<EventRuntimeHealthEvent>,
        message_expiry: Duration,
    ) -> Result<(), Error> {
        if asset_name.trim().is_empty() {
            return Err(Error(ErrorKind::ValidationError(
                "asset_name must not be empty".to_string(),
            )));
        }
        // If there are no health events to report, this is a no-op
        if runtime_healths.is_empty() {
            return Ok(());
        }

        let event_health_events = runtime_healths
            .into_iter()
            .map(|runtime_health_event| {
                if runtime_health_event.event_group_name.trim().is_empty() {
                    return Err(Error(ErrorKind::ValidationError(
                        "event_group_name must not be empty".to_string(),
                    )));
                }
                if runtime_health_event.event_name.trim().is_empty() {
                    return Err(Error(ErrorKind::ValidationError(
                        "event_name must not be empty".to_string(),
                    )));
                }
                Ok(runtime_health_event.into())
            })
            .collect::<Result<Vec<_>, _>>()?;

        let payload = base_service_gen::EventRuntimeHealthEventTelemetry {
            event_runtime_health_event: base_service_gen::EventRuntimeHealthEventSchema {
                asset_name,
                events: event_health_events,
            },
        };

        let health_status_message =
            base_service_gen::EventRuntimeHealthEventTelemetryMessageBuilder::default()
                .payload(payload)
                .map_err(ErrorKind::from)?
                .topic_tokens(Self::get_base_service_topic_tokens(
                    device_name,
                    inbound_endpoint_name,
                ))
                .message_expiry(message_expiry)
                .build()
                .map_err(ErrorKind::from)?;
        self.event_health_telemetry_sender
            .send(health_status_message)
            .await
            .map_err(|e| Error::from(ErrorKind::from(e)))
    }

    /// Reports Streams' runtime health statuses to the Azure Device Registry service.
    /// Note: Reporting multiple stream statuses in a single call has the same effect
    /// as reporting them individually, but reduced network calls. Duplicate stream names
    /// in the `runtime_healths` vector will result in the latest (by version and timestamp) entry being used
    ///
    /// # Arguments
    /// * `device_name` - The name of the device.
    /// * `inbound_endpoint_name` - The name of the inbound endpoint.
    /// * `asset_name` - The name of the asset.
    /// * `runtime_healths` - A vector of [`StreamRuntimeHealthEvent`] containing all runtime health information for the streams.
    /// * `message_expiry` - The duration for which the message will be attempted to be given to the service, it is rounded up to the nearest second.
    ///
    /// # Errors
    /// [`struct@Error`] of kind [`ValidationError`](ErrorKind::ValidationError)
    /// if `message_expiry` is > `u32::max` or if the asset or any stream name is empty.
    ///
    /// [`struct@Error`] of kind [`AIOProtocolError`](ErrorKind::AIOProtocolError) if:
    /// - device or inbound endpoint names are invalid.
    /// - there are any underlying errors from the AIO Telemetry protocol.
    pub async fn report_stream_runtime_health_events(
        &self,
        device_name: String,
        inbound_endpoint_name: String,
        asset_name: String,
        runtime_healths: Vec<StreamRuntimeHealthEvent>,
        message_expiry: Duration,
    ) -> Result<(), Error> {
        if asset_name.trim().is_empty() {
            return Err(Error(ErrorKind::ValidationError(
                "asset_name must not be empty".to_string(),
            )));
        }
        // If there are no health events to report, this is a no-op
        if runtime_healths.is_empty() {
            return Ok(());
        }

        let stream_health_events = runtime_healths
            .into_iter()
            .map(|runtime_health_event| {
                if runtime_health_event.stream_name.trim().is_empty() {
                    return Err(Error(ErrorKind::ValidationError(
                        "stream_name must not be empty".to_string(),
                    )));
                }
                Ok(runtime_health_event.into())
            })
            .collect::<Result<Vec<_>, _>>()?;

        let payload = base_service_gen::StreamRuntimeHealthEventTelemetry {
            stream_runtime_health_event: base_service_gen::StreamRuntimeHealthEventSchema {
                asset_name,
                streams: stream_health_events,
            },
        };

        let health_status_message =
            base_service_gen::StreamRuntimeHealthEventTelemetryMessageBuilder::default()
                .payload(payload)
                .map_err(ErrorKind::from)?
                .topic_tokens(Self::get_base_service_topic_tokens(
                    device_name,
                    inbound_endpoint_name,
                ))
                .message_expiry(message_expiry)
                .build()
                .map_err(ErrorKind::from)?;
        self.stream_health_telemetry_sender
            .send(health_status_message)
            .await
            .map_err(|e| Error::from(ErrorKind::from(e)))
    }

    /// Reports Management Actions' runtime health statuses to the Azure Device Registry service.
    /// Note: Reporting multiple management action statuses in a single call has the same effect
    /// as reporting them individually, but reduced network calls. Duplicate management action names
    /// in the `runtime_healths` vector will result in the latest (by version and timestamp) entry being used
    ///
    /// # Arguments
    /// * `device_name` - The name of the device.
    /// * `inbound_endpoint_name` - The name of the inbound endpoint.
    /// * `asset_name` - The name of the asset.
    /// * `runtime_healths` - A vector of [`ManagementActionRuntimeHealthEvent`] containing all runtime health information for the management actions.
    /// * `message_expiry` - The duration for which the message will be attempted to be given to the service, it is rounded up to the nearest second.
    ///
    /// # Errors
    /// [`struct@Error`] of kind [`ValidationError`](ErrorKind::ValidationError)
    /// if `message_expiry` is > `u32::max` or if the asset or any management group or management
    /// action name is empty.
    ///
    /// [`struct@Error`] of kind [`AIOProtocolError`](ErrorKind::AIOProtocolError) if:
    /// - device or inbound endpoint names are invalid.
    /// - there are any underlying errors from the AIO Telemetry protocol.
    pub async fn report_management_action_runtime_health_events(
        &self,
        device_name: String,
        inbound_endpoint_name: String,
        asset_name: String,
        runtime_healths: Vec<ManagementActionRuntimeHealthEvent>,
        message_expiry: Duration,
    ) -> Result<(), Error> {
        if asset_name.trim().is_empty() {
            return Err(Error(ErrorKind::ValidationError(
                "asset_name must not be empty".to_string(),
            )));
        }
        // If there are no health events to report, this is a no-op
        if runtime_healths.is_empty() {
            return Ok(());
        }

        let management_action_health_events = runtime_healths
            .into_iter()
            .map(|runtime_health_event| {
                if runtime_health_event.management_group_name.trim().is_empty() {
                    return Err(Error(ErrorKind::ValidationError(
                        "management_group_name must not be empty".to_string(),
                    )));
                }
                if runtime_health_event
                    .management_action_name
                    .trim()
                    .is_empty()
                {
                    return Err(Error(ErrorKind::ValidationError(
                        "management_action_name must not be empty".to_string(),
                    )));
                }
                Ok(runtime_health_event.into())
            })
            .collect::<Result<Vec<_>, _>>()?;

        let payload = base_service_gen::ManagementActionRuntimeHealthEventTelemetry {
            management_action_runtime_health_event:
                base_service_gen::ManagementActionRuntimeHealthEventSchema {
                    asset_name,
                    management_actions: management_action_health_events,
                },
        };

        let health_status_message =
            base_service_gen::ManagementActionRuntimeHealthEventTelemetryMessageBuilder::default()
                .payload(payload)
                .map_err(ErrorKind::from)?
                .topic_tokens(Self::get_base_service_topic_tokens(
                    device_name,
                    inbound_endpoint_name,
                ))
                .message_expiry(message_expiry)
                .build()
                .map_err(ErrorKind::from)?;
        self.management_action_health_telemetry_sender
            .send(health_status_message)
            .await
            .map_err(|e| Error::from(ErrorKind::from(e)))
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
    /// [`struct@Error`] of kind [`ValidationError`](ErrorKind::ValidationError) if timeout is 0
    /// or > `u32::max` or if the asset name is empty.
    ///
    /// [`struct@Error`] of kind [`AIOProtocolError`](ErrorKind::AIOProtocolError) if:
    /// - device or inbound endpoint names are invalid.
    /// - there are any underlying errors from the AIO RPC protocol.
    ///
    /// [`struct@Error`] of kind [`ServiceError`](ErrorKind::ServiceError) if an error is returned
    /// by the Azure Device Registry service.
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

        let receiver_id = AssetRef {
            device_name: device_name.clone(),
            inbound_endpoint_name: inbound_endpoint_name.clone(),
            name: asset_name.clone(),
        };

        let rx = self
            .asset_update_notification_dispatcher
            .register_receiver(receiver_id.clone())
            .map_err(ErrorKind::from)?;

        let payload = base_client_gen::SetNotificationPreferenceForAssetUpdatesRequestPayload {
            notification_preference_request:
                base_client_gen::SetNotificationPreferenceForAssetUpdatesRequestSchema {
                    asset_name,
                    notification_preference: base_client_gen::NotificationPreference::On,
                },
        };

        let command_request =
            base_client_gen::SetNotificationPreferenceForAssetUpdatesRequestBuilder::default()
                .payload(payload)
                .map_err(ErrorKind::from)?
                .topic_tokens(Self::get_base_service_topic_tokens(
                    device_name,
                    inbound_endpoint_name,
                ))
                .timeout(timeout)
                .build()
                .map_err(ErrorKind::from)?;

        let result = self
            .notify_on_asset_update_command_invoker
            .invoke(command_request)
            .await;

        match result {
            Ok(Ok(_)) => Ok(AssetUpdateObservation(rx)),
            error_branches => {
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
                match error_branches {
                    Ok(Err(e)) => Err(Error(ErrorKind::from(e))),
                    Err(e) => Err(Error(ErrorKind::from(e))),
                    Ok(Ok(_)) => unreachable!(), // This case should not happen, as we already checked this in the first match branch
                }
            }
        }
    }

    /// Stops observation of an [`Asset`]'s updates from the Azure Device Registry service.
    /// Once this request has been sent, no more notifications will be received, regardless
    /// of whether the service successfully processes it or not. However, if the request
    /// is not sent due to input validation errors, the observation will continue because
    /// the request could not be sent.
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
    /// [`struct@Error`] of kind [`ValidationError`](ErrorKind::ValidationError) if timeout is 0
    /// or > `u32::max` or if the asset name is empty.
    ///
    /// [`struct@Error`] of kind [`AIOProtocolError`](ErrorKind::AIOProtocolError) if:
    /// - device or inbound endpoint names are invalid.
    /// - there are any underlying errors from the AIO RPC protocol.
    ///
    /// [`struct@Error`] of kind [`ServiceError`](ErrorKind::ServiceError) if an error is returned
    /// by the Azure Device Registry service.
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

        let unobserve_payload =
            base_client_gen::SetNotificationPreferenceForAssetUpdatesRequestPayload {
                notification_preference_request:
                    base_client_gen::SetNotificationPreferenceForAssetUpdatesRequestSchema {
                        asset_name: asset_name.clone(),
                        notification_preference: base_client_gen::NotificationPreference::Off,
                    },
            };

        let unobserve_request =
            base_client_gen::SetNotificationPreferenceForAssetUpdatesRequestBuilder::default()
                .payload(unobserve_payload)
                .map_err(ErrorKind::from)?
                .topic_tokens(Self::get_base_service_topic_tokens(
                    device_name.clone(),
                    inbound_endpoint_name.clone(),
                ))
                .timeout(timeout)
                .build()
                .map_err(ErrorKind::from)?;

        let result = self
            .notify_on_asset_update_command_invoker
            .invoke(unobserve_request)
            .await;

        // Remove this asset from our dispatcher whether or not the unobserve was successful to clear this from the dispatcher so that a new observation can be created later
        let receiver_id = AssetRef {
            device_name,
            inbound_endpoint_name,
            name: asset_name,
        };
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
        // still return any errors from executing the request after removing from dispatcher
        result.map_err(ErrorKind::from)?.map_err(ErrorKind::from)?;
        Ok(())
    }

    /// Creates or updates a discovered asset in the Azure Device Registry service.
    ///
    /// If the specified discovered asset does not yet exist, it will be created.
    /// If it already exists, it will be replaced.
    ///
    /// # Arguments
    /// * `device_name` - The name of the device.
    /// * `inbound_endpoint_name` - The name of the inbound endpoint.
    /// * `asset_name` - The name of the discovered asset.
    /// * `asset` - The specification of the discovered asset.
    /// * `timeout` - The duration until the client stops waiting for a response to the request, it is rounded up to the nearest second.
    ///
    /// Returns a tuple containing the discovery ID and version of the discovered asset.
    ///
    /// # Errors
    /// [`struct@Error`] of kind [`ValidationError`](ErrorKind::ValidationError) if timeout is 0
    /// or > `u32::max` or if the asset name is empty.
    ///
    /// [`struct@Error`] of kind [`AIOProtocolError`](ErrorKind::AIOProtocolError) if:
    /// - device or inbound endpoint names are invalid.
    /// - there are any underlying errors from the AIO RPC protocol.
    ///
    /// [`struct@Error`] of kind [`ServiceError`](ErrorKind::ServiceError) if an error is returned
    /// by the Azure Device Registry service.
    pub async fn create_or_update_discovered_asset(
        &self,
        device_name: String,
        inbound_endpoint_name: String,
        asset_name: String,
        asset: DiscoveredAsset,
        timeout: Duration,
    ) -> Result<(String, u64), Error> {
        // TODO: do we need to take device_name at all as an argument? It's in the DeviceRef in the DiscoveredAsset
        if asset_name.trim().is_empty() {
            return Err(Error(ErrorKind::ValidationError(
                "asset_name must not be empty".to_string(),
            )));
        }

        let payload = base_client_gen::CreateOrUpdateDiscoveredAssetRequestPayload {
            discovered_asset_request: base_client_gen::CreateOrUpdateDiscoveredAssetRequestSchema {
                discovered_asset: asset.into(),
                discovered_asset_name: asset_name,
            },
        };
        let request = base_client_gen::CreateOrUpdateDiscoveredAssetRequestBuilder::default()
            .payload(payload)
            .map_err(ErrorKind::from)?
            .topic_tokens(Self::get_base_service_topic_tokens(
                device_name,
                inbound_endpoint_name,
            ))
            .timeout(timeout)
            .build()
            .map_err(ErrorKind::from)?;
        let response = self
            .create_or_update_discovered_asset_command_invoker
            .invoke(request)
            .await
            .map_err(ErrorKind::from)?
            .map_err(ErrorKind::from)?;

        let discovery_id = response.payload.discovered_asset_response.discovery_id;
        let version = response.payload.discovered_asset_response.version;
        Ok((discovery_id, version))
    }

    /// Creates a new background health reporter for a dataset.
    ///
    /// Spawns a background task that handles deduplication and periodic re-reporting
    /// of health status for the specified dataset within an asset.
    ///
    /// # Arguments
    /// * `asset_ref` - Reference to the asset containing the dataset.
    /// * `dataset_name` - The name of the dataset.
    /// * `message_expiry` - The duration for which the message will be attempted to be given to the service, it is rounded up to the nearest second.
    /// * `report_interval` - Interval for re-reporting steady-state health when no changes occur.
    /// * `cancellation_token` - Token to signal cancellation of the background task. Should be triggered on dataset deletion.
    ///
    /// Returns a [`HealthReporterSender`](health_reporter::HealthReporterSender) that can be used to send health events.
    #[must_use]
    pub fn new_dataset_health_reporter(
        &self,
        asset_ref: AssetRef,
        dataset_name: String,
        message_expiry: Duration,
        report_interval: health_reporter::ReportInterval,
        cancellation_token: tokio_util::sync::CancellationToken,
    ) -> health_reporter::HealthReporterSender {
        let reporter = health_reporter::DatasetHealthReporter {
            client: self.clone(),
            asset_ref,
            dataset_name,
            message_expiry,
        };
        health_reporter::new_health_reporter(reporter, report_interval, cancellation_token)
    }

    /// Creates a new background health reporter for an event.
    ///
    /// Spawns a background task that handles deduplication and periodic re-reporting
    /// of health status for the specified event within an asset.
    ///
    /// # Arguments
    /// * `asset_ref` - Reference to the asset containing the event.
    /// * `event_group_name` - The name of the event group.
    /// * `event_name` - The name of the event.
    /// * `message_expiry` - The duration for which the message will be attempted to be given to the service, it is rounded up to the nearest second.
    /// * `report_interval` - Interval for re-reporting steady-state health when no changes occur.
    /// * `cancellation_token` - Token to signal cancellation of the background task. Should be triggered on event deletion.
    ///
    /// Returns a [`HealthReporterSender`](health_reporter::HealthReporterSender) that can be used to send health events.
    #[must_use]
    pub fn new_event_health_reporter(
        &self,
        asset_ref: AssetRef,
        event_group_name: String,
        event_name: String,
        message_expiry: Duration,
        report_interval: health_reporter::ReportInterval,
        cancellation_token: tokio_util::sync::CancellationToken,
    ) -> health_reporter::HealthReporterSender {
        let reporter = health_reporter::EventHealthReporter {
            client: self.clone(),
            asset_ref,
            event_group_name,
            event_name,
            message_expiry,
        };
        health_reporter::new_health_reporter(reporter, report_interval, cancellation_token)
    }

    /// Creates a new background health reporter for a stream.
    ///
    /// Spawns a background task that handles deduplication and periodic re-reporting
    /// of health status for the specified stream within an asset.
    ///
    /// # Arguments
    /// * `asset_ref` - Reference to the asset containing the stream.
    /// * `stream_name` - The name of the stream.
    /// * `message_expiry` - The duration for which the message will be attempted to be given to the service, it is rounded up to the nearest second.
    /// * `report_interval` - Interval for re-reporting steady-state health when no changes occur.
    /// * `cancellation_token` - Token to signal cancellation of the background task. Should be triggered on stream deletion.
    ///
    /// Returns a [`HealthReporterSender`](health_reporter::HealthReporterSender) that can be used to send health events.
    #[must_use]
    pub fn new_stream_health_reporter(
        &self,
        asset_ref: AssetRef,
        stream_name: String,
        message_expiry: Duration,
        report_interval: health_reporter::ReportInterval,
        cancellation_token: tokio_util::sync::CancellationToken,
    ) -> health_reporter::HealthReporterSender {
        let reporter = health_reporter::StreamHealthReporter {
            client: self.clone(),
            asset_ref,
            stream_name,
            message_expiry,
        };
        health_reporter::new_health_reporter(reporter, report_interval, cancellation_token)
    }

    /// Creates a new background health reporter for a management action.
    ///
    /// Spawns a background task that handles deduplication and periodic re-reporting
    /// of health status for the specified management action within an asset.
    ///
    /// # Arguments
    /// * `asset_ref` - Reference to the asset containing the management action.
    /// * `management_group_name` - The name of the management group.
    /// * `management_action_name` - The name of the management action.
    /// * `message_expiry` - The duration for which the message will be attempted to be given to the service, it is rounded up to the nearest second.
    /// * `report_interval` - Interval for re-reporting steady-state health when no changes occur.
    /// * `cancellation_token` - Token to signal cancellation of the background task. Should be triggered on management action deletion.
    ///
    /// Returns a [`HealthReporterSender`](health_reporter::HealthReporterSender) that can be used to send health events.
    #[must_use]
    pub fn new_management_action_health_reporter(
        &self,
        asset_ref: AssetRef,
        management_group_name: String,
        management_action_name: String,
        message_expiry: Duration,
        report_interval: health_reporter::ReportInterval,
        cancellation_token: tokio_util::sync::CancellationToken,
    ) -> health_reporter::HealthReporterSender {
        let reporter = health_reporter::ManagementActionHealthReporter {
            client: self.clone(),
            asset_ref,
            management_group_name,
            management_action_name,
            message_expiry,
        };
        health_reporter::new_health_reporter(reporter, report_interval, cancellation_token)
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use azure_iot_operations_mqtt::aio::connection_settings::MqttConnectionSettingsBuilder;
    use azure_iot_operations_mqtt::session::{Session, SessionOptionsBuilder};
    use azure_iot_operations_protocol::application::ApplicationContextBuilder;
    use azure_iot_operations_protocol::common::aio_protocol_error::AIOProtocolErrorKind;
    use test_case::test_case;

    const DEVICE_NAME: &str = "test-device";
    const INBOUND_ENDPOINT_NAME: &str = "test-endpoint";
    const INBOUNT_ENDPOINT_TYPE: &str = "test-endpoint-type";
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

    fn create_adr_client() -> Client {
        let session = create_session();
        let managed_client = session.create_managed_client();

        super::Client::new(
            ApplicationContextBuilder::default().build().unwrap(),
            managed_client,
            super::ClientOptionsBuilder::default().build().unwrap(),
        )
        .unwrap()
    }

    fn create_dummy_discovered_device() -> DiscoveredDevice {
        DiscoveredDevice {
            attributes: HashMap::default(),
            endpoints: None,
            external_device_id: None,
            manufacturer: None,
            model: None,
            operating_system: None,
            operating_system_version: None,
        }
    }

    fn create_dummy_discovered_asset() -> DiscoveredAsset {
        let device_ref = DeviceRef {
            device_name: DEVICE_NAME.to_string(),
            endpoint_name: INBOUND_ENDPOINT_NAME.to_string(),
        };
        DiscoveredAsset {
            asset_type_refs: vec![],
            attributes: HashMap::default(),
            datasets: vec![],
            default_datasets_configuration: None,
            default_datasets_destinations: vec![],
            default_events_configuration: None,
            default_events_destinations: vec![],
            default_management_groups_configuration: None,
            default_streams_configuration: None,
            default_streams_destinations: vec![],
            description: None,
            device_ref,
            display_name: None,
            documentation_uri: None,
            event_groups: vec![],
            external_asset_id: None,
            hardware_revision: None,
            management_groups: vec![],
            manufacturer: None,
            manufacturer_uri: None,
            model: None,
            product_code: None,
            serial_number: None,
            software_revision: None,
            streams: vec![],
        }
    }

    #[test]
    fn invalid_id_new_client() {
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
        let adr_client: Client = create_adr_client();
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
            Error(ErrorKind::ValidationError(_))
        ));
    }

    #[tokio::test]
    async fn test_get_asset_status_empty_asset_name() {
        let adr_client = create_adr_client();
        let result = adr_client
            .get_asset_status(
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
    async fn test_get_asset_status_invalid_topic_tokens(device_name: &str, endpoint_name: &str) {
        let adr_client: Client = create_adr_client();
        let result = adr_client
            .get_asset_status(
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
    async fn test_get_asset_status_zero_timeout() {
        let adr_client = create_adr_client();
        let result = adr_client
            .get_asset_status(
                DEVICE_NAME.to_string(),
                INBOUND_ENDPOINT_NAME.to_string(),
                ASSET_NAME.to_string(),
                Duration::from_secs(0),
            )
            .await;
        assert!(matches!(
            result.unwrap_err(),
            Error(ErrorKind::ValidationError(_))
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
            ErrorKind::ValidationError(_)
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
            ErrorKind::ValidationError(_)
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
            ErrorKind::ValidationError(_)
        ));
    }

    #[tokio::test]
    async fn test_create_or_update_discovered_asset_empty_asset_name() {
        let adr_client = create_adr_client();
        let result = adr_client
            .create_or_update_discovered_asset(
                DEVICE_NAME.to_string(),
                INBOUND_ENDPOINT_NAME.to_string(),
                String::new(),
                create_dummy_discovered_asset(),
                DURATION,
            )
            .await;

        assert!(matches!(
            result.unwrap_err().0,
            ErrorKind::ValidationError(_)
        ));
    }

    #[test_case("", INBOUND_ENDPOINT_NAME)]
    #[test_case(DEVICE_NAME, "")]
    #[tokio::test]
    async fn test_create_or_update_discovered_asset_invalid_topic_tokens(
        device_name: &str,
        endpoint_name: &str,
    ) {
        let adr_client = create_adr_client();
        let result = adr_client
            .create_or_update_discovered_asset(
                device_name.to_string(),
                endpoint_name.to_string(),
                ASSET_NAME.to_string(),
                create_dummy_discovered_asset(),
                DURATION,
            )
            .await;

        assert!(matches!(
            result.unwrap_err().0,
            ErrorKind::AIOProtocolError(ref e) if matches!(e.kind, AIOProtocolErrorKind::ConfigurationInvalid)
        ));
    }

    #[tokio::test]
    async fn test_create_or_update_discovered_asset_zero_timeout() {
        let adr_client = create_adr_client();
        let result = adr_client
            .create_or_update_discovered_asset(
                DEVICE_NAME.to_string(),
                INBOUND_ENDPOINT_NAME.to_string(),
                ASSET_NAME.to_string(),
                create_dummy_discovered_asset(),
                Duration::from_secs(0),
            )
            .await;

        assert!(matches!(
            result.unwrap_err().kind(),
            ErrorKind::ValidationError(_)
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
            ErrorKind::ValidationError(_)
        ));
    }

    #[test_case("", INBOUND_ENDPOINT_NAME)]
    #[test_case(DEVICE_NAME, "")]
    #[tokio::test]
    async fn test_get_device_status_invalid_topic_tokens(device_name: &str, endpoint_name: &str) {
        let adr_client = create_adr_client();
        let result = adr_client
            .get_device_status(device_name.to_string(), endpoint_name.to_string(), DURATION)
            .await;

        assert!(matches!(
            result.unwrap_err().0,
            ErrorKind::AIOProtocolError(ref e) if matches!(e.kind, AIOProtocolErrorKind::ConfigurationInvalid)
        ));
    }

    #[tokio::test]
    async fn test_get_device_status_zero_timeout() {
        let adr_client = create_adr_client();
        let result = adr_client
            .get_device_status(
                DEVICE_NAME.to_string(),
                INBOUND_ENDPOINT_NAME.to_string(),
                Duration::from_secs(0),
            )
            .await;

        assert!(matches!(
            result.unwrap_err().kind(),
            ErrorKind::ValidationError(_)
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
            ErrorKind::ValidationError(_)
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
            ErrorKind::ValidationError(_)
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
            ErrorKind::ValidationError(_)
        ));
    }

    #[test_case(""; "empty endpoint type")]
    #[tokio::test]
    async fn test_create_or_update_discovered_device_invalid_topic_tokens(endpoint_type: &str) {
        let adr_client = create_adr_client();
        let result = adr_client
            .create_or_update_discovered_device(
                DEVICE_NAME.to_string(),
                create_dummy_discovered_device(),
                endpoint_type.to_string(),
                DURATION,
            )
            .await;

        assert!(matches!(
            result.unwrap_err().0,
            ErrorKind::AIOProtocolError(ref e) if matches!(e.kind, AIOProtocolErrorKind::ConfigurationInvalid)
        ));
    }

    #[tokio::test]
    async fn test_create_or_update_discovered_device_empty_device_name() {
        let adr_client = create_adr_client();
        let result = adr_client
            .create_or_update_discovered_device(
                String::new(),
                create_dummy_discovered_device(),
                INBOUNT_ENDPOINT_TYPE.to_string(),
                DURATION,
            )
            .await;

        assert!(matches!(
            result.unwrap_err().0,
            ErrorKind::ValidationError(_)
        ));
    }

    #[tokio::test]
    async fn test_create_or_update_discovered_device_zero_timeout() {
        let adr_client = create_adr_client();
        let result = adr_client
            .create_or_update_discovered_device(
                DEVICE_NAME.to_string(),
                create_dummy_discovered_device(),
                INBOUNT_ENDPOINT_TYPE.to_string(),
                Duration::from_secs(0),
            )
            .await;
        assert!(matches!(
            result.unwrap_err().kind(),
            ErrorKind::ValidationError(_)
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
    fn test_get_base_service_topic_tokens() {
        let device_name = "test-device".to_string();
        let inbound_endpoint_name = "test-endpoint".to_string();

        let topic_tokens = Client::get_base_service_topic_tokens(
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
    fn test_get_discovery_service_topic_tokens() {
        let inbound_endpoint_type = "test-endpoint-type".to_string();
        let topic_tokens =
            Client::get_discovery_service_topic_tokens(inbound_endpoint_type.clone());

        assert_eq!(topic_tokens.len(), 1);
        assert_eq!(
            topic_tokens.get(INBOUND_ENDPOINT_TYPE_TOPIC_TOKEN),
            Some(&inbound_endpoint_type)
        );
    }
}
