// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

//! This example demonstrates how to use the Azure IoT Operations MQTT library to connect to a TLS
//! enabled MQTT broker using a Shared Access Token (SAT) for authentication.
//!
//! The example connects to a local MQTT broker using a SAT and subscribes to a topic. It then
//! sends 10 messages to the topic and exits. The example demonstrates how to create a session with
//! CA and SAT files, create a managed client from the session, and use the managed client to send
//! send and receive messages.

use std::str;
use std::time::Duration;

use env_logger::Builder;

use azure_iot_operations_mqtt::control_packet::QoS;
use azure_iot_operations_mqtt::interface::{ManagedClient, MqttPubSub, PubReceiver};
use azure_iot_operations_mqtt::session::{
    Session, SessionExitHandle, SessionManagedClient, SessionOptionsBuilder,
};
use azure_iot_operations_mqtt::MqttConnectionSettingsBuilder;

const CLIENT_ID: &str = "aio_example_client";
const HOSTNAME: &str = "localhost";
const TOPIC: &str = "hello/mqtt";

// Path to the CA file and SAT auth file.
const CA_FILE: &str = "[PATH TO PEM CA FILE]";
const SAT_FILE: &str = "[PATH TO SAT AUTH FILE]";

#[tokio::main(flavor = "current_thread")]
async fn main() {
    Builder::new()
        .filter_level(log::LevelFilter::Warn)
        .format_timestamp(None)
        .filter_module("rumqttc", log::LevelFilter::Warn)
        .init();

    // Build the options and settings for the session.
    let connection_settings = MqttConnectionSettingsBuilder::default()
        .client_id(CLIENT_ID)
        .hostname(HOSTNAME)
        .ca_file(CA_FILE.to_string())
        .sat_file(SAT_FILE.to_string())
        .build()
        .unwrap();
    let session_options = SessionOptionsBuilder::default()
        .connection_settings(connection_settings)
        .build()
        .unwrap();

    // Create a new session.
    let mut session = Session::new(session_options).unwrap();

    // Spawn tasks for sending and receiving messages using managed clients
    // created from the session.
    tokio::spawn(receive_messages(session.create_managed_client()));
    tokio::spawn(send_messages(
        session.create_managed_client(),
        session.create_exit_handle(),
    ));

    // Run the session. This blocks until the session is exited.
    session.run().await.unwrap();
}

/// Indefinitely receive
async fn receive_messages(client: SessionManagedClient) {
    // Create a receiver from the SessionManagedClient and subscribe to the topic
    let mut receiver = client.create_filtered_pub_receiver(TOPIC, true).unwrap();
    println!("Subscribing to {TOPIC}");
    client.subscribe(TOPIC, QoS::AtLeastOnce).await.unwrap();

    // Receive indefinitely
    loop {
        let (msg, _) = receiver.recv().await.unwrap();
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
