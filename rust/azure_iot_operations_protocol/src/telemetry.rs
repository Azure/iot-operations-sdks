// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

//! Envoys for Telemetry operations.
use std::time::SystemTime;

use chrono::{DateTime, Utc};

use crate::common::user_properties::UserProperty;

/// This module contains the telemetry sender implementation.
pub mod telemetry_sender;

/// This module contains the telemetry receiver implementation.
pub mod telemetry_receiver;

const DEFAULT_CLOUD_EVENT_SPEC_VERSION: &str = "1.0";
const DEFAULT_CLOUD_EVENT_EVENT_TYPE: &str = "ms.aio.telemetry";

/// Cloud Event struct
///
/// Implements the cloud event spec 1.0.
/// See [CloudEvents Spec](https://github.com/cloudevents/spec/blob/main/cloudevents/spec.md).
#[derive(Builder, Clone)]
#[builder(setter(into), build_fn(validate = "Self::validate"))]
pub struct CloudEvent {
    // Required fields
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
    // Optional fields
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
    #[builder(default = "(DateTime::<Utc>::from(SystemTime::now())).to_rfc3339()")]
    pub time: String, // This is optional per spec, but we will always add it
}

impl CloudEventBuilder {
    fn validate(&self) -> Result<(), String> {
        let mut spec_version = DEFAULT_CLOUD_EVENT_SPEC_VERSION.to_string();

        if let Some(sv) = &self.spec_version {
            spec_version = sv.to_string();
        }
        // Future versions of the spec may have different requirements
        if spec_version == "1.0" {
            // Required fields are checked in build
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
            if let Some(time) = &self.time {
                if let Err(e) = DateTime::parse_from_rfc3339(time) {
                    return Err(format!("Invalid time: {e}"));
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
    /// Per spec, `subject` and `data_content_type` are optional, but we will always include them
    #[must_use]
    pub fn to_headers(
        self,
        id: String,
        subject: String,
        data_content_type: String,
    ) -> Vec<(String, String)> {
        let mut headers = vec![
            (UserProperty::CloudEventId.to_string(), id),
            (UserProperty::CloudEventSource.to_string(), self.source),
            (
                UserProperty::CloudEventSpecVersion.to_string(),
                self.spec_version,
            ),
            (UserProperty::CloudEventType.to_string(), self.event_type),
            (UserProperty::CloudEventTime.to_string(), self.time),
            (UserProperty::CloudEventSubject.to_string(), subject),
            (
                UserProperty::CloudEventDataContentType.to_string(),
                data_content_type,
            ),
        ];
        if let Some(data_schema) = self.data_schema {
            headers.push((UserProperty::CloudEventDataSchema.to_string(), data_schema));
        }
        headers
    }
}
