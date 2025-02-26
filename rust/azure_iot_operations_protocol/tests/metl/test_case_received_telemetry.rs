// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

use std::collections::HashMap;

use serde::Deserialize;

use crate::metl::optional_field::deserialize_optional_field;
use crate::metl::test_case_cloud_event::TestCaseCloudEvent;

#[derive(Clone, Deserialize, Debug)]
#[allow(dead_code)]
pub struct TestCaseReceivedTelemetry {
    #[serde(rename = "telemetry-value")]
    #[serde(default)]
    #[serde(deserialize_with = "deserialize_optional_field")]
    #[allow(clippy::option_option)]
    pub telemetry_value: Option<Option<String>>,

    #[serde(rename = "metadata")]
    #[serde(default)]
    pub metadata: HashMap<String, Option<String>>,

    #[serde(rename = "topic-tokens")]
    #[serde(default)]
    pub topic_tokens: HashMap<String, String>,

    #[serde(rename = "cloud-event")]
    #[serde(default)]
    #[serde(deserialize_with = "deserialize_optional_field")]
    #[allow(clippy::option_option)]
    pub cloud_event: Option<Option<TestCaseCloudEvent>>,

    #[serde(rename = "source-index")]
    pub source_index: Option<i32>,
}
