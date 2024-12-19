// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

use async_trait::async_trait;
use azure_iot_operations_mqtt::control_packet::{
    AuthProperties, Publish, PublishProperties, QoS, SubscribeProperties, UnsubscribeProperties,
};
use azure_iot_operations_mqtt::error::{
    AckError, DisconnectError, PublishError, ReauthError, SubscribeError, UnsubscribeError,
};
use azure_iot_operations_mqtt::interface::{
    CompletionToken, MqttAck, MqttClient, MqttDisconnect, MqttPubSub,
};
//use azure_iot_operations_mqtt::interface::ManagedClient;
//use azure_iot_operations_mqtt::topic::{TopicFilter, TopicParseError};
use bytes::Bytes;
use futures::future::TryFutureExt;
use rumqttc::v5::mqttbytes::v5::{PubAckReason, SubscribeReasonCode, UnsubAckReason};
use rumqttc::NoticeError;
use tokio::sync::{broadcast, mpsc, oneshot};

//use crate::metl::mqtt_listener::MqttListener;
use crate::metl::mqtt_operation::MqttOperation;
use crate::metl::test_ack_kind::TestAckKind;

#[derive(Clone)]
pub struct MqttDriver {
    _client_id: String,
    _message_tx: Option<broadcast::Sender<Publish>>,
    operation_tx: mpsc::UnboundedSender<MqttOperation>,
}

impl MqttDriver {
    pub fn new(
        client_id: String,
        message_tx: Option<broadcast::Sender<Publish>>,
        operation_tx: mpsc::UnboundedSender<MqttOperation>,
    ) -> Self {
        Self {
            _client_id: client_id,
            _message_tx: message_tx,
            operation_tx,
        }
    }

    fn publish_with_optional_properties(
        &self,
        topic: impl Into<String> + Send,
        qos: QoS,
        retain: bool,
        payload: impl Into<Bytes> + Send,
        properties: Option<PublishProperties>,
    ) -> CompletionToken {
        let (ack_tx, ack_rx) = oneshot::channel();
        self.operation_tx
            .send(MqttOperation::Publish {
                topic: topic.into(),
                qos,
                retain,
                payload: payload.into(),
                properties,
                ack_tx,
            })
            .unwrap();

        CompletionToken(Box::new(ack_rx.map_ok_or_else(
            |_: oneshot::error::RecvError| Err(NoticeError::Recv),
            |x: TestAckKind| match x {
                TestAckKind::Success => Ok(()),
                TestAckKind::Fail => Err(NoticeError::V5PubAck(PubAckReason::UnspecifiedError)),
                TestAckKind::Drop => Err(NoticeError::SessionReset),
            },
        )))
    }

    fn subscribe_with_optional_properties(
        &self,
        topic: impl Into<String> + Send,
        qos: QoS,
        properties: Option<SubscribeProperties>,
    ) -> CompletionToken {
        let (ack_tx, ack_rx) = oneshot::channel();
        let _ = self.operation_tx.send(MqttOperation::Subscribe {
            topic: topic.into(),
            _qos: qos,
            _properties: properties,
            ack_tx,
        });

        CompletionToken(Box::new(ack_rx.map_ok_or_else(
            |_: oneshot::error::RecvError| Err(NoticeError::Recv),
            |x: TestAckKind| match x {
                TestAckKind::Success => Ok(()),
                TestAckKind::Fail => Err(NoticeError::V5Subscribe(SubscribeReasonCode::Failure)),
                TestAckKind::Drop => Err(NoticeError::SessionReset),
            },
        )))
    }

    fn unsubscribe_with_optional_properties(
        &self,
        topic: impl Into<String> + Send,
        properties: Option<UnsubscribeProperties>,
    ) -> CompletionToken {
        let (ack_tx, ack_rx) = oneshot::channel();
        self.operation_tx
            .send(MqttOperation::Unsubscribe {
                _topic: topic.into(),
                _properties: properties,
                ack_tx,
            })
            .unwrap();

        CompletionToken(Box::new(ack_rx.map_ok_or_else(
            |_: oneshot::error::RecvError| Err(NoticeError::Recv),
            |x: TestAckKind| match x {
                TestAckKind::Success => Ok(()),
                TestAckKind::Fail => {
                    Err(NoticeError::V5Unsubscribe(UnsubAckReason::UnspecifiedError))
                }
                TestAckKind::Drop => Err(NoticeError::SessionReset),
            },
        )))
    }
}

