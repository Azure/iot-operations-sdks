// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

//! Generic xRegistry models.
//!
//! Contains the generic xRegistry entities (Groups, Resources, Versions) and the
//! shared value types (e.g. labels, deprecation info, validation status) used by
//! both the generic service and the extension models.

use std::collections::HashMap;

use bytes::Bytes;
use chrono::{DateTime, Utc};

use crate::edge_registry::edge_registry_gen::common_types::b64::{self};
use crate::edge_registry::edge_registry_gen::edge_registry::client as client_gen;

// ~~~~~~~~~~~~~~~~~~~Conversion helpers~~~~~~~~~~~~~~~~~~~~~~~~~~

/// Converts a generated list of `Label` key/value pairs into a map.
fn labels_from_gen(labels: Vec<client_gen::Label>) -> HashMap<String, String> {
    labels.into_iter().map(|l| (l.key, l.value)).collect()
}

/// Converts a map of labels into the generated list of `Label` key/value pairs.
pub(crate) fn labels_to_gen(labels: HashMap<String, String>) -> Vec<client_gen::Label> {
    labels
        .into_iter()
        .map(|(key, value)| client_gen::Label { key, value })
        .collect()
}

/// Converts a generated map of base64 extension values into byte buffers.
fn extensions_from_gen(extensions: HashMap<String, b64::Bytes>) -> HashMap<String, Bytes> {
    extensions
        .into_iter()
        .map(|(k, v)| (k, Bytes::from(v.0)))
        .collect()
}

/// Converts a map of extension byte buffers into the generated base64 type.
pub(crate) fn extensions_to_gen(extensions: HashMap<String, Bytes>) -> HashMap<String, b64::Bytes> {
    extensions
        .into_iter()
        .map(|(k, v)| (k, b64::Bytes(v.to_vec())))
        .collect()
}

// ~~~~~~~~~~~~~~~~~~~Shared value types~~~~~~~~~~~~~~~~~~~~~~~~~~

/// Indicates whether validation was performed, and if not, the reason why not (e.g., "unsupported
/// format", "validation disabled").
#[derive(Debug, Clone, PartialEq, Eq)]
pub enum Validated {
    /// Validation was performed and the entity adheres to the rules.
    True,
    /// Validation was not performed, along with the reason why not.
    False(String),
}

impl From<client_gen::Validated> for Validated {
    fn from(value: client_gen::Validated) -> Self {
        if value.validated {
            // Per the model, `reason` MUST NOT be present when `validated` is true.
            // Any value is dropped here, as the `True` variant carries no reason.
            Validated::True
        } else if let Some(reason) = value.reason {
            Validated::False(reason)
        } else {
            // `reason` SHOULD be present when `validated` is false; a missing
            // reason is tolerated and treated as an unknown (empty) reason.
            log::warn!(
                "'Validated.reason' is missing when 'validated' is false; treating as unknown reason"
            );
            Validated::False(String::new())
        }
    }
}

/// Information about the deprecation status of an entity.
#[derive(Debug, Clone, Default, PartialEq, Eq)]
pub struct DeprecatedInfo {
    /// Indicates the time when the entity entered, or will enter, a deprecated state. The date MAY
    /// be in the past or future. If this property is not present the entity is already in a
    /// deprecated state.
    pub effective: Option<DateTime<Utc>>,
    /// Indicates the time when the entity will be removed. The entity MUST NOT be removed before
    /// this time. If this property is not present, the client cannot make any assumptions as to
    /// when the entity might be removed. This MUST NOT be sooner than the `effective` time, if that
    /// is present.
    pub removal: Option<DateTime<Utc>>,
    /// The URL to an alternative entity the client can consider as a replacement for this entity.
    /// There is no guarantee that the referenced entity is an exact replacement.
    pub alternative: Option<String>,
    /// The URL to additional information about the deprecation of this entity.
    pub documentation: Option<String>,
}

impl From<client_gen::DeprecatedInfo> for DeprecatedInfo {
    fn from(value: client_gen::DeprecatedInfo) -> Self {
        DeprecatedInfo {
            effective: value.effective,
            removal: value.removal,
            alternative: value.alternative,
            documentation: value.documentation,
        }
    }
}

