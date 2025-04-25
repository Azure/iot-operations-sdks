// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
#![allow(dead_code)]

//! Types for extracting Connector configurations from an Akri deployment

use std::env::{self, VarError};
use std::ffi::OsString;
use std::path::{Path, PathBuf};

use serde::Deserialize;
use serde_json;
use thiserror::Error;

use azure_iot_operations_mqtt;

/// Indicates an error ocurred while parsing the artifacts in an Akri deployment
#[derive(Error, Debug)]
#[error(transparent)]
pub struct DeploymentArtifactError(#[from] DeploymentArtifactErrorRepr);

/// Represents the type of error encountered while parsing artifacts in an Akri deployment
#[derive(Error, Debug)]
enum DeploymentArtifactErrorRepr {
    /// A required environment variable was not found
    #[error("Required environment variable missing: {0}")]
    EnvVarMissing(String),
    /// The value contained in an environment variable was malformed
    #[error("Environment variable value malformed: {0}")]
    EnvVarValueMalformed(String),
    /// A required mount path could not be found in the filesystem
    #[error("Required mount path not found: {0:?}")]
    MountPathMissing(OsString),
    /// A required file path could not be found in the filesystem
    #[error("Required file path not found: {0:?}")]
    FilePathMissing(OsString),
    /// An error ocurred while trying to read a file in the filesystem
    #[error(transparent)]
    FileReadError(#[from] std::io::Error),
    /// JSON data could not be parsed
    #[error(transparent)]
    JsonParseError(#[from] serde_json::Error),
}

/// Struct representing the Connector Configuration extracted from the Akir deployment
#[derive(Debug)]
pub struct ConnectorConfiguration {
    /// Prefix of an MQTT client ID
    pub client_id_prefix: String,

    // NOTE: The following three structs are the actual contents of the Connector Configuration
    // file mount.
    /// MQTT connection details
    pub mqtt_connection_configuration: MqttConnectionConfiguration,
    /// Azure IoT Operations metadata
    pub aio_metadata: AioMetadata,
    /// Diagnostics
    pub diagnostics: Diagnostics,

    // NOTE: the below mounts are combined here for convenience, although are technically different
    // mounts. This will change in the future as the specification is updated
    /// Path to directory containing CA cert trust bundle
    pub broker_ca_cert_trustbundle_path: Option<String>,
    /// Path to file containing SAT token
    pub broker_sat_path: Option<String>,
}

impl ConnectorConfiguration {
    /// Create a `ConnectorConfiguration` from the environment variables and filemounts in an Akri
    /// deployment
    ///
    /// # Errors
    /// - Returns a `DeploymentArtifactError` if there is an error with one of the artifacts in the
    ///   Akri deployment.
    pub fn new_from_deployment() -> Result<Self, DeploymentArtifactError> {
        let client_id_prefix = string_from_environment("CONNECTOR_CLIENT_ID_PREFIX")?.ok_or(
            DeploymentArtifactErrorRepr::EnvVarMissing("CONNECTOR_CLIENT_ID_PREFIX".to_string()),
        )?;
        // MQTT Connection Configuration Filemount
        let cc_mount_pathbuf = PathBuf::from(
            string_from_environment("CONNECTOR_CONFIGURATION_MOUNT_PATH")?.ok_or(
                DeploymentArtifactErrorRepr::EnvVarMissing(
                    "CONNECTOR_CONFIGURATION_MOUNT_PATH".to_string(),
                ),
            )?,
        );
        if !cc_mount_pathbuf.as_path().exists() {
            return Err(DeploymentArtifactErrorRepr::MountPathMissing(
                cc_mount_pathbuf.into_os_string(),
            ))?;
        }
        let mqtt_connection_configuration =
            Self::extract_mqtt_connection_configuration(cc_mount_pathbuf.as_path())?;
        let aio_metadata = Self::extract_aio_metadata(&cc_mount_pathbuf)?;
        let diagnostics = Self::extract_diagnostics(&cc_mount_pathbuf)?;

        let broker_ca_cert_trustbundle_path =
            string_from_environment("BROKER_TLS_TRUST_BUNDLE_CACERT_MOUNT_PATH")?;
        let broker_sat_path = string_from_environment("BROKER_SAT_MOUNT_PATH")?;

        Ok(ConnectorConfiguration {
            client_id_prefix,
            mqtt_connection_configuration,
            aio_metadata,
            diagnostics,
            broker_ca_cert_trustbundle_path,
            broker_sat_path,
        })
    }

    fn extract_mqtt_connection_configuration(
        mount_path: &Path,
    ) -> Result<MqttConnectionConfiguration, DeploymentArtifactErrorRepr> {
        let mqtt_conn_config_pathbuf = mount_path.join("MQTT_CONNECTION_CONFIGURATION");
        if !mqtt_conn_config_pathbuf.as_path().exists() {
            return Err(DeploymentArtifactErrorRepr::FilePathMissing(
                mqtt_conn_config_pathbuf.into_os_string(),
            ))?;
        }
        // NOTE: Manual file read to memory is more efficient than using serde_json::from_reader()
        let m: MqttConnectionConfiguration =
            serde_json::from_str(&std::fs::read_to_string(&mqtt_conn_config_pathbuf)?)?;
        Ok(m)
    }

    // TODO: is this optional?
    fn extract_aio_metadata(mount_path: &Path) -> Result<AioMetadata, DeploymentArtifactErrorRepr> {
        let aio_metadata_pathbuf = mount_path.join("AIO_METADATA");
        if !aio_metadata_pathbuf.as_path().exists() {
            return Err(DeploymentArtifactErrorRepr::FilePathMissing(
                aio_metadata_pathbuf.into_os_string(),
            ))?;
        }
        let a: AioMetadata =
            serde_json::from_str(&std::fs::read_to_string(&aio_metadata_pathbuf)?)?;
        Ok(a)
    }

    fn extract_diagnostics(mount_path: &Path) -> Result<Diagnostics, DeploymentArtifactErrorRepr> {
        let diagnostics_pathbuf = mount_path.join("DIAGNOSTICS");
        if !diagnostics_pathbuf.as_path().exists() {
            return Err(DeploymentArtifactErrorRepr::FilePathMissing(
                diagnostics_pathbuf.into_os_string(),
            ))?;
        }
        let d: Diagnostics = serde_json::from_str(&std::fs::read_to_string(&diagnostics_pathbuf)?)?;
        Ok(d)
    }
}

impl TryFrom<ConnectorConfiguration> for azure_iot_operations_mqtt::MqttConnectionSettings {
    type Error = String;

    fn try_from(_value: ConnectorConfiguration) -> Result<Self, Self::Error> {
        unimplemented!()
    }
}

/// Configuration details related to an MQTT connection
#[derive(Debug, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct MqttConnectionConfiguration {
    /// Broker host in the format <hostname>:<port>
    pub host: String,
    /// Number of seconds to keep a connection to the broker alive for
    pub keep_alive_seconds: u16,
    /// Maximum number of messages that can be assigned a packet ID
    pub max_inflight_messages: u16,
    /// The type of MQTT connection being used
    pub protocol: Protocol,
    /// Number of seconds to keep a session with the broker alive for
    pub session_expiry_seconds: u32,
    /// TLS configuration
    pub tls: Tls,
}

/// Enum representing the type of MQTT connection
#[derive(Debug, Deserialize)]
#[serde(rename_all = "lowercase")]
pub enum Protocol {
    /// Regular MQTT
    Mqtt,
}

/// TLS configuration information
#[derive(Debug, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct Tls {
    /// Indicates if TLS is enabled or not
    pub mode: TlsMode,
}

/// Enum representing whether TLS is enabled or disabled
#[derive(Debug, Deserialize)]
pub enum TlsMode {
    /// TLS is enabled
    Enabled,
    /// TLS is disabled
    Disabled,
}

/// Metadata regaridng Azure IoT Operations
#[derive(Debug, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct AioMetadata {
    // TODO: implement regex parsing
    /// Minimum supported AIO version
    pub aio_min_version: String,
    /// Maximum supported AIO version
    pub aio_max_version: String,
}

