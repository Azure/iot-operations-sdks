// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

//! Types for Schema Registry operations.

use core::fmt::Debug;
use std::{collections::HashMap, fmt};

use azure_iot_operations_protocol::{common::aio_protocol_error::AIOProtocolError, rpc_command};
use derive_builder::Builder;
use thiserror::Error;

use schemaregistry_gen::schema_registry::client as sr_client_gen;

/// Schema Registry Client implementation wrapper
mod client;
/// Schema Registry generated code
mod schemaregistry_gen;

pub use client::Client;

/// The default schema version to use if not provided.
const DEFAULT_SCHEMA_VERSION: &str = "1";

// ~~~~~~~~~~~~~~~~~~~SDK Created Structs~~~~~~~~~~~~~~~~~~~~~~~~

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
    AIOProtocolError(#[from] AIOProtocolError),
    /// An argument provided for a request was invalid.
    #[error(transparent)]
    InvalidRequestArgument(#[from] rpc_command::invoker::RequestBuilderError),
    /// An error was returned by the Schema Registry Service.
    #[error("{0:?}")]
    ServiceError(#[from] ServiceError),
}

// ~~~~~~~~~~~~~~~~~~~DTDL Equivalent Error~~~~~~~

/// Error codes for schema operations.
#[derive(Debug, Clone)]
#[repr(i32)]
pub enum ErrorCode {
    /// Bad request error.
    BadRequest = 400,
    /// Internal server error.
    InternalError = 500,
    /// Not found error.
    NotFound = 404,
}

/// Additional details about the error.
#[derive(Debug, Clone)]
pub struct ErrorDetails {
    /// Multi-part error code for classification and root causing of errors (e.g., '400.200').
    pub code: Option<String>,
    /// Correlation ID for tracing the error across systems.
    pub correlation_id: Option<String>,
    /// Human-readable helpful error message to provide additional context for the error
    pub message: Option<String>,
}

/// Target of the error
#[derive(Debug, Clone)]
pub enum ErrorTarget {
    /// Schema description
    DescriptionProperty,
    /// Schema display name
    DisplayNameProperty,
    /// Schema format
    FormatProperty,
    /// Schema name
    NameProperty,
    /// Schema ARM resource
    SchemaArmResource,
    /// Content of the schema
    SchemaContentProperty,
    /// Schema registry ARM resource
    SchemaRegistryArmResource,
    /// Schema type
    SchemaTypeProperty,
    /// Schema version ARM resource
    SchemaVersionArmResource,
    /// Tags of the schema
    TagsProperty,
    /// Version of the schema
    VersionProperty,
}

/// Error object for schema operations
#[derive(Debug, Clone)]
pub struct ServiceError {
    /// Error code for classification of errors (ex: '400', '404', '500', etc.).
    pub code: ErrorCode,
    /// Additional details about the error, if available.
    pub details: Option<ErrorDetails>,
    /// Inner error object for nested errors, if applicable.
    pub inner_error: Option<ErrorDetails>,
    /// Human-readable error message.
    pub message: String,
    /// Target of the error, if applicable (e.g., 'schemaType').
    pub target: Option<ErrorTarget>,
}

impl From<sr_client_gen::SchemaRegistryErrorCode> for ErrorCode {
    fn from(code: sr_client_gen::SchemaRegistryErrorCode) -> Self {
        match code {
            sr_client_gen::SchemaRegistryErrorCode::BadRequest => ErrorCode::BadRequest,
            sr_client_gen::SchemaRegistryErrorCode::InternalError => ErrorCode::InternalError,
            sr_client_gen::SchemaRegistryErrorCode::NotFound => ErrorCode::NotFound,
        }
    }
}

impl From<sr_client_gen::SchemaRegistryErrorDetails> for ErrorDetails {
    fn from(details: sr_client_gen::SchemaRegistryErrorDetails) -> Self {
        ErrorDetails {
            code: details.code,
            correlation_id: details.correlation_id,
            message: details.message,
        }
    }
}

impl From<sr_client_gen::SchemaRegistryErrorTarget> for ErrorTarget {
    fn from(target: sr_client_gen::SchemaRegistryErrorTarget) -> Self {
        match target {
            sr_client_gen::SchemaRegistryErrorTarget::DescriptionProperty => {
                ErrorTarget::DescriptionProperty
            }
            sr_client_gen::SchemaRegistryErrorTarget::DisplayNameProperty => {
                ErrorTarget::DisplayNameProperty
            }
            sr_client_gen::SchemaRegistryErrorTarget::FormatProperty => ErrorTarget::FormatProperty,
            sr_client_gen::SchemaRegistryErrorTarget::NameProperty => ErrorTarget::NameProperty,
            sr_client_gen::SchemaRegistryErrorTarget::SchemaArmResource => {
                ErrorTarget::SchemaArmResource
            }
            sr_client_gen::SchemaRegistryErrorTarget::SchemaContentProperty => {
                ErrorTarget::SchemaContentProperty
            }
            sr_client_gen::SchemaRegistryErrorTarget::SchemaRegistryArmResource => {
                ErrorTarget::SchemaRegistryArmResource
            }
            sr_client_gen::SchemaRegistryErrorTarget::SchemaTypeProperty => {
                ErrorTarget::SchemaTypeProperty
            }
            sr_client_gen::SchemaRegistryErrorTarget::SchemaVersionArmResource => {
                ErrorTarget::SchemaVersionArmResource
            }
            sr_client_gen::SchemaRegistryErrorTarget::TagsProperty => ErrorTarget::TagsProperty,
            sr_client_gen::SchemaRegistryErrorTarget::VersionProperty => {
                ErrorTarget::VersionProperty
            }
        }
    }
}

impl From<sr_client_gen::SchemaRegistryError> for ServiceError {
    fn from(error: sr_client_gen::SchemaRegistryError) -> Self {
        ServiceError {
            code: error.code.into(),
            details: error.details.map(Into::into),
            inner_error: error.inner_error.map(Into::into),
            message: error.message,
            target: error.target.map(Into::into),
        }
    }
}

impl fmt::Display for ServiceError {
    fn fmt(&self, f: &mut fmt::Formatter<'_>) -> fmt::Result {
        write!(f, "{}", self.message)
    }
}

impl std::error::Error for ServiceError {
    fn source(&self) -> Option<&(dyn std::error::Error + 'static)> {
        None
    }
}

// ~~~~~~~~~~~~~~~~~~~DTDL Equivalent Structs and Enums~~~~~~~

/// Supported schema formats
#[derive(Debug, Clone, PartialEq, Eq)]
pub enum Format {
    /// Delta/1.0
    Delta1,
    /// JsonSchema/draft-07
    JsonSchemaDraft07,
}

/// Supported schema types.
#[derive(Debug, Clone, PartialEq, Eq)]
pub enum SchemaType {
    /// Message Schema
    MessageSchema,
}

// TODO: Implement proper Equality for schema_content. At this point, it is just a string comparison.
/// Schema object
#[derive(Debug, Clone, PartialEq, Eq)]
pub struct Schema {
    /// Human-readable description of the schema.
    pub description: Option<String>,
    /// Human-readable display name.
    pub display_name: Option<String>,
    /// Format of the schema.
    pub format: Format,
    /// Hash of the schema content.
    pub hash: Option<String>,
    /// Schema name.
    pub name: String,
    /// Schema registry namespace. Uniquely identifies a schema registry within a tenant.
    pub namespace: String,
    /// Content stored in the schema.
    pub schema_content: String,
    /// Type of the schema.
    pub schema_type: SchemaType,
    /// Schema tags.
    pub tags: HashMap<String, String>,
    /// Version of the schema. Allowed between 0-9.
    pub version: String,
}

// TODO: Implement proper Equality for schema_content. At this point, it is just a string comparison.
/// Request to put a schema in the schema registry.
#[derive(Builder, Clone, Debug, PartialEq, Eq)]
#[builder(setter(into), build_fn(validate = "Self::validate"))]
pub struct PutSchemaRequest {
    /// Human-readable description of the schema.
    #[builder(default)]
    pub description: Option<String>,
    /// Human-readable display name.
    #[builder(default)]
    pub display_name: Option<String>,
    /// The format of the schema.
    pub format: Format,
    /// Content stored in the schema.
    pub schema_content: String,
    /// Type of the schema.
    #[builder(default = "SchemaType::MessageSchema")]
    pub schema_type: SchemaType,
    /// Schema tags.
    #[builder(default)]
    pub tags: HashMap<String, String>,
    /// Version of the schema. Allowed between 0-9.
    #[builder(default = "DEFAULT_SCHEMA_VERSION.to_string()")]
    pub version: String,
}

impl PutSchemaRequestBuilder {
    /// Validate the [`PutSchemaRequest`].
    ///
    /// # Errors
    /// Returns a `String` describing the error if `display_name`, `schema_content`, or `version` is empty.
    fn validate(&self) -> Result<(), String> {
        if let Some(Some(display_name)) = &self.display_name {
            if display_name.is_empty() {
                return Err("display_name cannot be empty".to_string());
            }
        }

        if let Some(version) = &self.version {
            if version.is_empty() {
                return Err("version cannot be empty".to_string());
            }
        }

        if let Some(schema_content) = &self.schema_content {
            if schema_content.is_empty() {
                return Err("schema_content cannot be empty".to_string());
            }
        }

        Ok(())
    }
}

/// Request to get a schema from the schema registry.
#[derive(Builder, Clone, Debug, PartialEq, Eq)]
#[builder(setter(into), build_fn(validate = "Self::validate"))]
pub struct GetSchemaRequest {
    /// Schema name.
    name: String,
    /// Version of the schema. Allowed between 0-9.
    #[builder(default = "DEFAULT_SCHEMA_VERSION.to_string()")]
    version: String,
}

impl GetSchemaRequestBuilder {
    /// Validate the [`GetRequest`].
    ///
    /// # Errors
    /// Returns a `String` describing the error if `name` or `version` is empty.
    fn validate(&self) -> Result<(), String> {
        if let Some(name) = &self.name {
            if name.is_empty() {
                return Err("name cannot be empty".to_string());
            }
        }

        if let Some(version) = &self.version {
            if version.is_empty() {
                return Err("version cannot be empty".to_string());
            }
        }

        Ok(())
    }
}

impl From<Format> for sr_client_gen::Format {
    fn from(format: Format) -> Self {
        match format {
            Format::Delta1 => sr_client_gen::Format::Delta1,
            Format::JsonSchemaDraft07 => sr_client_gen::Format::JsonSchemaDraft07,
        }
    }
}

impl From<sr_client_gen::Format> for Format {
    fn from(format: sr_client_gen::Format) -> Self {
        match format {
            sr_client_gen::Format::Delta1 => Format::Delta1,
            sr_client_gen::Format::JsonSchemaDraft07 => Format::JsonSchemaDraft07,
        }
    }
}

impl From<SchemaType> for sr_client_gen::SchemaType {
    fn from(schema_type: SchemaType) -> Self {
        match schema_type {
            SchemaType::MessageSchema => sr_client_gen::SchemaType::MessageSchema,
        }
    }
}

impl From<sr_client_gen::SchemaType> for SchemaType {
    fn from(schema_type: sr_client_gen::SchemaType) -> Self {
        match schema_type {
            sr_client_gen::SchemaType::MessageSchema => SchemaType::MessageSchema,
        }
    }
}
impl From<sr_client_gen::Schema> for Schema {
    fn from(schema: sr_client_gen::Schema) -> Self {
        Schema {
            description: schema.description,
            display_name: schema.display_name,
            format: schema.format.into(),
            hash: schema.hash,
            name: schema.name,
            namespace: schema.namespace,
            schema_content: schema.schema_content,
            schema_type: schema.schema_type.into(),
            tags: schema.tags.unwrap_or_default(),
            version: schema.version,
        }
    }
}
