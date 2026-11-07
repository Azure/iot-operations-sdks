// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

//! Structures representing MQTT control packets and the values contained within them.

// Re-export topic types
pub use ms_mqtt_client::topic::{TopicFilter, TopicName};

// Re-export control packet types
pub use ms_mqtt_client::packet::{Auth, ConnAck, Disconnect, PubAck, Publish, SubAck, UnsubAck};

// Re-export control packet property types
pub use ms_mqtt_client::packet::{
    AuthProperties, ConnAckProperties, ConnectProperties, DisconnectProperties, PubAckProperties,
    PublishProperties, SubAckProperties, SubscribeProperties, UnsubAckProperties,
    UnsubscribeProperties,
};

// Re-export reason code types
pub use ms_mqtt_client::packet::{
    AuthReason, ConnAckReason, DisconnectReason, PubAckReason, SubAckReason, UnsubAckReason,
};

// Re-export misc. packet-related types
pub use ms_mqtt_client::packet::{
    AuthenticationInfo, DeliveryInfo, DeliveryQoS, KeepAlive, PacketIdentifier,
    PayloadFormatIndicator, QoS, RetainHandling, RetainOptions, SessionExpiryInterval,
};
