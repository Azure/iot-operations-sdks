// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

//! Types for Azure Device Registry operations.
use adr_name_gen::adr_base_service::client::{
    AssetEndpointProfileStatus, AssetStatus, CreateDetectedAssetRequestPayload,
    DatasetsSchemaSchemaElementSchema, DetectedAsset, DetectedAssetDataPointSchemaElementSchema,
    DetectedAssetDatasetSchemaElementSchema, DetectedAssetEventSchemaElementSchema,
    Error as AzureDeviceRegistryServiceError, EventsSchemaSchemaElementSchema,
    MessageSchemaReference, Topic, UpdateAssetEndpointProfileStatusRequestPayload,
    UpdateAssetStatusRequestPayload, UpdateAssetStatusRequestSchema,
};
use adr_type_gen::aep_type_service::client::{
    CreateDiscoveredAssetEndpointProfileRequestPayload, DiscoveredAssetEndpointProfile,
    SupportedAuthenticationMethodsSchemaElementSchema,
};
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
pub struct UpdateAssetEndpointProfileStatusReq {
    /// A collection of errors associated with the asset endpiint profile status request.
    pub errors: Option<Vec<AkriError>>,
}

impl From<UpdateAssetEndpointProfileStatusReq> for UpdateAssetEndpointProfileStatusRequestPayload {
    fn from(source: UpdateAssetEndpointProfileStatusReq) -> Self {
        let errors = source
            .errors
            .unwrap_or_default()
            .into_iter()
            .map(AkriError::into)
            .collect();

        UpdateAssetEndpointProfileStatusRequestPayload {
            asset_endpoint_profile_status_update: AssetEndpointProfileStatus {
                errors: Some(errors),
            },
        }
    }
}

/// Request to update the status of an asset in the ADR Service.
#[derive(Clone, Debug, Default)]
pub struct UpdateAssetStatusReq {
    /// The name of the asset whose status is being updated.
    pub name: String,
    /// The status of the asset to be updated.
    pub status: AssetStatusReq,
}

impl From<UpdateAssetStatusReq> for UpdateAssetStatusRequestPayload {
    fn from(source: UpdateAssetStatusReq) -> Self {
        let asset_status_update = UpdateAssetStatusRequestSchema {
            asset_name: source.name,
            asset_status: source.status.into(),
        };

        UpdateAssetStatusRequestPayload {
            asset_status_update,
        }
    }
}

#[derive(Clone, Debug, Default)]
/// Represents a request to update the status of an asset, including associated schemas and errors.
pub struct AssetStatusReq {
    /// A collection of schema references for datasets associated with the asset.
    pub datasets_schema: Option<Vec<SchemaReferenceReq>>,
    /// A collection of schema references for events associated with the asset.
    pub events_schema: Option<Vec<SchemaReferenceReq>>,
    /// A collection of errors associated with the asset status request.
    pub errors: Option<Vec<AkriError>>,
    /// The version of the asset status request.
    pub version: Option<i32>,
}

impl From<AssetStatusReq> for AssetStatus {
    fn from(source: AssetStatusReq) -> Self {
        let datasets_schema = source
            .datasets_schema
            .unwrap_or_default()
            .into_iter()
            .map(|schema_ref| DatasetsSchemaSchemaElementSchema {
                name: schema_ref.name,
                message_schema_reference: schema_ref
                    .message_schema_reference
                    .map(MessageSchemaReferenceReq::into),
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
                    .map(MessageSchemaReferenceReq::into),
            })
            .collect();
        let errors = source
            .errors
            .unwrap_or_default()
            .into_iter()
            .map(AkriError::into)
            .collect();

        AssetStatus {
            datasets_schema: Some(datasets_schema),
            events_schema: Some(events_schema),
            errors: Some(errors),
            version: Some(source.version.unwrap_or_default()),
        }
    }
}

#[derive(Clone, Debug)]
/// Represents a reference to the dataset or event schema.
pub struct SchemaReferenceReq {
    /// The name of the dataset or the event.
    pub name: String,
    /// The 'messageSchemaReference' Field.
    pub message_schema_reference: Option<MessageSchemaReferenceReq>,
}

#[derive(Clone, Debug)]
/// Represents a reference to a schema, including its name, version, and namespace.
pub struct MessageSchemaReferenceReq {
    /// The name of the message schema.
    pub message_schema_name: String,
    /// The version of the message schema.
    pub message_schema_version: String,
    /// The namespace of the message schema.
    pub message_schema_namespace: String,
}

