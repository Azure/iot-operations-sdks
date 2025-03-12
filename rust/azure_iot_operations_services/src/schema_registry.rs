// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

//! Types for Schema Registry operations.

use core::fmt::Debug;
use std::collections::HashMap;

use azure_iot_operations_protocol::common::aio_protocol_error::{
    AIOProtocolError, AIOProtocolErrorKind,
};
use derive_builder::Builder;
use thiserror::Error;

pub use schemaregistry_gen::schema_registry::client::{Format, Schema, SchemaType};

/// Schema Registry Client implementation wrapper
mod client;
/// Schema Registry generated code
mod schemaregistry_gen;

pub use client::Client;

/// The default schema version to use if not provided.
const DEFAULT_SCHEMA_VERSION: &str = "1";

/// Represents an error that occurred in the Azure IoT Operations Schema Registry Client implementation.
#[derive(Debug, Error)]
#[error(transparent)]
pub struct Error(#[from] ErrorKind);

impl Error {
    /// Returns the [`ErrorKind`] of the error.
    #[must_use]
    pub fn kind(&self) -> &ErrorKind {
        &self.0
    }
}

/// Represents the kinds of errors that occur in the Azure IoT Operations Schema Registry implementation.
#[derive(Error, Debug)]
#[allow(clippy::large_enum_variant)]
pub enum ErrorKind {
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

impl From<AIOProtocolError> for ErrorKind {
    fn from(error: AIOProtocolError) -> Self {
        match error.kind {
            AIOProtocolErrorKind::UnknownError => ErrorKind::ServiceError(ServiceError {
                message: error.message.unwrap_or_else(|| "Unknown error".to_string()),
                property_name: error.header_name,
                property_value: error.header_value,
            }),
            AIOProtocolErrorKind::ExecutionException => ErrorKind::ServiceError(ServiceError {
                message: error
                    .message
                    .unwrap_or_else(|| "Execution Exception".to_string()),
                property_name: None,
                property_value: None,
            }),
            _ => ErrorKind::AIOProtocolError(error),
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

/// Request to get a schema from the schema registry.
#[derive(Builder, Clone, Debug)]
#[builder(setter(into), build_fn(validate = "Self::validate"))]
pub struct GetRequest {
    /// The unique identifier of the schema to retrieve. Required to locate the schema in the registry.
    id: String,
    /// The version of the schema to fetch. If not specified, defaults to "1".
    #[builder(default = "DEFAULT_SCHEMA_VERSION.to_string()")]
    version: String,
}

impl GetRequestBuilder {
    /// Validate the [`GetRequest`].
    ///
    /// # Errors
    /// Returns a `String` describing the errors if `id` is empty or not provided.
    fn validate(&self) -> Result<(), String> {
        if let Some(id) = &self.id {
            if id.is_empty() {
                return Err("id cannot be empty".to_string());
            }
        }

        Ok(())
    }
}

/// Request to put a schema in the schema registry.
#[derive(Builder, Clone, Debug)]
#[builder(setter(into))]
pub struct PutRequest {
    /// The content of the schema to be added or updated in the registry.
    content: String,
    /// The format of the schema. Specifies how the schema content should be interpreted.
    format: Format,
    /// The type of the schema, such as message schema or data schema.
    #[builder(default = "SchemaType::MessageSchema")]
    schema_type: SchemaType,
    /// Optional metadata tags to associate with the schema. These tags can be used to store additional information about the schema in key-value format.
    #[builder(default)]
    tags: HashMap<String, String>,
    /// The version of the schema to add or update. If not specified, defaults to "1".
    #[builder(default = "DEFAULT_SCHEMA_VERSION.to_string()")]
    version: String,
}
