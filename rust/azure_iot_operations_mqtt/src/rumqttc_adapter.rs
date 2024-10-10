// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

//! Adapter layer for the rumqttc crate

use std::{
    fmt,
    fs::{self, File},
    io::BufReader,
    sync::Arc,
    time::Duration,
};

use async_trait::async_trait;
use bytes::Bytes;
use openssl::pkey::PKey;
use rumqttc::{
    self,
    tokio_rustls::rustls::{
        client::WebPkiServerVerifier, pki_types::PrivateKeyDer, ClientConfig, RootCertStore,
    },
    Transport,
};
use thiserror::Error;

use crate::connection_settings::MqttConnectionSettings;
use crate::control_packet::{
    AuthProperties, Publish, PublishProperties, QoS, SubscribeProperties, UnsubscribeProperties,
};
use crate::error::{ClientError, ConnectionError};
use crate::interface::{
    CompletionToken, Event, InternalClient, ManualAck, MqttAck, MqttDisconnect, MqttEventLoop,
    MqttPubSub,
};

pub type ClientAlias = rumqttc::v5::AsyncClient;
pub type EventLoopAlias = rumqttc::v5::EventLoop;

#[async_trait]
impl MqttPubSub for rumqttc::v5::AsyncClient {
    // NOTE: Ideally, we would just directly put the result of the MqttPubSub operations in a Box
    // without the intermediate step of calling .wait_async(), but the rumqttc NoticeFuture does
    // not actually implement Future despite the name.

    async fn publish(
        &self,
        topic: impl Into<String> + Send,
        qos: QoS,
        retain: bool,
        payload: impl Into<Bytes> + Send,
    ) -> Result<CompletionToken, ClientError> {
        let nf = self.publish(topic, qos, retain, payload).await?;
        Ok(CompletionToken(Box::new(nf.wait_async())))
    }

    async fn publish_with_properties(
        &self,
        topic: impl Into<String> + Send,
        qos: QoS,
        retain: bool,
        payload: impl Into<Bytes> + Send,
        properties: PublishProperties,
    ) -> Result<CompletionToken, ClientError> {
        let nf = self
            .publish_with_properties(topic, qos, retain, payload, properties)
            .await?;
        Ok(CompletionToken(Box::new(nf.wait_async())))
    }

    async fn subscribe(
        &self,
        topic: impl Into<String> + Send,
        qos: QoS,
    ) -> Result<CompletionToken, ClientError> {
        let nf = self.subscribe(topic, qos).await?;
        Ok(CompletionToken(Box::new(nf.wait_async())))
    }

    async fn subscribe_with_properties(
        &self,
        topic: impl Into<String> + Send,
        qos: QoS,
        properties: SubscribeProperties,
    ) -> Result<CompletionToken, ClientError> {
        let nf = self
            .subscribe_with_properties(topic, qos, properties)
            .await?;
        Ok(CompletionToken(Box::new(nf.wait_async())))
    }

    async fn unsubscribe(
        &self,
        topic: impl Into<String> + Send,
    ) -> Result<CompletionToken, ClientError> {
        let nf = self.unsubscribe(topic).await?;
        Ok(CompletionToken(Box::new(nf.wait_async())))
    }

    async fn unsubscribe_with_properties(
        &self,
        topic: impl Into<String> + Send,
        properties: UnsubscribeProperties,
    ) -> Result<CompletionToken, ClientError> {
        let nf = self.unsubscribe_with_properties(topic, properties).await?;
        Ok(CompletionToken(Box::new(nf.wait_async())))
    }
}

#[async_trait]
impl MqttAck for rumqttc::v5::AsyncClient {
    async fn ack(&self, publish: &Publish) -> Result<(), ClientError> {
        Ok(self.ack(publish).await?)
    }
}

#[async_trait]
impl InternalClient for rumqttc::v5::AsyncClient {
    fn get_manual_ack(&self, publish: &Publish) -> rumqttc::v5::ManualAck {
        self.get_manual_ack(publish)
    }

    async fn manual_ack(&self, ack: ManualAck) -> Result<(), ClientError> {
        self.manual_ack(ack).await
    }

    async fn reauth(&self, auth_props: AuthProperties) -> Result<(), ClientError> {
        self.reauth(Some(auth_props)).await
    }
}

