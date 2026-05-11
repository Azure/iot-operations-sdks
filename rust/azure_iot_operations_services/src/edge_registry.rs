// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

use std::collections::HashMap;

use azure_iot_operations_protocol::{common::aio_protocol_error::AIOProtocolError, rpc_command};
use chrono::{DateTime, Utc};
use derive_builder::Builder;
use thiserror::Error;

use crate::edge_registry::edge_registry_gen::edge_registry_client::client as er_client_gen;

mod client;
mod edge_registry_gen;

pub use client::Client;

/// Represents an error that occurred in the Azure IoT Operations xRegistry Client implementation.
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

/// Represents the kinds of errors that occur in the Azure IoT Operations xRegistry implementation.
#[derive(Error, Debug)]
#[allow(clippy::large_enum_variant)]
pub enum ErrorKind {
    /// An error occurred in the AIO Protocol. See [`AIOProtocolError`] for more information.
    #[error(transparent)]
    AIOProtocolError(#[from] AIOProtocolError),
    /// An argument provided for a request was invalid.
    #[error(transparent)]
    InvalidRequestArgument(#[from] rpc_command::invoker::RequestBuilderError),
    /// An error was returned by the xRegistry Service.
    #[error("{0:?}")]
    ServiceError(#[from] ServiceError),
}

/// Error object for xRegistry operations
#[derive(Debug, Clone)]
pub struct ServiceError {
    /// HTTP-style response code.
    pub code: u64,
    /// Detailed error description.
    pub detail: Option<String>,
    /// The XID of the entity that caused the error.
    pub instance: String,
    /// HTTP-style response status text.
    pub status: String,
    /// Short human-readable error title.
    pub title: String,
    /// URI identifying the error type.
    pub type_uri: String,
}

impl std::fmt::Display for ServiceError {
    fn fmt(&self, f: &mut std::fmt::Formatter<'_>) -> std::fmt::Result {
        // TODO: any other fields here?
        write!(f, "{} {}. {}", self.code, self.status, self.title)
    }
}

impl std::error::Error for ServiceError {
    fn source(&self) -> Option<&(dyn std::error::Error + 'static)> {
        None
    }
}

impl From<er_client_gen::EdgeRegistryError> for ServiceError {
    fn from(value: er_client_gen::EdgeRegistryError) -> Self {
        ServiceError {
            code: value.code,
            detail: value.detail,
            instance: value.instance,
            status: value.status,
            title: value.title,
            type_uri: value.r#type,
        }
    }
}

impl From<er_client_gen::SchemaExtensionError> for ServiceError {
    fn from(value: er_client_gen::SchemaExtensionError) -> Self {
        ServiceError {
            code: value.code,
            detail: value.detail,
            instance: value.instance,
            status: value.status,
            title: value.title,
            type_uri: value.r#type,
        }
    }
}

impl From<er_client_gen::ThingDescriptionExtensionError> for ServiceError {
    fn from(value: er_client_gen::ThingDescriptionExtensionError) -> Self {
        ServiceError {
            code: value.code,
            detail: value.detail,
            instance: value.instance,
            status: value.status,
            title: value.title,
            type_uri: value.r#type,
        }
    }
}

#[derive(Debug, Clone)]
pub struct Group {
    /// The 'createdAt' Field.
    pub created_at: DateTime<Utc>,
    /// The 'description' Field.
    pub description: Option<String>,
    /// The 'documentation' Field.
    pub documentation: Option<String>,
    /// Optimistic concurrency epoch.
    pub epoch: u64,
    /// Extension-specific attributes (e.g., `envelope`, `protocol` for message groups).
    pub extensions: HashMap<String, Vec<u8>>,
    /// Group identifier.
    pub id: String,
    /// The 'labels' Field.
    pub labels: HashMap<String, String>,
    /// The 'modifiedAt' Field.
    pub modified_at: DateTime<Utc>,
    /// Human-readable name.
    pub name: Option<String>,
    /// The 'resourcesCount' Field.
    pub resources_count: u64,
    /// Full XID path, e.g. /schemagroups/mygroup
    pub xid: String,
}

impl std::fmt::Display for Group {
    fn fmt(&self, f: &mut std::fmt::Formatter<'_>) -> std::fmt::Result {
        let extensions = self
            .extensions
            .iter()
            .map(|(k, v)| (k, v.len()))
            .collect::<Vec<_>>();
        write!(
            f,
            "Group {{ 
                id: {}, 
                name: {:?}, 
                description: {:?}, 
                documentation: {:?}, 
                labels: {:?}, 
                resources_count: {}, 
                epoch: {}, 
                created_at: {}, 
                modified_at: {}, 
                xid: {}, 
                extensions: {extensions:?} 
            }}",
            self.id,
            self.name,
            self.description,
            self.documentation,
            self.labels,
            self.resources_count,
            self.epoch,
            self.created_at,
            self.modified_at,
            self.xid
        )
    }
}

impl From<er_client_gen::Group> for Group {
    fn from(value: er_client_gen::Group) -> Self {
        Group {
            created_at: value.created_at,
            description: value.description,
            documentation: value.documentation,
            epoch: value.epoch,
            extensions: value
                .extensions
                .into_iter()
                .map(|(k, v)| (k, v.0))
                .collect(),
            id: value.id,
            labels: value.labels,
            modified_at: value.modified_at,
            name: value.name,
            resources_count: value.resources_count,
            xid: value.xid,
        }
    }
}

#[derive(Debug, Clone, Builder, Default)]
#[builder(setter(into, strip_option))]
pub struct GroupAttributes {
    #[builder(default = "None")]
    pub name: Option<String>,
    #[builder(default = "None")]
    pub description: Option<String>,
    #[builder(default = "None")]
    pub documentation: Option<String>,
    #[builder(default)]
    pub labels: HashMap<String, String>,
    #[builder(default)]
    pub extensions: HashMap<String, Vec<u8>>,
}

#[derive(Debug, Clone)]
pub struct ResourceMeta {
    /// The 'createdAt' Field.
    pub created_at: DateTime<Utc>,
    /// The 'defaultVersionId' Field.
    pub default_version_id: String,
    /// The 'epoch' Field.
    pub epoch: u64,
    /// Extension-specific attributes (e.g., `format` and `content_type` for schemas).
    pub extensions: HashMap<String, Vec<u8>>,
    pub labels: HashMap<String, String>,
    /// Resource identifier within the group.
    pub id: String,
    /// The 'modifiedAt' Field.
    pub modified_at: DateTime<Utc>,
    /// Full XID path.
    pub xid: String,
}

impl From<er_client_gen::ResourceMeta> for ResourceMeta {
    fn from(value: er_client_gen::ResourceMeta) -> Self {
        ResourceMeta {
            created_at: value.created_at,
            default_version_id: value.default_version_id,
            epoch: value.epoch,
            extensions: value
                .extensions
                .into_iter()
                .map(|(k, v)| (k, v.0))
                .collect(),
            id: value.id,
            modified_at: value.modified_at,
            xid: value.xid,
            labels: value.labels,
        }
    }
}

impl std::fmt::Display for ResourceMeta {
    fn fmt(&self, f: &mut std::fmt::Formatter<'_>) -> std::fmt::Result {
        let extensions = self
            .extensions
            .iter()
            .map(|(k, v)| (k, v.len()))
            .collect::<Vec<_>>();
        write!(
            f,
            "ResourceMeta {{ 
                id: {}, 
                xid: {}, 
                created_at: {}, 
                modified_at: {}, 
                default_version_id: {}, 
                epoch: {}, 
                extensions: {extensions:?},
                labels: {:?}
            }}",
            self.id,
            self.xid,
            self.created_at,
            self.modified_at,
            self.default_version_id,
            self.epoch,
            self.labels
        )
    }
}

