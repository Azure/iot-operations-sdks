// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

//! Bespoke mocks for relevant traits defined in the interface module.
#![allow(unused_variables)]
use std::sync::{Arc, Mutex};

use async_trait::async_trait;
use bytes::Bytes;
use tokio::sync::mpsc::{error::SendError, unbounded_channel, UnboundedReceiver, UnboundedSender};

use crate::control_packet::{
    AuthProperties, Publish, PublishProperties, QoS, SubscribeProperties, UnsubscribeProperties,
};
use crate::error::{
    AckError, CompletionError, ConnectionError, DisconnectError, PublishError, ReauthError,
    SubscribeError, UnsubscribeError,
};
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

#[derive(Clone)]
#[allow(missing_docs)]
pub enum MockClientCall {
    Publish(PublishCall),
    Subscribe(SubscribeCall),
    Unsubscribe(UnsubscribeCall),
    Ack(AckCall),
}

#[derive(Clone)]
#[allow(missing_docs)]
pub struct PublishCall {
    pub topic: String,
    pub qos: QoS,
    pub retain: bool,
    pub payload: Bytes,
    pub properties: Option<PublishProperties>,
}

#[derive(Clone)]
#[allow(missing_docs)]
pub struct SubscribeCall {
    pub topic: String,
    pub qos: QoS,
    pub properties: Option<SubscribeProperties>,
}

#[derive(Clone)]
#[allow(missing_docs)]
pub struct UnsubscribeCall {
    pub topic: String,
    pub properties: Option<UnsubscribeProperties>,
}

#[derive(Clone)]
#[allow(missing_docs)]
pub struct AckCall {
    pub publish: Publish,
}

/// Call data for [`MockClient`]
#[derive(Default)]
struct SharedCallTracker {
    call_sequence: Vec<MockClientCall>,
}

/// Tracks call information for a [`MockClient`] instance (including its clones).
pub struct MockClientController {
    shared_tracker: Arc<Mutex<SharedCallTracker>>,
}

impl MockClientController {
    /// Return the number of `.publish()` calls made to the client.
    #[must_use]
    #[allow(clippy::missing_panics_doc)]
    pub fn publish_count(&self) -> usize {
        self.shared_tracker
            .lock()
            .unwrap()
            .call_sequence
            .iter()
            .filter(|call| matches!(call, MockClientCall::Publish(_)))
            .count()
    }

    /// Return the number of `.subscribe()` calls made to the client.
    #[must_use]
    #[allow(clippy::missing_panics_doc)]
    pub fn subscribe_count(&self) -> usize {
        self.shared_tracker
            .lock()
            .unwrap()
            .call_sequence
            .iter()
            .filter(|call| matches!(call, MockClientCall::Subscribe(_)))
            .count()
    }

    /// Return the number of `.unsubscribe()` calls made to the client.
    #[must_use]
    #[allow(clippy::missing_panics_doc)]
    pub fn unsubscribe_count(&self) -> usize {
        self.shared_tracker
            .lock()
            .unwrap()
            .call_sequence
            .iter()
            .filter(|call| matches!(call, MockClientCall::Unsubscribe(_)))
            .count()
    }

    /// Return the number of `.ack()` calls made to the client.
    #[must_use]
    #[allow(clippy::missing_panics_doc)]
    pub fn ack_count(&self) -> usize {
        self.shared_tracker
            .lock()
            .unwrap()
            .call_sequence
            .iter()
            .filter(|call| matches!(call, MockClientCall::Ack(_)))
            .count()
    }

    /// Return a snapshot of the sequence of calls made to the mocked client so far
    #[must_use]
    #[allow(clippy::missing_panics_doc)]
    pub fn call_sequence(&self) -> Vec<MockClientCall> {
        self.shared_tracker.lock().unwrap().call_sequence.clone()
    }

    /// Reset the mock, clearing all prior call information and/or configuration
    #[allow(clippy::missing_panics_doc)]
    pub fn reset_mock(&self) {
        self.shared_tracker.lock().unwrap().call_sequence.clear();
    }

    // TODO: set return value(s) for calls, or perhaps full implementations of the call
}

// TODO: Will need to add a way to choose when acks return, and what rc they provide

