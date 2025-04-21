// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

// imports section (TODO: remove this comment)

//! Types for Azure Device Registry operations.

use core::fmt::Debug;
use std::collections::HashMap;

use azure_iot_operations_mqtt::interface::AckToken;

use crate::azure_device_registry::device_name_gen::adr_base_service::client as adr_name_gen;
use crate::common::dispatcher::Receiver;

/// Azure Device Registry Client implementation wrapper
mod client;
/// Azure Device Registry generated code
mod device_name_gen;

pub use client::Client;

// ~~~~~~~~~~~~~~~~~~~SDK Created Structs~~~~~~~~~~~~~~~~~~~~~~~~
pub struct Error {}

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

/// A struct to manage receiving notifications for a asset
#[derive(Debug)]
pub struct AssetUpdateObservation {
    /// The name of the asset (for convenience)
    pub name: String,
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

// ~~~~~~~~~~~~~~~~~~~Common DTDL Equivalent Structs~~~~~~~~~~~~~
#[derive(Clone, Debug, Default)]
pub struct StatusConfig {
    pub version: Option<u64>,
    pub error: Option<ConfigError>,
    pub last_transition_time: Option<String>,
}

#[derive(Clone, Debug)]
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
    pub inbound_endpoints: HashMap<String, InboundEndpoint>, // if None, we can represent as empty hashmap. Might be able to change this to a single InboundEndpoint
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

#[derive(Debug, Clone)]
pub struct InboundEndpoint {
    /// The 'additionalConfiguration' Field.
    pub additional_configuration: Option<String>,
    /// The 'address' Field.
    pub address: String,
    /// The 'authentication' Field.
    pub authentication: Authentication,
    /// The 'trustSettings' Field.
    pub trust_settings: Option<TrustSettings>,
    /// The 'type' Field.
    pub r#type: String,
    /// The 'version' Field.
    pub version: Option<String>,
}

#[derive(Debug, Clone)]
pub struct TrustSettings {
    /// The 'issuerList' Field.
    pub issuer_list: Option<String>,
    /// The 'trustList' Field.
    pub trust_list: Option<String>,
    /// The 'trustMode' Field.
    pub trust_mode: String,
}

#[derive(Debug, Clone)] // default Anonymous
pub enum Authentication {
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

// ~~~~~~~~~~~~~~~~~~~Device Endpoint Status DTDL Equivalent Structs~~~~
#[derive(Clone, Debug, Default)]
/// Represents the status of a Device in the ADR Service.
pub struct DeviceStatus {
    /// The 'config' Field.
    pub config: Option<StatusConfig>,
    /// The 'endpoints' Field.
    pub endpoints: HashMap<String, Option<ConfigError>>,
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
    pub attributes: Option<HashMap<String, String>>,
    pub datasets: Option<Vec<AssetDataset>>,
    pub default_datasets_configuration: Option<String>,
    pub default_datasets_destinations: Option<Vec<AssetAndDefaultDatasetsDestinations>>,
    pub default_events_configuration: Option<String>,
    pub default_events_destinations: Option<Vec<DefaultEventsAndStreamsDestinations>>,
    pub default_management_groups_configuration: Option<String>,
    pub default_streams_configuration: Option<String>,
    pub default_streams_destinations: Option<Vec<DefaultEventsAndStreamsDestinations>>,
    pub description: Option<String>,
    pub device_ref: DeviceRef,
    pub discovered_asset_refs: Option<Vec<String>>,
    pub display_name: Option<String>,
    pub documentation_uri: Option<String>,
    pub enabled: Option<bool>,
    pub events: Option<Vec<AssetEvent>>,
    pub external_asset_id: Option<String>,
    pub hardware_revision: Option<String>,
    pub last_transition_time: Option<String>,
    pub management_groups: Option<Vec<AssetManagementGroup>>,
    pub manufacturer: Option<String>,
    pub manufacturer_uri: Option<String>,
    pub model: Option<String>,
    pub product_code: Option<String>,
    pub serial_number: Option<String>,
    pub software_revision: Option<String>,
    pub streams: Option<Vec<AssetStream>>,
    pub uuid: Option<String>,
    pub version: Option<u64>,
}

#[derive(Clone, Debug)]
pub struct AssetDataset {
    pub data_points: Option<Vec<AssetDatasetDataPoint>>,
    pub data_source: Option<String>,
    pub destinations: Option<Vec<AssetAndDefaultDatasetsDestinations>>,
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
pub struct AssetAndDefaultDatasetsDestinations {
    pub configuration: DestinationConfiguration,
    pub target: DatasetTarget,
}

#[derive(Clone, Debug)]
pub struct DefaultEventsAndStreamsDestinations {
    pub configuration: DestinationConfiguration,
    pub target: EventStreamTarget,
}

#[derive(Clone, Debug)]
pub struct DeviceRef {
    pub device_name: String,
    pub endpoint_name: String,
}

#[derive(Clone, Debug)]
pub struct AssetEvent {
    pub data_points: Option<Vec<AssetEventDataPoint>>,
    pub destinations: Option<Vec<AssetStreamAndEventDestination>>,
    pub event_configuration: Option<String>,
    pub event_notifier: String,
    pub name: String,
    pub type_ref: Option<String>,
}

#[derive(Clone, Debug)]
pub struct AssetManagementGroup {
    pub actions: Option<Vec<AssetManagementGroupAction>>,
    pub default_time_out_in_seconds: Option<u32>,
    pub default_topic: Option<String>,
    pub management_group_configuration: Option<String>,
    pub name: String,
    pub type_ref: Option<String>,
}

#[derive(Clone, Debug)]
pub struct AssetManagementGroupAction {
    pub management_action_configuration: Option<String>,
    pub name: String,
    pub target_uri: String,
    pub time_out_in_seconds: Option<u32>,
    pub topic: Option<String>,
    pub r#type: AssetManagementGroupActionType,
    pub type_ref: Option<String>,
}

#[derive(Clone, Debug)]
pub struct AssetStream {
    pub destinations: Option<Vec<AssetStreamAndEventDestination>>,
    pub name: String,
    pub stream_configuration: Option<String>,
    pub type_ref: Option<String>,
}

#[derive(Clone, Debug)]
pub struct AssetStreamAndEventDestination {
    pub configuration: DestinationConfiguration,
    pub target: EventStreamTarget,
}

#[derive(Clone, Debug)]
pub struct AssetEventDataPoint {
    pub data_point_configuration: Option<String>,
    pub data_source: String,
    pub name: String,
}

#[derive(Clone, Debug)]
pub struct DestinationConfiguration {
    pub key: Option<String>,
    pub path: Option<String>,
    pub qos: Option<QoS>,
    pub retain: Option<Retain>,
    pub topic: Option<String>,
    pub ttl: Option<u64>,
}

impl From<Asset> for adr_name_gen::Asset {
    fn from(value: Asset) -> Self {
        adr_name_gen::Asset {
            name: value.name,
            specification: value.specification.into(),
            status: value.status.map(AssetStatus::into),
        }
    }
}

impl From<AssetSpecification> for adr_name_gen::AssetSpecificationSchema {
    fn from(value: AssetSpecification) -> Self {
        adr_name_gen::AssetSpecificationSchema {
            attributes: value.attributes,
            datasets: option_vec_from(value.datasets, AssetDataset::into),
            default_datasets_configuration: value.default_datasets_configuration,
            default_datasets_destinations: option_vec_from(
                value.default_datasets_destinations,
                AssetAndDefaultDatasetsDestinations::into,
            ),
            default_events_configuration: value.default_events_configuration,
            default_events_destinations: option_vec_from(
                value.default_events_destinations,
                DefaultEventsAndStreamsDestinations::into,
            ),
            default_management_groups_configuration: value.default_management_groups_configuration,
            default_streams_configuration: value.default_streams_configuration,
            default_streams_destinations: option_vec_from(
                value.default_streams_destinations,
                DefaultEventsAndStreamsDestinations::into,
            ),
            description: value.description,
            device_ref: value.device_ref.into(),
            discovered_asset_refs: value.discovered_asset_refs,
            display_name: value.display_name,
            documentation_uri: value.documentation_uri,
            enabled: value.enabled,
            events: option_vec_from(value.events, AssetEvent::into),
            external_asset_id: value.external_asset_id,
            hardware_revision: value.hardware_revision,
            last_transition_time: value.last_transition_time,
            management_groups: option_vec_from(value.management_groups, AssetManagementGroup::into),
            manufacturer: value.manufacturer,
            manufacturer_uri: value.manufacturer_uri,
            model: value.model,
            product_code: value.product_code,
            serial_number: value.serial_number,
            software_revision: value.software_revision,
            streams: option_vec_from(value.streams, AssetStream::into),
            uuid: value.uuid,
            version: value.version,
        }
    }
}

impl From<AssetDataset> for adr_name_gen::AssetDatasetSchemaElementSchema {
    fn from(value: AssetDataset) -> Self {
        adr_name_gen::AssetDatasetSchemaElementSchema {
            data_points: option_vec_from(value.data_points, AssetDatasetDataPoint::into),
            data_source: value.data_source,
            destinations: option_vec_from(
                value.destinations,
                AssetAndDefaultDatasetsDestinations::into,
            ),
            name: value.name,
            type_ref: value.type_ref,
        }
    }
}

impl From<AssetDatasetDataPoint> for adr_name_gen::AssetDatasetDataPointSchemaElementSchema {
    fn from(value: AssetDatasetDataPoint) -> Self {
        adr_name_gen::AssetDatasetDataPointSchemaElementSchema {
            data_point_configuration: value.data_point_configuration,
            data_source: value.data_source,
            name: value.name,
            type_ref: value.type_ref,
        }
    }
}

impl From<AssetAndDefaultDatasetsDestinations>
    for adr_name_gen::AssetDatasetDestinationSchemaElementSchema
{
    fn from(value: AssetAndDefaultDatasetsDestinations) -> Self {
        adr_name_gen::AssetDatasetDestinationSchemaElementSchema {
            configuration: value.configuration.into(),
            target: value.target.into(),
        }
    }
}

impl From<AssetAndDefaultDatasetsDestinations>
    for adr_name_gen::DefaultDatasetsDestinationsSchemaElementSchema
{
    fn from(value: AssetAndDefaultDatasetsDestinations) -> Self {
        adr_name_gen::DefaultDatasetsDestinationsSchemaElementSchema {
            configuration: value.configuration.into(),
            target: value.target.into(),
        }
    }
}

impl From<DefaultEventsAndStreamsDestinations>
    for adr_name_gen::DefaultStreamsDestinationsSchemaElementSchema
{
    fn from(value: DefaultEventsAndStreamsDestinations) -> Self {
        adr_name_gen::DefaultStreamsDestinationsSchemaElementSchema {
            configuration: value.configuration.into(),
            target: value.target.into(),
        }
    }
}

impl From<DefaultEventsAndStreamsDestinations>
    for adr_name_gen::DefaultEventsDestinationsSchemaElementSchema
{
    fn from(value: DefaultEventsAndStreamsDestinations) -> Self {
        adr_name_gen::DefaultEventsDestinationsSchemaElementSchema {
            configuration: value.configuration.into(),
            target: value.target.into(),
        }
    }
}

impl From<DeviceRef> for adr_name_gen::DeviceRefSchema {
    fn from(value: DeviceRef) -> Self {
        adr_name_gen::DeviceRefSchema {
            device_name: value.device_name,
            endpoint_name: value.endpoint_name,
        }
    }
}

impl From<AssetEvent> for adr_name_gen::AssetEventSchemaElementSchema {
    fn from(value: AssetEvent) -> Self {
        adr_name_gen::AssetEventSchemaElementSchema {
            data_points: option_vec_from(value.data_points, AssetEventDataPoint::into),
            destinations: option_vec_from(value.destinations, AssetStreamAndEventDestination::into),
            event_configuration: value.event_configuration,
            event_notifier: value.event_notifier,
            name: value.name,
            type_ref: value.type_ref,
        }
    }
}

impl From<AssetManagementGroup> for adr_name_gen::AssetManagementGroupSchemaElementSchema {
    fn from(value: AssetManagementGroup) -> Self {
        adr_name_gen::AssetManagementGroupSchemaElementSchema {
            actions: option_vec_from(value.actions, AssetManagementGroupAction::into),
            default_time_out_in_seconds: value.default_time_out_in_seconds,
            default_topic: value.default_topic,
            management_group_configuration: value.management_group_configuration,
            name: value.name,
            type_ref: value.type_ref,
        }
    }
}

impl From<AssetManagementGroupAction>
    for adr_name_gen::AssetManagementGroupActionSchemaElementSchema
{
    fn from(value: AssetManagementGroupAction) -> Self {
        adr_name_gen::AssetManagementGroupActionSchemaElementSchema {
            management_action_configuration: value.management_action_configuration,
            name: value.name,
            target_uri: value.target_uri,
            time_out_in_seconds: value.time_out_in_seconds,
            topic: value.topic,
            r#type: value.r#type.into(),
            type_ref: value.type_ref,
        }
    }
}

impl From<AssetStream> for adr_name_gen::AssetStreamSchemaElementSchema {
    fn from(value: AssetStream) -> Self {
        adr_name_gen::AssetStreamSchemaElementSchema {
            destinations: option_vec_from(value.destinations, AssetStreamAndEventDestination::into),
            name: value.name,
            stream_configuration: value.stream_configuration,
            type_ref: value.type_ref,
        }
    }
}

impl From<AssetStreamAndEventDestination>
    for adr_name_gen::AssetStreamDestinationSchemaElementSchema
{
    fn from(value: AssetStreamAndEventDestination) -> Self {
        adr_name_gen::AssetStreamDestinationSchemaElementSchema {
            configuration: value.configuration.into(),
            target: value.target.into(),
        }
    }
}

impl From<AssetStreamAndEventDestination>
    for adr_name_gen::AssetEventDestinationSchemaElementSchema
{
    fn from(value: AssetStreamAndEventDestination) -> Self {
        adr_name_gen::AssetEventDestinationSchemaElementSchema {
            configuration: value.configuration.into(),
            target: value.target.into(),
        }
    }
}

impl From<AssetEventDataPoint> for adr_name_gen::AssetEventDataPointSchemaElementSchema {
    fn from(value: AssetEventDataPoint) -> Self {
        adr_name_gen::AssetEventDataPointSchemaElementSchema {
            data_point_configuration: value.data_point_configuration,
            data_source: value.data_source,
            name: value.name,
        }
    }
}

impl From<DestinationConfiguration> for adr_name_gen::DestinationConfiguration {
    fn from(value: DestinationConfiguration) -> Self {
        adr_name_gen::DestinationConfiguration {
            key: value.key,
            path: value.path,
            qos: value.qos.map(QoS::into),
            retain: value.retain.map(Retain::into),
            topic: value.topic,
            ttl: value.ttl,
        }
    }
}

// ~~~~~~~~~~~~~~~~~~~Asset Status DTDL Equivalent Structs~~~~~~~
#[derive(Clone, Debug)]
/// Represents the observed status of an asset.
pub struct AssetStatus {
    /// The configuration of the asset.
    pub config: Option<Config>,
    /// A collection of datasets associated with the asset.
    pub datasets_schema: Option<Vec<AssetDatasetEventStream>>,
    /// A collection of events associated with the asset.
    pub events_schema: Option<Vec<AssetDatasetEventStream>>,
    /// A collection of management groups associated with the asset.
    pub management_groups: Option<Vec<AssetManagementGroupStatus>>,
    /// A collection of schema references for streams associated with the asset.
    pub streams: Option<Vec<AssetDatasetEventStream>>,
}

#[derive(Clone, Debug)]
/// Represents a schema to the dataset or event.
pub struct AssetDatasetEventStream {
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

#[derive(Clone, Debug)]
/// Represents the configuration status of an asset.
pub struct Config {
    /// Error code for classification of errors.
    pub error: Option<ConfigError>,
    /// The last time the configuration has been modified.
    pub last_transition_time: Option<String>,
    /// The version of the asset configuration.
    pub version: Option<u64>,
}

impl From<AssetStatus> for adr_name_gen::AssetStatus {
    fn from(value: AssetStatus) -> Self {
        adr_name_gen::AssetStatus {
            config: value.config.map(Config::into),
            datasets: option_vec_from(value.datasets_schema, AssetDatasetEventStream::into),
            events: option_vec_from(value.events_schema, AssetDatasetEventStream::into),
            management_groups: option_vec_from(
                value.management_groups,
                AssetManagementGroupStatus::into,
            ),
            streams: option_vec_from(value.streams, AssetDatasetEventStream::into),
        }
    }
}

impl From<AssetDatasetEventStream> for adr_name_gen::AssetDatasetEventStreamStatus {
    fn from(value: AssetDatasetEventStream) -> Self {
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

impl From<Config> for adr_name_gen::AssetConfigStatusSchema {
    fn from(value: Config) -> Self {
        adr_name_gen::AssetConfigStatusSchema {
            error: value.error.map(ConfigError::into),
            last_transition_time: value.last_transition_time,
            version: value.version,
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
#[derive(Clone, Debug)]
pub enum EventStreamTarget {
    BrokerStateStore,
    Storage,
}

#[derive(Clone, Debug)]
pub enum QoS {
    Qos0,
    Qos1,
}

#[derive(Clone, Debug)]
pub enum Retain {
    Keep,
    Never,
}

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

impl From<EventStreamTarget> for adr_name_gen::EventStreamTarget {
    fn from(value: EventStreamTarget) -> Self {
        match value {
            EventStreamTarget::BrokerStateStore => Self::BrokerStateStore,
            EventStreamTarget::Storage => Self::Storage,
        }
    }
}

impl From<QoS> for adr_name_gen::QoS {
    fn from(value: QoS) -> Self {
        match value {
            QoS::Qos0 => Self::Qos0,
            QoS::Qos1 => Self::Qos1,
        }
    }
}

impl From<Retain> for adr_name_gen::Retain {
    fn from(value: Retain) -> Self {
        match value {
            Retain::Keep => Self::Keep,
            Retain::Never => Self::Never,
        }
    }
}

impl From<DatasetTarget> for adr_name_gen::DatasetTarget {
    fn from(value: DatasetTarget) -> Self {
        match value {
            DatasetTarget::BrokerStateStore => Self::BrokerStateStore,
            DatasetTarget::Mqtt => Self::Mqtt,
            DatasetTarget::Storage => Self::Storage,
        }
    }
}

impl From<AssetManagementGroupActionType> for adr_name_gen::AssetManagementGroupActionTypeSchema {
    fn from(value: AssetManagementGroupActionType) -> Self {
        match value {
            AssetManagementGroupActionType::Call => Self::Call,
            AssetManagementGroupActionType::Read => Self::Read,
            AssetManagementGroupActionType::Write => Self::Write,
        }
    }
}

// ~~~~~~~~~~~~~~DTDL structs to SDK Asset Structs for Asset Observation Need~~~~~~~
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
            config: value.config.map(Config::from),
            datasets_schema: option_vec_from(value.datasets, AssetDatasetEventStream::from),
            events_schema: option_vec_from(value.events, AssetDatasetEventStream::from),
            management_groups: option_vec_from(
                value.management_groups,
                AssetManagementGroupStatus::from,
            ),
            streams: option_vec_from(value.streams, AssetDatasetEventStream::from),
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

impl From<adr_name_gen::AssetDatasetEventStreamStatus> for AssetDatasetEventStream {
    fn from(value: adr_name_gen::AssetDatasetEventStreamStatus) -> Self {
        AssetDatasetEventStream {
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

impl From<adr_name_gen::AssetConfigStatusSchema> for Config {
    fn from(value: adr_name_gen::AssetConfigStatusSchema) -> Self {
        Config {
            error: value.error.map(ConfigError::from),
            last_transition_time: value.last_transition_time,
            version: value.version,
        }
    }
}

impl From<adr_name_gen::AssetSpecificationSchema> for AssetSpecification {
    fn from(value: adr_name_gen::AssetSpecificationSchema) -> Self {
        AssetSpecification {
            attributes: value.attributes,
            datasets: option_vec_from(value.datasets, AssetDataset::from),
            default_datasets_configuration: value.default_datasets_configuration,
            default_datasets_destinations: option_vec_from(
                value.default_datasets_destinations,
                AssetAndDefaultDatasetsDestinations::from,
            ),
            default_events_configuration: value.default_events_configuration,
            default_events_destinations: option_vec_from(
                value.default_events_destinations,
                DefaultEventsAndStreamsDestinations::from,
            ),
            default_management_groups_configuration: value.default_management_groups_configuration,
            default_streams_configuration: value.default_streams_configuration,
            default_streams_destinations: option_vec_from(
                value.default_streams_destinations,
                DefaultEventsAndStreamsDestinations::from,
            ),
            description: value.description,
            device_ref: DeviceRef::from(value.device_ref),
            discovered_asset_refs: value.discovered_asset_refs,
            display_name: value.display_name,
            documentation_uri: value.documentation_uri,
            enabled: value.enabled,
            events: option_vec_from(value.events, AssetEvent::from),
            external_asset_id: value.external_asset_id,
            hardware_revision: value.hardware_revision,
            last_transition_time: value.last_transition_time,
            management_groups: option_vec_from(value.management_groups, AssetManagementGroup::from),
            manufacturer: value.manufacturer,
            manufacturer_uri: value.manufacturer_uri,
            model: value.model,
            product_code: value.product_code,
            serial_number: value.serial_number,
            software_revision: value.software_revision,
            streams: option_vec_from(value.streams, AssetStream::from),
            uuid: value.uuid,
            version: value.version,
        }
    }
}

impl From<adr_name_gen::AssetDatasetSchemaElementSchema> for AssetDataset {
    fn from(value: adr_name_gen::AssetDatasetSchemaElementSchema) -> Self {
        AssetDataset {
            data_points: option_vec_from(value.data_points, AssetDatasetDataPoint::from),
            data_source: value.data_source,
            destinations: option_vec_from(
                value.destinations,
                AssetAndDefaultDatasetsDestinations::from,
            ),
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

impl From<adr_name_gen::AssetDatasetDestinationSchemaElementSchema>
    for AssetAndDefaultDatasetsDestinations
{
    fn from(value: adr_name_gen::AssetDatasetDestinationSchemaElementSchema) -> Self {
        AssetAndDefaultDatasetsDestinations {
            configuration: value.configuration.into(),
            target: value.target.into(),
        }
    }
}

impl From<adr_name_gen::DefaultDatasetsDestinationsSchemaElementSchema>
    for AssetAndDefaultDatasetsDestinations
{
    fn from(value: adr_name_gen::DefaultDatasetsDestinationsSchemaElementSchema) -> Self {
        AssetAndDefaultDatasetsDestinations {
            configuration: value.configuration.into(),
            target: value.target.into(),
        }
    }
}

impl From<adr_name_gen::DefaultEventsDestinationsSchemaElementSchema>
    for DefaultEventsAndStreamsDestinations
{
    fn from(value: adr_name_gen::DefaultEventsDestinationsSchemaElementSchema) -> Self {
        DefaultEventsAndStreamsDestinations {
            configuration: value.configuration.into(),
            target: value.target.into(),
        }
    }
}

impl From<adr_name_gen::DefaultStreamsDestinationsSchemaElementSchema>
    for DefaultEventsAndStreamsDestinations
{
    fn from(value: adr_name_gen::DefaultStreamsDestinationsSchemaElementSchema) -> Self {
        DefaultEventsAndStreamsDestinations {
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
            data_points: option_vec_from(value.data_points, AssetEventDataPoint::from),
            destinations: option_vec_from(value.destinations, AssetStreamAndEventDestination::from),
            event_configuration: value.event_configuration,
            event_notifier: value.event_notifier,
            name: value.name,
            type_ref: value.type_ref,
        }
    }
}

impl From<adr_name_gen::AssetEventDestinationSchemaElementSchema>
    for AssetStreamAndEventDestination
{
    fn from(value: adr_name_gen::AssetEventDestinationSchemaElementSchema) -> Self {
        AssetStreamAndEventDestination {
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
            actions: option_vec_from(value.actions, AssetManagementGroupAction::from),
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
            management_action_configuration: value.management_action_configuration,
            name: value.name,
            target_uri: value.target_uri,
            time_out_in_seconds: value.time_out_in_seconds,
            topic: value.topic,
            r#type: value.r#type.into(),
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
            destinations: option_vec_from(value.destinations, AssetStreamAndEventDestination::from),
            name: value.name,
            stream_configuration: value.stream_configuration,
            type_ref: value.type_ref,
        }
    }
}

impl From<adr_name_gen::AssetStreamDestinationSchemaElementSchema>
    for AssetStreamAndEventDestination
{
    fn from(value: adr_name_gen::AssetStreamDestinationSchemaElementSchema) -> Self {
        AssetStreamAndEventDestination {
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
            qos: value.qos.map(QoS::from),
            retain: value.retain.map(Retain::from),
            topic: value.topic,
            ttl: value.ttl,
        }
    }
}

impl From<adr_name_gen::EventStreamTarget> for EventStreamTarget {
    fn from(value: adr_name_gen::EventStreamTarget) -> Self {
        match value {
            adr_name_gen::EventStreamTarget::BrokerStateStore => {
                EventStreamTarget::BrokerStateStore
            }
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

impl From<adr_name_gen::QoS> for QoS {
    fn from(value: adr_name_gen::QoS) -> Self {
        match value {
            adr_name_gen::QoS::Qos0 => QoS::Qos0,
            adr_name_gen::QoS::Qos1 => QoS::Qos1,
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