#[derive(Debug, Clone, Default)]
pub struct ResourceMetaAttributes {
    /// Extension-specific attributes (e.g., `format` and `content_type` for schemas).
    pub extensions: HashMap<String, Vec<u8>>,
    /// The 'labels' Field.
    pub labels: HashMap<String, String>,
}

// -----------------------------------------------------------------------
// Schema Extension
// -----------------------------------------------------------------------

pub type SchemaGroup = Group;
pub type SchemaGroupAttributes = GroupAttributes;
pub type SchemaMetaAttributes = ResourceMetaAttributes;

#[derive(Debug, Clone, Builder)]
#[builder(setter(into, strip_option))]
pub struct CreateSchemaVersionAttributes {
    /// The versionId of this version's ancestor if it has an ancestor.
    #[builder(default = "None")]
    pub ancestor: Option<u64>,
    /// Content type of the schema version.
    // TODO: do we want these to be required here?
    #[builder(default = "None")]
    pub content_type: Option<String>,
    /// The 'description' Field.
    #[builder(default = "None")]
    pub description: Option<String>,
    /// The 'documentation' Field.
    #[builder(default = "None")]
    pub documentation: Option<String>,
    /// Schema format identifier, e.g. `JsonSchema/draft-07`, `Protobuf/3`.
    pub format: SchemaFormat,
    /// The 'labels' Field.
    #[builder(default)]
    pub labels: HashMap<String, String>,
    /// The 'name' Field.
    #[builder(default = "None")]
    pub name: Option<String>,
    /// Base64-encoded schema document content.
    pub schema_document: Vec<u8>,
}

