// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

//! Types for Azure Device Registry operations.

use core::fmt::Debug;
use std::collections::HashMap;

use azure_iot_operations_mqtt::interface::AckToken;
use azure_iot_operations_protocol::common::aio_protocol_error::AIOProtocolError;
use azure_iot_operations_protocol::rpc_command;
use thiserror::Error;

use crate::azure_device_registry::adr_name_gen::adr_base_service::client as adr_base_service_gen;
use crate::azure_device_registry::adr_type_gen::aep_type_service::client as aep_type_service_gen;
use crate::common::dispatcher::Receiver;

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

/// Represents the kinds of errors that occur in the Azure Device Registry Client implementation.
#[derive(Error, Debug)]
#[allow(clippy::large_enum_variant)]
pub enum ErrorKind {
    /// An error occurred in the AIO Protocol. See [`AIOProtocolError`] for more information.
    #[error(transparent)]
    AIOProtocolError(#[from] AIOProtocolError),
    /// An error occurred during serialization of a request.
    #[error("{0}")]
    SerializationError(String),
    /// An argument provided for a request was invalid.
    #[error("{0}")]
    InvalidArgument(#[from] rpc_command::invoker::RequestBuilderError),
    /// An error was returned by the Azure Device Registry Service.
    #[error("{0:?}")]
    ServiceError(ServiceError),
    /// A aep or an asset may only have one observation at a time.
    #[error("Aep or asset may only be observed once at a time")]
    DuplicateObserve,
    /// A aep or an asset had an error during observation.
    #[error("Observation request for '{0}' not accepted by service")]
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
/// Represents the status of an asset endpoint profile in the ADR Service.
pub struct AssetEndpointProfileStatus {
    /// A collection of errors associated with the asset endpoint profile status.
    pub errors: Option<Vec<AkriError>>,
}

impl From<AssetEndpointProfileStatus> for adr_base_service_gen::AssetEndpointProfileStatus {
    fn from(source: AssetEndpointProfileStatus) -> Self {
        let errors = source
            .errors
            .unwrap_or_default()
            .into_iter()
            .map(AkriError::into)
            .collect();

        adr_base_service_gen::AssetEndpointProfileStatus {
            errors: Some(errors),
        }
    }
}

#[derive(Clone, Debug, Default)]
/// Represents the status of an asset, including associated schemas and errors.
pub struct AssetStatus {
    /// A collection of schema references for datasets associated with the asset.
    pub datasets_schema: Option<Vec<SchemaReference>>,
    /// A collection of schema references for events associated with the asset.
    pub events_schema: Option<Vec<SchemaReference>>,
    /// A collection of errors associated with the asset status.
    pub errors: Option<Vec<AkriError>>,
    /// The version of the asset status.
    pub version: Option<i32>,
}

impl From<AssetStatus> for adr_base_service_gen::AssetStatus {
    fn from(source: AssetStatus) -> Self {
        let datasets_schema = source.datasets_schema.map(|datasets_schema| {
            datasets_schema
                .into_iter()
                .map(
                    |schema_ref| adr_base_service_gen::DatasetsSchemaSchemaElementSchema {
                        name: schema_ref.name,
                        message_schema_reference: schema_ref
                            .message_schema_reference
                            .map(MessageSchemaReference::into),
                    },
                )
                .collect()
        });
        let events_schema = source.events_schema.map(|events_schema| {
            events_schema
                .into_iter()
                .map(
                    |schema_ref| adr_base_service_gen::EventsSchemaSchemaElementSchema {
                        name: schema_ref.name,
                        message_schema_reference: schema_ref
                            .message_schema_reference
                            .map(MessageSchemaReference::into),
                    },
                )
                .collect()
        });
        let errors = source
            .errors
            .map(|errors| errors.into_iter().map(AkriError::into).collect());
        adr_base_service_gen::AssetStatus {
            datasets_schema,
            events_schema,
            errors,
            version: source.version,
        }
    }
}

impl From<adr_base_service_gen::AssetStatus> for AssetStatus {
    fn from(source: adr_base_service_gen::AssetStatus) -> Self {
        let datasets_schema = source.datasets_schema.map(|datasets_schema| {
            datasets_schema
                .into_iter()
                .map(|schema_ref| SchemaReference {
                    name: schema_ref.name,
                    message_schema_reference: schema_ref
                        .message_schema_reference
                        .map(MessageSchemaReference::from),
                })
                .collect()
        });
        let events_schema = source.events_schema.map(|events_schema| {
            events_schema
                .into_iter()
                .map(|schema_ref| SchemaReference {
                    name: schema_ref.name,
                    message_schema_reference: schema_ref
                        .message_schema_reference
                        .map(MessageSchemaReference::from),
                })
                .collect()
        });
        let errors = source
            .errors
            .map(|errors| errors.into_iter().map(AkriError::from).collect());
        AssetStatus {
            datasets_schema,
            events_schema,
            errors,
            version: source.version,
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

impl From<MessageSchemaReference> for adr_base_service_gen::MessageSchemaReference {
    fn from(value: MessageSchemaReference) -> Self {
        adr_base_service_gen::MessageSchemaReference {
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

impl From<AkriError> for adr_base_service_gen::Error {
    fn from(value: AkriError) -> Self {
        adr_base_service_gen::Error {
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
    pub datasets: Option<Vec<DetectedAssetDataSet>>,
    /// Array of events that are part of the asset. Each event can reference an asset type capability and have per-event configuration.
    pub events: Option<Vec<DetectedAssetEvent>>,
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
impl From<DetectedAsset> for adr_base_service_gen::DetectedAsset {
    fn from(source: DetectedAsset) -> Self {
        adr_base_service_gen::DetectedAsset {
            asset_endpoint_profile_ref: source.asset_endpoint_profile_ref,
            asset_name: source.asset_name,
            datasets: source.datasets.map(|datasets| {
                datasets
                    .into_iter()
                    .map(DetectedAssetDataSet::into)
                    .collect()
            }),
            default_datasets_configuration: source.default_datasets_configuration,
            default_events_configuration: source.default_events_configuration,
            default_topic: source.default_topic.map(Topic::into),
            documentation_uri: source.documentation_uri,
            events: source
                .events
                .map(|events| events.into_iter().map(DetectedAssetEvent::into).collect()),
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
pub struct DetectedAssetEvent {
    /// The 'eventConfiguration' Field.
    pub event_configuration: Option<String>,
    /// The 'eventNotifier' Field.
    pub event_notifier: String,
    /// The 'name' Field.
    pub name: String,
    /// The 'topic' Field.
    pub topic: Option<Topic>,
    /// The 'lastUpdatedOn' Field.
    pub last_updated_on: Option<String>,
}

impl From<DetectedAssetEvent> for adr_base_service_gen::DetectedAssetEventSchemaElementSchema {
    fn from(value: DetectedAssetEvent) -> Self {
        adr_base_service_gen::DetectedAssetEventSchemaElementSchema {
            event_configuration: value.event_configuration,
            event_notifier: value.event_notifier,
            name: value.name,
            last_updated_on: value.last_updated_on,
            topic: value.topic.map(Topic::into),
        }
    }
}

/// Represents a data set schema for a detected asset.
pub struct DetectedAssetDataSet {
    /// The 'dataPoints' Field.
    pub data_points: Option<Vec<DetectedAssetDataPoint>>,
    /// The 'dataSetConfiguration' Field.
    pub data_set_configuration: Option<String>,
    /// The 'name' Field.
    pub name: String,
    /// The 'topic' Field.
    pub topic: Option<Topic>,
}

impl From<DetectedAssetDataSet> for adr_base_service_gen::DetectedAssetDatasetSchemaElementSchema {
    fn from(source: DetectedAssetDataSet) -> Self {
        adr_base_service_gen::DetectedAssetDatasetSchemaElementSchema {
            data_points: source.data_points.map(|points| {
                points
                    .into_iter()
                    .map(DetectedAssetDataPoint::into)
                    .collect()
            }),
            data_set_configuration: source.data_set_configuration,
            name: source.name,
            topic: source.topic.map(Topic::into),
        }
    }
}

/// Represents a data point schema for a detected asset.
pub struct DetectedAssetDataPoint {
    /// The 'dataPointConfiguration' Field.
    pub data_point_configuration: Option<String>,
    /// The 'dataSource' Field.
    pub data_source: String,
    /// The 'name' Field.
    pub name: Option<String>,
    /// The 'lastUpdatedOn' Field.
    pub last_updated_on: Option<String>,
}

impl From<DetectedAssetDataPoint>
    for adr_base_service_gen::DetectedAssetDataPointSchemaElementSchema
{
    fn from(source: DetectedAssetDataPoint) -> Self {
        adr_base_service_gen::DetectedAssetDataPointSchemaElementSchema {
            data_point_configuration: source.data_point_configuration,
            data_source: source.data_source,
            name: source.name,
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

impl From<Topic> for adr_base_service_gen::Topic {
    fn from(source: Topic) -> Self {
        adr_base_service_gen::Topic {
            path: source.path,
            retain: match source.retain {
                Some(RetainPolicy::Keep) => Some(adr_base_service_gen::RetainSchema::Keep),
                Some(RetainPolicy::Never) => Some(adr_base_service_gen::RetainSchema::Never),
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

    /// List of supported authentication methods
    pub supported_authentication_methods: Option<Vec<AuthenticationMethods>>,

    /// Local valid URI specifying the network address/dns name of southbound service.
    pub target_address: String,
}

/// Represents the supported authentication methods for a discovered asset endpoint profile.
pub enum AuthenticationMethods {
    /// Represents an anonymous authentication method.
    Anonymous,
    /// Represents certificate authentication method.
    Certificate,
    /// Represents an username pwd authentication method.
    UsernamePassword,
}

impl From<DiscoveredAssetEndpointProfile> for aep_type_service_gen::DiscoveredAssetEndpointProfile {
    fn from(source: DiscoveredAssetEndpointProfile) -> Self {
        let supported_authentication_methods = source
            .supported_authentication_methods
            .unwrap_or_default()
            .into_iter()
            .map(|method| match method {
                AuthenticationMethods::Anonymous => {
                    aep_type_service_gen::SupportedAuthenticationMethodsSchemaElementSchema::Anonymous
                }
                AuthenticationMethods::Certificate => {
                    aep_type_service_gen::SupportedAuthenticationMethodsSchemaElementSchema::Certificate
                }
                AuthenticationMethods::UsernamePassword => {
                    aep_type_service_gen::SupportedAuthenticationMethodsSchemaElementSchema::UsernamePassword
                }
            })
            .collect();
        aep_type_service_gen::DiscoveredAssetEndpointProfile {
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
    receiver: Receiver<(AssetEndpointProfile, Option<AckToken>)>,
}

impl AssetEndpointProfileObservation {
    /// Receives a [`AssetEndpointProfile`] or [`None`] if there will be no more notifications.
    ///
    /// If there are notifications:
    /// - Returns Some([`AssetEndpointProfile`], [`Option<AckToken>`]) on success
    ///     - If auto ack is disabled, the [`AckToken`] should be used or dropped when you want the ack to occur. If auto ack is enabled, you may use ([`AssetEndpointProfileUpdateEventTelemetry`], _) to ignore the [`AckToken`].
    ///
    /// A received telemetry can be acknowledged via the [`AckToken`] by calling [`AckToken::ack`] or dropping the [`AckToken`].
    pub async fn recv_notification(&mut self) -> Option<(AssetEndpointProfile, Option<AckToken>)> {
        self.receiver.recv().await
    }
    // on drop, don't remove from hashmap so we can differentiate between a aep
    // that was observed where the receiver was dropped and a aep that was never observed
}

impl From<adr_base_service_gen::AssetEndpointProfileUpdateEventTelemetry> for AssetEndpointProfile {
    fn from(source: adr_base_service_gen::AssetEndpointProfileUpdateEventTelemetry) -> Self {
        AssetEndpointProfile::from(
            source
                .asset_endpoint_profile_update_event
                .asset_endpoint_profile,
        )
    }
}

/// A struct to manage receiving notifications for a key
#[derive(Debug)]
pub struct AssetObservation {
    /// The name of the asset (for convenience)
    pub name: String,
    /// The internal channel for receiving update telemetry for this asset
    receiver: Receiver<(Asset, Option<AckToken>)>,
}

impl AssetObservation {
    /// Receives a [`Asset`] or [`None`] if there will be no more notifications.
    ///
    /// If there are notifications:
    /// - Returns Some([`Asset`], [`Option<AckToken>`]) on success
    ///     - If auto ack is disabled, the [`AckToken`] should be used or dropped when you want the ack to occur. If auto ack is enabled, you may use ([`AssetEndpointProfileUpdateEventTelemetry`], _) to ignore the [`AckToken`].
    ///
    /// A received telemetry can be acknowledged via the [`AckToken`] by calling [`AckToken::ack`] or dropping the [`AckToken`].
    pub async fn recv_notification(&mut self) -> Option<(Asset, Option<AckToken>)> {
        self.receiver.recv().await
    }
    // on drop, don't remove from hashmap so we can differentiate between a aep
    // that was observed where the receiver was dropped and a aep that was never observed
}

impl From<adr_base_service_gen::AssetUpdateEventTelemetry> for Asset {
    fn from(source: adr_base_service_gen::AssetUpdateEventTelemetry) -> Self {
        Asset::from(source.asset_update_event.asset)
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

impl From<adr_base_service_gen::AssetEndpointProfile> for AssetEndpointProfile {
    fn from(source: adr_base_service_gen::AssetEndpointProfile) -> Self {
        AssetEndpointProfile {
            name: source.name,
            specification: AssetEndpointProfileSpecificationSchema::from(source.specification),
            status: source.status.map(AssetEndpointProfileStatus::from),
        }
    }
}

impl From<adr_base_service_gen::AssetEndpointProfileStatus> for AssetEndpointProfileStatus {
    fn from(source: adr_base_service_gen::AssetEndpointProfileStatus) -> Self {
        AssetEndpointProfileStatus {
            errors: source
                .errors
                .map(|errors| errors.into_iter().map(AkriError::from).collect()),
        }
    }
}

impl From<adr_base_service_gen::Error> for AkriError {
    fn from(source: adr_base_service_gen::Error) -> Self {
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
    pub authentication: Option<Authentication>,

    /// The 'discoveredAssetEndpointProfileRef' Field.
    pub discovered_asset_endpoint_profile_ref: Option<String>,

    /// The 'endpointProfileType' Field.
    pub endpoint_profile_type: String,

    /// The 'targetAddress' Field.
    pub target_address: String,

    /// The 'uuid' Field.
    pub uuid: Option<String>,
}
impl From<adr_base_service_gen::AssetEndpointProfileSpecificationSchema>
    for AssetEndpointProfileSpecificationSchema
{
    fn from(source: adr_base_service_gen::AssetEndpointProfileSpecificationSchema) -> Self {
        AssetEndpointProfileSpecificationSchema {
            additional_configuration: source.additional_configuration,
            authentication: source.authentication.map(Authentication::from),
            discovered_asset_endpoint_profile_ref: source.discovered_asset_endpoint_profile_ref,
            endpoint_profile_type: source.endpoint_profile_type,
            target_address: source.target_address,
            uuid: source.uuid,
        }
    }
}

/// Represents the client authentication schema, including method and credentials.
/// // TODO Check enum with values
pub enum Authentication {
    /// Represents an anonymous authentication method.
    Anonymous,
    /// Represents certificate authentication method.
    Certificate(X509credentials),
    /// Represents an username pwd authentication method.
    UsernamePassword(UsernamePasswordCredentials),
}

// impl From<GenAuthenticationSchema> for Authentication {
//     fn from(source: GenAuthenticationSchema) -> Self {
//         let auth = match source.method {
//             MethodSchema::Anonymous => Authentication::Anonymous,
//             MethodSchema::Certificate => {
//                 Authentication::Certificate(X509credentials::from(source.x509credentials.unwrap()))
//             }
//             MethodSchema::UsernamePassword => Authentication::UsernamePassword(
//                 UsernamePasswordCredentials::from(source.username_password_credentials.unwrap()),
//             ),
//         };
//         return auth;
//     }
// }

impl From<adr_base_service_gen::AuthenticationSchema> for Authentication {
    fn from(source: adr_base_service_gen::AuthenticationSchema) -> Self {
        match source.method {
            adr_base_service_gen::MethodSchema::Anonymous => Authentication::Anonymous,
            adr_base_service_gen::MethodSchema::Certificate => {
                if let Some(credentials) = source.x509credentials {
                    Authentication::Certificate(X509credentials::from(credentials))
                } else {
                    panic!("Certificate method specified but x509credentials missing");
                }
            }
            adr_base_service_gen::MethodSchema::UsernamePassword => {
                if let Some(credentials) = source.username_password_credentials {
                    Authentication::UsernamePassword(UsernamePasswordCredentials::from(credentials))
                } else {
                    panic!("UsernamePassword method specified but credentials missing");
                }
            }
        }
    }
}

/// Represents the credentials schema for username and password authentication.
pub struct UsernamePasswordCredentials {
    /// The 'passwordSecretName' Field.
    pub password_secret_name: String,
    /// The 'usernameSecretName' Field.
    pub username_secret_name: String,
}

impl From<adr_base_service_gen::UsernamePasswordCredentialsSchema> for UsernamePasswordCredentials {
    fn from(source: adr_base_service_gen::UsernamePasswordCredentialsSchema) -> Self {
        UsernamePasswordCredentials {
            password_secret_name: source.password_secret_name,
            username_secret_name: source.username_secret_name,
        }
    }
}
/// Represents the X.509 credentials schema for client authentication.
pub struct X509credentials {
    /// The 'certificateSecretName' Field.
    pub certificate_secret_name: String,
}

impl From<adr_base_service_gen::X509credentialsSchema> for X509credentials {
    fn from(source: adr_base_service_gen::X509credentialsSchema) -> Self {
        X509credentials {
            certificate_secret_name: source.certificate_secret_name,
        }
    }
}

/// Represents an asset in the Azure Device Registry service.
pub struct Asset {
    /// The 'name' Field.
    pub name: String,
    /// The 'specification' Field.
    pub specification: AssetSpecification,
    /// The status of the asset, including associated schemas and errors.
    pub status: Option<AssetStatus>,
}

impl From<adr_base_service_gen::Asset> for Asset {
    fn from(source: adr_base_service_gen::Asset) -> Self {
        Asset {
            name: source.name,
            specification: AssetSpecification::from(source.specification),
            status: source.status.map(AssetStatus::from),
        }
    }
}

/// Represents the specification schema for a client asset, including attributes, datasets, and other metadata.
pub struct AssetSpecification {
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
    /// The 'attributes' Field.
    pub attributes: Option<HashMap<String, String>>,
    /// The 'datasets' Field.
    pub datasets: Option<Vec<AssetDataset>>,
    /// The 'description' Field.
    pub description: Option<String>,
    /// The 'discoveredAssetRefs' Field.
    pub discovered_asset_refs: Option<Vec<String>>,
    /// The 'displayName' Field.
    pub display_name: Option<String>,
    /// The 'enabled' Field.
    pub enabled: Option<bool>,
    /// The 'events' Field.
    pub events: Option<Vec<AssetEvent>>,
    /// The 'externalAssetId' Field.
    pub external_asset_id: Option<String>,
    /// The 'uuid' Field.
    pub uuid: Option<String>,
    /// The 'version' Field.
    pub version: Option<String>,
}

impl From<adr_base_service_gen::AssetSpecificationSchema> for AssetSpecification {
    fn from(source: adr_base_service_gen::AssetSpecificationSchema) -> Self {
        AssetSpecification {
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
            attributes: source.attributes,
            datasets: source
                .datasets
                .map(|datasets| datasets.into_iter().map(AssetDataset::from).collect()),
            description: source.description,
            discovered_asset_refs: source.discovered_asset_refs,
            display_name: source.display_name,
            enabled: source.enabled,
            events: source
                .events
                .map(|events| events.into_iter().map(AssetEvent::from).collect()),
            external_asset_id: source.external_asset_id,
            uuid: source.uuid,
            version: source.version,
        }
    }
}

/// The 'datasets' Field.
pub struct AssetDataset {
    /// The 'dataPoints' Field.
    pub data_points: Option<Vec<AssetDataPoint>>,
    /// The 'datasetConfiguration' Field.
    pub dataset_configuration: Option<String>,
    /// The 'name' Field.
    pub name: String,
    /// The 'topic' Field.
    pub topic: Option<Topic>,
}

impl From<adr_base_service_gen::AssetDatasetSchemaElementSchema> for AssetDataset {
    fn from(source: adr_base_service_gen::AssetDatasetSchemaElementSchema) -> Self {
        AssetDataset {
            data_points: source
                .data_points
                .map(|points| points.into_iter().map(AssetDataPoint::from).collect()),
            dataset_configuration: source.dataset_configuration,
            name: source.name,
            topic: source.topic.map(Topic::from),
        }
    }
}

/// Represents the schema for an asset data point, including its configuration and observability mode.
pub struct AssetDataPoint {
    /// The 'dataPointConfiguration' Field.
    pub data_point_configuration: Option<String>,
    /// The 'dataSource' Field.
    pub data_source: String,
    /// The 'name' Field.
    pub name: Option<String>,
    /// The 'observabilityMode' Field.
    pub observability_mode: Option<DataPointObservabilityMode>,
}

impl From<adr_base_service_gen::AssetDataPointSchemaElementSchema> for AssetDataPoint {
    fn from(source: adr_base_service_gen::AssetDataPointSchemaElementSchema) -> Self {
        AssetDataPoint {
            data_point_configuration: source.data_point_configuration,
            data_source: source.data_source,
            name: Some(source.name),
            observability_mode: source
                .observability_mode
                .map(DataPointObservabilityMode::from),
        }
    }
}
/// Represents the schema for an asset event, including its configuration and observability mode.
pub struct AssetEvent {
    /// The 'eventConfiguration' Field.
    pub event_configuration: Option<String>,
    /// The 'eventNotifier' Field.
    pub event_notifier: String,
    /// The 'name' Field.
    pub name: String,
    /// The 'topic' Field.
    pub topic: Option<Topic>,
    /// The 'observabilityMode' Field.
    pub observability_mode: Option<EventObservabilityMode>,
}

impl From<adr_base_service_gen::AssetEventSchemaElementSchema> for AssetEvent {
    fn from(source: adr_base_service_gen::AssetEventSchemaElementSchema) -> Self {
        AssetEvent {
            event_configuration: source.event_configuration,
            event_notifier: source.event_notifier,
            name: source.name,
            topic: source.topic.map(Topic::from),
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

impl From<adr_base_service_gen::AssetDataPointObservabilityModeSchema>
    for DataPointObservabilityMode
{
    fn from(source: adr_base_service_gen::AssetDataPointObservabilityModeSchema) -> Self {
        match source {
            adr_base_service_gen::AssetDataPointObservabilityModeSchema::Counter => {
                DataPointObservabilityMode::Counter
            }
            adr_base_service_gen::AssetDataPointObservabilityModeSchema::Gauge => {
                DataPointObservabilityMode::Gauge
            }
            adr_base_service_gen::AssetDataPointObservabilityModeSchema::Histogram => {
                DataPointObservabilityMode::Histogram
            }
            adr_base_service_gen::AssetDataPointObservabilityModeSchema::Log => {
                DataPointObservabilityMode::Log
            }
            adr_base_service_gen::AssetDataPointObservabilityModeSchema::None => {
                DataPointObservabilityMode::None
            }
        }
    }
}

impl From<adr_base_service_gen::AssetEventObservabilityModeSchema> for EventObservabilityMode {
    fn from(source: adr_base_service_gen::AssetEventObservabilityModeSchema) -> Self {
        match source {
            adr_base_service_gen::AssetEventObservabilityModeSchema::Log => {
                EventObservabilityMode::Log
            }
            adr_base_service_gen::AssetEventObservabilityModeSchema::None => {
                EventObservabilityMode::None
            }
        }
    }
}

impl From<adr_base_service_gen::Topic> for Topic {
    fn from(source: adr_base_service_gen::Topic) -> Self {
        Topic {
            path: (source.path),
            retain: match source.retain {
                Some(adr_base_service_gen::RetainSchema::Keep) => Some(RetainPolicy::Keep),
                Some(adr_base_service_gen::RetainSchema::Never) => Some(RetainPolicy::Never),
                None => None,
            },
        }
    }
}

impl From<adr_base_service_gen::MessageSchemaReference> for MessageSchemaReference {
    fn from(value: adr_base_service_gen::MessageSchemaReference) -> Self {
        MessageSchemaReference {
            message_schema_name: value.schema_name,
            message_schema_version: value.schema_version,
            message_schema_namespace: value.schema_namespace,
        }
    }
}

/// Represents the response status for a detected asset.
pub enum DetectedAssetResponseStatus {
    /// Represents the created status.
    Created,
    /// Represents the duplicate status.
    Duplicate,
    /// Represents the failed status.
    Failed,
}

impl From<adr_base_service_gen::DetectedAssetResponseStatusSchema> for DetectedAssetResponseStatus {
    fn from(source: adr_base_service_gen::DetectedAssetResponseStatusSchema) -> Self {
        match source {
            adr_base_service_gen::DetectedAssetResponseStatusSchema::Created => {
                DetectedAssetResponseStatus::Created
            }
            adr_base_service_gen::DetectedAssetResponseStatusSchema::Duplicate => {
                DetectedAssetResponseStatus::Duplicate
            }
            adr_base_service_gen::DetectedAssetResponseStatusSchema::Failed => {
                DetectedAssetResponseStatus::Failed
            }
        }
    }
}

/// Represents the response status for a discovered asset endpoint profile.
pub enum DiscoveredAssetEndpointProfileResponseStatus {
    /// Represents the created status.
    Created,
    /// Represents the duplicate status.
    Duplicate,
    /// Represents the failed status.
    Failed,
}

impl From<aep_type_service_gen::DiscoveredAssetEndpointProfileResponseStatusSchema>
    for DiscoveredAssetEndpointProfileResponseStatus
{
    fn from(
        source: aep_type_service_gen::DiscoveredAssetEndpointProfileResponseStatusSchema,
    ) -> Self {
        match source {
            aep_type_service_gen::DiscoveredAssetEndpointProfileResponseStatusSchema::Created => {
                DiscoveredAssetEndpointProfileResponseStatus::Created
            }
            aep_type_service_gen::DiscoveredAssetEndpointProfileResponseStatusSchema::Duplicate => {
                DiscoveredAssetEndpointProfileResponseStatus::Duplicate
            }
            aep_type_service_gen::DiscoveredAssetEndpointProfileResponseStatusSchema::Failed => {
                DiscoveredAssetEndpointProfileResponseStatus::Failed
            }
        }
    }
}
