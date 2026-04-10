// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

//! Types for extracting Connector configurations from an Akri deployment

use std::env::VarError;
use std::ffi::OsString;
use std::path::PathBuf;
use std::time::Duration;

use azure_iot_operations_mqtt as aio_mqtt;
use serde_json;
use thiserror::Error;

use crate::deployment_artifacts::{FileMount, Secrets};
// TODO: not sure if all this needs exposing here, revisit.
// These should probably be exported in the root of `deployment_artifacts` instead,
// but that should be done at the end of the `deployment_artifacts` feature changes.
pub use super::connector_configuration::{
    ConnectorConfiguration, Diagnostics, Logs, MqttConnectionConfiguration, Protocol, Tls, TlsMode,
};

const AGGREGATION_WINDOW: Duration = Duration::from_secs(10);

/// Indicates an error occurred while parsing the artifacts in an Akri deployment
#[derive(Error, Debug)]
#[error(transparent)]
pub struct DeploymentArtifactError(#[from] DeploymentArtifactErrorRepr);

/// Represents the type of error encountered while parsing artifacts in an Akri deployment
#[derive(Error, Debug)]
pub(crate) enum DeploymentArtifactErrorRepr {
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
    /// An error occurred while trying to read a file in the filesystem
    #[error(transparent)]
    FileReadError(#[from] std::io::Error),
    /// JSON data could not be parsed
    #[error(transparent)]
    JsonParseError(#[from] serde_json::Error),
    /// Could not monitor `FileMount` for changes
    #[error("Error initializing FileMount monitor: {0}")]
    MonitorError(#[from] super::filemount::Error),
    /// Could not set up Secrets
    #[error("Error initializing Secrets: {0}")]
    SecretsError(#[from] super::secrets::Error),
    /// Could not set up volume debouncer
    #[error("Error initializing configuration volume debouncer: {0}")]  // TODO: should this be wrapped?
    DebouncerError(#[from] super::atomic_writer_volume_debouncer::AtomicWriterVolumeError),
}

// TODO: Integrate ADR into this implementation

#[derive(Clone)]
/// Values extracted from the artifacts in an Akri deployment.
pub struct ConnectorArtifacts {
    /// The Azure extension resource ID
    pub azure_extension_resource_id: String,
    /// The connector ID
    pub connector_id: String,
    /// The connector namespace
    pub connector_namespace: String,
    /// The connector configuration
    pub connector_configuration: ConnectorConfiguration,
    /// The secrets deployed for the connector
    pub connector_secrets: Option<Secrets>,
    /// Path to directory containing trust list certificates for the connector
    pub connector_trust_settings_mount: Option<FileMount>, // TODO: dir (projected)
    /// Path to directory containing trust bundle for the broker
    pub broker_trust_bundle_mount: Option<FileMount>, // TODO: dir (projected)
    /// Path to file containing service account token for authentication with the broker
    pub broker_sat_mount: Option<FileMount>, // TODO: dir (projected)
    /// Path to directory containing trust bundle for device inbound endpoints
    pub device_endpoint_trust_bundle_mount: Option<FileMount>, // TODO: dir (unknown)
    /// Path to directory containing credentials for device inbound endpoints
    pub device_endpoint_credentials_mount: Option<FileMount>, // TODO: dir (unknown)

    // TODO: The following are stopgap variables - these will change in the future
    /// OTEL grpc/grpcs metric endpoint.
    pub grpc_metric_endpoint: Option<String>,
    /// OTEL grpc/grpcs log endpoint.
    pub grpc_log_endpoint: Option<String>,
    /// OTEL grpc/grpcs trace endpoint.
    pub grpc_trace_endpoint: Option<String>,
    /// Path to the directory containing trust bundle for 1P grpc metric collector.
    pub grpc_metric_collector_1p_ca_mount: Option<FileMount>, // TODO: file
    /// Path to the directory containing trust bundle for 1P grpc log collector.
    pub grpc_log_collector_1p_ca_mount: Option<FileMount>, // TODO: file
    /// OTEL http/https metric endpoint.
    pub http_metric_endpoint: Option<String>,
    /// OTEL http/https log endpoint.
    pub http_log_endpoint: Option<String>,
    /// OTEL http/https trace endpoint.
    pub http_trace_endpoint: Option<String>,
}

impl ConnectorArtifacts {
    /// Create a `ConnectorArtifacts` from the environment variables and filemounts in an Akri
    /// deployment
    ///
    /// # Errors
    /// - Returns a `DeploymentArtifactError` if there is an error with one of the artifacts in the
    ///   Akri deployment.
    pub fn new_from_deployment() -> Result<Self, DeploymentArtifactError> {
        // Azure Extension Resource ID
        let azure_extension_resource_id = string_from_environment("AZURE_EXTENSION_RESOURCEID")?
            .ok_or(DeploymentArtifactErrorRepr::EnvVarMissing(
                "AZURE_EXTENSION_RESOURCEID".to_string(),
            ))?;
        // Connector ID
        let connector_id = string_from_environment("CONNECTOR_ID")?.ok_or(
            DeploymentArtifactErrorRepr::EnvVarMissing("CONNECTOR_ID".to_string()),
        )?;

        // Connector Namespace
        let connector_namespace = string_from_environment("CONNECTOR_NAMESPACE")?.ok_or(
            DeploymentArtifactErrorRepr::EnvVarMissing("CONNECTOR_NAMESPACE".to_string()),
        )?;

        // Connector Configuration
        let connector_configuration = ConnectorConfiguration::new_from_mount_path(
            string_from_environment("CONNECTOR_CONFIGURATION_MOUNT_PATH")?
                .map(valid_pathbuf_from)
                .transpose()?
                .ok_or(DeploymentArtifactErrorRepr::EnvVarMissing(
                    "CONNECTOR_CONFIGURATION_MOUNT_PATH".to_string(),
                ))?,
        )?;

        // Connector Secrets
        let connector_secrets = string_from_environment("CONNECTOR_SECRETS_METADATA_MOUNT_PATH")?
            .map(valid_pathbuf_from)
            .transpose()?
            .zip(
                string_from_environment("CONNECTOR_SECRETS_MOUNT_PATH")?
                    .map(valid_pathbuf_from)
                    .transpose()?,
            )
            .map(|(metadata_path, secrets_path)| Secrets::new(metadata_path, secrets_path))
            .transpose()
            .map_err(DeploymentArtifactErrorRepr::SecretsError)?;

        // Connector Trust Settings Mount Path
        let connector_trust_settings_mount =
            string_from_environment("CONNECTOR_TRUST_SETTINGS_MOUNT_PATH")?
                .map(valid_filemount_from)
                .transpose()?;

        // Broker TLS trust bundle CA cert mount path
        let broker_trust_bundle_mount =
            string_from_environment("BROKER_TLS_TRUST_BUNDLE_CACERT_MOUNT_PATH")?
                .map(valid_filemount_from)
                .transpose()?;

        // Broker SAT token mount path
        let broker_sat_mount = string_from_environment("BROKER_SAT_MOUNT_PATH")?
            .map(valid_filemount_from)
            .transpose()?;

        // Device Endpoint TLS Trust Bundle CA cert mount path
        let device_endpoint_trust_bundle_mount =
            string_from_environment("DEVICE_ENDPOINT_TLS_TRUST_BUNDLE_CA_CERT_MOUNT_PATH")?
                .map(valid_filemount_from)
                .transpose()?;

        // Device Endpoint Credentials mount path
        let device_endpoint_credentials_mount =
            string_from_environment("DEVICE_ENDPOINT_CREDENTIALS_MOUNT_PATH")?
                .map(valid_filemount_from)
                .transpose()?;

        // TODO: Validate that mutually required fields are present/absent in tandem.
        // Wait for spec updates to finalize logic.

        // Stopgap variables beyond this point

        let grpc_metric_endpoint = string_from_environment("OTLP_GRPC_METRIC_ENDPOINT")?;
        let grpc_log_endpoint = string_from_environment("OTLP_GRPC_LOG_ENDPOINT")?;
        let grpc_trace_endpoint = string_from_environment("OTLP_GRPC_TRACE_ENDPOINT")?;

        let grpc_metric_collector_1p_ca_mount =
            string_from_environment("FIRST_PARTY_OTLP_GRPC_METRICS_COLLECTOR_CA_PATH")?
                .map(valid_filemount_from)
                .transpose()?;
        let grpc_log_collector_1p_ca_mount =
            string_from_environment("FIRST_PARTY_OTLP_GRPC_LOG_COLLECTOR_CA_PATH")?
                .map(valid_filemount_from)
                .transpose()?;

        let http_metric_endpoint = string_from_environment("OTLP_HTTP_METRIC_ENDPOINT")?;
        let http_log_endpoint = string_from_environment("OTLP_HTTP_LOG_ENDPOINT")?;
        let http_trace_endpoint = string_from_environment("OTLP_HTTP_TRACE_ENDPOINT")?;

        Ok(ConnectorArtifacts {
            azure_extension_resource_id,
            connector_id,
            connector_namespace,
            connector_configuration,
            connector_secrets,
            connector_trust_settings_mount,
            broker_trust_bundle_mount,
            broker_sat_mount,
            device_endpoint_trust_bundle_mount,
            device_endpoint_credentials_mount,
            grpc_metric_endpoint,
            grpc_log_endpoint,
            grpc_trace_endpoint,
            grpc_metric_collector_1p_ca_mount,
            grpc_log_collector_1p_ca_mount,
            http_metric_endpoint,
            http_log_endpoint,
            http_trace_endpoint,
        })
    }

    /// Creates an [`azure_iot_operations_mqtt::aio::connection_settings::MqttConnectionSettings`] struct given a suffix for
    /// the client ID.
    ///
    /// # Errors
    /// Returns a string indicating the cause of the error
    pub fn to_mqtt_connection_settings(
        &self,
        client_id_suffix: &str,
    ) -> Result<aio_mqtt::aio::connection_settings::MqttConnectionSettings, String> {
        let client_id = self.connector_id.clone() + client_id_suffix;
        let (host_c, keep_alive, receive_max, session_expiry, use_tls) = {
            let mc = self
                .connector_configuration
                .mqtt_connection_configuration
                .borrow();
            (
                mc.host.clone(),
                Duration::from_secs(mc.keep_alive_seconds.into()),
                mc.max_inflight_messages,
                Duration::from_secs(mc.session_expiry_seconds.into()),
                matches!(mc.tls.mode, TlsMode::Enabled),
            )
        };
        let (hostname, tcp_port) = host_c.split_once(':').ok_or(format!(
            "'host' malformed. Expected format <hostname>:<port>. Found: {host_c}"
        ))?;
        let tcp_port = tcp_port
            .parse::<u16>()
            .map_err(|_| format!("Cannot parse 'tcp_port' into u16. Value: {tcp_port}"))?;
        let sat_file = self
            .broker_sat_mount
            .as_ref()
            .map(|p| {
                p.as_path()
                    .to_str()
                    .ok_or_else(|| "Cannot convert SAT file path to String".to_string())
                    .map(std::borrow::ToOwned::to_owned)
            })
            .transpose()?;

        // NOTE: MQTT SDK only accepts a single cert, while the Akri deployment can have multiple.
        // Verify there is only a single CA cert in the path, and then we will pass the path to
        // that FILE into the MqttConnectionSettings
        let ca_file = {
            if let Some(ca_trustbundle_path) = &self.broker_trust_bundle_mount {
                let mut d = std::fs::read_dir(ca_trustbundle_path)
                    .map_err(|e| format!("Could not read trustbundle directory: {e}"))?;
                let path_s;
                loop {
                    let entry = d
                        .next()
                        .ok_or("No CA cert found in trustbundle directory".to_string())?
                        .map_err(|e| format!("Could not read trustbundle directory: {e}"))?;
                    // TODO:  Workaround to skip files that start with .. that aren't ca files.
                    if entry
                        .file_name()
                        .to_string_lossy()
                        .to_string()
                        .starts_with("..")
                    {
                        continue;
                    }
                    // Convert filepath to string for MqttConnectionSettings
                    path_s = entry
                        .path()
                        .to_str()
                        .ok_or("Could not convert Path to String".to_string())?
                        .to_string();
                    break;
                }
                Some(path_s)
            } else {
                None
            }
        };

        let c = aio_mqtt::aio::connection_settings::MqttConnectionSettingsBuilder::default()
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
}

/// Helper function to get an environment variable as a string.
fn string_from_environment(key: &str) -> Result<Option<String>, DeploymentArtifactErrorRepr> {
    match std::env::var(key) {
        Ok(value) => Ok(Some(value)),
        Err(VarError::NotPresent) => Ok(None),
        Err(VarError::NotUnicode(_)) => Err(DeploymentArtifactErrorRepr::EnvVarValueMalformed(
            key.to_string(),
        )),
    }
}

/// Helper function to validate a mount path and return it as a `PathBuf`
fn valid_pathbuf_from(mount_path_s: String) -> Result<PathBuf, DeploymentArtifactErrorRepr> {
    let mount_path = PathBuf::from(mount_path_s);
    if !mount_path.exists() {
        return Err(DeploymentArtifactErrorRepr::MountPathMissing(
            mount_path.into(),
        ));
    }
    Ok(mount_path)
}

/// Helper function to validate a mount path and return it as a `FileMount`.
fn valid_filemount_from(mount_path_s: String) -> Result<FileMount, DeploymentArtifactErrorRepr> {
    Ok(FileMount::new(
        valid_pathbuf_from(mount_path_s)?,
        AGGREGATION_WINDOW,
    )?)
}

#[cfg(test)]
mod tests {
    use super::super::test_utils::{TempAtomicWriterVolume, TempMount, TempPersistentVolumeManager};
    use super::*;
    use std::path::Path;
    use tempfile::NamedTempFile;
    use test_case::{test_case, test_matrix};

    // Environment variable constants
    const AZURE_EXTENSION_RESOURCE_ID: &str = "/subscriptions/extension/resource/id";
    const CONNECTOR_ID: &str = "connector_id";
    const CONNECTOR_NAMESPACE: &str = "connector_namespace";

    // Stopgap env var constants
    const GRPC_METRIC_ENDPOINT: &str = "grpcs://metric.endpoint";
    const GRPC_LOG_ENDPOINT: &str = "grpcs://log.endpoint";
    const GRPC_TRACE_ENDPOINT: &str = "grpcs://trace.endpoint";
    const HTTP_METRIC_ENDPOINT: &str = "https://metric.endpoint";
    const HTTP_LOG_ENDPOINT: &str = "https://log.endpoint";
    const HTTP_TRACE_ENDPOINT: &str = "https://trace.endpoint";

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
    fn minimum_artifacts() {
        let connector_configuration_mount = TempAtomicWriterVolume::new("connector_configuration");
        connector_configuration_mount
            .stage_file_create(Path::new("MQTT_CONNECTION_CONFIGURATION"), MQTT_CONNECTION_CONFIGURATION_JSON);
        connector_configuration_mount.execute_update();

        temp_env::with_vars(
            [
                (
                    "AZURE_EXTENSION_RESOURCEID",
                    Some(AZURE_EXTENSION_RESOURCE_ID),
                ),
                ("CONNECTOR_ID", Some(CONNECTOR_ID)),
                ("CONNECTOR_NAMESPACE", Some(CONNECTOR_NAMESPACE)),
                (
                    "CONNECTOR_CONFIGURATION_MOUNT_PATH",
                    Some(connector_configuration_mount.path().to_str().unwrap()),
                ),
                ("CONNECTOR_SECRETS_METADATA_MOUNT_PATH", None),
                ("CONNECTOR_TRUST_SETTINGS_MOUNT_PATH", None),
                ("BROKER_TLS_TRUST_BUNDLE_CACERT_MOUNT_PATH", None),
                ("BROKER_SAT_MOUNT_PATH", None),
                ("DEVICE_ENDPOINT_TLS_TRUST_BUNDLE_CA_CERT_MOUNT_PATH", None),
                ("DEVICE_ENDPOINT_CREDENTIALS_MOUNT_PATH", None),
                // Stopgap variables beyond this point
                ("OTLP_GRPC_METRIC_ENDPOINT", None),
                ("OTLP_GRPC_LOG_ENDPOINT", None),
                ("OTLP_GRPC_TRACE_ENDPOINT", None),
                ("FIRST_PARTY_OTLP_GRPC_METRICS_COLLECTOR_CA_PATH", None),
                ("FIRST_PARTY_OTLP_GRPC_LOG_COLLECTOR_CA_PATH", None),
                ("OTLP_HTTP_METRIC_ENDPOINT", None),
                ("OTLP_HTTP_LOG_ENDPOINT", None),
                ("OTLP_HTTP_TRACE_ENDPOINT", None),
            ],
            || {
                let artifacts = ConnectorArtifacts::new_from_deployment().unwrap();
                // -- Validate the values directly in the artifacts --
                assert_eq!(
                    artifacts.azure_extension_resource_id,
                    AZURE_EXTENSION_RESOURCE_ID
                );
                assert_eq!(artifacts.connector_id, CONNECTOR_ID);
                assert_eq!(artifacts.connector_namespace, CONNECTOR_NAMESPACE);
                assert!(artifacts.connector_secrets.is_none());
                assert!(artifacts.connector_trust_settings_mount.is_none());
                assert!(artifacts.broker_trust_bundle_mount.is_none());
                assert!(artifacts.broker_sat_mount.is_none());
                assert!(artifacts.device_endpoint_trust_bundle_mount.is_none());
                assert!(artifacts.device_endpoint_credentials_mount.is_none());

                // -- Validate the ConnectorConfiguration from the ConnectorArtifacts --
                assert_eq!(
                    *artifacts
                        .connector_configuration
                        .mqtt_connection_configuration
                        .borrow(),
                    serde_json::from_str::<MqttConnectionConfiguration>(
                        MQTT_CONNECTION_CONFIGURATION_JSON
                    )
                    .unwrap()
                );
                assert!(artifacts.connector_configuration.diagnostics.is_none());
                assert_eq!(
                    artifacts.connector_configuration.persistent_volumes,
                    Vec::<PathBuf>::new()
                );
                assert!(
                    artifacts
                        .connector_configuration
                        .additional_configuration
                        .is_none()
                );

                // -- Validate the stopgap variables in the ConnectorArtifacts --
                assert!(artifacts.grpc_metric_endpoint.is_none());
                assert!(artifacts.grpc_log_endpoint.is_none());
                assert!(artifacts.grpc_trace_endpoint.is_none());
                assert!(artifacts.grpc_metric_collector_1p_ca_mount.is_none());
                assert!(artifacts.grpc_log_collector_1p_ca_mount.is_none());
                assert!(artifacts.http_metric_endpoint.is_none());
                assert!(artifacts.http_log_endpoint.is_none());
                assert!(artifacts.http_trace_endpoint.is_none());
            },
        );
    }

    #[test]
    fn maximum_artifacts() {
        let mut persistent_volume_manager = TempPersistentVolumeManager::new();
        persistent_volume_manager.add_mount("persistent_volume_1");
        persistent_volume_manager.add_mount("persistent_volume_2");

        let connector_configuration_mount = TempAtomicWriterVolume::new("connector_configuration");
        connector_configuration_mount.stage_file_create(
            Path::new("MQTT_CONNECTION_CONFIGURATION"),
            MQTT_CONNECTION_CONFIGURATION_JSON,
        );
        connector_configuration_mount.stage_file_create(Path::new("DIAGNOSTICS"), DIAGNOSTICS_JSON);
        connector_configuration_mount.stage_file_create(
            Path::new("PERSISTENT_VOLUME_MOUNT_PATH"),
            &persistent_volume_manager.index_file_contents(),
        );
        connector_configuration_mount.stage_file_create(
            Path::new("ADDITIONAL_CONNECTOR_CONFIGURATION"),
            ADDITIONAL_CONNECTOR_CONFIGURATION_JSON,
        );
        connector_configuration_mount.execute_update();

        let broker_sat_file_mount = NamedTempFile::with_prefix("broker-sat").unwrap();

        let broker_trust_bundle_mount = TempMount::new("broker_tls_trust_bundle_ca_cert");
        broker_trust_bundle_mount.add_file("ca.txt", "");

        // NOTE: There do not have to be any files in these mounts
        let connector_secrets_metadata_mount = TempMount::new("connector_secrets_metadata");
        let connector_secrets_mount = TempMount::new("connector_secrets");
        let connector_trust_settings_mount = TempMount::new("connector_trust_settings");
        let device_endpoint_trust_bundle_mount =
            TempMount::new("device_endpoint_tls_trust_bundle_ca_cert");
        let device_endpoint_credentials_mount = TempMount::new("device_endpoint_credentials");

        // NOTE: there do not need to be files in these stopgap mounts... I think
        let grpc_metric_collector_1p_ca_mount = TempMount::new("1p_metrics_ca");
        let grpc_log_collector_1p_ca_mount = TempMount::new("1p_logs_ca");

        temp_env::with_vars(
            [
                (
                    "AZURE_EXTENSION_RESOURCEID",
                    Some(AZURE_EXTENSION_RESOURCE_ID),
                ),
                ("CONNECTOR_ID", Some(CONNECTOR_ID)),
                ("CONNECTOR_NAMESPACE", Some(CONNECTOR_NAMESPACE)),
                (
                    "CONNECTOR_CONFIGURATION_MOUNT_PATH",
                    Some(connector_configuration_mount.path().to_str().unwrap()),
                ),
                (
                    "CONNECTOR_SECRETS_METADATA_MOUNT_PATH",
                    Some(connector_secrets_metadata_mount.path().to_str().unwrap()),
                ),
                (
                    "CONNECTOR_SECRETS_MOUNT_PATH",
                    Some(connector_secrets_mount.path().to_str().unwrap()),
                ),
                (
                    "CONNECTOR_TRUST_SETTINGS_MOUNT_PATH",
                    Some(connector_trust_settings_mount.path().to_str().unwrap()),
                ),
                (
                    "BROKER_TLS_TRUST_BUNDLE_CACERT_MOUNT_PATH",
                    Some(broker_trust_bundle_mount.path().to_str().unwrap()),
                ),
                (
                    "BROKER_SAT_MOUNT_PATH",
                    Some(broker_sat_file_mount.path().to_str().unwrap()),
                ),
                (
                    "DEVICE_ENDPOINT_TLS_TRUST_BUNDLE_CA_CERT_MOUNT_PATH",
                    Some(device_endpoint_trust_bundle_mount.path().to_str().unwrap()),
                ),
                (
                    "DEVICE_ENDPOINT_CREDENTIALS_MOUNT_PATH",
                    Some(device_endpoint_credentials_mount.path().to_str().unwrap()),
                ),
                // Stopgap values beyond this point
                ("OTLP_GRPC_METRIC_ENDPOINT", Some(GRPC_METRIC_ENDPOINT)),
                ("OTLP_GRPC_LOG_ENDPOINT", Some(GRPC_LOG_ENDPOINT)),
                ("OTLP_GRPC_TRACE_ENDPOINT", Some(GRPC_TRACE_ENDPOINT)),
                (
                    "FIRST_PARTY_OTLP_GRPC_METRICS_COLLECTOR_CA_PATH",
                    Some(grpc_metric_collector_1p_ca_mount.path().to_str().unwrap()),
                ),
                (
                    "FIRST_PARTY_OTLP_GRPC_LOG_COLLECTOR_CA_PATH",
                    Some(grpc_log_collector_1p_ca_mount.path().to_str().unwrap()),
                ),
                ("OTLP_HTTP_METRIC_ENDPOINT", Some(HTTP_METRIC_ENDPOINT)),
                ("OTLP_HTTP_LOG_ENDPOINT", Some(HTTP_LOG_ENDPOINT)),
                ("OTLP_HTTP_TRACE_ENDPOINT", Some(HTTP_TRACE_ENDPOINT)),
            ],
            || {
                let artifacts = ConnectorArtifacts::new_from_deployment().unwrap();
                // -- Validate the values directly in the artifacts --
                assert_eq!(
                    artifacts.azure_extension_resource_id,
                    AZURE_EXTENSION_RESOURCE_ID
                );
                assert_eq!(artifacts.connector_id, CONNECTOR_ID);
                assert_eq!(artifacts.connector_namespace, CONNECTOR_NAMESPACE);
                assert!(artifacts.connector_secrets.is_some());
                assert_eq!(
                    artifacts.connector_trust_settings_mount.unwrap(),
                    connector_trust_settings_mount.path()
                );
                assert_eq!(
                    artifacts.broker_trust_bundle_mount.unwrap(),
                    broker_trust_bundle_mount.path()
                );
                assert_eq!(
                    artifacts.broker_sat_mount.unwrap(),
                    broker_sat_file_mount.path()
                );
                assert_eq!(
                    artifacts.device_endpoint_trust_bundle_mount.unwrap(),
                    device_endpoint_trust_bundle_mount.path()
                );
                assert_eq!(
                    artifacts.device_endpoint_credentials_mount.unwrap(),
                    device_endpoint_credentials_mount.path()
                );

                // -- Validate the ConnectorConfiguration from the ConnectorArtifacts --
                assert_eq!(
                    *artifacts
                        .connector_configuration
                        .mqtt_connection_configuration
                        .borrow(),
                    serde_json::from_str::<MqttConnectionConfiguration>(
                        MQTT_CONNECTION_CONFIGURATION_JSON
                    )
                    .unwrap()
                );
                assert_eq!(
                    artifacts.connector_configuration.diagnostics,
                    Some(serde_json::from_str::<Diagnostics>(DIAGNOSTICS_JSON).unwrap())
                );
                assert_eq!(
                    artifacts.connector_configuration.persistent_volumes,
                    persistent_volume_manager.volume_path_bufs()
                );
                assert_eq!(
                    artifacts.connector_configuration.additional_configuration,
                    Some(ADDITIONAL_CONNECTOR_CONFIGURATION_JSON.to_string())
                );

                // -- Validate the stopgap variables in the ConnectorArtifacts --
                assert_eq!(
                    artifacts.grpc_metric_endpoint,
                    Some(GRPC_METRIC_ENDPOINT.to_string())
                );
                assert_eq!(
                    artifacts.grpc_log_endpoint,
                    Some(GRPC_LOG_ENDPOINT.to_string())
                );
                assert_eq!(
                    artifacts.grpc_trace_endpoint,
                    Some(GRPC_TRACE_ENDPOINT.to_string())
                );
                assert_eq!(
                    artifacts.grpc_metric_collector_1p_ca_mount.unwrap(),
                    grpc_metric_collector_1p_ca_mount.path()
                );
                assert_eq!(
                    artifacts.grpc_log_collector_1p_ca_mount.unwrap(),
                    grpc_log_collector_1p_ca_mount.path()
                );
                assert_eq!(
                    artifacts.http_metric_endpoint,
                    Some(HTTP_METRIC_ENDPOINT.to_string())
                );
                assert_eq!(
                    artifacts.http_log_endpoint,
                    Some(HTTP_LOG_ENDPOINT.to_string())
                );
                assert_eq!(
                    artifacts.http_trace_endpoint,
                    Some(HTTP_TRACE_ENDPOINT.to_string())
                );
            },
        );
    }

    #[test_case("AZURE_EXTENSION_RESOURCEID")]
    #[test_case("CONNECTOR_ID")]
    #[test_case("CONNECTOR_NAMESPACE")]
    #[test_case("CONNECTOR_CONFIGURATION_MOUNT_PATH")]
    fn missing_required_env_var(missing_env_var: &str) {
        let connector_configuration_mount = TempAtomicWriterVolume::new("connector_configuration");
        connector_configuration_mount.stage_file_create(
            Path::new("MQTT_CONNECTION_CONFIGURATION"),
            MQTT_CONNECTION_CONFIGURATION_JSON,
        );
        connector_configuration_mount.execute_update();

        temp_env::with_vars(
            [
                (
                    "AZURE_EXTENSION_RESOURCEID",
                    Some(AZURE_EXTENSION_RESOURCE_ID),
                ),
                ("CONNECTOR_ID", Some(CONNECTOR_ID)),
                ("CONNECTOR_NAMESPACE", Some(CONNECTOR_NAMESPACE)),
                (
                    "CONNECTOR_CONFIGURATION_MOUNT_PATH",
                    Some(connector_configuration_mount.path().to_str().unwrap()),
                ),
                // NOTE: This will override one of the above
                (missing_env_var, None),
            ],
            || {
                assert!(ConnectorArtifacts::new_from_deployment().is_err());
            },
        );
    }

    #[test_case("CONNECTOR_CONFIGURATION_MOUNT_PATH")]
    #[test_case("CONNECTOR_SECRETS_METADATA_MOUNT_PATH")]
    #[test_case("CONNECTOR_TRUST_SETTINGS_MOUNT_PATH")]
    #[test_case("BROKER_TLS_TRUST_BUNDLE_CACERT_MOUNT_PATH")]
    #[test_case("BROKER_SAT_MOUNT_PATH")]
    #[test_case("DEVICE_ENDPOINT_TLS_TRUST_BUNDLE_CA_CERT_MOUNT_PATH")]
    #[test_case("DEVICE_ENDPOINT_CREDENTIALS_MOUNT_PATH")]
    #[test_case("FIRST_PARTY_OTLP_GRPC_METRICS_COLLECTOR_CA_PATH")]
    #[test_case("FIRST_PARTY_OTLP_GRPC_LOG_COLLECTOR_CA_PATH")]
    fn nonexistent_mount_path(invalid_mount_env_var: &str) {
        let invalid_mount = PathBuf::from("nonexistent/mount/path");
        assert!(!invalid_mount.exists());

        let connector_configuration_mount = TempAtomicWriterVolume::new("connector_configuration");
        connector_configuration_mount.stage_file_create(
            Path::new("MQTT_CONNECTION_CONFIGURATION"),
            MQTT_CONNECTION_CONFIGURATION_JSON,
        );
        connector_configuration_mount.execute_update();

        temp_env::with_vars(
            [
                (
                    "AZURE_EXTENSION_RESOURCEID",
                    Some(AZURE_EXTENSION_RESOURCE_ID),
                ),
                ("CONNECTOR_ID", Some(CONNECTOR_ID)),
                ("CONNECTOR_NAMESPACE", Some(CONNECTOR_NAMESPACE)),
                (
                    "CONNECTOR_CONFIGURATION_MOUNT_PATH",
                    Some(connector_configuration_mount.path().to_str().unwrap()),
                ),
                // NOTE: This may override CONNECTOR_CONFIGURATION_MOUNT_PATH
                (invalid_mount_env_var, Some(invalid_mount.to_str().unwrap())),
            ],
            || {
                assert!(ConnectorArtifacts::new_from_deployment().is_err());
            },
        );
    }

    #[test_case("MQTT_CONNECTION_CONFIGURATION")]
    fn missing_required_file_in_mount(required_file: &str) {
        let connector_configuration_mount = TempAtomicWriterVolume::new("connector_configuration");
        connector_configuration_mount.stage_file_create(
            Path::new("MQTT_CONNECTION_CONFIGURATION"),
            MQTT_CONNECTION_CONFIGURATION_JSON,
        );

        // NOTE: This will override one of the above files that was created
        connector_configuration_mount.stage_file_remove(Path::new(required_file));
        connector_configuration_mount.execute_update();

        temp_env::with_vars(
            [
                (
                    "AZURE_EXTENSION_RESOURCEID",
                    Some(AZURE_EXTENSION_RESOURCE_ID),
                ),
                ("CONNECTOR_ID", Some(CONNECTOR_ID)),
                ("CONNECTOR_NAMESPACE", Some(CONNECTOR_NAMESPACE)),
                (
                    "CONNECTOR_CONFIGURATION_MOUNT_PATH",
                    Some(connector_configuration_mount.path().to_str().unwrap()),
                ),
            ],
            || {
                assert!(ConnectorArtifacts::new_from_deployment().is_err());
            },
        );
    }

    #[test_matrix(
        ["MQTT_CONNECTION_CONFIGURATION", "DIAGNOSTICS"],
        [NOT_JSON, ARBITRARY_JSON, ]
    )]
    fn invalid_contents_in_json_file(file: &str, file_contents: &str) {
        let connector_configuration_mount = TempAtomicWriterVolume::new("connector_configuration");
        connector_configuration_mount.stage_file_create(
            Path::new("MQTT_CONNECTION_CONFIGURATION"),
            MQTT_CONNECTION_CONFIGURATION_JSON,
        );
        connector_configuration_mount
            .stage_file_create(Path::new("DIAGNOSTICS"), DIAGNOSTICS_JSON);

        // Replace one of the above with the invalid content
        connector_configuration_mount.stage_file_remove(Path::new(file));
        connector_configuration_mount.stage_file_create(Path::new(file), file_contents);
        connector_configuration_mount.execute_update();

        temp_env::with_vars(
            [
                (
                    "AZURE_EXTENSION_RESOURCEID",
                    Some(AZURE_EXTENSION_RESOURCE_ID),
                ),
                ("CONNECTOR_ID", Some(CONNECTOR_ID)),
                ("CONNECTOR_NAMESPACE", Some(CONNECTOR_NAMESPACE)),
                (
                    "CONNECTOR_CONFIGURATION_MOUNT_PATH",
                    Some(connector_configuration_mount.path().to_str().unwrap()),
                ),
            ],
            || {
                assert!(ConnectorArtifacts::new_from_deployment().is_err());
            },
        );
    }

    #[test]
    fn nonexistent_persistent_volume_mount() {
        let fake_mount_path = PathBuf::from("nonexistent/mount/path");
        assert!(!fake_mount_path.exists());

        let connector_configuration_mount = TempAtomicWriterVolume::new("connector_configuration");
        connector_configuration_mount.stage_file_create(
            Path::new("MQTT_CONNECTION_CONFIGURATION"),
            MQTT_CONNECTION_CONFIGURATION_JSON,
        );
        connector_configuration_mount.stage_file_create(
            Path::new("PERSISTENT_VOLUME_MOUNT_PATH"),
            fake_mount_path.to_str().unwrap(),
        );
        connector_configuration_mount.execute_update();

        temp_env::with_vars(
            [
                (
                    "AZURE_EXTENSION_RESOURCEID",
                    Some(AZURE_EXTENSION_RESOURCE_ID),
                ),
                ("CONNECTOR_ID", Some(CONNECTOR_ID)),
                ("CONNECTOR_NAMESPACE", Some(CONNECTOR_NAMESPACE)),
                (
                    "CONNECTOR_CONFIGURATION_MOUNT_PATH",
                    Some(connector_configuration_mount.path().to_str().unwrap()),
                ),
            ],
            || {
                assert!(ConnectorArtifacts::new_from_deployment().is_err());
            },
        );
    }

    #[test]
    fn convert_to_mqtt_connection_settings_minimum() {
        let mqtt_json = r#"{
            "host": "someHostName:1234",
            "keepAliveSeconds": 60,
            "maxInflightMessages": 100,
            "protocol": "mqtt",
            "sessionExpirySeconds": 3600,
            "tls": { "mode": "Disabled" }
        }"#;

        let connector_configuration_mount = TempAtomicWriterVolume::new("connector_configuration");
        connector_configuration_mount.stage_file_create(Path::new("MQTT_CONNECTION_CONFIGURATION"), mqtt_json);
        connector_configuration_mount.execute_update();

        temp_env::with_vars(
            [
                (
                    "AZURE_EXTENSION_RESOURCEID",
                    Some(AZURE_EXTENSION_RESOURCE_ID),
                ),
                ("CONNECTOR_ID", Some(CONNECTOR_ID)),
                ("CONNECTOR_NAMESPACE", Some(CONNECTOR_NAMESPACE)),
                (
                    "CONNECTOR_CONFIGURATION_MOUNT_PATH",
                    Some(connector_configuration_mount.path().to_str().unwrap()),
                ),
            ],
            || {
                let artifacts = ConnectorArtifacts::new_from_deployment().unwrap();
                let mqtt_connection_settings = artifacts
                    .to_mqtt_connection_settings("0")
                    .unwrap();
                assert_eq!(mqtt_connection_settings.client_id(), "connector_id0");
                assert_eq!(mqtt_connection_settings.hostname(), "someHostName");
                assert_eq!(mqtt_connection_settings.tcp_port(), 1234);
                assert_eq!(
                    *mqtt_connection_settings.keep_alive(),
                    Duration::from_secs(60)
                );
                assert_eq!(mqtt_connection_settings.receive_max(), 100);
                assert_eq!(
                    *mqtt_connection_settings.session_expiry(),
                    Duration::from_secs(3600)
                );
                assert!(!mqtt_connection_settings.use_tls());
                assert_eq!(*mqtt_connection_settings.ca_file(), None);
                assert_eq!(*mqtt_connection_settings.sat_file(), None);
            },
        );
    }

    #[test]
    fn convert_to_mqtt_connection_settings_maximum() {
        let mqtt_json = r#"{
            "host": "someHostName:1234",
            "keepAliveSeconds": 60,
            "maxInflightMessages": 100,
            "protocol": "mqtt",
            "sessionExpirySeconds": 3600,
            "tls": { "mode": "Enabled" }
        }"#;

        let connector_configuration_mount = TempAtomicWriterVolume::new("connector_configuration");
        connector_configuration_mount.stage_file_create(Path::new("MQTT_CONNECTION_CONFIGURATION"), mqtt_json);
        connector_configuration_mount.execute_update();

        let broker_sat_file_mount = NamedTempFile::with_prefix("broker-sat").unwrap();

        let broker_trust_bundle_mount = TempMount::new("broker_tls_trust_bundle_ca_cert");
        broker_trust_bundle_mount.add_file("ca.txt", "");

        temp_env::with_vars(
            [
                (
                    "AZURE_EXTENSION_RESOURCEID",
                    Some(AZURE_EXTENSION_RESOURCE_ID),
                ),
                ("CONNECTOR_ID", Some(CONNECTOR_ID)),
                ("CONNECTOR_NAMESPACE", Some(CONNECTOR_NAMESPACE)),
                (
                    "CONNECTOR_CONFIGURATION_MOUNT_PATH",
                    Some(connector_configuration_mount.path().to_str().unwrap()),
                ),
                (
                    "BROKER_TLS_TRUST_BUNDLE_CACERT_MOUNT_PATH",
                    Some(broker_trust_bundle_mount.path().to_str().unwrap()),
                ),
                (
                    "BROKER_SAT_MOUNT_PATH",
                    Some(broker_sat_file_mount.path().to_str().unwrap()),
                ),
            ],
            || {
                let artifacts = ConnectorArtifacts::new_from_deployment().unwrap();
                let mqtt_connection_settings = artifacts
                    .to_mqtt_connection_settings("0")
                    .unwrap();
                assert_eq!(mqtt_connection_settings.client_id(), "connector_id0");
                assert_eq!(mqtt_connection_settings.hostname(), "someHostName");
                assert_eq!(mqtt_connection_settings.tcp_port(), 1234);
                assert_eq!(
                    *mqtt_connection_settings.keep_alive(),
                    Duration::from_secs(60)
                );
                assert_eq!(mqtt_connection_settings.receive_max(), 100);
                assert_eq!(
                    *mqtt_connection_settings.session_expiry(),
                    Duration::from_secs(3600)
                );
                assert!(mqtt_connection_settings.use_tls());
                assert_eq!(
                    *mqtt_connection_settings.ca_file(),
                    Some(
                        broker_trust_bundle_mount
                            .path()
                            .join("ca.txt")
                            .into_os_string()
                            .into_string()
                            .unwrap()
                    )
                );
                assert_eq!(
                    *mqtt_connection_settings.sat_file(),
                    Some(broker_sat_file_mount.path().to_str().unwrap().to_string())
                );
            },
        );
    }

    #[test_case("someHostName:not_a_number"; "Invalid TCP port")]
    #[test_case("someHostName:1234:extra_colon"; "Extra colon in host")]
    #[test_case("not_a_host"; "No port in host")]
    fn convert_to_mqtt_connection_settings_malformed_host(host: &str) {
        let mqtt_json = format!(
            r#"{{
                "host": "{host}",
                "keepAliveSeconds": 60,
                "maxInflightMessages": 100,
                "protocol": "mqtt",
                "sessionExpirySeconds": 3600,
                "tls": {{ "mode": "Disabled" }}
            }}"#
        );

        let connector_configuration_mount = TempAtomicWriterVolume::new("connector_configuration");
        connector_configuration_mount.stage_file_create(Path::new("MQTT_CONNECTION_CONFIGURATION"), &mqtt_json);
        connector_configuration_mount.execute_update();

        temp_env::with_vars(
            [
                (
                    "AZURE_EXTENSION_RESOURCEID",
                    Some(AZURE_EXTENSION_RESOURCE_ID),
                ),
                ("CONNECTOR_ID", Some(CONNECTOR_ID)),
                ("CONNECTOR_NAMESPACE", Some(CONNECTOR_NAMESPACE)),
                (
                    "CONNECTOR_CONFIGURATION_MOUNT_PATH",
                    Some(connector_configuration_mount.path().to_str().unwrap()),
                ),
            ],
            || {
                let artifacts = ConnectorArtifacts::new_from_deployment().unwrap();
                assert!(
                    artifacts
                        .to_mqtt_connection_settings("0")
                        .is_err()
                );
            },
        );
    }

    #[test]
    fn convert_to_mqtt_connection_settings_no_ca_cert() {
        let connector_configuration_mount = TempAtomicWriterVolume::new("connector_configuration");
        connector_configuration_mount.stage_file_create(
            Path::new("MQTT_CONNECTION_CONFIGURATION"),
            MQTT_CONNECTION_CONFIGURATION_JSON,
        );
        connector_configuration_mount.execute_update();

        // NOTE: no CA cert is added to this mount
        let broker_trust_bundle_mount = TempMount::new("broker_tls_trust_bundle_ca_cert");

        temp_env::with_vars(
            [
                (
                    "AZURE_EXTENSION_RESOURCEID",
                    Some(AZURE_EXTENSION_RESOURCE_ID),
                ),
                ("CONNECTOR_ID", Some(CONNECTOR_ID)),
                ("CONNECTOR_NAMESPACE", Some(CONNECTOR_NAMESPACE)),
                (
                    "CONNECTOR_CONFIGURATION_MOUNT_PATH",
                    Some(connector_configuration_mount.path().to_str().unwrap()),
                ),
                (
                    "BROKER_TLS_TRUST_BUNDLE_CACERT_MOUNT_PATH",
                    Some(broker_trust_bundle_mount.path().to_str().unwrap()),
                ),
            ],
            || {
                let artifacts = ConnectorArtifacts::new_from_deployment().unwrap();
                assert!(
                    artifacts
                        .to_mqtt_connection_settings("0")
                        .is_err()
                );
            },
        );
    }

    // TODO: Simulate permissions issues in mounts
}
