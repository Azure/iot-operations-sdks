// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

use azure_iot_operations_connector::filemount::connector_artifacts::{
    ConnectorArtifacts, LogLevel, Protocol, TlsMode,
};
use std::path::PathBuf;

fn get_test_directory() -> PathBuf {
    let mut path = PathBuf::from(env!("CARGO_MANIFEST_DIR"));
    // TODO: make this platform independent
    path.push("../../eng/test/test-connector-mount-files");
    path
}

fn get_connector_config_mount_path(dir_name: &str) -> PathBuf {
    let mut path = get_test_directory();
    path.push(dir_name);
    path
}

fn get_trust_bundle_mount_path() -> PathBuf {
    let mut path = get_test_directory();
    path.push("trust-bundle");
    path
}

// TODO: make real
const FAKE_SAT_FILE: &str = "/path/to/sat/file";

#[test]
fn local_connector_artifacts() {
    let cc_mount_path = get_connector_config_mount_path("connector-config");
    let trust_bundle_mount_path = get_trust_bundle_mount_path();
    temp_env::with_vars(
        [
            ("CONNECTOR_ID", Some("connector_id")),
            (
                "CONNECTOR_CONFIGURATION_MOUNT_PATH",
                Some(cc_mount_path.to_str().unwrap()),
            ),
            (
                "BROKER_TLS_TRUST_BUNDLE_CACERT_MOUNT_PATH",
                Some(trust_bundle_mount_path.to_str().unwrap()),
            ),
            ("BROKER_SAT_MOUNT_PATH", Some(FAKE_SAT_FILE)),
        ],
        || {
            let ca = ConnectorArtifacts::new_from_deployment().unwrap();
            // -- Validate the ConnectorArtifacts --
            // NOTE: This value was set directly above in the environment variables
            assert_eq!(ca.connector_id, "connector_id");
            // NOTE: These values are paths specified in the environment variable
            assert_eq!(
                ca.broker_tls_trust_bundle_ca_cert_mount,
                Some(get_trust_bundle_mount_path())
            );
            assert_eq!(ca.broker_sat_mount, Some(PathBuf::from(FAKE_SAT_FILE)));

            // --- Validate the ConnectorConfiguration from the ConnectorArtifacts ---
            let cc = &ca.connector_configuration;
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
            assert!(matches!(diagnostics.logs.level, LogLevel::Trace));

            // --- Convert the ConnectorConfiguration to MqttConnectionSettings ---
            assert!(ca.to_mqtt_connection_settings("-id_suffix").is_ok());
            // TODO: validate - but need getters from MQTTCS first.
            // Or maybe that's just for unit tests and this should just make a session
        },
    );
}

// TODO: Operator deployment test
