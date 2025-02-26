/* Code generated by Azure.Iot.Operations.ProtocolCompiler v0.9.0.0; DO NOT EDIT. */

mod format;
mod get_command_invoker;
mod get_request_payload;
mod get_request_payload_serialization;
mod get_request_schema;
mod get_response_payload;
mod get_response_payload_serialization;
mod put_command_invoker;
mod put_request_payload;
mod put_request_payload_serialization;
mod put_request_schema;
mod put_response_payload;
mod put_response_payload_serialization;
mod schema;
mod schema_type;

pub use azure_iot_operations_protocol::common::aio_protocol_error::AIOProtocolError;

pub use super::common_types::common_options::{CommandOptions, TelemetryOptions};

pub const MODEL_ID: &str = "dtmi:ms:adr:SchemaRegistry;1";
pub const REQUEST_TOPIC_PATTERN: &str = "adr/{modelId}/{commandName}";

pub mod client {
    pub use super::format::*;
    pub use super::get_command_invoker::*;
    pub use super::get_request_payload::*;
    pub use super::get_request_schema::*;
    pub use super::get_response_payload::*;
    pub use super::put_command_invoker::*;
    pub use super::put_request_payload::*;
    pub use super::put_request_schema::*;
    pub use super::put_response_payload::*;
    pub use super::schema::*;
    pub use super::schema_type::*;
}
