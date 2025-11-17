// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

use std::collections::{VecDeque, hash_map::HashMap, hash_set::HashSet};

use azure_iot_operations_mqtt::test_utils::{IncomingPacketsTx, OutgoingPacketsRx};
use bytes::Bytes;
use tokio::sync::broadcast;

use crate::metl::mqtt_emulation_level::MqttEmulationLevel;
use crate::metl::test_ack_kind::TestAckKind;

const MAX_PENDING_MESSAGES: usize = 10;

pub struct MqttHub {
    event_tx: Option<IncomingPacketsTx>,
    message_tx: Option<broadcast::Sender<azure_mqtt::mqtt_proto::Publish<Bytes>>>,
    operation_rx: OutgoingPacketsRx,
    packet_id_sequencer: u16,
    puback_queue: VecDeque<TestAckKind>,
    suback_queue: VecDeque<TestAckKind>,
    unsuback_queue: VecDeque<TestAckKind>,
    acked_packet_ids: VecDeque<u16>,
    publication_count: i32,
    acknowledgement_count: i32,
    published_correlation_data: VecDeque<Option<azure_mqtt::mqtt_proto::BinaryData<Bytes>>>,
    subscribed_topics: HashSet<String>,
    published_messages: HashMap<
        Option<azure_mqtt::mqtt_proto::BinaryData<Bytes>>,
        azure_mqtt::mqtt_proto::Publish<Bytes>,
    >,
    published_message_seq: HashMap<i32, azure_mqtt::mqtt_proto::Publish<Bytes>>,
}

