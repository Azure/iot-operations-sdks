// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

use azure_iot_operations_connector::deployment_artifacts::connector::{
    ConnectorArtifacts, Protocol, TlsMode,
};
use azure_iot_operations_mqtt::session::{Session, SessionOptionsBuilder};
use std::path::PathBuf;
use std::time::Duration;

fn get_test_directory() -> PathBuf {
    let mut path = PathBuf::from(env!("CARGO_MANIFEST_DIR"));
    path.push("../../eng/test/test-connector-mount-files");
    path
}

fn get_connector_config_mount_path(dir_name: &str) -> PathBuf {
    let mut path = get_test_directory();
    path.push(dir_name);
    path
}

fn get_broker_trust_bundle_mount_path() -> PathBuf {
    let mut path = get_test_directory();
    path.push("trust-bundle");
    path
}

#[test]
#[allow(clippy::similar_names)]
fn local_connector_artifacts_tls() {
    let cc_mount_path = get_connector_config_mount_path("connector-config");
    let trust_bundle_mount_path = get_broker_trust_bundle_mount_path();
    temp_env::with_vars(
        [
            (
                "AZURE_EXTENSION_RESOURCEID",
                Some("/subscriptions/extension/resource/id"),
            ),
            ("CONNECTOR_ID", Some("connector_id")),
            ("CONNECTOR_NAMESPACE", Some("connector_namespace")),
            (
                "CONNECTOR_CONFIGURATION_MOUNT_PATH",
                Some(cc_mount_path.to_str().unwrap()),
            ),
            (
                "BROKER_TLS_TRUST_BUNDLE_CACERT_MOUNT_PATH",
                Some(trust_bundle_mount_path.to_str().unwrap()),
            ),
        ],
        || {
            let artifacts = ConnectorArtifacts::new_from_deployment().unwrap();
            // -- Validate the ConnectorArtifacts --
            // NOTE: This value was set directly above in the environment variables
            assert_eq!(
                artifacts.azure_extension_resource_id,
                "/subscriptions/extension/resource/id"
            );
            assert_eq!(artifacts.connector_id, "connector_id");
            assert_eq!(artifacts.connector_namespace, "connector_namespace");
            // NOTE: These values are paths specified in the environment variable
            assert_eq!(
                artifacts
                    .broker_trust_bundle_mount
                    .as_ref()
                    .unwrap()
                    .as_path(),
                get_broker_trust_bundle_mount_path().as_path(),
            );

            // --- Validate the ConnectorConfiguration from the ConnectorArtifacts ---
            let cc = &artifacts.connector_configuration;
            // NOTE: These values come from the MQTT_CONNECTION_CONFIGURATION file
            let mcc = &cc.mqtt_connection_configuration;
            assert_eq!(mcc.host, "someHostName:1234");
            assert_eq!(mcc.keep_alive_seconds, 10);
            assert_eq!(mcc.max_inflight_messages, 10);
            assert!(matches!(mcc.protocol, Protocol::Mqtt));
            assert_eq!(mcc.session_expiry_seconds, 20);
            assert!(matches!(mcc.tls.mode, TlsMode::Enabled));
            // NOTE: These values come from the DIAGNOSTICS file
            assert!(cc.diagnostics.is_some());
            let diagnostics = cc.diagnostics.as_ref().unwrap();
            assert_eq!(diagnostics.logs.level, "trace".to_string());

            // --- Convert the ConnectorConfiguration to MqttConnectionSettings ---
            let conversion_result = artifacts.to_mqtt_connection_settings("-id_suffix");
            assert!(conversion_result.is_ok());
            let mcs = conversion_result.unwrap();
            let expected_ca_file = trust_bundle_mount_path
                .join("ca.txt")
                .into_os_string()
                .into_string()
                .unwrap();
            assert_eq!(mcs.client_id(), "connector_id-id_suffix");
            assert_eq!(mcs.hostname(), "someHostName");
            assert_eq!(mcs.tcp_port(), 1234);
            assert_eq!(mcs.keep_alive(), &Duration::from_secs(10));
            assert_eq!(mcs.receive_max(), 10);
            assert_eq!(mcs.session_expiry(), &Duration::from_secs(20));
            assert!(mcs.use_tls());
            assert_eq!(mcs.ca_file(), &Some(expected_ca_file));

            // --- Create a Session from the MqttConnectionSettings ---
            let session_options = SessionOptionsBuilder::default()
                .connection_settings(mcs)
                .build()
                .unwrap();
            assert!(Session::new(session_options).is_ok());
        },
    );
}