impl From<DeprecatedInfo> for client_gen::DeprecatedInfo {
    fn from(value: DeprecatedInfo) -> Self {
        client_gen::DeprecatedInfo {
            effective: value.effective,
            removal: value.removal,
            alternative: value.alternative,
            documentation: value.documentation,
        }
    }
}

// ~~~~~~~~~~~~~~~~~~~Identifiers (XIDs)~~~~~~~~~~~~~~~~~~~~~~~~~~

/// The XID components that identify a Resource.
#[derive(Debug, Clone, PartialEq, Eq)]
pub struct ResourceXid {
    /// Group type.
    pub group_type: String,
    /// Group identifier.
    pub group_id: String,
    /// Resource type.
    pub resource_type: String,
    /// Resource identifier.
    pub resource_id: String,
}

impl From<client_gen::ResourceXid> for ResourceXid {
    fn from(value: client_gen::ResourceXid) -> Self {
        ResourceXid {
            group_type: value.group_type,
            group_id: value.group_id,
            resource_type: value.resource_type,
            resource_id: value.resource_id,
        }
    }
}

impl From<client_gen::ResourceXidList> for Vec<ResourceXid> {
    fn from(value: client_gen::ResourceXidList) -> Self {
        value.resources.into_iter().map(ResourceXid::from).collect()
    }
}

/// The XID components that identify a Version.
#[derive(Debug, Clone, PartialEq, Eq)]
pub struct VersionXid {
    /// Group type.
    pub group_type: String,
    /// Group identifier.
    pub group_id: String,
    /// Resource type.
    pub resource_type: String,
    /// Resource identifier.
    pub resource_id: String,
    /// Version identifier.
    pub version_id: String,
}

impl From<client_gen::VersionXid> for VersionXid {
    fn from(value: client_gen::VersionXid) -> Self {
        VersionXid {
            group_type: value.group_type,
            group_id: value.group_id,
            resource_type: value.resource_type,
            resource_id: value.resource_id,
            version_id: value.version_id,
        }
    }
}

impl From<client_gen::VersionXidList> for Vec<VersionXid> {
    fn from(value: client_gen::VersionXidList) -> Self {
        value.versions.into_iter().map(VersionXid::from).collect()
    }
}

// ~~~~~~~~~~~~~~~~~~~Read entities~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~

/// A Group entity — container for related Resources.
#[derive(Debug, Clone)]
pub struct Group {
    /// Group identifier.
    pub id: String,
    /// Full XID path, e.g. /schemagroups/mygroup
    pub xid: String,
    /// A numeric value used to determine whether an entity has been modified.
    pub epoch: u64,
    /// Human-readable name.
    pub name: Option<String>,
    /// A human-readable summary of the purpose of the entity.
    pub description: Option<String>,
    /// A URL to additional information about this entity.
    pub documentation: Option<String>,
    /// A URL to a graphical icon for the owning entity.
    pub icon: Option<String>,
    /// A mechanism in which additional metadata about the entity can be stored without changing the
    /// model definition of the entity. Labels can be used for querying.
    pub labels: HashMap<String, String>,
    /// The date/time of when the entity was created.
    pub created_at: DateTime<Utc>,
    /// The date/time of when the entity was last updated.
    pub modified_at: DateTime<Utc>,
    /// Information about deprecation status of the entity, if applicable.
    pub deprecated: Option<DeprecatedInfo>,
    /// Map of the count of each Resource type contained within this Group, keyed by Resource type.
    pub resources_counts: HashMap<String, u64>,
    /// Extension-specific attributes (e.g., `envelope`, `protocol` for message Groups).
    pub extensions: HashMap<String, Bytes>,
}

impl From<client_gen::Group> for Group {
    fn from(value: client_gen::Group) -> Self {
        Group {
            id: value.id,
            xid: value.xid,
            epoch: value.epoch,
            name: value.name,
            description: value.description,
            documentation: value.documentation,
            icon: value.icon,
            labels: labels_from_gen(value.labels),
            created_at: value.created_at,
            modified_at: value.modified_at,
            deprecated: value.deprecated.map(DeprecatedInfo::from),
            resources_counts: value.resources_counts,
            extensions: extensions_from_gen(value.extensions),
        }
    }
}