/// Diagnostic information
#[derive(Debug, Deserialize)]
pub struct Diagnostics {
    /// Log information
    pub logs: Logs,
}

/// Logging information
#[derive(Debug, Deserialize)]
pub struct Logs {
    /// Level to log at
    pub level: LogLevel,
}

/// Represents the logging level
#[derive(Debug, Deserialize)]
#[serde(rename_all = "lowercase")]
pub enum LogLevel {
    /// Info logging
    Info,
    /// Debug logging
    Debug,
    /// Warn logging
    Warn,
    /// Error logging
    Error,
    /// Trace logging
    Trace,
}

/// Helper function to get an environment variable as a string.
fn string_from_environment(key: &str) -> Result<Option<String>, DeploymentArtifactError> {
    match env::var(key) {
        Ok(value) => Ok(Some(value)),
        Err(VarError::NotPresent) => Ok(None),
        Err(VarError::NotUnicode(_)) => Err(DeploymentArtifactErrorRepr::EnvVarValueMalformed(
            key.to_string(),
        ))?,
    }
}

// #[cfg(test)]
// mod tests {
//     use super::*;
//     use test_case::test_case;

//     const FAKE_SAT_FILE: &str = "/path/to/sat/file";

//     fn get_dummy_file_directory() -> PathBuf {
//         let mut path = PathBuf::from(env!("CARGO_MANIFEST_DIR"));
//         // TODO: make this platform independent
//         path.push("../../eng/test/test-connector-mount-files");
//         path
//     }

