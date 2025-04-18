// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

//! Types for Azure Device Registry operations.

use std::collections::HashMap;

use crate::azure_device_registry::device_name_gen::adr_base_service::client as adr_base_service_gen;
/// Azure Device Registry generated code
mod device_name_gen;

#[derive(Clone, Debug)]
pub struct Asset {
    pub name: String,
    pub specification: AssetSpecificationSchema,
    pub status: Option<AssetStatus>,
}

#[derive(Clone, Debug)]
pub struct AssetSpecificationSchema {
    pub attributes: Option<HashMap<String, String>>,
    pub datasets: Option<Vec<AssetDataset>>,
    pub default_datasets_configuration: Option<String>,
    pub default_datasets_destinations: Option<Vec<DefaultDatasetsDestinations>>,
    pub default_events_configuration: Option<String>,
    pub default_events_destinations: Option<Vec<DefaultEventsDestinations>>,
    pub default_management_groups_configuration: Option<String>,
    pub default_streams_configuration: Option<String>,
    pub default_streams_destinations: Option<Vec<DefaultStreamsDestinations>>,
    pub description: Option<String>,
    pub device_ref: DeviceRefSchema,
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
    pub data_points: Option<Vec<AssetDatasetDataPointSchemaElementSchema>>,
    pub data_source: Option<String>,
    pub destinations: Option<Vec<AssetDatasetDestinationSchemaElementSchema>>,
    pub name: String,
    pub type_ref: Option<String>,
}

#[derive(Clone, Debug)]
pub struct AssetDatasetDataPointSchemaElementSchema {
    pub data_point_configuration: Option<String>,
    pub data_source: String,
    pub name: String,
    pub type_ref: Option<String>,
}

#[derive(Clone, Debug)]
pub struct AssetDatasetDestinationSchemaElementSchema {
    pub configuration: DestinationConfiguration,
    pub target: DatasetTarget,
}

#[derive(Clone, Debug)]
pub struct DefaultEventsDestinationsSchemaElementSchema {
    pub configuration: DestinationConfiguration,
    pub target: EventStreamTarget,
}

#[derive(Clone, Debug)]
pub enum EventStreamTarget {
    BrokerStateStore,
    Storage,
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
/// Represents the observed status of an asset.
pub struct AssetStatus {
    /// The configuration of the asset.
    pub config: Option<Config>,
    /// A collection of datasets associated with the asset.
    pub datasets_schema: Option<Vec<AssetDatasetEventStream>>,
    /// A collection of events associated with the asset.
    pub events_schema: Option<Vec<AssetDatasetEventStream>>,
    /// A collection of management groups associated with the asset.
    pub management_groups: Option<Vec<AssetManagementGroup>>,
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
pub struct AssetManagementGroup {
    /// A collection of actions associated with the management group.
    pub actions: Option<Vec<AssetManagementGroupAction>>,
    /// The name of the management group.
    pub name: String,
}

#[derive(Clone, Debug)]
/// Represents an action associated with an asset management group.
pub struct AssetManagementGroupAction {
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

#[derive(Clone, Debug)]
/// Represents an error in the configuration of an asset.
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

impl From<MessageSchemaReference> for adr_base_service_gen::MessageSchemaReference {
    fn from(value: MessageSchemaReference) -> Self {
        adr_base_service_gen::MessageSchemaReference {
            schema_name: value.name,
            schema_version: value.version,
            schema_registry_namespace: value.registry_namespace,
        }
    }
}