/// Resource entity
#[derive(Debug, Clone)]
pub struct Resource {
    /// Resource identifier.
    pub id: String,
    /// Full XID path.
    pub xid: String,
    /// An object that contains most of the Resource-level attributes.
    pub meta: ResourceMeta,
    /// A specific Version of a Resource.
    pub default_version: Version,
    /// The number of Versions contained on the Resource.
    pub versions_count: u64,
    /// Extension-specific attributes (e.g., `format` and `content_type` for schemas).
    pub extensions: HashMap<String, Bytes>,
}

impl From<client_gen::Resource> for Resource {
    fn from(value: client_gen::Resource) -> Self {
        Resource {
            id: value.id,
            xid: value.xid,
            meta: ResourceMeta::from(value.meta),
            default_version: Version::from(value.default_version),
            versions_count: value.versions_count,
            extensions: extensions_from_gen(value.extensions),
        }
    }
}

/// An object that contains most of the Resource-level attributes.
#[derive(Debug, Clone)]
pub struct ResourceMeta {
    /// Resource identifier.
    pub id: String,
    /// Full XID path.
    pub xid: String,
    /// Indicates that this Resource is a reference to another Resource within the same Registry.
    /// The XID path of the referenced Resource.
    pub xref: Option<String>,
    /// A numeric value used to determine whether an entity has been modified.
    pub epoch: u64,
    /// A mechanism in which additional metadata about the entity can be stored without changing the
    /// model definition of the entity. Labels can be used for querying.
    pub labels: HashMap<String, String>,
    /// The date/time of when the entity was created.
    pub created_at: DateTime<Utc>,
    /// The date/time of when the entity was last updated.
    pub modified_at: DateTime<Utc>,
    /// Indicates whether the Resource is updateable by clients.
    pub read_only: bool,
    /// States that Versions of this Resource adhere to a certain compatibility rule.
    pub compatibility: Option<String>,
    /// Information about deprecation status of the entity, if applicable.
    pub deprecated: Option<DeprecatedInfo>,
    /// The versionId of the default Version of the Resource.
    pub default_version_id: String,
    /// A value of true means that `defaultVersionId` has been explicitly set and MUST NOT
    /// automatically change if other Versions are added or removed. A value of false means the
    /// default Version MUST be the newest Version, as defined by the Resource's versionmode
    /// algorithm.
    pub default_version_sticky: bool,
    /// Extension-specific attributes (e.g., `format` and `content_type` for schemas).
    pub extensions: HashMap<String, Bytes>,
}

impl From<client_gen::ResourceMeta> for ResourceMeta {
    fn from(value: client_gen::ResourceMeta) -> Self {
        ResourceMeta {
            id: value.id,
            xid: value.xid,
            xref: value.xref,
            epoch: value.epoch,
            labels: labels_from_gen(value.labels),
            created_at: value.created_at,
            modified_at: value.modified_at,
            read_only: value.read_only,
            compatibility: value.compatibility,
            deprecated: value.deprecated.map(DeprecatedInfo::from),
            default_version_id: value.default_version_id,
            default_version_sticky: value.default_version_sticky,
            extensions: extensions_from_gen(value.extensions),
        }
    }
}

