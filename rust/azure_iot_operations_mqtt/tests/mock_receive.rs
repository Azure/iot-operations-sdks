// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

use tokio_test::{assert_pending, assert_ready};

use azure_iot_operations_mqtt::{
    MqttConnectionSettingsBuilder,
    control_packet::TopicFilter,
    session::{Session, SessionOptionsBuilder, SessionPubReceiver},
    test_utils::{IncomingPacketsTx, InjectedPacketChannels, MockServer, OutgoingPacketsRx},
};
use azure_mqtt::mqtt_proto;

fn setup_client_and_mock_server(client_id: &str) -> (Session, MockServer) {
    let connection_settings = MqttConnectionSettingsBuilder::default()
        .client_id(client_id)
        .hostname("test-hostname")
        .build()
        .unwrap();
    let incoming_packets_tx = IncomingPacketsTx::default();
    let outgoing_packets_rx = OutgoingPacketsRx::default();
    let mock_server = MockServer::new(incoming_packets_tx.clone(), outgoing_packets_rx.clone());
    let options = SessionOptionsBuilder::default()
        .connection_settings(connection_settings)
        .injected_packet_channels(Some(InjectedPacketChannels {
            incoming_packets_tx: incoming_packets_tx,
            outgoing_packets_rx: outgoing_packets_rx,
        }))
        .build()
        .unwrap();
    let session = Session::new(options).unwrap();
    (session, mock_server)
}

/// Common test logic for filtered/unfiltered single receiver tests at QoS 0.
/// Tests that it can:
/// - receive messages via recv()
/// - receive messages via recv_manual_ack()
/// - no PUBACKS are sent because QoS 0
/// - no mechanism for sending PUBACKs is exposed
async fn qos0_single_receiver_test_logic(
    mock_server: MockServer,
    mut receiver: SessionPubReceiver,
    topic_name: &str,
) {
    ///////////// Using recv() /////////////

    // Send publish from mock server and receive it via recv()
    let proto_publish = mqtt_proto::Publish {
        topic_name: mqtt_proto::topic(topic_name),
        packet_identifier_dup_qos: mqtt_proto::PacketIdentifierDupQoS::AtMostOnce,
        retain: false,
        payload: bytes::Bytes::from("Publish 1"),
        other_properties: mqtt_proto::PublishOtherProperties::default(),
    };
    let expected_publish = proto_publish.clone().into();
    mock_server.send_publish(proto_publish);
    assert_eq!(receiver.recv().await.unwrap(), expected_publish);

    // No ack expected for QoS 0
    mock_server.expect_no_packet();

    ///////////// Using recv_manual_ack() /////////////

    // Send publish from mock server and receive it via recv_manual_ack()
    let proto_publish = mqtt_proto::Publish {
        topic_name: mqtt_proto::topic(topic_name),
        packet_identifier_dup_qos: mqtt_proto::PacketIdentifierDupQoS::AtMostOnce,
        retain: false,
        payload: bytes::Bytes::from("Publish 2"),
        other_properties: mqtt_proto::PublishOtherProperties::default(),
    };
    let expected_publish = proto_publish.clone().into();
    mock_server.send_publish(proto_publish);
    let response = receiver.recv_manual_ack().await.unwrap();
    assert_eq!(response.0, expected_publish);
    assert!(response.1.is_none()); // No manual ack mechanism for QoS 0

    // No ack expected for QoS 0
    mock_server.expect_no_packet();
}

#[tokio::test]
async fn qos0_single_filtered_receiver() {
    let (session, mock_server) =
        setup_client_and_mock_server("qos0_single_filtered_receiver_test_client");
    let managed_client = session.create_managed_client();

    // Start the session run loop
    tokio::task::spawn(session.run());
    mock_server.expect_connect_and_accept(true).await;

    // Create filtered receivers for a topic filter
    // NOTE: Do not actually subscribe here, as it's not necessary for the test
    let topic_filter = TopicFilter::new("test/subscribe/topic").unwrap();
    let receiver = managed_client.create_filtered_pub_receiver(topic_filter);

    qos0_single_receiver_test_logic(mock_server, receiver, "test/subscribe/topic").await;
}

#[tokio::test]
async fn qos0_single_unfiltered_receiver() {
    let (session, mock_server) =
        setup_client_and_mock_server("qos0_single_unfiltered_receiver_test_client");
    let managed_client = session.create_managed_client();

    // Start the session run loop
    tokio::task::spawn(session.run());
    mock_server.expect_connect_and_accept(true).await;

    // Create unfiltered receiver
    // NOTE: Do not actually subscribe here, as it's not necessary for the test
    let receiver = managed_client.create_unfiltered_pub_receiver();

    qos0_single_receiver_test_logic(mock_server, receiver, "test/subscribe/topic").await;
}

