// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

//! Client for Azure Device Registry operations.
//!
//! To use this client, the `azure_device_registry` feature must be enabled.

use derive_builder::Builder;
use std::collections::HashMap;
use std::sync::Arc;
use std::time::Duration;

use azure_iot_operations_mqtt::interface::AckToken;
use azure_iot_operations_mqtt::interface::ManagedClient;
use azure_iot_operations_protocol::application::ApplicationContext;
use azure_iot_operations_protocol::rpc_command;
use tokio::{sync::Notify, task};

use crate::azure_device_registry::adr_name_gen::adr_base_service::client as adr_base_service_gen;
use crate::azure_device_registry::adr_name_gen::common_types::common_options::{
    CommandOptionsBuilder, TelemetryOptionsBuilder,
};
use crate::azure_device_registry::adr_type_gen::aep_type_service::client as aep_type_service_gen;

use crate::azure_device_registry::adr_type_gen::common_types::common_options::CommandOptionsBuilder as AepCommandOptionsBuilder;
use crate::azure_device_registry::{
    Asset, AssetEndpointProfile, AssetEndpointProfileObservation, AssetEndpointProfileStatus,
    AssetObservation, AssetStatus, DetectedAsset, DiscoveredAssetEndpointProfile, Error, ErrorKind,
};
use crate::common::dispatcher::{DispatchError, Dispatcher};

use super::DetectedAssetResponseStatus;
use super::DiscoveredAssetEndpointProfileResponseStatus;

/// Options for the Azure Device Registry client.
#[derive(Builder, Clone)]
#[builder(setter(into))]
pub struct ClientOptions {
    /// If true, update notifications are auto-acknowledged
    #[builder(default = "true")]
    key_notification_auto_ack: bool,
}

