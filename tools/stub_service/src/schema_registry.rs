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

const SERVICE_NAME: &str = "SchemaRegistry";
pub const CLIENT_ID: &str = "schema_registry_service_stub";
const NAMESPACE: &str = "aio-sr-ns-stub";

/// Supported schema formats
#[derive(Serialize, Deserialize, Debug, Clone, PartialEq, Eq)]
pub enum Format {
    #[serde(rename = "Delta/1.0")]
    Delta1,
    #[serde(rename = "JsonSchema/draft-07")]
    JsonSchemaDraft07,
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

/// Supported schema types
#[derive(Serialize, Deserialize, Debug, Clone, PartialEq, Eq)]
pub enum SchemaType {
    MessageSchema,
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
/// Schema object
#[derive(Serialize, Deserialize, Clone, Debug)]
struct Schema {
    /// Human-readable description of the schema.
    description: Option<String>,

    /// Human-readable display name.
    #[serde(rename = "displayName")]
    display_name: Option<String>,

    /// Format of the schema.
    format: Format,

    /// Hash of the schema content.
    hash: String,

    /// Schema name.
    name: String,

    /// Schema registry namespace. Uniquely identifies a schema registry within a tenant.
    pub namespace: String,

    /// Content stored in the schema.
    #[serde(rename = "schemaContent")]
    pub schema_content: String,

    /// Type of the schema.
    #[serde(rename = "schemaType")]
    pub schema_type: SchemaType,

    /// Schema tags.
    pub tags: HashMap<String, String>,

    /// Version of the schema. Allowed between 0-9.
    pub version: u32,
}

impl Ord for Schema {
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
            format: Some(schema.format.into()),
            hash: Some(schema.hash),
            name: Some(schema.name),
            namespace: Some(schema.namespace),
            schema_content: Some(schema.schema_content),
            schema_type: Some(schema.schema_type.into()),
            tags: Some(schema.tags),
            version: Some(schema.version.to_string()),
        }
    }
}

impl From<service_gen::PutRequestSchema> for Schema {
    fn from(put_request_schema: service_gen::PutRequestSchema) -> Self {
        // Create the hash of the schema content
        let schema_hash = {
            let mut hasher = DefaultHasher::new();
            let content = put_request_schema
                .schema_content
                .clone()
                .expect("Schema content is required");
            content.hash(&mut hasher);
            hasher.finish().to_string()
        };

        // Transfrom the put request schema into a Schema
        Self {
            description: put_request_schema.description,
            display_name: put_request_schema.display_name,
            format: put_request_schema
                .format
                .expect("Format is required")
                .into(),
            hash: schema_hash.clone(),
            name: schema_hash,
            namespace: NAMESPACE.to_string(),
            schema_content: put_request_schema
                .schema_content
                .expect("Schema content is required"),
            schema_type: put_request_schema
                .schema_type
                .expect("Schema type is required")
                .into(),
            tags: put_request_schema.tags.unwrap_or_default(),
            version: put_request_schema
                .version
                .expect("Schema version is required")
                .parse()
                .unwrap(), // TODO: Implement error handling for incorrect version number
        }
    }
}
