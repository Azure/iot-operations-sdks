// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

//! Types for extracting Connector Configuration from a ConfigMap volume mount in an Akri
//! deployment.

use std::fs::File;
use std::io::{BufRead, BufReader};
use std::path::{Path, PathBuf};
use std::sync::Arc;

use serde::Deserialize;

use super::atomic_writer_volume_debouncer::{
    AtomicWriterVolumeDebouncer, AtomicWriterVolumeEventKind, AtomicWriterVolumeEventResult,
};
use super::connector::DeploymentArtifactErrorRepr;
use super::watched::{Watched, watched_pair};


const MQTT_CONNECTION_CONFIGURATION_FILE: &str = "MQTT_CONNECTION_CONFIGURATION";

// TODO: Validat that this is okay to not support Debug and PartialEq
// TODO: Debouner logic, make sure it stays alive as long as there's a reference to a Watched that uses it

/// The Connector Configuration extracted from the Akri deployment
#[derive(Clone)]
pub struct ConnectorConfiguration {
    /// MQTT connection details
    pub mqtt_connection_configuration: Watched<MqttConnectionConfiguration>,
    // TODO: Expand to Watched<Option<Diagnostics>> once requirements are clarified
    /// Diagnostics
    pub diagnostics: Option<Diagnostics>,
    // TODO: Expand to Watched<Vec<PathBuf>> once requirements are clarified
    /// Persistent Volume Mount Path
    pub persistent_volumes: Vec<PathBuf>,
    // TODO: Expand to Watched<Option<String>> once requirements are clarified
    /// Additional connector configuration as a JSON string
    pub additional_configuration: Option<String>,
    /// Debouncer that monitors the `ConfigMap` mount for changes.
    /// Must be kept alive for the lifetime of this struct.
    _debouncer: Arc<AtomicWriterVolumeDebouncer>,
}

impl ConnectorConfiguration {
    /// Create a `ConnectorConfiguration` from the files in the specified mount path.
    ///
    /// Sets up an [`AtomicWriterVolumeDebouncer`] to monitor the mount path for changes
    /// and push updates to the [`Watched`] fields.
    pub(crate) fn new_from_mount_path(
        mount_path: PathBuf,
    ) -> Result<Self, DeploymentArtifactErrorRepr> {
        // NOTE: Handling errors here does end up requiring unnecessary allocations in the
        // FilePathMissing errors returned on optional files, but this is not (yet) code that will
        // be run often, and it keeps things simpler and easier to pivot as the spec evolves.
        // When finalized, consider optimizing so the individual helpers have logic to handle
        // optional files.

        let initial_mqtt_config =
            Self::extract_mqtt_connection_configuration(&mount_path)?;
        let diagnostics = match Self::extract_diagnostics(&mount_path) {
            Ok(d) => Some(d),
            Err(DeploymentArtifactErrorRepr::FilePathMissing(_)) => None,
            Err(e) => Err(e)?,
        };
        let persistent_volumes = match Self::extract_persistent_volumes(&mount_path) {
            Err(DeploymentArtifactErrorRepr::FilePathMissing(_)) => vec![],
            res => res?,
        };
        let additional_configuration = match Self::extract_additional_configuration(&mount_path) {
            Ok(ac) => Some(ac),
            Err(DeploymentArtifactErrorRepr::FilePathMissing(_)) => None,
            Err(e) => Err(e)?,
        };

        let (mqtt_tx, mqtt_rx) = watched_pair(initial_mqtt_config);
        let mount_path_for_debouncer = mount_path.clone();

        let debouncer = AtomicWriterVolumeDebouncer::new(
            mount_path,
            move |res: AtomicWriterVolumeEventResult| {
                match res {
                    Ok(events) => {
                        for event in &events {
                            let Some(file_name) = event.path.file_name() else {
                                continue;
                            };
                            if file_name != MQTT_CONNECTION_CONFIGURATION_FILE {
                                // TODO: Handle other configuration files when they use Watched<T>
                                continue;
                            }

                            match event.kind {
                                AtomicWriterVolumeEventKind::FileModified
                                | AtomicWriterVolumeEventKind::FileCreated => {
                                    let config_path = mount_path_for_debouncer
                                        .join(MQTT_CONNECTION_CONFIGURATION_FILE);
                                    match std::fs::read_to_string(&config_path)
                                        .map_err(|e| e.to_string())
                                        .and_then(|contents| {
                                            serde_json::from_str::<MqttConnectionConfiguration>(
                                                &contents,
                                            )
                                            .map_err(|e| e.to_string())
                                        }) {
                                        Ok(new_config) => {
                                            mqtt_tx.send(new_config);
                                        }
                                        Err(e) => {
                                            log::error!(
                                                "Failed to parse updated {MQTT_CONNECTION_CONFIGURATION_FILE}: {e}"
                                            );
                                        }
                                    }
                                }
                                AtomicWriterVolumeEventKind::FileRemoved => {
                                    log::warn!(
                                        "Required file {MQTT_CONNECTION_CONFIGURATION_FILE} was removed — ignoring"
                                    );
                                }
                                // Directory events are not expected for this file
                                _ => {}
                            }
                        }
                    }
                    Err(e) => {
                        log::error!("Error monitoring connector configuration mount: {e:?}");
                    }
                }
            },
        )?;

        Ok(Self {
            mqtt_connection_configuration: mqtt_rx,
            diagnostics,
            persistent_volumes,
            additional_configuration,
            _debouncer: Arc::new(debouncer),
        })
    }

