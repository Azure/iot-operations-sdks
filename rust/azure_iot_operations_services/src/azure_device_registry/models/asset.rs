// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

//! Asset models for Azure Device Registry operations.
use std::collections::HashMap;

use azure_iot_operations_mqtt::control_packet::QoS as MqttQoS;
use chrono::{DateTime, Utc};

use crate::azure_device_registry::adr_base_gen::adr_base_service::client as base_client_gen;
use crate::azure_device_registry::adr_base_gen::adr_base_service::service as base_service_gen;
use crate::azure_device_registry::helper::{ConvertOptionMap, ConvertOptionVec};
use crate::azure_device_registry::{ConfigError, ConfigStatus, RuntimeHealth};

// ~~~~~~~~~~~~~~~~~~~Asset DTDL Equivalent Structs~~~~~~~~~~~~~~
/// Represents an Asset in the Azure Device Registry service.
#[derive(Clone, Debug, PartialEq)]
pub struct Asset {
    /// URIs or type definition IDs.
    pub asset_type_refs: Vec<String>, // if None on generated model, we can represent as empty vec. Can currently only be length of 1
    /// A set of key-value pairs that contain custom attributes set by the customer.
    pub attributes: HashMap<String, String>, // if None on generated model, we can represent as empty hashmap
    /// Array of data sets that are part of the asset. Each data set describes the data points that make up the set.
    pub datasets: Vec<Dataset>, // if None on generated model, we can represent as empty vec
    /// Stringified JSON that contains connector-specific default configuration for all datasets. Each dataset can have its own configuration that overrides the default settings here.
    pub default_datasets_configuration: Option<String>,
    /// Default destinations for a dataset.
    pub default_datasets_destinations: Vec<DatasetDestination>, // if None on generated model, we can represent as empty vec.  Can currently only be length of 1
    /// Stringified JSON that contains connector-specific default configuration for all events. Each event can have its own configuration that overrides the default settings here.
    pub default_events_configuration: Option<String>,
    /// Default destinations for an event.
    pub default_events_destinations: Vec<EventStreamDestination>, // if None on generated model, we can represent as empty vec.  Can currently only be length of 1
    /// Stringified JSON that contains connector-specific default configuration for all management groups. Each management group can have its own configuration that overrides the default settings here.
    pub default_management_groups_configuration: Option<String>,
    /// Stringified JSON that contains connector-specific default configuration for all streams. Each stream can have its own configuration that overrides the default settings here.
    pub default_streams_configuration: Option<String>,
    /// Default destinations for a stream.
    pub default_streams_destinations: Vec<EventStreamDestination>, // if None on generated model, we can represent as empty vec. Can currently only be length of 1
    /// Human-readable description of the asset.
    pub description: Option<String>,
    /// Reference to the device that provides data for this asset. Must provide device name & endpoint on the device to use.
    pub device_ref: DeviceRef,
    /// Reference to a list of discovered assets. Populated only if the asset has been created from discovery flow. Discovered asset names must be provided.
    pub discovered_asset_refs: Vec<String>, // if None on generated model, we can represent as empty vec
    /// Human-readable display name.
    pub display_name: Option<String>,
    /// Asset documentation reference.
    pub documentation_uri: Option<String>,
    /// Enabled/Disabled status of the asset.
    pub enabled: Option<bool>, // TODO: just bool?
    /// Array of events that are part of the asset. Each event can have per-event configuration.
    pub event_groups: Vec<EventGroup>, // if None on generated model, we can represent as empty vec
    /// Asset ID provided by the customer.
    pub external_asset_id: Option<String>,
    /// Revision number of the hardware.
    pub hardware_revision: Option<String>,
    /// A timestamp (in UTC) that is updated each time the resource is modified.
    pub last_transition_time: Option<DateTime<Utc>>,
    /// Array of management groups that are part of the asset.
    pub management_groups: Vec<ManagementGroup>, // if None on generated model, we can represent as empty vec
    /// Asset manufacturer.
    pub manufacturer: Option<String>,
    /// Asset manufacturer URI.
    pub manufacturer_uri: Option<String>,
    /// Asset model.
    pub model: Option<String>,
    /// Asset product code.
    pub product_code: Option<String>,
    /// Asset serial number.
    pub serial_number: Option<String>,
    /// Asset software revision number.
    pub software_revision: Option<String>,
    /// Array of streams that are part of the asset. Each stream can have per-stream configuration.
    pub streams: Vec<Stream>, // if None on generated model, we can represent as empty vec
    /// Globally unique, immutable, non-reusable id.
    pub uuid: Option<String>,
    /// An integer that is incremented each time the resource is modified in the cloud.
    pub version: Option<u64>,
}

