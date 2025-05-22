// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

#![cfg(feature = "azure_device_registry")]

use std::collections::HashMap;
use std::{env, time::Duration};

use env_logger::Builder;

use azure_iot_operations_mqtt::MqttConnectionSettingsBuilder;
use azure_iot_operations_mqtt::session::{
    Session, SessionExitHandle, SessionManagedClient, SessionOptionsBuilder,
};
use azure_iot_operations_protocol::application::ApplicationContextBuilder;
use azure_iot_operations_services::azure_device_registry::{
    self, AssetStatus, DeviceStatus, StatusConfig,
};

const DEVICE1: &str = "my-thermostat";
#[allow(dead_code)]
const DEVICE2: &str = "test-thermostat";
const ENDPOINT1: &str = "my-rest-endpoint";
const ASSET_NAME1: &str = "my-rest-thermostat-asset";
const ENDPOINT_TYPE: &str = "rest-thermostat";
const TYPE: &str = "thermostat";
const TIMEOUT: Duration = Duration::from_secs(10);

// Test Scenarios:
// get device
// update status of device
// observe device telemetry
// unobserve device telemetry

fn setup_test(test_name: &str) -> bool {
    let _ = Builder::new()
        .filter_level(log::LevelFilter::Info)
        .format_timestamp(None)
        .filter_module("rumqttc", log::LevelFilter::Warn)
        .filter_module("azure_iot_operations", log::LevelFilter::Debug)
        .try_init();

    // TODO Uncomment this to enable network tests
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
        //.tcp_port(31883u16)
        // TODO Uncomment this
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
#[ignore = "For update notification tests focus"]
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
            log::info!("[{log_identifier}] Get Device: {response:?}",);

            assert_eq!(response.name, DEVICE1);
            assert_eq!(response.specification.attributes["deviceId"], DEVICE1);
            assert_eq!(response.specification.attributes["deviceType"], TYPE);
            assert!(
                response
                    .specification
                    .endpoints
                    .inbound
                    .contains_key(ENDPOINT1)
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
#[ignore = "For update notification tests focus"]
async fn update_device_plus_endpoint_status() {
    let log_identifier = "update_device_plus_endpoint_status_network_tests-rust";
    if !setup_test(log_identifier) {
        return;
    }
    let (session, azure_device_registry_client, exit_handle) =
        initialize_client(&format!("{log_identifier}-client"));

    let updated_status = DeviceStatus {
        config: Some(StatusConfig {
            error: None,
            version: Some(11),
            last_transition_time: Some(String::from("2025-11-11T00:00:00Z")),
        }),
        endpoints: vec![(ENDPOINT1.to_string(), None)]
            .into_iter()
            .collect::<std::collections::HashMap<_, _>>(),
    };
    let test_task = tokio::task::spawn({
        async move {
            let updated_response = azure_device_registry_client
                .update_device_plus_endpoint_status(
                    DEVICE1.to_string(),
                    ENDPOINT1.to_string(),
                    updated_status,
                    TIMEOUT,
                )
                .await
                .unwrap();
            log::info!("[{log_identifier}] Updated Response Device: {updated_response:?}",);

            assert_eq!(updated_response.name, DEVICE1);
            assert_eq!(
                updated_response.specification.attributes["deviceId"],
                DEVICE1
            );
            assert_eq!(
                updated_response.specification.attributes["deviceType"],
                TYPE
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
#[ignore = "For update notification tests focus"]
async fn get_asset() {
    let log_identifier = "get_asset_network_tests-rust";
    if !setup_test(log_identifier) {
        return;
    }
    let (session, azure_device_registry_client, exit_handle) =
        initialize_client(&format!("{log_identifier}-client"));

    let test_task = tokio::task::spawn({
        async move {
            let asset_response = azure_device_registry_client
                .get_asset(
                    DEVICE1.to_string(),
                    ENDPOINT1.to_string(),
                    ASSET_NAME1.to_string(),
                    TIMEOUT,
                )
                .await
                .unwrap();
            log::info!("[{log_identifier}] Response: {asset_response:?}",);

            assert_eq!(asset_response.name, ASSET_NAME1);
            assert_eq!(
                asset_response.specification.attributes["assetId"],
                ASSET_NAME1
            );
            assert_eq!(asset_response.specification.attributes["assetType"], TYPE);
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
#[ignore = "For update notification tests focus"]
async fn update_asset_status() {
    let log_identifier = "update_asset_status_network_tests-rust";
    if !setup_test(log_identifier) {
        return;
    }
    let (session, azure_device_registry_client, exit_handle) =
        initialize_client(&format!("{log_identifier}-client"));

    let updated_status = AssetStatus {
        config: Some(StatusConfig {
            error: None,
            version: Some(11),
            last_transition_time: Some(String::from("2025-11-11T00:00:00Z")),
        }),
        datasets: None,
        events: None,
        management_groups: None,
        streams: None,
    };

    let test_task = tokio::task::spawn({
        async move {
            let updated_response = azure_device_registry_client
                .update_asset_status(
                    DEVICE1.to_string(),
                    ENDPOINT1.to_string(),
                    ASSET_NAME1.to_string(),
                    updated_status,
                    TIMEOUT,
                )
                .await
                .unwrap();
            log::info!("[{log_identifier}] Updated Response Asset: {updated_response:?}",);

            assert_eq!(updated_response.name, ASSET_NAME1);
            assert_eq!(
                updated_response.specification.attributes["assetId"],
                ASSET_NAME1
            );
            assert_eq!(updated_response.specification.attributes["assetType"], TYPE);

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
// #[ignore = "This test is ignored as it is not fully implemented yet."]
async fn observe_device_update_notifications() {
    let log_identifier = "observe_device_update_notifications_network_tests-rust";
    if !setup_test(log_identifier) {
        return;
    }
    let (session, azure_device_registry_client, exit_handle) =
        initialize_client(&format!("{log_identifier}-client"));
    // let new_version = 12;
    //let mut old_version = 0;

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
            log::info!("[{log_identifier}] Observation Response: {observation:?}",);
            let receive_notifications_task = tokio::task::spawn({
                async move {
                    log::info!("[{log_identifier}] Device Update receiver started.");
                    let mut count = 0;
                    // while let Some((notification, _)) = observation.recv_notification().await {
                    //     log::info!("[{log_identifier}] Harry Potter");
                    //     log::info!("device updated: {notification:#?}");
                    // }
                    if let Some((device, _)) = observation.recv_notification().await {
                        count += 1;
                        log::info!("[{log_identifier}] FIRST OBS DELETE LOG");
                        log::info!("[{log_identifier}] Device Observation: {device:?}");
                        assert_eq!(device.name, DEVICE1);
                    }
                    // if let Some((device, _)) = observation.recv_notification().await {
                    //     count += 1;
                    //     log::info!("[{log_identifier}] Harry Potter DELETE LOG");
                    //     log::info!("[{log_identifier}] Device From Observation 2: {device:?}");
                    //     assert_eq!(device.name, DEVICE1);
                    // }
                    // if let Some((device, _)) = observation.recv_notification().await {
                    //     count += 1;
                    //     log::info!("[{log_identifier}] Harry Potter DELETE LOG");
                    //     log::info!("[{log_identifier}] Device From Observation 3: {device:?}");
                    // }
                    while let Some((device, _)) = observation.recv_notification().await {
                        count += 1;
                        log::info!("[{log_identifier}] ANY OTHER OBS DELETE LOG");
                        log::info!(
                            "[{log_identifier}] Device Observation If Any Other Works: {device:?}"
                        );
                        // if something weird happens, this should prevent an infinite loop.
                        // Note that this does prevent getting an accurate count of how many extra unexpected notifications were received
                        assert!(count < 4);
                    }
                    // only the 2 expected notifications should occur
                    assert_eq!(count, 1);
                    log::info!("[{log_identifier}] Device Update receiver closed");
                }
            });

            tokio::time::sleep(Duration::from_secs(1)).await;
            // Get the device
            let response = azure_device_registry_client
                .get_device(DEVICE1.to_string(), ENDPOINT1.to_string(), TIMEOUT)
                .await
                .unwrap();
            log::info!("[{log_identifier}] Get Device Reponse: {response:?}",);
            //old_version = response.specification.version.unwrap_or(0);
            // log::info!(
            //     "[{log_identifier}] Datetime: {}",
            //     time::OffsetDateTime::now_utc()
            // );
            let mut endpoint_statuses = HashMap::new();
            for (endpoint_name, endpoint) in response.specification.endpoints.inbound {
                if endpoint.endpoint_type == ENDPOINT_TYPE {
                    log::info!("Endpoint '{endpoint_name}' accepted");
                    // adding endpoint to status hashmap with None ConfigError to show that we accept the endpoint with no errors
                    endpoint_statuses.insert(endpoint_name, None);
                } else {
                    // if we don't support the endpoint type, then we can report that error
                    log::warn!(
                        "Endpoint '{endpoint_name}' not accepted. Endpoint type '{}' not supported.",
                        endpoint.endpoint_type
                    );
                    endpoint_statuses.insert(
                        endpoint_name,
                        Some(azure_device_registry::ConfigError {
                            message: Some("endpoint type is not supported".to_string()),
                            ..azure_device_registry::ConfigError::default()
                        }),
                    );
                }
            }
            // let status_to_be_updated = azure_device_registry::DeviceStatus {
            //     config: Some(azure_device_registry::StatusConfig {
            //         version: response.specification.version,
            //         last_transition_time: Some(time::OffsetDateTime::now_utc().to_string()),
            //         // error: Some(azure_device_registry::ConfigError {
            //         //     message: Some("device type is not supported".to_string()),
            //         //     ..azure_device_registry::ConfigError::default()
            //         // }),
            //         ..azure_device_registry::StatusConfig::default()
            //     }),
            //     endpoints: endpoint_statuses,
            // };
            let response_during_obs = azure_device_registry_client
                .update_device_plus_endpoint_status(
                    DEVICE1.to_string(),
                    ENDPOINT1.to_string(),
                    DeviceStatus {
                        config: Some(StatusConfig {
                            version: response.specification.version,
                            last_transition_time: Some(time::OffsetDateTime::now_utc().to_string()),
                            ..azure_device_registry::StatusConfig::default()
                        }),
                        endpoints: endpoint_statuses,
                    },
                    // status_to_be_updated,
                    TIMEOUT,
                )
                .await
                .unwrap();
            log::info!(
                "[{log_identifier}] Updated Response Device After Observation: {response_during_obs:?}",
            );
            // assert_eq!(
            //     updated_response1.specification.version.unwrap(),
            //     old_version + 1
            // );
            // let updated_response2 = azure_device_registry_client
            //     .update_device_plus_endpoint_status(
            //         DEVICE1.to_string(),
            //         ENDPOINT1.to_string(),
            //         DeviceStatus {
            //             config: Some(StatusConfig {
            //                 error: None,
            //                 version: Some(old_version + 2),
            //                 last_transition_time: Some(String::from("2025-12-12:00:00Z")),
            //             }),
            //             endpoints: std::collections::HashMap::new(),
            //         },
            //         TIMEOUT,
            //     )
            //     .await
            //     .unwrap();
            // log::info!("[{log_identifier}] Updated Response Device: {updated_response2:?}",);
            // assert_eq!(
            //     updated_response1.specification.version.unwrap(),
            //     old_version + 2
            // );
            // let updated_response3 = azure_device_registry_client
            //     .update_device_plus_endpoint_status(
            //         DEVICE1.to_string(),
            //         ENDPOINT1.to_string(),
            //         DeviceStatus {
            //             config: Some(StatusConfig {
            //                 error: None,
            //                 version: Some(old_version),
            //                 last_transition_time: Some(String::from("2025-12-12:00:00Z")),
            //             }),
            //             endpoints: std::collections::HashMap::new(),
            //         },
            //         TIMEOUT,
            //     )
            //     .await
            //     .unwrap();
            // log::info!("[{log_identifier}] Updated Response Device: {updated_response3:?}",);
            // assert_eq!(
            //     updated_response1.specification.version.unwrap(),
            //     old_version
            // );

            azure_device_registry_client
                .unobserve_device_update_notifications(
                    DEVICE1.to_string(),
                    ENDPOINT1.to_string(),
                    TIMEOUT,
                )
                .await
                .unwrap();
            log::info!("[{log_identifier}] Unobservation Device Response: {:?}", ());

            let response_after_unobs = azure_device_registry_client
                .update_device_plus_endpoint_status(
                    DEVICE1.to_string(),
                    ENDPOINT1.to_string(),
                    DeviceStatus {
                        config: None,
                        endpoints: std::collections::HashMap::new(),
                    },
                    TIMEOUT,
                )
                .await
                .unwrap();
            log::info!(
                "[{log_identifier}] Updated Response Device After Unobserve: {response_after_unobs:?}",
            );
            // wait for the receive_notifications_task to finish to ensure any failed asserts are captured.
            assert!(receive_notifications_task.await.is_ok());

            tokio::time::sleep(Duration::from_secs(1)).await;

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

async fn observe_asset_update_notifications(
    azure_device_registry_client: &azure_device_registry::Client<SessionManagedClient>,
) {
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
                    DEVICE1.to_string(),
                    ENDPOINT1.to_string(),
                    ASSET_NAME1.to_string(),
                    TIMEOUT,
                )
                .await
                .unwrap();
            log::info!("[{log_identifier}] Observation Response: {observation:?}",);
            let receive_notifications_task = tokio::task::spawn({
                async move {
                    log::info!("[{log_identifier}] Asset Notification receiver started.");
                    let mut count = 0;
                    if let Some((device, _)) = observation.recv_notification().await {
                        count += 1;
                        log::info!("[{log_identifier}] FIRST OBS DELETE LOG");
                        log::info!("[{log_identifier}] Asset Observation: {device:?}");
                        assert_eq!(device.name, DEVICE1);
                    }
                    // if let Some((device, _)) = observation.recv_notification().await {
                    //     count += 1;
                    //     log::info!("[{log_identifier}] Harry Potter DELETE LOG");
                    //     log::info!("[{log_identifier}] Device From Observation 2: {device:?}");
                    //     assert_eq!(device.name, DEVICE1);
                    // }
                    // if let Some((device, _)) = observation.recv_notification().await {
                    //     count += 1;
                    //     log::info!("[{log_identifier}] Harry Potter DELETE LOG");
                    //     log::info!("[{log_identifier}] Device From Observation 3: {device:?}");
                    // }
                    while let Some((device, _)) = observation.recv_notification().await {
                        count += 1;
                        log::info!("[{log_identifier}] ANY OTHER OBS DELETE LOG");
                        log::info!(
                            "[{log_identifier}] Asset Observation If Any Other Works: {device:?}"
                        );
                        // if something weird happens, this should prevent an infinite loop.
                        // Note that this does prevent getting an accurate count of how many extra unexpected notifications were received
                        assert!(count < 2);
                    }
                    // only the 1 expected notifications should occur
                    assert_eq!(count, 1);
                    log::info!("[{log_identifier}] Asset Notification receiver closed");
                }
            });

            tokio::time::sleep(Duration::from_secs(1)).await;
            // Get the device
            let response = azure_device_registry_client
                .get_asset(
                    DEVICE1.to_string(),
                    ENDPOINT1.to_string(),
                    ASSET_NAME1.to_string(),
                    TIMEOUT,
                )
                .await
                .unwrap();
            log::info!("[{log_identifier}] Get Asset Reponse: {response:?}",);
            //old_version = response.specification.version.unwrap_or(0);
            // log::info!(
            //     "[{log_identifier}] Datetime: {}",
            //     time::OffsetDateTime::now_utc()
            // );
            let mut dataset_statuses = Vec::new();
            for dataset in response.specification.datasets {
                dataset_statuses.push(azure_device_registry::DatasetEventStreamStatus {
                    error: None,
                    message_schema_reference: None,
                    name: dataset.name,
                });
            }
            let status_to_be_updated = azure_device_registry::AssetStatus {
                config: Some(azure_device_registry::StatusConfig {
                    version: response.specification.version,
                    last_transition_time: Some(time::OffsetDateTime::now_utc().to_string()),
                    ..azure_device_registry::StatusConfig::default()
                }),
                datasets: Some(dataset_statuses),
                ..azure_device_registry::AssetStatus::default()
            };
            let updated_response1 = azure_device_registry_client
                .update_asset_status(
                    DEVICE1.to_string(),
                    ENDPOINT1.to_string(),
                    ASSET_NAME1.to_string(),
                    status_to_be_updated,
                    TIMEOUT,
                )
                .await
                .unwrap();
            log::info!(
                "[{log_identifier}] Updated Response Device After Observation: {updated_response1:?}",
            );

            azure_device_registry_client
                .unobserve_device_update_notifications(
                    DEVICE1.to_string(),
                    ENDPOINT1.to_string(),
                    TIMEOUT,
                )
                .await
                .unwrap();
            log::info!("[{log_identifier}] Unobservation Device Response: {:?}", ());

            let updated_response4 = azure_device_registry_client
                .update_device_plus_endpoint_status(
                    DEVICE1.to_string(),
                    ENDPOINT1.to_string(),
                    DeviceStatus {
                        config: None,
                        endpoints: std::collections::HashMap::new(),
                    },
                    TIMEOUT,
                )
                .await
                .unwrap();
            log::info!(
                "[{log_identifier}] Updated Response Device After Unobserve: {updated_response4:?}",
            );
            // wait for the receive_notifications_task to finish to ensure any failed asserts are captured.
            assert!(receive_notifications_task.await.is_ok());

            tokio::time::sleep(Duration::from_secs(1)).await;

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
