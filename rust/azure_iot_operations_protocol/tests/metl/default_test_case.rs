// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

use serde::Deserialize;

use crate::metl::default_action::DefaultAction;
use crate::metl::default_prologue::DefaultPrologue;

#[derive(Deserialize, Debug)]
pub struct DefaultTestCase {
    #[serde(rename = "prologue")]
    pub prologue: Option<DefaultPrologue>,

    #[serde(rename = "actions")]
    pub actions: Option<DefaultAction>,
}
