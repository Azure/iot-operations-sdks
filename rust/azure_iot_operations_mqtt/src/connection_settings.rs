// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

//! Generic MQTT connection settings implementations

use std::env::{self, VarError};
use std::time::Duration;

// TODO: Split up this struct to avoid weird combinations and separate concern.
// Things like having both password and password_file don't make much sense,
// nor frankly does combining MQTT and TLS settings.

/// All the settings required to establish an MQTT connection.
#[derive(Builder, Clone, Debug, Getters)]
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
    /// Values that are not present in the environment will be set to defaults (including those
    /// that are not possible to be provided by the AIO environment variables).
    ///
    /// Example
    /// ```
    /// # use azure_iot_operations_mqtt::{MqttConnectionSettings, MqttConnectionSettingsBuilder, MqttConnectionSettingsBuilderError};
    /// # fn try_main() -> Result<MqttConnectionSettings, MqttConnectionSettingsBuilderError> {
    /// let connection_settings = MqttConnectionSettingsBuilder::from_environment().unwrap().build()?;
    /// # Ok(connection_settings)
    /// # }
    /// # fn main() {
    /// #     // NOTE: This example is organized like this because we don't actually have env vars set, so it always panics
    /// #     try_main().ok();
    /// # }
    /// ```
    ///
    /// # Errors
    /// Returns a `String` describing the error if any of the environment variables contain invalid data.
    pub fn from_environment() -> Result<Self, String> {
        // Extract values from environment variables and parse them as needed and transform them
        // into the expected values for the builder.
        let client_id = string_from_environment("AIO_MQTT_CLIENT_ID")?;
        let hostname = string_from_environment("AIO_BROKER_HOSTNAME")?;
        let tcp_port = string_from_environment("AIO_BROKER_TCP_PORT")?
            .map(|v| v.parse::<u16>())
            .transpose()
            .map_err(|e| format!("AIO_BROKER_TCP_PORT: {e}"))?;
        let keep_alive = string_from_environment("AIO_MQTT_KEEP_ALIVE")?
            .map(|v| v.parse::<u32>().map(u64::from).map(Duration::from_secs))
            .transpose()
            .map_err(|e| format!("AIO_MQTT_KEEP_ALIVE: {e}"))?;
        let session_expiry = string_from_environment("AIO_MQTT_SESSION_EXPIRY")?
            .map(|v| v.parse::<u32>().map(u64::from).map(Duration::from_secs))
            .transpose()
            .map_err(|e| format!("AIO_MQTT_SESSION_EXPIRY: {e}"))?;
        let clean_start = string_from_environment("AIO_MQTT_CLEAN_START")?
            .map(|v| v.parse::<bool>())
            .transpose()
            .map_err(|e| format!("AIO_MQTT_CLEAN_START: {e}"))?;
        let username = string_from_environment("AIO_MQTT_USERNAME")?.map(Some);
        let password_file = string_from_environment("AIO_MQTT_PASSWORD_FILE")?.map(Some);
        let use_tls = string_from_environment("AIO_MQTT_USE_TLS")?
            .map(|v| v.parse::<bool>())
            .transpose()
            .map_err(|e| format!("AIO_MQTT_USE_TLS: {e}"))?;
        let ca_file = string_from_environment("AIO_TLS_CA_FILE")?.map(Some);
        let cert_file = string_from_environment("AIO_TLS_CERT_FILE")?.map(Some);
        let key_file = string_from_environment("AIO_TLS_KEY_FILE")?.map(Some);
        let key_password_file = string_from_environment("AIO_TLS_KEY_PASSWORD_FILE")?.map(Some);
        let sat_file = string_from_environment("AIO_SAT_FILE")?.map(Some);

        // Log warnings if required values are missing
        // NOTE: Do not error. It is valid to have empty values if the user will be overriding them,
        // and we do not want to prevent that. However, it likely suggests a misconfiguration, and
        // the errors from .validate() will not be particularly clear in this case, as it has no
        // way of knowing if the values originally came from the environment or were set by the user.
        if client_id.is_none() {
            log::warn!("AIO_MQTT_CLIENT_ID is not set in environment");
        }
        if hostname.is_none() {
            log::warn!("AIO_BROKER_HOSTNAME is not set in environment");
        }
        // Similar to the above, some fields are mutually exclusive, but shouldn't be an error,
        // since, per the builder pattern, it should technically be possible to override them,
        // although this is almost certainly a misconfiguration.
        if let (Some(Some(_)), Some(Some(_))) = (&sat_file, &password_file) {
            log::warn!(
                "AIO_SAT_FILE and AIO_MQTT_PASSWORD_FILE are both set in environment. Only one should be used."
            );
        }
        // And some fields are required to be provided together.
        match (&cert_file, &key_file) {
            (Some(Some(_)), Some(Some(_)))  // Both are set
            | (None | Some(None), None | Some(None)) // Neither is set (Some(None) technically impossible, but...)
            => (),
            _ => {
                log::warn!(
                    "AIO_TLS_CERT_FILE and AIO_TLS_KEY_FILE need to be set in environment together."
                );
            }
        }
        // And some fields require the presence of another
        if let (None | Some(None), Some(Some(_))) = (&key_file, &key_password_file) {
            log::warn!(
                "AIO_TLS_KEY_PASSWORD_FILE is set in environment, but AIO_TLS_KEY_FILE is not."
            );
        }

        Ok(Self {
            client_id,
            hostname,
            tcp_port,
            keep_alive,
            session_expiry,
            clean_start,
            username,
            password_file,
            use_tls,
            ca_file,
            cert_file,
            key_file,
            key_password_file,
            sat_file,
            ..Default::default()
        })
    }

    /// Validate the MQTT Connection Settings.
    ///
    /// # Errors
    /// Returns a `String` describing the error if the fields contain invalid values
    fn validate(&self) -> Result<(), String> {
        if self.hostname.as_ref().is_some_and(String::is_empty) {
            return Err("Host name cannot be empty".to_string());
        }
        if self.client_id.as_ref().is_some_and(String::is_empty) {
            return Err("client_id cannot be empty".to_string());
        }
        if [
            self.password.as_ref(),
            self.password_file.as_ref(),
            self.sat_file.as_ref(),
        ]
        .into_iter()
        .filter(|&v| v.is_some_and(|s| s.as_ref().is_some()))
        .count()
            > 1
        {
            return Err("Only one of password, password_file or sat_file can be used.".to_string());
        }
        match (self.key_file.as_ref(), self.cert_file.as_ref()) {
            (None | Some(None), None | Some(None)) => (),
            (Some(Some(key_file)), Some(Some(cert_file))) => {
                if cert_file.is_empty() || key_file.is_empty() {
                    return Err("key_file and cert_file cannot be empty".to_string());
                }
            }
            _ => return Err("key_file and cert_file need to be provided together.".to_string()),
        }
        if let (None | Some(None), Some(Some(_))) =
            (self.key_file.as_ref(), self.key_password_file.as_ref())
        {
            return Err("key_password_file is set, but key_file is not.".to_string());
        }
        Ok(())
    }
}

