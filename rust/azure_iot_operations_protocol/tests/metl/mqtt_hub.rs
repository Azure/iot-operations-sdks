// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

use std::collections::{hash_map::HashMap, hash_set::HashSet, VecDeque};

use azure_iot_operations_mqtt::control_packet::Publish;
use azure_iot_operations_mqtt::error::{ConnectionError, StateError};
use azure_iot_operations_mqtt::interface::{Event, Incoming};
use bytes::Bytes;
use rumqttc::v5::mqttbytes::v5::DisconnectReasonCode;
use tokio::sync::{broadcast, mpsc};

use crate::metl::mqtt_driver::MqttDriver;
use crate::metl::mqtt_emulation_level::MqttEmulationLevel;
use crate::metl::mqtt_looper::MqttLooper;
use crate::metl::mqtt_operation::MqttOperation;
use crate::metl::test_ack_kind::TestAckKind;

const MAX_PENDING_MESSAGES: usize = 10;

pub struct MqttHub {
    client_id: String,
    event_tx: Option<mpsc::UnboundedSender<Result<Event, ConnectionError>>>,
    event_rx: Option<mpsc::UnboundedReceiver<Result<Event, ConnectionError>>>,
    message_tx: Option<broadcast::Sender<Publish>>,
    operation_tx: mpsc::UnboundedSender<MqttOperation>,
    operation_rx: mpsc::UnboundedReceiver<MqttOperation>,
    packet_id_sequencer: u16,
    puback_queue: VecDeque<TestAckKind>,
    suback_queue: VecDeque<TestAckKind>,
    unsuback_queue: VecDeque<TestAckKind>,
    acked_packet_ids: VecDeque<u16>,
    publication_count: i32,
    acknowledgement_count: i32,
    published_correlation_data: VecDeque<Option<Bytes>>,
    subscribed_topics: HashSet<String>,
    published_messages: HashMap<Option<Bytes>, Publish>,
}

impl MqttHub {
    pub fn new(client_id: String, emulation_level: MqttEmulationLevel) -> Self {
        let (event_tx, event_rx) = match emulation_level {
            MqttEmulationLevel::Event => {
                let (event_tx, event_rx) = mpsc::unbounded_channel();
                (Some(event_tx), Some(event_rx))
            }
            MqttEmulationLevel::Message => (None, None),
        };
        let message_tx = match emulation_level {
            MqttEmulationLevel::Message => {
                let (message_tx, _) = broadcast::channel(MAX_PENDING_MESSAGES);
                Some(message_tx)
            }
            MqttEmulationLevel::Event => None,
        };
        let (operation_tx, operation_rx) = mpsc::unbounded_channel();
        Self {
            client_id,
            event_tx,
            event_rx,
            message_tx,
            operation_tx,
            operation_rx,
            packet_id_sequencer: 0,
            puback_queue: VecDeque::new(),
            suback_queue: VecDeque::new(),
            unsuback_queue: VecDeque::new(),
            acked_packet_ids: VecDeque::new(),
            publication_count: 0,
            acknowledgement_count: 0,
            published_correlation_data: VecDeque::new(),
            subscribed_topics: HashSet::new(),
            published_messages: HashMap::new(),
        }
    }

    pub fn get_looper(&mut self) -> MqttLooper {
        MqttLooper::new(self.event_rx.take())
    }

    pub fn get_driver(&self) -> MqttDriver {
        MqttDriver::new(
            self.client_id.clone(),
            self.message_tx.clone(),
            self.operation_tx.clone(),
        )
    }

    pub fn get_publication_count(&self) -> i32 {
        self.publication_count
    }

    pub fn get_acknowledgement_count(&self) -> i32 {
        self.acknowledgement_count
    }

    pub fn enqueue_puback(&mut self, ack_kind: TestAckKind) {
        self.puback_queue.push_back(ack_kind);
    }

    pub fn enqueue_suback(&mut self, ack_kind: TestAckKind) {
        self.suback_queue.push_back(ack_kind);
    }

    pub fn enqueue_unsuback(&mut self, ack_kind: TestAckKind) {
        self.unsuback_queue.push_back(ack_kind);
    }

