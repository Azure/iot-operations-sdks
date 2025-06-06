// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

//! Device/Endpoint models for Azure Device Registry operations.
use std::collections::HashMap;

use chrono::{DateTime, Utc};

use crate::azure_device_registry::helper::{ConvertOptionMap, ConvertOptionVec};
use crate::azure_device_registry::{ConfigError, ConfigStatus};
use crate::azure_device_registry::{
    adr_base_gen::adr_base_service::client as base_client_gen,
    device_discovery_gen::device_discovery_service::client as discovery_client_gen,
};

// TODO: bidirectional transforms

// ~~~~~~~~~~~~~~~~~~~Device Endpoint DTDL Equivalent Structs~~~~

/// Represents a Device in the Azure Device Registry service.
#[derive(Debug, Clone)]
pub struct Device {
    /// The 'attributes' Field.
    pub attributes: HashMap<String, String>, // if None in generated model, we can represent as empty hashmap
    /// The 'discoveredDeviceRef' Field.
    pub discovered_device_ref: Option<String>,
    /// The 'enabled' Field.
    pub enabled: Option<bool>,
    /// The 'endpoints' Field.
    pub endpoints: Option<DeviceEndpoints>,
    /// The 'externalDeviceId' Field.
    pub external_device_id: Option<String>,
    /// The 'lastTransitionTime' Field.
    pub last_transition_time: Option<DateTime<Utc>>,
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
/// Represents a discovered device in the Azure Device Registry service.
pub struct DiscoveredDevice {
    /// The 'attributes' Field.
    pub attributes: HashMap<String, String>, // if empty hashmap, we can represent as None on generated model
    /// The 'endpoints' Field.
    pub endpoints: Option<DiscoveredDeviceEndpoints>,
    /// The 'externalDeviceId' Field.
    pub external_device_id: Option<String>,
    /// The 'manufacturer' Field.
    pub manufacturer: Option<String>,
    /// The 'model' Field.
    pub model: Option<String>,
    /// The 'operatingSystem' Field.
    pub operating_system: Option<String>,
    /// The 'operatingSystemVersion' Field.
    pub operating_system_version: Option<String>,
}

#[derive(Debug, Clone)]
/// Represents the endpoints of a device in the Azure Device Registry service.
pub struct DeviceEndpoints {
    /// The 'inbound' Field.
    pub inbound: HashMap<String, InboundEndpoint>, // if None on generated model, we can represent as empty hashmap. Might be able to change this to a single InboundEndpoint
    /// The 'outbound' Field.
    pub outbound: Option<OutboundEndpoints>,
}

/// Represents the endpoints of a discovered device in the Azure Device Registry service.
#[derive(Debug, Clone)]
pub struct DiscoveredDeviceEndpoints {
    /// The 'inbound' Field.
    pub inbound: HashMap<String, DiscoveredInboundEndpoint>, // if empty, we can represent as None on generated model.
    /// The 'outbound' Field.
    pub outbound: Option<DiscoveredOutboundEndpoints>,
}

/// Represents the outbound endpoints of a device in the Azure Device Registry service.
#[derive(Debug, Clone)]
pub struct OutboundEndpoints {
    /// The 'assigned' Field.
    pub assigned: HashMap<String, OutboundEndpoint>,
    /// The 'unassigned' Field.
    pub unassigned: HashMap<String, OutboundEndpoint>,
}

/// Represents the outbound endpoints of a discovered device in the Azure Device Registry service.
#[derive(Debug, Clone, Default)]
pub struct DiscoveredOutboundEndpoints {
    /// The 'assigned' Field.
    pub assigned: HashMap<String, OutboundEndpoint>,
}

/// Represents an outbound endpoint of a device in the Azure Device Registry service.
#[derive(Debug, Clone)]
pub struct OutboundEndpoint {
    /// The 'address' Field.
    pub address: String,
    /// The 'endpointType' Field.
    pub endpoint_type: Option<String>,
}

/// Represents an inbound endpoint of a device in the Azure Device Registry service.
#[derive(Debug, Clone)]
pub struct InboundEndpoint {
    /// The 'additionalConfiguration' Field.
    pub additional_configuration: Option<String>,
    /// The 'address' Field.
    pub address: String,
    /// The 'authentication' Field.
    pub authentication: Authentication,
    /// The 'endpointType' Field.
    pub endpoint_type: String,
    /// The 'trustSettings' Field.
    pub trust_settings: Option<TrustSettings>,
    /// The 'version' Field.
    pub version: Option<String>,
}

/// Represents an inbound endpoint of a discovered device in the Azure Device Registry service.
#[derive(Debug, Clone)]
pub struct DiscoveredInboundEndpoint {
    /// The 'additionalConfiguration' Field.
    pub additional_configuration: Option<String>,
    /// The 'address' Field.
    pub address: String,
    /// The 'endpointType' Field.
    pub endpoint_type: String,
    /// The 'lastUpdatedOn' Field.
    pub last_updated_on: Option<DateTime<Utc>>,
    /// The 'supportedAuthenticationMethods' Field.
    pub supported_authentication_methods: Vec<String>,
    /// The 'version' Field.
    pub version: Option<String>,
}

#[derive(Debug, Clone)]
/// Represents the trust settings for an endpoint.
pub struct TrustSettings {
    /// The 'trustList' Field.
    pub trust_list: Option<String>,
}

#[derive(Debug, Clone, Default)]
/// Represents the authentication method for an endpoint.
pub enum Authentication {
    #[default]
    /// Represents anonymous authentication.
    Anonymous,
    /// Represents authentication using a certificate.
    Certificate {
        /// The 'certificateSecretName' Field.
        certificate_secret_name: String,
    },
    /// Represents authentication using a username and password.
    UsernamePassword {
        /// The 'passwordSecretName' Field.
        password_secret_name: String,
        /// The 'usernameSecretName' Field.
        username_secret_name: String,
    },
}

// ~~ From impls ~~
impl From<base_client_gen::Device> for Device {
    fn from(value: base_client_gen::Device) -> Self {
        Device {
            attributes: value.attributes.unwrap_or_default(),
            discovered_device_ref: value.discovered_device_ref,
            enabled: value.enabled,
            endpoints: value.endpoints.map(Into::into),
            external_device_id: value.external_device_id,
            last_transition_time: value.last_transition_time,
            manufacturer: value.manufacturer,
            model: value.model,
            operating_system: value.operating_system,
            operating_system_version: value.operating_system_version,
            uuid: value.uuid,
            version: value.version,
        }
    }
}

impl From<DiscoveredDevice> for discovery_client_gen::DiscoveredDevice {
    fn from(value: DiscoveredDevice) -> Self {
        discovery_client_gen::DiscoveredDevice {
            attributes: value.attributes.option_map_into(),
            endpoints: value.endpoints.map(Into::into),
            external_device_id: value.external_device_id,
            manufacturer: value.manufacturer,
            model: value.model,
            operating_system: value.operating_system,
            operating_system_version: value.operating_system_version,
        }
    }
}

impl From<base_client_gen::DeviceEndpointsSchema> for DeviceEndpoints {
    fn from(value: base_client_gen::DeviceEndpointsSchema) -> Self {
        DeviceEndpoints {
            inbound: value.inbound.option_map_into().unwrap_or_default(),
            outbound: value.outbound.map(Into::into),
        }
    }
}

impl From<DiscoveredDeviceEndpoints> for discovery_client_gen::DiscoveredDeviceEndpoints {
    fn from(value: DiscoveredDeviceEndpoints) -> Self {
        discovery_client_gen::DiscoveredDeviceEndpoints {
            inbound: value.inbound.option_map_into(),
            outbound: value.outbound.map(Into::into),
        }
    }
}

impl From<base_client_gen::OutboundSchema> for OutboundEndpoints {
    fn from(value: base_client_gen::OutboundSchema) -> Self {
        OutboundEndpoints {
            assigned: value
                .assigned
                .into_iter()
                .map(|(k, v)| (k, v.into()))
                .collect(),
            unassigned: value.unassigned.option_map_into().unwrap_or_default(),
        }
    }
}

impl From<DiscoveredOutboundEndpoints>
    for discovery_client_gen::DiscoveredDeviceOutboundEndpointsSchema
{
    fn from(value: DiscoveredOutboundEndpoints) -> Self {
        discovery_client_gen::DiscoveredDeviceOutboundEndpointsSchema {
            assigned: value
                .assigned
                .into_iter()
                .map(|(k, v)| (k, v.into()))
                .collect(),
        }
    }
}

impl From<base_client_gen::DeviceOutboundEndpoint> for OutboundEndpoint {
    fn from(value: base_client_gen::DeviceOutboundEndpoint) -> Self {
        OutboundEndpoint {
            address: value.address,
            endpoint_type: value.endpoint_type,
        }
    }
}

impl From<OutboundEndpoint> for discovery_client_gen::DeviceOutboundEndpoint {
    fn from(value: OutboundEndpoint) -> Self {
        discovery_client_gen::DeviceOutboundEndpoint {
            address: value.address,
            endpoint_type: value.endpoint_type,
        }
    }
}

impl From<base_client_gen::InboundSchemaMapValueSchema> for InboundEndpoint {
    fn from(value: base_client_gen::InboundSchemaMapValueSchema) -> Self {
        InboundEndpoint {
            additional_configuration: value.additional_configuration,
            address: value.address,
            authentication: value.authentication.map(Into::into).unwrap_or_default(),
            trust_settings: value.trust_settings.map(Into::into),
            endpoint_type: value.endpoint_type,
            version: value.version,
        }
    }
}

impl From<DiscoveredInboundEndpoint>
    for discovery_client_gen::DiscoveredDeviceInboundEndpointSchema
{
    fn from(value: DiscoveredInboundEndpoint) -> Self {
        discovery_client_gen::DiscoveredDeviceInboundEndpointSchema {
            additional_configuration: value.additional_configuration,
            address: value.address,
            endpoint_type: value.endpoint_type,
            last_updated_on: value.last_updated_on,
            supported_authentication_methods: value
                .supported_authentication_methods
                .option_vec_into(),
            version: value.version,
        }
    }
}

impl From<base_client_gen::TrustSettingsSchema> for TrustSettings {
    fn from(value: base_client_gen::TrustSettingsSchema) -> Self {
        TrustSettings {
            trust_list: value.trust_list,
        }
    }
}

impl From<base_client_gen::AuthenticationSchema> for Authentication {
    fn from(value: base_client_gen::AuthenticationSchema) -> Self {
        match value.method {
            base_client_gen::MethodSchema::Anonymous => Authentication::Anonymous,
            base_client_gen::MethodSchema::Certificate => Authentication::Certificate {
                certificate_secret_name: if let Some(x509credentials) = value.x509credentials {
                    x509credentials.certificate_secret_name
                } else {
                    log::error!(
                        "Authentication method 'Certificate', but no 'x509Credentials' provided"
                    );
                    String::new()
                },
            },

            base_client_gen::MethodSchema::UsernamePassword => {
                if let Some(username_password_credentials) = value.username_password_credentials {
                    Authentication::UsernamePassword {
                        password_secret_name: username_password_credentials.password_secret_name,
                        username_secret_name: username_password_credentials.username_secret_name,
                    }
                } else {
                    log::error!(
                        "Authentication method 'UsernamePassword', but no 'usernamePasswordCredentials' provided"
                    );

                    Authentication::UsernamePassword {
                        password_secret_name: String::new(),
                        username_secret_name: String::new(),
                    }
                }
            }
        }
    }
}

// ~~~~~~~~~~~~~~~~~~~Device Endpoint Status DTDL Equivalent Structs~~~~
#[derive(Clone, Debug, Default, PartialEq)]
/// Represents the observed status of a Device in the ADR Service.
pub struct DeviceStatus {
    ///  Defines the status config properties.
    pub config: Option<ConfigStatus>,
    /// Defines the device status for inbound/outbound endpoints.
    pub endpoints: HashMap<String, Option<ConfigError>>,
}

// ~~ From impls ~~
impl From<DeviceStatus> for base_client_gen::DeviceStatus {
    fn from(value: DeviceStatus) -> Self {
        let endpoints = if value.endpoints.is_empty() {
            None
        } else {
            Some(base_client_gen::DeviceStatusEndpointSchema {
                inbound: Some(
                    value
                        .endpoints
                        .into_iter()
                        .map(|(k, v)| {
                            (
                                k,
                                base_client_gen::DeviceStatusInboundEndpointSchemaMapValueSchema {
                                    error: v.map(ConfigError::into),
                                },
                            )
                        })
                        .collect(),
                ),
            })
        };
        base_client_gen::DeviceStatus {
            config: value.config.map(ConfigStatus::into),
            endpoints,
        }
    }
}

impl From<base_client_gen::DeviceStatus> for DeviceStatus {
    fn from(value: base_client_gen::DeviceStatus) -> Self {
        let endpoints = match value.endpoints {
            Some(endpoint_status) => match endpoint_status.inbound {
                Some(inbound_endpoints) => inbound_endpoints
                    .into_iter()
                    .map(|(k, v)| (k, v.error.map(ConfigError::from)))
                    .collect(),
                None => HashMap::new(),
            },
            None => HashMap::new(),
        };
        DeviceStatus {
            config: value.config.map(base_client_gen::ConfigStatus::into),
            endpoints,
        }
    }
}
