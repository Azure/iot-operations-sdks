// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

//! Adapter layer for the `azure_mqtt` (TODO: rename this once settled) crate

use std::num::{NonZero, NonZeroU16, NonZeroU32};
use std::{fmt, fs, time::Duration};

use crate::azure_mqtt::client::{
    ClientOptions, ConnectionTransportConfig, ConnectionTransportTlsConfig, ConnectionTransportType,
};
use crate::azure_mqtt::packet::{ConnectProperties, SessionExpiryInterval, Will};
use bytes::Bytes;
use openssl::{
    pkey::{PKey, Private},
    x509::X509,
};
use thiserror::Error;

use crate::aio::connection_settings::MqttConnectionSettings;
#[cfg(feature = "test-utils")]
use crate::test_utils::InjectedPacketChannels;

// NOTE: This module combines two concepts - translating the MqttConnectionSettings into azure_mqtt constructs,
// and the configuration OF those constructs. This should ideally be separated, and doing so will be necessary
// in order to support a more generic `crate::session::SessionOptions`.

type ClientCert = (X509, PKey<Private>, Vec<X509>);

#[derive(Error, Debug)]
#[error("{msg}: {field}")]
pub struct ConnectionSettingsAdapterError {
    pub(crate) msg: String,
    pub(crate) field: ConnectionSettingsField,
    #[source]
    pub(crate) source: Option<Box<dyn std::error::Error + Send + 'static>>,
}

#[derive(Debug, Clone, Eq, PartialEq)]
pub enum ConnectionSettingsField {
    SessionExpiry(Duration),
    PasswordFile(String),
    UseTls(bool),
    KeepAlive(Duration),
    ReceivePacketSizeMax(u32),
    ReceiveMax(u16),
    SatFile(String),
}

impl fmt::Display for ConnectionSettingsField {
    fn fmt(&self, f: &mut fmt::Formatter<'_>) -> fmt::Result {
        match self {
            ConnectionSettingsField::SessionExpiry(v) => write!(f, "Session Expiry: {v:?}"),
            ConnectionSettingsField::PasswordFile(v) => write!(f, "Password File: {v:?}"),
            ConnectionSettingsField::UseTls(v) => write!(f, "Use TLS: {v:?}"),
            ConnectionSettingsField::KeepAlive(v) => write!(f, "Keep Alive: {v:?}"),
            ConnectionSettingsField::ReceivePacketSizeMax(v) => {
                write!(f, "Receive Packet Size Max: {v}")
            }
            ConnectionSettingsField::ReceiveMax(v) => write!(f, "Receive Max: {v}"),
            ConnectionSettingsField::SatFile(v) => write!(f, "SAT File: {v:?}"),
        }
    }
}

#[derive(Error, Debug)]
#[error("{msg}")]
pub struct TlsError {
    msg: String,
    source: Option<anyhow::Error>,
}

impl TlsError {
    pub fn new(msg: &str) -> Self {
        TlsError {
            msg: msg.to_string(),
            source: None,
        }
    }
}

/// Create [`ConnectProperties`]
fn create_connect_properties(
    session_expiry: Duration,
    receive_packet_size_max: Option<u32>,
    receive_max: u16,
    user_properties: Vec<(String, String)>,
) -> Result<ConnectProperties, ConnectionSettingsAdapterError> {
    // Session Expiry
    let session_expiry_secs =
        session_expiry
            .as_secs()
            .try_into()
            .map_err(|e| ConnectionSettingsAdapterError {
                msg: "cannot convert to u32".to_string(),
                field: ConnectionSettingsField::SessionExpiry(session_expiry),
                source: Some(Box::new(e)),
            })?;

    // Maximum Packet Size
    let maximum_packet_size = match receive_packet_size_max {
        Some(v) => NonZeroU32::new(v).ok_or_else(|| ConnectionSettingsAdapterError {
            msg: "receive_packet_size_max must be > 0".to_string(),
            field: ConnectionSettingsField::ReceivePacketSizeMax(v),
            source: None,
        })?,
        None => NonZeroU32::MAX,
    };

    // Receive Maximum
    let receive_maximum =
        NonZero::new(receive_max).ok_or_else(|| ConnectionSettingsAdapterError {
            msg: "receive_max must be > 0".to_string(),
            field: ConnectionSettingsField::ReceiveMax(receive_max),
            source: None,
        })?;

    Ok(ConnectProperties {
        session_expiry_interval: SessionExpiryInterval::Duration(session_expiry_secs),
        receive_maximum,
        maximum_packet_size,
        user_properties,
        ..Default::default()
    })
}

