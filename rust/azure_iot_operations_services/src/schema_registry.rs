// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

//! Types for Schema Registry operations.

use core::fmt::Debug;

use azure_iot_operations_protocol::common::aio_protocol_error::AIOProtocolError;
use thiserror::Error;

pub use schemaregistry_gen::dtmi_ms_adr_SchemaRegistry__1::client::{
    Enum_Ms_Adr_SchemaRegistry_Format__1, Enum_Ms_Adr_SchemaRegistry_SchemaType__1,
    Object_Ms_Adr_SchemaRegistry_Schema__1,
};

mod client;
mod schemaregistry_gen;

pub use client::{Client, GetRequest, GetRequestBuilder, PutRequest, PutRequestBuilder};

/// Represents the stored schema payload.
pub type Schema = Object_Ms_Adr_SchemaRegistry_Schema__1;
/// Represents the encoding used to store the schema. It specifies how the schema content
/// should be interpreted.
pub type Format = Enum_Ms_Adr_SchemaRegistry_Format__1;
/// Represents the type of the schema.
pub type SchemaType = Enum_Ms_Adr_SchemaRegistry_SchemaType__1;

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
    AIOProtocolError(#[from] AIOProtocolError),
    /// An error occurred during serialization of a request.
    #[error("{0}")]
    SerializationError(String),
    /// An argument provided for a request was invalid.
    #[error("{0}")]
    InvalidArgument(String),
}
