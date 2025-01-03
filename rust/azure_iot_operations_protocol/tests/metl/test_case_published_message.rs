// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

use std::collections::HashMap;

use serde::Deserialize;

use crate::metl::optional_field::deserialize_optional_field;

#[derive(Clone, Deserialize, Debug)]
#[allow(dead_code)]
pub struct TestCasePublishedMessage {
    #[serde(rename = "correlation-index")]
    pub correlation_index: Option<i32>,

    #[serde(rename = "topic")]
    pub topic: Option<String>,

    #[serde(rename = "payload")]
    #[serde(default)]
    #[serde(deserialize_with = "deserialize_optional_field")]
    #[allow(clippy::option_option)]
    pub payload: Option<Option<String>>,

    #[serde(rename = "metadata")]
    #[serde(default)]
    pub metadata: HashMap<String, Option<String>>,

    #[serde(rename = "command-status")]
    #[serde(default)]
    #[serde(deserialize_with = "deserialize_optional_field")]
    #[allow(clippy::option_option)]
    pub command_status: Option<Option<i32>>,

    #[serde(rename = "is-application-error")]
    pub is_application_error: Option<bool>,

    #[serde(rename = "source-id")]
    pub source_id: Option<String>,

    #[serde(rename = "expiry")]
    pub expiry: Option<u32>,
}
