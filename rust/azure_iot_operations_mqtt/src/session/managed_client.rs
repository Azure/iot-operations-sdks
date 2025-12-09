// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

//! Internal implementation of [`SessionManagedClient`] and [`SessionPubReceiver`].

use std::str::FromStr;
use std::sync::{Arc, Mutex};

use async_trait::async_trait;
use bytes::Bytes;

use crate::control_packet::{
    Publish, PublishProperties, QoS, SubscribeProperties, UnsubscribeProperties,
};
use crate::error::{PublishError, SubscribeError, UnsubscribeError};
use crate::interface::{CompletionToken, ManagedClient, MqttPubSub, PubReceiver};
use crate::session::receiver::{AckToken, PublishReceiverManager, PublishRx};
use crate::topic::{TopicFilter, TopicParseError};

/// An MQTT client that has it's connection state externally managed by a [`Session`](super::Session).
/// Can be used to send messages and create receivers for incoming messages.
#[derive(Clone)]
pub struct SessionManagedClient<PS>
where
    PS: MqttPubSub + Clone + Send + Sync,
{
    // Client ID of the `Session` that manages this client
    pub(crate) client_id: String,
    // PubSub for sending outgoing MQTT messages
    pub(crate) pub_sub: PS,
    /// Manager for receivers
    pub(crate) receiver_manager: Arc<Mutex<PublishReceiverManager>>,
}

impl<PS> ManagedClient for SessionManagedClient<PS>
where
    PS: MqttPubSub + Clone + Send + Sync,
{
    type PubReceiver = SessionPubReceiver;

    fn client_id(&self) -> &str {
        &self.client_id
    }

    fn create_filtered_pub_receiver(
        &self,
        topic_filter: &str,
    ) -> Result<SessionPubReceiver, TopicParseError> {
        let topic_filter = TopicFilter::from_str(topic_filter)?;
        let pub_rx = self
            .receiver_manager
            .lock()
            .unwrap()
            .create_filtered_receiver(&topic_filter);
        Ok(SessionPubReceiver { pub_rx })
    }

    fn create_unfiltered_pub_receiver(&self) -> SessionPubReceiver {
        let pub_rx = self
            .receiver_manager
            .lock()
            .unwrap()
            .create_unfiltered_receiver();
        SessionPubReceiver { pub_rx }
    }
}

#[async_trait]
impl<PS> MqttPubSub for SessionManagedClient<PS>
where
    PS: MqttPubSub + Clone + Send + Sync,
{
    async fn publish(
        &self,
        topic: impl Into<String> + Send,
        qos: QoS,
        retain: bool,
        payload: impl Into<Bytes> + Send,
    ) -> Result<CompletionToken, PublishError> {
        self.pub_sub.publish(topic, qos, retain, payload).await
    }

    async fn publish_with_properties(
        &self,
        topic: impl Into<String> + Send,
        qos: QoS,
        retain: bool,
        payload: impl Into<Bytes> + Send,
        properties: PublishProperties,
    ) -> Result<CompletionToken, PublishError> {
        self.pub_sub
            .publish_with_properties(topic, qos, retain, payload, properties)
            .await
    }

    async fn subscribe(
        &self,
        topic: impl Into<String> + Send,
        qos: QoS,
    ) -> Result<CompletionToken, SubscribeError> {
        self.pub_sub.subscribe(topic, qos).await
    }

    async fn subscribe_with_properties(
        &self,
        topic: impl Into<String> + Send,
        qos: QoS,
        properties: SubscribeProperties,
    ) -> Result<CompletionToken, SubscribeError> {
        self.pub_sub
            .subscribe_with_properties(topic, qos, properties)
            .await
    }

    async fn unsubscribe(
        &self,
        topic: impl Into<String> + Send,
    ) -> Result<CompletionToken, UnsubscribeError> {
        self.pub_sub.unsubscribe(topic).await
    }

    async fn unsubscribe_with_properties(
        &self,
        topic: impl Into<String> + Send,
        properties: UnsubscribeProperties,
    ) -> Result<CompletionToken, UnsubscribeError> {
        self.pub_sub
            .unsubscribe_with_properties(topic, properties)
            .await
    }
}

/// Receive and acknowledge incoming MQTT messages.
pub struct SessionPubReceiver {
    /// Receiver for incoming publishes
    pub_rx: PublishRx,
}

#[async_trait]
impl PubReceiver for SessionPubReceiver {
    async fn recv(&mut self) -> Option<Publish> {
        self.pub_rx.recv().await.map(|(publish, _)| publish)
    }

    async fn recv_manual_ack(&mut self) -> Option<(Publish, Option<AckToken>)> {
        self.pub_rx.recv().await
    }

    fn close(&mut self) {
        self.pub_rx.close();
    }
}
