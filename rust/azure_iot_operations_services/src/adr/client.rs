// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

//! Client for Schema Registry operations.
//!
//! To use this client, the `schema_registry` feature must be enabled.

use crate::adr::adr_name_gen::adr_base_service::client::{
    CreateDetectedAssetCommandInvoker, DetectedAssetResponseStatusSchema, GetAssetCommandInvoker,
    GetAssetEndpointProfileCommandInvoker, GetAssetRequestPayload,
    NotifyOnAssetEndpointProfileUpdateCommandInvoker,
    UpdateAssetEndpointProfileStatusCommandInvoker, UpdateAssetStatusCommandInvoker,
};
use crate::adr::adr_name_gen::common_types::common_options::CommandOptionsBuilder;
use crate::adr::{AdrError, Asset, CreateDetectedAssetReq, ErrorKind, UpdateAssetStatusReq};
use azure_iot_operations_mqtt::interface::ManagedClient;
use azure_iot_operations_protocol::application::ApplicationContext;
use azure_iot_operations_protocol::rpc_command;
use std::sync::Arc;
use std::time::Duration;

/// Schema registry client implementation.
#[derive(Clone)]
pub struct Client<C>
where
    C: ManagedClient + Clone + Send + Sync + 'static,
    C::PubReceiver: Send + Sync,
{
    // get_command_invoker: Arc<GetCommandInvoker<C>>,
    // put_command_invoker: Arc<PutCommandInvoker<C>>,
    create_detected_asset_command_invoker: Arc<CreateDetectedAssetCommandInvoker<C>>,
    get_asset_endpoint_profile_command_invoker: Arc<GetAssetEndpointProfileCommandInvoker<C>>,
    get_asset_command_invoker: Arc<GetAssetCommandInvoker<C>>,
    update_asset_endpoint_profile_status_command_invoker:
        Arc<UpdateAssetEndpointProfileStatusCommandInvoker<C>>,
    update_asset_status_command_invoker: Arc<UpdateAssetStatusCommandInvoker<C>>,
    notify_on_asset_endpoint_profile_update_command_invoker:
        Arc<NotifyOnAssetEndpointProfileUpdateCommandInvoker<C>>,
    client_id: String,
}