//     fn get_connector_config_mount_path() -> PathBuf {
//         let mut path = get_dummy_file_directory();
//         path.push("connector-config");
//         path
//     }

//     fn get_trust_bundle_mount_path() -> PathBuf {
//         let mut path = get_dummy_file_directory();
//         path.push("trust-bundle");
//         path
//     }

//     #[test]
//     fn all_artifacts() {
//         let cc_mount_path = get_connector_config_mount_path();
//         let trust_bundle_mount_path = get_trust_bundle_mount_path();
//         temp_env::with_vars(
//             [
//                 ("CONNECTOR_CLIENT_ID_PREFIX", Some("test-client-id")),
//                 (
//                     "CONNECTOR_CONFIGURATION_MOUNT_PATH",
//                     Some(cc_mount_path.to_str().unwrap()),
//                 ),
//                 (
//                     "BROKER_TLS_TRUST_BUNDLE_CACERT_MOUNT_PATH",
//                     Some(trust_bundle_mount_path.to_str().unwrap()),
//                 ),
//                 ("BROKER_SAT_MOUNT_PATH", Some(FAKE_SAT_FILE)),
//             ],
//             || {
//                 let cc = ConnectorConfiguration::new_from_deployment();
//                 assert!(cc.is_ok());
//             },
//         )
//     }

//     #[test]
//     fn only_required_artifacts() {
//         let cc_mount_path = get_connector_config_mount_path();
//         temp_env::with_vars(
//             [
//                 ("CONNECTOR_CLIENT_ID_PREFIX", Some("test-client-id")),
//                 (
//                     "CONNECTOR_CONFIGURATION_MOUNT_PATH",
//                     Some(cc_mount_path.to_str().unwrap()),
//                 ),
//                 ("BROKER_TLS_TRUST_BUNDLE_CACERT_MOUNT_PATH", None),
//                 ("BROKER_SAT_MOUNT_PATH", None),
//             ],
//             || {
//                 let cc = ConnectorConfiguration::new_from_deployment();
//                 assert!(cc.is_ok());
//             },
//         )
//     }

//     #[test_case(("CONNECTOR_CLIENT_ID_PREFIX", None); "CONNECTOR_CLIENT_ID_PREFIX")]
//     #[test_case(("CONNECTOR_CONFIGURATION_MOUNT_PATH", None); "CONNECTOR_CONFIGURATION_MOUNT_PATH")]
//     fn missing_required_env_var(env_var_kv: (&str, Option<&str>)) {
//         let cc_mount_path = get_connector_config_mount_path();
//         temp_env::with_vars(
//             [
//                 ("CONNECTOR_CLIENT_ID_PREFIX", Some("test-client-id")),
//                 (
//                     "CONNECTOR_CONFIGURATION_MOUNT_PATH",
//                     Some(cc_mount_path.to_str().unwrap()),
//                 ),
//                 ("BROKER_TLS_TRUST_BUNDLE_CACERT_MOUNT_PATH", None),
//                 ("BROKER_SAT_MOUNT_PATH", None),
//                 env_var_kv,
//             ],
//             || {
//                 let cc = ConnectorConfiguration::new_from_deployment();
//                 assert!(cc.is_err());
//             },
//         )
//     }
// }
