// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

//! Traits and types for defining sets and subsets of MQTT client functionality.

use async_trait::async_trait;
use bytes::Bytes;

use crate::control_packet::{
    AuthProperties, Publish, PublishProperties, QoS, SubscribeProperties, UnsubscribeProperties,
};
use crate::error::{
    AckError, CompletionError, ConnectionError, DisconnectError, PublishError, ReauthError,
    SubscribeError, UnsubscribeError,
};
pub use crate::session::receiver::AckToken; // TODO: remove this pub re-export after concretized receivers / managed clients
use crate::topic::TopicParseError;

// ---------- Concrete Types ----------

/// Awaitable token indicating completion of MQTT message delivery.
pub struct CompletionToken(
    pub Box<dyn std::future::Future<Output = Result<(), CompletionError>> + Send>,
);

impl std::future::Future for CompletionToken {
    type Output = Result<(), CompletionError>;

    fn poll(
        self: std::pin::Pin<&mut Self>,
        cx: &mut std::task::Context<'_>,
    ) -> std::task::Poll<Self::Output> {
        // NOTE: Need to use `unsafe` here because we need to poll the inner future, but can't get
        // a mutable reference to it, as it's in a box (at least, not without unsafe code).
        // It is safe for us to use the `unsafe` code for the following reasons:
        //
        // 1. This CompletionToken struct is the only reference to the boxed future that it holds
        // internally, so mutability among multiple references is not a concern. Nowhere is this
        // struct used in a way that the inner future can be accessed from multiple threads,
        // and so we are safe from race conditions.
        //
        // 2. We do not move the memory - the future stays right where it is, all we do is poll it,
        // so there is no risk of a null pointer error / segfault.
        let inner = unsafe { self.map_unchecked_mut(|s| &mut *s.0) };
        inner.poll(cx)
    }
}

// Re-export rumqttc types to avoid user code taking the dependency.
// TODO: Re-implement these instead of just aliasing / add to rumqttc adapter
// Only once there are non-rumqttc implementations of these can we allow non-rumqttc compilations

/// Event yielded by the event loop
pub type Event = rumqttc::v5::Event;
/// Incoming data on the event loop
pub type Incoming = rumqttc::v5::Incoming;
/// Outgoing data on the event loop
pub type Outgoing = rumqttc::Outgoing;

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
    ) -> Result<CompletionToken, PublishError>;

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
    ) -> Result<CompletionToken, PublishError>;

    /// MQTT Subscribe
    ///
    /// If connection is unavailable, subscribe will be queued and delivered when connection is re-established.
    /// Blocks if at capacity for queueing.
    async fn subscribe(
        &self,
        topic: impl Into<String> + Send,
        qos: QoS,
    ) -> Result<CompletionToken, SubscribeError>;

    /// MQTT Subscribe
    ///
    /// If connection is unavailable, subscribe will be queued and delivered when connection is re-established.
    /// Blocks if at capacity for queueing.
    async fn subscribe_with_properties(
        &self,
        topic: impl Into<String> + Send,
        qos: QoS,
        properties: SubscribeProperties,
    ) -> Result<CompletionToken, SubscribeError>;

    /// MQTT Unsubscribe
    ///
    /// If connection is unavailable, unsubscribe will be queued and delivered when connection is re-established.
    /// Blocks if at capacity for queueing.
    async fn unsubscribe(
        &self,
        topic: impl Into<String> + Send,
    ) -> Result<CompletionToken, UnsubscribeError>;

    /// MQTT Unsubscribe
    ///
    /// If connection is unavailable, unsubscribe will be queued and delivered when connection is re-established.
    /// Blocks if at capacity for queueing.
    async fn unsubscribe_with_properties(
        &self,
        topic: impl Into<String> + Send,
        properties: UnsubscribeProperties,
    ) -> Result<CompletionToken, UnsubscribeError>;
}

/// Provides functionality for acknowledging a received Publish message (QoS 1)
#[async_trait]
pub trait MqttAck {
    /// Acknowledge a received Publish.
    async fn ack(&self, publish: &Publish) -> Result<CompletionToken, AckError>;
}

// TODO: consider scoping this to also include a `connect`. Not currently needed, but would be more flexible,
// and make a lot more sense
/// MQTT disconnect functionality
#[async_trait]
pub trait MqttDisconnect {
    /// Disconnect from the MQTT broker.
    async fn disconnect(&self) -> Result<(), DisconnectError>;
}

/// Internally-facing APIs for the underlying client.
/// Use of this trait is not currently recommended except for mocking.
#[async_trait]
pub trait MqttClient: MqttPubSub + MqttAck + MqttDisconnect {
    /// Reauthenticate with the MQTT broker
    async fn reauth(&self, auth_props: AuthProperties) -> Result<(), ReauthError>;
}

/// MQTT Event Loop manipulation
#[async_trait]
pub trait MqttEventLoop {
    /// Poll the event loop for the next [`Event`]
    async fn poll(&mut self) -> Result<Event, ConnectionError>;

    /// Modify the clean start flag for subsequent MQTT connection attempts
    fn set_clean_start(&mut self, clean_start: bool);

    /// Set the authentication method
    fn set_authentication_method(&mut self, authentication_method: Option<String>);

    /// Set the authentication data
    fn set_authentication_data(&mut self, authentication_data: Option<Bytes>);
}

// ---------- Higher level MQTT abstractions ----------

/// An MQTT client that has it's connection state externally managed.
/// Can be used to send messages and create receivers for incoming messages.
pub trait ManagedClient: MqttPubSub {
    /// The type of receiver used by this client
    type PubReceiver: PubReceiver;

    /// Get the client id for the MQTT connection
    fn client_id(&self) -> &str;

    /// Creates a new [`PubReceiver`] that receives messages on a specific topic
    ///
    /// # Errors
    /// Returns a [`TopicParseError`] if the pub receiver cannot be registered.
    fn create_filtered_pub_receiver(
        &self,
        topic_filter: &str,
    ) -> Result<Self::PubReceiver, TopicParseError>;

    /// Creates a new [`PubReceiver`] that receives all messages not sent to other
    /// filtered receivers.
    fn create_unfiltered_pub_receiver(&self) -> Self::PubReceiver;
}

#[async_trait]
/// Receiver for incoming MQTT messages.
pub trait PubReceiver {
    /// Receives the next incoming publish.
    ///
    /// Return None if there will be no more incoming publishes.
    async fn recv(&mut self) -> Option<Publish>;

    /// Receives the next incoming publish, and a token that can be used to manually acknowledge
    /// the publish (Quality of Service 1 or 2), or `None` (Quality of Service 0).
    ///
    /// Return None if there will be no more incoming publishes.
    async fn recv_manual_ack(&mut self) -> Option<(Publish, Option<AckToken>)>;

    /// Close the receiver, preventing further incoming publishes.
    ///
    /// To guarantee no publish loss, `recv()`/`recv_manual_ack()` must be called until `None` is returned.
    fn close(&mut self);
}
