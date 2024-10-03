/* This file will be copied into the folder for generated code. */

use azure_iot_operations_protocol::common::payload_serialize::{
    FormatIndicator, PayloadSerialize, SerializerError,
};
use super::empty_json::EmptyJson;

impl PayloadSerialize for EmptyJson {
    fn content_type() -> &'static str {
        "application/json"
    }

    fn format_indicator() -> FormatIndicator {
        FormatIndicator::Utf8EncodedCharacterData
    }

    fn serialize(&self) -> Result<Vec<u8>, SerializerError> {
        Ok("{}".as_bytes().to_owned())
    }

    fn deserialize(_payload: &[u8]) -> Result<Self, SerializerError> {
        Ok(Self{})
    }
}
