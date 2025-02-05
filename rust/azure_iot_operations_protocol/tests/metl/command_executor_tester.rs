// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

use std::collections::HashMap;
use std::convert::TryFrom;
use std::marker::PhantomData;
use std::str::from_utf8;
use std::sync::{Arc, Mutex};

use async_std::future;
use azure_iot_operations_mqtt::control_packet::{Publish, PublishProperties};
use azure_iot_operations_mqtt::interface::ManagedClient;
use azure_iot_operations_protocol::application::{
    ApplicationContext, ApplicationContextOptionsBuilder,
};
use azure_iot_operations_protocol::common::aio_protocol_error::{
    AIOProtocolError, AIOProtocolErrorKind,
};
use azure_iot_operations_protocol::common::payload_serialize::PayloadSerialize;
use azure_iot_operations_protocol::rpc::command_executor::{
    CommandExecutor, CommandExecutorOptionsBuilder, CommandExecutorOptionsBuilderError,
    CommandResponseBuilder,
};
use bytes::Bytes;
use tokio::time;
use uuid::Uuid;

use crate::metl::aio_protocol_error_checker;
use crate::metl::countdown_event_map::CountdownEventMap;
use crate::metl::defaults::ExecutorDefaults;
use crate::metl::mqtt_hub::MqttHub;
use crate::metl::qos;
use crate::metl::test_case::TestCase;
use crate::metl::test_case_action::TestCaseAction;
use crate::metl::test_case_catch::TestCaseCatch;
use crate::metl::test_case_executor::TestCaseExecutor;
use crate::metl::test_case_published_message::TestCasePublishedMessage;
use crate::metl::test_error_kind::TestErrorKind;
use crate::metl::test_payload::TestPayload;

const TEST_TIMEOUT: time::Duration = time::Duration::from_secs(10);

pub struct CommandExecutorTester<C>
where
    C: ManagedClient + Clone + Send + Sync + 'static,
    C::PubReceiver: Send + Sync + 'static,
{
    managed_client: PhantomData<C>,
}