/// Common test logic for filtered/unfiltered single receiver tests at QoS 1.
/// Tests that it can:
/// - receive messages via recv() and auto-ack PUBLISHes
/// - receive messages via recv_manual_ack() and manually ack PUBLISHes via AckToken in order
/// - receive messages via recv_manual_ack() and manually ack PUBLISHes via AckToken out of order
/// - receive messages via recv_manual_ack() and manually ack PUBLISHes via dropped AckToken in order
/// - receive messages via recv_manual_ack() and manually ack PUBLISHes via dropped AckToken out of order
async fn qos1_single_receiver_test_logic(
    mock_server: MockServer,
    mut receiver: SessionPubReceiver,
    topic_name: &str,
) {
    ///////////// Single message using recv() /////////////

    // Send publish from mock server and receive it
    let proto_publish1 = mqtt_proto::Publish {
        topic_name: mqtt_proto::topic(topic_name),
        packet_identifier_dup_qos: mqtt_proto::PacketIdentifierDupQoS::AtLeastOnce(
            mqtt_proto::PacketIdentifier::new(1).unwrap(),
            false,
        ),
        retain: false,
        payload: bytes::Bytes::from("Publish 1"),
        other_properties: mqtt_proto::PublishOtherProperties::default(),
    };
    let expected_publish1 = proto_publish1.clone().into();
    mock_server.send_publish(proto_publish1);
    assert_eq!(receiver.recv().await.unwrap(), expected_publish1);

    // Expect PUBACK for QoS 1 (auto-acked by recv())
    let puback = mock_server.expect_puback_and_return().await;
    assert_eq!(puback.packet_identifier, 1);
    assert_eq!(puback.reason_code, mqtt_proto::PubAckReasonCode::Success);

    ///////////// Single message using recv_manual_ack() and AckToken /////////////

    // Send publish from mock server and receive it via recv_manual_ack()
    let proto_publish2 = mqtt_proto::Publish {
        topic_name: mqtt_proto::topic(topic_name),
        packet_identifier_dup_qos: mqtt_proto::PacketIdentifierDupQoS::AtLeastOnce(
            mqtt_proto::PacketIdentifier::new(2).unwrap(),
            false,
        ),
        retain: false,
        payload: bytes::Bytes::from("Publish 2"),
        other_properties: mqtt_proto::PublishOtherProperties::default(),
    };
    let expected_publish2 = proto_publish2.clone().into();
    mock_server.send_publish(proto_publish2);
    let response = receiver.recv_manual_ack().await.unwrap();
    assert_eq!(response.0, expected_publish2);
    let acktoken = response.1.expect("Expected ack token for QoS 1");

    // PUBACK has not yet been sent until manually acknowledged via AckToken
    mock_server.expect_no_packet();
    acktoken.ack().await.unwrap().await.unwrap();
    let puback = mock_server.expect_puback_and_return().await;
    assert_eq!(puback.packet_identifier, 2);
    assert_eq!(puback.reason_code, mqtt_proto::PubAckReasonCode::Success);

    ///////////// Single message using recv_manual_ack() with dropped AckToken /////////////

    let proto_publish3 = mqtt_proto::Publish {
        topic_name: mqtt_proto::topic(topic_name),
        packet_identifier_dup_qos: mqtt_proto::PacketIdentifierDupQoS::AtLeastOnce(
            mqtt_proto::PacketIdentifier::new(3).unwrap(),
            false,
        ),
        retain: false,
        payload: bytes::Bytes::from("Publish 3"),
        other_properties: mqtt_proto::PublishOtherProperties::default(),
    };
    let expected_publish3 = proto_publish3.clone().into();
    mock_server.send_publish(proto_publish3);
    let response = receiver.recv_manual_ack().await.unwrap();
    assert_eq!(response.0, expected_publish3);
    let acktoken = response.1.expect("Expected ack token for QoS 1");

    // PUBACK has not yet been sent until the AckToken is dropped
    mock_server.expect_no_packet();
    drop(acktoken);
    let puback = mock_server.expect_puback_and_return().await;
    assert_eq!(puback.packet_identifier, 3);
    assert_eq!(puback.reason_code, mqtt_proto::PubAckReasonCode::Success);

    ///////////// Multiple messages (unordered acks) using recv_manual_ack() and AckToken /////////////

    // Send multiple publishes from mock server and receive them via recv_manual_ack()
    let proto_publish4 = mqtt_proto::Publish {
        topic_name: mqtt_proto::topic(topic_name),
        packet_identifier_dup_qos: mqtt_proto::PacketIdentifierDupQoS::AtLeastOnce(
            mqtt_proto::PacketIdentifier::new(4).unwrap(),
            false,
        ),
        retain: false,
        payload: bytes::Bytes::from("Publish 4"),
        other_properties: mqtt_proto::PublishOtherProperties::default(),
    };
    let expected_publish4 = proto_publish4.clone().into();
    let proto_publish5 = mqtt_proto::Publish {
        topic_name: mqtt_proto::topic(topic_name),
        packet_identifier_dup_qos: mqtt_proto::PacketIdentifierDupQoS::AtLeastOnce(
            mqtt_proto::PacketIdentifier::new(5).unwrap(),
            false,
        ),
        retain: false,
        payload: bytes::Bytes::from("Publish 5"),
        other_properties: mqtt_proto::PublishOtherProperties::default(),
    };
    let expected_publish5 = proto_publish5.clone().into();
    let proto_publish6 = mqtt_proto::Publish {
        topic_name: mqtt_proto::topic(topic_name),
        packet_identifier_dup_qos: mqtt_proto::PacketIdentifierDupQoS::AtLeastOnce(
            mqtt_proto::PacketIdentifier::new(6).unwrap(),
            false,
        ),
        retain: false,
        payload: bytes::Bytes::from("Publish 6"),
        other_properties: mqtt_proto::PublishOtherProperties::default(),
    };
    let expected_publish6 = proto_publish6.clone().into();
    mock_server.send_publish(proto_publish4);
    mock_server.send_publish(proto_publish5);
    mock_server.send_publish(proto_publish6);
    let response4 = receiver.recv_manual_ack().await.unwrap();
    assert_eq!(response4.0, expected_publish4);
    let acktoken4 = response4.1.expect("Expected ack token for QoS 1");
    let response5 = receiver.recv_manual_ack().await.unwrap();
    assert_eq!(response5.0, expected_publish5);
    let acktoken5 = response5.1.expect("Expected ack token for QoS 1");
    let response6 = receiver.recv_manual_ack().await.unwrap();
    assert_eq!(response6.0, expected_publish6);
    let acktoken6 = response6.1.expect("Expected ack token for QoS 1");

    // PUBACKs are not sent until manually acknowledged via AckToken, and are only sent in the
    // order the corresponding PUBLISHes were received, no matter what order the acks were made in.
    mock_server.expect_no_packet();
    let ct6 = acktoken6.ack().await.unwrap(); // ACK Publish 6
    mock_server.expect_no_packet(); // No PUBACK yet for Publish 6
    let mut ct6 = tokio_test::task::spawn(ct6); // wrap in test harness
    assert_pending!(ct6.poll()); // Completion token for PUBACK 6 not ready yet
    let ct4 = acktoken4.ack().await.unwrap(); // ACK Publish 4
    ct4.await.unwrap(); // Wait for PUBACK 4 to complete
    let puback4 = mock_server.expect_puback_and_return().await; // PUBACK for Publish 4
    assert_eq!(puback4.packet_identifier, 4);
    assert_pending!(ct6.poll()); // Completion token for PUBACK 6 not ready yet
    let ct5 = acktoken5.ack().await.unwrap(); // ACK Publish 5
    ct5.await.unwrap(); // Wait for PUBACK 5 to complete
    let puback5 = mock_server.expect_puback_and_return().await; // PUBACK for Publish 5
    assert_eq!(puback5.packet_identifier, 5);
    ct6.await.unwrap(); // Wait for PUBACK 6 to complete
    let puback6 = mock_server.expect_puback_and_return().await; // PUBACK for Publish 6
    assert_eq!(puback6.packet_identifier, 6);

    ///////////// Multiple messages (unordered acks) using recv_manual_ack() with dropped AckTokens /////////////

    // Send multiple publishes from mock server and receive them via recv_manual_ack()
    let proto_publish7 = mqtt_proto::Publish {
        topic_name: mqtt_proto::topic(topic_name),
        packet_identifier_dup_qos: mqtt_proto::PacketIdentifierDupQoS::AtLeastOnce(
            mqtt_proto::PacketIdentifier::new(7).unwrap(),
            false,
        ),
        retain: false,
        payload: bytes::Bytes::from("Publish 7"),
        other_properties: mqtt_proto::PublishOtherProperties::default(),
    };
    let expected_publish7 = proto_publish7.clone().into();
    let proto_publish8 = mqtt_proto::Publish {
        topic_name: mqtt_proto::topic(topic_name),
        packet_identifier_dup_qos: mqtt_proto::PacketIdentifierDupQoS::AtLeastOnce(
            mqtt_proto::PacketIdentifier::new(8).unwrap(),
            false,
        ),
        retain: false,
        payload: bytes::Bytes::from("Publish 8"),
        other_properties: mqtt_proto::PublishOtherProperties::default(),
    };
    let expected_publish8 = proto_publish8.clone().into();
    let proto_publish9 = mqtt_proto::Publish {
        topic_name: mqtt_proto::topic(topic_name),
        packet_identifier_dup_qos: mqtt_proto::PacketIdentifierDupQoS::AtLeastOnce(
            mqtt_proto::PacketIdentifier::new(9).unwrap(),
            false,
        ),
        retain: false,
        payload: bytes::Bytes::from("Publish 9"),
        other_properties: mqtt_proto::PublishOtherProperties::default(),
    };
    let expected_publish9 = proto_publish9.clone().into();
    mock_server.send_publish(proto_publish7);
    mock_server.send_publish(proto_publish8);
    mock_server.send_publish(proto_publish9);
    let response7 = receiver.recv_manual_ack().await.unwrap();
    assert_eq!(response7.0, expected_publish7);
    let acktoken7 = response7.1.expect("Expected ack token for QoS 1");
    let response8 = receiver.recv_manual_ack().await.unwrap();
    assert_eq!(response8.0, expected_publish8);
    let acktoken8 = response8.1.expect("Expected ack token for QoS 1");
    let response9 = receiver.recv_manual_ack().await.unwrap();
    assert_eq!(response9.0, expected_publish9);
    let acktoken9 = response9.1.expect("Expected ack token for QoS 1");

    // PUBACKs are not sent until AckTokens are dropped, and are only sent in the
    // order the corresponding PUBLISHes were received, no matter what order the drops were made in.
    mock_server.expect_no_packet();
    drop(acktoken9);
    mock_server.expect_no_packet(); // No PUBACK yet for Publish 9
    drop(acktoken7);
    let puback7 = mock_server.expect_puback_and_return().await; // PUBACK for Publish 7
    assert_eq!(puback7.packet_identifier, 7);
    drop(acktoken8);
    let puback8 = mock_server.expect_puback_and_return().await; // PUBACK for Publish 8
    assert_eq!(puback8.packet_identifier, 8);
    let puback9 = mock_server.expect_puback_and_return().await; // PUBACK for Publish 9
    assert_eq!(puback9.packet_identifier, 9);

    // Validate that there are no other lingering packets
    mock_server.expect_no_packet();
}

