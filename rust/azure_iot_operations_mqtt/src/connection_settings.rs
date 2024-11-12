// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

//! Generic MQTT connection settings implementations

use std::env;
use std::time::Duration;

// TODO: Split up this struct to avoid weird combinations and separate concern.
// Things like having both password and password_file don't make much sense,
// nor frankly does combining MQTT and TLS settings.

/// All the settings required to establish an MQTT connection.
#[derive(Builder, Clone)]
#[builder(pattern = "owned", setter(into), build_fn(validate = "Self::validate"))]
pub struct MqttConnectionSettings {
    /// Client identifier
    pub(crate) client_id: String,
    /// FQDN of the host to connect to
    pub(crate) hostname: String,
    /// TCP port to connect to the host on
    #[builder(default = "8883")]
    pub(crate) tcp_port: u16,
    /// Max time between communications
    #[builder(default = "Duration::from_secs(60)")]
    pub(crate) keep_alive: Duration,
    /// Max number of in-flight Quality of Service 1 and 2 messages
    //TODO: This is probably better represented as an option. Do this when refactoring.
    #[builder(default = "u16::MAX")] // See: MQTT 5.0 spec, 3.1.2.11.3
    pub(crate) receive_max: u16,
    /// Max size of a received packet
    #[builder(default = "None")]
    pub(crate) receive_packet_size_max: Option<u32>,
    /// Session Expiry Interval
    #[builder(default = "Duration::from_secs(3600)")]
    // TODO: Would this would be better represented as an integer (probably, due to max value having distinct meaning in MQTT)
    pub(crate) session_expiry: Duration,
    /// Connection timeout
    #[builder(default = "Duration::from_secs(30)")]
    pub(crate) connection_timeout: Duration,
    /// Clean start
    #[builder(default = "false")]
    //NOTE: Should be `true` outside of AIO context. Consider when refactoring settings.
    pub(crate) clean_start: bool,
    /// Username for MQTT
    #[builder(default = "None")]
    pub(crate) username: Option<String>,
    /// Password for MQTT
    #[builder(default = "None")]
    pub(crate) password: Option<String>,
    /// Path to a file containing the MQTT password
    #[builder(default = "None")]
    pub(crate) password_file: Option<String>,
    /// TLS negotiation enabled
    #[builder(default = "true")]
    pub(crate) use_tls: bool,
    /// Path to a PEM file used to validate server identity
    #[builder(default = "None")]
    pub(crate) ca_file: Option<String>,
    /// Path to PEM file used to establish X509 client authentication
    #[builder(default = "None")]
    pub(crate) cert_file: Option<String>,
    /// Path to a file containing a key used to establish X509 client authentication
    #[builder(default = "None")]
    pub(crate) key_file: Option<String>,
    /// Path to a file containing the password used to decrypt the Key
    #[builder(default = "None")]
    pub(crate) key_password_file: Option<String>,
    /// Path to a SAT file to be used for SAT auth
    #[builder(default = "None")]
    pub(crate) sat_file: Option<String>,
}

impl MqttConnectionSettingsBuilder {
    /// Initialize the [`MqttConnectionSettingsBuilder`] from environment variables.
    ///
    /// Example
    /// ```
    /// # use azure_iot_operations_mqtt::{MqttConnectionSettings, MqttConnectionSettingsBuilder, MqttConnectionSettingsBuilderError};
    /// # fn try_main() -> Result<MqttConnectionSettings, MqttConnectionSettingsBuilderError> {
    /// let connection_settings = MqttConnectionSettingsBuilder::from_environment().build()?;
    /// # Ok(connection_settings)
    /// # }
    /// # fn main() {
    /// #     // NOTE: This example is organized like this because we don't actually have env vars set, so it always panics
    /// #     try_main().ok();
    /// # }
    /// ```
    #[must_use]
    pub fn from_environment() -> Self {
        let client_id = env::var("AIO_MQTT_CLIENT_ID").ok();
        let hostname = env::var("AIO_BROKER_HOSTNAME").ok();
        let tcp_port = env::var("AIO_BROKER_TCP_PORT")
            .ok()
            .map(|v| v.parse::<u16>())
            .transpose()
            .unwrap_or(None);
        let keep_alive = env::var("AIO_MQTT_KEEP_ALIVE")
            .ok()
            .map(|v| v.parse::<u64>().map(Duration::from_secs))
            .transpose()
            .unwrap_or(None);
        let session_expiry = env::var("AIO_MQTT_SESSION_EXPIRY")
            .ok()
            .map(|v| v.parse::<u64>().map(Duration::from_secs))
            .transpose()
            .unwrap_or(None);
        let clean_start = env::var("AIO_MQTT_CLEAN_START")
            .ok()
            .map(|v| v.parse::<bool>())
            .transpose()
            .unwrap_or(None);
        let username = Some(env::var("AIO_MQTT_USERNAME").ok());
        let password_file = Some(env::var("AIO_MQTT_PASSWORD_FILE").ok());
        let use_tls = env::var("AIO_MQTT_USE_TLS")
            .ok()
            .map(|v| v.parse::<bool>())
            .transpose()
            .unwrap_or(None);
        let ca_file = Some(env::var("AIO_TLS_CA_FILE").ok());
        let cert_file = Some(env::var("AIO_TLS_CERT_FILE").ok());
        let key_file = Some(env::var("AIO_TLS_KEY_FILE").ok());
        let key_password_file = Some(env::var("AIO_TLS_KEY_PASSWORD_FILE").ok());
        let sat_file = Some(env::var("AIO_SAT_FILE").ok());

        // TODO: consider removing some of the Option wrappers in the Builder definition to avoid these spurious Some() wrappers.

        Self {
            client_id,
            hostname,
            tcp_port,
            keep_alive,
            receive_max: Some(u16::MAX),
            receive_packet_size_max: None,
            session_expiry,
            connection_timeout: Some(Duration::from_secs(30)),
            clean_start,
            username,
            password: None,
            password_file,
            use_tls,
            ca_file,
            cert_file,
            key_file,
            key_password_file,
            sat_file,
        }
    }

