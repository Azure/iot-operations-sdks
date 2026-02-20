// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

//! Traits, types, and implementations for Azure IoT Operations Connector Management Action Executor.

use std::{collections::HashMap, sync::Arc, time::Duration};

use azure_iot_operations_protocol::{
    common::{
        aio_protocol_error::AIOProtocolError,
        hybrid_logical_clock::HybridLogicalClock,
        payload_serialize::{BypassPayload, FormatIndicator},
    },
    rpc_command::{
        self,
        executor::{ResponseBuilderError, ResponseCloudEvent, application_error_headers},
    },
};
use azure_iot_operations_services::azure_device_registry::Details;
use derive_builder::Builder;
use tokio::sync::Notify;

use crate::{AdrConfigError, ManagementActionRef, base_connector::ConnectorContext};

/// Executor for a Management Action. Used to receive requests.
pub struct ManagementActionExecutor {
    executor: rpc_command::Executor<BypassPayload, BypassPayload>,
    action_ref: ManagementActionRef,
    /// Notify that triggers shutdown of this executor
    shutdown_notifier: Arc<Notify>,
}

impl ManagementActionExecutor {
    /// Initial backoff delay for exponential backoff retries
    const INITIAL_BACKOFF_DELAY: Duration = Duration::from_millis(50);

    pub(crate) fn new(
        request_topic_pattern: String,
        management_action_ref: &ManagementActionRef,
        connector_context: &Arc<ConnectorContext>,
    ) -> Result<Self, AdrConfigError> {
        let executor_options = rpc_command::executor::OptionsBuilder::default()
            .request_topic_pattern(request_topic_pattern)
            .command_name(management_action_ref.command_name())
            .build()
            // note: no topic validation is done as part of this builder, which is why this expect is safe
            .expect("Options can't fail if request topic pattern and command name are provided");

        match rpc_command::Executor::new(
            connector_context.application_context.clone(),
            connector_context.managed_client.clone(),
            executor_options,
        ) {
            Ok(executor) => Ok(ManagementActionExecutor {
                executor,
                action_ref: management_action_ref.clone(),
                shutdown_notifier: Arc::new(Notify::new()),
            }),
            Err(e) => {
                log::warn!("Invalid definition for {management_action_ref:?} {e:?}");
                Err(AdrConfigError {
                    code: None,
                    details: Some(vec![Details {
                        info: Some(e.to_string()),
                        ..Default::default()
                    }]),
                    message: Some("Invalid topic or name for management action".to_string()),
                })
            }
        }
    }

    /// Get the shutdown notifier that triggers shutdown of this executor
    pub(crate) fn get_shutdown_notifier(&self) -> Arc<Notify> {
        self.shutdown_notifier.clone()
    }

