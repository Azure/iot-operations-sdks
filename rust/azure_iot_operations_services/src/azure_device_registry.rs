// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

//! Types for Azure Device Registry operations.

use core::fmt::Debug;

use azure_iot_operations_mqtt::token::AckToken;
use azure_iot_operations_protocol::{
    common::aio_protocol_error::AIOProtocolError, rpc_command, telemetry,
};
use chrono::{DateTime, Utc};
use thiserror::Error;

use crate::azure_device_registry::helper::ConvertOptionVec;
use crate::azure_device_registry::models::{Asset, Device};
use crate::azure_device_registry::{
    adr_base_gen::adr_base_service::client as base_client_gen,
    device_discovery_gen::device_discovery_service::client as discovery_client_gen,
};
use crate::common::dispatcher::{self, Receiver};

/// Azure Device Registry base service generated code
#[allow(clippy::doc_markdown)] // TODO: consider moving this to codegen
mod adr_base_gen;
/// Azure Device Registry device discovery generated code
mod device_discovery_gen;

pub mod client;
pub mod health_reporter;
mod helper;
pub mod models;

pub use client::{Client, ClientOptions, ClientOptionsBuilder};

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

// TODO: Revisit this error story. A lot of strange overlap in validation errors.
/// Represents the kinds of errors that occur in the Azure Device Registry Client implementation.
#[derive(Error, Debug)]
#[allow(clippy::large_enum_variant)]
pub enum ErrorKind {
    /// An error occurred in the AIO Protocol. See [`AIOProtocolError`] for more information.
    #[error(transparent)]
    AIOProtocolError(#[from] AIOProtocolError),
    /// An argument provided for a request was invalid.
    #[deprecated(note = "ValidationError will be used instead")]
    #[error(transparent)]
    InvalidRequestArgument(rpc_command::invoker::RequestBuilderError),
    /// An error was returned by the Azure Device Registry Service.
    #[error("{0:?}")]
    ServiceError(base_client_gen::AkriServiceError),
    /// A Device or an asset may only have one observation at a time.
    #[error("Device or asset may only be observed once at a time")]
    DuplicateObserve(#[from] dispatcher::RegisterError),
    /// An error occurred while shutting down the Azure Device Registry Client.
    #[error("Shutdown error occurred with the following protocol errors: {0:?}")]
    ShutdownError(Vec<AIOProtocolError>),
    /// An error occurred while validating the inputs.
    #[error("{0}")]
    ValidationError(String),
}

impl From<rpc_command::invoker::Response<base_client_gen::AkriServiceError>> for ErrorKind {
    fn from(value: rpc_command::invoker::Response<base_client_gen::AkriServiceError>) -> Self {
        Self::ServiceError(value.payload)
    }
}

impl From<rpc_command::invoker::Response<discovery_client_gen::AkriServiceError>> for ErrorKind {
    fn from(value: rpc_command::invoker::Response<discovery_client_gen::AkriServiceError>) -> Self {
        Self::ServiceError(value.payload.into())
    }
}

impl From<rpc_command::invoker::RequestBuilderError> for ErrorKind {
    fn from(e: rpc_command::invoker::RequestBuilderError) -> Self {
        ErrorKind::ValidationError(e.to_string())
    }
}

impl From<telemetry::sender::MessageBuilderError> for ErrorKind {
    fn from(e: telemetry::sender::MessageBuilderError) -> Self {
        ErrorKind::ValidationError(e.to_string())
    }
}

// ~~~~~~~~~~~~~~~~~~~SDK Created Device Structs~~~~~~~~~~~~~
/// A struct to manage receiving notifications for a device
#[derive(Debug)]
pub struct DeviceUpdateObservation(Receiver<(Device, Option<AckToken>)>);

impl DeviceUpdateObservation {
    /// Receives an updated [`Device`] or [`None`] if there will be no more notifications when the unobservation is complete.
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
#[derive(Debug, Clone, PartialEq, Eq, Hash)]
/// A reference to an asset, which includes the asset name, device name, and inbound endpoint name.
pub struct AssetRef {
    /// The name of the asset
    pub name: String,
    /// The name of the device
    pub device_name: String,
    /// The name of the endpoint
    pub inbound_endpoint_name: String,
}

/// A struct to manage receiving notifications for a asset
#[derive(Debug)]
pub struct AssetUpdateObservation(Receiver<(Asset, Option<AckToken>)>);

impl AssetUpdateObservation {
    /// Receives an updated [`Asset`] or [`None`] if there will be no more notifications when the unobservation is complete.
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

// ~~~~~~~~~~~~~~~~~~Status/ConfigError DTDL Equivalent Structs~~~~~~~~~~~~~
#[derive(Clone, Debug, Default, PartialEq)]
/// Represents the configuration status.
pub struct ConfigStatus {
    /// The last error that occurred while processing the configuration.
    pub error: Option<ConfigError>,
    /// A timestamp indicating the last time the configuration has been modified from the perspective of the current actual (Edge) state of the CRD.
    pub last_transition_time: Option<DateTime<Utc>>,
    /// The version of the Device or Asset configuration that this Status pertains to.
    pub version: Option<u64>,
}

// TODO: we cannot make a meaningful error message if everything is optional.
#[derive(Clone, Debug, Default, PartialEq, Error)]
#[error("{}", message.as_deref().unwrap_or("Unknown configuration error"))]
/// Represents an error in the configuration of an asset or device.
pub struct ConfigError {
    /// Error code for classification of errors (ex: '400', '404', '500', etc.).
    pub code: Option<String>,
    /// Array of error details that describe the status of each error.
    pub details: Option<Vec<Details>>,
    /// Human readable helpful error message to provide additional context for error (ex: “capability Id ''foo'' does not exist”).
    pub message: Option<String>,
}

#[derive(Clone, Debug, PartialEq, Default)]
/// Represents the details of an error.
pub struct Details {
    /// Multi-part error code for classification and root causing of errors (ex: 400.200.100.432).
    pub code: Option<String>,
    /// Unique identifier for the transaction to aid in debugging.
    pub correlation_id: Option<String>,
    /// Human readable helpful detailed text context for debugging (ex: “The following mechanisms are supported...”).
    pub info: Option<String>,
    /// Human readable helpful error message to provide additional context for error (ex: “Authentication method not supported”).
    pub message: Option<String>,
}

/// Represents the runtime health of a resource.
#[derive(Debug, Clone)]
pub struct RuntimeHealth {
    /// The timestamp (RFC3339) when the health status was last updated, even if the status did not change.
    pub last_update_time: DateTime<Utc>,
    /// A human-readable message describing the last transition.
    pub message: Option<String>,
    /// Unique, CamelCase reason code describing the cause of the last health state transition.
    pub reason_code: Option<String>,
    /// The current health status of the resource.
    pub status: HealthStatus,
    /// The version of the resource for which the runtime health is being reported.
    pub version: u64,
}

/// Represents the health status of a resource.
#[derive(Debug, Clone, PartialEq, Eq)]
#[non_exhaustive]
pub enum HealthStatus {
    /// The resource is currently available.
    Available,
    /// The resource is currently unavailable.
    Unavailable,
}

// ~~ From impls ~~

// NOTE: Each generated module has their own (identical) error, so unify them for error propagation.
impl From<discovery_client_gen::AkriServiceError> for base_client_gen::AkriServiceError {
    fn from(value: discovery_client_gen::AkriServiceError) -> Self {
        base_client_gen::AkriServiceError {
            code: value.code.into(),
            message: value.message,
            timestamp: value.timestamp,
        }
    }
}

impl From<discovery_client_gen::CodeSchema> for base_client_gen::CodeSchema {
    fn from(value: discovery_client_gen::CodeSchema) -> Self {
        match value {
            discovery_client_gen::CodeSchema::BadRequest => base_client_gen::CodeSchema::BadRequest,
            discovery_client_gen::CodeSchema::InternalError => {
                base_client_gen::CodeSchema::InternalError
            }
            discovery_client_gen::CodeSchema::KubeError => base_client_gen::CodeSchema::KubeError,
            discovery_client_gen::CodeSchema::SerializationError => {
                base_client_gen::CodeSchema::SerializationError
            }
            discovery_client_gen::CodeSchema::Unauthorized => {
                base_client_gen::CodeSchema::Unauthorized
            }
        }
    }
}

impl From<ConfigStatus> for base_client_gen::ConfigStatus {
    fn from(value: ConfigStatus) -> Self {
        base_client_gen::ConfigStatus {
            error: value.error.map(Into::into),
            last_transition_time: value.last_transition_time,
            version: value.version,
        }
    }
}

impl From<base_client_gen::ConfigStatus> for ConfigStatus {
    fn from(value: base_client_gen::ConfigStatus) -> Self {
        ConfigStatus {
            error: value.error.map(Into::into),
            last_transition_time: value.last_transition_time,
            version: value.version,
        }
    }
}

impl From<ConfigError> for base_client_gen::ConfigError {
    fn from(value: ConfigError) -> Self {
        base_client_gen::ConfigError {
            code: value.code,
            message: value.message,
            details: value.details.option_vec_into(),
        }
    }
}

impl From<base_client_gen::ConfigError> for ConfigError {
    fn from(value: base_client_gen::ConfigError) -> Self {
        ConfigError {
            code: value.code,
            message: value.message,
            details: value.details.option_vec_into(),
        }
    }
}

impl From<Details> for base_client_gen::DetailsSchemaElementSchema {
    fn from(value: Details) -> Self {
        base_client_gen::DetailsSchemaElementSchema {
            code: value.code,
            correlation_id: value.correlation_id,
            info: value.info,
            message: value.message,
        }
    }
}

impl From<base_client_gen::DetailsSchemaElementSchema> for Details {
    fn from(value: base_client_gen::DetailsSchemaElementSchema) -> Self {
        Details {
            code: value.code,
            correlation_id: value.correlation_id,
            info: value.info,
            message: value.message,
        }
    }
}

impl From<RuntimeHealth> for base_client_gen::RuntimeHealth {
    fn from(value: RuntimeHealth) -> Self {
        base_client_gen::RuntimeHealth {
            last_update_time: value.last_update_time,
            message: value.message,
            reason_code: value.reason_code,
            status: match value.status {
                HealthStatus::Available => base_client_gen::StatusSchema::Available,
                HealthStatus::Unavailable => base_client_gen::StatusSchema::Unavailable,
            },
            version: value.version,
        }
    }
}
