// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

//! Types for determining the Cloud Event headers the connector sends.

use azure_iot_operations_mqtt::aio::cloud_event as aio_cloud_event;
use chrono::{DateTime, Utc};
use derive_builder::Builder;

/// Determines the `source` header of a Cloud Event
#[derive(Clone, Debug, Default)]
#[non_exhaustive]
pub enum CloudEventSource {
    /// `ms-aio:<device identifier>[/<data source>]`, where:
    /// - the device identifier is the device's external ID, trimmed, if it differs from the
    ///   device's UUID, isn't empty, and forms a valid `source`; otherwise the device name
    /// - the data source is the asset component's `dataSource`, trimmed and without leading `/`s,
    ///   if it isn't empty and the resulting `source` is valid
    #[default]
    Automatic,
    /// `ms-aio:<identifier>[/<data source>]`, where the identifier is trimmed and the data source
    /// is the same as for [`Automatic`](CloudEventSource::Automatic). The inbound endpoint address
    /// is a recommended identifier. Building the Cloud Event returns an error if the trimmed
    /// identifier is empty or doesn't form a valid `source`.
    AutomaticWithProtocolSpecificIdentifier(String),
    /// The provided `source`, used as is
    Custom(String),
}

/// Determines the `type` header of a Cloud Event
#[derive(Clone, Debug, Default)]
#[non_exhaustive]
pub enum CloudEventType {
    /// `<kind>[/<type ref>]`, where the kind is the kind of asset component (`DataSet`, `Event`,
    /// or `Stream`) and the type ref is the asset component's `typeRef`, if any
    #[default]
    Automatic,
    /// The provided `type`
    Custom(String),
}

/// Determines the `subject` header of a Cloud Event
#[derive(Clone, Debug, Default)]
#[non_exhaustive]
pub enum CloudEventSubject {
    /// `<asset identifier>/<asset component name>`, where:
    /// - the asset identifier is the asset's external ID, trimmed, if it differs from the asset's
    ///   UUID and isn't empty; otherwise the asset name
    /// - the asset component name is the component's name, or `<event group name>/<event name>`
    ///   for an event
    #[default]
    Automatic,
    /// The topic the message is published to
    PublishTopic,
    /// The provided `subject`
    Custom(String),
    /// No `subject`
    None,
}

/// Determines the `dataschema` header of a Cloud Event
#[derive(Clone, Debug, Default)]
#[non_exhaustive]
pub enum CloudEventDataSchema {
    /// `aio-sr://<registry namespace>/<schema name>:<schema version>` for the message schema
    /// reported for the asset component, or no `dataschema` if one hasn't been reported
    #[default]
    Automatic,
    /// The provided `dataschema`
    Custom(String),
    /// No `dataschema`
    None,
}

/// Determines the `id` header of a Cloud Event
#[derive(Clone, Debug, Default)]
#[non_exhaustive]
pub enum CloudEventId {
    /// A new version 4 UUID
    #[default]
    Automatic,
    /// The provided `id`
    Custom(String),
}

/// Determines the `time` header of a Cloud Event
#[derive(Clone, Debug, Default)]
#[non_exhaustive]
pub enum CloudEventTime {
    /// The timestamp of the data being sent, if it has one, otherwise the time it is sent
    #[default]
    Automatic,
    /// The provided `time`
    Custom(DateTime<Utc>),
    /// No `time`
    None,
}

/// Determines the `aiodeviceref` header of a Cloud Event
#[derive(Clone, Debug, Default)]
#[non_exhaustive]
pub enum CloudEventAioDeviceRef {
    /// `ms-aio:<device UUID>/<inbound endpoint name>`, or `ms-aio:<inbound endpoint name>` if the
    /// device doesn't have a UUID
    #[default]
    Automatic,
    /// The provided `aiodeviceref`
    Custom(String),
    /// No `aiodeviceref`
    None,
}

