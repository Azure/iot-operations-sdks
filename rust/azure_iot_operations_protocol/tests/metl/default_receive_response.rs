// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

use serde::Deserialize;

use crate::metl::test_case_duration::TestCaseDuration;

#[derive(Deserialize, Debug)]
pub struct DefaultReceiveResponse {
    #[serde(rename = "topic")]
    pub topic: Option<String>,

    #[serde(rename = "payload")]
    pub payload: Option<String>,

    #[serde(rename = "content-type")]
    pub content_type: Option<String>,

    #[serde(rename = "format-indicator")]
    pub format_indicator: Option<u8>,

    #[serde(rename = "correlation-index")]
    pub correlation_index: Option<i32>,

    #[serde(rename = "qos")]
    pub qos: Option<i32>,

    #[serde(rename = "message-expiry")]
    pub message_expiry: Option<TestCaseDuration>,

    #[serde(rename = "status")]
    pub status: Option<String>,

    #[serde(rename = "status-message")]
    pub status_message: Option<String>,

    #[serde(rename = "is-application-error")]
    pub is_application_error: Option<String>,

    #[serde(rename = "invalid-property-name")]
    pub invalid_property_name: Option<String>,

    #[serde(rename = "invalid-property-value")]
    pub invalid_property_value: Option<String>,
}
