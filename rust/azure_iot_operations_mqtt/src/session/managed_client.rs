// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

//! Internal implementation of [`SessionManagedClient`] and [`SessionPubReceiver`].

use std::str::FromStr;
use std::sync::{Arc, Mutex};

use async_trait::async_trait;
use bytes::Bytes;
use tokio::sync::mpsc::UnboundedReceiver;

use crate::control_packet::{
    Publish, PublishProperties, QoS, SubscribeProperties, UnsubscribeProperties,
};
use crate::error::{AckError, PublishError, SubscribeError, UnsubscribeError};
use crate::interface::{CompletionToken, ManagedClient, MqttAck, MqttPubSub, PubReceiver};
use crate::session::dispatcher::IncomingPublishDispatcher;
use crate::session::pub_tracker::{self, PubTracker};
use crate::topic::{TopicFilter, TopicParseError};

impl From<pub_tracker::AckError> for AckError {
    fn from(e: pub_tracker::AckError) -> Self {
        match e {
            pub_tracker::AckError::AckOverflow => AckError::AlreadyAcked,
        }
    }
}

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
    /// Dispatcher for incoming publishes
    pub(crate) incoming_pub_dispatcher: Arc<Mutex<IncomingPublishDispatcher>>,
    /// Tracker for unacked incoming publishes
    pub(crate) pub_tracker: Arc<PubTracker>,
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
        auto_ack: bool,
    ) -> Result<SessionPubReceiver, TopicParseError> {
        let topic_filter = TopicFilter::from_str(topic_filter)?;
        let rx = self
            .incoming_pub_dispatcher
            .lock()
            .unwrap()
            .register_filter(&topic_filter);
        Ok(SessionPubReceiver::new(
            rx,
            self.pub_tracker.clone(),
            auto_ack,
        ))
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
    pub_rx: UnboundedReceiver<Publish>,
    /// Tracker for acks of incoming publishes
    pub_tracker: Arc<PubTracker>,
    /// Controls whether incoming publishes are auto-acked
    auto_ack: bool,
}

impl SessionPubReceiver {
    pub fn new(
        pub_rx: UnboundedReceiver<Publish>,
        pub_tracker: Arc<PubTracker>,
        auto_ack: bool,
    ) -> Self {
        Self {
            pub_rx,
            pub_tracker,
            auto_ack,
        }
    }
}

#[async_trait]
impl PubReceiver for SessionPubReceiver {
    async fn recv(&mut self) -> Option<Publish> {
        let result = self.pub_rx.recv().await;
        if let Some(publish) = &result {
            if self.auto_ack {
                // Ack immediately if auto-ack is enabled
                // NOTE: It is safe to assume that ack does not fail because failure is caused
                // exclusively by ack overflows (i.e. acking a publish more times than it was distributed).
                // By virtue of using auto-ack, this should not happen.
                self.pub_tracker
                    .ack(publish)
                    .await
                    .expect("Auto-ack failed");
                // TODO: Technically, there is an edge case where this panic could still occur.
                // If the incoming publish was distributed to multiple receivers, and one of them
                // was NOT using auto-ack, and somehow was able to get the publish and ack it
                // multiple times before this receiver was able to do an auto-ack, this would make
                // failure possible. Extremely unlikely, but theoretically possible.
                // In order to prevent this (and to make acks more generally portable), we need to
                // redefine Publish to have an internal unique identifier for the receiver it was
                // dispatched to. Out of scope for now.
            }
        }
        result
    }

    fn close(&mut self) {
        self.pub_rx.close();
    }
}

#[async_trait]
impl MqttAck for SessionPubReceiver {
    async fn ack(&self, publish: &Publish) -> Result<(), AckError> {
        self.pub_tracker.ack(publish).await?;
        Ok(())
    }
}

impl Drop for SessionPubReceiver {
    fn drop(&mut self) {
        // Close the receiver channel to ensure no more publishes are dispatched
        // while we clean up.
        self.pub_rx.close();

        // Drain and ack any remaining publishes that are in flight so as not to
        // hold up the ack ordering.
        //
        // NOTE: We MUST do this because if not, the pub tracker can enter a bad state.
        // Consider a SessionPubReceiver that drops while the Session remains alive,
        // where there are dispatched messages in the pub_rx channel. This puts the
        // PubTracker (and thus the Session) in a bad state. There will be an item in
        // it awaiting acks that will never come, thus blocking all other acks from being
        // able to be sent due to ordering rules. Once a publish is dispatched to a
        // SessionPubReceiver, the SessionPubReceiver MUST ack them all.
        while let Ok(publish) = self.pub_rx.try_recv() {
            // NOTE: Not ideal to spawn these tasks in a drop, but it can be safely
            // done here by moving the necessary values.
            log::warn!(
                "Dropping SessionPubReceiver with unacked publish (PKID {}). Auto-acking.",
                publish.pkid
            );
            tokio::task::spawn({
                let pub_tracker = self.pub_tracker.clone();
                let publish = publish;
                async move {
                    match pub_tracker.ack(&publish).await {
                        Ok(()) => log::debug!("Auto-ack of PKID {} successful", publish.pkid),
                        Err(e) => log::error!(
                            "Auto-ack failed for {}. Publish may be redelivered. Reason: {e:?}",
                            publish.pkid
                        ),
                        // TODO: Similar to the comment in `recv`, this error can only happen
                        // if another receiver received the same message and acked it multiple
                        // times. Unlike that case, there's no tight race condition here, so this
                        // one is a much more likely scenario. Thus, there's no .expect() here.
                        // Again, this logic would become unnecessary if Publish had a unique
                        // identifier per dispatched receiver.
                    };
                }
            });
        }
    }
}
