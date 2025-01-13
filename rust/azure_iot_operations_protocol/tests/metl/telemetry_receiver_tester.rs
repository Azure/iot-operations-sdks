// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

use std::collections::HashMap;
use std::convert::TryFrom;
use std::marker::PhantomData;
use std::sync::{Arc, Mutex};

use async_std::future;
use azure_iot_operations_mqtt::control_packet::{Publish, PublishProperties};
use azure_iot_operations_mqtt::interface::ManagedClient;
use azure_iot_operations_protocol::common::aio_protocol_error::{
    AIOProtocolError, AIOProtocolErrorKind,
};
use azure_iot_operations_protocol::common::payload_serialize::PayloadSerialize;
use azure_iot_operations_protocol::telemetry::telemetry_receiver::{
    TelemetryReceiver, TelemetryReceiverOptionsBuilder, TelemetryReceiverOptionsBuilderError,
};
use bytes::Bytes;
use tokio::sync::mpsc;
use tokio::time;
use uuid::Uuid;

use crate::metl::aio_protocol_error_checker;
use crate::metl::defaults::ReceiverDefaults;
use crate::metl::mqtt_hub::MqttHub;
use crate::metl::qos;
use crate::metl::test_case::TestCase;
use crate::metl::test_case_action::TestCaseAction;
use crate::metl::test_case_catch::TestCaseCatch;
use crate::metl::test_case_cloud_event::TestCaseCloudEvent;
use crate::metl::test_case_received_telemetry::TestCaseReceivedTelemetry;
use crate::metl::test_case_receiver::TestCaseReceiver;
use crate::metl::test_payload::TestPayload;

const TEST_TIMEOUT: time::Duration = time::Duration::from_secs(10);

struct ReceivedTelemetry {
    telemetry_value: Option<String>,
    metadata: HashMap<String, String>,
    cloud_event: Option<TestCaseCloudEvent>,
    source_id: Option<String>,
}

pub struct TelemetryReceiverTester<C>
where
    C: ManagedClient + Clone + Send + Sync + 'static,
    C::PubReceiver: Send + Sync + 'static,
{
    managed_client: PhantomData<C>,
}

