// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

use std::{env, sync::Arc, time::Duration};

use test_case::test_case;
use tokio::sync::Notify;

use azure_iot_operations_mqtt::control_packet::QoS;
use azure_iot_operations_mqtt::interface::{ManagedClient, MqttPubSub, PubReceiver};
use azure_iot_operations_mqtt::session::{Session, SessionOptionsBuilder};
use azure_iot_operations_mqtt::MqttConnectionSettingsBuilder;

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

#[test_case(QoS::AtLeastOnce; "QoS 1")]
// #[test_case(QoS::ExactlyOnce; "QoS 2")]
#[tokio::test]
async fn test_simple_manual_ack(qos: QoS) {
    let client_id = "network_test_simple_manual_ack";
    let Ok(mut session) = setup_test(client_id) else {
        // Network tests disabled, skipping tests
        return;
    };
    let exit_handle = session.create_exit_handle();
    let managed_client = session.create_managed_client();

    let topic = "mqtt/test/simple_manual_ack";
    let payload = "Hello, World!";

    let notify_ack = Arc::new(Notify::new());
    let notify_sub = Arc::new(Notify::new());

    // Task for the sender
    let sender = {
        let client = managed_client.clone();
        let notify_ack = notify_ack.clone();
        let notify_sub = notify_sub.clone();
        async move {
            // Wait for subscribe from receiver task
            notify_sub.notified().await;
            // Publish a message
            let ct = client.publish(topic, qos, true, payload).await.unwrap();
            let ct_complete = tokio::task::spawn(ct);
            assert!(!ct_complete.is_finished());
            // Wait for ack from receiver task
            notify_ack.notified().await;
            // Once acked by receiver, the completion token will return
            assert!(ct_complete.await.is_ok());

            // Test is complete, so exit session
            // exit_handle.try_exit().await.unwrap(); // TODO: uncomment once below race condition is fixed
            match exit_handle.try_exit().await {
                Ok(()) => Ok(()),
                Err(e) => {
                    match e {
                        azure_iot_operations_mqtt::session::SessionExitError::BrokerUnavailable { attempted } => {
                            // Because of a current race condition, we need to ignore this as it isn't indicative of a real error
                            if !attempted {
                                return Err(e.to_string());
                            }
                            Ok(())
                        },
                        _ => Err(e.to_string()),
                    }
                }
            }
        }
    };

    // Task for the receiver
    let receiver = {
        let client = managed_client;
        let notify_ack = notify_ack.clone();
        let notify_sub = notify_sub.clone();
        async move {
            // Subscribe
            client.subscribe(topic, qos).await.unwrap().await.unwrap();
            // Notify the sender that the subscription is ready
            notify_sub.notify_one();
            // Wait for message
            let mut receiver = client.create_filtered_pub_receiver(topic, false).unwrap();
            let (publish, ack_token) = receiver.recv().await.unwrap();
            // The message was the correct one
            assert_eq!(publish.payload, payload.as_bytes());
            assert!(ack_token.is_some());
            // Acknowledge the message
            ack_token.unwrap().ack().await.unwrap();
            // Notify the sender that the message was acked
            notify_ack.notify_one();
        }
    };

    let sender_jh = tokio::task::spawn(sender);

    let receiver_jh = tokio::task::spawn(receiver);

    assert!(tokio::try_join!(
        async move { sender_jh.await.map_err(|e| { e.to_string() }) },
        async move { receiver_jh.await.map_err(|e| { e.to_string() }) },
        async move { session.run().await.map_err(|e| { e.to_string() }) },
    )
    .is_ok());
}
