// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

#![cfg(feature = "azure_device_registry")]

use std::collections::HashMap;
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
        .tcp_port(31883u16)
        // .tcp_port(1883u16)
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
#[ignore]
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
#[ignore]
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
#[ignore]
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
#[ignore = "This test is ignored for preference of notify test."]
async fn observe_device_update_notifications() {
    let log_identifier = "observe_device_update_notifications_network_tests-rust";
    if !setup_test(log_identifier) {
        return;
    }
    let (session, azure_device_registry_client, exit_handle) =
        initialize_client(&format!("{log_identifier}-client"));

    let test_task = tokio::task::spawn({
        async move {
            let mut observation = azure_device_registry_client
                .observe_device_update_notifications(
                    DEVICE1.to_string(),
                    ENDPOINT1.to_string(),
                    TIMEOUT,
                )
                .await
                .unwrap();
            log::info!("[{log_identifier}] Device update observation: {observation:?}",);
            let receive_notifications_task = tokio::task::spawn({
                async move {
                    log::info!("[{log_identifier}] Device update notification receiver started.");
                    let mut count = 0;
                    if let Some((device, _)) = observation.recv_notification().await {
                        count += 1;
                        // log::info!("[{log_identifier}] FIRST OBS DELETE LOG");
                        log::info!("[{log_identifier}] Device Observation expected: {device:?}");
                        assert_eq!(device.name, DEVICE1);
                    }
                    while let Some((device, _)) = observation.recv_notification().await {
                        count += 1;
                        // log::info!("[{log_identifier}] ANY OTHER OBS DELETE LOG");
                        log::info!("[{log_identifier}] Device Observation unexpected: {device:?}");
                        // if something weird happens, this should prevent an infinite loop.
                        // Note that this does prevent getting an accurate count of how many extra unexpected notifications were received
                        assert!(count < 2);
                    }
                    // only the 1 expected notifications should occur
                    assert_eq!(count, 1);
                    log::info!("[{log_identifier}] Device update notification receiver closed");
                }
            });
            let response = azure_device_registry_client
                .get_device(DEVICE1.to_string(), ENDPOINT1.to_string(), TIMEOUT)
                .await
                .unwrap();
            log::info!("[{log_identifier}] Get device to update the status: {response:?}");

            let response_during_obs = azure_device_registry_client
                .update_device_plus_endpoint_status(
                    DEVICE1.to_string(),
                    ENDPOINT1.to_string(),
                    DeviceStatus {
                        config: Some(StatusConfig {
                            version: response.specification.version,
                            error: Some(ConfigError {
                                message: Some(format!(
                                    "Random test error for observation of device update {}",
                                    Uuid::new_v4()
                                )),
                                ..ConfigError::default()
                            }),
                            ..StatusConfig::default()
                        }),
                        endpoints: HashMap::new(),
                    },
                    TIMEOUT,
                )
                .await
                .unwrap();
            log::info!(
                "[{log_identifier}] Updated device response after observation: {response_during_obs:?}",
            );

            azure_device_registry_client
                .unobserve_device_update_notifications(
                    DEVICE1.to_string(),
                    ENDPOINT1.to_string(),
                    TIMEOUT,
                )
                .await
                .unwrap();
            log::info!("[{log_identifier}] Device update unobservation: {:?}", ());

            let response_after_unobs = azure_device_registry_client
                .update_device_plus_endpoint_status(
                    DEVICE1.to_string(),
                    ENDPOINT1.to_string(),
                    DeviceStatus {
                        config: Some(StatusConfig {
                            version: None,
                            error: Some({
                                ConfigError {
                                    message: None,
                                    ..ConfigError::default()
                                }
                            }),
                            ..StatusConfig::default()
                        }),
                        endpoints: HashMap::new(),
                    },
                    TIMEOUT,
                )
                .await
                .unwrap();
            log::info!(
                "[{log_identifier}] Updated device response after unobserve: {response_after_unobs:?}",
            );
            // wait for the receive_notifications_task to finish to ensure any failed asserts are captured.
            assert!(receive_notifications_task.await.is_ok());

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
#[ignore]
async fn observe_device_notify_simpler() {
    let log_identifier = "observe_device_update_notifications_network_tests-rust";
    if !setup_test(log_identifier) {
        return;
    }
    let (session, azure_device_registry_client, exit_handle) =
        initialize_client(&format!("{log_identifier}-client"));

    let test_task = tokio::task::spawn({
        async move {
            let mut observation = azure_device_registry_client
                .observe_device_update_notifications(
                    DEVICE1.to_string(),
                    ENDPOINT1.to_string(),
                    TIMEOUT,
                )
                .await
                .unwrap();
            log::info!("[{log_identifier}] Device update observation: {observation:?}",);

            let receive_notifications_task = tokio::task::spawn({
                async move {
                    log::info!("[{log_identifier}] Device update notification receiver started.");
                    let mut count = 0;
                    let timeout_duration = Duration::from_secs(30);

                    loop {
                        tokio::select! {
                            notification_result = tokio::time::timeout(timeout_duration, observation.recv_notification()) => {
                                match notification_result {
                                    Ok(Some((device, _))) => {
                                        count += 1;
                                        log::info!("[{log_identifier}] Device Observation received #{count}: {device:?}");

                                        if count == 1 {
                                            assert_eq!(device.name, DEVICE1);
                                            log::info!("[{log_identifier}] First expected notification received");
                                        } else {
                                            log::error!("[{log_identifier}] Unexpected additional notification #{count}");
                                            break;
                                        }
                                    }
                                    Ok(None) => {
                                        log::info!("[{log_identifier}] Notification channel closed");
                                        break;
                                    }
                                    Err(_) => {
                                        log::warn!("[{log_identifier}] 30-second timeout reached while waiting for notifications");
                                        break;
                                    }
                                }
                            }
                        }
                    }

                    log::info!(
                        "[{log_identifier}] Device update notification receiver closed with count: {count}"
                    );
                    count
                }
            });

            // Get current device state
            let response = azure_device_registry_client
                .get_device(DEVICE1.to_string(), ENDPOINT1.to_string(), TIMEOUT)
                .await
                .unwrap();
            log::info!("[{log_identifier}] Get device to update the status: {response:?}");

            // First update - should generate notification
            let response_during_obs = azure_device_registry_client
                .update_device_plus_endpoint_status(
                    DEVICE1.to_string(),
                    ENDPOINT1.to_string(),
                    DeviceStatus {
                        config: Some(StatusConfig {
                            version: response.specification.version,
                            error: Some(ConfigError {
                                message: Some(format!(
                                    "Random test error for observation of device update {}",
                                    Uuid::new_v4()
                                )),
                                ..ConfigError::default()
                            }),
                            ..StatusConfig::default()
                        }),
                        endpoints: HashMap::new(),
                    },
                    TIMEOUT,
                )
                .await
                .unwrap();
            log::info!(
                "[{log_identifier}] Updated device response after observation: {response_during_obs:?}",
            );

            // unobserve as part of the test
            azure_device_registry_client
                .unobserve_device_update_notifications(
                    DEVICE1.to_string(),
                    ENDPOINT1.to_string(),
                    TIMEOUT,
                )
                .await
                .unwrap();
            log::info!("[{log_identifier}] Device update unobservation: {:?}", ());

            // Second update - should NOT generate notification (after unobserve)
            let response_after_unobs = azure_device_registry_client
                .update_device_plus_endpoint_status(
                    DEVICE1.to_string(),
                    ENDPOINT1.to_string(),
                    DeviceStatus {
                        config: Some(StatusConfig {
                            version: None,
                            error: Some({
                                ConfigError {
                                    message: None,
                                    ..ConfigError::default()
                                }
                            }),
                            ..StatusConfig::default()
                        }),
                        endpoints: HashMap::new(),
                    },
                    TIMEOUT,
                )
                .await
                .unwrap();
            log::info!(
                "[{log_identifier}] Updated device response after unobserve: {response_after_unobs:?}",
            );

            // Let the background task run its full course (30s timeout) to verify no additional notifications
            // If a second notification comes, the task will exit early with failure
            // If no second notification comes, the task will timeout after 30s with success
            log::info!(
                "[{log_identifier}] Waiting for background task to complete (up to 30s timeout)..."
            );

            // Wait for the background task to finish
            let notification_count = receive_notifications_task
                .await
                .expect("Notification receiver task failed");

            log::info!(
                "[{log_identifier}] Notification receiver task completed with count: {notification_count}"
            );

            // Always attempt cleanup unobserve, regardless of test result
            log::info!("[{log_identifier}] Performing cleanup unobserve");
            let _ = azure_device_registry_client
                .unobserve_device_update_notifications(
                    DEVICE1.to_string(),
                    ENDPOINT1.to_string(),
                    TIMEOUT,
                )
                .await;

            // Shutdown client
            assert!(azure_device_registry_client.shutdown().await.is_ok());

            // Final assertion
            assert_eq!(
                notification_count, 1,
                "Expected exactly 1 notification, got {notification_count}",
            );

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
#[ignore = "reason: This test is ignored for preference of notify test."]
async fn observe_asset_update_notifications() {
    let log_identifier = "observe_asset_update_notifications_network_tests-rust";
    if !setup_test(log_identifier) {
        return;
    }
    let (session, azure_device_registry_client, exit_handle) =
        initialize_client(&format!("{log_identifier}-client"));

    let test_task = tokio::task::spawn({
        async move {
            let mut observation = azure_device_registry_client
                .observe_asset_update_notifications(
                    DEVICE2.to_string(),
                    ENDPOINT1.to_string(),
                    ASSET_NAME1.to_string(),
                    TIMEOUT,
                )
                .await
                .unwrap();
            log::info!("[{log_identifier}] Asset update observation: {observation:?}",);
            let receive_notifications_task = tokio::task::spawn({
                async move {
                    log::info!("[{log_identifier}] Asset update notification receiver started.");
                    let mut count = 0;
                    if let Some((asset, _)) = observation.recv_notification().await {
                        count += 1;
                        log::info!("[{log_identifier}] FIRST OBS DELETE LOG");
                        log::info!("[{log_identifier}] Asset observation expected: {asset:?}");
                        assert_eq!(asset.name, ASSET_NAME1);
                    }
                    while let Some((asset, _)) = observation.recv_notification().await {
                        count += 1;
                        log::info!("[{log_identifier}] ANY OTHER OBS DELETE LOG");
                        log::info!("[{log_identifier}] Asset observation unexpected: {asset:?}");
                        // if something weird happens, this should prevent an infinite loop.
                        // Note that this does prevent getting an accurate count of how many extra unexpected notifications were received
                        assert!(count < 2);
                    }
                    // only the 1 expected notifications should occur
                    assert_eq!(count, 1);
                    log::info!("[{log_identifier}] Asset update notification receiver closed");
                }
            });

            // let receive_notifications_task = tokio::task::spawn({
            //     async move {
            //         let task_result = tokio::time::timeout(Duration::from_secs(30), async move {
            //             log::info!("[{log_identifier}] Asset Notification receiver started.");
            //             let mut count = 0;
            //             if let Some((asset, _)) = observation.recv_notification().await {
            //                 count += 1;
            //                 log::info!("[{log_identifier}] Asset Observation Expected: {asset:?}");
            //                 assert_eq!(asset.name, ASSET_NAME1);
            //             }
            //             while let Some((asset, _)) = observation.recv_notification().await {
            //                 count += 1;
            //                 log::info!(
            //                     "[{log_identifier}] Asset Observation Unexpected: {asset:?}"
            //                 );
            //                 assert!(count < 2);
            //             }
            //             assert_eq!(count, 1);
            //             log::info!("[{log_identifier}] Asset Notification receiver closed");
            //         })
            //         .await;
            //         if task_result.is_err() {
            //             log::error!(
            //                 "[{log_identifier}] Entire notification task timed out after 30 seconds"
            //             );
            //             panic!("Notification receiver task timed out");
            //         }
            //     }
            // });

            let response = azure_device_registry_client
                .get_asset(
                    DEVICE2.to_string(),
                    ENDPOINT1.to_string(),
                    ASSET_NAME1.to_string(),
                    TIMEOUT,
                )
                .await
                .unwrap();
            log::info!("[{log_identifier}] Get asset to update the status: {response:?}",);

            let response_during_obs = azure_device_registry_client
                .update_asset_status(
                    DEVICE2.to_string(),
                    ENDPOINT1.to_string(),
                    ASSET_NAME1.to_string(),
                    AssetStatus {
                        config: Some(StatusConfig {
                            version: response.specification.version,
                            error: Some(ConfigError {
                                message: Some(format!(
                                    "Random test error for observation of asset update {}",
                                    Uuid::new_v4()
                                )),
                                ..ConfigError::default()
                            }),
                            // last_transition_time: Some(time::OffsetDateTime::now_utc().to_string()),
                            ..StatusConfig::default()
                        }),
                        ..AssetStatus::default()
                    },
                    TIMEOUT,
                )
                .await
                .unwrap();
            log::info!(
                "[{log_identifier}] Updated asset response after observation: {response_during_obs:?}",
            );

            azure_device_registry_client
                .unobserve_asset_update_notifications(
                    DEVICE2.to_string(),
                    ENDPOINT1.to_string(),
                    ASSET_NAME1.to_string(),
                    TIMEOUT,
                )
                .await
                .unwrap();
            log::info!("[{log_identifier}] Asset update unobservation: {:?}", ());

            let response_after_unobs = azure_device_registry_client
                .update_asset_status(
                    DEVICE2.to_string(),
                    ENDPOINT1.to_string(),
                    ASSET_NAME1.to_string(),
                    AssetStatus {
                        config: Some(StatusConfig {
                            version: None,
                            error: Some({
                                ConfigError {
                                    message: None,
                                    ..ConfigError::default()
                                }
                            }),
                            ..StatusConfig::default()
                        }),
                        ..AssetStatus::default()
                    },
                    TIMEOUT,
                )
                .await
                .unwrap();
            log::info!(
                "[{log_identifier}] Updated asset response after unobserve: {response_after_unobs:?}",
            );
            // wait for the receive_notifications_task to finish to ensure any failed asserts are captured.
            assert!(receive_notifications_task.await.is_ok());

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
#[ignore]
async fn observe_asset_notify_simpler() {
    let log_identifier = "observe_asset_update_notifications_network_tests-rust";
    if !setup_test(log_identifier) {
        return;
    }
    let (session, azure_device_registry_client, exit_handle) =
        initialize_client(&format!("{log_identifier}-client"));

    let test_task = tokio::task::spawn({
        async move {
            let mut observation = azure_device_registry_client
                .observe_asset_update_notifications(
                    DEVICE2.to_string(),
                    ENDPOINT1.to_string(),
                    ASSET_NAME1.to_string(),
                    TIMEOUT,
                )
                .await
                .unwrap();
            log::info!("[{log_identifier}] Asset update observation: {observation:?}");

            let receive_notifications_task = tokio::task::spawn({
                async move {
                    log::info!("[{log_identifier}] Asset update notification receiver started.");
                    let mut count = 0;
                    let timeout_duration = Duration::from_secs(30);

                    loop {
                        tokio::select! {
                            notification_result = tokio::time::timeout(timeout_duration, observation.recv_notification()) => {
                                match notification_result {
                                    Ok(Some((asset, _))) => {
                                        count += 1;
                                        log::info!("[{log_identifier}] Asset Observation received #{count}: {asset:?}");

                                        if count == 1 {
                                            assert_eq!(asset.name, ASSET_NAME1);
                                            log::info!("[{log_identifier}] First expected notification received");
                                        } else {
                                            log::error!("[{log_identifier}] Unexpected additional notification #{count}");
                                            break;
                                        }
                                    }
                                    Ok(None) => {
                                        log::info!("[{log_identifier}] Notification channel closed");
                                        break;
                                    }
                                    Err(_) => {
                                        log::warn!("[{log_identifier}] 30-second timeout reached while waiting for notifications");
                                        break;
                                    }
                                }
                            }
                        }
                    }

                    log::info!(
                        "[{log_identifier}] Asset update notification receiver closed with count: {count}"
                    );
                    count
                }
            });

            // Get current asset state
            let response = azure_device_registry_client
                .get_asset(
                    DEVICE2.to_string(),
                    ENDPOINT1.to_string(),
                    ASSET_NAME1.to_string(),
                    TIMEOUT,
                )
                .await
                .unwrap();
            log::info!("[{log_identifier}] Get asset to update the status: {response:?}");

            // First update - should generate notification
            let response_during_obs = azure_device_registry_client
                .update_asset_status(
                    DEVICE2.to_string(),
                    ENDPOINT1.to_string(),
                    ASSET_NAME1.to_string(),
                    AssetStatus {
                        config: Some(StatusConfig {
                            version: response.specification.version,
                            error: Some(ConfigError {
                                message: Some(format!(
                                    "Random test error for observation of asset update {}",
                                    Uuid::new_v4()
                                )),
                                ..ConfigError::default()
                            }),
                            ..StatusConfig::default()
                        }),
                        ..AssetStatus::default()
                    },
                    TIMEOUT,
                )
                .await
                .unwrap();
            log::info!(
                "[{log_identifier}] Updated asset response during observation: {response_during_obs:?}"
            );

            // unobserve as part of the test
            azure_device_registry_client
                .unobserve_asset_update_notifications(
                    DEVICE2.to_string(),
                    ENDPOINT1.to_string(),
                    ASSET_NAME1.to_string(),
                    TIMEOUT,
                )
                .await
                .unwrap();
            log::info!("[{log_identifier}] Asset update unobservation completed");

            // Second update - should NOT generate notification (after unobserve)
            let response_after_unobs = azure_device_registry_client
                .update_asset_status(
                    DEVICE2.to_string(),
                    ENDPOINT1.to_string(),
                    ASSET_NAME1.to_string(),
                    AssetStatus {
                        config: Some(StatusConfig {
                            version: None,
                            error: Some({
                                ConfigError {
                                    message: Some(format!(
                                        "Second update after unobserve {}",
                                        Uuid::new_v4()
                                    )),
                                    ..ConfigError::default()
                                }
                            }),
                            ..StatusConfig::default()
                        }),
                        ..AssetStatus::default()
                    },
                    TIMEOUT,
                )
                .await
                .unwrap();
            log::info!(
                "[{log_identifier}] Updated asset response after unobserve: {response_after_unobs:?}"
            );

            // Let the background task run its full course (30s timeout) to verify no additional notifications
            // If a second notification comes, the task will exit early with failure
            // If no second notification comes, the task will timeout after 30s with success
            log::info!(
                "[{log_identifier}] Waiting for background task to complete (up to 30s timeout)..."
            );

            // Wait for the background task to finish
            let notification_count = receive_notifications_task
                .await
                .expect("Notification receiver task failed");

            log::info!(
                "[{log_identifier}] Notification receiver task completed with count: {notification_count}"
            );

            // Always attempt cleanup unobserve, regardless of test result
            log::info!("[{log_identifier}] Performing cleanup unobserve");
            let _ = azure_device_registry_client
                .unobserve_asset_update_notifications(
                    DEVICE2.to_string(),
                    ENDPOINT1.to_string(),
                    ASSET_NAME1.to_string(),
                    TIMEOUT,
                )
                .await;

            // Shutdown client
            assert!(azure_device_registry_client.shutdown().await.is_ok());

            // Final assertion
            assert_eq!(
                notification_count, 1,
                "Expected exactly 1 notification, got {notification_count}",
            );

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
#[ignore]
async fn observe_asset_notify_simpler2() {
    let log_identifier = "observe_asset_update_notifications_network_tests-rust";
    if !setup_test(log_identifier) {
        return;
    }
    let (session, azure_device_registry_client, exit_handle) =
        initialize_client(&format!("{log_identifier}-client"));

    let test_task = tokio::task::spawn({
        async move {
            let mut observation = azure_device_registry_client
                .observe_asset_update_notifications(
                    DEVICE2.to_string(),
                    ENDPOINT1.to_string(),
                    ASSET_NAME1.to_string(),
                    TIMEOUT,
                )
                .await
                .unwrap();
            log::info!("[{log_identifier}] Asset update observation: {observation:?}");

            let first_notification_received = Arc::new(Notify::new());
            let cleanup_notify = Arc::new(Notify::new());

            let receive_notifications_task = tokio::task::spawn({
                let first_notification_received = first_notification_received.clone();
                let cleanup_notify = cleanup_notify.clone();

                async move {
                    log::info!("[{log_identifier}] Asset update notification receiver started.");
                    let mut count = 0;
                    let mut first_notification_sent = false;
                    let timeout_duration = Duration::from_secs(30);

                    loop {
                        tokio::select! {
                            notification_result = tokio::time::timeout(timeout_duration, observation.recv_notification()) => {
                                match notification_result {
                                    Ok(Some((asset, _))) => {
                                        count += 1;
                                        log::info!("[{log_identifier}] Asset Observation received #{count}: {asset:?}");

                                        if count == 1 {
                                            assert_eq!(asset.name, ASSET_NAME1);
                                            log::info!("[{log_identifier}] First expected notification received");
                                            // Signal that we got the first notification
                                            if !first_notification_sent {
                                                first_notification_received.notify_one();
                                                first_notification_sent = true;
                                            }
                                        } else {
                                            log::error!("[{log_identifier}] Unexpected additional notification #{count}");
                                            break;
                                        }
                                    }
                                    Ok(None) => {
                                        log::info!("[{log_identifier}] Notification channel closed");
                                        break;
                                    }
                                    Err(_) => {
                                        log::warn!("[{log_identifier}] 30-second timeout reached while waiting for notifications");
                                        break;
                                    }
                                }
                            }
                            () = cleanup_notify.notified() => {
                                log::info!("[{log_identifier}] Cleanup signal received, stopping notification receiver");
                                break;
                            }
                        }
                    }

                    // Always signal for timeout cases
                    if !first_notification_sent {
                        first_notification_received.notify_one();
                    }

                    log::info!(
                        "[{log_identifier}] Asset update notification receiver closed with count: {count}"
                    );
                    count
                }
            });

            // Get current asset state
            let response = azure_device_registry_client
                .get_asset(
                    DEVICE2.to_string(),
                    ENDPOINT1.to_string(),
                    ASSET_NAME1.to_string(),
                    TIMEOUT,
                )
                .await
                .unwrap();
            log::info!("[{log_identifier}] Get asset to update the status: {response:?}");

            // First update - should generate notification
            let response_during_obs = azure_device_registry_client
                .update_asset_status(
                    DEVICE2.to_string(),
                    ENDPOINT1.to_string(),
                    ASSET_NAME1.to_string(),
                    AssetStatus {
                        config: Some(StatusConfig {
                            version: response.specification.version,
                            error: Some(ConfigError {
                                message: Some(format!(
                                    "Random test error for observation of asset update {}",
                                    Uuid::new_v4()
                                )),
                                ..ConfigError::default()
                            }),
                            ..StatusConfig::default()
                        }),
                        ..AssetStatus::default()
                    },
                    TIMEOUT,
                )
                .await
                .unwrap();
            log::info!(
                "[{log_identifier}] Updated asset response during observation: {response_during_obs:?}"
            );

            // Wait for first notification (or timeout from background task)
            log::info!("[{log_identifier}] Waiting for first notification to be received...");
            first_notification_received.notified().await;
            log::info!(
                "[{log_identifier}] First notification signal received, proceeding with unobserve"
            );

            // Unobserve - this is part of the test
            azure_device_registry_client
                .unobserve_asset_update_notifications(
                    DEVICE2.to_string(),
                    ENDPOINT1.to_string(),
                    ASSET_NAME1.to_string(),
                    TIMEOUT,
                )
                .await
                .unwrap();
            log::info!("[{log_identifier}] Asset update unobservation completed");

            // Second update - should NOT generate notification (after unobserve)
            let response_after_unobs = azure_device_registry_client
                .update_asset_status(
                    DEVICE2.to_string(),
                    ENDPOINT1.to_string(),
                    ASSET_NAME1.to_string(),
                    AssetStatus {
                        config: Some(StatusConfig {
                            version: None,
                            error: Some({
                                ConfigError {
                                    message: Some(format!(
                                        "Second update after unobserve {}",
                                        Uuid::new_v4()
                                    )),
                                    ..ConfigError::default()
                                }
                            }),
                            ..StatusConfig::default()
                        }),
                        ..AssetStatus::default()
                    },
                    TIMEOUT,
                )
                .await
                .unwrap();
            log::info!(
                "[{log_identifier}] Updated asset response after unobserve: {response_after_unobs:?}"
            );

            // Signal cleanup to the background task to stop waiting
            cleanup_notify.notify_one();
            log::info!("[{log_identifier}] Cleanup signal sent to background task");

            // Wait for the background task to finish
            let notification_count = receive_notifications_task
                .await
                .expect("Notification receiver task failed"); // TODO Remove

            log::info!(
                "[{log_identifier}] Notification receiver task completed with count: {notification_count}"
            );

            // Always attempt cleanup unobserve, regardless of test result
            log::info!("[{log_identifier}] Performing cleanup unobserve");
            let _ = azure_device_registry_client
                .unobserve_asset_update_notifications(
                    DEVICE2.to_string(),
                    ENDPOINT1.to_string(),
                    ASSET_NAME1.to_string(),
                    TIMEOUT,
                )
                .await;

            // Shutdown client
            assert!(azure_device_registry_client.shutdown().await.is_ok());

            // Final assertion
            assert_eq!(
                notification_count, 1,
                "Expected exactly 1 notification, got {notification_count}",
            );

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
#[ignore]
async fn observe_asset_notify_no_loop_timeout() {
    let log_identifier = "observe_asset_update_notifications_network_tests-rust";
    if !setup_test(log_identifier) {
        return;
    }
    let (session, azure_device_registry_client, exit_handle) =
        initialize_client(&format!("{log_identifier}-client"));

    let test_task = tokio::task::spawn({
        async move {
            let mut observation = azure_device_registry_client
                .observe_asset_update_notifications(
                    DEVICE2.to_string(),
                    ENDPOINT1.to_string(),
                    ASSET_NAME1.to_string(),
                    TIMEOUT,
                )
                .await
                .unwrap();
            log::info!("[{log_identifier}] Asset update observation: {observation:?}",);

            let first_notification_notify = Arc::new(Notify::new());

            let receive_notifications_task = tokio::task::spawn({
                let first_notification_notify = first_notification_notify.clone();

                async move {
                    log::info!("[{log_identifier}] Asset update notification receiver started.");
                    let mut count = 0;
                    let mut first_notification_sent = false;
                    loop {
                        let notification_result = observation.recv_notification().await;
                        if let Some((asset, _)) = notification_result {
                            count += 1;
                            log::info!("[{log_identifier}] Asset Observation received: {asset:?}");

                            if count == 1 {
                                // Signal that we got the first notification
                                if !first_notification_sent {
                                    first_notification_notify.notify_one();
                                    first_notification_sent = true;
                                }
                                assert_eq!(asset.name, ASSET_NAME1);
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
                            // Channel closed or timeout/error
                            log::warn!(
                                "[{log_identifier}] Timeout or error while waiting for notifications"
                            );
                            break;
                        }
                    }

                    log::info!(
                        "[{log_identifier}] Asset update notification receiver closed with count: {count}"
                    );
                    count
                }
            });

            let response = azure_device_registry_client
                .get_asset(
                    DEVICE2.to_string(),
                    ENDPOINT1.to_string(),
                    ASSET_NAME1.to_string(),
                    TIMEOUT,
                )
                .await
                .unwrap();
            log::info!("[{log_identifier}] Get asset to update the status: {response:?}",);

            let response_during_obs = azure_device_registry_client
                .update_asset_status(
                    DEVICE2.to_string(),
                    ENDPOINT1.to_string(),
                    ASSET_NAME1.to_string(),
                    AssetStatus {
                        config: Some(StatusConfig {
                            version: response.specification.version,
                            error: Some(ConfigError {
                                message: Some(format!(
                                    "Random test error for observation of asset update {}",
                                    Uuid::new_v4()
                                )),
                                ..ConfigError::default()
                            }),
                            ..StatusConfig::default()
                        }),
                        ..AssetStatus::default()
                    },
                    TIMEOUT,
                )
                .await
                .unwrap();
            log::info!(
                "[{log_identifier}] Updated asset response after observation: {response_during_obs:?}",
            );

            // Wait for first notification with timeout (e.g., 30 seconds)
            let notification_timeout = Duration::from_secs(30);
            let notified_future = first_notification_notify.notified();
            let did_receive_1_notification_or_timeout =
                tokio::time::timeout(notification_timeout, notified_future).await;

            azure_device_registry_client
                .unobserve_asset_update_notifications(
                    DEVICE2.to_string(),
                    ENDPOINT1.to_string(),
                    ASSET_NAME1.to_string(),
                    TIMEOUT,
                )
                .await
                .unwrap();
            log::info!("[{log_identifier}] Asset update unobservation: {:?}", ());

            if did_receive_1_notification_or_timeout.is_ok() {
                log::info!("[{log_identifier}] First notification received successfully");

                let response_after_unobs = azure_device_registry_client
                    .update_asset_status(
                        DEVICE2.to_string(),
                        ENDPOINT1.to_string(),
                        ASSET_NAME1.to_string(),
                        AssetStatus {
                            config: Some(StatusConfig {
                                version: None,
                                error: Some({
                                    ConfigError {
                                        message: None,
                                        ..ConfigError::default()
                                    }
                                }),
                                ..StatusConfig::default()
                            }),
                            ..AssetStatus::default()
                        },
                        TIMEOUT,
                    )
                    .await
                    .unwrap();
                log::info!(
                    "[{log_identifier}] Updated asset response after unobserve: {response_after_unobs:?}",
                );
            }

            let notification_count = match receive_notifications_task.await {
                Ok(count) => {
                    log::info!(
                        "[{log_identifier}] Notification receiver task completed with count: {count}"
                    );
                    count
                }
                Err(e) => {
                    log::error!("[{log_identifier}] Notification receiver task failed: {e:?}");
                    panic!("Notification receiver task failed");
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
async fn observe_asset_notify_spec() {
    let log_identifier = "observe_asset_update_notifications_network_tests-rust";
    if !setup_test(log_identifier) {
        return;
    }
    let (session, azure_device_registry_client, exit_handle) =
        initialize_client(&format!("{log_identifier}-client"));

    let test_task = tokio::task::spawn({
        async move {
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
                let first_notification_notify = first_notification_notify.clone();

                async move {
                    log::info!("[{log_identifier}] Asset update notification receiver started.");
                    let mut count = 0;
                    let mut first_notification_sent = false;
                    loop {
                        let notification_result = observation.recv_notification().await;
                        if let Some((asset, _)) = notification_result {
                            count += 1;
                            log::info!("[{log_identifier}] Asset Observation received: {asset:?}");

                            if count == 1 {
                                // Signal that we got the first notification
                                if !first_notification_sent {
                                    first_notification_notify.notify_one();
                                    first_notification_sent = true;
                                }
                                assert_eq!(asset.name, ASSET_NAME3);
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
                            // Channel closed or timeout/error
                            log::warn!(
                                "[{log_identifier}] Timeout or error while waiting for notifications"
                            );
                            break;
                        }
                    }

                    log::info!(
                        "[{log_identifier}] Asset update notification receiver closed with count: {count}"
                    );
                    count
                }
            });

            let response = azure_device_registry_client
                .get_asset(
                    DEVICE3.to_string(),
                    ENDPOINT3.to_string(),
                    ASSET_NAME3.to_string(),
                    TIMEOUT,
                )
                .await
                .unwrap();
            log::info!("[{log_identifier}] Get asset to update the status: {response:?}",);

            patch_asset_specification(
                ASSET_NAME3,
                format!(
                    "Patch specification update to trigger notification after observe {}",
                    Uuid::new_v4()
                )
                .as_str(),
            );

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
                    log::error!("[{log_identifier}] Notification receiver task failed: {e:?}");
                    panic!("Notification receiver task failed");
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
async fn observe_device_notify_spec() {
    let log_identifier = "observe_device_update_notifications_network_tests-rust";
    if !setup_test(log_identifier) {
        return;
    }
    let (session, azure_device_registry_client, exit_handle) =
        initialize_client(&format!("{log_identifier}-client"));

    let test_task = tokio::task::spawn({
        async move {
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

                async move {
                    log::info!("[{log_identifier}] Device update notification receiver started.");
                    let mut count = 0;
                    let mut first_notification_sent = false;
                    loop {
                        let notification_result = observation.recv_notification().await;
                        if let Some((device, _)) = notification_result {
                            count += 1;
                            log::info!(
                                "[{log_identifier}] Device Observation received: {device:?}"
                            );

                            if count == 1 {
                                // Signal that we got the first notification
                                if !first_notification_sent {
                                    first_notification_notify.notify_one();
                                    first_notification_sent = true;
                                }
                                assert_eq!(device.name, DEVICE3);
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
                            // Channel closed or timeout/error
                            log::warn!(
                                "[{log_identifier}] Timeout or error while waiting for notifications"
                            );
                            break;
                        }
                    }

                    log::info!(
                        "[{log_identifier}] Device update notification receiver closed with count: {count}"
                    );
                    count
                }
            });

            let response = azure_device_registry_client
                .get_device(DEVICE3.to_string(), ENDPOINT3.to_string(), TIMEOUT)
                .await
                .unwrap();
            log::info!("[{log_identifier}] Get device to update the status: {response:?}",);

            patch_device_specification(
                DEVICE3,
                format!(
                    "Patch specification update to trigger notification after observe {}",
                    Uuid::new_v4()
                )
                .as_str(),
            );

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
                    log::error!("[{log_identifier}] Notification receiver task failed: {e:?}");
                    panic!("Notification receiver task failed");
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

fn patch_asset_specification(asset_name: &str, description: &str) {
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
        println!("Asset patched successfully!");
        println!("Output: {}", String::from_utf8_lossy(&output.stdout));
    } else {
        eprintln!("Patch failed: {}", String::from_utf8_lossy(&output.stderr));
    }
}

fn patch_device_specification(device_name: &str, manufacturer: &str) {
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
        println!("Device patched successfully!");
        println!("Output: {}", String::from_utf8_lossy(&output.stdout));
    } else {
        eprintln!("Patch failed: {}", String::from_utf8_lossy(&output.stderr));
    }
}
