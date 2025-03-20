// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

use std::{env, time::Duration};

use env_logger::Builder;

use azure_iot_operations_mqtt::MqttConnectionSettingsBuilder;
use azure_iot_operations_mqtt::{
    control_packet::QoS,
    session::{Session, SessionExitHandle, SessionManagedClient, SessionOptionsBuilder},
};
use azure_iot_operations_protocol::{
    application::ApplicationContextBuilder,
    common::payload_serialize::{
        DeserializationError, FormatIndicator, PayloadSerialize, SerializedPayload,
    },
    telemetry,
};

// These tests test these happy path scenarios
// - QoS 1 and auto ack off
// - QoS 1 and auto ack on
// - QoS 0 and auto ack off
// - QoS 0 and auto ack on
// - with payload
// - without payload
// - with custom user data
// - without custom user data
// - with cloud event
// - without cloud event
// - Shutdown after subscribed
// - (Shutdown before subscribed, no error  has been added in unit tests, connectivity not needed)
// - (currently not triggerable) None returned on Recv call when expected

// Possible future tests
// - message received on a different topic ignored
// - invalid message ignored
// - topic token scenarios
// - different protocol versions?

/// Create a session, telemetry sender, telemetry receiver, and exit handle for testing
#[allow(clippy::type_complexity)]
fn setup_test<T: PayloadSerialize + std::marker::Send + std::marker::Sync>(
    client_id: &str,
    topic: &str,
    auto_ack: bool,
) -> Result<
    (
        Session,
        telemetry::Sender<T, SessionManagedClient>,
        telemetry::Receiver<T, SessionManagedClient>,
        SessionExitHandle,
    ),
    (),
> {
    let _ = Builder::new()
        .filter_level(log::LevelFilter::max())
        .format_timestamp(None)
        .filter_module("rumqttc", log::LevelFilter::Warn)
        .filter_module("azure_iot_operations", log::LevelFilter::Warn)
        .try_init();
    if env::var("ENABLE_NETWORK_TESTS").is_err() {
        log::warn!("This test is skipped. Set ENABLE_NETWORK_TESTS to run.");
        return Err(());
    }

    let connection_settings = MqttConnectionSettingsBuilder::default()
        .client_id(client_id)
        .hostname("localhost")
        .tcp_port(1883u16)
        .keep_alive(Duration::from_secs(5))
        .clean_start(true)
        .use_tls(false)
        .build()
        .unwrap();
    let session_options = SessionOptionsBuilder::default()
        .connection_settings(connection_settings)
        .build()
        .unwrap();
    let session = Session::new(session_options).unwrap();

    let application_context = ApplicationContextBuilder::default().build().unwrap();

    let sender_options = telemetry::sender::OptionsBuilder::default()
        .topic_pattern(topic)
        .build()
        .unwrap();
    let sender: telemetry::Sender<T, _> = telemetry::Sender::new(
        application_context.clone(),
        session.create_managed_client(),
        sender_options,
    )
    .unwrap();

    let receiver_options = telemetry::receiver::OptionsBuilder::default()
        .topic_pattern(topic)
        .auto_ack(auto_ack)
        .build()
        .unwrap();
    let receiver: telemetry::Receiver<T, _> = telemetry::Receiver::new(
        application_context,
        session.create_managed_client(),
        receiver_options,
    )
    .unwrap();

    let exit_handle: SessionExitHandle = session.create_exit_handle();
    Ok((session, sender, receiver, exit_handle))
}

#[derive(Clone, Debug, Default, PartialEq)]
pub struct EmptyPayload {}
impl PayloadSerialize for EmptyPayload {
    type Error = String;

    fn serialize(self) -> Result<SerializedPayload, String> {
        Ok(SerializedPayload {
            payload: Vec::new(),
            content_type: "application/octet-stream".to_string(),
            format_indicator: FormatIndicator::UnspecifiedBytes,
        })
    }
    fn deserialize(
        _payload: &[u8],
        _content_type: &Option<String>,
        _format_indicator: &FormatIndicator,
    ) -> Result<EmptyPayload, DeserializationError<String>> {
        Ok(EmptyPayload::default())
    }
}

