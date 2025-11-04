// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

//! Adapter layer for the azuremqtt (TODO: Rename to whatever is the azuremqtt crate's final name) crate

use std::fs;
use std::num::{NonZero, NonZeroU32};

use azure_mqtt::packet::SessionExpiryInterval;
use openssl::{
    pkey::{PKey, Private},
    x509::X509,
};

use crate::connection_settings::MqttConnectionSettings;

impl From<MqttConnectionSettings> for azure_mqtt::client::ClientOptions {
    fn from(value: MqttConnectionSettings) -> Self {
        azure_mqtt::client::ClientOptions {
            client_id: Some(value.client_id),
            queue_size: 100, // ASK: Does this correspond to outgoing_max from the session?
        }
    }
}

impl From<MqttConnectionSettings> for azure_mqtt::packet::ConnectOptions {
    fn from(value: MqttConnectionSettings) -> Self {
        azure_mqtt::packet::ConnectOptions {
            username: value.username,
            password: value.password,
            ..Default::default() // ASK: Do we want will option? I'll say no
        }
    }
}

impl TryFrom<MqttConnectionSettings> for azure_mqtt::packet::ConnectProperties {
    type Error = String;

    fn try_from(value: MqttConnectionSettings) -> Result<Self, Self::Error> {
        let session_expiry_secs = value.session_expiry.as_secs().try_into().map_err(|_| {
            format!(
                "Session expiry duration {} seconds exceeds u32::MAX",
                value.session_expiry.as_secs()
            )
        })?;

        let maximum_packet_size = match value.receive_packet_size_max {
            Some(v) => NonZeroU32::new(v).ok_or("receive_packet_size_max must be > 0")?,
            None => NonZeroU32::MAX,
        };

        Ok(azure_mqtt::packet::ConnectProperties {
            session_expiry_interval: SessionExpiryInterval::Duration(session_expiry_secs),
            receive_maximum: NonZero::new(value.receive_max).ok_or("receive_max must be > 0")?, // ASK: from the connection settings we should be prohibiting values of 0 or greater too
            maximum_packet_size,
            topic_alias_maximum: 0,
            // request_response_information: false, // ASK: I assume these are left as the defaults?
            // request_problem_information: true,
            // user_properties: Vec::new(),
            ..Default::default()
        })
    }
}

fn az_mqtt_tls_config(
    ca_file: Option<String>,
    cert_file: Option<String>,
    key_file: Option<String>,
    key_password_file: Option<String>,
) -> Result<(Option<(X509, PKey<Private>, Vec<X509>)>, Vec<X509>), anyhow::Error> {
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

impl TryFrom<MqttConnectionSettings> for azure_mqtt::client::ConnectionTransportConfig {
    type Error = anyhow::Error;

    fn try_from(value: MqttConnectionSettings) -> Result<Self, Self::Error> {
        if value.use_tls {
            let (client_cert, ca_trust_bundle) = az_mqtt_tls_config(
                value.ca_file,
                value.cert_file,
                value.key_file,
                value.key_password_file,
            )?;
            let config = azure_mqtt::client::ConnectionTransportTlsConfig::new(
                client_cert,
                ca_trust_bundle,
            )?;
            Ok(azure_mqtt::client::ConnectionTransportConfig::Tls {
                config,
                hostname: value.hostname,
                port: value.tcp_port,
            })
        } else {
            Ok(azure_mqtt::client::ConnectionTransportConfig::Tcp {
                hostname: value.hostname,
                port: value.tcp_port,
            })
        }
    }
}
