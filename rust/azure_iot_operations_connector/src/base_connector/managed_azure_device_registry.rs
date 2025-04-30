use std::sync::Arc;

use azure_iot_operations_mqtt::interface::{AckToken, CompletionToken};
use azure_iot_operations_services::azure_device_registry::{Asset, AssetDataset, AssetUpdateObservation, ConfigError, Device, DeviceUpdateObservation, MessageSchemaReference};

use crate::{data_transformer::DataTransformer, destination_endpoint::Forwarder, filemount::azure_device_registry::{AssetCreateObservation, AssetDeletionToken, DeviceEndpointCreateObservation}, Data, MessageSchema};

use super::ConnectorContext;

pub struct ManagedDeviceCreateObservation {
  connector_context: ConnectorContext,
  device_endpoint_create_observation: DeviceEndpointCreateObservation
}
impl ManagedDeviceCreateObservation {
    pub fn new(connector_context: ConnectorContext) -> Self {
      let device_endpoint_create_observation = DeviceEndpointCreateObservation::new(connector_context.debounce_duration).unwrap();
      
        Self {
          connector_context,
          device_endpoint_create_observation
        }
    }
    pub fn recv_notification(&self) -> Option<(Device, DeviceUpdateObservation, /*DeviceDeleteToken,*/ ManagedAssetCreateObservation)> {
        // Handle the notification
        // self.device_endpoint_create_observation.recv_notification().await;
        // and then add device update observation to it as well
        // let device_update_observation = connector_context.azure_device_registry_client.observe_device_update_notifications(device_name, inbound_endpoint_name, timeout)
        None
    }
}

pub struct ManagedAssetCreateObservation {
  asset_create_observation: AssetCreateObservation
}
impl ManagedAssetCreateObservation {
  pub fn recv_notification(&self) -> Option<(ManagedAsset, ManagedAssetUpdateObservation, AssetDeletionToken)> {
    // handle the notification
    // add asset update observation
    // create copy of asset with asset and dataset connector functionality (status reporting, data forwarding, create data transformers)
    None
  }
}

pub struct ManagedAssetUpdateObservation {
  asset_update_observation: AssetUpdateObservation
}
impl ManagedAssetUpdateObservation {
  pub fn recv_notification(&self) -> Option<(ManagedAsset, Option<AckToken>)> {
    // handle the notification
    None
  }
}

pub struct ManagedAsset {
  // re-export of adr::Asset, but Dataset/Event/etc structs are of type ConnectorDataset/etc
  pub asset_definition: Asset
}
impl ManagedAsset {
  pub fn report_status(status: Result<(), ConfigError>) {

  }
}

pub struct ManagedDataset<T: DataTransformer> {
  pub dataset_definition: AssetDataset,
  data_transformer: T,
  reporter: Arc<Reporter>,
}
impl<T> ManagedDataset<T> where T: DataTransformer {
  pub fn new(dataset_definition: AssetDataset) -> Self {
    // Create a new dataset
    let forwarder = Forwarder::new(dataset_definition.clone());
    let reporter = Arc::new(Reporter::new(dataset_definition.clone()));
    let data_transformer = T::new(dataset_definition.clone(), forwarder, reporter.clone());
    Self {
      dataset_definition,
      data_transformer,
      reporter
    }
  }
  pub fn report_status(&self, status: Result<Option<MessageSchema>, ConfigError>) -> Result<Option<MessageSchemaReference>, String> {
    // Report the status of the dataset
    self.reporter.report_status(status)
  }
  pub async fn add_sampled_data(&self, data: Data) -> Result<CompletionToken, String> {
    // Add sampled data to the dataset
    self.data_transformer.add_sampled_data(data).await
  }
}

pub struct Reporter {
  message_schema_uri: Option<MessageSchemaReference>,
  message_schema: Option<MessageSchema>,
}
impl Reporter {
  pub fn new(dataset_definition: AssetDataset) -> Self {
    // Create a new forwarder
    Self {
      message_schema_uri: None,
      message_schema: None,
    }
  }
  pub fn report_status(&self, status: Result<Option<MessageSchema>, ConfigError>) -> Result<Option<MessageSchemaReference>, String> {
    // Report the status of the dataset
    Ok(None)
  }
}
