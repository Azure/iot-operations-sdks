// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

#![cfg(feature = "azure_device_registry")]

use std::process::Command;
use std::sync::Arc;
use std::{env, time::Duration};

use azure_iot_operations_mqtt::MqttConnectionSettingsBuilder;
use azure_iot_operations_mqtt::session::{
    Session, SessionExitHandle, SessionManagedClient, SessionOptionsBuilder,
};
use azure_iot_operations_protocol::application::ApplicationContextBuilder;
use env_logger::Builder;
use tokio::sync::Notify;
use uuid::Uuid;

use azure_iot_operations_services::azure_device_registry::models::{AssetStatus, DeviceStatus};
use azure_iot_operations_services::azure_device_registry::{self, ConfigError, StatusConfig};

const DEVICE1: &str = "my-thermostat";
const DEVICE2: &str = "test-thermostat";
const ENDPOINT1: &str = "my-rest-endpoint";
const ENDPOINT2: &str = "my-coap-endpoint";
const ASSET_NAME1: &str = "my-rest-thermostat-asset";
const ASSET_NAME2: &str = "my-coap-thermostat-asset";
// Unique names to avoid conflicts for specificaion updates
const ASSET_NAME3: &str = "unique-rest-thermostat-asset";
const ENDPOINT3: &str = "unique-endpoint";
const DEVICE3: &str = "unique-thermostat";
#[allow(dead_code)]
const ENDPOINT_TYPE: &str = "rest-thermostat";
const TYPE: &str = "thermostat";
const TIMEOUT: Duration = Duration::from_secs(10);

// Test Scenarios:
// get device
// update status of device
// get asset
// update status of asset
// observe device update notifications
// observe asset update notifications

fn setup_test(test_name: &str) -> bool {
    let _ = Builder::new()
        .filter_level(log::LevelFilter::Info)
        .format_timestamp(None)
        .filter_module("rumqttc", log::LevelFilter::Warn)
        .filter_module("azure_iot_operations", log::LevelFilter::Debug)
        .try_init();

    if env::var("ENABLE_NETWORK_TESTS").is_err() {
        log::warn!("Test {test_name} is skipped. Set ENABLE_NETWORK_TESTS to run.");
        return false;
    }

    true
}

fn initialize_client(
    client_id: &str,
) -> (
    Session,
    azure_device_registry::Client<SessionManagedClient>,
    SessionExitHandle,
) {
    let connection_settings = MqttConnectionSettingsBuilder::default()
        .client_id(client_id)
        .hostname("localhost")
        // .tcp_port(31883u16)
        .tcp_port(1883u16)
        .keep_alive(Duration::from_secs(5))
        .use_tls(false)
        .build()
        .unwrap();

    let session_options = SessionOptionsBuilder::default()
        .connection_settings(connection_settings)
        .build()
        .unwrap();

    let session = Session::new(session_options).unwrap();
    let application_context = ApplicationContextBuilder::default().build().unwrap();

    let azure_device_registry_client = azure_device_registry::Client::new(
        application_context,
        session.create_managed_client(),
        azure_device_registry::ClientOptionsBuilder::default()
            .build()
            .unwrap(),
    )
    .unwrap();
    let exit_handle: SessionExitHandle = session.create_exit_handle();
    (session, azure_device_registry_client, exit_handle)
}

#[tokio::test]
#[ignore = "This test is ignored as getting serialization error."]
async fn get_device() {
    let log_identifier = "get_device_network_tests-rust";
    if !setup_test(log_identifier) {
        return;
    }
    let (session, azure_device_registry_client, exit_handle) =
        initialize_client(&format!("{log_identifier}-client"));

    let test_task = tokio::task::spawn({
        async move {
            let response = azure_device_registry_client
                .get_device(DEVICE1.to_string(), ENDPOINT1.to_string(), TIMEOUT)
                .await
                .unwrap();
            log::info!("[{log_identifier}] Get Device: {response:?}");

            assert_eq!(response.name, DEVICE1);
            assert_eq!(response.specification.attributes["deviceId"], DEVICE1);
            assert_eq!(response.specification.attributes["deviceType"], TYPE);
            // Shutdown adr client and underlying resources
            assert!(azure_device_registry_client.shutdown().await.is_ok());

            exit_handle.try_exit().await.unwrap();
        }
    });

    assert!(
        tokio::try_join!(
            async move { test_task.await.map_err(|e| { e.to_string() }) },
            async move { session.run().await.map_err(|e| { e.to_string() }) }
        )
        .is_ok()
    );
}