#[tokio::test]
async fn qos1_single_filtered_receiver() {
    let (session, mock_server) =
        setup_client_and_mock_server("qos1_single_filtered_receiver_test_client");
    let managed_client = session.create_managed_client();

    // Start the session run loop
    tokio::task::spawn(session.run());
    mock_server.expect_connect_and_accept(true).await;

    // Create receivers for a topic filter
    // NOTE: Do not actually subscribe here, as it's not necessary for the test
    let topic_filter = TopicFilter::new("test/subscribe/topic").unwrap();
    let receiver = managed_client.create_filtered_pub_receiver(topic_filter);

    qos1_single_receiver_test_logic(mock_server, receiver, "test/subscribe/topic").await;
}

#[tokio::test]
async fn qos1_single_unfiltered_receiver() {
    let (session, mock_server) =
        setup_client_and_mock_server("qos1_single_filtered_receiver_test_client");
    let managed_client = session.create_managed_client();

    // Start the session run loop
    tokio::task::spawn(session.run());
    mock_server.expect_connect_and_accept(true).await;

    // Create receivers for a topic filter
    // NOTE: Do not actually subscribe here, as it's not necessary for the test
    let receiver = managed_client.create_unfiltered_pub_receiver();

    qos1_single_receiver_test_logic(mock_server, receiver, "test/subscribe/topic").await;
}

