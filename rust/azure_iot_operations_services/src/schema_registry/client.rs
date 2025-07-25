// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

//! Client for Schema Registry operations.
//!
//! To use this client, the `schema_registry` feature must be enabled.

use std::sync::Arc;
use std::time::Duration;

use azure_iot_operations_mqtt::interface::ManagedClient;
use azure_iot_operations_protocol::application::ApplicationContext;
use azure_iot_operations_protocol::rpc_command;

use crate::schema_registry::schemaregistry_gen::common_types::options::CommandInvokerOptionsBuilder;
use crate::schema_registry::schemaregistry_gen::schema_registry::client::{
    GetCommandInvoker, GetRequestSchemaBuilder, PutCommandInvoker, PutRequestSchemaBuilder,
};
use crate::schema_registry::{Error, ErrorKind, GetRequest, PutRequest, Schema};

/// Schema registry client implementation.
#[derive(Clone)]
pub struct Client<C>
where
    C: ManagedClient + Clone + Send + Sync + 'static,
    C::PubReceiver: Send + Sync,
{
    get_command_invoker: Arc<GetCommandInvoker<C>>,
    put_command_invoker: Arc<PutCommandInvoker<C>>,
}

impl<C> Client<C>
where
    C: ManagedClient + Clone + Send + Sync + 'static,
    C::PubReceiver: Send + Sync,
{
    /// Create a new Schema Registry Client.
    ///
    /// # Panics
    /// Panics if the options for the underlying command invokers cannot be built. Not possible since
    /// the options are statically generated.
    pub fn new(application_context: ApplicationContext, client: &C) -> Self {
        let options = CommandInvokerOptionsBuilder::default()
            .build()
            .expect("Statically generated options should not fail.");

        Self {
            get_command_invoker: Arc::new(GetCommandInvoker::new(
                application_context.clone(),
                client.clone(),
                &options,
            )),
            put_command_invoker: Arc::new(PutCommandInvoker::new(
                application_context,
                client.clone(),
                &options,
            ))
        }
    }

    /// Retrieves schema information from a schema registry service.
    ///
    /// # Arguments
    /// * `get_request` - The request to get a schema from the schema registry.
    /// * `timeout` - The duration until the Schema Registry Client stops waiting for a response to the request, it is rounded up to the nearest second.
    ///
    /// Returns a [`Schema`] if the schema was found, otherwise returns an error of type [`Error`].
    ///
    /// # Errors
    /// [`struct@Error`] of kind [`InvalidArgument`](ErrorKind::InvalidArgument)
    /// if the `timeout` is zero or > `u32::max`, or there is an error building the request.
    ///
    /// [`struct@Error`] of kind [`SerializationError`](ErrorKind::SerializationError)
    /// if there is an error serializing the request.
    ///
    /// [`struct@Error`] of kind [`ServiceError`](ErrorKind::ServiceError)
    /// if there is an error returned by the Schema Registry Service.
    ///
    /// [`struct@Error`] of kind [`AIOProtocolError`](ErrorKind::AIOProtocolError)
    /// if there are any underlying errors from the AIO RPC protocol.
    pub async fn get(&self, get_request: GetRequest, timeout: Duration) -> Result<Schema, Error> {
        let get_request_schema = GetRequestSchemaBuilder::default()
            .name(get_request.name)
            .version(get_request.version)
            .build()
            .map_err(|e| Error(ErrorKind::InvalidArgument(e.to_string())))?;

        let command_request = rpc_command::invoker::RequestBuilder::default()
            .payload(get_request_schema)
            .map_err(|e| Error(ErrorKind::SerializationError(e.to_string())))?
            .timeout(timeout)
            .build()
            .map_err(|e| Error(ErrorKind::InvalidArgument(e.to_string())))?;

        let response = self
            .get_command_invoker
            .invoke(command_request)
            .await
            .map_err(|e| Error(ErrorKind::from(e)))?
            .map_err(|e| Error(ErrorKind::from(e.payload)))?;

        Ok(response.payload.schema.into())
    }

    /// Adds or updates a schema in the schema registry service.
    ///
    /// # Arguments
    /// * `put_request` - The request to put a schema in the schema registry.
    /// * `timeout` - The duration until the Schema Registry Client stops waiting for a response to the request, it is rounded up to the nearest second.
    ///
    /// Returns the [`Schema`] that was put if the request was successful.
    ///
    /// # Errors
    /// [`struct@Error`] of kind [`InvalidArgument`](ErrorKind::InvalidArgument)
    /// if the `content` is empty, the `timeout` is zero or > `u32::max`, or there is an error building the request.
    ///
    /// [`struct@Error`] of kind [`SerializationError`](ErrorKind::SerializationError)
    /// if there is an error serializing the request.
    ///
    /// [`struct@Error`] of kind [`ServiceError`](ErrorKind::ServiceError)
    /// if there is an error returned by the Schema Registry Service.
    ///
    /// [`struct@Error`] of kind [`AIOProtocolError`](ErrorKind::AIOProtocolError)
    /// if there are any underlying errors from the AIO RPC protocol.
    pub async fn put(&self, put_request: PutRequest, timeout: Duration) -> Result<Schema, Error> {
        let put_request_schema = PutRequestSchemaBuilder::default()
            .format(put_request.format.into())
            .schema_content(put_request.content)
            .version(put_request.version)
            .tags(Some(put_request.tags))
            .schema_type(put_request.schema_type.into())
            .build()
            .map_err(|e| Error(ErrorKind::InvalidArgument(e.to_string())))?;

        let command_request = rpc_command::invoker::RequestBuilder::default()
            .payload(put_request_schema)
            .map_err(|e| Error(ErrorKind::SerializationError(e.to_string())))?
            .timeout(timeout)
            .build()
            .map_err(|e| Error(ErrorKind::InvalidArgument(e.to_string())))?;

        let response = self
            .put_command_invoker
            .invoke(command_request)
            .await
            .map_err(ErrorKind::from)?
            .map_err(|e| ErrorKind::from(e.payload))?;

        Ok(response.payload.schema.into())
    }

    /// Shutdown the [`Client`]. Shuts down the underlying command invokers for get and put operations.
    ///
    /// Note: If this method is called, the [`Client`] should not be used again.
    /// If the method returns an error, it may be called again to re-attempt unsubscribing.
    ///
    /// Returns Ok(()) on success, otherwise returns [`struct@Error`].
    /// # Errors
    /// [`struct@Error`] of kind [`AIOProtocolError`](ErrorKind::AIOProtocolError)
    /// if the unsubscribe fails or if the unsuback reason code doesn't indicate success.
    pub async fn shutdown(&self) -> Result<(), Error> {
        // Shutdown the get command invoker
        self.get_command_invoker
            .shutdown()
            .await
            .map_err(ErrorKind::from)?;
        // Shutdown the put command invoker
        self.put_command_invoker
            .shutdown()
            .await
            .map_err(ErrorKind::from)?;
        Ok(())
    }
}

