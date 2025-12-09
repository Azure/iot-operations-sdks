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
    /// UTF-8 Encoded Character Data (such as JSON)
    Utf8EncodedCharacterData = 1,
}

impl TryFrom<Option<u8>> for FormatIndicator {
    type Error = String;

    fn try_from(value: Option<u8>) -> Result<Self, Self::Error> {
        match value {
            Some(0) | None => Ok(FormatIndicator::default()),
            Some(1) => Ok(FormatIndicator::Utf8EncodedCharacterData),
            Some(_) => Err(format!(
                "Invalid format indicator value: {value:?}. Must be 0 or 1"
            )),
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
/// use azure_iot_operations_protocol::common::payload_serialize::{PayloadSerialize, DeserializationError, FormatIndicator, SerializedPayload};
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
///     content_type: Option<&String>,
///     _format_indicator: &FormatIndicator,
///   ) -> Result<Self, DeserializationError<String>> {
///     if let Some(content_type) = content_type {
///            if content_type != "application/json" {
///                return Err(DeserializationError::UnsupportedContentType(format!(
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
    /// Returns a [`DeserializationError::InvalidPayload`] over type [`PayloadSerialize::Error`] if the deserialization fails.
    ///
    /// Returns a [`DeserializationError::UnsupportedContentType`] if the content type isn't supported by this deserialization implementation.
    fn deserialize(
        payload: &[u8],
        content_type: Option<&String>,
        format_indicator: &FormatIndicator,
    ) -> Result<Self, DeserializationError<Self::Error>>;
}

/// Enum to describe the type of error that occurred during payload deserialization.
#[derive(thiserror::Error, Debug)]
pub enum DeserializationError<T: Debug + Into<Box<dyn std::error::Error + Sync + Send + 'static>>> {
    /// An error occurred while deserializing.
    #[error(transparent)]
    InvalidPayload(#[from] T),
    /// The content type received is not supported by the deserialization implementation.
    #[error("Unsupported content type: {0}")]
    UnsupportedContentType(String),
}

// Provided convenience implementations

/// A provided convenience struct for bypassing serialization and deserialization,
/// but having dynamic content type and format indicator.
pub type BypassPayload = SerializedPayload;

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
        content_type: Option<&String>,
        format_indicator: &FormatIndicator,
    ) -> Result<Self, DeserializationError<String>> {
        let ct = match content_type {
            Some(ct) => ct.clone(),
            None => String::default(),
        };
        Ok(BypassPayload {
            content_type: ct,
            format_indicator: format_indicator.clone(),
            payload: payload.to_vec(),
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
        content_type: Option<&String>,
        _format_indicator: &FormatIndicator,
    ) -> Result<Self, DeserializationError<String>> {
        if let Some(content_type) = content_type {
            if content_type != "application/octet-stream" {
                return Err(DeserializationError::UnsupportedContentType(format!(
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
    #[allow(clippy::ref_option_ref)]    // NOTE: This may not be required if mockall gets updated for 2024 edition
    pub Payload{}
    impl Clone for Payload {
        fn clone(&self) -> Self;
    }
    impl PayloadSerialize for Payload {
        type Error = String;
        fn serialize(self) -> Result<SerializedPayload, String>;
        #[allow(clippy::ref_option_ref)]    // NOTE: This may not be required if mockall gets updated for 2024 edition
        fn deserialize<'a>(payload: &[u8], content_type: Option<&'a String>, format_indicator: &FormatIndicator) -> Result<Self, DeserializationError<String>>;
    }
}
#[cfg(test)]
use std::sync::Mutex;

// TODO: Remove this mutex. Find a better way to control test ordering
/// Mutex needed to check mock calls of static method `PayloadSerialize::deserialize`,
#[cfg(test)]
pub static DESERIALIZE_MTX: Mutex<()> = Mutex::new(());

#[cfg(test)]
mod tests {
    use test_case::test_case;

    use crate::common::payload_serialize::FormatIndicator;

    #[test_case(&FormatIndicator::UnspecifiedBytes; "UnspecifiedBytes")]
    #[test_case(&FormatIndicator::Utf8EncodedCharacterData; "Utf8EncodedCharacterData")]
    fn test_to_from_u8(prop: &FormatIndicator) {
        assert_eq!(
            prop,
            &FormatIndicator::try_from(Some(prop.clone() as u8)).unwrap()
        );
    }

    #[test_case(Some(0), &FormatIndicator::UnspecifiedBytes; "0_to_UnspecifiedBytes")]
    #[test_case(Some(1), &FormatIndicator::Utf8EncodedCharacterData; "1_to_Utf8EncodedCharacterData")]
    #[test_case(None, &FormatIndicator::UnspecifiedBytes; "None_to_UnspecifiedBytes")]
    fn test_from_option_u8_success(value: Option<u8>, expected: &FormatIndicator) {
        let res = FormatIndicator::try_from(value);
        assert!(res.is_ok());
        assert_eq!(expected, &res.unwrap());
    }

    #[test_case(Some(2); "2")]
    #[test_case(Some(255); "255")]
    fn test_from_option_u8_failure(value: Option<u8>) {
        assert!(&FormatIndicator::try_from(value).is_err());
    }
}
