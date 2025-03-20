// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

//! Generic MQTT connection settings implementations

use std::env::{self, VarError};
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
    /// Returns a `String` describing the error if any of the environment variables are invalid.
    pub fn from_environment() -> Result<Self, String> {
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
        let username = Some(string_from_environment("AIO_MQTT_USERNAME")?);
        let password_file = Some(string_from_environment("AIO_MQTT_PASSWORD_FILE")?);
        let use_tls = string_from_environment("AIO_MQTT_USE_TLS")?
            .map(|v| v.parse::<bool>())
            .transpose()
            .map_err(|e| format!("AIO_MQTT_USE_TLS: {e}"))?;
        let ca_file = Some(string_from_environment("AIO_TLS_CA_FILE")?);
        let cert_file = Some(string_from_environment("AIO_TLS_CERT_FILE")?);
        let key_file = Some(string_from_environment("AIO_TLS_KEY_FILE")?);
        let key_password_file = Some(string_from_environment("AIO_TLS_KEY_PASSWORD_FILE")?);
        let sat_file = Some(string_from_environment("AIO_SAT_FILE")?);

        // Log errors if required values are missing
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

        // TODO: consider removing some of the Option wrappers in the Builder definition to avoid these spurious Some() wrappers.

        Ok(Self {
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
        })
    }
    /// Construct a builder from the configuration files mounted by the Akri Operator.
    /// This method is only usable for connector applications deployed as a kubernetes pod.
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
        let config_map_path = string_from_environment("AEP_CONFIGMAP_MOUNT_PATH")?
            .ok_or_else(|| "AEP_CONFIGMAP_MOUNT_PATH is not set".to_string())?;

        if !std::path::Path::new(&config_map_path).exists() {
            return Err(format!("Config map path does not exist: {config_map_path}"));
        }
        // Read target address (hostname:port)
        let target_address_path = format!("{config_map_path}/BROKER_TARGET_ADDRESS");
        let target_address = std::fs::read_to_string(&target_address_path)
            .map_err(|e| format!("Failed to read BROKER_TARGET_ADDRESS: {e}"))?;

        let target_address_and_port = target_address.trim();
        if target_address_and_port.is_empty() {
            return Err("BROKER_TARGET_ADDRESS is missing.".to_string());
        }

        // Parse hostname and port
        let target_address_parts: Vec<&str> = target_address_and_port.split(':').collect();
        if target_address_parts.len() != 2 {
            return Err(format!(
                "BROKER_TARGET_ADDRESS is malformed. Expected format <hostname>:<port>. Found: {target_address_and_port}",
            ));
        }

        let hostname = target_address_parts[0].to_string();
        let tcp_port = target_address_parts[1]
            .parse::<u16>()
            .map_err(|e| format!("Cannot parse MQTT port from BROKER_TARGET_ADDRESS: {e}"))?;

        // Read TLS setting
        let use_tls_path = format!("{config_map_path}/BROKER_USE_TLS");
        let use_tls_str = std::fs::read_to_string(&use_tls_path)
            .map_err(|e| format!("Failed to read BROKER_USE_TLS: {e}"))?;

        let use_tls = use_tls_str.trim().parse::<bool>().map_err(|_| {
            "BROKER_USE_TLS contains a value that could not be parsed as a boolean".to_string()
        })?;

        // Optional SAT file path, so no need to validate that this file exists
        let sat_file = Some(string_from_environment("BROKER_SAT_MOUNT_PATH")?);

        let ca_file = Some(string_from_environment(
            "BROKER_TLS_TRUST_BUNDLE_CACERT_MOUNT_PATH",
        )?);

        // Read client ID from configuration file
        let client_id_path = format!("{config_map_path}/AIO_MQTT_CLIENT_ID");
        let client_id = match std::fs::read_to_string(&client_id_path) {
            Ok(id) => {
                let id = id.trim();
                if id.is_empty() {
                    return Err("AIO_MQTT_CLIENT_ID is missing.".to_string());
                }
                id.to_string()
            }
            Err(e) => {
                return Err(format!(
                    "Missing or malformed client ID configuration file: {e}"
                ));
            }
        };

        // Return builder with settings
        Ok(Self {
            client_id: Some(client_id),
            hostname: Some(hostname),
            tcp_port: Some(tcp_port),
            keep_alive: Some(Duration::from_secs(60)),
            receive_max: Some(u16::MAX),
            receive_packet_size_max: None,
            session_expiry: Some(Duration::from_secs(3600)),
            connection_timeout: Some(Duration::from_secs(30)),
            clean_start: Some(true), // Default to true for file mount
            username: None,
            password: None,
            password_file: None,
            use_tls: Some(use_tls),
            ca_file,
            cert_file: None,
            key_file: None,
            key_password_file: None,
            sat_file,
        })
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

