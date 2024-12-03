// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

use serde::Deserialize;

use crate::metl::test_error_kind::TestErrorKind;

#[derive(Clone, Deserialize, Debug)]
#[allow(dead_code)]
pub struct TestCaseError {
    #[serde(rename = "kind")]
    pub kind: TestErrorKind,

    #[serde(rename = "message")]
    pub message: Option<String>,

    #[serde(rename = "property-name")]
    pub property_name: Option<String>,

    #[serde(rename = "property-value")]
    pub property_value: Option<String>,
}
