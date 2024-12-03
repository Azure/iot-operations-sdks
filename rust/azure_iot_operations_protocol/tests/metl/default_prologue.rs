// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

use serde::Deserialize;

use crate::metl::default_executor::DefaultExecutor;
use crate::metl::default_invoker::DefaultInvoker;

#[derive(Deserialize, Debug)]
pub struct DefaultPrologue {
    #[serde(rename = "executor")]
    pub executor: Option<DefaultExecutor>,

    #[serde(rename = "invoker")]
    pub invoker: Option<DefaultInvoker>,
}