/// Helper function to get an environment variable as a string.
fn string_from_environment(key: &str) -> Result<Option<String>, String> {
    match env::var(key) {
        Ok(value) => Ok(Some(value)),
        Err(VarError::NotPresent) => Ok(None), // Handled by the validate function if required
        Err(e) => Err(format!("Parsing {key} from environment failed: {e}")),
    }
}

#[cfg(test)]
mod tests {
    use super::MqttConnectionSettingsBuilder;
    // use std::env;
    // use std::fs;
    // use std::path::PathBuf;
    // use std::sync::Mutex;

    //pub static FILE_DIR_MTX: Mutex<()> = Mutex::new(()); // TODO: Find a better way to control test flow

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

    // // Helper function to create a unique temporary directory
    // fn create_temp_dir() -> (PathBuf, String) {
    //     // Create a directory name - it can be sae as tests are using mutex
    //     let temp_dir_path = "/tmp/mqtt_test".to_string();
    //     let path_buf = PathBuf::from(&temp_dir_path);

    //     // Create the directory
    //     fs::create_dir_all(&path_buf).expect("Failed to create temp directory");

    //     (path_buf, temp_dir_path)
    // }

    // // Helper function to clean up the temporary directory
    // fn cleanup_temp_dir(path: &PathBuf) {
    //     if path.exists() {
    //         let _ = fs::remove_dir_all(path);
    //     }
    // }

    // // Helper function to set up a test environment
    // fn setup_test_environment() -> (PathBuf, String) {
    //     // Create a temporary directory
    //     let (temp_dir, temp_path) = create_temp_dir();

    //     // Set the environment variable
    //     // TODO: Audit that the environment access only happens in single-threaded code.
    //     unsafe { env::set_var("AEP_CONFIGMAP_MOUNT_PATH", &temp_path) };

    //     (temp_dir, temp_path)
    // }

    // // Helper to create a file with contents
    // fn create_config_file(dir_path: &str, filename: &str, contents: &str) -> std::io::Result<()> {
    //     let file_path = format!("{dir_path}/{filename}");
    //     fs::write(file_path, contents)
    // }

    // #[test]
    // fn test_file_mount_successful_configuration() {
    //     let _file_dir_mutex = FILE_DIR_MTX.lock();
    //     let (temp_dir, temp_path) = setup_test_environment();

    //     create_config_file(
    //         &temp_path,
    //         "BROKER_TARGET_ADDRESS",
    //         "test.hostname.com:8883",
    //     )
    //     .unwrap();
    //     create_config_file(&temp_path, "BROKER_USE_TLS", "true").unwrap();
    //     create_config_file(&temp_path, "AIO_MQTT_CLIENT_ID", "test-client-id").unwrap();

    //     let builder_result = MqttConnectionSettingsBuilder::from_file_mount();
    //     assert!(builder_result.is_ok());

    //     let builder = builder_result.unwrap();
    //     assert_eq!(builder.hostname, Some("test.hostname.com".to_string()));
    //     assert_eq!(builder.tcp_port, Some(8883));
    //     assert_eq!(builder.use_tls, Some(true));
    //     assert_eq!(builder.client_id, Some("test-client-id".to_string()));

    //     let settings_result = builder.build();
    //     assert!(settings_result.is_ok());

    //     cleanup_temp_dir(&temp_dir);
    // }

