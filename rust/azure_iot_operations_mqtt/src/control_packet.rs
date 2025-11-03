// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

//! Structures representing MQTT control packets.

// TODO: Re-implement these instead of just aliasing / add to rumqttc adapter

/// Quality of Service
pub type QoS = azure_mqtt::QoS;

/// PUBLISH packet
pub type Publish = azure_mqtt::Publish;

/// Properties for a CONNECT packet
pub type ConnectProperties = azure_mqtt::ConnectProperties;
/// Properties for a PUBLISH packet
pub type PublishProperties = azure_mqtt::PublishProperties;
/// Properties for a SUBSCRIBE packet
pub type SubscribeProperties = azure_mqtt::SubscribeProperties;
/// Properties for a UNSUBSCRIBE packet
pub type UnsubscribeProperties = azure_mqtt::UnsubscribeProperties;
/// Properties for an AUTH packet
pub type AuthProperties = azure_mqtt::AuthProperties;

/// Connect Return Code
pub type ConnectReturnCode = azure_mqtt::ConnAckReason;
