// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

//! Types for Azure Device Registry operations.

// Large TODOs for this client: docs, unit tests, validation

use core::fmt::Debug;
use std::collections::HashMap;

use azure_iot_operations_mqtt::interface::AckToken;
use azure_iot_operations_protocol::{common::aio_protocol_error::AIOProtocolError, rpc_command};
use thiserror::Error;

use crate::azure_device_registry::device_name_gen::adr_base_service::client as adr_name_gen;
use crate::azure_device_registry::device_name_gen::common_types::options::{
    CommandInvokerOptionsBuilderError, TelemetryReceiverOptionsBuilderError,
};
use crate::common::dispatcher::{self, Receiver};

/// Azure Device Registry Client implementation wrapper
mod client;
/// Azure Device Registry generated code
mod device_name_gen;

pub use client::{Client, ClientOptions, ClientOptionsBuilder};

// ~~~~~~~~~~~~~~~~~~~SDK Created Structs~~~~~~~~~~~~~~~~~~~~~~~~
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
    /// An argument provided for a request was invalid.
    #[error(transparent)]
    InvalidRequestArgument(#[from] rpc_command::invoker::RequestBuilderError),
    // An argument provided for a request was invalid.
    #[error("{0}")]
    InvalidClientId(String),
    /// An error was returned by the Azure Device Registry Service.
    #[error("{0:?}")]
    ServiceError(ServiceError),
    /// A Device or an asset may only have one observation at a time.
    #[error("Device or asset may only be observed once at a time")]
    DuplicateObserve(#[from] dispatcher::RegisterError),
    /// A Device or an asset had an error during observation or unobservation.
    #[error("Observation/Unobservation not accepted by service")]
    ObservationError,
    /// An error occurred while shutting down the Azure Device Registry Client.
    #[error("Shutdown error occurred with the following protocol errors: {0:?}")]
    ShutdownError(Vec<AIOProtocolError>),
}

/// An error returned by the Azure Device Registry Service.
/// TODO placeholder until we get the format from the service
#[derive(Debug)]
pub struct ServiceError {
    pub message: String,
}

impl From<CommandInvokerOptionsBuilderError> for ErrorKind {
    fn from(value: CommandInvokerOptionsBuilderError) -> Self {
        ErrorKind::InvalidClientId(value.to_string())
    }
}

impl From<TelemetryReceiverOptionsBuilderError> for ErrorKind {
    fn from(value: TelemetryReceiverOptionsBuilderError) -> Self {
        ErrorKind::InvalidClientId(value.to_string())
    }
}

// ~~~~~~~~~~~~~~~~~~~SDK Created Device Structs~~~~~~~~~~~~~
/// A struct to manage receiving notifications for a device
#[derive(Debug)]
pub struct DeviceUpdateObservation {
    /// The internal channel for receiving update telemetry for this device
    receiver: Receiver<(Device, Option<AckToken>)>,
}

impl DeviceUpdateObservation {
    /// Receives an updated [`Device`] or [`None`] if there will be no more notifications.
    ///
    /// If there are notifications:
    /// - Returns Some([`Device`], [`Option<AckToken>`]) on success
    ///     - If auto ack is disabled, the [`AckToken`] should be used or dropped when you want the ack to occur. If auto ack is enabled, you may use ([`Device`], _) to ignore the [`AckToken`].
    ///
    /// A received notification can be acknowledged via the [`AckToken`] by calling [`AckToken::ack`] or dropping the [`AckToken`].
    pub async fn recv_notification(&mut self) -> Option<(Device, Option<AckToken>)> {
        self.receiver.recv().await
    }
}

// ~~~~~~~~~~~~~~~~~~~SDK Created Asset Structs~~~~~~~~~~~~~
/// A struct to manage receiving notifications for a asset
#[derive(Debug)]
pub struct AssetUpdateObservation {
    /// The internal channel for receiving update telemetry for this asset
    receiver: Receiver<(Asset, Option<AckToken>)>,
}

impl AssetUpdateObservation {
    /// Receives an updated [`Asset`] or [`None`] if there will be no more notifications.
    ///
    /// If there are notifications:
    /// - Returns Some([`Asset`], [`Option<AckToken>`]) on success
    ///     - If auto ack is disabled, the [`AckToken`] should be used or dropped when you want the ack to occur. If auto ack is enabled, you may use ([`Asset`], _) to ignore the [`AckToken`].
    ///
    /// A received notification can be acknowledged via the [`AckToken`] by calling [`AckToken::ack`] or dropping the [`AckToken`].
    pub async fn recv_notification(&mut self) -> Option<(Asset, Option<AckToken>)> {
        self.receiver.recv().await
    }
}

// ~~~~~~~~~~~~~~~~~~~Helper fns ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
fn option_vec_from<T, U>(source: Option<Vec<T>>, into_fn: impl Fn(T) -> U) -> Option<Vec<U>> {
    source.map(|vec| vec.into_iter().map(into_fn).collect())
}

fn vec_from_option_vec<T, U>(source: Option<Vec<T>>, into_fn: impl Fn(T) -> U) -> Vec<U> {
    source.map_or(vec![], |vec| vec.into_iter().map(into_fn).collect())
}

// ~~~~~~~~~~~~~~~~~~~Common DTDL Equivalent Structs~~~~~~~~~~~~~
#[derive(Clone, Debug, Default)]
pub struct StatusConfig {
    /// Error code for classification of errors.
    pub error: Option<ConfigError>,
    /// The last time the configuration has been modified.
    pub last_transition_time: Option<String>,
    /// The version of the asset configuration.
    pub version: Option<u64>,
}

#[derive(Clone, Debug, Default)]
/// Represents an error in the configuration of an asset or device.
pub struct ConfigError {
    /// The code of the error.
    pub code: Option<String>,
    /// Array of event statuses that describe the status of each event.
    pub details: Option<Vec<Details>>,
    /// The inner error, if any.
    pub inner_error: Option<HashMap<String, String>>,
    /// The message of the error.
    pub message: Option<String>,
}

#[derive(Clone, Debug)]
/// Represents the details of an error.
pub struct Details {
    /// The multi part error code for root cause analysis.
    pub code: Option<String>,
    /// The correlation ID of the details.
    pub correlation_id: Option<String>,
    /// Any helpful information associated with the details.
    pub info: Option<String>,
    /// The error message of the details.
    pub message: Option<String>,
}

// ~~ From impls ~~
impl From<StatusConfig> for adr_name_gen::DeviceStatusConfigSchema {
    fn from(value: StatusConfig) -> Self {
        adr_name_gen::DeviceStatusConfigSchema {
            version: value.version,
            error: value.error.map(ConfigError::into),
            last_transition_time: value.last_transition_time,
        }
    }
}

impl From<adr_name_gen::DeviceStatusConfigSchema> for StatusConfig {
    fn from(value: adr_name_gen::DeviceStatusConfigSchema) -> Self {
        StatusConfig {
            version: value.version,
            error: value.error.map(adr_name_gen::ConfigError::into),
            last_transition_time: value.last_transition_time,
        }
    }
}

impl From<StatusConfig> for adr_name_gen::AssetConfigStatusSchema {
    fn from(value: StatusConfig) -> Self {
        adr_name_gen::AssetConfigStatusSchema {
            error: value.error.map(ConfigError::into),
            last_transition_time: value.last_transition_time,
            version: value.version,
        }
    }
}

impl From<adr_name_gen::AssetConfigStatusSchema> for StatusConfig {
    fn from(value: adr_name_gen::AssetConfigStatusSchema) -> Self {
        StatusConfig {
            error: value.error.map(ConfigError::from),
            last_transition_time: value.last_transition_time,
            version: value.version,
        }
    }
}

impl From<ConfigError> for adr_name_gen::ConfigError {
    fn from(value: ConfigError) -> Self {
        adr_name_gen::ConfigError {
            code: value.code,
            message: value.message,
            details: option_vec_from(value.details, |details| {
                adr_name_gen::DetailsSchemaElementSchema {
                    code: details.code,
                    correlation_id: details.correlation_id,
                    info: details.info,
                    message: details.message,
                }
            }),
            inner_error: value.inner_error,
        }
    }
}

impl From<adr_name_gen::ConfigError> for ConfigError {
    fn from(value: adr_name_gen::ConfigError) -> Self {
        ConfigError {
            code: value.code,
            message: value.message,
            details: option_vec_from(value.details, |details| Details {
                code: details.code,
                correlation_id: details.correlation_id,
                info: details.info,
                message: details.message,
            }),
            inner_error: value.inner_error,
        }
    }
}

// ~~~~~~~~~~~~~~~~~~~Device Endpoint DTDL Equivalent Structs~~~~

/// Represents a Device in the Azure Device Registry service.
#[derive(Clone, Debug)]
pub struct Device {
    /// The 'name' Field.
    pub name: String,
    /// The 'specification' Field.
    pub specification: DeviceSpecification,
    /// The 'status' Field.
    pub status: Option<DeviceStatus>,
}

#[derive(Debug, Clone)]
pub struct DeviceSpecification {
    /// The 'attributes' Field.
    pub attributes: HashMap<String, String>, // if None, we can represent as empty hashmap
    /// The 'discoveredDeviceRef' Field.
    pub discovered_device_ref: Option<String>,
    /// The 'enabled' Field.
    pub enabled: Option<bool>,
    /// The 'endpoints' Field.
    pub endpoints: DeviceEndpoints,
    /// The 'externalDeviceId' Field.
    pub external_device_id: Option<String>,
    /// The 'lastTransitionTime' Field.
    pub last_transition_time: Option<String>, // DateTime?
    /// The 'manufacturer' Field.
    pub manufacturer: Option<String>,
    /// The 'model' Field.
    pub model: Option<String>,
    /// The 'operatingSystem' Field.
    pub operating_system: Option<String>,
    /// The 'operatingSystemVersion' Field.
    pub operating_system_version: Option<String>,
    /// The 'uuid' Field.
    pub uuid: Option<String>,
    /// The 'version' Field.
    pub version: Option<u64>,
}

#[derive(Debug, Clone, Default)]
pub struct DeviceEndpoints {
    pub inbound: HashMap<String, InboundEndpoint>, // if None, we can represent as empty hashmap. Might be able to change this to a single InboundEndpoint
    pub outbound_assigned: HashMap<String, OutboundEndpoint>,
    pub outbound_unassigned: HashMap<String, OutboundEndpoint>,
}

#[derive(Debug, Clone)]
pub struct OutboundEndpoint {
    /// The 'address' Field.
    pub address: String,
    /// The 'endpointType' Field.
    pub endpoint_type: Option<String>,
}

#[derive(Debug, Clone)]
pub struct InboundEndpoint {
    /// The 'additionalConfiguration' Field.
    pub additional_configuration: Option<String>,
    /// The 'address' Field.
    pub address: String,
    /// The 'authentication' Field.
    pub authentication: Authentication,
    /// The 'endpointType' Field.
    pub endpoint_type: String,
    /// The 'trustSettings' Field.
    pub trust_settings: Option<TrustSettings>,
    /// The 'version' Field.
    pub version: Option<String>,
}

#[derive(Debug, Clone)]
pub struct TrustSettings {
    /// The 'issuerList' Field.
    pub issuer_list: Option<String>,
    /// The 'trustList' Field.
    pub trust_list: Option<String>,
}

#[derive(Debug, Clone, Default)]
pub enum Authentication {
    #[default]
    Anonymous,
    Certificate {
        /// The 'certificateSecretName' Field.
        certificate_secret_name: String,
    },
    UsernamePassword {
        /// The 'passwordSecretName' Field.
        password_secret_name: String,
        /// The 'usernameSecretName' Field.
        username_secret_name: String,
    },
}
// ~~ From impls ~~
impl From<adr_name_gen::Device> for Device {
    fn from(value: adr_name_gen::Device) -> Self {
        Device {
            name: value.name,
            specification: value.specification.into(),
            status: value.status.map(DeviceStatus::from),
        }
    }
}

impl From<adr_name_gen::DeviceUpdateEventTelemetry> for Device {
    fn from(value: adr_name_gen::DeviceUpdateEventTelemetry) -> Self {
        Device {
            name: value.device_update_event.device.name,
            specification: value.device_update_event.device.specification.into(),
            status: value
                .device_update_event
                .device
                .status
                .map(DeviceStatus::from),
        }
    }
}

impl From<adr_name_gen::DeviceSpecificationSchema> for DeviceSpecification {
    fn from(value: adr_name_gen::DeviceSpecificationSchema) -> Self {
        DeviceSpecification {
            attributes: value.attributes.unwrap_or_default(),
            discovered_device_ref: value.discovered_device_ref,
            enabled: value.enabled,
            endpoints: value
                .endpoints
                .map(DeviceEndpoints::from)
                .unwrap_or_default(),
            external_device_id: value.external_device_id,
            last_transition_time: value.last_transition_time,
            manufacturer: value.manufacturer,
            model: value.model,
            operating_system: value.operating_system,
            operating_system_version: value.operating_system_version,
            uuid: value.uuid,
            version: value.version,
        }
    }
}

impl From<adr_name_gen::DeviceEndpointSchema> for DeviceEndpoints {
    fn from(value: adr_name_gen::DeviceEndpointSchema) -> Self {
        let inbound = match value.inbound {
            Some(inbound_endpoints) => inbound_endpoints
                .into_iter()
                .map(|(k, v)| (k, v.into()))
                .collect(),
            None => HashMap::new(),
        };
        let outbound_assigned;
        let outbound_unassigned;
        if let Some(outbound) = value.outbound {
            outbound_assigned = outbound
                .assigned
                .into_iter()
                .map(|(k, v)| (k, OutboundEndpoint::from(v)))
                .collect();
            outbound_unassigned = match outbound.unassigned {
                Some(unassigned) => unassigned
                    .into_iter()
                    .map(|(k, v)| (k, OutboundEndpoint::from(v)))
                    .collect(),
                None => HashMap::new(),
            };
        } else {
            outbound_assigned = HashMap::new();
            outbound_unassigned = HashMap::new();
        }
        DeviceEndpoints {
            inbound,
            outbound_assigned,
            outbound_unassigned,
        }
    }
}

impl From<adr_name_gen::DeviceOutboundEndpoint> for OutboundEndpoint {
    fn from(value: adr_name_gen::DeviceOutboundEndpoint) -> Self {
        OutboundEndpoint {
            address: value.address,
            endpoint_type: value.endpoint_type,
        }
    }
}

impl From<adr_name_gen::InboundSchemaMapValueSchema> for InboundEndpoint {
    fn from(value: adr_name_gen::InboundSchemaMapValueSchema) -> Self {
        InboundEndpoint {
            additional_configuration: value.additional_configuration,
            address: value.address,
            authentication: value
                .authentication
                .map(Authentication::from)
                .unwrap_or_default(),
            trust_settings: value.trust_settings.map(TrustSettings::from),
            endpoint_type: value.endpoint_type,
            version: value.version,
        }
    }
}

impl From<adr_name_gen::TrustSettingsSchema> for TrustSettings {
    fn from(value: adr_name_gen::TrustSettingsSchema) -> Self {
        TrustSettings {
            issuer_list: value.issuer_list,
            trust_list: value.trust_list,
        }
    }
}

impl From<adr_name_gen::AuthenticationSchema> for Authentication {
    fn from(value: adr_name_gen::AuthenticationSchema) -> Self {
        match value.method {
            adr_name_gen::MethodSchema::Anonymous => Authentication::Anonymous,
            adr_name_gen::MethodSchema::Certificate => {
                Authentication::Certificate {
                    certificate_secret_name: match value.x509credentials {
                        Some(x509credentials) => x509credentials.certificate_secret_name,
                        None => String::new(), // TODO: might want to log an error or handle this differently in the future. Shouldn't be possible though
                    },
                }
            }
            adr_name_gen::MethodSchema::UsernamePassword => {
                match value.username_password_credentials {
                    Some(username_password_credentials) => Authentication::UsernamePassword {
                        password_secret_name: username_password_credentials.password_secret_name,
                        username_secret_name: username_password_credentials.username_secret_name,
                    },
                    None => {
                        Authentication::UsernamePassword {
                            password_secret_name: String::new(), // TODO: might want to log an error or handle this differently in the future. Shouldn't be possible though
                            username_secret_name: String::new(),
                        }
                    }
                }
            }
        }
    }
}

// ~~~~~~~~~~~~~~~~~~~Device Endpoint Status DTDL Equivalent Structs~~~~
#[derive(Clone, Debug, Default)]
/// Represents the status of a Device in the ADR Service.
pub struct DeviceStatus {
    /// The 'config' Field.
    pub config: Option<StatusConfig>,
    /// The 'endpoints' Field.
    pub endpoints: HashMap<String, Option<ConfigError>>,
}

// ~~ From impls ~~
impl From<DeviceStatus> for adr_name_gen::DeviceStatus {
    fn from(value: DeviceStatus) -> Self {
        let endpoints = if value.endpoints.is_empty() {
            None
        } else {
            Some(adr_name_gen::DeviceStatusEndpointSchema {
                inbound: Some(
                    value
                        .endpoints
                        .into_iter()
                        .map(|(k, v)| {
                            (
                                k,
                                adr_name_gen::DeviceStatusInboundEndpointSchemaMapValueSchema {
                                    error: v.map(ConfigError::into),
                                },
                            )
                        })
                        .collect(),
                ),
            })
        };
        adr_name_gen::DeviceStatus {
            config: value.config.map(StatusConfig::into),
            endpoints,
        }
    }
}

impl From<adr_name_gen::DeviceStatus> for DeviceStatus {
    fn from(value: adr_name_gen::DeviceStatus) -> Self {
        let endpoints = match value.endpoints {
            Some(endpoint_status) => match endpoint_status.inbound {
                Some(inbound_endpoints) => inbound_endpoints
                    .into_iter()
                    .map(|(k, v)| (k, v.error.map(ConfigError::from)))
                    .collect(),
                None => HashMap::new(),
            },
            None => HashMap::new(),
        };
        DeviceStatus {
            config: value
                .config
                .map(adr_name_gen::DeviceStatusConfigSchema::into),
            endpoints,
        }
    }
}

// ~~~~~~~~~~~~~~~~~~~Asset DTDL Equivalent Structs~~~~~~~~~~~~~~

#[derive(Clone, Debug)]
pub struct Asset {
    pub name: String,
    pub specification: AssetSpecification,
    pub status: Option<AssetStatus>,
}

#[derive(Clone, Debug)]
pub struct AssetSpecification {
    pub asset_type_refs: Vec<String>, // if None, we can represent as empty vec. Can currently only be length of 1
    pub attributes: HashMap<String, String>, // if None, we can represent as empty hashmap
    pub datasets: Vec<AssetDataset>,  // if None, we can represent as empty vec
    pub default_datasets_configuration: Option<String>,
    pub default_datasets_destinations: Vec<AssetDatasetsDestination>, // if None, we can represent as empty vec.  Can currently only be length of 1
    pub default_events_configuration: Option<String>,
    pub default_events_destinations: Vec<EventsAndStreamsDestination>, // if None, we can represent as empty vec.  Can currently only be length of 1
    pub default_management_groups_configuration: Option<String>,
    pub default_streams_configuration: Option<String>,
    pub default_streams_destinations: Vec<EventsAndStreamsDestination>, // if None, we can represent as empty vec. Can currently only be length of 1
    pub description: Option<String>,
    pub device_ref: DeviceRef,
    pub discovered_asset_refs: Vec<String>, // if None, we can represent as empty vec
    pub display_name: Option<String>,
    pub documentation_uri: Option<String>,
    pub enabled: Option<bool>,   // TODO: just bool?
    pub events: Vec<AssetEvent>, // if None, we can represent as empty vec
    pub external_asset_id: Option<String>,
    pub hardware_revision: Option<String>,
    pub last_transition_time: Option<String>,
    pub management_groups: Vec<AssetManagementGroup>, // if None, we can represent as empty vec
    pub manufacturer: Option<String>,
    pub manufacturer_uri: Option<String>,
    pub model: Option<String>,
    pub product_code: Option<String>,
    pub serial_number: Option<String>,
    pub software_revision: Option<String>,
    pub streams: Vec<AssetStream>, // if None, we can represent as empty vec
    pub uuid: Option<String>,
    pub version: Option<u64>,
}

#[derive(Clone, Debug)]
pub struct AssetDataset {
    pub data_points: Vec<AssetDatasetDataPoint>, // if None, we can represent as empty vec
    pub data_source: Option<String>,
    pub destinations: Vec<AssetDatasetsDestination>, // if None, we can represent as empty vec. Can currently only be length of 1
    pub name: String,
    pub type_ref: Option<String>,
}

#[derive(Clone, Debug)]
pub struct AssetDatasetDataPoint {
    pub data_point_configuration: Option<String>,
    pub data_source: String,
    pub name: String,
    pub type_ref: Option<String>,
}

#[derive(Clone, Debug)]
pub struct AssetDatasetsDestination {
    pub configuration: DestinationConfiguration,
    pub target: DatasetTarget,
}
// TODO: switch to this  rust enum
// pub enum AssetDatasetsDestination {
//     BrokerStateStore{key: String},
//     Mqtt{ topic: String,
//         qos: Option<Qos>,
//         retain: Option<Retain>,
//         ttl: Option<u64>},
//     Storage {path: String},
// }

#[derive(Clone, Debug)]
pub struct EventsAndStreamsDestination {
    pub configuration: DestinationConfiguration,
    pub target: EventStreamTarget,
}

// TODO: switch to this  rust enum
// pub enum EventsAndStreamsDestination {
//     Mqtt{ topic: String,
//         qos: Option<Qos>,
//         retain: Option<Retain>,
//         ttl: Option<u64>},
//     Storage {path: String},
// }

#[derive(Clone, Debug)]
pub struct DeviceRef {
    pub device_name: String,
    pub endpoint_name: String,
}

#[derive(Clone, Debug)]
pub struct AssetEvent {
    pub data_points: Vec<AssetEventDataPoint>, // if None, we can represent as empty vec
    pub destinations: Vec<EventsAndStreamsDestination>, // if None, we can represent as empty vec. Can currently only be length of 1
    pub event_configuration: Option<String>,
    pub event_notifier: String,
    pub name: String,
    pub type_ref: Option<String>,
}

#[derive(Clone, Debug)]
pub struct AssetManagementGroup {
    pub actions: Vec<AssetManagementGroupAction>, // if None, we can represent as empty vec
    pub default_time_out_in_seconds: Option<u32>,
    pub default_topic: Option<String>,
    pub management_group_configuration: Option<String>,
    pub name: String,
    pub type_ref: Option<String>,
}

#[derive(Clone, Debug)]
pub struct AssetManagementGroupAction {
    pub action_configuration: Option<String>,
    pub action_type: AssetManagementGroupActionType,
    pub name: String,
    pub target_uri: String,
    pub time_out_in_seconds: Option<u32>,
    pub topic: Option<String>,
    pub type_ref: Option<String>,
}

#[derive(Clone, Debug)]
pub struct AssetStream {
    pub destinations: Vec<EventsAndStreamsDestination>, // if None, we can represent as empty vec. Can currently only be length of 1
    pub name: String,
    pub stream_configuration: Option<String>,
    pub type_ref: Option<String>,
}

#[derive(Clone, Debug)]
pub struct AssetEventDataPoint {
    pub data_point_configuration: Option<String>,
    pub data_source: String,
    pub name: String,
}

// TODO: turn into rust enums for which of these options can correlate to which destination enums
#[derive(Clone, Debug)]
pub struct DestinationConfiguration {
    pub key: Option<String>,
    pub path: Option<String>,
    pub qos: Option<Qos>,
    pub retain: Option<Retain>,
    pub topic: Option<String>,
    pub ttl: Option<u64>,
}

// ~~~~~~~~~~~~~~~~~~~Asset Status DTDL Equivalent Structs~~~~~~~
#[derive(Clone, Debug, Default)]
/// Represents the observed status of an asset.
pub struct AssetStatus {
    /// The configuration of the asset.
    pub config: Option<StatusConfig>,
    /// A collection of datasets associated with the asset.
    pub datasets_schema: Option<Vec<AssetDatasetEventStreamStatus>>,
    /// A collection of events associated with the asset.
    pub events_schema: Option<Vec<AssetDatasetEventStreamStatus>>,
    /// A collection of management groups associated with the asset.
    pub management_groups: Option<Vec<AssetManagementGroupStatus>>,
    /// A collection of schema references for streams associated with the asset.
    pub streams: Option<Vec<AssetDatasetEventStreamStatus>>,
}

#[derive(Clone, Debug)]
/// Represents a schema to the dataset or event.
pub struct AssetDatasetEventStreamStatus {
    /// The name of the dataset or the event.
    pub name: String,
    /// The message schema associated with the dataset or event.
    pub message_schema_reference: Option<MessageSchemaReference>,
    /// An error associated with the dataset or event.
    pub error: Option<ConfigError>,
}

#[derive(Clone, Debug)]
/// Represents an asset management group
pub struct AssetManagementGroupStatus {
    /// A collection of actions associated with the management group.
    pub actions: Option<Vec<AssetManagementGroupActionStatus>>,
    /// The name of the management group.
    pub name: String,
}

#[derive(Clone, Debug)]
/// Represents an action associated with an asset management group.
pub struct AssetManagementGroupActionStatus {
    /// The configuration error of the management group action.
    pub error: Option<ConfigError>,
    /// The name of the management group action.
    pub name: String,
    /// The request message schema references for the management group action.
    pub request_message_schema_reference: Option<MessageSchemaReference>,
    /// The response message schema references for the management group action.
    pub response_message_schema_reference: Option<MessageSchemaReference>,
}

#[derive(Clone, Debug)]
/// Represents a reference to a schema, including its name, version, and namespace.
pub struct MessageSchemaReference {
    /// The name of the message schema.
    pub name: String,
    /// The version of the message schema.
    pub version: String,
    /// The namespace of the message schema.
    pub registry_namespace: String,
}

impl From<AssetStatus> for adr_name_gen::AssetStatus {
    fn from(value: AssetStatus) -> Self {
        adr_name_gen::AssetStatus {
            config: value.config.map(StatusConfig::into),
            datasets: option_vec_from(value.datasets_schema, AssetDatasetEventStreamStatus::into),
            events: option_vec_from(value.events_schema, AssetDatasetEventStreamStatus::into),
            management_groups: option_vec_from(
                value.management_groups,
                AssetManagementGroupStatus::into,
            ),
            streams: option_vec_from(value.streams, AssetDatasetEventStreamStatus::into),
        }
    }
}

impl From<AssetDatasetEventStreamStatus> for adr_name_gen::AssetDatasetEventStreamStatus {
    fn from(value: AssetDatasetEventStreamStatus) -> Self {
        adr_name_gen::AssetDatasetEventStreamStatus {
            name: value.name,
            message_schema_reference: value
                .message_schema_reference
                .map(MessageSchemaReference::into),
            error: value.error.map(ConfigError::into),
        }
    }
}

impl From<AssetManagementGroupStatus>
    for adr_name_gen::AssetManagementGroupStatusSchemaElementSchema
{
    fn from(value: AssetManagementGroupStatus) -> Self {
        adr_name_gen::AssetManagementGroupStatusSchemaElementSchema {
            actions: option_vec_from(value.actions, AssetManagementGroupActionStatus::into),
            name: value.name,
        }
    }
}

impl From<AssetManagementGroupActionStatus>
    for adr_name_gen::AssetManagementGroupActionStatusSchemaElementSchema
{
    fn from(value: AssetManagementGroupActionStatus) -> Self {
        adr_name_gen::AssetManagementGroupActionStatusSchemaElementSchema {
            error: value.error.map(ConfigError::into),
            name: value.name,
            request_message_schema_reference: value
                .request_message_schema_reference
                .map(MessageSchemaReference::into),
            response_message_schema_reference: value
                .response_message_schema_reference
                .map(MessageSchemaReference::into),
        }
    }
}

impl From<MessageSchemaReference> for adr_name_gen::MessageSchemaReference {
    fn from(value: MessageSchemaReference) -> Self {
        adr_name_gen::MessageSchemaReference {
            schema_name: value.name,
            schema_version: value.version,
            schema_registry_namespace: value.registry_namespace,
        }
    }
}

// ~~~~~~~~~~~~~~~~~~~Detected Asset DTDL Equivalent Structs~~~~~~~
#[derive(Clone, Debug)]
pub struct DetectedAsset {
    pub asset_endpoint_profile_ref: String,
    pub asset_name: Option<String>,
    pub datasets: Option<Vec<DetectedAssetDataset>>,
    pub default_datasets_configuration: Option<String>,
    pub default_events_configuration: Option<String>,
    pub default_topic: Option<Topic>,
    pub documentation_uri: Option<String>,
    pub events: Option<Vec<DetectedAssetEvent>>,
    pub hardware_revision: Option<String>,
    pub manufacturer: Option<String>,
    pub manufacturer_uri: Option<String>,
    pub model: Option<String>,
    pub product_code: Option<String>,
    pub serial_number: Option<String>,
    pub software_revision: Option<String>,
}

#[derive(Clone, Debug)]
pub struct DetectedAssetEvent {
    pub event_configuration: Option<String>,
    pub event_notifier: String,
    pub last_updated_on: Option<String>,
    pub name: String,
    pub topic: Option<Topic>,
}

#[derive(Clone, Debug)]
pub struct DetectedAssetDataset {
    pub data_points: Option<Vec<DetectedAssetDataPoint>>,
    pub data_set_configuration: Option<String>,
    pub name: String,
    pub topic: Option<Topic>,
}

#[derive(Clone, Debug)]
pub struct Topic {
    pub path: String,
    pub retain: Option<Retain>,
}

#[derive(Clone, Debug)]
pub struct DetectedAssetDataPoint {
    pub data_point_configuration: Option<String>,
    pub data_source: String,
    pub last_updated_on: Option<String>,
    pub name: Option<String>,
}

impl From<DetectedAssetDataset> for adr_name_gen::DetectedAssetDatasetSchemaElementSchema {
    fn from(value: DetectedAssetDataset) -> Self {
        adr_name_gen::DetectedAssetDatasetSchemaElementSchema {
            data_points: option_vec_from(value.data_points, DetectedAssetDataPoint::into),
            data_set_configuration: value.data_set_configuration,
            name: value.name,
            topic: value.topic.map(Topic::into),
        }
    }
}

impl From<Topic> for adr_name_gen::Topic {
    fn from(value: Topic) -> Self {
        adr_name_gen::Topic {
            path: value.path,
            retain: value.retain.map(Retain::into),
        }
    }
}

impl From<DetectedAssetDataPoint> for adr_name_gen::DetectedAssetDataPointSchemaElementSchema {
    fn from(value: DetectedAssetDataPoint) -> Self {
        adr_name_gen::DetectedAssetDataPointSchemaElementSchema {
            data_point_configuration: value.data_point_configuration,
            data_source: value.data_source,
            last_updated_on: value.last_updated_on,
            name: value.name,
        }
    }
}

// ~~~~~~~~~~~~~~~~~~~DTDL Equivalent Enums~~~~~~~
// TODO: remove in favor of Rust enum
#[derive(Clone, Debug)]
pub enum EventStreamTarget {
    Mqtt,
    Storage,
}

#[derive(Clone, Debug)]
pub enum Qos {
    Qos0,
    Qos1,
}

#[derive(Clone, Debug)]
pub enum Retain {
    Keep,
    Never,
}

// TODO: remove in favor of Rust enum
#[derive(Clone, Debug)]
pub enum DatasetTarget {
    BrokerStateStore,
    Mqtt,
    Storage,
}

#[derive(Clone, Debug)]
pub enum AssetManagementGroupActionType {
    Call,
    Read,
    Write,
}

impl From<Retain> for adr_name_gen::Retain {
    fn from(value: Retain) -> Self {
        match value {
            Retain::Keep => Self::Keep,
            Retain::Never => Self::Never,
        }
    }
}

impl From<adr_name_gen::Asset> for Asset {
    fn from(value: adr_name_gen::Asset) -> Self {
        Asset {
            name: value.name,
            specification: AssetSpecification::from(value.specification),
            status: value.status.map(AssetStatus::from),
        }
    }
}

impl From<adr_name_gen::AssetStatus> for AssetStatus {
    fn from(value: adr_name_gen::AssetStatus) -> Self {
        AssetStatus {
            config: value.config.map(StatusConfig::from),
            datasets_schema: option_vec_from(value.datasets, AssetDatasetEventStreamStatus::from),
            events_schema: option_vec_from(value.events, AssetDatasetEventStreamStatus::from),
            management_groups: option_vec_from(
                value.management_groups,
                AssetManagementGroupStatus::from,
            ),
            streams: option_vec_from(value.streams, AssetDatasetEventStreamStatus::from),
        }
    }
}

impl From<adr_name_gen::AssetManagementGroupStatusSchemaElementSchema>
    for AssetManagementGroupStatus
{
    fn from(value: adr_name_gen::AssetManagementGroupStatusSchemaElementSchema) -> Self {
        AssetManagementGroupStatus {
            actions: option_vec_from(value.actions, AssetManagementGroupActionStatus::from),
            name: value.name,
        }
    }
}

impl From<adr_name_gen::AssetManagementGroupActionStatusSchemaElementSchema>
    for AssetManagementGroupActionStatus
{
    fn from(value: adr_name_gen::AssetManagementGroupActionStatusSchemaElementSchema) -> Self {
        AssetManagementGroupActionStatus {
            error: value.error.map(ConfigError::from),
            name: value.name,
            request_message_schema_reference: value
                .request_message_schema_reference
                .map(MessageSchemaReference::from),
            response_message_schema_reference: value
                .response_message_schema_reference
                .map(MessageSchemaReference::from),
        }
    }
}

impl From<adr_name_gen::AssetDatasetEventStreamStatus> for AssetDatasetEventStreamStatus {
    fn from(value: adr_name_gen::AssetDatasetEventStreamStatus) -> Self {
        AssetDatasetEventStreamStatus {
            name: value.name,
            message_schema_reference: value
                .message_schema_reference
                .map(MessageSchemaReference::from),
            error: value.error.map(ConfigError::from),
        }
    }
}

impl From<adr_name_gen::MessageSchemaReference> for MessageSchemaReference {
    fn from(value: adr_name_gen::MessageSchemaReference) -> Self {
        MessageSchemaReference {
            name: value.schema_name,
            version: value.schema_version,
            registry_namespace: value.schema_registry_namespace,
        }
    }
}

impl From<adr_name_gen::AssetSpecificationSchema> for AssetSpecification {
    fn from(value: adr_name_gen::AssetSpecificationSchema) -> Self {
        AssetSpecification {
            asset_type_refs: value.asset_type_refs.unwrap_or_default(),
            attributes: value.attributes.unwrap_or_default(),
            datasets: vec_from_option_vec(value.datasets, AssetDataset::from),
            default_datasets_configuration: value.default_datasets_configuration,
            default_datasets_destinations: vec_from_option_vec(
                value.default_datasets_destinations,
                AssetDatasetsDestination::from,
            ),
            default_events_configuration: value.default_events_configuration,
            default_events_destinations: vec_from_option_vec(
                value.default_events_destinations,
                EventsAndStreamsDestination::from,
            ),
            default_management_groups_configuration: value.default_management_groups_configuration,
            default_streams_configuration: value.default_streams_configuration,
            default_streams_destinations: vec_from_option_vec(
                value.default_streams_destinations,
                EventsAndStreamsDestination::from,
            ),
            description: value.description,
            device_ref: DeviceRef::from(value.device_ref),
            discovered_asset_refs: value.discovered_asset_refs.unwrap_or_default(),
            display_name: value.display_name,
            documentation_uri: value.documentation_uri,
            enabled: value.enabled,
            events: vec_from_option_vec(value.events, AssetEvent::from),
            external_asset_id: value.external_asset_id,
            hardware_revision: value.hardware_revision,
            last_transition_time: value.last_transition_time,
            management_groups: vec_from_option_vec(
                value.management_groups,
                AssetManagementGroup::from,
            ),
            manufacturer: value.manufacturer,
            manufacturer_uri: value.manufacturer_uri,
            model: value.model,
            product_code: value.product_code,
            serial_number: value.serial_number,
            software_revision: value.software_revision,
            streams: vec_from_option_vec(value.streams, AssetStream::from),
            uuid: value.uuid,
            version: value.version,
        }
    }
}

impl From<adr_name_gen::AssetDatasetSchemaElementSchema> for AssetDataset {
    fn from(value: adr_name_gen::AssetDatasetSchemaElementSchema) -> Self {
        AssetDataset {
            data_points: vec_from_option_vec(value.data_points, AssetDatasetDataPoint::from),
            data_source: value.data_source,
            destinations: vec_from_option_vec(value.destinations, AssetDatasetsDestination::from),
            name: value.name,
            type_ref: value.type_ref,
        }
    }
}

impl From<adr_name_gen::AssetDatasetDataPointSchemaElementSchema> for AssetDatasetDataPoint {
    fn from(value: adr_name_gen::AssetDatasetDataPointSchemaElementSchema) -> Self {
        AssetDatasetDataPoint {
            data_point_configuration: value.data_point_configuration,
            data_source: value.data_source,
            name: value.name,
            type_ref: value.type_ref,
        }
    }
}

impl From<adr_name_gen::AssetDatasetDestinationSchemaElementSchema> for AssetDatasetsDestination {
    fn from(value: adr_name_gen::AssetDatasetDestinationSchemaElementSchema) -> Self {
        AssetDatasetsDestination {
            configuration: value.configuration.into(),
            target: value.target.into(),
        }
    }
}

impl From<adr_name_gen::DefaultDatasetsDestinationsSchemaElementSchema>
    for AssetDatasetsDestination
{
    fn from(value: adr_name_gen::DefaultDatasetsDestinationsSchemaElementSchema) -> Self {
        AssetDatasetsDestination {
            configuration: value.configuration.into(),
            target: value.target.into(),
        }
    }
}

impl From<adr_name_gen::DefaultEventsDestinationsSchemaElementSchema>
    for EventsAndStreamsDestination
{
    fn from(value: adr_name_gen::DefaultEventsDestinationsSchemaElementSchema) -> Self {
        EventsAndStreamsDestination {
            configuration: value.configuration.into(),
            target: value.target.into(),
        }
    }
}

impl From<adr_name_gen::DefaultStreamsDestinationsSchemaElementSchema>
    for EventsAndStreamsDestination
{
    fn from(value: adr_name_gen::DefaultStreamsDestinationsSchemaElementSchema) -> Self {
        EventsAndStreamsDestination {
            configuration: value.configuration.into(),
            target: value.target.into(),
        }
    }
}

impl From<adr_name_gen::DeviceRefSchema> for DeviceRef {
    fn from(value: adr_name_gen::DeviceRefSchema) -> Self {
        DeviceRef {
            device_name: value.device_name,
            endpoint_name: value.endpoint_name,
        }
    }
}

impl From<adr_name_gen::AssetEventSchemaElementSchema> for AssetEvent {
    fn from(value: adr_name_gen::AssetEventSchemaElementSchema) -> Self {
        AssetEvent {
            data_points: vec_from_option_vec(value.data_points, AssetEventDataPoint::from),
            destinations: vec_from_option_vec(
                value.destinations,
                EventsAndStreamsDestination::from,
            ),
            event_configuration: value.event_configuration,
            event_notifier: value.event_notifier,
            name: value.name,
            type_ref: value.type_ref,
        }
    }
}

impl From<adr_name_gen::AssetEventDestinationSchemaElementSchema> for EventsAndStreamsDestination {
    fn from(value: adr_name_gen::AssetEventDestinationSchemaElementSchema) -> Self {
        EventsAndStreamsDestination {
            configuration: value.configuration.into(),
            target: value.target.into(),
        }
    }
}

impl From<adr_name_gen::AssetEventDataPointSchemaElementSchema> for AssetEventDataPoint {
    fn from(value: adr_name_gen::AssetEventDataPointSchemaElementSchema) -> Self {
        AssetEventDataPoint {
            data_point_configuration: value.data_point_configuration,
            data_source: value.data_source,
            name: value.name,
        }
    }
}

impl From<adr_name_gen::AssetManagementGroupSchemaElementSchema> for AssetManagementGroup {
    fn from(value: adr_name_gen::AssetManagementGroupSchemaElementSchema) -> Self {
        AssetManagementGroup {
            actions: vec_from_option_vec(value.actions, AssetManagementGroupAction::from),
            default_time_out_in_seconds: value.default_time_out_in_seconds,
            default_topic: value.default_topic,
            management_group_configuration: value.management_group_configuration,
            name: value.name,
            type_ref: value.type_ref,
        }
    }
}

impl From<adr_name_gen::AssetManagementGroupActionSchemaElementSchema>
    for AssetManagementGroupAction
{
    fn from(value: adr_name_gen::AssetManagementGroupActionSchemaElementSchema) -> Self {
        AssetManagementGroupAction {
            action_configuration: value.action_configuration,
            action_type: value.action_type.into(),
            name: value.name,
            target_uri: value.target_uri,
            time_out_in_seconds: value.time_out_in_seconds,
            topic: value.topic,
            type_ref: value.type_ref,
        }
    }
}

impl From<adr_name_gen::AssetManagementGroupActionTypeSchema> for AssetManagementGroupActionType {
    fn from(value: adr_name_gen::AssetManagementGroupActionTypeSchema) -> Self {
        match value {
            adr_name_gen::AssetManagementGroupActionTypeSchema::Call => {
                AssetManagementGroupActionType::Call
            }
            adr_name_gen::AssetManagementGroupActionTypeSchema::Read => {
                AssetManagementGroupActionType::Read
            }
            adr_name_gen::AssetManagementGroupActionTypeSchema::Write => {
                AssetManagementGroupActionType::Write
            }
        }
    }
}
impl From<adr_name_gen::AssetStreamSchemaElementSchema> for AssetStream {
    fn from(value: adr_name_gen::AssetStreamSchemaElementSchema) -> Self {
        AssetStream {
            destinations: vec_from_option_vec(
                value.destinations,
                EventsAndStreamsDestination::from,
            ),
            name: value.name,
            stream_configuration: value.stream_configuration,
            type_ref: value.type_ref,
        }
    }
}

impl From<adr_name_gen::AssetStreamDestinationSchemaElementSchema> for EventsAndStreamsDestination {
    fn from(value: adr_name_gen::AssetStreamDestinationSchemaElementSchema) -> Self {
        EventsAndStreamsDestination {
            configuration: value.configuration.into(),
            target: value.target.into(),
        }
    }
}

impl From<adr_name_gen::DestinationConfiguration> for DestinationConfiguration {
    fn from(value: adr_name_gen::DestinationConfiguration) -> Self {
        DestinationConfiguration {
            key: value.key,
            path: value.path,
            qos: value.qos.map(Qos::from),
            retain: value.retain.map(Retain::from),
            topic: value.topic,
            ttl: value.ttl,
        }
    }
}

impl From<adr_name_gen::EventStreamTarget> for EventStreamTarget {
    fn from(value: adr_name_gen::EventStreamTarget) -> Self {
        match value {
            adr_name_gen::EventStreamTarget::Mqtt => EventStreamTarget::Mqtt,
            adr_name_gen::EventStreamTarget::Storage => EventStreamTarget::Storage,
        }
    }
}

impl From<adr_name_gen::DatasetTarget> for DatasetTarget {
    fn from(value: adr_name_gen::DatasetTarget) -> Self {
        match value {
            adr_name_gen::DatasetTarget::BrokerStateStore => DatasetTarget::BrokerStateStore,
            adr_name_gen::DatasetTarget::Mqtt => DatasetTarget::Mqtt,
            adr_name_gen::DatasetTarget::Storage => DatasetTarget::Storage,
        }
    }
}

impl From<adr_name_gen::Qos> for Qos {
    fn from(value: adr_name_gen::Qos) -> Self {
        match value {
            adr_name_gen::Qos::Qos0 => Qos::Qos0,
            adr_name_gen::Qos::Qos1 => Qos::Qos1,
        }
    }
}

impl From<adr_name_gen::Retain> for Retain {
    fn from(value: adr_name_gen::Retain) -> Self {
        match value {
            adr_name_gen::Retain::Keep => Retain::Keep,
            adr_name_gen::Retain::Never => Retain::Never,
        }
    }
}