impl<C> Client<C>
where
    C: ManagedClient + Clone + Send + Sync + 'static,
    C::PubReceiver: Send + Sync,
{
    /// Create a new Schema Registry Client.
    ///
    /// # Panics
    /// Panics if the options for the underlying command invokers cannot be built. Not possible since
    /// the options are statically generated.
    pub fn new(application_context: ApplicationContext, client: &C) -> Self {
        let options = CommandOptionsBuilder::default()
            .build()
            .expect("Statically generated options should not fail.");

        Self {
            create_detected_asset_command_invoker: Arc::new(
                CreateDetectedAssetCommandInvoker::new(
                    application_context.clone(),
                    client.clone(),
                    &options,
                ),
            ),
            get_asset_endpoint_profile_command_invoker: Arc::new(
                GetAssetEndpointProfileCommandInvoker::new(
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
            notify_on_asset_endpoint_profile_update_command_invoker: Arc::new(
                NotifyOnAssetEndpointProfileUpdateCommandInvoker::new(
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
            update_asset_status_command_invoker: Arc::new(UpdateAssetStatusCommandInvoker::new(
                application_context,
                client.clone(),
                &options,
            )),
            client_id: client.client_id().to_string(), // TODO: Is this temporary?
        }
    }

    /// Retrieves an asset from a ADR service.
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
    ) -> Result<Asset, AdrError> {
        let get_request_payload = GetAssetRequestPayload {
            asset_name: asset_name,
        };

        let command_request = rpc_command::invoker::RequestBuilder::default()
            .custom_user_data(vec![("__invId".to_string(), self.client_id.clone())]) // TODO: This was for Schema Registry. But do we need this ?
            .payload(get_request_payload)
            .map_err(|e| AdrError(ErrorKind::SerializationError(e.to_string())))?
            .timeout(timeout)
            .build()
            .map_err(|e| AdrError(ErrorKind::InvalidArgument(e.to_string())))?;

        let get_result = self.get_asset_command_invoker.invoke(command_request).await;

        match get_result {
            Ok(response) => Ok(response.payload.asset),
            Err(e) => {
                if let azure_iot_operations_protocol::common::aio_protocol_error::AIOProtocolErrorKind::PayloadInvalid = e.kind {
                    if let Some(nested_error) = &e.nested_error {
                        if let Some(json_error) = nested_error.downcast_ref::<serde_json::Error>() {
                            return Err(AdrError(ErrorKind::SerializationError(json_error.to_string()))); // TODO What should we return if it is not an option ?
                        }
                    }
                }
                Err(AdrError(ErrorKind::from(e)))
            }
        }
    }

    /// Updates an asset in a ADR service.
    ///
    /// # Arguments
    /// * [`UpdateAssetStatusRequest`] - The request containing all the information about an aseet for the update.
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
    ) -> Result<Asset, AdrError> {
        let command_request = rpc_command::invoker::RequestBuilder::default()
            .custom_user_data(vec![("__invId".to_string(), self.client_id.clone())]) // TODO: ASK what to do with this ? Probaly not needed Temporary until the schema registry service updates their executor
            .payload(source.into())
            .map_err(|e| AdrError(ErrorKind::SerializationError(e.to_string())))?
            .timeout(timeout)
            .build()
            .map_err(|e| AdrError(ErrorKind::InvalidArgument(e.to_string())))?;

        let result = self
            .update_asset_status_command_invoker
            .invoke(command_request)
            .await;

        match result {
            Ok(response) => Ok(response.payload.updated_asset),
            Err(e) => {
                if let azure_iot_operations_protocol::common::aio_protocol_error::AIOProtocolErrorKind::PayloadInvalid = e.kind {
                    if let Some(nested_error) = &e.nested_error {
                        if let Some(json_error) = nested_error.downcast_ref::<serde_json::Error>() {
                            if json_error.is_eof() && json_error.column() == 0 && json_error.line() == 1 {
                                return Err(AdrError(ErrorKind::SerializationError(json_error.to_string()))); // TODO What should we return if it is not an option ?
                            }
                        }
                    }
                }
                Err(AdrError(ErrorKind::from(e)))
            }
        }
    }

    /// Creates an asset inside the ADR service.
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
    ) -> Result<DetectedAssetResponseStatusSchema, AdrError> {
        let command_request = rpc_command::invoker::RequestBuilder::default()
            .custom_user_data(vec![("__invId".to_string(), self.client_id.clone())]) // TODO: This was for Schema Registry. But do we need this ?
            .payload(source.into())
            .map_err(|e| AdrError(ErrorKind::SerializationError(e.to_string())))?
            .timeout(timeout)
            .build()
            .map_err(|e| AdrError(ErrorKind::InvalidArgument(e.to_string())))?;

        let result = self
            .create_detected_asset_command_invoker
            .invoke(command_request)
            .await;

        match result {
            Ok(response) => Ok(response.payload.create_detected_asset_response.status),
            Err(e) => {
                if let azure_iot_operations_protocol::common::aio_protocol_error::AIOProtocolErrorKind::PayloadInvalid = e.kind {
                    if let Some(nested_error) = &e.nested_error {
                        if let Some(json_error) = nested_error.downcast_ref::<serde_json::Error>() {
                            return Err(AdrError(ErrorKind::SerializationError(json_error.to_string()))); // TODO What should we return if it is not an option ?
                        }
                    }
                }
                Err(AdrError(ErrorKind::from(e)))
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
    pub async fn shutdown(&self) -> Result<(), AdrError> {
        self.create_detected_asset_command_invoker
            .shutdown()
            .await
            .map_err(ErrorKind::from)?;
        self.get_asset_command_invoker
            .shutdown()
            .await
            .map_err(ErrorKind::from)?;
        self.update_asset_status_command_invoker
            .shutdown()
            .await
            .map_err(ErrorKind::from)?;
        Ok(())
    }
}
