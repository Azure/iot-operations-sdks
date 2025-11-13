// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

use std::collections::HashMap;
use std::convert::TryFrom;
use std::sync::Arc;

use async_std::future;
use azure_iot_operations_mqtt::session::{
    managed_client::SessionManagedClient, session::SessionMonitor,
};
use azure_iot_operations_protocol::application::ApplicationContextBuilder;
use azure_iot_operations_protocol::common::aio_protocol_error::{
    AIOProtocolError, AIOProtocolErrorKind,
};
use azure_iot_operations_protocol::rpc_command;
use bytes::Bytes;
use serde_json;
use tokio::sync::oneshot;
use tokio::time;

use super::mqtt_hub::to_is_utf8;
use crate::metl::aio_protocol_error_checker;
use crate::metl::defaults::{InvokerDefaults, get_invoker_defaults};
use crate::metl::mqtt_hub::MqttHub;
use crate::metl::qos::{self, new_packet_identifier_dup_qos};
use crate::metl::test_case::TestCase;
use crate::metl::test_case_action::TestCaseAction;
use crate::metl::test_case_catch::TestCaseCatch;
use crate::metl::test_case_invoker::TestCaseInvoker;
use crate::metl::test_case_published_message::TestCasePublishedMessage;
use crate::metl::test_case_serializer::TestCaseSerializer;
use crate::metl::test_payload::TestPayload;

const TEST_TIMEOUT: time::Duration = time::Duration::from_secs(10);

type InvokeResultReceiver =
    oneshot::Receiver<Result<rpc_command::invoker::Response<TestPayload>, AIOProtocolError>>;

pub struct CommandInvokerTester {}