    /// Extract an `MqttConnectionConfiguration` from the specified mount path
    fn extract_mqtt_connection_configuration(
        mount_path: &Path,
    ) -> Result<MqttConnectionConfiguration, DeploymentArtifactErrorRepr> {
        let mqtt_conn_config_pathbuf = mount_path.join("MQTT_CONNECTION_CONFIGURATION");
        if !mqtt_conn_config_pathbuf.exists() {
            return Err(DeploymentArtifactErrorRepr::FilePathMissing(
                mqtt_conn_config_pathbuf.into(),
            ))?;
        }
        // NOTE: Manual file read to memory is more efficient than using serde_json::from_reader()
        let m: MqttConnectionConfiguration =
            serde_json::from_str(&std::fs::read_to_string(&mqtt_conn_config_pathbuf)?)?;
        Ok(m)
    }

    /// Extract a `Diagnostics` struct from the specified mount path
    fn extract_diagnostics(mount_path: &Path) -> Result<Diagnostics, DeploymentArtifactErrorRepr> {
        let diagnostics_pathbuf = mount_path.join("DIAGNOSTICS");
        if !diagnostics_pathbuf.exists() {
            return Err(DeploymentArtifactErrorRepr::FilePathMissing(
                diagnostics_pathbuf.into(),
            ))?;
        }
        // NOTE: Manual file read to memory is more efficient than using serde_json::from_reader()
        let d: Diagnostics = serde_json::from_str(&std::fs::read_to_string(&diagnostics_pathbuf)?)?;
        Ok(d)
    }

    /// Extract a list of persistent volumes paths from the specified mount path
    fn extract_persistent_volumes(
        mount_path: &Path,
    ) -> Result<Vec<PathBuf>, DeploymentArtifactErrorRepr> {
        let persistent_volumes_pathbuf = mount_path.join("PERSISTENT_VOLUME_MOUNT_PATH");
        if !persistent_volumes_pathbuf.exists() {
            return Err(DeploymentArtifactErrorRepr::FilePathMissing(
                persistent_volumes_pathbuf.into(),
            ))?;
        }
        // NOTE: Use a BufReader to reduce allocations
        let persistent_volumes = BufReader::new(File::open(&persistent_volumes_pathbuf)?)
            .lines()
            .map_while(Result::ok)
            .map(PathBuf::from)
            .try_fold(vec![], |mut acc, pv_pathbuf| {
                if !pv_pathbuf.exists() {
                    return Err(DeploymentArtifactErrorRepr::MountPathMissing(
                        pv_pathbuf.into_os_string(),
                    ));
                }
                acc.push(pv_pathbuf);
                Ok(acc)
            })?;
        Ok(persistent_volumes)
    }

    /// Extract additional configuration JSON string from the specified mount path
    fn extract_additional_configuration(
        mount_path: &Path,
    ) -> Result<String, DeploymentArtifactErrorRepr> {
        let additional_config_pathbuf = mount_path.join("ADDITIONAL_CONNECTOR_CONFIGURATION");
        if !additional_config_pathbuf.exists() {
            return Err(DeploymentArtifactErrorRepr::FilePathMissing(
                additional_config_pathbuf.into_os_string(),
            ))?;
        }
        let additional_config = std::fs::read_to_string(&additional_config_pathbuf)?;
        Ok(additional_config)
    }
}

/// Configuration details related to an MQTT connection
#[derive(Debug, Deserialize, Clone, PartialEq)]
#[serde(rename_all = "camelCase")]
pub struct MqttConnectionConfiguration {
    /// Broker host in the format `<hostname>:<port>`
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
#[derive(Debug, Deserialize, Clone, PartialEq)]
pub enum Protocol {
    /// Regular MQTT
    #[serde(alias = "mqtt")]
    Mqtt,
}

/// TLS configuration information
#[derive(Debug, Deserialize, Clone, PartialEq)]
#[serde(rename_all = "camelCase")]
pub struct Tls {
    /// Indicates if TLS is enabled or not
    pub mode: TlsMode,
}

/// Enum representing whether TLS is enabled or disabled
#[derive(Debug, Deserialize, Clone, PartialEq)]
pub enum TlsMode {
    /// TLS is enabled
    Enabled,
    /// TLS is disabled
    Disabled,
}

/// Diagnostic information
#[derive(Debug, Deserialize, Clone, PartialEq)]
pub struct Diagnostics {
    /// Log information
    pub logs: Logs,
}

/// Logging information
#[derive(Debug, Deserialize, Clone, PartialEq)]
pub struct Logs {
    /// The log level. Examples - 'debug', 'info', 'warn', 'error', 'trace'.
    pub level: String,
}
