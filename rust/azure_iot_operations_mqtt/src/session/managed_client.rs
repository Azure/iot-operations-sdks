// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

//! Internal implementation of [`SessionManagedClient`] and [`SessionPubReceiver`].
use std::sync::{Arc, Mutex};

use bytes::Bytes;

use crate::control_packet::{
    Publish, PublishProperties, QoS, RetainHandling, SubscribeProperties, TopicFilter, TopicName,
    UnsubscribeProperties,
};
use crate::error::ClientError;
use crate::session::dispatcher::{AckToken, IncomingPublishDispatcher, PublishRx};
use crate::token::{
    PublishQoS0CompletionToken, PublishQoS1CompletionToken, SubscribeCompletionToken,
    UnsubscribeCompletionToken,
};

/// An MQTT client that has it's connection state externally managed by a [`Session`](super::Session).
/// Can be used to send messages and create receivers for incoming messages.
#[derive(Clone)]
pub struct SessionManagedClient {
    // Client ID of the `Session` that manages this client
    pub(crate) client_id: String,
    // PubSub for sending outgoing MQTT messages
    pub(crate) client: azure_mqtt::client::Client,
    /// Manager for receivers
    pub(crate) dispatcher: Arc<Mutex<IncomingPublishDispatcher>>,
}

impl SessionManagedClient {
    pub fn client_id(&self) -> &str {
        &self.client_id
    }

    pub fn create_filtered_pub_receiver(&self, topic_filter: &TopicFilter) -> SessionPubReceiver {
        let pub_rx = self
            .dispatcher
            .lock()
            .unwrap()
            .create_filtered_receiver(&topic_filter);
        SessionPubReceiver { pub_rx }
    }

    pub fn create_unfiltered_pub_receiver(self) -> SessionPubReceiver {
        let pub_rx = self.dispatcher.lock().unwrap().create_unfiltered_receiver();
        SessionPubReceiver { pub_rx }
    }

    pub async fn publish_qos0(
        &self,
        topic: TopicName,
        retain: bool,
        payload: impl Into<Bytes> + Send,
        properties: PublishProperties,
    ) -> Result<PublishQoS0CompletionToken, ClientError> {
        self.client
            .publish_qos0(topic, payload.into(), retain, properties)
            .await
    }

    pub async fn publish_qos1(
        &self,
        topic: TopicName,
        retain: bool,
        payload: impl Into<Bytes> + Send,
        properties: PublishProperties,
    ) -> Result<PublishQoS1CompletionToken, ClientError> {
        self.client
            .publish_qos1(topic, payload.into(), retain, properties)
            .await
    }

    pub async fn subscribe(
        &self,
        topic_filter: TopicFilter,
        max_qos: QoS,
        no_local: bool,
        retain_as_published: bool,
        retain_handling: RetainHandling,
        properties: SubscribeProperties,
    ) -> Result<SubscribeCompletionToken, ClientError> {
        self.client
            .subscribe(
                topic_filter,
                max_qos,
                no_local,
                retain_as_published,
                retain_handling,
                properties,
            )
            .await
    }

    pub async fn unsubscribe(
        &self,
        topic_filter: TopicFilter,
        properties: UnsubscribeProperties,
    ) -> Result<UnsubscribeCompletionToken, ClientError> {
        self.client.unsubscribe(topic_filter, properties).await
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
