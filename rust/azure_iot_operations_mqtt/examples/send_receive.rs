// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
use std::str;
use std::time::Duration;

use env_logger::Builder;

use azure_iot_operations_mqtt::MqttConnectionSettingsBuilder;
use azure_iot_operations_mqtt::control_packet::{
    PublishProperties, QoS, RetainHandling, SubscribeProperties, TopicFilter, TopicName,
};
use azure_iot_operations_mqtt::session::{
    Session, SessionExitHandle, SessionManagedClient, SessionOptionsBuilder,
};

const CLIENT_ID: &str = "aio_send_receive_client";
const HOSTNAME: &str = "localhost";

const PORT: u16 = 1883;
const TOPIC: &str = "hello/mqtt";

#[tokio::main(flavor = "current_thread")]
async fn main() -> Result<(), Box<dyn std::error::Error>> {
    Builder::new()
        .filter_level(log::LevelFilter::Warn)
        .format_timestamp(None)
        .filter_module("azure_mqttqtt", log::LevelFilter::Warn)
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
    let topic_filter = TopicFilter::new(TOPIC)?;
    let mut receiver = client.create_filtered_pub_receiver(topic_filter.clone());

    // Subscribe to the topic and wait for the subscription to be acknowledged
    client
        .subscribe(
            topic_filter,
            QoS::AtLeastOnce,
            false,
            false,
            RetainHandling::DoNotSend,
            SubscribeProperties::default(),
        )
        .await?
        .await?;
    println!("Subscribed to topic");

    // Receive until there are no more messages
    while let Some(msg) = receiver.recv().await {
        println!("Received: {:?}", msg.payload);
    }

    Ok(())
}

/// Indefinitely send messages and wait for acknowledgement.
async fn send_messages(
    client: SessionManagedClient,
) -> Result<(), Box<dyn std::error::Error + Send + Sync>> {
    let mut i = 0;

    let topic_name = TopicName::new(TOPIC).unwrap();
    loop {
        i += 1;
        let payload = format!("Hello #{i}");
        // Send message and receive a CompletionToken which will notify when the message is acknowledged
        let completion_token = client
            .publish_qos1(
                topic_name.clone(),
                false,
                payload,
                PublishProperties::default(),
            )
            .await?;
        println!("Sent message #{i}");
        match completion_token.await {
            Ok(_) => println!("Message #{i} acknowledgement received"),
            Err(e) => {
                println!("Message #{i} delivery failure: {e}");
            }
        }
        tokio::time::sleep(Duration::from_secs(1)).await;
    }
}

// Exit the Session
async fn exit(exit_handle: SessionExitHandle) {
    match exit_handle.try_exit() {
        Ok(()) => println!("Session exited gracefully"),
        Err(e) => {
            println!("Graceful session exit failed: {e}");
            println!("Forcing session exit");
            exit_handle.exit_force().await;
        }
    }
}
