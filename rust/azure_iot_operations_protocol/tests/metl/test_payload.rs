// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

use serde::{Deserialize, Serialize};
use serde_json;

use azure_iot_operations_protocol::common::payload_serialize::{
    DeserializationError, FormatIndicator, PayloadSerialize, SerializedPayload,
};

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
        Ok(SerializedPayload {
            payload: serde_json::to_vec(&self)?,
            content_type: "application/json".to_string(),
            format_indicator: FormatIndicator::Utf8EncodedCharacterData,
        })
    }

    fn deserialize(
        payload: &[u8],
        content_type: &Option<String>,
        _format_indicator: &FormatIndicator,
    ) -> Result<Self, DeserializationError<Self::Error>> {
        if let Some(content_type) = content_type {
            if content_type != "application/json" {
                return Err(DeserializationError::UnsupportedContentType(format!(
                    "Invalid content type: '{content_type}'. Must be 'application/json'"
                )));
            }
        }
        serde_json::from_slice(payload).map_err(DeserializationError::InvalidPayload)
    }
}
