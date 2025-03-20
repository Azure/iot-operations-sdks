/* Code generated by Azure.Iot.Operations.ProtocolCompiler v0.10.0.0; DO NOT EDIT. */
#![allow(unused_imports)]

use std::collections::HashMap;

use chrono::{DateTime, Utc};
use derive_builder::Builder;
use iso8601_duration::Duration;
use serde::{Deserialize, Serialize};
use uuid::Uuid;

use super::super::common_types::{b64::Bytes, date_only::Date, decimal::Decimal, time_only::Time};

#[derive(Serialize, Deserialize, Debug, Clone, Builder)]
pub struct TelemetryCollection {
    /// The current value of the counter.
    #[serde(rename = "CounterValue")]
    #[serde(skip_serializing_if = "Option::is_none")]
    #[builder(default = "None")]
    pub counter_value: Option<i32>,
}