#[async_trait]
impl MqttDisconnect for rumqttc::v5::AsyncClient {
    async fn disconnect(&self) -> Result<(), ClientError> {
        Ok(self.disconnect().await?)
    }
}

#[async_trait]
impl MqttEventLoop for rumqttc::v5::EventLoop {
    async fn poll(&mut self) -> Result<Event, ConnectionError> {
        self.poll().await
    }

    fn set_clean_start(&mut self, clean_start: bool) {
        self.options.set_clean_start(clean_start);
    }
}

pub fn client(
    connection_settings: MqttConnectionSettings,
    channel_capacity: usize,
    manual_ack: bool,
) -> Result<(rumqttc::v5::AsyncClient, rumqttc::v5::EventLoop), ConnectionSettingsAdapterError> {
    // NOTE: channel capacity for AsyncClient must be less than usize::MAX - 1.
    let mut mqtt_options: rumqttc::v5::MqttOptions = connection_settings.try_into()?;
    mqtt_options.set_manual_acks(manual_ack);
    Ok(rumqttc::v5::AsyncClient::new(
        mqtt_options,
        channel_capacity,
    ))
}

// TODO: This error story needs improvement once we find out how much of this
// adapter code will stay after TLS dependency changes.
#[derive(Error, Debug)]
#[error("{msg}: {field}")]
pub struct ConnectionSettingsAdapterError {
    msg: String,
    field: ConnectionSettingsField,
    #[source]
    source: Option<Box<dyn std::error::Error>>,
}

// TODO: As above, this will potentially be updated once final TLS implementation takes shape
#[derive(Debug)]
pub enum ConnectionSettingsField {
    // ClientId(String),
    // HostName(String),
    // TcpPort(u16),
    // KeepAlive(Duration),
    SessionExpiry(Duration),
    // ConnectionTimeout(Duration),
    // CleanStart(bool),
    // Username(String),
    // Password(String),
    PasswordFile(String),
    UseTls(bool),
    // CaFile(String),
    // CaRequireRevocationCheck(bool),
    // CertFile(String),
    // KeyFile(String),
    // KeyFilePassword(String),
    SatAuthFile(String),
}

