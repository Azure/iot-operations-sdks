// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

use azure_iot_operations_mqtt::control_packet::{
    AuthProperties, PublishProperties, QoS, SubscribeProperties, UnsubscribeProperties,
};
use bytes::Bytes;
use tokio::sync::oneshot;

use crate::metl::test_ack_kind::TestAckKind;

pub enum MqttOperation {
    Publish {
        topic: String,
        qos: QoS,
        retain: bool,
        payload: Bytes,
        properties: Option<PublishProperties>,
        ack_tx: oneshot::Sender<TestAckKind>,
    },

    Subscribe {
        topic: String,
        _qos: QoS,
        _properties: Option<SubscribeProperties>,
        ack_tx: oneshot::Sender<TestAckKind>,
    },

    Unsubscribe {
        _topic: String,
        _properties: Option<UnsubscribeProperties>,
        ack_tx: oneshot::Sender<TestAckKind>,
    },

    Ack {
        pkid: u16,
    },

    Disconnect,

    Auth {
        _auth_props: AuthProperties,
    },
}
