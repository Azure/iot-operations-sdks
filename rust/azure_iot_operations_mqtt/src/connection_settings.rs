// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

//! Generic MQTT connection settings implementations

use std::env;
use std::time::Duration;

/// All the settings required to establish an MQTT connection.
#[derive(Builder, Clone)]
#[builder(pattern = "owned", setter(into), build_fn(validate = "Self::validate"))]
pub struct MqttConnectionSettings {
    /// Client identifier
    pub(crate) client_id: String,
    /// FQDN of the host to connect to
    pub(crate) host_name: String,
    /// TCP port to connect to the host on
    #[builder(default = "8883")]
    pub(crate) tcp_port: u16,
    /// Max time between communications
    #[builder(default = "Duration::from_secs(60)")]
    pub(crate) keep_alive: Duration,
    /// Session Expiry Interval
    #[builder(default = "Duration::from_secs(3600)")]
    // What is the default for this? Spec is unclear
    pub(crate) session_expiry: Duration,
    /// Connection timeout
    #[builder(default = "Duration::from_secs(30)")]
    pub(crate) connection_timeout: Duration,
    /// Clean start
    #[builder(default = "true")]
    pub(crate) clean_start: bool,
    /// Username for MQTT
    #[builder(default = "None")]
    pub(crate) username: Option<String>,
    /// Password for MQTT
    #[builder(default = "None")]
    pub(crate) password: Option<String>,
    /// Path to a file with the MQTT password
    #[builder(default = "None")]
    pub(crate) password_file: Option<String>,
    /// TLS negotiation enabled
    #[builder(default = "true")]
    pub(crate) use_tls: bool,
    /// Path to a PEM file to validate server identity
    #[builder(default = "None")]
    pub(crate) ca_file: Option<String>,
    /// Check the revocation status of the CA
    #[builder(default = "false")]
    pub(crate) ca_require_revocation_check: bool,
    /// Path to PEM file to establish X509 client authentication
    #[builder(default = "None")]
    pub(crate) cert_file: Option<String>,
    /// Path to a KEY file to establish X509 client authentication
    #[builder(default = "None")]
    pub(crate) key_file: Option<String>,
    /// Password (aka pass-phrase) to protect the KEY file
    #[builder(default = "None")]
    pub(crate) key_file_password: Option<String>,
    /// Path to a SAT file to be used for SAT auth
    #[builder(default = "None")]
    pub(crate) sat_auth_file: Option<String>,
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
        let client_id = env::var("MQTT_CLIENT_ID").ok();
        let host_name = env::var("MQTT_HOST_NAME").ok();
        let tcp_port = env::var("MQTT_TCP_PORT")
            .ok()
            .map(|v| v.parse::<u16>())
            .transpose()
            .unwrap_or(None);
        let keep_alive = env::var("MQTT_KEEP_ALIVE")
            .ok()
            .map(|v| v.parse::<u64>().map(Duration::from_secs))
            .transpose()
            .unwrap_or(None);
        let session_expiry = env::var("MQTT_SESSION_EXPIRY")
            .ok()
            .map(|v| v.parse::<u64>().map(Duration::from_secs))
            .transpose()
            .unwrap_or(None);
        let connection_timeout = env::var("MQTT_CONNECTION_TIMEOUT")
            .ok()
            .map(|v| v.parse::<u64>().map(Duration::from_secs))
            .transpose()
            .unwrap_or(None);
        let clean_start = env::var("MQTT_CLEAN_START")
            .ok()
            .map(|v| v.parse::<bool>())
            .transpose()
            .unwrap_or(None);
        let username = Some(env::var("MQTT_USERNAME").ok());
        let password = Some(env::var("MQTT_PASSWORD").ok());
        let password_file = Some(env::var("MQTT_PASSWORD_FILE").ok());
        let use_tls = env::var("MQTT_USE_TLS")
            .ok()
            .map(|v| v.parse::<bool>())
            .transpose()
            .unwrap_or(None);
        let ca_file = Some(env::var("MQTT_CA_FILE").ok());
        let ca_require_revocation_check = env::var("MQTT_CA_REQUIRE_REVOCATION_CHECK")
            .ok()
            .map(|v| v.parse::<bool>())
            .transpose()
            .unwrap_or(None);
        let cert_file = Some(env::var("MQTT_CERT_FILE").ok());
        let key_file = Some(env::var("MQTT_KEY_FILE").ok());
        let key_file_password = Some(env::var("MQTT_KEY_FILE_PASSWORD").ok());
        let sat_auth_file = Some(env::var("MQTT_SAT_AUTH_FILE").ok());

        // TODO: `keep_alive`, `session_expiry` and `connection_timeout` are supposed to be serialized using ISO 8601
        // TODO: consider removing some of the Option wrappers in the Builder definition to avoid these spurious Some() wrappers.

