// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

#![cfg(feature = "test-utils")]

use std::{pin::pin, time::Duration};

use azure_iot_operations_mqtt::azure_mqtt::{
    client::{
        ClientOptions, ConnectResult, Connection, DisconnectedEvent, KeepAliveConfig,
        ManualAcknowledgement, Receiver, new_client,
    },
    mqtt_proto::{
        self, ConnectReasonCode, Packet, PacketIdentifier, PacketIdentifierDupQoS, topic,
    },
    packet::{ConnAck, ConnectProperties},
    transport::{ConnectionTransportConfig, ConnectionTransportType},
};
use bytes::Bytes;
use tokio::sync::mpsc::{UnboundedReceiver, UnboundedSender, unbounded_channel};

async fn connect(
    incoming_qos0_queue_size: usize,
) -> (
    UnboundedSender<Packet<Bytes>>,
    UnboundedReceiver<Packet<Bytes>>,
    Connection,
    Receiver,
) {
    let options = ClientOptions {
        client_id: Some("receive-queue-test".to_string()),
        incoming_qos0_queue_size,
        ..Default::default()
    };
    let (_client, connect_handle, receiver) = new_client(options);
    let (incoming_packets_tx, incoming_packets_rx) = unbounded_channel();
    let (outgoing_packets_tx, mut outgoing_packets_rx) = unbounded_channel();

    incoming_packets_tx
        .send(Packet::ConnAck(mqtt_proto::ConnAck {
            reason_code: ConnectReasonCode::Success {
                session_present: false,
            },
            other_properties: mqtt_proto::ConnAckOtherProperties::default(),
        }))
        .unwrap();

    let ConnectResult::Success(connection, connack, _disconnect_handle) = connect_handle
        .connect(
            ConnectionTransportConfig {
                transport_type: ConnectionTransportType::Test {
                    incoming_packets: incoming_packets_rx,
                    outgoing_packets: outgoing_packets_tx,
                },
                timeout: None,
                proxy: None,
                tcp_nodelay: true,
            },
            false,
            KeepAliveConfig::Infinite,
            None,
            None,
            None,
            ConnectProperties::default(),
            None,
        )
        .await
    else {
        panic!("expected successful connect")
    };

    assert!(matches!(
        outgoing_packets_rx.recv().await,
        Some(Packet::Connect(_))
    ));
    assert!(matches!(connack, ConnAck { .. }));

    (
        incoming_packets_tx,
        outgoing_packets_rx,
        connection,
        receiver,
    )
}

fn qos0_publish(payload: &'static [u8]) -> Packet<Bytes> {
    Packet::Publish(mqtt_proto::Publish {
        topic_name: topic("incoming/qos0"),
        packet_identifier_dup_qos: PacketIdentifierDupQoS::AtMostOnce,
        retain: false,
        payload: Bytes::from_static(payload),
        other_properties: mqtt_proto::PublishOtherProperties::default(),
    })
}

fn qos1_publish(packet_identifier: u16, payload: &'static [u8]) -> Packet<Bytes> {
    Packet::Publish(mqtt_proto::Publish {
        topic_name: topic("incoming/qos1"),
        packet_identifier_dup_qos: PacketIdentifierDupQoS::AtLeastOnce(
            PacketIdentifier::new(packet_identifier).unwrap(),
            false,
        ),
        retain: false,
        payload: Bytes::from_static(payload),
        other_properties: mqtt_proto::PublishOtherProperties::default(),
    })
}

#[tokio::test(start_paused = true)]
async fn incoming_qos0_queue_drops_overflow_and_recovers_capacity() {
    let (incoming_packets_tx, _outgoing_packets_rx, connection, mut receiver) = connect(1).await;
    let mut connection = pin!(connection.run_until_disconnect());

    incoming_packets_tx.send(qos0_publish(b"accepted")).unwrap();
    incoming_packets_tx
        .send(qos0_publish(b"dropped-1"))
        .unwrap();
    incoming_packets_tx
        .send(qos0_publish(b"dropped-2"))
        .unwrap();

    assert!(
        tokio::time::timeout(Duration::from_secs(1), &mut connection)
            .await
            .is_err()
    );

    let (publish, acknowledgement) = receiver.recv().await.unwrap();
    assert_eq!(publish.payload, Bytes::from_static(b"accepted"));
    assert!(matches!(acknowledgement, ManualAcknowledgement::QoS0));
    assert!(
        tokio::time::timeout(Duration::from_millis(1), receiver.recv())
            .await
            .is_err()
    );

    incoming_packets_tx
        .send(qos0_publish(b"accepted-after-drain"))
        .unwrap();
    assert!(
        tokio::time::timeout(Duration::from_secs(1), &mut connection)
            .await
            .is_err()
    );

    let (publish, acknowledgement) = receiver.recv().await.unwrap();
    assert_eq!(publish.payload, Bytes::from_static(b"accepted-after-drain"));
    assert!(matches!(acknowledgement, ManualAcknowledgement::QoS0));

    drop(incoming_packets_tx);
    let (_connect_handle, disconnected_event) = connection.await;
    assert!(matches!(disconnected_event, DisconnectedEvent::IoError(_)));
}

#[tokio::test(start_paused = true)]
async fn incoming_qos1_is_not_limited_by_qos0_queue_capacity() {
    let (incoming_packets_tx, _outgoing_packets_rx, connection, mut receiver) = connect(1).await;
    let mut connection = pin!(connection.run_until_disconnect());

    incoming_packets_tx
        .send(qos1_publish(1, b"qos1-1"))
        .unwrap();
    incoming_packets_tx
        .send(qos1_publish(2, b"qos1-2"))
        .unwrap();

    assert!(
        tokio::time::timeout(Duration::from_secs(1), &mut connection)
            .await
            .is_err()
    );

    for expected_payload in [b"qos1-1".as_slice(), b"qos1-2".as_slice()] {
        let (publish, acknowledgement) = receiver.recv().await.unwrap();
        assert_eq!(publish.payload.as_ref(), expected_payload);
        assert!(matches!(acknowledgement, ManualAcknowledgement::QoS1(_)));
    }

    drop(incoming_packets_tx);
    let (_connect_handle, disconnected_event) = connection.await;
    assert!(matches!(disconnected_event, DisconnectedEvent::IoError(_)));
}
