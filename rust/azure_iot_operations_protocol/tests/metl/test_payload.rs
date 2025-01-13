// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

use serde::{Deserialize, Serialize};
use serde_json;

use azure_iot_operations_protocol::common::payload_serialize::{FormatIndicator, PayloadError, PayloadSerialize, SerializedPayload};

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

    fn serialize(self) -> Result<SerializedPayload, Self::Error> {
        match serde_json::to_vec(&self) {
            Ok(payload) => Ok(SerializedPayload {
                payload,
                content_type: "application/json",
                format_indicator: FormatIndicator::Utf8EncodedCharacterData,
            }),
            Err(e) => Err(e),
        }
    }

    fn deserialize(payload: &[u8], content_type: &Option<String>, _format_indicator: &FormatIndicator) -> Result<Self, PayloadError<Self::Error>> {
        if *content_type != Some("application/json".to_string()) {
            return Err(PayloadError::UnsupportedContentType(format!("Invalid content type: '{content_type:?}'. Must be 'application/json'")));
            // return Err(serde_json::Error::custom(format!("Invalid content type: '{:?}'. Must be 'application/json'", content_type)));
        }
        serde_json::from_slice(payload).map_err(|e| PayloadError::DeserializationError(e))
    }
}
