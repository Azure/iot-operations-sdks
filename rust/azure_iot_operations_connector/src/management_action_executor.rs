// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

//! Traits, types, and implementations for Azure IoT Operations Connector Management Action Executor.

use std::sync::Arc;

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
use tokio_util::sync::CancellationToken;

use crate::{AdrConfigError, ManagementActionRef, base_connector::ConnectorContext};

pub struct ManagementActionExecutor {
    executor: rpc_command::Executor<BypassPayload, BypassPayload>,
    action_ref: ManagementActionRef,
    cancellation_token: CancellationToken,
}

// /// Represents whether there is currently a valid Executor or not for a Management Action
// pub(crate) enum ActionExecutor {
//     Executor(rpc_command::Executor<BypassPayload, BypassPayload>), // TODO: probably Box this?
//     Error(AdrConfigError),
// }

impl ManagementActionExecutor {
    pub(crate) fn new(
        // definition: &ManagementActionSpecification,
        topic: &Option<String>,
        default_topic: &Option<String>,
        management_action_ref: &ManagementActionRef,
        connector_context: Arc<ConnectorContext>,
    ) -> Result<Self, AdrConfigError> {
        let request_topic_pattern = if let Some(topic) = topic.as_ref().or(default_topic.as_ref()) {
            // TODO: if using default, ensure it has the correct token
            topic.clone()
        } else {
            return Err(AdrConfigError {
                code: None,
                details: None,
                message: Some("Management Group must have default topic if Management Action doesn't have a topic".to_string()),
            });
        };
        let executor_options = rpc_command::executor::OptionsBuilder::default()
            .request_topic_pattern(request_topic_pattern)
            // TODO: handle topic tokens
            .command_name(management_action_ref.command_name())
            .build()
            .expect("Options can't fail if request topic pattern and command name are provided");
        //  {
        //     Ok(options) => options,
        //     Err(e) => {
        //         let err = AdrConfigError {
        //             code: None,
        //             details: Some(vec![Details { info: Some(e.to_string()), ..Default::default()}]),
        //             message: Some(format!("Invalid topic or name for management action")),
        //         };
        //         return (ManagementActionExecutor::Error(err.clone()), Err(err));
        //     }
        // };
        match rpc_command::Executor::new(
            connector_context.application_context.clone(),
            connector_context.managed_client.clone(),
            executor_options,
        ) {
            Ok(executor) => Ok(ManagementActionExecutor {
                executor,
                action_ref: management_action_ref.clone(),
                cancellation_token: CancellationToken::new(),
            }),
            Err(e) => {
                log::warn!(
                    "Invalid definition for management action: {:?} {e:?}",
                    management_action_ref
                );
                Err(AdrConfigError {
                    code: None,
                    details: Some(vec![Details {
                        info: Some(e.to_string()),
                        ..Default::default()
                    }]),
                    message: Some(format!("Invalid topic or name for management action")),
                })
            }
        }
    }

    pub(crate) fn get_cancellation_token(&self) -> CancellationToken {
        self.cancellation_token.clone()
    }

