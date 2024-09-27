// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
use std::str;
use std::time::Duration;

use env_logger::Builder;

use azure_iot_operations_mqtt::control_packet::QoS;
use azure_iot_operations_mqtt::interface::{ManagedClient, MqttPubReceiver, MqttPubSub};
use azure_iot_operations_mqtt::session::{
    Session, SessionExitHandle, SessionManagedClient, SessionOptionsBuilder, SessionPubReceiver,
};
use azure_iot_operations_mqtt::MqttConnectionSettingsBuilder;

const CLIENT_ID: &str = "aio_example_client";
const HOST: &str = "localhost";
const PORT: u16 = 1883;
const TOPIC: &str = "hello/mqtt";

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
    let managed_client1 = session.get_managed_client();
    let managed_client2 = session.get_managed_client();
    let exit_handle = session.get_session_exit_handle();

    // Send the created components to their respective tasks.
    tokio::spawn(receive_messages(managed_client1));
    tokio::spawn(send_messages(managed_client2, exit_handle));

    // Run the session. This blocks until the session is exited.
    session.run().await.unwrap();
}

/// Indefinitely receive
async fn receive_messages(client: SessionManagedClient) {
    // Create a receiver from the SessionManagedClient and subscribe to the topic
    let mut receiver = client.filtered_pub_receiver(TOPIC, true).unwrap();
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
        comp_token.wait().await.unwrap();
        tokio::time::sleep(Duration::from_secs(1)).await;
    }

    exit_handler.try_exit().await.unwrap();
}
