// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

//! Asset/Dataset models for Azure Device Registry operations.
use std::collections::HashMap;

use azure_iot_operations_mqtt::control_packet::QoS as MqttQoS;
use chrono::{DateTime, Utc};

use crate::azure_device_registry::adr_base_gen::adr_base_service::client as base_client_gen;
use crate::azure_device_registry::helper::{ConvertOptionMap, ConvertOptionVec};
use crate::azure_device_registry::{ConfigError, StatusConfig};

// ~~~~~~~~~~~~~~~~~~~Asset DTDL Equivalent Structs~~~~~~~~~~~~~~

/// Represents an Asset in the Azure Device Registry service.
#[derive(Clone, Debug, PartialEq)]
pub struct Asset {
    /// The name of the asset.
    pub name: String,
    /// The 'specification' Field.
    pub specification: AssetSpecification,
    /// The 'status' Field.
    pub status: Option<AssetStatus>,
}

/// Represents the specification of an Asset in the Azure Device Registry service.
#[derive(Clone, Debug, PartialEq)]
pub struct AssetSpecification {
    /// URI or type definition ids.
    pub asset_type_refs: Vec<String>, // if None on generated model, we can represent as empty vec. Can currently only be length of 1
    /// A set of key-value pairs that contain custom attributes
    pub attributes: HashMap<String, String>, // if None on generated model, we can represent as empty hashmap
    /// Array of datasets that are part of the asset.
    pub datasets: Vec<Dataset>, // if None on generated model, we can represent as empty vec
    /// Default configuration for datasets.
    pub default_datasets_configuration: Option<String>,
    /// Default destinations for datasets.
    pub default_datasets_destinations: Vec<DatasetDestination>, // if None on generated model, we can represent as empty vec.  Can currently only be length of 1
    /// Default configuration for events.
    pub default_events_configuration: Option<String>,
    /// Default destinations for events.
    pub default_events_destinations: Vec<EventStreamDestination>, // if None on generated model, we can represent as empty vec.  Can currently only be length of 1
    /// Default configuration for management groups.
    pub default_management_groups_configuration: Option<String>,
    /// Default configuration for streams.
    pub default_streams_configuration: Option<String>,
    /// Default destinations for streams.
    pub default_streams_destinations: Vec<EventStreamDestination>, // if None on generated model, we can represent as empty vec. Can currently only be length of 1
    /// The description of the asset.
    pub description: Option<String>,
    /// A reference to the Device and Endpoint within the device
    pub device_ref: DeviceRef,
    /// Reference to a list of discovered assets
    pub discovered_asset_refs: Vec<String>, // if None on generated model, we can represent as empty vec
    /// The display name of the asset.
    pub display_name: Option<String>,
    /// Reference to the documentation.
    pub documentation_uri: Option<String>,
    /// Enabled/Disabled status of the asset.
    pub enabled: Option<bool>, // TODO: just bool?
    ///  Array of events that are part of the asset.
    pub events: Vec<Event>, // if None on generated model, we can represent as empty vec
    /// Asset id provided by the customer.
    pub external_asset_id: Option<String>,
    /// Revision number of the hardware.
    pub hardware_revision: Option<String>,
    /// The last time the asset has been modified.
    pub last_transition_time: Option<DateTime<Utc>>,
    /// Array of management groups that are part of the asset.
    pub management_groups: Vec<ManagementGroup>, // if None on generated model, we can represent as empty vec
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
    pub streams: Vec<Stream>, // if None on generated model, we can represent as empty vec
    ///  Globally unique, immutable, non-reusable id.
    pub uuid: Option<String>,
    /// The version of the asset.
    pub version: Option<u64>,
}

