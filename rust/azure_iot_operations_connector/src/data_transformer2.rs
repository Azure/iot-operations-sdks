// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

//! Traits, types, and implementations for Azure IoT Operations Connector Data Transformers.

#![allow(missing_docs)]

use std::sync::Arc;

use azure_iot_operations_services::azure_device_registry::AssetDataset;

use crate::destination_endpoint::Forwarder;
use crate::{Data, base_connector::managed_azure_device_registry::Reporter};


// DataTransformerHub / Data Transformer
// DataTransformer / DatasetDataTransformerClient
// DataTransformer / Transform / TransformJson / TransformWasm

// Transform<U> where U is generic type? No, probably not because it's not a type

// JSONTransformer / WASMTransformer : Transform?

// DataTransformerFactory / DataTransformer
// This model would appear to mean that we need to use Box<dyn DataTransform>...

// pub trait DataTransform {
//     fn transform(&self, data: Data) -> Data;
// }

// pub trait DataTransformerFactory<T>
// where T: DataTransform {
//     fn create_transformer(
//         &self,
//         dataset_definition: AssetDataset,
//     ) -> T;
// }

//pub struct MyConnectorDataTransformerFactory

// This model would appear to mean that we need to use Box<dyn DataTransform>...

pub trait DatasetDataTransform {
    async fn transform(&self, data: Data, dataset_definition: &AssetDataset) -> Result<Data, String>; //probably needs to be mut
}

// pub trait DataTransform {
//     fn transform(&self, data: Data) -> Data;
// }

pub struct MyConnectorTransformer {

}

impl DatasetDataTransform for MyConnectorTransformer {
    async fn transform(&self, data: Data, _dataset_definition: &AssetDataset) -> Result<Data, String> {
        Ok(data)
    }
}


struct SampleDataSetClient












// struct DataTransformerClient<T>
// where T: DatasetDataTransform
// {
//     dataset_definition: AssetDataset,
//     transformer: Arc<T>,
// }

// impl<T> DataTransformerClient<T>
// where T: DatasetDataTransform
// {
//     fn new(dataset_definition: AssetDataset, transformer: Arc<T>) -> Self {
//         Self {
//             dataset_definition,
//             transformer,
//         }
//     }

//     async fn transform(&self, data: Data) -> Result<Data, String> {
//         self.transformer.transform(data, &self.dataset_definition).await
//     }
// }






// pub trait DataTransformer {
//     // TODO: rename
//     type MyDatasetDataTransformer: DatasetDataTransformer;
//     fn new_dataset_data_transformer(
//         &self,
//         dataset_definition: AssetDataset,
//         forwarder: Forwarder,
//         reporter: Arc<Reporter>,
//     ) -> Self::MyDatasetDataTransformer;
// }

// pub trait DatasetDataTransformer {
//     // optionally include specific datapoint that this data is for
//     fn add_sampled_data(
//         &self,
//         data: Data,
//     ) -> impl std::future::Future<Output = Result<(), String>> + Send;
// }

// pub struct PassthroughDataTransformer {}
// impl DataTransformer for PassthroughDataTransformer {
//     type MyDatasetDataTransformer = PassthroughDatasetDataTransformer;
//     #[must_use]
//     fn new_dataset_data_transformer(
//         &self,
//         _dataset_definition: AssetDataset,
//         forwarder: Forwarder,
//         _reporter: Arc<Reporter>,
//     ) -> Self::MyDatasetDataTransformer {
//         Self::MyDatasetDataTransformer { forwarder }
//     }
// }

// pub struct PassthroughDatasetDataTransformer {
//     forwarder: Forwarder,
// }
// impl DatasetDataTransformer for PassthroughDatasetDataTransformer {
//     /// # Errors
//     /// TODO
//     async fn add_sampled_data(&self, data: Data) -> Result<(), String> {
//         // immediately forward data without any processing
//         self.forwarder.send_data(data).await
//     }
// }
