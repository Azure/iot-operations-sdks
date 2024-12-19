// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

use std::collections::HashMap;

use azure_iot_operations_protocol::common::aio_protocol_error::AIOProtocolErrorKind;
use serde::Deserialize;

use crate::metl::optional_field::deserialize_optional_field;

pub const HEADER_NAME_KEY: &str = "header-name";
pub const HEADER_VALUE_KEY: &str = "header-value";
pub const TIMEOUT_NAME_KEY: &str = "timeout-name";
pub const TIMEOUT_VALUE_KEY: &str = "timeout-value";
pub const PROPERTY_NAME_KEY: &str = "property-name";
pub const PROPERTY_VALUE_KEY: &str = "property-value";
pub const COMMAND_NAME_KEY: &str = "command-name";

#[derive(Clone, Deserialize, Debug)]
#[allow(dead_code)]
pub struct TestCaseCatch {
    #[serde(rename = "error-kind")]
    pub error_kind: String,

    #[serde(rename = "in-application")]
    pub in_application: Option<bool>,

    #[serde(rename = "is-shallow")]
    pub is_shallow: Option<bool>,

    #[serde(rename = "is-remote")]
    pub is_remote: Option<bool>,

    #[serde(rename = "status-code")]
    #[serde(default)]
    #[serde(deserialize_with = "deserialize_optional_field")]
    #[allow(clippy::option_option)]
    pub status_code: Option<Option<u16>>,

    #[serde(rename = "message")]
    pub message: Option<String>,

    #[serde(rename = "supplemental")]
    #[serde(default)]
    pub supplemental: HashMap<String, Option<String>>,
}

impl TestCaseCatch {
    pub fn get_error_kind(&self) -> AIOProtocolErrorKind {
        match self.error_kind.as_str() {
            "missing header" => AIOProtocolErrorKind::HeaderMissing,
            "invalid header" => AIOProtocolErrorKind::HeaderInvalid,
            "invalid payload" => AIOProtocolErrorKind::PayloadInvalid,
            "timeout" => AIOProtocolErrorKind::Timeout,
            "cancellation" => AIOProtocolErrorKind::Cancellation,
            "invalid configuration" => AIOProtocolErrorKind::ConfigurationInvalid,
            "invalid argument" => AIOProtocolErrorKind::ArgumentInvalid,
            "invalid state" => AIOProtocolErrorKind::StateInvalid,
            "internal logic error" => AIOProtocolErrorKind::InternalLogicError,
            "unknown error" => AIOProtocolErrorKind::UnknownError,
            "invocation error" => AIOProtocolErrorKind::InvocationException,
            "execution error" => AIOProtocolErrorKind::ExecutionException,
            "mqtt error" => AIOProtocolErrorKind::ClientError,
            "request version not supported" => AIOProtocolErrorKind::UnsupportedRequestVersion,
            "response version not supported" => AIOProtocolErrorKind::UnsupportedResponseVersion,
            _ => panic!("Unrecognized error kind"),
        }
    }
}
