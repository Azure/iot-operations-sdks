// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

use std::{env, time::Duration};

use env_logger::Builder;

use azure_iot_operations_mqtt::MqttConnectionSettingsBuilder;
use azure_iot_operations_mqtt::session::{
    Session, SessionExitHandle, SessionManagedClient, SessionOptionsBuilder,
};
use azure_iot_operations_protocol::application::ApplicationContextBuilder;
use azure_iot_operations_protocol::{
    common::payload_serialize::{
        DeserializationError, FormatIndicator, PayloadSerialize, SerializedPayload,
    },
    rpc_command,
};

// These tests test these happy path scenarios
// - request with payload
// - request without payload
// - request with custom user data
// - request without custom user data
// - response with payload
// - response without payload
// - response with custom user data
// - response without custom user data
// - TODO: different errors received from the executor (invoker only)
// - Executor shutdown after subscribed
// - (Executor shutdown before subscribed (no error) has been added in unit tests, connectivity not needed)

/// Create a session, command invoker, command executor, and exit handle for testing
#[allow(clippy::type_complexity)]
fn setup_test<
    TReq: PayloadSerialize + std::marker::Send,
    TResp: PayloadSerialize + std::marker::Send,
>(
    client_id: &str,
    topic: &str,
) -> Result<
    (
        Session,
        rpc_command::Invoker<TReq, TResp, SessionManagedClient>,
        rpc_command::Executor<TReq, TResp, SessionManagedClient>,
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
        .hostname("172.22.0.3")
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

    let invoker_options = rpc_command::invoker::OptionsBuilder::default()
        .request_topic_pattern(topic)
        .response_topic_prefix("response".to_string())
        .command_name(client_id)
        .build()
        .unwrap();
    let invoker: rpc_command::Invoker<TReq, TResp, _> = rpc_command::Invoker::new(
        application_context.clone(),
        session.create_managed_client(),
        invoker_options,
    )
    .unwrap();

    let executor_options = rpc_command::executor::OptionsBuilder::default()
        .request_topic_pattern(topic)
        .command_name(client_id)
        .build()
        .unwrap();
    let executor: rpc_command::Executor<TReq, TResp, _> = rpc_command::Executor::new(
        application_context,
        session.create_managed_client(),
        executor_options,
    )
    .unwrap();

    let exit_handle: SessionExitHandle = session.create_exit_handle();
    Ok((session, invoker, executor, exit_handle))
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
        _content_type: Option<&String>,
        _format_indicator: &FormatIndicator,
    ) -> Result<EmptyPayload, DeserializationError<String>> {
        Ok(EmptyPayload::default())
    }
}

