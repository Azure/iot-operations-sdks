// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

//! Structures representing MQTT control packets.

// TODO: Re-implement these instead of just aliasing / add to rumqttc adapter

/// Quality of Service
pub type QoS = azure_mqtt::packet::QoS;

/// PUBLISH packet
pub type Publish = azure_mqtt::packet::Publish;

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
