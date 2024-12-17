/* Code generated by Azure.Iot.Operations.ProtocolCompiler; DO NOT EDIT. */

use std::collections::HashMap;
use std::time::Duration;

use azure_iot_operations_mqtt::interface::ManagedClient;
use azure_iot_operations_protocol::common::aio_protocol_error::AIOProtocolError;
use azure_iot_operations_protocol::common::hybrid_logical_clock::HybridLogicalClock;
use azure_iot_operations_protocol::rpc::command_invoker::{
    CommandInvoker, CommandInvokerOptionsBuilder, CommandRequest, CommandRequestBuilder,
    CommandRequestBuilderError, CommandResponse,
};

use super::MODEL_ID;
use super::REQUEST_TOPIC_PATTERN;
use crate::common_types::common_options::CommandOptions;
use crate::common_types::empty_json::EmptyJson;

pub type ResetRequest = CommandRequest<EmptyJson>;
pub type ResetResponse = CommandResponse<EmptyJson>;
pub type ResetRequestBuilderError = CommandRequestBuilderError;

#[derive(Default)]
/// Builder for [`ResetRequest`]
pub struct ResetRequestBuilder {
    inner_builder: CommandRequestBuilder<EmptyJson>,
    set_executor_id: bool,
}

impl ResetRequestBuilder {
    /// Custom user data to set on the request
    pub fn custom_user_data(&mut self, custom_user_data: Vec<(String, String)>) -> &mut Self {
        self.inner_builder.custom_user_data(custom_user_data);
        self
    }

    /// Fencing token for the request
    pub fn fencing_token(&mut self, fencing_token: Option<HybridLogicalClock>) -> &mut Self {
        self.inner_builder.fencing_token(fencing_token);
        self
    }

    /// Timeout for the request
    pub fn timeout(&mut self, timeout: Duration) -> &mut Self {
        self.inner_builder.timeout(timeout);
        self
    }

    /// Target executor ID
    pub fn executor_id(&mut self, executor_id: String) -> &mut Self {
        self.inner_builder
            .topic_tokens(HashMap::from([("executorId".to_string(), executor_id)]));
        self.set_executor_id = true;
        self
    }

    /// Builds a new `ResetRequest`
    ///
    /// # Errors
    /// If a required field has not been initialized
    #[allow(clippy::missing_panics_doc)] // The panic is not possible
    pub fn build(&mut self) -> Result<ResetRequest, ResetRequestBuilderError> {
        if !self.set_executor_id {
            return Err(ResetRequestBuilderError::UninitializedField("executor_id"));
        }

        self.inner_builder.payload(&EmptyJson {}).unwrap();

        self.inner_builder.build()
    }
}

/// Command Invoker for `Reset`
pub struct ResetCommandInvoker<C>(CommandInvoker<EmptyJson, EmptyJson, C>)
where
    C: ManagedClient + Clone + Send + Sync + 'static,
    C::PubReceiver: Send + Sync + 'static;

impl<C> ResetCommandInvoker<C>
where
    C: ManagedClient + Clone + Send + Sync + 'static,
    C::PubReceiver: Send + Sync + 'static,
{
    /// Creates a new [`ResetCommandInvoker`]
    ///
    /// # Panics
    /// If the DTDL that generated this code was invalid
    pub fn new(client: C, options: &CommandOptions) -> Self {
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
        topic_token_map.insert("commandName".to_string(), "reset".to_string());

        let invoker_options = invoker_options_builder
            .request_topic_pattern(REQUEST_TOPIC_PATTERN)
            .command_name("reset")
            .topic_token_map(topic_token_map)
            .build()
            .expect("DTDL schema generated invalid arguments");

        Self(
            CommandInvoker::new(client, invoker_options)
                .expect("DTDL schema generated invalid arguments"),
        )
    }

    /// Invokes the [`ResetRequest`]
    ///
    /// # Errors
    /// [`AIOProtocolError`] if there is a failure invoking the request
    pub async fn invoke(&self, request: ResetRequest) -> Result<ResetResponse, AIOProtocolError> {
        self.0.invoke(request).await
    }
}