/// Tests basic command invoke/response scenario
/// Payloads are empty, no custom user data
#[tokio::test]
async fn command_basic_invoke_response_network_tests() {
    let invoker_id = "command_basic_invoke_response_network_tests-rust";
    let Ok((session, invoker, mut executor, exit_handle)) =
        setup_test::<EmptyPayload, EmptyPayload>(invoker_id, "protocol/tests/basic/command")
    else {
        // Network tests disabled, skipping tests
        return;
    };
    let monitor = session.create_connection_monitor();

    let test_task = tokio::task::spawn({
        async move {
            // async task to receive command requests on executor
            let receive_requests_task = tokio::task::spawn({
                async move {
                    let mut count = 0;
                    if let Some(Ok(request)) = executor.recv().await {
                        count += 1;

                        // Validate contents of the request match expected based on what was sent
                        assert_eq!(request.payload, EmptyPayload::default());
                        assert!(request.custom_user_data.is_empty());
                        assert!(request.timestamp.is_some());
                        assert_eq!(request.invoker_id, Some(String::from(invoker_id)));
                        assert!(request.topic_tokens.is_empty());

                        // send response
                        let response = rpc_command::executor::ResponseBuilder::default()
                            .payload(EmptyPayload::default())
                            .unwrap()
                            .build()
                            .unwrap();
                        assert!(request.complete(response).await.is_ok());
                    }

                    // only the 1 expected request should occur (checks that recv() didn't return None when it shouldn't have)
                    assert_eq!(count, 1);
                    // cleanup should be successful
                    assert!(executor.shutdown().await.is_ok());
                }
            });
            // briefly wait after connection to let executor subscribe before sending requests
            monitor.connected().await;
            tokio::time::sleep(Duration::from_secs(1)).await;

            // Send request with empty payload
            let request = rpc_command::invoker::RequestBuilder::default()
                .payload(EmptyPayload::default())
                .unwrap()
                .timeout(Duration::from_secs(2))
                .build()
                .unwrap();
            let result = invoker.invoke(request).await;
            // Validate contents of the response match expected based on what was sent
            assert!(result.is_ok(), "result: {result:?}");
            let response = result.unwrap();
            assert_eq!(response.payload, EmptyPayload::default());
            assert!(response.custom_user_data.is_empty());
            assert!(response.timestamp.is_some());

            // wait for the receive_requests_task to finish to ensure any failed asserts are captured.
            assert!(receive_requests_task.await.is_ok());

            // cleanup should be successful
            assert!(invoker.shutdown().await.is_ok());

            exit_handle.try_exit().await.unwrap();
        }
    });

    // if an assert fails in the test task, propagate the panic to end the test,
    // while still running the test task and the session to completion on the happy path
    assert!(
        tokio::try_join!(
            async move { test_task.await.map_err(|e| { e.to_string() }) },
            async move { session.run().await.map_err(|e| { e.to_string() }) }
        )
        .is_ok()
    );
}

/// Tests application error code and payload headers
#[tokio::test]
async fn command_response_apperrorcode_and_apperrorpayload_network_tests() {
    let invoker_id = "command_response_apperrorcode_and_apperrorpayload_network_tests-rust";
    let Ok((session, invoker, mut executor, exit_handle)) =
        setup_test::<EmptyPayload, EmptyPayload>(invoker_id, "protocol/tests/apperror/command")
    else {
        // Network tests disabled, skipping tests
        return;
    };
    let monitor = session.create_connection_monitor();

    let test_task = tokio::task::spawn({
        async move {
            // async task to receive command requests on executor
            let receive_requests_task = tokio::task::spawn({
                async move {
                    let mut count = 0;
                    if let Some(Ok(request)) = executor.recv().await {
                        count += 1;

                        // Validate contents of the request match expected based on what was sent
                        assert_eq!(request.invoker_id, Some(String::from(invoker_id)));

                        // send response
                        let mut response = rpc_command::executor::ResponseBuilder::default()
                            .payload(EmptyPayload::default())
                            .unwrap()
                            .build()
                            .unwrap();
                        assert!(
                            rpc_command::executor::ResponseBuilder::add_application_error_headers(
                                &mut response,
                                "345".into(),
                                "Failed543".into()
                            )
                            .is_ok()
                        );
                        assert!(request.complete(response).await.is_ok());
                    }

                    // only the 1 expected request should occur (checks that recv() didn't return None when it shouldn't have)
                    assert_eq!(count, 1);
                    // cleanup should be successful
                    assert!(executor.shutdown().await.is_ok());
                }
            });
            // briefly wait after connection to let executor subscribe before sending requests
            monitor.connected().await;
            tokio::time::sleep(Duration::from_secs(1)).await;

            // Send request with empty payload
            let request = rpc_command::invoker::RequestBuilder::default()
                .payload(EmptyPayload::default())
                .unwrap()
                .timeout(Duration::from_secs(2))
                .build()
                .unwrap();

            let result = invoker.invoke(request).await;
            // Validate contents of the response match expected based on what was sent
            assert!(result.is_ok(), "result: {result:?}");
            let response = result.unwrap();
            assert_eq!(response.custom_user_data.len(), 2);

            let mut app_err_code_header_count = 0;
            let mut app_err_payload_header_count = 0;
            for (key, value) in response.custom_user_data {
                if key == "AppErrCode" {
                    assert_eq!(value, "345");
                    app_err_code_header_count += 1;
                }

                if key == "AppErrPayload" {
                    assert_eq!(value, "Failed543");
                    app_err_payload_header_count += 1;
                }
            }
            assert_eq!(app_err_code_header_count, 1);
            assert_eq!(app_err_payload_header_count, 1);

            // wait for the receive_requests_task to finish to ensure any failed asserts are captured.
            assert!(receive_requests_task.await.is_ok());

            // cleanup should be successful
            assert!(invoker.shutdown().await.is_ok());

            exit_handle.try_exit().await.unwrap();
        }
    });

    // if an assert fails in the test task, propagate the panic to end the test,
    // while still running the test task and the session to completion on the happy path
    assert!(
        tokio::try_join!(
            async move { test_task.await.map_err(|e| { e.to_string() }) },
            async move { session.run().await.map_err(|e| { e.to_string() }) }
        )
        .is_ok()
    );
}

