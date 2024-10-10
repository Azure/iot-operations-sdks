// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

use std::{
    fmt::{self, Display, Formatter},
    str::FromStr,
};

/// A reserved prefix for all user properties known to `azure_iot_operations_protocol`; custom properties from user code may not start with this prefix.
pub const RESERVED_PREFIX: &str = "__";

/// Enum representing the system properties.
#[derive(Debug, Copy, Clone, PartialEq)]
pub enum UserProperty {
    /// A [`HybridLogicalClock`](super::hybrid_logical_clock::HybridLogicalClock) timestamp associated with the request or response.
    Timestamp,
    /// A [`HybridLogicalClock`](super::hybrid_logical_clock::HybridLogicalClock) fencing token used to protect the object of the request from conflicting updates.
    FencingToken,
    /// User Property indicating an HTTP status code.
    Status,
    /// User Property indicating a human-readable status message; used when Status != 200 (OK).
    StatusMessage,
    /// User property indicating if a non-200 <see cref="Status"/> is an application-level error.
    IsApplicationError,
    /// User Property indicating the MQTT Client ID of a [`CommandInvoker`](crate::rpc::command_invoker::CommandInvoker).
    CommandInvokerId,
    /// The name of an MQTT property in a request header that is missing or has an invalid value.
    InvalidPropertyName,
    /// The value of an MQTT property in a request header that is invalid.
    InvalidPropertyValue,
    /// User property that indicates the protocol version of an RPC/telemetry request.
    ProtocolVersion,
    /// User property indicating which major versions the command executor supports. The value of
    /// this property is a space-separated list of integers like "1 2 3".
    SupportedMajorVersions,
    /// User Property indicating the MQTT Client ID of a [`TelemetrySender`](crate::telemetry::telemetry_sender::TelemetrySender).
    SenderClientId,
    /// Unique identifier of the message, produced by the [`TelemetrySender`](crate::telemetry::telemetry_sender::TelemetrySender) as a Guid
    CloudEventSpecVersion,
    /// Describes the type of event related to the originating occurrence.
    /// Often this attribute is used for routing, observability, policy enforcement, etc.
    /// The format of this is producer defined and might include information such as the version of the type
    CloudEventType,
    /// Identifies the context in which an event happened.
    /// Often this will include information such as the type of the event source,
    /// the organization publishing the event or the process that produced the event.
    /// The exact syntax and semantics behind the data encoded in the URI is defined by the event producer.
    CloudEventSource,
    /// Identifies the event. Producers MUST ensure that source + id is unique for each distinct event.
    ///  If a duplicate event is re-sent (e.g. due to a network error) it MAY have the same id.
    ///  Consumers MAY assume that Events with identical source and id are duplicates.
    CloudEventId,
    /// Identifies the subject of the event in the context of the event producer (identified by source).
    /// In publish-subscribe scenarios, a subscriber will typically subscribe to events emitted by a source,
    /// but the source identifier alone might not be sufficient as a qualifier for any specific event if the source context has internal sub-structure.
    CloudEventSubject,
    /// Timestamp of when the occurrence happened.
    /// If the time of the occurrence cannot be determined then this attribute MAY be set to some other time (such as the current time)
    /// by the `CloudEvents` producer,
    /// however all producers for the same source MUST be consistent in this respect.
    CloudEventTime,
    ///  Content type of data value. This attribute enables data to carry any type of content,
    ///  whereby format and encoding might differ from that of the chosen event format.
    CloudEventDataContentType,
    ///  Identifies the schema that data adheres to.
    ///  Incompatible changes to the schema SHOULD be reflected by a different URI.
    CloudEventDataSchema,
}

impl Display for UserProperty {
    /// Get the string representation of the user property.
    fn fmt(&self, f: &mut Formatter<'_>) -> fmt::Result {
        match self {
            UserProperty::Timestamp => write!(f, "__ts"),
            UserProperty::FencingToken => write!(f, "__ft"),
            UserProperty::Status => write!(f, "__stat"),
            UserProperty::StatusMessage => write!(f, "__stMsg"),
            UserProperty::IsApplicationError => write!(f, "__apErr"),
            UserProperty::CommandInvokerId => write!(f, "__invId"),
            UserProperty::InvalidPropertyName => write!(f, "__propName"),
            UserProperty::InvalidPropertyValue => write!(f, "__propVal"),
            UserProperty::ProtocolVersion => write!(f, "__protVer"),
            UserProperty::SupportedMajorVersions => write!(f, "__supProtMajVer"),
            UserProperty::SenderClientId => write!(f, "__sndId"),
            UserProperty::CloudEventSpecVersion => write!(f, "specversion"),
            UserProperty::CloudEventType => write!(f, "type"),
            UserProperty::CloudEventSource => write!(f, "source"),
            UserProperty::CloudEventId => write!(f, "id"),
            UserProperty::CloudEventSubject => write!(f, "subject"),
            UserProperty::CloudEventTime => write!(f, "time"),
            UserProperty::CloudEventDataContentType => write!(f, "datacontenttype"),
            UserProperty::CloudEventDataSchema => write!(f, "dataschema"),
        }
    }
}