    // #[test]
    // fn test_file_mount_missing_config_path() {
    //     let _file_dir_mutex = FILE_DIR_MTX.lock();
    //     // TODO: Audit that the environment access only happens in single-threaded code.
    //     unsafe { env::set_var("AEP_CONFIGMAP_MOUNT_PATH", "/path/that/does/not/exist") };

    //     let builder_result = MqttConnectionSettingsBuilder::from_file_mount();
    //     match builder_result {
    //         Ok(_) => panic!("Expected error"),
    //         Err(e) => {
    //             assert!(e.contains("Config map path does not exist"));
    //         }
    //     }
    // }

    // #[test]
    // fn test_file_mount_missing_env_var() {
    //     let _file_dir_mutex = FILE_DIR_MTX.lock();
    //     // TODO: Audit that the environment access only happens in single-threaded code.
    //     unsafe { env::remove_var("AEP_CONFIGMAP_MOUNT_PATH") };

    //     let builder_result = MqttConnectionSettingsBuilder::from_file_mount();
    //     match builder_result {
    //         Ok(_) => panic!("Expected error"),
    //         Err(e) => assert_eq!(e.to_string(), "AEP_CONFIGMAP_MOUNT_PATH is not set"),
    //     }
    // }

    // #[test]
    // fn test_file_mount_missing_target_address() {
    //     let _file_dir_mutex = FILE_DIR_MTX.lock();

    //     let (temp_dir, temp_path) = setup_test_environment();

    //     create_config_file(&temp_path, "BROKER_USE_TLS", "true").unwrap();
    //     create_config_file(&temp_path, "AIO_MQTT_CLIENT_ID", "test-client-id").unwrap();

    //     let builder_result = MqttConnectionSettingsBuilder::from_file_mount();

    //     match builder_result {
    //         Ok(_) => panic!("Expected error"),
    //         Err(e) => {
    //             assert!(e.contains("Failed to read BROKER_TARGET_ADDRESS"));
    //         }
    //     }
    //     cleanup_temp_dir(&temp_dir);
    // }

    // #[test]
    // fn test_file_mount_empty_target_address() {
    //     let _file_dir_mutex = FILE_DIR_MTX.lock();

    //     let (temp_dir, temp_path) = setup_test_environment();

    //     create_config_file(&temp_path, "BROKER_TARGET_ADDRESS", "").unwrap();
    //     create_config_file(&temp_path, "BROKER_USE_TLS", "true").unwrap();
    //     create_config_file(&temp_path, "AIO_MQTT_CLIENT_ID", "test-client-id").unwrap();

    //     let builder_result = MqttConnectionSettingsBuilder::from_file_mount();

    //     match builder_result {
    //         Ok(_) => panic!("Expected error"),
    //         Err(e) => {
    //             assert_eq!(e, "BROKER_TARGET_ADDRESS is missing.");
    //         }
    //     }
    //     cleanup_temp_dir(&temp_dir);
    // }

    // #[test]
    // fn test_file_mount_invalid_target_address_format() {
    //     let _file_dir_mutex = FILE_DIR_MTX.lock();
    //     let (temp_dir, temp_path) = setup_test_environment();

    //     create_config_file(&temp_path, "BROKER_TARGET_ADDRESS", "hostname-without-port").unwrap();
    //     create_config_file(&temp_path, "BROKER_USE_TLS", "true").unwrap();
    //     create_config_file(&temp_path, "AIO_MQTT_CLIENT_ID", "test-client-id").unwrap();

    //     let builder_result = MqttConnectionSettingsBuilder::from_file_mount();

    //     match builder_result {
    //         Ok(_) => panic!("Expected error"),
    //         Err(e) => {
    //             assert!(e.contains("BROKER_TARGET_ADDRESS is malformed"));
    //         }
    //     }
    //     cleanup_temp_dir(&temp_dir);
    // }

    // #[test]
    // fn test_file_mount_invalid_port() {
    //     let _file_dir_mutex = FILE_DIR_MTX.lock();
    //     let (temp_dir, temp_path) = setup_test_environment();

    //     create_config_file(
    //         &temp_path,
    //         "BROKER_TARGET_ADDRESS",
    //         "test.hostname.com:not_a_number",
    //     )
    //     .unwrap();
    //     create_config_file(&temp_path, "BROKER_USE_TLS", "true").unwrap();
    //     create_config_file(&temp_path, "AIO_MQTT_CLIENT_ID", "test-client-id").unwrap();

