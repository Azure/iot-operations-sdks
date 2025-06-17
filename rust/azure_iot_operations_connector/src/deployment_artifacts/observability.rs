// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

//! Types for extracting Observability configurations from an Akri/AIO deployment.
//! Note that this module is a stopgap implementation and will be replaced in
//! the future by a unified approach to observability endpoints.

use std::path::PathBuf;

use crate::deployment_artifacts::{
    DeploymentArtifactError, DeploymentArtifactErrorRepr, string_from_environment,
    valid_mount_pathbuf_from,
};

/// Values extracted from the observability artifacts in an Akri deployment.
#[derive(Debug, Clone, PartialEq)]
pub struct ObservabilityArtifacts {
    /// The Azure extension resource ID.
    pub azure_extension_resource_id: String,
    /// OTEL grpc/grpcs metric endpoint.
    pub grpc_metric_endpoint: Option<String>,
    /// OTEL grpc/grpcs log endpoint.
    pub grpc_log_endpoint: Option<String>,
    /// OTEL grpc/grpcs trace endpoint.
    pub grpc_trace_endpoint: Option<String>,
    /// Path to the directory containing trust bundle for 1P grpc metric collector.
    pub grpc_metric_collector_1p_ca_mount: Option<PathBuf>,
    /// Path to the directory containing trust bundle for 1P grpc log collector.
    pub grpc_log_collector_1p_ca_mount: Option<PathBuf>,

    /// OTEL http/https metric endpoint.
    pub http_metric_endpoint: Option<String>,
    /// OTEL http/https log endpoint.
    pub http_log_endpoint: Option<String>,
    /// OTEL http/https trace endpoint.
    pub http_trace_endpoint: Option<String>,

    /// OTEL 3P metric endpoint.
    pub metric_endpoint_3p: Option<String>,
    /// OTEL 3P metric export interval.
    pub metric_export_interval_3p: Option<u32>,
}