/// Create [`ConnectionTransportConfig`]
#[allow(clippy::too_many_arguments)]
fn create_connection_transport_config(
    ca_file: Option<String>,
    cert_file: Option<String>,
    key_file: Option<String>,
    key_password_file: Option<String>,
    use_tls: bool,
    hostname: String,
    tcp_port: u16,
    timeout: Duration,
) -> Result<ConnectionTransportConfig, ConnectionSettingsAdapterError> {
    let transport_type = if use_tls {
        let (client_cert, ca_trust_bundle) =
            tls_config(ca_file, cert_file, key_file, key_password_file).map_err(|e| {
                ConnectionSettingsAdapterError {
                    msg: "tls config error".to_string(),
                    field: ConnectionSettingsField::UseTls(true),
                    source: Some(Box::new(TlsError {
                        msg: e.to_string(),
                        source: Some(e),
                    })),
                }
            })?;

        let config =
            ConnectionTransportTlsConfig::new(client_cert, ca_trust_bundle).map_err(|e| {
                ConnectionSettingsAdapterError {
                    msg: "failed to create TLS config".to_string(),
                    field: ConnectionSettingsField::UseTls(true),
                    source: Some(Box::new(TlsError {
                        msg: e.to_string(),
                        source: Some(e.into()),
                    })),
                }
            })?;

        ConnectionTransportType::Tls {
            config,
            hostname,
            port: tcp_port,
        }
    } else {
        ConnectionTransportType::Tcp {
            hostname,
            port: tcp_port,
        }
    };

    Ok(ConnectionTransportConfig {
        transport_type,
        timeout: Some(timeout),
    })
}

/// Parameters for establishing an MQTT connection using the `azure_mqtt` crate
pub struct AzureMqttConnectParameters {
    /// Initial clean start flag, use ONLY during the initial connection
    pub initial_clean_start: bool,
    /// Keep alive duration
    pub keep_alive: crate::azure_mqtt::client::KeepAliveConfig,
    /// Will message
    pub will: Option<Will>,
    /// Username
    pub username: Option<String>,
    /// Password
    pub password: Option<Bytes>,
    /// Connect properties
    pub connect_properties: ConnectProperties,
    /// Connection timeout duration
    pub connection_timeout: Duration,

    /// properties used to create the `ConnectionTransportConfig` on demand
    ca_file: Option<String>,
    cert_file: Option<String>,
    key_file: Option<String>,
    key_password_file: Option<String>,
    use_tls: bool,
    hostname: String,
    tcp_port: u16,

    /// Injected packet channels for test purposes. Can be None to use normal transport config.
    #[cfg(feature = "test-utils")]
    pub injected_packet_channels: Option<InjectedPacketChannels>,
}

impl AzureMqttConnectParameters {
    /// Create a new `ConnectionTransportConfig` from stored parameters
    ///
    /// # Errors
    /// Returns [`ConnectionSettingsAdapterError`] if there is an error creating the config
    pub fn connection_transport_config(
        &self,
    ) -> Result<ConnectionTransportConfig, ConnectionSettingsAdapterError> {
        #[cfg(feature = "test-utils")]
        if let Some(injected_packet_channels) = &self.injected_packet_channels {
            let (incoming_packets_tx, incoming_packets_rx) = tokio::sync::mpsc::unbounded_channel();
            let (outgoing_packets_tx, outgoing_packets_rx) = tokio::sync::mpsc::unbounded_channel();
            injected_packet_channels
                .incoming_packets_tx
                .set_new_tx(incoming_packets_tx);
            injected_packet_channels
                .outgoing_packets_rx
                .set_new_rx(outgoing_packets_rx);

            return Ok(ConnectionTransportConfig {
                transport_type: ConnectionTransportType::Test {
                    incoming_packets: incoming_packets_rx,
                    outgoing_packets: outgoing_packets_tx,
                },
                timeout: Some(self.connection_timeout),
            });
        }

        create_connection_transport_config(
            self.ca_file.clone(),
            self.cert_file.clone(),
            self.key_file.clone(),
            self.key_password_file.clone(),
            self.use_tls,
            self.hostname.clone(),
            self.tcp_port,
            self.connection_timeout,
        )
    }
}

