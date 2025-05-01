// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

//! Types for Azure IoT Operations Connectors.

use std::sync::Arc;

use azure_iot_operations_mqtt::interface::{AckToken, CompletionToken};
use azure_iot_operations_services::azure_device_registry::{
    Asset, AssetDataset, AssetDatasetDataPoint, AssetUpdateObservation, ConfigError, Device,
    DeviceUpdateObservation, MessageSchemaReference,
};

use crate::{
    Data, MessageSchema,
    data_transformer::{DataTransformer, DatasetDataTransformer},
    destination_endpoint::Forwarder,
    filemount::azure_device_registry::{
        AssetCreateObservation, AssetDeletionToken, DeviceEndpointCreateObservation,
    },
};

use super::ConnectorContext;

/// An Observation for device endpoint creation events that uses
/// multiple underlying clients to get full information for a
/// [`ProtocolTranslator`] to use.
pub struct ManagedDeviceCreateObservation<T: DataTransformer> {
    _connector_context: ConnectorContext<T>,
    _device_endpoint_create_observation: DeviceEndpointCreateObservation,
}
impl<T> ManagedDeviceCreateObservation<T>
where
    T: DataTransformer,
{
    /// Creates a new [`ManagedDeviceCreateObservation`] that uses the given [`ConnectorContext`]
    pub(crate) fn new(connector_context: ConnectorContext<T>) -> Self {
        let device_endpoint_create_observation =
            DeviceEndpointCreateObservation::new(connector_context.debounce_duration).unwrap();

        Self {
            _connector_context: connector_context,
            _device_endpoint_create_observation: device_endpoint_create_observation,
        }
    }

    /// Receives a notification for a newly created device endpoint. This notification includes
    /// the [`ManagedDeviceEndpoint`], a [`ManagedDeviceEndpointUpdateObservation`] to observe for updates on
    /// the new Device, and a [`ManagedAssetCreateObservation`] to observe for newly created
    /// Assets related to this Device
    #[allow(clippy::unused_async)]
    pub async fn recv_notification(
        &self,
    ) -> Option<(
        ManagedDeviceEndpoint<T>,
        ManagedDeviceEndpointUpdateObservation<T>,
        /*DeviceDeleteToken,*/ ManagedAssetCreateObservation<T>,
    )> {
        // Handle the notification
        // self.device_endpoint_create_observation.recv_notification().await;
        // and then add device update observation to it as well
        // let device_update_observation = connector_context.azure_device_registry_client.observe_device_update_notifications(device_name, inbound_endpoint_name, timeout)
        None
    }
}

/// Azure Device Registry Device that includes additional functionality to report status
pub struct ManagedDeviceEndpoint<T: DataTransformer> {
    /// Device definition TODO: derive getter?
    pub device: Device, // TODO: create new struct that only has one endpoint
    _endpoint_name: String, // needed for easy status reporting?
    _data_transformer: Arc<T>,
}
impl<T> ManagedDeviceEndpoint<T>
where
    T: DataTransformer,
{
    /// Used to report the status of a device endpoint
    /// Can report both success or failures for the device and the endpoint separately
    pub fn report_status(
        _device_status: Result<(), ConfigError>,
        _endpoint_status: Result<(), ConfigError>,
    ) {
    }
}

/// An Observation for device endpoint update events that uses
/// multiple underlying clients to get full information for a
/// [`ProtocolTranslator`] to use.
pub struct ManagedDeviceEndpointUpdateObservation<T: DataTransformer> {
    _device_update_observation: DeviceUpdateObservation,
    _data_transformer: Arc<T>,
}
impl<T> ManagedDeviceEndpointUpdateObservation<T>
where
    T: DataTransformer,
{
    /// Receives an updated [`ManagedDeviceEndpoint`] or [`None`] if there will be no more notifications.
    ///
    /// If there are notifications:
    /// - Returns Some([`ManagedDeviceEndpoint`], [`Option<AckToken>`]) on success
    ///     - If auto ack is disabled, the [`AckToken`] should be used or dropped when you want the ack to occur. If auto ack is enabled, you may use ([`ManagedDeviceEndpoint`], _) to ignore the [`AckToken`].
    ///
    /// A received notification can be acknowledged via the [`AckToken`] by calling [`AckToken::ack`] or dropping the [`AckToken`].
    #[allow(clippy::unused_async)]
    pub async fn recv_notification(&self) -> Option<(ManagedDeviceEndpoint<T>, Option<AckToken>)> {
        // handle the notification
        // convert into ManagedDeviceEndpoint
        None
    }
}

/// An Observation for asset creation events that uses
/// multiple underlying clients to get full information for a
/// [`ProtocolTranslator`] to use.
pub struct ManagedAssetCreateObservation<T: DataTransformer> {
    _asset_create_observation: AssetCreateObservation,
    _data_transformer: Arc<T>,
}
impl<T> ManagedAssetCreateObservation<T>
where
    T: DataTransformer,
{
    /// Receives a notification for a newly created asset. This notification includes
    /// the [`ManagedAsset`], a [`ManagedAssetUpdateObservation`] to observe for updates on
    /// the new Asset, and a [`AssetDeletionToken`] to observe for deletion of this Asset
    #[allow(clippy::unused_async)]
    pub async fn recv_notification(
        &self,
    ) -> Option<(
        ManagedAsset<T>,
        ManagedAssetUpdateObservation<T>,
        AssetDeletionToken,
    )> {
        // handle the notification
        // add asset update observation
        // create copy of asset with asset and dataset connector functionality (status reporting, data forwarding, create data transformers)
        None
    }
}

