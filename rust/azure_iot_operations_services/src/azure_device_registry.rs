// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

//! Types for Azure Device Registry operations.
use crate::common::dispatcher::Receiver;
use adr_name_gen::adr_base_service::client::{
    AssetEndpointProfileStatus as GenAssetEndpointProfileStatus,
    AssetEndpointProfileUpdateEventTelemetry, AssetStatus as GenAssetStatus,
    AssetUpdateEventTelemetry, DatasetsSchemaSchemaElementSchema,
    DetectedAsset as GenDetectedAsset, DetectedAssetDataPointSchemaElementSchema,
    DetectedAssetDatasetSchemaElementSchema, DetectedAssetEventSchemaElementSchema,
    Error as GenError, EventsSchemaSchemaElementSchema,
    MessageSchemaReference as GenMessageSchemaReference, Topic as GenTopic,
    UpdateAssetEndpointProfileStatusRequestPayload,
};
use adr_type_gen::aep_type_service::client::{
    DiscoveredAssetEndpointProfile as GenDiscoveredAssetEndpointProfile,
    SupportedAuthenticationMethodsSchemaElementSchema,
};
use azure_iot_operations_mqtt::interface::AckToken;
use azure_iot_operations_protocol::common::aio_protocol_error::AIOProtocolError;
use core::fmt::Debug;
use thiserror::Error;

/// Azure Device Registry generated code
mod adr_name_gen;
mod adr_type_gen;
/// Azure Device Registry Client implementation wrapper
mod client;

pub use client::Client;

