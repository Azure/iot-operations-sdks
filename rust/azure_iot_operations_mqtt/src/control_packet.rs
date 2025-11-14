// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

//! Structures representing MQTT control packets and the values contained within them.

/// Topic Name
pub type TopicName = azure_mqtt::topic::TopicName;
/// Topic Filter
pub type TopicFilter = azure_mqtt::topic::TopicFilter;

/// Quality of Service
pub type QoS = azure_mqtt::packet::QoS;

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

/// Properties for a CONNECT packet
pub type ConnectProperties = azure_mqtt::packet::ConnectProperties;
/// Properties for a PUBLISH packet
pub type PublishProperties = azure_mqtt::packet::PublishProperties;
/// Properties for a SUBSCRIBE packet
pub type SubscribeProperties = azure_mqtt::packet::SubscribeProperties;
/// Properties for a UNSUBSCRIBE packet
pub type UnsubscribeProperties = azure_mqtt::packet::UnsubscribeProperties;
/// Properties for an AUTH packet
pub type AuthProperties = azure_mqtt::packet::AuthProperties;

/// Connect Return Code
pub type ConnectReturnCode = azure_mqtt::packet::ConnAckReason;

/// Retain Handling
pub type RetainHandling = azure_mqtt::packet::RetainHandling;
/// Session Expiry Interval
pub type SessionExpiryInterval = azure_mqtt::packet::SessionExpiryInterval;
