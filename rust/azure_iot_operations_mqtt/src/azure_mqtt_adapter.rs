// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

//! Adapter layer for the azuremqtt (TODO: Rename to whatever is the azuremqtt crate's final name) crate

use std::{fmt, fs, time::Duration};
use std::num::{NonZero, NonZeroU32};

use azure_mqtt::packet::{AuthenticationInfo, ConnectOptions, ConnectProperties, SessionExpiryInterval};
use azure_mqtt::client::{ClientOptions, ConnectionTransportConfig, ConnectionTransportTlsConfig};
use openssl::{
    pkey::{PKey, Private},
    x509::X509,
};
use thiserror::Error;

use crate::connection_settings::MqttConnectionSettings;

type ClientCert = (X509, PKey<Private>, Vec<X509>);

fn create_connect_options(username: Option<String>, password: Option<String>, password_file: Option<String>) -> Result<ConnectOptions, ConnectionSettingsAdapterError> {
    let password = if let Some(password_file) = password_file {
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
        password
    };

    Ok(ConnectOptions {
        username,
        password,
        ..Default::default()
    })
}

fn create_connect_properties(session_expiry: Duration, receive_packet_size_max: Option<u32>, receive_max: u16, user_properties: Vec<(String, String)>) -> Result<ConnectProperties, ConnectionSettingsAdapterError> {
    // Session Expiry
    let session_expiry_secs = session_expiry.as_secs().try_into().map_err(|e| {
        ConnectionSettingsAdapterError {
            msg: "cannot convert to u32".to_string(),
            field: ConnectionSettingsField::SessionExpiry(session_expiry),
            source: Some(Box::new(e)),
        }
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
    let receive_maximum = NonZero::new(receive_max).ok_or_else(|| {
        ConnectionSettingsAdapterError {
            msg: "receive_max must be > 0".to_string(),
            field: ConnectionSettingsField::ReceiveMax(receive_max),
            source: None,
        }
    })?;

    Ok(ConnectProperties {
        session_expiry_interval: SessionExpiryInterval::Duration(session_expiry_secs),
        receive_maximum,
        maximum_packet_size,
        user_properties,
        ..Default::default()
    })
}

fn create_connection_transport_config(ca_file: Option<String>, cert_file: Option<String>, key_file: Option<String>, key_password_file: Option<String>, use_tls: bool, hostname: String, tcp_port: u16) -> Result<ConnectionTransportConfig, ConnectionSettingsAdapterError> {
    if use_tls {
        let (client_cert, ca_trust_bundle) = tls_config(
            ca_file,
            cert_file,
            key_file,
            key_password_file,
        )
        .map_err(|e| ConnectionSettingsAdapterError {
            msg: "tls config error".to_string(),
            field: ConnectionSettingsField::UseTls(true),
            source: Some(Box::new(TlsError {
                msg: e.to_string(),
                source: Some(e),
            })),
        })?;

        let config = ConnectionTransportTlsConfig::new(
            client_cert,
            ca_trust_bundle,
        )
        .map_err(|e| ConnectionSettingsAdapterError {
            msg: "failed to create TLS config".to_string(),
            field: ConnectionSettingsField::UseTls(true),
            source: Some(Box::new(TlsError {
                msg: e.to_string(),
                source: Some(e.into()),
            })),
        })?;

        Ok(ConnectionTransportConfig::Tls {
            config,
            hostname,
            port: tcp_port,
        })
    } else {
        Ok(ConnectionTransportConfig::Tcp {
            hostname,
            port: tcp_port,
        })
    }
}

pub struct AzureMqttConnectParameters {
  pub initial_clean_start: bool,
  pub keep_alive: Duration,
  pub connection_transport_config: azure_mqtt::client::ConnectionTransportConfig,
  pub connect_options: azure_mqtt::packet::ConnectOptions,
  pub connect_properties: azure_mqtt::packet::ConnectProperties,
  sat_file: Option<String>,
}

impl AzureMqttConnectParameters {
    pub fn authentication_info(&self) -> Result<Option<AuthenticationInfo>, ConnectionSettingsAdapterError> {
      if let Some(sat_file) = &self.sat_file {
        let sat_auth =
          fs::read(sat_file).map_err(|e| ConnectionSettingsAdapterError {
              msg: "cannot read sat auth file".to_string(),
              field: ConnectionSettingsField::SatAuthFile(sat_file.clone()),
              source: Some(Box::new(e)),
          })?;
        Ok(Some(AuthenticationInfo {
          method: "K8S-SAT".to_string(),
          data: Some(sat_auth.into()),
        }))
      }
      else {
        Ok(None)
      }
    }
}

impl MqttConnectionSettings {
    pub fn to_azure_mqtt_connect_parameters(
        self,
        user_properties: Vec<(String, String)>, // TODO: This is passed in when creating the session
        outgoing_max: usize, // TODO: this is passed in from the session options
    ) -> Result<
        (
            azure_mqtt::client::ClientOptions, // TODO: Single struct minus this guy
            AzureMqttConnectParameters,
        ),
        ConnectionSettingsAdapterError,
    > {
        let client_options = ClientOptions {
          client_id: Some(self.client_id),
          queue_size: outgoing_max,
        };

        let connect_options = create_connect_options(self.username, self.password, self.password_file)?;

        let connect_properties = create_connect_properties(
            self.session_expiry,
            self.receive_packet_size_max,
            self.receive_max,
            user_properties,
        )?;

        let connection_transport_config = create_connection_transport_config(
            self.ca_file,
            self.cert_file,
            self.key_file,
            self.key_password_file,
            self.use_tls,
            self.hostname,
            self.tcp_port,
        )?;

        Ok(
          (
            client_options,
            AzureMqttConnectParameters {
              initial_clean_start: self.clean_start,
              keep_alive: self.keep_alive,
              connection_transport_config,
              connect_options,
              connect_properties,
              sat_file: self.sat_file,
            }
          )
        )
    }
}


fn tls_config(
    ca_file: Option<String>,
    cert_file: Option<String>,
    key_file: Option<String>,
    key_password_file: Option<String>,
) -> Result<(Option<ClientCert>, Vec<X509>), anyhow::Error> {
    // Handle CA trust bundle
    let ca_trust_bundle = if let Some(ca_file) = ca_file {
        let ca_pem = fs::read(ca_file)?;
        X509::stack_from_pem(&ca_pem)?
    } else {
        // If no CA file is provided, we could use system certs or return an error
        // For now, return an error as azure_mqtt requires a CA trust bundle
        return Err(anyhow::anyhow!(
            "CA file is required for azure_mqtt TLS configuration"
        ));
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

#[derive(Error, Debug)]
#[error("{msg}: {field}")]
pub struct ConnectionSettingsAdapterError {
    msg: String,
    field: ConnectionSettingsField,
    #[source]
    source: Option<Box<dyn std::error::Error>>,
}

#[derive(Debug)]
pub enum ConnectionSettingsField {
    SessionExpiry(Duration),
    PasswordFile(String),
    UseTls(bool),
    ReceivePacketSizeMax(u32),
    ReceiveMax(u16),
    SatAuthFile(String)
}

impl fmt::Display for ConnectionSettingsField {
    fn fmt(&self, f: &mut fmt::Formatter<'_>) -> fmt::Result {
        match self {
            ConnectionSettingsField::SessionExpiry(v) => write!(f, "Session Expiry: {v:?}"),
            ConnectionSettingsField::PasswordFile(v) => write!(f, "Password File: {v:?}"),
            ConnectionSettingsField::UseTls(v) => write!(f, "Use TLS: {v:?}"),
            ConnectionSettingsField::ReceivePacketSizeMax(v) => write!(f, "Receive Packet Size Max: {v}"),
            ConnectionSettingsField::ReceiveMax(v) => write!(f, "Receive Max: {v}"),
            ConnectionSettingsField::SatAuthFile(v) => write!(f, "SAT Auth File: {v:?}"),
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
