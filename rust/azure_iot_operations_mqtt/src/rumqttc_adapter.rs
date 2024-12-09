// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

//! Adapter layer for the rumqttc crate

use std::{fmt, fs, time::Duration};

use async_trait::async_trait;
use bytes::Bytes;
use openssl::{pkey::PKey, x509::X509};
use rumqttc::{self, tokio_native_tls::native_tls, TlsConfiguration, Transport};
use thiserror::Error;

use crate::connection_settings::MqttConnectionSettings;
use crate::control_packet::{
    AuthProperties, Publish, PublishProperties, QoS, SubscribeProperties, UnsubscribeProperties,
};
use crate::error::{
    AckError, ConnectionError, DisconnectError, PublishError, ReauthError, SubscribeError,
    UnsubscribeError,
};
use crate::interface::{
    CompletionToken, Event, MqttAck, MqttClient, MqttDisconnect, MqttEventLoop, MqttPubSub,
};
use crate::topic::{TopicFilter, TopicName};

pub type ClientAlias = rumqttc::v5::AsyncClient;
pub type EventLoopAlias = rumqttc::v5::EventLoop;

impl From<rumqttc::v5::ClientError> for PublishError {
    fn from(err: rumqttc::v5::ClientError) -> Self {
        // NOTE: Technically, the rumqttc ClientError can also include some input validation for
        // publish topics but since there's no way to identify those, we will need to check for them
        // ourselves anyway ahead of invoking rumqttc, thus preventing that case from happening.
        // As such, we can assume that all rumqttc ClientErrors on publish are due to the client
        // being detached from the event loop
        match err {
            rumqttc::v5::ClientError::Request(r) | rumqttc::v5::ClientError::TryRequest(r) => {
                PublishError::DetachedClient(r)
            }
        }
    }
}

impl From<rumqttc::v5::ClientError> for SubscribeError {
    fn from(err: rumqttc::v5::ClientError) -> Self {
        // NOTE: Technically, the rumqttc ClientError can also include some input validation for
        // subscribe topics but since there's no way to identify those, we will need to check for them
        // ourselves anyway ahead of invoking rumqttc, thus preventing that case from happening.
        // As such, we can assume that all rumqttc ClientErrors on subscribe are due to the client
        // being detached from the event loop
        match err {
            rumqttc::v5::ClientError::Request(r) | rumqttc::v5::ClientError::TryRequest(r) => {
                SubscribeError::DetachedClient(r)
            }
        }
    }
}

impl From<rumqttc::v5::ClientError> for UnsubscribeError {
    fn from(err: rumqttc::v5::ClientError) -> Self {
        match err {
            rumqttc::v5::ClientError::Request(r) | rumqttc::v5::ClientError::TryRequest(r) => {
                UnsubscribeError::DetachedClient(r)
            }
        }
    }
}

impl From<rumqttc::v5::ClientError> for AckError {
    fn from(err: rumqttc::v5::ClientError) -> Self {
        match err {
            rumqttc::v5::ClientError::Request(r) | rumqttc::v5::ClientError::TryRequest(r) => {
                AckError::DetachedClient(r)
            }
        }
    }
}

impl From<rumqttc::v5::ClientError> for DisconnectError {
    fn from(err: rumqttc::v5::ClientError) -> Self {
        match err {
            rumqttc::v5::ClientError::Request(r) | rumqttc::v5::ClientError::TryRequest(r) => {
                DisconnectError::DetachedClient(r)
            }
        }
    }
}

impl From<rumqttc::v5::ClientError> for ReauthError {
    fn from(err: rumqttc::v5::ClientError) -> Self {
        match err {
            rumqttc::v5::ClientError::Request(r) | rumqttc::v5::ClientError::TryRequest(r) => {
                ReauthError::DetachedClient(r)
            }
        }
    }
}

#[async_trait]
impl MqttPubSub for rumqttc::v5::AsyncClient {
    // NOTE: Ideally, we would just directly put the result of the MqttPubSub operations in a Box
    // without the intermediate step of calling .wait_async(), but the rumqttc NoticeFuture does
    // not actually implement Future despite the name.

    // NOTE: Validating the topic name here does unfortunately require an additional allocation.
    // This is only true because rumqttc will always reallocate, even if it's being given an owned
    // string.

    // NOTE: It also might be nice to be able to provide the exact reason the topic is invalid here,
    // but the current topic parsing API doesn't have a way to do this without yet another allocation.
    // This may be worth reconsidering in the future, but for now, this is already more information
    // than was previously available.

