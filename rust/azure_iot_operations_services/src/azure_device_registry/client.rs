// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

//! Client for Azure Device Registry operations.
//!
//! To use this client, the `azure_device_registry` feature must be enabled.

use derive_builder::Builder;
use std::time::Duration;

use azure_iot_operations_mqtt::interface::ManagedClient;
use azure_iot_operations_protocol::application::ApplicationContext;

// use crate::azure_device_registry::device_name_gen::adr_base_service::client as adr_name_gen;

use super::{Device, DeviceStatus, DeviceUpdateObservation, Error};

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
    temp: std::marker::PhantomData<C>,
}

impl<C> Client<C>
where
    C: ManagedClient + Clone + Send + Sync + 'static,
    C::PubReceiver: Send + Sync,
{
    // ~~~~~~~~~~~~~~~~~ General APIs ~~~~~~~~~~~~~~~~~~~~~
    pub fn new(
        _application_context: ApplicationContext,
        _client: &C,
        _options: &ClientOptions,
    ) -> Self {
        Self {
            temp: std::marker::PhantomData,
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
}
