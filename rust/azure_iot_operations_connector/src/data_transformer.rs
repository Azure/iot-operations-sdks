// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

//! Traits, types, and implementations for Azure IoT Operations Connector Data Transformers.

#![allow(missing_docs)]

use std::sync::Arc;

use azure_iot_operations_services::azure_device_registry::Dataset;

use crate::destination_endpoint::Forwarder;
use crate::{Data, base_connector::managed_azure_device_registry::Reporter};

pub trait DataTransformer {
    // TODO: rename
    type MyDatasetDataTransformer: DatasetDataTransformer;
    fn new_dataset_data_transformer(
        &self,
        dataset_definition: Dataset,
        forwarder: Forwarder,
        reporter: Arc<Reporter>,
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
        _dataset_definition: Dataset,
        forwarder: Forwarder,
        _reporter: Arc<Reporter>,
    ) -> Self::MyDatasetDataTransformer {
        Self::MyDatasetDataTransformer { forwarder }
    }
}

pub struct PassthroughDatasetDataTransformer {
    forwarder: Forwarder,
}
impl DatasetDataTransformer for PassthroughDatasetDataTransformer {
    /// # Errors
    /// TODO
    async fn add_sampled_data(&self, data: Data) -> Result<(), String> {
        // immediately forward data without any processing
        self.forwarder.send_data(data).await
    }
}