    async fn publish(
        &self,
        topic: impl Into<String> + Send,
        qos: QoS,
        retain: bool,
        payload: impl Into<Bytes> + Send,
    ) -> Result<CompletionToken, PublishError> {
        let topic = topic.into();
        if !TopicName::is_valid_topic_name(&topic) {
            return Err(PublishError::InvalidTopicName);
        }
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
    ) -> Result<CompletionToken, PublishError> {
        let topic = topic.into();
        if !TopicName::is_valid_topic_name(&topic) {
            return Err(PublishError::InvalidTopicName);
        }
        let nf = self
            .publish_with_properties(topic, qos, retain, payload, properties)
            .await?;
        Ok(CompletionToken(Box::new(nf.wait_async())))
    }

    async fn subscribe(
        &self,
        topic: impl Into<String> + Send,
        qos: QoS,
    ) -> Result<CompletionToken, SubscribeError> {
        let topic = topic.into();
        if !TopicFilter::is_valid_topic_filter(&topic) {
            return Err(SubscribeError::InvalidTopicFilter);
        }
        let nf = self.subscribe(topic, qos).await?;
        Ok(CompletionToken(Box::new(nf.wait_async())))
    }

    async fn subscribe_with_properties(
        &self,
        topic: impl Into<String> + Send,
        qos: QoS,
        properties: SubscribeProperties,
    ) -> Result<CompletionToken, SubscribeError> {
        let topic = topic.into();
        if !TopicFilter::is_valid_topic_filter(&topic) {
            return Err(SubscribeError::InvalidTopicFilter);
        }
        let nf = self
            .subscribe_with_properties(topic, qos, properties)
            .await?;
        Ok(CompletionToken(Box::new(nf.wait_async())))
    }

    async fn unsubscribe(
        &self,
        topic: impl Into<String> + Send,
    ) -> Result<CompletionToken, UnsubscribeError> {
        let topic = topic.into();
        if !TopicFilter::is_valid_topic_filter(&topic) {
            return Err(UnsubscribeError::InvalidTopicFilter);
        }
        let nf = self.unsubscribe(topic).await?;
        Ok(CompletionToken(Box::new(nf.wait_async())))
    }

    async fn unsubscribe_with_properties(
        &self,
        topic: impl Into<String> + Send,
        properties: UnsubscribeProperties,
    ) -> Result<CompletionToken, UnsubscribeError> {
        let topic = topic.into();
        if !TopicFilter::is_valid_topic_filter(&topic) {
            return Err(UnsubscribeError::InvalidTopicFilter);
        }
        let nf = self.unsubscribe_with_properties(topic, properties).await?;
        Ok(CompletionToken(Box::new(nf.wait_async())))
    }
}

#[async_trait]
impl MqttAck for rumqttc::v5::AsyncClient {
    async fn ack(&self, publish: &Publish) -> Result<(), AckError> {
        // NOTE: Despite the contract, there's no (easy) way to have this return AckError::AlreadyAcked
        // if the publish in question has already been acked - doing so would require adding a
        // wrapper, and moving significant portions of the pub_tracker behind the adapter layer.
        // This would need to be implemented before any non-Session MQTT client gets exposed in API.
        let mut manual_ack = self.get_manual_ack(publish);
        manual_ack.set_reason(rumqttc::v5::ManualAckReason::Success);
        // NOTE: Technically we could have achieved this same behavior by just calling .ack() on
        // the rumqttc client which assumes rc=0, but I prefer to be explicit here.
        Ok(self.manual_ack(manual_ack).await?)
    }
}

#[async_trait]
impl MqttClient for rumqttc::v5::AsyncClient {
    async fn reauth(&self, auth_props: AuthProperties) -> Result<(), ReauthError> {
        Ok(self.reauth(Some(auth_props)).await?)
    }
}

