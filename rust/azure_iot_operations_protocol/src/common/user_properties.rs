// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

use std::{
    fmt::{self, Display, Formatter},
    str::FromStr,
};

// NOTE: The use of these broker/protocol properties is currently rather inconsistent.
// The current implementation does not necessarily capture the intent, do not read too much into
// the design choices.

/// Enum representing user properties that are AIO MQ broker-owned/reserved.
/// They correspond to broker-specific functionality and may be set by the protocols.
/// May or may not be restricted from the end-user (case-by-case)
pub(crate) enum BrokerReservedUserProperty {
    /// The partition ID that the MQ broker uses to determine which subscriber in a shared subscription
    /// receives a given message. Partition ID should correspond with an MQTT client ID.
    /// For more details, see: [shared_subscriptions.md](https://github.com/Azure/iot-operations-sdks/blob/main/doc/reference/shared-subscriptions.md).
    Partition,
    /// Flag indicating high priority for the message (i.e. backpressure bypass). No associated value.
    HighPriority,
    /// Indicates that the message should be persisted to disk by the MQ broker.
    Persist,
}

impl BrokerReservedUserProperty {
    /// Indicates if the user property is restricted from being set by, or provided to the end-user.
    pub(crate) fn is_user_restricted(&self) -> bool {
        match self {
            BrokerReservedUserProperty::Partition => true,
            BrokerReservedUserProperty::HighPriority => false,
            BrokerReservedUserProperty::Persist => false,
        }
    }
}

impl Display for BrokerReservedUserProperty {
    /// Get the string representation of the broker user property.
    fn fmt(&self, f: &mut Formatter<'_>) -> fmt::Result {
        match self {
            BrokerReservedUserProperty::Partition => write!(f, "$partition"),
            BrokerReservedUserProperty::HighPriority => write!(f, "$high_priority"),
            BrokerReservedUserProperty::Persist => write!(f, "aio-persistence"),
        }
    }
}

impl FromStr for BrokerReservedUserProperty {
    type Err = ();

    fn from_str(s: &str) -> Result<Self, Self::Err> {
        match s {
            "$partition" => Ok(BrokerReservedUserProperty::Partition),
            "$high_priority" => Ok(BrokerReservedUserProperty::HighPriority),
            "aio-persistence" => Ok(BrokerReservedUserProperty::Persist),
            _ => Err(()),
        }
    }
}

/// Enum representing the system properties.
#[derive(Debug, Copy, Clone, PartialEq, Eq, Hash)]
pub(crate) enum ProtocolReservedUserProperty {
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

impl Display for ProtocolReservedUserProperty {
    /// Get the string representation of the user property.
    fn fmt(&self, f: &mut Formatter<'_>) -> fmt::Result {
        match self {
            ProtocolReservedUserProperty::Timestamp => write!(f, "__ts"),
            ProtocolReservedUserProperty::Status => write!(f, "__stat"),
            ProtocolReservedUserProperty::StatusMessage => write!(f, "__stMsg"),
            ProtocolReservedUserProperty::IsApplicationError => write!(f, "__apErr"),
            ProtocolReservedUserProperty::SourceId => write!(f, "__srcId"),
            ProtocolReservedUserProperty::InvalidPropertyName => write!(f, "__propName"),
            ProtocolReservedUserProperty::InvalidPropertyValue => write!(f, "__propVal"),
            ProtocolReservedUserProperty::ProtocolVersion => write!(f, "__protVer"),
            ProtocolReservedUserProperty::SupportedMajorVersions => write!(f, "__supProtMajVer"),
            ProtocolReservedUserProperty::RequestProtocolVersion => write!(f, "__requestProtVer"),
        }
    }
}

impl FromStr for ProtocolReservedUserProperty {
    type Err = ();

