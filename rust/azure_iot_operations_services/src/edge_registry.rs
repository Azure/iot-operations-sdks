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
/// The label belongs on the Resource, and a lookup filters on this key with the original
/// identifier as the value, not the derived Resource identifier.
pub const ORIGINAL_ID_LABEL_KEY: &str = "originalid";

/// Derives a Resource identifier from an arbitrary identifier, recording the original in `labels`
/// under [`ORIGINAL_ID_LABEL_KEY`] and returning both.
///
/// `original_id` is used as the Resource identifier unchanged when it already satisfies the cloud
/// naming rules: 3 to 64 characters drawn from `[a-z0-9-]`, beginning and ending with an
/// alphanumeric. Every other identifier is hashed, yielding the lowercase hex SHA-256 of
/// `original_id`. A hashed identifier is always 64 characters drawn from `[0-9a-f]`, which
/// satisfies both the xRegistry identifier rules and the stricter cloud rules.
///
/// An identical [`ORIGINAL_ID_LABEL_KEY`] entry is never duplicated, while entries recording a
/// *different* identifier are left in place.
///
/// # Errors
/// [`struct@Error`] of kind [`ValidationError`](ErrorKind::ValidationError) if `original_id` is
/// empty.
///
/// # Example
/// ```
/// # use azure_iot_operations_services::edge_registry::{
/// #     ORIGINAL_ID_LABEL_KEY, derive_resource_id,
/// # };
/// // An identifier that already conforms to the cloud naming rules is used as-is.
/// let (resource_id, resource_labels) = derive_resource_id("asset-td", vec![])?;
/// assert_eq!(resource_id, "asset-td");
///
/// // Every other identifier is hashed.
/// let (resource_id, resource_labels) =
///     derive_resource_id("urn:azureiot:aio:dev:ep:opcua:asset:td.g", vec![])?;
///
/// assert_eq!(resource_id.len(), 64);
/// assert_eq!(resource_labels[0].key, ORIGINAL_ID_LABEL_KEY);
/// assert_eq!(resource_labels[0].value, "urn:azureiot:aio:dev:ep:opcua:asset:td.g");
/// # Ok::<(), azure_iot_operations_services::edge_registry::Error>(())
/// ```
pub fn derive_resource_id(
    original_id: impl Into<String>,
    mut resource_labels: Vec<Label>,
) -> Result<(String, Vec<Label>), Error> {
    let original_id = original_id.into();

    if original_id.is_empty() {
        return Err(ErrorKind::ValidationError("original_id must not be empty".to_string()).into());
    }

    let resource_id = if conforms_to_cloud_naming_rules(&original_id) {
        original_id.clone()
    } else {
        HEXLOWER.encode(&Sha256::digest(original_id.as_bytes()))
    };

    resource_labels
        .retain(|label| !(label.key == ORIGINAL_ID_LABEL_KEY && label.value == original_id));
    resource_labels.push(Label {
        key: ORIGINAL_ID_LABEL_KEY.to_string(),
        value: original_id,
    });

    Ok((resource_id, resource_labels))
}

/// Returns whether `id` satisfies the cloud naming rules: 3 to 64 characters drawn from
/// `[a-z0-9-]`, beginning and ending with an alphanumeric.
fn conforms_to_cloud_naming_rules(id: &str) -> bool {
    let bytes = id.as_bytes();

    (3..=64).contains(&bytes.len())
        && bytes
            .iter()
            .all(|b| b.is_ascii_lowercase() || b.is_ascii_digit() || *b == b'-')
        && bytes.first().is_some_and(u8::is_ascii_alphanumeric)
        && bytes.last().is_some_and(u8::is_ascii_alphanumeric)
}

#[cfg(test)]
mod tests {
    use test_case::test_case;

    use super::*;

    /// Pins the hash to the one the Edge Registry service computes, the lowercase hex SHA-256 of
    /// the input. Every SDK must produce this value for identifiers to be reproducible across
    /// implementations.
    #[test]
    fn hashes_to_lowercase_sha256_hex() {
        let (resource_id, _) = derive_resource_id("Test-Document", vec![]).unwrap();

        assert_eq!(
            resource_id,
            "1ae8481659aaf8fe08cb58818b1793849756b0f526d835e5106cfb76e558cfdd"
        );
    }

