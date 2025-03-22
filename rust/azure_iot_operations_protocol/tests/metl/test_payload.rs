// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

use serde::{Deserialize, Serialize};
use serde_json;

use azure_iot_operations_protocol::common::payload_serialize::{
    DeserializationError, FormatIndicator, PayloadSerialize, SerializedPayload,
};

#[derive(Clone, Serialize, Deserialize, Debug)]
pub struct TestPayload {
    pub payload: Option<String>,
    pub out_content_type: Option<String>,
    pub accept_content_types: Vec<String>,
    pub indicate_character_data: bool,
    pub allow_character_data: bool,
    pub fail_deserialization: bool,
}

impl PayloadSerialize for TestPayload {
    type Error = String;

    fn serialize(self) -> Result<SerializedPayload, Self::Error> {
        Ok(SerializedPayload {
            payload: if let Some(payload) = &self.payload {
                payload.as_bytes().to_vec()
            } else {
                Vec::new()
            },
            content_type: self.out_content_type.unwrap().to_string(),
            format_indicator: if self.indicate_character_data {
                FormatIndicator::Utf8EncodedCharacterData
            } else {
                FormatIndicator::UnspecifiedBytes
            },
        })
    }

    fn deserialize(
        payload: &[u8],
        content_type: Option<&String>,
        format_indicator: &FormatIndicator,
    ) -> Result<Self, DeserializationError<Self::Error>> {
        let test_payload: TestPayload = serde_json::from_slice(payload).unwrap();

        if let Some(content_type) = content_type {
            if !test_payload.accept_content_types.contains(content_type) {
                return Err(DeserializationError::UnsupportedContentType(format!(
                    "Invalid content type: '{content_type}'."
                )));
            }
        }

        if *format_indicator == FormatIndicator::Utf8EncodedCharacterData
            && !test_payload.allow_character_data
        {
            return Err(DeserializationError::UnsupportedContentType(format!(
                "Invalid format indicator: '{format_indicator:?}'."
            )));
        }

        if test_payload.fail_deserialization {
            return Err(DeserializationError::InvalidPayload(
                "Deserialization failed.".to_string(),
            ));
        }

        Ok(test_payload)
    }
}