/// Azure Device Registry client implementation.
#[derive(Clone)]
pub struct Client<C>
where
    C: ManagedClient + Clone + Send + Sync + 'static,
    C::PubReceiver: Send + Sync,
{
    get_asset_endpoint_profile_command_invoker:
        Arc<adr_base_service_gen::GetAssetEndpointProfileCommandInvoker<C>>,
    update_asset_endpoint_profile_status_command_invoker:
        Arc<adr_base_service_gen::UpdateAssetEndpointProfileStatusCommandInvoker<C>>,
    notify_on_asset_endpoint_profile_update_command_invoker:
        Arc<adr_base_service_gen::NotifyOnAssetEndpointProfileUpdateCommandInvoker<C>>,
    get_asset_command_invoker: Arc<adr_base_service_gen::GetAssetCommandInvoker<C>>,
    update_asset_status_command_invoker:
        Arc<adr_base_service_gen::UpdateAssetStatusCommandInvoker<C>>,
    notify_on_asset_update_command_invoker:
        Arc<adr_base_service_gen::NotifyOnAssetUpdateCommandInvoker<C>>,
    create_detected_asset_command_invoker:
        Arc<adr_base_service_gen::CreateDetectedAssetCommandInvoker<C>>,
    create_asset_endpoint_profile_command_invoker:
        Arc<aep_type_service_gen::CreateDiscoveredAssetEndpointProfileCommandInvoker<C>>,
    aep_update_event_telemetry_dispatcher:
        Arc<Dispatcher<(AssetEndpointProfile, Option<AckToken>)>>,
    asset_update_event_telemetry_dispatcher: Arc<Dispatcher<(Asset, Option<AckToken>)>>,
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
        options: &ClientOptions,
    ) -> Self {
        let aep_name_command_options = CommandOptionsBuilder::default()
            .topic_token_map(HashMap::from([(
                "connectorClientId".to_string(),
                client.client_id().to_string(),
            )]))
            .build()
            .expect("Statically generated options should not fail.");

        let aep_telemetry_options = TelemetryOptionsBuilder::default()
            .topic_token_map(HashMap::from([(
                "connectorClientId".to_string(),
                client.client_id().to_string(),
            )]))
            .auto_ack(options.key_notification_auto_ack)
            .build()
            .expect("DTDL schema generated invalid arguments");

        // Create the shutdown notifier for the receiver loop
        let aep_shutdown_notifier = Arc::new(Notify::new());
        let asset_shutdown_notifier = Arc::new(Notify::new());

        // Dispatchers for AEPs and Assets
        let asset_update_event_telemetry_dispatcher = Arc::new(Dispatcher::new());
        let aep_update_event_telemetry_dispatcher = Arc::new(Dispatcher::new());

        let aep_update_event_telemetry_dispatcher_clone =
            aep_update_event_telemetry_dispatcher.clone();
        let asset_update_event_telemetry_dispatcher_clone =
            asset_update_event_telemetry_dispatcher.clone();

        // Start the update aep and assets notification loop
        task::spawn({
            let asset_endpoint_profile_update_event_telemetry_receiver =
                adr_base_service_gen::AssetEndpointProfileUpdateEventTelemetryReceiver::new(
                    application_context.clone(),
                    client.clone(),
                    &aep_telemetry_options,
                );
            let asset_update_event_telemetry_receiver =
                adr_base_service_gen::AssetUpdateEventTelemetryReceiver::new(
                    application_context.clone(),
                    client.clone(),
                    &aep_telemetry_options,
                );

            async move {
                Self::receive_update_event_telemetry_loop(
                    aep_shutdown_notifier,
                    asset_shutdown_notifier,
                    asset_endpoint_profile_update_event_telemetry_receiver,
                    asset_update_event_telemetry_receiver,
                    aep_update_event_telemetry_dispatcher,
                    asset_update_event_telemetry_dispatcher,
                )
                .await;
            }
        });
        Self {
            get_asset_endpoint_profile_command_invoker: Arc::new(
                adr_base_service_gen::GetAssetEndpointProfileCommandInvoker::new(
                    application_context.clone(),
                    client.clone(),
                    &aep_name_command_options,
                ),
            ),
            update_asset_endpoint_profile_status_command_invoker: Arc::new(
                adr_base_service_gen::UpdateAssetEndpointProfileStatusCommandInvoker::new(
                    application_context.clone(),
                    client.clone(),
                    &aep_name_command_options,
                ),
            ),
            notify_on_asset_endpoint_profile_update_command_invoker: Arc::new(
                adr_base_service_gen::NotifyOnAssetEndpointProfileUpdateCommandInvoker::new(
                    application_context.clone(),
                    client.clone(),
                    &aep_name_command_options,
                ),
            ),
            get_asset_command_invoker: Arc::new(adr_base_service_gen::GetAssetCommandInvoker::new(
                application_context.clone(),
                client.clone(),
                &aep_name_command_options,
            )),
            update_asset_status_command_invoker: Arc::new(
                adr_base_service_gen::UpdateAssetStatusCommandInvoker::new(
                    application_context.clone(),
                    client.clone(),
                    &aep_name_command_options,
                ),
            ),
            create_detected_asset_command_invoker: Arc::new(
                adr_base_service_gen::CreateDetectedAssetCommandInvoker::new(
                    application_context.clone(),
                    client.clone(),
                    &aep_name_command_options,
                ),
            ),
            notify_on_asset_update_command_invoker: Arc::new(
                adr_base_service_gen::NotifyOnAssetUpdateCommandInvoker::new(
                    application_context.clone(),
                    client.clone(),
                    &aep_name_command_options,
                ),
            ),
            create_asset_endpoint_profile_command_invoker: Arc::new(
                aep_type_service_gen::CreateDiscoveredAssetEndpointProfileCommandInvoker::new(
                    application_context,
                    client.clone(),
                    &AepCommandOptionsBuilder::default()
                        .topic_token_map(HashMap::from([(
                            "connectorClientId".to_string(),
                            client.client_id().to_string(),
                        )]))
                        .build()
                        .expect("Statically generated options should not fail."),
                ),
            ),
            aep_update_event_telemetry_dispatcher: aep_update_event_telemetry_dispatcher_clone,
            asset_update_event_telemetry_dispatcher: asset_update_event_telemetry_dispatcher_clone,
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
            .topic_tokens(HashMap::from([("aepName".to_string(), aep_name)]))
            .timeout(timeout)
            .build()
            .map_err(|e| Error(ErrorKind::from(e)))?;

        let response = self
            .get_asset_endpoint_profile_command_invoker
            .invoke(command_request)
            .await
            .map_err(ErrorKind::from)?;

        Ok(response.payload.asset_endpoint_profile.into())
    }

    /// Updates an asset endpoint profile's status in the Azure Device Registry service.
    ///
    /// # Arguments
    /// * `aep_name` - The name of the asset endpoint profile.
    /// * [`AssetEndpointProfileStatus`] - The request containing all the information about an asset for the update.
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
    pub async fn update_asset_endpoint_profile_status(
        &self,
        aep_name: String,
        status: AssetEndpointProfileStatus,
        timeout: Duration,
    ) -> Result<AssetEndpointProfile, Error> {
        let payload = adr_base_service_gen::UpdateAssetEndpointProfileStatusRequestPayload {
            asset_endpoint_profile_status_update: status.into(),
        };

        let command_request = rpc_command::invoker::RequestBuilder::default()
            .topic_tokens(HashMap::from([("aepName".to_string(), aep_name)]))
            .payload(payload)
            .map_err(|e| Error(ErrorKind::SerializationError(e.to_string())))?
            .timeout(timeout)
            .build()
            .map_err(ErrorKind::from)?;

        let response = self
            .update_asset_endpoint_profile_status_command_invoker
            .invoke(command_request)
            .await
            .map_err(ErrorKind::from)?;

        Ok(response.payload.updated_asset_endpoint_profile.into())
    }

    /// Notifies the Azure Device Registry service that client is listening for asset endpoint profile updates.
    ///
    /// # Arguments
    /// * `aep_name` - The name of the asset endpoint profile.
    /// * `timeout` - The duration until the Client stops waiting for a response to the request, it is rounded up to the nearest second.
    ///
    /// Returns the [`AssetEndpointProfileObservation`] if observation was done successfully or [`Error`].
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
    ///
    /// [`struct@Error`] of kind [`ObservationError`](ErrorKind::ObservationError)
    /// if notification response failed.
    ///
    /// [`struct@Error`] of kind [`DuplicateObserve`](ErrorKind::DuplicateObserve)
    /// if duplicate aeps are being observed.
    pub async fn observe_asset_endpoint_profile_update(
        &self,
        aep_name: String,
        timeout: Duration,
    ) -> Result<AssetEndpointProfileObservation, Error> {
        let rx = self
            .aep_update_event_telemetry_dispatcher
            .register_receiver(aep_name.clone())
            .map_err(|_| Error(ErrorKind::DuplicateObserve))?;

        let payload = adr_base_service_gen::NotifyOnAssetEndpointProfileUpdateRequestPayload {
            notification_request: adr_base_service_gen::NotificationMessageType::On,
        };

        let command_request = rpc_command::invoker::RequestBuilder::default()
            .topic_tokens(HashMap::from([("aepName".to_string(), aep_name.clone())]))
            .payload(payload)
            .map_err(|e| Error(ErrorKind::SerializationError(e.to_string())))?
            .timeout(timeout)
            .build()
            .map_err(ErrorKind::from)?;

        let result = self
            .notify_on_asset_endpoint_profile_update_command_invoker
            .invoke(command_request)
            .await;

        match result {
            Ok(response) => {
                if let adr_base_service_gen::NotificationResponse::Accepted =
                    response.payload.notification_response
                {
                    Ok(AssetEndpointProfileObservation {
                        name: aep_name.clone(),
                        receiver: rx,
                    })
                } else {
                    // TODO Check error kind - another kind needs to be included ?
                    Err(Error(ErrorKind::ObservationError(aep_name)))
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

    /// Notifies the Azure Device Registry service that client is no longer listening for asset endpoint profile updates.
    ///
    /// # Arguments
    /// * `aep_name` - The name of the asset endpoint profile.
    /// * `timeout` - The duration until the Client stops waiting for a response to the request, it is rounded up to the nearest second.
    ///
    /// Returns the [`AssetEndpointProfileObservation`] if observation was done succesfully or [`Error`].
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
    ///     
    /// [`struct@Error`] of kind [`ObservationError`](ErrorKind::ObservationError)
    /// if notification response failed.
    pub async fn unobserve_asset_endpoint_profile_update(
        &self,
        aep_name: String,
        timeout: Duration,
    ) -> Result<(), Error> {
        let payload = adr_base_service_gen::NotifyOnAssetEndpointProfileUpdateRequestPayload {
            notification_request: adr_base_service_gen::NotificationMessageType::Off,
        };

        let command_request = rpc_command::invoker::RequestBuilder::default()
            .topic_tokens(HashMap::from([("aepName".to_string(), aep_name.clone())]))
            .payload(payload)
            .map_err(|e| Error(ErrorKind::SerializationError(e.to_string())))?
            .timeout(timeout)
            .build()
            .map_err(ErrorKind::from)?;

        let result = self
            .notify_on_asset_endpoint_profile_update_command_invoker
            .invoke(command_request)
            .await;

        match result {
            Ok(response) => {
                if let adr_base_service_gen::NotificationResponse::Accepted =
                    response.payload.notification_response
                {
                    Ok(())
                } else {
                    // TODO Check error kind - another kind needs to be incldued ?
                    Err(Error(ErrorKind::ObservationError(aep_name)))
                }
            }
            Err(e) => {
                // If the unobserve request wasn't successful, remove it from our dispatcher
                if self
                    .aep_update_event_telemetry_dispatcher
                    .unregister_receiver(&aep_name)
                {
                    log::debug!("Aep removed from observed list: {aep_name:?}");
                } else {
                    log::debug!("Aep not in observed list: {aep_name:?}");
                }
                Err(Error(e.into()))
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
        discovered_aep: DiscoveredAssetEndpointProfile,
        timeout: Duration,
    ) -> Result<DiscoveredAssetEndpointProfileResponseStatus, Error> {
        let paylaod = aep_type_service_gen::CreateDiscoveredAssetEndpointProfileRequestPayload {
            discovered_asset_endpoint_profile: discovered_aep.into(),
        };
        let command_request = rpc_command::invoker::RequestBuilder::default()
            .payload(paylaod)
            .map_err(|e| Error(ErrorKind::SerializationError(e.to_string())))?
            .timeout(timeout)
            .build()
            .map_err(ErrorKind::from)?;

        let response = self
            .create_asset_endpoint_profile_command_invoker
            .invoke(command_request)
            .await
            .map_err(ErrorKind::from)?;

        Ok(response
            .payload
            .create_discovered_asset_endpoint_profile_response
            .status
            .into())
    }

    /// Retrieves an asset from a Azure Device Registry service.
    ///
    /// # Arguments
    /// * `aep_name` - The name of the asset endpoint profile.
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
    pub async fn get_asset(
        &self,
        aep_name: String,
        asset_name: String,
        timeout: Duration,
    ) -> Result<Asset, Error> {
        let get_request_payload = adr_base_service_gen::GetAssetRequestPayload { asset_name };

        let command_request = rpc_command::invoker::RequestBuilder::default()
            .topic_tokens(HashMap::from([("aepName".to_string(), aep_name.clone())]))
            .payload(get_request_payload)
            .map_err(|e| Error(ErrorKind::SerializationError(e.to_string())))?
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

    /// Updates an asset in the Azure Device Registry service.
    ///
    /// # Arguments
    /// # `name` - The name of the asset.
    /// * [`AssetStatus`] - The status of an asset for the update.
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
        let payload = adr_base_service_gen::UpdateAssetStatusRequestPayload {
            asset_status_update: adr_base_service_gen::UpdateAssetStatusRequestSchema {
                asset_name: name,
                asset_status: status.into(),
            },
        };

        let command_request = rpc_command::invoker::RequestBuilder::default()
            .payload(payload)
            .map_err(|e| Error(ErrorKind::SerializationError(e.to_string())))?
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
    ) -> Result<DetectedAssetResponseStatus, Error> {
        let payload = adr_base_service_gen::CreateDetectedAssetRequestPayload {
            detected_asset: asset.into(),
        };
        let command_request = rpc_command::invoker::RequestBuilder::default()
            .payload(payload)
            .map_err(|e| Error(ErrorKind::SerializationError(e.to_string())))?
            .timeout(timeout)
            .build()
            .map_err(ErrorKind::from)?;

        let response = self
            .create_detected_asset_command_invoker
            .invoke(command_request)
            .await
            .map_err(ErrorKind::from)?;

        Ok(response
            .payload
            .create_detected_asset_response
            .status
            .into())
    }

    /// Notifies the Azure Device Registry service that client is listening for asset updates.
    ///
    /// # Arguments
    /// * `aep_name` - The name of the asset endpoint profile.
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
    ///
    /// [`struct@Error`] of kind [`ObservationError`](ErrorKind::ObservationError)
    /// if notification response failed.
    ///
    /// [`struct@Error`] of kind [`DuplicateObserve`](ErrorKind::DuplicateObserve)
    /// if duplicate assets are being observed.
    pub async fn observe_asset_update(
        &self,
        aep_name: String,
        asset_name: String,
        timeout: Duration,
    ) -> Result<AssetObservation, Error> {
        // TODO Right now using aep_name + asset_name as the key for the dispatcher, consider using tuple
        let receiver_id = aep_name.clone() + "~" + &asset_name;

        let rx = self
            .asset_update_event_telemetry_dispatcher
            .register_receiver(receiver_id.clone())
            .map_err(|_| Error(ErrorKind::DuplicateObserve))?;

        let notification_payload = adr_base_service_gen::NotifyOnAssetUpdateRequestPayload {
            notification_request: adr_base_service_gen::NotifyOnAssetUpdateRequestSchema {
                asset_name: asset_name.clone(),
                notification_message_type: adr_base_service_gen::NotificationMessageType::On,
            },
        };

        let command_request = rpc_command::invoker::RequestBuilder::default()
            .topic_tokens(HashMap::from([("aepName".to_string(), aep_name)]))
            .payload(notification_payload)
            .map_err(|e| Error(ErrorKind::SerializationError(e.to_string())))?
            .timeout(timeout)
            .build()
            .map_err(ErrorKind::from)?;

        let result = self
            .notify_on_asset_update_command_invoker
            .invoke(command_request)
            .await;

        match result {
            Ok(response) => {
                if let adr_base_service_gen::NotificationResponse::Accepted =
                    response.payload.notification_response
                {
                    Ok(AssetObservation {
                        name: asset_name,
                        receiver: rx,
                    })
                } else {
                    // TODO Check error kind - another kind needs to be incldued ?
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
                        "AEP and Asset combination removed from observed list: {receiver_id:?}"
                    );
                } else {
                    log::debug!("AEP and Asset combination not in observed list: {receiver_id:?}");
                }
                Err(Error(ErrorKind::AIOProtocolError(e)))
            }
        }
    }

    /// Notifies the Azure Device Registry service that client is no longer listening for asset updates.
    ///
    /// # Arguments
    /// * `aep_name` - The name of the asset endpoint profile.
    /// * `asset_name` - The name of the asset endpoint profile.
    /// * `timeout` - The duration until the Client stops waiting for a response to the request, it is rounded up to the nearest second.
    ///
    /// Returns the [`AssetEndpointProfileObservation`] if observation was done succesfully or [`Error`].
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
    ///     
    /// [`struct@Error`] of kind [`ObservationError`](ErrorKind::ObservationError)
    /// if notification response failed.
    pub async fn unobserve_asset_update(
        &self,
        aep_name: String,
        asset_name: String,
        timeout: Duration,
    ) -> Result<(), Error> {
        // TODO Right now using aep_name + asset_name as the key for the dispatcher, consider using tuple
        let receiver_id = aep_name.clone() + "~" + &asset_name;

        let notification_payload = adr_base_service_gen::NotifyOnAssetUpdateRequestPayload {
            notification_request: adr_base_service_gen::NotifyOnAssetUpdateRequestSchema {
                asset_name: asset_name.clone(),
                notification_message_type: adr_base_service_gen::NotificationMessageType::On,
            },
        };

        let command_request = rpc_command::invoker::RequestBuilder::default()
            .topic_tokens(HashMap::from([("aepName".to_string(), aep_name)]))
            .payload(notification_payload)
            .map_err(|e| Error(ErrorKind::SerializationError(e.to_string())))?
            .timeout(timeout)
            .build()
            .map_err(ErrorKind::from)?;

        let result = self
            .notify_on_asset_update_command_invoker
            .invoke(command_request)
            .await;

        match result {
            Ok(response) => {
                if let adr_base_service_gen::NotificationResponse::Accepted =
                    response.payload.notification_response
                {
                    Ok(())
                } else {
                    Err(Error(ErrorKind::ObservationError(asset_name)))
                }
            }
            Err(e) => {
                // If the unobserve request wasn't successful, remove it from our dispatcher
                if self
                    .aep_update_event_telemetry_dispatcher
                    .unregister_receiver(&receiver_id)
                {
                    log::debug!("Asset removed from observed list: {receiver_id:?}");
                } else {
                    log::debug!("Asset not in observed list: {receiver_id:?}");
                }
                Err(Error(ErrorKind::AIOProtocolError(e)))
            }
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
        let mut errors = Vec::new();

        // Attempt to shut down each invoker and collect any AIOProtocolError
        if let Err(e) = self
            .get_asset_endpoint_profile_command_invoker
            .shutdown()
            .await
        {
            errors.push(e);
        }

        if let Err(e) = self
            .update_asset_endpoint_profile_status_command_invoker
            .shutdown()
            .await
        {
            errors.push(e);
        }

        if let Err(e) = self
            .notify_on_asset_endpoint_profile_update_command_invoker
            .shutdown()
            .await
        {
            errors.push(e);
        }

        if let Err(e) = self.get_asset_command_invoker.shutdown().await {
            errors.push(e);
        }

        if let Err(e) = self.update_asset_status_command_invoker.shutdown().await {
            errors.push(e);
        }

        if let Err(e) = self.create_detected_asset_command_invoker.shutdown().await {
            errors.push(e);
        }

        if let Err(e) = self.notify_on_asset_update_command_invoker.shutdown().await {
            errors.push(e);
        }

        // If there are any errors, return them as a ShutdownError
        if errors.is_empty() {
            Ok(())
        } else {
            Err(ErrorKind::ShutdownError(errors).into())
        }
    }

    async fn receive_update_event_telemetry_loop(
        aep_shutdown_notifier: Arc<Notify>,   // Separate notifier for AEP
        asset_shutdown_notifier: Arc<Notify>, // Separate notifier for Asset
        mut asset_endpoint_profile_update_event_telemetry_receiver: adr_base_service_gen::AssetEndpointProfileUpdateEventTelemetryReceiver<C>,
        mut asset_update_event_telemetry_receiver: adr_base_service_gen::AssetUpdateEventTelemetryReceiver<C>,
        aep_update_event_telemetry_dispatcher: Arc<
            Dispatcher<(AssetEndpointProfile, Option<AckToken>)>,
        >,
        asset_update_event_telemetry_dispatcher: Arc<Dispatcher<(Asset, Option<AckToken>)>>,
    ) {
        let mut aep_shutdown_attempt_count = 0;
        let mut asset_shutdown_attempt_count = 0;

        loop {
            tokio::select! {
                // AEP shutdown handler
                () = aep_shutdown_notifier.notified() => {
                    match asset_endpoint_profile_update_event_telemetry_receiver.shutdown().await {
                        Ok(()) => {
                            log::info!("AssetEndpointProfileUpdateEventTelemetryReceiver shutdown");

                        }
                        Err(e) => {
                            log::error!("Error shutting down AssetEndpointProfileUpdateEventTelemetryReceiver: {e}");
                            // try shutdown again, but not indefinitely
                            if aep_shutdown_attempt_count < 3 {
                                aep_shutdown_attempt_count += 1;
                                aep_shutdown_notifier.notify_one();
                            }
                        }
                    }
                },

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

                aep_msg = asset_endpoint_profile_update_event_telemetry_receiver.recv() => {
                    if let Some(m) = aep_msg {
                        match m {
                            Ok((aep_update_event_telemetry, ack_token)) => {
                                let Some(aep_name) = aep_update_event_telemetry.topic_tokens.get("ex:aepName") else {
                                    log::error!("AssetEndpointProfileUpdateEventTelemetry missing ex:aepName topic token.");
                                    continue;
                                };
                                // Try to send the notification to the associated receiver
                                match aep_update_event_telemetry_dispatcher.dispatch(aep_name, (aep_update_event_telemetry.payload.into(), ack_token)) {
                                    Ok(()) => {
                                        log::debug!("AssetEndpointProfileUpdateEventTelemetry dispatched for aep name: {aep_name:?}");
                                    }
                                    Err(DispatchError::SendError(payload)) => {
                                        log::warn!("AssetEndpointProfileUpdateEventTelemetryReceiver has been dropped. Received Telemetry: {payload:?}",);
                                    }
                                    Err(DispatchError::NotFound(payload)) => {
                                        log::warn!("AssetEndpointProfile is not being observed. Received AssetEndpointProfileUpdateEventTelemetry: {payload:?}",);
                                    }
                                }
                            }
                            Err(e) => {
                                // This should only happen on errors subscribing, but it's likely not recoverable
                                log::error!("Error receiving AssetEndpointProfileUpdateEventTelemetry: {e}. Shutting down AssetEndpointProfileUpdateEventTelemetryReceiver.");
                                // try to shutdown telemetry receiver, but not indefinitely
                                if aep_shutdown_attempt_count < 3 {
                                    aep_shutdown_notifier.notify_one();
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
                                let Some(aep_name) = asset_update_event_telemetry.topic_tokens.get("ex:aepName") else {
                                    log::error!("AssetEndpointProfileUpdateEventTelemetry missing ex:aepName topic token.");
                                    continue;
                                };
                                // TODO Consider making the receiver id a tuple in the dispatcher
                                let dispatch_receiver_id = format!("{}~{}", aep_name, asset_update_event_telemetry.payload.asset_update_event.asset_name);

                                match asset_update_event_telemetry_dispatcher.dispatch(&dispatch_receiver_id, (asset_update_event_telemetry.payload.into(), ack_token)) {
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
