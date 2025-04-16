/* Code generated by Azure.Iot.Operations.ProtocolCompiler v0.10.0.0; DO NOT EDIT. */

mod asset;
mod asset_config_status_schema;
mod asset_dataset_data_point_schema_element_schema;
mod asset_dataset_destination_schema_element_schema;
mod asset_dataset_event_stream_status;
mod asset_dataset_schema_element_schema;
mod asset_event_data_point_schema_element_schema;
mod asset_event_destination_schema_element_schema;
mod asset_event_schema_element_schema;
mod asset_management_group_action_schema_element_schema;
mod asset_management_group_action_status_schema_element_schema;
mod asset_management_group_action_type_schema;
mod asset_management_group_schema_element_schema;
mod asset_management_group_status_schema_element_schema;
mod asset_specification_schema;
mod asset_status;
mod asset_stream_destination_schema_element_schema;
mod asset_stream_schema_element_schema;
mod asset_update_event_schema;
mod asset_update_event_telemetry;
mod asset_update_event_telemetry_receiver;
mod asset_update_event_telemetry_serialization;
mod authentication_schema;
mod config_error;
mod create_detected_asset_command_invoker;
mod create_detected_asset_request_payload;
mod create_detected_asset_request_payload_serialization;
mod create_detected_asset_response_payload;
mod create_detected_asset_response_payload_serialization;
mod create_detected_asset_response_schema;
mod dataset_target;
mod default_datasets_destinations_schema_element_schema;
mod default_events_destinations_schema_element_schema;
mod default_streams_destinations_schema_element_schema;
mod destination_configuration;
mod details_schema_element_schema;
mod detected_asset;
mod detected_asset_data_point_schema_element_schema;
mod detected_asset_dataset_schema_element_schema;
mod detected_asset_event_schema_element_schema;
mod detected_asset_response_status_schema;
mod device;
mod device_endpoint_schema;
mod device_inbound_endpoint_schema_map_value_schema;
mod device_ref_schema;
mod device_specification_schema;
mod device_status;
mod device_status_config_schema;
mod device_status_endpoint_schema;
mod device_status_inbound_endpoint_schema_map_value_schema;
mod device_update_event_schema;
mod device_update_event_telemetry;
mod device_update_event_telemetry_receiver;
mod device_update_event_telemetry_serialization;
mod event_stream_target;
mod get_asset_command_invoker;
mod get_asset_request_payload;
mod get_asset_request_payload_serialization;
mod get_asset_response_payload;
mod get_asset_response_payload_serialization;
mod get_device_command_invoker;
mod get_device_response_payload;
mod get_device_response_payload_serialization;
mod message_schema_reference;
mod method_schema;
mod notification_preference;
mod notification_preference_response;
mod qo_s;
mod retain;
mod set_notification_preference_for_asset_updates_command_invoker;
mod set_notification_preference_for_asset_updates_request_payload;
mod set_notification_preference_for_asset_updates_request_payload_serialization;
mod set_notification_preference_for_asset_updates_request_schema;
mod set_notification_preference_for_asset_updates_response_payload;
mod set_notification_preference_for_asset_updates_response_payload_serialization;
mod set_notification_preference_for_device_updates_command_invoker;
mod set_notification_preference_for_device_updates_request_payload;
mod set_notification_preference_for_device_updates_request_payload_serialization;
mod set_notification_preference_for_device_updates_response_payload;
mod set_notification_preference_for_device_updates_response_payload_serialization;
mod topic;
mod trust_settings_schema;
mod update_asset_status_command_invoker;
mod update_asset_status_request_payload;
mod update_asset_status_request_payload_serialization;
mod update_asset_status_request_schema;
mod update_asset_status_response_payload;
mod update_asset_status_response_payload_serialization;
mod update_device_status_command_invoker;
mod update_device_status_request_payload;
mod update_device_status_request_payload_serialization;
mod update_device_status_response_payload;
mod update_device_status_response_payload_serialization;
mod username_password_credentials_schema;
mod x509credentials_schema;

pub use azure_iot_operations_protocol::common::aio_protocol_error::AIOProtocolError;

pub use super::common_types::common_options::{CommandOptions, TelemetryOptions};

pub const MODEL_ID: &str = "dtmi:com:microsoft:akri:AdrBaseService;1";
pub const REQUEST_TOPIC_PATTERN: &str = "akri/connector/resources/{ex:connectorClientId}/{ex:deviceName}/{ex:inboundEndpointName}/{commandName}";
pub const TELEMETRY_TOPIC_PATTERN: &str = "akri/connector/resources/telemetry/{ex:connectorClientId}/{ex:deviceName}/{ex:inboundEndpointName}/{telemetryName}";

