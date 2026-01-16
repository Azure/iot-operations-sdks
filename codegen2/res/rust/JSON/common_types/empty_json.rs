/* This file will be copied into the folder for generated code. */

use azure_iot_operations_protocol::common::payload_serialize::{
    DeserializationError, FormatIndicator, PayloadSerialize, SerializedPayload,
};

#[derive(Debug, Clone)]
pub struct EmptyJson {}

impl PayloadSerialize for EmptyJson {
    type Error = String;

    fn serialize(self) -> Result<SerializedPayload, Self::Error> {
        Ok(SerializedPayload {
            payload: "".as_bytes().to_owned(),
            content_type: "application/json".to_string(),
            format_indicator: FormatIndicator::Utf8EncodedCharacterData,
        })
    }

    fn deserialize(
        _payload: &[u8],
        _content_type: Option<&String>,
        _format_indicator: &FormatIndicator,
    ) -> Result<Self, DeserializationError<Self::Error>> {
        Ok(Self {})
    }
}