/// Supported Schema Formats or the ability to specify a custom format, although it may not be supported.
#[derive(Debug, Clone)]
#[non_exhaustive]
pub enum SchemaFormat {
    /// JSON Schema draft-07 format
    JsonSchemaDraft07,
    /// Avro 1.11.0 format
    Avro1110,
    /// Custom specified format. May be rejected by the service
    Other(String),
}

impl From<SchemaFormat> for String {
    fn from(format: SchemaFormat) -> Self {
        match format {
            SchemaFormat::JsonSchemaDraft07 => er_client_gen::JSON_SCHEMA_DRAFT07.to_string(),
            SchemaFormat::Avro1110 => er_client_gen::AVRO1110.to_string(),
            SchemaFormat::Other(s) => s,
        }
    }
}

#[derive(Debug, Clone)]
pub struct Schema {
    /// The default version of the schema.
    pub default_version: SchemaVersion,
    /// Extension-specific attributes (e.g., `format` and `content_type` for schemas).
    pub extensions: HashMap<String, Vec<u8>>,
    /// Resource identifier within the group.
    pub id: String,
    /// The 'meta' Field.
    pub meta: SchemaMeta,
    /// The 'versionsCount' Field.
    pub versions_count: u64,
    /// Full XID path.
    pub xid: String,
}

impl std::fmt::Display for Schema {
    fn fmt(&self, f: &mut std::fmt::Formatter<'_>) -> std::fmt::Result {
        let extensions = self
            .extensions
            .iter()
            .map(|(k, v)| (k, v.len()))
            .collect::<Vec<_>>();
        write!(
            f,
            "Schema {{ 
                id: {}, 
                xid: {}, 
                versions_count: {}, 
                default_version: {}, 
                extensions: {extensions:?}, 
                meta: {} 
            }}",
            self.id, self.xid, self.versions_count, self.default_version, self.meta
        )
    }
}

#[derive(Debug, Clone)]
pub struct SchemaMeta {
    /// The 'createdAt' Field.
    pub created_at: DateTime<Utc>,
    /// The 'defaultVersionId' Field.
    pub default_version_id: u64,
    /// The 'epoch' Field.
    pub epoch: u64,
    /// Extension-specific attributes (e.g., `format` and `content_type` for schemas).
    pub extensions: HashMap<String, Vec<u8>>,
    pub labels: HashMap<String, String>,
    /// Resource identifier within the group.
    pub id: String,
    /// The 'modifiedAt' Field.
    pub modified_at: DateTime<Utc>,
    /// Full XID path.
    pub xid: String,
    // Extension field
    /// Whether the schema should be validated or not
    pub validation: bool,
}

impl std::fmt::Display for SchemaMeta {
    fn fmt(&self, f: &mut std::fmt::Formatter<'_>) -> std::fmt::Result {
        let extensions = self
            .extensions
            .iter()
            .map(|(k, v)| (k, v.len()))
            .collect::<Vec<_>>();
        write!(
            f,
            "SchemaMeta {{ 
                id: {}, 
                xid: {}, 
                created_at: {}, 
                modified_at: {}, 
                default_version_id: {}, 
                epoch: {}, 
                validation: {}, 
                labels: {:?},
                extensions: {extensions:?} 
            }}",
            self.id,
            self.xid,
            self.created_at,
            self.modified_at,
            self.default_version_id,
            self.epoch,
            self.validation,
            self.labels
        )
    }
}

