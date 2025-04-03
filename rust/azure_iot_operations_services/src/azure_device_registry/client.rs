// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

//! Client for Azure Device Registry operations.
//!
//! To use this client, the `azure_device_registry` feature must be enabled.

use std::collections::HashMap;
use std::sync::Arc;
use std::time::Duration;

use azure_iot_operations_mqtt::interface::AckToken;
use azure_iot_operations_mqtt::interface::ManagedClient;
use azure_iot_operations_protocol::application::ApplicationContext;
use azure_iot_operations_protocol::rpc_command;
use tokio::sync::mpsc::UnboundedReceiver;
use tokio::{sync::Notify, task};

use crate::azure_device_registry::adr_name_gen::adr_base_service::client::{
    Asset, AssetEndpointProfile, AssetEndpointProfileUpdateEventTelemetry,
    AssetEndpointProfileUpdateEventTelemetryReceiver, AssetUpdateEventTelemetry,
    AssetUpdateEventTelemetryReceiver, CreateDetectedAssetCommandInvoker,
    CreateDetectedAssetRequestPayload, DetectedAssetResponseStatusSchema, GetAssetCommandInvoker,
    GetAssetEndpointProfileCommandInvoker, GetAssetRequestPayload, NotificationMessageType,
    NotificationResponse, NotifyOnAssetEndpointProfileUpdateCommandInvoker,
    NotifyOnAssetEndpointProfileUpdateRequestPayload, NotifyOnAssetUpdateCommandInvoker,
    NotifyOnAssetUpdateRequestPayload, NotifyOnAssetUpdateRequestSchema,
    UpdateAssetEndpointProfileStatusCommandInvoker, UpdateAssetStatusCommandInvoker,
    UpdateAssetStatusRequestPayload, UpdateAssetStatusRequestSchema,
};
use crate::azure_device_registry::adr_name_gen::common_types::common_options::{
    CommandOptionsBuilder, TelemetryOptionsBuilder,
};
use crate::azure_device_registry::adr_type_gen::aep_type_service::client::{
    CreateDiscoveredAssetEndpointProfileCommandInvoker,
    CreateDiscoveredAssetEndpointProfileRequestPayload,
    DiscoveredAssetEndpointProfileResponseStatusSchema,
};
use crate::azure_device_registry::adr_type_gen::common_types::common_options::CommandOptionsBuilder as AepCommandOptionsBuilder;
use crate::azure_device_registry::{
    AssetEndpointProfileStatus, AssetStatus, DetectedAsset, DiscoveredAssetEndpointProfile, Error,
    ErrorKind,
};
use crate::common::dispatcher::{DispatchError, Dispatcher};

/// Azure Device Registry client implementation.
#[derive(Clone)]
pub struct Client<C>
where
    C: ManagedClient + Clone + Send + Sync + 'static,
    C::PubReceiver: Send + Sync,
{
    get_asset_endpoint_profile_command_invoker: Arc<GetAssetEndpointProfileCommandInvoker<C>>,
    update_asset_endpoint_profile_status_command_invoker:
        Arc<UpdateAssetEndpointProfileStatusCommandInvoker<C>>,
    notify_on_asset_endpoint_profile_update_command_invoker:
        Arc<NotifyOnAssetEndpointProfileUpdateCommandInvoker<C>>,
    get_asset_command_invoker: Arc<GetAssetCommandInvoker<C>>,
    update_asset_status_command_invoker: Arc<UpdateAssetStatusCommandInvoker<C>>,
    notify_on_asset_update_command_invoker: Arc<NotifyOnAssetUpdateCommandInvoker<C>>,
    create_detected_asset_command_invoker: Arc<CreateDetectedAssetCommandInvoker<C>>,
    create_asset_endpoint_profile_command_invoker:
        Arc<CreateDiscoveredAssetEndpointProfileCommandInvoker<C>>,
    aep_update_event_telemetry_dispatcher:
        Arc<Dispatcher<(AssetEndpointProfileUpdateEventTelemetry, Option<AckToken>)>>,
    asset_update_event_telemetry_dispatcher:
        Arc<Dispatcher<(AssetUpdateEventTelemetry, Option<AckToken>)>>,
}

