// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

use std::collections::HashMap;

use serde::Deserialize;

use crate::metl::test_case_duration::TestCaseDuration;

#[derive(Deserialize, Debug)]
pub struct DefaultExecutor {
    #[serde(rename = "command-name")]
    pub command_name: Option<String>,

    #[serde(rename = "request-topic")]
    pub request_topic: Option<String>,

    #[serde(rename = "executor-id")]
    pub executor_id: Option<String>,

    #[serde(rename = "topic-namespace")]
    pub topic_namespace: Option<String>,

    #[serde(rename = "idempotent")]
    pub idempotent: Option<bool>,

    #[serde(rename = "cache-ttl")]
    pub cache_ttl: Option<TestCaseDuration>,

    #[serde(rename = "execution-timeout")]
    pub execution_timeout: Option<TestCaseDuration>,

    #[serde(rename = "request-responses-map")]
    pub request_responses_map: Option<HashMap<String, Vec<String>>>,

    #[serde(rename = "execution-concurrency")]
    pub execution_concurrency: Option<i32>,
}