impl MqttHub {
    pub fn new(
        emulation_level: MqttEmulationLevel,
        incoming_packets_tx: IncomingPacketsTx,
        outgoing_packets_rx: OutgoingPacketsRx,
    ) -> Self {
        let event_tx = match emulation_level {
            MqttEmulationLevel::Event => Some(incoming_packets_tx),
            MqttEmulationLevel::Message => None,
        };
        let message_tx = match emulation_level {
            MqttEmulationLevel::Message => {
                let (message_tx, _) = broadcast::channel(MAX_PENDING_MESSAGES);
                Some(message_tx)
            }
            MqttEmulationLevel::Event => None,
        };
        Self {
            event_tx,
            message_tx,
            operation_rx: outgoing_packets_rx,
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
            published_message_seq: HashMap::new(),
        }
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

    pub async fn await_publish(&mut self) -> Option<azure_mqtt::mqtt_proto::BinaryData<Bytes>> {
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

    #[allow(clippy::ref_option)] // TODO: refactor to Option<&Bytes>
    pub fn get_published_message(
        &self,
        correlation_data: &Option<azure_mqtt::mqtt_proto::BinaryData<Bytes>>,
    ) -> Option<&azure_mqtt::mqtt_proto::Publish<Bytes>> {
        self.published_messages.get(correlation_data)
    }

    pub fn get_sequentially_published_message(
        &self,
        sequence_index: i32,
    ) -> Option<&azure_mqtt::mqtt_proto::Publish<Bytes>> {
        self.published_message_seq.get(&sequence_index)
    }

    pub fn receive_message(&mut self, message: azure_mqtt::mqtt_proto::Publish<Bytes>) {
        match self.message_tx.as_mut() {
            Some(message_tx) => {
                message_tx.send(message).unwrap();
            }
            _ => {
                self.receive_incoming_event(azure_mqtt::mqtt_proto::Packet::Publish(message));
            }
        }
    }

    pub fn disconnect(&mut self) {
        self.receive_incoming_event(azure_mqtt::mqtt_proto::Packet::Disconnect(
            azure_mqtt::mqtt_proto::Disconnect {
                reason_code: azure_mqtt::mqtt_proto::DisconnectReasonCode::Normal,
                other_properties: azure_mqtt::mqtt_proto::DisconnectOtherProperties::default(),
            },
        ));
    }

    pub async fn await_operation(&mut self) {
        if let Some(packet) = self.operation_rx.recv().await {
            match packet {
                azure_mqtt::mqtt_proto::Packet::Publish(publish) => {
                    self.publication_count += 1;

                    let correlation_data = publish.other_properties.correlation_data.clone();
                    self.published_correlation_data
                        .push_back(correlation_data.clone());
                    self.published_messages
                        .insert(correlation_data, publish.clone());
                    self.published_message_seq
                        .insert(self.publication_count - 1, publish.clone());

                    match publish.packet_identifier_dup_qos {
                        azure_mqtt::mqtt_proto::PacketIdentifierDupQoS::AtLeastOnce(pkid, _) => {
                            let reason_code = match self.puback_queue.pop_front() {
                                Some(TestAckKind::Success) | None => {
                                    azure_mqtt::mqtt_proto::PubAckReasonCode::Success
                                }
                                Some(TestAckKind::Fail) => {
                                    azure_mqtt::mqtt_proto::PubAckReasonCode::UnspecifiedError
                                }
                                Some(TestAckKind::Drop) => {
                                    // emulate not getting the puback from the broker
                                    return;
                                }
                            };
                            let puback = azure_mqtt::mqtt_proto::Packet::PubAck(
                                azure_mqtt::mqtt_proto::PubAck {
                                    packet_identifier: pkid,
                                    reason_code,
                                    other_properties:
                                        azure_mqtt::mqtt_proto::PubAckOtherProperties::default(),
                                },
                            );
                            self.receive_incoming_event(puback);
                        }
                        // ignore QoS 2 because we never use QoS 2, and QoS 0 doesn't require an ack
                        azure_mqtt::mqtt_proto::PacketIdentifierDupQoS::AtMostOnce
                        | azure_mqtt::mqtt_proto::PacketIdentifierDupQoS::ExactlyOnce(_, _) => {}
                    }
                }
                azure_mqtt::mqtt_proto::Packet::Subscribe(subscribe) => {
                    self.subscribed_topics
                        .insert(subscribe.subscribe_to[0].topic_filter.as_str().to_string());
                    let reason_code = match self.suback_queue.pop_front() {
                        Some(TestAckKind::Success) | None => {
                            azure_mqtt::mqtt_proto::SubscribeReasonCode::GrantedQoS1
                        } // TODO: this should be fine since we always sub with qos 1, but could determine it from the sub packet
                        Some(TestAckKind::Fail) => {
                            azure_mqtt::mqtt_proto::SubscribeReasonCode::UnspecifiedError
                        }
                        Some(TestAckKind::Drop) => {
                            // emulate not getting the suback from the broker
                            return;
                        }
                    };
                    let suback =
                        azure_mqtt::mqtt_proto::Packet::SubAck(azure_mqtt::mqtt_proto::SubAck {
                            packet_identifier: subscribe.packet_identifier,
                            reason_codes: vec![reason_code],
                            other_properties:
                                azure_mqtt::mqtt_proto::SubAckOtherProperties::default(),
                        });
                    self.receive_incoming_event(suback);
                }
                azure_mqtt::mqtt_proto::Packet::Unsubscribe(unsubscribe) => {
                    let reason_code = match self.unsuback_queue.pop_front() {
                        Some(TestAckKind::Success) | None => {
                            azure_mqtt::mqtt_proto::UnsubAckReasonCode::Success
                        }
                        Some(TestAckKind::Fail) => {
                            azure_mqtt::mqtt_proto::UnsubAckReasonCode::UnspecifiedError
                        }
                        Some(TestAckKind::Drop) => {
                            // emulate not getting the unsuback from the broker
                            return;
                        }
                    };
                    let unsuback = azure_mqtt::mqtt_proto::Packet::UnsubAck(
                        azure_mqtt::mqtt_proto::UnsubAck {
                            packet_identifier: unsubscribe.packet_identifier,
                            reason_codes: vec![reason_code],
                            other_properties:
                                azure_mqtt::mqtt_proto::UnsubAckOtherProperties::default(),
                        },
                    );
                    self.receive_incoming_event(unsuback);
                }
                azure_mqtt::mqtt_proto::Packet::PubAck(puback) => {
                    self.acknowledgement_count += 1;
                    self.acked_packet_ids
                        .push_back(puback.packet_identifier.get());
                }
                azure_mqtt::mqtt_proto::Packet::Connect(connect) => {
                    let connack =
                        azure_mqtt::mqtt_proto::Packet::ConnAck(azure_mqtt::mqtt_proto::ConnAck {
                            reason_code: azure_mqtt::mqtt_proto::ConnectReasonCode::Success {
                                session_present: !connect.clean_start,
                            },
                            other_properties:
                                azure_mqtt::mqtt_proto::ConnAckOtherProperties::default(),
                        });
                    self.receive_incoming_event(connack);
                }
                _ => {}
            }
        }
    }

    fn receive_incoming_event(&mut self, incoming_packet: azure_mqtt::mqtt_proto::Packet<Bytes>) {
        self.event_tx
            .as_mut()
            .expect("receive_incoming_event() called but MQTT emulation is not at Event level")
            .send(incoming_packet);
    }
}

/// Converts the test framework format indicator to what `mqtt_proto` expects for `payload_is_utf8`
pub fn to_is_utf8(format_indicator: Option<&u8>) -> bool {
    matches!(format_indicator, Some(1))
}