/// Tests application error code header only, without payload
#[tokio::test]
async fn command_response_apperrorcode_no_apperrorpayload_network_tests() {
    let invoker_id = "command_response_apperrorcode_no_apperrorpayload_network_tests-rust";
    let Ok((session, invoker, mut executor, exit_handle)) = setup_test::<EmptyPayload, EmptyPayload>(
        invoker_id,
        "protocol/tests/apperrorcodeonly/command",
    ) else {
        // Network tests disabled, skipping tests
        return;
    };
    let monitor = session.create_connection_monitor();

    let test_task = tokio::task::spawn({
        async move {
            // async task to receive command requests on executor
            let receive_requests_task = tokio::task::spawn({
                async move {
                    let mut count = 0;
                    if let Some(Ok(request)) = executor.recv().await {
                        count += 1;

                        // Validate contents of the request match expected based on what was sent
                        assert_eq!(request.invoker_id, Some(String::from(invoker_id)));

                        // send response
                        let mut response = rpc_command::executor::ResponseBuilder::default()
                            .payload(EmptyPayload::default())
                            .unwrap()
                            .build()
                            .unwrap();
                        assert!(
                            rpc_command::executor::ResponseBuilder::add_application_error_headers(
                                &mut response,
                                "345".into(),
                                "".into()
                            )
                            .is_ok()
                        );
                        assert!(request.complete(response).await.is_ok());
                    }

                    // only the 1 expected request should occur (checks that recv() didn't return None when it shouldn't have)
                    assert_eq!(count, 1);
                    // cleanup should be successful
                    assert!(executor.shutdown().await.is_ok());
                }
            });
            // briefly wait after connection to let executor subscribe before sending requests
            monitor.connected().await;
            tokio::time::sleep(Duration::from_secs(1)).await;

            // Send request with empty payload
            let request = rpc_command::invoker::RequestBuilder::default()
                .payload(EmptyPayload::default())
                .unwrap()
                .timeout(Duration::from_secs(2))
                .build()
                .unwrap();

            let result = invoker.invoke(request).await;
            // Validate contents of the response match expected based on what was sent
            assert!(result.is_ok(), "result: {result:?}");
            let response = result.unwrap();

            assert_eq!(response.custom_user_data.len(), 1);

            let mut app_err_code_header_count = 0;
            let mut app_err_payload_header_count = 0;
            for (key, value) in response.custom_user_data {
                if key == "AppErrCode" {
                    assert_eq!(value, "345");
                    app_err_code_header_count += 1;
                }

                if key == "AppErrPayload" {
                    app_err_payload_header_count += 1;
                }
            }

            assert_eq!(app_err_code_header_count, 1);
            assert_eq!(app_err_payload_header_count, 0);

            // wait for the receive_requests_task to finish to ensure any failed asserts are captured.
            assert!(receive_requests_task.await.is_ok());

            // cleanup should be successful
            assert!(invoker.shutdown().await.is_ok());

            exit_handle.try_exit().await.unwrap();
        }
    });

    // if an assert fails in the test task, propagate the panic to end the test,
    // while still running the test task and the session to completion on the happy path
    assert!(
        tokio::try_join!(
            async move { test_task.await.map_err(|e| { e.to_string() }) },
            async move { session.run().await.map_err(|e| { e.to_string() }) }
        )
        .is_ok()
    );
}