#[cfg(test)]
mod tests {
    use std::collections::HashMap;

    use azure_iot_operations_mqtt::{
        MqttConnectionSettingsBuilder,
        session::{Session, SessionOptionsBuilder},
    };
    use azure_iot_operations_protocol::application::ApplicationContextBuilder;

    use crate::schema_registry::{
        Client, DEFAULT_SCHEMA_VERSION, Error, ErrorKind, Format, GetRequestBuilder,
        GetRequestBuilderError, PutRequestBuilder, SchemaType,
    };

    // TODO: This should return a mock ManagedClient instead.
    // Until that's possible, need to return a Session so that the Session doesn't go out of
    // scope and render the ManagedClient unable to to be used correctly.
    fn create_session() -> Session {
        // TODO: Make a real mock that implements MqttProvider
        let connection_settings = MqttConnectionSettingsBuilder::default()
            .hostname("localhost")
            .client_id("test_client")
            .build()
            .unwrap();
        let session_options = SessionOptionsBuilder::default()
            .connection_settings(connection_settings)
            .build()
            .unwrap();
        Session::new(session_options).unwrap()
    }

    const TEST_SCHEMA_NAME: &str = "test_schema_name";
    const TEST_SCHEMA_CONTENT: &str = r#"
    {
        "$schema": "http://json-schema.org/draft-07/schema#",
        "type": "object",
        "properties": {
            "test": {
                "type": "integer"
            },
        }
    }
    "#;

