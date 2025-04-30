use std::sync::Arc;

use azure_iot_operations_mqtt::interface::CompletionToken;
use azure_iot_operations_services::azure_device_registry::AssetDataset;

use crate::{base_connector::managed_azure_device_registry::Reporter, Data};
use crate::destination_endpoint::Forwarder;

pub trait DataTransformer {
  fn new(dataset_definition: AssetDataset, forwarder: Forwarder, reporter: Arc<Reporter>) -> Self;

  async fn add_sampled_data(&self, data: Data) -> Result<CompletionToken, String>;
}

pub struct DefaultDataTransformer {
  forwarder: Forwarder,
}
impl DefaultDataTransformer {
  pub fn new(_dataset_definition: AssetDataset, forwarder: Forwarder, _reporter: Arc<Reporter>) -> Self {
    Self {
      forwarder,
    }
  }
  pub async fn add_sampled_data(&self, data: Data) -> Result<CompletionToken, String> {
    // immediately forward data without any processing
    self.forwarder.send_data(data).await
  }
}