/// Common test logic for multiple filtered/unfiltered single receiver tests at QoS 0.
/// Tests that:
/// - all receivers receive all messages with both recv() and recv_manual_ack()
/// - no PUBACKs are sent because QoS 0
/// - no mechanism for sending PUBACKs is exposed
async fn qos0_multiple_receiver_same_filter_test_logic(
    mock_server: MockServer,
    mut receiver1: SessionPubReceiver,
    mut receiver2: SessionPubReceiver,
    mut receiver3: SessionPubReceiver,
    topic_name: &str,
) {
    ///////////// Using recv() /////////////

    // Send publish from mock server and all receivers receive it via recv()
    let proto_publish = mqtt_proto::Publish {
        topic_name: mqtt_proto::topic(topic_name),
        packet_identifier_dup_qos: mqtt_proto::PacketIdentifierDupQoS::AtMostOnce,
        retain: false,
        payload: bytes::Bytes::from("Publish 1"),
        other_properties: mqtt_proto::PublishOtherProperties::default(),
    };
    let expected_publish = proto_publish.clone().into();
    mock_server.send_publish(proto_publish);
    assert_eq!(receiver1.recv().await.unwrap(), expected_publish);
    assert_eq!(receiver2.recv().await.unwrap(), expected_publish);
    assert_eq!(receiver3.recv().await.unwrap(), expected_publish);

    // No ack expected for QoS 0
    mock_server.expect_no_packet();

    ///////////// Using recv_manual_ack() /////////////

    // Send publish from mock server and receive it via recv_manual_ack()
    let proto_publish = mqtt_proto::Publish {
        topic_name: mqtt_proto::topic(topic_name),
        packet_identifier_dup_qos: mqtt_proto::PacketIdentifierDupQoS::AtMostOnce,
        retain: false,
        payload: bytes::Bytes::from("Publish 2"),
        other_properties: mqtt_proto::PublishOtherProperties::default(),
    };
    let expected_publish = proto_publish.clone().into();
    mock_server.send_publish(proto_publish);
    let response1 = receiver1.recv_manual_ack().await.unwrap();
    assert_eq!(response1.0, expected_publish);
    assert!(response1.1.is_none()); // No manual ack mechanism for QoS 0
    let response2 = receiver2.recv_manual_ack().await.unwrap();
    assert_eq!(response2.0, expected_publish);
    assert!(response2.1.is_none()); // No manual ack mechanism for QoS 0
    let response3 = receiver3.recv_manual_ack().await.unwrap();
    assert_eq!(response3.0, expected_publish);
    assert!(response3.1.is_none()); // No manual ack mechanism for QoS 0

    // No ack expected for QoS 0
    mock_server.expect_no_packet();
}

