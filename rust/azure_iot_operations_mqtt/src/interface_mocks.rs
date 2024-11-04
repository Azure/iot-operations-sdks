// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

//! Bespoke mocks for relevant traits defined in the interface module.
#![allow(unused_variables)]
#![allow(dead_code)]
#![allow(unused_imports)]
#![allow(missing_docs)]

use std::sync::Mutex;

use async_trait::async_trait;
use bytes::Bytes;

use crate::interface::{CompletionToken, MqttAck, MqttClient, MqttDisconnect, MqttPubSub};
use crate::control_packet::{AuthProperties, Publish, PublishProperties, QoS, SubscribeProperties, UnsubscribeProperties};
use crate::error::{ClientError, CompletionError};


pub struct MockMqttClient {
    publishes: Mutex<Vec<Publish>>,
    //subscribes: Mutex<Vec<

    // TODO: what about ordering though?
    // We may need to know that a subscribe happens before any publishes, etc.
}

impl MockMqttClient {
    pub fn new() -> Self {
        Self {
            publishes: Mutex::new(Vec::new()),
            //subscribes: Mutex::new(Vec::new()),
        }
    }
}

// expect calls like in mockall do not work very well here because of the asynchronous code.
// We don't want some background thread to panic - the tests wouldn't know.
// Instead, I suspect we need to have checkable stuff after the fact.

#[async_trait]
impl MqttPubSub for MockMqttClient {
    async fn publish(
        &self,
        topic: impl Into<String> + Send,
        qos: QoS,
        retain: bool,
        payload: impl Into<Bytes> + Send,
    ) -> Result<CompletionToken, ClientError> {
        let mut publish = Publish::new(topic, qos, payload, None);
        publish.retain = retain;
        self.publishes.lock().unwrap().push(publish);
        Ok(CompletionToken(Box::new(DummyAckFuture {})))
    }

    async fn publish_with_properties(
        &self,
        topic: impl Into<String> + Send,
        qos: QoS,
        retain: bool,
        payload: impl Into<Bytes> + Send,
        properties: PublishProperties,
    ) -> Result<CompletionToken, ClientError> {
        Ok(CompletionToken(Box::new(DummyAckFuture {})))
    }

    async fn subscribe(
        &self,
        topic: impl Into<String> + Send,
        qos: QoS,
    ) -> Result<CompletionToken, ClientError> {
        Ok(CompletionToken(Box::new(DummyAckFuture {})))
    }

    async fn subscribe_with_properties(
        &self,
        topic: impl Into<String> + Send,
        qos: QoS,
        properties: SubscribeProperties,
    ) -> Result<CompletionToken, ClientError> {
        Ok(CompletionToken(Box::new(DummyAckFuture {})))
    }

    async fn unsubscribe(
        &self,
        topic: impl Into<String> + Send,
    ) -> Result<CompletionToken, ClientError> {
        Ok(CompletionToken(Box::new(DummyAckFuture {})))
    }

    async fn unsubscribe_with_properties(
        &self,
        topic: impl Into<String> + Send,
        properties: UnsubscribeProperties,
    ) -> Result<CompletionToken, ClientError> {
        Ok(CompletionToken(Box::new(DummyAckFuture {})))
    }
}

#[async_trait]
impl MqttAck for MockMqttClient {
    async fn ack(&self, publish: &Publish) -> Result<(), ClientError> {
        unimplemented!()
    }
}

#[async_trait]
impl MqttDisconnect for MockMqttClient {
    async fn disconnect(&self) -> Result<(), ClientError> {
        unimplemented!()
    }
}

#[async_trait]
impl MqttClient for MockMqttClient {
    async fn reauth(&self, auth_props: AuthProperties) -> Result<(), ClientError> {
        unimplemented!()
    }
}


/// Stand-in for the inner future of a CompletionToken.
/// Always returns Ok, indicating the ack was completed.
struct DummyAckFuture {}

impl std::future::Future for DummyAckFuture {
    type Output = Result<(), CompletionError>;

    fn poll(self: std::pin::Pin<&mut Self>, cx: &mut std::task::Context<'_>) -> std::task::Poll<Self::Output> {
        std::task::Poll::Ready(Ok(()))
    }
}