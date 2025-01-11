// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

use serde::Deserialize;

#[derive(Deserialize, Debug)]
pub struct DefaultInvoker {
    #[serde(rename = "command-name")]
    pub command_name: Option<String>,

    #[serde(rename = "request-topic")]
    pub request_topic: Option<String>,

    #[serde(rename = "topic-namespace")]
    pub topic_namespace: Option<String>,

    #[serde(rename = "response-topic-prefix")]
    pub response_topic_prefix: Option<String>,

    #[serde(rename = "response-topic-suffix")]
    pub response_topic_suffix: Option<String>,
}
