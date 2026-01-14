// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

//! Device/Endpoint models for Azure Device Registry operations.
use std::collections::HashMap;

use chrono::{DateTime, Utc};

use crate::azure_device_registry::helper::{ConvertOptionMap, ConvertOptionVec};
use crate::azure_device_registry::{ConfigError, ConfigStatus, RuntimeHealth};
use crate::azure_device_registry::{
    adr_base_gen::adr_base_service::client as base_client_gen,
    adr_base_gen::adr_base_service::service as base_service_gen,
    device_discovery_gen::device_discovery_service::client as discovery_client_gen,
};

// TODO: bidirectional transforms

// ~~~~~~~~~~~~~~~~~~~Device Endpoint DTDL Equivalent Structs~~~~

/// Represents a Device resource, modeled after the devices.namespaces.deviceregistry.microsoft.com CRD in Kubernetes.
#[derive(Debug, Clone)]
pub struct Device {
    /// A set of key-value pairs that contain custom attributes set by the customer.
    pub attributes: HashMap<String, String>, // if None in generated model, we can represent as empty hashmap
    /// Reference to a device. Populated only if the device had been created from discovery flow. Discovered device name must be provided.
    pub discovered_device_ref: Option<String>,
    /// Indicates if the resource and identity are enabled or not. A disabled device cannot authenticate with Microsoft Entra ID.
    pub enabled: Option<bool>,
    /// Connection endpoint url a device can use to connect to a service.
    pub endpoints: Option<DeviceEndpoints>,
    /// The Device ID provided by the customer.
    pub external_device_id: Option<String>,
    /// A timestamp (in UTC) that is updated each time the resource is modified.
    pub last_transition_time: Option<DateTime<Utc>>,
    /// Device manufacturer.
    pub manufacturer: Option<String>,
    /// Device model.
    pub model: Option<String>,
    /// Device operating system.
    pub operating_system: Option<String>,
    /// Device operating system version.
    pub operating_system_version: Option<String>,
    /// A unique identifier for this resource.
    pub uuid: Option<String>,
    /// An integer that is incremented each time the resource is modified.
    pub version: Option<u64>,
}

#[derive(Debug, Clone)]
/// Represents a discovered device in the Azure Device Registry service.
pub struct DiscoveredDevice {
    /// A set of key-value pairs that contain custom attributes set by the customer.
    pub attributes: HashMap<String, String>, // if empty hashmap, we can represent as None on generated model
    /// Connection endpoint URL a device can use to connect to a service.
    pub endpoints: Option<DiscoveredDeviceEndpoints>,
    /// The unique identifier of the device.
    pub external_device_id: Option<String>,
    /// Device manufacturer.
    pub manufacturer: Option<String>,
    /// Device model.
    pub model: Option<String>,
    /// Device operating system name.
    pub operating_system: Option<String>,
    /// Device operating system version.
    pub operating_system_version: Option<String>,
}

#[derive(Debug, Clone)]
/// Represents the endpoints of a device in the Azure Device Registry service.
pub struct DeviceEndpoints {
    /// The 'inbound' Field.
    pub inbound: HashMap<String, InboundEndpoint>, // if None on generated model, we can represent as empty hashmap. Might be able to change this to a single InboundEndpoint
    /// Set of endpoints for device to connect to.
    pub outbound: Option<OutboundEndpoints>,
}

/// Represents the endpoints of a discovered device in the Azure Device Registry service.
#[derive(Debug, Clone)]
pub struct DiscoveredDeviceEndpoints {
    /// Set of endpoints to connect to the device.
    pub inbound: HashMap<String, DiscoveredInboundEndpoint>, // if empty, we can represent as None on generated model.
    /// Property bag contains the device's outbound endpoints
    pub outbound: Option<DiscoveredOutboundEndpoints>,
}

/// Represents the outbound endpoints of a device in the Azure Device Registry service.
#[derive(Debug, Clone)]
pub struct OutboundEndpoints {
    /// Device messaging endpoint model.
    pub assigned: HashMap<String, OutboundEndpoint>,
    /// Device messaging endpoint model.
    pub unassigned: HashMap<String, OutboundEndpoint>,
}

/// Represents the outbound endpoints of a discovered device in the Azure Device Registry service.
#[derive(Debug, Clone, Default)]
pub struct DiscoveredOutboundEndpoints {
    ///  Endpoints the device can connect to.
    pub assigned: HashMap<String, OutboundEndpoint>,
}

