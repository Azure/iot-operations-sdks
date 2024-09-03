// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
use std::str;
use std::time::Duration;

use env_logger::Builder;

use azure_iot_operations_mqtt::control_packet::QoS;
use azure_iot_operations_mqtt::interface::{MqttProvider, MqttPubReceiver, MqttPubSub};
use azure_iot_operations_mqtt::session::{
    Session, SessionExitHandle, SessionOptionsBuilder, SessionPubReceiver, SessionPubSub,
};
use azure_iot_operations_mqtt::MqttConnectionSettingsBuilder;

const CLIENT_ID: &str = "<client id>";
const HOST: &str = "<broker host>";
const PORT: u16 = 1883;
const SUBSCRIBE_TOPIC: &str = "<sub topic>";
const PUBLISH_TOPIC: &str = "<publish topic>";

#[tokio::main(flavor = "current_thread")]
async fn main() {
    Builder::new()
        .filter_level(log::LevelFilter::max())
        .format_timestamp(None)
        .filter_module("rumqttc", log::LevelFilter::Warn)
        .init();

    let connection_settings = MqttConnectionSettingsBuilder::default()
        .client_id(CLIENT_ID)
        .host_name(HOST)
        .tcp_port(PORT)
        .keep_alive(Duration::from_secs(5))
        .use_tls(false)
        .build()
        .unwrap();

    let session_options = SessionOptionsBuilder::default()
        .connection_settings(connection_settings)
        .build()
        .unwrap();

    let mut session = Session::new(session_options).unwrap();

    let receiver = session
        .filtered_pub_receiver(SUBSCRIBE_TOPIC, true)
        .unwrap();
    let pub_sub = session.pub_sub();
    let exit_handle = session.get_session_exit_handle();

    tokio::spawn(receive_loop(receiver));
    tokio::spawn(do_sub_and_publishes(pub_sub, exit_handle));

    session.run().await.unwrap();
}

/// Indefinitely receive
async fn receive_loop(mut receiver: SessionPubReceiver) {
    loop {
        let msg = receiver.recv().await.unwrap();
        println!("Received: {}", str::from_utf8(&msg.payload).unwrap());
    }
}

/// Subscribe, publish 10 messages, then disconnect
async fn do_sub_and_publishes(pub_sub: SessionPubSub, exit_handler: SessionExitHandle) {
    println!("Subscribing to {SUBSCRIBE_TOPIC}");
    pub_sub
        .subscribe(SUBSCRIBE_TOPIC, QoS::AtLeastOnce)
        .await
        .unwrap();

    for i in 1..10 {
        let payload = format!("Publish #{i}");
        println!("Sending: {payload}");
        let comp_token = pub_sub
            .publish(PUBLISH_TOPIC, QoS::AtLeastOnce, false, payload)
            .await
            .unwrap();
        comp_token.wait().await.unwrap();
        tokio::time::sleep(Duration::from_secs(1)).await;
    }

    exit_handler.exit_session().await.unwrap();
}