/// Represents the partial specification of an Discovered Asset in the Azure Device Registry service.
#[derive(Clone, Debug)]
pub struct DiscoveredAssetSpecification {
    /// URI or type definition ids.
    pub asset_type_refs: Vec<String>, // if empty, we can represent as None on generated model.
    /// A set of key-value pairs that contain custom attributes
    pub attributes: HashMap<String, String>, // if empty, we can represent as None on generated model.
    /// Array of datasets that are part of the asset.
    pub datasets: Vec<DiscoveredDataset>, // if empty, we can represent as None on generated model
    /// Default configuration for datasets.
    pub default_datasets_configuration: Option<String>,
    /// Default destinations for datasets.
    pub default_datasets_destinations: Vec<DatasetDestination>, // if empty, we can represent as None on generated model.
    /// Default configuration for events.
    pub default_events_configuration: Option<String>,
    /// Default destinations for events.
    pub default_events_destinations: Vec<EventStreamDestination>, // if empty, we can represent as None on generated model.
    /// Default configuration for management groups.
    pub default_management_groups_configuration: Option<String>,
    /// Default configuration for streams.
    pub default_streams_configuration: Option<String>,
    /// Default destinations for streams.
    pub default_streams_destinations: Vec<EventStreamDestination>, // if empty, we can represent as None on generated model.
    /// A reference to the Device and Endpoint within the device
    pub device_ref: DeviceRef,
    /// Reference to the documentation.
    pub documentation_uri: Option<String>,
    ///  Array of events that are part of the asset.
    pub events: Vec<DiscoveredEvent>, // if empty, we can represent as None on generated model.
    /// Revision number of the hardware.
    pub hardware_revision: Option<String>,
    /// Array of management groups that are part of the asset.
    pub management_groups: Vec<DiscoveredManagementGroup>, // if empty, we can represent as None on generated model.
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
    pub streams: Vec<DiscoveredStream>, // if empty, we can represent as None on generated model.
}

/// Represents a dataset.
#[derive(Clone, Debug, PartialEq)]
pub struct Dataset {
    /// Configuration for the dataset.
    pub dataset_configuration: Option<String>,
    /// Array of data points that are part of the dataset.
    pub data_points: Vec<DatasetDataPoint>, // if None on generated model, we can represent as empty vec
    /// The address of the source of the data in the dataset
    pub data_source: Option<String>,
    /// Destinations for a dataset.
    pub destinations: Vec<DatasetDestination>, // if None on generated model, we can represent as empty vec. Can currently only be length of 1
    /// The name of the dataset.
    pub name: String,
    /// Type definition id or URI of the dataset
    pub type_ref: Option<String>,
}

/// Represents a discovered dataset.
#[derive(Clone, Debug)]
pub struct DiscoveredDataset {
    /// Configuration for the dataset.
    pub dataset_configuration: Option<String>,
    /// Array of data points that are part of the dataset.
    pub data_points: Vec<DiscoveredDatasetDataPoint>, // if empty, we can represent as None on generated model
    /// The address of the source of the data in the dataset
    pub data_source: Option<String>,
    /// Destinations for a dataset.
    pub destinations: Vec<DatasetDestination>, // if empty, we can represent as None on generated model.
    /// The last time the dataset was updated
    pub last_updated_on: Option<DateTime<Utc>>,
    /// The name of the dataset.
    pub name: String,
    /// Type definition id or URI of the dataset
    pub type_ref: Option<String>,
}

/// Represents a data point in a dataset.
#[derive(Clone, Debug, PartialEq)]
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

/// Represents a data point in a discovered dataset.
#[derive(Clone, Debug)]
pub struct DiscoveredDatasetDataPoint {
    /// Configuration for the data point
    pub data_point_configuration: Option<String>,
    /// The data source for the data point
    pub data_source: String,
    /// The last time the data point was updated
    pub last_updated_on: Option<DateTime<Utc>>,
    /// The name of the data point
    pub name: Option<String>,
    /// URI or type definition id
    pub type_ref: Option<String>,
}

/// Represents the destination for a dataset.
#[derive(Clone, Debug, PartialEq)]
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
#[derive(Clone, Debug, PartialEq)]
pub struct EventStreamDestination {
    /// The configuration for the destination
    pub configuration: DestinationConfiguration,
    /// The target for the destination
    pub target: EventStreamTarget,
}

// TODO: switch to this  rust enum
// pub enum EventStreamDestination {
//     Mqtt{ topic: String,
//         qos: Option<Qos>,
//         retain: Option<Retain>,
//         ttl: Option<u64>},
//     Storage {path: String},
// }

/// A reference to the Device and Endpoint within the device
#[derive(Clone, Debug, PartialEq)]
pub struct DeviceRef {
    /// The name of the device
    pub device_name: String,
    /// The endpoint name of the device
    pub endpoint_name: String,
}

/// Represents an event in an asset.
#[derive(Clone, Debug, PartialEq)]
pub struct Event {
    /// Array of data points that are part of the event.
    pub data_points: Vec<EventDataPoint>, // if None on generated model, we can represent as empty vec
    /// The destination for the event.
    pub destinations: Vec<EventStreamDestination>, // if None on generated model, we can represent as empty vec. Can currently only be length of 1
    /// The configuration for the event.
    pub event_configuration: Option<String>,
    /// The address of the notifier of the event
    pub event_notifier: String,
    /// The name of the event.
    pub name: String,
    /// URI or type definition id of the event
    pub type_ref: Option<String>,
}