    /// Validate the MQTT Connection Settings.
    ///
    /// # Errors
    /// Returns a `String` describing the error if
    /// - `hostname` is empty
    /// - `client_id` is empty and `clean_start` is false
    /// - `password` and `password_file` are both Some
    /// - `sat_file` is Some and `password` or `password_file` are Some
    /// - `key_file` is Some and `cert_file` is None or empty
    fn validate(&self) -> Result<(), String> {
        if let Some(hostname) = &self.hostname {
            if hostname.is_empty() {
                return Err("Host name cannot be empty".to_string());
            }
        }
        if let Some(client_id) = &self.client_id {
            if client_id.is_empty() {
                if let Some(clean_start) = self.clean_start {
                    if !clean_start {
                        return Err(
                            "client_id is mandatory when clean_start is set to false".to_string()
                        );
                    }
                } else {
                    // default for clean_start is false
                    return Err(
                        "client_id is mandatory when clean_start is set to false".to_string()
                    );
                }
            }
        }
        if let (Some(password), Some(password_file)) = (&self.password, &self.password_file) {
            if password.is_some() && password_file.is_some() {
                return Err(
                    "password and password_file should not be used at the same time.".to_string(),
                );
            }
        }
        if let Some(Some(_)) = &self.sat_file {
            if let Some(Some(_)) = &self.password {
                return Err("sat_file cannot be used with password or password_file.".to_string());
            }
            if let Some(Some(_)) = &self.password_file {
                return Err("sat_file cannot be used with password or password_file.".to_string());
            }
        }
        if let Some(Some(_)) = &self.key_file {
            if let Some(Some(cert_file)) = &self.cert_file {
                if cert_file.is_empty() {
                    return Err("key_file and cert_file need to be provided together.".to_string());
                }
            } else {
                return Err("key_file and cert_file need to be provided together.".to_string());
            }
        }
        Ok(())
    }
}

#[cfg(test)]
mod tests {
    use super::MqttConnectionSettingsBuilder;

    #[test]
    fn test_connection_settings_empty_hostname() {
        let connection_settings_builder_result = MqttConnectionSettingsBuilder::default()
            .client_id("test_client_id".to_string())
            .hostname(String::new())
            .build();
        match connection_settings_builder_result {
            Ok(_) => panic!("Expected error"),
            Err(e) => assert_eq!(e.to_string(), "Host name cannot be empty"),
        }
    }

    #[test]
    fn test_connection_settings_empty_client_id() {
        let connection_settings_builder_result = MqttConnectionSettingsBuilder::default()
            .client_id(String::new())
            .hostname("test_host".to_string())
            .build();
        match connection_settings_builder_result {
            Ok(_) => panic!("Expected error"),
            Err(e) => assert_eq!(
                e.to_string(),
                "client_id is mandatory when clean_start is set to false"
            ),
        }

        let connection_settings_builder_result = MqttConnectionSettingsBuilder::default()
            .client_id(String::new())
            .hostname("test_host".to_string())
            .clean_start(false)
            .build();
        match connection_settings_builder_result {
            Ok(_) => panic!("Expected error"),
            Err(e) => assert_eq!(
                e.to_string(),
                "client_id is mandatory when clean_start is set to false"
            ),
        }

        let connection_settings_builder_result = MqttConnectionSettingsBuilder::default()
            .client_id(String::new())
            .hostname("test_host".to_string())
            .clean_start(true)
            .build();
        assert!(connection_settings_builder_result.is_ok());
    }