/// Represents a Discovered Asset in the Azure Device Registry service.
#[derive(Clone, Debug)]
pub struct DiscoveredAsset {
    /// URIs or type definition IDs for the asset type.
    pub asset_type_refs: Vec<String>, // if empty, we can represent as None on generated model.
    /// A set of key-value pairs that contain custom attributes.
    pub attributes: HashMap<String, String>, // if empty, we can represent as None on generated model.
    /// Array of datasets that are part of the asset.
    /// Each data set spec describes the data points that make up the set.
    pub datasets: Vec<DiscoveredDataset>, // if empty, we can represent as None on generated model
    /// Stringified JSON that contains connector-specific default configuration for all datasets.
    /// Each dataset can have its own configuration that overrides the default settings here.
    pub default_datasets_configuration: Option<String>,
    /// Default destinations for a dataset.
    pub default_datasets_destinations: Vec<DatasetDestination>, // if empty, we can represent as None on generated model.
    /// Stringified JSON that contains connector-specific default configuration for all events.
    /// Each event can have its own configuration that overrides the default settings here.
    pub default_events_configuration: Option<String>,
    /// Default destinations for an event.
    pub default_events_destinations: Vec<EventStreamDestination>, // if empty, we can represent as None on generated model.
    /// Stringified JSON that contains connector-specific default configuration for all management groups.
    /// Each management group can have its own configuration that overrides the default settings here.
    pub default_management_groups_configuration: Option<String>,
    /// Stringified JSON that contains connector-specific default configuration for all streams.
    /// Each stream can have its own configuration that overrides the default settings here.
    pub default_streams_configuration: Option<String>,
    /// Default destinations for a stream.
    pub default_streams_destinations: Vec<EventStreamDestination>, // if empty, we can represent as None on generated model.
    /// Human-readable description of the asset.
    pub description: Option<String>,
    /// Reference to the device that provides data for this asset.
    /// Must provide device name & endpoint on the device to use.
    pub device_ref: DeviceRef,
    /// Human-readable display name.
    pub display_name: Option<String>,
    /// Asset documentation reference.
    pub documentation_uri: Option<String>,
    /// Array of events that are part of the asset. Each event can have per-event configuration.
    pub event_groups: Vec<DiscoveredEventGroup>, // if empty, we can represent as None on generated model.
    /// Asset ID provided by the customer.
    pub external_asset_id: Option<String>,
    /// Asset hardware revision number.
    pub hardware_revision: Option<String>,
    /// Array of management groups that are part of the asset.
    /// Each management group can have a per-group configuration.
    pub management_groups: Vec<DiscoveredManagementGroup>, // if empty, we can represent as None on generated model.
    /// The name of the manufacturer.
    pub manufacturer: Option<String>,
    /// Asset manufacturer URI.
    pub manufacturer_uri: Option<String>,
    /// The model of the asset.
    pub model: Option<String>,
    /// The product code of the asset.
    pub product_code: Option<String>,
    /// The revision number of the software.
    pub serial_number: Option<String>,
    /// Asset software revision number.
    pub software_revision: Option<String>,
    /// Array of streams that are part of the asset. Each stream can have per-stream configuration.
    pub streams: Vec<DiscoveredStream>, // if empty, we can represent as None on generated model.
}

/// Represents a dataset.
#[derive(Clone, Debug, PartialEq)]
pub struct Dataset {
    /// Stringified JSON that contains connector-specific JSON string that describes configuration for the specific dataset.
    pub dataset_configuration: Option<String>,
    /// Array of data points that are part of the dataset.
    pub data_points: Vec<DatasetDataPoint>, // if None on generated model, we can represent as empty vec
    /// Reference to a data source for a given dataset.
    pub data_source: Option<String>,
    /// Destinations for a dataset.
    pub destinations: Vec<DatasetDestination>, // if None on generated model, we can represent as empty vec. Can currently only be length of 1
    /// Name of the dataset.
    pub name: String,
    /// URI or type definition ID.
    pub type_ref: Option<String>,
}

