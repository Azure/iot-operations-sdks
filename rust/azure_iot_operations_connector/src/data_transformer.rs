// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

//! Traits, types, and implementations for Azure IoT Operations Connector Data Transformers.

#![allow(missing_docs)]

use std::sync::Arc;

use azure_iot_operations_mqtt::interface::CompletionToken;
use azure_iot_operations_services::azure_device_registry::{AssetDataset, AssetDatasetDataPoint};

use crate::destination_endpoint::Forwarder;
use crate::{Data, base_connector::managed_azure_device_registry::Reporter};

pub trait DataTransformer {
    // TODO: rename
    type MyDatasetDataTransformer: DatasetDataTransformer;
    fn new_dataset_data_transformer(
        &self,
        dataset_definition: AssetDataset,
        forwarder: Forwarder,
        reporter: Arc<Reporter>,
    ) -> Self::MyDatasetDataTransformer;
}

pub trait DatasetDataTransformer {
    // optionally include specific datapoint that this data is for
    fn add_sampled_data(
        &self,
        data: Data,
        datapoint: Option<AssetDatasetDataPoint>,
    ) -> impl std::future::Future<Output = Result<CompletionToken, String>> + Send;
}

pub struct DefaultDataTransformer {
    forwarder: Forwarder,
}
impl DefaultDataTransformer {
    #[must_use]
    pub fn new(
        _dataset_definition: AssetDataset,
        forwarder: Forwarder,
        _reporter: Arc<Reporter>,
    ) -> Self {
        Self { forwarder }
    }

    /// # Errors
    /// TODO
    pub async fn add_sampled_data(
        &self,
        data: Data,
        _datapoint: Option<AssetDatasetDataPoint>,
    ) -> Result<CompletionToken, String> {
        // immediately forward data without any processing
        self.forwarder.send_data(data).await
    }
}
