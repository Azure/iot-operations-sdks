// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

use std::{
    fmt::{self, Display, Formatter},
    str::FromStr,
};

/// A reserved prefix for all user properties known to `azure_iot_operations_protocol`, `azure_iot_operations_services`, and `azure_iot_operations_mqtt`; custom properties from user code should not start with this prefix.
pub const RESERVED_PREFIX: &str = "__";

/// Enum representing the system properties.
#[derive(Debug, Copy, Clone, PartialEq)]
pub enum UserProperty {
    /// A [`HybridLogicalClock`](super::hybrid_logical_clock::HybridLogicalClock) timestamp associated with the request or response.
    Timestamp,
    /// User Property indicating an HTTP status code.
    Status,
    /// User Property indicating a human-readable status message; used when Status != 200 (OK).
    StatusMessage,
    /// User property indicating if a non-200 see <cref="Status"/> is an application-level error.
    IsApplicationError,
    /// User Property indicating the source ID of a request, response, or message.
    SourceId,
    /// The name of an MQTT property in a request header that is missing or has an invalid value.
    InvalidPropertyName,
    /// The value of an MQTT property in a request header that is invalid.
    InvalidPropertyValue,
    /// User property that indicates the protocol version of an RPC/telemetry request.
    ProtocolVersion,
    /// User property indicating which major versions the command executor supports. The value of
    /// this property is a space-separated list of integers like "1 2 3".
    SupportedMajorVersions,
    /// User property indicating what protocol version the request had.
    /// This property is only used when a command executor rejects a command invocation because the
    /// requested protocol version either wasn't supported or was malformed.
    RequestProtocolVersion,
}

impl Display for UserProperty {
    /// Get the string representation of the user property.
    fn fmt(&self, f: &mut Formatter<'_>) -> fmt::Result {
        match self {
            UserProperty::Timestamp => write!(f, "__ts"),
            UserProperty::Status => write!(f, "__stat"),
            UserProperty::StatusMessage => write!(f, "__stMsg"),
            UserProperty::IsApplicationError => write!(f, "__apErr"),
            UserProperty::SourceId => write!(f, "__srcId"),
            UserProperty::InvalidPropertyName => write!(f, "__propName"),
            UserProperty::InvalidPropertyValue => write!(f, "__propVal"),
            UserProperty::ProtocolVersion => write!(f, "__protVer"),
            UserProperty::SupportedMajorVersions => write!(f, "__supProtMajVer"),
            UserProperty::RequestProtocolVersion => write!(f, "__requestProtVer"),
        }
    }
}

impl FromStr for UserProperty {
    type Err = ();

    fn from_str(s: &str) -> Result<Self, Self::Err> {
        match s {
            "__ts" => Ok(UserProperty::Timestamp),
            "__stat" => Ok(UserProperty::Status),
            "__stMsg" => Ok(UserProperty::StatusMessage),
            "__apErr" => Ok(UserProperty::IsApplicationError),
            "__srcId" => Ok(UserProperty::SourceId),
            "__propName" => Ok(UserProperty::InvalidPropertyName),
            "__propVal" => Ok(UserProperty::InvalidPropertyValue),
            "__protVer" => Ok(UserProperty::ProtocolVersion),
            "__supProtMajVer" => Ok(UserProperty::SupportedMajorVersions),
            "__requestProtVer" => Ok(UserProperty::RequestProtocolVersion),
            _ => Err(()),
        }
    }
}

/// Validates a vector of custom user properties provided to the protocol crate.
///
/// # Errors
/// Returns a `String` describing the error if any of `property_list`'s keys or values are invalid utf-8
pub fn validate_user_properties(property_list: &[(String, String)]) -> Result<(), String> {
    for (key, value) in property_list {
        if super::is_invalid_utf8(key) || super::is_invalid_utf8(value) {
            return Err(format!(
                "Invalid user data key '{key}' or value '{value}' isn't valid utf-8"
            ));
        }
    }
    Ok(())
}

#[cfg(test)]
mod tests {
    use std::str::FromStr;

    use test_case::test_case;

    use super::validate_user_properties;
    use crate::common::user_properties::UserProperty;

    #[test_case(UserProperty::Timestamp; "timestamp")]
    #[test_case(UserProperty::Status; "status")]
    #[test_case(UserProperty::StatusMessage; "status_message")]
    #[test_case(UserProperty::IsApplicationError; "is_application_error")]
    #[test_case(UserProperty::SourceId; "source_id")]
    #[test_case(UserProperty::InvalidPropertyName; "invalid_property_name")]
    #[test_case(UserProperty::InvalidPropertyValue; "invalid_property_value")]
    #[test_case(UserProperty::ProtocolVersion; "protocol_version")]
    #[test_case(UserProperty::SupportedMajorVersions; "supported_major_versions")]
    #[test_case(UserProperty::RequestProtocolVersion; "request_protocol_version")]
    fn test_to_from_string(prop: UserProperty) {
        assert_eq!(prop, UserProperty::from_str(&prop.to_string()).unwrap());
    }

    /// Tests failure: Custom user data key is malformed utf-8 and an error is returned
    #[test_case(&[("abc\ndef".to_string(),"abcdef".to_string())]; "custom_user_data_malformed_key")]
    /// Tests failure: Custom user data value is malformed utf-8 and an error is returned
    #[test_case(&[("abcdef".to_string(),"abc\ndef".to_string())]; "custom_user_data_malformed_value")]
    fn test_validate_user_properties_invalid_value(custom_user_data: &[(String, String)]) {
        assert!(validate_user_properties(custom_user_data).is_err());
    }

    /// Tests success: Custom user data key starts with '__' and no error is returned
    #[test_case(&[("__abcdef".to_string(),"abcdef".to_string())]; "custom_user_data_reserved_prefix")]
    /// Tests success: Custom user data is valid
    #[test_case(&[("abcdef".to_string(),"abcdef".to_string())]; "custom_user_data_valid")]
    fn test_validate_user_properties_valid_value(custom_user_data: &[(String, String)]) {
        assert!(validate_user_properties(custom_user_data).is_ok());
    }
}