impl CommandInvokerTester {
    pub async fn test_command_invoker(
        test_case: TestCase<InvokerDefaults>,
        managed_client: SessionManagedClient,
        session_monitor: SessionMonitor,
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

        // force connack to be first
        mqtt_hub.await_operation().await;
        session_monitor.connected().await;

        let mut invokers: HashMap<String, Arc<rpc_command::Invoker<TestPayload, TestPayload>>> =
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
            )
            .await
            {
                invokers.insert(
                    test_case_invoker.command_name.clone().unwrap(),
                    Arc::new(invoker),
                );
            }
        }

        let test_case_serializer = &test_case.prologue.invokers[0].serializer;

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
                        test_case_serializer,
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
                        test_case_serializer,
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
                Self::check_published_message(published_message, &mqtt_hub, &correlation_ids);
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
        managed_client: SessionManagedClient,
        tci: &TestCaseInvoker<InvokerDefaults>,
        catch: Option<&TestCaseCatch>,
        mqtt_hub: &mut MqttHub,
    ) -> Option<rpc_command::Invoker<TestPayload, TestPayload>> {
        let mut invoker_options_builder = rpc_command::invoker::OptionsBuilder::default();

        if let Some(request_topic) = tci.request_topic.as_ref() {
            invoker_options_builder.request_topic_pattern(request_topic);
        }
        invoker_options_builder.response_topic_pattern(tci.response_topic_pattern.clone());
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

        match rpc_command::Invoker::new(
            ApplicationContextBuilder::default().build().unwrap(),
            managed_client,
            invoker_options,
        ) {
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

                    let mut command_request_builder =
                        rpc_command::invoker::RequestBuilder::default();

                    if let Some(request_value) = default_invoke_command.request_value.clone() {
                        command_request_builder
                            .payload(TestPayload {
                                payload: Some(request_value),
                                out_content_type: tci.serializer.out_content_type.clone(),
                                accept_content_types: tci.serializer.accept_content_types.clone(),
                                indicate_character_data: tci.serializer.indicate_character_data,
                                allow_character_data: tci.serializer.allow_character_data,
                                fail_deserialization: tci.serializer.fail_deserialization,
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
                                catch.error_kind
                            );
                        }
                    }

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
        invokers: &HashMap<String, Arc<rpc_command::Invoker<TestPayload, TestPayload>>>,
        invocation_chans: &mut HashMap<i32, Option<InvokeResultReceiver>>,
        tcs: &TestCaseSerializer<InvokerDefaults>,
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
            let mut command_request_builder = rpc_command::invoker::RequestBuilder::default();

            if let Some(request_value) = request_value {
                command_request_builder
                    .payload(TestPayload {
                        payload: Some(request_value.clone()),
                        out_content_type: tcs.out_content_type.clone(),
                        accept_content_types: tcs.accept_content_types.clone(),
                        indicate_character_data: tcs.indicate_character_data,
                        allow_character_data: tcs.allow_character_data,
                        fail_deserialization: tcs.fail_deserialization,
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
        tcs: &TestCaseSerializer<InvokerDefaults>,
    ) {
        if let TestCaseAction::ReceiveResponse {
            defaults_type: _,
            topic,
            payload,
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
            let mut user_properties: Vec<(
                azure_mqtt::packet::ByteStr<Bytes>,
                azure_mqtt::packet::ByteStr<Bytes>,
            )> = metadata
                .iter()
                .map(|(k, v)| (k.as_str().into(), v.as_str().into()))
                .collect();

            let correlation_data = if let Some(correlation_index) = correlation_index {
                correlation_ids.get(correlation_index).unwrap().clone()
            } else {
                None
            }
            .map(|cd| cd.as_ref().into());

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
                user_properties.push(("__stat".into(), status.as_str().into()));
            }

            if let Some(status_message) = status_message {
                user_properties.push(("__stMsg".into(), status_message.as_str().into()));
            }

            if let Some(is_application_error) = is_application_error {
                user_properties.push(("__apErr".into(), is_application_error.as_str().into()));
            }

            if let Some(invalid_property_name) = invalid_property_name {
                user_properties.push(("__propName".into(), invalid_property_name.as_str().into()));
            }

            if let Some(invalid_property_value) = invalid_property_value {
                user_properties.push(("__propVal".into(), invalid_property_value.as_str().into()));
            }

            let topic = if let Some(topic) = topic {
                topic.clone()
            } else {
                String::new()
            };

            let payload = serde_json::to_vec(&TestPayload {
                payload: payload.clone(),
                out_content_type: tcs.out_content_type.clone(),
                accept_content_types: tcs.accept_content_types.clone(),
                indicate_character_data: tcs.indicate_character_data,
                allow_character_data: tcs.allow_character_data,
                fail_deserialization: tcs.fail_deserialization,
            })
            .unwrap();

            let properties = azure_mqtt::mqtt_proto::PublishOtherProperties {
                payload_is_utf8: to_is_utf8(format_indicator),
                message_expiry_interval,
                correlation_data,
                user_properties,
                content_type: content_type.as_ref().map(|ct| ct.as_str().into()),
                ..Default::default()
            };

            let publish = azure_mqtt::mqtt_proto::Publish {
                packet_identifier_dup_qos: new_packet_identifier_dup_qos(
                    qos::to_enum(*qos),
                    false,
                    packet_id,
                ),
                topic_name: azure_mqtt::mqtt_proto::Topic::new(topic).unwrap().into(),
                payload: payload.into(),
                other_properties: properties,
                retain: false,
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
                correlation_ids.insert(*correlation_index, correlation_id);
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
    ) {
        let published_message: &azure_mqtt::mqtt_proto::Publish<Bytes> =
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
            assert_eq!(topic, published_message.topic_name.as_str(), "topic");
        }

        if let Some(payload) = expected_message.payload.as_ref() {
            if let Some(payload) = payload {
                assert_eq!(published_message.payload, *payload.as_bytes(), "payload");
            } else {
                assert!(published_message.payload.is_empty());
            }
        }

        if let Some(ref expected_content_type) = expected_message.content_type {
            let pub_content_type = published_message
                .other_properties
                .content_type
                .as_ref()
                .unwrap();
            assert_eq!(*pub_content_type, **expected_content_type);
        }

        if expected_message.format_indicator.is_some() {
            assert_eq!(
                to_is_utf8(&expected_message.format_indicator),
                published_message.other_properties.payload_is_utf8
            );
        }

        if !expected_message.metadata.is_empty() {
            for (key, value) in &expected_message.metadata {
                let found = published_message
                    .other_properties
                    .user_properties
                    .iter()
                    .find(|&k| k.0 == **key);
                if let Some(value) = value {
                    assert_eq!(
                        found.unwrap().1,
                        **value,
                        "metadata key {key} expected {value}"
                    );
                } else {
                    assert_eq!(None, found, "metadata key {key} not expected");
                }
            }
        }

        if let Some(command_status) = expected_message.command_status {
            let found = published_message
                .other_properties
                .user_properties
                .iter()
                .find(|&k| k.0 == "__stat");
            if let Some(command_status) = command_status {
                assert_eq!(
                    found.unwrap().1,
                    *command_status.to_string(),
                    "status property expected {command_status}"
                );
            } else {
                assert_eq!(None, found, "status property not expected");
            }
        }

        if let Some(is_application_error) = expected_message.is_application_error {
            let found: Option<_> = published_message
                .other_properties
                .user_properties
                .iter()
                .find(|&k| &k.0 == "__apErr");
            if is_application_error {
                assert!(
                    found.unwrap().1.as_ref().to_lowercase() == "true",
                    "is application error"
                );
            } else {
                assert!(
                    found.is_none() || found.unwrap().1.as_ref().to_lowercase() == "false",
                    "is application error"
                );
            }
        }

        if expected_message.expiry.is_some() {
            assert_eq!(
                expected_message.expiry,
                published_message.other_properties.message_expiry_interval
            );
        }
    }

    fn from_invoker_options_builder_error(
        builder_error: rpc_command::invoker::OptionsBuilderError,
    ) -> AIOProtocolError {
        let property_name = match builder_error {
            rpc_command::invoker::OptionsBuilderError::UninitializedField(field_name) => {
                Some(field_name.to_string())
            }
            _ => None,
        };

        let mut protocol_error = AIOProtocolError {
            message: None,
            kind: AIOProtocolErrorKind::ConfigurationInvalid,
            is_shallow: true,
            is_remote: false,
            nested_error: Some(Box::new(builder_error)),
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
        builder_error: rpc_command::invoker::RequestBuilderError,
    ) -> AIOProtocolError {
        let property_name = match builder_error {
            rpc_command::invoker::RequestBuilderError::UninitializedField(field_name) => {
                Some(field_name.to_string())
            }
            _ => None,
        };

        let mut protocol_error = AIOProtocolError {
            message: None,
            kind: AIOProtocolErrorKind::ConfigurationInvalid,
            is_shallow: true,
            is_remote: false,
            nested_error: Some(Box::new(builder_error)),
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