/// Represents an outbound endpoint of a device in the Azure Device Registry service.
#[derive(Debug, Clone)]
pub struct OutboundEndpoint {
    /// The endpoint address to connect to.
    pub address: String,
    /// Type of connection used for the messaging endpoint.
    pub endpoint_type: Option<String>,
}

/// Represents an inbound endpoint of a device in the Azure Device Registry service.
#[derive(Debug, Clone)]
pub struct InboundEndpoint {
    /// Stringified JSON that contains connectivity type specific further configuration (e.g. OPC UA, ONVIF).
    pub additional_configuration: Option<String>,
    /// The endpoint address & port. This can be either an IP address (e.g., 192.168.1.1) or a fully qualified domain name (FQDN, e.g., server.example.com).
    pub address: String,
    /// Defines the client authentication mechanism to the server.
    pub authentication: Authentication,
    /// Type of connection endpoint.
    pub endpoint_type: String,
    /// Defines server trust settings for the endpoint.
    pub trust_settings: Option<TrustSettings>,
    /// Version associated with device endpoint.
    pub version: Option<String>,
}

/// Represents an inbound endpoint of a discovered device in the Azure Device Registry service.
#[derive(Debug, Clone)]
pub struct DiscoveredInboundEndpoint {
    /// Stringified JSON that contains connectivity type specific further configuration (e.g. OPC UA, Modbus, ONVIF).
    pub additional_configuration: Option<String>,
    /// The endpoint address & port. This can be either an IP address (e.g., 192.168.1.1) or a fully qualified domain name (FQDN, e.g., server.example.com).
    pub address: String,
    /// Type of connection endpoint.
    pub endpoint_type: String,
    /// The timestamp (in UTC) when the endpoint was discovered.
    pub last_updated_on: Option<DateTime<Utc>>,
    /// List of supported authentication methods supported by device for Inbound connections.
    pub supported_authentication_methods: Vec<String>,
    /// Version associated with the device endpoint.
    pub version: Option<String>,
}

#[derive(Debug, Clone)]
/// Represents the trust settings for an endpoint.
pub struct TrustSettings {
    /// Secret reference to certificates list to trust.
    pub trust_list: Option<String>,
}

#[derive(Debug, Clone, Default)]
/// Defines the method to authenticate the user of the client at the server.
pub enum Authentication {
    #[default]
    /// Represents anonymous authentication.
    Anonymous,
    /// Represents authentication using an x509 certificate.
    Certificate {
        /// The name of the secret containing the certificate and private key (e.g. stored as .der/.pem or .der/.pfx).
        certificate_secret_name: String,
        /// A reference to the secret containing the combined intermediate certificates in PEM format.
        intermediate_certificates_secret_name: Option<String>,
        /// A reference to the secret containing the certificate private key in PEM or DER format.
        key_secret_name: Option<String>,
    },
    /// Represents authentication using a username and password.
    UsernamePassword {
        /// The name of the secret containing the password.
        password_secret_name: String,
        /// The name of the secret containing the username.
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
            base_client_gen::MethodSchema::Certificate => {
                if let Some(x509credential) = value.x509credentials {
                    Authentication::Certificate {
                        certificate_secret_name: x509credential.certificate_secret_name,
                        intermediate_certificates_secret_name: x509credential
                            .intermediate_certificates_secret_name,
                        key_secret_name: x509credential.key_secret_name,
                    }
                } else {
                    log::error!(
                        "Authentication method 'Certificate', but no 'x509Credentials' provided"
                    );
                    Authentication::Certificate {
                        certificate_secret_name: String::new(),
                        intermediate_certificates_secret_name: None,
                        key_secret_name: None,
                    }
                }
            }

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
    ///  The configuration status of the device.
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

impl From<RuntimeHealth> for base_service_gen::DeviceEndpointRuntimeHealthEventTelemetry {
    fn from(value: RuntimeHealth) -> Self {
        base_service_gen::DeviceEndpointRuntimeHealthEventTelemetry {
            device_endpoint_runtime_health_event:
                base_service_gen::DeviceEndpointRuntimeHealthEventSchema {
                    runtime_health: value.into(),
                },
        }
    }
}