/// A specific Version of a Resource.
#[derive(Debug, Clone)]
pub struct Version {
    /// Resource identifier.
    pub resource_id: String,
    /// Version identifier.
    pub version_id: String,
    /// Full XID path.
    pub xid: String,
    /// A numeric value used to determine whether an entity has been modified.
    pub epoch: u64,
    /// Human-readable name.
    pub name: Option<String>,
    /// Indicates whether this Version is the default Version of the owning Resource.
    pub is_default: bool,
    /// A human-readable summary of the purpose of the entity.
    pub description: Option<String>,
    /// A URL to additional information about this entity.
    pub documentation: Option<String>,
    /// A URL to a graphical icon for the owning entity.
    pub icon: Option<String>,
    /// A mechanism in which additional metadata about the entity can be stored without changing the
    /// model definition of the entity. Labels can be used for querying.
    pub labels: HashMap<String, String>,
    /// The date/time of when the entity was created.
    pub created_at: DateTime<Utc>,
    /// The date/time of when the entity was last updated.
    pub modified_at: DateTime<Utc>,
    /// The versionId of this Version's ancestor, or this Version's versionId if it has no ancestor.
    pub ancestor: String,
    /// The media type of the entity as defined by RFC9110.
    pub content_type: Option<String>,
    /// Identifies what the Version represents (e.g. `JsonSchema/draft-07`, `JSON-LD/1.1`).
    pub format: Option<String>,
    /// When format validation is enabled, indicates whether the server has validated that the
    /// Version conforms to the rules defined by its `format` attribute.
    pub format_validated: Option<Validated>,
    /// When compatibility validation is enabled, indicates whether the server has validated that
    /// the Version conforms to the rules defined by its Resource's `meta.compatibility` attribute.
    pub compatibility_validated: Option<Validated>,
    /// The raw document content for this Version as base64-encoded bytes. The interpretation
    /// (schema, thing description, thing model, …) is determined by the parent Resource's type.
    pub document: Option<Bytes>,
    /// The hash of the document content for this Version.
    pub document_hash: Option<String>,
    /// Extension-specific attributes.
    pub extensions: HashMap<String, Bytes>,
}

impl From<client_gen::Version> for Version {
    fn from(value: client_gen::Version) -> Self {
        Version {
            resource_id: value.resource_id,
            version_id: value.version_id,
            xid: value.xid,
            epoch: value.epoch,
            name: value.name,
            is_default: value.is_default,
            description: value.description,
            documentation: value.documentation,
            icon: value.icon,
            labels: labels_from_gen(value.labels),
            created_at: value.created_at,
            modified_at: value.modified_at,
            ancestor: value.ancestor,
            content_type: value.content_type,
            format: value.format,
            format_validated: value.format_validated.map(Validated::from),
            compatibility_validated: value.compatibility_validated.map(Validated::from),
            document: value.document.map(|b| Bytes::from(b.0)),
            document_hash: value.document_hash,
            extensions: extensions_from_gen(value.extensions),
        }
    }
}

// ~~~~~~~~~~~~~~~~~~~Request payloads~~~~~~~~~~~~~~~~~~~~~~~~~~~~

/// Request payload for creating or updating a Group.
#[derive(Debug, Clone, Default)]
pub struct GroupAttributes {
    /// Human-readable name.
    pub name: Option<String>,
    /// A human-readable summary of the purpose of the entity.
    pub description: Option<String>,
    /// A URL to additional information about this entity.
    pub documentation: Option<String>,
    /// A URL to a graphical icon for the owning entity.
    pub icon: Option<String>,
    /// A mechanism in which additional metadata about the entity can be stored without changing the
    /// model definition of the entity. Labels can be used for querying.
    pub labels: HashMap<String, String>,
    /// Information about deprecation status of the entity, if applicable.
    pub deprecated: Option<DeprecatedInfo>,
    /// Extension-specific attributes (e.g., `envelope`, `protocol` for message Groups).
    pub extensions: HashMap<String, Bytes>,
}

/// Mutable attributes for creating or updating a Resource (its `meta` sub-entity).
#[derive(Debug, Clone, Default)]
pub struct ResourceMetaAttributes {
    /// Indicates that this Resource is a reference to another Resource within the same Registry. The XID path of the referenced Resource.
    pub xref: Option<String>,
    /// A mechanism in which additional metadata about the entity can be stored without changing the model definition of the entity. Labels can be used for querying.
    pub labels: HashMap<String, String>,
    /// States that Versions of this Resource adhere to a certain compatibility rule.
    pub compatibility: Option<String>,
    /// Information about deprecation status of the entity, if applicable.
    pub deprecated: Option<DeprecatedInfo>,
    /// Extension-specific attributes (e.g., `format` and `content_type` for schemas).
    pub extensions: HashMap<String, Bytes>,
}

