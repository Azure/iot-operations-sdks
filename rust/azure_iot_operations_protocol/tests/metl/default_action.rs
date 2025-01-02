// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

use serde::Deserialize;

use crate::metl::default_invoke_command::DefaultInvokeCommand;
use crate::metl::default_receive_request::DefaultReceiveRequest;
use crate::metl::default_receive_response::DefaultReceiveResponse;

#[derive(Deserialize, Debug)]
pub struct DefaultAction {
    #[serde(rename = "invoke-command")]
    pub invoke_command: Option<DefaultInvokeCommand>,

    #[serde(rename = "receive-request")]
    pub receive_request: Option<DefaultReceiveRequest>,

    #[serde(rename = "receive-response")]
    pub receive_response: Option<DefaultReceiveResponse>,
}
