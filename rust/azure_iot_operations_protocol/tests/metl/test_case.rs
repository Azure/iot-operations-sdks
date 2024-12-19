// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

use std::marker::PhantomData;

use serde::Deserialize;

use crate::metl::defaults::DefaultsType;
use crate::metl::test_case_action::TestCaseAction;
use crate::metl::test_case_description::TestCaseDescription;
use crate::metl::test_case_epilogue::TestCaseEpilogue;
use crate::metl::test_case_prologue::TestCasePrologue;
use crate::metl::test_feature_kind::TestFeatureKind;

#[derive(Clone, Deserialize, Debug)]
#[allow(dead_code)]
pub struct TestCase<T: DefaultsType + Default> {
    #[serde(default)]
    pub defaults_type: PhantomData<T>,

    #[serde(rename = "test-name")]
    pub test_name: String,

    #[serde(rename = "description")]
    pub description: TestCaseDescription,

    #[serde(rename = "requires")]
    #[serde(default)]
    pub requires: Vec<TestFeatureKind>,

    #[serde(rename = "prologue")]
    pub prologue: TestCasePrologue<T>,

    #[serde(rename = "actions")]
    #[serde(default)]
    pub actions: Vec<TestCaseAction<T>>,

    #[serde(rename = "epilogue")]
    pub epilogue: Option<TestCaseEpilogue>,
}
