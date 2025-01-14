// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

use serde::{Deserialize, Serialize};
use serde_json;

use azure_iot_operations_protocol::common::payload_serialize::{FormatIndicator, PayloadSerialize};

#[derive(Clone, Serialize, Deserialize, Debug)]
pub struct TestPayload {
    #[serde(skip_serializing_if = "Option::is_none")]
    pub payload: Option<String>,

    // The 'testCaseIndex' Field.
    #[serde(rename = "testCaseIndex")]
    #[serde(skip_serializing_if = "Option::is_none")]
    pub test_case_index: Option<i32>,
}

impl PayloadSerialize for TestPayload {
    type Error = serde_json::Error;

    fn content_type() -> &'static str {
        "application/json"
    }

    fn format_indicator() -> FormatIndicator {
        FormatIndicator::Utf8EncodedCharacterData
    }

    fn serialize(self) -> Result<Vec<u8>, Self::Error> {
        serde_json::to_vec(&self)
    }

    fn deserialize(payload: &[u8]) -> Result<Self, Self::Error> {
        serde_json::from_slice(payload)
    }
}