    fn from_str(s: &str) -> Result<Self, Self::Err> {
        match s {
            "__ts" => Ok(ProtocolReservedUserProperty::Timestamp),
            "__stat" => Ok(ProtocolReservedUserProperty::Status),
            "__stMsg" => Ok(ProtocolReservedUserProperty::StatusMessage),
            "__apErr" => Ok(ProtocolReservedUserProperty::IsApplicationError),
            "__srcId" => Ok(ProtocolReservedUserProperty::SourceId),
            "__propName" => Ok(ProtocolReservedUserProperty::InvalidPropertyName),
            "__propVal" => Ok(ProtocolReservedUserProperty::InvalidPropertyValue),
            "__protVer" => Ok(ProtocolReservedUserProperty::ProtocolVersion),
            "__supProtMajVer" => Ok(ProtocolReservedUserProperty::SupportedMajorVersions),
            "__requestProtVer" => Ok(ProtocolReservedUserProperty::RequestProtocolVersion),
            _ => Err(()),
        }
    }
}

/// Validates a vector of custom user properties provided to the invoker of the protocol crate.
///
/// # Errors
/// Returns a `String` describing the error if any of `property_list`'s keys are invalid utf-8
/// or are a user-restricted broker reserved property.
pub(crate) fn validate_invoker_user_properties(
    property_list: &[(String, String)],
) -> Result<(), String> {
    for (key, value) in property_list {
        if super::is_invalid_utf8(key) || super::is_invalid_utf8(value) {
            return Err(format!(
                "Invalid user data key '{key}' or value '{value}' isn't valid utf-8"
            ));
        }
        if let Ok(broker_prop) = BrokerReservedUserProperty::from_str(key) {
            if broker_prop.is_user_restricted() {
                return Err(format!(
                    "User data key '{key}' is a restricted broker property"
                ));
            }
        }
    }
    Ok(())
}

/// Validates a vector of custom user properties provided to the protocol crate.
///
/// # Errors
/// Returns a `String` describing the error if any of `property_list`'s keys or values are invalid utf-8
pub(crate) fn validate_user_properties(property_list: &[(String, String)]) -> Result<(), String> {
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

    use super::*;
    use crate::common::user_properties::ProtocolReservedUserProperty;

    #[test_case(ProtocolReservedUserProperty::Timestamp; "timestamp")]
    #[test_case(ProtocolReservedUserProperty::Status; "status")]
    #[test_case(ProtocolReservedUserProperty::StatusMessage; "status_message")]
    #[test_case(ProtocolReservedUserProperty::IsApplicationError; "is_application_error")]
    #[test_case(ProtocolReservedUserProperty::SourceId; "source_id")]
    #[test_case(ProtocolReservedUserProperty::InvalidPropertyName; "invalid_property_name")]
    #[test_case(ProtocolReservedUserProperty::InvalidPropertyValue; "invalid_property_value")]
    #[test_case(ProtocolReservedUserProperty::ProtocolVersion; "protocol_version")]
    #[test_case(ProtocolReservedUserProperty::SupportedMajorVersions; "supported_major_versions")]
    #[test_case(ProtocolReservedUserProperty::RequestProtocolVersion; "request_protocol_version")]
    fn test_to_from_string(prop: ProtocolReservedUserProperty) {
        assert_eq!(
            prop,
            ProtocolReservedUserProperty::from_str(&prop.to_string()).unwrap()
        );
    }

    /// Tests failure: Custom user data key is malformed utf-8 and an error is returned
    #[test_case(&[("abc\ndef".to_string(),"abcdef".to_string())]; "custom_user_data_malformed_key")]
    /// Tests failure: Custom user data value is malformed utf-8 and an error is returned
    #[test_case(&[("abcdef".to_string(),"abc\ndef".to_string())]; "custom_user_data_malformed_value")]
    fn test_validate_user_properties_invalid_value(custom_user_data: &[(String, String)]) {
        assert!(validate_user_properties(custom_user_data).is_err());
        assert!(validate_invoker_user_properties(custom_user_data).is_err());
    }

    /// Tests success: Custom user data key starts with '__' and no error is returned
    #[test_case(&[("__abcdef".to_string(),"abcdef".to_string())]; "custom_user_data_reserved_prefix")]
    /// Tests success: Custom user data is valid
    #[test_case(&[("abcdef".to_string(),"abcdef".to_string())]; "custom_user_data_valid")]
    fn test_validate_user_properties_valid_value(custom_user_data: &[(String, String)]) {
        assert!(validate_user_properties(custom_user_data).is_ok());
        assert!(validate_invoker_user_properties(custom_user_data).is_ok());
    }

    #[test]
    fn test_restricted_broker_property_rejected_by_invoker_validation() {
        let props = vec![(
            BrokerReservedUserProperty::Partition.to_string(),
            "abcdef".to_string(),
        )];
        assert!(validate_user_properties(&props).is_ok());
        assert!(validate_invoker_user_properties(&props).is_err());
    }

    #[test]
    fn test_unrestricted_broker_property_allowed_by_invoker_validation() {
        let props = vec![(
            BrokerReservedUserProperty::HighPriority.to_string(),
            "true".to_string(),
        )];
        assert!(validate_user_properties(&props).is_ok());
        assert!(validate_invoker_user_properties(&props).is_ok());
    }

    #[test]
    fn test_persist_broker_property_allowed_by_invoker_validation() {
        let props = vec![(
            BrokerReservedUserProperty::Persist.to_string(),
            "true".to_string(),
        )];
        assert!(validate_user_properties(&props).is_ok());
        assert!(validate_invoker_user_properties(&props).is_ok());
    }
}
