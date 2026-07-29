// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

use azure_iot_operations_mqtt::azure_mqtt::mqtt_proto::{
    PacketIdentifier, PacketIdentifierDupQoS, QoS,
};

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
) -> PacketIdentifierDupQoS {
    match qos {
        QoS::AtMostOnce => PacketIdentifierDupQoS::AtMostOnce,
        QoS::AtLeastOnce => {
            PacketIdentifierDupQoS::AtLeastOnce(PacketIdentifier::new(packet_id).unwrap(), dup)
        }
        QoS::ExactlyOnce => {
            PacketIdentifierDupQoS::ExactlyOnce(PacketIdentifier::new(packet_id).unwrap(), dup)
        }
    }
}
