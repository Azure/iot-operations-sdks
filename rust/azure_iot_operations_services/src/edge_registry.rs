// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

//! Types for Edge Registry (xRegistry) operations.

use azure_iot_operations_protocol::common::aio_protocol_error::AIOProtocolError;
use azure_iot_operations_protocol::rpc_command;
use thiserror::Error;

use crate::edge_registry::edge_registry_gen::edge_registry::client as client_gen;

/// Edge Registry generated code
mod edge_registry_gen;

/// Edge Registry Client implementation wrapper
pub mod client;
pub mod models;

pub use client::Client;

// ~~~~~~~~~~~~~~~~~~~SDK Created Structs~~~~~~~~~~~~~~~~~~~~~~~~

/// Represents an error that occurred in the Azure IoT Operations Edge Registry Client implementation.
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

/// Represents the kinds of errors that occur in the Azure IoT Operations Edge Registry implementation.
#[derive(Debug, Error)]
pub enum ErrorKind {
    /// An error occurred in the AIO Protocol. See [`AIOProtocolError`] for more information.
    #[error(transparent)]
    AIOProtocolError(#[from] AIOProtocolError),
    /// An error was returned by the Edge Registry service.
    #[error("{0:?}")]
    ServiceError(client_gen::EdgeRegistryError),
    /// An argument provided for a request was invalid.
    #[error("{0}")]
    ValidationError(String),
    /// An error occurred while shutting down the Edge Registry Client.
    #[error("Shutdown error occurred with the following protocol errors: {0:?}")]
    ShutdownError(Vec<AIOProtocolError>),
}

impl From<rpc_command::invoker::Response<client_gen::EdgeRegistryError>> for ErrorKind {
    fn from(value: rpc_command::invoker::Response<client_gen::EdgeRegistryError>) -> Self {
        Self::ServiceError(value.payload)
    }
}

impl From<rpc_command::invoker::RequestBuilderError> for ErrorKind {
    fn from(e: rpc_command::invoker::RequestBuilderError) -> Self {
        ErrorKind::ValidationError(e.to_string())
    }
}

// ~~~~~~~~~~~~~~~~~~~SDK Created Helper Structs~~~~~~~~~~~~~~~~~~~~~~~~

/// Identifies a Group within its Group type for a request.
#[derive(Debug, Clone, Default)]
pub enum GroupId {
    /// Use the cloud default Group Id of the Group type.
    #[default]
    CloudDefault,
    /// Use the Group with the specified identifier.
    Specified(String),
}

impl From<GroupId> for Option<String> {
    fn from(value: GroupId) -> Self {
        match value {
            GroupId::CloudDefault => None,
            GroupId::Specified(id) => Some(id),
        }
    }
}

/// Identifies which Version of a Resource to retrieve.
#[derive(Debug, Clone, Default)]
pub enum GetVersionId {
    /// Retrieve the default Version of the Resource.
    #[default]
    ResourceDefault,
    /// Retrieve the Version with the specified identifier.
    Specified(String),
}

impl From<GetVersionId> for Option<String> {
    fn from(value: GetVersionId) -> Self {
        match value {
            GetVersionId::ResourceDefault => None,
            GetVersionId::Specified(id) => Some(id),
        }
    }
}

/// Identifies the Version identifier to assign when creating a Version.
#[derive(Debug, Clone, Default)]
pub enum CreateVersionId {
    /// Let the server assign the Version identifier.
    #[default]
    ServerAssigned,
    /// Create the Version with this specific Version identifier.
    Specified(String),
}

impl From<CreateVersionId> for Option<String> {
    fn from(value: CreateVersionId) -> Self {
        match value {
            CreateVersionId::ServerAssigned => None,
            CreateVersionId::Specified(id) => Some(id),
        }
    }
}

/// Selects which Groups a label query spans.
pub enum GroupQuery {
    /// Search across all Group types. There is no default Group without a fixed Group type, so only
    /// "all Groups" or a specific Group id may be selected.
    AllGroupTypes(AnyGroupSelection),
    /// Search within a single Group type.
    GroupType {
        /// The Group type to search within.
        group_type: String,
        /// The Groups of that type to search.
        groups: GroupSelection,
    },
}

/// Group selection when no Group type is fixed.
pub enum AnyGroupSelection {
    /// All Groups of every type.
    All,
    /// Groups with this id, across all Group types.
    GroupId(String),
}

/// Group selection within a fixed Group type.
pub enum GroupSelection {
    /// All Groups of the type.
    All,
    /// The Group with this id.
    GroupId(String),
    /// The default Group of the type.
    Default,
}

/// A label key/value pair used to filter list queries.
#[derive(Debug, Clone)]
pub struct Label {
    /// The label key.
    pub key: String,
    /// The label value.
    pub value: String,
}

impl From<Label> for client_gen::Label {
    fn from(value: Label) -> Self {
        client_gen::Label {
            key: value.key,
            value: value.value,
        }
    }
}

impl From<client_gen::Label> for Label {
    fn from(value: client_gen::Label) -> Self {
        Self {
            key: value.key,
            value: value.value,
        }
    }
}