        Self {
            client_id,
            host_name,
            tcp_port,
            keep_alive,
            session_expiry,
            connection_timeout,
            clean_start,
            username,
            password,
            password_file,
            use_tls,
            ca_file,
            ca_require_revocation_check,
            cert_file,
            key_file,
            key_file_password,
            sat_auth_file,
        }
    }

    /// Validate the MQTT Connection Settings.
    ///
    /// # Errors
    /// Returns a `String` describing the error if
    /// - `host_name` is empty
    /// - `client_id` is empty and `clean_start` is false
    /// - `password` and `password_file` are both Some
    /// - `sat_auth_file` is Some and `password` or `password_file` are Some
    /// - `key_file` is Some and `cert_file` is None or empty
    fn validate(&self) -> Result<(), String> {
        if let Some(host_name) = &self.host_name {
            if host_name.is_empty() {
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
        if let Some(sat_auth_file) = &self.sat_auth_file {
            if sat_auth_file.is_some() {
                if let Some(password) = &self.password {
                    if password.is_some() {
                        return Err(
                            "sat_auth_file cannot be used with password or password_file."
                                .to_string(),
                        );
                    }
                }
                if let Some(password_file) = &self.password_file {
                    if password_file.is_some() {
                        return Err(
                            "sat_auth_file cannot be used with password or password_file."
                                .to_string(),
                        );
                    }
                }
            }
        }
        if let Some(key_file) = &self.key_file {
            if key_file.is_some() {
                if let Some(cert_file) = &self.cert_file {
                    if cert_file.is_none() || cert_file.clone().unwrap().is_empty() {
                        return Err(
                            "key_file and cert_file need to be provided together.".to_string()
                        );
                    }
                } else {
                    return Err("key_file and cert_file need to be provided together.".to_string());
                }
            }
        }
        Ok(())
    }
}

#[cfg(test)]
mod tests {
    use super::MqttConnectionSettingsBuilder;

    #[test]
    fn test_connection_settings_empty_host_name() {
        let connection_settings_builder_result = MqttConnectionSettingsBuilder::default()
            .client_id("test_client_id".to_string())
            .host_name(String::new())
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
            .host_name("test_host".to_string())
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
            .host_name("test_host".to_string())
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
            .host_name("test_host".to_string())
            .clean_start(true)
            .build();
        assert!(connection_settings_builder_result.is_ok());
    }

    #[test]
    fn test_connection_settings_password_combos() {
        let connection_settings_builder_result = MqttConnectionSettingsBuilder::default()
            .client_id("test_client_id".to_string())
            .host_name("test_host".to_string())
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
            .host_name("test_host".to_string())
            .password("test_password".to_string())
            .sat_auth_file("test_sat_auth_file".to_string())
            .build();
        match connection_settings_builder_result {
            Ok(_) => panic!("Expected error"),
            Err(e) => assert_eq!(
                e.to_string(),
                "sat_auth_file cannot be used with password or password_file."
            ),
        }
        let connection_settings_builder_result = MqttConnectionSettingsBuilder::default()
            .client_id("test_client_id".to_string())
            .host_name("test_host".to_string())
            .password_file("test_password_file".to_string())
            .sat_auth_file("test_sat_auth_file".to_string())
            .build();
        match connection_settings_builder_result {
            Ok(_) => panic!("Expected error"),
            Err(e) => assert_eq!(
                e.to_string(),
                "sat_auth_file cannot be used with password or password_file."
            ),
        }

        let connection_settings_builder_result = MqttConnectionSettingsBuilder::default()
            .client_id("test_client_id".to_string())
            .host_name("test_host".to_string())
            .password("test_password".to_string())
            .password_file("test_password_file".to_string())
            .sat_auth_file("test_sat_auth_file".to_string())
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
            .host_name("test_host".to_string())
            .password("test_password".to_string())
            .build();
        assert!(connection_settings_builder_result.is_ok());

        let connection_settings_builder_result = MqttConnectionSettingsBuilder::default()
            .client_id("test_client_id".to_string())
            .host_name("test_host".to_string())
            .password_file("test_password_file".to_string())
            .build();
        assert!(connection_settings_builder_result.is_ok());

        let connection_settings_builder_result = MqttConnectionSettingsBuilder::default()
            .client_id("test_client_id".to_string())
            .host_name("test_host".to_string())
            .sat_auth_file("test_sat_auth_file".to_string())
            .build();
        assert!(connection_settings_builder_result.is_ok());
    }

    #[test]
    fn test_connection_settings_cert_key_file() {
        let connection_settings_builder_result = MqttConnectionSettingsBuilder::default()
            .client_id("test_client_id".to_string())
            .host_name("test_host".to_string())
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
            .host_name("test_host".to_string())
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
            .host_name("test_host".to_string())
            .cert_file(String::new())
            .build();
        assert!(connection_settings_builder_result.is_ok());

        let connection_settings_builder_result = MqttConnectionSettingsBuilder::default()
            .client_id("test_client_id".to_string())
            .host_name("test_host".to_string())
            .cert_file("test_cert_file".to_string())
            .build();
        assert!(connection_settings_builder_result.is_ok());

        let connection_settings_builder_result = MqttConnectionSettingsBuilder::default()
            .client_id("test_client_id".to_string())
            .host_name("test_host".to_string())
            .cert_file("test_cert_file".to_string())
            .key_file("test_key_file".to_string())
            .build();
        assert!(connection_settings_builder_result.is_ok());
    }
}
