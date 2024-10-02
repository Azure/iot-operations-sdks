/* This file will be copied into the folder for generated code. */

use azure_iot_operations_protocol::common::payload_serialize::{
    FormatIndicator, PayloadSerialize, SerializerError,
};
use bytes;
use super::bytes::Bytes;

impl PayloadSerialize for Bytes {
    fn content_type() -> &'static str {
        "application/octet-stream"
    }

    fn format_indicator() -> FormatIndicator {
        FormatIndicator::UnspecifiedBytes
    }

    fn serialize(&self) -> Result<Vec<u8>, SerializerError> {
        Ok(self.to_vec())
    }

    fn deserialize(payload: &[u8]) -> Result<Self, SerializerError> {
        Ok(Bytes(bytes::Bytes::from(payload.to_vec())))
    }
}
