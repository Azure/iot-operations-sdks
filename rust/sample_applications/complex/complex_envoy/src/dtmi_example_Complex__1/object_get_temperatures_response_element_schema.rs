/* Code generated by Azure.Iot.Operations.ProtocolCompiler v0.6.0.0; DO NOT EDIT. */
#![allow(non_camel_case_types)]
#![allow(unused_imports)]

use std::collections::HashMap;

use chrono::{DateTime, Utc};
use derive_builder::Builder;
use iso8601_duration::Duration;
use serde::{Deserialize, Serialize};
use uuid::Uuid;

use super::super::common_types::{b64::Bytes, date_only::Date, decimal::Decimal, time_only::Time};
use super::enum_test_result__1::Enum_Test_Result__1;

#[derive(Serialize, Deserialize, Debug, Clone, Builder)]
pub struct Object_GetTemperatures_Response_ElementSchema {
    /// The 'city' Field.
    #[serde(skip_serializing_if = "Option::is_none")]
    #[builder(default = "None")]
    pub city: Option<String>,

    /// The 'result' Field.
    #[serde(skip_serializing_if = "Option::is_none")]
    #[builder(default = "None")]
    pub result: Option<Enum_Test_Result__1>,

    /// The 'temperature' Field.
    #[serde(skip_serializing_if = "Option::is_none")]
    #[builder(default = "None")]
    pub temperature: Option<f64>,
}
