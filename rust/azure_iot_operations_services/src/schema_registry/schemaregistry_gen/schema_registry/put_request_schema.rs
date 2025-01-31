/* Code generated by Azure.Iot.Operations.ProtocolCompiler v0.6.0.0; DO NOT EDIT. */
#![allow(unused_imports)]

use std::collections::HashMap;

use chrono::{DateTime, Utc};
use derive_builder::Builder;
use iso8601_duration::Duration;
use serde::{Deserialize, Serialize};
use uuid::Uuid;

use super::super::common_types::{b64::Bytes, date_only::Date, decimal::Decimal, time_only::Time};
use super::format::Format;
use super::schema_type::SchemaType;

#[derive(Serialize, Deserialize, Debug, Clone, Builder)]
pub struct PutRequestSchema {
    /// The 'description' Field.
    #[serde(skip_serializing_if = "Option::is_none")]
    #[builder(default = "None")]
    pub description: Option<String>,

    /// The 'displayName' Field.
    #[serde(rename = "displayName")]
    #[serde(skip_serializing_if = "Option::is_none")]
    #[builder(default = "None")]
    pub display_name: Option<String>,

    /// The 'format' Field.
    #[serde(skip_serializing_if = "Option::is_none")]
    #[builder(default = "None")]
    pub format: Option<Format>,

    /// The 'schemaContent' Field.
    #[serde(rename = "schemaContent")]
    #[serde(skip_serializing_if = "Option::is_none")]
    #[builder(default = "None")]
    pub schema_content: Option<String>,

    /// The 'schemaType' Field.
    #[serde(rename = "schemaType")]
    #[serde(skip_serializing_if = "Option::is_none")]
    #[builder(default = "None")]
    pub schema_type: Option<SchemaType>,

    /// The 'tags' Field.
    #[serde(skip_serializing_if = "Option::is_none")]
    #[builder(default = "None")]
    pub tags: Option<HashMap<String, String>>,

    /// The 'version' Field.
    #[serde(skip_serializing_if = "Option::is_none")]
    #[builder(default = "None")]
    pub version: Option<String>,
}