    //     let builder_result = MqttConnectionSettingsBuilder::from_file_mount();

    //     match builder_result {
    //         Ok(_) => panic!("Expected error"),
    //         Err(e) => {
    //             assert!(e.contains("Cannot parse MQTT port"));
    //         }
    //     }
    //     cleanup_temp_dir(&temp_dir);
    // }

    // #[test]
    // fn test_file_mount_missing_use_tls() {
    //     let _file_dir_mutex = FILE_DIR_MTX.lock();
    //     let (temp_dir, temp_path) = setup_test_environment();

    //     create_config_file(
    //         &temp_path,
    //         "BROKER_TARGET_ADDRESS",
    //         "test.hostname.com:8883",
    //     )
    //     .unwrap();
    //     create_config_file(&temp_path, "AIO_MQTT_CLIENT_ID", "test-client-id").unwrap();

    //     let builder_result = MqttConnectionSettingsBuilder::from_file_mount();
    //     match builder_result {
    //         Ok(_) => panic!("Expected error"),
    //         Err(e) => {
    //             assert!(e.contains("Failed to read BROKER_USE_TLS"));
    //         }
    //     }
    //     cleanup_temp_dir(&temp_dir);
    // }

    // #[test]
    // fn test_file_mount_invalid_use_tls() {
    //     let _file_dir_mutex = FILE_DIR_MTX.lock();
    //     let (temp_dir, temp_path) = setup_test_environment();

    //     create_config_file(
    //         &temp_path,
    //         "BROKER_TARGET_ADDRESS",
    //         "test.hostname.com:8883",
    //     )
    //     .unwrap();
    //     create_config_file(&temp_path, "BROKER_USE_TLS", "not-a-boolean").unwrap();
    //     create_config_file(&temp_path, "AIO_MQTT_CLIENT_ID", "test-client-id").unwrap();

    //     let builder_result = MqttConnectionSettingsBuilder::from_file_mount();

    //     match builder_result {
    //         Ok(_) => panic!("Expected error"),
    //         Err(e) => {
    //             assert!(e.contains(
    //                 "BROKER_USE_TLS contains a value that could not be parsed as a boolean"
    //             ));
    //         }
    //     }

    //     cleanup_temp_dir(&temp_dir);
    // }

    // #[test]
    // fn test_file_mount_missing_client_id() {
    //     let _file_dir_mutex = FILE_DIR_MTX.lock();
    //     let (temp_dir, temp_path) = setup_test_environment();

    //     create_config_file(
    //         &temp_path,
    //         "BROKER_TARGET_ADDRESS",
    //         "test.hostname.com:8883",
    //     )
    //     .unwrap();
    //     create_config_file(&temp_path, "BROKER_USE_TLS", "true").unwrap();

    //     let builder_result = MqttConnectionSettingsBuilder::from_file_mount();

    //     match builder_result {
    //         Ok(_) => panic!("Expected error"),
    //         Err(e) => {
    //             assert!(e.contains("Missing or malformed client ID configuration file"));
    //         }
    //     }
    //     cleanup_temp_dir(&temp_dir);
    // }

    // #[test]
    // fn test_file_mount_empty_client_id() {
    //     let _file_dir_mutex = FILE_DIR_MTX.lock();
    //     let (temp_dir, temp_path) = setup_test_environment();

    //     create_config_file(
    //         &temp_path,
    //         "BROKER_TARGET_ADDRESS",
    //         "test.hostname.com:8883",
    //     )
    //     .unwrap();
    //     create_config_file(&temp_path, "BROKER_USE_TLS", "true").unwrap();
    //     create_config_file(&temp_path, "AIO_MQTT_CLIENT_ID", "").unwrap();

    //     let builder_result = MqttConnectionSettingsBuilder::from_file_mount();

    //     match builder_result {
    //         Ok(_) => panic!("Expected error"),
    //         Err(e) => assert_eq!(e.to_string(), "AIO_MQTT_CLIENT_ID is missing."),
    //     }
    //     cleanup_temp_dir(&temp_dir);
    // }