#[derive(Debug, Clone)]
pub struct SchemaVersion {
    /// The versionId of this version's ancestor, or this version's versionId if it has no ancestor.
    pub ancestor: u64,
    /// The 'createdAt' Field.
    pub created_at: DateTime<Utc>,
    /// The 'description' Field.
    pub description: Option<String>,
    /// The 'documentation' Field.
    pub documentation: Option<String>,
    /// The 'epoch' Field.
    pub epoch: u64,
    /// Extension-specific attributes.
    pub extensions: HashMap<String, Vec<u8>>,
    /// The 'labels' Field.
    pub labels: HashMap<String, String>,
    /// The 'modifiedAt' Field.
    pub modified_at: DateTime<Utc>,
    /// The 'name' Field.
    pub name: Option<String>,
    /// Resource identifier.
    pub resource_id: String,
    /// Version identifier.
    pub version_id: u64,
    /// The 'xid' Field.
    pub xid: String,
    // Schema extensions
    /// Content type of the schema version.
    pub content_type: Option<String>,
    /// Schema format identifier, e.g. `JsonSchema/draft-07`, `Protobuf/3`.
    pub format: Option<String>,
    /// The schema document as a byte array.
    pub schema_document: Vec<u8>,
}

impl std::fmt::Display for SchemaVersion {
    fn fmt(&self, f: &mut std::fmt::Formatter<'_>) -> std::fmt::Result {
        let extensions = self
            .extensions
            .iter()
            .map(|(k, v)| (k, v.len()))
            .collect::<Vec<_>>();
        write!(
            f,
            "SchemaVersion {{ 
                resource_id: {}, 
                version_id: {}, 
                ancestor: {}, 
                name: {:?}, 
                description: {:?}, 
                format: {:?}, 
                content_type: {:?}, 
                created_at: {},
                modified_at: {},
                documentation: {:?},
                epoch: {},
                xid: {},
                extensions: {extensions:?},
                labels: {:?},
                schema_document length: {:?}   
            }}",
            self.resource_id,
            self.version_id,
            self.ancestor,
            self.name,
            self.description,
            self.format,
            self.content_type,
            self.created_at,
            self.modified_at,
            self.documentation,
            self.epoch,
            self.xid,
            self.labels,
            self.schema_document.len()
        )
    }
}

impl From<er_client_gen::Schema> for Schema {
    fn from(schema: er_client_gen::Schema) -> Self {
        Schema {
            default_version: (
                schema.resource.default_version,
                schema.default_version_extensions,
            )
                .into(),
            extensions: schema
                .resource
                .extensions
                .into_iter()
                .map(|(k, v)| (k, v.0))
                .collect(),
            id: schema.resource.id,
            meta: (schema.resource.meta, schema.meta_extensions).into(),
            versions_count: schema.resource.versions_count,
            xid: schema.resource.xid,
        }
    }
}

impl
    From<(
        er_client_gen::ResourceMeta,
        er_client_gen::SchemaMetaExtensions,
    )> for SchemaMeta
{
    fn from(
        (resource_meta, schema_meta_extensions): (
            er_client_gen::ResourceMeta,
            er_client_gen::SchemaMetaExtensions,
        ),
    ) -> Self {
        SchemaMeta {
            created_at: resource_meta.created_at,
            default_version_id: schema_meta_extensions.default_version_id,
            epoch: resource_meta.epoch,
            extensions: resource_meta
                .extensions
                .into_iter()
                .map(|(k, v)| (k, v.0))
                .collect(),
            id: resource_meta.id,
            modified_at: resource_meta.modified_at,
            xid: resource_meta.xid,
            validation: schema_meta_extensions.validation,
            labels: resource_meta.labels,
        }
    }
}