#[derive(Clone, Debug, Default, PartialEq)]
pub struct DataRequestPayload {
    pub requested_temperature: f64,
    pub requested_color: String,
}
impl PayloadSerialize for DataRequestPayload {
    type Error = String;
    fn serialize(self) -> Result<SerializedPayload, String> {
        Ok(SerializedPayload {
            payload: format!(
                "{{\"requestedTemperature\":{},\"requestedColor\":{}}}",
                self.requested_temperature, self.requested_color
            )
            .into(),
            content_type: "application/json".to_string(),
            format_indicator: FormatIndicator::Utf8EncodedCharacterData,
        })
    }
    fn deserialize(
        payload: &[u8],
        content_type: Option<&String>,
        _format_indicator: &FormatIndicator,
    ) -> Result<DataRequestPayload, DeserializationError<String>> {
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
                    "Error while deserializing request: {e}"
                )));
            }
        };
        let payload = payload.split(',').collect::<Vec<&str>>();

        let requested_temperature = match payload[0]
            .trim_start_matches("{\"requestedTemperature\":")
            .parse::<f64>()
        {
            Ok(req_temp) => req_temp,
            Err(e) => {
                return Err(DeserializationError::InvalidPayload(format!(
                    "Error while deserializing request: {e}"
                )));
            }
        };
        let requested_color = payload[1]
            .trim_start_matches("\"requestedColor\":")
            .trim_end_matches('}')
            .to_string();

        Ok(DataRequestPayload {
            requested_temperature,
            requested_color,
        })
    }
}

#[derive(Clone, Debug, Default, PartialEq)]
pub struct DataResponsePayload {
    pub old_temperature: f64,
    pub old_color: String,
    pub minutes_to_change: u32,
}
impl PayloadSerialize for DataResponsePayload {
    type Error = String;
    fn serialize(self) -> Result<SerializedPayload, String> {
        Ok(SerializedPayload {
            payload: format!(
                "{{\"oldTemperature\":{},\"oldColor\":{},\"minutesToChange\":{}}}",
                self.old_temperature, self.old_color, self.minutes_to_change
            )
            .into(),
            content_type: "application/something".to_string(),
            format_indicator: FormatIndicator::UnspecifiedBytes,
        })
    }
    fn deserialize(
        payload: &[u8],
        content_type: Option<&String>,
        _format_indicator: &FormatIndicator,
    ) -> Result<DataResponsePayload, DeserializationError<String>> {
        if let Some(content_type) = content_type {
            if content_type != "application/something" {
                return Err(DeserializationError::UnsupportedContentType(format!(
                    "Invalid content type: '{content_type:?}'. Must be 'application/something'"
                )));
            }
        }
        let payload = match String::from_utf8(payload.to_vec()) {
            Ok(p) => p,
            Err(e) => {
                return Err(DeserializationError::InvalidPayload(format!(
                    "Error while deserializing response: {e}"
                )));
            }
        };
        let payload = payload.split(',').collect::<Vec<&str>>();

        let old_temperature = match payload[0]
            .trim_start_matches("{\"oldTemperature\":")
            .parse::<f64>()
        {
            Ok(old_temp) => old_temp,
            Err(e) => {
                return Err(DeserializationError::InvalidPayload(format!(
                    "Error while deserializing response: {e}"
                )));
            }
        };
        let old_color = payload[1].trim_start_matches("\"oldColor\":").to_string();

        let minutes_to_change = match payload[2]
            .trim_start_matches("\"minutesToChange\":")
            .trim_end_matches('}')
            .parse::<u32>()
        {
            Ok(min) => min,
            Err(e) => {
                return Err(DeserializationError::InvalidPayload(format!(
                    "Error while deserializing response: {e}"
                )));
            }
        };

        Ok(DataResponsePayload {
            old_temperature,
            old_color,
            minutes_to_change,
        })
    }
}