    #[tokio::test]
    async fn test_get_request_valid() {
        let get_request = GetRequestBuilder::default()
            .name(TEST_SCHEMA_NAME.to_string())
            .build()
            .unwrap();

        assert_eq!(get_request.name, TEST_SCHEMA_NAME);
        assert_eq!(get_request.version, DEFAULT_SCHEMA_VERSION.to_string());
    }

    #[tokio::test]
    async fn test_get_request_invalid_name() {
        let get_request = GetRequestBuilder::default().build();

        assert!(matches!(
            get_request.unwrap_err(),
            GetRequestBuilderError::UninitializedField(_)
        ));

        let get_request = GetRequestBuilder::default().name(String::new()).build();

        assert!(matches!(
            get_request.unwrap_err(),
            GetRequestBuilderError::ValidationError(_)
        ));
    }

    #[tokio::test]
    async fn test_put_request_valid() {
        let put_request = PutRequestBuilder::default()
            .content(TEST_SCHEMA_CONTENT.to_string())
            .format(Format::JsonSchemaDraft07)
            .build()
            .unwrap();

        assert_eq!(put_request.content, TEST_SCHEMA_CONTENT);
        assert!(matches!(put_request.format, Format::JsonSchemaDraft07));
        assert!(matches!(put_request.schema_type, SchemaType::MessageSchema));
        assert_eq!(put_request.tags, HashMap::new());
        assert_eq!(put_request.version, DEFAULT_SCHEMA_VERSION.to_string());
    }

    #[tokio::test]
    async fn test_get_timeout_invalid() {
        let session = create_session();
        let client = Client::new(
            ApplicationContextBuilder::default().build().unwrap(),
            &session.create_managed_client(),
        );

        let get_result = client
            .get(
                GetRequestBuilder::default()
                    .name(TEST_SCHEMA_NAME.to_string())
                    .build()
                    .unwrap(),
                std::time::Duration::from_millis(0),
            )
            .await;

        assert!(matches!(
            get_result.unwrap_err(),
            Error(ErrorKind::InvalidArgument(_))
        ));

        let get_result = client
            .get(
                GetRequestBuilder::default()
                    .name(TEST_SCHEMA_NAME.to_string())
                    .build()
                    .unwrap(),
                std::time::Duration::from_secs(u64::from(u32::MAX) + 1),
            )
            .await;

        assert!(matches!(
            get_result.unwrap_err(),
            Error(ErrorKind::InvalidArgument(_))
        ));
    }

    #[tokio::test]
    async fn test_put_timeout_invalid() {
        let session = create_session();
        let client = Client::new(
            ApplicationContextBuilder::default().build().unwrap(),
            &session.create_managed_client(),
        );

        let put_result = client
            .put(
                PutRequestBuilder::default()
                    .content(TEST_SCHEMA_CONTENT.to_string())
                    .format(Format::JsonSchemaDraft07)
                    .build()
                    .unwrap(),
                std::time::Duration::from_millis(0),
            )
            .await;

        assert!(matches!(
            put_result.unwrap_err(),
            Error(ErrorKind::InvalidArgument(_))
        ));

        let put_result = client
            .put(
                PutRequestBuilder::default()
                    .content(TEST_SCHEMA_CONTENT.to_string())
                    .format(Format::JsonSchemaDraft07)
                    .build()
                    .unwrap(),
                std::time::Duration::from_secs(u64::from(u32::MAX) + 1),
            )
            .await;

        assert!(matches!(
            put_result.unwrap_err(),
            Error(ErrorKind::InvalidArgument(_))
        ));
    }
}