#[tokio::test]
async fn qos0_multiple_identical_filtered_receivers() {
    let (session, mock_server) =
        setup_client_and_mock_server("qos0_multiple_identical_filtered_receivers_test_client");
    let managed_client = session.create_managed_client();

    // Start the session run loop
    tokio::task::spawn(session.run());
    mock_server.expect_connect_and_accept(true).await;

    // Create multiple receivers with identical filters
    // NOTE: Do not actually subscribe here, as it's not necessary for the test
    let filter = TopicFilter::new("test/subscribe/topic").unwrap();
    let receiver1 = managed_client.create_filtered_pub_receiver(filter.clone());
    let receiver2 = managed_client.create_filtered_pub_receiver(filter.clone());
    let receiver3 = managed_client.create_filtered_pub_receiver(filter);

    qos0_multiple_receiver_same_filter_test_logic(
        mock_server,
        receiver1,
        receiver2,
        receiver3,
        "test/subscribe/topic",
    )
    .await;
}

#[tokio::test]
async fn qos0_multiple_unfiltered_receivers() {
    let (session, mock_server) =
        setup_client_and_mock_server("qos0_multiple_unfiltered_receivers_test_client");
    let managed_client = session.create_managed_client();

    // Start the session run loop
    tokio::task::spawn(session.run());
    mock_server.expect_connect_and_accept(true).await;

    // Create multiple receivers with identical filters
    // NOTE: Do not actually subscribe here, as it's not necessary for the test
    let receiver1 = managed_client.create_unfiltered_pub_receiver();
    let receiver2 = managed_client.create_unfiltered_pub_receiver();
    let receiver3 = managed_client.create_unfiltered_pub_receiver();

    qos0_multiple_receiver_same_filter_test_logic(
        mock_server,
        receiver1,
        receiver2,
        receiver3,
        "test/subscribe/topic",
    )
    .await;
}

