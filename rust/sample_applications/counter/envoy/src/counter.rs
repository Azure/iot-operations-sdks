/* Code generated by Azure.Iot.Operations.ProtocolCompiler v0.10.0.0; DO NOT EDIT. */

mod increment_command_executor;
mod increment_command_invoker;
mod increment_request_payload;
mod increment_request_payload_serialization;
mod increment_response_payload;
mod increment_response_payload_serialization;
mod read_counter_command_executor;
mod read_counter_command_invoker;
mod read_counter_response_payload;
mod read_counter_response_payload_serialization;
mod reset_command_executor;
mod reset_command_invoker;
mod telemetry_collection;
mod telemetry_collection_serialization;
mod telemetry_receiver;
mod telemetry_sender;

pub use azure_iot_operations_protocol::common::aio_protocol_error::AIOProtocolError;

pub use super::common_types::options::{CommandExecutorOptions, TelemetrySenderOptions};
pub use super::common_types::options::{CommandInvokerOptions, TelemetryReceiverOptions};

pub const MODEL_ID: &str = "dtmi:com:example:Counter;1";
pub const REQUEST_TOPIC_PATTERN: &str = "rpc/command-samples/{executorId}/{commandName}";
pub const TELEMETRY_TOPIC_PATTERN: &str = "telemetry/telemetry-samples/counterValue";

pub mod client {
    pub use super::increment_command_invoker::*;
    pub use super::increment_request_payload::*;
    pub use super::increment_response_payload::*;
    pub use super::read_counter_command_invoker::*;
    pub use super::read_counter_response_payload::*;
    pub use super::reset_command_invoker::*;
    pub use super::telemetry_collection::*;
    pub use super::telemetry_receiver::*;
}

pub mod service {
    pub use super::increment_command_executor::*;
    pub use super::increment_request_payload::*;
    pub use super::increment_response_payload::*;
    pub use super::read_counter_command_executor::*;
    pub use super::read_counter_response_payload::*;
    pub use super::reset_command_executor::*;
    pub use super::telemetry_collection::*;
    pub use super::telemetry_sender::*;
}