    #[test_case("abc"; "shortest permitted")]
    #[test_case("123"; "digits only")]
    #[test_case("a-b-c"; "internal hyphens")]
    #[test_case("opcua-asset-td"; "typical identifier")]
    #[test_case(&"a".repeat(64); "longest permitted")]
    fn uses_a_conforming_identifier_as_the_resource_id(original_id: &str) {
        let (resource_id, resource_labels) = derive_resource_id(original_id, vec![]).unwrap();

        assert_eq!(resource_id, original_id);
        assert_eq!(resource_labels.len(), 1);
        assert_eq!(resource_labels[0].value, original_id);
    }

    #[test_case("ab"; "shorter than permitted")]
    #[test_case(&"a".repeat(65); "longer than permitted")]
    #[test_case("-abc"; "leading hyphen")]
    #[test_case("abc-"; "trailing hyphen")]
    #[test_case("Abc"; "uppercase")]
    #[test_case("a_c"; "underscore")]
    #[test_case("a\u{00f1}b"; "non ascii")]
    #[test_case("urn:azureiot:aio:dev:ep:opcua:asset:td.g"; "wot identifier")]
    #[test_case(&"x".repeat(512); "longer than an xregistry identifier")]
    fn hashes_a_non_conforming_identifier(original_id: &str) {
        let (resource_id, resource_labels) = derive_resource_id(original_id, vec![]).unwrap();

        assert_ne!(resource_id, original_id);
        assert!(conforms_to_cloud_naming_rules(&resource_id));
        assert_eq!(resource_id.len(), 64);
        assert!(
            resource_id
                .chars()
                .all(|c| c.is_ascii_lowercase() || c.is_ascii_digit())
        );
        assert_eq!(resource_labels.len(), 1);
        assert_eq!(resource_labels[0].value, original_id);
    }

    #[test]
    fn derivation_is_deterministic() {
        assert_eq!(
            derive_resource_id("Some:Original@Id", vec![]).unwrap().0,
            derive_resource_id("Some:Original@Id", vec![]).unwrap().0
        );
    }

    #[test]
    fn distinct_identifiers_derive_distinct_resource_ids() {
        assert_ne!(
            derive_resource_id("Some:Original@Id", vec![]).unwrap().0,
            derive_resource_id("Some:Other@Id", vec![]).unwrap().0
        );
    }

    #[test]
    fn rejects_an_empty_identifier() {
        match derive_resource_id("", vec![]) {
            Err(e) => assert!(matches!(e.kind(), ErrorKind::ValidationError(_))),
            Ok((resource_id, _)) => panic!("expected a validation error, got {resource_id}"),
        }
    }

    #[test_case("Some:Original@Id_"; "hashed identifier")]
    #[test_case("opcua-asset-td"; "conforming identifier")]
    fn records_the_original_id_verbatim(original_id: &str) {
        let (_, resource_labels) = derive_resource_id(original_id, vec![]).unwrap();

        assert_eq!(resource_labels.len(), 1);
        assert_eq!(resource_labels[0].key, ORIGINAL_ID_LABEL_KEY);
        assert_eq!(resource_labels[0].value, original_id);
    }

    #[test]
    fn does_not_duplicate_an_identical_original_id_label() {
        let existing = vec![Label {
            key: ORIGINAL_ID_LABEL_KEY.to_string(),
            value: "current".to_string(),
        }];
        let (_, resource_labels) = derive_resource_id("current", existing).unwrap();

        assert_eq!(resource_labels.len(), 1);
        assert_eq!(resource_labels[0].value, "current");
    }

    #[test]
    fn keeps_an_original_id_label_recording_a_different_identifier() {
        let existing = vec![Label {
            key: ORIGINAL_ID_LABEL_KEY.to_string(),
            value: "other".to_string(),
        }];
        let (_, resource_labels) = derive_resource_id("current", existing).unwrap();

        assert_eq!(resource_labels.len(), 2);
        assert_eq!(resource_labels[0].value, "other");
        assert_eq!(resource_labels[1].value, "current");
    }

    #[test]
    fn preserves_unrelated_labels() {
        let existing = vec![
            Label {
                key: "first".to_string(),
                value: "1".to_string(),
            },
            // Shares the value, but not the key, of the label being recorded.
            Label {
                key: "second".to_string(),
                value: "some-id".to_string(),
            },
        ];
        let (_, resource_labels) = derive_resource_id("some-id", existing).unwrap();

        assert_eq!(resource_labels.len(), 3);
        assert_eq!(resource_labels[0].key, "first");
        assert_eq!(resource_labels[1].key, "second");
        assert_eq!(resource_labels[2].key, ORIGINAL_ID_LABEL_KEY);
    }
}
