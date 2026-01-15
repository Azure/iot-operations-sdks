// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

#![cfg(feature = "azure_device_registry")]

use std::process::Command;
use std::sync::Arc;
use std::{env, time::Duration};

use azure_iot_operations_mqtt::aio::connection_settings::MqttConnectionSettingsBuilder;
use azure_iot_operations_mqtt::control_packet::{QoS, SubscribeProperties, TopicFilter};
use azure_iot_operations_mqtt::session::{Session, SessionExitHandle, SessionOptionsBuilder};
use azure_iot_operations_protocol::application::ApplicationContextBuilder;
use chrono::{DateTime, Utc};
use env_logger::Builder;
use serde::Deserialize;
use tokio::sync::Notify;
use uuid::Uuid;

use azure_iot_operations_services::azure_device_registry::models::{
    AssetStatus, DatasetRuntimeHealthEvent, DeviceStatus, EventRuntimeHealthEvent,
    ManagementActionRuntimeHealthEvent, StreamRuntimeHealthEvent,
};
use azure_iot_operations_services::azure_device_registry::{
    self, ConfigError, ConfigStatus, HealthStatus, RuntimeHealth,
};

const DEVICE1: &str = "my-thermostat";
const DEVICE2: &str = "test-thermostat";
const ENDPOINT1: &str = "my-rest-endpoint";
const ENDPOINT2: &str = "my-coap-endpoint";
// Unique names to avoid conflicts for spec updates
const ENDPOINT3: &str = "unique-endpoint";
const DEVICE3: &str = "unique-thermostat";
const TIMEOUT: Duration = Duration::from_secs(10);