/// Represents a discovered dataset.
#[derive(Clone, Debug)]
pub struct DiscoveredDataset {
    /// Stringified JSON that contains connector-specific properties that describes configuration for the specific dataset.
    pub dataset_configuration: Option<String>,
    /// Array of data points that are part of the dataset. Each data point can have per-data-point configuration.
    pub data_points: Vec<DiscoveredDatasetDataPoint>, // if empty, we can represent as None on generated model
    /// Name of the data source within a dataset.
    pub data_source: Option<String>,
    /// Destinations for a dataset.
    pub destinations: Vec<DatasetDestination>, // if empty, we can represent as None on generated model.
    /// Timestamp (in UTC) indicating when the dataset was added or modified.
    pub last_updated_on: Option<DateTime<Utc>>,
    /// The name of the dataset.
    pub name: String,
    /// Type definition id or URI of the dataset
    pub type_ref: Option<String>,
}

/// Represents a data point in a dataset.
#[derive(Clone, Debug, PartialEq)]
pub struct DatasetDataPoint {
    /// Stringified JSON that contains connector-specific configuration for the data point.
    pub data_point_configuration: Option<String>,
    /// The address of the source of the data in the asset (e.g. URL) so that a client can access the data source on the asset.
    pub data_source: Option<String>,
    /// The name of the data point.
    pub name: String,
    /// URI or type definition ID.
    pub type_ref: Option<String>,
}

/// Represents a data point in a discovered dataset.
#[derive(Clone, Debug)]
pub struct DiscoveredDatasetDataPoint {
    /// Stringified JSON that contains connector-specific configuration for the data point.
    pub data_point_configuration: Option<String>,
    /// The address of the source of the data in the discovered asset (e.g. URL) so that a client can access the data source on the asset.
    pub data_source: Option<String>,
    /// UTC timestamp indicating when the data point was added or modified.
    pub last_updated_on: Option<DateTime<Utc>>,
    /// The name of the data point
    pub name: String,
    /// URI or type definition id
    pub type_ref: Option<String>,
}