/// Determines the `aioassetref` header of a Cloud Event
#[derive(Clone, Debug, Default)]
#[non_exhaustive]
pub enum CloudEventAioAssetRef {
    /// `ms-aio:<asset UUID>`, or `ms-aio:` if the asset doesn't have a UUID
    #[default]
    Automatic,
    /// The provided `aioassetref`
    Custom(String),
    /// No `aioassetref`
    None,
}

/// Determines the Cloud Event headers of data forwarded with
/// [`DataOperationClient::forward_data_with_cloud_event`](crate::base_connector::managed_azure_device_registry::DataOperationClient::forward_data_with_cloud_event).
/// Values that aren't set are `Automatic`, and automatic values are determined when the data is
/// forwarded.
#[derive(Builder, Clone, Debug, Default)]
#[builder(build_fn(validate = "Self::validate"))]
pub struct DataOperationCloudEvent {
    /// Identifies the context in which the event happened. Defaults to
    /// [`CloudEventSource::Automatic`].
    #[builder(default)]
    pub(crate) source: CloudEventSource,
    /// Describes the type of event. Defaults to [`CloudEventType::Automatic`].
    #[builder(default)]
    pub(crate) event_type: CloudEventType,
    /// Identifies the subject of the event within the `source`. Defaults to
    /// [`CloudEventSubject::Automatic`].
    #[builder(default)]
    pub(crate) subject: CloudEventSubject,
    /// Identifies the schema that the data adheres to. Defaults to
    /// [`CloudEventDataSchema::Automatic`].
    #[builder(default)]
    pub(crate) data_schema: CloudEventDataSchema,
    /// Identifies the event. Defaults to [`CloudEventId::Automatic`].
    #[builder(default)]
    pub(crate) id: CloudEventId,
    /// Timestamp of when the occurrence happened. Defaults to
    /// [`CloudEventTime::Automatic`].
    #[builder(default)]
    pub(crate) time: CloudEventTime,
    /// AIO extension referencing the device and inbound endpoint. Defaults to
    /// [`CloudEventAioDeviceRef::Automatic`].
    #[builder(default)]
    pub(crate) aio_device_ref: CloudEventAioDeviceRef,
    /// AIO extension referencing the asset. Defaults to
    /// [`CloudEventAioAssetRef::Automatic`].
    #[builder(default)]
    pub(crate) aio_asset_ref: CloudEventAioAssetRef,
}

impl DataOperationCloudEventBuilder {
    fn validate(&self) -> Result<(), String> {
        let spec_version = aio_cloud_event::DEFAULT_CLOUD_EVENT_SPEC_VERSION;
        match &self.source {
            Some(CloudEventSource::AutomaticWithProtocolSpecificIdentifier(identifier)) => {
                if cloud_event_header_source_identifier(identifier).is_none() {
                    return Err(format!(
                        "Protocol specific identifier '{identifier}' does not form a valid source"
                    ));
                }
            }
            Some(CloudEventSource::Custom(source)) => {
                aio_cloud_event::CloudEventFields::Source.validate(source, spec_version)?;
            }
            Some(CloudEventSource::Automatic) | None => {}
        }
        if let Some(CloudEventType::Custom(event_type)) = &self.event_type {
            aio_cloud_event::CloudEventFields::EventType.validate(event_type, spec_version)?;
        }
        if let Some(CloudEventSubject::Custom(subject)) = &self.subject {
            aio_cloud_event::CloudEventFields::Subject.validate(subject, spec_version)?;
        }
        if let Some(CloudEventDataSchema::Custom(data_schema)) = &self.data_schema {
            aio_cloud_event::CloudEventFields::DataSchema.validate(data_schema, spec_version)?;
        }
        if let Some(CloudEventId::Custom(id)) = &self.id {
            aio_cloud_event::CloudEventFields::Id.validate(id, spec_version)?;
        }
        Ok(())
    }
}

/// Returns the trimmed identifier if it's non-empty and forms a valid `ms-aio:<identifier>` source
pub(crate) fn cloud_event_header_source_identifier(identifier: &str) -> Option<&str> {
    let trimmed_identifier = identifier.trim();
    (!trimmed_identifier.is_empty()
        && aio_cloud_event::CloudEventFields::Source
            .validate(
                &format!("ms-aio:{trimmed_identifier}"),
                aio_cloud_event::DEFAULT_CLOUD_EVENT_SPEC_VERSION,
            )
            .is_ok())
    .then_some(trimmed_identifier)
}

