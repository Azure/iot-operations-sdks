// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
use std::io::Write;
use std::str;
use std::time::Duration;

use env_logger::Builder;

use azure_iot_operations_mqtt::MqttConnectionSettingsBuilder;
use azure_iot_operations_mqtt::control_packet::{Publish, QoS};
use azure_iot_operations_mqtt::interface::{ManagedClient, MqttPubSub, PubReceiver};
use azure_iot_operations_mqtt::session::{
    Session, SessionExitHandle, SessionManagedClient, SessionOptionsBuilder,
};

const CLIENT_ID: &str = "aio_example_client";
const HOSTNAME: &str = "localhost";
const PORT: u16 = 1883;
const TOPIC: &str = "hello/mqtt";

const STORAGE_FILE: &str = "[PATH TO STORAGE FILE]";

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
        .build()?;

    // Create a new session.
    let session = Session::new(session_options)?;

    // Run the Session and the program concurrently
    let result = tokio::join!(
        run_program(
            session.create_managed_client(),
            session.create_exit_handle()
        ),
        session.run(),
    );
    Ok(result.1?)
}

/// Run program logic with an active Session
async fn run_program(client: SessionManagedClient, exit_handle: SessionExitHandle) {
    match tokio::try_join!(receive_messages(client.clone()), send_messages(client)) {
        Ok(_) => {
            // Program runs indefinitely, this shouldn't happen.
            unreachable!();
        }
        Err(e) => {
            println!("Program failed: {e}");
            exit(exit_handle).await;
        }
    }
}

/// Indefinitely receive messages
async fn receive_messages(
    client: SessionManagedClient,
) -> Result<(), Box<dyn std::error::Error + Send + Sync>> {
    // Create a receiver from the SessionManagedClient and subscribe to the topic
    let mut receiver = client.create_filtered_pub_receiver(TOPIC)?;

    // Subscribe to the topic and wait for the subscription to be acknowledged
    client.subscribe(TOPIC, QoS::AtLeastOnce).await?.await?;
    println!("Subscribed to topic");

    // Receive until there are no more messages
    while let Some((msg, ack_token)) = receiver.recv_manual_ack().await {
        println!("Received: {:?}", msg.payload);

        match store_message(&msg) {
            Ok(()) => {
                println!("Stored message");
                // Acknowledge the message once it is stored
                if let Some(ack_token) = ack_token {
                    let completion_token = ack_token.ack().await?;
                    match completion_token.await {
                        Ok(()) => println!("Sent message acknowledgement"),
                        Err(e) => println!("Error acknowledging message: {e}"),
                    }
                }
            }
            Err(e) => println!("Error storing message: {e}"),
        }
    }

    Ok(())
}

/// Indefinitely send messages and wait for acknowledgement.
async fn send_messages(
    client: SessionManagedClient,
) -> Result<(), Box<dyn std::error::Error + Send + Sync>> {
    let mut i = 0;

    loop {
        i += 1;
        let payload = format!("Hello #{i}");
        // Send message and receive a CompletionToken which will notify when the message is acknowledged
        let completion_token = client
            .publish(TOPIC, QoS::AtLeastOnce, false, payload)
            .await?;
        println!("Sent message #{i}");
        match completion_token.await {
            Ok(()) => println!("Message #{i} acknowledgement received"),
            Err(e) => {
                println!("Message #{i} delivery failure: {e}");
            }
        }
        tokio::time::sleep(Duration::from_secs(1)).await;
    }
}

/// Write the message to disk
fn store_message(publish: &Publish) -> Result<(), Box<dyn std::error::Error + Send + Sync>> {
    let payload = str::from_utf8(&publish.payload)?;
    // Store the message in a file
    let mut file = std::fs::OpenOptions::new()
        .create(true)
        .append(true)
        .open(STORAGE_FILE)?;
    writeln!(file, "{payload}")?;
    Ok(())
}

/// Exit the Session
async fn exit(exit_handle: SessionExitHandle) {
    match exit_handle.try_exit().await {
        Ok(()) => println!("Session exited gracefully"),
        Err(e) => {
            println!("Graceful session exit failed: {e}");
            println!("Forcing session exit");
            exit_handle.exit_force().await;
        }
    }
}
