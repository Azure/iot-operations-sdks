// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

//! Traits and types for defining sets and subsets of MQTT client functionality.

use async_trait::async_trait;
use bytes::Bytes;

use crate::control_packet::{
    AuthProperties, Publish, PublishProperties, QoS, SubscribeProperties, UnsubscribeProperties,
};
use crate::error::{ClientError, CompletionError, ConnectionError};
use crate::topic::TopicParseError;
use crate::Event;

// TODO: restrict the visibility of these to match InternalClient
/// Data for acking a publish. Currently internal use only.
pub type ManualAck = rumqttc::v5::ManualAck;
/// Reason Code for ack. Currently internal use only.
pub type ManualAckReason = rumqttc::v5::ManualAckReason;

// ---------- Concrete Types ----------

/// Awaitable token indicating completion of MQTT message delivery.
pub struct CompletionToken(pub rumqttc::NoticeFuture);

// NOTE: Ideally, this would impl Future instead, but the rumqttc NoticeFuture does not implement Future
impl CompletionToken {
    /// Wait for the ack to be received
    ///
    /// # Errors
    /// Returns a [`CompletionError`](error::CompletionError) if the response indicates the operation failed.
    pub async fn wait(self) -> Result<(), CompletionError> {
        self.0.wait_async().await
    }
}

// pub struct BoxCompletionToken {
//     inner: Box<dyn std::future::Future<Output = Result<(), error::CompletionError>> + Send>,
// }

// impl std::future::Future for BoxCompletionToken {
//     type Output = Result<(), error::CompletionError>;

//     fn poll(self: std::pin::Pin<&mut Self>, cx: &mut std::task::Context<'_>) -> std::task::Poll<Self::Output> {
//         //self.inner.as_mut().poll(cx)
//         let inner = unsafe { self.map_unchecked_mut(|s| &mut *s.inner)};
//         inner.poll(cx)
//     }
// }

// pub struct MyPubSub {
//     client: rumqttc::v5::AsyncClient,
// }

// use crate::control_packet::QoS;
// use crate::error::ClientError;
// use bytes::Bytes;
// impl MyPubSub {
//     pub async fn publish(&self, topic: String, qos: QoS, retain: bool, payload: Bytes) -> Result<BoxCompletionToken, ClientError>{
//         //let r = self.client.publish(topic, qos, retain, payload).await?;
//         let nf = self.client.publish(topic, qos, retain, payload).await?;
//         let bnf = Box::new(nf.wait_async());
//         let ct = BoxCompletionToken {
//             inner: bnf,
//         };
//         Ok(ct)
//     }
// }

// pub struct NewCompletionToken {

// }

// impl std::future::Future for NewCompletionToken {
//     type Output = Result<(), error::CompletionError>;

//     fn poll(self: std::pin::Pin<&mut Self>, cx: &mut std::task::Context<'_>) -> std::task::Poll<Self::Output> {
//         std::task::Poll::Ready(Ok(()))
//     }
// }

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

/// An MQTT client that has it's connection state externally managed.
/// Can be used to send messages and create receivers for incoming messages.
pub trait ManagedClient: MqttPubSub {
    /// The type of receiver used by this client
    type PubReceiver: PubReceiver + MqttAck;

    /// Get the client id for the MQTT connection
    fn client_id(&self) -> &str;

    /// Creates a new [`PubReceiver`] that receives messages on a specific topic
    ///
    /// # Errors
    /// Returns a [`TopicParseError`] if the pub receiver cannot be registered.
    fn create_filtered_pub_receiver(
        &self,
        topic_filter: &str,
        auto_ack: bool,
    ) -> Result<Self::PubReceiver, TopicParseError>;
}

#[async_trait]
/// Receiver for incoming MQTT messages.
pub trait PubReceiver {
    /// Receives the next incoming publish.
    ///
    /// Return None if there will be no more incoming publishes.
    async fn recv(&mut self) -> Option<Publish>;
}
