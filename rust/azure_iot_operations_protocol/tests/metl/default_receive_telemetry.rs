// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

use serde::Deserialize;

use crate::metl::test_case_duration::TestCaseDuration;

#[derive(Deserialize, Debug)]
pub struct DefaultReceiveTelemetry {
    #[serde(rename = "topic")]
    pub topic: Option<String>,

    #[serde(rename = "payload")]
    pub payload: Option<String>,

    #[serde(rename = "content-type")]
    pub content_type: Option<String>,

    #[serde(rename = "format-indicator")]
    pub format_indicator: Option<u8>,

    #[serde(rename = "qos")]
    pub qos: Option<i32>,

    #[serde(rename = "message-expiry")]
    pub message_expiry: Option<TestCaseDuration>,

    #[serde(rename = "source-index")]
    pub source_index: Option<i32>,
}