/// An Observation for asset update events that uses
/// multiple underlying clients to get full information for a
/// [`ProtocolTranslator`] to use.
pub struct ManagedAssetUpdateObservation<T: DataTransformer> {
    _asset_update_observation: AssetUpdateObservation,
    _data_transformer: Arc<T>,
}
impl<T> ManagedAssetUpdateObservation<T>
where
    T: DataTransformer,
{
    /// Receives an updated [`ManagedAsset`] or [`None`] if there will be no more notifications.
    ///
    /// If there are notifications:
    /// - Returns Some([`ManagedAsset`], [`Option<AckToken>`]) on success
    ///     - If auto ack is disabled, the [`AckToken`] should be used or dropped when you want the ack to occur. If auto ack is enabled, you may use ([`ManagedAsset`], _) to ignore the [`AckToken`].
    ///
    /// A received notification can be acknowledged via the [`AckToken`] by calling [`AckToken::ack`] or dropping the [`AckToken`].
    #[allow(clippy::unused_async)]
    pub async fn recv_notification(&self) -> Option<(ManagedAsset<T>, Option<AckToken>)> {
        // handle the notification
        None
    }
}

/// Azure Device Registry Asset that includes additional functionality
/// to report status, translate data, and send data to the destination
#[allow(dead_code)]
pub struct ManagedAsset<T: DataTransformer> {
    // re-export of adr::Asset, but Dataset/Event/etc structs are of type ConnectorDataset/etc
    /// Asset Definition
    pub asset_definition: Asset,
    data_transformer: Arc<T>,
    /// datasets associated with the asset. Will be part of [`ManagedAsset`] struct in future, but for now this creates the right dependencies
    pub datasets: Vec<ManagedDataset<T>>,
}
impl<T> ManagedAsset<T>
where
    T: DataTransformer,
{
    /// Used to report the status of an Asset
    pub fn report_status(_status: Result<(), ConfigError>) {}
}

/// Azure Device Registry Dataset that includes additional functionality
/// to report status, translate data, and send data to the destination
pub struct ManagedDataset<T: DataTransformer> {
    /// Dataset Definition
    pub dataset_definition: AssetDataset,
    dataset_data_transformer: T::MyDatasetDataTransformer,
    reporter: Arc<Reporter>,
}
#[allow(dead_code)]
impl<T> ManagedDataset<T>
where
    T: DataTransformer,
{
    pub(crate) fn new(dataset_definition: AssetDataset, data_transformer: &T) -> Self {
        // Create a new dataset
        let forwarder = Forwarder::new(dataset_definition.clone());
        let reporter = Arc::new(Reporter::new(dataset_definition.clone()));
        let dataset_data_transformer = data_transformer.new_dataset_data_transformer(
            dataset_definition.clone(),
            forwarder,
            reporter.clone(),
        );
        Self {
            dataset_definition,
            dataset_data_transformer,
            reporter,
        }
    }
    /// Used to report the status and/or [`MessageSchema`] of an dataset
    /// # Errors
    /// TODO
    pub fn report_status(
        &self,
        status: Result<Option<MessageSchema>, ConfigError>,
    ) -> Result<Option<MessageSchemaReference>, String> {
        // Report the status of the dataset
        self.reporter.report_status(status)
    }

    /// Used to send sampled data to the [`DataTransformer`], which will then send
    /// the transformed data to the destination
    /// # Errors
    /// TODO
    pub async fn add_sampled_data(
        &self,
        data: Data,
        datapoint: Option<AssetDatasetDataPoint>,
    ) -> Result<CompletionToken, String> {
        // Add sampled data to the dataset
        self.dataset_data_transformer
            .add_sampled_data(data, datapoint)
            .await
    }
}

/// Convenience struct to manage reporting the status of a dataset
pub struct Reporter {
    message_schema_uri: Option<MessageSchemaReference>,
    _message_schema: Option<MessageSchema>,
}
#[allow(dead_code)]
impl Reporter {
    pub(crate) fn new(_dataset_definition: AssetDataset) -> Self {
        // Create a new forwarder
        Self {
            message_schema_uri: None,
            _message_schema: None,
        }
    }
    /// Used to report the status and/or [`MessageSchema`] of an dataset
    /// # Errors
    /// TODO
    pub fn report_status(
        &self,
        _status: Result<Option<MessageSchema>, ConfigError>,
    ) -> Result<Option<MessageSchemaReference>, String> {
        // Report the status of the dataset
        Ok(None)
    }

    /// Returns the current message schema URI
    #[must_use]
    pub fn get_current_message_schema_uri(&self) -> Option<MessageSchemaReference> {
        // Get the current message schema URI
        self.message_schema_uri.clone()
    }
}
