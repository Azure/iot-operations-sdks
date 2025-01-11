// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

use std::collections::HashMap;
use std::convert::TryFrom;
use std::marker::PhantomData;
use std::str::from_utf8;
use std::sync::Arc;

use async_std::future;
use azure_iot_operations_mqtt::control_packet::{Publish, PublishProperties};
use azure_iot_operations_mqtt::interface::ManagedClient;
use azure_iot_operations_protocol::common::aio_protocol_error::{
    AIOProtocolError, AIOProtocolErrorKind,
};
use azure_iot_operations_protocol::common::payload_serialize::PayloadSerialize;
use azure_iot_operations_protocol::rpc::command_invoker::{
    CommandInvoker, CommandInvokerOptionsBuilder, CommandInvokerOptionsBuilderError,
    CommandRequestBuilder, CommandRequestBuilderError, CommandResponse,
};
use bytes::Bytes;
use tokio::sync::oneshot;
use tokio::time;

use crate::metl::aio_protocol_error_checker;
use crate::metl::defaults::{get_invoker_defaults, InvokerDefaults};
use crate::metl::mqtt_hub::MqttHub;
use crate::metl::qos;
use crate::metl::test_case::TestCase;
use crate::metl::test_case_action::TestCaseAction;
use crate::metl::test_case_catch::TestCaseCatch;
use crate::metl::test_case_invoker::TestCaseInvoker;
use crate::metl::test_case_published_message::TestCasePublishedMessage;
use crate::metl::test_payload::TestPayload;

const TEST_TIMEOUT: time::Duration = time::Duration::from_secs(10);

type InvokeResultReceiver =
    oneshot::Receiver<Result<CommandResponse<TestPayload>, AIOProtocolError>>;

pub struct CommandInvokerTester<C>
where
    C: ManagedClient + Clone + Send + Sync + 'static,
    C::PubReceiver: Send + Sync + 'static,
{
    managed_client: PhantomData<C>,
}

