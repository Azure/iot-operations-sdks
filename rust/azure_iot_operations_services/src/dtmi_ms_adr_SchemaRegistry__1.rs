/* Code generated by Azure.Iot.Operations.ProtocolCompiler; DO NOT EDIT. */
#![allow(non_snake_case)]

mod enum_ms_adr_schema_registry_format__1;
mod enum_ms_adr_schema_registry_schema_type__1;
mod get_command_invoker;
mod get_request_payload;
mod get_request_payload_serialization;
mod get_response_payload;
mod get_response_payload_serialization;
mod object_get_request;
mod object_ms_adr_schema_registry_schema__1;
mod object_put_request;
mod put_command_invoker;
mod put_request_payload;
mod put_request_payload_serialization;
mod put_response_payload;
mod put_response_payload_serialization;

pub use azure_iot_operations_protocol::common::aio_protocol_error::AIOProtocolError;

pub use crate::common_types::common_options::{CommandOptions, TelemetryOptions};

pub const MODEL_ID: &str = "dtmi:ms:adr:SchemaRegistry;1";
pub const REQUEST_TOPIC_PATTERN: &str = "adr/{modelId}/{commandName}";
pub const COMMAND_SERVICE_GROUP_ID: &str = "MyServiceGroup";

pub mod client {
    pub use super::enum_ms_adr_schema_registry_format__1::*;
    pub use super::enum_ms_adr_schema_registry_schema_type__1::*;
    pub use super::get_command_invoker::*;
    pub use super::get_request_payload::*;
    pub use super::get_response_payload::*;
    pub use super::object_get_request::*;
    pub use super::object_ms_adr_schema_registry_schema__1::*;
    pub use super::object_put_request::*;
    pub use super::put_command_invoker::*;
    pub use super::put_request_payload::*;
    pub use super::put_response_payload::*;
}
