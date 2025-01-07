// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

use serde::Deserialize;

#[derive(Clone, Deserialize, Debug)]
#[allow(dead_code)]
pub struct TestCaseMqttConfig {
    #[serde(rename = "client-id")]
    pub client_id: Option<String>,
}

impl TestCaseMqttConfig {
    pub fn get_default() -> Self {
        Self { client_id: None }
    }
}
