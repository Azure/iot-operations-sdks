// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

use azure_iot_operations_mqtt::session::Session;
use azure_iot_operations_mqtt::{
    MqttConnectionSettingsBuilder,
    control_packet::TopicFilter,
    session::{SessionOptionsBuilder, SessionPubReceiver},
    test_utils::{IncomingPacketsTx, InjectedPacketChannels, OutgoingPacketsRx},
};
use azure_mqtt::mqtt_proto::PublishOtherProperties;
use bytes::Bytes;

#[tokio::test]
async fn mock_event_injection() {
    const CLIENT_ID: &str = "MyClientId";

    let connection_settings = MqttConnectionSettingsBuilder::default()
        .client_id(CLIENT_ID)
        .hostname("localhost")
        .tcp_port(1883u16)
        .use_tls(false)
        .build()
        .unwrap();

    let incoming_packets_tx = IncomingPacketsTx::default();
    let outgoing_packets_rx = OutgoingPacketsRx::default();
    let session_options = SessionOptionsBuilder::default()
        .connection_settings(connection_settings)
        .injected_packet_channels(Some(InjectedPacketChannels {
            incoming_packets_tx: incoming_packets_tx.clone(),
            outgoing_packets_rx: outgoing_packets_rx.clone(),
        }))
        .build()
        .unwrap();
    let session = Session::new(session_options).unwrap();

    let topic_filter = TopicFilter::new("test/resp/topic").unwrap();
    let mut pub_receiver = session
        .create_managed_client()
        .create_filtered_pub_receiver(topic_filter);

    #[allow(clippy::items_after_statements)]
    async fn receive_publish(
        incoming_packets_tx: IncomingPacketsTx,
        pub_receiver: &mut SessionPubReceiver,
    ) {
        // establish connection
        let connack = azure_mqtt::mqtt_proto::Packet::ConnAck(azure_mqtt::mqtt_proto::ConnAck {
            reason_code: azure_mqtt::mqtt_proto::ConnectReasonCode::Success {
                session_present: false,
            },
            other_properties: azure_mqtt::mqtt_proto::ConnAckOtherProperties::default(),
        });
        incoming_packets_tx.send(connack);

        // inject a PUBLISH packet
        incoming_packets_tx.send(azure_mqtt::mqtt_proto::Packet::Publish(
            azure_mqtt::mqtt_proto::Publish::<Bytes> {
                packet_identifier_dup_qos:
                    azure_mqtt::mqtt_proto::PacketIdentifierDupQoS::AtLeastOnce(
                        azure_mqtt::mqtt_proto::PacketIdentifier::new(1).unwrap(),
                        false,
                    ),
                topic_name: azure_mqtt::mqtt_proto::Topic::new("test/resp/topic".to_string())
                    .unwrap()
                    .into(),
                payload: vec![].into(),
                other_properties: PublishOtherProperties::default(),
                retain: false,
            },
        ));

        let received_pub = pub_receiver.recv().await.unwrap();
        assert_eq!(received_pub.topic_name.as_str(), "test/resp/topic");
    }

    tokio::select! {
        () = receive_publish(incoming_packets_tx, &mut pub_receiver) => {}
        _ = session.run() => {}
    }
}
