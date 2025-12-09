// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

//! Structures representing MQTT control packets and the values contained within them.

/// Topic Name
pub type TopicName = crate::azure_mqtt::topic::TopicName;
/// Topic Filter
pub type TopicFilter = crate::azure_mqtt::topic::TopicFilter;

/// PUBLISH packet
pub type Publish = crate::azure_mqtt::packet::Publish;
/// PUBACK packet
pub type PubAck = crate::azure_mqtt::packet::PubAck;
/// SUBACK packet
pub type SubAck = crate::azure_mqtt::packet::SubAck;
/// UNSUBACK packet
pub type UnsubAck = crate::azure_mqtt::packet::UnsubAck;
/// DISCONNECT packet
pub type Disconnect = crate::azure_mqtt::packet::Disconnect;
/// AUTH packet
pub type Auth = crate::azure_mqtt::packet::Auth;

/// Properties for a CONNECT packet
pub type ConnectProperties = crate::azure_mqtt::packet::ConnectProperties;
/// Properties for a PUBLISH packet
pub type PublishProperties = crate::azure_mqtt::packet::PublishProperties;
/// Properties for a SUBSCRIBE packet
pub type SubscribeProperties = crate::azure_mqtt::packet::SubscribeProperties;
/// Properties for a UNSUBSCRIBE packet
pub type UnsubscribeProperties = crate::azure_mqtt::packet::UnsubscribeProperties;
/// Properties for a DISCONNECT packet
pub type DisconnectProperties = crate::azure_mqtt::packet::DisconnectProperties;
/// Properties for an AUTH packet
pub type AuthProperties = crate::azure_mqtt::packet::AuthProperties;

/// CONNECT Return Code
pub type ConnectReturnCode = crate::azure_mqtt::packet::ConnAckReason;
/// PUBACK Reason Code
pub type PubAckReasonCode = crate::azure_mqtt::packet::PubAckReason;
/// SUBACK Reason Code
pub type SubAckReasonCode = crate::azure_mqtt::packet::SubAckReason;
/// UNSUBACK Reason Code
pub type UnsubAckReasonCode = crate::azure_mqtt::packet::UnsubAckReason;
/// DISCONNECT Reason Code
pub type DisconnectReasonCode = crate::azure_mqtt::packet::DisconnectReason;
/// AUTH Reason Code
pub type AuthReasonCode = crate::azure_mqtt::packet::AuthReason;

/// Authentication Info
pub type AuthenticationInfo = crate::azure_mqtt::packet::AuthenticationInfo;
/// Retain Options
pub type RetainOptions = crate::azure_mqtt::packet::RetainOptions;
/// Retain Handling
pub type RetainHandling = crate::azure_mqtt::packet::RetainHandling;
/// Session Expiry Interval
pub type SessionExpiryInterval = crate::azure_mqtt::packet::SessionExpiryInterval;
/// Quality of Service
pub type QoS = crate::azure_mqtt::packet::QoS;
/// Quality of Service on a received message
pub type DeliveryQoS = crate::azure_mqtt::packet::DeliveryQoS;
/// Payload Format Indicator
pub type PayloadFormatIndicator = crate::azure_mqtt::packet::PayloadFormatIndicator;
/// Packet Identifier
pub type PacketIdentifier = crate::azure_mqtt::packet::PacketIdentifier;
