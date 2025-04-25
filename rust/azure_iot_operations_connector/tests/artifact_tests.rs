// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

use azure_iot_operations_connector::filemount::connector_config::{
    ConnectorConfiguration, LogLevel, Protocol, TlsMode,
};
use std::path::PathBuf;

fn get_dummy_file_directory() -> PathBuf {
    let mut path = PathBuf::from(env!("CARGO_MANIFEST_DIR"));
    // TODO: make this platform independent
    path.push("../../eng/test/test-connector-mount-files");
    path
}

fn get_connector_config_mount_path(dir_name: &str) -> PathBuf {
    let mut path = get_dummy_file_directory();
    path.push(dir_name);
    path
}

fn get_trust_bundle_mount_path() -> PathBuf {
    let mut path = get_dummy_file_directory();
    path.push("trust-bundle");
    path
}

// TODO: make real
const FAKE_SAT_FILE: &str = "/path/to/sat/file";

#[test]
fn local_connector_config() {
    let cc_mount_path = get_connector_config_mount_path("connector-config");
    let trust_bundle_mount_path = get_trust_bundle_mount_path();
    temp_env::with_vars(
        [
            ("CONNECTOR_CLIENT_ID_PREFIX", Some("test-client-id")),
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
            let cc = ConnectorConfiguration::new_from_deployment().unwrap();
            // NOTE: This value was set directly above in the environment variables
            assert_eq!(cc.client_id_prefix, "test-client-id");
            // NOTE: These values come from the MQTT_CONNECTION_CONFIGURATION file
            assert_eq!(cc.mqtt_connection_configuration.host, "someHostName:1234");
            assert_eq!(cc.mqtt_connection_configuration.keep_alive_seconds, 10);
            assert_eq!(cc.mqtt_connection_configuration.max_inflight_messages, 10);
            matches!(cc.mqtt_connection_configuration.protocol, Protocol::Mqtt);
            assert_eq!(cc.mqtt_connection_configuration.session_expiry_seconds, 20);
            matches!(cc.mqtt_connection_configuration.tls.mode, TlsMode::Enabled);
            // NOTE: These values come from the AIO_METADATA file
            assert_eq!(cc.aio_metadata.aio_min_version, "1.0");
            assert_eq!(cc.aio_metadata.aio_max_version, "2.0");
            // NOTE: These values come from the DIAGNOSTICS file
            matches!(cc.diagnostics.logs.level, LogLevel::Trace);
        },
    );
}

// TODO: Operator deployment test
