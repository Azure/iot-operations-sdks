/* Code generated by Azure.Iot.Operations.ProtocolCompiler v0.6.0.0; DO NOT EDIT. */

use std::collections::HashMap;

use azure_iot_operations_mqtt::interface::ManagedClient;
use azure_iot_operations_protocol::common::aio_protocol_error::AIOProtocolError;
use azure_iot_operations_protocol::common::payload_serialize::PayloadSerialize;
use azure_iot_operations_protocol::rpc::command_executor::{
    CommandExecutor, CommandExecutorOptionsBuilder, CommandRequest, CommandResponse,
    CommandResponseBuilder, CommandResponseBuilderError,
};
use azure_iot_operations_protocol::ApplicationContext;

use super::super::common_types::common_options::CommandOptions;
use super::increment_request_payload::IncrementRequestPayload;
use super::increment_response_payload::IncrementResponsePayload;
use super::MODEL_ID;
use super::REQUEST_TOPIC_PATTERN;

pub type IncrementRequest = CommandRequest<IncrementRequestPayload, IncrementResponsePayload>;
pub type IncrementResponse = CommandResponse<IncrementResponsePayload>;
pub type IncrementResponseBuilderError = CommandResponseBuilderError;

/// Builder for [`IncrementResponse`]
#[derive(Default)]
pub struct IncrementResponseBuilder {
    inner_builder: CommandResponseBuilder<IncrementResponsePayload>,
}

impl IncrementResponseBuilder {
    /// Custom user data to set on the response
    pub fn custom_user_data(&mut self, custom_user_data: Vec<(String, String)>) -> &mut Self {
        self.inner_builder.custom_user_data(custom_user_data);
        self
    }

    /// Payload of the response
    ///
    /// # Errors
    /// If the payload cannot be serialized
    pub fn payload(
        &mut self,
        payload: IncrementResponsePayload,
    ) -> Result<&mut Self, <IncrementResponsePayload as PayloadSerialize>::Error> {
        self.inner_builder.payload(payload)?;
        Ok(self)
    }

    /// Builds a new `IncrementResponse`
    ///
    /// # Errors
    /// If a required field has not been initialized
    #[allow(clippy::missing_panics_doc)] // The panic is not possible
    pub fn build(&mut self) -> Result<IncrementResponse, IncrementResponseBuilderError> {
        self.inner_builder.build()
    }
}

/// Command Executor for `Increment`
pub struct IncrementCommandExecutor<C>(
    CommandExecutor<IncrementRequestPayload, IncrementResponsePayload, C>,
)
where
    C: ManagedClient + Clone + Send + Sync + 'static,
    C::PubReceiver: Send + Sync + 'static;

impl<C> IncrementCommandExecutor<C>
where
    C: ManagedClient + Clone + Send + Sync + 'static,
    C::PubReceiver: Send + Sync + 'static,
{
    /// Creates a new [`IncrementCommandExecutor`]
    ///
    /// # Panics
    /// If the DTDL that generated this code was invalid
    pub fn new(
        client: C,
        application_context: ApplicationContext,
        options: &CommandOptions,
    ) -> Self {
        let mut executor_options_builder = CommandExecutorOptionsBuilder::default();
        if let Some(topic_namespace) = &options.topic_namespace {
            executor_options_builder.topic_namespace(topic_namespace.clone());
        }

        let mut topic_token_map: HashMap<String, String> = options
            .topic_token_map
            .clone()
            .into_iter()
            .map(|(k, v)| (format!("ex:{k}"), v))
            .collect();

        topic_token_map.insert("modelId".to_string(), MODEL_ID.to_string());
        topic_token_map.insert("executorId".to_string(), client.client_id().to_string());
        topic_token_map.insert("commandName".to_string(), "increment".to_string());

        let executor_options = executor_options_builder
            .request_topic_pattern(REQUEST_TOPIC_PATTERN)
            .command_name("increment")
            .is_idempotent(false)
            .topic_token_map(topic_token_map)
            .build()
            .expect("DTDL schema generated invalid arguments");

        Self(
            CommandExecutor::new(client, application_context, executor_options)
                .expect("DTDL schema generated invalid arguments"),
        )
    }

    /// Receive the next [`IncrementRequest`] or [`None`] if there will be no more requests
    ///
    /// # Errors
    /// [`AIOProtocolError`] if there is a failure receiving a request
    pub async fn recv(&mut self) -> Option<Result<IncrementRequest, AIOProtocolError>> {
        self.0.recv().await
    }

    /// Shutdown the [`IncrementCommandExecutor`]. Unsubscribes from the request topic.
    ///
    /// Returns Ok(()) on success, otherwise returns [`AIOProtocolError`].
    /// # Errors
    /// [`AIOProtocolError`] of kind [`ClientError`](azure_iot_operations_protocol::common::aio_protocol_error::AIOProtocolErrorKind::ClientError) if the unsubscribe fails or if the unsuback reason code doesn't indicate success.
    pub async fn shutdown(&mut self) -> Result<(), AIOProtocolError> {
        self.0.shutdown().await
    }
}
