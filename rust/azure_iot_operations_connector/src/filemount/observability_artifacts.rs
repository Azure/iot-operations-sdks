// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

//! Types for extracting Observability configurations from an AIO deployment.
//! Note that this module is a stopgap implementation and will be replaced in
//! the future by a unified approach to observability endpoints.

use std::path::PathBuf;

/// Values extracted from the observability artifacts in an AIO deployment.
#[derive(Debug, Clone, PartialEq)]
pub struct ObservabilityArtifacts {
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
    /// Create an `ObservabilityArtifacts` instance from the environment variables in an AIO deployment.
    pub fn new_from_deployment() -> ObservabilityArtifacts {
        let grpc_metric_endpoint = std::env::var("OTLP_GRPC_METRIC_ENDPOINT").ok();
        let grpc_log_endpoint = std::env::var("OTLP_GRPC_LOG_ENDPOINT").ok();
        let grpc_trace_endpoint = std::env::var("OTLP_GRPC_TRACE_ENDPOINT").ok();

        let grpc_metric_collector_1p_ca_mount =
            std::env::var("FIRST_PARTY_OTLP_GRPC_METRICS_COLLECTOR_CA_PATH")
                .ok()
                .map(PathBuf::from);
        let grpc_log_collector_1p_ca_mount =
            std::env::var("FIRST_PARTY_OTLP_GRPC_LOG_COLLECTOR_CA_PATH")
                .ok()
                .map(PathBuf::from);

        let http_metric_endpoint = std::env::var("OTLP_HTTP_METRIC_ENDPOINT").ok();
        let http_log_endpoint = std::env::var("OTLP_HTTP_LOG_ENDPOINT").ok();
        let http_trace_endpoint = std::env::var("OTLP_HTTP_TRACE_ENDPOINT").ok();

        let metric_endpoint_3p = std::env::var("OTLP_METRIC_ENDPOINT_3P").ok();
        let metric_export_interval_3p = std::env::var("OTLP_METRIC_EXPORT_INTERVAL_3P")
            .map(|v| v.parse::<u32>())
            .ok()
            .transpose()
            .ok()
            .flatten();

        // NOTE: Not going to put any validation here, as this is a stopgap implementation.
        // When finalized would want to validate the mount paths for certs, however many there
        // end up being, and validating / restructuring for conditional presence of values
        // (e.g. one implies the existence of another, etc.)

        ObservabilityArtifacts {
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
        }
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn no_artifacts() {
        temp_env::with_vars(
            [
                ("OTLP_GRPC_METRIC_ENDPOINT", None::<&str>),
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
                let artifacts = ObservabilityArtifacts::new_from_deployment();
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
    fn with_artifacts() {
        temp_env::with_vars(
            [
                ("OTLP_GRPC_METRIC_ENDPOINT", Some("grpcs://metric.endpoint")),
                ("OTLP_GRPC_LOG_ENDPOINT", Some("grpcs://log.endpoint")),
                ("OTLP_GRPC_TRACE_ENDPOINT", Some("grpcs://trace.endpoint")),
                (
                    "FIRST_PARTY_OTLP_GRPC_METRICS_COLLECTOR_CA_PATH",
                    Some("/path/to/metrics/ca"),
                ),
                (
                    "FIRST_PARTY_OTLP_GRPC_LOG_COLLECTOR_CA_PATH",
                    Some("/path/to/logs/ca"),
                ),
                ("OTLP_HTTP_METRIC_ENDPOINT", Some("https://metric.endpoint")),
                ("OTLP_HTTP_LOG_ENDPOINT", Some("https://log.endpoint")),
                ("OTLP_HTTP_TRACE_ENDPOINT", Some("https://trace.endpoint")),
                (
                    "OTLP_METRIC_ENDPOINT_3P",
                    Some("https://3p.metric.endpoint"),
                ),
                ("OTLP_METRIC_EXPORT_INTERVAL_3P", Some("30")),
            ],
            || {
                let artifacts = ObservabilityArtifacts::new_from_deployment();
                assert_eq!(
                    artifacts.grpc_metric_endpoint,
                    Some("grpcs://metric.endpoint".to_string())
                );
                assert_eq!(
                    artifacts.grpc_log_endpoint,
                    Some("grpcs://log.endpoint".to_string())
                );
                assert_eq!(
                    artifacts.grpc_trace_endpoint,
                    Some("grpcs://trace.endpoint".to_string())
                );
                assert_eq!(
                    artifacts.grpc_metric_collector_1p_ca_mount,
                    Some(PathBuf::from("/path/to/metrics/ca"))
                );
                assert_eq!(
                    artifacts.grpc_log_collector_1p_ca_mount,
                    Some(PathBuf::from("/path/to/logs/ca"))
                );
                assert_eq!(
                    artifacts.http_metric_endpoint,
                    Some("https://metric.endpoint".to_string())
                );
                assert_eq!(
                    artifacts.http_log_endpoint,
                    Some("https://log.endpoint".to_string())
                );
                assert_eq!(
                    artifacts.http_trace_endpoint,
                    Some("https://trace.endpoint".to_string())
                );
                assert_eq!(
                    artifacts.metric_endpoint_3p,
                    Some("https://3p.metric.endpoint".to_string())
                );
                assert_eq!(artifacts.metric_export_interval_3p, Some(30));
            },
        );
    }
}
