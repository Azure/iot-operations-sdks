// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

//! Types for Edge Registry (xRegistry) operations.

use azure_iot_operations_protocol::common::aio_protocol_error::AIOProtocolError;
use azure_iot_operations_protocol::rpc_command;
use data_encoding::HEXLOWER;
use sha2::{Digest, Sha256};
use thiserror::Error;

use crate::edge_registry::edge_registry_gen::edge_registry::client as client_gen;

/// Edge Registry generated code
mod edge_registry_gen;

/// Edge Registry Client implementation wrapper
pub mod client;
pub mod models;

pub use client::Client;

// `client_gen::JSON_LD11` cannot be used in the models: the generated `client` module flattens both
// the Thing Description and Thing Model `JSON_LD11` consts into one namespace (and their source
// modules are private), so the identifier is ambiguous.
// TODO: consider generating the format identifiers into separate namespaces to avoid this ambiguity.
/// JSON-LD 1.1 format.
const JSON_LD11: &str = "JsonLD/1.1";

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
#[non_exhaustive]
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

impl From<rpc_command::invoker::Response<client_gen::SchemaExtensionError>> for ErrorKind {
    fn from(value: rpc_command::invoker::Response<client_gen::SchemaExtensionError>) -> Self {
        Self::ServiceError(value.payload.into())
    }
}

impl From<rpc_command::invoker::Response<client_gen::ThingDescriptionExtensionError>>
    for ErrorKind
{
    fn from(
        value: rpc_command::invoker::Response<client_gen::ThingDescriptionExtensionError>,
    ) -> Self {
        Self::ServiceError(value.payload.into())
    }
}

impl From<rpc_command::invoker::Response<client_gen::ThingModelExtensionError>> for ErrorKind {
    fn from(value: rpc_command::invoker::Response<client_gen::ThingModelExtensionError>) -> Self {
        Self::ServiceError(value.payload.into())
    }
}

impl From<client_gen::SchemaExtensionError> for client_gen::EdgeRegistryError {
    fn from(value: client_gen::SchemaExtensionError) -> Self {
        client_gen::EdgeRegistryError {
            code: value.code,
            detail: value.detail,
            source: value.source,
            status: value.status,
            subject: value.subject,
            title: value.title,
            r#type: value.r#type,
        }
    }
}

impl From<client_gen::ThingDescriptionExtensionError> for client_gen::EdgeRegistryError {
    fn from(value: client_gen::ThingDescriptionExtensionError) -> Self {
        client_gen::EdgeRegistryError {
            code: value.code,
            detail: value.detail,
            source: value.source,
            status: value.status,
            subject: value.subject,
            title: value.title,
            r#type: value.r#type,
        }
    }
}

