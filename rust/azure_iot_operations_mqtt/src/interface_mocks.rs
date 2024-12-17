// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

//! Bespoke mocks for relevant traits defined in the interface module.
#![allow(unused_variables)]

use async_trait::async_trait;
use bytes::Bytes;
use tokio::sync::mpsc::{error::SendError, unbounded_channel, UnboundedReceiver, UnboundedSender};

use crate::control_packet::{
    AuthProperties, Publish, PublishProperties, QoS, SubscribeProperties, UnsubscribeProperties,
};
use crate::error::{ClientError, CompletionError, ConnectionError};
use crate::interface::{
    CompletionToken, Event, MqttAck, MqttClient, MqttDisconnect, MqttEventLoop, MqttPubSub,
};

/// Stand-in for the inner future of a [`CompletionToken`].
/// Always returns Ok, indicating the ack was completed.
struct CompletedAckFuture {}

impl std::future::Future for CompletedAckFuture {
    type Output = Result<(), CompletionError>;

    fn poll(
        self: std::pin::Pin<&mut Self>,
        cx: &mut std::task::Context<'_>,
    ) -> std::task::Poll<Self::Output> {
        std::task::Poll::Ready(Ok(()))
    }
}

// TODO: Will need to add a way to choose when acks return, and what rc they provide

/// Mock implementation of an MQTT client.
///
/// Currently always succeeds on all operations.
#[derive(Clone)]
pub struct MockClient {}

impl MockClient {
    /// Return a new mocked MQTT client.
    #[must_use]
    #[allow(clippy::new_without_default)]
    pub fn new() -> Self {
        Self {}
    }
}

// TODO: Need to flesh out the mock more
// - ability to change calls to fail / inject failure
// - ability to check which operations occurred (e.g. Publish, Subscribe, etc.), and the details of those operations
// - ability to check which order the calls ocurred in
// - ability to throttle outgoing events by capacity (e.g. queueing)
// - must be able to track this over all potential clones of the mocked client

#[async_trait]
impl MqttPubSub for MockClient {
    async fn publish(
        &self,
        topic: impl Into<String> + Send,
        qos: QoS,
        retain: bool,
        payload: impl Into<Bytes> + Send,
    ) -> Result<CompletionToken, ClientError> {
        Ok(CompletionToken(Box::new(CompletedAckFuture {})))
    }

    async fn publish_with_properties(
        &self,
        topic: impl Into<String> + Send,
        qos: QoS,
        retain: bool,
        payload: impl Into<Bytes> + Send,
        properties: PublishProperties,
    ) -> Result<CompletionToken, ClientError> {
        Ok(CompletionToken(Box::new(CompletedAckFuture {})))
    }

    async fn subscribe(
        &self,
        topic: impl Into<String> + Send,
        qos: QoS,
    ) -> Result<CompletionToken, ClientError> {
        Ok(CompletionToken(Box::new(CompletedAckFuture {})))
    }

    async fn subscribe_with_properties(
        &self,
        topic: impl Into<String> + Send,
        qos: QoS,
        properties: SubscribeProperties,
    ) -> Result<CompletionToken, ClientError> {
        Ok(CompletionToken(Box::new(CompletedAckFuture {})))
    }

    async fn unsubscribe(
        &self,
        topic: impl Into<String> + Send,
    ) -> Result<CompletionToken, ClientError> {
        Ok(CompletionToken(Box::new(CompletedAckFuture {})))
    }

    async fn unsubscribe_with_properties(
        &self,
        topic: impl Into<String> + Send,
        properties: UnsubscribeProperties,
    ) -> Result<CompletionToken, ClientError> {
        Ok(CompletionToken(Box::new(CompletedAckFuture {})))
    }
}

#[async_trait]
impl MqttAck for MockClient {
    async fn ack(&self, publish: &Publish) -> Result<(), ClientError> {
        Ok(())
    }
}

#[async_trait]
impl MqttDisconnect for MockClient {
    async fn disconnect(&self) -> Result<(), ClientError> {
        Ok(())
    }
}

#[async_trait]
impl MqttClient for MockClient {
    async fn reauth(&self, auth_props: AuthProperties) -> Result<(), ClientError> {
        Ok(())
    }
}

/// Mock implementation of an MQTT event loop
pub struct MockEventLoop {
    rx: UnboundedReceiver<Event>,
}

impl MockEventLoop {
    /// Return a new mocked MQTT event loop along with an event injector.
    #[must_use]
    pub fn new() -> (Self, EventInjector) {
        let (tx, rx) = unbounded_channel();
        (Self { rx }, EventInjector { tx })
    }
}

#[async_trait]
impl MqttEventLoop for MockEventLoop {
    async fn poll(&mut self) -> Result<Event, ConnectionError> {
        match self.rx.recv().await {
            Some(e) => Ok(e),
            None => Err(ConnectionError::RequestsDone),
        }
    }

    fn set_clean_start(&mut self, _clean_start: bool) {}
}

/// Used to inject events into the [`MockEventLoop`].
#[derive(Clone)]
pub struct EventInjector {
    tx: UnboundedSender<Event>,
}

impl EventInjector {
    /// Inject an event into the [`MockEventLoop`].
    ///
    /// # Errors
    /// Returns a [`SendError`] if the event could not be injected
    /// (i.e. the event loop has been dropped).
    pub fn inject(&self, event: Event) -> Result<(), SendError<Event>> {
        self.tx.send(event)
    }
}
