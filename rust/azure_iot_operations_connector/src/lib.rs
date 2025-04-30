// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

//! Connector framework for Azure IoT Operations

#![warn(missing_docs)]

use azure_iot_operations_services::schema_registry::PutRequest;

pub mod base_connector;
pub mod destination_endpoint;
pub mod filemount;
pub mod source_endpoint;
pub mod data_transformer;

pub type MessageSchema = PutRequest;

pub struct Data {
  pub data: Vec<u8>,
  pub content_type: Option<String>,
  pub custom_user_data: Vec<(String, String)>
}