impl
    From<(
        er_client_gen::Version,
        er_client_gen::SchemaVersionExtensions,
    )> for SchemaVersion
{
    fn from(
        (version, schema_version_extensions): (
            er_client_gen::Version,
            er_client_gen::SchemaVersionExtensions,
        ),
    ) -> Self {
        SchemaVersion {
            ancestor: schema_version_extensions.ancestor,
            created_at: version.created_at,
            description: version.description,
            documentation: version.documentation,
            epoch: version.epoch,
            extensions: version
                .extensions
                .into_iter()
                .map(|(k, v)| (k, v.0))
                .collect(),
            labels: version.labels,
            modified_at: version.modified_at,
            name: version.name,
            resource_id: version.resource_id,
            version_id: schema_version_extensions.version_id,
            xid: version.xid,
            content_type: schema_version_extensions.content_type,
            format: schema_version_extensions.format,
            schema_document: schema_version_extensions.schema_doc.to_vec(),
        }
    }
}

impl From<er_client_gen::SchemaVersion> for SchemaVersion {
    fn from(schema_version: er_client_gen::SchemaVersion) -> Self {
        (schema_version.version, schema_version.extensions).into()
    }
}

// -----------------------------------------------------------------------
// Thing Description Extension
// -----------------------------------------------------------------------

pub type ThingDescriptionGroupAttributes = GroupAttributes;
pub type ThingDescriptionGroup = Group;
pub type ThingDescriptionMeta = ResourceMeta;
pub type ThingDescriptionMetaAttributes = ResourceMetaAttributes;

#[derive(Debug, Clone)]
pub struct ThingDescription {
    /// The default version of the thing description.
    pub default_version: ThingDescriptionVersion,
    /// Extension-specific attributes (e.g., `format` and `content_type` for thing descriptions).
    pub extensions: HashMap<String, Vec<u8>>,
    /// Resource identifier within the group.
    pub id: String,
    /// The 'meta' Field.
    pub meta: ThingDescriptionMeta,
    /// The 'versionsCount' Field.
    pub versions_count: u64,
    /// Full XID path.
    pub xid: String,
}

#[derive(Debug, Clone)]
pub struct ThingDescriptionVersion {
    /// The versionId of this version's ancestor, or this version's versionId if it has no ancestor.
    pub ancestor: String,
    /// The 'createdAt' Field.
    pub created_at: DateTime<Utc>,
    /// The 'description' Field.
    pub description: Option<String>,
    /// The 'documentation' Field.
    pub documentation: Option<String>,
    /// The 'epoch' Field.
    pub epoch: u64,
    /// Extension-specific attributes.
    pub extensions: HashMap<String, Vec<u8>>,
    /// The 'labels' Field.
    pub labels: HashMap<String, String>,
    /// The 'modifiedAt' Field.
    pub modified_at: DateTime<Utc>,
    /// The 'name' Field.
    pub name: Option<String>,
    /// Resource identifier.
    pub resource_id: String,
    /// Version identifier.
    pub version_id: String,
    /// The 'xid' Field.
    pub xid: String,
    // Thing Description extensions
    /// Content type of the thing description version.
    pub content_type: Option<String>,
    /// Thing description format identifier, e.g. `WoT-TD/1.1`.
    pub format: Option<String>,
    /// The thing description document as a byte array.
    pub thing_description_document: Vec<u8>,
}

#[derive(Debug, Clone, Builder)]
#[builder(setter(into, strip_option))]
pub struct CreateThingDescriptionVersionAttributes {
    /// The version id for this thing description version.
    pub version_id: String,
    /// The versionId of this version's ancestor if it has an ancestor.
    #[builder(default = "None")]
    pub ancestor: Option<String>,
    /// Content type of the thing description version.
    // TODO: do we want these to be required here?
    #[builder(default = "None")]
    pub content_type: Option<String>,
    /// The 'description' Field.
    #[builder(default = "None")]
    pub description: Option<String>,
    /// The 'documentation' Field.
    #[builder(default = "None")]
    pub documentation: Option<String>,
    /// Thing description format identifier, e.g. `WoT-TD/1.1`.
    #[builder(default = "ThingDescriptionFormat::WotTd11")]
    pub format: ThingDescriptionFormat,
    /// The 'labels' Field.
    #[builder(default)]
    pub labels: HashMap<String, String>,
    /// The 'name' Field.
    #[builder(default = "None")]
    pub name: Option<String>,
    /// Base64-encoded thing description document content.
    pub thing_description_document: Vec<u8>,
}