/// Mock implementation of an MQTT client.
///
/// Currently always succeeds on all operations.
#[derive(Clone)]
pub struct MockClient {
    /// Shared state for calls made to this client and all its clones.
    shared_tracker: Arc<Mutex<SharedCallTracker>>,
}

impl MockClient {
    /// Return a new mocked MQTT client.
    #[must_use]
    #[allow(clippy::new_without_default)]
    pub fn new() -> Self {
        Self {
            shared_tracker: Arc::new(Mutex::new(SharedCallTracker::default())),
        }
    }

    /// Return a monitor that tracks the calls to this client (including any of its clones)
    #[must_use]
    pub fn mock_controller(&self) -> MockClientController {
        MockClientController {
            shared_tracker: self.shared_tracker.clone(),
        }
    }
}

// TODO: Need to flesh out the mock more
// - ability to change calls to fail / inject failure
// - ability to throttle outgoing events by capacity (e.g. queueing)
// - full functionality for disconnect and reauth

#[async_trait]
impl MqttPubSub for MockClient {
    async fn publish(
        &self,
        topic: impl Into<String> + Send,
        qos: QoS,
        retain: bool,
        payload: impl Into<Bytes> + Send,
    ) -> Result<CompletionToken, PublishError> {
        let call = PublishCall {
            topic: topic.into(),
            qos,
            retain,
            payload: payload.into(),
            properties: None,
        };
        self.shared_tracker
            .lock()
            .unwrap()
            .call_sequence
            .push(MockClientCall::Publish(call));
        Ok(CompletionToken(Box::new(CompletedAckFuture {})))
    }

    async fn publish_with_properties(
        &self,
        topic: impl Into<String> + Send,
        qos: QoS,
        retain: bool,
        payload: impl Into<Bytes> + Send,
        properties: PublishProperties,
    ) -> Result<CompletionToken, PublishError> {
        let call = PublishCall {
            topic: topic.into(),
            qos,
            retain,
            payload: payload.into(),
            properties: Some(properties),
        };
        self.shared_tracker
            .lock()
            .unwrap()
            .call_sequence
            .push(MockClientCall::Publish(call));
        Ok(CompletionToken(Box::new(CompletedAckFuture {})))
    }

    async fn subscribe(
        &self,
        topic: impl Into<String> + Send,
        qos: QoS,
    ) -> Result<CompletionToken, SubscribeError> {
        let call = SubscribeCall {
            topic: topic.into(),
            qos,
            properties: None,
        };
        self.shared_tracker
            .lock()
            .unwrap()
            .call_sequence
            .push(MockClientCall::Subscribe(call));
        Ok(CompletionToken(Box::new(CompletedAckFuture {})))
    }

    async fn subscribe_with_properties(
        &self,
        topic: impl Into<String> + Send,
        qos: QoS,
        properties: SubscribeProperties,
    ) -> Result<CompletionToken, SubscribeError> {
        let call = SubscribeCall {
            topic: topic.into(),
            qos,
            properties: Some(properties),
        };
        self.shared_tracker
            .lock()
            .unwrap()
            .call_sequence
            .push(MockClientCall::Subscribe(call));
        Ok(CompletionToken(Box::new(CompletedAckFuture {})))
    }

    async fn unsubscribe(
        &self,
        topic: impl Into<String> + Send,
    ) -> Result<CompletionToken, UnsubscribeError> {
        let call = UnsubscribeCall {
            topic: topic.into(),
            properties: None,
        };
        self.shared_tracker
            .lock()
            .unwrap()
            .call_sequence
            .push(MockClientCall::Unsubscribe(call));
        Ok(CompletionToken(Box::new(CompletedAckFuture {})))
    }

    async fn unsubscribe_with_properties(
        &self,
        topic: impl Into<String> + Send,
        properties: UnsubscribeProperties,
    ) -> Result<CompletionToken, UnsubscribeError> {
        let call = UnsubscribeCall {
            topic: topic.into(),
            properties: Some(properties),
        };
        self.shared_tracker
            .lock()
            .unwrap()
            .call_sequence
            .push(MockClientCall::Unsubscribe(call));
        Ok(CompletionToken(Box::new(CompletedAckFuture {})))
    }
}