#[async_trait]
impl MqttPubSub for MqttDriver {
    #[allow(clippy::unused_async)]
    async fn publish(
        &self,
        topic: impl Into<String> + Send,
        qos: QoS,
        retain: bool,
        payload: impl Into<Bytes> + Send,
    ) -> Result<CompletionToken, PublishError> {
        Ok(self.publish_with_optional_properties(topic, qos, retain, payload, None))
    }

    #[allow(clippy::unused_async)]
    async fn publish_with_properties(
        &self,
        topic: impl Into<String> + Send,
        qos: QoS,
        retain: bool,
        payload: impl Into<Bytes> + Send,
        properties: PublishProperties,
    ) -> Result<CompletionToken, PublishError> {
        Ok(self.publish_with_optional_properties(topic, qos, retain, payload, Some(properties)))
    }

    #[allow(clippy::unused_async)]
    async fn subscribe(
        &self,
        topic: impl Into<String> + Send,
        qos: QoS,
    ) -> Result<CompletionToken, SubscribeError> {
        Ok(self.subscribe_with_optional_properties(topic, qos, None))
    }

    #[allow(clippy::unused_async)]
    async fn subscribe_with_properties(
        &self,
        topic: impl Into<String> + Send,
        qos: QoS,
        properties: SubscribeProperties,
    ) -> Result<CompletionToken, SubscribeError> {
        Ok(self.subscribe_with_optional_properties(topic, qos, Some(properties)))
    }

    #[allow(clippy::unused_async)]
    async fn unsubscribe(
        &self,
        topic: impl Into<String> + Send,
    ) -> Result<CompletionToken, UnsubscribeError> {
        Ok(self.unsubscribe_with_optional_properties(topic, None))
    }

    #[allow(clippy::unused_async)]
    async fn unsubscribe_with_properties(
        &self,
        topic: impl Into<String> + Send,
        properties: UnsubscribeProperties,
    ) -> Result<CompletionToken, UnsubscribeError> {
        Ok(self.unsubscribe_with_optional_properties(topic, Some(properties)))
    }
}

#[async_trait]
impl MqttAck for MqttDriver {
    async fn ack(&self, publish: &Publish) -> Result<(), AckError> {
        self.operation_tx
            .send(MqttOperation::Ack { pkid: publish.pkid })
            .unwrap();
        Ok(())
    }
}

#[async_trait]
impl MqttDisconnect for MqttDriver {
    async fn disconnect(&self) -> Result<(), DisconnectError> {
        let _ = self.operation_tx.send(MqttOperation::Disconnect);
        Ok(())
    }
}

#[async_trait]
impl MqttClient for MqttDriver {
    async fn reauth(&self, auth_props: AuthProperties) -> Result<(), ReauthError> {
        self.operation_tx
            .send(MqttOperation::Auth {
                _auth_props: auth_props,
            })
            .unwrap();
        Ok(())
    }
}

/*
impl ManagedClient for MqttDriver {
    type PubReceiver = MqttListener;

    fn client_id(&self) -> &str {
        &self.client_id
    }

    fn create_filtered_pub_receiver(
        &self,
        topic_filter: &str,
        auto_ack: bool,
    ) -> Result<MqttListener, TopicParseError> {
        let topic_filter = TopicFilter::from_string(topic_filter.to_string())?;
        Ok(MqttListener::new(
            topic_filter,
            auto_ack,
            self.message_tx
                .as_ref()
                .expect("create_filtered_pub_receiver() called but MQTT emulation is not at Message level")
                .subscribe(),
            self.operation_tx.clone(),
        ))
    }
}
*/
