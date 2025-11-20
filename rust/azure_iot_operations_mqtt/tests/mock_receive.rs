// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
use test_case::test_case;

use azure_iot_operations_mqtt::{
    MqttConnectionSettingsBuilder,
    session::{Session, SessionOptionsBuilder},
    test_utils::{IncomingPacketsTx, OutgoingPacketsRx, InjectedPacketChannels},
    control_packet::{QoS, TopicFilter},
};

#[test_case(QoS::AtMostOnce; "QoS 0")]
#[test_case(QoS::AtLeastOnce; "QoS 1")]
#[tokio::test]
async fn receive_qos0(qos: QoS) {
    let connection_settings = MqttConnectionSettingsBuilder::default()
        .client_id("test-client-id")
        .hostname("test-hostname")
        .build()
        .unwrap();
    let incoming_packets_tx = IncomingPacketsTx::default();
    let outgoing_packets_rx = OutgoingPacketsRx::default();
    let options = SessionOptionsBuilder::default()
        .connection_settings(connection_settings)
        .injected_packet_channels(Some(InjectedPacketChannels {
            incoming_packets_tx: incoming_packets_tx.clone(),
            outgoing_packets_rx: outgoing_packets_rx.clone(),
        }))
        .build()
        .unwrap();
    let session = Session::new(options).unwrap();
    let managed_client = session.create_managed_client();

    let runner_jh = tokio::task::spawn(session.run());

    // let subscribe_topic = TopicFilter::new("test/subscribe/topic").unwrap();
    // managed_client.subscribe();
    // let receiver = managed_client.create_filtered_pub_receiver(subscribe_topic).await.unwrap();

    assert!(true);

}

// async fn run(session: Session) {
//     session.run().await.unwrap();
// }