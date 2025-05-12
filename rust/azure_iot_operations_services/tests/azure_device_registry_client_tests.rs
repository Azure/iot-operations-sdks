// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

#![cfg(feature = "azure_device_registry")]

use std::{env, time::Duration};

use env_logger::Builder;

use azure_iot_operations_mqtt::MqttConnectionSettingsBuilder;
use azure_iot_operations_mqtt::session::{
    Session, SessionExitHandle, SessionManagedClient, SessionOptionsBuilder,
};
use azure_iot_operations_protocol::application::ApplicationContextBuilder;
use azure_iot_operations_services::azure_device_registry::{self, DeviceStatus, StatusConfig};

const DEVICE1: &str = "my-thermostat";
#[allow(dead_code)]
const DEVICE2: &str = "test-thermostat";
const ENDPOINT1: &str = "my-rest-endpoint";
const ASSET_NAME1: &str = "my-rest-thermostat-asset";
const TYPE: &str = "thermostat";
const TIMEOUT: Duration = Duration::from_secs(10);

// Test Scenarios:
// get device
// update status of device
// observe device telemetry
// unobserve device telemetry

fn setup_test(
    client_id: &str,
) -> Result<
    (
        Session,
        azure_device_registry::Client<SessionManagedClient>,
        SessionExitHandle,
    ),
    (),
> {
    let _ = Builder::new()
        .filter_level(log::LevelFilter::max())
        .format_timestamp(None)
        .filter_module("rumqttc", log::LevelFilter::Warn)
        .filter_module("azure_iot_operations", log::LevelFilter::Warn)
        .try_init();
    // TODO Uncomment this to enable network tests
    // if env::var("ENABLE_NETWORK_TESTS").is_err() {
    //     log::warn!("This test is skipped. Set ENABLE_NETWORK_TESTS to run.");
    //     return Err(());
    // }

    let connection_settings = MqttConnectionSettingsBuilder::default()
        .client_id(client_id)
        .hostname("localhost")
        .tcp_port(31883u16)
        // TODO Uncomment this
        //.tcp_port(1883u16)
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
    Ok((session, azure_device_registry_client, exit_handle))
}

#[tokio::test]
async fn get_device() {
    let log_identifier = "get_device";
    let Ok((session, azure_device_registry_client, exit_handle)) =
        setup_test("get_device_network_tests-rust")
    else {
        // Network tests disabled, skipping tests
        return;
    };

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
async fn update_device_plus_endpoint_status() {
    let log_identifier = "update_device_plus_endpoint_status";
    let Ok((session, azure_device_registry_client, exit_handle)) =
        setup_test("update_device_plus_endpoint_status_network_tests-rust")
    else {
        // Network tests disabled, skipping tests
        return;
    };

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
            let updated_device_response = azure_device_registry_client
                .update_device_plus_endpoint_status(
                    DEVICE1.to_string(),
                    ENDPOINT1.to_string(),
                    updated_status,
                    TIMEOUT,
                )
                .await
                .unwrap();
            log::info!("[{log_identifier}] Updated Response Device: {updated_device_response:?}",);

            assert_eq!(updated_device_response.name, DEVICE1);
            assert_eq!(
                updated_device_response.specification.attributes["deviceId"],
                DEVICE1
            );
            assert_eq!(
                updated_device_response.specification.attributes["deviceType"],
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
async fn get_asset() {
    let log_identifier = "get_asset";
    let Ok((session, azure_device_registry_client, exit_handle)) =
        setup_test("get_asset_network_tests-rust")
    else {
        // Network tests disabled, skipping tests
        return;
    };

    let test_task = tokio::task::spawn({
        async move {
            let response = azure_device_registry_client
                .get_asset(
                    DEVICE1.to_string(),
                    ENDPOINT1.to_string(),
                    ASSET_NAME1.to_string(),
                    TIMEOUT,
                )
                .await
                .unwrap();
            log::info!("[{log_identifier}] Response: {response:?}",);

            assert_eq!(response.name, ASSET_NAME1);
            assert_eq!(response.specification.attributes["assetId"], ASSET_NAME1);
            assert_eq!(response.specification.attributes["assetType"], TYPE);
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