#[test]
#[allow(clippy::similar_names)]
fn local_connector_artifacts_no_tls() {
    let cc_mount_path = get_connector_config_mount_path("connector-config-no-auth-no-tls");
    temp_env::with_vars(
        [
            (
                "AZURE_EXTENSION_RESOURCEID",
                Some("/subscriptions/extension/resource/id"),
            ),
            ("CONNECTOR_ID", Some("connector_id")),
            ("CONNECTOR_NAMESPACE", Some("connector_namespace")),
            (
                "CONNECTOR_CONFIGURATION_MOUNT_PATH",
                Some(cc_mount_path.to_str().unwrap()),
            ),
            ("BROKER_TLS_TRUST_BUNDLE_CACERT_MOUNT_PATH", None),
        ],
        || {
            let artifacts = ConnectorArtifacts::new_from_deployment().unwrap();
            // -- Validate the ConnectorArtifacts --
            // NOTE: This value was set directly above in the environment variables
            assert_eq!(
                artifacts.azure_extension_resource_id,
                "/subscriptions/extension/resource/id"
            );
            assert_eq!(artifacts.connector_id, "connector_id");
            assert_eq!(artifacts.connector_namespace, "connector_namespace");
            // NOTE: These values are paths specified in the environment variable
            assert_eq!(artifacts.broker_trust_bundle_mount, None);

            // --- Validate the ConnectorConfiguration from the ConnectorArtifacts ---
            let cc = &artifacts.connector_configuration;
            // NOTE: These values come from the MQTT_CONNECTION_CONFIGURATION file
            let mcc = &cc.mqtt_connection_configuration;
            assert_eq!(mcc.host, "someHostName:1234");
            assert_eq!(mcc.keep_alive_seconds, 10);
            assert_eq!(mcc.max_inflight_messages, 10);
            assert!(matches!(mcc.protocol, Protocol::Mqtt));
            assert_eq!(mcc.session_expiry_seconds, 20);
            assert!(matches!(mcc.tls.mode, TlsMode::Disabled));
            // NOTE: These values come from the DIAGNOSTICS file
            assert!(cc.diagnostics.is_some());
            let diagnostics = cc.diagnostics.as_ref().unwrap();
            assert_eq!(diagnostics.logs.level, "trace".to_string());

            // --- Convert the ConnectorConfiguration to MqttConnectionSettings ---
            let conversion_result = artifacts.to_mqtt_connection_settings("-id_suffix");
            assert!(conversion_result.is_ok());
            let mcs = conversion_result.unwrap();
            assert_eq!(mcs.client_id(), "connector_id-id_suffix");
            assert_eq!(mcs.hostname(), "someHostName");
            assert_eq!(mcs.tcp_port(), 1234);
            assert_eq!(mcs.keep_alive(), &Duration::from_secs(10));
            assert_eq!(mcs.receive_max(), 10);
            assert_eq!(mcs.session_expiry(), &Duration::from_secs(20));
            assert!(!mcs.use_tls());
            assert_eq!(mcs.ca_file(), &None);

            // --- Create a Session from the MqttConnectionSettings ---
            let session_options = SessionOptionsBuilder::default()
                .connection_settings(mcs)
                .build()
                .unwrap();
            assert!(Session::new(session_options).is_ok());
        },
    );
}

// TODO: Operator deployment test
