// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

//! Internal implementation of [`SessionManagedClient`] and [`SessionPubReceiver`].

use std::str::FromStr;
use std::sync::{Arc, Mutex};

use bytes::Bytes;

use crate::control_packet::{
    Publish, PublishProperties, QoS, SubscribeProperties, UnsubscribeProperties,
};
use crate::error::{PublishError, SubscribeError, UnsubscribeError};
use crate::session::receiver::{AckToken, PublishReceiverManager, PublishRx};
use crate::topic::{TopicFilter, TopicParseError};

/// An MQTT client that has it's connection state externally managed by a [`Session`](super::Session).
/// Can be used to send messages and create receivers for incoming messages.
#[derive(Clone)]
pub struct SessionManagedClient {
    // Client ID of the `Session` that manages this client
    pub(crate) client_id: String,
    // PubSub for sending outgoing MQTT messages
    pub(crate) client: azure_mqtt::client::Client,
    /// Manager for receivers
    pub(crate) receiver_manager: Arc<Mutex<PublishReceiverManager>>,
}

impl SessionManagedClient {
    pub fn client_id(&self) -> &str {
        &self.client_id
    }

    pub fn create_filtered_pub_receiver(
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

    pub fn create_unfiltered_pub_receiver(&self) -> SessionPubReceiver {
        let pub_rx = self
            .receiver_manager
            .lock()
            .unwrap()
            .create_unfiltered_receiver();
        SessionPubReceiver { pub_rx }
    }

    pub async fn publish_qos0(
        &self,
        topic: impl Into<String> + Send,
        retain: bool,
        payload: impl Into<Bytes> + Send,
        properties: PublishProperties,
    ) -> Result<CompletionToken, PublishError> {
        let topic = azure_mqtt::topic::TopicName::new(topic.into())?;
        self.client
            .publish_qos0(topic, payload.into(), retain, properties)
            .await
    }

    pub async fn publish_qos1(
        &self,
        topic: impl Into<String> + Send,
        retain: bool,
        payload: impl Into<Bytes> + Send,
        properties: PublishProperties,
    ) -> Result<CompletionToken, PublishError> {
        let topic = azure_mqtt::topic::TopicName::new(topic.into())?;
        self.client
            .publish_qos1(topic, payload.into(), retain, properties)
            .await
    }

    pub async fn subscribe(
        &self,
        topic: impl Into<String> + Send,
        qos: QoS,
        no_local: bool,
        retain_as_published: bool,
        retain_handling: azure_mqtt::packet::RetainHandling,
        properties: SubscribeProperties,
    ) -> Result<CompletionToken, SubscribeError> {
        let topic = azure_mqtt::topic::TopicFilter::new(topic.into())?;
        self.client
            .subscribe(
                topic,
                qos,
                no_local,
                retain_as_published,
                retain_handling,
                properties,
            )
            .await
    }

    pub async fn unsubscribe(
        &self,
        topic: impl Into<String> + Send,
        properties: UnsubscribeProperties,
    ) -> Result<CompletionToken, UnsubscribeError> {
        let topic = azure_mqtt::topic::TopicFilter::new(topic.into())?;
        self.client.unsubscribe(topic, properties).await
    }
}

/// Receive and acknowledge incoming MQTT messages.
pub struct SessionPubReceiver {
    /// Receiver for incoming publishes
    pub_rx: PublishRx,
}

impl SessionPubReceiver {
    pub async fn recv(&mut self) -> Option<Publish> {
        self.pub_rx.recv().await.map(|(publish, _)| publish)
    }

    pub async fn recv_manual_ack(&mut self) -> Option<(Publish, Option<AckToken>)> {
        self.pub_rx.recv().await
    }

    pub fn close(&mut self) {
        self.pub_rx.close();
    }
}