    #[test]
    fn test_connection_settings_password_combos() {
        let connection_settings_builder_result = MqttConnectionSettingsBuilder::default()
            .client_id("test_client_id".to_string())
            .hostname("test_host".to_string())
            .password("test_password".to_string())
            .password_file("test_password_file".to_string())
            .build();
        match connection_settings_builder_result {
            Ok(_) => panic!("Expected error"),
            Err(e) => assert_eq!(
                e.to_string(),
                "password and password_file should not be used at the same time."
            ),
        }

        let connection_settings_builder_result = MqttConnectionSettingsBuilder::default()
            .client_id("test_client_id".to_string())
            .hostname("test_hostname".to_string())
            .password("test_password".to_string())
            .sat_file("test_sat_file".to_string())
            .build();
        match connection_settings_builder_result {
            Ok(_) => panic!("Expected error"),
            Err(e) => assert_eq!(
                e.to_string(),
                "sat_file cannot be used with password or password_file."
            ),
        }
        let connection_settings_builder_result = MqttConnectionSettingsBuilder::default()
            .client_id("test_client_id".to_string())
            .hostname("test_host".to_string())
            .password_file("test_password_file".to_string())
            .sat_file("test_sat_auth_file".to_string())
            .build();
        match connection_settings_builder_result {
            Ok(_) => panic!("Expected error"),
            Err(e) => assert_eq!(
                e.to_string(),
                "sat_file cannot be used with password or password_file."
            ),
        }

        let connection_settings_builder_result = MqttConnectionSettingsBuilder::default()
            .client_id("test_client_id".to_string())
            .hostname("test_host".to_string())
            .password("test_password".to_string())
            .password_file("test_password_file".to_string())
            .sat_file("test_sat_auth_file".to_string())
            .build();
        match connection_settings_builder_result {
            Ok(_) => panic!("Expected error"),
            Err(e) => assert_eq!(
                e.to_string(),
                "password and password_file should not be used at the same time."
            ),
        }

        // just one of each
        let connection_settings_builder_result = MqttConnectionSettingsBuilder::default()
            .client_id("test_client_id".to_string())
            .hostname("test_host".to_string())
            .password("test_password".to_string())
            .build();
        assert!(connection_settings_builder_result.is_ok());

        let connection_settings_builder_result = MqttConnectionSettingsBuilder::default()
            .client_id("test_client_id".to_string())
            .hostname("test_host".to_string())
            .password_file("test_password_file".to_string())
            .build();
        assert!(connection_settings_builder_result.is_ok());

        let connection_settings_builder_result = MqttConnectionSettingsBuilder::default()
            .client_id("test_client_id".to_string())
            .hostname("test_host".to_string())
            .sat_file("test_sat_auth_file".to_string())
            .build();
        assert!(connection_settings_builder_result.is_ok());
    }

    #[test]
    fn test_connection_settings_cert_key_file() {
        let connection_settings_builder_result = MqttConnectionSettingsBuilder::default()
            .client_id("test_client_id".to_string())
            .hostname("test_host".to_string())
            .key_file("test_key_file".to_string())
            .build();
        match connection_settings_builder_result {
            Ok(_) => panic!("Expected error"),
            Err(e) => assert_eq!(
                e.to_string(),
                "key_file and cert_file need to be provided together."
            ),
        }

        let connection_settings_builder_result = MqttConnectionSettingsBuilder::default()
            .client_id("test_client_id".to_string())
            .hostname("test_host".to_string())
            .key_file("test_key_file".to_string())
            .cert_file(String::new())
            .build();
        match connection_settings_builder_result {
            Ok(_) => panic!("Expected error"),
            Err(e) => assert_eq!(
                e.to_string(),
                "key_file and cert_file need to be provided together."
            ),
        }

        let connection_settings_builder_result = MqttConnectionSettingsBuilder::default()
            .client_id("test_client_id".to_string())
            .hostname("test_host".to_string())
            .cert_file(String::new())
            .build();
        assert!(connection_settings_builder_result.is_ok());

        let connection_settings_builder_result = MqttConnectionSettingsBuilder::default()
            .client_id("test_client_id".to_string())
            .hostname("test_host".to_string())
            .cert_file("test_cert_file".to_string())
            .build();
        assert!(connection_settings_builder_result.is_ok());

        let connection_settings_builder_result = MqttConnectionSettingsBuilder::default()
            .client_id("test_client_id".to_string())
            .hostname("test_host".to_string())
            .cert_file("test_cert_file".to_string())
            .key_file("test_key_file".to_string())
            .build();
        assert!(connection_settings_builder_result.is_ok());
    }
}
