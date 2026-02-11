// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
use std::str;
use std::time::Duration;
use chrono::prelude::*;

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
        client_1_task(session_1.create_managed_client(), session_2.create_managed_client()),
        session_1.run(),
        session_2.run(),
    );
    Ok(result.2?)
}

// Client 1 task
async fn client_1_task(client1: SessionManagedClient, client2: SessionManagedClient) {
    let topic1_name = TopicName::new(REQUEST_TOPIC).unwrap();
    let topic1_filter = TopicFilter::new(RESPONSE_TOPIC).unwrap();
    let topic2_name = TopicName::new(RESPONSE_TOPIC).unwrap();
    let topic2_filter = TopicFilter::new(REQUEST_TOPIC).unwrap();

    // Subscribe to response topic
    let mut receiver1 = client1.create_filtered_pub_receiver(topic1_filter.clone());
    client1
        .subscribe(
            topic1_filter,
            QoS::AtLeastOnce,
            false,
            RetainOptions::default(),
            SubscribeProperties::default(),
        )
        .await
        .unwrap()
        .await
        .unwrap();

    let mut receiver2 = client2.create_filtered_pub_receiver(topic2_filter.clone());
    client2
        .subscribe(
            topic2_filter,
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

        let utc: DateTime<Utc> = Utc::now();
        println!("{}", utc);
        // Publish a QoS 1 message to request topic
        let completion_token1 = client1
            .publish_qos1(
                topic1_name.clone(),
                false,
                "Hello from client 1",
                PublishProperties::default(),
            )
            .await;
        let completion_token2 = client2
            .publish_qos1(
                topic2_name.clone(),
                false,
                "Hello from client 2",
                PublishProperties::default(),
            )
            .await;
/*
        match completion_token1 {
            Ok(token) => match token.await {
                Ok(_) => {},
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

        match completion_token2 {
            Ok(token) => match token.await {
                Ok(_) => {},
                Err(e) => {
                    println!("Client 2: Message delivery failure: {e}");
                    continue;
                }
            },
            Err(e) => {
                println!("Client 2: Failed to publish message: {e}");
                continue;
            }
        }
*/
        match receiver1.recv().await {
            Some(publish) => {
                let payload_str = str::from_utf8(&publish.payload).unwrap_or("<invalid utf-8>");
                let utc2: DateTime<Utc> = Utc::now();
                println!("{}", utc2);
            }
            None => {
                break;
            }
        }

        match receiver2.recv().await {
            Some(publish) => {
                let payload_str = str::from_utf8(&publish.payload).unwrap_or("<invalid utf-8>");
                let utc2: DateTime<Utc> = Utc::now();
                println!("{}", utc2);
            }
            None => {
                break;
            }
        }

        println!("");
    }
}
