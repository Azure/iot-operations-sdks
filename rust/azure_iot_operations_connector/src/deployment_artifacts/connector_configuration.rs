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
use super::watched::Watched;

const MQTT_CONNECTION_CONFIGURATION_FILENAME: &str = "MQTT_CONNECTION_CONFIGURATION";
const DIAGNOSTICS_FILENAME: &str = "DIAGNOSTICS";
const ADDITIONAL_CONNECTOR_CONFIGURATION_FILENAME: &str = "ADDITIONAL_CONNECTOR_CONFIGURATION";
const PERSISTENT_VOLUME_MOUNT_PATHS_FILENAME: &str = "PERSISTENT_VOLUME_MOUNT_PATH";

// TODO: Validate that this is okay to not support Debug and PartialEq

/// The Connector Configuration extracted from the Akri deployment
#[derive(Clone)]
pub struct ConnectorConfiguration {
    /// MQTT connection details
    pub mqtt_connection_configuration: Watched<MqttConnectionConfiguration>,
    // TODO: Expand to Watched<Option<Diagnostics>> once requirements are clarified
    /// Diagnostics
    pub diagnostics: Option<Diagnostics>,           // TODO: WILL CHANGE
    // TODO: Expand to Watched<Vec<PathBuf>> once requirements are clarified
    /// Persistent Volume Mount Path
    pub persistent_volumes: Vec<PathBuf>, // WILL NOT CHANGE!
    // TODO: Expand to Watched<Option<String>> once requirements are clarified
    /// Additional connector configuration as a JSON string
    pub additional_configuration: Option<String>, // CAN CHANGE
}

