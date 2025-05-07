// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

//! Types for Azure Device Registry operations.

use core::fmt::Debug;
use std::collections::HashMap;

use azure_iot_operations_mqtt::control_packet::QoS as rumqttc_qos;
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
    /// Client Id used for the ADR Client was invalid.
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
    /// A message describing the error returned by the Azure Device Registry Service.
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
pub struct DeviceUpdateObservation(Receiver<(Device, Option<AckToken>)>);

impl DeviceUpdateObservation {
    /// Receives an updated [`Device`] or [`None`] if there will be no more notifications.
    ///
    /// If there are notifications:
    /// - Returns Some([`Device`], [`Option<AckToken>`]) on success
    ///     - If auto ack is disabled, the [`AckToken`] should be used or dropped when you want the ack to occur. If auto ack is enabled, you may use ([`Device`], _) to ignore the [`AckToken`].
    ///
    /// A received notification can be acknowledged via the [`AckToken`] by calling [`AckToken::ack`] or dropping the [`AckToken`].
    pub async fn recv_notification(&mut self) -> Option<(Device, Option<AckToken>)> {
        self.0.recv().await
    }
}

// ~~~~~~~~~~~~~~~~~~~SDK Created Asset Structs~~~~~~~~~~~~~
/// A struct to manage receiving notifications for a asset
#[derive(Debug)]
pub struct AssetUpdateObservation(Receiver<(Asset, Option<AckToken>)>);

impl AssetUpdateObservation {
    /// Receives an updated [`Asset`] or [`None`] if there will be no more notifications.
    ///
    /// If there are notifications:
    /// - Returns Some([`Asset`], [`Option<AckToken>`]) on success
    ///     - If auto ack is disabled, the [`AckToken`] should be used or dropped when you want the ack to occur. If auto ack is enabled, you may use ([`Asset`], _) to ignore the [`AckToken`].
    ///
    /// A received notification can be acknowledged via the [`AckToken`] by calling [`AckToken::ack`] or dropping the [`AckToken`].
    pub async fn recv_notification(&mut self) -> Option<(Asset, Option<AckToken>)> {
        self.0.recv().await
    }
}

// ~~~~~~~~~~~~~~~~~~~Helper fns ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
/// Helper fn to convert `Option<Vec<T>>` to `Option<Vec<U>>`
fn option_vec_from<T, U>(source: Option<Vec<T>>, into_fn: impl Fn(T) -> U) -> Option<Vec<U>> {
    source.map(|vec| vec.into_iter().map(into_fn).collect())
}

/// Helper fn to convert `Option<Vec<T>>` to `Vec<U>`, where `Vec<U>` will be an empty Vec if source was `None`
fn vec_from_option_vec<T, U>(source: Option<Vec<T>>, into_fn: impl Fn(T) -> U) -> Vec<U> {
    source.map_or(vec![], |vec| vec.into_iter().map(into_fn).collect())
}

// ~~~~~~~~~~~~~~~~~~~Common DTDL Equivalent Structs~~~~~~~~~~~~~
#[derive(Clone, Debug, Default, PartialEq)]
/// Represents the configuration status.
pub struct StatusConfig {
    /// Error details for status.
    pub error: Option<ConfigError>,
    /// The last time the configuration has been modified.
    pub last_transition_time: Option<String>,
    /// The version of the Device or Asset configuration.
    pub version: Option<u64>,
}

#[derive(Clone, Debug, Default, PartialEq)]
/// Represents an error in the configuration of an asset or device.
pub struct ConfigError {
    /// Error code for classification of errors (ex: ''400'', ''404'', ''500'', etc.).
    pub code: Option<String>,
    /// Array of event statuses that describe the status of each event.
    pub details: Option<Vec<Details>>,
    /// The inner error, if any.
    pub inner_error: Option<HashMap<String, String>>,
    /// The message of the error.
    pub message: Option<String>,
}

#[derive(Clone, Debug, PartialEq, Default)]
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
/// Represents the specification of a device in the Azure Device Registry service.
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
    pub last_transition_time: Option<String>, // TODO DateTime?
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
/// Represents the endpoints of a device in the Azure Device Registry service.
pub struct DeviceEndpoints {
    /// The 'inbound' Field.
    pub inbound: HashMap<String, InboundEndpoint>, // if None, we can represent as empty hashmap. Might be able to change this to a single InboundEndpoint
    /// The 'outbound' Field.
    pub outbound_assigned: HashMap<String, OutboundEndpoint>,
    /// The 'outboundUnassigned' Field.
    pub outbound_unassigned: HashMap<String, OutboundEndpoint>,
}