/// Tests basic telemetry send/receive scenario
/// Auto-ack is on, payload is empty, no custom user data, no cloud event
/// Tested for a QoS 0 and a QoS 1 message
#[tokio::test]
async fn telemetry_basic_send_receive_network_tests() {
    let sender_id = "telemetry_basic_send_receive_network_tests-rust";
    let Ok((session, sender, mut telemetry_receiver, exit_handle)) =
        setup_test::<EmptyPayload>(sender_id, "protocol/tests/basic/telemetry", true)
    else {
        // Network tests disabled, skipping tests
        return;
    };
    let monitor = session.create_connection_monitor();

    let test_task = tokio::task::spawn({
        async move {
            // async task to receive telemetry messages on telemetry_receiver
            let receive_telemetry_task = tokio::task::spawn({
                async move {
                    let mut count = 0;
                    // no difference between QoS 0 and QoS 1 for telemetry receiver if auto-ack is true
                    while let Some(Ok((message, ack_token))) = telemetry_receiver.recv().await {
                        count += 1;
                        // if auto-ack is true, this should always be None
                        assert!(ack_token.is_none());

                        // Validate contents of message match expected based on what was sent
                        assert!(telemetry::receiver::CloudEvent::from_telemetry(&message).is_err());
                        assert_eq!(message.payload, EmptyPayload::default());
                        assert!(message.custom_user_data.is_empty());
                        assert_eq!(message.sender_id.unwrap(), sender_id);
                        assert!(message.timestamp.is_some());
                        assert!(message.topic_tokens.is_empty());
                        // stop waiting for more messages after we shouldn't get any more
                        if count == 2 {
                            break;
                        }
                    }

                    // only the 2 expected messages should occur (checks that recv() didn't return None when it shouldn't have)
                    assert_eq!(count, 2);
                    // cleanup should be successful
                    assert!(telemetry_receiver.shutdown().await.is_ok());
                }
            });
            // briefly wait after connection to let receiver subscribe before sending messages
            monitor.connected().await;
            tokio::time::sleep(Duration::from_secs(1)).await;

            // Send QoS 0 message with empty payload
            let message_qos0 = telemetry::sender::MessageBuilder::default()
                .payload(EmptyPayload::default())
                .unwrap()
                .qos(QoS::AtMostOnce)
                .build()
                .unwrap();
            assert!(sender.send(message_qos0).await.is_ok());

            // Send QoS 1 message with empty payload
            let message_qos1 = telemetry::sender::MessageBuilder::default()
                .payload(EmptyPayload::default())
                .unwrap()
                .qos(QoS::AtLeastOnce)
                .build()
                .unwrap();
            assert!(sender.send(message_qos1).await.is_ok());

            // wait for the receive_telemetry_task to finish to ensure any failed asserts are captured.
            assert!(receive_telemetry_task.await.is_ok());

            exit_handle.try_exit().await.unwrap();
        }
    });

    // if an assert fails in the test task, propagate the panic to end the test,
    // while still running the test task and the session to completion on the happy path
    assert!(tokio::try_join!(
        async move { test_task.await.map_err(|e| { e.to_string() }) },
        async move { session.run().await.map_err(|e| { e.to_string() }) }
    )
    .is_ok());
}

