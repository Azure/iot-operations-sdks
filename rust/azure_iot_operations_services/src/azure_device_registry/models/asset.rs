// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

//! Asset/Dataset models for Azure Device Registry operations.
use std::collections::HashMap;

use crate::azure_device_registry::adr_base_gen::adr_base_service::client as base_client_gen;
use crate::azure_device_registry::helper::ConvertOptionVec;
use crate::azure_device_registry::{ConfigError, StatusConfig};
use azure_iot_operations_mqtt::control_packet::QoS as MqttQoS;

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
    pub default_events_destinations: Vec<EventStreamDestination>, // if None, we can represent as empty vec.  Can currently only be length of 1
    /// Default configuration for management groups.
    pub default_management_groups_configuration: Option<String>,
    /// Default configuration for streams.
    pub default_streams_configuration: Option<String>,
    /// Default destinations for streams.
    pub default_streams_destinations: Vec<EventStreamDestination>, // if None, we can represent as empty vec. Can currently only be length of 1
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
    pub destinations: Vec<EventStreamDestination>, // if None, we can represent as empty vec. Can currently only be length of 1
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
    pub destinations: Vec<EventStreamDestination>, // if None, we can represent as empty vec. Can currently only be length of 1
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
    pub qos: Option<MqttQoS>,
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

impl From<base_client_gen::DatasetDestination> for DatasetDestination {
    fn from(value: base_client_gen::DatasetDestination) -> Self {
        DatasetDestination {
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

impl From<base_client_gen::EventStreamDestination> for EventStreamDestination {
    fn from(value: base_client_gen::EventStreamDestination) -> Self {
        EventStreamDestination {
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

impl From<base_client_gen::AssetManagementGroupActionType> for ActionType {
    fn from(value: base_client_gen::AssetManagementGroupActionType) -> Self {
        match value {
            base_client_gen::AssetManagementGroupActionType::Call => ActionType::Call,
            base_client_gen::AssetManagementGroupActionType::Read => ActionType::Read,
            base_client_gen::AssetManagementGroupActionType::Write => ActionType::Write,
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

impl From<base_client_gen::EventStreamTarget> for EventStreamTarget {
    fn from(value: base_client_gen::EventStreamTarget) -> Self {
        match value {
            base_client_gen::EventStreamTarget::Mqtt => EventStreamTarget::Mqtt,
            base_client_gen::EventStreamTarget::Storage => EventStreamTarget::Storage,
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

impl From<base_client_gen::Qos> for azure_iot_operations_mqtt::control_packet::QoS {
    fn from(value: base_client_gen::Qos) -> Self {
        match value {
            base_client_gen::Qos::Qos0 => MqttQoS::AtMostOnce,
            base_client_gen::Qos::Qos1 => MqttQoS::AtLeastOnce,
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
