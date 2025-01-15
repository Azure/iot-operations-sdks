// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

use std::fmt::Debug;

/// Format indicator for serialization and deserialization.
#[repr(u8)]
#[derive(Clone, PartialEq, Debug, Default)]
pub enum FormatIndicator {
    /// Unspecified Bytes
    #[default]
    UnspecifiedBytes = 0,
    /// UTF-8 Encoded Character Data (as JSON)
    Utf8EncodedCharacterData = 1,
}

impl From<u8> for FormatIndicator {
    fn from(value: u8) -> Self {
        match value {
            0 => FormatIndicator::UnspecifiedBytes,
            1 => FormatIndicator::Utf8EncodedCharacterData,
            _ => FormatIndicator::default(),
        }
    }
}

impl From<Option<u8>> for FormatIndicator {
    fn from(value: Option<u8>) -> Self {
        match value {
            Some(num) => FormatIndicator::from(num),
            None => FormatIndicator::default(),
        }
    }
}

/// Struct that specifies the content type, format indicator, and payload for a serialized payload.
#[derive(Clone, Debug, Default, PartialEq)]
pub struct SerializedPayload {
    /// The content type of the payload
    pub content_type: String,
    /// The format indicator of the payload
    pub format_indicator: FormatIndicator,
    /// The payload as a serialized byte vector
    pub payload: Vec<u8>,
}

/// Trait for serializing and deserializing payloads.
/// # Examples
/// ```
/// use azure_iot_operations_protocol::common::payload_serialize::{PayloadSerialize, PayloadError, FormatIndicator, SerializedPayload};
/// #[derive(Clone, Debug)]
/// pub struct CarLocationResponse {
///   latitude: f64,
///   longitude: f64,
/// }
/// impl PayloadSerialize for CarLocationResponse {
///   type Error = String;
///   fn serialize(self) -> Result<SerializedPayload, String> {
///     let response = format!("{{\"latitude\": {}, \"longitude\": {}}}", self.latitude, self.longitude);
///     Ok(SerializedPayload {
///         payload: response.as_bytes().to_vec(),
///         content_type: "application/json".to_string(),
///         format_indicator: FormatIndicator::Utf8EncodedCharacterData,
///     })
///   }
///   fn deserialize(payload: &[u8],
///     content_type: &Option<String>,
///     _format_indicator: &FormatIndicator,
///   ) -> Result<Self, PayloadError<String>> {
///     if let Some(content_type) = content_type {
///            if content_type != "application/json" {
///                return Err(PayloadError::UnsupportedContentType(format!(
///                    "Invalid content type: '{content_type:?}'. Must be 'application/json'"
///                )));
///            }
///        }
///     // mock deserialization here for brevity
///     let _payload = String::from_utf8(payload.to_vec()).unwrap();
///     Ok(CarLocationResponse {latitude: 12.0, longitude: 35.0})
///   }
/// }
/// ```
pub trait PayloadSerialize: Clone {
    /// The type returned in the event of a serialization/deserialization error
    type Error: Debug + Into<Box<dyn std::error::Error + Sync + Send + 'static>>;

    /// Serializes the payload from the generic type to a byte vector and specifies the content type and format indicator.
    /// The content type and format indicator could be the same every time or dynamic per payload.
    ///
    /// # Errors
    /// Returns a [`PayloadSerialize::Error`] if the serialization fails.
    fn serialize(self) -> Result<SerializedPayload, Self::Error>;

    /// Deserializes the payload from a byte vector to the generic type
    ///
    /// # Errors
    /// Returns a [`PayloadError::DeserializationError`] over Type [`PayloadSerialize::Error`] if the deserialization fails.
    /// Returns a [`PayloadError::UnsupportedContentType`] if the content type isn't supported by this deserialization implementation.
    fn deserialize(
        payload: &[u8],
        content_type: &Option<String>,
        format_indicator: &FormatIndicator,
    ) -> Result<Self, PayloadError<Self::Error>>;
}

/// Enum to describe the type of error that occurred during payload deserialization.
#[derive(thiserror::Error, Debug)]
pub enum PayloadError<T: Debug + Into<Box<dyn std::error::Error + Sync + Send + 'static>>> {
    /// An error occurred while deserializing
    #[error(transparent)]
    DeserializationError(#[from] T),
    /// The content type received is not supported by the deserialization implementation.
    #[error("Unsupported content type: {0}")]
    UnsupportedContentType(String),
}

// Provided convenience implementations

/// A provided convenience struct for bypassing serialization and deserialization,
/// but having dynamic content type and format indicator.
#[derive(Clone, Debug)]
pub struct BypassPayload {
    /// The content type of the payload
    pub content_type: String,
    /// The format indicator of the payload
    pub format_indicator: FormatIndicator,
    /// The raw bytes to be sent as the payload
    pub payload: Vec<u8>,
}