/// Common test logic for multiple filtered/unfiltered single receiver tests at QoS 0.
/// Tests that:
/// - Receive PUBLISHes via recv() and send auto-PUBACK only after all receivers have received it
/// - Receive PUBLISHes via recv_manual_ack() and send PUBACKs only after all receivers have acked it via AckToken in order
/// - Receive PUBLISHes via recv_manual_ack() and send PUBACKs only after all receivers have acked it via AckToken out of order
/// - Receive PUBLISHes via recv_manual_ack() and send PUBACKs only after all receivers have acked it via dropped AckToken in order
/// - Receive PUBLISHes via recv_manual_ack() and send PUBACKs only after all receivers have acked it via dropped AckToken out of order
/// - Receive PUBLISHES via recv_manual_ack() and ??? (out or order delayed messages)
async fn qos1_multiple_receiver_same_filter_test_logic(mock_server: MockServer, mut receiver1: SessionPubReceiver, mut receiver2: SessionPubReceiver, mut receiver3: SessionPubReceiver, topic_name: &str) {
    ///////////// Using recv() /////////////
    
    // Send publish from mock server
    let proto_publish1 = mqtt_proto::Publish {
        topic_name: mqtt_proto::topic(topic_name),
        packet_identifier_dup_qos: mqtt_proto::PacketIdentifierDupQoS::AtLeastOnce(mqtt_proto::PacketIdentifier::new(1).unwrap(), false),
        retain: false,
        payload: bytes::Bytes::from("Publish 1"),
        other_properties: mqtt_proto::PublishOtherProperties::default(),
    };
    let expected_publish1 = proto_publish1.clone().into();
    mock_server.send_publish(proto_publish1);

    // Receive publish from all receivers.
    // The PUBACK is not sent until all receivers have received the PUBLISH
    // (QoS 1 with auto-ack via recv())
    mock_server.expect_no_packet();
    assert_eq!(receiver1.recv().await.unwrap(), expected_publish1);
    mock_server.expect_no_packet();
    assert_eq!(receiver2.recv().await.unwrap(), expected_publish1);
    mock_server.expect_no_packet();
    assert_eq!(receiver3.recv().await.unwrap(), expected_publish1);
    let puback1 = mock_server.expect_puback_and_return().await;
    assert_eq!(puback1.packet_identifier, 1);
    assert_eq!(puback1.reason_code, mqtt_proto::PubAckReasonCode::Success);

    // Send multiple publishes from the mock server
    let proto_publish2 = mqtt_proto::Publish {
        topic_name: mqtt_proto::topic(topic_name),
        packet_identifier_dup_qos: mqtt_proto::PacketIdentifierDupQoS::AtLeastOnce(mqtt_proto::PacketIdentifier::new(2).unwrap(), false),
        retain: false,
        payload: bytes::Bytes::from("Publish 2"),
        other_properties: mqtt_proto::PublishOtherProperties::default(),
    };
    let expected_publish2 = proto_publish2.clone().into();
    let proto_publish3 = mqtt_proto::Publish {
        topic_name: mqtt_proto::topic(topic_name),
        packet_identifier_dup_qos: mqtt_proto::PacketIdentifierDupQoS::AtLeastOnce(mqtt_proto::PacketIdentifier::new(3).unwrap(), false),
        retain: false,
        payload: bytes::Bytes::from("Publish 3"),
        other_properties: mqtt_proto::PublishOtherProperties::default(),
    };
    let expected_publish3 = proto_publish3.clone().into();
    let proto_publish4 = mqtt_proto::Publish {
        topic_name: mqtt_proto::topic(topic_name),
        packet_identifier_dup_qos: mqtt_proto::PacketIdentifierDupQoS::AtLeastOnce(mqtt_proto::PacketIdentifier::new(4).unwrap(), false),
        retain: false,
        payload: bytes::Bytes::from("Publish 4"),
        other_properties: mqtt_proto::PublishOtherProperties::default(),
    };
    let expected_publish4 = proto_publish4.clone().into();
    mock_server.send_publish(proto_publish2);
    mock_server.send_publish(proto_publish3);
    mock_server.send_publish(proto_publish4);

    // Receive the publishes in different orders, with the PUBACK being sent only after all
    // receivers have received each PUBLISH
    mock_server.expect_no_packet();
    assert_eq!(receiver1.recv().await.unwrap(), expected_publish2); // Receiver 1 gets Publish 2
    mock_server.expect_no_packet(); // No PUBACK yet
    assert_eq!(receiver2.recv().await.unwrap(), expected_publish2); // Receiver 2 gets Publish 2
    mock_server.expect_no_packet(); // No PUBACK yet
    assert_eq!(receiver1.recv().await.unwrap(), expected_publish3); // Receiver 1 gets Publish 3
    mock_server.expect_no_packet(); // No PUBACK yet
    assert_eq!(receiver3.recv().await.unwrap(), expected_publish2); // Receiver 3 gets Publish 2
    let puback2 = mock_server.expect_puback_and_return().await; // PUBACK for Publish 2 now that all received
    assert_eq!(puback2.packet_identifier, 2);
    assert_eq!(puback2.reason_code, mqtt_proto::PubAckReasonCode::Success);
    mock_server.expect_no_packet(); // No more PUBACKs yet
    assert_eq!(receiver2.recv().await.unwrap(), expected_publish3); // Receiver 2 gets Publish 3
    mock_server.expect_no_packet(); // No PUBACK yet
    assert_eq!(receiver1.recv().await.unwrap(), expected_publish4); // Receiver 1 gets Publish 4
    mock_server.expect_no_packet(); // No PUBACK yet
    assert_eq!(receiver2.recv().await.unwrap(), expected_publish4); // Receiver 2 gets Publish 4
    mock_server.expect_no_packet(); // No PUBACK yet
    assert_eq!(receiver3.recv().await.unwrap(), expected_publish3); // Receiver 3 gets Publish 3
    let puback3 = mock_server.expect_puback_and_return().await; // PUBACK for Publish 3 now that all received
    assert_eq!(puback3.packet_identifier, 3);
    assert_eq!(puback3.reason_code, mqtt_proto::PubAckReasonCode::Success);
    mock_server.expect_no_packet(); // No more PUBACKs yet
    assert_eq!(receiver3.recv().await.unwrap(), expected_publish4); // Receiver 3 gets Publish 4
    let puback4 = mock_server.expect_puback_and_return().await; // PUBACK for Publish 4 now that all received
    assert_eq!(puback4.packet_identifier, 4);
    assert_eq!(puback4.reason_code, mqtt_proto::PubAckReasonCode::Success);

    // No further packets expected
    mock_server.expect_no_packet();

    ///////////// Using recv_manual_ack() and AckToken /////////////

    // Send publish from mock server
    let proto_publish5 = mqtt_proto::Publish {
        topic_name: mqtt_proto::topic(topic_name),
        packet_identifier_dup_qos: mqtt_proto::PacketIdentifierDupQoS::AtLeastOnce(mqtt_proto::PacketIdentifier::new(5).unwrap(), false),
        retain: false,
        payload: bytes::Bytes::from("Publish 5"),
        other_properties: mqtt_proto::PublishOtherProperties::default(),
    };
    let expected_publish5 = proto_publish5.clone().into();
    mock_server.send_publish(proto_publish5);

    // Receive publish from all receivers.
    // The PUBACK is not sent until all receivers have manually acknowedged via AckToken
    mock_server.expect_no_packet();
    let r1_response5 = receiver1.recv_manual_ack().await.unwrap();
    assert_eq!(r1_response5.0, expected_publish5);
    let r1_acktoken5 = r1_response5.1.expect("Expected ack token for QoS 1");
    let r2_response5 = receiver2.recv_manual_ack().await.unwrap();
    assert_eq!(r2_response5.0, expected_publish5);
    let r2_acktoken5 = r2_response5.1.expect("Expected ack token for QoS 1");
    let r3_response5 = receiver3.recv_manual_ack().await.unwrap();
    assert_eq!(r3_response5.0, expected_publish5);
    let r3_acktoken5 = r3_response5.1.expect("Expected ack token for QoS 1");
    mock_server.expect_no_packet();
    // Begin acknowledging
    
    let mut r1_ack5 = tokio_test::task::spawn(r1_acktoken5.ack()); // ACK from Receiver 1
    assert_pending!(r1_ack5.poll()); // Receiver 1 ACK not yet complete
    mock_server.expect_no_packet(); // No PUBACK yet
    let mut r2_ack5 = tokio_test::task::spawn(r2_acktoken5.ack()); // ACK from Receiver 2
    assert_pending!(r2_ack5.poll()); // Receiver 2 ACK not yet complete
    assert_pending!(r1_ack5.poll()); // Receiver 1 ACK still not yet complete
    mock_server.expect_no_packet(); // No PUBACK yet
    let mut r3_ack5 = tokio_test::task::spawn(r3_acktoken5.ack()); // ACK from Receiver 3

    assert_pending!(r3_ack5.poll()); // Receiver 3 ACK not yet complete
    let r3_ct5 = assert_ready!(r3_ack5.poll()); // Receiver 3 ACK should be complete now

    //let r = r3_ack5.await.unwrap();
    
    // tokio::time::sleep(std::time::Duration::from_secs(1)).await; // Give some time for the ACKs to process
    // let r3_ct5 = assert_ready!(r3_ack5.poll()); // Receiver 3 ACK should be complete now
    // let r2_ct5 = assert_ready!(r2_ack5.poll()); // Receiver 2 ACK should be complete now
    // let r1_ct5 = assert_ready!(r1_ack5.poll()); // Receiver 1 ACK should be complete now

    // let r3_ct5 = assert_ready!(r3_ack5.poll()).unwrap(); // Receiver 3 ACK should be complete now
    // let r2_ct5 = assert_ready!(r2_ack5.poll()).unwrap(); // Receiver 2 ACK should be complete now
    // let r1_ct5 = assert_ready!(r1_ack5.poll()).unwrap(); // Receiver 1 ACK should be complete now



    // assert_ready!(tokio_test::task::spawn(r3_ct5).poll()).unwrap(); // Receiver 3 ACK completion token should be complete
    // assert_ready!(tokio_test::task::spawn(r2_ct5).poll()).unwrap(); // Receiver 2 ACK completion token should be complete
    // assert_ready!(tokio_test::task::spawn(r1_ct5).poll()).unwrap(); // Receiver 1 ACK completion token should be complete

    let puback5 = mock_server.expect_puback_and_return().await; // PUBACK for Publish 5 now that all have ACKed
    assert_eq!(puback5.packet_identifier, 5);
    assert_eq!(puback5.reason_code, mqtt_proto::PubAckReasonCode::Success);

    // let mut r3_ack = tokio_test::task::spawn(r3_acktoken5.ack()); // ACK from Receiver 3


    //let mut r1_ct5 = tokio_test::task::spawn(r1_acktoken5.ack().await.unwrap()); // ACK from Receiver 1
    //mock_server.expect_no_packet(); // No PUBACK yet
    //assert_pending!(r1_ct5.poll()); // Receiver 1 completion token not ready yet
    // let mut r2_ct5 = tokio_test::task::spawn(r2_acktoken5.ack().await.unwrap()); // ACK from Receiver 2
    // mock_server.expect_no_packet(); // No PUBACK yet
    // assert_pending!(r2_ct5.poll()); // Receiver 2 completion token not ready yet
    // assert_pending!(r1_ct5.poll()); // Receiver 1 completion token still not ready yet
    // let r3_ct5 = tokio_test::task::spawn(r3_acktoken5.ack().await.unwrap()); // ACK from Receiver 3
    // let puback5 = mock_server.expect_puback_and_return().await; // PUBACK now that all have ACKed
    // assert_eq!(puback5.packet_identifier, 5);
    // assert_eq!(puback5.reason_code, mqtt_proto::PubAckReasonCode::Success);
    // r1_ct5.await.unwrap(); // Wait for Receiver 1 completion token
    // r2_ct5.await.unwrap(); // Wait for Receiver 2 completion token
    // r3_ct5.await.unwrap(); // Wait for Receiver 3 completion token

    // It's gotta be something about a mutex right?

 


}



