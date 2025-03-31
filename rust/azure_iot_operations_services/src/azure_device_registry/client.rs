// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

//! Client for Azure Device Registry operations.
//!
//! To use this client, the `azure_device_registry` feature must be enabled.

use crate::azure_device_registry::adr_name_gen::adr_base_service::client::{
    Asset, AssetEndpointProfile, CreateDetectedAssetCommandInvoker,
    DetectedAssetResponseStatusSchema, GetAssetCommandInvoker,
    GetAssetEndpointProfileCommandInvoker, GetAssetRequestPayload, NotificationMessageType,
    NotificationResponse, NotifyOnAssetEndpointProfileUpdateCommandInvoker,
    NotifyOnAssetEndpointProfileUpdateRequestPayload, NotifyOnAssetUpdateRequestPayload,
    NotifyOnAssetUpdateRequestSchema, UpdateAssetEndpointProfileStatusCommandInvoker,
    UpdateAssetStatusCommandInvoker,
};
use crate::azure_device_registry::adr_name_gen::common_types::common_options::CommandOptionsBuilder;
use crate::azure_device_registry::adr_type_gen::aep_type_service::client::DiscoveredAssetEndpointProfileResponseStatusSchema;
use crate::azure_device_registry::adr_type_gen::common_types::common_options::CommandOptionsBuilder as AepCommandOptionsBuilder;
use crate::azure_device_registry::{
    AzureDeviceRegistryError, CreateDetectedAssetReq, CreateDiscoveredAssetEndpointProfileReq,
    ErrorKind, UpdateAssetEndpointProfileStatusReq, UpdateAssetStatusReq,
};
use azure_iot_operations_mqtt::interface::ManagedClient;
use azure_iot_operations_protocol::application::ApplicationContext;
use azure_iot_operations_protocol::rpc_command;
use std::sync::Arc;
use std::time::Duration;

use super::adr_name_gen::adr_base_service::client::NotifyOnAssetUpdateCommandInvoker;
use super::adr_type_gen::aep_type_service::client::CreateDiscoveredAssetEndpointProfileCommandInvoker;

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
}