    pub async fn recv_request(&mut self) -> Option<ManagementActionRequest> {
        // TODO: need to sort out how to handle changing executors on updates - allow existing commands to drain still? This means we can't
        // drop the old executor until all of these complete though. Looks like we can shut it down to prevent more requests,
        // but we need to make sure not to drop it to be able to complete in flight requests

        // maybe have the new and update return the ManagementActionExecutor and then we shut it down, but
        // it's the application's responsibility to drain it before polling the new one? And then the cloud event
        // headers would be locked to when the executor was created

        // and actually, this simplifies a lot since recv_request and recv_notification both take &mut self
        // although you wouldn't/couldn't really have two recv_requests be draining/calling at the same time on your select
        // but also you only need a new executor if the topic/default topic has changed, so you'd only return it sometimes?

        // if recv() returns none, this indicates that the management action was deleted

        loop {
            // match &mut self.executor {
            //     ActionExecutor::Error(e) => {
            //         log::error!(
            //             "Management action executor in error state for management action {:?}: {:?}",
            //             self.action_ref,
            //             e
            //         );
            //         // continue waiting for the next request after a delay
            //         tokio::time::sleep(std::time::Duration::from_secs(5)).await;
            //         continue;
            //         // Return None? Indicating no more requests will be received until there's a definition update
            //     }
            //     ActionExecutor::Executor(executor) => {
            tokio::select! {
                biased;
                _ = self.cancellation_token.cancelled() => {
                    log::info!("Management action no longer active, shutting down executor for {}", self.action_ref.name());
                    _ = self.executor.shutdown().await;
                    break;
                },
                res = self.executor.recv() => {
                    match res {
                        Some(request_result) => {
                            match request_result {
                                Ok(request) => return Some(ManagementActionRequest { request: request }),
                                Err(e) => {
                                    log::error!(
                                        "Error receiving request for {}: {:?}",
                                        self.action_ref.name(),
                                        e
                                    );
                                    // TODO: continue waiting for the next request after a delay (means we need to retry subscribe)
                                }
                            }
                        }
                        None => return None
                    }
                }
            }
            // match self.executor.recv().await? {
            //     Ok(request) => return Some(ManagementActionRequest { request: request }),
            //     Err(e) => {
            //         log::error!(
            //             "Error receiving request for {}: {:?}",
            //             self.action_ref.name(),
            //             e
            //         );
            //         // TODO: continue waiting for the next request after a delay (means we need to retry subscribe)
            //     }
            // }
            // log::info!("ma executor looping");
            //     }
            // }
        }
        loop {
            match self.executor.recv().await? {
                Ok(request) => return Some(ManagementActionRequest { request: request }),
                Err(e) => {
                    log::error!(
                        "Error receiving request for {}: {:?}",
                        self.action_ref.name(),
                        e
                    );
                    // TODO: continue waiting for the next request after a delay (means we need to retry subscribe)
                }
            }
            // log::info!("ma executor looping");
        }
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
    pub fn is_cancelled(&self) -> bool {
        self.request.is_cancelled()
    }

    /// Payload of the request.
    pub fn payload(&self) -> &BypassPayload {
        &self.request.payload
    }

    // this will contain ARM correlation ID (likely x-ms-correlation-request-id)
    /// Custom user data set as custom MQTT User Properties on the request message.
    pub fn custom_user_data(&self) -> &Vec<(String, String)> {
        &self.request.custom_user_data
    }

    /// Timestamp of the request.
    pub fn timestamp(&self) -> &Option<HybridLogicalClock> {
        &self.request.timestamp
    }

    /// If present, contains the client ID of the invoker of the request.
    pub fn invoker_id(&self) -> &Option<String> {
        &self.request.invoker_id
    }
}

/// Represents an application error to include in a management action response
#[derive(Builder, Clone, Debug)]
pub struct ManagementActionApplicationError {
    /// Application error code to include in the response headers
    pub application_error_code: String,
    /// Application error payload to include in the response headers
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
    // Default is a Cloud Event aligning to the AIO standards, but it can be overwritten if desired
    cloud_event: Option<Option<ResponseCloudEvent>>,
    /// Whether the execution was successful or not, and any error details to include.
    /// An Err() will be displayed on the calling side if this is set to Err(). The payload can still have any
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

    /// Cloud event for the response.
    // Default is no Cloud Event
    pub fn cloud_event(&mut self, cloud_event: Option<ResponseCloudEvent>) -> &mut Self {
        self.cloud_event = Some(cloud_event);
        self
    }

    /// Add error details about the execution fo the request.
    /// An Err() will be displayed on the calling side if this is set. The payload can still have any
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
        let Some(payload) = &self.payload else {
            return Err(ResponseBuilderError::UninitializedField("payload"));
        };
        let Some(content_type) = &self.content_type else {
            return Err(ResponseBuilderError::UninitializedField("content_type"));
        };
        if let Err(application_error) = &self.application_error {
            application_error_headers(
                &mut self.custom_user_data,
                application_error.application_error_code.clone(),
                application_error.application_error_payload.clone(),
            )
            .map_err(|e| ResponseBuilderError::ValidationError(e))?;
        }
        let cloud_event = match &self.cloud_event {
            Some(cloud_event) => cloud_event.clone(),
            // TODO: should we require this field for now so not specifying it to use automagic one in the future is
            // an additive change?
            None => None,
        };

        let mut inner_builder = rpc_command::executor::ResponseBuilder::default();
        inner_builder
            .payload(BypassPayload {
                payload: payload.clone(),
                content_type: content_type.clone(),
                format_indicator: self.format_indicator,
            })
            .unwrap() // TODO: handle. Can fail if content type is invalid
            .custom_user_data(self.custom_user_data.clone());
        if let Some(cloud_event) = cloud_event {
            inner_builder.cloud_event(cloud_event.clone());
        }
        inner_builder.build()
    }
}

// #[derive(Builder, Clone, Debug)]
// #[builder(setter(into), build_fn(validate = "Self::validate"))]
// pub struct Response<TResp>
// where
//     TResp: PayloadSerialize,
// {
//     /// Payload of the command response.
//     #[builder(setter(custom))]
//     payload: BypassPayload,
//     /// Strongly link `Response` with type `TResp`
//     #[builder(private)]
//     payload_type: PhantomData<TResp>,
//     /// Custom user data set as custom MQTT User Properties on the response message.
//     /// Used to pass additional metadata to the invoker.
//     /// Default is an empty vector.
//     #[builder(default)]
//     custom_user_data: Vec<(String, String)>,
//     /// Cloud event of the response.
//     #[builder(default = "None")]
//     cloud_event: Option<ResponseCloudEvent>,
// }

// impl ManagementActionResponseBuilder {
//     pub fn custom_user_data(&mut self, custom_user_data: Vec<(String, String)>) -> Self {
//         self.inner_builder = self.inner_builder.custom_user_data(custom_user_data);
//         self
//     }
// }