/// Tests more complex command invoke/response scenario
/// Payloads are not empty and custom user data is present
#[tokio::test]
async fn command_complex_invoke_response_network_tests() {
    let invoker_id = "command_complex_invoke_response_network_tests-rust";
    let Ok((session, invoker, mut executor, exit_handle)) =
        setup_test::<DataRequestPayload, DataResponsePayload>(
            invoker_id,
            "protocol/tests/complex/command",
        )
    else {
        // Network tests disabled, skipping tests
        return;
    };
    let monitor = session.create_connection_monitor();

    let test_request_payload = DataRequestPayload {
        requested_temperature: 78.0,
        requested_color: "blue".to_string(),
    };
    let test_response_payload = DataResponsePayload {
        old_temperature: 72.0,
        old_color: "red".to_string(),
        minutes_to_change: 30,
    };
    let test_request_custom_user_data = vec![
        ("test1".to_string(), "value1".to_string()),
        ("test2".to_string(), "value2".to_string()),
    ];
    let test_response_custom_user_data = vec![
        ("test3".to_string(), "value3".to_string()),
        ("test4".to_string(), "value4".to_string()),
    ];

    let test_task = tokio::task::spawn({
        let test_request_custom_user_data_clone = test_request_custom_user_data.clone();
        let test_response_custom_user_data_clone = test_response_custom_user_data.clone();
        let test_request_payload_clone = test_request_payload.clone();
        let test_response_payload_clone: DataResponsePayload = test_response_payload.clone();
        async move {
            // async task to receive command requests on executor
            let receive_requests_task = tokio::task::spawn({
                async move {
                    let mut count = 0;
                    if let Some(Ok(request)) = executor.recv().await {
                        count += 1;

                        // Validate contents of the request match expected based on what was sent
                        assert_eq!(request.payload, test_request_payload_clone);
                        assert_eq!(
                            request.custom_user_data,
                            test_request_custom_user_data_clone
                        );
                        assert!(request.timestamp.is_some());
                        assert_eq!(request.invoker_id, Some(String::from(invoker_id)));
                        assert!(request.topic_tokens.is_empty());

                        // send response
                        let response = rpc_command::executor::ResponseBuilder::default()
                            .payload(test_response_payload_clone)
                            .unwrap()
                            .custom_user_data(test_response_custom_user_data_clone)
                            .build()
                            .unwrap();
                        assert!(request.complete(response).await.is_ok());
                    }

                    // only the 1 expected request should occur (checks that recv() didn't return None when it shouldn't have)
                    assert_eq!(count, 1);
                    // cleanup should be successful
                    assert!(executor.shutdown().await.is_ok());
                }
            });

            // briefly wait after connection to let executor subscribe before sending requests
            monitor.connected().await;
            tokio::time::sleep(Duration::from_secs(1)).await;

            // Send request with more complex payload and custom user data
            let request = rpc_command::invoker::RequestBuilder::default()
                .payload(test_request_payload)
                .unwrap()
                .custom_user_data(test_request_custom_user_data.clone())
                .timeout(Duration::from_secs(2))
                .build()
                .unwrap();
            let result = invoker.invoke(request).await;
            // Validate contents of the response match expected based on what was sent
            assert!(result.is_ok(), "result: {result:?}");
            let response = result.unwrap();
            assert_eq!(response.payload, test_response_payload);
            assert_eq!(response.custom_user_data, test_response_custom_user_data);
            assert!(response.timestamp.is_some());

            // wait for the receive_requests_task to finish to ensure any failed asserts are captured.
            assert!(receive_requests_task.await.is_ok());

            // cleanup should be successful
            assert!(invoker.shutdown().await.is_ok());

            exit_handle.try_exit().await.unwrap();
        }
    });

    // if an assert fails in the test task, propagate the panic to end the test,
    // while still running the test task and the session to completion on the happy path
    assert!(
        tokio::try_join!(
            async move { test_task.await.map_err(|e| { e.to_string() }) },
            async move { session.run().await.map_err(|e| { e.to_string() }) }
        )
        .is_ok()
    );
}