#[async_trait]
impl MqttAck for MockClient {
    async fn ack(&self, publish: &Publish) -> Result<(), AckError> {
        let call = AckCall {
            publish: publish.clone(),
        };
        self.shared_tracker
            .lock()
            .unwrap()
            .call_sequence
            .push(MockClientCall::Ack(call));
        Ok(())
    }
}

#[async_trait]
impl MqttDisconnect for MockClient {
    async fn disconnect(&self) -> Result<(), DisconnectError> {
        Ok(())
    }
}

#[async_trait]
impl MqttClient for MockClient {
    async fn reauth(&self, auth_props: AuthProperties) -> Result<(), ReauthError> {
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

    fn set_authentication_method(&mut self, authentication_method: Option<String>) {}

    fn set_authentication_data(&mut self, authentication_data: Option<Bytes>) {}
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

#[cfg(test)]
mod test {
    use super::*;
    use tokio::sync::Notify;

    #[tokio::test]
    async fn mock_client_call_tracking_single_client() {
        let client = MockClient::new();
        let controller = client.mock_controller();

        let publish1_topic = "test/publish/topic/1";
        let publish1_payload = "payload 1";
        let publish2_topic = "test/publish/topic/2";
        let publish2_payload = "payload 2";
        let publish2_properties = PublishProperties::default();
        let subscribe1_topic = "test/subscribe/topic/1";
        let subscribe2_topic = "test/subscribe/topic/2";
        let subscribe2_properties = SubscribeProperties {
            id: None,
            user_properties: vec![("sub2key".to_string(), "sub2value".to_string())],
        };
        let unsubscribe1_topic = "test/unsubscribe/topic/1";
        let unsubscribe2_topic = "test/unsubscribe/topic/2";
        let unsubscribe2_properties = UnsubscribeProperties {
            user_properties: vec![("unsub2key".to_string(), "unsub2value".to_string())],
        };
        let ack_publish = Publish::new(
            "recv/msg/topic",
            QoS::AtLeastOnce,
            Bytes::from("msg_payload"),
            None,
        );

        // Make the calls to the mock
        client
            .publish(publish1_topic, QoS::AtLeastOnce, false, publish1_payload)
            .await
            .unwrap();
        client
            .subscribe(subscribe1_topic, QoS::AtLeastOnce)
            .await
            .unwrap();
        client.unsubscribe(unsubscribe1_topic).await.unwrap();
        client
            .publish_with_properties(
                publish2_topic,
                QoS::AtLeastOnce,
                false,
                publish2_payload,
                publish2_properties.clone(),
            )
            .await
            .unwrap();
        client
            .subscribe_with_properties(
                subscribe2_topic,
                QoS::AtLeastOnce,
                subscribe2_properties.clone(),
            )
            .await
            .unwrap();
        client
            .unsubscribe_with_properties(unsubscribe2_topic, unsubscribe2_properties.clone())
            .await
            .unwrap();
        client.ack(&ack_publish).await.unwrap();

        // Assert call sequence is as expected
        let calls = controller.call_sequence();
        assert_eq!(calls.len(), 7);

        match &calls[0] {
            MockClientCall::Publish(call) => {
                assert_eq!(call.topic, publish1_topic);
                assert_eq!(call.qos, QoS::AtLeastOnce);
                assert!(!call.retain);
                assert_eq!(call.payload, Bytes::from(publish1_payload));
                assert_eq!(call.properties, None);
            }
            _ => panic!("Expected Publish call"),
        }

        match &calls[1] {
            MockClientCall::Subscribe(call) => {
                assert_eq!(call.topic, subscribe1_topic);
                assert_eq!(call.qos, QoS::AtLeastOnce);
                assert_eq!(call.properties, None);
            }
            _ => panic!("Expected Subscribe call"),
        }

        match &calls[2] {
            MockClientCall::Unsubscribe(call) => {
                assert_eq!(call.topic, unsubscribe1_topic);
                assert_eq!(call.properties, None);
            }
            _ => panic!("Expected Unsubscribe call"),
        }

        match &calls[3] {
            MockClientCall::Publish(call) => {
                assert_eq!(call.topic, publish2_topic);
                assert_eq!(call.qos, QoS::AtLeastOnce);
                assert!(!call.retain);
                assert_eq!(call.payload, Bytes::from(publish2_payload));
                assert_eq!(call.properties, Some(publish2_properties));
            }
            _ => panic!("Expected Publish call"),
        }

        match &calls[4] {
            MockClientCall::Subscribe(call) => {
                assert_eq!(call.topic, subscribe2_topic);
                assert_eq!(call.qos, QoS::AtLeastOnce);
                assert_eq!(call.properties, Some(subscribe2_properties));
            }
            _ => panic!("Expected Subscribe call"),
        }

        match &calls[5] {
            MockClientCall::Unsubscribe(call) => {
                assert_eq!(call.topic, unsubscribe2_topic);
                assert_eq!(call.properties, Some(unsubscribe2_properties));
            }
            _ => panic!("Expected Unsubscribe call"),
        }

        match &calls[6] {
            MockClientCall::Ack(call) => {
                assert_eq!(call.publish, ack_publish);
            }
            _ => panic!("Expected Ack call"),
        }

        // Assert call counts are as expected
        assert_eq!(controller.publish_count(), 2);
        assert_eq!(controller.subscribe_count(), 2);
        assert_eq!(controller.unsubscribe_count(), 2);
        assert_eq!(controller.ack_count(), 1);

        // Reset the mock
        controller.reset_mock();

        // Call counts and sequence should be reset
        assert_eq!(controller.call_sequence().len(), 0);
        assert_eq!(controller.publish_count(), 0);
        assert_eq!(controller.subscribe_count(), 0);
        assert_eq!(controller.unsubscribe_count(), 0);
        assert_eq!(controller.ack_count(), 0);
    }

    #[tokio::test]
    async fn mock_client_call_tracking_multiple_clients() {
        let client1 = MockClient::new();
        let client2 = client1.clone();
        let controller = client1.mock_controller();

        let publish1_topic = "test/publish/topic/1";
        let publish1_payload = "payload 1";
        let publish2_topic = "test/publish/topic/2";
        let publish2_payload = "payload 2";
        let publish2_properties = PublishProperties::default();
        let subscribe1_topic = "test/subscribe/topic/1";
        let subscribe2_topic = "test/subscribe/topic/2";
        let subscribe2_properties = SubscribeProperties {
            id: None,
            user_properties: vec![("sub2key".to_string(), "sub2value".to_string())],
        };
        let unsubscribe1_topic = "test/unsubscribe/topic/1";
        let unsubscribe2_topic = "test/unsubscribe/topic/2";
        let unsubscribe2_properties = UnsubscribeProperties {
            user_properties: vec![("unsub2key".to_string(), "unsub2value".to_string())],
        };
        let ack_publish1 = Publish::new(
            "recv/msg/topic",
            QoS::AtLeastOnce,
            Bytes::from("msg_payload"),
            None,
        );
        let ack_publish2 = Publish::new(
            "recv/msg/topic",
            QoS::AtLeastOnce,
            Bytes::from("msg_payload2"),
            None,
        );

        // Use notifies to ensure correct interlacing of client calls across tasks for reproducible test
        let c1_proceed = Arc::new(Notify::new());
        let c2_approve = c1_proceed.clone();
        let c2_proceed = Arc::new(Notify::new());
        let c1_approve = c2_proceed.clone();

        // Client 1 calls
        let c1_work_jh = tokio::task::spawn({
            let ack_publish1 = ack_publish1.clone();
            async move {
                client1
                    .publish(publish1_topic, QoS::AtLeastOnce, false, publish1_payload)
                    .await
                    .unwrap();
                c1_approve.notify_one();
                c1_proceed.notified().await;
                client1
                    .subscribe(subscribe1_topic, QoS::AtLeastOnce)
                    .await
                    .unwrap();
                c1_approve.notify_one();
                c1_proceed.notified().await;
                client1.unsubscribe(unsubscribe1_topic).await.unwrap();
                c1_approve.notify_one();
                c1_proceed.notified().await;
                client1.ack(&ack_publish1).await.unwrap();
                c1_approve.notify_one();
            }
        });

        // Client 2 calls
        let c2_work_jh = tokio::task::spawn({
            let ack_publish2 = ack_publish2.clone();
            let publish2_properties = publish2_properties.clone();
            let subscribe2_properties = subscribe2_properties.clone();
            let unsubscribe2_properties = unsubscribe2_properties.clone();
            async move {
                c2_proceed.notified().await;
                client2
                    .publish_with_properties(
                        publish2_topic,
                        QoS::AtLeastOnce,
                        false,
                        publish2_payload,
                        publish2_properties.clone(),
                    )
                    .await
                    .unwrap();
                c2_approve.notify_one();
                c2_proceed.notified().await;
                client2
                    .subscribe_with_properties(
                        subscribe2_topic,
                        QoS::AtLeastOnce,
                        subscribe2_properties.clone(),
                    )
                    .await
                    .unwrap();
                c2_approve.notify_one();
                c2_proceed.notified().await;
                client2
                    .unsubscribe_with_properties(
                        unsubscribe2_topic,
                        unsubscribe2_properties.clone(),
                    )
                    .await
                    .unwrap();
                c2_approve.notify_one();
                c2_proceed.notified().await;
                client2.ack(&ack_publish2).await.unwrap();
            }
        });

        // Wait for tasks to complete
        c1_work_jh.await.unwrap();
        c2_work_jh.await.unwrap();

        // Assert call sequence is as expected
        let calls = controller.call_sequence();
        assert_eq!(calls.len(), 8);

        match &calls[0] {
            MockClientCall::Publish(call) => {
                assert_eq!(call.topic, publish1_topic);
                assert_eq!(call.qos, QoS::AtLeastOnce);
                assert!(!call.retain);
                assert_eq!(call.payload, Bytes::from(publish1_payload));
                assert_eq!(call.properties, None);
            }
            _ => panic!("Expected Publish call"),
        }

        match &calls[1] {
            MockClientCall::Publish(call) => {
                assert_eq!(call.topic, publish2_topic);
                assert_eq!(call.qos, QoS::AtLeastOnce);
                assert!(!call.retain);
                assert_eq!(call.payload, Bytes::from(publish2_payload));
                assert_eq!(call.properties, Some(publish2_properties));
            }
            _ => panic!("Expected Publish call"),
        }

        match &calls[2] {
            MockClientCall::Subscribe(call) => {
                assert_eq!(call.topic, subscribe1_topic);
                assert_eq!(call.qos, QoS::AtLeastOnce);
                assert_eq!(call.properties, None);
            }
            _ => panic!("Expected Subscribe call"),
        }

        match &calls[3] {
            MockClientCall::Subscribe(call) => {
                assert_eq!(call.topic, subscribe2_topic);
                assert_eq!(call.qos, QoS::AtLeastOnce);
                assert_eq!(call.properties, Some(subscribe2_properties));
            }
            _ => panic!("Expected Subscribe call"),
        }

        match &calls[4] {
            MockClientCall::Unsubscribe(call) => {
                assert_eq!(call.topic, unsubscribe1_topic);
                assert_eq!(call.properties, None);
            }
            _ => panic!("Expected Unsubscribe call"),
        }

        match &calls[5] {
            MockClientCall::Unsubscribe(call) => {
                assert_eq!(call.topic, unsubscribe2_topic);
                assert_eq!(call.properties, Some(unsubscribe2_properties));
            }
            _ => panic!("Expected Unsubscribe call"),
        }

        match &calls[6] {
            MockClientCall::Ack(call) => {
                assert_eq!(call.publish, ack_publish1);
            }
            _ => panic!("Expected Ack call"),
        }

        match &calls[7] {
            MockClientCall::Ack(call) => {
                assert_eq!(call.publish, ack_publish2);
            }
            _ => panic!("Expected Ack call"),
        }

        // Assert call counts are as expected
        assert_eq!(controller.publish_count(), 2);
        assert_eq!(controller.subscribe_count(), 2);
        assert_eq!(controller.unsubscribe_count(), 2);
        assert_eq!(controller.ack_count(), 2);

        // Reset the mock
        controller.reset_mock();

        // Call counts and sequence should be reset
        assert_eq!(controller.call_sequence().len(), 0);
        assert_eq!(controller.publish_count(), 0);
        assert_eq!(controller.subscribe_count(), 0);
        assert_eq!(controller.unsubscribe_count(), 0);
        assert_eq!(controller.ack_count(), 0);
    }
}
