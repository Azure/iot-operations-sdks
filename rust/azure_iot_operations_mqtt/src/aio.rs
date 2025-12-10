// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

//! AIO Specific tools

use std::{
    fmt::{self, Display, Formatter},
    str::FromStr,
    time::SystemTime,
};

use chrono::{DateTime, SecondsFormat, Utc};
use fluent_uri::{Uri, UriRef};
use regex::Regex;
use uuid::Uuid;

use crate::control_packet::PublishProperties;

/// Default spec version for a `CloudEvent`. Compliant event producers MUST
/// use a value of 1.0 when referring to this version of the specification.
pub const DEFAULT_CLOUD_EVENT_SPEC_VERSION: &str = "1.0";

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
    /// Validates that the cloud event field is valid based on the spec version.
    ///
    /// # Errors
    /// Returns a string containing the error message if either the field or spec version are invalid.
    ///
    /// # Panics
    /// If any regex fails to compile which is impossible given that the regex is pre-defined.
    pub fn validate(&self, value: &str, spec_version: &str) -> Result<(), String> {
        if spec_version == "1.0" {
            if value.is_empty() {
                return Err(format!("{self} cannot be empty"));
            }
            match self {
                CloudEventFields::Id
                | CloudEventFields::SpecVersion
                | CloudEventFields::EventType
                | CloudEventFields::Subject => {}
                CloudEventFields::Source => {
                    // URI reference
                    match UriRef::parse(value) {
                        Ok(_) => {}
                        Err(e) => {
                            return Err(format!(
                                "Invalid {self} value: {value}. Must adhere to RFC 3986 Section 4.1. Error: {e}"
                            ));
                        }
                    }
                }
                CloudEventFields::DataSchema => {
                    // URI
                    match Uri::parse(value) {
                        Ok(_) => {}
                        Err(e) => {
                            return Err(format!(
                                "Invalid {self} value: {value}. Must adhere to RFC 3986 Section 4.3. Error: {e}"
                            ));
                        }
                    }
                }
                CloudEventFields::Time => {
                    // serializable as RFC3339
                    match DateTime::parse_from_rfc3339(value) {
                        Ok(_) => {}
                        Err(e) => {
                            return Err(format!(
                                "Invalid {self} value: {value}. Must adhere to RFC 3339. Error: {e}"
                            ));
                        }
                    }
                }
                CloudEventFields::DataContentType => {
                    let rfc_2045_regex =
                        Regex::new(r"^([-a-z]+)/([-a-z0-9.]+)(\+([-a-z0-9.]+))?(;.*)?$")
                            .expect("Static regex string should not fail");

                    if !rfc_2045_regex.is_match(value) {
                        return Err(format!(
                            "Invalid {self} value: {value}. Must adhere to RFC 2045"
                        ));
                    }
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

/// Cloud Event struct.
///
/// Implements the cloud event spec 1.0 for the telemetry sender.
/// See [CloudEvents Spec](https://github.com/cloudevents/spec/blob/main/cloudevents/spec.md).
#[derive(Builder, Clone, Debug)]
#[builder(setter(into), build_fn(validate = "Self::validate"))]
pub struct CloudEvent {
    /// Identifies the context in which an event happened. Often this will include information such
    /// as the type of the event source, the organization publishing the event or the process that
    /// produced the event. The exact syntax and semantics behind the data encoded in the URI is
    /// defined by the event producer.
    pub source: String,
    /// The version of the cloud events specification which the event uses. This enables the
    /// interpretation of the context. Compliant event producers MUST use a value of 1.0 when
    /// referring to this version of the specification.
    #[builder(default = "DEFAULT_CLOUD_EVENT_SPEC_VERSION.to_string()")] // no default on recv
    pub spec_version: String,
    /// Contains a value describing the type of event related to the originating occurrence. Often
    /// this attribute is used for routing, observability, policy enforcement, etc. The format of
    /// this is producer defined and might include information such as the version of the type.
    pub event_type: String,
    /// Identifies the schema that data adheres to. Incompatible changes to the schema SHOULD be
    /// reflected by a different URI.
    #[builder(default = "None")]
    pub data_schema: Option<String>,
    /// Identifies the event. Producers MUST ensure that source + id is unique for each distinct
    /// event. If a duplicate event is re-sent (e.g. due to a network error) it MAY have the same
    /// id. Consumers MAY assume that Events with identical source and id are duplicates.
    #[builder(default = "Uuid::new_v4().to_string()")] // no default on recv
    pub id: String,
    /// Timestamp of when the occurrence happened. If the time of the occurrence cannot be
    /// determined then this attribute MAY be set to some other time (such as the current time) by
    /// the cloud event producer, however all producers for the same source MUST be consistent in
    /// this respect. In other words, either they all use the actual time of the occurrence or they
    /// all use the same algorithm to determine the value used.
    #[builder(default = "Some(DateTime::<Utc>::from(SystemTime::now()))")]
    pub time: Option<DateTime<Utc>>,
    /// Identifies the subject of the event in the context of the event producer (identified by
    /// source). In publish-subscribe scenarios, a subscriber will typically subscribe to events
    /// emitted by a source, but the source identifier alone might not be sufficient as a qualifier
    /// for any specific event if the source context has internal sub-structure.
    #[builder(default = "None")]
    pub subject: Option<String>,
    /// Content type of data value. This attribute enables data to carry any type of content,
    /// whereby format and encoding might differ from that of the chosen event format.
    #[builder(default = "None")]
    pub data_content_type: Option<String>,
}

impl CloudEventBuilder {
    fn validate(&self) -> Result<(), String> {
        let mut spec_version = DEFAULT_CLOUD_EVENT_SPEC_VERSION.to_string();

        if let Some(sv) = &self.spec_version {
            CloudEventFields::SpecVersion.validate(sv, &spec_version)?;
            spec_version.clone_from(sv);
        }

        if let Some(source) = &self.source {
            CloudEventFields::Source.validate(source, &spec_version)?;
        }

        if let Some(event_type) = &self.event_type {
            CloudEventFields::EventType.validate(event_type, &spec_version)?;
        }

        if let Some(Some(data_schema)) = &self.data_schema {
            CloudEventFields::DataSchema.validate(data_schema, &spec_version)?;
        }

        if let Some(id) = &self.id {
            CloudEventFields::Id.validate(id, &spec_version)?;
        }

        if let Some(Some(data_content_type)) = &self.data_content_type {
            CloudEventFields::DataContentType.validate(data_content_type, &spec_version)?;
        }

        if let Some(Some(subject)) = &self.subject {
            CloudEventFields::Subject.validate(subject, &spec_version)?;
        }

        // time does not need to be validated because converting it to an rfc3339 compliant string will always succeed

        Ok(())
    }
}

impl CloudEvent {
    /// Parse a [`CloudEvent`] from a Publish's [`PublishProperties`].
    /// Note that this will return an error if the [`PublishProperties`] do not contain the required fields for a [`CloudEvent`].
    ///
    /// # Errors
    /// [`CloudEventBuilderError::UninitializedField`] if the [`PublishProperties`] do not contain the required fields for a [`CloudEvent`].
    ///
    /// [`CloudEventBuilderError::ValidationError`] if any of the field values are not valid for a [`CloudEvent`].
    pub fn from_publish_properties(
        publish_properties: &PublishProperties,
    ) -> Result<Self, CloudEventBuilderError> {
        Self::from_user_properties_and_content_type(
            &publish_properties.user_properties,
            publish_properties.content_type.as_ref(),
        )
    }

    /// Parse a [`CloudEvent`] from a Publish's [`PublishProperties`].
    /// Note that this will return an error if the [`PublishProperties`] do not contain the required fields for a [`CloudEvent`].
    ///
    /// # Errors
    /// [`CloudEventBuilderError::UninitializedField`] if the [`PublishProperties`] do not contain the required fields for a [`CloudEvent`].
    ///
    /// [`CloudEventBuilderError::ValidationError`] if any of the field values are not valid for a [`CloudEvent`].
    pub fn from_user_properties_and_content_type(
        user_properties: &Vec<(String, String)>,
        content_type: Option<&String>,
    ) -> Result<Self, CloudEventBuilderError> {
        // use builder so that all fields can be validated together
        let mut received_cloud_event_builder = ReceivedCloudEventBuilder::default();
        if let Some(content_type) = content_type {
            received_cloud_event_builder.data_content_type(content_type.clone());
        }

        for (key, value) in user_properties {
            match CloudEventFields::from_str(key) {
                Ok(CloudEventFields::Id) => {
                    received_cloud_event_builder.id(value);
                }
                Ok(CloudEventFields::Source) => {
                    received_cloud_event_builder.source(value);
                }
                Ok(CloudEventFields::SpecVersion) => {
                    received_cloud_event_builder.spec_version(value);
                }
                Ok(CloudEventFields::EventType) => {
                    received_cloud_event_builder.event_type(value);
                }
                Ok(CloudEventFields::Subject) => {
                    received_cloud_event_builder.subject(Some(value.into()));
                }
                Ok(CloudEventFields::DataSchema) => {
                    received_cloud_event_builder.data_schema(Some(value.into()));
                }
                Ok(CloudEventFields::Time) => {
                    received_cloud_event_builder.builder_time(Some(value.into()));
                }
                _ => {}
            }
        }
        let mut received_cloud_event =
            received_cloud_event_builder.build().map_err(|e| match e {
                ReceivedCloudEventBuilderError::UninitializedField(field_name) => {
                    CloudEventBuilderError::UninitializedField(field_name)
                }
                ReceivedCloudEventBuilderError::ValidationError(message) => {
                    CloudEventBuilderError::ValidationError(message)
                }
            })?;
        // now that everything is validated, update the time field to its correct typing
        // NOTE: If the spec_version changes in the future, that may need to be taken into account here.
        // For now, the builder validates spec version 1.0
        if let Some(ref time_str) = received_cloud_event.builder_time {
            match DateTime::parse_from_rfc3339(time_str) {
                Ok(parsed_time) => {
                    let time = parsed_time.with_timezone(&Utc);
                    received_cloud_event.time = Some(time);
                }
                Err(_) => {
                    // Builder should have already caught this error
                    unreachable!()
                }
            }
        }
        Ok(received_cloud_event.into())
    }

    /// Get [`CloudEvent`] as headers for an MQTT message
    /// This fn ignores `data_content_type` so that it can be set separately if needed
    #[must_use]
    pub fn into_headers(self) -> Vec<(String, String)> {
        let mut headers = vec![
            (CloudEventFields::Id.to_string(), self.id),
            (CloudEventFields::Source.to_string(), self.source),
            (CloudEventFields::SpecVersion.to_string(), self.spec_version),
            (CloudEventFields::EventType.to_string(), self.event_type),
        ];
        if let Some(subject) = self.subject {
            headers.push((CloudEventFields::Subject.to_string(), subject));
        }
        if let Some(time) = self.time {
            headers.push((
                CloudEventFields::Time.to_string(),
                time.to_rfc3339_opts(SecondsFormat::Secs, true),
            ));
        }
        if let Some(data_schema) = self.data_schema {
            headers.push((CloudEventFields::DataSchema.to_string(), data_schema));
        }
        headers
    }

    /// Set [`CloudEvent`] as headers on a [`PublishProperties`] for an MQTT message
    /// Note that if `data_content_type` is `Some` on the [`CloudEvent`], the value will override
    /// any `content_type` already set in the `PublishProperties`
    #[must_use]
    pub fn set_on_publish_properties(
        self,
        mut publish_properties: PublishProperties,
    ) -> PublishProperties {
        if let Some(ref data_content_type) = self.data_content_type {
            publish_properties.content_type = Some(data_content_type.clone());
        }
        let mut headers = self.into_headers();
        publish_properties.user_properties.append(&mut headers);
        publish_properties
    }
}

/// Internal Cloud Event struct for building a [`CloudEvent`] from received [`PublishProperties`].
///
/// Implements the cloud event spec 1.0.
/// See [CloudEvents Spec](https://github.com/cloudevents/spec/blob/main/cloudevents/spec.md).
#[derive(Builder, Clone)]
#[builder(setter(into), build_fn(validate = "Self::validate"))]
struct ReceivedCloudEvent {
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
    pub spec_version: String,
    /// Contains a value describing the type of event related to the originating occurrence. Often
    /// this attribute is used for routing, observability, policy enforcement, etc. The format of
    /// this is producer defined and might include information such as the version of the type.
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
    #[builder(setter(skip))]
    pub time: Option<DateTime<Utc>>,
    /// time as a string so that it can be validated during build
    #[builder(default = "None")]
    builder_time: Option<String>,
}

impl ReceivedCloudEventBuilder {
    // now that spec version is known, all fields can be validated against that spec version
    fn validate(&self) -> Result<(), String> {
        let mut spec_version = DEFAULT_CLOUD_EVENT_SPEC_VERSION.to_string();

        if let Some(sv) = &self.spec_version {
            CloudEventFields::SpecVersion.validate(sv, &spec_version)?;
            spec_version.clone_from(sv);
        }

        if let Some(id) = &self.id {
            CloudEventFields::Id.validate(id, &spec_version)?;
        }

        if let Some(source) = &self.source {
            CloudEventFields::Source.validate(source, &spec_version)?;
        }

        if let Some(event_type) = &self.event_type {
            CloudEventFields::EventType.validate(event_type, &spec_version)?;
        }

        if let Some(Some(subject)) = &self.subject {
            CloudEventFields::Subject.validate(subject, &spec_version)?;
        }

        if let Some(Some(data_schema)) = &self.data_schema {
            CloudEventFields::DataSchema.validate(data_schema, &spec_version)?;
        }

        if let Some(Some(data_content_type)) = &self.data_content_type {
            CloudEventFields::DataContentType.validate(data_content_type, &spec_version)?;
        }

        if let Some(Some(builder_time)) = &self.builder_time {
            CloudEventFields::Time.validate(builder_time, &spec_version)?;
        }

        Ok(())
    }
}

// implementing display because debug prints private fields
impl Display for CloudEvent {
    fn fmt(&self, f: &mut std::fmt::Formatter<'_>) -> std::fmt::Result {
        write!(
            f,
            "CloudEvent {{ id: {id}, source: {source}, spec_version: {spec_version}, event_type: {event_type}, subject: {subject}, data_schema: {data_schema}, data_content_type: {data_content_type}, time: {time:?} }}",
            id = self.id,
            source = self.source,
            spec_version = self.spec_version,
            event_type = self.event_type,
            subject = self.subject.as_deref().unwrap_or("None"),
            data_schema = self.data_schema.as_deref().unwrap_or("None"),
            data_content_type = self.data_content_type.as_deref().unwrap_or("None"),
            time = self.time,
        )
    }
}

impl From<ReceivedCloudEvent> for CloudEvent {
    fn from(received: ReceivedCloudEvent) -> Self {
        CloudEvent {
            id: received.id,
            source: received.source,
            spec_version: received.spec_version,
            event_type: received.event_type,
            subject: received.subject,
            data_schema: received.data_schema,
            data_content_type: received.data_content_type,
            time: received.time,
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

    #[test_case("aio://oven/sample", true; "absolute_uri")]
    #[test_case("./bar", true; "uri_reference")]
    #[test_case("::::", false; "not_uri_reference")]
    fn test_cloud_event_validate_invalid_source(source: &str, expected: bool) {
        assert_eq!(
            CloudEventFields::Source
                .validate(source, DEFAULT_CLOUD_EVENT_SPEC_VERSION)
                .is_ok(),
            expected
        );
    }

    #[test_case("aio://oven/sample", true; "absolute_uri")]
    #[test_case("./bar", false; "uri_reference")]
    #[test_case("ht!tp://example.com", false; "invalid_uri")]
    fn test_cloud_event_validate_data_schema(data_schema: &str, expected: bool) {
        assert_eq!(
            CloudEventFields::DataSchema
                .validate(data_schema, DEFAULT_CLOUD_EVENT_SPEC_VERSION)
                .is_ok(),
            expected
        );
    }

    #[test_case("application/json", true; "json")]
    #[test_case("text/csv", true; "csv")]
    #[test_case("application/avro", true; "avro")]
    #[test_case("application/octet-stream", true; "dash_second_half")]
    #[test_case("application/f0o", true; "number_second_half")]
    #[test_case("application/f.o", true; "period_second_half")]
    #[test_case("foo/bar+bazz", true; "plus_extra")]
    #[test_case("f0o/json", false; "number_first_half")]
    #[test_case("foo", false; "no_slash")]
    #[test_case("foo/bar?bazz", false; "question_mark")]
    #[test_case("application/json; charset=utf-8", true; "parameter")]
    fn test_cloud_event_validate_data_content_type(data_content_type: &str, expected: bool) {
        assert_eq!(
            CloudEventFields::DataContentType
                .validate(data_content_type, DEFAULT_CLOUD_EVENT_SPEC_VERSION)
                .is_ok(),
            expected
        );
    }

    #[test]
    fn test_cloud_event_validate_invalid_spec_version() {
        CloudEventFields::Id.validate("id", "0.0").unwrap_err();
    }
}
