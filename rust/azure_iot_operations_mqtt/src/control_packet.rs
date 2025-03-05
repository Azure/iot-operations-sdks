// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

//! Structures representing MQTT control packets.

// TODO: Re-implement these instead of just aliasing / add to rumqttc adapter

/// Quality of Service
pub type QoS = rumqttc::v5::mqttbytes::QoS;

/// Enumerates possible packets
pub type Request = rumqttc::v5::Request;

/// PUBLISH packet
pub type Publish = rumqttc::v5::mqttbytes::v5::Publish;
/// SUBSCRIBE packet
pub type Subscribe = rumqttc::v5::mqttbytes::v5::Subscribe;
/// UNSUBSCRIBE packet
pub type Unsubscribe = rumqttc::v5::mqttbytes::v5::Unsubscribe;
/// AUTH packet
pub type Auth = rumqttc::v5::mqttbytes::v5::Auth;

/// Properties for a CONNECT packet
pub type ConnectProperties = rumqttc::v5::mqttbytes::v5::ConnectProperties;
/// Properties for a PUBLISH packet
pub type PublishProperties = rumqttc::v5::mqttbytes::v5::PublishProperties;
/// Properties for a SUBSCRIBE packet
pub type SubscribeProperties = rumqttc::v5::mqttbytes::v5::SubscribeProperties;
/// Properties for a UNSUBSCRIBE packet
pub type UnsubscribeProperties = rumqttc::v5::mqttbytes::v5::UnsubscribeProperties;
/// Properties for an AUTH packet
pub type AuthProperties = rumqttc::v5::mqttbytes::v5::AuthProperties;
