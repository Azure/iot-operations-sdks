// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

use std::collections::{hash_map::HashMap, VecDeque};
use std::marker::PhantomData;
use std::str::from_utf8;
use std::sync::Arc;

use async_std::future;
use azure_iot_operations_mqtt::interface::ManagedClient;
use azure_iot_operations_protocol::application::ApplicationContextBuilder;
use azure_iot_operations_protocol::common::aio_protocol_error::{
    AIOProtocolError, AIOProtocolErrorKind,
};
use azure_iot_operations_protocol::telemetry::telemetry_sender::{
    CloudEventBuilder, CloudEventBuilderError, CloudEventSubject, TelemetryMessageBuilder,
    TelemetryMessageBuilderError, TelemetrySender, TelemetrySenderOptionsBuilder,
    TelemetrySenderOptionsBuilderError,
};
use chrono::{DateTime, Utc};
use tokio::sync::oneshot;
use tokio::time;

use crate::metl::aio_protocol_error_checker;
use crate::metl::defaults::{get_sender_defaults, SenderDefaults};
use crate::metl::mqtt_hub::MqttHub;
use crate::metl::qos;
use crate::metl::test_case::TestCase;
use crate::metl::test_case_action::TestCaseAction;
use crate::metl::test_case_catch::TestCaseCatch;
use crate::metl::test_case_published_message::TestCasePublishedMessage;
use crate::metl::test_case_sender::TestCaseSender;
use crate::metl::test_case_serializer::TestCaseSerializer;
use crate::metl::test_payload::TestPayload;

const TEST_TIMEOUT: time::Duration = time::Duration::from_secs(10);

type SendResultReceiver = oneshot::Receiver<Result<(), AIOProtocolError>>;

pub struct TelemetrySenderTester<C>
where
    C: ManagedClient + Clone + Send + Sync + 'static,
    C::PubReceiver: Send + Sync + 'static,
{
    managed_client: PhantomData<C>,
}

