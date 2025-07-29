// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

//! Types for the Schema Registry stub service.

mod schema_registry_gen;
mod service;

use std::{
    collections::HashMap,
    hash::{DefaultHasher, Hash, Hasher},
};

pub use crate::schema_registry::service::Service;
use schema_registry_gen::schema_registry::service as service_gen;
use serde::{Deserialize, Serialize};

pub const SERVICE_NAME: &str = "schema_registry";
pub const CLIENT_ID: &str = "schema_registry_service_stub";
const NAMESPACE: &str = "aio-sr-ns-stub";

// ~~~~~~~~~~~~~~~~~~~DTDL Equivalent Structs and Enums~~~~~~~

/// Supported schema formats
#[derive(Debug, Clone, PartialEq, Eq, Serialize, Deserialize)]
pub enum Format {
    /// Delta/1.0
    #[serde(rename = "Delta/1.0")]
    Delta1,
    /// JsonSchema/draft-07
    #[serde(rename = "JsonSchema/draft-07")]
    JsonSchemaDraft07,
}

/// Supported schema types.
#[derive(Debug, Clone, PartialEq, Eq, Serialize, Deserialize)]
pub enum SchemaType {
    /// Message Schema
    #[serde(rename = "MessageSchema")]
    MessageSchema,
}

/// Schema object
#[derive(Debug, Clone, Serialize, Deserialize)]
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
    pub version: u32,
}

/// Request to get a schema from the schema registry.
#[derive(Clone, Debug, PartialEq, Eq)]
pub struct GetRequest {
    /// Schema name.
    name: String,
    /// Version of the schema. Allowed between 0-9.
    version: String,
}

impl From<service_gen::Format> for Format {
    fn from(format: service_gen::Format) -> Self {
        match format {
            service_gen::Format::Delta1 => Format::Delta1,
            service_gen::Format::JsonSchemaDraft07 => Format::JsonSchemaDraft07,
        }
    }
}

impl From<Format> for service_gen::Format {
    fn from(format: Format) -> Self {
        match format {
            Format::Delta1 => schema_registry_gen::schema_registry::service::Format::Delta1,
            Format::JsonSchemaDraft07 => {
                schema_registry_gen::schema_registry::service::Format::JsonSchemaDraft07
            }
        }
    }
}

impl From<service_gen::SchemaType> for SchemaType {
    fn from(schema_type: service_gen::SchemaType) -> Self {
        match schema_type {
            service_gen::SchemaType::MessageSchema => SchemaType::MessageSchema,
        }
    }
}

impl From<SchemaType> for service_gen::SchemaType {
    fn from(schema_type: SchemaType) -> Self {
        match schema_type {
            SchemaType::MessageSchema => service_gen::SchemaType::MessageSchema,
        }
    }
}

impl Ord for Schema {
    // Ordering done by version to ensure output is sorted by version
    fn cmp(&self, other: &Self) -> std::cmp::Ordering {
        self.version.cmp(&other.version)
    }
}

impl PartialOrd for Schema {
    fn partial_cmp(&self, other: &Self) -> Option<std::cmp::Ordering> {
        Some(self.cmp(other))
    }
}

impl Eq for Schema {}

impl PartialEq for Schema {
    fn eq(&self, other: &Self) -> bool {
        self.version == other.version && self.hash == other.hash
    }
}

impl From<Schema> for service_gen::Schema {
    fn from(schema: Schema) -> Self {
        Self {
            description: schema.description,
            display_name: schema.display_name,
            format: schema.format.into(),
            hash: schema.hash,
            name: schema.name,
            namespace: schema.namespace,
            schema_content: schema.schema_content,
            schema_type: schema.schema_type.into(),
            tags: if schema.tags.is_empty() {
                None
            } else {
                Some(schema.tags)
            },
            version: schema.version.to_string(),
        }
    }
}

impl TryFrom<service_gen::PutRequestSchema> for Schema {
    type Error = String;

    fn try_from(put_request_schema: service_gen::PutRequestSchema) -> Result<Self, Self::Error> {
        // Create the hash of the schema content
        let schema_hash = {
            let mut hasher = DefaultHasher::new();
            let content = put_request_schema.schema_content.clone();
            content.hash(&mut hasher);
            hasher.finish().to_string()
        };

        // Transform the put request schema into a Schema
        Ok(Self {
            description: put_request_schema.description,
            display_name: put_request_schema.display_name,
            format: put_request_schema.format.into(),
            hash: Some(schema_hash.clone()),
            name: schema_hash,
            namespace: NAMESPACE.to_string(),
            schema_content: put_request_schema.schema_content,
            schema_type: put_request_schema.schema_type.into(),
            tags: put_request_schema.tags.unwrap_or_default(),
            version: put_request_schema
                .version
                .parse()
                .map_err(|_| "Invalid schema version, skipping request".to_string())?,
        })
    }
}
