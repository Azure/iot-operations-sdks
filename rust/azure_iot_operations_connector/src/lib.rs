// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

//! Connector framework for Azure IoT Operations

#![warn(missing_docs)]

use std::fmt::Display;

use azure_iot_operations_protocol::common::hybrid_logical_clock::HybridLogicalClock;
use azure_iot_operations_services::{
    azure_device_registry,
    schema_registry::{PutSchemaRequest, PutSchemaRequestBuilder, PutSchemaRequestBuilderError},
};

pub mod base_connector;
pub mod data_processor;
pub mod deployment_artifacts;
pub mod destination_endpoint;
pub mod management_action_executor;

#[macro_use]
extern crate derive_getters;

/// Message Schema to send to the Schema Registry Service
pub type MessageSchema = PutSchemaRequest;
/// Reference of an existing Message Schema in the Schema Registry Service
pub use azure_device_registry::models::MessageSchemaReference;
/// Config Error type used with the Azure Device Registry Service
pub type AdrConfigError = azure_device_registry::ConfigError;
/// Builder for [`MessageSchema`]
pub type MessageSchemaBuilder = PutSchemaRequestBuilder;
/// Error type for [`MessageSchemaBuilder`]
pub type MessageSchemaBuilderError = PutSchemaRequestBuilderError;

/// Struct format for data sent to the destination
#[derive(Debug, Clone, PartialEq)]
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

/// Represents the kind of a `DataOperation`
#[derive(Debug, Clone, Copy, PartialEq, Eq, Hash)]
pub enum DataOperationKind {
    /// Dataset
    Dataset,
    /// Event
    Event,
    /// Stream
    Stream,
}

/// Represents the name of a `DataOperation`
#[derive(Debug, Clone, PartialEq, Eq, Hash)]
pub enum DataOperationName {
    /// Dataset
    Dataset {
        /// The name of the dataset
        name: String,
    },
    /// Event
    Event {
        /// The name of the event
        name: String,
        /// The name of the event's parent event group
        event_group_name: String,
    },
    /// Stream
    Stream {
        /// The name of the stream
        name: String,
    },
}

impl Display for DataOperationName {
    fn fmt(&self, f: &mut std::fmt::Formatter<'_>) -> std::fmt::Result {
        match self {
            DataOperationName::Dataset { name } => write!(f, "Dataset: {name}"),
            DataOperationName::Event {
                name,
                event_group_name,
            } => write!(f, "Event: {event_group_name}::{name}"),
            DataOperationName::Stream { name } => write!(f, "Stream: {name}"),
        }
    }
}

/// Represents a `DataOperation` (Dataset, Event, or Stream) associated with a specific device, endpoint, and asset.
#[derive(Debug, Clone, PartialEq, Eq, Hash)]
pub struct DataOperationRef {
    /// The name of the `DataOperation`
    pub data_operation_name: DataOperationName,
    /// The name of the asset
    pub asset_name: String,
    /// The name of the device
    pub device_name: String,
    /// The name of the endpoint
    pub inbound_endpoint_name: String,
}

/// Represents a `ManagementAction` associated with a specific device, endpoint, asset, and management group.
#[derive(Debug, Clone, PartialEq, Eq, Hash)]
pub struct ManagementActionRef {
    /// The name of the management action
    pub management_action_name: String,
    /// The name of the management group
    pub management_group_name: String,
    /// The name of the asset
    pub asset_name: String,
    /// The name of the device
    pub device_name: String,
    /// The name of the endpoint
    pub inbound_endpoint_name: String,
}

impl ManagementActionRef {
    /// Gets the command name for this management action
    pub(crate) fn command_name(&self) -> String {
        format!(
            "{}::{}",
            self.management_group_name, self.management_action_name
        )
    }

    /// Printable name for management action
    #[must_use]
    pub fn name(&self) -> String {
        format!(
            "Management Action: {}::{}",
            self.management_group_name, self.management_action_name
        )
    }
}
