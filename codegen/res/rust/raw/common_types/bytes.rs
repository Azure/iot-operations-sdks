/* This file will be copied into the folder for generated code. */

use std::ops::{Deref, DerefMut};

use azure_iot_operations_protocol::common::payload_serialize::{FormatIndicator, PayloadSerialize};
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

    fn content_type() -> &'static str {
        "application/octet-stream"
    }

    fn format_indicator() -> FormatIndicator {
        FormatIndicator::UnspecifiedBytes
    }

    fn serialize(&self) -> Result<Vec<u8>, Self::Error> {
        Ok(self.to_vec())
    }

    fn deserialize(payload: &[u8]) -> Result<Self, Self::Error> {
        Ok(Bytes(bytes::Bytes::from(payload.to_vec())))
    }
}
