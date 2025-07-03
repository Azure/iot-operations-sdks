// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

//! Connector framework for Azure IoT Operations

#![warn(missing_docs)]

use azure_iot_operations_protocol::common::hybrid_logical_clock::HybridLogicalClock;
use azure_iot_operations_services::{
    azure_device_registry,
    schema_registry::{PutRequest, PutRequestBuilder, PutRequestBuilderError},
};

pub mod base_connector;
pub mod data_processor;
pub mod deployment_artifacts;
pub mod destination_endpoint;
pub mod source_endpoint;

#[macro_use]
extern crate derive_getters;

/// Message Schema to send to the Schema Registry Service
pub type MessageSchema = PutRequest;
/// Config Error type used with the Azure Device Registry Service
pub type AdrConfigError = azure_device_registry::ConfigError;
/// Builder for [`MessageSchema`]
pub type MessageSchemaBuilder = PutRequestBuilder;
/// Error type for [`MessageSchemaBuilder`]
pub type MessageSchemaBuilderError = PutRequestBuilderError;

/// Struct format for data sent to the destination
#[derive(Debug, Clone, PartialEq)]
/// Struct format for data sent to the [`DataTransformer`] and the destination
pub struct Data {
    /// The payload in raw bytes
    pub payload: Vec<u8>,
    /// The content type of the payload. May be ignored depending on the destination
    pub content_type: String,
    /// Any custom user data related to the payload. May be ignored depending on the destination
    pub custom_user_data: Vec<(String, String)>,
    /// Timestamp of the actual data. May be ignored depending on the destination
    /// May be removed in the near future. May not be Option in the near future
    pub timestamp: Option<HybridLogicalClock>,
}

/// Represents a dataset associated with a specific device, endpoint, and asset.
#[derive(Debug, Clone, PartialEq, Eq, Hash)]
pub struct DatasetRef {
    /// The name of the dataset
    pub dataset_name: String,
    /// The name of the asset
    pub asset_name: String,
    /// The name of the device
    pub device_name: String,
    /// The name of the endpoint
    pub inbound_endpoint_name: String,
}
