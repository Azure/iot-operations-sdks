// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

use serde::Deserialize;

#[derive(Clone, Deserialize, Debug)]
#[allow(dead_code)]
pub struct TestCaseCloudEvent {
    #[serde(rename = "source")]
    pub source: Option<String>,

    #[serde(rename = "type")]
    pub event_type: Option<String>,

    #[serde(rename = "spec-version")]
    pub spec_version: Option<String>,

    #[serde(rename = "data-content-type")]
    pub data_content_type: Option<String>,

    #[serde(rename = "subject")]
    pub subject: Option<String>,

    #[serde(rename = "data-schema")]
    pub data_schema: Option<String>,
}
