// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

use serde::Deserialize;

#[derive(Clone, Deserialize, Debug)]
#[allow(dead_code)]
pub struct TestCaseSync {
    #[serde(rename = "signal-event")]
    pub signal_event: Option<String>,

    #[serde(rename = "wait-event")]
    pub wait_event: Option<String>,
}
