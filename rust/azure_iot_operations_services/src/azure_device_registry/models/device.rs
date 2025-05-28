// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

//! Device/Endpoint models for Azure Device Registry operations.
use std::collections::HashMap;

use crate::azure_device_registry::adr_base_gen::adr_base_service::client as base_client_gen;
use crate::azure_device_registry::{ConfigError, StatusConfig};

// ~~~~~~~~~~~~~~~~~~~Device Endpoint DTDL Equivalent Structs~~~~

/// Represents a Device in the Azure Device Registry service.
#[derive(Clone, Debug)]
pub struct Device {
    /// The 'name' Field.
    pub name: String,
    /// The 'specification' Field.
    pub specification: DeviceSpecification,
    /// The 'status' Field.S
    pub status: Option<DeviceStatus>,
}

#[derive(Debug, Clone)]
/// Represents the specification of a device in the Azure Device Registry service.
pub struct DeviceSpecification {
    /// The 'attributes' Field.
    pub attributes: HashMap<String, String>, // if None, we can represent as empty hashmap
    /// The 'discoveredDeviceRef' Field.
    pub discovered_device_ref: Option<String>,
    /// The 'enabled' Field.
    pub enabled: Option<bool>,
    /// The 'endpoints' Field.
    pub endpoints: DeviceEndpoints,
    /// The 'externalDeviceId' Field.
    pub external_device_id: Option<String>,
    /// The 'lastTransitionTime' Field.
    pub last_transition_time: Option<String>, // TODO DateTime?
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
pub struct DiscoveredDeviceSpecification {
    /// The 'attributes' Field.
    pub attributes: HashMap<String, String>, // if None, we can represent as empty hashmap
    /// The 'endpoints' Field.
    pub endpoints: DeviceEndpoints,     // TODO: custom type
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


#[derive(Debug, Clone, Default)]
/// Represents the endpoints of a device in the Azure Device Registry service.
pub struct DeviceEndpoints {
    /// The 'inbound' Field.
    pub inbound: HashMap<String, InboundEndpoint>, // if None, we can represent as empty hashmap. Might be able to change this to a single InboundEndpoint
    /// The 'outbound' Field.
    pub outbound_assigned: HashMap<String, OutboundEndpoint>,
    /// The 'outboundUnassigned' Field.
    pub outbound_unassigned: HashMap<String, OutboundEndpoint>,
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

#[derive(Debug, Clone)]
/// Represents the trust settings for an endpoint.
pub struct TrustSettings {
    /// The 'issuerList' Field.
    pub issuer_list: Option<String>,
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
            name: value.name,
            specification: value.specification.into(),
            status: value.status.map(Into::into),
        }
    }
}

impl From<base_client_gen::DeviceUpdateEventTelemetry> for Device {
    fn from(value: base_client_gen::DeviceUpdateEventTelemetry) -> Self {
        Device {
            name: value.device_update_event.device.name,
            specification: value.device_update_event.device.specification.into(),
            status: value.device_update_event.device.status.map(Into::into),
        }
    }
}

impl From<base_client_gen::DeviceSpecificationSchema> for DeviceSpecification {
    fn from(value: base_client_gen::DeviceSpecificationSchema) -> Self {
        DeviceSpecification {
            attributes: value.attributes.unwrap_or_default(),
            discovered_device_ref: value.discovered_device_ref,
            enabled: value.enabled,
            endpoints: value.endpoints.map(Into::into).unwrap_or_default(),
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

impl From<base_client_gen::DeviceEndpointSchema> for DeviceEndpoints {
    fn from(value: base_client_gen::DeviceEndpointSchema) -> Self {
        let inbound = match value.inbound {
            Some(inbound_endpoints) => inbound_endpoints
                .into_iter()
                .map(|(k, v)| (k, v.into()))
                .collect(),
            None => HashMap::new(),
        };
        let mut outbound_assigned = HashMap::new();
        let mut outbound_unassigned = HashMap::new();

        if let Some(outbound) = value.outbound {
            for (k, v) in outbound.assigned {
                outbound_assigned.insert(k, v.into());
            }

            if let Some(map) = outbound.unassigned {
                for (k, v) in map {
                    outbound_unassigned.insert(k, v.into());
                }
            }
        }
        DeviceEndpoints {
            inbound,
            outbound_assigned,
            outbound_unassigned,
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

impl From<base_client_gen::TrustSettingsSchema> for TrustSettings {
    fn from(value: base_client_gen::TrustSettingsSchema) -> Self {
        TrustSettings {
            issuer_list: value.issuer_list,
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
    pub config: Option<StatusConfig>,
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
            config: value.config.map(StatusConfig::into),
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
            config: value
                .config
                .map(base_client_gen::DeviceStatusConfigSchema::into),
            endpoints,
        }
    }
}