/// Represents the destination for a dataset.
#[derive(Clone, Debug, PartialEq)]
pub struct DatasetDestination {
    /// The destination configuration.
    pub configuration: DestinationConfiguration,
    /// The target destination.
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
    /// The destination configuration.
    pub configuration: DestinationConfiguration,
    /// The target destination.
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

/// A reference to the Device and Endpoint within the device (connection information) used by brokers to connect that provides data points for this asset.
#[derive(Debug, Clone, PartialEq, Eq, Hash)]
pub struct DeviceRef {
    /// Name of the device resource.
    pub device_name: String,
    /// The name of endpoint to use.
    pub endpoint_name: String,
}

/// Represents an event group in an asset.
#[derive(Clone, Debug, PartialEq)]
pub struct EventGroup {
    /// The address of the notifier of the event in the asset (e.g. URL) so that a client can access the event on the asset.
    pub data_source: Option<String>,
    /// Default destinations for an event.
    pub default_destinations: Vec<EventStreamDestination>, // if None on generated model, we can represent as empty vec. Can currently only be length of 1
    /// Stringified JSON that contains connector-specific configuration for the event. For OPC UA, this could include configuration like, publishingInterval, samplingInterval, and queueSize.
    pub event_group_configuration: Option<String>,
    /// Array of events that are part of the asset. Each event can have per-event configuration.
    pub events: Vec<Event>, // if None on generated model, we can represent as empty vec
    /// Name of the event group.
    pub name: String,
    /// URI or type definition ID.
    pub type_ref: Option<String>,
}

/// Represents an event group in an asset.
#[derive(Clone, Debug)]
pub struct DiscoveredEventGroup {
    /// The address of the notifier of the event in the asset (e.g. URL) so that a client can access the event on the asset.
    pub data_source: Option<String>,
    /// Default destinations for an event.
    pub default_destinations: Vec<EventStreamDestination>, // if None on generated model, we can represent as empty vec. Can currently only be length of 1
    /// Stringified JSON that contains connector-specific configuration for the event. For OPC UA, this could include configuration like, publishingInterval, samplingInterval, and queueSize.
    pub event_group_configuration: Option<String>,
    /// Array of events that are part of the asset. Each event can have per-event configuration.
    pub events: Vec<DiscoveredEvent>, // if None on generated model, we can represent as empty vec
    /// Name of the event group.
    pub name: String,
    /// URI or type definition ID.
    pub type_ref: Option<String>,
}

/// Represents an event in an event group.
#[derive(Clone, Debug, PartialEq)]
pub struct Event {
    /// Reference to a data source for a given event.
    pub data_source: Option<String>,
    /// Destinations for an event.
    pub destinations: Vec<EventStreamDestination>, // if None on generated model, we can represent as empty vec. Can currently only be length of 1
    /// Stringified JSON that contains connector-specific configuration for the specific event.
    pub event_configuration: Option<String>,
    /// The name of the event.
    pub name: String,
    /// URI or type definition ID.
    pub type_ref: Option<String>,
}

/// Represents an event in a discovered asset.
#[derive(Clone, Debug)]
pub struct DiscoveredEvent {
    /// Reference to a data source for a given event.
    pub data_source: Option<String>,
    /// The destination for the event.
    pub destinations: Vec<EventStreamDestination>, // if empty, we can represent as None on generated model.
    /// Stringified JSON that contains connector-specific configuration for the specific event.
    pub event_configuration: Option<String>,
    /// UTC timestamp indicating when the event was added or modified.
    pub last_updated_on: Option<DateTime<Utc>>,
    /// The name of the event.
    pub name: String,
    /// URI or type definition id of the event
    pub type_ref: Option<String>,
}

/// Represents a management group
#[derive(Clone, Debug, PartialEq)]
pub struct ManagementGroup {
    /// Array of actions that are part of the management group. Each action can have an individual configuration.
    pub actions: Vec<ManagementGroupAction>, // if None on generated model, we can represent as empty vec
    /// Reference to a data source for a given management group.
    pub data_source: Option<String>,
    /// Default response timeout for all actions that are part of the management group.
    pub default_timeout_in_seconds: Option<u64>,
    /// Default MQTT topic path on which a client will receive the request for all actions that are part of the management group.
    pub default_topic: Option<String>,
    /// Stringified JSON that contains connector-specific configuration for the management group.
    pub management_group_configuration: Option<String>,
    /// Name of the management group.
    pub name: String,
    /// URI or type definition ID.
    pub type_ref: Option<String>,
}

/// Represents a discovered management group
#[derive(Clone, Debug)]
pub struct DiscoveredManagementGroup {
    /// Array of actions that are part of the management group. Each action can have an individual configuration.
    pub actions: Vec<DiscoveredManagementGroupAction>, // if None on generated model, we can represent as empty vec
    /// Reference to a data source for a given management group.
    pub data_source: Option<String>,
    /// Default response timeout for all actions that are part of the management group.
    pub default_timeout_in_seconds: Option<u64>,
    /// Default MQTT topic path on which a client will receive the request for all actions that are part of the management group.
    pub default_topic: Option<String>,
    /// Timestamp (in UTC) indicating when the management group was added or modified.
    pub last_updated_on: Option<DateTime<Utc>>,
    /// Stringified JSON that contains connector-specific configuration for the management group.
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
    /// Type of the action.
    pub action_type: ActionType,
    /// Name of the action.
    pub name: String,
    /// The target URI on which a client can invoke the specific action.
    pub target_uri: String,
    /// Response timeout for the action.
    pub timeout_in_seconds: Option<u64>,
    /// The MQTT topic path on which a client will receive the request for the action.
    pub topic: Option<String>,
    /// URI or type definition ID.
    pub type_ref: Option<String>,
}

/// Represents a discovered management group action
#[derive(Clone, Debug)]
pub struct DiscoveredManagementGroupAction {
    /// Configuration for the action.
    pub action_configuration: Option<String>,
    /// Type of the action.
    pub action_type: ActionType,
    /// Timestamp (in UTC) indicating when the management action was added or modified.
    pub last_updated_on: Option<DateTime<Utc>>,
    /// The name of the action.
    pub name: String,
    /// The target URI on which a client can invoke the specific action.
    pub target_uri: String,
    /// Response timeout for the action.
    pub timeout_in_seconds: Option<u64>,
    /// The MQTT topic path on which a client will receive the request for the action.
    pub topic: Option<String>,
    /// URI or type definition id of the management group action
    pub type_ref: Option<String>,
}

/// Represents a stream for an asset.
#[derive(Clone, Debug, PartialEq)]
pub struct Stream {
    /// Destinations for a Stream.
    pub destinations: Vec<EventStreamDestination>, // if None on generated model, we can represent as empty vec. Can currently only be length of 1
    /// Name of the stream definition.
    pub name: String,
    /// Stringified JSON that contains connector-specific JSON string that describes configuration for the specific stream.
    pub stream_configuration: Option<String>,
    /// URI or type definition ID.
    pub type_ref: Option<String>,
}

/// Represents a stream for a discovered asset.
#[derive(Clone, Debug)]
pub struct DiscoveredStream {
    /// Destinations for a stream.
    pub destinations: Vec<EventStreamDestination>, // if empty we can represent as None on generated model.
    /// Timestamp (in UTC) indicating when the stream was added or modified.
    pub last_updated_on: Option<DateTime<Utc>>,
    /// The name of the stream.
    pub name: String,
    /// Stringified JSON that contains connector-specific configuration that describes configuration for the specific stream.
    pub stream_configuration: Option<String>,
    /// URI or type definition id of the stream
    pub type_ref: Option<String>,
}

// TODO: turn into rust enums for which of these options can correlate to which destination enums
/// The configuration for the destination
#[derive(Clone, Debug, PartialEq)]
pub struct DestinationConfiguration {
    /// The Broker State Store destination configuration key.
    pub key: Option<String>,
    /// The Storage destination configuration path.
    pub path: Option<String>,
    /// The MQTT `QoS` setting.
    pub qos: Option<QoS>,
    /// When set to 'Keep', messages published to an MQTT broker will have the retain flag set.
    pub retain: Option<Retain>,
    /// The MQTT topic.
    pub topic: Option<String>,
    /// The MQTT TTL setting.
    pub ttl: Option<u64>,
}

// ~~~~~~~~~~~~~~~~~~~Asset Status DTDL Equivalent Structs~~~~~~~
#[derive(Clone, Debug, Default, PartialEq)]
/// Represents the observed status of an asset.
pub struct AssetStatus {
    /// The configuration status of the asset.
    pub config: Option<ConfigStatus>,
    /// Array of dataset statuses that describe the status of each dataset.
    pub datasets: Option<Vec<DatasetEventStreamStatus>>,
    /// Array of event group statuses that describe the status of each event group.
    pub event_groups: Option<Vec<EventGroupStatus>>,
    /// Array of management group statuses that describe the status of each management group.
    pub management_groups: Option<Vec<ManagementGroupStatus>>,
    /// Array of stream statuses that describe the status of each stream.
    pub streams: Option<Vec<DatasetEventStreamStatus>>,
}

#[derive(Clone, Debug, PartialEq)]
/// Represents the status for a dataset, event, or stream.
pub struct DatasetEventStreamStatus {
    /// The name of the dataset/event/stream.
    /// Must be unique within the `status.datasets`/`status.events`/`streams` array.
    /// This name is used to correlate between the spec and status dataset/event/stream information.
    pub name: String,
    /// The message schema reference.
    pub message_schema_reference: Option<MessageSchemaReference>,
    /// The last error that occurred while processing the dataset/event/stream.
    pub error: Option<ConfigError>,
}

#[derive(Clone, Debug, PartialEq)]
/// Represents the status for an event group
pub struct EventGroupStatus {
    /// Array of event statuses that describe the status of each event in the event group.
    pub events: Option<Vec<DatasetEventStreamStatus>>,
    /// The name of the event group. Must be unique within the `status.eventGroups` array. This name is used to correlate between the spec and status event group information.
    pub name: String,
}

#[derive(Clone, Debug, PartialEq)]
/// Represents the status for a management group
pub struct ManagementGroupStatus {
    /// Array of action statuses that describe the status of each action.
    pub actions: Option<Vec<ActionStatus>>,
    /// The name of the managementgroup. Must be unique within the `status.managementGroup` array. This name is used to correlate between the spec and status management group information.
    pub name: String,
}

#[derive(Clone, Debug, PartialEq)]
/// Represents the status for an action associated with a management group.
pub struct ActionStatus {
    /// The last error that occurred while processing the action.
    pub error: Option<ConfigError>,
    /// The name of the action. Must be unique within the `status.managementGroup[i].actions` array. This name is used to correlate between the spec and status management group action information.
    pub name: String,
    /// The request message schema reference.
    pub request_message_schema_reference: Option<MessageSchemaReference>,
    /// The response message schema reference.
    pub response_message_schema_reference: Option<MessageSchemaReference>,
}

#[derive(Clone, Debug, PartialEq)]
/// Represents a reference to a schema, including its name, version, and namespace.
pub struct MessageSchemaReference {
    /// The message schema name.
    pub name: String,
    /// The message schema version.
    pub version: String,
    /// The message schema registry namespace.
    pub registry_namespace: String,
}

impl From<AssetStatus> for base_client_gen::AssetStatus {
    fn from(value: AssetStatus) -> Self {
        base_client_gen::AssetStatus {
            config: value.config.map(Into::into),
            datasets: value.datasets.option_vec_into(),
            event_groups: value.event_groups.option_vec_into(),
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

impl From<EventGroupStatus> for base_client_gen::AssetEventGroupStatusSchemaElementSchema {
    fn from(value: EventGroupStatus) -> Self {
        base_client_gen::AssetEventGroupStatusSchemaElementSchema {
            events: value.events.option_vec_into(),
            name: value.name,
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

// ~~~~~~~~~~~~~~~~~~~Asset Runtime Health Event Structs~~~~~~~~~

/// Represents the runtime health event for a dataset.
#[derive(Debug, Clone)]
pub struct DatasetRuntimeHealthEvent {
    /// The name of the dataset for which the runtime health is being reported.
    pub dataset_name: String,
    /// The runtime health of the specific dataset.
    pub runtime_health: RuntimeHealth,
}

/// Represents the runtime health event for an event.
#[derive(Debug, Clone)]
pub struct EventRuntimeHealthEvent {
    /// The name of the event group containing the event for which the runtime health is being reported.
    pub event_group_name: String,
    /// The name of the event for which the runtime health is being reported.
    pub event_name: String,
    /// The runtime health of the specific event.
    pub runtime_health: RuntimeHealth,
}

/// Represents the runtime health event for a stream.
#[derive(Debug, Clone)]
pub struct StreamRuntimeHealthEvent {
    /// The name of the stream for which the runtime health is being reported.
    pub stream_name: String,
    /// The runtime health of the specific stream.
    pub runtime_health: RuntimeHealth,
}

/// Represents the runtime health event for a management action.
#[derive(Debug, Clone)]
pub struct ManagementActionRuntimeHealthEvent {
    /// The name of the management group for which the runtime health is being reported.
    pub management_group_name: String,
    /// The name of the management action for which the runtime health is being reported.
    pub management_action_name: String,
    /// The runtime health of the specific management action.
    pub runtime_health: RuntimeHealth,
}

impl From<DatasetRuntimeHealthEvent> for base_service_gen::DatasetsSchemaElementSchema {
    fn from(value: DatasetRuntimeHealthEvent) -> Self {
        base_service_gen::DatasetsSchemaElementSchema {
            dataset_name: value.dataset_name,
            runtime_health: value.runtime_health.into(),
        }
    }
}

impl From<EventRuntimeHealthEvent> for base_service_gen::EventsSchemaElementSchema {
    fn from(value: EventRuntimeHealthEvent) -> Self {
        base_service_gen::EventsSchemaElementSchema {
            event_group_name: value.event_group_name,
            event_name: value.event_name,
            runtime_health: value.runtime_health.into(),
        }
    }
}

impl From<StreamRuntimeHealthEvent> for base_service_gen::StreamsSchemaElementSchema {
    fn from(value: StreamRuntimeHealthEvent) -> Self {
        base_service_gen::StreamsSchemaElementSchema {
            stream_name: value.stream_name,
            runtime_health: value.runtime_health.into(),
        }
    }
}

impl From<ManagementActionRuntimeHealthEvent>
    for base_service_gen::ManagementActionsSchemaElementSchema
{
    fn from(value: ManagementActionRuntimeHealthEvent) -> Self {
        base_service_gen::ManagementActionsSchemaElementSchema {
            management_group_name: value.management_group_name,
            management_action_name: value.management_action_name,
            runtime_health: value.runtime_health.into(),
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

impl From<base_client_gen::AssetStatus> for AssetStatus {
    fn from(value: base_client_gen::AssetStatus) -> Self {
        AssetStatus {
            config: value.config.map(Into::into),
            datasets: value.datasets.option_vec_into(),
            event_groups: value.event_groups.option_vec_into(),
            management_groups: value.management_groups.option_vec_into(),
            streams: value.streams.option_vec_into(),
        }
    }
}

impl From<base_client_gen::AssetEventGroupStatusSchemaElementSchema> for EventGroupStatus {
    fn from(value: base_client_gen::AssetEventGroupStatusSchemaElementSchema) -> Self {
        EventGroupStatus {
            events: value.events.option_vec_into(),
            name: value.name,
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

impl From<base_client_gen::Asset> for Asset {
    fn from(value: base_client_gen::Asset) -> Self {
        Asset {
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
            event_groups: value.event_groups.option_vec_into().unwrap_or_default(),
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

impl From<DiscoveredAsset> for base_client_gen::DiscoveredAsset {
    fn from(value: DiscoveredAsset) -> Self {
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
            description: value.description,
            device_ref: value.device_ref.into(),
            display_name: value.display_name,
            documentation_uri: value.documentation_uri,
            event_groups: value.event_groups.option_vec_into(),
            external_asset_id: value.external_asset_id,
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

impl From<base_client_gen::AssetEventGroupSchemaElementSchema> for EventGroup {
    fn from(value: base_client_gen::AssetEventGroupSchemaElementSchema) -> Self {
        EventGroup {
            events: value.events.option_vec_into().unwrap_or_default(),
            default_destinations: value
                .default_destinations
                .option_vec_into()
                .unwrap_or_default(),
            event_group_configuration: value.event_group_configuration,
            data_source: value.data_source,
            name: value.name,
            type_ref: value.type_ref,
        }
    }
}

impl From<DiscoveredEventGroup> for base_client_gen::DiscoveredAssetEventGroup {
    fn from(value: DiscoveredEventGroup) -> Self {
        base_client_gen::DiscoveredAssetEventGroup {
            events: value.events.option_vec_into(),
            default_destinations: value.default_destinations.option_vec_into(),
            event_group_configuration: value.event_group_configuration,
            data_source: value.data_source,
            name: value.name,
            type_ref: value.type_ref,
        }
    }
}

impl From<base_client_gen::AssetEventSchemaElementSchema> for Event {
    fn from(value: base_client_gen::AssetEventSchemaElementSchema) -> Self {
        Event {
            destinations: value.destinations.option_vec_into().unwrap_or_default(),
            event_configuration: value.event_configuration,
            data_source: value.data_source,
            name: value.name,
            type_ref: value.type_ref,
        }
    }
}

impl From<DiscoveredEvent> for base_client_gen::DiscoveredAssetEvent {
    fn from(value: DiscoveredEvent) -> Self {
        base_client_gen::DiscoveredAssetEvent {
            data_source: value.data_source,
            destinations: value.destinations.option_vec_into(),
            event_configuration: value.event_configuration,
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

impl From<base_client_gen::AssetManagementGroupSchemaElementSchema> for ManagementGroup {
    fn from(value: base_client_gen::AssetManagementGroupSchemaElementSchema) -> Self {
        ManagementGroup {
            actions: value.actions.option_vec_into().unwrap_or_default(),
            data_source: value.data_source,
            default_timeout_in_seconds: value.default_timeout_in_seconds,
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
            data_source: value.data_source,
            default_timeout_in_seconds: value.default_timeout_in_seconds,
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
            timeout_in_seconds: value.timeout_in_seconds,
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
            timeout_in_seconds: value.timeout_in_seconds,
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
