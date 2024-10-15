// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

use std::{
    fmt::{self, Display, Formatter},
    str::FromStr,
};

use chrono::{DateTime, Utc};

const DEFAULT_CLOUD_EVENT_SPEC_VERSION: &str = "1.0";
const DEFAULT_CLOUD_EVENT_EVENT_TYPE: &str = "ms.aio.telemetry";

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
    /// The version of the CloudEvents specification which the event uses. This enables the
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

/// Cloud Event struct
///
/// Implements the cloud event spec 1.0.
/// See [CloudEvents Spec](https://github.com/cloudevents/spec/blob/main/cloudevents/spec.md).
#[derive(Builder, Clone)]
#[builder(setter(into), build_fn(validate = "Self::validate"))]
pub struct CloudEvent {
    /// Identifies the event. Producers MUST ensure that source + id is unique for each distinct
    /// event. If a duplicate event is re-sent (e.g. due to a network error) it MAY have the same
    /// id. Consumers MAY assume that Events with identical source and id are duplicates.
    pub id: String,
    /// Identifies the context in which an event happened. Often this will include information such
    /// as the type of the event source, the organization publishing the event or the process that
    /// produced the event. The exact syntax and semantics behind the data encoded in the URI is
    /// defined by the event producer.
    pub source: String,
    /// The version of the cloud events specification which the event uses. This enables the
    /// interpretation of the context. Compliant event producers MUST use a value of 1.0 when
    /// referring to this version of the specification.
    #[builder(default = "DEFAULT_CLOUD_EVENT_SPEC_VERSION.to_string()")]
    pub spec_version: String,
    /// Contains a value describing the type of event related to the originating occurrence. Often
    /// this attribute is used for routing, observability, policy enforcement, etc. The format of
    /// this is producer defined and might include information such as the version of the type.
    #[builder(default = "DEFAULT_CLOUD_EVENT_EVENT_TYPE.to_string()")]
    pub event_type: String,
    /// Identifies the subject of the event in the context of the event producer (identified by
    /// source). In publish-subscribe scenarios, a subscriber will typically subscribe to events
    /// emitted by a source, but the source identifier alone might not be sufficient as a qualifier
    /// for any specific event if the source context has internal sub-structure.
    #[builder(default = "None")]
    pub subject: Option<String>,
    /// Identifies the schema that data adheres to. Incompatible changes to the schema SHOULD be
    /// reflected by a different URI.
    #[builder(default = "None")]
    pub data_schema: Option<String>,
    /// Content type of data value. This attribute enables data to carry any type of content,
    /// whereby format and encoding might differ from that of the chosen event format.
    #[builder(default = "None")]
    pub data_content_type: Option<String>,
    /// Timestamp of when the occurrence happened. If the time of the occurrence cannot be
    /// determined then this attribute MAY be set to some other time (such as the current time) by
    /// the cloud event producer, however all producers for the same source MUST be consistent in
    /// this respect. In other words, either they all use the actual time of the occurrence or they
    /// all use the same algorithm to determine the value used.
    #[builder(default = "None")]
    #[builder(setter(skip))]
    pub time: Option<DateTime<Utc>>, // This is optional per spec, but we will always add it
}

impl CloudEventBuilder {
    fn validate(&self) -> Result<(), String> {
        let mut spec_version = DEFAULT_CLOUD_EVENT_SPEC_VERSION.to_string();

        if let Some(sv) = &self.spec_version {
            spec_version = sv.to_string();
        }
        // Future versions of the spec may have different requirements
        if spec_version == "1.0" {
            // Any missing required fields are caught by the builder
            if let Some(id) = &self.id {
                if id.is_empty() {
                    return Err("id cannot be empty".to_string());
                }
            }
            if let Some(source) = &self.source {
                if source.is_empty() {
                    return Err("source cannot be empty".to_string());
                }
            }
            if let Some(event_type) = &self.event_type {
                if event_type.is_empty() {
                    return Err("event_type cannot be empty".to_string());
                }
            }
        } else {
            return Err("Invalid spec_version".to_string());
        }
        Ok(())
    }
}

impl CloudEvent {
    /// Get Cloud Event as headers for MQTT message
    #[must_use]
    pub fn to_headers(self) -> Vec<(String, String)> {
        let mut headers = vec![
            (CloudEventFields::Id.to_string(), self.id),
            (CloudEventFields::Source.to_string(), self.source),
            (CloudEventFields::SpecVersion.to_string(), self.spec_version),
            (CloudEventFields::EventType.to_string(), self.event_type),
        ];
        if let Some(subject) = self.subject {
            headers.push((CloudEventFields::Subject.to_string(), subject));
        }
        if let Some(data_schema) = self.data_schema {
            headers.push((CloudEventFields::DataSchema.to_string(), data_schema));
        }
        if let Some(data_content_type) = self.data_content_type {
            headers.push((
                CloudEventFields::DataContentType.to_string(),
                data_content_type,
            ));
        }
        if let Some(time) = self.time {
            headers.push((CloudEventFields::Time.to_string(), time.to_rfc3339()));
        }
        headers
    }
}

impl Display for CloudEvent {
    fn fmt(&self, f: &mut std::fmt::Formatter<'_>) -> std::fmt::Result {
        write!(
            f,
            "id: {} \n\
            source: {} \n\
            event_type: {} \n\
            subject: {:?} \n\
            data_schema: {:?} \n\
            data_content_type: {:?} \n\
            time: {:?}",
            self.id,
            self.source,
            self.event_type,
            self.subject,
            self.data_schema,
            self.data_content_type,
            self.time
        )
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
}