/// Represents an error that occurred in the Azure Device Registry Client implementation.
#[derive(Debug, Error)]
#[error(transparent)]
pub struct Error(#[from] ErrorKind);

impl Error {
    /// Returns the [`ErrorKind`] of the error.
    #[must_use]
    pub fn kind(&self) -> &ErrorKind {
        &self.0
    }
}

/// Represents the kinds of errors that occur in the Azure Device Registry implementation.
#[derive(Error, Debug)]
#[allow(clippy::large_enum_variant)]
pub enum ErrorKind {
    /// An error occurred in the AIO Protocol. See [`AIOProtocolError`] for more information.
    #[error(transparent)]
    AIOProtocolError(AIOProtocolError),
    /// An error occurred during serialization of a request.
    #[error("{0}")]
    SerializationError(String),
    /// An argument provided for a request was invalid.
    #[error("{0}")]
    InvalidArgument(String),
    /// An error was returned by the Azure Device Registry Service.
    #[error("{0:?}")]
    ServiceError(ServiceError),
    /// A aep or an asset may only have one observation at a time.
    #[error("Aep or asset may only be observed once at a time")]
    DuplicateObserve,
    /// A aep or an asset had an error during observation.
    #[error("{0}")]
    ObservationError(String),
}

/// An error returned by the Azure Device Registry Service.
/// // TODO Ask service team about what tsrcuture of service errors ? And redefine the struct based on that.
#[derive(Debug)]
pub struct ServiceError {
    /// The error message.
    pub message: String,
    /// The name of the property associated with the error, if present.
    pub property_name: Option<String>,
    /// The value of the property associated with the error, if present.
    pub property_value: Option<String>,
}

#[derive(Clone, Debug, Default)]
/// Represents a request to update the status of an asset endpoint profile in the ADR Service.
pub struct AssetEndpointProfileStatus {
    /// A collection of errors associated with the asset endpiint profile status request.
    pub errors: Option<Vec<AkriError>>,
}

impl From<AssetEndpointProfileStatus> for UpdateAssetEndpointProfileStatusRequestPayload {
    fn from(source: AssetEndpointProfileStatus) -> Self {
        let errors = source
            .errors
            .unwrap_or_default()
            .into_iter()
            .map(AkriError::into)
            .collect();

        UpdateAssetEndpointProfileStatusRequestPayload {
            asset_endpoint_profile_status_update: GenAssetEndpointProfileStatus {
                errors: Some(errors),
            },
        }
    }
}

#[derive(Clone, Debug, Default)]
/// Represents a request to update the status of an asset, including associated schemas and errors.
pub struct AssetStatus {
    /// A collection of schema references for datasets associated with the asset.
    pub datasets_schema: Option<Vec<SchemaReference>>,
    /// A collection of schema references for events associated with the asset.
    pub events_schema: Option<Vec<SchemaReference>>,
    /// A collection of errors associated with the asset status request.
    pub errors: Option<Vec<AkriError>>,
    /// The version of the asset status request.
    pub version: Option<i32>,
}

impl From<AssetStatus> for GenAssetStatus {
    fn from(source: AssetStatus) -> Self {
        let datasets_schema = source
            .datasets_schema
            .unwrap_or_default()
            .into_iter()
            .map(|schema_ref| DatasetsSchemaSchemaElementSchema {
                name: schema_ref.name,
                message_schema_reference: schema_ref
                    .message_schema_reference
                    .map(MessageSchemaReference::into),
            })
            .collect();
        let events_schema = source
            .events_schema
            .unwrap_or_default()
            .into_iter()
            .map(|schema_ref| EventsSchemaSchemaElementSchema {
                name: schema_ref.name,
                message_schema_reference: schema_ref
                    .message_schema_reference
                    .map(MessageSchemaReference::into),
            })
            .collect();
        let errors = source
            .errors
            .unwrap_or_default()
            .into_iter()
            .map(AkriError::into)
            .collect();

        GenAssetStatus {
            datasets_schema: Some(datasets_schema),
            events_schema: Some(events_schema),
            errors: Some(errors),
            version: Some(source.version.unwrap_or_default()),
        }
    }
}

#[derive(Clone, Debug)]
/// Represents a reference to the dataset or event schema.
pub struct SchemaReference {
    /// The name of the dataset or the event.
    pub name: String,
    /// The 'messageSchemaReference' Field.
    pub message_schema_reference: Option<MessageSchemaReference>,
}

#[derive(Clone, Debug)]
/// Represents a reference to a schema, including its name, version, and namespace.
pub struct MessageSchemaReference {
    /// The name of the message schema.
    pub message_schema_name: String,
    /// The version of the message schema.
    pub message_schema_version: String,
    /// The namespace of the message schema.
    pub message_schema_namespace: String,
}

impl From<MessageSchemaReference> for GenMessageSchemaReference {
    fn from(value: MessageSchemaReference) -> Self {
        GenMessageSchemaReference {
            schema_name: value.message_schema_name,
            schema_namespace: value.message_schema_namespace,
            schema_version: value.message_schema_version,
        }
    }
}

#[derive(Clone, Debug)]
/// Represents an error in the ADR service, including a code and a message.
pub struct AkriError {
    /// The error code.
    pub code: Option<i32>,
    /// The error message.
    pub message: Option<String>,
}

impl From<AkriError> for GenError {
    fn from(value: AkriError) -> Self {
        GenError {
            code: value.code,
            message: value.message,
        }
    }
}

/// Represents a request to create a detected asset in the ADR service.
pub struct DetectedAsset {
    /// A reference to the asset endpoint profile.
    pub asset_endpoint_profile_ref: String,

    /// Name of the asset if available.
    pub asset_name: Option<String>,

    /// Array of datasets that are part of the asset. Each dataset spec describes the datapoints that make up the set.
    pub datasets: Option<Vec<DetectedAssetDataSetSchema>>,

    /// The 'defaultDatasetsConfiguration' Field.
    pub default_datasets_configuration: Option<String>,

    /// The 'defaultEventsConfiguration' Field.
    pub default_events_configuration: Option<String>,

    /// The 'defaultTopic' Field.
    pub default_topic: Option<Topic>,

    /// URI to the documentation of the asset.
    pub documentation_uri: Option<String>,

    /// Array of events that are part of the asset. Each event can reference an asset type capability and have per-event configuration.
    pub events: Option<Vec<DetectedAssetEventSchema>>,

    /// The 'hardwareRevision' Field.
    pub hardware_revision: Option<String>,

    /// Asset manufacturer name.
    pub manufacturer: Option<String>,

    /// URI to the manufacturer of the asset.
    pub manufacturer_uri: Option<String>,

    /// Asset model name.
    pub model: Option<String>,

    /// Asset product code.
    pub product_code: Option<String>,

    /// Asset serial number.
    pub serial_number: Option<String>,

    /// Revision number of the software.
    pub software_revision: Option<String>,
}
impl From<DetectedAsset> for GenDetectedAsset {
    fn from(source: DetectedAsset) -> Self {
        GenDetectedAsset {
            asset_endpoint_profile_ref: source.asset_endpoint_profile_ref,
            asset_name: source.asset_name,
            datasets: source.datasets.map(|datasets| {
                datasets
                    .into_iter()
                    .map(DetectedAssetDataSetSchema::into)
                    .collect()
            }),
            default_datasets_configuration: source.default_datasets_configuration,
            default_events_configuration: source.default_events_configuration,
            default_topic: source.default_topic.map(Topic::into),
            documentation_uri: source.documentation_uri,
            events: source.events.map(|events| {
                events
                    .into_iter()
                    .map(DetectedAssetEventSchema::into)
                    .collect()
            }),
            hardware_revision: source.hardware_revision,
            manufacturer: source.manufacturer,
            manufacturer_uri: source.manufacturer_uri,
            model: source.model,
            product_code: source.product_code,
            serial_number: source.serial_number,
            software_revision: source.software_revision,
        }
    }
}

/// Represents a event schema for a detected asset.
pub struct DetectedAssetEventSchema {
    /// The 'eventConfiguration' Field.
    pub event_configuration: Option<String>,

    /// The 'eventNotifier' Field.
    pub event_notifier: String,

    /// The 'lastUpdatedOn' Field.
    pub last_updated_on: Option<String>,

    /// The 'name' Field.
    pub name: String,

    /// The 'topic' Field.
    pub topic: Option<Topic>,
}

impl From<DetectedAssetEventSchema> for DetectedAssetEventSchemaElementSchema {
    fn from(value: DetectedAssetEventSchema) -> Self {
        DetectedAssetEventSchemaElementSchema {
            event_configuration: value.event_configuration,
            event_notifier: value.event_notifier,
            last_updated_on: value.last_updated_on,
            name: value.name,
            topic: value.topic.map(Topic::into),
        }
    }
}

/// Represents a data set schema for a detected asset.
pub struct DetectedAssetDataSetSchema {
    /// The 'dataPoints' Field.
    pub data_points: Option<Vec<DetectedAssetDataPointSchema>>,

    /// The 'dataSetConfiguration' Field.
    pub data_set_configuration: Option<String>,

    /// The 'name' Field.
    pub name: String,

    /// The 'topic' Field.
    pub topic: Option<Topic>,
}

impl From<DetectedAssetDataSetSchema> for DetectedAssetDatasetSchemaElementSchema {
    fn from(source: DetectedAssetDataSetSchema) -> Self {
        DetectedAssetDatasetSchemaElementSchema {
            data_points: source.data_points.map(|points| {
                points
                    .into_iter()
                    .map(DetectedAssetDataPointSchema::into)
                    .collect()
            }),
            data_set_configuration: source.data_set_configuration,
            name: source.name,
            topic: source.topic.map(Topic::into),
        }
    }
}

/// Represents a data point schema for a detected asset.
pub struct DetectedAssetDataPointSchema {
    /// The 'dataPointConfiguration' Field.
    pub data_point_configuration: Option<String>,

    /// The 'dataSource' Field.
    pub data_source: String,

    /// The 'lastUpdatedOn' Field.
    pub last_updated_on: Option<String>,

    /// The 'name' Field.
    pub name: Option<String>,
}

impl From<DetectedAssetDataPointSchema> for DetectedAssetDataPointSchemaElementSchema {
    fn from(source: DetectedAssetDataPointSchema) -> Self {
        DetectedAssetDataPointSchemaElementSchema {
            data_point_configuration: source.data_point_configuration,
            data_source: source.data_source,
            last_updated_on: source.last_updated_on,
            name: source.name,
        }
    }
}
/// Represents a topic
pub struct Topic {
    /// The 'path' Field.
    pub path: String,

    /// The 'retain' Field.
    pub retain: Option<RetainPolicy>,
}

#[derive(Debug, Clone, PartialEq)]
/// Represents the retain policy for a topic.
pub enum RetainPolicy {
    /// Retain the messages in the topic.
    Keep,
    /// Do not retain the messages in the topic.
    Never,
}

impl From<Topic> for GenTopic {
    fn from(source: Topic) -> Self {
        GenTopic {
            path: source.path,
            retain: match source.retain {
                Some(RetainPolicy::Keep) => {
                    Some(adr_name_gen::adr_base_service::client::RetainSchema::Keep)
                }
                Some(RetainPolicy::Never) => {
                    Some(adr_name_gen::adr_base_service::client::RetainSchema::Never)
                }
                None => None,
            },
        }
    }
}

/// Represents a request to create a discovered asset endpoint profile in the Azure Device Registry service.
pub struct DiscoveredAssetEndpointProfile {
    /// A unique identifier for a discovered asset.
    pub additional_configuration: Option<String>,

    /// Name of the discovered asset endpoint profile. If not provided it will get generated by Akri.
    pub daep_name: Option<String>,

    /// Defines the configuration for the connector type that is being used with the endpoint profile.
    pub endpoint_profile_type: String,

    /// list of supported authentication methods
    pub supported_authentication_methods: Option<Vec<AuthenticationMethodsSchema>>,

    /// local valid URI specifying the network address/dns name of southbound service.
    pub target_address: String,
}

/// Represents the supported authentication methods for a discovered asset endpoint profile.
pub enum AuthenticationMethodsSchema {
    /// Represents an anonymous authentication method.
    Anonymous,
    /// Represents certificate authentication method.
    Certificate,
    /// Represents an username pwd authentication method.
    UsernamePassword,
}

impl From<DiscoveredAssetEndpointProfile> for GenDiscoveredAssetEndpointProfile {
    fn from(source: DiscoveredAssetEndpointProfile) -> Self {
        let supported_authentication_methods = source
            .supported_authentication_methods
            .unwrap_or_default()
            .into_iter()
            .map(|method| match method {
                AuthenticationMethodsSchema::Anonymous => {
                    SupportedAuthenticationMethodsSchemaElementSchema::Anonymous
                }
                AuthenticationMethodsSchema::Certificate => {
                    SupportedAuthenticationMethodsSchemaElementSchema::Certificate
                }
                AuthenticationMethodsSchema::UsernamePassword => {
                    SupportedAuthenticationMethodsSchemaElementSchema::UsernamePassword
                }
            })
            .collect();
        GenDiscoveredAssetEndpointProfile {
            additional_configuration: source.additional_configuration,
            daep_name: source.daep_name,
            endpoint_profile_type: source.endpoint_profile_type,
            supported_authentication_methods: Some(supported_authentication_methods),
            target_address: source.target_address,
        }
    }
}

/// A struct to manage receiving notifications for a key
#[derive(Debug)]
pub struct AssetEndpointProfileObservation {
    /// The name of the asset endpoint profile (for convenience)
    pub name: String,
    /// The internal channel for receiving update telemetry for this aep
    receiver: Receiver<(AssetEndpointProfileUpdateEventTelemetry, Option<AckToken>)>,
}

impl AssetEndpointProfileObservation {
    /// Receives a [`AssetEndpointProfileUpdateEventTelemetry`] or [`None`] if there will be no more notifications.
    ///
    /// If there are notifications:
    /// - Returns Some([`AssetEndpointProfileUpdateEventTelemetry`], [`Option<AckToken>`]) on success
    ///     - If auto ack is disabled, the [`AckToken`] should be used or dropped when you want the ack to occur. If auto ack is enabled, you may use ([`AssetEndpointProfileUpdateEventTelemetry`], _) to ignore the [`AckToken`].
    ///
    /// A received telemetry can be acknowledged via the [`AckToken`] by calling [`AckToken::ack`] or dropping the [`AckToken`].
    pub async fn recv_notification(
        &mut self,
    ) -> Option<(AssetEndpointProfileUpdateEventTelemetry, Option<AckToken>)> {
        self.receiver.recv().await
    }
    // on drop, don't remove from hashmap so we can differentiate between a aep
    // that was observed where the receiver was dropped and a aep that was never observed
}

/// A struct to manage receiving notifications for a key
#[derive(Debug)]
pub struct AssetObservation {
    /// The name of the asset (for convenience)
    pub name: String,
    /// The internal channel for receiving update telemetry for this asset
    receiver: Receiver<(AssetUpdateEventTelemetry, Option<AckToken>)>,
}

impl AssetObservation {
    /// Receives a [`AssetUpdateEventTelemetry`] or [`None`] if there will be no more notifications.
    ///
    /// If there are notifications:
    /// - Returns Some([`AssetUpdateEventTelemetry`], [`Option<AckToken>`]) on success
    ///     - If auto ack is disabled, the [`AckToken`] should be used or dropped when you want the ack to occur. If auto ack is enabled, you may use ([`AssetEndpointProfileUpdateEventTelemetry`], _) to ignore the [`AckToken`].
    ///
    /// A received telemetry can be acknowledged via the [`AckToken`] by calling [`AckToken::ack`] or dropping the [`AckToken`].
    pub async fn recv_notification(
        &mut self,
    ) -> Option<(AssetUpdateEventTelemetry, Option<AckToken>)> {
        self.receiver.recv().await
    }
    // on drop, don't remove from hashmap so we can differentiate between a aep
    // that was observed where the receiver was dropped and a aep that was never observed
}