impl<C> TelemetryReceiverTester<C>
where
    C: ManagedClient + Clone + Send + Sync + 'static,
    C::PubReceiver: Send + Sync + 'static,
{
    pub async fn test_telemetry_receiver(
        test_case: TestCase<ReceiverDefaults>,
        test_case_index: i32,
        managed_client: C,
        mut mqtt_hub: MqttHub,
    ) {
        if let Some(push_acks) = test_case.prologue.push_acks.as_ref() {
            for ack_kind in &push_acks.publish {
                mqtt_hub.enqueue_puback(ack_kind.clone());
            }

            for ack_kind in &push_acks.subscribe {
                mqtt_hub.enqueue_suback(ack_kind.clone());
            }

            for ack_kind in &push_acks.unsubscribe {
                mqtt_hub.enqueue_unsuback(ack_kind.clone());
            }
        }

        let receiver_count = test_case.prologue.receivers.len();
        let mut telemetry_counts = Vec::with_capacity(receiver_count);
        let (telemetry_tx, mut telemetry_rx) = mpsc::unbounded_channel();

        let mut ix = 0;
        for test_case_receiver in &test_case.prologue.receivers {
            ix += 1;
            let catch = if ix == receiver_count {
                test_case.prologue.catch.as_ref()
            } else {
                None
            };

            let telemetry_count = Arc::new(Mutex::new(0));
            telemetry_counts.push(telemetry_count.clone());

            if let Some(receiver) = Self::get_telemetry_receiver(
                managed_client.clone(),
                test_case_receiver,
                catch,
                &mut mqtt_hub,
            )
            .await
            {
                if !test_case.actions.is_empty() {
                    tokio::task::spawn(Self::receiver_loop(
                        receiver,
                        telemetry_count,
                        telemetry_tx.clone(),
                    ));
                }
            }
        }

        let mut source_ids: HashMap<i32, Uuid> = HashMap::new();
        let mut packet_ids: HashMap<i32, u16> = HashMap::new();

        for test_case_action in &test_case.actions {
            match test_case_action {
                action_receive_telemetry @ TestCaseAction::ReceiveTelemetry { .. } => {
                    Self::receive_telemetry(
                        action_receive_telemetry,
                        &mut mqtt_hub,
                        &mut source_ids,
                        &mut packet_ids,
                        test_case_index,
                    );
                }
                action_await_ack @ TestCaseAction::AwaitAck { .. } => {
                    Self::await_acknowledgement(action_await_ack, &mut mqtt_hub, &packet_ids).await;
                }
                action_sleep @ TestCaseAction::Sleep { .. } => {
                    Self::sleep(action_sleep).await;
                }
                _action_disconnect @ TestCaseAction::Disconnect { .. } => {
                    Self::disconnect(&mut mqtt_hub);
                }
                _action_freeze_time @ TestCaseAction::FreezeTime { .. } => {
                    Self::freeze_time();
                }
                _action_unfreeze_time @ TestCaseAction::UnfreezeTime { .. } => {
                    Self::unfreeze_time();
                }
                _ => {
                    panic!("unexpected action kind");
                }
            }
        }

        if let Some(test_case_epilogue) = test_case.epilogue.as_ref() {
            for topic in &test_case_epilogue.subscribed_topics {
                assert!(
                    mqtt_hub.has_subscribed(topic),
                    "topic {topic} has not been subscribed"
                );
            }

            if let Some(acknowledgement_count) = test_case_epilogue.acknowledgement_count {
                assert_eq!(
                    acknowledgement_count,
                    mqtt_hub.get_acknowledgement_count(),
                    "acknowledgement count"
                );
            }

            if let Some(telemetry_count) = test_case_epilogue.telemetry_count {
                assert_eq!(
                    telemetry_count,
                    *telemetry_counts[0].lock().unwrap(),
                    "telemetry count"
                );
            }

            for (receiver_index, telemetry_count) in &test_case_epilogue.telemetry_counts {
                assert_eq!(
                    *telemetry_count,
                    *telemetry_counts[*receiver_index].lock().unwrap(),
                    "telemetry count"
                );
            }

            for received_telemetry in &test_case_epilogue.received_telemetries {
                Self::check_received_telemetry(received_telemetry, &mut telemetry_rx, &source_ids)
                    .await;
            }
        }
    }

    async fn receiver_loop(
        mut receiver: TelemetryReceiver<TestPayload, C>,
        telemetry_count: Arc<Mutex<i32>>,
        telemetry_tx: mpsc::UnboundedSender<ReceivedTelemetry>,
    ) {
        while let Some(message) = receiver.recv().await {
            match message {
                Ok((telemetry, ack_token)) => {
                    *telemetry_count.lock().unwrap() += 1;

                    let mut metadata = HashMap::new();
                    for (key, value) in telemetry.custom_user_data {
                        metadata.insert(key, value);
                    }

                    let cloud_event = match telemetry.cloud_event {
                        Some(cloud_event) => Some(TestCaseCloudEvent {
                            source: Some(cloud_event.source),
                            event_type: Some(cloud_event.event_type),
                            spec_version: Some(cloud_event.spec_version),
                            data_content_type: cloud_event.data_content_type,
                            subject: cloud_event.subject,
                            data_schema: cloud_event.data_schema,
                        }),
                        None => None,
                    };

                    telemetry_tx
                        .send(ReceivedTelemetry {
                            telemetry_value: telemetry.payload.payload,
                            metadata,
                            cloud_event,
                            source_id: telemetry.sender_id,
                        })
                        .unwrap();

                    if let Some(ack_token) = ack_token {
                        ack_token.ack();
                    }
                }
                Err(e) => {
                    panic!("Error receiving telemetry message: {e:?}");
                }
            }
        }
    }

    async fn get_telemetry_receiver(
        managed_client: C,
        tcr: &TestCaseReceiver<ReceiverDefaults>,
        catch: Option<&TestCaseCatch>,
        mqtt_hub: &mut MqttHub,
    ) -> Option<TelemetryReceiver<TestPayload, C>> {
        let mut receiver_options_builder = TelemetryReceiverOptionsBuilder::default();

        if let Some(telemetry_topic) = tcr.telemetry_topic.as_ref() {
            receiver_options_builder.topic_pattern(telemetry_topic);
        }

        if let Some(topic_namespace) = tcr.topic_namespace.as_ref() {
            receiver_options_builder.topic_namespace(topic_namespace);
        }

        if let Some(topic_token_map) = tcr.topic_token_map.as_ref() {
            receiver_options_builder.topic_token_map(topic_token_map.clone());
        }

        let options_result = receiver_options_builder.build();
        if let Err(error) = options_result {
            if let Some(catch) = catch {
                aio_protocol_error_checker::check_error(
                    catch,
                    &Self::from_receiver_options_builder_error(error),
                );
            } else {
                panic!("Unexpected error when building TelemetryReceiver options: {error}");
            }

            return None;
        }

        let receiver_options = options_result.unwrap();

        match TelemetryReceiver::new(managed_client, receiver_options) {
            Ok(mut receiver) => {
                if let Some(catch) = catch {
                    // TelemetryReceiver has no start method, so if an exception is expected, recv may be needed to trigger it.
                    let (recv_result, _) = tokio::join!(
                        time::timeout(TEST_TIMEOUT, receiver.recv()),
                        time::timeout(TEST_TIMEOUT, mqtt_hub.await_operation())
                    );
                    match recv_result {
                        Ok(Some(Ok(_))) => {
                            panic!(
                                "Expected {} error when constructing TelemetryReceiver but no error returned",
                                catch.error_kind
                            );
                        }
                        Ok(Some(Err(error))) => {
                            aio_protocol_error_checker::check_error(catch, &error);
                        }
                        _ => {
                            panic!(
                                "Expected {} error when calling recv() on TelemetryReceiver but got timeout instead",
                                catch.error_kind);
                        }
                    };
                    None
                } else {
                    Some(receiver)
                }
            }
            Err(error) => {
                if let Some(catch) = catch {
                    aio_protocol_error_checker::check_error(catch, &error);
                    None
                } else {
                    panic!("Unexpected error when constructing TelemetryReceiver: {error}");
                }
            }
        }
    }

    fn receive_telemetry(
        action: &TestCaseAction<ReceiverDefaults>,
        mqtt_hub: &mut MqttHub,
        source_ids: &mut HashMap<i32, Uuid>,
        packet_ids: &mut HashMap<i32, u16>,
        test_case_index: i32,
    ) {
        if let TestCaseAction::ReceiveTelemetry {
            defaults_type: _,
            topic,
            payload,
            bypass_serialization,
            content_type,
            format_indicator,
            metadata,
            qos,
            message_expiry,
            source_index,
            packet_index,
        } = action
        {
            let mut user_properties: Vec<(String, String)> = metadata
                .iter()
                .map(|(k, v)| (k.clone(), v.clone()))
                .collect();

            if let Some(source_index) = source_index {
                if let Some(source_id) = source_ids.get(source_index) {
                    user_properties
                        .push(("__srcId".to_string(), source_id.hyphenated().to_string()));
                } else {
                    let source_id = Uuid::new_v4();
                    user_properties
                        .push(("__srcId".to_string(), source_id.hyphenated().to_string()));
                    source_ids.insert(*source_index, source_id);
                }
            }

            let message_expiry_interval = message_expiry.as_ref().map(|message_expiry| {
                u32::try_from(message_expiry.to_duration().as_secs()).unwrap()
            });

            let packet_id = if let Some(packet_index) = packet_index {
                packet_ids.get(packet_index)
            } else {
                None
            };
            let packet_id = if let Some(packet_id) = packet_id {
                *packet_id
            } else {
                mqtt_hub.get_new_packet_id()
            };
            if let Some(packet_index) = packet_index {
                packet_ids.insert(*packet_index, packet_id);
            }

            let topic = if let Some(topic) = topic {
                Bytes::copy_from_slice(topic.as_bytes())
            } else {
                Bytes::new()
            };

            let payload = if let Some(payload) = payload {
                if *bypass_serialization {
                    Bytes::copy_from_slice(payload.as_bytes())
                } else {
                    Bytes::copy_from_slice(
                        TestPayload {
                            payload: Some(payload.clone()),
                            test_case_index: Some(test_case_index),
                        }
                        .serialize()
                        .unwrap()
                        .as_slice(),
                    )
                }
            } else {
                Bytes::new()
            };

            let properties = PublishProperties {
                payload_format_indicator: *format_indicator,
                message_expiry_interval,
                user_properties,
                content_type: content_type.clone(),
                ..Default::default()
            };

            let publish = Publish {
                qos: qos::to_enum(*qos),
                topic,
                pkid: packet_id,
                payload,
                properties: Some(properties),
                ..Default::default()
            };

            mqtt_hub.receive_message(publish);
        } else {
            panic!("internal logic error");
        }
    }

    async fn await_acknowledgement(
        action: &TestCaseAction<ReceiverDefaults>,
        mqtt_hub: &mut MqttHub,
        packet_ids: &HashMap<i32, u16>,
    ) {
        if let TestCaseAction::AwaitAck {
            defaults_type: _,
            packet_index,
        } = action
        {
            let packet_id = future::timeout(TEST_TIMEOUT, mqtt_hub.await_acknowledgement())
                .await
                .expect("test timeout in await_acknowledgement");
            if let Some(packet_index) = packet_index {
                assert_eq!(
                    *packet_ids
                        .get(packet_index)
                        .expect("packet index {packet_index} not found in packet id map"),
                    packet_id,
                    "packet ID"
                );
            }
        } else {
            panic!("internal logic error");
        }
    }

    async fn sleep(action: &TestCaseAction<ReceiverDefaults>) {
        if let TestCaseAction::Sleep {
            defaults_type: _,
            duration,
        } = action
        {
            time::sleep(duration.to_duration()).await;
        } else {
            panic!("internal logic error");
        }
    }

    fn disconnect(mqtt_hub: &mut MqttHub) {
        mqtt_hub.disconnect();
    }

    fn freeze_time() {}

    fn unfreeze_time() {}

    async fn check_received_telemetry(
        expected_telemetry: &TestCaseReceivedTelemetry,
        telemetry_rx: &mut mpsc::UnboundedReceiver<ReceivedTelemetry>,
        source_ids: &HashMap<i32, Uuid>,
    ) {
        let received_telemetry = telemetry_rx
            .recv()
            .await
            .expect("missing expected telemetry");

        if let Some(expected_value) = expected_telemetry.telemetry_value.as_ref() {
            assert_eq!(expected_value, &received_telemetry.telemetry_value);
        }

        for (key, value) in &expected_telemetry.metadata {
            assert_eq!(
                value.as_ref(),
                received_telemetry.metadata.get(key),
                "metadata key {key} expected {value:?}"
            );
        }

        if let Some(expected_cloud_event) = &expected_telemetry.cloud_event {
            if let Some(received_cloud_event) = &received_telemetry.cloud_event {
                if let Some(expected_source) = expected_cloud_event.source.as_ref() {
                    assert_eq!(
                        expected_source,
                        received_cloud_event
                            .source
                            .as_ref()
                            .expect("missing cloud event source")
                    );
                }
                if let Some(expected_event_type) = expected_cloud_event.event_type.as_ref() {
                    assert_eq!(
                        expected_event_type,
                        received_cloud_event
                            .event_type
                            .as_ref()
                            .expect("missing cloud event type")
                    );
                }
                if let Some(expected_spec_version) = expected_cloud_event.spec_version.as_ref() {
                    assert_eq!(
                        expected_spec_version,
                        received_cloud_event
                            .spec_version
                            .as_ref()
                            .expect("missing cloud event spec version")
                    );
                }
                if let Some(expected_data_content_type) =
                    expected_cloud_event.data_content_type.as_ref()
                {
                    assert_eq!(
                        expected_data_content_type,
                        received_cloud_event
                            .data_content_type
                            .as_ref()
                            .expect("missing cloud event data content type")
                    );
                }
                if let Some(expected_subject) = expected_cloud_event.subject.as_ref() {
                    assert_eq!(
                        expected_subject,
                        received_cloud_event
                            .subject
                            .as_ref()
                            .expect("missing cloud event subject")
                    );
                }
                if let Some(expected_data_schema) = expected_cloud_event.data_schema.as_ref() {
                    assert_eq!(
                        expected_data_schema,
                        received_cloud_event
                            .data_schema
                            .as_ref()
                            .expect("missing cloud event data schema")
                    );
                }
            } else {
                panic!("expected cloud event but not found in received telemetry");
            }
        }

        if let Some(source_index) = &expected_telemetry.source_index {
            let source_id = source_ids
                .get(source_index)
                .expect("source index {source_index} not found in source id map");
            assert_eq!(
                source_id.hyphenated().to_string(),
                received_telemetry.source_id.expect("missing source id")
            );
        }
    }

    fn from_receiver_options_builder_error(
        builder_error: TelemetryReceiverOptionsBuilderError,
    ) -> AIOProtocolError {
        let property_name = match builder_error {
            TelemetryReceiverOptionsBuilderError::UninitializedField(field_name) => {
                Some(field_name.to_string())
            }
            _ => None,
        };

        let mut protocol_error = AIOProtocolError {
            message: None,
            kind: AIOProtocolErrorKind::ConfigurationInvalid,
            in_application: false,
            is_shallow: true,
            is_remote: false,
            nested_error: Some(Box::new(builder_error)),
            http_status_code: None,
            header_name: None,
            header_value: None,
            timeout_name: None,
            timeout_value: None,
            property_name,
            property_value: None,
            command_name: None,
            protocol_version: None,
            supported_protocol_major_versions: None,
        };

        protocol_error.ensure_error_message();
        protocol_error
    }
}