impl MqttConnectionSettings {
    /// Convert to Azure MQTT connect parameters
    ///
    /// # Parameters
    /// - `user_properties`: User properties to include in the connect properties
    /// - `outgoing_max`: Outgoing message queue size (100 is a good default value)
    ///
    /// # Errors
    /// Returns [`ConnectionSettingsAdapterError`] if any conversion fails
    pub fn to_azure_mqtt_connect_parameters(
        self,
        user_properties: Vec<(String, String)>,
        max_packet_identifier: crate::azure_mqtt::packet::PacketIdentifier,
        publish_qos0_queue_size: usize,
        publish_qos1_qos2_queue_size: usize,
        #[cfg(feature = "test-utils")] injected_packet_channels: Option<InjectedPacketChannels>,
    ) -> Result<(ClientOptions, AzureMqttConnectParameters), ConnectionSettingsAdapterError> {
        let client_options = ClientOptions {
            client_id: Some(self.client_id),
            max_packet_identifier,
            publish_qos0_queue_size,
            publish_qos1_qos2_queue_size,
        };

        let ping_after =
            NonZeroU16::new(u16::try_from(self.keep_alive.as_secs()).map_err(|e| {
                ConnectionSettingsAdapterError {
                    msg: "cannot convert keep_alive to u16".to_string(),
                    field: ConnectionSettingsField::KeepAlive(self.keep_alive),
                    source: Some(Box::new(e)),
                }
            })?)
            .ok_or(ConnectionSettingsAdapterError {
                msg: "keep_alive must be greater than zero".to_string(),
                field: ConnectionSettingsField::KeepAlive(self.keep_alive),
                source: None,
            })?;

        let keep_alive = crate::azure_mqtt::client::KeepAliveConfig::Duration {
            ping_after,
            response_timeout: Duration::from_secs(2), // TODO: Make configurable?
        };

        let password = if let Some(password_file) = self.password_file {
            match fs::read_to_string(&password_file) {
                Ok(password) => Some(password),
                Err(e) => {
                    return Err(ConnectionSettingsAdapterError {
                        msg: "cannot read password file".to_string(),
                        field: ConnectionSettingsField::PasswordFile(password_file),
                        source: Some(Box::new(e)),
                    });
                }
            }
        } else {
            self.password
        }
        .map(Bytes::from);

        let connect_properties = create_connect_properties(
            self.session_expiry,
            self.receive_packet_size_max,
            self.receive_max,
            user_properties,
        )?;

        // not used, but we want to validate failures early.
        let _connection_transport_config = create_connection_transport_config(
            self.ca_file.clone(),
            self.cert_file.clone(),
            self.key_file.clone(),
            self.key_password_file.clone(),
            self.use_tls,
            self.hostname.clone(),
            self.tcp_port,
            self.connection_timeout,
        )?;

        Ok((
            client_options,
            AzureMqttConnectParameters {
                initial_clean_start: self.clean_start,
                keep_alive,
                will: None,
                username: self.username,
                password,
                ca_file: self.ca_file,
                cert_file: self.cert_file,
                key_file: self.key_file,
                key_password_file: self.key_password_file,
                use_tls: self.use_tls,
                hostname: self.hostname,
                tcp_port: self.tcp_port,
                connect_properties,
                connection_timeout: self.connection_timeout,
                #[cfg(feature = "test-utils")]
                injected_packet_channels,
            },
        ))
    }
}

fn read_root_ca_certs(ca_file: String) -> Result<Vec<X509>, anyhow::Error> {
    let mut ca_certs = Vec::new();
    let ca_pem = fs::read(ca_file)?;

    let certs = &mut X509::stack_from_pem(&ca_pem)?;
    ca_certs.append(certs);

    if ca_certs.is_empty() {
        Err(TlsError::new("No CA certs available in CA File"))?;
    }

    ca_certs.sort();
    ca_certs.dedup();

    Ok(ca_certs)
}

/// Create TLS configuration. Returns an optional client certificate (main cert, private key, chain certs)
/// and CA trust bundle as a tuple.
fn tls_config(
    ca_file: Option<String>,
    cert_file: Option<String>,
    key_file: Option<String>,
    key_password_file: Option<String>,
) -> Result<(Option<ClientCert>, Vec<X509>), anyhow::Error> {
    // Handle CA trust bundle
    let ca_trust_bundle = if let Some(ca_file) = ca_file {
        // CA File
        read_root_ca_certs(ca_file)?

        // CA Revocation Check TODO: add this back in
    } else {
        // If no CA file is provided, return empty bundle
        Vec::new()
    };

    // Handle client certificate
    let client_cert = if let (Some(cert_file), Some(key_file)) = (cert_file, key_file) {
        // Read and parse certificate chain
        let cert_file_contents = fs::read(cert_file)?;
        let cert_chain = X509::stack_from_pem(&cert_file_contents)?;

        if cert_chain.is_empty() {
            return Err(anyhow::anyhow!("No certificates found in cert file"));
        }

        // The first cert is the main client cert, the rest are chain certs
        let main_cert = cert_chain[0].clone();
        let chain_certs = cert_chain.into_iter().skip(1).collect();

        // Read and process private key
        let private_key = {
            let key_file_contents = fs::read(key_file)?;
            if let Some(key_password_file) = key_password_file {
                let key_password_file_contents = fs::read(key_password_file)?;
                PKey::private_key_from_pem_passphrase(
                    &key_file_contents,
                    &key_password_file_contents,
                )?
            } else {
                PKey::private_key_from_pem(&key_file_contents)?
            }
        };

        Some((main_cert, private_key, chain_certs))
    } else {
        None
    };

    Ok((client_cert, ca_trust_bundle))
}

