// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

//! Traits, types, and implementations for Azure IoT Operations Connector Data Transformers.

#![allow(missing_docs)]

use crate::{destination_endpoint, Data};
use crate::base_connector::managed_azure_device_registry::DatasetClient;

pub trait DataTransformer {
    // TODO: rename
    type MyDatasetDataTransformer: DatasetDataTransformer;
    fn new_dataset_data_transformer(
        &self,
        dataset: DatasetClient,
    ) -> Self::MyDatasetDataTransformer;
}

pub trait DatasetDataTransformer {
    // optionally include specific datapoint that this data is for
    fn add_sampled_data(
        &self,
        data: Data,
    ) -> impl std::future::Future<Output = Result<(), String>> + Send;
}

pub struct PassthroughDataTransformer {}
impl DataTransformer for PassthroughDataTransformer {
    type MyDatasetDataTransformer = PassthroughDatasetDataTransformer;
    #[must_use]
    fn new_dataset_data_transformer(
        &self,
        dataset: DatasetClient,
        // forwarder: Forwarder,
    ) -> Self::MyDatasetDataTransformer {
        Self::MyDatasetDataTransformer { dataset }
    }
}

pub struct PassthroughDatasetDataTransformer {
    dataset: DatasetClient,
}
impl DatasetDataTransformer for PassthroughDatasetDataTransformer {
    /// # Errors
    /// TODO
    async fn add_sampled_data(&self, data: Data) -> Result<(), destination_endpoint::Error> {
        // immediately forward data without any processing
        self.dataset.forward_data(data).await
    }
}