#[derive(Clone, Copy, Debug, Default, PartialEq)]
pub struct DataPayload {
    pub external_temperature: f64,
    pub internal_temperature: f64,
}
impl PayloadSerialize for DataPayload {
    type Error = String;
    fn serialize(self) -> Result<SerializedPayload, String> {
        Ok(SerializedPayload {
            payload: format!(
                "{{\"externalTemperature\":{},\"internalTemperature\":{}}}",
                self.external_temperature, self.internal_temperature
            )
            .into(),
            content_type: "application/json".to_string(),
            format_indicator: FormatIndicator::Utf8EncodedCharacterData,
        })
    }
    fn deserialize(
        payload: &[u8],
        content_type: &Option<String>,
        _format_indicator: &FormatIndicator,
    ) -> Result<DataPayload, DeserializationError<String>> {
        if let Some(content_type) = content_type {
            if content_type != "application/json" {
                return Err(DeserializationError::UnsupportedContentType(format!(
                    "Invalid content type: '{content_type:?}'. Must be 'application/json'"
                )));
            }
        }

        let payload = match String::from_utf8(payload.to_vec()) {
            Ok(p) => p,
            Err(e) => {
                return Err(DeserializationError::InvalidPayload(format!(
                    "Error while deserializing telemetry: {e}"
                )))
            }
        };
        let payload = payload.split(',').collect::<Vec<&str>>();

        let external_temperature = match payload[0]
            .trim_start_matches("{\"externalTemperature\":")
            .parse::<f64>()
        {
            Ok(ext_temp) => ext_temp,
            Err(e) => {
                return Err(DeserializationError::InvalidPayload(format!(
                    "Error while deserializing telemetry: {e}"
                )))
            }
        };
        let internal_temperature = match payload[1]
            .trim_start_matches("\"internalTemperature\":")
            .trim_end_matches('}')
            .parse::<f64>()
        {
            Ok(int_temp) => int_temp,
            Err(e) => {
                return Err(DeserializationError::InvalidPayload(format!(
                    "Error while deserializing telemetry: {e}"
                )))
            }
        };

        Ok(DataPayload {
            external_temperature,
            internal_temperature,
        })
    }
}

