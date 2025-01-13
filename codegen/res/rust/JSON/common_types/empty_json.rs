/* This file will be copied into the folder for generated code. */

use azure_iot_operations_protocol::common::payload_serialize::{
    FormatIndicator, PayloadError, PayloadSerialize, SerializedPayload,
};

#[derive(Debug, Clone)]
pub struct EmptyJson {}

impl PayloadSerialize for EmptyJson {
    type Error = String;

    fn serialize(self) -> Result<SerializedPayload, Self::Error> {
        Ok(SerializedPayload {
            payload: "".as_bytes().to_owned(),
            content_type: "application/json",
            format_indicator: FormatIndicator::Utf8EncodedCharacterData,
        })
    }

    fn deserialize(
        _payload: &[u8],
        _content_type: &Option<String>,
        _format_indicator: &FormatIndicator,
    ) -> Result<Self, PayloadError<Self::Error>> {
        Ok(Self {})
    }
}