// NOTE!: Must run `kubectl apply -f eng/test/test-adr-resources` before running these tests to have the necessary prerequisites

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
        .filter_module("azure_mqtt", log::LevelFilter::Warn)
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
) -> (Session, azure_device_registry::Client, SessionExitHandle) {
    let connection_settings = MqttConnectionSettingsBuilder::default()
        .client_id(client_id)
        .hostname("localhost")
        .tcp_port(1883u16)
        .keep_alive(Duration::from_secs(5))
        .use_tls(false)
        .clean_start(true)
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

            assert_eq!(response.attributes["deviceId"], DEVICE1);

            // Shutdown adr client and underlying resources
            assert!(azure_device_registry_client.shutdown().await.is_ok());

            exit_handle.try_exit().unwrap();
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

// This test also tests get_device_status, since the setup would be the same as this test
#[tokio::test]
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
        config: Some(ConfigStatus {
            error: Some(ConfigError {
                message: Some(message),
                ..Default::default()
            }),
            ..Default::default()
        }),
        ..Default::default()
    };
    let test_task = tokio::task::spawn({
        async move {
            let device_status_response = azure_device_registry_client
                .update_device_plus_endpoint_status(
                    DEVICE2.to_string(),
                    ENDPOINT2.to_string(),
                    updated_status.clone(),
                    TIMEOUT,
                )
                .await
                .unwrap();
            log::info!(
                "[{log_identifier}] Updated Response Device Status: {device_status_response:?}"
            );

            // TODO: switch back to this full matching once service properly clears endpoint values
            // assert_eq!(device_status_response, updated_status);
            assert_eq!(device_status_response.config, updated_status.config);

            // Test that get_device_status returns the same status as well
            let recvd_device_status = azure_device_registry_client
                .get_device_status(DEVICE2.to_string(), ENDPOINT2.to_string(), TIMEOUT)
                .await
                .unwrap();
            log::info!("[{log_identifier}] Get Device Status: {recvd_device_status:?}");
            assert_eq!(recvd_device_status.config, updated_status.config);

            // Shutdown adr client and underlying resources
            assert!(azure_device_registry_client.shutdown().await.is_ok());

            exit_handle.try_exit().unwrap();
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
    let asset_name: &str = "my-rest-thermostat-asset";
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
                    asset_name.to_string(),
                    TIMEOUT,
                )
                .await
                .unwrap();
            log::info!("[{log_identifier}] Response: {asset:?}");

            assert_eq!(asset.attributes["assetId"], asset_name);

            // Shutdown adr client and underlying resources
            assert!(azure_device_registry_client.shutdown().await.is_ok());
            exit_handle.try_exit().unwrap();
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

// This test also tests get_asset_status, since the setup would be the same as this test
#[tokio::test]
async fn update_asset_status() {
    let log_identifier = "update_asset_status_network_tests-rust";
    let asset_name: &str = "my-coap-thermostat-asset";
    if !setup_test(log_identifier) {
        return;
    }
    let (session, azure_device_registry_client, exit_handle) =
        initialize_client(&format!("{log_identifier}-client"));

    let message = format!("Random test error for asset update {}", Uuid::new_v4());
    let updated_status = AssetStatus {
        config: Some(ConfigStatus {
            error: Some(ConfigError {
                message: Some(message),
                ..Default::default()
            }),
            ..Default::default()
        }),
        ..Default::default()
    };

    let test_task = tokio::task::spawn({
        async move {
            let asset_status_response = azure_device_registry_client
                .update_asset_status(
                    DEVICE2.to_string(),
                    ENDPOINT2.to_string(),
                    asset_name.to_string(),
                    updated_status.clone(),
                    TIMEOUT,
                )
                .await
                .unwrap();
            log::info!(
                "[{log_identifier}] Updated Response Asset Status: {asset_status_response:?}"
            );

            assert_eq!(asset_status_response, updated_status);

            // Test that get_asset_status returns the same status as well
            let recvd_asset_status = azure_device_registry_client
                .get_asset_status(
                    DEVICE2.to_string(),
                    ENDPOINT2.to_string(),
                    asset_name.to_string(),
                    TIMEOUT,
                )
                .await
                .unwrap();
            log::info!("[{log_identifier}] Get Asset Status: {recvd_asset_status:?}");
            assert_eq!(recvd_asset_status, updated_status);

            // Shutdown adr client and underlying resources
            assert!(azure_device_registry_client.shutdown().await.is_ok());
            exit_handle.try_exit().unwrap();
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
async fn observe_asset_update_notifications() {
    let log_identifier = "observe_asset_update_notifications_network_tests-rust";
    let asset_name: &str = "unique-rest-thermostat-asset";
    if !setup_test(log_identifier) {
        return;
    }
    let (session, azure_device_registry_client, exit_handle) =
        initialize_client(&format!("{log_identifier}-client"));

    let test_task = tokio::task::spawn({
        async move {
            // This unobserve is to ensure we start the test with a clean state
            assert!(
                azure_device_registry_client
                    .unobserve_asset_update_notifications(
                        DEVICE3.to_string(),
                        ENDPOINT3.to_string(),
                        asset_name.to_string(),
                        TIMEOUT,
                    )
                    .await
                    .is_ok()
            );

            let update_desc = format!(
                "Patch specification update to trigger notification during observe {}",
                Uuid::new_v4()
            );
            let mut observation = azure_device_registry_client
                .observe_asset_update_notifications(
                    DEVICE3.to_string(),
                    ENDPOINT3.to_string(),
                    asset_name.to_string(),
                    TIMEOUT,
                )
                .await
                .unwrap();
            log::info!("[{log_identifier}] Asset update observation completed.");

            let first_notification_notify = Arc::new(Notify::new());

            let receive_notifications_task = tokio::task::spawn({
                let description_for_task = update_desc.clone();
                let first_notification_notify = first_notification_notify.clone();

                async move {
                    log::info!("[{log_identifier}] Asset update notification receiver started.");
                    let mut count = 0;
                    loop {
                        if let Some((asset, _)) = observation.recv_notification().await {
                            count += 1;

                            if count == 1 {
                                log::info!(
                                    "[{log_identifier}] Asset Update notification expected: {asset:?}"
                                );
                                // Signal that we got the first notification
                                first_notification_notify.notify_one();
                                assert_eq!(asset.description.unwrap(), description_for_task);
                            } else {
                                log::error!(
                                    "[{log_identifier}] Asset Update notification unexpected: {asset:?}"
                                );
                                // Should not receive more than 1 notification
                                assert!(
                                    count < 2,
                                    "Received unexpected additional asset update notification"
                                );
                            }
                        } else {
                            log::info!("[{log_identifier}] Receiving no more notifications.");
                            break;
                        }
                    }
                    count
                }
            });

            assert!(
                patch_asset_specification(log_identifier, asset_name, &update_desc).is_ok(),
                "Failed to patch asset specification"
            );

            // Wait for first notification with timeout (e.g., 30 seconds)
            let did_receive_1_notification_or_timeout = tokio::time::timeout(
                Duration::from_secs(30),
                first_notification_notify.notified(),
            )
            .await;

            // unobserve regardless of whether the notification was received or not for cleanup purposes
            azure_device_registry_client
                .unobserve_asset_update_notifications(
                    DEVICE3.to_string(),
                    ENDPOINT3.to_string(),
                    asset_name.to_string(),
                    TIMEOUT,
                )
                .await
                .unwrap();
            log::info!("[{log_identifier}] Asset update unobservation completed");

            // If the first notification wasn't received, skip directly to asserting that the count is wrong instead of sending a second update
            if did_receive_1_notification_or_timeout.is_ok() {
                log::info!("[{log_identifier}] First notification received successfully");

                assert!(patch_asset_specification(
                    log_identifier,
                    asset_name,
                    format!(
                        "Patch specification update to NOT trigger notification during unobserve {}",
                        Uuid::new_v4()
                    )
                    .as_str(),
                ).is_ok(), "Failed to patch asset specification");
            }

            match receive_notifications_task.await {
                Ok(count) => {
                    // Verify we got exactly 1 notification (only from the first update, not the second)
                    assert_eq!(count, 1, "Expected exactly 1 notification, got {count}");
                }
                Err(e) => {
                    panic!(
                        "Notification receiver task failed due to unexpected counts or mismatch notification: {e}"
                    );
                }
            }

            // Shutdown adr client and underlying resources
            assert!(azure_device_registry_client.shutdown().await.is_ok());
            exit_handle.try_exit().unwrap();
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

            let update_manufacturer = format!(
                "Patch specification update to trigger notification during observe {}",
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
            log::info!("[{log_identifier}] Device update observation completed.");

            let first_notification_notify = Arc::new(Notify::new());

            let receive_notifications_task = tokio::task::spawn({
                let first_notification_notify = first_notification_notify.clone();
                let update_manu_for_task = update_manufacturer.clone();
                async move {
                    log::info!("[{log_identifier}] Device update notification receiver started.");
                    let mut count = 0;
                    loop {
                        if let Some((device, _)) = observation.recv_notification().await {
                            count += 1;

                            if count == 1 {
                                log::info!(
                                    "[{log_identifier}] Device update notification expected: {device:?}"
                                );
                                // Signal that we got the first notification
                                first_notification_notify.notify_one();
                                assert_eq!(device.manufacturer.unwrap(), update_manu_for_task);
                            } else {
                                log::error!(
                                    "[{log_identifier}] Device update notification unexpected: {device:?}"
                                );
                                // Should not receive more than 1 notification
                                assert!(
                                    count < 2,
                                    "Received unexpected additional device update notification"
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

            assert!(
                patch_device_specification(log_identifier, DEVICE3, &update_manufacturer).is_ok(),
                "Failed to patch device specification"
            );

            // Wait for first notification with timeout (e.g., 30 seconds)
            let did_receive_1_notification_or_timeout = tokio::time::timeout(
                Duration::from_secs(30),
                first_notification_notify.notified(),
            )
            .await;

            // unobserve regardless of whether the notification was received or not for cleanup purposes
            azure_device_registry_client
                .unobserve_device_update_notifications(
                    DEVICE3.to_string(),
                    ENDPOINT3.to_string(),
                    TIMEOUT,
                )
                .await
                .unwrap();
            log::info!("[{log_identifier}] Device update unobservation was completed.");

            // If the first notification wasn't received, skip directly to asserting that the count is wrong instead of sending a second update
            if did_receive_1_notification_or_timeout.is_ok() {
                log::info!("[{log_identifier}] First notification received successfully");

                assert!(patch_device_specification(log_identifier,
                    DEVICE3,
                    format!(
                        "Patch specification update to NOT trigger notification during unobserve {}",
                        Uuid::new_v4()
                    )
                    .as_str(),
                ).is_ok(), "Failed to patch device specification");
            }
            match receive_notifications_task.await {
                // Verify we got exactly 1 notification (only from the first update, not the second)
                Ok(count) => {
                    assert_eq!(count, 1, "Expected exactly 1 notification, got {count}");
                }
                Err(e) => {
                    panic!(
                        "Notification receiver task failed due to unexpected counts or mismatch notification: {e}"
                    );
                }
            }

            // Shutdown adr client and underlying resources
            assert!(azure_device_registry_client.shutdown().await.is_ok());
            exit_handle.try_exit().unwrap();
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
    log_identifier: &str,
    asset_name: &str,
    description: &str,
) -> Result<(), String> {
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
        log::error!("[{log_identifier}] Failed to patch asset specification: {error_msg}");
        Err(error_msg.to_string())
    }
}

fn patch_device_specification(
    log_identifier: &str,
    device_name: &str,
    manufacturer: &str,
) -> Result<(), String> {
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
        log::error!("[{log_identifier}] Failed to patch device specification: {error_msg}");
        Err(error_msg.to_string())
    }
}

// ~~~ Runtime Health Event Tests ~~~

/// Expected JSON structure for device endpoint runtime health event payload
#[derive(Debug, Deserialize)]
#[serde(rename_all = "camelCase")]
struct DeviceEndpointRuntimeHealthEventPayload {
    device_endpoint_runtime_health_event: DeviceEndpointRuntimeHealthEventSchema,
}

#[derive(Debug, Deserialize)]
#[serde(rename_all = "camelCase")]
struct DeviceEndpointRuntimeHealthEventSchema {
    runtime_health: RuntimeHealthSchema,
}

#[derive(Debug, Deserialize)]
#[serde(rename_all = "camelCase")]
struct RuntimeHealthSchema {
    #[allow(dead_code)]
    last_update_time: DateTime<Utc>,
    message: Option<String>,
    reason_code: Option<String>,
    status: String,
    version: u64,
}

#[tokio::test]
async fn report_device_endpoint_runtime_health_event() {
    let log_identifier = "report_device_endpoint_runtime_health_event_network_tests-rust";
    if !setup_test(log_identifier) {
        return;
    }

    let device_name = DEVICE1;
    let endpoint_name = ENDPOINT1;
    let sender_client_id = format!("{log_identifier}-sender");

    // Expected telemetry topic pattern:
    // akri/connector/resources/telemetry/{connectorClientId}/{deviceName}/{inboundEndpointName}/{telemetryName}
    let subscribe_topic = format!(
        "akri/connector/resources/telemetry/{}/{}/{}/deviceEndpointRuntimeHealthEvent",
        sender_client_id, device_name, endpoint_name
    );

    // Create receiver session
    let receiver_connection_settings = MqttConnectionSettingsBuilder::default()
        .client_id(format!("{log_identifier}-receiver"))
        .hostname("localhost")
        .tcp_port(1883u16)
        .keep_alive(Duration::from_secs(5))
        .use_tls(false)
        .clean_start(true)
        .build()
        .unwrap();

    let receiver_session_options = SessionOptionsBuilder::default()
        .connection_settings(receiver_connection_settings)
        .build()
        .unwrap();

    let receiver_session = Session::new(receiver_session_options).unwrap();
    let receiver_client = receiver_session.create_managed_client();
    let receiver_exit_handle = receiver_session.create_exit_handle();

    // Create filtered receiver before subscribing
    let topic_filter = TopicFilter::new(&subscribe_topic).unwrap();
    let mut pub_receiver = receiver_client.create_filtered_pub_receiver(topic_filter.clone());

    // Create sender session with ADR client
    let (sender_session, adr_client, sender_exit_handle) = initialize_client(&sender_client_id);

    // Test data
    let test_timestamp = Utc::now();
    let test_message = "Test health message".to_string();
    let test_reason_code = "TestReason".to_string();
    let test_version = 1u64;

    let runtime_health = RuntimeHealth {
        last_update_time: test_timestamp,
        message: Some(test_message.clone()),
        reason_code: Some(test_reason_code.clone()),
        status: HealthStatus::Available,
        version: test_version,
    };

    let message_received = Arc::new(Notify::new());
    let message_received_clone = message_received.clone();

    // Receiver task
    let receiver_task = tokio::task::spawn({
        async move {
            // Subscribe to the topic
            receiver_client
                .subscribe(
                    topic_filter,
                    QoS::AtLeastOnce,
                    false,
                    azure_iot_operations_mqtt::control_packet::RetainOptions::default(),
                    SubscribeProperties::default(),
                )
                .await
                .unwrap();

            log::info!("[{log_identifier}] Subscribed to topic: {subscribe_topic}");

            // Wait for message with timeout
            let receive_result =
                tokio::time::timeout(Duration::from_secs(10), pub_receiver.recv()).await;

            match receive_result {
                Ok(Some(publish)) => {
                    log::info!(
                        "[{log_identifier}] Received message on topic: {}",
                        publish.topic_name
                    );

                    // Validate topic
                    assert_eq!(publish.topic_name.as_str(), subscribe_topic);

                    // Parse and validate payload
                    let payload: DeviceEndpointRuntimeHealthEventPayload =
                        serde_json::from_slice(&publish.payload).expect("Failed to parse payload");

                    let health = &payload.device_endpoint_runtime_health_event.runtime_health;
                    assert_eq!(health.status, "Available");
                    assert_eq!(health.version, test_version);
                    assert_eq!(health.message, Some(test_message));
                    assert_eq!(health.reason_code, Some(test_reason_code));

                    log::info!("[{log_identifier}] Payload validated successfully");
                    message_received_clone.notify_one();
                }
                Ok(None) => {
                    panic!("[{log_identifier}] Receiver channel closed unexpectedly");
                }
                Err(_) => {
                    panic!("[{log_identifier}] Timeout waiting for message");
                }
            }

            receiver_exit_handle.try_exit().unwrap();
        }
    });

    // Sender task
    let sender_task = tokio::task::spawn({
        async move {
            // Give receiver time to subscribe
            tokio::time::sleep(Duration::from_millis(500)).await;

            // Send the health event
            adr_client
                .report_device_endpoint_runtime_health_event(
                    device_name.to_string(),
                    endpoint_name.to_string(),
                    runtime_health,
                    Duration::from_secs(30),
                )
                .await
                .expect("Failed to send device endpoint runtime health event");

            log::info!("[{log_identifier}] Sent device endpoint runtime health event");

            // Wait for message to be received before shutdown
            tokio::time::timeout(Duration::from_secs(15), message_received.notified())
                .await
                .expect("Timeout waiting for message confirmation");

            // Shutdown
            adr_client.shutdown().await.unwrap();
            sender_exit_handle.try_exit().unwrap();
        }
    });

    // Run both sessions
    let result = tokio::try_join!(
        async move { receiver_task.await.map_err(|e| e.to_string()) },
        async move { sender_task.await.map_err(|e| e.to_string()) },
        async move { receiver_session.run().await.map_err(|e| e.to_string()) },
        async move { sender_session.run().await.map_err(|e| e.to_string()) }
    );

    assert!(result.is_ok(), "Test failed: {result:?}");
}

// ~~~ Dataset Runtime Health Event Test ~~~

#[derive(Debug, Deserialize)]
#[serde(rename_all = "camelCase")]
struct DatasetRuntimeHealthEventPayload {
    dataset_runtime_health_event: DatasetRuntimeHealthEventSchemaPayload,
}

#[derive(Debug, Deserialize)]
#[serde(rename_all = "camelCase")]
struct DatasetRuntimeHealthEventSchemaPayload {
    asset_name: String,
    datasets: Vec<DatasetElementPayload>,
}

#[derive(Debug, Deserialize)]
#[serde(rename_all = "camelCase")]
struct DatasetElementPayload {
    dataset_name: String,
    runtime_health: RuntimeHealthSchema,
}

#[tokio::test]
async fn report_dataset_runtime_health_events() {
    let log_identifier = "report_dataset_runtime_health_events_network_tests-rust";
    if !setup_test(log_identifier) {
        return;
    }

    let device_name = DEVICE1;
    let endpoint_name = ENDPOINT1;
    let asset_name = "test-asset";
    let sender_client_id = format!("{log_identifier}-sender");

    let subscribe_topic = format!(
        "akri/connector/resources/telemetry/{}/{}/{}/datasetRuntimeHealthEvent",
        sender_client_id, device_name, endpoint_name
    );

    // Create receiver session
    let receiver_connection_settings = MqttConnectionSettingsBuilder::default()
        .client_id(format!("{log_identifier}-receiver"))
        .hostname("localhost")
        .tcp_port(1883u16)
        .keep_alive(Duration::from_secs(5))
        .use_tls(false)
        .clean_start(true)
        .build()
        .unwrap();

    let receiver_session_options = SessionOptionsBuilder::default()
        .connection_settings(receiver_connection_settings)
        .build()
        .unwrap();

    let receiver_session = Session::new(receiver_session_options).unwrap();
    let receiver_client = receiver_session.create_managed_client();
    let receiver_exit_handle = receiver_session.create_exit_handle();

    let topic_filter = TopicFilter::new(&subscribe_topic).unwrap();
    let mut pub_receiver = receiver_client.create_filtered_pub_receiver(topic_filter.clone());

    let (sender_session, adr_client, sender_exit_handle) = initialize_client(&sender_client_id);

    // Test data
    let test_dataset_name = "test-dataset-1".to_string();
    let test_version = 100u64;

    let runtime_health_events = vec![DatasetRuntimeHealthEvent {
        dataset_name: test_dataset_name.clone(),
        runtime_health: RuntimeHealth {
            last_update_time: Utc::now(),
            message: Some("Dataset healthy".to_string()),
            reason_code: Some("DatasetOK".to_string()),
            status: HealthStatus::Available,
            version: test_version,
        },
    }];

    let message_received = Arc::new(Notify::new());
    let message_received_clone = message_received.clone();

    let receiver_task = tokio::task::spawn({
        let asset_name = asset_name.to_string();
        async move {
            receiver_client
                .subscribe(
                    topic_filter,
                    QoS::AtLeastOnce,
                    false,
                    azure_iot_operations_mqtt::control_packet::RetainOptions::default(),
                    SubscribeProperties::default(),
                )
                .await
                .unwrap();

            log::info!("[{log_identifier}] Subscribed to topic: {subscribe_topic}");

            let receive_result =
                tokio::time::timeout(Duration::from_secs(10), pub_receiver.recv()).await;

            match receive_result {
                Ok(Some(publish)) => {
                    log::info!(
                        "[{log_identifier}] Received message on topic: {}",
                        publish.topic_name
                    );

                    assert_eq!(publish.topic_name.as_str(), subscribe_topic);

                    let payload: DatasetRuntimeHealthEventPayload =
                        serde_json::from_slice(&publish.payload).expect("Failed to parse payload");

                    assert_eq!(payload.dataset_runtime_health_event.asset_name, asset_name);
                    assert_eq!(payload.dataset_runtime_health_event.datasets.len(), 1);

                    let dataset = &payload.dataset_runtime_health_event.datasets[0];
                    assert_eq!(dataset.dataset_name, test_dataset_name);
                    assert_eq!(dataset.runtime_health.status, "Available");
                    assert_eq!(dataset.runtime_health.version, test_version);

                    log::info!("[{log_identifier}] Payload validated successfully");
                    message_received_clone.notify_one();
                }
                Ok(None) => panic!("[{log_identifier}] Receiver channel closed unexpectedly"),
                Err(_) => panic!("[{log_identifier}] Timeout waiting for message"),
            }

            receiver_exit_handle.try_exit().unwrap();
        }
    });

    let sender_task = tokio::task::spawn({
        let asset_name = asset_name.to_string();
        async move {
            tokio::time::sleep(Duration::from_millis(500)).await;

            adr_client
                .report_dataset_runtime_health_events(
                    device_name.to_string(),
                    endpoint_name.to_string(),
                    asset_name,
                    runtime_health_events,
                    Duration::from_secs(30),
                )
                .await
                .expect("Failed to send dataset runtime health events");

            log::info!("[{log_identifier}] Sent dataset runtime health events");

            tokio::time::timeout(Duration::from_secs(15), message_received.notified())
                .await
                .expect("Timeout waiting for message confirmation");

            adr_client.shutdown().await.unwrap();
            sender_exit_handle.try_exit().unwrap();
        }
    });

    let result = tokio::try_join!(
        async move { receiver_task.await.map_err(|e| e.to_string()) },
        async move { sender_task.await.map_err(|e| e.to_string()) },
        async move { receiver_session.run().await.map_err(|e| e.to_string()) },
        async move { sender_session.run().await.map_err(|e| e.to_string()) }
    );

    assert!(result.is_ok(), "Test failed: {result:?}");
}

// ~~~ Event Runtime Health Event Test ~~~

#[derive(Debug, Deserialize)]
#[serde(rename_all = "camelCase")]
struct EventRuntimeHealthEventPayload {
    event_runtime_health_event: EventRuntimeHealthEventSchemaPayload,
}

#[derive(Debug, Deserialize)]
#[serde(rename_all = "camelCase")]
struct EventRuntimeHealthEventSchemaPayload {
    asset_name: String,
    events: Vec<EventElementPayload>,
}

#[derive(Debug, Deserialize)]
#[serde(rename_all = "camelCase")]
struct EventElementPayload {
    event_group_name: String,
    event_name: String,
    runtime_health: RuntimeHealthSchema,
}

#[tokio::test]
async fn report_event_runtime_health_events() {
    let log_identifier = "report_event_runtime_health_events_network_tests-rust";
    if !setup_test(log_identifier) {
        return;
    }

    let device_name = DEVICE1;
    let endpoint_name = ENDPOINT1;
    let asset_name = "test-asset";
    let sender_client_id = format!("{log_identifier}-sender");

    let subscribe_topic = format!(
        "akri/connector/resources/telemetry/{}/{}/{}/eventRuntimeHealthEvent",
        sender_client_id, device_name, endpoint_name
    );

    let receiver_connection_settings = MqttConnectionSettingsBuilder::default()
        .client_id(format!("{log_identifier}-receiver"))
        .hostname("localhost")
        .tcp_port(1883u16)
        .keep_alive(Duration::from_secs(5))
        .use_tls(false)
        .clean_start(true)
        .build()
        .unwrap();

    let receiver_session_options = SessionOptionsBuilder::default()
        .connection_settings(receiver_connection_settings)
        .build()
        .unwrap();

    let receiver_session = Session::new(receiver_session_options).unwrap();
    let receiver_client = receiver_session.create_managed_client();
    let receiver_exit_handle = receiver_session.create_exit_handle();

    let topic_filter = TopicFilter::new(&subscribe_topic).unwrap();
    let mut pub_receiver = receiver_client.create_filtered_pub_receiver(topic_filter.clone());

    let (sender_session, adr_client, sender_exit_handle) = initialize_client(&sender_client_id);

    // Test data
    let test_event_group_name = "test-event-group".to_string();
    let test_event_name = "test-event-1".to_string();
    let test_version = 200u64;

    let runtime_health_events = vec![EventRuntimeHealthEvent {
        event_group_name: test_event_group_name.clone(),
        event_name: test_event_name.clone(),
        runtime_health: RuntimeHealth {
            last_update_time: Utc::now(),
            message: Some("Event healthy".to_string()),
            reason_code: Some("EventOK".to_string()),
            status: HealthStatus::Unavailable,
            version: test_version,
        },
    }];

    let message_received = Arc::new(Notify::new());
    let message_received_clone = message_received.clone();

    let receiver_task = tokio::task::spawn({
        let asset_name = asset_name.to_string();
        async move {
            receiver_client
                .subscribe(
                    topic_filter,
                    QoS::AtLeastOnce,
                    false,
                    azure_iot_operations_mqtt::control_packet::RetainOptions::default(),
                    SubscribeProperties::default(),
                )
                .await
                .unwrap();

            log::info!("[{log_identifier}] Subscribed to topic: {subscribe_topic}");

            let receive_result =
                tokio::time::timeout(Duration::from_secs(10), pub_receiver.recv()).await;

            match receive_result {
                Ok(Some(publish)) => {
                    log::info!(
                        "[{log_identifier}] Received message on topic: {}",
                        publish.topic_name
                    );

                    assert_eq!(publish.topic_name.as_str(), subscribe_topic);

                    let payload: EventRuntimeHealthEventPayload =
                        serde_json::from_slice(&publish.payload).expect("Failed to parse payload");

                    assert_eq!(payload.event_runtime_health_event.asset_name, asset_name);
                    assert_eq!(payload.event_runtime_health_event.events.len(), 1);

                    let event = &payload.event_runtime_health_event.events[0];
                    assert_eq!(event.event_group_name, test_event_group_name);
                    assert_eq!(event.event_name, test_event_name);
                    assert_eq!(event.runtime_health.status, "Unavailable");
                    assert_eq!(event.runtime_health.version, test_version);

                    log::info!("[{log_identifier}] Payload validated successfully");
                    message_received_clone.notify_one();
                }
                Ok(None) => panic!("[{log_identifier}] Receiver channel closed unexpectedly"),
                Err(_) => panic!("[{log_identifier}] Timeout waiting for message"),
            }

            receiver_exit_handle.try_exit().unwrap();
        }
    });

    let sender_task = tokio::task::spawn({
        let asset_name = asset_name.to_string();
        async move {
            tokio::time::sleep(Duration::from_millis(500)).await;

            adr_client
                .report_event_runtime_health_events(
                    device_name.to_string(),
                    endpoint_name.to_string(),
                    asset_name,
                    runtime_health_events,
                    Duration::from_secs(30),
                )
                .await
                .expect("Failed to send event runtime health events");

            log::info!("[{log_identifier}] Sent event runtime health events");

            tokio::time::timeout(Duration::from_secs(15), message_received.notified())
                .await
                .expect("Timeout waiting for message confirmation");

            adr_client.shutdown().await.unwrap();
            sender_exit_handle.try_exit().unwrap();
        }
    });

    let result = tokio::try_join!(
        async move { receiver_task.await.map_err(|e| e.to_string()) },
        async move { sender_task.await.map_err(|e| e.to_string()) },
        async move { receiver_session.run().await.map_err(|e| e.to_string()) },
        async move { sender_session.run().await.map_err(|e| e.to_string()) }
    );

    assert!(result.is_ok(), "Test failed: {result:?}");
}

// ~~~ Stream Runtime Health Event Test ~~~

#[derive(Debug, Deserialize)]
#[serde(rename_all = "camelCase")]
struct StreamRuntimeHealthEventPayload {
    stream_runtime_health_event: StreamRuntimeHealthEventSchemaPayload,
}

#[derive(Debug, Deserialize)]
#[serde(rename_all = "camelCase")]
struct StreamRuntimeHealthEventSchemaPayload {
    asset_name: String,
    streams: Vec<StreamElementPayload>,
}

#[derive(Debug, Deserialize)]
#[serde(rename_all = "camelCase")]
struct StreamElementPayload {
    stream_name: String,
    runtime_health: RuntimeHealthSchema,
}

#[tokio::test]
async fn report_stream_runtime_health_events() {
    let log_identifier = "report_stream_runtime_health_events_network_tests-rust";
    if !setup_test(log_identifier) {
        return;
    }

    let device_name = DEVICE1;
    let endpoint_name = ENDPOINT1;
    let asset_name = "test-asset";
    let sender_client_id = format!("{log_identifier}-sender");

    let subscribe_topic = format!(
        "akri/connector/resources/telemetry/{}/{}/{}/streamRuntimeHealthEvent",
        sender_client_id, device_name, endpoint_name
    );

    let receiver_connection_settings = MqttConnectionSettingsBuilder::default()
        .client_id(format!("{log_identifier}-receiver"))
        .hostname("localhost")
        .tcp_port(1883u16)
        .keep_alive(Duration::from_secs(5))
        .use_tls(false)
        .clean_start(true)
        .build()
        .unwrap();

    let receiver_session_options = SessionOptionsBuilder::default()
        .connection_settings(receiver_connection_settings)
        .build()
        .unwrap();

    let receiver_session = Session::new(receiver_session_options).unwrap();
    let receiver_client = receiver_session.create_managed_client();
    let receiver_exit_handle = receiver_session.create_exit_handle();

    let topic_filter = TopicFilter::new(&subscribe_topic).unwrap();
    let mut pub_receiver = receiver_client.create_filtered_pub_receiver(topic_filter.clone());

    let (sender_session, adr_client, sender_exit_handle) = initialize_client(&sender_client_id);

    // Test data
    let test_stream_name = "test-stream-1".to_string();
    let test_version = 300u64;

    let runtime_health_events = vec![StreamRuntimeHealthEvent {
        stream_name: test_stream_name.clone(),
        runtime_health: RuntimeHealth {
            last_update_time: Utc::now(),
            message: Some("Stream healthy".to_string()),
            reason_code: Some("StreamOK".to_string()),
            status: HealthStatus::Available,
            version: test_version,
        },
    }];

    let message_received = Arc::new(Notify::new());
    let message_received_clone = message_received.clone();

    let receiver_task = tokio::task::spawn({
        let asset_name = asset_name.to_string();
        async move {
            receiver_client
                .subscribe(
                    topic_filter,
                    QoS::AtLeastOnce,
                    false,
                    azure_iot_operations_mqtt::control_packet::RetainOptions::default(),
                    SubscribeProperties::default(),
                )
                .await
                .unwrap();

            log::info!("[{log_identifier}] Subscribed to topic: {subscribe_topic}");

            let receive_result =
                tokio::time::timeout(Duration::from_secs(10), pub_receiver.recv()).await;

            match receive_result {
                Ok(Some(publish)) => {
                    log::info!(
                        "[{log_identifier}] Received message on topic: {}",
                        publish.topic_name
                    );

                    assert_eq!(publish.topic_name.as_str(), subscribe_topic);

                    let payload: StreamRuntimeHealthEventPayload =
                        serde_json::from_slice(&publish.payload).expect("Failed to parse payload");

                    assert_eq!(payload.stream_runtime_health_event.asset_name, asset_name);
                    assert_eq!(payload.stream_runtime_health_event.streams.len(), 1);

                    let stream = &payload.stream_runtime_health_event.streams[0];
                    assert_eq!(stream.stream_name, test_stream_name);
                    assert_eq!(stream.runtime_health.status, "Available");
                    assert_eq!(stream.runtime_health.version, test_version);

                    log::info!("[{log_identifier}] Payload validated successfully");
                    message_received_clone.notify_one();
                }
                Ok(None) => panic!("[{log_identifier}] Receiver channel closed unexpectedly"),
                Err(_) => panic!("[{log_identifier}] Timeout waiting for message"),
            }

            receiver_exit_handle.try_exit().unwrap();
        }
    });

    let sender_task = tokio::task::spawn({
        let asset_name = asset_name.to_string();
        async move {
            tokio::time::sleep(Duration::from_millis(500)).await;

            adr_client
                .report_stream_runtime_health_events(
                    device_name.to_string(),
                    endpoint_name.to_string(),
                    asset_name,
                    runtime_health_events,
                    Duration::from_secs(30),
                )
                .await
                .expect("Failed to send stream runtime health events");

            log::info!("[{log_identifier}] Sent stream runtime health events");

            tokio::time::timeout(Duration::from_secs(15), message_received.notified())
                .await
                .expect("Timeout waiting for message confirmation");

            adr_client.shutdown().await.unwrap();
            sender_exit_handle.try_exit().unwrap();
        }
    });

    let result = tokio::try_join!(
        async move { receiver_task.await.map_err(|e| e.to_string()) },
        async move { sender_task.await.map_err(|e| e.to_string()) },
        async move { receiver_session.run().await.map_err(|e| e.to_string()) },
        async move { sender_session.run().await.map_err(|e| e.to_string()) }
    );

    assert!(result.is_ok(), "Test failed: {result:?}");
}

// ~~~ Management Action Runtime Health Event Test ~~~

#[derive(Debug, Deserialize)]
#[serde(rename_all = "camelCase")]
struct ManagementActionRuntimeHealthEventPayload {
    management_action_runtime_health_event: ManagementActionRuntimeHealthEventSchemaPayload,
}

#[derive(Debug, Deserialize)]
#[serde(rename_all = "camelCase")]
struct ManagementActionRuntimeHealthEventSchemaPayload {
    asset_name: String,
    management_actions: Vec<ManagementActionElementPayload>,
}

#[derive(Debug, Deserialize)]
#[serde(rename_all = "camelCase")]
struct ManagementActionElementPayload {
    management_group_name: String,
    management_action_name: String,
    runtime_health: RuntimeHealthSchema,
}

#[tokio::test]
async fn report_management_action_runtime_health_events() {
    let log_identifier = "report_management_action_runtime_health_events_network_tests-rust";
    if !setup_test(log_identifier) {
        return;
    }

    let device_name = DEVICE1;
    let endpoint_name = ENDPOINT1;
    let asset_name = "test-asset";
    let sender_client_id = format!("{log_identifier}-sender");

    let subscribe_topic = format!(
        "akri/connector/resources/telemetry/{}/{}/{}/managementActionRuntimeHealthEvent",
        sender_client_id, device_name, endpoint_name
    );

    let receiver_connection_settings = MqttConnectionSettingsBuilder::default()
        .client_id(format!("{log_identifier}-receiver"))
        .hostname("localhost")
        .tcp_port(1883u16)
        .keep_alive(Duration::from_secs(5))
        .use_tls(false)
        .clean_start(true)
        .build()
        .unwrap();

    let receiver_session_options = SessionOptionsBuilder::default()
        .connection_settings(receiver_connection_settings)
        .build()
        .unwrap();

    let receiver_session = Session::new(receiver_session_options).unwrap();
    let receiver_client = receiver_session.create_managed_client();
    let receiver_exit_handle = receiver_session.create_exit_handle();

    let topic_filter = TopicFilter::new(&subscribe_topic).unwrap();
    let mut pub_receiver = receiver_client.create_filtered_pub_receiver(topic_filter.clone());

    let (sender_session, adr_client, sender_exit_handle) = initialize_client(&sender_client_id);

    // Test data
    let test_management_group_name = "test-management-group".to_string();
    let test_management_action_name = "test-action-1".to_string();
    let test_version = 400u64;

    let runtime_health_events = vec![ManagementActionRuntimeHealthEvent {
        management_group_name: test_management_group_name.clone(),
        management_action_name: test_management_action_name.clone(),
        runtime_health: RuntimeHealth {
            last_update_time: Utc::now(),
            message: Some("Management action healthy".to_string()),
            reason_code: Some("ActionOK".to_string()),
            status: HealthStatus::Unavailable,
            version: test_version,
        },
    }];

    let message_received = Arc::new(Notify::new());
    let message_received_clone = message_received.clone();

    let receiver_task = tokio::task::spawn({
        let asset_name = asset_name.to_string();
        async move {
            receiver_client
                .subscribe(
                    topic_filter,
                    QoS::AtLeastOnce,
                    false,
                    azure_iot_operations_mqtt::control_packet::RetainOptions::default(),
                    SubscribeProperties::default(),
                )
                .await
                .unwrap();

            log::info!("[{log_identifier}] Subscribed to topic: {subscribe_topic}");

            let receive_result =
                tokio::time::timeout(Duration::from_secs(10), pub_receiver.recv()).await;

            match receive_result {
                Ok(Some(publish)) => {
                    log::info!(
                        "[{log_identifier}] Received message on topic: {}",
                        publish.topic_name
                    );

                    assert_eq!(publish.topic_name.as_str(), subscribe_topic);

                    let payload: ManagementActionRuntimeHealthEventPayload =
                        serde_json::from_slice(&publish.payload).expect("Failed to parse payload");

                    assert_eq!(
                        payload.management_action_runtime_health_event.asset_name,
                        asset_name
                    );
                    assert_eq!(
                        payload
                            .management_action_runtime_health_event
                            .management_actions
                            .len(),
                        1
                    );

                    let action = &payload
                        .management_action_runtime_health_event
                        .management_actions[0];
                    assert_eq!(action.management_group_name, test_management_group_name);
                    assert_eq!(action.management_action_name, test_management_action_name);
                    assert_eq!(action.runtime_health.status, "Unavailable");
                    assert_eq!(action.runtime_health.version, test_version);

                    log::info!("[{log_identifier}] Payload validated successfully");
                    message_received_clone.notify_one();
                }
                Ok(None) => panic!("[{log_identifier}] Receiver channel closed unexpectedly"),
                Err(_) => panic!("[{log_identifier}] Timeout waiting for message"),
            }

            receiver_exit_handle.try_exit().unwrap();
        }
    });

    let sender_task = tokio::task::spawn({
        let asset_name = asset_name.to_string();
        async move {
            tokio::time::sleep(Duration::from_millis(500)).await;

            adr_client
                .report_management_action_runtime_health_events(
                    device_name.to_string(),
                    endpoint_name.to_string(),
                    asset_name,
                    runtime_health_events,
                    Duration::from_secs(30),
                )
                .await
                .expect("Failed to send management action runtime health events");

            log::info!("[{log_identifier}] Sent management action runtime health events");

            tokio::time::timeout(Duration::from_secs(15), message_received.notified())
                .await
                .expect("Timeout waiting for message confirmation");

            adr_client.shutdown().await.unwrap();
            sender_exit_handle.try_exit().unwrap();
        }
    });

    let result = tokio::try_join!(
        async move { receiver_task.await.map_err(|e| e.to_string()) },
        async move { sender_task.await.map_err(|e| e.to_string()) },
        async move { receiver_session.run().await.map_err(|e| e.to_string()) },
        async move { sender_session.run().await.map_err(|e| e.to_string()) }
    );

    assert!(result.is_ok(), "Test failed: {result:?}");
}