/// Tests more complex telemetry send/receive scenario
/// Auto-ack is off, payload is not empty, custom user data is present, cloud event is present
/// Tested for a QoS 0 and a QoS 1 message
#[tokio::test]
async fn telemetry_complex_send_receive_network_tests() {
    let topic = "protocol/tests/complex/telemetry";
    let client_id = "telemetry_complex_send_receive_network_tests-rust";
    let Ok((session, sender, mut telemetry_receiver, exit_handle)) =
        setup_test::<DataPayload>(client_id, topic, false)
    else {
        // Network tests disabled, skipping tests
        return;
    };
    let monitor = session.create_connection_monitor();

    let test_payload1 = DataPayload {
        external_temperature: 100.0,
        internal_temperature: 200.0,
    };
    let test_payload2 = DataPayload {
        external_temperature: 300.0,
        internal_temperature: 400.0,
    };
    let test_custom_user_data = vec![
        ("test1".to_string(), "value1".to_string()),
        ("test2".to_string(), "value2".to_string()),
    ];
    let test_cloud_event_source = "aio://test/telemetry";
    let test_cloud_event = telemetry::sender::CloudEventBuilder::default()
        .source(test_cloud_event_source)
        .build()
        .unwrap();

    let test_task = tokio::task::spawn({
        let test_custom_user_data_clone = test_custom_user_data.clone();
        async move {
            // async task to receive telemetry messages on telemetry_receiver
            let receive_telemetry_task = tokio::task::spawn({
                async move {
                    let mut count = 0;
                    // QoS 0 message
                    if let Some(Ok((message, ack_token))) = telemetry_receiver.recv().await {
                        count += 1;
                        // if auto-ack is true and QoS is 0, this should be None
                        assert!(ack_token.is_none());

                        // Validate contents of message match expected based on what was sent
                        let cloud_event =
                            telemetry::receiver::CloudEvent::from_telemetry(&message).unwrap();
                        assert_eq!(message.payload, test_payload1);
                        assert!(test_custom_user_data_clone.iter().all(|(key, value)| {
                            message
                                .custom_user_data
                                .iter()
                                .any(|(test_key, test_value)| {
                                    key == test_key && value == test_value
                                })
                        }));
                        assert_eq!(message.sender_id.unwrap(), client_id);
                        assert!(message.timestamp.is_some());
                        assert_eq!(cloud_event.source, test_cloud_event_source);
                        assert_eq!(
                            cloud_event.spec_version,
                            telemetry::cloud_event::DEFAULT_CLOUD_EVENT_SPEC_VERSION
                        );
                        assert_eq!(
                            cloud_event.event_type,
                            telemetry::cloud_event::DEFAULT_CLOUD_EVENT_EVENT_TYPE
                        );
                        assert_eq!(cloud_event.subject.unwrap(), topic);
                        assert_eq!(cloud_event.data_content_type.unwrap(), "application/json");
                        assert!(cloud_event.time.is_some());
                        assert!(message.topic_tokens.is_empty());
                    }

                    // QoS 1 message
                    if let Some(Ok((message, ack_token))) = telemetry_receiver.recv().await {
                        count += 1;
                        // if auto-ack is true and QoS is 1, this should be Some
                        assert!(ack_token.is_some());

                        // Validate contents of message match expected based on what was sent
                        let cloud_event =
                            telemetry::receiver::CloudEvent::from_telemetry(&message).unwrap();
                        assert_eq!(message.payload, test_payload2);
                        assert!(test_custom_user_data_clone.iter().all(|(key, value)| {
                            message
                                .custom_user_data
                                .iter()
                                .any(|(test_key, test_value)| {
                                    key == test_key && value == test_value
                                })
                        }));
                        assert_eq!(message.sender_id.unwrap(), client_id);
                        assert!(message.timestamp.is_some());
                        assert_eq!(cloud_event.source, test_cloud_event_source);
                        assert_eq!(
                            cloud_event.spec_version,
                            telemetry::cloud_event::DEFAULT_CLOUD_EVENT_SPEC_VERSION
                        );
                        assert_eq!(
                            cloud_event.event_type,
                            telemetry::cloud_event::DEFAULT_CLOUD_EVENT_EVENT_TYPE
                        );
                        assert_eq!(cloud_event.subject.unwrap(), topic);
                        assert_eq!(cloud_event.data_content_type.unwrap(), "application/json");
                        assert!(cloud_event.time.is_some());
                        assert!(message.topic_tokens.is_empty());

                        // need to ack
                        ack_token.unwrap().ack().await.unwrap().await.unwrap();
                    }

                    // only the 2 expected messages should occur (checks that recv() didn't return None when it shouldn't have)
                    assert_eq!(count, 2);
                    // cleanup should be successful
                    assert!(telemetry_receiver.shutdown().await.is_ok());
                }
            });

            // briefly wait after connection to let receiver subscribe before sending messages
            monitor.connected().await;
            tokio::time::sleep(Duration::from_secs(1)).await;

            // Send QoS 0 message with more complex payload, custom user data, and a cloud event
            let message_qos0 = telemetry::sender::MessageBuilder::default()
                .payload(test_payload1)
                .unwrap()
                .custom_user_data(test_custom_user_data.clone())
                .cloud_event(test_cloud_event.clone())
                .qos(QoS::AtMostOnce)
                .build()
                .unwrap();
            assert!(sender.send(message_qos0).await.is_ok());

            // Send QoS 1 message with more complex payload, custom user data, and a cloud event
            let message_qos1 = telemetry::sender::MessageBuilder::default()
                .payload(test_payload2)
                .unwrap()
                .custom_user_data(test_custom_user_data)
                .qos(QoS::AtLeastOnce)
                .cloud_event(test_cloud_event.clone())
                .build()
                .unwrap();
            assert!(sender.send(message_qos1).await.is_ok());

            // wait for the receive_telemetry_task to finish to ensure any failed asserts are captured.
            assert!(receive_telemetry_task.await.is_ok());

            exit_handle.try_exit().await.unwrap();
        }
    });

    // if an assert fails in the test task, propagate the panic to end the test,
    // while still running the test task and the session to completion on the happy path
    assert!(tokio::try_join!(
        async move { test_task.await.map_err(|e| { e.to_string() }) },
        async move { session.run().await.map_err(|e| { e.to_string() }) }
    )
    .is_ok());
}