#[cfg(test)]
mod tests {

    use test_case::test_case;

    use super::*;

    #[test]
    fn cloud_event_builder_defaults() {
        let cloud_event = DataOperationCloudEventBuilder::default().build().unwrap();
        assert!(matches!(cloud_event.source, CloudEventSource::Automatic));
        assert!(matches!(cloud_event.event_type, CloudEventType::Automatic));
        assert!(matches!(cloud_event.subject, CloudEventSubject::Automatic));
        assert!(matches!(
            cloud_event.data_schema,
            CloudEventDataSchema::Automatic
        ));
        assert!(matches!(cloud_event.id, CloudEventId::Automatic));
        assert!(matches!(cloud_event.time, CloudEventTime::Automatic));
        assert!(matches!(
            cloud_event.aio_device_ref,
            CloudEventAioDeviceRef::Automatic
        ));
        assert!(matches!(
            cloud_event.aio_asset_ref,
            CloudEventAioAssetRef::Automatic
        ));
    }

    #[test_case(DataOperationCloudEventBuilder::default().source(CloudEventSource::AutomaticWithProtocolSpecificIdentifier(" protocol123 ".to_string())); "protocol_specific_identifier")]
    #[test_case(DataOperationCloudEventBuilder::default().source(CloudEventSource::Custom("aio://custom/source".to_string())); "custom_source")]
    #[test_case(DataOperationCloudEventBuilder::default().event_type(CloudEventType::Custom("custom.type".to_string())).id(CloudEventId::Custom("custom-id".to_string())); "custom_type_and_id")]
    #[test_case(DataOperationCloudEventBuilder::default().subject(CloudEventSubject::PublishTopic).data_schema(CloudEventDataSchema::Custom("aio://custom/schema".to_string())).time(CloudEventTime::Custom(Utc::now())); "custom_optional_values")]
    #[test_case(DataOperationCloudEventBuilder::default().subject(CloudEventSubject::None).data_schema(CloudEventDataSchema::None).time(CloudEventTime::None).aio_device_ref(CloudEventAioDeviceRef::None).aio_asset_ref(CloudEventAioAssetRef::None); "omitted_optional_values")]
    fn cloud_event_builder_valid(builder: &DataOperationCloudEventBuilder) {
        assert!(builder.build().is_ok());
    }

    #[test_case(DataOperationCloudEventBuilder::default().source(CloudEventSource::AutomaticWithProtocolSpecificIdentifier(String::new())); "empty_protocol_specific_identifier")]
    #[test_case(DataOperationCloudEventBuilder::default().source(CloudEventSource::AutomaticWithProtocolSpecificIdentifier(" ".to_string())); "whitespace_protocol_specific_identifier")]
    #[test_case(DataOperationCloudEventBuilder::default().source(CloudEventSource::AutomaticWithProtocolSpecificIdentifier("not valid ?!#$# url".to_string())); "invalid_protocol_specific_identifier")]
    #[test_case(DataOperationCloudEventBuilder::default().source(CloudEventSource::Custom("::::".to_string())); "invalid_custom_source")]
    #[test_case(DataOperationCloudEventBuilder::default().event_type(CloudEventType::Custom(String::new())); "empty_event_type")]
    #[test_case(DataOperationCloudEventBuilder::default().subject(CloudEventSubject::Custom(String::new())); "empty_subject")]
    #[test_case(DataOperationCloudEventBuilder::default().data_schema(CloudEventDataSchema::Custom("./relative".to_string())); "relative_data_schema")]
    #[test_case(DataOperationCloudEventBuilder::default().id(CloudEventId::Custom(String::new())); "empty_id")]
    fn cloud_event_builder_invalid(builder: &DataOperationCloudEventBuilder) {
        assert!(matches!(
            builder.build(),
            Err(DataOperationCloudEventBuilderError::ValidationError(_))
        ));
    }
}
