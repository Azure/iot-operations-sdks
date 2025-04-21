// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

//! Client for Azure Device Registry operations.
//!
//! To use this client, the `azure_device_registry` feature must be enabled.

use azure_iot_operations_protocol::rpc_command;
use derive_builder::Builder;
use std::collections::HashMap;
use std::sync::Arc;
use std::time::Duration;

use azure_iot_operations_mqtt::interface::ManagedClient;
use azure_iot_operations_protocol::application::ApplicationContext;

use crate::azure_device_registry::device_name_gen::adr_base_service::client as adr_name_gen;
use crate::azure_device_registry::device_name_gen::common_types::options::CommandInvokerOptionsBuilder;

use super::{
    Asset, AssetStatus, AssetUpdateObservation, Device, DeviceStatus, DeviceUpdateObservation,
    Error, ErrorKind,
};

/// Options for the Azure Device Registry client.
#[derive(Builder, Clone)]
#[builder(setter(into))]
pub struct ClientOptions {}

/// Azure Device Registry client implementation.
#[derive(Clone)]
pub struct Client<C>
where
    C: ManagedClient + Clone + Send + Sync + 'static,
    C::PubReceiver: Send + Sync,
{
    get_asset_command_invoker: Arc<adr_name_gen::GetAssetCommandInvoker<C>>,
}

impl<C> Client<C>
where
    C: ManagedClient + Clone + Send + Sync + 'static,
    C::PubReceiver: Send + Sync,
{
    // ~~~~~~~~~~~~~~~~~ General APIs ~~~~~~~~~~~~~~~~~~~~~
    pub fn new(
        application_context: ApplicationContext,
        client: &C,
        _options: &ClientOptions,
    ) -> Self {
        let aep_name_command_options = CommandInvokerOptionsBuilder::default()
            .topic_token_map(HashMap::from([(
                "connectorClientId".to_string(),
                client.client_id().to_string(),
            )]))
            .build()
            .expect("Statically generated options should not fail.");
        Self {
            get_asset_command_invoker: Arc::new(adr_name_gen::GetAssetCommandInvoker::new(
                application_context.clone(),
                client.clone(),
                &aep_name_command_options,
            )),
        }
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
        Err(Error {})
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
    #[allow(clippy::unused_async)]
    pub async fn get_device(
        &self,
        _device_name: String,
        _inbound_endpoint_name: String,
        _timeout: Duration,
    ) -> Result<Device, Error> {
        Err(Error {})
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
    #[allow(clippy::unused_async)]
    pub async fn update_device_plus_endpoint_status(
        &self,
        _device_name: String,
        _inbound_endpoint_name: String,
        _status: DeviceStatus, // TODO: should this be DeviceEndpointStatus that doesn't have hashmap of endpoints?
        _timeout: Duration,
    ) -> Result<Device, Error> {
        Err(Error {})
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
        Err(Error {})
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
        Err(Error {})
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
    #[allow(clippy::unused_async)]
    pub async fn get_asset(
        &self,
        _device_name: String,
        _inbound_endpoint_name: String,
        _asset_name: String,
        _timeout: Duration,
    ) -> Result<Device, Error> {
        let get_request_payload = adr_name_gen::GetAssetRequestPayload {
            asset_name: _asset_name,
        };

        let command_request = rpc_command::invoker::RequestBuilder::default()
            .topic_tokens(HashMap::from([
                ("deviceName".to_string(), _device_name.clone()),
                (
                    "inboundEndpointName".to_string(),
                    _inbound_endpoint_name.clone(),
                ),
            ]))
            .payload(get_request_payload)
            .map_err(|e| Error(ErrorKind::SerializationError(e.to_string())))?
            .timeout(_timeout)
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
        _device_name: String,
        _inbound_endpoint_name: String,
        _asset_name: String,
        _status: AssetStatus,
        _timeout: Duration,
    ) -> Result<Asset, Error> {
        Err(Error {})
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
    #[allow(clippy::unused_async)]
    pub async fn observe_asset_update_notifications(
        &self,
        _device_name: String,
        _inbound_endpoint_name: String,
        _asset_name: String,
        _timeout: Duration,
    ) -> Result<AssetUpdateObservation, Error> {
        Err(Error {})
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
        Err(Error {})
    }
}