#[async_trait]
impl MqttDisconnect for rumqttc::v5::AsyncClient {
    async fn disconnect(&self) -> Result<(), DisconnectError> {
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

/// Client constructors + TLS
/// -------------------------------------------
pub fn client(
    connection_settings: MqttConnectionSettings,
    channel_capacity: usize,
    manual_ack: bool,
) -> Result<(rumqttc::v5::AsyncClient, rumqttc::v5::EventLoop), MqttAdapterError> {
    // NOTE: channel capacity for AsyncClient must be less than usize::MAX - 1 due to (presumably) a bug.
    // It panics if you set MAX, although MAX - 1 is fine.
    if channel_capacity == usize::MAX {
        return Err(MqttAdapterError::Other(
            "rumqttc does not support channel capacity of usize::MAX".to_string(),
        ));
    }
    let mut mqtt_options: rumqttc::v5::MqttOptions = connection_settings.try_into()?;
    mqtt_options.set_manual_acks(manual_ack);
    Ok(rumqttc::v5::AsyncClient::new(
        mqtt_options,
        channel_capacity,
    ))
}

#[derive(Error, Debug)]
pub enum MqttAdapterError {
    #[error(transparent)]
    ConnectionSettings(#[from] ConnectionSettingsAdapterError),
    #[error("Other adapter error: {0}")]
    Other(String),
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
            rumqttc::v5::MqttOptions::new(value.client_id.clone(), value.hostname, value.tcp_port);
        // Keep Alive
        mqtt_options.set_keep_alive(value.keep_alive);
        // Receive Maximum
        mqtt_options.set_receive_maximum(Some(value.receive_max));
        // Max Packet Size
        // NOTE: due to a bug in rumqttc, we need to set None to u32::MAX, since rumqttc overrides
        // None values with an arbitrary default that can't be changed. This may or may not be
        // exactly the same thing, but it is in most circumstances.
        mqtt_options.set_max_packet_size(value.receive_packet_size_max.or(Some(u32::MAX)));
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
            let transport = tls_config(
                value.ca_file,
                value.cert_file,
                value.key_file,
                value.key_password_file,
            )
            .map_err(|e| ConnectionSettingsAdapterError {
                msg: "tls config error".to_string(),
                field: ConnectionSettingsField::UseTls(true),
                source: Some(Box::new(TlsError {
                    msg: e.to_string(),
                    source: Some(e),
                })),
            })?;
            mqtt_options.set_transport(transport);
        }

        // SAT Auth File
        if let Some(sat_file) = value.sat_file {
            mqtt_options.set_authentication_method(Some("K8S-SAT".to_string()));
            let sat_auth =
                fs::read(sat_file.clone()).map_err(|e| ConnectionSettingsAdapterError {
                    msg: "cannot read sat auth file".to_string(),
                    field: ConnectionSettingsField::SatAuthFile(sat_file),
                    source: Some(Box::new(e)),
                })?;
            mqtt_options.set_authentication_data(Some(sat_auth.into()));
        }

        // NOTE: MqttOptions has a field called "request_channel_capacity" which currently does nothing.
        // We do not set it.
        Ok(mqtt_options)
    }
}

fn read_root_ca_certs(ca_file: String) -> Result<Vec<native_tls::Certificate>, anyhow::Error> {
    let mut ca_certs = Vec::new();
    let ca_pem = fs::read(ca_file)?;

    // native_tls does not have a function to deserialize multiple certs at once, so
    // use openssl X509::stack_from_pem to parse certs.
    let certs = &mut X509::stack_from_pem(&ca_pem)?;
    ca_certs.append(certs);

    if ca_certs.is_empty() {
        Err(TlsError::new("No CA certs available in CA File"))?;
    }

    ca_certs.sort();
    ca_certs.dedup();

    Ok(ca_certs
        .iter()
        .map(|cert| {
            // Serializing a valid openssl X509 and deserializing as a native_tls Certificate
            // should never fail.
            native_tls::Certificate::from_pem(&cert.to_pem().expect("cert should serialize to PEM"))
                .expect("Failed to deserialize cert")
        })
        .collect())
}

fn tls_config(
    ca_file: Option<String>,
    cert_file: Option<String>,
    key_file: Option<String>,
    key_password_file: Option<String>,
) -> Result<Transport, anyhow::Error> {
    let mut tls_connector_builder = native_tls::TlsConnector::builder();
    tls_connector_builder.min_protocol_version(Some(native_tls::Protocol::Tlsv12));

    // Provided CA certs
    // don't need an else because TlsConnector uses the system's trust root by default, and this just adds additional root certs
    if let Some(ca_file) = ca_file {
        // CA File
        let ca_certs = read_root_ca_certs(ca_file)?;
        for ca_cert in ca_certs {
            tls_connector_builder.add_root_certificate(ca_cert);
        }

        // CA Revocation Check TODO: add this back in
    }

    // Certs
    if let (Some(cert_file), Some(key_file)) = (cert_file, key_file) {
        // Cert
        let cert_file_contents = fs::read(cert_file)?;
        let client_cert_chain = X509::stack_from_pem(&cert_file_contents)?;
        let mut client_cert_chain_pem = Vec::new();
        for cert in client_cert_chain {
            let mut cert_pem = cert.to_pem()?;
            client_cert_chain_pem.append(&mut cert_pem);
        }

        // Key, with or without password
        let private_key_pem = {
            let key_file_contents = fs::read(key_file)?;
            if let Some(key_password_file) = key_password_file {
                let key_password_file_contents = fs::read(key_password_file)?;
                let private_key = PKey::private_key_from_pem_passphrase(
                    &key_file_contents,
                    &key_password_file_contents,
                )?;
                private_key.private_key_to_pem_pkcs8()?
            } else {
                let private_key = PKey::private_key_from_pem(&key_file_contents)?;
                private_key.private_key_to_pem_pkcs8()?
            }
        };

        let identity = native_tls::Identity::from_pkcs8(&client_cert_chain_pem, &private_key_pem)
            .map_err(|err| {
            TlsError::new(&format!("Failed to build TLS client identity: {err}"))
        })?;
        tls_connector_builder.identity(identity);
    }

    let tls_connector = tls_connector_builder
        .build()
        .map_err(|err| TlsError::new(&format!("Failed to build TLS connector: {err}")))?;

    Ok(Transport::Tls(TlsConfiguration::NativeConnector(
        tls_connector,
    )))
}

/// -------------------------------------------

#[cfg(test)]
mod tests {
    use std::path::PathBuf;