impl From<client_gen::ThingModelExtensionError> for client_gen::EdgeRegistryError {
    fn from(value: client_gen::ThingModelExtensionError) -> Self {
        client_gen::EdgeRegistryError {
            code: value.code,
            detail: value.detail,
            source: value.source,
            status: value.status,
            subject: value.subject,
            title: value.title,
            r#type: value.r#type,
        }
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
pub enum GetVersionId<T> {
    /// Retrieve the default Version of the Resource.
    #[default]
    ResourceDefault,
    /// Retrieve the Version with the specified identifier.
    Specified(T),
}

impl<T> From<GetVersionId<T>> for Option<T> {
    fn from(value: GetVersionId<T>) -> Self {
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

// ~~~~~~~~~~~~~~~~~~~SDK Created Helper Functions~~~~~~~~~~~~~~~~~~~~~~~~

/// The label key under which the original, pre-derivation identifier is recorded by
/// [`derive_resource_id`].
///
/// A lookup filters on this key with the original identifier as the value, not the derived
/// Resource identifier.
pub const ORIGINAL_ID_LABEL_KEY: &str = "originalid";

/// Derives a conforming Resource identifier from an arbitrary identifier, recording the original
/// in `labels` under [`ORIGINAL_ID_LABEL_KEY`].
///
/// Any existing [`ORIGINAL_ID_LABEL_KEY`] entry is replaced, so repeated calls against the same
/// `labels` do not accumulate duplicates.
///
/// # Example
/// ```
/// # use azure_iot_operations_services::edge_registry::{
/// #     ORIGINAL_ID_LABEL_KEY, derive_resource_id,
/// # };
/// let mut labels = vec![];
/// let resource_id = derive_resource_id("urn:azureiot:aio:dev:ep:opcua:asset:td.g", &mut labels);
///
/// assert_eq!(resource_id.len(), 64);
/// assert_eq!(labels[0].key, ORIGINAL_ID_LABEL_KEY);
/// assert_eq!(labels[0].value, "urn:azureiot:aio:dev:ep:opcua:asset:td.g");
/// ```
#[must_use]
pub fn derive_resource_id(original_id: &str, labels: &mut Vec<Label>) -> String {
    labels.retain(|label| label.key != ORIGINAL_ID_LABEL_KEY);
    labels.push(Label {
        key: ORIGINAL_ID_LABEL_KEY.to_string(),
        value: original_id.to_string(),
    });

    HEXLOWER.encode(&Sha256::digest(original_id.as_bytes()))
}

#[cfg(test)]
mod tests {
    use super::*;

    /// Pins the derivation to `xregistry_core::compute_document_hash` in the Edge Registry
    /// service. Every SDK must produce this value for identifiers to be reproducible across
    /// implementations.
    #[test]
    fn derives_lowercase_sha256_hex() {
        assert_eq!(
            derive_resource_id("test-document", &mut vec![]),
            "b72686d533cb3987150ab6455021dfed113a2a538d7421c8ef40cbdf02543831"
        );
    }

    #[test]
    fn derived_identifier_conforms_to_the_cloud_naming_rules() {
        for original_id in [
            "a",
            "urn:azureiot:aio:dev:ep:opcua:asset:td.g",
            &"x".repeat(512),
        ] {
            let resource_id = derive_resource_id(original_id, &mut vec![]);

            assert_eq!(resource_id.len(), 64);
            assert!(
                resource_id
                    .chars()
                    .all(|c| c.is_ascii_lowercase() || c.is_ascii_digit())
            );
        }
    }

    #[test]
    fn derivation_is_deterministic() {
        assert_eq!(
            derive_resource_id("some-id", &mut vec![]),
            derive_resource_id("some-id", &mut vec![])
        );
    }

    #[test]
    fn records_the_original_id_verbatim() {
        let mut labels = vec![];
        let _ = derive_resource_id("Some:Original@Id_", &mut labels);

        assert_eq!(labels.len(), 1);
        assert_eq!(labels[0].key, ORIGINAL_ID_LABEL_KEY);
        assert_eq!(labels[0].value, "Some:Original@Id_");
    }

    #[test]
    fn replaces_an_existing_original_id_label() {
        let mut labels = vec![Label {
            key: ORIGINAL_ID_LABEL_KEY.to_string(),
            value: "stale".to_string(),
        }];
        let _ = derive_resource_id("current", &mut labels);

        assert_eq!(labels.len(), 1);
        assert_eq!(labels[0].value, "current");
    }

    #[test]
    fn preserves_unrelated_labels() {
        let mut labels = vec![
            Label {
                key: "first".to_string(),
                value: "1".to_string(),
            },
            Label {
                key: "second".to_string(),
                value: "2".to_string(),
            },
        ];
        let _ = derive_resource_id("some-id", &mut labels);

        assert_eq!(labels.len(), 3);
        assert_eq!(labels[0].key, "first");
        assert_eq!(labels[1].key, "second");
        assert_eq!(labels[2].key, ORIGINAL_ID_LABEL_KEY);
    }
}
