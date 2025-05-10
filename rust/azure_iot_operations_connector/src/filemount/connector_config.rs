// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
#![allow(dead_code)]

//! Types for extracting Connector configurations from an Akri deployment

use std::env::{self, VarError};
use std::ffi::OsString;
use std::path::{Path, PathBuf};
use std::time::Duration;

use serde::Deserialize;
use serde_json;
use thiserror::Error;

use azure_iot_operations_mqtt as aio_mqtt;

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
    /// A specified mount path could not be found in the filesystem
    #[error("Specified mount path not found in filesystem: {0:?}")]
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
#[derive(Debug, Clone)]
pub struct ConnectorConfiguration {
    /// Prefix of an MQTT client ID
    pub client_id_prefix: String,

    // NOTE: The following three structs are the actual contents of the Connector Configuration
    // file mount.
    /// MQTT connection details
    pub mqtt_connection_configuration: MqttConnectionConfiguration,
    /// Azure IoT Operations metadata
    pub aio_metadata: Option<AioMetadata>, // TODO: Not option
    /// Diagnostics
    pub diagnostics: Option<Diagnostics>, // TODO: not option

    // NOTE: the below mounts are combined here for convenience, although are technically different
    // mounts. This will change in the future as the specification is updated
    /// Path to directory containing CA cert trust bundle
    pub broker_ca_cert_trustbundle_path: Option<PathBuf>,
    /// Path to file containing SAT token
    // TODO: Should be PathBuf but this will have testing implications
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
        // TODO: re-enable this functionality when spec is finalized
        //let aio_metadata = Self::extract_aio_metadata(&cc_mount_pathbuf)?;
        //let diagnostics = Self::extract_diagnostics(&cc_mount_pathbuf)?;
        let aio_metadata = None;
        let diagnostics = None;

        let broker_ca_cert_trustbundle_path =
            string_from_environment("BROKER_TLS_TRUST_BUNDLE_CACERT_MOUNT_PATH")?
                .map(PathBuf::from);
        if let Some(tb_path) = &broker_ca_cert_trustbundle_path {
            if !tb_path.as_path().exists() {
                return Err(DeploymentArtifactErrorRepr::MountPathMissing(
                    // TODO: This clone is almost certainly unnecessary. Fix.
                    tb_path.clone().into_os_string(),
                ))?;
            }
        }

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

    // TODO: Need a wrapper function that will make the suffix automatic
    /// Converts the value to an [`azure_iot_operations_mqtt::MqttConnectionSettings`] struct,
    /// given a suffix for the client ID.
    ///
    /// # Errors
    /// Returns a string indicating the cause of the error
    pub fn to_mqtt_connection_settings(
        &self,
        client_id_suffix: &str,
    ) -> Result<aio_mqtt::MqttConnectionSettings, String> {
        let client_id = self.client_id_prefix.clone() + client_id_suffix;
        let host_c = self.mqtt_connection_configuration.host.clone();
        let (hostname, tcp_port) = host_c.split_once(':').ok_or(format!(
            "'host' malformed. Expected format <hostname>:<port>. Found: {host_c}"
        ))?;
        let tcp_port = tcp_port
            .parse::<u16>()
            .map_err(|_| format!("Cannot parse 'tcp_port' into u16. Value: {tcp_port}"))?;
        let keep_alive =
            Duration::from_secs(self.mqtt_connection_configuration.keep_alive_seconds.into());
        let receive_max = self.mqtt_connection_configuration.max_inflight_messages;
        let session_expiry = Duration::from_secs(
            self.mqtt_connection_configuration
                .session_expiry_seconds
                .into(),
        );
        let use_tls = matches!(
            self.mqtt_connection_configuration.tls.mode,
            TlsMode::Enabled
        );
        let sat_file = self.broker_sat_path.clone();

        // NOTE: MQTT SDK only accepts a single cert, while the Akri deployment can have multiple.
        // Verify there is only a single CA cert in the path, and then we will pass the path to
        // that FILE into the MqttConnectionSettings
        let ca_file = {
            if let Some(ca_trustbundle_path) = &self.broker_ca_cert_trustbundle_path {
                let mut d = std::fs::read_dir(ca_trustbundle_path)
                    .map_err(|e| format!("Could not read trustbundle directory: {e}"))?;
                let entry = d
                    .next()
                    .ok_or("No CA cert found in trustbundle directory".to_string())?
                    .map_err(|e| format!("Could not read trustbundle directory: {e}"))?;
                if d.next().is_some() {
                    Err("MQTTConnectionSettings only supports a single CA cert".to_string())?
                } else {
                    // Convert filepath to string for MqttConnectionSettings
                    let path_s = entry
                        .path()
                        .to_str()
                        .ok_or("Could not convert Path to String".to_string())?
                        .to_string();
                    Some(path_s)
                }
            } else {
                None
            }
        };

        let c = aio_mqtt::MqttConnectionSettingsBuilder::default()
            .client_id(client_id)
            .hostname(hostname)
            .tcp_port(tcp_port)
            .keep_alive(keep_alive)
            .receive_max(receive_max)
            .session_expiry(session_expiry)
            .use_tls(use_tls)
            .ca_file(ca_file)
            .sat_file(sat_file)
            .build()
            .map_err(|e| format!("{e}"))?;
        Ok(c)
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

    fn extract_aio_metadata(mount_path: &Path) -> Result<AioMetadata, DeploymentArtifactErrorRepr> {
        let aio_metadata_pathbuf = mount_path.join("AIO_METADATA");
        if !aio_metadata_pathbuf.as_path().exists() {
            return Err(DeploymentArtifactErrorRepr::FilePathMissing(
                aio_metadata_pathbuf.into_os_string(),
            ))?;
        }
        // NOTE: Manual file read to memory is more efficient than using serde_json::from_reader()
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
        // NOTE: Manual file read to memory is more efficient than using serde_json::from_reader()
        let d: Diagnostics = serde_json::from_str(&std::fs::read_to_string(&diagnostics_pathbuf)?)?;
        Ok(d)
    }
}

/// Configuration details related to an MQTT connection
#[derive(Debug, Deserialize, Clone)]
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
#[derive(Debug, Deserialize, Clone)]
#[serde(rename_all = "lowercase")]
pub enum Protocol {
    /// Regular MQTT
    Mqtt,
}

/// TLS configuration information
#[derive(Debug, Deserialize, Clone)]
#[serde(rename_all = "camelCase")]
pub struct Tls {
    /// Indicates if TLS is enabled or not
    pub mode: TlsMode,
}

/// Enum representing whether TLS is enabled or disabled
#[derive(Debug, Deserialize, Clone)]
pub enum TlsMode {
    /// TLS is enabled
    Enabled,
    /// TLS is disabled
    Disabled,
}

/// Metadata regaridng Azure IoT Operations
#[derive(Debug, Deserialize, Clone)]
#[serde(rename_all = "camelCase")]
pub struct AioMetadata {
    // TODO: implement regex parsing
    /// Minimum supported AIO version
    pub aio_min_version: String,
    /// Maximum supported AIO version
    pub aio_max_version: String,
}

/// Diagnostic information
#[derive(Debug, Deserialize, Clone)]
pub struct Diagnostics {
    /// Log information
    pub log_level: Logs, // TODO: change to match spec when fixed
                         //pub logs: Logs,
}

/// Logging information
#[derive(Debug, Deserialize, Clone)]
pub struct Logs {
    /// Level to log at
    pub level: LogLevel,
}

/// Represents the logging level
#[derive(Debug, Deserialize, Clone)]
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
