// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

use azure_iot_operations_connector::filemount::connector_config::{
    ConnectorConfiguration, Protocol, TlsMode,
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
fn local_connector_config() {
    let cc_mount_path = get_connector_config_mount_path("connector-config");
    let trust_bundle_mount_path = get_trust_bundle_mount_path();
    temp_env::with_vars(
        [
            ("CONNECTOR_CLIENT_ID_PREFIX", Some("id_prefix")),
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
            // --- Create and validate the ConnectorConfiguration ---
            let cc = ConnectorConfiguration::new_from_deployment().unwrap();
            // NOTE: This value was set directly above in the environment variables
            assert_eq!(cc.client_id_prefix, "id_prefix");
            // NOTE: These values come from the MQTT_CONNECTION_CONFIGURATION file
            assert_eq!(cc.mqtt_connection_configuration.host, "someHostName:1234");
            assert_eq!(cc.mqtt_connection_configuration.keep_alive_seconds, 10);
            assert_eq!(cc.mqtt_connection_configuration.max_inflight_messages, 10);
            assert!(matches!(
                cc.mqtt_connection_configuration.protocol,
                Protocol::Mqtt
            ));
            assert_eq!(cc.mqtt_connection_configuration.session_expiry_seconds, 20);
            assert!(matches!(
                cc.mqtt_connection_configuration.tls.mode,
                TlsMode::Enabled
            ));
            // NOTE: These values come from the AIO_METADATA file
            // TODO: reenable test
            // assert_eq!(cc.aio_metadata.aio_min_version, "1.0");
            // assert_eq!(cc.aio_metadata.aio_max_version, "2.0");
            // NOTE: These values come from the DIAGNOSTICS file
            // TODO: reenable test
            //assert!(matches!(cc.diagnostics.logs.level, LogLevel::Trace));
            // NOTE: These values are paths specified in the environment variable
            assert_eq!(
                cc.broker_ca_cert_trustbundle_path,
                Some(get_trust_bundle_mount_path())
            );
            assert_eq!(cc.broker_sat_path, Some(FAKE_SAT_FILE.to_string()));

            // --- Convert the ConnectorConfiguration to MqttConnectionSettings ---
            assert!(cc.to_mqtt_connection_settings("-id_suffix").is_ok());
            // TODO: validate - but need getters from MQTTCS first.
            // Or maybe that's just for unit tests and this should just make a session
        },
    );
}

// TODO: Operator deployment test