#[tokio::test]
// #[ignore]
async fn update_device_plus_endpoint_status() {
    let log_identifier = "update_device_plus_endpoint_status_network_tests-rust";
    if !setup_test(log_identifier) {
        return;
    }
    let (session, azure_device_registry_client, exit_handle) =
        initialize_client(&format!("{log_identifier}-client"));

    let message = format!(
        "Random test error for device plus endpoint update {}",
        Uuid::new_v4()
    );
    let updated_status = DeviceStatus {
        config: Some(StatusConfig {
            error: Some(ConfigError {
                message: Some(message.clone()),
                ..ConfigError::default()
            }),
            ..StatusConfig::default()
        }),
        ..DeviceStatus::default()
    };
    let test_task = tokio::task::spawn({
        async move {
            let updated_device = azure_device_registry_client
                .update_device_plus_endpoint_status(
                    DEVICE2.to_string(),
                    ENDPOINT2.to_string(),
                    updated_status.clone(),
                    TIMEOUT,
                )
                .await
                .unwrap();
            log::info!("[{log_identifier}] Updated Response Device: {updated_device:?}",);

            assert_eq!(updated_device.name, DEVICE2);
            assert_eq!(updated_device.specification.attributes["deviceId"], DEVICE2);
            assert_eq!(updated_device.status.unwrap(), updated_status);
            // Shutdown adr client and underlying resources
            assert!(azure_device_registry_client.shutdown().await.is_ok());

            exit_handle.try_exit().await.unwrap();
        }
    });

    assert!(
        tokio::try_join!(
            async move { test_task.await.map_err(|e| { e.to_string() }) },
            async move { session.run().await.map_err(|e| { e.to_string() }) }
        )
        .is_ok()
    );
}

#[tokio::test]
// #[ignore]
async fn get_asset() {
    let log_identifier = "get_asset_network_tests-rust";
    if !setup_test(log_identifier) {
        return;
    }
    let (session, azure_device_registry_client, exit_handle) =
        initialize_client(&format!("{log_identifier}-client"));

    let test_task = tokio::task::spawn({
        async move {
            let asset = azure_device_registry_client
                .get_asset(
                    DEVICE1.to_string(),
                    ENDPOINT1.to_string(),
                    ASSET_NAME1.to_string(),
                    TIMEOUT,
                )
                .await
                .unwrap();
            log::info!("[{log_identifier}] Response: {asset:?}",);

            assert_eq!(asset.name, ASSET_NAME1);
            assert_eq!(asset.specification.attributes["assetId"], ASSET_NAME1);
            assert_eq!(asset.specification.attributes["assetType"], TYPE);
            // Shutdown adr client and underlying resources
            assert!(azure_device_registry_client.shutdown().await.is_ok());

            exit_handle.try_exit().await.unwrap();
        }
    });

    assert!(
        tokio::try_join!(
            async move { test_task.await.map_err(|e| { e.to_string() }) },
            async move { session.run().await.map_err(|e| { e.to_string() }) }
        )
        .is_ok()
    );
}

