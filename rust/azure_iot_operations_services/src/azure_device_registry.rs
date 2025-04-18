// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

// imports section (TODO: remove this comment)

//! Types for Azure Device Registry operations.

use core::fmt::Debug;
use std::collections::HashMap;

use crate::azure_device_registry::device_name_gen::adr_base_service::client as adr_name_gen;

/// Azure Device Registry generated code
mod device_name_gen;

// ~~~~~~~~~~~~~~~~~~~SDK Created Structs~~~~~~~~~~~~~~~~~~~~~~~~

// ~~~~~~~~~~~~~~~~~~~Helper fns ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
fn option_vec_from<T, U>(source: Option<Vec<T>>, into_fn: impl Fn(T) -> U) -> Option<Vec<U>> {
    source.map(|vec| vec.into_iter().map(into_fn).collect())
}

// ~~~~~~~~~~~~~~~~~~~Common DTDL Equivalent Structs~~~~~~~~~~~~~
#[derive(Clone, Debug, Default)]
pub struct StatusConfig {
    pub version: Option<u64>,
    pub error: Option<ConfigError>,
    pub last_transition_time: Option<String>,
}

#[derive(Clone, Debug)]
/// Represents an error in the configuration of an asset or device.
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

impl From<ConfigError> for adr_name_gen::ConfigError {
    fn from(value: ConfigError) -> Self {
        adr_name_gen::ConfigError {
            code: value.code,
            message: value.message,
            details: option_vec_from(value.details, |details| {
                adr_name_gen::DetailsSchemaElementSchema {
                    code: details.code,
                    correlation_id: details.correlation_id,
                    info: details.info,
                    message: details.message,
                }
            }),
            inner_error: value.inner_error,
        }
    }
}

impl From<adr_name_gen::ConfigError> for ConfigError {
    fn from(value: adr_name_gen::ConfigError) -> Self {
        ConfigError {
            code: value.code,
            message: value.message,
            details: option_vec_from(value.details, |details| Details {
                code: details.code,
                correlation_id: details.correlation_id,
                info: details.info,
                message: details.message,
            }),
            inner_error: value.inner_error,
        }
    }
}

#[derive(Clone, Debug)]
pub struct Details {
    /// The 'code' Field.
    pub code: Option<String>,
    /// The 'correlationId' Field.
    pub correlation_id: Option<String>,
    /// The 'info' Field.
    pub info: Option<String>,
    /// The 'message' Field.
    pub message: Option<String>,
}

// ~~~~~~~~~~~~~~~~~~~Device Endpoint DTDL Equivalent Structs~~~~

/// Represents a Device in the Azure Device Registry service.
pub struct Device {
    /// The 'name' Field.
    pub name: String,
    /// The 'specification' Field.
    pub specification: DeviceSpecification,
    /// The 'status' Field.
    pub status: Option<DeviceStatus>,
}

#[derive(Debug, Clone)]
pub struct DeviceSpecification {
    /// The 'attributes' Field.
    pub attributes: HashMap<String, String>, // if None, we can represent as empty hashmap
    /// The 'discoveredDeviceRef' Field.
    pub discovered_device_ref: Option<String>,
    /// The 'enabled' Field.
    pub enabled: Option<bool>,
    /// The 'endpoints' Field.
    pub inbound_endpoints: HashMap<String, InboundEndpoint>, // if None, we can represent as empty hashmap
    /// The 'externalDeviceId' Field.
    pub external_device_id: Option<String>,
    /// The 'lastTransitionTime' Field.
    pub last_transition_time: Option<String>, // DateTime?
    /// The 'manufacturer' Field.
    pub manufacturer: Option<String>,
    /// The 'model' Field.
    pub model: Option<String>,
    /// The 'operatingSystem' Field.
    pub operating_system: Option<String>,
    /// The 'operatingSystemVersion' Field.
    pub operating_system_version: Option<String>,
    /// The 'uuid' Field.
    pub uuid: Option<String>,
    /// The 'version' Field.
    pub version: Option<u64>,
}

#[derive(Debug, Clone)]
pub struct InboundEndpoint {
    /// The 'additionalConfiguration' Field.
    pub additional_configuration: Option<String>,
    /// The 'address' Field.
    pub address: String,
    /// The 'authentication' Field.
    pub authentication: Authentication,
    /// The 'trustSettings' Field.
    pub trust_settings: Option<TrustSettings>,
    /// The 'type' Field.
    pub r#type: String,
    /// The 'version' Field.
    pub version: Option<String>,
}

#[derive(Debug, Clone)]
pub struct TrustSettings {
    /// The 'issuerList' Field.
    pub issuer_list: Option<String>,
    /// The 'trustList' Field.
    pub trust_list: Option<String>,
    /// The 'trustMode' Field.
    pub trust_mode: String,
}

#[derive(Debug, Clone)] // default Anonymous
pub enum Authentication {
    Anonymous,
    Certificate {
        /// The 'certificateSecretName' Field.
        certificate_secret_name: String,
    },
    UsernamePassword {
        /// The 'passwordSecretName' Field.
        password_secret_name: String,
        /// The 'usernameSecretName' Field.
        username_secret_name: String,
    },
}
// ~~~~~~~~~~~~~~~~~~~Device Endpoint Status DTDL Equivalent Structs~~~~
#[derive(Clone, Debug, Default)]
/// Represents the status of a Device in the ADR Service.
pub struct DeviceStatus {
    /// The 'config' Field.
    pub config: Option<StatusConfig>,
    /// The 'endpoints' Field.
    pub endpoints: HashMap<String, Option<ConfigError>>,
}

// ~~~~~~~~~~~~~~~~~~~Asset DTDL Equivalent Structs~~~~~~~~~~~~~~

// ~~~~~~~~~~~~~~~~~~~Asset Status DTDL Equivalent Structs~~~~~~~
