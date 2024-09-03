// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

// TODO: Re-implement these instead of just aliasing / add to rumqttc adapter

/// Quality of Service
pub type QoS = rumqttc::v5::mqttbytes::QoS;

/// PUBLISH packet
pub type Publish = rumqttc::v5::mqttbytes::v5::Publish;

/// Properties for a CONNECT packet
pub type ConnectProperties = rumqttc::v5::mqttbytes::v5::ConnectProperties;
/// Properties for a PUBLISH packet
pub type PublishProperties = rumqttc::v5::mqttbytes::v5::PublishProperties;
/// Properties for a SUBSCRIBE packet
pub type SubscribeProperties = rumqttc::v5::mqttbytes::v5::SubscribeProperties;
/// Properties for a UNSUBSCRIBE packet
pub type UnsubscribeProperties = rumqttc::v5::mqttbytes::v5::UnsubscribeProperties;

#[cfg(test)]
pub type PubAck = rumqttc::v5::mqttbytes::v5::PubAck;
