// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

use serde::Deserialize;

#[derive(Clone, Deserialize, Debug)]
#[allow(dead_code)]
pub struct TestCaseDescription {
    #[serde(rename = "condition")]
    pub condition: String,

    #[serde(rename = "expect")]
    pub expect: String,
}