pub mod client {
    pub use super::asset::*;
    pub use super::asset_config_status_schema::*;
    pub use super::asset_dataset_data_point_schema_element_schema::*;
    pub use super::asset_dataset_destination_schema_element_schema::*;
    pub use super::asset_dataset_event_stream_status::*;
    pub use super::asset_dataset_schema_element_schema::*;
    pub use super::asset_event_data_point_schema_element_schema::*;
    pub use super::asset_event_destination_schema_element_schema::*;
    pub use super::asset_event_schema_element_schema::*;
    pub use super::asset_management_group_action_schema_element_schema::*;
    pub use super::asset_management_group_action_status_schema_element_schema::*;
    pub use super::asset_management_group_action_type_schema::*;
    pub use super::asset_management_group_schema_element_schema::*;
    pub use super::asset_management_group_status_schema_element_schema::*;
    pub use super::asset_specification_schema::*;
    pub use super::asset_status::*;
    pub use super::asset_stream_destination_schema_element_schema::*;
    pub use super::asset_stream_schema_element_schema::*;
    pub use super::asset_update_event_schema::*;
    pub use super::asset_update_event_telemetry::*;
    pub use super::asset_update_event_telemetry_receiver::*;
    pub use super::authentication_schema::*;
    pub use super::config_error::*;
    pub use super::create_detected_asset_command_invoker::*;
    pub use super::create_detected_asset_request_payload::*;
    pub use super::create_detected_asset_response_payload::*;
    pub use super::create_detected_asset_response_schema::*;
    pub use super::dataset_target::*;
    pub use super::default_datasets_destinations_schema_element_schema::*;
    pub use super::default_events_destinations_schema_element_schema::*;
    pub use super::default_streams_destinations_schema_element_schema::*;
    pub use super::destination_configuration::*;
    pub use super::details_schema_element_schema::*;
    pub use super::detected_asset::*;
    pub use super::detected_asset_data_point_schema_element_schema::*;
    pub use super::detected_asset_dataset_schema_element_schema::*;
    pub use super::detected_asset_event_schema_element_schema::*;
    pub use super::detected_asset_response_status_schema::*;
    pub use super::device::*;
    pub use super::device_endpoint_schema::*;
    pub use super::device_inbound_endpoint_schema_map_value_schema::*;
    pub use super::device_ref_schema::*;
    pub use super::device_specification_schema::*;
    pub use super::device_status::*;
    pub use super::device_status_config_schema::*;
    pub use super::device_status_endpoint_schema::*;
    pub use super::device_status_inbound_endpoint_schema_map_value_schema::*;
    pub use super::device_update_event_schema::*;
    pub use super::device_update_event_telemetry::*;
    pub use super::device_update_event_telemetry_receiver::*;
    pub use super::event_stream_target::*;
    pub use super::get_asset_command_invoker::*;
    pub use super::get_asset_request_payload::*;
    pub use super::get_asset_response_payload::*;
    pub use super::get_device_command_invoker::*;
    pub use super::get_device_response_payload::*;
    pub use super::message_schema_reference::*;
    pub use super::method_schema::*;
    pub use super::notification_preference::*;
    pub use super::notification_preference_response::*;
    pub use super::qo_s::*;
    pub use super::retain::*;
    pub use super::set_notification_preference_for_asset_updates_command_invoker::*;
    pub use super::set_notification_preference_for_asset_updates_request_payload::*;
    pub use super::set_notification_preference_for_asset_updates_request_schema::*;
    pub use super::set_notification_preference_for_asset_updates_response_payload::*;
    pub use super::set_notification_preference_for_device_updates_command_invoker::*;
    pub use super::set_notification_preference_for_device_updates_request_payload::*;
    pub use super::set_notification_preference_for_device_updates_response_payload::*;
    pub use super::topic::*;
    pub use super::trust_settings_schema::*;
    pub use super::update_asset_status_command_invoker::*;
    pub use super::update_asset_status_request_payload::*;
    pub use super::update_asset_status_request_schema::*;
    pub use super::update_asset_status_response_payload::*;
    pub use super::update_device_status_command_invoker::*;
    pub use super::update_device_status_request_payload::*;
    pub use super::update_device_status_response_payload::*;
    pub use super::username_password_credentials_schema::*;
    pub use super::x509credentials_schema::*;
}
