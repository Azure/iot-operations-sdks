# Azure IoT Operations - MQTT
MQTT version 5.0 client library providing flexibility for decoupled asynchronous applications

[API documentation](https://azure.github.io/iot-operations-sdks/rust/azure_iot_operations_mqtt) |
[Examples](examples) |
[Release Notes](https://github.com/Azure/iot-operations-sdks/releases?q=rust%2Fmqtt&expanded=true)

## Overview
* Easily send and receive messages over MQTT from different tasks in asynchronous applications.
* Automatic reconnect and connection management (with customizable policy)
* Enables you to create decoupled components without the need for considering connection state.

## Simple Send and Receive
The Azure IoT Operations MQTT crate is intended for use with the Azure IoT Operations MQ broker, but is compatible with any MQTTv5 broker, local or remote.

```rust, no_run
use std::str;
use std::time::Duration;
use azure_iot_operations_mqtt::control_packet::QoS;
use azure_iot_operations_mqtt::interface::{ManagedClient, PubReceiver, MqttPubSub};
use azure_iot_operations_mqtt::session::{
    Session, SessionManagedClient, SessionExitHandle, SessionOptionsBuilder,
};
use azure_iot_operations_mqtt::MqttConnectionSettingsBuilder;

const CLIENT_ID: &str = "aio_example_client";
const HOSTNAME: &str = "localhost";
const PORT: u16 = 1883;
const TOPIC: &str = "hello/mqtt";

#[tokio::main(flavor = "current_thread")]
async fn main() {
    // Build the options and settings for the session.
    let connection_settings = MqttConnectionSettingsBuilder::default()
        .client_id(CLIENT_ID)
        .hostname(HOSTNAME)
        .tcp_port(PORT)
        .use_tls(false)
        .build()
        .unwrap();
    let session_options = SessionOptionsBuilder::default()
        .connection_settings(connection_settings)
        .build()
        .unwrap();

    // Create a new session.
    let session = Session::new(session_options).unwrap();

    // Spawn tasks for sending and receiving messages using managed clients
    // created from the session.
    tokio::spawn(receive_messages(session.create_managed_client()));
    tokio::spawn(send_messages(session.create_managed_client(), session.create_exit_handle()));

    // Run the session. This blocks until the session is exited.
    session.run().await.unwrap();
}

/// Indefinitely receive
async fn receive_messages(client: SessionManagedClient) {
    // Create a receiver from the SessionManagedClient and subscribe to the topic
    let mut receiver = client.create_filtered_pub_receiver(TOPIC).unwrap();
    println!("Subscribing to {TOPIC}");
    client.subscribe(TOPIC, QoS::AtLeastOnce).await.unwrap();

    // Receive indefinitely
    while let Some(msg) = receiver.recv().await {
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
```