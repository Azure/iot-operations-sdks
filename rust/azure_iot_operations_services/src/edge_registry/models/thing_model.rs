// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

//! Typed models for the xRegistry Thing Model extension.

use std::collections::HashMap;

use bytes::Bytes;
use chrono::{DateTime, Utc};
use derive_builder::Builder;

use crate::edge_registry::edge_registry_gen::common_types::b64::{self};
use crate::edge_registry::edge_registry_gen::edge_registry::client as client_gen;
use crate::edge_registry::models::xregistry::{Validated, VersionXId};
use crate::edge_registry::models::{
    extensions_from_gen, extensions_to_gen, labels_from_gen, labels_to_gen,
};
use crate::edge_registry::{JSON_LD11, Label};

/// A specific Version of a Thing Model Resource.
#[derive(Debug, Clone)]
pub struct ThingModelVersionEntity {
    /// Version identifier.
    pub version_id: u64,
    /// Resource identifier.
    pub resource_id: String,
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
    pub labels: Vec<Label>,
    /// The date/time of when the entity was created.
    pub created_at: DateTime<Utc>,
    /// The date/time of when the entity was last updated.
    pub modified_at: DateTime<Utc>,
    /// The versionId of this Version's ancestor, or this Version's versionId if it has no ancestor.
    pub ancestor: u64,
    /// The media type of the entity as defined by RFC9110.
    pub content_type: Option<String>,
    /// Identifies what the Version represents.
    pub format: ThingModelFormat,
    /// When format validation is enabled, indicates whether the server has validated that the
    /// Version conforms to the rules defined by its `format` attribute.
    pub format_validated: Option<Validated>,
    /// When compatibility validation is enabled, indicates whether the server has validated that
    /// the Version conforms to the rules defined by its Resource's `meta.compatibility` attribute.
    pub compatibility_validated: Option<Validated>,
    /// The raw document content for this Version.
    pub document: Bytes,
    /// The hash of the document content for this Version.
    pub document_hash: String,
    /// Extension-specific attributes.
    pub extensions: HashMap<String, Bytes>,
}

impl From<client_gen::ThingModelVersion> for ThingModelVersionEntity {
    fn from(value: client_gen::ThingModelVersion) -> Self {
        ThingModelVersionEntity {
            version_id: value.version_id,
            resource_id: value.resource_id,
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
            format: value.format.into(),
            format_validated: value.format_validated.map(Validated::from),
            compatibility_validated: value.compatibility_validated.map(Validated::from),
            document: Bytes::from(value.document.0),
            document_hash: value.document_hash,
            extensions: extensions_from_gen(value.extensions),
        }
    }
}

impl From<client_gen::ThingModelVersionXid> for VersionXId<u64> {
    fn from(value: client_gen::ThingModelVersionXid) -> Self {
        VersionXId {
            group_type: value.group_type,
            group_id: value.group_id,
            resource_type: value.resource_type,
            resource_id: value.resource_id,
            version_id: value.version_id,
        }
    }
}

impl From<client_gen::ThingModelVersionXidList> for Vec<VersionXId<u64>> {
    fn from(value: client_gen::ThingModelVersionXidList) -> Self {
        value.versions.into_iter().map(Into::into).collect()
    }
}

/// Format of a Thing Model Version document.
///
/// The known variants mirror the formats defined by the xRegistry Thing Model extension; any other
/// identifier can be supplied via [`Custom`](ThingModelFormat::Custom).
#[derive(Debug, Clone, PartialEq, Eq)]
#[non_exhaustive]
pub enum ThingModelFormat {
    /// JSON-LD 1.1 format.
    JsonLd11,
    /// A format identifier not covered by the known variants.
    Custom(String),
}

impl From<ThingModelFormat> for String {
    fn from(value: ThingModelFormat) -> Self {
        match value {
            ThingModelFormat::JsonLd11 => JSON_LD11.to_string(),
            ThingModelFormat::Custom(format) => format,
        }
    }
}

impl From<String> for ThingModelFormat {
    fn from(value: String) -> Self {
        match value.as_str() {
            JSON_LD11 => ThingModelFormat::JsonLd11,
            _ => ThingModelFormat::Custom(value),
        }
    }
}

/// Attributes needed to create a Thing Model Version.
#[derive(Debug, Clone, Builder)]
pub struct ThingModelVersionAttributes {
    /// Human-readable name.
    #[builder(default = "None")]
    pub name: Option<String>,
    /// A human-readable summary of the purpose of the entity.
    #[builder(default = "None")]
    pub description: Option<String>,
    /// A URL to additional information about this entity.
    #[builder(default = "None")]
    pub documentation: Option<String>,
    /// A URL to a graphical icon for the owning entity.
    #[builder(default = "None")]
    pub icon: Option<String>,
    /// Queryable Key Value pairs to be added to the Version.
    #[builder(default)]
    pub labels: Vec<Label>,
    /// The versionId of this Version's ancestor if it has an ancestor.
    #[builder(default = "None")]
    pub ancestor: Option<u64>,
    /// Content type of the Version document.
    #[builder(default = "None")]
    pub content_type: Option<String>,
    /// Format of the Version document.
    pub format: ThingModelFormat,
    /// Document content for the Version.
    pub document: Bytes,
    /// Extension-specific attributes.
    #[builder(default)]
    pub extensions: HashMap<String, Bytes>,
}

impl ThingModelVersionAttributes {
    /// Builds the generated create payload, supplying the Group identifier and the Thing
    /// Model (parent Resource) labels that are carried alongside the Version attributes.
    pub(crate) fn into_gen(
        self,
        group_id: Option<String>,
        thing_model_labels: Vec<Label>,
    ) -> client_gen::CreateThingModelVersionAttributes {
        client_gen::CreateThingModelVersionAttributes {
            group_id,
            name: self.name,
            description: self.description,
            documentation: self.documentation,
            icon: self.icon,
            labels: labels_to_gen(self.labels),
            ancestor: self.ancestor,
            content_type: self.content_type,
            format: self.format.into(),
            document: b64::Bytes(self.document.to_vec()),
            extensions: extensions_to_gen(self.extensions),
            thing_model_labels: labels_to_gen(thing_model_labels),
        }
    }
}
