// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
use std::str;
use std::time::Duration;

use env_logger::Builder;

use azure_iot_operations_mqtt::control_packet::QoS;
use azure_iot_operations_mqtt::error::ConnectionError;
use azure_iot_operations_mqtt::interface::{ManagedClient, MqttPubSub, PubReceiver};
use azure_iot_operations_mqtt::session::{
    reconnect_policy::ReconnectPolicy, Session, SessionExitHandle, SessionManagedClient,
    SessionOptionsBuilder,
};
use azure_iot_operations_mqtt::MqttConnectionSettingsBuilder;

const CLIENT_ID: &str = "aio_example_client";
const HOSTNAME: &str = "localhost";
const PORT: u16 = 1883;
const TOPIC: &str = "hello/mqtt";

#[tokio::main(flavor = "current_thread")]
async fn main() -> Result<(), Box<dyn std::error::Error>> {
    Builder::new()
        .filter_level(log::LevelFilter::Warn)
        .format_timestamp(None)
        .filter_module("rumqttc", log::LevelFilter::Warn)
        .init();

    // Build the options and settings for the session.
    let connection_settings = MqttConnectionSettingsBuilder::default()
        .client_id(CLIENT_ID)
        .hostname(HOSTNAME)
        .tcp_port(PORT)
        .use_tls(false)
        .build()?;
    let session_options = SessionOptionsBuilder::default()
        .connection_settings(connection_settings)
        .reconnect_policy(Box::new(CustomReconnectPolicy::default()))
        .build()?;

    // Create a new session.
    let session = Session::new(session_options).unwrap();

    // Spawn tasks for sending and receiving messages using managed clients
    // created from the session.
    tokio::spawn(receive_messages(session.create_managed_client()));
    tokio::spawn(send_messages(
        session.create_managed_client(),
        session.create_exit_handle(),
    ));

    // Run the session. This blocks until the session is exited.
    session.run().await?;

    Ok(())
}

/// Indefinitely receive
async fn receive_messages(client: SessionManagedClient) {
    // Create a receiver from the SessionManagedClient and subscribe to the topic
    let mut receiver = client.create_filtered_pub_receiver(TOPIC).unwrap();
    println!("Subscribing to {TOPIC}");
    client.subscribe(TOPIC, QoS::AtLeastOnce).await.unwrap();

    // Receive indefinitely
    loop {
        let msg = receiver.recv().await.unwrap();
        println!("Received: {}", str::from_utf8(&msg.payload).unwrap());
    }
}

/// Publish 10 messages, then exit
async fn send_messages(client: SessionManagedClient, exit_handler: SessionExitHandle) {
    for i in 1..=10 {
        let payload = format!("Hello #{i}");
        println!("Sending: {payload}");
        let comp_token = client
            .publish(TOPIC, QoS::AtLeastOnce, false, payload)
            .await
            .unwrap();
        comp_token.await.unwrap();
        tokio::time::sleep(Duration::from_secs(1)).await;
    }

    exit_handler.try_exit().await.unwrap();
}

/// Custom reconnect policy for Session that will attempt to reconnect only 10 times
/// with a 1 second delay between each attempt
#[derive(Default)]
struct CustomReconnectPolicy {}

impl ReconnectPolicy for CustomReconnectPolicy {
    fn next_reconnect_delay(
        &self,
        prev_attempts: u32,
        _error: &ConnectionError,
    ) -> Option<Duration> {
        if prev_attempts < 10 {
            Some(Duration::from_secs(1))
        } else {
            None
        }
    }
}