impl<'a, C> CommandInvokerTester<C>
where
    C: ManagedClient + Clone + Send + Sync + 'static,
    C::PubReceiver: Send + Sync + 'static,
{
    pub async fn test_command_invoker(
        test_case: TestCase<InvokerDefaults>,
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

        let mut invokers: HashMap<String, Arc<CommandInvoker<TestPayload, TestPayload, C>>> =
            HashMap::new();

        let invoker_count = test_case.prologue.invokers.len();
        let mut ix = 0;
        for test_case_invoker in &test_case.prologue.invokers {
            ix += 1;
            let catch = if ix == invoker_count {
                test_case.prologue.catch.as_ref()
            } else {
                None
            };

            if let Some(invoker) = Self::get_command_invoker(
                managed_client.clone(),
                test_case_invoker,
                catch,
                &mut mqtt_hub,
                test_case_index,
            )
            .await
            {
                invokers.insert(
                    test_case_invoker.command_name.clone().unwrap(),
                    Arc::new(invoker),
                );
            }
        }

        let mut invocation_chans: HashMap<i32, Option<InvokeResultReceiver>> = HashMap::new();
        let mut correlation_ids: HashMap<i32, Option<Bytes>> = HashMap::new();
        let mut packet_ids: HashMap<i32, u16> = HashMap::new();

        for test_case_action in &test_case.actions {
            match test_case_action {
                action_invoke_command @ TestCaseAction::InvokeCommand { .. } => {
                    Self::invoke_command(
                        action_invoke_command,
                        &invokers,
                        &mut invocation_chans,
                        test_case_index,
                    );
                }
                action_await_invocation @ TestCaseAction::AwaitInvocation { .. } => {
                    Self::await_invocation(action_await_invocation, &mut invocation_chans).await;
                }
                action_receive_response @ TestCaseAction::ReceiveResponse { .. } => {
                    Self::receive_response(
                        action_receive_response,
                        &mut mqtt_hub,
                        &mut correlation_ids,
                        &mut packet_ids,
                        test_case_index,
                    );
                }
                action_await_ack @ TestCaseAction::AwaitAck { .. } => {
                    Self::await_acknowledgement(action_await_ack, &mut mqtt_hub, &packet_ids).await;
                }
                action_await_publish @ TestCaseAction::AwaitPublish { .. } => {
                    Self::await_publish(action_await_publish, &mut mqtt_hub, &mut correlation_ids)
                        .await;
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
        }
    }

    async fn get_command_invoker(
        managed_client: C,
        tci: &TestCaseInvoker<InvokerDefaults>,
        catch: Option<&TestCaseCatch>,
        mqtt_hub: &mut MqttHub,
        test_case_index: i32,
    ) -> Option<CommandInvoker<TestPayload, TestPayload, C>> {
        let mut invoker_options_builder = CommandInvokerOptionsBuilder::default();

        if let Some(request_topic) = tci.request_topic.as_ref() {
            invoker_options_builder.request_topic_pattern(request_topic);
        }

        invoker_options_builder.topic_namespace(tci.topic_namespace.clone());
        invoker_options_builder.response_topic_prefix(tci.response_topic_prefix.clone());
        invoker_options_builder.response_topic_suffix(tci.response_topic_suffix.clone());

        if let Some(topic_token_map) = tci.topic_token_map.as_ref() {
            invoker_options_builder.topic_token_map(topic_token_map.clone());
        }

        if let Some(command_name) = tci.command_name.as_ref() {
            invoker_options_builder.command_name(command_name);
        }

        let options_result = invoker_options_builder.build();
        if let Err(error) = options_result {
            if let Some(catch) = catch {
                aio_protocol_error_checker::check_error(
                    catch,
                    &Self::from_invoker_options_builder_error(error),
                );
            } else {
                panic!("Unexpected error when building CommandInvoker options: {error}");
            }

            return None;
        }

        let invoker_options = options_result.unwrap();

        match CommandInvoker::new(managed_client, invoker_options) {
            Ok(invoker) => {
                if let Some(catch) = catch {
                    // CommandInvoker has no start method, so if an exception is expected, invoke may be needed to trigger it.

                    let default_invoke_command = get_invoker_defaults()
                        .as_ref()
                        .unwrap()
                        .actions
                        .as_ref()
                        .unwrap()
                        .invoke_command
                        .as_ref()
                        .unwrap();

                    let mut command_request_builder = CommandRequestBuilder::default();

                    if let Some(request_value) = default_invoke_command.request_value.clone() {
                        command_request_builder
                            .payload(&TestPayload {
                                payload: Some(request_value.clone()),
                                test_case_index: Some(test_case_index),
                            })
                            .unwrap();
                    }

                    if let Some(timeout) = default_invoke_command.timeout.clone() {
                        command_request_builder.timeout(timeout.to_duration());
                    }

                    let request = command_request_builder.build().unwrap();

                    let (invoke_result, _) = tokio::join!(
                        time::timeout(TEST_TIMEOUT, invoker.invoke(request)),
                        time::timeout(TEST_TIMEOUT, mqtt_hub.await_operation())
                    );

                    match invoke_result {
                        Ok(Ok(_)) => {
                            panic!(
                                "Expected {} error when constructing CommandInvoker but no error returned",
                                catch.error_kind
                            );
                        }
                        Ok(Err(error)) => {
                            aio_protocol_error_checker::check_error(catch, &error);
                        }
                        _ => {
                            panic!(
                                "Expected {} error when calling recv() on CommandInvoker but got timeout instead",
                                catch.error_kind);
                        }
                    };

                    None
                } else {
                    Some(invoker)
                }
            }
            Err(error) => {
                if let Some(catch) = catch {
                    aio_protocol_error_checker::check_error(catch, &error);
                    None
                } else {
                    panic!("Unexpected error when constructing CommandInvoker: {error}");
                }
            }
        }
    }

    fn invoke_command(
        action: &TestCaseAction<InvokerDefaults>,
        invokers: &'a HashMap<String, Arc<CommandInvoker<TestPayload, TestPayload, C>>>,
        invocation_chans: &mut HashMap<i32, Option<InvokeResultReceiver>>,
        test_case_index: i32,
    ) {
        if let TestCaseAction::InvokeCommand {
            defaults_type: _,
            invocation_index,
            command_name,
            topic_token_map,
            timeout,
            request_value,
            metadata,
        } = action
        {
            let mut command_request_builder = CommandRequestBuilder::default();

            if let Some(request_value) = request_value {
                command_request_builder
                    .payload(&TestPayload {
                        payload: Some(request_value.clone()),
                        test_case_index: Some(test_case_index),
                    })
                    .unwrap();
            }

            if let Some(topic_token_map) = topic_token_map {
                command_request_builder.topic_tokens(topic_token_map.clone());
            }

            if let Some(timeout) = timeout {
                command_request_builder.timeout(timeout.to_duration());
            }

            if let Some(metadata) = metadata {
                let mut user_data = Vec::with_capacity(metadata.len());
                for (key, value) in metadata {
                    user_data.push((key.clone(), value.clone()));
                }
                command_request_builder.custom_user_data(user_data);
            }

            if let Some(command_name) = &command_name {
                let invoker = invokers[command_name].clone();
                let (response_tx, response_rx) = oneshot::channel();
                invocation_chans.insert(*invocation_index, Some(response_rx));
                match command_request_builder.build() {
                    Ok(request) => {
                        tokio::spawn(async move {
                            let response = invoker.invoke(request).await;
                            response_tx.send(response).unwrap();
                        });
                    }
                    Err(error) => {
                        response_tx
                            .send(Err(Self::from_command_request_builder_error(error)))
                            .unwrap();
                    }
                }
            }
        } else {
            panic!("internal logic error");
        }
    }

    async fn await_invocation(
        action: &TestCaseAction<InvokerDefaults>,
        invocation_chans: &mut HashMap<i32, Option<InvokeResultReceiver>>,
    ) {
        if let TestCaseAction::AwaitInvocation {
            defaults_type: _,
            invocation_index,
            response_value,
            metadata,
            catch,
        } = action
        {
            let response_rx = invocation_chans.get_mut(invocation_index).unwrap();
            let response = response_rx.take().unwrap().await;

            match response {
                Ok(Ok(response)) => {
                    if let Some(catch) = catch {
                        panic!(
                            "Expected error {} but no error returned from awaited command",
                            catch.error_kind
                        );
                    }

                    if let Some(response_value) = response_value {
                        assert_eq!(response_value, &response.payload.payload);
                    }

                    if let Some(metadata) = metadata {
                        for (key, value) in metadata {
                            let found = response.custom_user_data.iter().find(|&k| &k.0 == key);
                            assert_eq!(
                                value,
                                &found.unwrap().1,
                                "metadata key {key} expected {value}"
                            );
                        }
                    }
                }
                Ok(Err(error)) => {
                    if let Some(catch) = catch {
                        aio_protocol_error_checker::check_error(catch, &error);
                    } else {
                        panic!("Unexpected error when awaiting command: {error}");
                    }
                }
                _ => {
                    panic!("unexpected error from command invocation channel");
                }
            }
        } else {
            panic!("internal logic error");
        }
    }

    fn receive_response(
        action: &TestCaseAction<InvokerDefaults>,
        mqtt_hub: &mut MqttHub,
        correlation_ids: &mut HashMap<i32, Option<Bytes>>,
        packet_ids: &mut HashMap<i32, u16>,
        test_case_index: i32,
    ) {
        if let TestCaseAction::ReceiveResponse {
            defaults_type: _,
            topic,
            payload,
            bypass_serialization,
            content_type,
            format_indicator,
            metadata,
            correlation_index,
            qos,
            message_expiry,
            status,
            status_message,
            is_application_error,
            invalid_property_name,
            invalid_property_value,
            packet_index,
        } = action
        {
            let mut user_properties: Vec<(String, String)> = metadata
                .iter()
                .map(|(k, v)| (k.clone(), v.clone()))
                .collect();

            let correlation_data = if let Some(correlation_index) = correlation_index {
                correlation_ids.get(correlation_index).unwrap().clone()
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

            if let Some(status) = status {
                user_properties.push(("__stat".to_string(), status.clone()));
            }

            if let Some(status_message) = status_message {
                user_properties.push(("__stMsg".to_string(), status_message.clone()));
            }

            if let Some(is_application_error) = is_application_error {
                user_properties.push(("__apErr".to_string(), is_application_error.clone()));
            }

            if let Some(invalid_property_name) = invalid_property_name {
                user_properties.push(("__propName".to_string(), invalid_property_name.clone()));
            }

            if let Some(invalid_property_value) = invalid_property_value {
                user_properties.push(("__propVal".to_string(), invalid_property_value.clone()));
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
        action: &TestCaseAction<InvokerDefaults>,
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
        action: &TestCaseAction<InvokerDefaults>,
        mqtt_hub: &mut MqttHub,
        correlation_ids: &mut HashMap<i32, Option<Bytes>>,
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
                correlation_ids.insert(*correlation_index, correlation_id.clone());
            }
        } else {
            panic!("internal logic error");
        }
    }

    async fn sleep(action: &TestCaseAction<InvokerDefaults>) {
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

    fn from_invoker_options_builder_error(
        builder_error: CommandInvokerOptionsBuilderError,
    ) -> AIOProtocolError {
        let property_name = match builder_error {
            CommandInvokerOptionsBuilderError::UninitializedField(field_name) => {
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

    fn from_command_request_builder_error(
        builder_error: CommandRequestBuilderError,
    ) -> AIOProtocolError {
        let property_name = match builder_error {
            CommandRequestBuilderError::UninitializedField(field_name) => {
                Some(field_name.to_string())
            }
            _ => None,
        };

        let mut protocol_error = AIOProtocolError {
            message: None,
            kind: AIOProtocolErrorKind::ArgumentInvalid,
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
