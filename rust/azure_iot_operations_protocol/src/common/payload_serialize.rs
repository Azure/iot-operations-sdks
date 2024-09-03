// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

use std::{error::Error, fmt};

/// Error type for serialization and deserialization
#[derive(Debug)]
pub struct SerializerError {
    /// Error created by the custom serializer
    pub nested_error: Box<dyn Error>,
}

impl fmt::Display for SerializerError {
    fn fmt(&self, f: &mut fmt::Formatter<'_>) -> fmt::Result {
        write!(f, "{:?}", self.nested_error)
    }
}

impl Error for SerializerError {
    fn source(&self) -> Option<&(dyn Error + 'static)> {
        Some(self.nested_error.as_ref())
    }
}

/// Format indicator for serialization and deserialization.
#[repr(u8)]
#[derive(Clone, PartialEq)]
pub enum FormatIndicator {
    /// Unspecified Bytes
    UnspecifiedBytes = 0,
    /// UTF-8 Encoded Character Data (as JSON)
    Utf8EncodedCharacterData = 1,
}

/// Trait for serializing and deserializing payloads.
/// # Examples
/// ```
/// use azure_iot_operations_protocol::common::payload_serialize::{PayloadSerialize, SerializerError, FormatIndicator};
/// #[derive(Clone, Debug)]
/// pub struct CarLocationResponse {
///   latitude: f64,
///   longitude: f64,
/// }
/// impl PayloadSerialize for CarLocationResponse {
///   fn content_type() -> &'static str {
///     "application/json"
///   }
///   fn format_indicator() -> FormatIndicator {
///    FormatIndicator::Utf8EncodedCharacterData
///   }
///   fn serialize(&self) -> Result<Vec<u8>, SerializerError> {
///     let response = format!("{{\"latitude\": {}, \"longitude\": {}}}", self.latitude, self.longitude);
///     Ok(response.as_bytes().to_vec())
///   }
///   fn deserialize(payload: &[u8]) -> Result<Self, SerializerError> {
///     // mock deserialization here for brevity
///     let _payload = String::from_utf8(payload.to_vec()).unwrap();
///     Ok(CarLocationResponse {latitude: 12.0, longitude: 35.0})
///   }
/// }
/// ```
///
pub trait PayloadSerialize: Clone {
    /// Return content type
    /// Returns a String value to specify the binary format used in the payload, e.g., application/json, application/protobuf, or application/avro.
    fn content_type() -> &'static str;

    /// Return format indicator
    /// [`FormatIndicator::Utf8EncodedCharacterData`] for character data (as JSON), [`FormatIndicator::UnspecifiedBytes`] for unspecified.
    fn format_indicator() -> FormatIndicator;

    /// Serializes the payload from the generic type to a byte vector
    ///
    /// # Errors
    /// Returns a [`SerializerError`] if the serialization fails.
    fn serialize(&self) -> Result<Vec<u8>, SerializerError>;

    /// Deserializes the payload from a byte vector to the generic type
    ///
    /// # Errors
    /// Returns a [`SerializerError`] if the deserialization fails.
    fn deserialize(payload: &[u8]) -> Result<Self, SerializerError>;
}

#[cfg(test)]
use mockall::mock;
#[cfg(test)]
mock! {
    pub Payload{}
    impl Clone for Payload {
        fn clone(&self) -> Self;
    }
    impl PayloadSerialize for Payload {
        fn content_type() -> &'static str;
        fn format_indicator() -> FormatIndicator;
        fn serialize(&self) -> Result<Vec<u8>, SerializerError>;
        fn deserialize(payload: &[u8]) -> Result<Self, SerializerError>;
    }
}
