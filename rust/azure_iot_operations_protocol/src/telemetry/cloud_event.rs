// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

use std::{
    fmt::{self, Display, Formatter},
    str::FromStr,
};

pub(crate) const DEFAULT_CLOUD_EVENT_SPEC_VERSION: &str = "1.0";
pub(crate) const DEFAULT_CLOUD_EVENT_EVENT_TYPE: &str = "ms.aio.telemetry";

/// Enum representing the cloud event fields.
#[derive(Debug, Copy, Clone, PartialEq)]
pub enum CloudEventFields {
    /// Identifies the event. Producers MUST ensure that source + id is unique for each distinct event.
    /// If a duplicate event is re-sent (e.g. due to a network error) it MAY have the same id.
    /// Consumers MAY assume that Events with identical source and id are duplicates.
    Id,
    /// Identifies the context in which an event happened.
    /// Often this will include information such as the type of the event source,
    /// the organization publishing the event or the process that produced the event.
    /// The exact syntax and semantics behind the data encoded in the URI is defined by the event producer.
    Source,
    /// The version of the `CloudEvents` specification which the event uses. This enables the
    /// interpretation of the context. Compliant event producers MUST use a value of 1.0 when
    /// referring to this version of the specification.
    SpecVersion,
    /// Describes the type of event related to the originating occurrence.
    /// Often this attribute is used for routing, observability, policy enforcement, etc.
    /// The format of this is producer defined and might include information such as the version of the type
    EventType,
    /// Identifies the subject of the event in the context of the event producer (identified by source).
    /// In publish-subscribe scenarios, a subscriber will typically subscribe to events emitted by a source,
    /// but the source identifier alone might not be sufficient as a qualifier for any specific event if the source context has internal sub-structure.
    Subject,
    /// Timestamp of when the occurrence happened.
    /// If the time of the occurrence cannot be determined then this attribute MAY be set to some other time (such as the current time)
    /// by the `CloudEvents` producer,
    /// however all producers for the same source MUST be consistent in this respect.
    Time,
    ///  Content type of data value. This attribute enables data to carry any type of content,
    ///  whereby format and encoding might differ from that of the chosen event format.
    DataContentType,
    ///  Identifies the schema that data adheres to.
    ///  Incompatible changes to the schema SHOULD be reflected by a different URI.
    DataSchema,
}

impl CloudEventFields {
    pub fn validate(&self, value: &str, spec_version: &str) -> Result<(), String> {
        if spec_version == "1.0" {
            match self {
                CloudEventFields::Id
                | CloudEventFields::Source
                | CloudEventFields::SpecVersion
                | CloudEventFields::EventType
                | CloudEventFields::DataSchema
                | CloudEventFields::Subject
                | CloudEventFields::Time
                | CloudEventFields::DataContentType => {
                    if value.is_empty() {
                        return Err(format!("{self} cannot be empty"));
                    }
                    // TODO: Ensure DataContentType adheres to RFC 2046
                }
            }
        } else {
            return Err(format!("Invalid spec version: {spec_version}"));
        }
        Ok(())
    }
}

impl Display for CloudEventFields {
    fn fmt(&self, f: &mut Formatter<'_>) -> fmt::Result {
        match self {
            CloudEventFields::SpecVersion => write!(f, "specversion"),
            CloudEventFields::EventType => write!(f, "type"),
            CloudEventFields::Source => write!(f, "source"),
            CloudEventFields::Id => write!(f, "id"),
            CloudEventFields::Subject => write!(f, "subject"),
            CloudEventFields::Time => write!(f, "time"),
            CloudEventFields::DataContentType => write!(f, "datacontenttype"),
            CloudEventFields::DataSchema => write!(f, "dataschema"),
        }
    }
}

impl FromStr for CloudEventFields {
    type Err = ();

    fn from_str(s: &str) -> Result<Self, Self::Err> {
        match s {
            "id" => Ok(CloudEventFields::Id),
            "source" => Ok(CloudEventFields::Source),
            "specversion" => Ok(CloudEventFields::SpecVersion),
            "type" => Ok(CloudEventFields::EventType),
            "subject" => Ok(CloudEventFields::Subject),
            "dataschema" => Ok(CloudEventFields::DataSchema),
            "datacontenttype" => Ok(CloudEventFields::DataContentType),
            "time" => Ok(CloudEventFields::Time),
            _ => Err(()),
        }
    }
}

#[cfg(test)]
mod tests {
    use test_case::test_case;

    use super::*;

    #[test_case(CloudEventFields::SpecVersion; "cloud_event_spec_version")]
    #[test_case(CloudEventFields::EventType; "cloud_event_type")]
    #[test_case(CloudEventFields::Source; "cloud_event_source")]
    #[test_case(CloudEventFields::Id; "cloud_event_id")]
    #[test_case(CloudEventFields::Subject; "cloud_event_subject")]
    #[test_case(CloudEventFields::Time; "cloud_event_time")]
    #[test_case(CloudEventFields::DataContentType; "cloud_event_data_content_type")]
    #[test_case(CloudEventFields::DataSchema; "cloud_event_data_schema")]
    fn test_cloud_event_to_from_string(prop: CloudEventFields) {
        assert_eq!(prop, CloudEventFields::from_str(&prop.to_string()).unwrap());
    }

    #[test]
    fn test_cloud_event_validate_empty() {
        CloudEventFields::Id
            .validate("", DEFAULT_CLOUD_EVENT_SPEC_VERSION)
            .unwrap_err();
        CloudEventFields::Source
            .validate("", DEFAULT_CLOUD_EVENT_SPEC_VERSION)
            .unwrap_err();
        CloudEventFields::SpecVersion
            .validate("", DEFAULT_CLOUD_EVENT_SPEC_VERSION)
            .unwrap_err();
        CloudEventFields::EventType
            .validate("", DEFAULT_CLOUD_EVENT_SPEC_VERSION)
            .unwrap_err();
        CloudEventFields::DataSchema
            .validate("", DEFAULT_CLOUD_EVENT_SPEC_VERSION)
            .unwrap_err();
        CloudEventFields::Subject
            .validate("", DEFAULT_CLOUD_EVENT_SPEC_VERSION)
            .unwrap_err();
        CloudEventFields::Time
            .validate("", DEFAULT_CLOUD_EVENT_SPEC_VERSION)
            .unwrap_err();
        CloudEventFields::DataContentType
            .validate("", DEFAULT_CLOUD_EVENT_SPEC_VERSION)
            .unwrap_err();
    }

    #[test]
    fn test_cloud_event_validate_invalid_spec_version() {
        CloudEventFields::Id.validate("id", "0.0").unwrap_err();
    }
}