impl fmt::Display for ConnectionSettingsField {
    fn fmt(&self, f: &mut fmt::Formatter<'_>) -> fmt::Result {
        match self {
            ConnectionSettingsField::SessionExpiry(v) => write!(f, "Session Expiry: {v:?}"),
            ConnectionSettingsField::PasswordFile(v) => write!(f, "Password File: {v:?}"),
            ConnectionSettingsField::UseTls(v) => write!(f, "Use TLS: {v:?}"),
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

impl TryFrom<MqttConnectionSettings> for rumqttc::v5::MqttOptions {
    type Error = ConnectionSettingsAdapterError;

    fn try_from(value: MqttConnectionSettings) -> Result<Self, Self::Error> {
        // Client ID, Host Name, TCP Port
        let mut mqtt_options =
            rumqttc::v5::MqttOptions::new(value.client_id.clone(), value.host_name, value.tcp_port);
        // Keep Alive
        mqtt_options.set_keep_alive(value.keep_alive);
        // Session Expiry
        match value.session_expiry.as_secs().try_into() {
            Ok(se) => {
                // validate this is >= 5 seconds otherwise rumqttc will panic
                if se < 5 {
                    return Err(ConnectionSettingsAdapterError {
                        msg: "require > 5 seconds".to_string(),
                        field: ConnectionSettingsField::SessionExpiry(value.session_expiry),
                        source: None,
                    });
                }
                mqtt_options.set_session_expiry_interval(Some(se));
            }
            Err(e) => {
                return Err(ConnectionSettingsAdapterError {
                    msg: "cannot convert to u32".to_string(),
                    field: ConnectionSettingsField::SessionExpiry(value.session_expiry),
                    source: Some(Box::new(e)),
                });
            }
        };
        // Connection Timeout
        mqtt_options.set_connection_timeout(value.connection_timeout.as_secs());
        // Clean Start
        mqtt_options.set_clean_start(value.clean_start);
        // Username, Password, Password File
        if let Some(username) = value.username {
            let password = {
                if let Some(password_file) = value.password_file {
                    match fs::read_to_string(&password_file) {
                        Ok(password) => password,
                        Err(e) => {
                            return Err(ConnectionSettingsAdapterError {
                                msg: "cannot read password file".to_string(),
                                field: ConnectionSettingsField::PasswordFile(password_file),
                                source: Some(Box::new(e)),
                            });
                        }
                    }
                } else {
                    value.password.unwrap_or_default()
                }
            };
            mqtt_options.set_credentials(username, password);
        }

        // Use TLS, CA File, CA Require Revocation Check, Cert File, Key File, Key File Password
        if value.use_tls {
            let config = tls_config(
                value.ca_file,
                value.ca_require_revocation_check,
                value.cert_file,
                value.key_file,
                value.key_file_password,
            )
            .map_err(|e| ConnectionSettingsAdapterError {
                msg: "tls config error".to_string(),
                field: ConnectionSettingsField::UseTls(true),
                source: Some(Box::new(TlsError {
                    msg: e.to_string(),
                    source: Some(e),
                })),
            })?;
            mqtt_options.set_transport(Transport::tls_with_config(
                rumqttc::TlsConfiguration::Rustls(Arc::new(config)),
            ));
        }

        // SAT Auth File
        if let Some(sat_auth_file) = value.sat_auth_file {
            mqtt_options.set_authentication_method(Some("K8S-SAT".to_string()));
            let sat_auth =
                fs::read(sat_auth_file.clone()).map_err(|e| ConnectionSettingsAdapterError {
                    msg: "cannot read sat auth file".to_string(),
                    field: ConnectionSettingsField::SatAuthFile(sat_auth_file),
                    source: Some(Box::new(e)),
                })?;
            mqtt_options.set_authentication_data(Some(sat_auth.into()));
        }

        // NOTE: MqttOptions has a field called "request_channel_capacity" which currently does nothing.
        // We do not set it.
        Ok(mqtt_options)
    }
}

fn tls_config(
    ca_file: Option<String>,
    ca_require_revocation_check: bool,
    cert_file: Option<String>,
    key_file: Option<String>,
    key_file_password: Option<String>,
) -> Result<ClientConfig, anyhow::Error> {
    let config_builder = {
        // Provided CA certs
        if let Some(ca_file) = ca_file {
            // CA File
            let mut root_cert_store = RootCertStore::empty();
            let fh = File::open(ca_file)?;
            let certs =
                rustls_pemfile::certs(&mut BufReader::new(fh)).collect::<Result<Vec<_>, _>>()?;
            root_cert_store.add_parsable_certificates(certs);

            // CA Revocation Check
            if ca_require_revocation_check {
                rumqttc::tokio_rustls::rustls::ClientConfig::builder().with_webpki_verifier(
                    WebPkiServerVerifier::builder(root_cert_store.into()).build()?,
                )
            } else {
                rumqttc::tokio_rustls::rustls::ClientConfig::builder()
                    .with_root_certificates(root_cert_store)
            }

        // Use native certs since CA not provided
        } else {
            let mut root_cert_store = RootCertStore::empty();
            let native_certs = rustls_native_certs::load_native_certs()?;
            for cert in native_certs {
                root_cert_store.add(cert)?;
            }
            rumqttc::tokio_rustls::rustls::ClientConfig::builder()
                .with_root_certificates(root_cert_store)
        }
    };

    let config = {
        if let (Some(cert_file), Some(key_file)) = (cert_file, key_file) {
            // Certs
            let certs = {
                let fh = File::open(cert_file.clone())?;
                let certs = rustls_pemfile::certs(&mut BufReader::new(fh))
                    .collect::<Result<Vec<_>, _>>()?;
                if certs.is_empty() {
                    Err(TlsError::new("no valid client cert in cert file chain"))?;
                }
                certs
            };

            // Key
            let key = {
                // Handle key_file_password
                if let Some(key_file_password) = key_file_password {
                    let pem = fs::read(key_file)?;
                    let pkey =
                        PKey::private_key_from_pem_passphrase(&pem, key_file_password.as_bytes())?;
                    match PrivateKeyDer::try_from(pkey.private_key_to_der()?) {
                        Ok(key) => key,
                        Err(e) => {
                            return Err(TlsError::new(e))?;
                        }
                    }
                } else {
                    let fh = File::open(key_file.clone())?;
                    let mut key_reader = BufReader::new(fh);
                    match rustls_pemfile::private_key(&mut key_reader) {
                        Ok(Some(key)) => key,
                        Ok(None) => {
                            return Err(TlsError::new("no valid client key in key file"))?;
                        }
                        Err(e) => {
                            return Err(e)?;
                        }
                    }
                }
            };
            config_builder.with_client_auth_cert(certs, key)?
        } else {
            config_builder.with_no_client_auth()
        }
    };

    Ok(config)
}

/// -------------------------------------------

// TODO: Remove ignore from tests once filepath for test certs is known

#[cfg(test)]
mod tests {
    use std::path::PathBuf;

    use crate::{rumqttc_adapter::ConnectionSettingsAdapterError, MqttConnectionSettingsBuilder};

    #[test]
    fn test_mqtt_connection_settings_no_tls() {
        let connection_settings = MqttConnectionSettingsBuilder::default()
            .client_id("test_client_id".to_string())
            .host_name("test_host".to_string())
            .use_tls(false)
            .build()
            .unwrap();
        let mqtt_options_result: Result<rumqttc::v5::MqttOptions, ConnectionSettingsAdapterError> =
            connection_settings.try_into();
        assert!(mqtt_options_result.is_ok());
    }

    #[test]
    #[ignore = "unknown cert path"]
    fn test_mqtt_connection_settings_username() {
        // username and password
        let connection_settings = MqttConnectionSettingsBuilder::default()
            .client_id("test_client_id".to_string())
            .host_name("test_host".to_string())
            .use_tls(false)
            .username("test_username".to_string())
            .password("test_password".to_string())
            .build()
            .unwrap();
        let mqtt_options_result: Result<rumqttc::v5::MqttOptions, ConnectionSettingsAdapterError> =
            connection_settings.try_into();
        assert!(mqtt_options_result.is_ok());

        // just username
        let connection_settings = MqttConnectionSettingsBuilder::default()
            .client_id("test_client_id".to_string())
            .host_name("test_host".to_string())
            .use_tls(false)
            .username("test_username".to_string())
            .build()
            .unwrap();
        let mqtt_options_result: Result<rumqttc::v5::MqttOptions, ConnectionSettingsAdapterError> =
            connection_settings.try_into();
        assert!(mqtt_options_result.is_ok());

        // username and password file
        let mut password_file_path = PathBuf::from(env!("CARGO_MANIFEST_DIR"));
        password_file_path
            .push("../../../../lib/dotnet/test/Azure.Iot.Operations.Protocol.UnitTests/Connection/mypassword.txt");

        let connection_settings = MqttConnectionSettingsBuilder::default()
            .client_id("test_client_id".to_string())
            .host_name("test_host".to_string())
            .use_tls(false)
            .username("test_username".to_string())
            .password_file(password_file_path.into_os_string().into_string().unwrap())
            .build()
            .unwrap();
        let mqtt_options_result: Result<rumqttc::v5::MqttOptions, ConnectionSettingsAdapterError> =
            connection_settings.try_into();
        assert!(mqtt_options_result.is_ok());
    }

    #[test]
    #[ignore = "unknown cert path"]
    fn test_mqtt_connection_settings_ca_file() {
        let mut ca_file_path = PathBuf::from(env!("CARGO_MANIFEST_DIR"));
        ca_file_path.push(
            "../../../../lib/dotnet/test/Azure.Iot.Operations.Protocol.UnitTests/Connection/ca.txt",
        );

        let connection_settings = MqttConnectionSettingsBuilder::default()
            .client_id("test_client_id".to_string())
            .host_name("test_host".to_string())
            .ca_file(ca_file_path.into_os_string().into_string().unwrap())
            .build()
            .unwrap();
        let mqtt_options_result: Result<rumqttc::v5::MqttOptions, ConnectionSettingsAdapterError> =
            connection_settings.try_into();
        assert!(mqtt_options_result.is_ok());
    }

    #[test]
    #[ignore = "unknown cert path"]
    fn test_mqtt_connection_settings_ca_file_revocation_check() {
        let mut ca_file_path = PathBuf::from(env!("CARGO_MANIFEST_DIR"));
        ca_file_path.push(
            "../../../../lib/dotnet/test/Azure.Iot.Operations.Protocol.UnitTests/Connection/ca.txt",
        );

        let connection_settings = MqttConnectionSettingsBuilder::default()
            .client_id("test_client_id".to_string())
            .host_name("test_host".to_string())
            .ca_file(ca_file_path.into_os_string().into_string().unwrap())
            .ca_require_revocation_check(true)
            .build()
            .unwrap();
        let mqtt_options_result: Result<rumqttc::v5::MqttOptions, ConnectionSettingsAdapterError> =
            connection_settings.try_into();
        assert!(mqtt_options_result.is_ok());
    }

    #[test]
    #[ignore = "unknown cert path"]
    fn test_mqtt_connection_settings_ca_file_plus_cert() {
        let mut dir = PathBuf::from(env!("CARGO_MANIFEST_DIR"));
        dir.push("../../../../lib/dotnet/test/Azure.Iot.Operations.Protocol.UnitTests/Connection/");
        let mut ca_file = dir.clone();
        ca_file.push("ca.txt");
        let mut cert_file = dir.clone();
        cert_file.push("TestSdkLiteCertPem.txt");
        let mut key_file = dir.clone();
        key_file.push("TestSdkLiteCertKey.txt");

        let connection_settings = MqttConnectionSettingsBuilder::default()
            .client_id("test_client_id".to_string())
            .host_name("test_host".to_string())
            .ca_file(ca_file.into_os_string().into_string().unwrap())
            .cert_file(cert_file.into_os_string().into_string().unwrap())
            .key_file(key_file.into_os_string().into_string().unwrap())
            .build()
            .unwrap();
        let mqtt_options_result: Result<rumqttc::v5::MqttOptions, ConnectionSettingsAdapterError> =
            connection_settings.try_into();
        assert!(mqtt_options_result.is_ok());
    }

    #[test]
    #[ignore = "unknown cert path"]
    fn test_mqtt_connection_settings_cert() {
        let mut dir = PathBuf::from(env!("CARGO_MANIFEST_DIR"));
        dir.push("../../../../lib/dotnet/test/Azure.Iot.Operations.Protocol.UnitTests/Connection/");
        let mut cert_file = dir.clone();
        cert_file.push("TestSdkLiteCertPem.txt");
        let mut key_file = dir.clone();
        key_file.push("TestSdkLiteCertKey.txt");

        let connection_settings = MqttConnectionSettingsBuilder::default()
            .client_id("test_client_id".to_string())
            .host_name("test_host".to_string())
            .cert_file(cert_file.into_os_string().into_string().unwrap())
            .key_file(key_file.into_os_string().into_string().unwrap())
            .build()
            .unwrap();
        let mqtt_options_result: Result<rumqttc::v5::MqttOptions, ConnectionSettingsAdapterError> =
            connection_settings.try_into();
        assert!(mqtt_options_result.is_ok());
    }

    #[test]
    #[ignore = "unknown cert path"]
    fn test_mqtt_connection_settings_cert_key_file_password() {
        let dir = PathBuf::from(env!("CARGO_MANIFEST_DIR"));
        let mut cert_file = dir.clone();
        cert_file.push(
            "../../../../lib/dotnet/test/Azure.Iot.Operations.Protocol.UnitTests/Connection/TestSdkLiteCertPwdPem.txt",
        );
        let mut key_file = dir.clone();
        key_file.push(
            "../../../../lib/dotnet/test/Azure.Iot.Operations.Protocol.UnitTests/Connection/TestSdkLiteCertPwdKey.txt",
        );

        let connection_settings = MqttConnectionSettingsBuilder::default()
            .client_id("test_client_id".to_string())
            .host_name("test_host".to_string())
            .cert_file(cert_file.into_os_string().into_string().unwrap())
            .key_file(key_file.into_os_string().into_string().unwrap())
            .key_file_password("sdklite".to_string())
            .build()
            .unwrap();
        let mqtt_options_result: Result<rumqttc::v5::MqttOptions, ConnectionSettingsAdapterError> =
            connection_settings.try_into();
        assert!(mqtt_options_result.is_ok());
    }
}
