// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

use serde::Deserialize;

#[derive(Deserialize, Debug)]
pub struct DefaultSender {
    #[serde(rename = "telemetry-name")]
    pub telemetry_name: Option<String>,

    #[serde(rename = "telemetry-topic")]
    pub telemetry_topic: Option<String>,

    #[serde(rename = "model-id")]
    pub model_id: Option<String>,

    #[serde(rename = "data-schema")]
    pub data_schema: Option<String>,

    #[serde(rename = "topic-namespace")]
    pub topic_namespace: Option<String>,
}
