// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

//! Types for Azure Device Registry operations.

use core::fmt::Debug;
use std::collections::HashMap;

use azure_iot_operations_mqtt::interface::AckToken;
use azure_iot_operations_protocol::{common::aio_protocol_error::AIOProtocolError, rpc_command};
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
mod adr_base_gen;
/// Azure Device Registry device discovery generated code
mod device_discovery_gen;

pub mod client;
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
    #[error(transparent)]
    InvalidRequestArgument(#[from] rpc_command::invoker::RequestBuilderError),
    /// An error was returned by the Azure Device Registry Service.
    #[error("{0:?}")]
    ServiceError(#[from] base_client_gen::AkriServiceError),
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

// ~~~~~~~~~~~~~~~~~~~SDK Created Device Structs~~~~~~~~~~~~~
/// A struct to manage receiving notifications for a device
#[derive(Debug)]
pub struct DeviceUpdateObservation(Receiver<(Device, Option<AckToken>)>);

impl DeviceUpdateObservation {
    /// Receives an updated [`Device`] or [`None`] if there will be no more notifications.
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
/// A struct to manage receiving notifications for a asset
#[derive(Debug)]
pub struct AssetUpdateObservation(Receiver<(Asset, Option<AckToken>)>);

impl AssetUpdateObservation {
    /// Receives an updated [`Asset`] or [`None`] if there will be no more notifications.
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
pub struct StatusConfig {
    /// Error details for status.
    pub error: Option<ConfigError>,
    /// The last time the configuration has been modified.
    pub last_transition_time: Option<DateTime<Utc>>,
    /// The version of the Device or Asset configuration.
    pub version: Option<u64>,
}

// TODO: we cannot make a meaningful error message if everything is optional.
#[derive(Clone, Debug, Default, PartialEq, Error)]
#[error("Configuration error")]
/// Represents an error in the configuration of an asset or device.
pub struct ConfigError {
    /// Error code for classification of errors (ex: ''400'', ''404'', ''500'', etc.).
    pub code: Option<String>,
    /// Array of event statuses that describe the status of each event.
    pub details: Option<Vec<Details>>,
    /// The inner error, if any.
    pub inner_error: Option<HashMap<String, String>>,
    /// The message of the error.
    pub message: Option<String>,
}

#[derive(Clone, Debug, PartialEq, Default)]
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
        }
    }
}

impl From<StatusConfig> for base_client_gen::DeviceStatusConfigSchema {
    fn from(value: StatusConfig) -> Self {
        base_client_gen::DeviceStatusConfigSchema {
            version: value.version,
            error: value.error.map(Into::into),
            last_transition_time: value.last_transition_time,
        }
    }
}

impl From<base_client_gen::DeviceStatusConfigSchema> for StatusConfig {
    fn from(value: base_client_gen::DeviceStatusConfigSchema) -> Self {
        StatusConfig {
            version: value.version,
            error: value.error.map(Into::into),
            last_transition_time: value.last_transition_time,
        }
    }
}

impl From<StatusConfig> for base_client_gen::AssetConfigStatusSchema {
    fn from(value: StatusConfig) -> Self {
        base_client_gen::AssetConfigStatusSchema {
            error: value.error.map(Into::into),
            last_transition_time: value.last_transition_time,
            version: value.version,
        }
    }
}

impl From<base_client_gen::AssetConfigStatusSchema> for StatusConfig {
    fn from(value: base_client_gen::AssetConfigStatusSchema) -> Self {
        StatusConfig {
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
            inner_error: value.inner_error,
        }
    }
}

impl From<base_client_gen::ConfigError> for ConfigError {
    fn from(value: base_client_gen::ConfigError) -> Self {
        ConfigError {
            code: value.code,
            message: value.message,
            details: value.details.option_vec_into(),
            inner_error: value.inner_error,
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
