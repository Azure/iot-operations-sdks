/* Code generated by Azure.Iot.Operations.ProtocolCompiler v0.6.0.0; DO NOT EDIT. */
#![allow(non_camel_case_types)]

use azure_iot_operations_protocol::common::payload_serialize::{
    DeserializationError, FormatIndicator, PayloadSerialize, SerializedPayload,
};
use serde_json;

use super::read_counter_response_payload::ReadCounterResponsePayload;

impl PayloadSerialize for ReadCounterResponsePayload {
    type Error = serde_json::Error;

    fn serialize(self) -> Result<SerializedPayload, Self::Error> {
        let payload = serde_json::to_vec(&self);
        Ok(SerializedPayload {
            payload: payload?,
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