// -------------------------------------------

#[cfg(test)]
mod tests {
    use crate::azure_mqtt;
    use std::path::PathBuf;
    use std::time::Duration;

    use crate::aio::connection_settings::MqttConnectionSettingsBuilder;

    #[test]
    fn test_azure_mqtt_config_no_tls() {
        let connection_settings = MqttConnectionSettingsBuilder::default()
            .client_id("test_client_id".to_string())
            .hostname("test_host".to_string())
            .use_tls(false)
            .build()
            .unwrap();

        let result = connection_settings.to_azure_mqtt_connect_parameters(
            vec![],
            azure_mqtt::packet::PacketIdentifier::MAX,
            100,
            100,
            None,
        );
        assert!(result.is_ok());
    }

    #[test]
    fn test_azure_mqtt_config_with_username_password() {
        let connection_settings = MqttConnectionSettingsBuilder::default()
            .client_id("test_client_id".to_string())
            .hostname("test_host".to_string())
            .use_tls(false)
            .username("test_username".to_string())
            .password("test_password".to_string())
            .build()
            .unwrap();

        let result = connection_settings.to_azure_mqtt_connect_parameters(
            vec![],
            azure_mqtt::packet::PacketIdentifier::MAX,
            100,
            100,
            None,
        );
        assert!(result.is_ok());
    }

    #[test]
    fn test_azure_mqtt_config_with_username_only() {
        let connection_settings = MqttConnectionSettingsBuilder::default()
            .client_id("test_client_id".to_string())
            .hostname("test_host".to_string())
            .use_tls(false)
            .username("test_username".to_string())
            .build()
            .unwrap();

        let result = connection_settings.to_azure_mqtt_connect_parameters(
            vec![],
            azure_mqtt::packet::PacketIdentifier::MAX,
            100,
            100,
            None,
        );
        assert!(result.is_ok());
    }

    #[test]
    fn test_azure_mqtt_config_with_password_file() {
        let mut password_file_path = PathBuf::from(env!("CARGO_MANIFEST_DIR"));
        password_file_path.push("../../eng/test/dummy_credentials/TestMqttPasswordFile.txt");

        let connection_settings = MqttConnectionSettingsBuilder::default()
            .client_id("test_client_id".to_string())
            .hostname("test_host".to_string())
            .use_tls(false)
            .username("test_username".to_string())
            .password_file(password_file_path.into_os_string().into_string().unwrap())
            .build()
            .unwrap();

        let result = connection_settings.to_azure_mqtt_connect_parameters(
            vec![],
            azure_mqtt::packet::PacketIdentifier::MAX,
            100,
            100,
            None,
        );
        assert!(result.is_ok());
    }

    #[test]
    fn test_azure_mqtt_config_with_user_properties() {
        let connection_settings = MqttConnectionSettingsBuilder::default()
            .client_id("test_client_id".to_string())
            .hostname("test_host".to_string())
            .use_tls(false)
            .build()
            .unwrap();

        let user_properties = vec![
            ("prop1".to_string(), "value1".to_string()),
            ("prop2".to_string(), "value2".to_string()),
        ];

        let result = connection_settings.to_azure_mqtt_connect_parameters(
            user_properties,
            azure_mqtt::packet::PacketIdentifier::MAX,
            100,
            100,
            None,
        );
        assert!(result.is_ok());
    }

    #[test]
    fn test_azure_mqtt_config_with_custom_settings() {
        let connection_settings = MqttConnectionSettingsBuilder::default()
            .client_id("test_client_id".to_string())
            .hostname("test_host".to_string())
            .tcp_port(8883u16)
            .use_tls(false)
            .clean_start(true)
            .keep_alive(Duration::from_secs(120))
            .session_expiry(Duration::from_secs(3600))
            .receive_max(50u16)
            .receive_packet_size_max(Some(1024))
            .build()
            .unwrap();

        let result = connection_settings.to_azure_mqtt_connect_parameters(
            vec![],
            azure_mqtt::packet::PacketIdentifier::new(500).unwrap(),
            200,
            200,
            None,
        );
        assert!(result.is_ok());
    }

