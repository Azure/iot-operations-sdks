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

const DEFAULT_SPEC_VERSION: &str = "1.0";
const DEFAULT_EVENT_TYPE: &str = "ms.aio.telemetry";

#[derive(Builder, Clone)]
#[builder(setter(into), build_fn(validate = "Self::validate"))]
pub struct CloudEvent {
    // Required fields
    pub id: String,
    pub source: String,
    //#[builder(default = "\"1.0\".to_string()")]
    #[builder(default = "DEFAULT_SPEC_VERSION.to_string()")]
    pub spec_version: String,
    #[builder(default = "DEFAULT_EVENT_TYPE.to_string()")]
    pub event_type: String,
    // Optional fields
    #[builder(default = "None")]
    pub subject: Option<String>,
    #[builder(default = "None")]
    pub data_schema: Option<String>,
    #[builder(default = "None")]
    pub data_content_type: Option<String>,
    #[builder(default = "(DateTime::<Utc>::from(SystemTime::now())).to_rfc3339()")]
    pub time: String, // This is optional per spec, but we will always add it
}

impl CloudEventBuilder {
    fn validate(&self) -> Result<(), String> {
        let mut spec_version = DEFAULT_SPEC_VERSION.to_string();

        if let Some(sv) = &self.spec_version {
            spec_version = sv.to_string();
        }
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