#[derive(Debug, Clone)]
#[non_exhaustive]
pub enum ThingDescriptionFormat {
    /// WoT TD 1.1 format
    WotTd11,
    /// Custom specified format. May be rejected by the service
    Other(String),
}

impl From<ThingDescriptionFormat> for String {
    fn from(format: ThingDescriptionFormat) -> Self {
        match format {
            ThingDescriptionFormat::WotTd11 => er_client_gen::WOT_TD11.to_string(),
            ThingDescriptionFormat::Other(s) => s,
        }
    }
}

impl From<er_client_gen::ThingDescription> for ThingDescription {
    fn from(td: er_client_gen::ThingDescription) -> Self {
        ThingDescription {
            default_version: (td.resource.default_version, td.default_version_extensions).into(),
            extensions: td
                .resource
                .extensions
                .into_iter()
                .map(|(k, v)| (k, v.0))
                .collect(),
            id: td.resource.id,
            meta: td.resource.meta.into(),
            versions_count: td.resource.versions_count,
            xid: td.resource.xid,
        }
    }
}

impl From<er_client_gen::ThingDescriptionVersion> for ThingDescriptionVersion {
    fn from(tdv: er_client_gen::ThingDescriptionVersion) -> Self {
        (tdv.version, tdv.extensions).into()
    }
}

impl
    From<(
        er_client_gen::Version,
        er_client_gen::ThingDescriptionVersionExtensions,
    )> for ThingDescriptionVersion
{
    fn from(
        (version, extensions): (
            er_client_gen::Version,
            er_client_gen::ThingDescriptionVersionExtensions,
        ),
    ) -> Self {
        ThingDescriptionVersion {
            ancestor: version.ancestor,
            created_at: version.created_at,
            description: version.description,
            documentation: version.documentation,
            epoch: version.epoch,
            extensions: version
                .extensions
                .into_iter()
                .map(|(k, v)| (k, v.0))
                .collect(),
            labels: version.labels,
            modified_at: version.modified_at,
            name: version.name,
            resource_id: version.resource_id,
            version_id: version.version_id,
            xid: version.xid,
            content_type: extensions.content_type,
            format: extensions.format,
            thing_description_document: extensions.thing_description_document.0,
        }
    }
}

impl std::fmt::Display for ThingDescription {
    fn fmt(&self, f: &mut std::fmt::Formatter<'_>) -> std::fmt::Result {
        let extensions = self
            .extensions
            .iter()
            .map(|(k, v)| (k, v.len()))
            .collect::<Vec<_>>();
        write!(
            f,
            "Thing Description {{ 
                id: {}, 
                xid: {}, 
                versions_count: {}, 
                default_version: {}, 
                extensions: {extensions:?}, 
                meta: {} 
            }}",
            self.id, self.xid, self.versions_count, self.default_version, self.meta
        )
    }
}

impl std::fmt::Display for ThingDescriptionVersion {
    fn fmt(&self, f: &mut std::fmt::Formatter<'_>) -> std::fmt::Result {
        let extensions = self
            .extensions
            .iter()
            .map(|(k, v)| (k, v.len()))
            .collect::<Vec<_>>();
        write!(
            f,
            "ThingDescriptionVersion {{ 
                resource_id: {}, 
                version_id: {}, 
                ancestor: {}, 
                name: {:?}, 
                description: {:?}, 
                format: {:?}, 
                content_type: {:?}, 
                created_at: {},
                modified_at: {},
                documentation: {:?},
                epoch: {},
                xid: {},
                extensions: {extensions:?},
                labels: {:?},
                thing_description_document length: {:?}   
            }}",
            self.resource_id,
            self.version_id,
            self.ancestor,
            self.name,
            self.description,
            self.format,
            self.content_type,
            self.created_at,
            self.modified_at,
            self.documentation,
            self.epoch,
            self.xid,
            self.labels,
            self.thing_description_document.len()
        )
    }
}
