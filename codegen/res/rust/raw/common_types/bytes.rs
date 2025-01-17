/* This file will be copied into the folder for generated code. */

use std::ops::{Deref, DerefMut};

use azure_iot_operations_protocol::common::payload_serialize::{
    DeserializationError, FormatIndicator, PayloadSerialize, SerializedPayload,
};
use bytes;

#[derive(Clone, Debug)]
pub struct Bytes(pub bytes::Bytes);

impl Deref for Bytes {
    type Target = bytes::Bytes;

    fn deref(&self) -> &Self::Target {
        &self.0
    }
}

impl DerefMut for Bytes {
    fn deref_mut(&mut self) -> &mut Self::Target {
        &mut self.0
    }
}

impl PayloadSerialize for Bytes {
    type Error = String;

    fn serialize(self) -> Result<SerializedPayload, Self::Error> {
        Ok(SerializedPayload {
            payload: self.to_vec(),
            content_type: "application/octet-stream".to_string(),
            format_indicator: FormatIndicator::UnspecifiedBytes,
        })
    }

    fn deserialize(
        payload: &[u8],
        _content_type: &Option<String>,
        _format_indicator: &FormatIndicator,
    ) -> Result<Self, DeserializationError<Self::Error>> {
        Ok(Bytes(bytes::Bytes::from(payload.to_vec())))
    }
}