/// Attributes needed to create a Version.
#[derive(Debug, Clone, Default)]
pub struct VersionAttributes {
    /// Human-readable name.
    pub name: Option<String>,
    /// A human-readable summary of the purpose of the entity.
    pub description: Option<String>,
    /// A URL to additional information about this entity.
    pub documentation: Option<String>,
    /// A URL to a graphical icon for the owning entity.
    pub icon: Option<String>,
    /// Queryable Key Value pairs to be added to the Version.
    pub labels: HashMap<String, String>,
    /// The versionId of this Version's ancestor if it has an ancestor.
    pub ancestor: Option<String>,
    /// Content type of the Version document.
    pub content_type: Option<String>,
    /// Format identifier of the Version document (resource-type-specific, e.g. `JsonSchema/draft-07`, `JSON-LD/1.1`).
    pub format: Option<String>,
    /// Base64-encoded document content for the Version. The interpretation (schema, thing description, thing model, etc.) is determined by the Resource type.
    pub document: Option<Bytes>,
    /// Extension-specific attributes.
    pub extensions: HashMap<String, Bytes>,
}

/// Request payload for creating a generic xRegistry Resource entity along with its default Version.
#[derive(Debug, Clone)]
pub struct CreateResourceRequest {
    /// Group identifier. Uses the default if not specified.
    pub group_id: Option<String>,
    /// The attributes needed to create the Resource Meta (its `meta` sub-entity).
    pub meta: ResourceMetaAttributes,
    /// The attributes of the Resource's default Version, which will be created along side this
    /// Resource.
    pub default_version: VersionAttributes,
    /// Version identifier for the Resource's default Version, which is created along with this
    /// Resource. If omitted, the server determines the versionId.
    pub default_version_id: Option<String>,
    /// Extension-specific attributes.
    pub extensions: HashMap<String, Bytes>,
}
/// Request payload for creating a generic xRegistry Version entity. The parent Resource is
/// implicitly created if it doesn't already exist. Create is strictly idempotent: if every field
/// matches the latest Version of the parent Resource (including labels), the latest Version is
/// returned; otherwise a new Version is created.
#[derive(Debug, Clone)]
pub struct CreateVersionRequest {
    /// Group identifier. Uses the default if not specified.
    pub group_id: Option<String>,
    /// Version identifier of the Version to create. If omitted, the server determines the
    /// versionId.
    pub version_id: Option<String>,
    /// The attributes of the Version to create.
    pub version: VersionAttributes,
    /// Queryable Key Value pairs to be added to the parent Resource. The parent Resource is
    /// implicitly created if it doesn't already exist.
    pub resource_labels: HashMap<String, String>,
}

impl GroupAttributes {
    pub(crate) fn into(self, group_id: Option<String>) -> client_gen::GroupAttributes {
        client_gen::GroupAttributes {
            group_id,
            name: self.name,
            description: self.description,
            documentation: self.documentation,
            icon: self.icon,
            labels: labels_to_gen(self.labels),
            deprecated: self.deprecated.map(client_gen::DeprecatedInfo::from),
            extensions: extensions_to_gen(self.extensions),
        }
    }
}

impl From<ResourceMetaAttributes> for client_gen::ResourceMetaAttributes {
    fn from(value: ResourceMetaAttributes) -> Self {
        client_gen::ResourceMetaAttributes {
            xref: value.xref,
            labels: labels_to_gen(value.labels),
            compatibility: value.compatibility,
            deprecated: value.deprecated.map(client_gen::DeprecatedInfo::from),
            extensions: extensions_to_gen(value.extensions),
        }
    }
}

impl From<VersionAttributes> for client_gen::VersionAttributes {
    fn from(value: VersionAttributes) -> Self {
        client_gen::VersionAttributes {
            name: value.name,
            description: value.description,
            documentation: value.documentation,
            icon: value.icon,
            labels: labels_to_gen(value.labels),
            ancestor: value.ancestor,
            content_type: value.content_type,
            format: value.format,
            document: value.document.map(|b| b64::Bytes(b.to_vec())),
            extensions: extensions_to_gen(value.extensions),
        }
    }
}
