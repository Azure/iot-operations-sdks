/* Code generated by Azure.Iot.Operations.ProtocolCompiler v0.10.0.0; DO NOT EDIT. */
#![allow(unused_imports)]

use std::collections::HashMap;

use chrono::{DateTime, Utc};
use derive_builder::Builder;
use iso8601_duration::Duration;
use serde::{Deserialize, Serialize};
use uuid::Uuid;

use super::device_status_config_schema::DeviceStatusConfigSchema;
use super::device_status_endpoint_schema::DeviceStatusEndpointSchema;
use super::super::common_types::{b64::Bytes, date_only::Date, decimal::Decimal, time_only::Time};

#[derive(Serialize, Deserialize, Debug, Clone, Builder)]
pub struct DeviceStatus {
    /// The 'config' Field.
    #[serde(skip_serializing_if = "Option::is_none")]
    #[builder(default = "None")]
    pub config: Option<DeviceStatusConfigSchema>,

    /// The 'endpoints' Field.
    #[serde(skip_serializing_if = "Option::is_none")]
    #[builder(default = "None")]
    pub endpoints: Option<DeviceStatusEndpointSchema>,
}
