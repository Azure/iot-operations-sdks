/* Code generated by Azure.Iot.Operations.ProtocolCompiler v0.10.0.0; DO NOT EDIT. */
#![allow(unused_imports)]

use std::collections::HashMap;

use chrono::{DateTime, Utc};
use derive_builder::Builder;
use iso8601_duration::Duration;
use serde::{Deserialize, Serialize};
use uuid::Uuid;

use super::put_request_schema::PutRequestSchema;
use super::super::common_types::{b64::Bytes, date_only::Date, decimal::Decimal, time_only::Time};

#[derive(Serialize, Deserialize, Debug, Clone, Builder)]
pub struct PutRequestPayload {
    /// The Command request argument.
    #[serde(rename = "putSchemaRequest")]
    pub put_schema_request: PutRequestSchema,
}
