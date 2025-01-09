// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

//! Client for Schema Registry operations.

use std::collections::HashMap;
use std::sync::Arc;
use std::time::Duration;

use azure_iot_operations_mqtt::interface::ManagedClient;
use azure_iot_operations_protocol::rpc::command_invoker::CommandRequestBuilder;
use derive_builder::Builder;
use tokio::sync::Mutex;

use super::schemaregistry_gen::common_types::common_options::CommandOptionsBuilder;
use super::schemaregistry_gen::dtmi_ms_adr_SchemaRegistry__1::client::{
    GetCommandInvoker, GetRequestPayloadBuilder, Object_Get_RequestBuilder,
    Object_Put_RequestBuilder, PutCommandInvoker, PutRequestPayloadBuilder,
};
use super::{Format, Schema, SchemaType};
use super::{SchemaRegistryError, SchemaRegistryErrorKind};

/// The default schema version to use if not provided.
const DEFAULT_SCHEMA_VERSION: &str = "1.0.0";

/// Handle for shutting down the [`Client`].
struct ShutdownHandle {
    /// Whether the get command invoker has been shut down.
    put_shutdown: bool,
    /// Whether the put command invoker has been shut down.
    get_shutdown: bool,
}

/// Request to get a schema from the schema registry.
#[derive(Builder, Clone, Debug)]
#[builder(setter(into), build_fn(validate = "Self::validate"))]
pub struct GetRequest {
    /// The unique identifier of the schema to retrieve. Required to locate the schema in the registry.
    id: String,
    /// The version of the schema to fetch. If not specified, defaults to "1.0.0".
    #[builder(default = "DEFAULT_SCHEMA_VERSION.to_string()")]
    version: String,
}

impl GetRequestBuilder {
    /// Validate the [`GetRequest`].
    ///
    /// # Errors
    /// Returns a `String` describing the errors if `id` is empty or not provided.
    fn validate(&self) -> Result<(), String> {
        if let Some(id) = &self.id {
            if id.is_empty() {
                return Err("id cannot be empty".to_string());
            }
        }

        Ok(())
    }
}

/// Request to put a schema in the schema registry.
#[derive(Builder, Clone, Debug)]
#[builder(setter(into))]
pub struct PutRequest {
    /// The content of the schema to be added or updated in the registry.
    content: String,
    /// The format of the schema. Specifies how the schema content should be interpreted.
    format: Format,
    /// The type of the schema, such as message schema or data schema.
    #[builder(default = "SchemaType::MessageSchema")]
    schema_type: SchemaType,
    /// Optional metadata tags to associate with the schema. These tags can be used to store additional information about the schema in key-value format.
    #[builder(default)]
    tags: HashMap<String, String>,
    /// The version of the schema to add or update. If not specified, defaults to "1.0.0".
    #[builder(default = "DEFAULT_SCHEMA_VERSION.to_string()")]
    version: String,
}

