// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

use serde::Deserialize;

use crate::metl::optional_field::deserialize_optional_field;

#[derive(Clone, Deserialize, Debug)]
#[allow(dead_code)]
pub struct TestCaseCloudEvent {
    #[serde(rename = "source")]
    pub source: Option<String>,

    #[serde(rename = "type")]
    pub event_type: Option<String>,

    #[serde(rename = "spec-version")]
    pub spec_version: Option<String>,

    #[serde(rename = "id")]
    pub id: Option<String>,

    #[serde(rename = "time")]
    #[serde(default)]
    #[serde(deserialize_with = "deserialize_optional_field")]
    #[allow(clippy::option_option)]
    pub time: Option<Option<String>>,

    #[serde(rename = "data-content-type")]
    pub data_content_type: Option<String>,

    #[serde(rename = "subject")]
    #[serde(default)]
    #[serde(deserialize_with = "deserialize_optional_field")]
    #[allow(clippy::option_option)]
    pub subject: Option<Option<String>>,

    #[serde(rename = "data-schema")]
    #[serde(default)]
    #[serde(deserialize_with = "deserialize_optional_field")]
    #[allow(clippy::option_option)]
    pub data_schema: Option<Option<String>>,
}