impl ConnectorConfiguration {
    /// Create a `ConnectorConfiguration` from the files in the specified mount path.
    ///
    /// Sets up an [`AtomicWriterVolumeDebouncer`] to monitor the mount path for changes
    /// and push updates to the [`Watched`] fields.
    pub(crate) fn new_from_mount_path(
        mount_path: PathBuf,
    ) -> Result<Self, DeploymentArtifactErrorRepr> {

        // TODO: Validate this commment.
        // NOTE: Handling errors here does end up requiring unnecessary allocations in the
        // FilePathMissing errors returned on optional files, but this is not (yet) code that will
        // be run often, and it keeps things simpler and easier to pivot as the spec evolves.
        // When finalized, consider optimizing so the individual helpers have logic to handle
        // optional files.

        let initial_mqtt_config = extract_mqtt_connection_configuration(
            &mount_path.join(MQTT_CONNECTION_CONFIGURATION_FILENAME),
        )?;
        let diagnostics = match extract_diagnostics(
            &mount_path.join(DIAGNOSTICS_FILENAME),
        ) {
            Ok(d) => Some(d),
            Err(DeploymentArtifactErrorRepr::FilePathMissing(_)) => None,
            Err(e) => Err(e)?,
        };
        let persistent_volumes = match extract_persistent_volumes(
            &mount_path.join(PERSISTENT_VOLUME_MOUNT_PATHS_FILENAME),
        ) {
            Err(DeploymentArtifactErrorRepr::FilePathMissing(_)) => vec![],
            res => res?,
        };
        let additional_configuration = match extract_additional_configuration(
            &mount_path.join(ADDITIONAL_CONNECTOR_CONFIGURATION_FILENAME),
        ) {
            Ok(ac) => Some(ac),
            Err(DeploymentArtifactErrorRepr::FilePathMissing(_)) => None,
            Err(e) => Err(e)?,
        };

        let (mqtt_tx, mqtt_rx) = tokio::sync::watch::channel(initial_mqtt_config);

        let debouncer = AtomicWriterVolumeDebouncer::new(
            mount_path,
            move |res: AtomicWriterVolumeEventResult| {
                match res {
                    Ok(events) => {
                        for event in &events {
                            // NOTE: ConfigMap filenames must be UTF-8, thus can safely convert to &str
                            let Some(file_name) = event.path.file_name().and_then(|f| f.to_str()) else {
                                continue;
                            };

                            match event.kind {
                                AtomicWriterVolumeEventKind::FileModified
                                | AtomicWriterVolumeEventKind::FileCreated => {

                                    match file_name {
                                        MQTT_CONNECTION_CONFIGURATION_FILENAME => {
                                            match extract_mqtt_connection_configuration(&event.path) {
                                                Ok(new_config) => {
                                                    let _ = mqtt_tx.send(new_config);
                                                }
                                                Err(e) => {
                                                    log::error!(
                                                        "Failed to parse updated {MQTT_CONNECTION_CONFIGURATION_FILENAME}: {e}"
                                                    );
                                                }
                                            }
                                        }
                                        DIAGNOSTICS_FILENAME => {
                                            unimplemented!("stuff");
                                        }
                                        ADDITIONAL_CONNECTOR_CONFIGURATION_FILENAME | PERSISTENT_VOLUME_MOUNT_PATHS_FILENAME => {
                                            // log::info!(
                                            //     "Change detected in {file_name.to_string_lossy()} - currently not supported to update at runtime, ignoring"
                                            // );
                                        }
                                        _ => {
                                            // log::warn!(
                                            //     "Unexpected file {file_name.to_string_lossy()} was modified or created in connector configuration mount - ignoring"
                                            // );
                                        }
                                    }

                                }
                                AtomicWriterVolumeEventKind::FileRemoved => {
                                    log::warn!(
                                        "Required file {MQTT_CONNECTION_CONFIGURATION_FILENAME} was removed — ignoring"
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
            mqtt_connection_configuration: Watched::new(mqtt_rx, Some(Arc::new(debouncer))),
            diagnostics,
            persistent_volumes,
            additional_configuration,
        })
    }
}

/// Extract an `MqttConnectionConfiguration` from the specified file path.
fn extract_mqtt_connection_configuration(
    file_path: &Path,
) -> Result<MqttConnectionConfiguration, DeploymentArtifactErrorRepr> {
    if !file_path.exists() {
        return Err(DeploymentArtifactErrorRepr::FilePathMissing(
            file_path.into(),
        ))?;
    }
    let m: MqttConnectionConfiguration =
        serde_json::from_str(&std::fs::read_to_string(file_path)?)?;
    Ok(m)
}

/// Extract a `Diagnostics` struct from the specified file path.
fn extract_diagnostics(
    file_path: &Path,
) -> Result<Diagnostics, DeploymentArtifactErrorRepr> {
    if !file_path.exists() {
        return Err(DeploymentArtifactErrorRepr::FilePathMissing(
            file_path.into(),
        ))?;
    }
    let d: Diagnostics = serde_json::from_str(&std::fs::read_to_string(file_path)?)?;
    Ok(d)
}

/// Extract a list of persistent volume paths from the specified file path.
fn extract_persistent_volumes(
    file_path: &Path,
) -> Result<Vec<PathBuf>, DeploymentArtifactErrorRepr> {
    if !file_path.exists() {
        return Err(DeploymentArtifactErrorRepr::FilePathMissing(
            file_path.into(),
        ))?;
    }
    let persistent_volumes = BufReader::new(File::open(file_path)?)
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

/// Extract additional configuration JSON string from the specified file path.
fn extract_additional_configuration(
    file_path: &Path,
) -> Result<String, DeploymentArtifactErrorRepr> {
    if !file_path.exists() {
        return Err(DeploymentArtifactErrorRepr::FilePathMissing(
            file_path.into(),
        ))?;
    }
    let additional_config = std::fs::read_to_string(file_path)?;
    Ok(additional_config)
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

// TOOD: where should the filepath validation be? Here or in connector artifacts?

#[cfg(test)]
mod tests {
    use test_case::{test_case, test_matrix};
    use std::time::Duration;
    use super::super::test_utils::{TempAtomicWriterVolume, TempPersistentVolumeManager};
    use super::super::atomic_writer_volume_debouncer::{TICK_RATE, DEBOUNCE_WINDOW};
    use super::*;

    // Worst case: DEBOUNCE_WINDOW + TICK_RATE (jitter) + margin
    const _: () = assert!(
        DEBOUNCE_WINDOW.as_millis()
            + TICK_RATE.as_millis()
            + 100 // Margin
        <= u64::MAX as u128,
        "UPDATE_WINDOW computation would truncate"
    );
    #[allow(clippy::cast_possible_truncation)] // Safety: const assertion above proves no truncation
    const UPDATE_WINDOW: Duration = Duration::from_millis(
        DEBOUNCE_WINDOW.as_millis() as u64
            + TICK_RATE.as_millis() as u64
            + 100 // Margin
    );

    const MQTT_CONNECTION_CONFIGURATION_JSON: &str = r#"
    {
        "host": "someHostName:1234",
        "keepAliveSeconds": 60,
        "maxInflightMessages": 100,
        "protocol": "mqtt",
        "sessionExpirySeconds": 3600,
        "tls": {
            "mode": "Enabled"
        }
    }"#;

    const DIAGNOSTICS_JSON: &str = r#"
    {
        "logs": {
            "level": "info"
        }
    }"#;

    const ADDITIONAL_CONNECTOR_CONFIGURATION_JSON: &str = r#"
    {
        "arbitraryConnectorDeveloperConfiguration": "value"
    }"#;

    const ARBITRARY_JSON: &str = r#"
    {
        "arbitraryKey": "arbitraryValue"
    }"#;

    const NOT_JSON: &str = "this is not json";

    #[test]
    fn instantiation_all_values() {
        let mut persistent_volume_manager = TempPersistentVolumeManager::new();
        persistent_volume_manager.add_mount("persistent_volume_1");
        persistent_volume_manager.add_mount("persistent_volume_2");

        let config_mount = TempAtomicWriterVolume::new("connector_config");
        config_mount.stage_file_create(
            Path::new(MQTT_CONNECTION_CONFIGURATION_FILENAME),
            MQTT_CONNECTION_CONFIGURATION_JSON,
        );
        config_mount.stage_file_create(Path::new(DIAGNOSTICS_FILENAME), DIAGNOSTICS_JSON);
        config_mount.stage_file_create(
            Path::new(PERSISTENT_VOLUME_MOUNT_PATHS_FILENAME),
            &persistent_volume_manager.index_file_contents(),
        );
        config_mount.stage_file_create(
            Path::new(ADDITIONAL_CONNECTOR_CONFIGURATION_FILENAME),
            ADDITIONAL_CONNECTOR_CONFIGURATION_JSON,
        );
        config_mount.execute_update();

        let config = ConnectorConfiguration::new_from_mount_path(config_mount.path().to_path_buf())
            .expect("Failed to create ConnectorConfiguration from mount path");

        assert_eq!(
            *config.mqtt_connection_configuration.borrow(),
            serde_json::from_str::<MqttConnectionConfiguration>(MQTT_CONNECTION_CONFIGURATION_JSON)
                .expect("Failed to parse MQTT_CONNECTION_CONFIGURATION_JSON")
        );
        assert_eq!(
            config.diagnostics,
            Some(
                serde_json::from_str::<Diagnostics>(DIAGNOSTICS_JSON)
                    .expect("Failed to parse DIAGNOSTICS_JSON")
            )
        );
        assert_eq!(
            config.persistent_volumes,
            persistent_volume_manager.volume_path_bufs()
        );
        assert_eq!(
            config.additional_configuration,
            Some(ADDITIONAL_CONNECTOR_CONFIGURATION_JSON.to_string())
        );
    }

    #[test]
    fn instantiation_required_values_only() {
        let config_mount = TempAtomicWriterVolume::new("connector_config");
        config_mount.stage_file_create(Path::new(MQTT_CONNECTION_CONFIGURATION_FILENAME), MQTT_CONNECTION_CONFIGURATION_JSON);
        config_mount.execute_update();

        let config = ConnectorConfiguration::new_from_mount_path(config_mount.path().to_path_buf())
            .expect("Failed to create ConnectorConfiguration from mount path");

        assert_eq!(
            *config.mqtt_connection_configuration.borrow(),
            serde_json::from_str::<MqttConnectionConfiguration>(MQTT_CONNECTION_CONFIGURATION_JSON)
                .expect("Failed to parse MQTT_CONNECTION_CONFIGURATION_JSON")
        );
        assert_eq!(config.diagnostics, None);
        assert_eq!(config.persistent_volumes, Vec::<PathBuf>::new());
        assert_eq!(config.additional_configuration, None);
    }

    #[test_case("MQTT_CONNECTION_CONFIGURATION")]
    fn instantiation_error_missing_required_file(required_file: &str) {
        let config_mount = TempAtomicWriterVolume::new("connector_config");
        config_mount.stage_file_create(Path::new(MQTT_CONNECTION_CONFIGURATION_FILENAME), MQTT_CONNECTION_CONFIGURATION_JSON);

        // Stage the removal of the required file
        config_mount.stage_file_remove(Path::new(required_file));

        config_mount.execute_update();

        assert!(ConnectorConfiguration::new_from_mount_path(config_mount.path().to_path_buf()).is_err());
    }

    #[test]
    fn instantiation_error_nonexistent_persistent_volume_path() {
        let nonexistent_mount_path = PathBuf::from("nonexistent/persisent/volume/mount");
        assert!(!nonexistent_mount_path.exists());

        let config_mount = TempAtomicWriterVolume::new("connector_config");
        config_mount.stage_file_create(Path::new(MQTT_CONNECTION_CONFIGURATION_FILENAME), MQTT_CONNECTION_CONFIGURATION_JSON);
        config_mount.stage_file_create(Path::new(PERSISTENT_VOLUME_MOUNT_PATHS_FILENAME), nonexistent_mount_path.to_str().unwrap());
        config_mount.execute_update();

        assert!(ConnectorConfiguration::new_from_mount_path(config_mount.path().to_path_buf()).is_err());
    }

    #[test_matrix(
        [MQTT_CONNECTION_CONFIGURATION_FILENAME, DIAGNOSTICS_FILENAME],
        [NOT_JSON, ARBITRARY_JSON]
    )]
    fn instantiation_error_invalid_json_content(target_file: &str, invalid_content: &str) {
        let config_mount = TempAtomicWriterVolume::new("connector_config");
        config_mount.stage_file_create(Path::new(MQTT_CONNECTION_CONFIGURATION_FILENAME), MQTT_CONNECTION_CONFIGURATION_JSON);
        config_mount.stage_file_create(Path::new(DIAGNOSTICS_FILENAME), DIAGNOSTICS_JSON);

        // Override one of the above files with the invalid content
        config_mount.stage_file_remove(Path::new(target_file));
        config_mount.stage_file_create(Path::new(target_file), invalid_content);

        config_mount.execute_update();

        assert!(ConnectorConfiguration::new_from_mount_path(config_mount.path().to_path_buf()).is_err());
    }

    #[tokio::test]
    async fn mqtt_configuration_modify() {
        let config_mount = TempAtomicWriterVolume::new("connector_config");
        config_mount.stage_file_create(Path::new(MQTT_CONNECTION_CONFIGURATION_FILENAME), MQTT_CONNECTION_CONFIGURATION_JSON);
        config_mount.execute_update();

        let mut config = ConnectorConfiguration::new_from_mount_path(config_mount.path().to_path_buf())
            .expect("Failed to create ConnectorConfiguration from mount path");
        assert_eq!(
            *config.mqtt_connection_configuration.borrow(),
            serde_json::from_str::<MqttConnectionConfiguration>(MQTT_CONNECTION_CONFIGURATION_JSON)
                .expect("Failed to parse MQTT_CONNECTION_CONFIGURATION_JSON")
        );


        let new_mqtt_config_json = r#"
        {
            "host": "someOtherHostName:5678",
            "keepAliveSeconds": 120,
            "maxInflightMessages": 50,
            "protocol": "mqtt",
            "sessionExpirySeconds": 7200,
            "tls": {
                "mode": "Disabled"
            }
        }"#;
        assert!(new_mqtt_config_json != MQTT_CONNECTION_CONFIGURATION_JSON);

        config_mount.stage_file_remove(Path::new(MQTT_CONNECTION_CONFIGURATION_FILENAME));
        config_mount.stage_file_create(Path::new(MQTT_CONNECTION_CONFIGURATION_FILENAME), new_mqtt_config_json);
        config_mount.execute_update();

        tokio::time::timeout(
            UPDATE_WINDOW,
            config.mqtt_connection_configuration.changed(),
        )
        .await
        .expect("Timed out waiting for MQTT config change")
        .expect("Failed to receive change notification");

        assert_eq!(
            *config.mqtt_connection_configuration.borrow(),
            serde_json::from_str::<MqttConnectionConfiguration>(new_mqtt_config_json)
                .expect("Failed to parse new MQTT connection configuration JSON")
        );
    }

    // This is an invalid scenario - it should never happen, but we test the expected behavior if it were to.
    #[tokio::test]
    async fn mqtt_configuration_modify_invalid_json() {
        let config_mount = TempAtomicWriterVolume::new("connector_config");
        config_mount.stage_file_create(Path::new(MQTT_CONNECTION_CONFIGURATION_FILENAME), MQTT_CONNECTION_CONFIGURATION_JSON);
        config_mount.execute_update();

        let mut config = ConnectorConfiguration::new_from_mount_path(config_mount.path().to_path_buf())
            .expect("Failed to create ConnectorConfiguration from mount path");
        assert_eq!(
            *config.mqtt_connection_configuration.borrow(),
            serde_json::from_str::<MqttConnectionConfiguration>(MQTT_CONNECTION_CONFIGURATION_JSON)
                .expect("Failed to parse MQTT_CONNECTION_CONFIGURATION_JSON")
        );

        config_mount.stage_file_remove(Path::new(MQTT_CONNECTION_CONFIGURATION_FILENAME));
        config_mount.stage_file_create(Path::new(MQTT_CONNECTION_CONFIGURATION_FILENAME), NOT_JSON);
        config_mount.execute_update();

        // Expect that the invalid JSON update is ignored and the old value remains
        tokio::time::timeout(
            UPDATE_WINDOW,
            config.mqtt_connection_configuration.changed(),
        )
        .await
        .expect_err("Unexpectedly received change notification for invalid MQTT config update");

        assert_eq!(
            *config.mqtt_connection_configuration.borrow(),
            serde_json::from_str::<MqttConnectionConfiguration>(MQTT_CONNECTION_CONFIGURATION_JSON)
                .expect("Failed to parse MQTT_CONNECTION_CONFIGURATION_JSON")
        );
    }

    // This is an invalid scenario - it should never happen, but we test the expected behavior if it were to.
    #[tokio::test]
    async fn mqtt_configuration_remove() {
        let config_mount = TempAtomicWriterVolume::new("connector_config");
        config_mount.stage_file_create(Path::new(MQTT_CONNECTION_CONFIGURATION_FILENAME), MQTT_CONNECTION_CONFIGURATION_JSON);
        config_mount.execute_update();

        let mut config = ConnectorConfiguration::new_from_mount_path(config_mount.path().to_path_buf())
            .expect("Failed to create ConnectorConfiguration from mount path");
        assert_eq!(
            *config.mqtt_connection_configuration.borrow(),
            serde_json::from_str::<MqttConnectionConfiguration>(MQTT_CONNECTION_CONFIGURATION_JSON)
                .expect("Failed to parse MQTT_CONNECTION_CONFIGURATION_JSON")
        );

        config_mount.stage_file_remove(Path::new(MQTT_CONNECTION_CONFIGURATION_FILENAME));
        config_mount.execute_update();

        // Expect that the removal of the required file is ignored and the old value remains
        tokio::time::timeout(
            UPDATE_WINDOW,
            config.mqtt_connection_configuration.changed(),
        )
        .await
        .expect_err("Unexpectedly received change notification for MQTT config file removal");

        assert_eq!(
            *config.mqtt_connection_configuration.borrow(),
            serde_json::from_str::<MqttConnectionConfiguration>(MQTT_CONNECTION_CONFIGURATION_JSON)
                .expect("Failed to parse MQTT_CONNECTION_CONFIGURATION_JSON")
        );
    }

}
