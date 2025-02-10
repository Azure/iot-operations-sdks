// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
use std::io::Write;
use std::str;
use std::time::Duration;

use env_logger::Builder;

use azure_iot_operations_mqtt::control_packet::{Publish, QoS};
use azure_iot_operations_mqtt::interface::{AckToken, ManagedClient, MqttPubSub, PubReceiver};
use azure_iot_operations_mqtt::session::{
    Session, SessionExitHandle, SessionManagedClient, SessionOptionsBuilder,
};
use azure_iot_operations_mqtt::MqttConnectionSettingsBuilder;

const CLIENT_ID: &str = "aio_example_client";
const HOSTNAME: &str = "localhost";
const PORT: u16 = 1883;
const TOPIC: &str = "hello/mqtt";

const STORAGE_FILE: &str = "[PATH TO STORAGE FILE]";

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
    let mut receiver = client.create_filtered_pub_receiver(TOPIC).unwrap();
    println!("Subscribing to {TOPIC}");
    client.subscribe(TOPIC, QoS::AtLeastOnce).await.unwrap();

    // Receive indefinitely
    loop {
        let (msg, ack_token) = receiver.recv_manual_ack().await.unwrap();
        tokio::spawn(store_and_acknowledge(msg, ack_token));
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

async fn store_and_acknowledge(publish: Publish, ack_token: Option<AckToken>) {
    let payload = str::from_utf8(&publish.payload).unwrap();
    println!("Received: {payload}");
    // Store the message in a file
    let mut file = std::fs::OpenOptions::new()
        .create(true)
        .append(true)
        .open(STORAGE_FILE)
        .unwrap();
    writeln!(file, "{payload}").unwrap();
    // Acknowledge the message once it is stored
    if let Some(ack_token) = ack_token {
        let comp_token = ack_token.ack().await.unwrap();
        comp_token.await.unwrap();
    }
}