/// Represents an outbound endpoint of a device in the Azure Device Registry service.
#[derive(Debug, Clone)]
pub struct OutboundEndpoint {
    /// The 'address' Field.
    pub address: String,
    /// The 'endpointType' Field.
    pub endpoint_type: Option<String>,
}

/// Represents an inbound endpoint of a device in the Azure Device Registry service.
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
/// Represents the trust settings for an endpoint.
pub struct TrustSettings {
    /// The 'issuerList' Field.
    pub issuer_list: Option<String>,
    /// The 'trustList' Field.
    pub trust_list: Option<String>,
}

#[derive(Debug, Clone, Default)]
/// Represents the authentication method for an endpoint.
pub enum Authentication {
    #[default]
    /// Represents anonymous authentication.
    Anonymous,
    /// Represents authentication using a certificate.
    Certificate {
        /// The 'certificateSecretName' Field.
        certificate_secret_name: String,
    },
    /// Represents authentication using a username and password.
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
        let mut outbound_assigned = HashMap::new();
        let mut outbound_unassigned = HashMap::new();

        if let Some(outbound) = value.outbound {
            for (k, v) in outbound.assigned {
                outbound_assigned.insert(k, OutboundEndpoint::from(v));
            }

            if let Some(map) = outbound.unassigned {
                for (k, v) in map {
                    outbound_unassigned.insert(k, OutboundEndpoint::from(v));
                }
            }
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
            adr_name_gen::MethodSchema::Certificate => Authentication::Certificate {
                certificate_secret_name: if let Some(x509credentials) = value.x509credentials {
                    x509credentials.certificate_secret_name
                } else {
                    log::error!(
                        "Authentication method 'Certificate', but no 'x509Credentials' provided"
                    );
                    String::new()
                },
            },

            adr_name_gen::MethodSchema::UsernamePassword => {
                if let Some(username_password_credentials) = value.username_password_credentials {
                    Authentication::UsernamePassword {
                        password_secret_name: username_password_credentials.password_secret_name,
                        username_secret_name: username_password_credentials.username_secret_name,
                    }
                } else {
                    log::error!(
                        "Authentication method 'UsernamePassword', but no 'usernamePasswordCredentials' provided"
                    );

                    Authentication::UsernamePassword {
                        password_secret_name: String::new(),
                        username_secret_name: String::new(),
                    }
                }
            }
        }
    }
}