/// Schema registry client implementation.
#[derive(Clone)]
pub struct Client<C>
where
    C: ManagedClient + Clone + Send + Sync + 'static,
    C::PubReceiver: Send + Sync,
{
    get_command_invoker: Arc<GetCommandInvoker<C>>,
    put_command_invoker: Arc<PutCommandInvoker<C>>,
    shutdown_handle: Arc<Mutex<ShutdownHandle>>,
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
    pub fn new(client: &C) -> Self {
        let options = CommandOptionsBuilder::default()
            .build()
            .expect("Statically generated options should not fail.");

        Self {
            get_command_invoker: Arc::new(GetCommandInvoker::new(client.clone(), &options)),
            put_command_invoker: Arc::new(PutCommandInvoker::new(client.clone(), &options)),
            shutdown_handle: Arc::new(Mutex::new(ShutdownHandle {
                put_shutdown: false,
                get_shutdown: false,
            })),
        }
    }

    /// Retrieves schema information from a schema registry service.
    ///
    /// # Arguments
    /// * `get_request` - The request to get a schema from the schema registry.
    /// * `timeout` - The duration until the Schema Registry Client stops waiting for a response to the request.
    ///
    /// Returns a [`Schema`] if the schema was found, otherwise returns `None`.
    ///
    /// # Errors
    /// [`SchemaRegistryError`] of kind [`InvalidArgument`](SchemaRegistryErrorKind::InvalidArgument)
    /// if the `timeout` is < 1 ms or > `u32::max`, or there is an error building the request.
    ///
    /// [`SchemaRegistryError`] of kind [`SerializationError`](SchemaRegistryErrorKind::SerializationError)
    /// if there is an error serializing the request.
    ///
    /// [`SchemaRegistryError`] of kind [`AIOProtocolError`](SchemaRegistryErrorKind::AIOProtocolError)
    /// if there are any underlying errors from the AIO RPC protocol.
    pub async fn get(
        &self,
        get_request: GetRequest,
        timeout: Duration,
    ) -> Result<Option<Schema>, SchemaRegistryError> {
        let get_request_payload = GetRequestPayloadBuilder::default()
            .get_schema_request(
                Object_Get_RequestBuilder::default()
                    .name(Some(get_request.id))
                    .version(Some(get_request.version))
                    .build()
                    .map_err(|e| {
                        SchemaRegistryError(SchemaRegistryErrorKind::InvalidArgument(e.to_string()))
                    })?,
            )
            .build()
            .map_err(|e| {
                SchemaRegistryError(SchemaRegistryErrorKind::InvalidArgument(e.to_string()))
            })?;

        let command_request = CommandRequestBuilder::default()
            .payload(&get_request_payload)
            .map_err(|e| {
                SchemaRegistryError(SchemaRegistryErrorKind::SerializationError(e.to_string()))
            })?
            .timeout(timeout)
            .build()
            .map_err(|e| {
                SchemaRegistryError(SchemaRegistryErrorKind::InvalidArgument(e.to_string()))
            })?;

        Ok(self
            .get_command_invoker
            .invoke(command_request)
            .await
            .map_err(SchemaRegistryErrorKind::from)?
            .payload
            .schema)
    }

    /// Adds or updates a schema in the schema registry service.
    ///
    /// # Arguments
    /// * `put_request` - The request to put a schema in the schema registry.
    /// * `timeout` - The duration until the Schema Registry Client stops waiting for a response to the request.
    ///
    /// Returns the [`Schema`] that was put if the request was successful.
    ///
    /// # Errors
    /// [`SchemaRegistryError`] of kind [`InvalidArgument`](SchemaRegistryErrorKind::InvalidArgument)
    /// if the `content` is empty, the `timeout` is < 1 ms or > `u32::max`, or there is an error building the request.
    ///
    /// [`SchemaRegistryError`] of kind [`SerializationError`](SchemaRegistryErrorKind::SerializationError)
    /// if there is an error serializing the request.
    ///
    /// [`SchemaRegistryError`] of kind [`AIOProtocolError`](SchemaRegistryErrorKind::AIOProtocolError)
    /// if there are any underlying errors from the AIO RPC protocol.
    pub async fn put(
        &self,
        put_request: PutRequest,
        timeout: Duration,
    ) -> Result<Schema, SchemaRegistryError> {
        let put_request_payload = PutRequestPayloadBuilder::default()
            .put_schema_request(
                Object_Put_RequestBuilder::default()
                    .format(Some(put_request.format))
                    .schema_content(Some(put_request.content))
                    .version(Some(put_request.version))
                    .tags(Some(put_request.tags))
                    .schema_type(Some(put_request.schema_type))
                    .build()
                    .map_err(|e| {
                        SchemaRegistryError(SchemaRegistryErrorKind::InvalidArgument(e.to_string()))
                    })?,
            )
            .build()
            .map_err(|e| {
                SchemaRegistryError(SchemaRegistryErrorKind::InvalidArgument(e.to_string()))
            })?;

        let command_request = CommandRequestBuilder::default()
            .payload(&put_request_payload)
            .map_err(|e| {
                SchemaRegistryError(SchemaRegistryErrorKind::SerializationError(e.to_string()))
            })?
            .timeout(timeout)
            .build()
            .map_err(|e| {
                SchemaRegistryError(SchemaRegistryErrorKind::InvalidArgument(e.to_string()))
            })?;

        Ok(self
            .put_command_invoker
            .invoke(command_request)
            .await
            .map_err(SchemaRegistryErrorKind::from)?
            .payload
            .schema)
    }

    // TODO: Finish implementing shutdown logic
    /// Shutdown the [`Client`]. Shuts down the underlying command invokers for get and put operations.
    ///
    /// Note: If this method is called, the [`Client`] should not be used again.
    /// If the method returns an error, it may be called again to re-attempt unsubscribing.
    ///
    /// Returns Ok(()) on success, otherwise returns [`SchemaRegistryError`].'
    /// # Errors
    /// [`SchemaRegistryError`] of kind [`AIOProtocolError`](SchemaRegistryErrorKind::AIOProtocolError)
    /// if the unsubscribe fails or if the unsuback reason code doesn't indicate success.
    pub async fn shutdown(&self) -> Result<(), SchemaRegistryError> {
        // Obtain a lock on the shutdown handle to ensure that the shutdown logic is only executed once.
        let mut shutdown_handle = self.shutdown_handle.lock().await;

        // If the command invokers have already been shut down, return Ok(()).
        if shutdown_handle.get_shutdown && shutdown_handle.put_shutdown {
            return Ok(());
        }

        // If the get command invoker has not been shut down, shut it down.
        if !shutdown_handle.get_shutdown {
            self.get_command_invoker
                .shutdown()
                .await
                .map_err(SchemaRegistryErrorKind::from)?;
            shutdown_handle.get_shutdown = true;
        }

        // If the put command invoker has not been shut down, shut it down.
        if !shutdown_handle.put_shutdown {
            self.put_command_invoker
                .shutdown()
                .await
                .map_err(SchemaRegistryErrorKind::from)?;
            shutdown_handle.put_shutdown = true;
        }

        Ok(())
    }
}

