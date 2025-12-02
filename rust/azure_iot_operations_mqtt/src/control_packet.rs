// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

//! Structures representing MQTT control packets and the values contained within them.

/// Topic Name
pub type TopicName = azure_mqtt::topic::TopicName;
/// Topic Filter
pub type TopicFilter = azure_mqtt::topic::TopicFilter;

/// PUBLISH packet
pub type Publish = azure_mqtt::packet::Publish;
/// PUBACK packet
pub type PubAck = azure_mqtt::packet::PubAck;
/// SUBACK packet
pub type SubAck = azure_mqtt::packet::SubAck;
/// UNSUBACK packet
pub type UnsubAck = azure_mqtt::packet::UnsubAck;
/// DISCONNECT packet
pub type Disconnect = azure_mqtt::packet::Disconnect;
/// AUTH packet
pub type Auth = azure_mqtt::packet::Auth;

/// Properties for a CONNECT packet
pub type ConnectProperties = azure_mqtt::packet::ConnectProperties;
/// Properties for a PUBLISH packet
pub type PublishProperties = azure_mqtt::packet::PublishProperties;
/// Properties for a SUBSCRIBE packet
pub type SubscribeProperties = azure_mqtt::packet::SubscribeProperties;
/// Properties for a UNSUBSCRIBE packet
pub type UnsubscribeProperties = azure_mqtt::packet::UnsubscribeProperties;
/// Properties for a DISCONNECT packet
pub type DisconnectProperties = azure_mqtt::packet::DisconnectProperties;
/// Properties for an AUTH packet
pub type AuthProperties = azure_mqtt::packet::AuthProperties;

/// CONNECT Return Code
pub type ConnectReturnCode = azure_mqtt::packet::ConnAckReason;
/// PUBACK Reason Code
pub type PubAckReasonCode = azure_mqtt::packet::PubAckReason;
/// SUBACK Reason Code
pub type SubAckReasonCode = azure_mqtt::packet::SubAckReason;
/// UNSUBACK Reason Code
pub type UnsubAckReasonCode = azure_mqtt::packet::UnsubAckReason;
/// DISCONNECT Reason Code
pub type DisconnectReasonCode = azure_mqtt::packet::DisconnectReason;
/// AUTH Reason Code
pub type AuthReasonCode = azure_mqtt::packet::AuthReason;

/// Authentication Info
pub type AuthenticationInfo = azure_mqtt::packet::AuthenticationInfo;
/// Retain Options
pub type RetainOptions = azure_mqtt::packet::RetainOptions;
/// Retain Handling
pub type RetainHandling = azure_mqtt::packet::RetainHandling;
/// Session Expiry Interval
pub type SessionExpiryInterval = azure_mqtt::packet::SessionExpiryInterval;
/// Quality of Service
pub type QoS = azure_mqtt::packet::QoS;
/// Quality of Service on a received message
pub type DeliveryQoS = azure_mqtt::packet::DeliveryQoS;
/// Payload Format Indicator
pub type PayloadFormatIndicator = azure_mqtt::packet::PayloadFormatIndicator;
/// Packet Identifier
pub type PacketIdentifier = azure_mqtt::packet::PacketIdentifier;
