// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
use std::str;
use std::time::Duration;

use env_logger::Builder;

use azure_iot_operations_mqtt::control_packet::QoS;
use azure_iot_operations_mqtt::interface::MqttPubSub;
use azure_iot_operations_mqtt::session::{Session, SessionManagedClient, SessionOptionsBuilder};
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
        .build()?;

    // Create a new session.
    let session = Session::new(session_options)?;

    // Spawn task for sending messages using ManagedClient created from the Session.
    tokio::spawn(send_messages(session.create_managed_client()));

    // Run the session. This blocks until the session is exited.
    session.run().await?;
    Ok(())
}

/// Indefinitely send messages every second
async fn send_messages(client: SessionManagedClient) {
    let mut i = 0;
    loop {
        i += 1;
        let payload = format!("Hello #{i}");
        match client
            .publish(TOPIC, QoS::AtLeastOnce, false, payload)
            .await
        {
            Ok(_) => println!("Sent message #{i}"),
            Err(e) => {
                println!("Error sending message: {e}");
            }
        }
        tokio::time::sleep(Duration::from_secs(1)).await;
    }
}
