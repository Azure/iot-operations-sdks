// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

use std::str::from_utf8;

use async_trait::async_trait;
use azure_iot_operations_mqtt::control_packet::Publish;
use azure_iot_operations_mqtt::error::AckError;
use azure_iot_operations_mqtt::interface::{MqttAck, PubReceiver};
use azure_iot_operations_mqtt::topic::{TopicFilter, TopicName};
use tokio::sync::{broadcast, mpsc};

use crate::metl::mqtt_operation::MqttOperation;

pub struct MqttListener {
    topic_filter: TopicFilter,
    auto_ack: bool,
    message_rx: broadcast::Receiver<Publish>,
    operation_tx: mpsc::UnboundedSender<MqttOperation>,
}

impl MqttListener {
    pub fn new(
        topic_filter: TopicFilter,
        auto_ack: bool,
        message_rx: broadcast::Receiver<Publish>,
        operation_tx: mpsc::UnboundedSender<MqttOperation>,
    ) -> Self {
        Self {
            topic_filter,
            auto_ack,
            message_rx,
            operation_tx,
        }
    }
}

#[async_trait]
impl MqttAck for MqttListener {
    async fn ack(&self, publish: &Publish) -> Result<(), AckError> {
        self.operation_tx
            .send(MqttOperation::Ack { pkid: publish.pkid })
            .unwrap();
        Ok(())
    }
}

#[async_trait]
impl PubReceiver for MqttListener {
    async fn recv(&mut self) -> Option<Publish> {
        loop {
            let message = self.message_rx.recv().await.unwrap();
            let topic_name = TopicName::from_string(
                from_utf8(message.topic.to_vec().as_slice())
                    .unwrap()
                    .to_string(),
            )
            .unwrap();
            if self.topic_filter.matches_topic_name(&topic_name) {
                if self.auto_ack {
                    self.ack(&message).await.unwrap();
                }
                return Some(message);
            }
        }
    }

    fn close(&mut self) {}
}
