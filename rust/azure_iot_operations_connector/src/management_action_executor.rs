// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

//! Traits, types, and implementations for Azure IoT Operations Connector Management Action Executor.

use std::{sync::Arc, time::Duration};

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
    pub(crate) fn new(
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
                log::warn!("Invalid definition for {:?} {e:?}", management_action_ref);
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

    // /// Get the cancellation token that triggers shutdown of this executor
    // pub(crate) fn get_cancellation_token(&self) -> CancellationToken {
    //     self.cancellation_token.clone()
    // }

    /// Get the shutdown notifier that triggers shutdown of this executor
    pub(crate) fn get_shutdown_notifier(&self) -> Arc<Notify> {
        self.shutdown_notifier.clone()
    }

    /// Receive a [`ManagementActionRequest`] or [`None`] if there will be no more requests on this executor.
    ///
    /// Will also subscribe to the request topic if not already subscribed. If this operation fails, it will log an error and retry
    /// after a delay
    pub async fn recv_request(&mut self) -> Option<ManagementActionRequest> {
        // almost want to check for shutdown notifier here first, and then not bias it in the retry loop, or even bias against it in the retry loop

        // scenarios
        // new, subscribe success and then recv request after some time (no loop, returns)
        // new, subscribe fails and retry (loops after exponential delay, calls recv again. Shouldn't ever stop trying? returns after succeeds and first request)
        // already subscribed, recv request after some time (no loop, returns)
        // get shutdown, shutdown success, nothing to drain in queue (first loop shuts down, second loop returns None from .recv)
        // get shutdown, shutdown success, some requests to drain in queue (first loop MUST shut down, subsequent loop returns next request. Next call returns next request. Next call req or none)
        // get shutdown, shutdown fails, nothing to drain in queue. Shutdown retries (first loop MUST shut down, delay and then loop again.)
        // get shutdown, shutdown fails, retry shutdown and some requests to drain in queue (shouldn't be blocked on retrying shutdown)
        //
        // need to make sure that recv() doesn't return None from the fn if the shutdown hasn't successfully completed yet - if I unbias the select, then it could return None before retrying the shutdown again
        // before loop, bias for shutdown notifier
        // if looping, and shutdown was attempted, then bias for try_recv (aka if recv wouldn't return None).
        // This way, it will return, and then the shutdown will be immediately attempted when recv is called next again (implicit retry)
        //
        // so maybe biased tokio select, but if shutdown fails, do a try_recv() and return if it isn't None, otherwise loop again with the bias so we retry shutdown until it succeeds or we stop trying and
        // we can have the fn return None
        // ah poop we don't have try_recv here
        //
        // do shutdown check first outside of select, then in select bias for recv and only return None if shutdown attempts have ended (will only return None if shutdown has been attempted at least once)

        // check for shutdown first
        // if it succeeds, call recv() and return whatever it returns (Some or None)
        // if it returns Some, on subsequent calls, the shutdown notifier won't do anything, and we'll call recv and return Some or None
        // if it fails, call recv() and return Some if it returns Some, otherwise loop again to retry shutdown. In this scenario, recv and notifier will both return immediately

        // // this loop is just for errors subscribing
        // loop {
        //     if is_notified_now(&self.shutdown_notifier) {
        //         // if we go in here, this branch will always return
        //         loop {
        //             match shutdown.await() {
        //                 Ok(_) => {
        //                     return executor.recv().await; // will complete immediately either some or None, can't fail in this case
        //                 },
        //                 Err(e) => {
        //                     shutdown_notifier.notify_one(); // retry shutdown later
        //                     match executor.recv().await { // will complete immediately either some or None, can't fail in this case
        //                         Some(request) => {
        //                             return Some(request)
        //                         },
        //                         None => {
        //                             // delay and loop this if # of attempts isn't too many, otherwise return None
        //                         }
        //                     }
        //                 }
        //             }
        //         }
        //     }

        //     // only going to here if the shutdown hasn't been requested yet
        //     tokio::select! {
        //         request = executor.recv() => {
        //             match request {
        //                 Some(request) => return Some(request),
        //                 None => return None,
        //                 Err(e) => {
        //                     // retry recv to give subscribe another chance after a delay
        //                 }
        //             }
        //         }
        //         () = self.shutdown_notifier.notified() => {
        //             // same as within if statement above??
        //             // and same, if we go in this branch, it will always return
        //         }
        //     }
        // }

        // // this loop is just for errors subscribing to retry
        // let mut delay = Duration::from_millis(50);
        // loop {
        //     if is_notified_now(&self.shutdown_notifier) {
        //         // attempt shutdown until it succeeds or fails too many times, while draining any requests in the meantime
        //         // and returning None once shutdown doesn't need to be attempted anymore and there are no more requests
        //         return self.on_shutdown().await;
        //     }

        //     // only going to here if the shutdown hasn't been requested yet
        //     tokio::select! {
        //         () = self.shutdown_notifier.notified() => {
        //             // if the shutdown request happens, go to the shutdown flow
        //             return self.on_shutdown().await;
        //         },
        //         res = self.executor.recv() => {
        //             match res {
        //                 Some(Ok(request)) => {
        //                     return Some(ManagementActionRequest { request: request })
        //                 },
        //                 Some(Err(e)) => {
        //                     log::error!(
        //                         "Error receiving request for {}: {:?}",
        //                         self.action_ref.name(),
        //                         e
        //                     );
        //                     // Continue waiting for the next request after a delay (means we need to retry subscribe)
        //                 }
        //                 None => {
        //                     // executor has been successfully shutdown and there are no more requests to drain
        //                     return None;
        //                 }
        //             }
        //         }
        //     }
        //     // wait with exponential backoff before retrying subscribe
        //     tokio::time::sleep(delay).await;
        //     delay = delay.saturating_mul(2);
        // }

        let mut subscribe_delay = Duration::from_millis(50);
        let mut shutdown_delay = Duration::from_millis(50);
        // must be a local variable in case there are many requests to drain after shutdown is requested
        let mut shutdown_attempts = 0;
        loop {
            tokio::select! {
                biased; // always check shutdown request first, since it will always drain the next request if there is one
                () = self.shutdown_notifier.notified() => {
                    log::info!("Management action no longer active, shutting down executor for {}", self.action_ref.name());
                    match self.executor.shutdown().await {
                        Ok(_) => {
                            // notify won't be notified anymore, so recv() will be evaluated in select on next loop
                            // and eventually return None when there are no more requests to drain
                            continue;
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
                                Some(Ok(request)) => return Some(ManagementActionRequest { request: request }),
                                Some(Err(_)) | None => {
                                    if !keep_going {
                                        // if we've already tried to shutdown 10 times, Some(Err(_)) isn't really a scenario that should happen,
                                        // but just to avoid getting stuck in an infinite loop, we can return None
                                        return None;
                                    }
                                }
                            }
                        }
                    }
                    // wait with exponential backoff before retrying shutdown
                    tokio::time::sleep(shutdown_delay).await;
                    shutdown_delay = shutdown_delay.saturating_mul(2);
                },
                res = self.executor.recv() => {
                    match res {
                        Some(Ok(request)) => {
                            return Some(ManagementActionRequest { request: request })
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

        // let mut shutdown_completed = false;
        // if is_notified_now(&self.shutdown_notifier) {
        //     log::info!("Management action no longer active, shutting down executor for {}", self.action_ref.name());
        //         // TODO: retry on any failures here? See telemetry receiver shutdown handling in ADR client
        //         match self.executor.shutdown().await {
        //             Ok(_) => {
        //                 shutdown_completed = true;
        //             },
        //             Err(e) => {
        //                 log::warn!("Error shutting down executor for {}: {e:?}", self.action_ref.name());
        //                 // try to shut down again on the next loop iteration
        //                 // TODO: delay?
        //                 if self.shutdown_attempts < 10 {
        //                     self.shutdown_attempts += 1;
        //                     self.shutdown_notifier.notify_one();
        //                 } else {
        //                     log::warn!("Exceeded maximum executor shutdown attempts for {}, giving up", self.action_ref.name());
        //                 }
        //             }
        //         }
        // }
        // loop {
        //     tokio::select! {
        //         biased; // always check shutdown request first
        //         () = self.shutdown_notifier.notified() => {
        //             log::info!("Management action no longer active, shutting down executor for {}", self.action_ref.name());
        //             // TODO: retry on any failures here? See telemetry receiver shutdown handling in ADR client
        //             _ = self.executor.shutdown().await.inspect_err(|e| {
        //                 log::warn!("Error shutting down executor for {}: {e:?}", self.action_ref.name());
        //                 // try to shut down again on the next loop iteration
        //                 // TODO: delay?
        //                 if self.shutdown_attempts < 10 {
        //                     self.shutdown_attempts += 1;
        //                     self.shutdown_notifier.notify_one();
        //                 } else {
        //                     log::warn!("Exceeded maximum executor shutdown attempts for {}, giving up", self.action_ref.name());
        //                 }
        //             })
        //         }
        //         // _ = self.cancellation_token.cancelled() => {
        //         //     log::info!("Management action no longer active, shutting down executor for {}", self.action_ref.name());
        //         //     // TODO: retry on any failures here? See telemetry receiver shutdown handling in ADR client
        //         //     _ = self.executor.shutdown().await;
        //         //     break;
        //         // },
        //         res = self.executor.recv() => {
        //             match res {
        //                 Some(request_result) => {
        //                     match request_result {
        //                         Ok(request) => return Some(ManagementActionRequest { request: request }),
        //                         Err(e) => {
        //                             log::error!(
        //                                 "Error receiving request for {}: {:?}",
        //                                 self.action_ref.name(),
        //                                 e
        //                             );
        //                             // TODO: continue waiting for the next request after a delay (means we need to retry subscribe)
        //                         }
        //                     }
        //                 }
        //                 None => {
        //                     if shutdown_completed {
        //                         return None;
        //                     }
        //                 }
        //             }
        //         }
        //     }
        //     // match self.executor.recv().await? {
        //     //     Ok(request) => return Some(ManagementActionRequest { request: request }),
        //     //     Err(e) => {
        //     //         log::error!(
        //     //             "Error receiving request for {}: {:?}",
        //     //             self.action_ref.name(),
        //     //             e
        //     //         );
        //     //         // TODO: continue waiting for the next request after a delay (means we need to retry subscribe)
        //     //     }
        //     // }
        //     log::info!("ma executor looping");
        //     //     }
        //     // }
        // }
        // loop {
        //     match self.executor.recv().await? {
        //         Ok(request) => return Some(ManagementActionRequest { request: request }),
        //         Err(e) => {
        //             log::error!(
        //                 "Error receiving request for {}: {:?}",
        //                 self.action_ref.name(),
        //                 e
        //             );
        //             // TODO: continue waiting for the next request after a delay (means we need to retry subscribe)
        //         }
        //     }
        //     log::info!("ma executor looping");
        // }
    }

    // async fn on_shutdown(&mut self) -> Option<ManagementActionRequest> {
    //     let mut delay = Duration::from_millis(50);
    //     loop {
    //         log::info!("Management action no longer active, shutting down executor for {}", self.action_ref.name());
    //         match self.executor.shutdown().await {
    //             Ok(_) => {
    //                 // drain next request or return none
    //                 // could just bounce out here and continue in outer fn
    //                 match self.executor.recv().await? {
    //                     Ok(request) => return Some(ManagementActionRequest { request: request }),
    //                     Err(_) => {
    //                         unreachable!(); // shouldn't be possible after shutdown

    //                         // log::error!(
    //                         //     "Error receiving request for {}: {:?}",
    //                         //     self.action_ref.name(),
    //                         //     e
    //                         // );
    //                         // // TODO: continue waiting for the next request after a delay (means we need to retry subscribe)
    //                     }
    //                 }
    //             },
    //             Err(e) => {
    //                 log::warn!("Error shutting down executor for {}: {e:?}", self.action_ref.name());
    //                 // try to shut down again on the next loop iteration
    //                 // TODO: delay?
    //                 let keep_going = self.shutdown_attempts < 10;
    //                 if keep_going {
    //                     self.shutdown_attempts += 1;
    //                     self.shutdown_notifier.notify_one();
    //                 } else {
    //                     log::warn!("Exceeded maximum executor shutdown attempts for {}, giving up", self.action_ref.name());
    //                 }
    //                 // drain next request, or loop if none
    //                 match self.executor.recv().await {
    //                     Some(Ok(request)) => return Some(ManagementActionRequest { request: request }),
    //                     Some(Err(_)) => {
    //                         unreachable!(); // shouldn't be possible after shutdown
    //                         // tbh, if we hit this and we're trying to shutdown, can we just consider this the same as None?
    //                     }
    //                     None => {
    //                         if !keep_going {
    //                             return None;
    //                         }
    //                     }
    //                 }
    //             }
    //         }
    //         // wait with exponential backoff before retrying shutdown
    //         tokio::time::sleep(delay).await;
    //         delay = delay.saturating_mul(2);
    //     }
    // }
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
    // Default is a no cloud event
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
        // TODO: new error type here?
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
