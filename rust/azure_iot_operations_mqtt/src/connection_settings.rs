// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

//! Generic MQTT connection settings implementations

use std::env::{self, VarError};
use std::path::{Path, PathBuf};
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
        if sat_file.is_some() && password_file.is_some() {
            log::warn!(
                "AIO_SAT_FILE and AIO_MQTT_PASSWORD_FILE are both set in environment. Only one should be used."
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

    /// Construct a builder from the configuration files mounted by the Akri Operator.
    /// This method is only usable for connector applications deployed as a kubernetes pod.
    ///
    /// Values that are not present in the configuration file mounts will be set to defaults
    /// (including those that are not possible to be provided by file mounts).
    ///
    /// # Examples
    ///
    /// ```
    /// # use azure_iot_operations_mqtt::{MqttConnectionSettings, MqttConnectionSettingsBuilder, MqttConnectionSettingsBuilderError};
    /// # fn try_main() -> Result<MqttConnectionSettings, String> {
    /// let builder = MqttConnectionSettingsBuilder::from_file_mount()?;
    /// let connection_settings = builder.build()
    ///     .map_err(|e| format!("Failed to build settings: {}", e))?;
    /// # Ok(connection_settings)
    /// # }
    /// # fn main() {
    /// #     // Example not run as part of docs
    /// #     try_main().ok();
    /// # }
    /// ```
    ///
    /// # Errors
    ///
    /// Returns a `String` describing the error if:
    /// - Required environment variables are missing
    /// - Configuration files cannot be read
    /// - Configuration values are invalid
    pub fn from_file_mount() -> Result<Self, String> {
        // --- Mount 1: AEP_CONFIGMAP_MOUNT_PATH ---
        let (client_id, hostname, tcp_port, use_tls) = {
            if let Some(s) = string_from_environment("AEP_CONFIGMAP_MOUNT_PATH")? {
                let aep_pathbuf = PathBuf::from(&s);
                if !aep_pathbuf.as_path().exists() {
                    return Err(format!("Config map path does not exist: {s}"));
                }
                // Read target address (hostname:port)
                let (hostname, tcp_port) = {
                    match string_from_configmap_file(&aep_pathbuf, "BROKER_TARGET_ADDRESS")? {
                        Some(target_address) => {
                            // Parse hostname and port from target address
                            let (hostname, tcp_port) = target_address.split_once(':').ok_or(
                                format!(
                                    "BROKER_TARGET_ADDRESS is malformed. Expected format <hostname>:<port>. Found: {target_address}"
                                )
                            )?;
                            (
                                Some(hostname.to_string()),
                                Some(tcp_port.parse::<u16>().map_err(|e| {
                                    format!(
                                        "Cannot parse MQTT port from BROKER_TARGET_ADDRESS: {e}"
                                    )
                                })?),
                            )
                        }
                        None => (None, None),
                    }
                };
                // Read client ID
                let client_id = string_from_configmap_file(&aep_pathbuf, "AIO_MQTT_CLIENT_ID")?;
                // Read use TLS setting
                let use_tls = string_from_configmap_file(&aep_pathbuf, "BROKER_USE_TLS")?
                    .map(|v| v.parse::<bool>())
                    .transpose()
                    .map_err(|e| format!("BROKER_USE_TLS: {e}"))?;

                (client_id, hostname, tcp_port, use_tls)
            } else {
                // NOTE: See the warning section father below in the function for an
                // explanation of why this isn't an error.
                log::warn!("AEP_CONFIGMAP_MOUNT_PATH is not set in environment");
                (None, None, None, None)
            }
        };

        // --- Mount 2: BROKER_SAT_MOUNT_PATH ---
        // NOTE: This will be moved to be part of Mount 1 in the future.
        let sat_file = string_from_environment("BROKER_SAT_MOUNT_PATH")?.map(Some);

        // --- Mount 3: BROKER_TLS_TRUST_BUNDLE_CACERT_MOUNT_PATH ---
        let ca_file =
            string_from_environment("BROKER_TLS_TRUST_BUNDLE_CACERT_MOUNT_PATH")?.map(Some);

        // Log warnings if required values are missing
        // NOTE: Do not error. It is valid to have empty values if the user will be overriding them
        // (as is idiomatic for the Builder pattern), and we do not want to prevent that. However,
        // it likely suggests a misconfiguration, and the errors from .validate() will not be
        // particularly clear in this case, as it has no way of knowing if the values came from the
        // configmap or were set by the user.
        if client_id.is_none() {
            log::warn!("AIO_MQTT_CLIENT_ID is not set in AEP configmap");
        }
        if hostname.is_none() || tcp_port.is_none() {
            log::warn!("BROKER_TARGET_ADDRESS is not set in AEP configmap");
        }

        Ok(Self {
            client_id,
            hostname,
            tcp_port,
            use_tls,
            ca_file,
            sat_file,
            ..Default::default()
        })
    }

    /// Validate the MQTT Connection Settings.
    ///
    /// # Errors
    /// Returns a `String` describing the error if the fields contain invalid values
    fn validate(&self) -> Result<(), String> {
        if self.hostname.as_ref().is_some_and(|h| h.is_empty()) {
            return Err("Host name cannot be empty".to_string());
        }
        if self.client_id.as_ref().is_some_and(|c| c.is_empty()) {
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

/// Helper function to extract the value from a config map file as a string.
fn string_from_configmap_file(
    configmap_path: &Path,
    filename: &str,
) -> Result<Option<String>, String> {
    let file_pathbuf = configmap_path.join(filename);
    if !file_pathbuf.as_path().exists() {
        return Ok(None); // File doesn't exist, return None
    }
    let value = std::fs::read_to_string(file_pathbuf)
        .map_err(|e| format!("Malformed {filename} file: {e}"))?;
    // NOTE: Can't chain .trim() due to memory rules
    Ok(Some(value.trim().to_string()))
}

#[cfg(test)]
mod tests {
    use super::*;
    use std::fs;
    use std::path::Path;
    use test_case::test_case;

    /// Struct that managed a temporary Config Map created for testing purposes.
    /// When dropped, the Config Map directory, and all files in it will be removed.
    struct TempConfigMapManager {
        dir: tempfile::TempDir,
    }

    impl TempConfigMapManager {
        fn new(dir_name: &str) -> Self {
            Self {
                dir: tempfile::TempDir::with_prefix(dir_name).unwrap(),
            }
        }

        fn add_file(&self, file_name: &str, contents: &str) {
            let file_path = self.dir.path().join(file_name);
            fs::write(file_path, contents).unwrap();
        }

        fn path(&self) -> &Path {
            self.dir.path()
        }
    }

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

    #[test]
    fn from_file_mount_full_configuration() {
        let aep_configmap_manager = TempConfigMapManager::new("aep_configmap");
        aep_configmap_manager.add_file("BROKER_TARGET_ADDRESS", "test.hostname.com:8883");
        aep_configmap_manager.add_file("AIO_MQTT_CLIENT_ID", "test-client-id");
        aep_configmap_manager.add_file("BROKER_USE_TLS", "true");

        temp_env::with_vars(
            [
                (
                    "AEP_CONFIGMAP_MOUNT_PATH",
                    Some(aep_configmap_manager.path().to_str().unwrap()),
                ),
                // NOTE: BROKER_SAT_MOUNT_PATH will eventually be part of the above Config Map
                ("BROKER_SAT_MOUNT_PATH", Some("/path/to/sat/file.sat")),
                // NOTE: Currently this is just a file, may need to be made a Config Map in the future
                (
                    "BROKER_TLS_TRUST_BUNDLE_CACERT_MOUNT_PATH",
                    Some("/path/to/ca/certs"),
                ),
            ],
            || {
                let builder = MqttConnectionSettingsBuilder::from_file_mount().unwrap();
                // Validate AEP ConfigMap values
                assert_eq!(builder.hostname, Some("test.hostname.com".to_string()));
                assert_eq!(builder.tcp_port, Some(8883));
                assert_eq!(builder.client_id, Some("test-client-id".to_string()));
                assert_eq!(builder.use_tls, Some(true));
                // Validate SAT file value
                assert_eq!(
                    builder.sat_file,
                    Some(Some("/path/to/sat/file.sat".to_string()))
                );
                // Validate CA file value
                assert_eq!(builder.ca_file, Some(Some("/path/to/ca/certs".to_string())));
                // Validate that the default values were set correctly for values that were not
                // provided
                let default_builder = MqttConnectionSettingsBuilder::default();
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
                assert_eq!(builder.cert_file, default_builder.cert_file);
                assert_eq!(builder.key_file, default_builder.key_file);
                assert_eq!(builder.key_password_file, default_builder.key_password_file);
                // Validate that the settings struct can be built using only the values provided
                // from the file mounts
                assert!(builder.build().is_ok());
            },
        );
    }

    #[test]
    fn from_file_mount_minimum_configuration() {
        let aep_configmap_manager = TempConfigMapManager::new("aep_configmap");
        aep_configmap_manager.add_file("BROKER_TARGET_ADDRESS", "test.hostname.com:8883");
        aep_configmap_manager.add_file("AIO_MQTT_CLIENT_ID", "test-client-id");

        temp_env::with_var(
            "AEP_CONFIGMAP_MOUNT_PATH",
            Some(aep_configmap_manager.path().to_str().unwrap()),
            || {
                let builder = MqttConnectionSettingsBuilder::from_file_mount().unwrap();
                // Validate AEP ConfigMap values
                assert_eq!(builder.hostname, Some("test.hostname.com".to_string()));
                assert_eq!(builder.tcp_port, Some(8883));
                assert_eq!(builder.client_id, Some("test-client-id".to_string()));
                // Validate that the default values were set correctly for values that were not
                // provided
                let default_builder = MqttConnectionSettingsBuilder::default();
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
                // from the file mounts
                assert!(builder.build().is_ok());
            },
        );
    }

    #[test_case("BROKER_TARGET_ADDRESS"; "BROKER_TARGET_ADDRESS")]
    #[test_case("AIO_MQTT_CLIENT_ID"; "AIO_MQTT_CLIENT_ID")]
    fn from_file_mount_missing_files_with_required_values(missing_filename: &str) {
        let aep_configmap_manager = TempConfigMapManager::new("aep_configmap");
        if missing_filename != "BROKER_TARGET_ADDRESS" {
            aep_configmap_manager.add_file("BROKER_TARGET_ADDRESS", "test.hostname.com:8883");
        } else if missing_filename != "AIO_MQTT_CLIENT_ID" {
            aep_configmap_manager.add_file("AIO_MQTT_CLIENT_ID", "test-client-id");
        }

        temp_env::with_var(
            "AEP_CONFIGMAP_MOUNT_PATH",
            Some(aep_configmap_manager.path().to_str().unwrap()),
            || {
                let builder = MqttConnectionSettingsBuilder::from_file_mount().unwrap();
                // Builder can be created successfully with .from_file_mount(), but will fail on
                // .build() unless modified to include the required values.
                assert!(builder.build().is_err());
            },
        );
    }

    // NOTE: this will need test cases if there are ever additional mounts with required values
    #[test]
    fn from_file_mount_no_env_var_for_mount_with_required_values() {
        temp_env::with_var(
            "AEP_CONFIGMAP_MOUNT_PATH",
            None::<&str>, // Simulate missing environment variable (no mount dir set)
            || {
                let builder = MqttConnectionSettingsBuilder::from_file_mount().unwrap();
                // Builder can be created successfully with .from_file_mount(), but will fail on
                // .build() unless modified to include the required values.
                assert!(builder.build().is_err());
            },
        );
    }

    // NOTE: There is no test for the case where environment variable is set to a value
    // that cannot be parsed as a unicode string. While there is error handling for that cases
    // in the implementation (it causes failure), we cannot programmatically set environment
    // variables to invalid strings (e.g. utf-16) in a platform independent way. Revisit with
    // platform-specific tests if necessary.
    // NOTE: this will need a test matrix if there are ever additional directory mounts (e.g. CA cert)
    #[test_case("/nonexistent/path/to/configmap"; "Nonexistent path")]
    #[test_case(""; "Empty string")]
    fn from_file_mount_invalid_configmap_mount_env_var(invalid_path: &str) {
        temp_env::with_var("AEP_CONFIGMAP_MOUNT_PATH", Some(invalid_path), || {
            assert!(MqttConnectionSettingsBuilder::from_file_mount().is_err());
        });
    }

    #[test_case("BROKER_TARGET_ADDRESS", "not-correct-format"; "Target address format")]
    #[test_case("BROKER_TARGET_ADDRESS", "test.hostname.com:not_a_number"; "u16 parsing tcp_port")]
    #[test_case("BROKER_USE_TLS", "not boolean"; "bool parsing use_tls")]
    fn from_file_mount_configmap_file_parsing(env_var: &str, invalid_value: &str) {
        // NOTE: This does not meet the minimum configuration requirements for the builder to build,
        // but as previous tests demonstrated, not having the values != failure when creating the
        // builder, only when building. What we're testing for here is failures that happen when
        // creating the builder from the file mount.
        let aep_configmap_manager = TempConfigMapManager::new("aep_configmap");
        aep_configmap_manager.add_file(env_var, invalid_value);

        temp_env::with_var(
            "AEP_CONFIGMAP_MOUNT_PATH",
            Some(aep_configmap_manager.path().to_str().unwrap()),
            || {
                // Fails on .from_file_mount(), not .build()
                assert!(MqttConnectionSettingsBuilder::from_file_mount().is_err());
            },
        );
    }
}