impl<C> CommandExecutorTester<C>
where
    C: ManagedClient + Clone + Send + Sync + 'static,
    C::PubReceiver: Send + Sync + 'static,
{
    pub async fn test_command_executor(
        test_case: TestCase<ExecutorDefaults>,
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

        let mut countdown_events = CountdownEventMap::new();
        for (event_name, &init_count) in &test_case.prologue.countdown_events {
            countdown_events.insert(event_name.clone(), init_count);
        }

        let executor_count = test_case.prologue.executors.len();
        let mut execution_counts = Vec::with_capacity(executor_count);

        let mut ix = 0;
        for test_case_executor in &test_case.prologue.executors {
            ix += 1;
            let catch = if ix == executor_count {
                test_case.prologue.catch.as_ref()
            } else {
                None
            };

            let execution_count = Arc::new(Mutex::new(0));
            execution_counts.push(execution_count.clone());

            if let Some(executor) = Self::get_command_executor(
                managed_client.clone(),
                test_case_executor,
                catch,
                &mut mqtt_hub,
            )
            .await
            {
                tokio::task::spawn(Self::executor_loop(
                    executor,
                    (*test_case_executor).clone(),
                    countdown_events.clone(),
                    execution_count,
                ));
            }
        }

        let mut source_ids: HashMap<i32, Uuid> = HashMap::new();
        let mut correlation_ids: HashMap<i32, Option<Bytes>> = HashMap::new();
        let mut packet_ids: HashMap<i32, u16> = HashMap::new();

        for test_case_action in &test_case.actions {
            match test_case_action {
                action_receive_request @ TestCaseAction::ReceiveRequest { .. } => {
                    Self::receive_request(
                        action_receive_request,
                        &mut mqtt_hub,
                        &mut source_ids,
                        &mut correlation_ids,
                        &mut packet_ids,
                        test_case_index,
                    );
                }
                action_await_ack @ TestCaseAction::AwaitAck { .. } => {
                    Self::await_acknowledgement(action_await_ack, &mut mqtt_hub, &packet_ids).await;
                }
                action_await_publish @ TestCaseAction::AwaitPublish { .. } => {
                    Self::await_publish(action_await_publish, &mut mqtt_hub, &correlation_ids)
                        .await;
                }
                action_sync @ TestCaseAction::Sync { .. } => {
                    Self::sync(action_sync, &countdown_events);
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

            if let Some(publication_count) = test_case_epilogue.publication_count {
                assert_eq!(
                    publication_count,
                    mqtt_hub.get_publication_count(),
                    "publication count"
                );
            }

            for published_message in &test_case_epilogue.published_messages {
                Self::check_published_message(
                    published_message,
                    &mqtt_hub,
                    &correlation_ids,
                    test_case_index,
                );
            }

            if let Some(acknowledgement_count) = test_case_epilogue.acknowledgement_count {
                assert_eq!(
                    acknowledgement_count,
                    mqtt_hub.get_acknowledgement_count(),
                    "acknowledgement count"
                );
            }

            if let Some(execution_count) = test_case_epilogue.execution_count {
                assert_eq!(execution_count, *execution_counts[0].lock().unwrap());
            }

            for (executor_index, execution_count) in &test_case_epilogue.execution_counts {
                assert_eq!(
                    *execution_count,
                    *execution_counts[*executor_index].lock().unwrap()
                );
            }
        }
    }

    async fn executor_loop(
        mut executor: CommandExecutor<TestPayload, TestPayload, C>,
        test_case_executor: TestCaseExecutor<ExecutorDefaults>,
        countdown_events: CountdownEventMap,
        execution_count: Arc<Mutex<i32>>,
    ) {
        let mut sequencers = HashMap::new();
        for req in test_case_executor.request_responses_map.keys() {
            sequencers.insert(req.clone(), 0);
        }

        loop {
            if let Some(Ok(request)) = executor.recv().await {
                *execution_count.lock().unwrap() += 1;

                for test_case_sync in &test_case_executor.sync {
                    if let Some(wait_event) = &test_case_sync.wait_event {
                        countdown_events
                            .wait_timeout(wait_event.as_str(), TEST_TIMEOUT)
                            .expect("test timeout in countdown_event.wait");
                    }

                    if let Some(signal_event) = &test_case_sync.signal_event {
                        countdown_events.signal(signal_event.as_str());
                    }
                }

                if let Some(raise_error) = &test_case_executor.raise_error {
                    if raise_error.kind != TestErrorKind::None {
                        let message = match &raise_error.message {
                            Some(message) => message.clone(),
                            None => String::default(),
                        };
                        request.error(message).await.unwrap();
                        continue;
                    }
                }

                let response_value = if let Some(request_value) = request.payload.payload.as_ref() {
                    if let Some(response_sequence) =
                        test_case_executor.request_responses_map.get(request_value)
                    {
                        let sequencer = sequencers.get_mut(request_value).unwrap();
                        let index = *sequencer % response_sequence.len();
                        *sequencer += 1;
                        Some(response_sequence[index].clone())
                    } else {
                        None
                    }
                } else {
                    None
                };

                let response_payload = TestPayload {
                    payload: response_value,
                    test_case_index: request.payload.test_case_index,
                };

                let mut metadata = Vec::with_capacity(test_case_executor.response_metadata.len());
                for (key, value) in &test_case_executor.response_metadata {
                    if let Some(val) = value {
                        metadata.push((key.clone(), val.clone()));
                    } else if let Some(kvp) = request.custom_user_data.iter().find(|&m| m.0 == *key)
                    {
                        metadata.push((key.clone(), kvp.1.to_string()));
                    }
                }

                let response = CommandResponseBuilder::default()
                    .payload(response_payload)
                    .unwrap()
                    .custom_user_data(metadata)
                    .build()
                    .unwrap();

                request.complete(response).await.unwrap();
            }
        }
    }

    async fn get_command_executor(
        managed_client: C,
        tce: &TestCaseExecutor<ExecutorDefaults>,
        catch: Option<&TestCaseCatch>,
        mqtt_hub: &mut MqttHub,
    ) -> Option<CommandExecutor<TestPayload, TestPayload, C>> {
        let mut executor_options_builder = CommandExecutorOptionsBuilder::default();

        if let Some(request_topic) = tce.request_topic.as_ref() {
            executor_options_builder.request_topic_pattern(request_topic);
        }

        if let Some(topic_namespace) = tce.topic_namespace.as_ref() {
            executor_options_builder.topic_namespace(topic_namespace);
        }

        if let Some(topic_token_map) = tce.topic_token_map.as_ref() {
            executor_options_builder.topic_token_map(topic_token_map.clone());
        }

        if let Some(command_name) = tce.command_name.as_ref() {
            executor_options_builder.command_name(command_name);
        }

        executor_options_builder.is_idempotent(tce.idempotent);

        if let Some(cache_ttl) = tce.cache_ttl.as_ref() {
            executor_options_builder.cacheable_duration(cache_ttl.to_duration());
        }

        let options_result = executor_options_builder.build();
        if let Err(error) = options_result {
            if let Some(catch) = catch {
                aio_protocol_error_checker::check_error(
                    catch,
                    &Self::from_executor_options_builder_error(error),
                );
            } else {
                panic!("Unexpected error when building CommandExecutor options: {error}");
            }

            return None;
        }

        let executor_options = options_result.unwrap();

        match CommandExecutor::new(
            ApplicationContext::new(ApplicationContextOptionsBuilder::default().build().unwrap()),
            managed_client,
            executor_options,
        ) {
            Ok(mut executor) => {
                if let Some(catch) = catch {
                    // CommandExecutor has no start method, so if an exception is expected, recv may be needed to trigger it.
                    let (recv_result, _) = tokio::join!(
                        time::timeout(TEST_TIMEOUT, executor.recv()),
                        time::timeout(TEST_TIMEOUT, mqtt_hub.await_operation())
                    );
                    match recv_result {
                        Ok(Some(Ok(_))) => {
                            panic!(
                                "Expected {} error when constructing CommandExecutor but no error returned",
                                catch.error_kind
                            );
                        }
                        Ok(Some(Err(error))) => {
                            aio_protocol_error_checker::check_error(catch, &error);
                        }
                        _ => {
                            panic!(
                                "Expected {} error when calling recv() on CommandExecutor but got timeout instead",
                                catch.error_kind);
                        }
                    };
                    None
                } else {
                    Some(executor)
                }
            }
            Err(error) => {
                if let Some(catch) = catch {
                    aio_protocol_error_checker::check_error(catch, &error);
                    None
                } else {
                    panic!("Unexpected error when constructing CommandExecutor: {error}");
                }
            }
        }
    }

    fn receive_request(
        action: &TestCaseAction<ExecutorDefaults>,
        mqtt_hub: &mut MqttHub,
        source_ids: &mut HashMap<i32, Uuid>,
        correlation_ids: &mut HashMap<i32, Option<Bytes>>,
        packet_ids: &mut HashMap<i32, u16>,
        test_case_index: i32,
    ) {
        if let TestCaseAction::ReceiveRequest {
            defaults_type: _,
            topic,
            payload,
            bypass_serialization,
            content_type,
            format_indicator,
            metadata,
            correlation_index,
            correlation_id,
            qos,
            message_expiry,
            response_topic,
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

            let correlation_data = if let Some(correlation_index) = correlation_index {
                if let Some(correlation_data) = correlation_ids.get(correlation_index) {
                    correlation_data.clone()
                } else {
                    let correlation_data = if let Some(correlation_id) = correlation_id {
                        Some(Bytes::from(correlation_id.clone()))
                    } else {
                        Some(Bytes::copy_from_slice(Uuid::new_v4().as_bytes()))
                    };
                    correlation_ids.insert(*correlation_index, correlation_data.clone());
                    correlation_data
                }
            } else {
                None
            };

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
                        .payload
                        .as_slice(),
                    )
                }
            } else {
                Bytes::new()
            };

            let properties = PublishProperties {
                payload_format_indicator: *format_indicator,
                message_expiry_interval,
                response_topic: response_topic.clone(),
                correlation_data,
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
        action: &TestCaseAction<ExecutorDefaults>,
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

    async fn await_publish(
        action: &TestCaseAction<ExecutorDefaults>,
        mqtt_hub: &mut MqttHub,
        correlation_ids: &HashMap<i32, Option<Bytes>>,
    ) {
        if let TestCaseAction::AwaitPublish {
            defaults_type: _,
            correlation_index,
        } = action
        {
            let correlation_id = future::timeout(TEST_TIMEOUT, mqtt_hub.await_publish())
                .await
                .expect("test timeout in await_publish");
            if let Some(correlation_index) = correlation_index {
                assert_eq!(
                    correlation_ids.get(correlation_index).expect(
                        "correlation index {correlation_index} not found in correlation map"
                    ),
                    &correlation_id,
                    "correlation ID"
                );
            }
        } else {
            panic!("internal logic error");
        }
    }

    fn sync(action: &TestCaseAction<ExecutorDefaults>, countdown_events: &CountdownEventMap) {
        if let TestCaseAction::Sync {
            defaults_type: _,
            signal_event,
            wait_event,
        } = action
        {
            if let Some(wait_event) = wait_event {
                countdown_events
                    .wait_timeout(wait_event.as_str(), TEST_TIMEOUT)
                    .expect("test timeout in countdown_event.wait");
            }

            if let Some(signal_event) = signal_event {
                countdown_events.signal(signal_event.as_str());
            }
        } else {
            panic!("internal logic error");
        }
    }

    async fn sleep(action: &TestCaseAction<ExecutorDefaults>) {
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

    fn check_published_message(
        expected_message: &TestCasePublishedMessage,
        mqtt_hub: &MqttHub,
        correlation_ids: &HashMap<i32, Option<Bytes>>,
        test_case_index: i32,
    ) {
        let published_message =
            if let Some(correlation_index) = expected_message.correlation_index {
                if let Some(correlation_id) = correlation_ids.get(&correlation_index) {
                    let publish = mqtt_hub.get_published_message(correlation_id);
                    if publish.is_some() {
                        publish
                    } else {
                        panic!("no message published with correlation data corresponding to index {correlation_index}");
                    }
                } else {
                    panic!("no correlation data recorded for correlation index {correlation_index}");
                }
            } else {
                let publish = mqtt_hub.get_published_message(&None);
                if publish.is_some() {
                    publish
                } else {
                    panic!("no message published with empty correlation data");
                }
            }.unwrap();

        if let Some(topic) = expected_message.topic.as_ref() {
            assert_eq!(
                topic,
                from_utf8(published_message.topic.to_vec().as_slice())
                    .expect("could not process published message topic as UTF8"),
                "topic"
            );
        }

        if let Some(payload) = expected_message.payload.as_ref() {
            if let Some(payload) = payload {
                let payload = Bytes::copy_from_slice(
                    TestPayload {
                        payload: Some(payload.clone()),
                        test_case_index: Some(test_case_index),
                    }
                    .serialize()
                    .unwrap()
                    .payload
                    .as_slice(),
                );
                assert_eq!(payload, published_message.payload, "payload");
            } else {
                assert!(published_message.payload.is_empty());
            }
        }

        if !expected_message.metadata.is_empty() {
            if let Some(properties) = published_message.properties.as_ref() {
                for (key, value) in &expected_message.metadata {
                    let found = properties.user_properties.iter().find(|&k| &k.0 == key);
                    if let Some(value) = value {
                        assert_eq!(
                            value,
                            &found.unwrap().1,
                            "metadata key {key} expected {value}"
                        );
                    } else {
                        assert_eq!(None, found, "metadata key {key} not expected");
                    }
                }
            } else {
                panic!("expected metadata but found no properties in published message");
            }
        }

        if let Some(command_status) = expected_message.command_status {
            if let Some(properties) = published_message.properties.as_ref() {
                let found = properties
                    .user_properties
                    .iter()
                    .find(|&k| &k.0 == "__stat");
                if let Some(command_status) = command_status {
                    assert_eq!(
                        command_status.to_string(),
                        found.unwrap().1,
                        "status property expected {command_status}"
                    );
                } else {
                    assert_eq!(None, found, "status property not expected");
                }
            } else {
                panic!("expected status property but found no properties in published message");
            }
        }

        if let Some(is_application_error) = expected_message.is_application_error {
            if let Some(properties) = published_message.properties.as_ref() {
                let found = properties
                    .user_properties
                    .iter()
                    .find(|&k| &k.0 == "__apErr");
                if is_application_error {
                    assert!(
                        found.unwrap().1.to_lowercase() == "true",
                        "is application error"
                    );
                } else {
                    assert!(
                        found.is_none() || found.unwrap().1.to_lowercase() == "false",
                        "is application error"
                    );
                }
            } else if is_application_error {
                panic!("expected is application error property but found no properties in published message");
            }
        }

        if expected_message.expiry.is_some() {
            if let Some(properties) = published_message.properties.as_ref() {
                assert_eq!(expected_message.expiry, properties.message_expiry_interval);
            } else {
                panic!(
                    "expected message expiry interval but found no properties in published message"
                );
            }
        }
    }

    fn from_executor_options_builder_error(
        builder_error: CommandExecutorOptionsBuilderError,
    ) -> AIOProtocolError {
        let property_name = match builder_error {
            CommandExecutorOptionsBuilderError::UninitializedField(field_name) => {
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
