// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

use azure_iot_operations_mqtt::control_packet::QoS;

pub fn to_enum(qos: Option<i32>) -> QoS {
    match qos {
        None | Some(0) => QoS::AtMostOnce,
        Some(1) => QoS::AtLeastOnce,
        Some(2) => QoS::ExactlyOnce,
        Some(x) => panic!("unexpected qos value {x}"),
    }
}
