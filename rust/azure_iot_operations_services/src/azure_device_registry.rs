// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

//! Types for Azure Device Registry operations.
use crate::common::dispatcher::Receiver;
use adr_name_gen::adr_base_service::client::{
    Asset as GenAsset, AssetDataPointObservabilityModeSchema, AssetDataPointSchemaElementSchema,
    AssetDatasetSchemaElementSchema, AssetEndpointProfile as GenAssetEndpointProfile,
    AssetEndpointProfileSpecificationSchema as GenAssetEndpointProfileSpecificationSchema,
    AssetEndpointProfileStatus as GenAssetEndpointProfileStatus,
    AssetEndpointProfileUpdateEventTelemetry as GenAssetEndpointProfileUpdateEventTelemetry,
    AssetEventObservabilityModeSchema, AssetEventSchemaElementSchema,
    AssetSpecificationSchema as GenAssetSpecificationSchema, AssetStatus as GenAssetStatus,
    AssetUpdateEventTelemetry as GenAssetUpdateEventTelemetry,
    AuthenticationSchema as GenAuthenticationSchema, DatasetsSchemaSchemaElementSchema,
    DetectedAsset as GenDetectedAsset, DetectedAssetDataPointSchemaElementSchema,
    DetectedAssetDatasetSchemaElementSchema, DetectedAssetEventSchemaElementSchema,
    Error as GenError, EventsSchemaSchemaElementSchema,
    MessageSchemaReference as GenMessageSchemaReference, MethodSchema, RetainSchema,
    Topic as GenTopic, UsernamePasswordCredentialsSchema as GenUsernamePasswordCredentialsSchema,
    X509credentialsSchema as GenX509credentialsSchema,
};
use adr_type_gen::aep_type_service::client::{
    DiscoveredAssetEndpointProfile as GenDiscoveredAssetEndpointProfile,
    SupportedAuthenticationMethodsSchemaElementSchema,
};
use azure_iot_operations_mqtt::interface::AckToken;
use azure_iot_operations_protocol::common::aio_protocol_error::AIOProtocolError;
use core::fmt::Debug;
use std::collections::HashMap;
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
    /// An error occurred while shutting down the Azure Device Registry Client.
    #[error("Shutdown error occurred with the following protocol errors: {0:?}")]
    ShutdownError(Vec<AIOProtocolError>),
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

