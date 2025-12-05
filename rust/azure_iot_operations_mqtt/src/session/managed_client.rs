// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

//! Internal implementation of [`SessionManagedClient`] and [`SessionPubReceiver`].
use std::sync::{Arc, Mutex};

use bytes::Bytes;

use crate::control_packet::{
    Publish, PublishProperties, QoS, RetainOptions, SubscribeProperties, TopicFilter, TopicName,
    UnsubscribeProperties,
};
use crate::error::DetachedError;
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
    /// Get the client id used by this Session
    #[must_use]
    pub fn client_id(&self) -> &str {
        &self.client_id
    }

    /// Creates a new [`SessionPubReceiver`] that will receive incoming publishes matching the
    /// provided topic filter.
    ///
    /// Note that you still must subscribe before you can receive any messages.
    ///
    /// # Panics
    /// Panics if internal state is invalid (this should not be possible).
    #[must_use]
    pub fn create_filtered_pub_receiver(&self, topic_filter: TopicFilter) -> SessionPubReceiver {
        let pub_rx = self
            .dispatcher
            .lock()
            .unwrap()
            .create_filtered_receiver(topic_filter);
        SessionPubReceiver { pub_rx }
    }

    /// Creates a new [`SessionPubReceiver`] that will receive all incoming publishes that are NOT
    /// sent to any filtered receivers.
    ///
    /// If you want to receive ALL publishes, use a filtered receiver with a wildcard topic (#).
    ///
    /// Note that you still must subscribe before you can receive any messages.
    ///
    /// # Panics
    /// Panics if internal state is invalid (this should not be possible).
    #[must_use]
    pub fn create_unfiltered_pub_receiver(&self) -> SessionPubReceiver {
        let pub_rx = self.dispatcher.lock().unwrap().create_unfiltered_receiver();
        SessionPubReceiver { pub_rx }
    }

    /// Issue an MQTT `PUBLISH` at Quality of Service 0 ("at most once" delivery).
    ///
    /// If connection is unavailable, `PUBLISH` will be queued and delivered when connection is
    /// re-established. Blocks if at capacity for queueing.
    ///
    /// Returns a token that can be awaited to indicate the result of the completion of the
    /// `PUBLISH` operation (i.e. when the `PUBLISH` has been sent to the server).
    ///
    /// # Errors
    /// Returns a [`DetachedError`] if the `PUBLISH` could not be issued due to being detached from
    /// the Session
    pub async fn publish_qos0(
        &self,
        topic: TopicName,
        retain: bool,
        payload: impl Into<Bytes> + Send,
        properties: PublishProperties,
    ) -> Result<PublishQoS0CompletionToken, DetachedError> {
        self.client
            .publish_qos0(topic, payload.into(), retain, properties)
            .await
    }

    /// Issue an MQTT `PUBLISH` at Quality of Service 1 ("at least once" delivery).
    ///
    /// If connection is unavailable, `PUBLISH` will be queued and delivered when connection is
    /// re-established. Blocks if at capacity for queueing.
    ///
    /// Returns a token that can be awaited to indicate the result of the completion of the
    /// `PUBLISH` operation (i.e. when the corresponding PUBACK is received from the server).
    ///
    /// # Errors
    /// Returns a [`DetachedError`] if the `PUBLISH` could not be issued due to being detached from
    /// the Session
    pub async fn publish_qos1(
        &self,
        topic: TopicName,
        retain: bool,
        payload: impl Into<Bytes> + Send,
        properties: PublishProperties,
    ) -> Result<PublishQoS1CompletionToken, DetachedError> {
        self.client
            .publish_qos1(topic, payload.into(), retain, properties)
            .await
    }

    /// Issue an MQTT `SUBSCRIBE` to receive `PUBLISH`es on the provided topic filter.
    ///
    /// If connection is unavailable, `SUBSCRIBE` will be queued and delivered when connection is
    /// re-established. Blocks if at capacity for queueing.
    ///
    /// Returns a token that can be awaited to indicate the result of the completion of the
    /// `SUBSCRIBE` operation (i.e. when the corresponding SUBACK is received from the server).
    ///
    /// # Errors
    /// Returns a [`DetachedError`] if the `SUBSCRIBE` could not be issued due to being detached from
    /// the Session
    pub async fn subscribe(
        &self,
        topic_filter: TopicFilter,
        max_qos: QoS,
        no_local: bool,
        retain_options: RetainOptions,
        properties: SubscribeProperties,
    ) -> Result<SubscribeCompletionToken, DetachedError> {
        self.client
            .subscribe(topic_filter, max_qos, no_local, retain_options, properties)
            .await
    }

    /// Issue an MQTT `UNSUBSCRIBE` to stop receiving `PUBLISH`es on the provided topic filter.
    ///
    /// If connection is unavailable, `UNSUBSCRIBE` will be queued and delivered when connection is
    /// re-established. Blocks if at capacity for queueing.
    ///
    /// Returns a token that can be awaited to indicate the result of the completion of the
    /// `UNSUBSCRIBE` operation (i.e. when the corresponding UNSUBACK is received from the server).
    ///
    /// # Errors
    /// Returns a [`DetachedError`] if the `UNSUBSCRIBE` could not be issued due to being detached
    /// from the Session
    pub async fn unsubscribe(
        &self,
        topic_filter: TopicFilter,
        properties: UnsubscribeProperties,
    ) -> Result<UnsubscribeCompletionToken, DetachedError> {
        self.client.unsubscribe(topic_filter, properties).await
    }
}

/// Receive and acknowledge incoming [`Publish`]es
pub struct SessionPubReceiver {
    /// Receiver for incoming publishes
    pub_rx: PublishRx,
}

impl SessionPubReceiver {
    /// Receive the next incoming [`Publish`] delivered to this receiver.
    /// The [`Publish`] will be automatically acknowledged upon delivery if QoS 1.
    pub async fn recv(&mut self) -> Option<Publish> {
        self.pub_rx.recv().await.map(|(publish, _)| publish)
    }

    /// Receive the next incoming [`Publish`] delivered to this receiver, along with an
    /// [`AckToken`] if received at QoS 1.
    /// The [`AckToken`] can be used to manually acknowledge the [`Publish`].
    pub async fn recv_manual_ack(&mut self) -> Option<(Publish, Option<AckToken>)> {
        self.pub_rx.recv().await
    }

    /// Close this receiver, dropping all undelivered [`Publish`]es.
    /// Any [`Publish`]es undelivered that required acknowledgement will be automatically
    /// acknowledged on drop.
    pub fn close(&mut self) {
        self.pub_rx.close();
    }
}