    #[test]
    fn test_azure_mqtt_config_with_ca_file() {
        let mut ca_file_path = PathBuf::from(env!("CARGO_MANIFEST_DIR"));
        ca_file_path.push("../../eng/test/dummy_credentials/TestCa.txt");

        let connection_settings = MqttConnectionSettingsBuilder::default()
            .client_id("test_client_id".to_string())
            .hostname("test_host".to_string())
            .ca_file(ca_file_path.into_os_string().into_string().unwrap())
            .build()
            .unwrap();

        let result = connection_settings.to_azure_mqtt_connect_parameters(
            vec![],
            azure_mqtt::packet::PacketIdentifier::MAX,
            100,
            100,
            None,
        );
        assert!(result.is_ok());
    }

    #[test]
    fn test_azure_mqtt_config_with_ca_file_plus_cert() {
        let mut dir = PathBuf::from(env!("CARGO_MANIFEST_DIR"));
        dir.push("../../eng/test/dummy_credentials/");
        let ca_file = dir.join("TestCa.txt");
        let cert_file = dir.join("TestCert1Pem.txt");
        let key_file = dir.join("TestCert1Key.txt");

        let connection_settings = MqttConnectionSettingsBuilder::default()
            .client_id("test_client_id".to_string())
            .hostname("test_host".to_string())
            .ca_file(ca_file.into_os_string().into_string().unwrap())
            .cert_file(cert_file.into_os_string().into_string().unwrap())
            .key_file(key_file.into_os_string().into_string().unwrap())
            .build()
            .unwrap();

        let result = connection_settings.to_azure_mqtt_connect_parameters(
            vec![],
            azure_mqtt::packet::PacketIdentifier::MAX,
            100,
            100,
            None,
        );
        assert!(result.is_ok());
    }

    #[test]
    fn test_azure_mqtt_config_with_cert_only() {
        let mut dir = PathBuf::from(env!("CARGO_MANIFEST_DIR"));
        dir.push("../../eng/test/dummy_credentials/");
        let cert_file = dir.join("TestCert1Pem.txt");
        let key_file = dir.join("TestCert1Key.txt");

        let connection_settings = MqttConnectionSettingsBuilder::default()
            .client_id("test_client_id".to_string())
            .hostname("test_host".to_string())
            .cert_file(cert_file.into_os_string().into_string().unwrap())
            .key_file(key_file.into_os_string().into_string().unwrap())
            .build()
            .unwrap();

        let result = connection_settings.to_azure_mqtt_connect_parameters(
            vec![],
            azure_mqtt::packet::PacketIdentifier::MAX,
            100,
            100,
            None,
        );
        assert!(result.is_ok());
    }

    #[test]
    fn test_azure_mqtt_config_with_encrypted_key() {
        let mut dir = PathBuf::from(env!("CARGO_MANIFEST_DIR"));
        dir.push("../../eng/test/dummy_credentials/");
        let ca_file = dir.join("TestCa.txt");
        let cert_file = dir.join("TestCert2Pem.txt");
        let key_file = dir.join("TestCert2KeyEncrypted.txt");
        let key_password_file = dir.join("TestCert2KeyPasswordFile.txt");

        let connection_settings = MqttConnectionSettingsBuilder::default()
            .client_id("test_client_id".to_string())
            .hostname("test_host".to_string())
            .ca_file(ca_file.into_os_string().into_string().unwrap())
            .cert_file(cert_file.into_os_string().into_string().unwrap())
            .key_file(key_file.into_os_string().into_string().unwrap())
            .key_password_file(key_password_file.into_os_string().into_string().unwrap())
            .build()
            .unwrap();

        let result = connection_settings.to_azure_mqtt_connect_parameters(
            vec![],
            azure_mqtt::packet::PacketIdentifier::MAX,
            100,
            100,
            None,
        );
        assert!(result.is_ok());
    }

    #[test]
    fn test_azure_mqtt_config_receive_packet_size_max_none() {
        let connection_settings = MqttConnectionSettingsBuilder::default()
            .client_id("test_client_id".to_string())
            .hostname("test_host".to_string())
            .use_tls(false)
            .receive_packet_size_max(None)
            .build()
            .unwrap();

        let result = connection_settings.to_azure_mqtt_connect_parameters(
            vec![],
            azure_mqtt::packet::PacketIdentifier::MAX,
            100,
            100,
            None,
        );
        assert!(result.is_ok());
        assert_eq!(
            result
                .unwrap()
                .1
                .connect_properties
                .maximum_packet_size
                .get(),
            u32::MAX
        );
    }
}