impl<'a, C> TelemetrySenderTester<C>
where
    C: ManagedClient + Clone + Send + Sync + 'static,
    C::PubReceiver: Send + Sync + 'static,
{
    pub async fn test_telemetry_sender(
        test_case: TestCase<SenderDefaults>,
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

        let mut senders: HashMap<String, Arc<TelemetrySender<TestPayload, C>>> = HashMap::new();

        let sender_count = test_case.prologue.senders.len();
        let mut ix = 0;
        for test_case_sender in &test_case.prologue.senders {
            ix += 1;
            let catch = if ix == sender_count {
                test_case.prologue.catch.as_ref()
            } else {
                None
            };

            if let Some(sender) = Self::get_telemetry_sender(
                managed_client.clone(),
                test_case_sender,
                catch,
                &mut mqtt_hub,
            )
            .await
            {
                senders.insert(
                    test_case_sender.telemetry_name.clone().unwrap(),
                    Arc::new(sender),
                );
            }
        }

        let test_case_serializer = &test_case.prologue.senders[0].serializer;

        let mut send_chans: VecDeque<SendResultReceiver> = VecDeque::new();

        for test_case_action in &test_case.actions {
            match test_case_action {
                action_send_telemetry @ TestCaseAction::SendTelemetry { .. } => {
                    Self::send_telemetry(
                        action_send_telemetry,
                        &senders,
                        &mut send_chans,
                        test_case_serializer,
                    );
                }
                action_await_send @ TestCaseAction::AwaitSend { .. } => {
                    Self::await_send(action_await_send, &mut send_chans).await;
                }
                action_await_publish @ TestCaseAction::AwaitPublish { .. } => {
                    Self::await_publish(action_await_publish, &mut mqtt_hub).await;
                }
                _action_disconnect @ TestCaseAction::Disconnect { .. } => {
                    Self::disconnect(&mut mqtt_hub);
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

            for (sequence_index, published_message) in
                test_case_epilogue.published_messages.iter().enumerate()
            {
                Self::check_published_message(
                    sequence_index.try_into().unwrap(),
                    published_message,
                    &mqtt_hub,
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

    async fn get_telemetry_sender(
        managed_client: C,
        tcs: &TestCaseSender<SenderDefaults>,
        catch: Option<&TestCaseCatch>,
        mqtt_hub: &mut MqttHub,
    ) -> Option<TelemetrySender<TestPayload, C>> {
        let mut sender_options_builder = TelemetrySenderOptionsBuilder::default();

        if let Some(telemetry_topic) = tcs.telemetry_topic.as_ref() {
            sender_options_builder.topic_pattern(telemetry_topic);
        }

        if let Some(topic_namespace) = tcs.topic_namespace.as_ref() {
            sender_options_builder.topic_namespace(topic_namespace);
        }

        if let Some(topic_token_map) = tcs.topic_token_map.as_ref() {
            sender_options_builder.topic_token_map(topic_token_map.clone());
        }

        let options_result = sender_options_builder.build();
        if let Err(error) = options_result {
            if let Some(catch) = catch {
                aio_protocol_error_checker::check_error(
                    catch,
                    &Self::from_sender_options_builder_error(error),
                );
            } else {
                panic!("Unexpected error when building TelemetrySender options: {error}");
            }

            return None;
        }

        let sender_options = options_result.unwrap();

        match TelemetrySender::new(
            ApplicationContextBuilder::default().build().unwrap(),
            managed_client,
            sender_options,
        ) {
            Ok(sender) => {
                if let Some(catch) = catch {
                    // TelemetrySender has no start method, so if an exception is expected, send may be needed to trigger it.

                    let default_send_telemetry = get_sender_defaults()
                        .as_ref()
                        .unwrap()
                        .actions
                        .as_ref()
                        .unwrap()
                        .send_telemetry
                        .as_ref()
                        .unwrap();

                    let mut telemetry_message_builder = TelemetryMessageBuilder::default();

                    if let Some(telemetry_value) = default_send_telemetry.telemetry_value.clone() {
                        telemetry_message_builder
                            .payload(TestPayload {
                                payload: Some(telemetry_value.clone()),
                                out_content_type: tcs.serializer.out_content_type.clone(),
                                accept_content_types: tcs.serializer.accept_content_types.clone(),
                                indicate_character_data: tcs.serializer.indicate_character_data,
                                allow_character_data: tcs.serializer.allow_character_data,
                                fail_deserialization: tcs.serializer.fail_deserialization,
                            })
                            .unwrap();
                    }

                    if let Some(timeout) = default_send_telemetry.timeout.clone() {
                        telemetry_message_builder.message_expiry(timeout.to_duration());
                    }

                    let request = telemetry_message_builder.build().unwrap();

                    let (send_result, _) = tokio::join!(
                        time::timeout(TEST_TIMEOUT, sender.send(request)),
                        time::timeout(TEST_TIMEOUT, mqtt_hub.await_operation())
                    );

                    match send_result {
                        Ok(Ok(())) => {
                            panic!(
                                "Expected {} error when constructing TelemetrySender but no error returned",
                                catch.error_kind
                            );
                        }
                        Ok(Err(error)) => {
                            aio_protocol_error_checker::check_error(catch, &error);
                        }
                        _ => {
                            panic!(
                                "Expected {} error when calling recv() on TelemetrySender but got timeout instead",
                                catch.error_kind);
                        }
                    };

                    None
                } else {
                    Some(sender)
                }
            }
            Err(error) => {
                if let Some(catch) = catch {
                    aio_protocol_error_checker::check_error(catch, &error);
                    None
                } else {
                    panic!("Unexpected error when constructing TelemetrySender: {error}");
                }
            }
        }
    }

    fn send_telemetry(
        action: &TestCaseAction<SenderDefaults>,
        senders: &'a HashMap<String, Arc<TelemetrySender<TestPayload, C>>>,
        send_chans: &mut VecDeque<SendResultReceiver>,
        tcs: &TestCaseSerializer<SenderDefaults>,
    ) {
        if let TestCaseAction::SendTelemetry {
            defaults_type: _,
            telemetry_name,
            timeout,
            telemetry_value,
            metadata,
            cloud_event,
            qos,
        } = action
        {
            let mut telemetry_message_builder = TelemetryMessageBuilder::default();

            if let Some(telemetry_value) = telemetry_value {
                telemetry_message_builder
                    .payload(TestPayload {
                        payload: Some(telemetry_value.clone()),
                        out_content_type: tcs.out_content_type.clone(),
                        accept_content_types: tcs.accept_content_types.clone(),
                        indicate_character_data: tcs.indicate_character_data,
                        allow_character_data: tcs.allow_character_data,
                        fail_deserialization: tcs.fail_deserialization,
                    })
                    .unwrap();
            }

            telemetry_message_builder.qos(qos::to_enum(*qos));

            if let Some(timeout) = timeout {
                telemetry_message_builder.message_expiry(timeout.to_duration());
            }

            if let Some(metadata) = metadata {
                let mut user_data = Vec::with_capacity(metadata.len());
                for (key, value) in metadata {
                    user_data.push((key.clone(), value.clone()));
                }
                telemetry_message_builder.custom_user_data(user_data);
            }

            if let Some(cloud_event) = cloud_event {
                let mut cloud_event_builder = CloudEventBuilder::default();

                if let Some(source) = &cloud_event.source {
                    cloud_event_builder.source(source);
                }

                if let Some(event_type) = &cloud_event.event_type {
                    cloud_event_builder.event_type(event_type);
                }

                if let Some(spec_version) = &cloud_event.spec_version {
                    cloud_event_builder.spec_version(spec_version);
                }

                if let Some(id) = &cloud_event.id {
                    cloud_event_builder.id(id);
                }

                if let Some(Some(time)) = &cloud_event.time {
                    match DateTime::parse_from_rfc3339(time) {
                        Ok(time) => {
                            cloud_event_builder.time(time.with_timezone(&Utc));
                        }
                        Err(error) => {
                            let (response_tx, response_rx) = oneshot::channel();
                            send_chans.push_back(response_rx);
                            response_tx
                                .send(Err(Self::from_cloud_event_builder_error(
                                    CloudEventBuilderError::ValidationError(error.to_string()),
                                )))
                                .unwrap();
                            return;
                        }
                    }
                }

                if let Some(Some(data_schema)) = &cloud_event.data_schema {
                    cloud_event_builder.data_schema(data_schema.clone());
                }

                if let Some(Some(subject)) = &cloud_event.subject {
                    cloud_event_builder.subject(CloudEventSubject::Custom(subject.to_string()));
                }

                match cloud_event_builder.build() {
                    Ok(cloud_event) => {
                        telemetry_message_builder.cloud_event(cloud_event);
                    }
                    Err(error) => {
                        let (response_tx, response_rx) = oneshot::channel();
                        send_chans.push_back(response_rx);
                        response_tx
                            .send(Err(Self::from_cloud_event_builder_error(error)))
                            .unwrap();
                        return;
                    }
                }
            }

            if let Some(telemetry_name) = &telemetry_name {
                let sender = senders[telemetry_name].clone();
                let (response_tx, response_rx) = oneshot::channel();
                send_chans.push_back(response_rx);
                match telemetry_message_builder.build() {
                    Ok(request) => {
                        tokio::spawn(async move {
                            let response = sender.send(request).await;
                            response_tx.send(response).unwrap();
                        });
                    }
                    Err(error) => {
                        response_tx
                            .send(Err(Self::from_telemetry_message_builder_error(error)))
                            .unwrap();
                    }
                }
            }
        } else {
            panic!("internal logic error");
        }
    }

    async fn await_send(
        action: &TestCaseAction<SenderDefaults>,
        send_chans: &mut VecDeque<SendResultReceiver>,
    ) {
        if let TestCaseAction::AwaitSend {
            defaults_type: _,
            catch,
        } = action
        {
            let response_rx = send_chans.pop_front().unwrap();
            let response = response_rx.await;

            match response {
                Ok(Ok(())) => {
                    if let Some(catch) = catch {
                        panic!(
                            "Expected error {} but no error returned from awaited send",
                            catch.error_kind
                        );
                    }
                }
                Ok(Err(error)) => {
                    if let Some(catch) = catch {
                        aio_protocol_error_checker::check_error(catch, &error);
                    } else {
                        panic!("Unexpected error when awaiting send: {error}");
                    }
                }
                _ => {
                    panic!("unexpected error from send channel");
                }
            }
        } else {
            panic!("internal logic error");
        }
    }

    async fn await_publish(action: &TestCaseAction<SenderDefaults>, mqtt_hub: &mut MqttHub) {
        if let TestCaseAction::AwaitPublish {
            defaults_type: _,
            correlation_index: _,
        } = action
        {
            future::timeout(TEST_TIMEOUT, mqtt_hub.await_publish())
                .await
                .expect("test timeout in await_publish");
        } else {
            panic!("internal logic error");
        }
    }

    fn disconnect(mqtt_hub: &mut MqttHub) {
        mqtt_hub.disconnect();
    }

    fn check_published_message(
        sequence_index: i32,
        expected_message: &TestCasePublishedMessage,
        mqtt_hub: &MqttHub,
    ) {
        let published_message = mqtt_hub
            .get_sequentially_published_message(sequence_index)
            .expect("no message published with sequence index {sequence_index}");

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
                assert_eq!(
                    payload,
                    from_utf8(published_message.payload.to_vec().as_slice())
                        .expect("could not process published payload topic as UTF8"),
                    "payload"
                );
            } else {
                assert!(published_message.payload.is_empty());
            }
        }

        if expected_message.content_type.is_some() {
            if let Some(properties) = published_message.properties.as_ref() {
                assert_eq!(expected_message.content_type, properties.content_type);
            } else {
                panic!("expected content type but found no properties in published message");
            }
        }

        if expected_message.format_indicator.is_some() {
            if let Some(properties) = published_message.properties.as_ref() {
                assert_eq!(
                    expected_message.format_indicator,
                    properties.payload_format_indicator
                );
            } else {
                panic!("expected format indicator but found no properties in published message");
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

    fn from_sender_options_builder_error(
        builder_error: TelemetrySenderOptionsBuilderError,
    ) -> AIOProtocolError {
        let property_name = match builder_error {
            TelemetrySenderOptionsBuilderError::UninitializedField(field_name) => {
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

    fn from_cloud_event_builder_error(builder_error: CloudEventBuilderError) -> AIOProtocolError {
        let property_name = match builder_error {
            CloudEventBuilderError::UninitializedField(field_name) => Some(field_name.to_string()),
            _ => Some("cloud_event".to_string()),
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

    fn from_telemetry_message_builder_error(
        builder_error: TelemetryMessageBuilderError,
    ) -> AIOProtocolError {
        let property_name = match builder_error {
            TelemetryMessageBuilderError::UninitializedField(field_name) => {
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