    pub fn get_new_packet_id(&mut self) -> u16 {
        self.packet_id_sequencer += 1;
        self.packet_id_sequencer
    }

    pub async fn await_publish(&mut self) -> Option<Bytes> {
        loop {
            if let Some(correlation_data) = self.published_correlation_data.pop_front() {
                return correlation_data;
            }
            self.await_operation().await;
        }
    }

    pub async fn await_acknowledgement(&mut self) -> u16 {
        loop {
            if let Some(pkid) = self.acked_packet_ids.pop_front() {
                return pkid;
            }
            self.await_operation().await;
        }
    }

    pub fn has_subscribed(&self, topic: &str) -> bool {
        self.subscribed_topics.contains(topic)
    }

    pub fn get_published_message(&self, correlation_data: &Option<Bytes>) -> Option<&Publish> {
        self.published_messages.get(correlation_data)
    }

    pub fn receive_message(&mut self, message: Publish) {
        if let Some(message_tx) = self.message_tx.as_mut() {
            message_tx.send(message).unwrap();
        } else {
            self.receive_incoming_event(Incoming::Publish(message));
        }
    }

    pub fn disconnect(&mut self) {
        self.receive_error(ConnectionError::MqttState(StateError::ConnectionAborted));
    }

    pub async fn await_operation(&mut self) {
        if let Some(operation) = self.operation_rx.recv().await {
            match operation {
                MqttOperation::Publish {
                    topic,
                    qos,
                    retain,
                    payload,
                    properties,
                    ack_tx,
                } => {
                    self.publication_count += 1;

                    let correlation_data = if let Some(properties) = properties.clone() {
                        properties.correlation_data
                    } else {
                        None
                    };
                    self.published_correlation_data
                        .push_back(correlation_data.clone());
                    let publish = Publish {
                        dup: false,
                        qos,
                        retain,
                        topic: Bytes::copy_from_slice(&topic.into_bytes()),
                        pkid: 1,
                        payload,
                        properties,
                    };
                    self.published_messages.insert(correlation_data, publish);

                    if let Some(ack_kind) = self.puback_queue.pop_front() {
                        ack_tx.send(ack_kind).unwrap();
                    } else {
                        ack_tx.send(TestAckKind::Success).unwrap();
                    }
                }

                MqttOperation::Subscribe {
                    topic,
                    _qos: _,
                    _properties: _,
                    ack_tx,
                } => {
                    self.subscribed_topics.insert(topic.clone());
                    if let Some(ack_kind) = self.suback_queue.pop_front() {
                        ack_tx.send(ack_kind).unwrap();
                    } else {
                        ack_tx.send(TestAckKind::Success).unwrap();
                    }
                }

                MqttOperation::Unsubscribe {
                    _topic: _,
                    _properties: _,
                    ack_tx,
                } => {
                    if let Some(ack_kind) = self.unsuback_queue.pop_front() {
                        ack_tx.send(ack_kind).unwrap();
                    } else {
                        ack_tx.send(TestAckKind::Success).unwrap();
                    }
                }

                MqttOperation::Ack { pkid } => {
                    self.acknowledgement_count += 1;
                    self.acked_packet_ids.push_back(pkid);
                }

                MqttOperation::Disconnect => {
                    self.receive_error(ConnectionError::MqttState(StateError::ServerDisconnect {
                        reason_code: DisconnectReasonCode::NormalDisconnection,
                        reason_string: None,
                    }));
                }

                MqttOperation::Auth { _auth_props: _ } => {}
            }
        }
    }

    fn receive_incoming_event(&mut self, incoming_event: Incoming) {
        self.event_tx
            .as_mut()
            .expect("receive_incoming_event() called but MQTT emulation is not at Event level")
            .send(Ok(Event::Incoming(incoming_event)))
            .unwrap();
    }

    fn receive_error(&mut self, error: ConnectionError) {
        self.event_tx
            .as_mut()
            .expect("receive_error() called but MQTT emulation is not at Event level")
            .send(Err(error))
            .unwrap();
    }
}