#[tokio::test]
// #[ignore]
async fn update_asset_status() {
    let log_identifier = "update_asset_status_network_tests-rust";
    if !setup_test(log_identifier) {
        return;
    }
    let (session, azure_device_registry_client, exit_handle) =
        initialize_client(&format!("{log_identifier}-client"));

    let message = format!("Random test error for asset update {}", Uuid::new_v4());
    let updated_status = AssetStatus {
        config: Some(StatusConfig {
            error: Some(ConfigError {
                message: Some(message.clone()),
                ..ConfigError::default()
            }),
            ..StatusConfig::default()
        }),
        ..AssetStatus::default()
    };

    let test_task = tokio::task::spawn({
        async move {
            let updated_asset = azure_device_registry_client
                .update_asset_status(
                    DEVICE2.to_string(),
                    ENDPOINT2.to_string(),
                    ASSET_NAME2.to_string(),
                    updated_status.clone(),
                    TIMEOUT,
                )
                .await
                .unwrap();
            log::info!("[{log_identifier}] Updated Response Asset: {updated_asset:?}");

            assert_eq!(updated_asset.name, ASSET_NAME2);
            assert_eq!(
                updated_asset.specification.attributes["assetId"],
                ASSET_NAME2
            );
            assert_eq!(updated_asset.status.unwrap(), updated_status);

            // Shutdown adr client and underlying resources
            assert!(azure_device_registry_client.shutdown().await.is_ok());

            exit_handle.try_exit().await.unwrap();
        }
    });

    assert!(
        tokio::try_join!(
            async move { test_task.await.map_err(|e| { e.to_string() }) },
            async move { session.run().await.map_err(|e| { e.to_string() }) }
        )
        .is_ok()
    );
}

#[tokio::test]
// #[ignore]
async fn observe_asset_update_notifications() {
    let log_identifier = "observe_asset_update_notifications_network_tests-rust";
    if !setup_test(log_identifier) {
        return;
    }
    let (session, azure_device_registry_client, exit_handle) =
        initialize_client(&format!("{log_identifier}-client"));

    let test_task = tokio::task::spawn({
        async move {
            // This unobserve is to ensure we start the test with a clean state
            azure_device_registry_client
                .unobserve_asset_update_notifications(
                    DEVICE3.to_string(),
                    ENDPOINT3.to_string(),
                    ASSET_NAME3.to_string(),
                    TIMEOUT,
                )
                .await
                .unwrap();

            let update_desc = format!(
                "Patch specification update to NOT trigger notification after unobserve {}",
                Uuid::new_v4()
            );
            let mut observation = azure_device_registry_client
                .observe_asset_update_notifications(
                    DEVICE3.to_string(),
                    ENDPOINT3.to_string(),
                    ASSET_NAME3.to_string(),
                    TIMEOUT,
                )
                .await
                .unwrap();
            log::info!("[{log_identifier}] Asset update observation: {observation:?}",);

            let first_notification_notify = Arc::new(Notify::new());

            let receive_notifications_task = tokio::task::spawn({
                let description_for_task = update_desc.clone();
                let first_notification_notify = first_notification_notify.clone();

                async move {
                    log::info!("[{log_identifier}] Asset update notification receiver started.");
                    let mut count = 0;
                    loop {
                        let notification_result = observation.recv_notification().await;
                        if let Some((asset, _)) = notification_result {
                            count += 1;
                            log::info!("[{log_identifier}] Asset Observation received: {asset:?}");

                            if count == 1 {
                                // Signal that we got the first notification
                                first_notification_notify.notify_one();
                                // TODO Assert on something changed
                                assert_eq!(asset.name, ASSET_NAME3);
                                assert_eq!(
                                    asset.specification.description.unwrap(),
                                    description_for_task
                                );
                            } else {
                                log::info!(
                                    "[{log_identifier}] Asset Observation unexpected: {asset:?}"
                                );
                                // Should not receive more than 1 notification
                                assert!(
                                    count < 2,
                                    "Received unexpected additional asset observation notification"
                                );
                            }
                        } else {
                            log::info!("[{log_identifier}] Receiving no more notifications.");
                            break;
                        }
                    }

                    log::info!(
                        "[{log_identifier}] Asset update notification receiver closed with count: {count}"
                    );
                    count
                }
            });

            match patch_asset_specification(ASSET_NAME3, &update_desc) {
                Ok(()) => log::info!("Asset patched successfully"),
                Err(e) => {
                    log::error!("Failed to patch asset specification: {}", e);
                    panic!("Failed to patch asset specification: {}", e);
                }
            }

            // Wait for first notification with timeout (e.g., 30 seconds)
            let notification_timeout = Duration::from_secs(30);
            let notified_future = first_notification_notify.notified();
            let did_receive_1_notification_or_timeout =
                tokio::time::timeout(notification_timeout, notified_future).await;

            azure_device_registry_client
                .unobserve_asset_update_notifications(
                    DEVICE3.to_string(),
                    ENDPOINT3.to_string(),
                    ASSET_NAME3.to_string(),
                    TIMEOUT,
                )
                .await
                .unwrap();
            log::info!("[{log_identifier}] Asset update unobservation: {:?}", ());

            if did_receive_1_notification_or_timeout.is_ok() {
                log::info!("[{log_identifier}] First notification received successfully");
            }

            patch_asset_specification(
                ASSET_NAME3,
                format!(
                    "Patch specification update to NOT trigger notification after unobserve {}",
                    Uuid::new_v4()
                )
                .as_str(),
            );

            let notification_count = match receive_notifications_task.await {
                Ok(count) => {
                    log::info!(
                        "[{log_identifier}] Notification receiver task completed with count: {count}"
                    );
                    count
                }
                Err(e) => {
                    log::error!(
                        "[{log_identifier}] Notification receiver task failed due to unexpected counts or mismatch notification: {e:?}"
                    );
                    panic!(
                        "Notification receiver task failed due to unexpected counts or mismatch notification"
                    );
                }
            };

            // Verify we got exactly 1 notification (only from the first update, not the second)
            assert_eq!(
                notification_count, 1,
                "Expected exactly 1 notification, got {notification_count}",
            );

            // Shutdown adr client and underlying resources
            assert!(azure_device_registry_client.shutdown().await.is_ok());
            exit_handle.try_exit().await.unwrap();
        }
    });
    assert!(
        tokio::try_join!(
            async move { test_task.await.map_err(|e| { e.to_string() }) },
            async move { session.run().await.map_err(|e| { e.to_string() }) }
        )
        .is_ok()
    );
}