impl ObservabilityArtifacts {
    /// Create an `ObservabilityArtifacts` instance from the environment variables in an Akri
    /// deployment.
    ///
    /// # Errors
    /// - Returns a `DeploymentArtifactError` if there is an error with one of the artifacts in the
    ///   Akri deployment
    pub fn new_from_deployment() -> Result<ObservabilityArtifacts, DeploymentArtifactError> {
        let azure_extension_resource_id = string_from_environment("AZURE_EXTENSION_RESOURCEID")?
            .ok_or(DeploymentArtifactErrorRepr::EnvVarMissing(
                "AZURE_EXTENSION_RESOURCEID".to_string(),
            ))?;

        let grpc_metric_endpoint = string_from_environment("OTLP_GRPC_METRIC_ENDPOINT")?;
        let grpc_log_endpoint = string_from_environment("OTLP_GRPC_LOG_ENDPOINT")?;
        let grpc_trace_endpoint = string_from_environment("OTLP_GRPC_TRACE_ENDPOINT")?;

        let grpc_metric_collector_1p_ca_mount =
            string_from_environment("FIRST_PARTY_OTLP_GRPC_METRICS_COLLECTOR_CA_PATH")?
                .map(valid_mount_pathbuf_from)
                .transpose()?;
        let grpc_log_collector_1p_ca_mount =
            string_from_environment("FIRST_PARTY_OTLP_GRPC_LOG_COLLECTOR_CA_PATH")?
                .map(valid_mount_pathbuf_from)
                .transpose()?;

        let http_metric_endpoint = string_from_environment("OTLP_HTTP_METRIC_ENDPOINT")?;
        let http_log_endpoint = string_from_environment("OTLP_HTTP_LOG_ENDPOINT")?;
        let http_trace_endpoint = string_from_environment("OTLP_HTTP_TRACE_ENDPOINT")?;

        let metric_endpoint_3p = string_from_environment("OTLP_METRIC_ENDPOINT_3P")?;
        let metric_export_interval_3p = string_from_environment("OTLP_METRIC_EXPORT_INTERVAL_3P")?
            .map(|s| s.parse::<u32>())
            .transpose()
            .map_err(|_| {
                DeploymentArtifactErrorRepr::EnvVarValueMalformed(
                    "OTLP_METRIC_EXPORT_INTERVAL_3P".to_string(),
                )
            })?;

        Ok(ObservabilityArtifacts {
            azure_extension_resource_id,
            grpc_metric_endpoint,
            grpc_log_endpoint,
            grpc_trace_endpoint,
            grpc_metric_collector_1p_ca_mount,
            grpc_log_collector_1p_ca_mount,
            http_metric_endpoint,
            http_log_endpoint,
            http_trace_endpoint,
            metric_endpoint_3p,
            metric_export_interval_3p,
        })
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use crate::deployment_artifacts::test_utils::TempMount;
    use test_case::test_case;

    const AZURE_EXTENSION_RESOURCE_ID: &str = "/subscriptions/extension/resource/id";
    const GRPC_METRIC_ENDPOINT: &str = "grpcs://metric.endpoint";
    const GRPC_LOG_ENDPOINT: &str = "grpcs://log.endpoint";
    const GRPC_TRACE_ENDPOINT: &str = "grpcs://trace.endpoint";
    const HTTP_METRIC_ENDPOINT: &str = "https://metric.endpoint";
    const HTTP_LOG_ENDPOINT: &str = "https://log.endpoint";
    const HTTP_TRACE_ENDPOINT: &str = "https://trace.endpoint";
    const METRIC_ENDPOINT_3P: &str = "https://3p.metric.endpoint";
    const METRIC_EXPORT_INTERVAL_3P: u32 = 30;

    #[test]
    fn min_artifacts() {
        temp_env::with_vars(
            [
                (
                    "AZURE_EXTENSION_RESOURCEID",
                    Some(AZURE_EXTENSION_RESOURCE_ID),
                ),
                ("OTLP_GRPC_METRIC_ENDPOINT", None),
                ("OTLP_GRPC_LOG_ENDPOINT", None),
                ("OTLP_GRPC_TRACE_ENDPOINT", None),
                ("FIRST_PARTY_OTLP_GRPC_METRICS_COLLECTOR_CA_PATH", None),
                ("FIRST_PARTY_OTLP_GRPC_LOG_COLLECTOR_CA_PATH", None),
                ("OTLP_HTTP_METRIC_ENDPOINT", None),
                ("OTLP_HTTP_LOG_ENDPOINT", None),
                ("OTLP_HTTP_TRACE_ENDPOINT", None),
                ("OTLP_METRIC_ENDPOINT_3P", None),
                ("OTLP_METRIC_EXPORT_INTERVAL_3P", None),
            ],
            || {
                let artifacts = ObservabilityArtifacts::new_from_deployment().unwrap();
                assert_eq!(
                    artifacts.azure_extension_resource_id,
                    AZURE_EXTENSION_RESOURCE_ID
                );
                assert!(artifacts.grpc_metric_endpoint.is_none());
                assert!(artifacts.grpc_log_endpoint.is_none());
                assert!(artifacts.grpc_trace_endpoint.is_none());
                assert!(artifacts.grpc_metric_collector_1p_ca_mount.is_none());
                assert!(artifacts.grpc_log_collector_1p_ca_mount.is_none());
                assert!(artifacts.http_metric_endpoint.is_none());
                assert!(artifacts.http_log_endpoint.is_none());
                assert!(artifacts.http_trace_endpoint.is_none());
                assert!(artifacts.metric_endpoint_3p.is_none());
                assert!(artifacts.metric_export_interval_3p.is_none());
            },
        );
    }

    #[test]
    fn max_artifacts() {
        // NOTE: there do not need to be files in these mounts.. I think
        let grpc_metric_collector_1p_ca_mount = TempMount::new("1p_metrics_ca");
        let grpc_log_collector_1p_ca_mount = TempMount::new("1p_logs_ca");

        temp_env::with_vars(
            [
                (
                    "AZURE_EXTENSION_RESOURCEID",
                    Some(AZURE_EXTENSION_RESOURCE_ID),
                ),
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
                ("OTLP_METRIC_ENDPOINT_3P", Some(METRIC_ENDPOINT_3P)),
                (
                    "OTLP_METRIC_EXPORT_INTERVAL_3P",
                    Some(&format!("{METRIC_EXPORT_INTERVAL_3P}")),
                ),
            ],
            || {
                let artifacts = ObservabilityArtifacts::new_from_deployment().unwrap();
                assert_eq!(
                    artifacts.azure_extension_resource_id,
                    AZURE_EXTENSION_RESOURCE_ID
                );
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
                assert_eq!(
                    artifacts.metric_endpoint_3p,
                    Some(METRIC_ENDPOINT_3P.to_string())
                );
                assert_eq!(
                    artifacts.metric_export_interval_3p,
                    Some(METRIC_EXPORT_INTERVAL_3P)
                );
            },
        );
    }

    #[test_case("AZURE_EXTENSION_RESOURCEID")]
    fn missing_required_artifacts(missing_env_var: &str) {
        temp_env::with_vars(
            [
                (
                    "AZURE_EXTENSION_RESOURCEID",
                    Some(AZURE_EXTENSION_RESOURCE_ID),
                ),
                // NOTE: This will override one of the above
                (missing_env_var, None),
            ],
            || {
                assert!(ObservabilityArtifacts::new_from_deployment().is_err());
            },
        );
    }

    #[test_case("FIRST_PARTY_OTLP_GRPC_METRICS_COLLECTOR_CA_PATH")]
    #[test_case("FIRST_PARTY_OTLP_GRPC_LOG_COLLECTOR_CA_PATH")]
    fn nonexistent_mount_path(invalid_mount_env_var: &str) {
        let invalid_mount = PathBuf::from("/nonexistent/mount/path/");
        assert!(!invalid_mount.exists());

        temp_env::with_vars(
            [
                (
                    "AZURE_EXTENSION_RESOURCEID",
                    Some(AZURE_EXTENSION_RESOURCE_ID),
                ),
                (invalid_mount_env_var, Some(invalid_mount.to_str().unwrap())),
            ],
            || {
                assert!(ObservabilityArtifacts::new_from_deployment().is_err());
            },
        );
    }

    #[test_case("OTLP_METRIC_EXPORT_INTERVAL_3P", "not_a_number")]
    fn malformed_value(env_var: &str, malformed_value: &str) {
        temp_env::with_vars(
            [
                (
                    "AZURE_EXTENSION_RESOURCEID",
                    Some(AZURE_EXTENSION_RESOURCE_ID),
                ),
                (env_var, Some(malformed_value)),
            ],
            || {
                assert!(ObservabilityArtifacts::new_from_deployment().is_err());
            },
        );
    }
}
