/* This file will be copied into the folder for generated code. */

use azure_iot_operations_protocol::common::payload_serialize::{FormatIndicator, PayloadSerialize};

#[derive(Debug, Clone)]
pub struct EmptyJson {}

impl PayloadSerialize for EmptyJson {
    type Error = String;

    fn content_type() -> &'static str {
        "application/json"
    }

    fn is_content_type_supersedable() -> bool {
        false
    }

    fn format_indicator() -> FormatIndicator {
        FormatIndicator::Utf8EncodedCharacterData
    }

    fn serialize(&self) -> Result<Vec<u8>, Self::Error> {
        Ok("".as_bytes().to_owned())
    }

    fn deserialize(_payload: &[u8]) -> Result<Self, Self::Error> {
        Ok(Self {})
    }
}