#[tokio::test]
#[ignore = "This test is ignored as getting serialization error."]
async fn observe_device_update_notifications() {
    let log_identifier = "observe_device_update_notifications_network_tests-rust";
    if !setup_test(log_identifier) {
        return;
    }
    let (session, azure_device_registry_client, exit_handle) =
        initialize_client(&format!("{log_identifier}-client"));

    let test_task = tokio::task::spawn({
        async move {
            // This unobserve is to ensure we start the test with a clean state
            azure_device_registry_client
                .unobserve_device_update_notifications(
                    DEVICE3.to_string(),
                    ENDPOINT3.to_string(),
                    TIMEOUT,
                )
                .await
                .unwrap();

            let update_manu = format!(
                "Patch specification update to NOT trigger notification after unobserve {}",
                Uuid::new_v4()
            );

            let mut observation = azure_device_registry_client
                .observe_device_update_notifications(
                    DEVICE3.to_string(),
                    ENDPOINT3.to_string(),
                    TIMEOUT,
                )
                .await
                .unwrap();
            log::info!("[{log_identifier}] Device update observation: {observation:?}",);

            let first_notification_notify = Arc::new(Notify::new());

            let receive_notifications_task = tokio::task::spawn({
                let first_notification_notify = first_notification_notify.clone();
                let update_manu_for_task = update_manu.clone();
                async move {
                    log::info!("[{log_identifier}] Device update notification receiver started.");
                    let mut count = 0;
                    loop {
                        let notification_result = observation.recv_notification().await;
                        if let Some((device, _)) = notification_result {
                            count += 1;
                            log::info!(
                                "[{log_identifier}] Device Observation received: {device:?}"
                            );

                            if count == 1 {
                                // Signal that we got the first notification
                                first_notification_notify.notify_one();
                                // TODO Assert on something changed
                                assert_eq!(device.name, DEVICE3);
                                assert_eq!(
                                    device.specification.manufacturer.unwrap(),
                                    update_manu_for_task
                                );
                            } else {
                                log::info!(
                                    "[{log_identifier}] Device Observation unexpected: {device:?}"
                                );
                                // Should not receive more than 1 notification
                                assert!(
                                    count < 2,
                                    "Received unexpected additional device observation notification"
                                );
                            }
                        } else {
                            log::info!("[{log_identifier}] Receiving no more notifications.");
                            break;
                        }
                    }

                    log::info!(
                        "[{log_identifier}] Device update notification receiver closed with count: {count}"
                    );
                    count
                }
            });

            match patch_device_specification(DEVICE3, &update_manu) {
                Ok(()) => log::info!("Device patched successfully"),
                Err(e) => {
                    log::error!("Failed to patch device specification: {}", e);
                    panic!("Failed to patch device specification: {}", e);
                }
            }

            // Wait for first notification with timeout (e.g., 30 seconds)
            let notification_timeout = Duration::from_secs(30);
            let notified_future = first_notification_notify.notified();
            let did_receive_1_notification_or_timeout =
                tokio::time::timeout(notification_timeout, notified_future).await;

            azure_device_registry_client
                .unobserve_device_update_notifications(
                    DEVICE3.to_string(),
                    ENDPOINT3.to_string(),
                    TIMEOUT,
                )
                .await
                .unwrap();
            log::info!("[{log_identifier}] Device update unobservation: {:?}", ());

            if did_receive_1_notification_or_timeout.is_ok() {
                log::info!("[{log_identifier}] First notification received successfully");
            }

            patch_device_specification(
                DEVICE3,
                format!(
                    "Patch specification update to NOT trigger notification after unobserve {}",
                    Uuid::new_v4()
                )
                .as_str(),
            );

            let notification_count = match receive_notifications_task.await {
                Ok(count) => {
                    log::info!(
                        "[{log_identifier}] Notification receiver task completed with count: {count}"
                    );
                    count
                }
                Err(e) => {
                    log::error!(
                        "[{log_identifier}] Notification receiver task failed due to unexpected counts or mismatch notification: {e:?}"
                    );
                    panic!(
                        "Notification receiver task failed due to unexpected counts or mismatch notification"
                    );
                }
            };

            // Verify we got exactly 1 notification (only from the first update, not the second)
            assert_eq!(
                notification_count, 1,
                "Expected exactly 1 notification, got {notification_count}",
            );

            // Shutdown adr client and underlying resources
            assert!(azure_device_registry_client.shutdown().await.is_ok());
            exit_handle.try_exit().await.unwrap();
        }
    });
    assert!(
        tokio::try_join!(
            async move { test_task.await.map_err(|e| { e.to_string() }) },
            async move { session.run().await.map_err(|e| { e.to_string() }) }
        )
        .is_ok()
    );
}

