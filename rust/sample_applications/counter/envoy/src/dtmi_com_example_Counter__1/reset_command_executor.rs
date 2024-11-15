/* Code generated by Azure.Iot.Operations.ProtocolCompiler; DO NOT EDIT. */

use std::collections::HashMap;

use azure_iot_operations_mqtt::interface::ManagedClient;
use azure_iot_operations_protocol::common::aio_protocol_error::AIOProtocolError;
use azure_iot_operations_protocol::rpc::command_executor::{
    CommandExecutor, CommandExecutorOptionsBuilder, CommandRequest, CommandResponse,
    CommandResponseBuilder, CommandResponseBuilderError,
};

use super::MODEL_ID;
use super::REQUEST_TOPIC_PATTERN;
use crate::common_types::common_options::CommonOptions;
use crate::common_types::empty_json::EmptyJson;

pub type ResetRequest = CommandRequest<EmptyJson, EmptyJson>;
pub type ResetResponse = CommandResponse<EmptyJson>;
pub type ResetResponseBuilderError = CommandResponseBuilderError;

/// Builder for [`ResetResponse`]
#[derive(Default)]
pub struct ResetResponseBuilder {
    inner_builder: CommandResponseBuilder<EmptyJson>,
}

impl ResetResponseBuilder {
    /// Custom user data to set on the response
    pub fn custom_user_data(&mut self, custom_user_data: Vec<(String, String)>) -> &mut Self {
        self.inner_builder.custom_user_data(custom_user_data);
        self
    }

    /// Builds a new `ResetResponse`
    ///
    /// # Errors
    /// If a required field has not been initialized
    #[allow(clippy::missing_panics_doc)] // The panic is not possible
    pub fn build(&mut self) -> Result<ResetResponse, ResetResponseBuilderError> {
        self.inner_builder.payload(&EmptyJson {}).unwrap();

        self.inner_builder.build()
    }
}

/// Command Executor for `Reset`
pub struct ResetCommandExecutor<C>(CommandExecutor<EmptyJson, EmptyJson, C>)
where
    C: ManagedClient + Clone + Send + Sync + 'static,
    C::PubReceiver: Send + Sync + 'static;

impl<C> ResetCommandExecutor<C>
where
    C: ManagedClient + Clone + Send + Sync + 'static,
    C::PubReceiver: Send + Sync + 'static,
{
    /// Creates a new [`ResetCommandExecutor`]
    ///
    /// # Panics
    /// If the DTDL that generated this code was invalid
    pub fn new(client: C, options: &CommonOptions) -> Self {
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
        topic_token_map.insert("commandName".to_string(), "reset".to_string());

        let executor_options = executor_options_builder
            .request_topic_pattern(REQUEST_TOPIC_PATTERN)
            .command_name("reset")
            .is_idempotent(false)
            .topic_token_map(topic_token_map)
            .build()
            .expect("DTDL schema generated invalid arguments");

        Self(
            CommandExecutor::new(client, executor_options)
                .expect("DTDL schema generated invalid arguments"),
        )
    }

    /// Receive the next [`ResetRequest`]
    ///
    /// # Errors
    /// [`AIOProtocolError`] if there is a failure receiving a request
    pub async fn recv(&mut self) -> Result<ResetRequest, AIOProtocolError> {
        self.0.recv().await
    }
}
