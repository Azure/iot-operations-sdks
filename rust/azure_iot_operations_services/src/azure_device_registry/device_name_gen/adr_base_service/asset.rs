/* Code generated by Azure.Iot.Operations.ProtocolCompiler v0.10.0.0; DO NOT EDIT. */
#![allow(unused_imports)]

use std::collections::HashMap;

use chrono::{DateTime, Utc};
use derive_builder::Builder;
use iso8601_duration::Duration;
use serde::{Deserialize, Serialize};
use uuid::Uuid;

use super::asset_specification_schema::AssetSpecificationSchema;
use super::asset_status::AssetStatus;
use super::super::common_types::{b64::Bytes, date_only::Date, decimal::Decimal, time_only::Time};

#[derive(Serialize, Deserialize, Debug, Clone, Builder)]
pub struct Asset {
    /// The 'name' Field.
    pub name: String,

    /// The 'specification' Field.
    pub specification: AssetSpecificationSchema,

    /// The 'status' Field.
    #[serde(skip_serializing_if = "Option::is_none")]
    #[builder(default = "None")]
    pub status: Option<AssetStatus>,
}
