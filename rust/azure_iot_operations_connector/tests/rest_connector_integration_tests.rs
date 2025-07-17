// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

use std::{env, sync::Arc, time::Duration};

use tokio::sync::Notify;

use azure_iot_operations_mqtt::MqttConnectionSettingsBuilder;
use azure_iot_operations_mqtt::control_packet::QoS;
use azure_iot_operations_mqtt::interface::{ManagedClient, MqttPubSub, PubReceiver};
use azure_iot_operations_mqtt::session::{Session, SessionOptionsBuilder};

fn setup_test(client_id: &str) -> Result<Session, ()> {
    let _ = env_logger::Builder::new()
        .filter_level(log::LevelFilter::max())
        .format_timestamp(None)
        .filter_module("rumqttc", log::LevelFilter::Warn)
        .filter_module("azure_iot_operations", log::LevelFilter::Warn)
        .try_init();
    if env::var("ENABLE_NETWORK_TESTS").is_err() {
        log::warn!("This test is skipped. Set ENABLE_NETWORK_TESTS to run.");
        return Err(());
    }

    let connection_settings = MqttConnectionSettingsBuilder::default()
        .client_id(client_id)
        .hostname("localhost")
        .tcp_port(1883u16)
        .keep_alive(Duration::from_secs(5))
        .clean_start(true)
        .use_tls(false)
        .build()
        .unwrap();
    let session_options = SessionOptionsBuilder::default()
        .connection_settings(connection_settings)
        .build()
        .unwrap();
    let session = Session::new(session_options).unwrap();
    Ok(session)
}

#[tokio::test]
async fn test_connector_simple_recv() {
    let client_id = "network_test_connector_simple_recv";
    let Ok(session) = setup_test(client_id) else {
        // Network tests disabled, skipping tests
        return;
    };
    let exit_handle = session.create_exit_handle();
    let managed_client = session.create_managed_client();

    let topic = "weather/sensor";
    let payload = "simple_recv_test_payload";

    let receiver_done = Arc::new(Notify::new());
    // Task for the receiving client
    let receiver = {
        let client = managed_client.clone();
        // let notify_sub = notify_sub.clone();
        let receiver_done = receiver_done.clone();
        async move {
            let mut receiver = client.create_filtered_pub_receiver(topic).unwrap();
            // Subscribe
            client
                .subscribe(topic, QoS::AtLeastOnce)
                .await
                .unwrap()
                .await
                .unwrap();
            // let publish = receiver.recv().await.unwrap();
            let publish = tokio::time::timeout(Duration::from_secs(30), receiver.recv())
                .await
                .unwrap();

            log::warn!(
                "The published payload is: {}",
                String::from_utf8_lossy(&publish.payload)
            );

            assert_eq!(publish.payload, payload.as_bytes());
            // Indicate completion
            receiver_done.notify_one();
        }
    };

    let test_complete = async move {
        receiver_done.notified().await;
        exit_handle.try_exit().await
    };

    assert!(
        tokio::try_join!(
            async move {
                tokio::task::spawn(receiver)
                    .await
                    .map_err(|e| e.to_string())
            },
            async move { test_complete.await.map_err(|e| { e.to_string() }) },
            async move { session.run().await.map_err(|e| { e.to_string() }) },
        )
        .is_ok()
    );
}