#[tokio::test]
async fn qos1_multiple_identical_filtered_receivers() {
    let (session, mock_server) =
        setup_client_and_mock_server("qos1_multiple_identical_filtered_receivers_test_client");
    let managed_client = session.create_managed_client();

    // Start the session run loop
    tokio::task::spawn(session.run());
    mock_server.expect_connect_and_accept(true).await;

    // Create multiple receivers with identical filters
    // NOTE: Do not actually subscribe here, as it's not necessary for the test
    let filter = TopicFilter::new("test/subscribe/topic").unwrap();
    let receiver1 = managed_client.create_filtered_pub_receiver(filter.clone());
    let receiver2 = managed_client.create_filtered_pub_receiver(filter.clone());
    let receiver3 = managed_client.create_filtered_pub_receiver(filter);

    qos1_multiple_receiver_same_filter_test_logic(mock_server, receiver1, receiver2, receiver3, "test/subscribe/topic").await;
}

#[tokio::test]
async fn qos1_multiple_unfiltered_receivers() {
        let (session, mock_server) =
        setup_client_and_mock_server("qos1_multiple_unfiltered_receivers_test_client");
    let managed_client = session.create_managed_client();

    // Start the session run loop
    tokio::task::spawn(session.run());
    mock_server.expect_connect_and_accept(true).await;

    // Create multiple receivers with identical filters
    // NOTE: Do not actually subscribe here, as it's not necessary for the test
    let receiver1 = managed_client.create_unfiltered_pub_receiver();
    let receiver2 = managed_client.create_unfiltered_pub_receiver();
    let receiver3 = managed_client.create_unfiltered_pub_receiver();

    qos1_multiple_receiver_same_filter_test_logic(mock_server, receiver1, receiver2, receiver3, "test/subscribe/topic").await;
}



// #[tokio::test]












// - Validate dispatching (including advanced stuff like dynamic changing as receivers drop/get added)
// - QoS 0 messages aren't acked until received by all
// - QoS 1 messages aren't acked until received and acked by all
// - mixed qos?
// - drops / transport disconnects + ack tokens + completion tokens