impl From<AssetEndpointProfileStatus> for GenAssetEndpointProfileStatus {
    fn from(source: AssetEndpointProfileStatus) -> Self {
        let errors = source
            .errors
            .unwrap_or_default()
            .into_iter()
            .map(AkriError::into)
            .collect();

        GenAssetEndpointProfileStatus {
            errors: Some(errors),
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

impl From<GenAssetStatus> for AssetStatus {
    fn from(source: GenAssetStatus) -> Self {
        let datasets_schema = source
            .datasets_schema
            .unwrap_or_default()
            .into_iter()
            .map(|schema_ref| SchemaReference {
                name: schema_ref.name,
                message_schema_reference: schema_ref
                    .message_schema_reference
                    .map(MessageSchemaReference::from),
            })
            .collect();
        let events_schema = source
            .events_schema
            .unwrap_or_default()
            .into_iter()
            .map(|schema_ref| SchemaReference {
                name: schema_ref.name,
                message_schema_reference: schema_ref
                    .message_schema_reference
                    .map(MessageSchemaReference::from),
            })
            .collect();
        let errors = source
            .errors
            .unwrap_or_default()
            .into_iter()
            .map(AkriError::from)
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
    /// Name of the asset if available.
    pub asset_name: Option<String>,

    /// Array of datasets that are part of the asset. Each dataset spec describes the datapoints that make up the set.
    pub datasets: Option<Vec<DetectedAssetDataSetSchema>>,

    /// Array of events that are part of the asset. Each event can reference an asset type capability and have per-event configuration.
    pub events: Option<Vec<DetectedAssetEventSchema>>,

    /// TODO Common schema for the asset, including its configuration and source.
    pub asset_schema: AssetSpecificationSchemaCommon,
}
impl From<DetectedAsset> for GenDetectedAsset {
    fn from(source: DetectedAsset) -> Self {
        GenDetectedAsset {
            asset_endpoint_profile_ref: source.asset_schema.asset_endpoint_profile_ref,
            asset_name: source.asset_name,
            datasets: source.datasets.map(|datasets| {
                datasets
                    .into_iter()
                    .map(DetectedAssetDataSetSchema::into)
                    .collect()
            }),
            default_datasets_configuration: source.asset_schema.default_datasets_configuration,
            default_events_configuration: source.asset_schema.default_events_configuration,
            default_topic: source.asset_schema.default_topic.map(Topic::into),
            documentation_uri: source.asset_schema.documentation_uri,
            events: source.events.map(|events| {
                events
                    .into_iter()
                    .map(DetectedAssetEventSchema::into)
                    .collect()
            }),
            hardware_revision: source.asset_schema.hardware_revision,
            manufacturer: source.asset_schema.manufacturer,
            manufacturer_uri: source.asset_schema.manufacturer_uri,
            model: source.asset_schema.model,
            product_code: source.asset_schema.product_code,
            serial_number: source.asset_schema.serial_number,
            software_revision: source.asset_schema.software_revision,
        }
    }
}

/// Represents a event schema for a detected asset.
pub struct DetectedAssetEventSchema {
    /// TODO Common schema for the event, including its configuration and source.
    pub event_schema: EventSchemaCommon,
    /// The 'lastUpdatedOn' Field.
    pub last_updated_on: Option<String>,
}

/// TODO Represents the common fields of the event schema.
pub struct EventSchemaCommon {
    /// The 'eventConfiguration' Field.
    pub event_configuration: Option<String>,

    /// The 'eventNotifier' Field.
    pub event_notifier: String,

    /// The 'name' Field.
    pub name: String,

    /// The 'topic' Field.
    pub topic: Option<Topic>,
}

impl From<DetectedAssetEventSchema> for DetectedAssetEventSchemaElementSchema {
    fn from(value: DetectedAssetEventSchema) -> Self {
        DetectedAssetEventSchemaElementSchema {
            event_configuration: value.event_schema.event_configuration,
            event_notifier: value.event_schema.event_notifier,
            name: value.event_schema.name,
            last_updated_on: value.last_updated_on,
            topic: value.event_schema.topic.map(Topic::into),
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
    /// TODO Common schema for the data point, including its configuration and source.
    pub data_point_schema: DataPointSchemaCommon,

    /// The 'lastUpdatedOn' Field.
    pub last_updated_on: Option<String>,
}

/// TODO Represents the common fields of the data point schema.
pub struct DataPointSchemaCommon {
    /// The 'dataPointConfiguration' Field.
    pub data_point_configuration: Option<String>,

    /// The 'dataSource' Field.
    pub data_source: String,

    /// The 'name' Field.
    pub name: Option<String>,
}

impl From<DetectedAssetDataPointSchema> for DetectedAssetDataPointSchemaElementSchema {
    fn from(source: DetectedAssetDataPointSchema) -> Self {
        DetectedAssetDataPointSchemaElementSchema {
            data_point_configuration: source.data_point_schema.data_point_configuration,
            data_source: source.data_point_schema.data_source,
            name: source.data_point_schema.name,
            last_updated_on: source.last_updated_on,
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

/// Represents telemetry data for an update event of an asset endpoint profile.
pub struct AssetEndpointProfileUpdateEventTelemetry {
    /// The 'assetEndpointProfile' Field.
    pub asset_endpoint_profile: AssetEndpointProfile,
}

impl From<GenAssetEndpointProfileUpdateEventTelemetry>
    for AssetEndpointProfileUpdateEventTelemetry
{
    fn from(source: GenAssetEndpointProfileUpdateEventTelemetry) -> Self {
        AssetEndpointProfileUpdateEventTelemetry {
            asset_endpoint_profile: AssetEndpointProfile::from(
                source
                    .asset_endpoint_profile_update_event
                    .asset_endpoint_profile,
            ),
        }
    }
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

/// Represents telemetry data for an asset update event.
pub struct AssetUpdateEventTelemetry {
    /// The 'asset' Field.
    pub asset: Asset,

    /// The 'assetName' Field.
    pub asset_name: String,
}

impl From<GenAssetUpdateEventTelemetry> for AssetUpdateEventTelemetry {
    fn from(source: GenAssetUpdateEventTelemetry) -> Self {
        AssetUpdateEventTelemetry {
            asset: Asset::from(source.asset_update_event.asset),
            asset_name: source.asset_update_event.asset_name,
        }
    }
}

// =================================== RESPONSE CLASSES ===================================
/// Represents an asset endpoint profile in the Azure Device Registry service.
pub struct AssetEndpointProfile {
    /// The 'name' Field.
    pub name: String,

    /// The 'specification' Field.
    pub specification: AssetEndpointProfileSpecificationSchema,

    /// The 'status' Field.
    pub status: Option<AssetEndpointProfileStatus>,
}

impl From<GenAssetEndpointProfile> for AssetEndpointProfile {
    fn from(source: GenAssetEndpointProfile) -> Self {
        AssetEndpointProfile {
            name: source.name,
            specification: AssetEndpointProfileSpecificationSchema::from(source.specification),
            status: source.status.map(AssetEndpointProfileStatus::from),
        }
    }
}

impl From<GenAssetEndpointProfileStatus> for AssetEndpointProfileStatus {
    fn from(source: GenAssetEndpointProfileStatus) -> Self {
        AssetEndpointProfileStatus {
            errors: source
                .errors
                .map(|errors| errors.into_iter().map(AkriError::from).collect()),
        }
    }
}

impl From<GenError> for AkriError {
    fn from(source: GenError) -> Self {
        AkriError {
            code: source.code,
            message: source.message,
        }
    }
}
/// Represents the specification schema for an asset endpoint profile in the Azure Device Registry service.
pub struct AssetEndpointProfileSpecificationSchema {
    /// The 'additionalConfiguration' Field.
    pub additional_configuration: Option<String>,

    /// The 'authentication' Field.
    pub authentication: Option<AuthenticationSchema>,

    /// The 'discoveredAssetEndpointProfileRef' Field.
    pub discovered_asset_endpoint_profile_ref: Option<String>,

    /// The 'endpointProfileType' Field.
    pub endpoint_profile_type: String,

    /// The 'targetAddress' Field.
    pub target_address: String,

    /// The 'uuid' Field.
    pub uuid: Option<String>,
}
impl From<GenAssetEndpointProfileSpecificationSchema> for AssetEndpointProfileSpecificationSchema {
    fn from(source: GenAssetEndpointProfileSpecificationSchema) -> Self {
        AssetEndpointProfileSpecificationSchema {
            additional_configuration: source.additional_configuration,
            authentication: source.authentication.map(AuthenticationSchema::from),
            discovered_asset_endpoint_profile_ref: source.discovered_asset_endpoint_profile_ref,
            endpoint_profile_type: source.endpoint_profile_type,
            target_address: source.target_address,
            uuid: source.uuid,
        }
    }
}

/// Represents the client authentication schema, including method and credentials.
pub struct AuthenticationSchema {
    /// The 'method' Field.
    pub method: AuthenticationMethodsSchema,

    /// The 'usernamePasswordCredentials' Field.
    pub username_password_credentials: Option<UsernamePasswordCredentialsSchema>,

    /// The 'x509Credentials' Field.
    pub x509credentials: Option<X509credentialsSchema>,
}

impl From<GenAuthenticationSchema> for AuthenticationSchema {
    fn from(source: GenAuthenticationSchema) -> Self {
        AuthenticationSchema {
            method: match source.method {
                MethodSchema::Anonymous => AuthenticationMethodsSchema::Anonymous,
                MethodSchema::Certificate => AuthenticationMethodsSchema::Certificate,
                MethodSchema::UsernamePassword => AuthenticationMethodsSchema::UsernamePassword,
            },
            username_password_credentials: source
                .username_password_credentials
                .map(UsernamePasswordCredentialsSchema::from),
            x509credentials: source.x509credentials.map(X509credentialsSchema::from),
        }
    }
}
/// Represents the credentials schema for username and password authentication.
pub struct UsernamePasswordCredentialsSchema {
    /// The 'passwordSecretName' Field.
    pub password_secret_name: String,

    /// The 'usernameSecretName' Field.
    pub username_secret_name: String,
}

impl From<GenUsernamePasswordCredentialsSchema> for UsernamePasswordCredentialsSchema {
    fn from(source: GenUsernamePasswordCredentialsSchema) -> Self {
        UsernamePasswordCredentialsSchema {
            password_secret_name: source.password_secret_name,
            username_secret_name: source.username_secret_name,
        }
    }
}
/// Represents the X.509 credentials schema for client authentication.
pub struct X509credentialsSchema {
    /// The 'certificateSecretName' Field.
    pub certificate_secret_name: String,
}

impl From<GenX509credentialsSchema> for X509credentialsSchema {
    fn from(source: GenX509credentialsSchema) -> Self {
        X509credentialsSchema {
            certificate_secret_name: source.certificate_secret_name,
        }
    }
}

/// Represents an asset in the Azure Device Registry service.
pub struct Asset {
    /// The 'name' Field.
    pub name: String,

    /// The 'specification' Field.
    pub specification: AssetSpecificationSchema,

    /// The status of the asset, including associated schemas and errors.
    pub status: Option<AssetStatus>,
}

impl From<GenAsset> for Asset {
    fn from(source: GenAsset) -> Self {
        Asset {
            name: source.name,
            specification: AssetSpecificationSchema::from(source.specification),
            status: source.status.map(AssetStatus::from),
        }
    }
}

/// Represents the specification schema for a client asset, including attributes, datasets, and other metadata.
pub struct AssetSpecificationSchema {
    /// TODO Should we take out common fields and put them in a common struct?
    pub asset_specification_schema_common: AssetSpecificationSchemaCommon,
    /// The 'attributes' Field.
    pub attributes: Option<HashMap<String, String>>,

    /// The 'datasets' Field.
    pub datasets: Option<Vec<AssetDatasetSchema>>,

    /// The 'description' Field.
    pub description: Option<String>,

    /// The 'discoveredAssetRefs' Field.
    pub discovered_asset_refs: Option<Vec<String>>,

    /// The 'displayName' Field.
    pub display_name: Option<String>,

    /// The 'enabled' Field.
    pub enabled: Option<bool>,

    /// The 'events' Field.
    pub events: Option<Vec<AssetEventSchema>>,

    /// The 'externalAssetId' Field.
    pub external_asset_id: Option<String>,

    /// The 'uuid' Field.
    pub uuid: Option<String>,

    /// The 'version' Field.
    pub version: Option<String>,
}

/// Represents the common fields of the asset specification schema.
pub struct AssetSpecificationSchemaCommon {
    /// The 'assetEndpointProfileRef' Field.
    pub asset_endpoint_profile_ref: String,

    /// The 'defaultDatasetsConfiguration' Field.
    pub default_datasets_configuration: Option<String>,

    /// The 'defaultEventsConfiguration' Field.
    pub default_events_configuration: Option<String>,

    /// The 'defaultTopic' Field.
    pub default_topic: Option<Topic>,

    /// The 'documentationUri' Field.
    pub documentation_uri: Option<String>,

    /// The 'hardwareRevision' Field.
    pub hardware_revision: Option<String>,

    /// The 'manufacturer' Field.
    pub manufacturer: Option<String>,

    /// The 'manufacturerUri' Field.
    pub manufacturer_uri: Option<String>,

    /// The 'model' Field.
    pub model: Option<String>,

    /// The 'productCode' Field.
    pub product_code: Option<String>,

    /// The 'serialNumber' Field.
    pub serial_number: Option<String>,

    /// The 'softwareRevision' Field.
    pub software_revision: Option<String>,
}

impl From<GenAssetSpecificationSchema> for AssetSpecificationSchema {
    fn from(source: GenAssetSpecificationSchema) -> Self {
        AssetSpecificationSchema {
            asset_specification_schema_common: AssetSpecificationSchemaCommon {
                asset_endpoint_profile_ref: source.asset_endpoint_profile_ref,
                default_datasets_configuration: source.default_datasets_configuration,
                default_events_configuration: source.default_events_configuration,
                default_topic: source.default_topic.map(Topic::from),
                documentation_uri: source.documentation_uri,
                hardware_revision: source.hardware_revision,
                manufacturer: source.manufacturer,
                manufacturer_uri: source.manufacturer_uri,
                model: source.model,
                product_code: source.product_code,
                serial_number: source.serial_number,
                software_revision: source.software_revision,
            },
            attributes: source.attributes,
            datasets: source
                .datasets
                .map(|datasets| datasets.into_iter().map(AssetDatasetSchema::from).collect()),
            description: source.description,
            discovered_asset_refs: source.discovered_asset_refs,
            display_name: source.display_name,
            enabled: source.enabled,
            events: source
                .events
                .map(|events| events.into_iter().map(AssetEventSchema::from).collect()),
            external_asset_id: source.external_asset_id,
            uuid: source.uuid,
            version: source.version,
        }
    }
}

/// The 'datasets' Field.
pub struct AssetDatasetSchema {
    /// The 'dataPoints' Field.
    pub data_points: Option<Vec<AssetDataPointSchema>>,

    /// The 'datasetConfiguration' Field.
    pub dataset_configuration: Option<String>,

    /// The 'name' Field.
    pub name: String,

    /// The 'topic' Field.
    pub topic: Option<Topic>,
}

impl From<AssetDatasetSchemaElementSchema> for AssetDatasetSchema {
    fn from(source: AssetDatasetSchemaElementSchema) -> Self {
        AssetDatasetSchema {
            data_points: source
                .data_points
                .map(|points| points.into_iter().map(AssetDataPointSchema::from).collect()),
            dataset_configuration: source.dataset_configuration,
            name: source.name,
            topic: source.topic.map(Topic::from),
        }
    }
}

/// Represents the schema for an asset data point, including its configuration and observability mode.
pub struct AssetDataPointSchema {
    /// The 'dataPointSchema' Field.
    pub data_point_schema: DataPointSchemaCommon,
    /// The 'observabilityMode' Field.
    pub observability_mode: Option<DataPointObservabilityMode>,
}

impl From<AssetDataPointSchemaElementSchema> for AssetDataPointSchema {
    fn from(source: AssetDataPointSchemaElementSchema) -> Self {
        AssetDataPointSchema {
            data_point_schema: DataPointSchemaCommon {
                data_point_configuration: source.data_point_configuration,
                data_source: source.data_source,
                name: Some(source.name),
            },
            observability_mode: source
                .observability_mode
                .map(DataPointObservabilityMode::from),
        }
    }
}
/// Represents the schema for an asset event, including its configuration and observability mode.
pub struct AssetEventSchema {
    /// The 'eventSchema' Field.
    pub event_schema: EventSchemaCommon,

    /// The 'observabilityMode' Field.
    pub observability_mode: Option<EventObservabilityMode>,
}

impl From<AssetEventSchemaElementSchema> for AssetEventSchema {
    fn from(source: AssetEventSchemaElementSchema) -> Self {
        AssetEventSchema {
            event_schema: EventSchemaCommon {
                event_configuration: source.event_configuration,
                event_notifier: source.event_notifier,
                name: source.name,
                topic: source.topic.map(Topic::from),
            },
            observability_mode: source.observability_mode.map(EventObservabilityMode::from),
        }
    }
}
/// Represents the observability mode for data point.
pub enum DataPointObservabilityMode {
    /// Represents the counter observability mode.
    Counter,
    /// Represents the gauge observability mode.
    Gauge,
    /// Represents the histogram observability mode.
    Histogram,
    /// Represents the log observability mode.
    Log,
    /// Represents the none observability mode.
    None,
}

/// Represents the observability mode for an event.
pub enum EventObservabilityMode {
    /// Represents the log observability mode.
    Log,
    /// Represents the none observability mode.
    None,
}

impl From<AssetDataPointObservabilityModeSchema> for DataPointObservabilityMode {
    fn from(source: AssetDataPointObservabilityModeSchema) -> Self {
        match source {
            AssetDataPointObservabilityModeSchema::Counter => DataPointObservabilityMode::Counter,
            AssetDataPointObservabilityModeSchema::Gauge => DataPointObservabilityMode::Gauge,
            AssetDataPointObservabilityModeSchema::Histogram => {
                DataPointObservabilityMode::Histogram
            }
            AssetDataPointObservabilityModeSchema::Log => DataPointObservabilityMode::Log,
            AssetDataPointObservabilityModeSchema::None => DataPointObservabilityMode::None,
        }
    }
}

impl From<AssetEventObservabilityModeSchema> for EventObservabilityMode {
    fn from(source: AssetEventObservabilityModeSchema) -> Self {
        match source {
            AssetEventObservabilityModeSchema::Log => EventObservabilityMode::Log,
            AssetEventObservabilityModeSchema::None => EventObservabilityMode::None,
        }
    }
}

impl From<GenTopic> for Topic {
    fn from(source: GenTopic) -> Self {
        Topic {
            path: (source.path),
            retain: match source.retain {
                Some(RetainSchema::Keep) => Some(RetainPolicy::Keep),
                Some(RetainSchema::Never) => Some(RetainPolicy::Never),
                None => None,
            },
        }
    }
}

impl From<GenMessageSchemaReference> for MessageSchemaReference {
    fn from(value: GenMessageSchemaReference) -> Self {
        MessageSchemaReference {
            message_schema_name: value.schema_name,
            message_schema_version: value.schema_version,
            message_schema_namespace: value.schema_namespace,
        }
    }
}