    /// Receive a [`ManagementActionRequest`] or [`None`] if there will be no more requests on this executor.
    ///
    /// Will also subscribe to the request topic if not already subscribed. If this operation fails, it will log an error and retry
    /// after a delay
    pub async fn recv_request(&mut self) -> Option<ManagementActionRequest> {
        // Logic/validations:
        // new, subscribe success and then recv request after some time (no loop, returns)
        // new, subscribe fails and retry (loops after exponential delay, calls recv again. Shouldn't ever stop trying. Eventually returns after succeeds and first request)
        // already subscribed, recv request after some time (no loop, returns)
        // get shutdown, shutdown success, nothing to drain in queue (first loop shuts down, second iterations of loop returns None from .recv)
        // get shutdown, shutdown success, some requests to drain in queue (first loop MUST shut down, subsequent iteration of loop returns next request. Next call returns next request or none, etc)
        // get shutdown, shutdown fails, nothing to drain in queue. Shutdown retries (first loop MUST shut down, delay and then loop again)
        // get shutdown, shutdown fails, retry shutdown and some requests to drain in queue (shouldn't be blocked on retrying shutdown)
        //
        // need to make sure that recv() doesn't return None from the fn if the shutdown hasn't successfully completed yet (or tried too many times) - if I unbias the select, then it could return None before retrying the shutdown again
        // this is also why the shutdown flow calls recv() internally if shutdown fails, so that None can be ignored until shutdown is successful

        let mut subscribe_delay = Self::INITIAL_BACKOFF_DELAY;
        let mut shutdown_delay = Self::INITIAL_BACKOFF_DELAY;
        // must be a local variable in case there are many requests to drain after shutdown is requested
        let mut shutdown_attempts = 0;
        loop {
            tokio::select! {
                biased; // always check shutdown request first, since it will always drain the next request if there is one
                () = self.shutdown_notifier.notified() => {
                    log::info!("Management action no longer active, shutting down executor for {}", self.action_ref.name());
                    match self.executor.shutdown().await {
                        Ok(()) => {
                            // notify won't be notified anymore, so recv() will be evaluated in select on next loop
                            // and eventually return None when there are no more requests to drain
                        },
                        Err(e) => {
                            log::warn!("Error shutting down executor for {}: {e:?}", self.action_ref.name());
                            // try to shut down again on the next loop iteration unless we've tried too many times already, in which case, break out of the loop and return None
                            let keep_going = shutdown_attempts < 10;
                            if keep_going {
                                shutdown_attempts += 1;
                                self.shutdown_notifier.notify_one();
                            } else {
                                log::warn!("Exceeded maximum executor shutdown attempts for {}, giving up", self.action_ref.name());
                            }
                            // drain next request, or retry shutting down again if none
                            match self.executor.recv().await {
                                Some(Ok(request)) => return Some(ManagementActionRequest { request }),
                                Some(Err(_)) | None => {
                                    if !keep_going {
                                        // if we've already tried to shutdown 10 times, Some(Err(_)) isn't really a scenario that should happen,
                                        // but just to avoid getting stuck in an infinite loop, we can return None
                                        return None;
                                    }
                                }
                            }
                            // wait with exponential backoff before retrying shutdown
                            tokio::time::sleep(shutdown_delay).await;
                            shutdown_delay = shutdown_delay.saturating_mul(2);
                        }
                    }
                },
                res = self.executor.recv() => {
                    match res {
                        Some(Ok(request)) => {
                            return Some(ManagementActionRequest { request })
                        },
                        Some(Err(e)) => {
                            log::error!(
                                "Error receiving request for {}: {:?}",
                                self.action_ref.name(),
                                e
                            );
                            // Continue waiting for the next request after a delay (means we need to retry subscribe)
                            // wait with exponential backoff before retrying subscribe
                            tokio::time::sleep(subscribe_delay).await;
                            subscribe_delay = subscribe_delay.saturating_mul(2);
                        }
                        None => {
                            // executor has been successfully shutdown and there are no more requests to drain
                            return None;
                        }
                    }
                }
            }
        }
    }
}

pub(crate) fn try_executor_topic_from_management_topics(
    topic: Option<&String>,
    _default_topic: Option<&String>, // keeping this in for if/when we support this field in the future
) -> Result<String, AdrConfigError> {
    if let Some(topic) = topic {
        Ok(topic.clone())
    } else {
        Err(AdrConfigError {
            code: None,
            details: None,
            message: Some("Management Action topic is required".to_string()),
        })
    }
}

/// Represents a received Management Action Request
/// If dropped, will send an error response to the invoker
pub struct ManagementActionRequest {
    pub(crate) request: rpc_command::executor::Request<BypassPayload, BypassPayload>,
}
impl ManagementActionRequest {
    /// Consumes the management action request and attempts to
    /// send the response to the invoker.
    ///
    /// Returns Ok(()) on success, otherwise returns [`AIOProtocolError`].
    ///
    /// # Arguments
    /// * `response` - The [`ManagementActionResponse`] to send.
    ///
    /// # Errors
    ///
    /// [`AIOProtocolError`] of kind [`Timeout`](azure_iot_operations_protocol::common::aio_protocol_error::AIOProtocolErrorKind::Timeout) if the request
    /// has expired.
    ///
    /// [`AIOProtocolError`] of kind [`ClientError`](azure_iot_operations_protocol::common::aio_protocol_error::AIOProtocolErrorKind::ClientError) if the response
    /// acknowledgement returns an error.
    ///
    /// [`AIOProtocolError`] of kind [`Cancellation`](azure_iot_operations_protocol::common::aio_protocol_error::AIOProtocolErrorKind::Cancellation) if the
    /// management action definition that this request was received on is out of date.
    ///
    /// [`AIOProtocolError`] of kind [`InternalLogicError`](azure_iot_operations_protocol::common::aio_protocol_error::AIOProtocolErrorKind::InternalLogicError)
    /// if the response publish completion fails. This should not happen.
    pub async fn complete(
        self,
        response: ManagementActionResponse,
    ) -> Result<(), AIOProtocolError> {
        self.request.complete(response).await
    }