    use super::*;
    use crate::MqttConnectionSettingsBuilder;

    #[test]
    fn test_mqtt_connection_settings_no_tls() {
        let connection_settings = MqttConnectionSettingsBuilder::default()
            .client_id("test_client_id".to_string())
            .hostname("test_host".to_string())
            .use_tls(false)
            .build()
            .unwrap();
        let mqtt_options_result: Result<rumqttc::v5::MqttOptions, ConnectionSettingsAdapterError> =
            connection_settings.try_into();
        assert!(mqtt_options_result.is_ok());
    }

    #[test]
    fn test_mqtt_connection_settings_username() {
        // username and password
        let connection_settings = MqttConnectionSettingsBuilder::default()
            .client_id("test_client_id".to_string())
            .hostname("test_host".to_string())
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
            .hostname("test_host".to_string())
            .use_tls(false)
            .username("test_username".to_string())
            .build()
            .unwrap();
        let mqtt_options_result: Result<rumqttc::v5::MqttOptions, ConnectionSettingsAdapterError> =
            connection_settings.try_into();
        assert!(mqtt_options_result.is_ok());

        // username and password file
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
        let mqtt_options_result: Result<rumqttc::v5::MqttOptions, ConnectionSettingsAdapterError> =
            connection_settings.try_into();
        assert!(mqtt_options_result.is_ok());
    }

    #[test]
    fn test_mqtt_connection_settings_ca_file() {
        let mut ca_file_path = PathBuf::from(env!("CARGO_MANIFEST_DIR"));
        ca_file_path.push("../../eng/test/dummy_credentials/TestCa.txt");

        let connection_settings = MqttConnectionSettingsBuilder::default()
            .client_id("test_client_id".to_string())
            .hostname("test_host".to_string())
            .ca_file(ca_file_path.into_os_string().into_string().unwrap())
            .build()
            .unwrap();
        let mqtt_options_result: Result<rumqttc::v5::MqttOptions, ConnectionSettingsAdapterError> =
            connection_settings.try_into();
        assert!(mqtt_options_result.is_ok());
    }

    #[test]
    fn test_mqtt_connection_settings_ca_file_plus_cert() {
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
        let mqtt_options_result: Result<rumqttc::v5::MqttOptions, ConnectionSettingsAdapterError> =
            connection_settings.try_into();
        assert!(mqtt_options_result.is_ok());
    }

    #[test]
    fn test_mqtt_connection_settings_cert() {
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
        let mqtt_options_result: Result<rumqttc::v5::MqttOptions, ConnectionSettingsAdapterError> =
            connection_settings.try_into();
        assert!(mqtt_options_result.is_ok());
    }

    #[test]
    fn test_mqtt_connection_settings_cert_key_file_password() {
        let mut dir = PathBuf::from(env!("CARGO_MANIFEST_DIR"));
        dir.push("../../eng/test/dummy_credentials/");
        let cert_file = dir.join("TestCert2Pem.txt");
        let key_file = dir.join("TestCert2KeyEncrypted.txt");
        let key_password_file = dir.join("TestCert2KeyPasswordFile.txt");

        let connection_settings = MqttConnectionSettingsBuilder::default()
            .client_id("test_client_id".to_string())
            .hostname("test_host".to_string())
            .cert_file(cert_file.into_os_string().into_string().unwrap())
            .key_file(key_file.into_os_string().into_string().unwrap())
            .key_password_file(key_password_file.into_os_string().into_string().unwrap())
            .build()
            .unwrap();
        let mqtt_options_result: Result<rumqttc::v5::MqttOptions, ConnectionSettingsAdapterError> =
            connection_settings.try_into();
        assert!(mqtt_options_result.is_ok());
    }

    #[test]
    fn test_receive_packet_size_max_override_none() {
        let connection_settings = MqttConnectionSettingsBuilder::default()
            .client_id("test_client_id".to_string())
            .hostname("test_host".to_string())
            .receive_packet_size_max(None)
            .build()
            .unwrap();
        let mqtt_options_result: Result<rumqttc::v5::MqttOptions, ConnectionSettingsAdapterError> =
            connection_settings.try_into();
        assert!(mqtt_options_result.is_ok());
        assert_eq!(
            mqtt_options_result.unwrap().max_packet_size(),
            Some(u32::MAX)
        );
    }
}
