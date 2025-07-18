// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

//! Client to do Azure Device Registry Discovery operations with a base connector.

use std::sync::Arc;

use azure_iot_operations_services::azure_device_registry::{
    self,
    models::{self as adr_models},
};

use super::ConnectorContext;

/// Client exposing Azure Device Registry Discovery operations
pub struct Client(Arc<ConnectorContext>);

impl Client {
    /// Creates a new Azure Device Registry Discovery [`Client`].
    pub(crate) fn new(connector_context: Arc<ConnectorContext>) -> Self {
        Self(connector_context)
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
    /// [`azure_device_registry::Error`] of kind [`InvalidRequestArgument`](azure_device_registry::ErrorKind::InvalidRequestArgument)
    /// if timeout is 0 or > `u32::max`.
    ///
    /// [`azure_device_registry::Error`] of kind [`AIOProtocolError`](azure_device_registry::ErrorKind::AIOProtocolError) if:
    /// - inbound endpoint type is invalid for the topic.
    /// - there are any underlying errors from the AIO RPC protocol.
    ///
    /// [`azure_device_registry::Error`] of kind [`ValidationError`](azure_device_registry::ErrorKind::ValidationError)
    /// if the device name is empty.
    ///
    /// [`azure_device_registry::Error`] of kind [`ServiceError`](azure_device_registry::ErrorKind::ServiceError) if an error is returned
    /// by the Azure Device Registry service.
    pub async fn create_or_update_discovered_device(
        &self,
        device_name: String,
        device: adr_models::DiscoveredDevice,
        inbound_endpoint_type: String,
    ) -> Result<(String, u64), azure_device_registry::Error> {
        self.0
            .azure_device_registry_client
            .create_or_update_discovered_device(
                device_name,
                device,
                inbound_endpoint_type,
                self.0.default_timeout,
            )
            .await
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
    /// [`azure_device_registry::Error`] of kind [`InvalidRequestArgument`](azure_device_registry::ErrorKind::InvalidRequestArgument)
    /// if timeout is 0 or > `u32::max`.
    ///
    /// [`azure_device_registry::Error`] of kind [`AIOProtocolError`](azure_device_registry::ErrorKind::AIOProtocolError) if:
    /// - device or inbound endpoint names are invalid.
    /// - there are any underlying errors from the AIO RPC protocol.
    ///
    /// [`azure_device_registry::Error`] of kind [`ValidationError`](azure_device_registry::ErrorKind::ValidationError)
    /// if the asset name is empty.
    ///
    /// [`azure_device_registry::Error`] of kind [`ServiceError`](azure_device_registry::ErrorKind::ServiceError) if an error is returned
    /// by the Azure Device Registry service.
    pub async fn create_or_update_discovered_asset(
        &self,
        device_name: String,
        inbound_endpoint_name: String,
        asset_name: String,
        asset: adr_models::DiscoveredAsset,
    ) -> Result<(String, u64), azure_device_registry::Error> {
        self.0
            .azure_device_registry_client
            .create_or_update_discovered_asset(
                device_name,
                inbound_endpoint_name,
                asset_name,
                asset,
                self.0.default_timeout,
            )
            .await
    }
}