/// Represents an event in a discovered asset.
#[derive(Clone, Debug)]
pub struct DiscoveredEvent {
    /// Array of data points that are part of the event.
    pub data_points: Vec<DiscoveredEventDataPoint>, // if empty, we can represent as None on generated model
    /// The destination for the event.
    pub destinations: Vec<EventStreamDestination>, // if empty, we can represent as None on generated model.
    /// The configuration for the event.
    pub event_configuration: Option<String>,
    /// The address of the notifier of the event
    pub event_notifier: String,
    /// The last time the event was updated
    pub last_updated_on: Option<DateTime<Utc>>,
    /// The name of the event.
    pub name: String,
    /// URI or type definition id of the event
    pub type_ref: Option<String>,
}

/// Represents a management group
#[derive(Clone, Debug, PartialEq)]
pub struct ManagementGroup {
    /// Actions for this management group
    pub actions: Vec<ManagementGroupAction>, // if None on generated model, we can represent as empty vec
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

/// Represents a discovered management group
#[derive(Clone, Debug)]
pub struct DiscoveredManagementGroup {
    /// Actions for this management group
    pub actions: Vec<DiscoveredManagementGroupAction>, // if None on generated model, we can represent as empty vec
    /// Default timeout in seconds for this management group
    pub default_time_out_in_seconds: Option<u32>,
    /// The default MQTT topic for the management group.
    pub default_topic: Option<String>,
    /// The last time the management group was updated
    pub last_updated_on: Option<DateTime<Utc>>,
    /// Configuration for the management group.
    pub management_group_configuration: Option<String>,
    /// The name of the management group.
    pub name: String,
    /// URI or type definition id of the management group
    pub type_ref: Option<String>,
}

/// Represents a management group action
#[derive(Clone, Debug, PartialEq)]
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

/// Represents a discovered management group action
#[derive(Clone, Debug)]
pub struct DiscoveredManagementGroupAction {
    /// Configuration for the action.
    pub action_configuration: Option<String>,
    /// Type of action.
    pub action_type: ActionType,
    /// The last time the management group action was updated
    pub last_updated_on: Option<DateTime<Utc>>,
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
#[derive(Clone, Debug, PartialEq)]
pub struct Stream {
    /// Destinations for a stream.
    pub destinations: Vec<EventStreamDestination>, // if None on generated model, we can represent as empty vec. Can currently only be length of 1
    /// The name of the stream.
    pub name: String,
    /// The configuration for the stream.
    pub stream_configuration: Option<String>,
    /// URI or type definition id of the stream
    pub type_ref: Option<String>,
}

/// Represents a stream for a discovered asset.
#[derive(Clone, Debug)]
pub struct DiscoveredStream {
    /// Destinations for a stream.
    pub destinations: Vec<EventStreamDestination>, // if empty we can represent as None on generated model.
    /// The last time the stream was updated
    pub last_updated_on: Option<DateTime<Utc>>,
    /// The name of the stream.
    pub name: String,
    /// The configuration for the stream.
    pub stream_configuration: Option<String>,
    /// URI or type definition id of the stream
    pub type_ref: Option<String>,
}

/// A data point in an event.
#[derive(Clone, Debug, PartialEq)]
pub struct EventDataPoint {
    /// The configuration for the data point in the event.
    pub data_point_configuration: Option<String>,
    /// The data source for the data point in the event.
    pub data_source: String,
    /// The name of the data point in the event.
    pub name: String,
}

/// A data point in a discovered event.
#[derive(Clone, Debug)]
pub struct DiscoveredEventDataPoint {
    /// The configuration for the data point in the event.
    pub data_point_configuration: Option<String>,
    /// The data source for the data point in the event.
    pub data_source: String,
    /// The last time the data point was updated
    pub last_updated_on: Option<DateTime<Utc>>,
    /// The name of the data point in the event.
    pub name: Option<String>,
}

// TODO: turn into rust enums for which of these options can correlate to which destination enums
/// The configuration for the destination
#[derive(Clone, Debug, PartialEq)]
pub struct DestinationConfiguration {
    /// The key of the destination configuration.
    pub key: Option<String>,
    /// The description of the destination configuration.
    pub path: Option<String>,
    /// The MQTT `QoS` setting for the destination configuration.
    pub qos: Option<QoS>,
    /// The MQTT retain setting for the destination configuration.
    pub retain: Option<Retain>,
    /// The MQTT topic for the destination configuration.
    pub topic: Option<String>,
    /// The MQTT TTL setting for the destination configuration.
    pub ttl: Option<u64>,
}

// ~~~~~~~~~~~~~~~~~~~Asset Status DTDL Equivalent Structs~~~~~~~
#[derive(Clone, Debug, Default, PartialEq)]
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

#[derive(Clone, Debug, PartialEq)]
/// Represents the status for a dataset, event, or stream.
pub struct DatasetEventStreamStatus {
    /// The name of the dataset, event, or stream.
    pub name: String,
    /// The message schema associated with the dataset, event, or stream.
    pub message_schema_reference: Option<MessageSchemaReference>,
    /// An error associated with the dataset, event, or stream.
    pub error: Option<ConfigError>,
}

#[derive(Clone, Debug, PartialEq)]
/// Represents the status for a management group
pub struct ManagementGroupStatus {
    /// A collection of actions associated with the management group.
    pub actions: Option<Vec<ActionStatus>>,
    /// The name of the management group.
    pub name: String,
}

#[derive(Clone, Debug, PartialEq)]
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

#[derive(Clone, Debug, PartialEq)]
/// Represents a reference to a schema, including its name, version, and namespace.
pub struct MessageSchemaReference {
    /// The name of the message schema.
    pub name: String,
    /// The version of the message schema.
    pub version: String,
    /// The namespace of the message schema.
    pub registry_namespace: String,
}

impl From<AssetStatus> for base_client_gen::AssetStatus {
    fn from(value: AssetStatus) -> Self {
        base_client_gen::AssetStatus {
            config: value.config.map(Into::into),
            datasets: value.datasets.option_vec_into(),
            events: value.events.option_vec_into(),
            management_groups: value.management_groups.option_vec_into(),
            streams: value.streams.option_vec_into(),
        }
    }
}

impl From<DatasetEventStreamStatus> for base_client_gen::AssetDatasetEventStreamStatus {
    fn from(value: DatasetEventStreamStatus) -> Self {
        base_client_gen::AssetDatasetEventStreamStatus {
            name: value.name,
            message_schema_reference: value.message_schema_reference.map(Into::into),
            error: value.error.map(Into::into),
        }
    }
}

impl From<ManagementGroupStatus>
    for base_client_gen::AssetManagementGroupStatusSchemaElementSchema
{
    fn from(value: ManagementGroupStatus) -> Self {
        base_client_gen::AssetManagementGroupStatusSchemaElementSchema {
            actions: value.actions.option_vec_into(),
            name: value.name,
        }
    }
}

impl From<ActionStatus> for base_client_gen::AssetManagementGroupActionStatusSchemaElementSchema {
    fn from(value: ActionStatus) -> Self {
        base_client_gen::AssetManagementGroupActionStatusSchemaElementSchema {
            error: value.error.map(Into::into),
            name: value.name,
            request_message_schema_reference: value
                .request_message_schema_reference
                .map(Into::into),
            response_message_schema_reference: value
                .response_message_schema_reference
                .map(Into::into),
        }
    }
}

impl From<MessageSchemaReference> for base_client_gen::MessageSchemaReference {
    fn from(value: MessageSchemaReference) -> Self {
        base_client_gen::MessageSchemaReference {
            schema_name: value.name,
            schema_version: value.version,
            schema_registry_namespace: value.registry_namespace,
        }
    }
}

// ~~~~~~~~~~~~~~~~~~~DTDL Equivalent Enums~~~~~~~
// TODO: remove in favor of Rust enum
/// The target of the event or stream.
#[derive(Clone, Debug, PartialEq)]
pub enum EventStreamTarget {
    /// MQTT
    Mqtt,
    /// Storage
    Storage,
}

#[derive(Clone, Debug, PartialEq)]
/// Represents the retain policy.
pub enum Retain {
    /// Should be retained.
    Keep,
    /// Should not be retained.
    Never,
}

// TODO: remove in favor of Rust enum
#[derive(Clone, Debug, PartialEq)]
/// Represents the target type for a dataset.
pub enum DatasetTarget {
    /// Represents a broker state store dataset target.
    BrokerStateStore,
    /// Represents a MQTT dataset target.
    Mqtt,
    /// Represents a storage dataset target.
    Storage,
}

#[derive(Clone, Debug, PartialEq)]
/// Represents the type of action that can be performed in an asset management group.
pub enum ActionType {
    /// Represents a call action type.
    Call,
    /// Represents a read action type.
    Read,
    /// Represents a write action type.
    Write,
}

/// Represents the MQTT Quality of Service level.
/// Currently does not include Quality of Service 2.
#[derive(Clone, Debug, Copy, PartialEq)]
pub enum QoS {
    /// Quality of Service level 0 (At most once)
    AtMostOnce,
    /// Quality of Service level 1 (At least once)
    AtLeastOnce,
}

impl From<Retain> for base_client_gen::Retain {
    fn from(value: Retain) -> Self {
        match value {
            Retain::Keep => Self::Keep,
            Retain::Never => Self::Never,
        }
    }
}

impl From<base_client_gen::Asset> for Asset {
    fn from(value: base_client_gen::Asset) -> Self {
        Asset {
            name: value.name,
            specification: value.specification.into(),
            status: value.status.map(Into::into),
        }
    }
}

impl From<base_client_gen::AssetStatus> for AssetStatus {
    fn from(value: base_client_gen::AssetStatus) -> Self {
        AssetStatus {
            config: value.config.map(Into::into),
            datasets: value.datasets.option_vec_into(),
            events: value.events.option_vec_into(),
            management_groups: value.management_groups.option_vec_into(),
            streams: value.streams.option_vec_into(),
        }
    }
}

impl From<base_client_gen::AssetManagementGroupStatusSchemaElementSchema>
    for ManagementGroupStatus
{
    fn from(value: base_client_gen::AssetManagementGroupStatusSchemaElementSchema) -> Self {
        ManagementGroupStatus {
            actions: value.actions.option_vec_into(),
            name: value.name,
        }
    }
}

impl From<base_client_gen::AssetManagementGroupActionStatusSchemaElementSchema> for ActionStatus {
    fn from(value: base_client_gen::AssetManagementGroupActionStatusSchemaElementSchema) -> Self {
        ActionStatus {
            error: value.error.map(Into::into),
            name: value.name,
            request_message_schema_reference: value
                .request_message_schema_reference
                .map(Into::into),
            response_message_schema_reference: value
                .response_message_schema_reference
                .map(Into::into),
        }
    }
}

impl From<base_client_gen::AssetDatasetEventStreamStatus> for DatasetEventStreamStatus {
    fn from(value: base_client_gen::AssetDatasetEventStreamStatus) -> Self {
        DatasetEventStreamStatus {
            name: value.name,
            message_schema_reference: value.message_schema_reference.map(Into::into),
            error: value.error.map(Into::into),
        }
    }
}

impl From<base_client_gen::MessageSchemaReference> for MessageSchemaReference {
    fn from(value: base_client_gen::MessageSchemaReference) -> Self {
        MessageSchemaReference {
            name: value.schema_name,
            version: value.schema_version,
            registry_namespace: value.schema_registry_namespace,
        }
    }
}

impl From<base_client_gen::AssetSpecificationSchema> for AssetSpecification {
    fn from(value: base_client_gen::AssetSpecificationSchema) -> Self {
        AssetSpecification {
            asset_type_refs: value.asset_type_refs.unwrap_or_default(),
            attributes: value.attributes.unwrap_or_default(),
            datasets: value.datasets.option_vec_into().unwrap_or_default(),
            default_datasets_configuration: value.default_datasets_configuration,
            default_datasets_destinations: value
                .default_datasets_destinations
                .option_vec_into()
                .unwrap_or_default(),
            default_events_configuration: value.default_events_configuration,
            default_events_destinations: value
                .default_events_destinations
                .option_vec_into()
                .unwrap_or_default(),
            default_management_groups_configuration: value.default_management_groups_configuration,
            default_streams_configuration: value.default_streams_configuration,
            default_streams_destinations: value
                .default_streams_destinations
                .option_vec_into()
                .unwrap_or_default(),
            description: value.description,
            device_ref: value.device_ref.into(),
            discovered_asset_refs: value.discovered_asset_refs.unwrap_or_default(),
            display_name: value.display_name,
            documentation_uri: value.documentation_uri,
            enabled: value.enabled,
            events: value.events.option_vec_into().unwrap_or_default(),
            external_asset_id: value.external_asset_id,
            hardware_revision: value.hardware_revision,
            last_transition_time: value.last_transition_time,
            management_groups: value
                .management_groups
                .option_vec_into()
                .unwrap_or_default(),
            manufacturer: value.manufacturer,
            manufacturer_uri: value.manufacturer_uri,
            model: value.model,
            product_code: value.product_code,
            serial_number: value.serial_number,
            software_revision: value.software_revision,
            streams: value.streams.option_vec_into().unwrap_or_default(),
            uuid: value.uuid,
            version: value.version,
        }
    }
}

impl From<DiscoveredAssetSpecification> for base_client_gen::DiscoveredAsset {
    fn from(value: DiscoveredAssetSpecification) -> Self {
        base_client_gen::DiscoveredAsset {
            asset_type_refs: value.asset_type_refs.option_vec_into(),
            attributes: value.attributes.option_map_into(),
            datasets: value.datasets.option_vec_into(),
            default_datasets_configuration: value.default_datasets_configuration,
            default_datasets_destinations: value.default_datasets_destinations.option_vec_into(),
            default_events_configuration: value.default_events_configuration,
            default_events_destinations: value.default_events_destinations.option_vec_into(),
            default_management_groups_configuration: value.default_management_groups_configuration,
            default_streams_configuration: value.default_streams_configuration,
            default_streams_destinations: value.default_streams_destinations.option_vec_into(),
            device_ref: value.device_ref.into(),
            documentation_uri: value.documentation_uri,
            events: value.events.option_vec_into(),
            hardware_revision: value.hardware_revision,
            management_groups: value.management_groups.option_vec_into(),
            manufacturer: value.manufacturer,
            manufacturer_uri: value.manufacturer_uri,
            model: value.model,
            product_code: value.product_code,
            serial_number: value.serial_number,
            software_revision: value.software_revision,
            streams: value.streams.option_vec_into(),
        }
    }
}

impl From<base_client_gen::AssetDatasetSchemaElementSchema> for Dataset {
    fn from(value: base_client_gen::AssetDatasetSchemaElementSchema) -> Self {
        Dataset {
            dataset_configuration: value.dataset_configuration,
            data_points: value.data_points.option_vec_into().unwrap_or_default(),
            data_source: value.data_source,
            destinations: value.destinations.option_vec_into().unwrap_or_default(),
            name: value.name,
            type_ref: value.type_ref,
        }
    }
}

impl From<DiscoveredDataset> for base_client_gen::DiscoveredAssetDataset {
    fn from(value: DiscoveredDataset) -> Self {
        base_client_gen::DiscoveredAssetDataset {
            dataset_configuration: value.dataset_configuration,
            data_points: value.data_points.option_vec_into(),
            data_source: value.data_source,
            destinations: value.destinations.option_vec_into(),
            last_updated_on: value.last_updated_on,
            name: value.name,
            type_ref: value.type_ref,
        }
    }
}

impl From<base_client_gen::AssetDatasetDataPointSchemaElementSchema> for DatasetDataPoint {
    fn from(value: base_client_gen::AssetDatasetDataPointSchemaElementSchema) -> Self {
        DatasetDataPoint {
            data_point_configuration: value.data_point_configuration,
            data_source: value.data_source,
            name: value.name,
            type_ref: value.type_ref,
        }
    }
}

impl From<DiscoveredDatasetDataPoint> for base_client_gen::DiscoveredAssetDatasetDataPoint {
    fn from(value: DiscoveredDatasetDataPoint) -> Self {
        base_client_gen::DiscoveredAssetDatasetDataPoint {
            data_point_configuration: value.data_point_configuration,
            data_source: value.data_source,
            last_updated_on: value.last_updated_on,
            name: value.name,
            type_ref: value.type_ref,
        }
    }
}

impl From<base_client_gen::DatasetDestination> for DatasetDestination {
    fn from(value: base_client_gen::DatasetDestination) -> Self {
        DatasetDestination {
            configuration: value.configuration.into(),
            target: value.target.into(),
        }
    }
}

impl From<DatasetDestination> for base_client_gen::DatasetDestination {
    fn from(value: DatasetDestination) -> Self {
        base_client_gen::DatasetDestination {
            configuration: value.configuration.into(),
            target: value.target.into(),
        }
    }
}

impl From<base_client_gen::AssetDeviceRef> for DeviceRef {
    fn from(value: base_client_gen::AssetDeviceRef) -> Self {
        DeviceRef {
            device_name: value.device_name,
            endpoint_name: value.endpoint_name,
        }
    }
}

impl From<DeviceRef> for base_client_gen::AssetDeviceRef {
    fn from(value: DeviceRef) -> Self {
        base_client_gen::AssetDeviceRef {
            device_name: value.device_name,
            endpoint_name: value.endpoint_name,
        }
    }
}

impl From<base_client_gen::AssetEventSchemaElementSchema> for Event {
    fn from(value: base_client_gen::AssetEventSchemaElementSchema) -> Self {
        Event {
            data_points: value.data_points.option_vec_into().unwrap_or_default(),
            destinations: value.destinations.option_vec_into().unwrap_or_default(),
            event_configuration: value.event_configuration,
            event_notifier: value.event_notifier,
            name: value.name,
            type_ref: value.type_ref,
        }
    }
}

impl From<DiscoveredEvent> for base_client_gen::DiscoveredAssetEvent {
    fn from(value: DiscoveredEvent) -> Self {
        base_client_gen::DiscoveredAssetEvent {
            data_points: value.data_points.option_vec_into(),
            destinations: value.destinations.option_vec_into(),
            event_configuration: value.event_configuration,
            event_notifier: value.event_notifier,
            last_updated_on: value.last_updated_on,
            name: value.name,
            type_ref: value.type_ref,
        }
    }
}

impl From<base_client_gen::EventStreamDestination> for EventStreamDestination {
    fn from(value: base_client_gen::EventStreamDestination) -> Self {
        EventStreamDestination {
            configuration: value.configuration.into(),
            target: value.target.into(),
        }
    }
}

impl From<EventStreamDestination> for base_client_gen::EventStreamDestination {
    fn from(value: EventStreamDestination) -> Self {
        base_client_gen::EventStreamDestination {
            configuration: value.configuration.into(),
            target: value.target.into(),
        }
    }
}

impl From<base_client_gen::AssetEventDataPointSchemaElementSchema> for EventDataPoint {
    fn from(value: base_client_gen::AssetEventDataPointSchemaElementSchema) -> Self {
        EventDataPoint {
            data_point_configuration: value.data_point_configuration,
            data_source: value.data_source,
            name: value.name,
        }
    }
}

impl From<DiscoveredEventDataPoint> for base_client_gen::DiscoveredAssetEventDataPoint {
    fn from(value: DiscoveredEventDataPoint) -> Self {
        base_client_gen::DiscoveredAssetEventDataPoint {
            data_point_configuration: value.data_point_configuration,
            data_source: value.data_source,
            last_updated_on: value.last_updated_on,
            name: value.name,
        }
    }
}

impl From<base_client_gen::AssetManagementGroupSchemaElementSchema> for ManagementGroup {
    fn from(value: base_client_gen::AssetManagementGroupSchemaElementSchema) -> Self {
        ManagementGroup {
            actions: value.actions.option_vec_into().unwrap_or_default(),
            default_time_out_in_seconds: value.default_time_out_in_seconds,
            default_topic: value.default_topic,
            management_group_configuration: value.management_group_configuration,
            name: value.name,
            type_ref: value.type_ref,
        }
    }
}

impl From<DiscoveredManagementGroup> for base_client_gen::DiscoveredAssetManagementGroup {
    fn from(value: DiscoveredManagementGroup) -> Self {
        base_client_gen::DiscoveredAssetManagementGroup {
            actions: value.actions.option_vec_into(),
            default_time_out_in_seconds: value.default_time_out_in_seconds,
            default_topic: value.default_topic,
            last_updated_on: value.last_updated_on,
            management_group_configuration: value.management_group_configuration,
            name: value.name,
            type_ref: value.type_ref,
        }
    }
}

impl From<base_client_gen::AssetManagementGroupActionSchemaElementSchema>
    for ManagementGroupAction
{
    fn from(value: base_client_gen::AssetManagementGroupActionSchemaElementSchema) -> Self {
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

impl From<DiscoveredManagementGroupAction>
    for base_client_gen::DiscoveredAssetManagementGroupAction
{
    fn from(value: DiscoveredManagementGroupAction) -> Self {
        base_client_gen::DiscoveredAssetManagementGroupAction {
            action_configuration: value.action_configuration,
            action_type: value.action_type.into(),
            last_updated_on: value.last_updated_on,
            name: value.name,
            target_uri: value.target_uri,
            time_out_in_seconds: value.time_out_in_seconds,
            topic: value.topic,
            type_ref: value.type_ref,
        }
    }
}

impl From<base_client_gen::AssetManagementGroupActionType> for ActionType {
    fn from(value: base_client_gen::AssetManagementGroupActionType) -> Self {
        match value {
            base_client_gen::AssetManagementGroupActionType::Call => ActionType::Call,
            base_client_gen::AssetManagementGroupActionType::Read => ActionType::Read,
            base_client_gen::AssetManagementGroupActionType::Write => ActionType::Write,
        }
    }
}

impl From<ActionType> for base_client_gen::AssetManagementGroupActionType {
    fn from(value: ActionType) -> Self {
        match value {
            ActionType::Call => Self::Call,
            ActionType::Read => Self::Read,
            ActionType::Write => Self::Write,
        }
    }
}

impl From<base_client_gen::AssetStreamSchemaElementSchema> for Stream {
    fn from(value: base_client_gen::AssetStreamSchemaElementSchema) -> Self {
        Stream {
            destinations: value.destinations.option_vec_into().unwrap_or_default(),
            name: value.name,
            stream_configuration: value.stream_configuration,
            type_ref: value.type_ref,
        }
    }
}

impl From<DiscoveredStream> for base_client_gen::DiscoveredAssetStream {
    fn from(value: DiscoveredStream) -> Self {
        base_client_gen::DiscoveredAssetStream {
            destinations: value.destinations.option_vec_into(),
            last_updated_on: value.last_updated_on,
            name: value.name,
            stream_configuration: value.stream_configuration,
            type_ref: value.type_ref,
        }
    }
}

impl From<base_client_gen::DestinationConfiguration> for DestinationConfiguration {
    fn from(value: base_client_gen::DestinationConfiguration) -> Self {
        DestinationConfiguration {
            key: value.key,
            path: value.path,
            qos: value.qos.map(Into::into),
            retain: value.retain.map(Into::into),
            topic: value.topic,
            ttl: value.ttl,
        }
    }
}

impl From<DestinationConfiguration> for base_client_gen::DestinationConfiguration {
    fn from(value: DestinationConfiguration) -> Self {
        base_client_gen::DestinationConfiguration {
            key: value.key,
            path: value.path,
            qos: value.qos.map(Into::into),
            retain: value.retain.map(Into::into),
            topic: value.topic,
            ttl: value.ttl,
        }
    }
}

impl From<base_client_gen::EventStreamTarget> for EventStreamTarget {
    fn from(value: base_client_gen::EventStreamTarget) -> Self {
        match value {
            base_client_gen::EventStreamTarget::Mqtt => EventStreamTarget::Mqtt,
            base_client_gen::EventStreamTarget::Storage => EventStreamTarget::Storage,
        }
    }
}

impl From<EventStreamTarget> for base_client_gen::EventStreamTarget {
    fn from(value: EventStreamTarget) -> Self {
        match value {
            EventStreamTarget::Mqtt => base_client_gen::EventStreamTarget::Mqtt,
            EventStreamTarget::Storage => base_client_gen::EventStreamTarget::Storage,
        }
    }
}

impl From<base_client_gen::DatasetTarget> for DatasetTarget {
    fn from(value: base_client_gen::DatasetTarget) -> Self {
        match value {
            base_client_gen::DatasetTarget::BrokerStateStore => DatasetTarget::BrokerStateStore,
            base_client_gen::DatasetTarget::Mqtt => DatasetTarget::Mqtt,
            base_client_gen::DatasetTarget::Storage => DatasetTarget::Storage,
        }
    }
}

impl From<DatasetTarget> for base_client_gen::DatasetTarget {
    fn from(value: DatasetTarget) -> Self {
        match value {
            DatasetTarget::BrokerStateStore => base_client_gen::DatasetTarget::BrokerStateStore,
            DatasetTarget::Mqtt => base_client_gen::DatasetTarget::Mqtt,
            DatasetTarget::Storage => base_client_gen::DatasetTarget::Storage,
        }
    }
}

impl From<base_client_gen::Qos> for QoS {
    fn from(value: base_client_gen::Qos) -> Self {
        match value {
            base_client_gen::Qos::Qos0 => QoS::AtMostOnce,
            base_client_gen::Qos::Qos1 => QoS::AtLeastOnce,
        }
    }
}

impl From<QoS> for base_client_gen::Qos {
    fn from(value: QoS) -> Self {
        match value {
            QoS::AtMostOnce => base_client_gen::Qos::Qos0,
            QoS::AtLeastOnce => base_client_gen::Qos::Qos1,
        }
    }
}

impl From<QoS> for MqttQoS {
    fn from(value: QoS) -> Self {
        match value {
            QoS::AtMostOnce => MqttQoS::AtMostOnce,
            QoS::AtLeastOnce => MqttQoS::AtLeastOnce,
        }
    }
}

impl From<base_client_gen::Retain> for Retain {
    fn from(value: base_client_gen::Retain) -> Self {
        match value {
            base_client_gen::Retain::Keep => Retain::Keep,
            base_client_gen::Retain::Never => Retain::Never,
        }
    }
}
