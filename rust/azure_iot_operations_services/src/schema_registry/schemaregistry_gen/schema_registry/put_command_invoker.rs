/* Code generated by Azure.Iot.Operations.ProtocolCompiler v0.9.0.0; DO NOT EDIT. */

use std::collections::HashMap;
use std::time::Duration;

use azure_iot_operations_mqtt::interface::ManagedClient;
use azure_iot_operations_protocol::application::ApplicationContext;
use azure_iot_operations_protocol::common::aio_protocol_error::AIOProtocolError;
use azure_iot_operations_protocol::common::payload_serialize::PayloadSerialize;
use azure_iot_operations_protocol::rpc::command_invoker::{
    CommandInvoker, CommandInvokerOptionsBuilder, CommandRequest, CommandRequestBuilder,
    CommandRequestBuilderError, CommandResponse,
};

use super::super::common_types::common_options::CommandOptions;
use super::put_request_payload::PutRequestPayload;
use super::put_response_payload::PutResponsePayload;
use super::MODEL_ID;
use super::REQUEST_TOPIC_PATTERN;

pub type PutRequest = CommandRequest<PutRequestPayload>;
pub type PutResponse = CommandResponse<PutResponsePayload>;
pub type PutRequestBuilderError = CommandRequestBuilderError;

#[derive(Default)]
/// Builder for [`PutRequest`]
pub struct PutRequestBuilder {
    inner_builder: CommandRequestBuilder<PutRequestPayload>,
    topic_tokens: HashMap<String, String>,
}

impl PutRequestBuilder {
    /// Custom user data to set on the request
    pub fn custom_user_data(&mut self, custom_user_data: Vec<(String, String)>) -> &mut Self {
        self.inner_builder.custom_user_data(custom_user_data);
        self
    }

    /// Topic token keys/values to be replaced into the publish topic of the request message.
    /// A prefix of "ex:" will be prepended to each key before scanning the topic pattern.
    /// Thus, only tokens of the form `{ex:SOMEKEY}` will be replaced.
    pub fn topic_tokens(&mut self, topic_tokens: HashMap<String, String>) -> &mut Self {
        topic_tokens.into_iter().for_each(|(k, v)| {
            self.topic_tokens.insert(format!("ex:{k}"), v);
        });
        self
    }

    /// Timeout for the request
    pub fn timeout(&mut self, timeout: Duration) -> &mut Self {
        self.inner_builder.timeout(timeout);
        self
    }

    /// Payload of the request
    ///
    /// # Errors
    /// If the payload cannot be serialized
    pub fn payload(&mut self, payload: PutRequestPayload) -> Result<&mut Self, AIOProtocolError> {
        self.inner_builder.payload(payload)?;
        Ok(self)
    }

    /// Builds a new `PutRequest`
    ///
    /// # Errors
    /// If a required field has not been initialized
    #[allow(clippy::missing_panics_doc)] // The panic is not possible
    pub fn build(&mut self) -> Result<PutRequest, PutRequestBuilderError> {
        self.inner_builder.topic_tokens(self.topic_tokens.clone());

        self.inner_builder.build()
    }
}

/// Command Invoker for `put`
pub struct PutCommandInvoker<C>(CommandInvoker<PutRequestPayload, PutResponsePayload, C>)
where
    C: ManagedClient + Clone + Send + Sync + 'static,
    C::PubReceiver: Send + Sync + 'static;

impl<C> PutCommandInvoker<C>
where
    C: ManagedClient + Clone + Send + Sync + 'static,
    C::PubReceiver: Send + Sync + 'static,
{
    /// Creates a new [`PutCommandInvoker`]
    ///
    /// # Panics
    /// If the DTDL that generated this code was invalid
    pub fn new(
        application_context: ApplicationContext,
        client: C,
        options: &CommandOptions,
    ) -> Self {
        let mut invoker_options_builder = CommandInvokerOptionsBuilder::default();
        if let Some(topic_namespace) = &options.topic_namespace {
            invoker_options_builder.topic_namespace(topic_namespace.clone());
        }

        let mut topic_token_map: HashMap<String, String> = options
            .topic_token_map
            .clone()
            .into_iter()
            .map(|(k, v)| (format!("ex:{k}"), v))
            .collect();

        topic_token_map.insert("modelId".to_string(), MODEL_ID.to_string());
        topic_token_map.insert(
            "invokerClientId".to_string(),
            client.client_id().to_string(),
        );
        topic_token_map.insert("commandName".to_string(), "put".to_string());

        let invoker_options = invoker_options_builder
            .request_topic_pattern(REQUEST_TOPIC_PATTERN)
            .command_name("put")
            .topic_token_map(topic_token_map)
            .build()
            .expect("DTDL schema generated invalid arguments");

        Self(
            CommandInvoker::new(application_context, client, invoker_options)
                .expect("DTDL schema generated invalid arguments"),
        )
    }

    /// Invokes the [`PutRequest`]
    ///
    /// # Errors
    /// [`AIOProtocolError`] if there is a failure invoking the request
    pub async fn invoke(&self, request: PutRequest) -> Result<PutResponse, AIOProtocolError> {
        self.0.invoke(request).await
    }

    /// Shutdown the [`PutCommandInvoker`]. Unsubscribes from the response topic and cancels the receiver loop to drop the receiver and to prevent the task from looping indefinitely.
    ///
    /// Returns Ok(()) on success, otherwise returns [`AIOProtocolError`].
    /// # Errors
    /// [`AIOProtocolError`] of kind [`ClientError`](azure_iot_operations_protocol::common::aio_protocol_error::AIOProtocolErrorKind::ClientError) if the unsubscribe fails or if the unsuback reason code doesn't indicate success.
    pub async fn shutdown(&self) -> Result<(), AIOProtocolError> {
        self.0.shutdown().await
    }
}
