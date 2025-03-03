// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

//! Types for Schema Registry operations.

use core::fmt::Debug;

use azure_iot_operations_protocol::common::aio_protocol_error::{
    AIOProtocolError, AIOProtocolErrorKind,
};
use thiserror::Error;

pub use schemaregistry_gen::schema_registry::client::{Format, Schema, SchemaType};

mod client;
mod schemaregistry_gen;

pub use client::{Client, GetRequest, GetRequestBuilder, PutRequest, PutRequestBuilder};

/// Represents an error that occurred in the Azure IoT Operations Schema Registry Client implementation.
#[derive(Debug, Error)]
#[error(transparent)]
pub struct SchemaRegistryError(#[from] SchemaRegistryErrorKind);

impl SchemaRegistryError {
    /// Returns the [`SchemaRegistryErrorKind`] of the error.
    #[must_use]
    pub fn kind(&self) -> &SchemaRegistryErrorKind {
        &self.0
    }
}

/// Represents the kinds of errors that occur in the Azure IoT Operations Schema Registry implementation.
#[derive(Error, Debug)]
#[allow(clippy::large_enum_variant)]
pub enum SchemaRegistryErrorKind {
    /// An error occurred in the AIO Protocol. See [`AIOProtocolError`] for more information.
    #[error(transparent)]
    AIOProtocolError(AIOProtocolError),
    /// An error occurred during serialization of a request.
    #[error("{0}")]
    SerializationError(String),
    /// An argument provided for a request was invalid.
    #[error("{0}")]
    InvalidArgument(String),
    /// An error was returned by the Schema Registry Service.
    #[error("{0:?}")]
    ServiceError(ServiceError),
}

impl From<AIOProtocolError> for SchemaRegistryErrorKind {
    fn from(error: AIOProtocolError) -> Self {
        match error.kind {
            AIOProtocolErrorKind::UnknownError => {
                SchemaRegistryErrorKind::ServiceError(ServiceError {
                    message: error.message.unwrap_or_else(|| "Unknown error".to_string()),
                    property_name: error.header_name,
                    property_value: error.header_value,
                })
            }
            AIOProtocolErrorKind::ExecutionException => {
                SchemaRegistryErrorKind::ServiceError(ServiceError {
                    message: error
                        .message
                        .unwrap_or_else(|| "Execution Exception".to_string()),
                    property_name: None,
                    property_value: None,
                })
            }
            _ => SchemaRegistryErrorKind::AIOProtocolError(error),
        }
    }
}

/// An error returned by the Schema Registry Service.
#[derive(Debug)]
pub struct ServiceError {
    /// The error message.
    pub message: String,
    /// The name of the property associated with the error, if present.
    pub property_name: Option<String>,
    /// The value of the property associated with the error, if present.
    pub property_value: Option<String>,
}