    // #[test]
    // fn test_file_mount_with_optional_sat_file() {
    //     let _file_dir_mutex = FILE_DIR_MTX.lock();
    //     let (temp_dir, temp_path) = setup_test_environment();

    //     create_config_file(
    //         &temp_path,
    //         "BROKER_TARGET_ADDRESS",
    //         "test.hostname.com:8883",
    //     )
    //     .unwrap();
    //     create_config_file(&temp_path, "BROKER_USE_TLS", "true").unwrap();
    //     create_config_file(&temp_path, "AIO_MQTT_CLIENT_ID", "test-client-id").unwrap();

    //     // TODO: Audit that the environment access only happens in single-threaded code.
    //     unsafe { env::set_var("BROKER_SAT_MOUNT_PATH", "/path/to/sat/file.sat") };

    //     let builder_result = MqttConnectionSettingsBuilder::from_file_mount();
    //     assert!(builder_result.is_ok());

    //     let builder = builder_result.unwrap();
    //     assert_eq!(
    //         builder.sat_file,
    //         Some(Some("/path/to/sat/file.sat".to_string()))
    //     );
    //     let settings_result = builder.build();
    //     assert!(settings_result.is_ok());

    //     // TODO: Audit that the environment access only happens in single-threaded code.
    //     unsafe { env::remove_var("BROKER_SAT_MOUNT_PATH") };
    //     cleanup_temp_dir(&temp_dir);
    // }

    // #[test]
    // fn test_file_mount_with_ca_file_path() {
    //     let _file_dir_mutex = FILE_DIR_MTX.lock();
    //     let (temp_dir, temp_path) = setup_test_environment();

    //     create_config_file(
    //         &temp_path,
    //         "BROKER_TARGET_ADDRESS",
    //         "test.hostname.com:8883",
    //     )
    //     .unwrap();
    //     create_config_file(&temp_path, "BROKER_USE_TLS", "true").unwrap();
    //     create_config_file(&temp_path, "AIO_MQTT_CLIENT_ID", "test-client-id").unwrap();

    //     let ca_path = "/path/to/ca/certs";
    //     // TODO: Audit that the environment access only happens in single-threaded code.
    //     unsafe { env::set_var("BROKER_TLS_TRUST_BUNDLE_CACERT_MOUNT_PATH", ca_path) };

    //     let builder_result = MqttConnectionSettingsBuilder::from_file_mount();
    //     assert!(builder_result.is_ok());

    //     let builder = builder_result.unwrap();
    //     assert_eq!(builder.ca_file, Some(Some("/path/to/ca/certs".to_string())));
    //     let settings_result = builder.build();
    //     assert!(settings_result.is_ok());

    //     // TODO: Audit that the environment access only happens in single-threaded code.
    //     unsafe { env::remove_var("BROKER_TLS_TRUST_BUNDLE_CACERT_MOUNT_PATH") };
    //     cleanup_temp_dir(&temp_dir);
    // }

    // #[test]
    // fn test_file_mount_with_non_default_values() {
    //     let _file_dir_mutex = FILE_DIR_MTX.lock();
    //     let (temp_dir, temp_path) = setup_test_environment();

    //     create_config_file(&temp_path, "BROKER_TARGET_ADDRESS", "custom.host:1234").unwrap();
    //     create_config_file(&temp_path, "BROKER_USE_TLS", "false").unwrap();
    //     create_config_file(&temp_path, "AIO_MQTT_CLIENT_ID", "custom-client-id").unwrap();

    //     let builder_result = MqttConnectionSettingsBuilder::from_file_mount();
    //     assert!(builder_result.is_ok());

    //     let builder = builder_result.unwrap();
    //     assert_eq!(builder.hostname, Some("custom.host".to_string()));
    //     assert_eq!(builder.tcp_port, Some(1234));
    //     assert_eq!(builder.use_tls, Some(false));
    //     assert_eq!(builder.client_id, Some("custom-client-id".to_string()));

    //     let settings_result = builder.build();
    //     assert!(settings_result.is_ok());
    //     cleanup_temp_dir(&temp_dir);
    // }
}
