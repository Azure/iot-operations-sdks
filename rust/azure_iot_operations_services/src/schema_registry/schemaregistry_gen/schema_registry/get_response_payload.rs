/* Code generated by Azure.Iot.Operations.ProtocolCompiler v0.6.0.0; DO NOT EDIT. */
#![allow(unused_imports)]

use std::collections::HashMap;

use chrono::{DateTime, Utc};
use derive_builder::Builder;
use iso8601_duration::Duration;
use serde::{Deserialize, Serialize};
use uuid::Uuid;

use super::super::common_types::{b64::Bytes, date_only::Date, decimal::Decimal, time_only::Time};
use super::schema::Schema;

#[derive(Serialize, Deserialize, Debug, Clone, Builder)]
pub struct GetResponsePayload {
    /// The Command response argument.
    #[serde(skip_serializing_if = "Option::is_none")]
    #[builder(default = "None")]
    pub schema: Option<Schema>,
}
