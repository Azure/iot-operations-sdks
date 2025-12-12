// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

use std::time::SystemTime;

use azure_iot_operations_mqtt::aio::cloud_event::{
    CloudEventFields, DEFAULT_CLOUD_EVENT_SPEC_VERSION,
};
use chrono::{DateTime, SecondsFormat, Utc};
use uuid::Uuid;

/// Protocol-level Cloud Event struct used for sending messages of various types (e.g., telemetry, RPC).
///
/// Implements the Cloud Events spec 1.0 for all protocol messages, including telemetry and request/response (RPC).
/// See [CloudEvents Spec](https://github.com/cloudevents/spec/blob/main/cloudevents/spec.md).
#[derive(Builder, Clone, Debug)]
#[builder(setter(into), build_fn(validate = "Self::validate"))]
pub struct CloudEvent {
    /// Identifies the context in which an event happened. Often this will include information such
    /// as the type of the event source, the organization publishing the event or the process that
    /// produced the event. The exact syntax and semantics behind the data encoded in the URI is
    /// defined by the event producer.
    source: String,
    /// The version of the cloud events specification which the event uses. This enables the
    /// interpretation of the context. Compliant event producers MUST use a value of 1.0 when
    /// referring to this version of the specification.
    #[builder(default = "DEFAULT_CLOUD_EVENT_SPEC_VERSION.to_string()")]
    pub spec_version: String,
    /// Contains a value describing the type of event related to the originating occurrence. Often
    /// this attribute is used for routing, observability, policy enforcement, etc. The format of
    /// this is producer defined and might include information such as the version of the type.
    #[builder(default = "self.custom_default_event_type()")]
    event_type: String,
    /// Identifies the schema that data adheres to. Incompatible changes to the schema SHOULD be
    /// reflected by a different URI.
    #[builder(default = "None")]
    data_schema: Option<String>,
    /// Identifies the event. Producers MUST ensure that source + id is unique for each distinct
    /// event. If a duplicate event is re-sent (e.g. due to a network error) it MAY have the same
    /// id. Consumers MAY assume that Events with identical source and id are duplicates.
    #[builder(default = "Uuid::new_v4().to_string()")]
    id: String,
    /// Timestamp of when the occurrence happened. If the time of the occurrence cannot be
    /// determined then this attribute MAY be set to some other time (such as the current time) by
    /// the cloud event producer, however all producers for the same source MUST be consistent in
    /// this respect. In other words, either they all use the actual time of the occurrence or they
    /// all use the same algorithm to determine the value used.
    #[builder(default = "Some(DateTime::<Utc>::from(SystemTime::now()))")]
    time: Option<DateTime<Utc>>,
    /// Identifies the subject of the event in the context of the event producer (identified by
    /// source). In publish-subscribe scenarios, a subscriber will typically subscribe to events
    /// emitted by a source, but the source identifier alone might not be sufficient as a qualifier
    /// for any specific event if the source context has internal sub-structure.
    #[builder(default = "CloudEventSubject::PublishTopic")]
    subject: CloudEventSubject,
    #[builder(private)]
    _default_event_type: String,
}

/// Enum representing the different values that the [`subject`](CloudEventBuilder::subject) field of a [`CloudEvent`] can take.
#[derive(Clone, Debug)]
pub enum CloudEventSubject {
    /// The publish topic should be used as the subject when the [`CloudEvent`] is sent across the wire
    PublishTopic,
    /// A custom (provided) `String` should be used for the `subject` of the [`CloudEvent`]
    Custom(String),
    /// No subject should be included on the [`CloudEvent`]
    None,
}

impl CloudEventBuilder {
    pub fn new(default_event_type: String) -> Self {
        CloudEventBuilder {
            _default_event_type: Some(default_event_type),
            ..Default::default()
        }
    }

    fn custom_default_event_type(&self) -> String {
        self._default_event_type.clone().expect("This CloudEventBuilder must be initialized with a default event type or one must be set on the builder")
    }

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

        if let Some(CloudEventSubject::Custom(subject)) = &self.subject {
            CloudEventFields::Subject.validate(subject, &spec_version)?;
        }

        // time does not need to be validated because converting it to an rfc3339 compliant string will always succeed

        Ok(())
    }
}

impl CloudEvent {
    /// Get [`CloudEvent`] as user properties for an MQTT publish
    #[must_use]
    pub fn into_headers(self, publish_topic: &str) -> Vec<(String, String)> {
        let mut headers = vec![
            (CloudEventFields::Id.to_string(), self.id),
            (CloudEventFields::Source.to_string(), self.source),
            (CloudEventFields::SpecVersion.to_string(), self.spec_version),
            (CloudEventFields::EventType.to_string(), self.event_type),
        ];
        match self.subject {
            CloudEventSubject::Custom(subject) => {
                headers.push((CloudEventFields::Subject.to_string(), subject));
            }
            CloudEventSubject::PublishTopic => {
                headers.push((
                    CloudEventFields::Subject.to_string(),
                    publish_topic.to_string(),
                ));
            }
            CloudEventSubject::None => {}
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
}