impl From<MessageSchemaReferenceReq> for MessageSchemaReference {
    fn from(value: MessageSchemaReferenceReq) -> Self {
        MessageSchemaReference {
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
    pub code: i32,
    /// The error message.
    pub message: String,
}

impl From<AkriError> for AzureDeviceRegistryServiceError {
    fn from(value: AkriError) -> Self {
        AzureDeviceRegistryServiceError {
            code: Some(value.code),
            message: Some(value.message),
        }
    }
}

/// Represents a request to create a detected asset in the ADR service.
pub struct CreateDetectedAssetReq {
    /// A reference to the asset endpoint profile.
    pub asset_endpoint_profile_ref: String,

    /// Name of the asset if available.
    pub asset_name: Option<String>,

    /// Array of datasets that are part of the asset. Each dataset spec describes the datapoints that make up the set.
    pub datasets: Option<Vec<DetectedAssetDataSetSchemaReq>>,

    /// The 'defaultDatasetsConfiguration' Field.
    pub default_datasets_configuration: Option<String>,

    /// The 'defaultEventsConfiguration' Field.
    pub default_events_configuration: Option<String>,

    /// The 'defaultTopic' Field.
    pub default_topic: Option<TopicReq>,

    /// URI to the documentation of the asset.
    pub documentation_uri: Option<String>,

    /// Array of events that are part of the asset. Each event can reference an asset type capability and have per-event configuration.
    pub events: Option<Vec<DetectedAssetEventSchemaReq>>,

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

impl From<CreateDetectedAssetReq> for CreateDetectedAssetRequestPayload {
    fn from(source: CreateDetectedAssetReq) -> Self {
        CreateDetectedAssetRequestPayload {
            detected_asset: DetectedAsset {
                asset_endpoint_profile_ref: source.asset_endpoint_profile_ref,
                asset_name: source.asset_name,
                datasets: source.datasets.map(|datasets| {
                    datasets
                        .into_iter()
                        .map(DetectedAssetDataSetSchemaReq::into)
                        .collect()
                }),
                default_datasets_configuration: source.default_datasets_configuration,
                default_events_configuration: source.default_events_configuration,
                default_topic: source.default_topic.map(TopicReq::into),
                documentation_uri: source.documentation_uri,
                events: source.events.map(|events| {
                    events
                        .into_iter()
                        .map(DetectedAssetEventSchemaReq::into)
                        .collect()
                }),
                hardware_revision: source.hardware_revision,
                manufacturer: source.manufacturer,
                manufacturer_uri: source.manufacturer_uri,
                model: source.model,
                product_code: source.product_code,
                serial_number: source.serial_number,
                software_revision: source.software_revision,
            },
        }
    }
}
/// Represents a event schema for a detected asset.
pub struct DetectedAssetEventSchemaReq {
    /// The 'eventConfiguration' Field.
    pub event_configuration: Option<String>,

    /// The 'eventNotifier' Field.
    pub event_notifier: String,

    /// The 'lastUpdatedOn' Field.
    pub last_updated_on: Option<String>,

    /// The 'name' Field.
    pub name: String,

    /// The 'topic' Field.
    pub topic: Option<TopicReq>,
}

impl From<DetectedAssetEventSchemaReq> for DetectedAssetEventSchemaElementSchema {
    fn from(value: DetectedAssetEventSchemaReq) -> Self {
        DetectedAssetEventSchemaElementSchema {
            event_configuration: value.event_configuration,
            event_notifier: value.event_notifier,
            last_updated_on: value.last_updated_on,
            name: value.name,
            topic: value.topic.map(TopicReq::into),
        }
    }
}

/// Represents a data set schema for a detected asset.
pub struct DetectedAssetDataSetSchemaReq {
    /// The 'dataPoints' Field.
    pub data_points: Option<Vec<DetectedAssetDataPointSchemaReq>>,

    /// The 'dataSetConfiguration' Field.
    pub data_set_configuration: Option<String>,

    /// The 'name' Field.
    pub name: String,

    /// The 'topic' Field.
    pub topic: Option<TopicReq>,
}

impl From<DetectedAssetDataSetSchemaReq> for DetectedAssetDatasetSchemaElementSchema {
    fn from(source: DetectedAssetDataSetSchemaReq) -> Self {
        DetectedAssetDatasetSchemaElementSchema {
            data_points: source.data_points.map(|points| {
                points
                    .into_iter()
                    .map(DetectedAssetDataPointSchemaReq::into)
                    .collect()
            }),
            data_set_configuration: source.data_set_configuration,
            name: source.name,
            topic: source.topic.map(TopicReq::into),
        }
    }
}

/// Represents a data point schema for a detected asset.
pub struct DetectedAssetDataPointSchemaReq {
    /// The 'dataPointConfiguration' Field.
    pub data_point_configuration: Option<String>,

    /// The 'dataSource' Field.
    pub data_source: String,

    /// The 'lastUpdatedOn' Field.
    pub last_updated_on: Option<String>,

    /// The 'name' Field.
    pub name: Option<String>,
}

impl From<DetectedAssetDataPointSchemaReq> for DetectedAssetDataPointSchemaElementSchema {
    fn from(source: DetectedAssetDataPointSchemaReq) -> Self {
        DetectedAssetDataPointSchemaElementSchema {
            data_point_configuration: source.data_point_configuration,
            data_source: source.data_source,
            last_updated_on: source.last_updated_on,
            name: source.name,
        }
    }
}
/// Represents a topic
pub struct TopicReq {
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

impl From<TopicReq> for Topic {
    fn from(source: TopicReq) -> Self {
        Topic {
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
pub struct CreateDiscoveredAssetEndpointProfileReq {
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

impl From<CreateDiscoveredAssetEndpointProfileReq>
    for CreateDiscoveredAssetEndpointProfileRequestPayload
{
    fn from(source: CreateDiscoveredAssetEndpointProfileReq) -> Self {
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

        CreateDiscoveredAssetEndpointProfileRequestPayload {
            discovered_asset_endpoint_profile: DiscoveredAssetEndpointProfile {
                additional_configuration: source.additional_configuration,
                daep_name: source.daep_name,
                endpoint_profile_type: source.endpoint_profile_type,
                supported_authentication_methods: Some(supported_authentication_methods),
                target_address: source.target_address,
            },
        }
    }
}