impl FromStr for UserProperty {
    type Err = ();

    fn from_str(s: &str) -> Result<Self, Self::Err> {
        match s {
            "__ts" => Ok(UserProperty::Timestamp),
            "__ft" => Ok(UserProperty::FencingToken),
            "__stat" => Ok(UserProperty::Status),
            "__stMsg" => Ok(UserProperty::StatusMessage),
            "__apErr" => Ok(UserProperty::IsApplicationError),
            "__invId" => Ok(UserProperty::CommandInvokerId),
            "__propName" => Ok(UserProperty::InvalidPropertyName),
            "__propVal" => Ok(UserProperty::InvalidPropertyValue),
            "__protVer" => Ok(UserProperty::ProtocolVersion),
            "__supProtMajVer" => Ok(UserProperty::SupportedMajorVersions),
            "__sndId" => Ok(UserProperty::SenderClientId),
            "id" => Ok(UserProperty::CloudEventId),
            "source" => Ok(UserProperty::CloudEventSource),
            "specversion" => Ok(UserProperty::CloudEventSpecVersion),
            "type" => Ok(UserProperty::CloudEventType),
            "subject" => Ok(UserProperty::CloudEventSubject),
            "dataschema" => Ok(UserProperty::CloudEventDataSchema),
            "datacontenttype" => Ok(UserProperty::CloudEventDataContentType),
            "time" => Ok(UserProperty::CloudEventTime),
            _ => Err(()),
        }
    }
}

/// Validates a vector of custom user properties that shouldn't use the reserved prefix
///
/// # Errors
/// Returns a `String` describing the error if
///     - any of `property_list`'s keys start with the [`RESERVED_PREFIX`]
///     - any of `property_list`'s keys or values are invalid utf-8
pub fn validate_user_properties(property_list: &[(String, String)]) -> Result<(), String> {
    for (key, value) in property_list {
        if key.starts_with(RESERVED_PREFIX) {
            return Err(format!(
                "Invalid user data property '{key}' starts with reserved prefix '{RESERVED_PREFIX}'"
            ));
        }
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
    #[test_case(UserProperty::FencingToken; "fencing_token")]
    #[test_case(UserProperty::Status; "status")]
    #[test_case(UserProperty::StatusMessage; "status_message")]
    #[test_case(UserProperty::IsApplicationError; "is_application_error")]
    #[test_case(UserProperty::CommandInvokerId; "command_invoker_id")]
    #[test_case(UserProperty::InvalidPropertyName; "invalid_property_name")]
    #[test_case(UserProperty::InvalidPropertyValue; "invalid_property_value")]
    #[test_case(UserProperty::ProtocolVersion; "protocol_version")]
    #[test_case(UserProperty::SupportedMajorVersions; "supported_major_versions")]
    #[test_case(UserProperty::SenderClientId; "sender_client_id")]
    #[test_case(UserProperty::CloudEventSpecVersion; "cloud_event_spec_version")]
    #[test_case(UserProperty::CloudEventType; "cloud_event_type")]
    #[test_case(UserProperty::CloudEventSource; "cloud_event_source")]
    #[test_case(UserProperty::CloudEventId; "cloud_event_id")]
    #[test_case(UserProperty::CloudEventSubject; "cloud_event_subject")]
    #[test_case(UserProperty::CloudEventTime; "cloud_event_time")]
    #[test_case(UserProperty::CloudEventDataContentType; "cloud_event_data_content_type")]
    #[test_case(UserProperty::CloudEventDataSchema; "cloud_event_data_schema")]
    fn test_to_from_string(prop: UserProperty) {
        assert_eq!(prop, UserProperty::from_str(&prop.to_string()).unwrap());
    }

    /// Tests failure: Custom user data key starts with '__' and an error is returned
    #[test_case(&[("__abcdef".to_string(),"abcdef".to_string())]; "custom_user_data_reserved_prefix")]
    /// Tests failure: Custom user data key is malformed utf-8 and an error is returned
    #[test_case(&[("abc\ndef".to_string(),"abcdef".to_string())]; "custom_user_data_malformed_key")]
    /// Tests failure: Custom user data value is malformed utf-8 and an error is returned
    #[test_case(&[("abcdef".to_string(),"abc\ndef".to_string())]; "custom_user_data_malformed_value")]
    fn test_validate_user_properties_invalid_value(custom_user_data: &[(String, String)]) {
        assert!(validate_user_properties(custom_user_data).is_err());
    }

    #[test]
    fn test_validate_user_properties_valid_value() {
        assert!(validate_user_properties(&[("abcdef".to_string(), "abcdef".to_string())]).is_ok());
    }
}
