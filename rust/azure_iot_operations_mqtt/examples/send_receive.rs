// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
use std::str;
use std::time::Duration;

use env_logger::Builder;

use azure_iot_operations_mqtt::aio::connection_settings::MqttConnectionSettingsBuilder;
use azure_iot_operations_mqtt::control_packet::{
    PublishProperties, QoS, RetainOptions, SubscribeProperties, TopicFilter, TopicName,
};
use azure_iot_operations_mqtt::session::{Session, SessionManagedClient, SessionOptionsBuilder};

const CLIENT_ID_1: &str = "aio_send_receive_client_1";
const CLIENT_ID_2: &str = "aio_send_receive_client_2";
const HOSTNAME: &str = "localhost";

const PORT: u16 = 1883;
// const TOPIC: &str = "clients/timtay-dotnet-invoker/rpc/command-samples/someCommandName";
const REQUEST_TOPIC: &str = "timtay/requestTopic";
const RESPONSE_TOPIC: &str = "timtay/responseTopic";

#[tokio::main(flavor = "current_thread")]
async fn main() -> Result<(), Box<dyn std::error::Error>> {
    Builder::new()
        .filter_level(log::LevelFilter::Warn)
        .format_timestamp(None)
        .filter_module("azure_mqtt", log::LevelFilter::Warn)
        .init();

    // Build the options, connection settings, and session 1
    let connection_settings_1 = MqttConnectionSettingsBuilder::default()
        .client_id(CLIENT_ID_1)
        .hostname(HOSTNAME)
        .tcp_port(PORT)
        .use_tls(false)
        .build()?;
    let session_options_1 = SessionOptionsBuilder::default()
        .connection_settings(connection_settings_1)
        .build()?;

    // Create a new session.
    let session_1 = Session::new(session_options_1)?;

    // Build the options, connection settings, and session 1
    let connection_settings_2 = MqttConnectionSettingsBuilder::default()
        .client_id(CLIENT_ID_2)
        .hostname(HOSTNAME)
        .tcp_port(PORT)
        .use_tls(false)
        .build()?;
    let session_options_2 = SessionOptionsBuilder::default()
        .connection_settings(connection_settings_2)
        .build()?;

    // Create a new session.
    let session_2 = Session::new(session_options_2)?;

    // Run the Session and the program concurrently
    let result = tokio::join!(
        client_1_task(session_1.create_managed_client()),
        client_2_task(session_2.create_managed_client()),
        session_1.run(),
        session_2.run(),
    );
    Ok(result.2?)
}

// Client 1 task
async fn client_1_task(client: SessionManagedClient) {
    let topic_name = TopicName::new(REQUEST_TOPIC).unwrap();
    let topic_filter = TopicFilter::new(RESPONSE_TOPIC).unwrap();

    // Subscribe to response topic
    let mut receiver = client.create_filtered_pub_receiver(topic_filter.clone());
    client
        .subscribe(
            topic_filter,
            QoS::AtLeastOnce,
            false,
            RetainOptions::default(),
            SubscribeProperties::default(),
        )
        .await
        .unwrap()
        .await
        .unwrap();

    loop {
        // Wait a second
        tokio::time::sleep(Duration::from_secs(1)).await;

        // Publish a QoS 1 message to request topic
        let completion_token = client
            .publish_qos1(
                topic_name.clone(),
                false,
                "Hello from client 1",
                PublishProperties::default(),
            )
            .await;

        match completion_token {
            Ok(token) => match token.await {
                Ok(_) => println!("Client 1: Message acknowledgement received"),
                Err(e) => {
                    println!("Client 1: Message delivery failure: {e}");
                    continue;
                }
            },
            Err(e) => {
                println!("Client 1: Failed to publish message: {e}");
                continue;
            }
        }

        // Wait for a message on the response topic
        match receiver.recv().await {
            Some(publish) => {
                let payload_str = str::from_utf8(&publish.payload).unwrap_or("<invalid utf-8>");
                println!("Client 1: Received response: {payload_str}");
            }
            None => {
                println!("Client 1: Response topic receiver closed");
                break;
            }
        }
    }
}

// Client 2 task
async fn client_2_task(client: SessionManagedClient) {
    let topic_name = TopicName::new(RESPONSE_TOPIC).unwrap();
    let topic_filter = TopicFilter::new(REQUEST_TOPIC).unwrap();

    // Subscribe to request topic
    let mut receiver = client.create_filtered_pub_receiver(topic_filter.clone());
    client
        .subscribe(
            topic_filter,
            QoS::AtLeastOnce,
            false,
            RetainOptions::default(),
            SubscribeProperties::default(),
        )
        .await
        .unwrap()
        .await
        .unwrap();

    loop {
        // Wait a second
        tokio::time::sleep(Duration::from_secs(1)).await;

        // Wait for a message on the request topic
        match receiver.recv().await {
            Some(publish) => {
                let payload_str = str::from_utf8(&publish.payload).unwrap_or("<invalid utf-8>");
                println!("Client 2: Received request: {payload_str}");

                // Publish a response
                let completion_token = client
                    .publish_qos1(
                        topic_name.clone(),
                        false,
                        "Hello from client 2",
                        PublishProperties::default(),
                    )
                    .await;

                match completion_token {
                    Ok(token) => match token.await {
                        Ok(_) => println!("Client 2: Response message acknowledgement received"),
                        Err(e) => {
                            println!("Client 2: Response message delivery failure: {e}");
                            continue;
                        }
                    },
                    Err(e) => {
                        println!("Client 2: Failed to publish response message: {e}");
                        continue;
                    }
                }
            }
            None => {
                println!("Client 2: Request topic receiver closed");
                break;
            }
        }
    }
}