/// Helper function to get an environment variable as a string.
fn string_from_environment(key: &str) -> Result<Option<String>, String> {
    match env::var(key) {
        Ok(value) => Ok(Some(value)),
        Err(VarError::NotPresent) => Ok(None), // Handled by the validate function if required
        Err(VarError::NotUnicode(_)) => {
            Err("Could not parse non-unicode environment variable".to_string())
        }
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use test_case::test_case;

    #[test]
    fn minimum_configuration() {
        let connection_settings_builder_result = MqttConnectionSettingsBuilder::default()
            .client_id("test_client_id".to_string())
            .hostname("test_host".to_string())
            .build();
        assert!(connection_settings_builder_result.is_ok());
    }

    #[test]
    fn hostname() {
        let result = MqttConnectionSettingsBuilder::default()
            .client_id("test_client_id".to_string())
            .hostname(String::new())
            .build();
        assert!(result.is_err());
    }

    #[test]
    fn client_id() {
        let result = MqttConnectionSettingsBuilder::default()
            .hostname("test_host".to_string())
            .client_id(String::new())
            .build();
        assert!(result.is_err());
    }

    #[test]
    fn password_combos() {
        // The password and password_file cannot be used at the same time
        let result = MqttConnectionSettingsBuilder::default()
            .client_id("test_client_id".to_string())
            .hostname("test_host".to_string())
            .password("test_password".to_string())
            .password_file("test_password_file".to_string())
            .build();
        assert!(result.is_err());

        // The sat_file and password cannot be used at the same time
        let result = MqttConnectionSettingsBuilder::default()
            .client_id("test_client_id".to_string())
            .hostname("test_hostname".to_string())
            .password("test_password".to_string())
            .sat_file("test_sat_file".to_string())
            .build();
        assert!(result.is_err());

        // The sat_file and password_file cannot be used at the same time
        let result = MqttConnectionSettingsBuilder::default()
            .client_id("test_client_id".to_string())
            .hostname("test_host".to_string())
            .password_file("test_password_file".to_string())
            .sat_file("test_sat_auth_file".to_string())
            .build();
        assert!(result.is_err());

        // The sat_file, password and password_file cannot be used at the same time
        let result = MqttConnectionSettingsBuilder::default()
            .client_id("test_client_id".to_string())
            .hostname("test_host".to_string())
            .password("test_password".to_string())
            .password_file("test_password_file".to_string())
            .sat_file("test_sat_auth_file".to_string())
            .build();
        assert!(result.is_err());

        // But password alone works
        let connection_settings_builder_result = MqttConnectionSettingsBuilder::default()
            .client_id("test_client_id".to_string())
            .hostname("test_host".to_string())
            .password("test_password".to_string())
            .build();
        assert!(connection_settings_builder_result.is_ok());

        // But password_file alone works
        let connection_settings_builder_result = MqttConnectionSettingsBuilder::default()
            .client_id("test_client_id".to_string())
            .hostname("test_host".to_string())
            .password_file("test_password_file".to_string())
            .build();
        assert!(connection_settings_builder_result.is_ok());

        // But sat_file alone works
        let connection_settings_builder_result = MqttConnectionSettingsBuilder::default()
            .client_id("test_client_id".to_string())
            .hostname("test_host".to_string())
            .sat_file("test_sat_auth_file".to_string())
            .build();
        assert!(connection_settings_builder_result.is_ok());
    }

    #[test]
    fn cert_file_key_file_combos() {
        // The cert_file and key_file can be provided together
        let result = MqttConnectionSettingsBuilder::default()
            .client_id("test_client_id".to_string())
            .hostname("test_host".to_string())
            .cert_file("test_cert_file".to_string())
            .key_file("test_key_file".to_string())
            .build();
        assert!(result.is_ok());

        // The cert_file cannot be used without key_file
        let result = MqttConnectionSettingsBuilder::default()
            .client_id("test_client_id".to_string())
            .hostname("test_host".to_string())
            .cert_file("test_cert_file".to_string())
            .build();
        assert!(result.is_err());

        // The key_file cannot be used without cert_file
        let result = MqttConnectionSettingsBuilder::default()
            .client_id("test_client_id".to_string())
            .hostname("test_host".to_string())
            .key_file("test_key_file".to_string())
            .build();
        assert!(result.is_err());

        // The cert_file must have a non-empty value
        let result = MqttConnectionSettingsBuilder::default()
            .client_id("test_client_id".to_string())
            .hostname("test_host".to_string())
            .key_file("test_key_file".to_string())
            .cert_file(String::new())
            .build();
        assert!(result.is_err());

        // The key_file must have a non-empty value
        let result = MqttConnectionSettingsBuilder::default()
            .client_id("test_client_id".to_string())
            .hostname("test_host".to_string())
            .cert_file("test_cert_file".to_string())
            .key_file(String::new())
            .build();
        assert!(result.is_err());
    }

    #[test]
    fn key_file_password_combos() {
        // NOTE: Key file implies cert file as well, so cert file will be included in this test
        // even though it is not the element under test

        // The key file and key password file can be provided together
        let result = MqttConnectionSettingsBuilder::default()
            .client_id("test_client_id".to_string())
            .hostname("test_host".to_string())
            .cert_file("test_cert_file".to_string())
            .key_file("test_key_file".to_string())
            .key_password_file("test_key_password_file".to_string())
            .build();
        assert!(result.is_ok());

        // The key file can be provided without the key password file
        let result = MqttConnectionSettingsBuilder::default()
            .client_id("test_client_id".to_string())
            .hostname("test_host".to_string())
            .cert_file("test_cert_file".to_string())
            .key_file("test_key_file".to_string())
            .build();
        assert!(result.is_ok());

        // But the key password file cannot be used without the key file
        let result = MqttConnectionSettingsBuilder::default()
            .client_id("test_client_id".to_string())
            .hostname("test_host".to_string())
            .cert_file("test_cert_file".to_string())
            .key_password_file("test_key_password_file".to_string())
            .build();
        assert!(result.is_err());
    }

    // NOTE: Need to use alternate test cases here as these two forms of providing auth
    // are mutually exclusive.
    #[test_case("AIO_MQTT_PASSWORD_FILE", Some("/path/to/password/file"); "Password File Auth")]
    #[test_case("AIO_SAT_FILE", Some("/path/to/sat/file"); "SAT File Auth")]
    fn from_environment_full_configuration(auth_env_var: &str, auth_env_value: Option<&str>) {
        temp_env::with_vars(
            [
                ("AIO_MQTT_CLIENT_ID", Some("test-client-id")),
                ("AIO_BROKER_HOSTNAME", Some("test.hostname.com")),
                ("AIO_BROKER_TCP_PORT", Some("1883")),
                ("AIO_MQTT_KEEP_ALIVE", Some("60")),
                ("AIO_MQTT_SESSION_EXPIRY", Some("3600")),
                ("AIO_MQTT_CLEAN_START", Some("true")),
                ("AIO_MQTT_USERNAME", Some("test-username")),
                ("AIO_MQTT_USE_TLS", Some("true")),
                ("AIO_TLS_CA_FILE", Some("/path/to/ca/file")),
                ("AIO_TLS_CERT_FILE", Some("/path/to/cert/file")),
                ("AIO_TLS_KEY_FILE", Some("/path/to/key/file")),
                (
                    "AIO_TLS_KEY_PASSWORD_FILE",
                    Some("/path/to/key/password/file"),
                ),
                // Set default None values for mutually exclusive auth vars, then override
                ("AIO_MQTT_PASSWORD_FILE", None),
                ("AIO_SAT_FILE", None),
                (auth_env_var, auth_env_value), // This will override one of the above two vars
            ],
            || {
                let builder = MqttConnectionSettingsBuilder::from_environment().unwrap();
                // Validate that all values from env variables were set on the builder
                assert_eq!(builder.client_id, Some("test-client-id".to_string()));
                assert_eq!(builder.hostname, Some("test.hostname.com".to_string()));
                assert_eq!(builder.tcp_port, Some(1883));
                assert_eq!(builder.keep_alive, Some(Duration::from_secs(60)));
                assert_eq!(builder.session_expiry, Some(Duration::from_secs(3600)));
                assert_eq!(builder.clean_start, Some(true));
                assert_eq!(builder.username, Some(Some("test-username".to_string())));
                assert_eq!(builder.use_tls, Some(true));
                assert_eq!(builder.ca_file, Some(Some("/path/to/ca/file".to_string())));
                assert_eq!(
                    builder.cert_file,
                    Some(Some("/path/to/cert/file".to_string()))
                );
                assert_eq!(
                    builder.key_file,
                    Some(Some("/path/to/key/file".to_string()))
                );
                assert_eq!(
                    builder.key_password_file,
                    Some(Some("/path/to/key/password/file".to_string()))
                );

                if auth_env_var == "AIO_MQTT_PASSWORD_FILE" {
                    assert_eq!(
                        builder.password_file,
                        Some(Some("/path/to/password/file".to_string()))
                    );
                } else if auth_env_var == "AIO_SAT_FILE" {
                    assert_eq!(
                        builder.sat_file,
                        Some(Some("/path/to/sat/file".to_string()))
                    );
                } else {
                    panic!("Unexpected auth_env_var: {auth_env_var}");
                }
                // Validate that the default values were set correctly for values that were not
                // provided
                let default_builder = MqttConnectionSettingsBuilder::default();
                assert_eq!(builder.receive_max, default_builder.receive_max);
                assert_eq!(
                    builder.receive_packet_size_max,
                    default_builder.receive_packet_size_max
                );
                assert_eq!(
                    builder.connection_timeout,
                    default_builder.connection_timeout
                );
                assert_eq!(builder.password, default_builder.password);
                // Validate that the settings struct can be built using only the values provided
                // from the environment
                assert!(builder.build().is_ok());
            },
        );
    }

    #[test]
    fn from_environment_minimal_configuration() {
        temp_env::with_vars(
            [
                ("AIO_MQTT_CLIENT_ID", Some("test-client-id")),
                ("AIO_BROKER_HOSTNAME", Some("test.hostname.com")),
                ("AIO_BROKER_TCP_PORT", None),
                ("AIO_MQTT_KEEP_ALIVE", None),
                ("AIO_MQTT_SESSION_EXPIRY", None),
                ("AIO_MQTT_CLEAN_START", None),
                ("AIO_MQTT_USERNAME", None),
                ("AIO_MQTT_PASSWORD_FILE", None),
                ("AIO_MQTT_USE_TLS", None),
                ("AIO_TLS_CA_FILE", None),
                ("AIO_TLS_CERT_FILE", None),
                ("AIO_TLS_KEY_FILE", None),
                ("AIO_TLS_KEY_PASSWORD_FILE", None),
                ("AIO_SAT_FILE", None),
            ],
            || {
                let builder = MqttConnectionSettingsBuilder::from_environment().unwrap();
                // Validate that all values from env variables were set on the builder
                assert_eq!(builder.client_id, Some("test-client-id".to_string()));
                assert_eq!(builder.hostname, Some("test.hostname.com".to_string()));
                // Validate that the default values were set correctly for values that were not
                // provided
                let default_builder = MqttConnectionSettingsBuilder::default();
                assert_eq!(builder.tcp_port, default_builder.tcp_port);
                assert_eq!(builder.keep_alive, default_builder.keep_alive);
                assert_eq!(builder.receive_max, default_builder.receive_max);
                assert_eq!(
                    builder.receive_packet_size_max,
                    default_builder.receive_packet_size_max
                );
                assert_eq!(builder.session_expiry, default_builder.session_expiry);
                assert_eq!(
                    builder.connection_timeout,
                    default_builder.connection_timeout
                );
                assert_eq!(builder.clean_start, default_builder.clean_start);
                assert_eq!(builder.username, default_builder.username);
                assert_eq!(builder.password, default_builder.password);
                assert_eq!(builder.password_file, default_builder.password_file);
                assert_eq!(builder.use_tls, default_builder.use_tls);
                assert_eq!(builder.ca_file, default_builder.ca_file);
                assert_eq!(builder.cert_file, default_builder.cert_file);
                assert_eq!(builder.key_file, default_builder.key_file);
                assert_eq!(builder.key_password_file, default_builder.key_password_file);
                assert_eq!(builder.sat_file, default_builder.sat_file);
                // Validate that the settings struct can be built using only the values provided
                // from the environment
                assert!(builder.build().is_ok());
            },
        );
    }

    #[test_case(None, None; "All required values missing")]
    #[test_case(Some("test-client-id"), None; "Client ID missing")]
    #[test_case(None, Some("test.hostname.com"); "Hostname missing")]
    fn from_environment_missing_required_values(client_id: Option<&str>, hostname: Option<&str>) {
        // No environment variables
        temp_env::with_vars(
            [
                ("AIO_MQTT_CLIENT_ID", client_id),
                ("AIO_BROKER_HOSTNAME", hostname),
            ],
            || {
                let builder = MqttConnectionSettingsBuilder::from_environment().unwrap();
                // Builder can be created successfully with .from_environment(), but will fail on
                // .build() unless modified to include the required values.
                assert!(builder.build().is_err());
            },
        );
    }

    // NOTE: This test does NOT cover the case where environment variable is set to a value
    // that cannot be parsed as a unicode string. While there is error handling for that case
    // in the implementation, we cannot programmatically set environment variables to invalid
    // strings (e.g. utf-16) in a platform independent way. Revisit with platform-specific tests
    // if necessary.
    #[test_case("AIO_BROKER_TCP_PORT", "not numeric"; "tcp_port")]
    #[test_case("AIO_MQTT_KEEP_ALIVE", "not numeric"; "keep_alive")]
    #[test_case("AIO_MQTT_SESSION_EXPIRY", "not numeric"; "session_expiry")]
    #[test_case("AIO_MQTT_CLEAN_START", "not boolean"; "clean_start")]
    #[test_case("AIO_MQTT_USE_TLS", "not boolean"; "use_tls")]
    fn from_environment_nonstring_value_parsing(env_var: &str, invalid_value: &str) {
        // Provide minimal configuration
        temp_env::with_vars(
            [
                ("AIO_MQTT_CLIENT_ID", Some("test-client-id")),
                ("AIO_BROKER_HOSTNAME", Some("test.hostname.com")),
                (env_var, Some(invalid_value)),
            ],
            || {
                // Fails on .from_environment(), not .build()
                assert!(MqttConnectionSettingsBuilder::from_environment().is_err());
            },
        );
    }
}