#[cfg(test)]
mod tests {
    use std::collections::HashMap;

    use azure_iot_operations_mqtt::{
        session::{Session, SessionOptionsBuilder},
        MqttConnectionSettingsBuilder,
    };

    use crate::schema_registry::{
        client::{GetRequestBuilderError, DEFAULT_SCHEMA_VERSION},
        Client, Format, GetRequestBuilder, PutRequestBuilder, SchemaRegistryError,
        SchemaRegistryErrorKind, SchemaType,
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

    const TEST_SCHEMA_ID: &str = "test_schema_id";
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
            .id(TEST_SCHEMA_ID.to_string())
            .build()
            .unwrap();

        assert_eq!(get_request.id, TEST_SCHEMA_ID);
        assert_eq!(get_request.version, DEFAULT_SCHEMA_VERSION.to_string());
    }

    #[tokio::test]
    async fn test_get_request_invalid_id() {
        let get_request = GetRequestBuilder::default().build();

        assert!(matches!(
            get_request.unwrap_err(),
            GetRequestBuilderError::UninitializedField(_)
        ));

        let get_request = GetRequestBuilder::default().id(String::new()).build();

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
        let client = Client::new(&session.create_managed_client());

        let get_result = client
            .get(
                GetRequestBuilder::default()
                    .id(TEST_SCHEMA_ID.to_string())
                    .build()
                    .unwrap(),
                std::time::Duration::from_millis(0),
            )
            .await;

        assert!(matches!(
            get_result.unwrap_err(),
            SchemaRegistryError(SchemaRegistryErrorKind::InvalidArgument(_))
        ));

        let get_result = client
            .get(
                GetRequestBuilder::default()
                    .id(TEST_SCHEMA_ID.to_string())
                    .build()
                    .unwrap(),
                std::time::Duration::from_secs(u64::from(u32::MAX) + 1),
            )
            .await;

        assert!(matches!(
            get_result.unwrap_err(),
            SchemaRegistryError(SchemaRegistryErrorKind::InvalidArgument(_))
        ));
    }

    #[tokio::test]
    async fn test_put_timeout_invalid() {
        let session = create_session();
        let client = Client::new(&session.create_managed_client());

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
            SchemaRegistryError(SchemaRegistryErrorKind::InvalidArgument(_))
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
            SchemaRegistryError(SchemaRegistryErrorKind::InvalidArgument(_))
        ));
    }
}