impl<C> Client<C>
where
    C: ManagedClient + Clone + Send + Sync + 'static,
    C::PubReceiver: Send + Sync,
{
    /// Create a new Azure Device Registry Client.
    ///
    /// # Panics
    /// Panics if the options for the underlying command invokers cannot be built. Not possible since
    /// the options are statically generated.
    pub fn new(application_context: ApplicationContext, client: &C) -> Self {
        let options = CommandOptionsBuilder::default()
            .build()
            .expect("Statically generated options should not fail.");

        Self {
            get_asset_endpoint_profile_command_invoker: Arc::new(
                GetAssetEndpointProfileCommandInvoker::new(
                    application_context.clone(),
                    client.clone(),
                    &options,
                ),
            ),
            update_asset_endpoint_profile_status_command_invoker: Arc::new(
                UpdateAssetEndpointProfileStatusCommandInvoker::new(
                    application_context.clone(),
                    client.clone(),
                    &options,
                ),
            ),
            notify_on_asset_endpoint_profile_update_command_invoker: Arc::new(
                NotifyOnAssetEndpointProfileUpdateCommandInvoker::new(
                    application_context.clone(),
                    client.clone(),
                    &options,
                ),
            ),
            get_asset_command_invoker: Arc::new(GetAssetCommandInvoker::new(
                application_context.clone(),
                client.clone(),
                &options,
            )),
            update_asset_status_command_invoker: Arc::new(UpdateAssetStatusCommandInvoker::new(
                application_context.clone(),
                client.clone(),
                &options,
            )),
            create_detected_asset_command_invoker: Arc::new(
                CreateDetectedAssetCommandInvoker::new(
                    application_context.clone(),
                    client.clone(),
                    &options,
                ),
            ),
            notify_on_asset_update_command_invoker: Arc::new(
                NotifyOnAssetUpdateCommandInvoker::new(
                    application_context.clone(),
                    client.clone(),
                    &options,
                ),
            ),
            create_asset_endpoint_profile_command_invoker: Arc::new(
                CreateDiscoveredAssetEndpointProfileCommandInvoker::new(
                    application_context,
                    client.clone(),
                    &AepCommandOptionsBuilder::default()
                        .build()
                        .expect("Statically generated options should not fail."),
                ),
            ),
        }
    }

    /// Retrieves an asset endpoint profile from a Azure Device Registry service.
    ///
    /// # Arguments
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
        timeout: Duration,
    ) -> Result<AssetEndpointProfile, AzureDeviceRegistryError> {
        let command_request = rpc_command::invoker::RequestBuilder::default()
            .timeout(timeout)
            .build()
            .map_err(|e| AzureDeviceRegistryError(ErrorKind::InvalidArgument(e.to_string())))?;

        let result = self
            .get_asset_endpoint_profile_command_invoker
            .invoke(command_request)
            .await;

        match result {
            Ok(response) => Ok(response.payload.asset_endpoint_profile),
            Err(e) => Err(AzureDeviceRegistryError(ErrorKind::AIOProtocolError(e))),
        }
    }

    /// Updates an asset endprofile in the Azure Device Registry service.
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
        source: UpdateAssetEndpointProfileStatusReq,
        timeout: Duration,
    ) -> Result<AssetEndpointProfile, AzureDeviceRegistryError> {
        let command_request = rpc_command::invoker::RequestBuilder::default()
            .payload(source.into())
            .map_err(|e| AzureDeviceRegistryError(ErrorKind::SerializationError(e.to_string())))?
            .timeout(timeout)
            .build()
            .map_err(|e| AzureDeviceRegistryError(ErrorKind::InvalidArgument(e.to_string())))?;

        let result = self
            .update_asset_endpoint_profile_status_command_invoker
            .invoke(command_request)
            .await;

        match result {
            Ok(response) => Ok(response.payload.updated_asset_endpoint_profile),
            Err(e) => Err(AzureDeviceRegistryError(ErrorKind::AIOProtocolError(e))),
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
        notification_type: bool,
        timeout: Duration,
    ) -> Result<NotificationResponse, AzureDeviceRegistryError> {
        let payload = NotifyOnAssetEndpointProfileUpdateRequestPayload {
            notification_request: if notification_type {
                NotificationMessageType::On
            } else {
                NotificationMessageType::Off
            },
        };

        let command_request = rpc_command::invoker::RequestBuilder::default()
            .payload(payload)
            .map_err(|e| AzureDeviceRegistryError(ErrorKind::SerializationError(e.to_string())))?
            .timeout(timeout)
            .build()
            .map_err(|e| AzureDeviceRegistryError(ErrorKind::InvalidArgument(e.to_string())))?;

        let result = self
            .notify_on_asset_endpoint_profile_update_command_invoker
            .invoke(command_request)
            .await;

        match result {
            Ok(response) => Ok(response.payload.notification_response),
            Err(e) => Err(AzureDeviceRegistryError(ErrorKind::AIOProtocolError(e))),
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
    pub async fn get_asset(
        &self,
        asset_name: String,
        timeout: Duration,
    ) -> Result<Asset, AzureDeviceRegistryError> {
        let get_request_payload = GetAssetRequestPayload { asset_name };

        let command_request = rpc_command::invoker::RequestBuilder::default()
            .payload(get_request_payload)
            .map_err(|e| AzureDeviceRegistryError(ErrorKind::SerializationError(e.to_string())))?
            .timeout(timeout)
            .build()
            .map_err(|e| AzureDeviceRegistryError(ErrorKind::InvalidArgument(e.to_string())))?;

        let result = self.get_asset_command_invoker.invoke(command_request).await;

        match result {
            Ok(response) => Ok(response.payload.asset),
            Err(e) => Err(AzureDeviceRegistryError(ErrorKind::AIOProtocolError(e))),
        }
    }

    /// Updates an asset in the Azure Device Registry service.
    ///
    /// # Arguments
    /// * [`UpdateAssetStatusReq`] - The request containing all the information about an aseet for the update.
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
        source: UpdateAssetStatusReq,
        timeout: Duration,
    ) -> Result<Asset, AzureDeviceRegistryError> {
        let command_request = rpc_command::invoker::RequestBuilder::default()
            .payload(source.into())
            .map_err(|e| AzureDeviceRegistryError(ErrorKind::SerializationError(e.to_string())))?
            .timeout(timeout)
            .build()
            .map_err(|e| AzureDeviceRegistryError(ErrorKind::InvalidArgument(e.to_string())))?;

        let result = self
            .update_asset_status_command_invoker
            .invoke(command_request)
            .await;

        match result {
            Ok(response) => Ok(response.payload.updated_asset),
            Err(e) => Err(AzureDeviceRegistryError(ErrorKind::AIOProtocolError(e))),
        }
    }

    /// Creates an asset inside the Azure Device Registry service.
    ///
    /// # Arguments
    /// * [`CreateDetectedAssetReq`] - All relevant details needed for an aset cretaion
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
        source: CreateDetectedAssetReq,
        timeout: Duration,
    ) -> Result<DetectedAssetResponseStatusSchema, AzureDeviceRegistryError> {
        let command_request = rpc_command::invoker::RequestBuilder::default()
            .payload(source.into())
            .map_err(|e| AzureDeviceRegistryError(ErrorKind::SerializationError(e.to_string())))?
            .timeout(timeout)
            .build()
            .map_err(|e| AzureDeviceRegistryError(ErrorKind::InvalidArgument(e.to_string())))?;

        let result = self
            .create_detected_asset_command_invoker
            .invoke(command_request)
            .await;

        match result {
            Ok(response) => Ok(response.payload.create_detected_asset_response.status),
            Err(e) => Err(AzureDeviceRegistryError(ErrorKind::AIOProtocolError(e))),
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
    ) -> Result<NotificationResponse, AzureDeviceRegistryError> {
        let notification_paylaod = NotifyOnAssetUpdateRequestPayload {
            notification_request: NotifyOnAssetUpdateRequestSchema {
                asset_name,
                notification_message_type: if notification_type {
                    NotificationMessageType::On
                } else {
                    NotificationMessageType::Off
                },
            },
        };

        let command_request = rpc_command::invoker::RequestBuilder::default()
            .payload(notification_paylaod)
            .map_err(|e| AzureDeviceRegistryError(ErrorKind::SerializationError(e.to_string())))?
            .timeout(timeout)
            .build()
            .map_err(|e| AzureDeviceRegistryError(ErrorKind::InvalidArgument(e.to_string())))?;

        let result = self
            .notify_on_asset_update_command_invoker
            .invoke(command_request)
            .await;

        match result {
            Ok(response) => Ok(response.payload.notification_response),
            Err(e) => Err(AzureDeviceRegistryError(ErrorKind::AIOProtocolError(e))),
        }
    }

    /// Creates an asset endpoint profile inside the Azure Device Registry service.
    ///
    /// # Arguments
    /// * [`CreateDiscoveredAssetEndpointProfileReq`] - All relevant details needed for an asset endpoint profile cretaion
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
        source: CreateDiscoveredAssetEndpointProfileReq,
        timeout: Duration,
    ) -> Result<DiscoveredAssetEndpointProfileResponseStatusSchema, AzureDeviceRegistryError> {
        let command_request = rpc_command::invoker::RequestBuilder::default()
            .payload(source.into())
            .map_err(|e| AzureDeviceRegistryError(ErrorKind::SerializationError(e.to_string())))?
            .timeout(timeout)
            .build()
            .map_err(|e| AzureDeviceRegistryError(ErrorKind::InvalidArgument(e.to_string())))?;

        let result = self
            .create_asset_endpoint_profile_command_invoker
            .invoke(command_request)
            .await;

        match result {
            Ok(response) => Ok(response
                .payload
                .create_discovered_asset_endpoint_profile_response
                .status),
            Err(e) => Err(AzureDeviceRegistryError(ErrorKind::AIOProtocolError(e))),
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
    pub async fn shutdown(&self) -> Result<(), AzureDeviceRegistryError> {
        self.get_asset_endpoint_profile_command_invoker
            .shutdown()
            .await
            .map_err(|e| AzureDeviceRegistryError(ErrorKind::AIOProtocolError(e)))?;
        self.update_asset_endpoint_profile_status_command_invoker
            .shutdown()
            .await
            .map_err(|e| AzureDeviceRegistryError(ErrorKind::AIOProtocolError(e)))?;
        self.notify_on_asset_endpoint_profile_update_command_invoker
            .shutdown()
            .await
            .map_err(|e| AzureDeviceRegistryError(ErrorKind::AIOProtocolError(e)))?;
        self.get_asset_command_invoker
            .shutdown()
            .await
            .map_err(|e| AzureDeviceRegistryError(ErrorKind::AIOProtocolError(e)))?;
        self.update_asset_status_command_invoker
            .shutdown()
            .await
            .map_err(|e| AzureDeviceRegistryError(ErrorKind::AIOProtocolError(e)))?;
        self.create_detected_asset_command_invoker
            .shutdown()
            .await
            .map_err(|e| AzureDeviceRegistryError(ErrorKind::AIOProtocolError(e)))?;
        self.notify_on_asset_update_command_invoker
            .shutdown()
            .await
            .map_err(|e| AzureDeviceRegistryError(ErrorKind::AIOProtocolError(e)))?;
        Ok(())
    }
}
