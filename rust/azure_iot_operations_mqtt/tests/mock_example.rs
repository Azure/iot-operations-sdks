// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

use async_trait::async_trait;
use flume::{bounded, Receiver, Sender};
use rumqttc::v5::AsyncClient;
use tokio::select;

use azure_iot_operations_mqtt::control_packet::Publish;
use azure_iot_operations_mqtt::error::ConnectionError;
use azure_iot_operations_mqtt::interface::{MqttEventLoop, MqttProvider, MqttPubReceiver};
use azure_iot_operations_mqtt::session::{
    internal::Session, reconnect_policy::ExponentialBackoffWithJitter,
};
use azure_iot_operations_mqtt::{Event, Incoming};

struct MockEventLoop {
    rx: Receiver<Event>,
}

impl MockEventLoop {
    pub fn new(rx: Receiver<Event>) -> Self {
        Self { rx }
    }
}

#[async_trait]
impl MqttEventLoop for MockEventLoop {
    async fn poll(&mut self) -> Result<Event, ConnectionError> {
        match self.rx.recv_async().await {
            Ok(e) => Ok(e),
            Err(_) => Err(ConnectionError::RequestsDone),
        }
    }

    fn set_clean_start(&mut self, _clean_start: bool) {}
}

#[tokio::test]
async fn mock_event_loop() {
    const MAX_PENDING_MESSAGES: usize = 10;
    const CLIENT_ID: &str = "MyClientId";

    let (event_tx, event_rx) = bounded(MAX_PENDING_MESSAGES);
    let (requests_tx, _requests_rx) = bounded(MAX_PENDING_MESSAGES);
    let event_loop = MockEventLoop::new(event_rx);
    let injector = event_tx;

    // NOTE: you could also of course, make a mock for this client too - no need to use rumqttc here.
    // The problem right now is that it needs to return a CompletionToken, which currently cannot easily be
    // created due to the way it's implemented on top of a NoticeFuture from rumqttc.
    // Will get tooling to support this in ASAP.
    let client = AsyncClient::from_senders(requests_tx);

    let mut session = Session::new_from_injection(
        client,
        event_loop,
        Box::new(ExponentialBackoffWithJitter::default()),
        CLIENT_ID.to_string(),
        None,
        MAX_PENDING_MESSAGES,
    );

    let mut pub_receiver = session
        .filtered_pub_receiver("test/resp/topic", true)
        .unwrap();

    #[allow(clippy::items_after_statements)]
    async fn receive_publish(injector: Sender<Event>, pub_receiver: &mut impl MqttPubReceiver) {
        injector
            .send_async(Event::Incoming(Incoming::Publish(Publish {
                dup: false,
                qos: rumqttc::v5::mqttbytes::QoS::AtLeastOnce,
                retain: false,
                topic: "test/resp/topic".into(),
                pkid: 1,
                payload: vec![].into(),
                properties: None,
            })))
            .await
            .unwrap();

        let received_pub = pub_receiver.recv().await.unwrap();
        assert_eq!(received_pub.topic, "test/resp/topic");
    }

    select! {
        () = receive_publish(injector, &mut pub_receiver) => {}
        _ = session.run() => {}
    }
}
