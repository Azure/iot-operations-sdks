// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

//! Connector framework for Azure IoT Operations

#![warn(missing_docs)]

use azure_iot_operations_protocol::common::hybrid_logical_clock::HybridLogicalClock;
use azure_iot_operations_services::schema_registry::PutRequest;

pub mod base_connector;
pub mod data_transformer;
pub mod data_transformer2;
pub mod sample_data_pipeline;
pub mod destination_endpoint;
pub mod filemount;
pub mod source_endpoint;

/// Message Schema to send to the Schema Registry Service
pub type MessageSchema = PutRequest;

/// Struct format for data sent to the [`DataTransformer`] and the destination
pub struct Data {
    /// The payload in raw bytes
    pub payload: Vec<u8>,
    /// The content type of the payload. May be ignored depending on the destination
    pub content_type: Option<String>,
    /// Any custom user data related to the payload. May be ignored depending on the destination
    pub custom_user_data: Vec<(String, String)>,
    /// Timestamp of the actual data. May be ignored depending on the destination
    /// May be removed in the near future. May not be Option in the near future
    pub timestamp: Option<HybridLogicalClock>,
}
