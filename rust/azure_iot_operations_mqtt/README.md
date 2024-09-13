# Azure IoT Operations - MQTT
MQTT version 5.0 client library providing flexibility for decoupled asynchronous applications

[API documentation] |
[Examples](examples) |
[Release Notes](https://github.com/Azure/iot-operations-sdks/releases?q=rust%2Fmqtt&expanded=true)

## Overview
* Easily send and receive messages over MQTT from different tasks in asynchronous applications.
* Automatic reconnect and connection management (with customizable policy)
* Enables you to create decoupled components without the need for considering connection state.

## Simple Send and Receive

```rust
use std::str;
use std::time::Duration;
use azure_iot_operations_mqtt::control_packet::QoS;
use azure_iot_operations_mqtt::interface::{MqttProvider, MqttPubReceiver, MqttPubSub};
use azure_iot_operations_mqtt::session::{
    Session, SessionExitHandle, SessionOptionsBuilder, SessionPubReceiver, SessionPubSub,
};
use azure_iot_operations_mqtt::MqttConnectionSettingsBuilder;

const CLIENT_ID: &str = "aio_example_client";
const HOST: &str = "localhost";
const PORT: u16 = 1883;
const TOPIC: &str = "hello/mqtt";

#[tokio::main(flavor = "current_thread")]
async fn main() {
    // Build the options and settings for the session.
    let connection_settings = MqttConnectionSettingsBuilder::default()
        .client_id(CLIENT_ID)
        .host_name(HOST)
        .tcp_port(PORT)
        .use_tls(false)
        .build()
        .unwrap();
    let session_options = SessionOptionsBuilder::default()
        .connection_settings(connection_settings)
        .build()
        .unwrap();

    // Create a new session.
    let mut session = Session::new(session_options).unwrap();

    // Create PubSubs to send, PubReceivers to receive, and an ExitHandle for exiting.
    let pub_sub1 = session.pub_sub();
    let pub_sub2 = session.pub_sub();
    let receiver = session
        .filtered_pub_receiver(TOPIC, true)
        .unwrap();
    let exit_handle = session.get_session_exit_handle();

    // Send the created components to their respective tasks.
    tokio::spawn(receive_messages(pub_sub1, receiver));
    tokio::spawn(send_messages(pub_sub2, exit_handle));

    // Run the session. This blocks until the session is exited.
    session.run().await.unwrap();
}

/// Indefinitely receive
async fn receive_messages(pub_sub: SessionPubSub, mut receiver: SessionPubReceiver) {
    println!("Subscribing to {TOPIC}");
    pub_sub.subscribe(TOPIC, QoS::AtLeastOnce).await.unwrap();
    loop {
        let msg = receiver.recv().await.unwrap();
        println!("Received: {}", str::from_utf8(&msg.payload).unwrap());
    }
}

/// Publish 10 messages, then exit
async fn send_messages(pub_sub: SessionPubSub, exit_handler: SessionExitHandle) {
    for i in 1..=10 {
        let payload = format!("Hello #{i}");
        println!("Sending: {payload}");
        let comp_token = pub_sub
            .publish(TOPIC, QoS::AtLeastOnce, false, payload)
            .await
            .unwrap();
        comp_token.wait().await.unwrap();
        tokio::time::sleep(Duration::from_secs(1)).await;
    }

    exit_handler.exit_session().await.unwrap();
}
```