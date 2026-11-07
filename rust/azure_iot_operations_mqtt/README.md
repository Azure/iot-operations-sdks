# Azure IoT Operations - MQTT
MQTT version 5.0 client library providing flexibility for decoupled asynchronous applications

[Examples](examples) |
[Release Notes](https://github.com/Azure/iot-operations-sdks/releases?q=rust%2Fmqtt&expanded=true)

## Overview
* Easily send and receive messages over MQTT from different tasks in asynchronous applications.
* Automatic reconnect and connection management (with customizable policy)
* Enables you to create decoupled components without the need for considering connection state.

## MQTT Implementation

The low-level MQTT implementation is provided by
[ms-mqtt-client 0.1.0](https://crates.io/crates/ms-mqtt-client/0.1.0). The SDK adds managed
sessions and retains its existing control-packet, error, and completion-token re-exports.
It enables the authentication-method omission bypass required for AIO MQ versions before 1.6.2.

QoS 0 and QoS 1 are supported. QoS 2 subscription requests currently panic in the underlying
client; this limitation is tracked by a TODO in the SDK's subscription implementation.

The `test-utils` feature retains the `azure_mqtt` alias for integration-test compatibility.
Low-level protocol internals exposed through that alias are not part of the stable SDK API.

## Simple Send and Receive
The Azure IoT Operations MQTT crate is intended for use with the Azure IoT Operations MQ broker, but is compatible with any MQTTv5 broker, local or remote.

```rust, no_run
use std::str;
use std::time::Duration;
use azure_iot_operations_mqtt::aio::connection_settings::MqttConnectionSettingsBuilder;
use azure_iot_operations_mqtt::control_packet::{PublishProperties, QoS, RetainOptions, SubscribeProperties, TopicFilter, TopicName};
use azure_iot_operations_mqtt::session::{Session, SessionManagedClient, SessionOptionsBuilder, SessionExitHandle};

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
    let topic_filter = TopicFilter::new(TOPIC).unwrap();
    let mut receiver = client.create_filtered_pub_receiver(topic_filter.clone());
    println!("Subscribing to {TOPIC}");
    client
        .subscribe(
            topic_filter,
            QoS::AtLeastOnce,
            false,
            RetainOptions::default(),
            SubscribeProperties::default(),
        )
        .await
        .unwrap();

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
            .publish_qos1(
                TopicName::new(TOPIC).unwrap(),
                false,
                payload,
                PublishProperties::default()
            )
            .await
            .unwrap();
        comp_token.await.unwrap();
        tokio::time::sleep(Duration::from_secs(1)).await;
    }

    exit_handler.try_exit().unwrap();
}
```