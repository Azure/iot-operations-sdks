// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

use azure_iot_operations_mqtt::control_packet::{Publish, QoS};
use azure_iot_operations_mqtt::interface::{Event, Incoming, ManagedClient, PubReceiver};
use azure_iot_operations_mqtt::interface_mocks::{EventInjector, MockClient, MockEventLoop};
use azure_iot_operations_mqtt::session::{
    reconnect_policy::ExponentialBackoffWithJitter, session::Session,
};

#[tokio::test]
async fn mock_event_injection() {
    const CLIENT_ID: &str = "MyClientId";

    let (event_loop, injector) = MockEventLoop::new();
    let client = MockClient::new();

    let mut session = Session::new_from_injection(
        client,
        event_loop,
        Box::new(ExponentialBackoffWithJitter::default()),
        CLIENT_ID.to_string(),
        None,
    );

    let mut pub_receiver = session
        .create_managed_client()
        .create_filtered_pub_receiver("test/resp/topic", true)
        .unwrap();

    #[allow(clippy::items_after_statements)]
    async fn receive_publish(injector: EventInjector, pub_receiver: &mut impl PubReceiver) {
        injector
            .inject(Event::Incoming(Incoming::Publish(Publish {
                dup: false,
                qos: QoS::AtLeastOnce,
                retain: false,
                topic: "test/resp/topic".into(),
                pkid: 1,
                payload: vec![].into(),
                properties: None,
            })))
            .unwrap();

        let (received_pub, _) = pub_receiver.recv().await.unwrap();
        assert_eq!(received_pub.topic, "test/resp/topic");
    }

    tokio::select! {
        () = receive_publish(injector, &mut pub_receiver) => {}
        _ = session.run() => {}
    }
}