impl PayloadSerialize for BypassPayload {
    type Error = String;
    fn serialize(self) -> Result<SerializedPayload, String> {
        Ok(SerializedPayload {
            payload: self.payload,
            content_type: self.content_type,
            format_indicator: self.format_indicator,
        })
    }

    fn deserialize(
        payload: &[u8],
        content_type: &Option<String>,
        format_indicator: &FormatIndicator,
    ) -> Result<Self, PayloadError<String>> {
        let ct: String = content_type.clone().unwrap_or_default();
        Ok(BypassPayload {
            content_type: ct,
            format_indicator: format_indicator.clone(),
            payload: payload.to_vec(),
        })
    }
}

/// A provided convenience struct for an empty payload.
#[derive(Clone, Debug)]
pub struct EmptyPayload {
    /// The content type of the payload
    pub content_type: String,
    /// The format indicator of the payload
    pub format_indicator: FormatIndicator,
}

impl PayloadSerialize for EmptyPayload {
    type Error = String;
    fn serialize(self) -> Result<SerializedPayload, String> {
        Ok(SerializedPayload {
            payload: vec![],
            content_type: self.content_type,
            format_indicator: self.format_indicator,
        })
    }

    fn deserialize(
        _payload: &[u8],
        content_type: &Option<String>,
        format_indicator: &FormatIndicator,
    ) -> Result<Self, PayloadError<String>> {
        let ct: String = content_type.clone().unwrap_or_default();
        Ok(EmptyPayload {
            content_type: ct,
            format_indicator: format_indicator.clone(),
        })
    }
}

/// Provided convenience implementation for sending raw bytes as `content_type` "application/octet-stream".
impl PayloadSerialize for Vec<u8> {
    type Error = String;
    fn serialize(self) -> Result<SerializedPayload, String> {
        Ok(SerializedPayload {
            payload: self,
            content_type: "application/octet-stream".to_string(),
            format_indicator: FormatIndicator::UnspecifiedBytes,
        })
    }

    fn deserialize(
        payload: &[u8],
        content_type: &Option<String>,
        _format_indicator: &FormatIndicator,
    ) -> Result<Self, PayloadError<String>> {
        if let Some(content_type) = content_type {
            if content_type != "application/octet-stream" {
                return Err(PayloadError::UnsupportedContentType(format!(
                    "Invalid content type: '{content_type:?}'. Must be 'application/octet-stream'"
                )));
            }
        }
        Ok(payload.to_vec())
    }
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
        type Error = String;
        fn serialize(self) -> Result<SerializedPayload, String>;
        fn deserialize(payload: &[u8], content_type: &Option<String>, format_indicator: &FormatIndicator) -> Result<Self, PayloadError<String>>;
    }
}
#[cfg(test)]
use std::sync::Mutex;

// Mutex needed to check mock calls of static method `PayloadSerialize::deserialize`,
#[cfg(test)]
pub static DESERIALIZE_MTX: Mutex<()> = Mutex::new(());

#[cfg(test)]
mod tests {
    use test_case::test_case;

    use crate::common::payload_serialize::FormatIndicator;

    #[test_case(&FormatIndicator::UnspecifiedBytes; "UnspecifiedBytes")]
    #[test_case(&FormatIndicator::Utf8EncodedCharacterData; "Utf8EncodedCharacterData")]
    fn test_to_from_u8(prop: &FormatIndicator) {
        assert_eq!(prop, &FormatIndicator::from(prop.clone() as u8));
    }

    #[test_case(0, &FormatIndicator::UnspecifiedBytes; "0_to_UnspecifiedBytes")]
    #[test_case(1, &FormatIndicator::Utf8EncodedCharacterData; "1_to_Utf8EncodedCharacterData")]
    #[test_case(2, &FormatIndicator::UnspecifiedBytes; "2_to_UnspecifiedBytes")]
    #[test_case(255, &FormatIndicator::UnspecifiedBytes; "255_to_UnspecifiedBytes")]
    fn test_from_u8(value: u8, expected: &FormatIndicator) {
        assert_eq!(expected, &FormatIndicator::from(value));
    }

    #[test_case(Some(0), &FormatIndicator::UnspecifiedBytes; "0_to_UnspecifiedBytes")]
    #[test_case(Some(1), &FormatIndicator::Utf8EncodedCharacterData; "1_to_Utf8EncodedCharacterData")]
    #[test_case(Some(2), &FormatIndicator::UnspecifiedBytes; "2_to_UnspecifiedBytes")]
    #[test_case(Some(255), &FormatIndicator::UnspecifiedBytes; "255_to_UnspecifiedBytes")]
    #[test_case(None, &FormatIndicator::UnspecifiedBytes; "None_to_UnspecifiedBytes")]
    fn test_from_option_u8(value: Option<u8>, expected: &FormatIndicator) {
        assert_eq!(expected, &FormatIndicator::from(value));
    }
}