impl<C> Client<C>
where
    C: ManagedClient + Clone + Send + Sync + 'static,
    C::PubReceiver: Send + Sync,
{
    /// Create a new Azure Device Registry Client.
    ///
    /// # Panics
    /// Panics if the options for the underlying command invokers and telemetry receivers cannot be built. Not possible since
    /// the options are statically generated.
    pub fn new(
        application_context: ApplicationContext,
        client: &C,
        notification_auto_ack: bool,
    ) -> Self {
        let aep_name_command_options = CommandOptionsBuilder::default()
            .topic_token_map(HashMap::from([(
                "ex:connectorClientId".to_string(),
                client.client_id().to_string(),
            )]))
            .build()
            .expect("Statically generated options should not fail.");

        let aep_telemetry_options = TelemetryOptionsBuilder::default()
            .topic_token_map(HashMap::from([(
                "ex:connectorClientId".to_string(),
                client.client_id().to_string(),
            )]))
            .auto_ack(notification_auto_ack)
            .build()
            .expect("DTDL schema generated invalid arguments");

        // Create the shutdown notifier for the receiver loop
        let shutdown_notifier = Arc::new(Notify::new());

        // Create a hashmap of aeps being observed and channels to send their notifications to
        let aep_update_event_telemetry_dispatcher = Arc::new(Dispatcher::new());
        let asset_update_event_telemetry_dispatcher = Arc::new(Dispatcher::new());

        // Start the receive key notification loop
        task::spawn({
            let asset_endpoint_profile_update_event_telemetry_receiver =
                AssetEndpointProfileUpdateEventTelemetryReceiver::new(
                    application_context.clone(),
                    client.clone(),
                    &aep_telemetry_options,
                );
            let asset_update_event_telemetry_receiver = AssetUpdateEventTelemetryReceiver::new(
                application_context.clone(),
                client.clone(),
                &aep_telemetry_options,
            );
            let shutdown_notifier_clone = shutdown_notifier.clone();
            let aep_update_event_telemetry_dispatcher_clone =
                aep_update_event_telemetry_dispatcher.clone();
            let asset_update_event_telemetry_dispatcher_clone =
                asset_update_event_telemetry_dispatcher.clone();
            async move {
                Self::receive_update_event_telemetry_loop(
                    shutdown_notifier_clone,
                    asset_endpoint_profile_update_event_telemetry_receiver,
                    asset_update_event_telemetry_receiver,
                    aep_update_event_telemetry_dispatcher_clone,
                    asset_update_event_telemetry_dispatcher_clone,
                )
                .await;
            }
        });
        Self {
            get_asset_endpoint_profile_command_invoker: Arc::new(
                GetAssetEndpointProfileCommandInvoker::new(
                    application_context.clone(),
                    client.clone(),
                    &aep_name_command_options,
                ),
            ),
            update_asset_endpoint_profile_status_command_invoker: Arc::new(
                UpdateAssetEndpointProfileStatusCommandInvoker::new(
                    application_context.clone(),
                    client.clone(),
                    &aep_name_command_options,
                ),
            ),
            notify_on_asset_endpoint_profile_update_command_invoker: Arc::new(
                NotifyOnAssetEndpointProfileUpdateCommandInvoker::new(
                    application_context.clone(),
                    client.clone(),
                    &aep_name_command_options,
                ),
            ),
            get_asset_command_invoker: Arc::new(GetAssetCommandInvoker::new(
                application_context.clone(),
                client.clone(),
                &aep_name_command_options,
            )),
            update_asset_status_command_invoker: Arc::new(UpdateAssetStatusCommandInvoker::new(
                application_context.clone(),
                client.clone(),
                &aep_name_command_options,
            )),
            create_detected_asset_command_invoker: Arc::new(
                CreateDetectedAssetCommandInvoker::new(
                    application_context.clone(),
                    client.clone(),
                    &aep_name_command_options,
                ),
            ),
            notify_on_asset_update_command_invoker: Arc::new(
                NotifyOnAssetUpdateCommandInvoker::new(
                    application_context.clone(),
                    client.clone(),
                    &aep_name_command_options,
                ),
            ),
            create_asset_endpoint_profile_command_invoker: Arc::new(
                CreateDiscoveredAssetEndpointProfileCommandInvoker::new(
                    application_context,
                    client.clone(),
                    &AepCommandOptionsBuilder::default()
                        .topic_token_map(HashMap::from([(
                            "ex:connectorClientId".to_string(),
                            client.client_id().to_string(),
                        )]))
                        .build()
                        .expect("Statically generated options should not fail."),
                ),
            ),
            aep_update_event_telemetry_dispatcher,
            asset_update_event_telemetry_dispatcher,
        }
    }

    /// Retrieves an asset endpoint profile from a Azure Device Registry service.
    ///
    /// # Arguments
    /// * `aep_name` - The name of the asset endpoint profile.
    /// * `timeout` - The duration until the Client stops waiting for a response to the request, it is rounded up to the nearest second.
    ///
    /// Returns a [`AssetEndpointProfile`] if the the endpoint was found.
    ///
    /// # Errors
    /// [`struct@Error`] of kind [`InvalidArgument`](ErrorKind::InvalidArgument)
    /// if the `timeout` is zero or > `u32::max`, or there is an error building the request.
    ///
    /// [`struct@Error`] of kind [`ServiceError`](ErrorKind::ServiceError)
    /// if there is an error returned by the ADR Service.
    ///
    /// [`struct@Error`] of kind [`AIOProtocolError`](ErrorKind::AIOProtocolError)
    /// if there are any underlying errors from the AIO RPC protocol.
    pub async fn get_asset_endpoint_profile(
        &self,
        aep_name: String,
        timeout: Duration,
    ) -> Result<AssetEndpointProfile, Error> {
        let command_request = rpc_command::invoker::RequestBuilder::default()
            .topic_tokens(HashMap::from([("ex:aepName".to_string(), aep_name)]))
            .timeout(timeout)
            .build()
            .map_err(|e| Error(ErrorKind::InvalidArgument(e.to_string())))?;

        let result = self
            .get_asset_endpoint_profile_command_invoker
            .invoke(command_request)
            .await;

        match result {
            Ok(response) => Ok(response.payload.asset_endpoint_profile),
            Err(e) => Err(Error(ErrorKind::AIOProtocolError(e))),
        }
    }

    /// Updates an asset endpoint profile's status in the Azure Device Registry service.
    ///
    /// # Arguments
    /// * [`UpdateAssetEndpointProfileStatusReq`] - The request containing all the information about an aseet for the update.
    /// * `timeout` - The duration until the Client stops waiting for a response to the request, it is rounded up to the nearest second.
    ///
    /// Returns the updated [`AssetEndpointProfile`] once updated.
    ///
    /// # Errors
    /// [`struct@Error`] of kind [`InvalidArgument`](ErrorKind::InvalidArgument)
    /// if the `timeout` is zero or > `u32::max`, or there is an error building the request.
    ///
    /// [`struct@Error`] of kind [`SerializationError`](ErrorKind::SerializationError)
    /// if there is an error serializing the request.
    ///
    /// [`struct@Error`] of kind [`ServiceError`](ErrorKind::ServiceError)
    /// if there is an error returned by the ADR Service.
    ///
    /// [`struct@Error`] of kind [`AIOProtocolError`](ErrorKind::AIOProtocolError)
    /// if there are any underlying errors from the AIO RPC protocol.
    pub async fn update_asset_endprofile_status(
        &self,
        source: AssetEndpointProfileStatus,
        timeout: Duration,
    ) -> Result<AssetEndpointProfile, Error> {
        let command_request = rpc_command::invoker::RequestBuilder::default()
            .payload(source.into())
            .map_err(|e| Error(ErrorKind::SerializationError(e.to_string())))?
            .timeout(timeout)
            .build()
            .map_err(|e| Error(ErrorKind::InvalidArgument(e.to_string())))?;

        let result = self
            .update_asset_endpoint_profile_status_command_invoker
            .invoke(command_request)
            .await;

        match result {
            Ok(response) => Ok(response.payload.updated_asset_endpoint_profile),
            Err(e) => Err(Error(ErrorKind::AIOProtocolError(e))),
        }
    }

    /// Notifies the Azure Device Registry service that client is listening for asset updates.
    ///
    /// # Arguments
    /// * `notification_type` - The type of notification to send, true for "On" and false for "Off".
    /// * `timeout` - The duration until the Client stops waiting for a response to the request, it is rounded up to the nearest second.
    ///
    /// Returns the [`NotificationResponse`].
    ///
    /// # Errors
    /// [`struct@Error`] of kind [`InvalidArgument`](ErrorKind::InvalidArgument)
    /// if the `timeout` is zero or > `u32::max`, or there is an error building the request.
    ///
    /// [`struct@Error`] of kind [`SerializationError`](ErrorKind::SerializationError)
    /// if there is an error serializing the request.
    ///
    /// [`struct@Error`] of kind [`ServiceError`](ErrorKind::ServiceError)
    /// if there is an error returned by the ADR Service.
    ///
    /// [`struct@Error`] of kind [`AIOProtocolError`](ErrorKind::AIOProtocolError)
    /// if there are any underlying errors from the AIO RPC protocol.
    pub async fn notify_asset_endpoint_profile_update(
        &self,
        aep_name: String,
        notification_type: bool,
        timeout: Duration,
    ) -> Result<
        UnboundedReceiver<(AssetEndpointProfileUpdateEventTelemetry, Option<AckToken>)>,
        Error,
    > //Result<AssetEndpointProfileObservation, Error>
    {
        let rx = self
            .aep_update_event_telemetry_dispatcher
            .register_receiver(aep_name.clone())
            .map_err(|_| Error(ErrorKind::DuplicateObserve))?;

        let payload = NotifyOnAssetEndpointProfileUpdateRequestPayload {
            notification_request: if notification_type {
                NotificationMessageType::On
            } else {
                NotificationMessageType::Off
            },
        };

        let command_request = rpc_command::invoker::RequestBuilder::default()
            .topic_tokens(HashMap::from([(
                "ex:aepName".to_string(),
                aep_name.clone(),
            )]))
            .payload(payload)
            .map_err(|e| Error(ErrorKind::SerializationError(e.to_string())))?
            .timeout(timeout)
            .build()
            .map_err(|e| Error(ErrorKind::InvalidArgument(e.to_string())))?;

        let result: Result<rpc_command::invoker::Response<super::adr_name_gen::adr_base_service::client::NotifyOnAssetEndpointProfileUpdateResponsePayload>, azure_iot_operations_protocol::common::aio_protocol_error::AIOProtocolError> = self
            .notify_on_asset_endpoint_profile_update_command_invoker
            .invoke(command_request)
            .await;

        match result {
            Ok(response) => {
                if let NotificationResponse::Accepted = response.payload.notification_response {
                    Ok(rx)
                } else {
                    // TODO Check error kind - another kind needs to be incldued ?
                    Err(Error(ErrorKind::InvalidArgument(
                        ("Notification Response Failed").to_string(),
                    )))
                }
            }
            Err(e) => {
                // If the observe request wasn't successful, remove it from our dispatcher
                if self
                    .aep_update_event_telemetry_dispatcher
                    .unregister_receiver(&aep_name)
                {
                    log::debug!("Aep removed from observed list: {aep_name:?}");
                } else {
                    log::debug!("Aep not in observed list: {aep_name:?}");
                }
                Err(Error(ErrorKind::AIOProtocolError(e)))
            }
        }
    }

    // pub async observe_asset_endpoint_profile_update(
    //     &self,
    //     aep_name
    //     timeout: Duration,
    // ) -> Result<String, AzureDeviceRegistryError> {
    //     // pass
    //     // TODO
    // }
    /// Retrieves an asset from a Azure Device Registry service.
    ///
    /// # Arguments
    /// * `asset_name` - The name of the asset.
    /// * `timeout` - The duration until the Client stops waiting for a response to the request, it is rounded up to the nearest second.
    ///
    /// Returns a [`Asset`] if the the asset was found.
    ///
    /// # Errors
    /// [`struct@Error`] of kind [`InvalidArgument`](ErrorKind::InvalidArgument)
    /// if the `timeout` is zero or > `u32::max`, or there is an error building the request.
    ///
    /// [`struct@Error`] of kind [`SerializationError`](ErrorKind::SerializationError)
    /// if there is an error serializing the request.
    ///
    /// [`struct@Error`] of kind [`ServiceError`](ErrorKind::ServiceError)
    /// if there is an error returned by the ADR Service.
    ///
    /// [`struct@Error`] of kind [`AIOProtocolError`](ErrorKind::AIOProtocolError)
    /// if there are any underlying errors from the AIO RPC protocol.
    pub async fn get_asset(&self, asset_name: String, timeout: Duration) -> Result<Asset, Error> {
        let get_request_payload = GetAssetRequestPayload { asset_name };

        let command_request = rpc_command::invoker::RequestBuilder::default()
            .payload(get_request_payload)
            .map_err(|e| Error(ErrorKind::SerializationError(e.to_string())))?
            .timeout(timeout)
            .build()
            .map_err(|e| Error(ErrorKind::InvalidArgument(e.to_string())))?;

        let result = self.get_asset_command_invoker.invoke(command_request).await;

        match result {
            Ok(response) => Ok(response.payload.asset),
            Err(e) => Err(Error(ErrorKind::AIOProtocolError(e))),
        }
    }

    /// Updates an asset in the Azure Device Registry service.
    ///
    /// # Arguments
    /// # `name` - The name of the asset.
    /// * [`AssetStatusReq`] - The status of an asset for the update.
    /// * `timeout` - The duration until the Client stops waiting for a response to the request, it is rounded up to the nearest second.
    ///
    /// Returns the updated [`Asset`] once updated.
    ///
    /// # Errors
    /// [`struct@Error`] of kind [`InvalidArgument`](ErrorKind::InvalidArgument)
    /// if the `timeout` is zero or > `u32::max`, or there is an error building the request.
    ///
    /// [`struct@Error`] of kind [`SerializationError`](ErrorKind::SerializationError)
    /// if there is an error serializing the request.
    ///
    /// [`struct@Error`] of kind [`ServiceError`](ErrorKind::ServiceError)
    /// if there is an error returned by the ADR Service.
    ///
    /// [`struct@Error`] of kind [`AIOProtocolError`](ErrorKind::AIOProtocolError)
    /// if there are any underlying errors from the AIO RPC protocol.
    pub async fn update_asset_status(
        &self,
        name: String,
        status: AssetStatus,
        timeout: Duration,
    ) -> Result<Asset, Error> {
        let payload = UpdateAssetStatusRequestPayload {
            asset_status_update: UpdateAssetStatusRequestSchema {
                asset_name: name,
                asset_status: status.into(),
            },
        };

        let command_request = rpc_command::invoker::RequestBuilder::default()
            .payload(payload)
            .map_err(|e| Error(ErrorKind::SerializationError(e.to_string())))?
            .timeout(timeout)
            .build()
            .map_err(|e| Error(ErrorKind::InvalidArgument(e.to_string())))?;

        let result = self
            .update_asset_status_command_invoker
            .invoke(command_request)
            .await;

        match result {
            Ok(response) => Ok(response.payload.updated_asset),
            Err(e) => Err(Error(ErrorKind::AIOProtocolError(e))),
        }
    }

    /// Creates an asset inside the Azure Device Registry service.
    ///
    /// # Arguments
    /// * [`DetectedAsset`] - All relevant details needed for an asset creation
    /// * `timeout` - The duration until the Client stops waiting for a response to the request, it is rounded up to the nearest second.
    ///
    /// Returns a [`DetectedAssetResponseStatusSchema`] depending on the status of the asset creation.
    ///
    /// # Errors
    /// [`struct@Error`] of kind [`InvalidArgument`](ErrorKind::InvalidArgument)
    /// if the `timeout` is zero or > `u32::max`, or there is an error building the request.
    ///
    /// [`struct@Error`] of kind [`SerializationError`](ErrorKind::SerializationError)
    /// if there is an error serializing the request.
    ///
    /// [`struct@Error`] of kind [`ServiceError`](ErrorKind::ServiceError)
    /// if there is an error returned by the ADR Service.
    ///
    /// [`struct@Error`] of kind [`AIOProtocolError`](ErrorKind::AIOProtocolError)
    /// if there are any underlying errors from the AIO RPC protocol.
    pub async fn create_detected_asset(
        &self,
        asset: DetectedAsset,
        timeout: Duration,
    ) -> Result<DetectedAssetResponseStatusSchema, Error> {
        let payload = CreateDetectedAssetRequestPayload {
            detected_asset: asset.into(),
        };
        let command_request = rpc_command::invoker::RequestBuilder::default()
            .payload(payload)
            .map_err(|e| Error(ErrorKind::SerializationError(e.to_string())))?
            .timeout(timeout)
            .build()
            .map_err(|e| Error(ErrorKind::InvalidArgument(e.to_string())))?;

        let result = self
            .create_detected_asset_command_invoker
            .invoke(command_request)
            .await;

        match result {
            Ok(response) => Ok(response.payload.create_detected_asset_response.status),
            Err(e) => Err(Error(ErrorKind::AIOProtocolError(e))),
        }
    }

    /// Notifies the Azure Device Registry service that client is listening for asset updates.
    ///
    /// # Arguments
    /// * `asset_name` - The name of the asset.
    /// * `notification_type` - The type of notification to send, true for "On" and false for "Off".
    /// * `timeout` - The duration until the Client stops waiting for a response to the request, it is rounded up to the nearest second.
    ///
    /// Returns the updated [`NotificationResponse`].
    ///
    /// # Errors
    /// [`struct@Error`] of kind [`InvalidArgument`](ErrorKind::InvalidArgument)
    /// if the `timeout` is zero or > `u32::max`, or there is an error building the request.
    ///
    /// [`struct@Error`] of kind [`SerializationError`](ErrorKind::SerializationError)
    /// if there is an error serializing the request.
    ///
    /// [`struct@Error`] of kind [`ServiceError`](ErrorKind::ServiceError)
    /// if there is an error returned by the ADR Service.
    ///
    /// [`struct@Error`] of kind [`AIOProtocolError`](ErrorKind::AIOProtocolError)
    /// if there are any underlying errors from the AIO RPC protocol.
    pub async fn notify_asset_update(
        &self,
        asset_name: String,
        notification_type: bool,
        timeout: Duration,
    ) -> Result<UnboundedReceiver<(AssetUpdateEventTelemetry, Option<AckToken>)>, Error>
//Result<NotificationResponse, Error>
    {
        let rx = self
            .asset_update_event_telemetry_dispatcher
            .register_receiver(asset_name.clone())
            .map_err(|_| Error(ErrorKind::DuplicateObserve))?;

        let notification_payload = NotifyOnAssetUpdateRequestPayload {
            notification_request: NotifyOnAssetUpdateRequestSchema {
                asset_name: asset_name.clone(),
                notification_message_type: if notification_type {
                    NotificationMessageType::On
                } else {
                    NotificationMessageType::Off
                },
            },
        };

        let command_request = rpc_command::invoker::RequestBuilder::default()
            .payload(notification_payload)
            .map_err(|e| Error(ErrorKind::SerializationError(e.to_string())))?
            .timeout(timeout)
            .build()
            .map_err(|e| Error(ErrorKind::InvalidArgument(e.to_string())))?;

        let result = self
            .notify_on_asset_update_command_invoker
            .invoke(command_request)
            .await;

        match result {
            Ok(response) => {
                if let NotificationResponse::Accepted = response.payload.notification_response {
                    Ok(rx)
                } else {
                    // TODO Check error kind - another kind needs to be incldued ?
                    Err(Error(ErrorKind::InvalidArgument(
                        ("Notification Response Failed").to_string(),
                    )))
                }
            }
            Err(e) => {
                // If the observe request wasn't successful, remove it from our dispatcher
                if self
                    .asset_update_event_telemetry_dispatcher
                    .unregister_receiver(&asset_name)
                {
                    log::debug!("Asset removed from observed list: {asset_name:?}");
                } else {
                    log::debug!("Asset not in observed list: {asset_name:?}");
                }
                Err(Error(ErrorKind::AIOProtocolError(e)))
            }
        }
    }

    /// Creates an asset endpoint profile inside the Azure Device Registry service.
    ///
    /// # Arguments
    /// * [`DiscoveredAssetEndpointProfile`] - All relevant details needed for an asset endpoint profile creation.
    /// * `timeout` - The duration until the Client stops waiting for a response to the request, it is rounded up to the nearest second.
    ///
    /// Returns a [`DiscoveredAssetEndpointProfileResponseStatusSchema`] depending on the status of the asset endpoint profile creation.
    ///
    /// # Errors
    /// [`struct@Error`] of kind [`InvalidArgument`](ErrorKind::InvalidArgument)
    /// if the `timeout` is zero or > `u32::max`, or there is an error building the request.
    ///
    /// [`struct@Error`] of kind [`SerializationError`](ErrorKind::SerializationError)
    /// if there is an error serializing the request.
    ///
    /// [`struct@Error`] of kind [`ServiceError`](ErrorKind::ServiceError)
    /// if there is an error returned by the ADR Service.
    ///
    /// [`struct@Error`] of kind [`AIOProtocolError`](ErrorKind::AIOProtocolError)
    /// if there are any underlying errors from the AIO RPC protocol.
    pub async fn create_discovered_asset_endpoint_profile(
        &self,
        daep: DiscoveredAssetEndpointProfile,
        timeout: Duration,
    ) -> Result<DiscoveredAssetEndpointProfileResponseStatusSchema, Error> {
        let paylaod = CreateDiscoveredAssetEndpointProfileRequestPayload {
            discovered_asset_endpoint_profile: daep.into(),
        };
        let command_request = rpc_command::invoker::RequestBuilder::default()
            .payload(paylaod)
            .map_err(|e| Error(ErrorKind::SerializationError(e.to_string())))?
            .timeout(timeout)
            .build()
            .map_err(|e| Error(ErrorKind::InvalidArgument(e.to_string())))?;

        let result = self
            .create_asset_endpoint_profile_command_invoker
            .invoke(command_request)
            .await;

        match result {
            Ok(response) => Ok(response
                .payload
                .create_discovered_asset_endpoint_profile_response
                .status),
            Err(e) => Err(Error(ErrorKind::AIOProtocolError(e))),
        }
    }

    /// Shutdown the [`Client`]. Shuts down the underlying command invokers for get and put operations.
    ///
    /// Note: If this method is called, the [`Client`] should not be used again.
    /// If the method returns an error, it may be called again to re-attempt unsubscribing.
    ///
    /// Returns Ok(()) on success, otherwise returns [`struct@Error`].
    /// # Errors
    /// [`struct@Error`] of kind [`AIOProtocolError`](ErrorKind::AIOProtocolError)
    /// if the unsubscribe fails or if the unsuback reason code doesn't indicate success.
    pub async fn shutdown(&self) -> Result<(), Error> {
        self.get_asset_endpoint_profile_command_invoker
            .shutdown()
            .await
            .map_err(|e| Error(ErrorKind::AIOProtocolError(e)))?;
        self.update_asset_endpoint_profile_status_command_invoker
            .shutdown()
            .await
            .map_err(|e| Error(ErrorKind::AIOProtocolError(e)))?;
        self.notify_on_asset_endpoint_profile_update_command_invoker
            .shutdown()
            .await
            .map_err(|e| Error(ErrorKind::AIOProtocolError(e)))?;
        self.get_asset_command_invoker
            .shutdown()
            .await
            .map_err(|e| Error(ErrorKind::AIOProtocolError(e)))?;
        self.update_asset_status_command_invoker
            .shutdown()
            .await
            .map_err(|e| Error(ErrorKind::AIOProtocolError(e)))?;
        self.create_detected_asset_command_invoker
            .shutdown()
            .await
            .map_err(|e| Error(ErrorKind::AIOProtocolError(e)))?;
        self.notify_on_asset_update_command_invoker
            .shutdown()
            .await
            .map_err(|e| Error(ErrorKind::AIOProtocolError(e)))?;
        Ok(())
    }

    async fn receive_update_event_telemetry_loop(
        shutdown_notifier: Arc<Notify>,
        mut asset_endpoint_profile_update_event_telemetry_receiver: AssetEndpointProfileUpdateEventTelemetryReceiver<C>,
        mut asset_update_event_telemetry_receiver: AssetUpdateEventTelemetryReceiver<C>,
        aep_update_event_telemetry_dispatcher: Arc<
            Dispatcher<(AssetEndpointProfileUpdateEventTelemetry, Option<AckToken>)>,
        >,
        asset_update_event_telemetry_dispatcher: Arc<
            Dispatcher<(AssetUpdateEventTelemetry, Option<AckToken>)>,
        >,
    ) {
        let mut shutdown_attempt_count = 0;
        loop {
            tokio::select! {
                  // on shutdown/drop, we will be notified so that we can stop receiving any more messages
                  // The loop will continue to receive any more publishes that are already in the queue
                  () = shutdown_notifier.notified() => {
                    match asset_endpoint_profile_update_event_telemetry_receiver.shutdown().await {
                        Ok(()) => {
                            log::info!("AssetEndpointProfileUpdateEventTelemetryReceiver shutdown");
                        }
                        Err(e) => {
                            log::error!("Error shutting down AssetEndpointProfileUpdateEventTelemetryReceiver: {e}");
                            // try shutdown again, but not indefinitely
                            if shutdown_attempt_count < 3 {
                                shutdown_attempt_count += 1;
                                shutdown_notifier.notify_one();
                            }
                        }
                    }
                    match asset_update_event_telemetry_receiver.shutdown().await {
                        Ok(()) => {
                            log::info!("AssetUpdateEventTelemetryReceiver shutdown");
                        }
                        Err(e) => {
                            log::error!("Error shutting down AssetUpdateEventTelemetryReceiver: {e}");
                            // try shutdown again, but not indefinitely
                            if shutdown_attempt_count < 3 {
                                shutdown_attempt_count += 1;
                                shutdown_notifier.notify_one();
                            }
                        }
                    }
                  },
                  aep_msg = asset_endpoint_profile_update_event_telemetry_receiver.recv() => {
                    if let Some(m) = aep_msg {
                        match m {
                            Ok((aep_update_event_telemetry, ack_token)) => {
                                let Some(aep_name) = aep_update_event_telemetry.topic_tokens.get("ex:aepName") else {
                                    log::error!("AssetEndpointProfileUpdateEventTelemetry missing ex:aepName topic token.");
                                    continue;
                                };
                                // Try to send the notification to the associated receiver
                                let aep_update_event_telemetry_payload_clone = aep_update_event_telemetry.payload.clone();
                                match aep_update_event_telemetry_dispatcher.dispatch(aep_name, (aep_update_event_telemetry.payload, ack_token)) {
                                    Ok(()) => {
                                        log::debug!("AssetEndpointProfileUpdateEventTelemetry dispatched for aep name: {aep_name:?}");
                                    }
                                    Err(DispatchError::SendError(_)) => {
                                        log::warn!("AssetEndpointProfileUpdateEventTelemetryReceiver has been dropped. Received Telemetry: {aep_update_event_telemetry_payload_clone:?}",);
                                    }
                                    Err(DispatchError::NotFound(_)) => {
                                        log::warn!("AssetEndpointProfile is not being observed. Received AssetEndpointProfileUpdateEventTelemetry: {aep_update_event_telemetry_payload_clone:?}",);
                                    }
                                }
                            }
                            Err(e) => {
                                // This should only happen on errors subscribing, but it's likely not recoverable
                                log::error!("Error receiving AssetEndpointProfileUpdateEventTelemetry: {e}. Shutting down AssetEndpointProfileUpdateEventTelemetryReceiver.");
                                // try to shutdown telemetry receiver, but not indefinitely
                                if shutdown_attempt_count < 3 {
                                    shutdown_notifier.notify_one();
                                }
                            }
                        }
                    } else {
                        log::info!("AssetEndpointProfileUpdateEventTelemetryReceiver closed, no more AssetEndpointProfileUpdateEventTelemetry will be received");
                        // Unregister all receivers, closing the associated channels
                        aep_update_event_telemetry_dispatcher.unregister_all();
                        break;
                    }
                },
                asset_msg = asset_update_event_telemetry_receiver.recv() => {
                    if let Some(m) = asset_msg {
                        match m {
                            Ok((asset_update_event_telemetry, ack_token)) => {
                                let asset_name = asset_update_event_telemetry.payload.asset_update_event.asset_name.clone();
                                match asset_update_event_telemetry_dispatcher.dispatch(&asset_name, (asset_update_event_telemetry.payload, ack_token)) {
                                    Ok(()) => {
                                        log::debug!("AssetUpdateEventTelemetry dispatched for aep name: {asset_name:?}");
                                    }
                                    Err(DispatchError::SendError(e)) => {
                                        log::warn!("AssetUpdateEventTelemetryReceiver has been dropped. Received Telemetry: {e:?}",);
                                    }
                                    Err(DispatchError::NotFound(e)) => {
                                        log::warn!("Asset is not being observed. Received AssetUpdateEventTelemetry: {e:?}",);
                                    }
                                }
                            }
                            Err(e) => {
                                // This should only happen on errors subscribing, but it's likely not recoverable
                                log::error!("Error receiving AssetUpdateEventTelemetry: {e}. Shutting down AssetUpdateEventTelemetryReceiver.");
                                // try to shutdown telemetry receiver, but not indefinitely
                                if shutdown_attempt_count < 3 {
                                    shutdown_notifier.notify_one();
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