fn patch_asset_specification(
    asset_name: &str,
    description: &str,
) -> Result<(), Box<dyn std::error::Error>> {
    let patch_json = format!(r#"{{"spec":{{"description":"{description}"}}}}"#);

    let output = Command::new("kubectl")
        .args([
            "patch",
            "assets.namespaces.deviceregistry.microsoft.com",
            asset_name,
            "-n",
            "azure-iot-operations",
            "--type",
            "merge",
            "--patch",
            &patch_json,
        ])
        .output()
        .expect("Failed to execute kubectl patch command");

    if output.status.success() {
        log::info!("Asset patched successfully!");
        log::info!("Output: {}", String::from_utf8_lossy(&output.stdout));
        Ok(())
    } else {
        let error_msg = String::from_utf8_lossy(&output.stderr);
        Err(format!("Patch failed: {}", error_msg).into())
    }
}

fn patch_device_specification(
    device_name: &str,
    manufacturer: &str,
) -> Result<(), Box<dyn std::error::Error>> {
    let patch_json = format!(r#"{{"spec":{{"manufacturer":"{manufacturer}"}}}}"#);

    let output = Command::new("kubectl")
        .args([
            "patch",
            "devices.namespaces.deviceregistry.microsoft.com",
            device_name,
            "-n",
            "azure-iot-operations",
            "--type",
            "merge",
            "--patch",
            &patch_json,
        ])
        .output()
        .expect("Failed to execute kubectl patch command");

    if output.status.success() {
        log::info!("Device patched successfully!");
        log::info!("Output: {}", String::from_utf8_lossy(&output.stdout));
        Ok(())
    } else {
        let error_msg = String::from_utf8_lossy(&output.stderr);
        Err(format!("Patch failed: {}", error_msg).into())
    }
}
