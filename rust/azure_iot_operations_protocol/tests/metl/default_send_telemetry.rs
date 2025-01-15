// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

use serde::Deserialize;

use crate::metl::test_case_duration::TestCaseDuration;

#[derive(Deserialize, Debug)]
pub struct DefaultSendTelemetry {
    #[serde(rename = "telemetry-name")]
    pub telemetry_name: Option<String>,

    #[serde(rename = "telemetry-value")]
    pub telemetry_value: Option<String>,

    #[serde(rename = "timeout")]
    pub timeout: Option<TestCaseDuration>,

    #[serde(rename = "qos")]
    pub qos: Option<i32>,
}
