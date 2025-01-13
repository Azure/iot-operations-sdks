// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

use serde::Deserialize;

use crate::metl::default_executor::DefaultExecutor;
use crate::metl::default_invoker::DefaultInvoker;
use crate::metl::default_receiver::DefaultReceiver;
use crate::metl::default_sender::DefaultSender;

#[derive(Deserialize, Debug)]
pub struct DefaultPrologue {
    #[serde(rename = "executor")]
    pub executor: Option<DefaultExecutor>,

    #[serde(rename = "invoker")]
    pub invoker: Option<DefaultInvoker>,

    #[serde(rename = "receiver")]
    pub receiver: Option<DefaultReceiver>,

    #[serde(rename = "sender")]
    pub sender: Option<DefaultSender>,
}
