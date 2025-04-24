// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

//! Types for extracting Connector configurations from an Akri deployment

use std::env::{self, VarError};
use std::path::{Path, PathBuf};
use std::fs::File;
use std::io::BufReader;
use std::ffi::OsString;

use serde::Deserialize;
use serde_json;
use thiserror::Error;

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
pub struct ConnectorConfiguration {
    pub client_id_prefix: String,

    // NOTE: The following three structs are the actual contents of the Connector Configuration
    // file mount.
    pub mqtt_connection_configuration: MqttConnectionConfiguration,
    pub aio_metadata: AioMetadata,
    pub diagnostics: Diagnostics,

    // NOTE: the below mounts are combined here for convenience, although are technically different
    // mounts. This will change in the future as the specification is updated

    /// Path to directory containing CA cert trust bundle
    pub broker_ca_cert_trustbundle_path: String,
    /// Path to file containing SAT token
    pub broker_sat_path: String,
}

impl ConnectorConfiguration {
    /// Create a `ConnectorConfiguration` from the environment variables and filemounts in an Akri
    /// deployment
    pub fn new_from_deployment() -> Result<Self, DeploymentArtifactError> {
        let client_id_prefix = string_from_environment("CONNECTOR_CLIENT_ID_PREFIX")?
            .ok_or(DeploymentArtifactErrorRepr::EnvVarMissing("CONNECTOR_CLIENT_ID_PREFIX".to_string()))?;
        // MQTT Connection Configuration Filemount
        let cc_mount_pathbuf = PathBuf::from(string_from_environment("CONNECTOR_CONFIGURATION_MOUNT_PATH")?
            .ok_or(DeploymentArtifactErrorRepr::EnvVarMissing("CONNECTOR_CONFIGURATION_MOUNT_PATH".to_string()))?);
        if !cc_mount_pathbuf.as_path().exists() {
            return Err(DeploymentArtifactErrorRepr::MountPathMissing(cc_mount_pathbuf.into_os_string()))?;
        }
        let mqtt_connection_configuration = Self::extract_mqtt_connection_configuration(cc_mount_pathbuf.as_path())?;
        let aio_metadata = Self::extract_aio_metadata(&cc_mount_pathbuf)?;
        let diagnostics = Self::extract_diagnostics(&cc_mount_pathbuf)?;

        let broker_ca_cert_trustbundle_path = string_from_environment("BROKER_TLS_TRUST_BUNDLE_CACERT_MOUNT_PATH")?
            .ok_or(DeploymentArtifactErrorRepr::EnvVarMissing("BROKER_TLS_TRUST_BUNDLE_CACERT_MOUNT_PATH".to_string()))?;

        let broker_sat_path = string_from_environment("BROKER_SAT_MOUNT_PATH")?
            .ok_or(DeploymentArtifactErrorRepr::EnvVarMissing("BROKER_SAT_MOUNT_PATH".to_string()))?;

        Ok(ConnectorConfiguration {
            client_id_prefix,
            mqtt_connection_configuration,
            aio_metadata,
            diagnostics,
            broker_ca_cert_trustbundle_path,
            broker_sat_path,

        })
    }

    fn extract_mqtt_connection_configuration(mount_path: &Path) -> Result<MqttConnectionConfiguration, DeploymentArtifactErrorRepr> {
        let mqtt_conn_config_pathbuf = mount_path.join("MQTT_CONNECTION_CONFIGURATION");
        if !mqtt_conn_config_pathbuf.as_path().exists() {
            return Err(DeploymentArtifactErrorRepr::FilePathMissing(mqtt_conn_config_pathbuf.into_os_string()))?;
        }
        let m: MqttConnectionConfiguration = serde_json::from_reader(BufReader::new(File::open(mqtt_conn_config_pathbuf)?))?;
        Ok(m)
    }

    fn extract_aio_metadata(_mount_path: &Path) -> Result<AioMetadata, DeploymentArtifactErrorRepr> {
        // TODO: Implement
        Ok(AioMetadata {})
    }

    fn extract_diagnostics(_mount_path: &Path) -> Result<Diagnostics, DeploymentArtifactErrorRepr> {
        // TODO: Implement
        Ok(Diagnostics {})
    }
}

#[derive(Deserialize)]
pub struct MqttConnectionConfiguration {

    host: String,
    keep_alive_seconds: u16,
    max_inflight_messages: u16,
    protocol: Protocol,
    session_expiry_seconds: u32,
    tls: Tls,
}

pub struct AioMetadata {
    //aio_min_version:
    //aio_max_version:
}

pub struct Diagnostics {}

#[derive(Deserialize)]
pub struct Tls {
    pub mode: TlsMode,
}

#[derive(Deserialize)]
pub enum Protocol {
    MQTT
}

#[derive(Deserialize)]
pub enum TlsMode {
    Enabled,
    Disabled,
}

/// Helper function to get an environment variable as a string.
fn string_from_environment(key: &str) -> Result<Option<String>, DeploymentArtifactError> {
    match env::var(key) {
        Ok(value) => Ok(Some(value)),
        Err(VarError::NotPresent) => Ok(None),
        Err(VarError::NotUnicode(_)) => {
            Err(DeploymentArtifactErrorRepr::EnvVarValueMalformed(key.to_string()))?
        }
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    fn get_dummy_file_directory() -> PathBuf {
        let mut path = PathBuf::from(env!("CARGO_MANIFEST_DIR"));
        path.push("../../eng/test/test-connector-mount-files");
        path
    }

    #[test]
    fn full() {
        temp_env::with_vars(
            ["CONNECTOR_CLIENT_ID_PREFIX", Some("test-client-id")]
        )
        let c = ConnectorConfiguration::new_from_deployment();

    }

}