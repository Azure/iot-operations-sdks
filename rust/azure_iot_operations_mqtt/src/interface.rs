// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

//! Traits and types for defining sets and subsets of MQTT client functionality.

use async_trait::async_trait;
use bytes::Bytes;

use crate::control_packet::{
    AuthProperties, Publish, PublishProperties, QoS, SubscribeProperties, UnsubscribeProperties,
};
use crate::error::{ClientError, ConnectionError};
use crate::topic::TopicParseError;
use crate::{CompletionToken, Event};

// TODO: restrict the visibility of these to match InternalClient
/// Data for acking a publish. Currently internal use only.
pub type ManualAck = rumqttc::v5::ManualAck;
/// Reason Code for ack. Currently internal use only.
pub type ManualAckReason = rumqttc::v5::ManualAckReason;

// ---------- Lower level MQTT abstractions ----------

/// MQTT publish, subscribe and unsubscribe functionality
#[async_trait]
pub trait MqttPubSub {
    /// MQTT Publish
    ///
    /// If connection is unavailable, publish will be queued and delivered when connection is re-established.
    /// Blocks if at capacity for queueing.
    async fn publish(
        &self,
        topic: impl Into<String> + Send,
        qos: QoS,
        retain: bool,
        payload: impl Into<Bytes> + Send,
    ) -> Result<CompletionToken, ClientError>;

    /// MQTT Publish
    ///
    /// If connection is unavailable, publish will be queued and delivered when connection is re-established.
    /// Blocks if at capacity for queueing.
    async fn publish_with_properties(
        &self,
        topic: impl Into<String> + Send,
        qos: QoS,
        retain: bool,
        payload: impl Into<Bytes> + Send,
        properties: PublishProperties,
    ) -> Result<CompletionToken, ClientError>;

    /// MQTT Subscribe
    ///
    /// If connection is unavailable, subscribe will be queued and delivered when connection is re-established.
    /// Blocks if at capacity for queueing.
    async fn subscribe(
        &self,
        topic: impl Into<String> + Send,
        qos: QoS,
    ) -> Result<CompletionToken, ClientError>;

    /// MQTT Subscribe
    ///
    /// If connection is unavailable, subscribe will be queued and delivered when connection is re-established.
    /// Blocks if at capacity for queueing.
    async fn subscribe_with_properties(
        &self,
        topic: impl Into<String> + Send,
        qos: QoS,
        properties: SubscribeProperties,
    ) -> Result<CompletionToken, ClientError>;

    /// MQTT Unsubscribe
    ///
    /// If connection is unavailable, unsubscribe will be queued and delivered when connection is re-established.
    /// Blocks if at capacity for queueing.
    async fn unsubscribe(
        &self,
        topic: impl Into<String> + Send,
    ) -> Result<CompletionToken, ClientError>;

    /// MQTT Unsubscribe
    ///
    /// If connection is unavailable, unsubscribe will be queued and delivered when connection is re-established.
    /// Blocks if at capacity for queueing.
    async fn unsubscribe_with_properties(
        &self,
        topic: impl Into<String> + Send,
        properties: UnsubscribeProperties,
    ) -> Result<CompletionToken, ClientError>;
}

/// Provides functionality for acknowledging a received Publish message (QoS 1)
#[async_trait]
pub trait MqttAck {
    /// Acknowledge a received Publish.
    async fn ack(&self, publish: &Publish) -> Result<(), ClientError>;
}

// TODO: consider scoping this to also include a `connect`. Not currently needed, but would be more flexible,
// and make a lot more sense
/// MQTT disconnect functionality
#[async_trait]
pub trait MqttDisconnect {
    /// Disconnect from the MQTT broker.
    async fn disconnect(&self) -> Result<(), ClientError>;
}

/// Internally-facing APIs for the underlying client.
/// Use of this trait is not currently recommended except for mocking.
#[async_trait]
pub trait InternalClient: MqttPubSub + MqttAck + MqttDisconnect {
    /// Get a [`ManualAck`] for the given [`Publish`] to send later
    fn get_manual_ack(&self, publish: &Publish) -> ManualAck;

    /// Send a [`ManualAck`] to acknowledge the publish it was created from
    async fn manual_ack(&self, ack: ManualAck) -> Result<(), ClientError>;

    /// Reauthenticate with the MQTT broker
    async fn reauth(&self, auth_props: AuthProperties) -> Result<(), ClientError>;
}

/// MQTT Event Loop manipulation
#[async_trait]
pub trait MqttEventLoop {
    /// Poll the event loop for the next [`Event`]
    async fn poll(&mut self) -> Result<Event, ConnectionError>;

    /// Modify the clean start flag for subsequent MQTT connection attempts
    fn set_clean_start(&mut self, clean_start: bool);
}

// ---------- Higher level MQTT abstractions ----------

#[async_trait]
/// Functionality for receiving an MQTT publish
pub trait MqttPubReceiver {
    /// Receives the next incoming publish.
    ///
    /// Return None if there will be no more incoming publishes.
    async fn recv(&mut self) -> Option<Publish>;
}

// TODO: refactor into "ManagedClient"
/// Spawns [`MqttPubSub`] and [`MqttPubReceiver`]
pub trait MqttProvider<PS, PR>
where
    PS: MqttPubSub + Clone + Send + Sync,
    PR: MqttPubReceiver + Send + Sync,
{
    /// Get the client id for the MQTT connection
    fn client_id(&self) -> &str;

    /// Get an [`MqttPubSub`] for this connection
    fn pub_sub(&self) -> PS;

    /// Get an [`MqttPubReceiver`] for a specific topic
    ///
    /// # Errors
    /// Returns a [`TopicParseError`] if the pub receiver cannot be registered.
    fn filtered_pub_receiver(
        &mut self,
        topic_filter: &str,
        auto_ack: bool,
    ) -> Result<PR, TopicParseError>;
}
