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
            Some(0) => FormatIndicator::UnspecifiedBytes,
            Some(1) => FormatIndicator::Utf8EncodedCharacterData,
            _ => FormatIndicator::default(),
        }
    }
}

/// Trait for serializing and deserializing payloads.
/// # Examples
/// ```
/// use azure_iot_operations_protocol::common::payload_serialize::{PayloadSerialize, FormatIndicator};
/// #[derive(Clone, Debug)]
/// pub struct CarLocationResponse {
///   latitude: f64,
///   longitude: f64,
/// }
/// impl PayloadSerialize for CarLocationResponse {
///   type Error = String;
///   fn content_type() -> &'static str {
///     "application/json"
///   }
///   fn format_indicator() -> FormatIndicator {
///    FormatIndicator::Utf8EncodedCharacterData
///   }
///   fn serialize(self) -> Result<Vec<u8>, String> {
///     let response = format!("{{\"latitude\": {}, \"longitude\": {}}}", self.latitude, self.longitude);
///     Ok(response.as_bytes().to_vec())
///   }
///   fn deserialize(payload: &[u8]) -> Result<Self, String> {
///     // mock deserialization here for brevity
///     let _payload = String::from_utf8(payload.to_vec()).unwrap();
///     Ok(CarLocationResponse {latitude: 12.0, longitude: 35.0})
///   }
/// }
/// ```
///
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
    fn deserialize(payload: &[u8], content_type: &Option<String>, format_indicator: &FormatIndicator) -> Result<Self, PayloadError<Self::Error>>;
}

#[derive(thiserror::Error, Debug)]
pub enum PayloadError<T: Debug + Into<Box<dyn std::error::Error + Sync + Send + 'static>>> {
    /// An error occurred while deserializing
    #[error(transparent)]
    DeserializationError(#[from] T),
    /// The content type received is not supported by the deserialization implementation.
    #[error("Unsupported content type: {0}")]
    UnsupportedContentType(String),
}

#[derive(Clone, Debug, Default)]
pub struct SerializedPayload {
    pub content_type: &'static str,
    pub format_indicator: FormatIndicator,
    pub payload: Vec<u8>,
}

// pub struct BypassPayload {
//     pub content_type: &'static str,
//     pub format_indicator: FormatIndicator,
//     pub payload: &'static str,
// }

// impl PayloadSerialize for BypassPayload {
//     type Error = String;

//     // fn content_type() -> &'static str {
//     //     "application/octet-stream"
//     // }

//     // fn format_indicator() -> FormatIndicator {
//     //     FormatIndicator::UnspecifiedBytes
//     // }

//     fn serialize(&self) -> Result<SerializedPayload, String> {
//         // Ok(self.payload.clone())
//         Ok(SerializedPayload {
//             payload: self.payload,
//             content_type: "application/json",
//             format_indicator: FormatIndicator::Utf8EncodedCharacterData,
//         })
//     }

//     fn deserialize(payload: &[u8]) -> Result<Self, String> {
//         // pass in format indicator and format indicator?
//         Ok(BypassPayload {
//             content_type: "application/octet-stream",
//             format_indicator: FormatIndicator::UnspecifiedBytes,
//             payload: payload.to_vec(),
//         })
//     }
// }

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

// Mutex needed to check mock calls of static methods `PayloadSerialize::content_type`, `PayloadSerialize::format_indicator`, and `PayloadSerialize::deserialize`,
#[cfg(test)]
pub static CONTENT_TYPE_MTX: Mutex<()> = Mutex::new(());
#[cfg(test)]
pub static FORMAT_INDICATOR_MTX: Mutex<()> = Mutex::new(());
#[cfg(test)]
pub static DESERIALIZE_MTX: Mutex<()> = Mutex::new(());