    /// Check if the management action response is no longer expected.
    ///
    /// Returns true if the response is no longer expected, otherwise returns false.
    #[must_use]
    pub fn is_cancelled(&self) -> bool {
        self.request.is_cancelled()
    }

    /// Payload of the request, with the content type and format indicator.
    #[must_use]
    pub fn serialized_payload(&self) -> &BypassPayload {
        &self.request.payload
    }

    /// Raw bytes of the payload of the request.
    #[must_use]
    pub fn raw_payload(&self) -> &[u8] {
        &self.request.payload.payload
    }

    /// Content type of the request payload.
    #[must_use]
    pub fn content_type(&self) -> &String {
        &self.request.payload.content_type
    }

    /// Format indicator of the request payload.
    #[must_use]
    pub fn format_indicator(&self) -> &FormatIndicator {
        &self.request.payload.format_indicator
    }

    // this will contain ARM correlation ID (likely x-ms-correlation-request-id)
    /// Custom user data set as custom MQTT User Properties on the request message.
    #[must_use]
    pub fn custom_user_data(&self) -> &Vec<(String, String)> {
        &self.request.custom_user_data
    }

    /// Timestamp of the request.
    #[must_use]
    pub fn timestamp(&self) -> &Option<HybridLogicalClock> {
        &self.request.timestamp
    }

    /// If present, contains the client ID of the invoker of the request.
    #[must_use]
    pub fn invoker_id(&self) -> &Option<String> {
        &self.request.invoker_id
    }

    /// Resolved static and dynamic topic tokens from the incoming request's topic.
    #[must_use]
    pub fn topic_tokens(&self) -> &HashMap<String, String> {
        &self.request.topic_tokens
    }
}

/// Represents an application error to include in a management action response
#[derive(Builder, Clone, Debug)]
pub struct ManagementActionApplicationError {
    /// Application error code to include in the response headers
    pub application_error_code: String,
    /// Application error payload to include in the response headers. May be an empty string.
    #[builder(default)]
    pub application_error_payload: String,
}

/// Management Action Response struct.
pub type ManagementActionResponse = rpc_command::executor::Response<BypassPayload>;

/// Builder for [`ManagementActionResponse`]
pub struct ManagementActionResponseBuilder {
    /// Payload of the response as a serialized byte vector.
    payload: Option<Vec<u8>>,
    /// Content type of the response payload.
    content_type: Option<String>,
    /// Format indicator of the response payload.
    format_indicator: FormatIndicator,
    /// Custom user data set as custom MQTT User Properties on the response message.
    /// Used to pass additional metadata to the invoker.
    /// Default is an empty vector.
    custom_user_data: Vec<(String, String)>,
    /// Cloud event of the response.
    #[allow(clippy::option_option)]
    // to allow specifying None as a value vs not specifying the field at all in the builder
    cloud_event: Option<Option<ResponseCloudEvent>>,
    /// Whether the execution was successful or not, and any error details to include.
    /// An `Err()` will be displayed on the calling side if this is set to `Err()`. The payload can still have any
    /// additional details for custom logic.
    /// Default is Ok(())
    application_error: Result<(), ManagementActionApplicationError>,
}

impl Default for ManagementActionResponseBuilder {
    fn default() -> Self {
        Self {
            payload: None,
            content_type: None,
            format_indicator: FormatIndicator::default(),
            custom_user_data: Vec::new(),
            cloud_event: None,
            application_error: Ok(()),
        }
    }
}

impl ManagementActionResponseBuilder {
    /// Payload of the response, with the content type and format indicator.
    ///
    /// Either this function or `payload()`, `content_type()`, and `format_indicator()` may be called to set these fields.
    /// Whatever is called last takes precedence.
    pub fn serialized_payload(&mut self, serialized_payload: BypassPayload) -> &mut Self {
        self.payload = Some(serialized_payload.payload);
        self.content_type = Some(serialized_payload.content_type);
        self.format_indicator = serialized_payload.format_indicator;
        self
    }

    /// Payload for the response.
    pub fn payload(&mut self, payload: Vec<u8>) -> &mut Self {
        self.payload = Some(payload);
        self
    }

    /// Content type for the response.
    pub fn content_type(&mut self, content_type: String) -> &mut Self {
        self.content_type = Some(content_type);
        self
    }