// ~~~~~~~~~~~~~~~~~~~Device Endpoint Status DTDL Equivalent Structs~~~~
#[derive(Clone, Debug, Default, PartialEq)]
/// Represents the observed status of a Device in the ADR Service.
pub struct DeviceStatus {
    ///  Defines the status config properties.
    pub config: Option<StatusConfig>,
    /// Defines the device status for inbound/outbound endpoints.
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

/// Represents an Asset in the Azure Device Registry service.
#[derive(Clone, Debug)]
pub struct Asset {
    /// The name of the asset.
    pub name: String,
    /// The 'specification' Field.
    pub specification: AssetSpecification,
    /// The 'status' Field.
    pub status: Option<AssetStatus>,
}

/// Represents the specification of an Asset in the Azure Device Registry service.
#[derive(Clone, Debug)]
pub struct AssetSpecification {
    /// URI or type definition ids.
    pub asset_type_refs: Vec<String>, // if None, we can represent as empty vec. Can currently only be length of 1
    /// A set of key-value pairs that contain custom attributes
    pub attributes: HashMap<String, String>, // if None, we can represent as empty hashmap
    /// Array of datasets that are part of the asset.
    pub datasets: Vec<Dataset>, // if None, we can represent as empty vec
    /// Default configuration for datasets.
    pub default_datasets_configuration: Option<String>,
    /// Default destinations for datasets.
    pub default_datasets_destinations: Vec<DatasetDestination>, // if None, we can represent as empty vec.  Can currently only be length of 1
    /// Default configuration for events.
    pub default_events_configuration: Option<String>,
    /// Default destinations for events.
    pub default_events_destinations: Vec<EventsAndStreamsDestination>, // if None, we can represent as empty vec.  Can currently only be length of 1
    /// Default configuration for management groups.
    pub default_management_groups_configuration: Option<String>,
    /// Default configuration for streams.
    pub default_streams_configuration: Option<String>,
    /// Default destinations for streams.
    pub default_streams_destinations: Vec<EventsAndStreamsDestination>, // if None, we can represent as empty vec. Can currently only be length of 1
    /// The description of the asset.
    pub description: Option<String>,
    /// A reference to the Device and Endpoint within the device
    pub device_ref: DeviceRef,
    /// Reference to a list of discovered assets
    pub discovered_asset_refs: Vec<String>, // if None, we can represent as empty vec
    /// The display name of the asset.
    pub display_name: Option<String>,
    /// Reference to the documentation.
    pub documentation_uri: Option<String>,
    /// Enabled/Disabled status of the asset.
    pub enabled: Option<bool>, // TODO: just bool?
    ///  Array of events that are part of the asset.
    pub events: Vec<Event>, // if None, we can represent as empty vec
    /// Asset id provided by the customer.
    pub external_asset_id: Option<String>,
    /// Revision number of the hardware.
    pub hardware_revision: Option<String>,
    /// The last time the asset has been modified.
    pub last_transition_time: Option<String>,
    /// Array of management groups that are part of the asset.
    pub management_groups: Vec<ManagementGroup>, // if None, we can represent as empty vec
    /// The name of the manufacturer.
    pub manufacturer: Option<String>,
    /// The URI of the manufacturer.
    pub manufacturer_uri: Option<String>,
    /// The model of the asset.
    pub model: Option<String>,
    /// The product code of the asset.
    pub product_code: Option<String>,
    /// The revision number of the software.
    pub serial_number: Option<String>,
    /// The revision number of the software.
    pub software_revision: Option<String>,
    /// Array of streams that are part of the asset.
    pub streams: Vec<Stream>, // if None, we can represent as empty vec
    ///  Globally unique, immutable, non-reusable id.
    pub uuid: Option<String>,
    /// The version of the asset.
    pub version: Option<u64>,
}

/// Represents a dataset.
#[derive(Clone, Debug)]
pub struct Dataset {
    /// Configuration for the dataset.
    pub dataset_configuration: Option<String>,
    /// Array of data points that are part of the dataset.
    pub data_points: Vec<DatasetDataPoint>, // if None, we can represent as empty vec
    /// The address of the source of the data in the dataset
    pub data_source: Option<String>,
    /// Destinations for a dataset.
    pub destinations: Vec<DatasetDestination>, // if None, we can represent as empty vec. Can currently only be length of 1
    /// The name of the dataset.
    pub name: String,
    /// Type definition id or URI of the dataset
    pub type_ref: Option<String>,
}

/// Represents a data point in a dataset.
#[derive(Clone, Debug)]
pub struct DatasetDataPoint {
    /// Configuration for the data point
    pub data_point_configuration: Option<String>,
    /// The data source for the data point
    pub data_source: String,
    /// The name of the data point
    pub name: String,
    /// URI or type definition id
    pub type_ref: Option<String>,
}

/// Represents the destination for a dataset.
#[derive(Clone, Debug)]
pub struct DatasetDestination {
    /// The configuration for the destination
    pub configuration: DestinationConfiguration,
    /// The target for the destination
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

/// Represents the destination for an event or stream.
#[derive(Clone, Debug)]
pub struct EventsAndStreamsDestination {
    /// The configuration for the destination
    pub configuration: DestinationConfiguration,
    /// The target for the destination
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

/// A reference to the Device and Endpoint within the device
#[derive(Clone, Debug)]
pub struct DeviceRef {
    /// The name of the device
    pub device_name: String,
    /// The endpoint name of the device
    pub endpoint_name: String,
}

/// Represents an event in an asset.
#[derive(Clone, Debug)]
pub struct Event {
    /// Array of data points that are part of the event.
    pub data_points: Vec<EventDataPoint>, // if None, we can represent as empty vec
    /// The destination for the event.
    pub destinations: Vec<EventsAndStreamsDestination>, // if None, we can represent as empty vec. Can currently only be length of 1
    /// The configuration for the event.
    pub event_configuration: Option<String>,
    /// The address of the notifier of the event
    pub event_notifier: String,
    /// The name of the event.
    pub name: String,
    /// URI or type definition id of the event
    pub type_ref: Option<String>,
}

/// Represents a management group
#[derive(Clone, Debug)]
pub struct ManagementGroup {
    /// Actions for this management group
    pub actions: Vec<ManagementGroupAction>, // if None, we can represent as empty vec
    /// Default timeout in seconds for this management group
    pub default_time_out_in_seconds: Option<u32>,
    /// The default MQTT topic for the management group.
    pub default_topic: Option<String>,
    /// Configuration for the management group.
    pub management_group_configuration: Option<String>,
    /// The name of the management group.
    pub name: String,
    /// URI or type definition id of the management group
    pub type_ref: Option<String>,
}

/// Represents a management group action
#[derive(Clone, Debug)]
pub struct ManagementGroupAction {
    /// Configuration for the action.
    pub action_configuration: Option<String>,
    /// Type of action.
    pub action_type: ActionType,
    /// The name of the action.
    pub name: String,
    /// The target URI for the action.
    pub target_uri: String,
    /// The timeout for the action.
    pub time_out_in_seconds: Option<u32>,
    /// The MQTT topic for the action.
    pub topic: Option<String>,
    /// URI or type definition id of the management group action
    pub type_ref: Option<String>,
}

/// Represents a stream for an asset.
#[derive(Clone, Debug)]
pub struct Stream {
    /// Destinations for a stream.
    pub destinations: Vec<EventsAndStreamsDestination>, // if None, we can represent as empty vec. Can currently only be length of 1
    /// The name of the stream.
    pub name: String,
    /// The configuration for the stream.
    pub stream_configuration: Option<String>,
    /// URI or type definition id of the stream
    pub type_ref: Option<String>,
}

/// A data point in an event.
#[derive(Clone, Debug)]
pub struct EventDataPoint {
    /// The configuration for the data point in the event.
    pub data_point_configuration: Option<String>,
    /// The data source for the data point in the event.
    pub data_source: String,
    /// The name of the data point in the event.
    pub name: String,
}

// TODO: turn into rust enums for which of these options can correlate to which destination enums
/// The configuration for the destination
#[derive(Clone, Debug)]
pub struct DestinationConfiguration {
    /// The key of the destination configuration.
    pub key: Option<String>,
    /// The description of the destination configuration.
    pub path: Option<String>,
    /// The MQTT `QoS` setting for the destination configuration.
    pub qos: Option<rumqttc_qos>,
    /// The MQTT retain setting for the destination configuration.
    pub retain: Option<Retain>,
    /// The MQTT topic for the destination configuration.
    pub topic: Option<String>,
    /// The MQTT TTL setting for the destination configuration.
    pub ttl: Option<u64>,
}

// ~~~~~~~~~~~~~~~~~~~Asset Status DTDL Equivalent Structs~~~~~~~
#[derive(Clone, Debug, Default)]
/// Represents the observed status of an asset.
pub struct AssetStatus {
    /// The configuration of the asset.
    pub config: Option<StatusConfig>,
    /// A collection of datasets associated with the asset.
    pub datasets: Option<Vec<DatasetEventStreamStatus>>,
    /// A collection of events associated with the asset.
    pub events: Option<Vec<DatasetEventStreamStatus>>,
    /// A collection of management groups associated with the asset.
    pub management_groups: Option<Vec<ManagementGroupStatus>>,
    /// A collection of schema references for streams associated with the asset.
    pub streams: Option<Vec<DatasetEventStreamStatus>>,
}

#[derive(Clone, Debug)]
/// Represents the status for a dataset, event, or stream.
pub struct DatasetEventStreamStatus {
    /// The name of the dataset, event, or stream.
    pub name: String,
    /// The message schema associated with the dataset, event, or stream.
    pub message_schema_reference: Option<MessageSchemaReference>,
    /// An error associated with the dataset, event, or stream.
    pub error: Option<ConfigError>,
}

#[derive(Clone, Debug)]
/// Represents the status for a management group
pub struct ManagementGroupStatus {
    /// A collection of actions associated with the management group.
    pub actions: Option<Vec<ActionStatus>>,
    /// The name of the management group.
    pub name: String,
}

#[derive(Clone, Debug)]
/// Represents the status for an action associated with a management group.
pub struct ActionStatus {
    /// The configuration error of the management group action.
    pub error: Option<ConfigError>,
    /// The name of the management group action.
    pub name: String,
    /// The request message schema reference for the management group action.
    pub request_message_schema_reference: Option<MessageSchemaReference>,
    /// The response message schema reference for the management group action.
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
            datasets: option_vec_from(value.datasets, DatasetEventStreamStatus::into),
            events: option_vec_from(value.events, DatasetEventStreamStatus::into),
            management_groups: option_vec_from(
                value.management_groups,
                ManagementGroupStatus::into,
            ),
            streams: option_vec_from(value.streams, DatasetEventStreamStatus::into),
        }
    }
}

impl From<DatasetEventStreamStatus> for adr_name_gen::AssetDatasetEventStreamStatus {
    fn from(value: DatasetEventStreamStatus) -> Self {
        adr_name_gen::AssetDatasetEventStreamStatus {
            name: value.name,
            message_schema_reference: value
                .message_schema_reference
                .map(MessageSchemaReference::into),
            error: value.error.map(ConfigError::into),
        }
    }
}

impl From<ManagementGroupStatus> for adr_name_gen::AssetManagementGroupStatusSchemaElementSchema {
    fn from(value: ManagementGroupStatus) -> Self {
        adr_name_gen::AssetManagementGroupStatusSchemaElementSchema {
            actions: option_vec_from(value.actions, ActionStatus::into),
            name: value.name,
        }
    }
}

impl From<ActionStatus> for adr_name_gen::AssetManagementGroupActionStatusSchemaElementSchema {
    fn from(value: ActionStatus) -> Self {
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

// ~~~~~~~~~~~~~~~~~~~DTDL Equivalent Enums~~~~~~~
// TODO: remove in favor of Rust enum
/// The target of the event or stream.
#[derive(Clone, Debug)]
pub enum EventStreamTarget {
    /// MQTT
    Mqtt,
    /// Storage
    Storage,
}

#[derive(Clone, Debug)]
/// Represents the retain policy.
pub enum Retain {
    /// Should be retained.
    Keep,
    /// Should not be retained.
    Never,
}

// TODO: remove in favor of Rust enum
#[derive(Clone, Debug)]
/// Represents the target type for a dataset.
pub enum DatasetTarget {
    /// Represents a broker state store dataset target.
    BrokerStateStore,
    /// Represents a MQTT dataset target.
    Mqtt,
    /// Represents a storage dataset target.
    Storage,
}

#[derive(Clone, Debug)]
/// Represents the type of action that can be performed in an asset management group.
pub enum ActionType {
    /// Represents a call action type.
    Call,
    /// Represents a read action type.
    Read,
    /// Represents a write action type.
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
            datasets: option_vec_from(value.datasets, DatasetEventStreamStatus::from),
            events: option_vec_from(value.events, DatasetEventStreamStatus::from),
            management_groups: option_vec_from(
                value.management_groups,
                ManagementGroupStatus::from,
            ),
            streams: option_vec_from(value.streams, DatasetEventStreamStatus::from),
        }
    }
}

impl From<adr_name_gen::AssetManagementGroupStatusSchemaElementSchema> for ManagementGroupStatus {
    fn from(value: adr_name_gen::AssetManagementGroupStatusSchemaElementSchema) -> Self {
        ManagementGroupStatus {
            actions: option_vec_from(value.actions, ActionStatus::from),
            name: value.name,
        }
    }
}

impl From<adr_name_gen::AssetManagementGroupActionStatusSchemaElementSchema> for ActionStatus {
    fn from(value: adr_name_gen::AssetManagementGroupActionStatusSchemaElementSchema) -> Self {
        ActionStatus {
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

impl From<adr_name_gen::AssetDatasetEventStreamStatus> for DatasetEventStreamStatus {
    fn from(value: adr_name_gen::AssetDatasetEventStreamStatus) -> Self {
        DatasetEventStreamStatus {
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
            datasets: vec_from_option_vec(value.datasets, Dataset::from),
            default_datasets_configuration: value.default_datasets_configuration,
            default_datasets_destinations: vec_from_option_vec(
                value.default_datasets_destinations,
                DatasetDestination::from,
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
            events: vec_from_option_vec(value.events, Event::from),
            external_asset_id: value.external_asset_id,
            hardware_revision: value.hardware_revision,
            last_transition_time: value.last_transition_time,
            management_groups: vec_from_option_vec(value.management_groups, ManagementGroup::from),
            manufacturer: value.manufacturer,
            manufacturer_uri: value.manufacturer_uri,
            model: value.model,
            product_code: value.product_code,
            serial_number: value.serial_number,
            software_revision: value.software_revision,
            streams: vec_from_option_vec(value.streams, Stream::from),
            uuid: value.uuid,
            version: value.version,
        }
    }
}

impl From<adr_name_gen::AssetDatasetSchemaElementSchema> for Dataset {
    fn from(value: adr_name_gen::AssetDatasetSchemaElementSchema) -> Self {
        Dataset {
            dataset_configuration: value.dataset_configuration,
            data_points: vec_from_option_vec(value.data_points, DatasetDataPoint::from),
            data_source: value.data_source,
            destinations: vec_from_option_vec(value.destinations, DatasetDestination::from),
            name: value.name,
            type_ref: value.type_ref,
        }
    }
}

impl From<adr_name_gen::AssetDatasetDataPointSchemaElementSchema> for DatasetDataPoint {
    fn from(value: adr_name_gen::AssetDatasetDataPointSchemaElementSchema) -> Self {
        DatasetDataPoint {
            data_point_configuration: value.data_point_configuration,
            data_source: value.data_source,
            name: value.name,
            type_ref: value.type_ref,
        }
    }
}

impl From<adr_name_gen::AssetDatasetDestinationSchemaElementSchema> for DatasetDestination {
    fn from(value: adr_name_gen::AssetDatasetDestinationSchemaElementSchema) -> Self {
        DatasetDestination {
            configuration: value.configuration.into(),
            target: value.target.into(),
        }
    }
}

impl From<adr_name_gen::DefaultDatasetsDestinationsSchemaElementSchema> for DatasetDestination {
    fn from(value: adr_name_gen::DefaultDatasetsDestinationsSchemaElementSchema) -> Self {
        DatasetDestination {
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

impl From<adr_name_gen::AssetEventSchemaElementSchema> for Event {
    fn from(value: adr_name_gen::AssetEventSchemaElementSchema) -> Self {
        Event {
            data_points: vec_from_option_vec(value.data_points, EventDataPoint::from),
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

impl From<adr_name_gen::AssetEventDataPointSchemaElementSchema> for EventDataPoint {
    fn from(value: adr_name_gen::AssetEventDataPointSchemaElementSchema) -> Self {
        EventDataPoint {
            data_point_configuration: value.data_point_configuration,
            data_source: value.data_source,
            name: value.name,
        }
    }
}

impl From<adr_name_gen::AssetManagementGroupSchemaElementSchema> for ManagementGroup {
    fn from(value: adr_name_gen::AssetManagementGroupSchemaElementSchema) -> Self {
        ManagementGroup {
            actions: vec_from_option_vec(value.actions, ManagementGroupAction::from),
            default_time_out_in_seconds: value.default_time_out_in_seconds,
            default_topic: value.default_topic,
            management_group_configuration: value.management_group_configuration,
            name: value.name,
            type_ref: value.type_ref,
        }
    }
}

impl From<adr_name_gen::AssetManagementGroupActionSchemaElementSchema> for ManagementGroupAction {
    fn from(value: adr_name_gen::AssetManagementGroupActionSchemaElementSchema) -> Self {
        ManagementGroupAction {
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

impl From<adr_name_gen::AssetManagementGroupActionTypeSchema> for ActionType {
    fn from(value: adr_name_gen::AssetManagementGroupActionTypeSchema) -> Self {
        match value {
            adr_name_gen::AssetManagementGroupActionTypeSchema::Call => ActionType::Call,
            adr_name_gen::AssetManagementGroupActionTypeSchema::Read => ActionType::Read,
            adr_name_gen::AssetManagementGroupActionTypeSchema::Write => ActionType::Write,
        }
    }
}
impl From<adr_name_gen::AssetStreamSchemaElementSchema> for Stream {
    fn from(value: adr_name_gen::AssetStreamSchemaElementSchema) -> Self {
        Stream {
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
            qos: value.qos.map(rumqttc_qos::from),
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

impl From<adr_name_gen::Qos> for azure_iot_operations_mqtt::control_packet::QoS {
    fn from(value: adr_name_gen::Qos) -> Self {
        match value {
            adr_name_gen::Qos::Qos0 => rumqttc_qos::AtMostOnce,
            adr_name_gen::Qos::Qos1 => rumqttc_qos::AtLeastOnce,
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
