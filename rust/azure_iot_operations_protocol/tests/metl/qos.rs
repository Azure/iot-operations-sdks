// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

use azure_mqtt::mqtt_proto::QoS;

pub fn to_enum(qos: Option<i32>) -> QoS {
    match qos {
        None | Some(0) => QoS::AtMostOnce,
        Some(1) => QoS::AtLeastOnce,
        Some(2) => QoS::ExactlyOnce,
        Some(x) => panic!("unexpected qos value {x}"),
    }
}

pub fn new_packet_identifier_dup_qos(
    qos: QoS,
    dup: bool,
    packet_id: u16,
) -> azure_mqtt::mqtt_proto::PacketIdentifierDupQoS {
    match qos {
        QoS::AtMostOnce => azure_mqtt::mqtt_proto::PacketIdentifierDupQoS::AtMostOnce,
        QoS::AtLeastOnce => azure_mqtt::mqtt_proto::PacketIdentifierDupQoS::AtLeastOnce(
            azure_mqtt::mqtt_proto::PacketIdentifier::new(packet_id).unwrap(),
            dup,
        ),
        QoS::ExactlyOnce => azure_mqtt::mqtt_proto::PacketIdentifierDupQoS::ExactlyOnce(
            azure_mqtt::mqtt_proto::PacketIdentifier::new(packet_id).unwrap(),
            dup,
        ),
    }
}