    /// Format indicator for the response.
    /// Default is `FormatIndicator::UnspecifiedBytes`.
    pub fn format_indicator(&mut self, format_indicator: FormatIndicator) -> &mut Self {
        self.format_indicator = format_indicator;
        self
    }

    /// Custom user data set as custom MQTT User Properties on the response message.
    /// Used to pass additional metadata to the invoker.
    /// Default is an empty vector.
    pub fn custom_user_data(&mut self, custom_user_data: Vec<(String, String)>) -> &mut Self {
        self.custom_user_data = custom_user_data;
        self
    }

    /// Cloud event for the response, if desired.
    pub fn cloud_event(&mut self, cloud_event: Option<ResponseCloudEvent>) -> &mut Self {
        self.cloud_event = Some(cloud_event);
        self
    }

    /// Add error details about the execution fo the request.
    /// An `Err()` will be displayed on the calling side if this is set. The payload can still have any
    /// additional details for custom logic.
    pub fn application_error(
        &mut self,
        application_error: ManagementActionApplicationError,
    ) -> &mut Self {
        self.application_error = Err(application_error);
        self
    }

    /// Builds a new `ManagementActionResponse`.
    ///
    /// # Errors
    /// If a field is not valid or a required field has not been initialized.
    pub fn build(&mut self) -> Result<ManagementActionResponse, ResponseBuilderError> {
        // TODO: new error type here?
        let Some(payload) = &self.payload else {
            return Err(ResponseBuilderError::UninitializedField("payload"));
        };
        let Some(content_type) = &self.content_type else {
            return Err(ResponseBuilderError::UninitializedField("content_type"));
        };
        let Some(cloud_event) = &self.cloud_event else {
            // requiring this field for now so that not specifying it to use an
            // automagicially created one in the future is an additive change
            return Err(ResponseBuilderError::UninitializedField("cloud_event"));
        };
        if let Err(application_error) = &self.application_error {
            application_error_headers(
                &mut self.custom_user_data,
                application_error.application_error_code.clone(),
                application_error.application_error_payload.clone(),
            )
            .map_err(ResponseBuilderError::ValidationError)?;
        }

        let mut inner_builder = rpc_command::executor::ResponseBuilder::default();
        inner_builder
            .payload(BypassPayload {
                payload: payload.clone(),
                content_type: content_type.clone(),
                format_indicator: self.format_indicator,
            })
            .map_err(|e| ResponseBuilderError::ValidationError(e.to_string()))?
            .custom_user_data(self.custom_user_data.clone());
        if let Some(cloud_event) = cloud_event {
            inner_builder.cloud_event(cloud_event.clone());
        }
        inner_builder.build()
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use azure_iot_operations_protocol::common::payload_serialize::SerializedPayload;
    use test_case::test_case;

    #[test_case(Some("default/topic".to_string()); "default topic defined")]
    #[test_case(None; "default topic not defined")]
    fn test_get_executor_topic(default_topic: Option<String>) {
        let topic = Some("test/topic".to_string());
        assert_eq!(
            try_executor_topic_from_management_topics(topic.as_ref(), default_topic.as_ref())
                .unwrap(),
            "test/topic".to_string()
        );
    }

    #[test_case(Some("default/topic".to_string()); "default topic defined")]
    #[test_case(None; "default topic not defined")]
    fn test_get_executor_topic_no_action_topic(default_topic: Option<String>) {
        let topic = None;
        assert!(
            try_executor_topic_from_management_topics(topic.as_ref(), default_topic.as_ref())
                .is_err()
        );
    }

    #[test]
    fn action_response_uninitialized_fields() {
        let full_default = ManagementActionResponseBuilder::default().build();
        assert!(matches!(
            full_default,
            Err(ResponseBuilderError::UninitializedField(_))
        ));

        let missing_payload = ManagementActionResponseBuilder::default()
            .content_type("application/json".to_string())
            .cloud_event(None)
            .build();
        assert!(matches!(
            missing_payload,
            Err(ResponseBuilderError::UninitializedField(_))
        ));

        let missing_content_type = ManagementActionResponseBuilder::default()
            .payload(vec![])
            .cloud_event(None)
            .build();
        assert!(matches!(
            missing_content_type,
            Err(ResponseBuilderError::UninitializedField(_))
        ));

        let missing_cloud_event = ManagementActionResponseBuilder::default()
            .payload(vec![])
            .content_type("application/json".to_string())
            .build();
        assert!(matches!(
            missing_cloud_event,
            Err(ResponseBuilderError::UninitializedField(_))
        ));
    }

    #[test]
    fn action_response_minimum_fields() {
        let _ = ManagementActionResponseBuilder::default()
            .payload(vec![])
            .content_type("application/octet-stream".to_string())
            .cloud_event(None)
            .build()
            .unwrap();
    }

    #[test]
    fn action_response_maximum_fields() {
        let cloud_event = rpc_command::executor::ResponseCloudEventBuilder::default()
            .source("aio://test/action")
            .build()
            .unwrap();
        let _ = ManagementActionResponseBuilder::default()
            .payload("test payload".as_bytes().to_vec())
            .content_type("application/json".to_string())
            .format_indicator(FormatIndicator::Utf8EncodedCharacterData)
            .custom_user_data(vec![("key".to_string(), "value".to_string())])
            .cloud_event(Some(cloud_event))
            .application_error(ManagementActionApplicationError {
                application_error_code: "ManagementActionInvalidState".to_string(),
                application_error_payload:
                    "The management action is in an invalid state and cannot process requests."
                        .to_string(),
            })
            .build()
            .unwrap();
    }

    #[test]
    fn action_response_maximum_ok() {
        let cloud_event = rpc_command::executor::ResponseCloudEventBuilder::default()
            .source("aio://test/action")
            .build()
            .unwrap();
        let _ = ManagementActionResponseBuilder::default()
            .payload("test payload".as_bytes().to_vec())
            .content_type("application/json".to_string())
            .format_indicator(FormatIndicator::Utf8EncodedCharacterData)
            .custom_user_data(vec![("key".to_string(), "value".to_string())])
            .cloud_event(Some(cloud_event))
            .build()
            .unwrap();
    }

    #[test]
    fn action_response_maximum_ok_serialized() {
        let cloud_event = rpc_command::executor::ResponseCloudEventBuilder::default()
            .source("aio://test/action")
            .build()
            .unwrap();
        let serialized_payload = SerializedPayload {
            payload: "test payload".as_bytes().to_vec(),
            content_type: "application/json".to_string(),
            format_indicator: FormatIndicator::Utf8EncodedCharacterData,
        };
        let _ = ManagementActionResponseBuilder::default()
            .serialized_payload(serialized_payload)
            .custom_user_data(vec![("key".to_string(), "value".to_string())])
            .cloud_event(Some(cloud_event))
            .build()
            .unwrap();
    }

    #[test]
    fn action_response_invalid_error() {
        let invalid_error = ManagementActionResponseBuilder::default()
            .payload(vec![])
            .content_type("application/octet-stream".to_string())
            .cloud_event(None)
            .application_error(ManagementActionApplicationError {
                application_error_code: String::new(),
                application_error_payload: String::new(),
            })
            .build();
        assert!(matches!(
            invalid_error,
            Err(ResponseBuilderError::ValidationError(_))
        ));
    }

    #[test]
    fn action_response_valid_minimal_error() {
        let _ = ManagementActionResponseBuilder::default()
            .payload(vec![])
            .content_type("application/octet-stream".to_string())
            .cloud_event(None)
            .application_error(ManagementActionApplicationError {
                application_error_code: "ManagementActionInvalidState".to_string(),
                application_error_payload: String::new(),
            })
            .build()
            .unwrap();
    }

    #[test]
    fn action_response_invalid_content_type() {
        let invalid_error = ManagementActionResponseBuilder::default()
            .payload(vec![])
            .content_type("application/json\u{0000}".to_string())
            .cloud_event(None)
            .build();
        assert!(matches!(
            invalid_error,
            Err(ResponseBuilderError::ValidationError(_))
        ));
    }

    #[test]
    fn action_response_invalid_cloud_event_content_type() {
        let cloud_event = rpc_command::executor::ResponseCloudEventBuilder::default()
            .source("aio://test/action")
            .build()
            .unwrap();
        let invalid_error = ManagementActionResponseBuilder::default()
            .payload(vec![])
            .content_type("not a valid content type".to_string())
            .cloud_event(Some(cloud_event))
            .build();
        assert!(matches!(
            invalid_error,
            Err(ResponseBuilderError::ValidationError(_))
        ));
    }
}
