// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

use serde::Deserialize;

use crate::metl::default_invoke_command::DefaultInvokeCommand;
use crate::metl::default_receive_request::DefaultReceiveRequest;
use crate::metl::default_receive_response::DefaultReceiveResponse;
use crate::metl::default_receive_telemetry::DefaultReceiveTelemetry;
use crate::metl::default_send_telemetry::DefaultSendTelemetry;

#[derive(Deserialize, Debug)]
pub struct DefaultAction {
    #[serde(rename = "invoke-command")]
    pub invoke_command: Option<DefaultInvokeCommand>,

    #[serde(rename = "send-telemetry")]
    pub send_telemetry: Option<DefaultSendTelemetry>,

    #[serde(rename = "receive-request")]
    pub receive_request: Option<DefaultReceiveRequest>,

    #[serde(rename = "receive-response")]
    pub receive_response: Option<DefaultReceiveResponse>,

    #[serde(rename = "receive-telemetry")]
    pub receive_telemetry: Option<DefaultReceiveTelemetry>,
}
