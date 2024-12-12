// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

//! Client for Schema Registry operations.

use std::collections::HashMap;
use std::sync::Arc;
use std::time::Duration;

use azure_iot_operations_mqtt::interface::ManagedClient;
use azure_iot_operations_protocol::rpc::command_invoker::CommandRequestBuilder;
use tokio::sync::Mutex;

use super::schemaregistry_gen::common_types::common_options::CommandOptionsBuilder;
use super::schemaregistry_gen::dtmi_ms_adr_SchemaRegistry__1::client::{
    Enum_Ms_Adr_SchemaRegistry_Format__1, Enum_Ms_Adr_SchemaRegistry_SchemaType__1,
    GetCommandInvoker, GetRequestPayloadBuilder, Object_Get_RequestBuilder,
    Object_Ms_Adr_SchemaRegistry_Schema__1, Object_Put_RequestBuilder, PutCommandInvoker,
    PutRequestPayloadBuilder,
};
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

/// Schema registry client implementation.
#[derive(Clone)]
pub struct Client<C>
where
    C: ManagedClient + Clone + Send + Sync + 'static,
    C::PubReceiver: Send + Sync,
{
    get_command_invoker: Arc<Mutex<GetCommandInvoker<C>>>,
    put_command_invoker: Arc<Mutex<PutCommandInvoker<C>>>,
    shutdown_handle: Arc<Mutex<ShutdownHandle>>,
}

impl<C> Client<C>
where
    C: ManagedClient + Clone + Send + Sync + 'static,
    C::PubReceiver: Send + Sync,
{
    /// Create a new Schema Registry Client.
    ///
    /// # Errors
    /// [`SchemaRegistryError`] of kind [`InvalidArgument`](SchemaRegistryErrorKind::InvalidArgument)
    /// if there is an error building the underlying command invokers.
    ///
    /// # Panics
    /// Panics if the options for the underlying command invokers cannot be built. Not possible since
    /// the options are statically generated.
    pub fn new(client: &C) -> Self {
        let options = CommandOptionsBuilder::default()
            .build()
            .expect("Statically generated options should not fail.");

        Self {
            get_command_invoker: Arc::new(Mutex::new(GetCommandInvoker::new(
                client.clone(),
                &options,
            ))),
            put_command_invoker: Arc::new(Mutex::new(PutCommandInvoker::new(
                client.clone(),
                &options,
            ))),
            shutdown_handle: Arc::new(Mutex::new(ShutdownHandle {
                put_shutdown: false,
                get_shutdown: false,
            })),
        }
    }

    /// Get a schema by its ID from the Schema Registry service.
    ///
    /// # Arguments
    /// * `id` - The ID of the schema to get.
    /// * `version` - The version of the schema to get. If not provided, the default version is used.
    /// * `timeout` - The duration until the Schema Registry Client stops waiting for a response to the request.
    ///
    /// Returns a [`client::Object_Ms_Adr_SchemaRegistry_Schema__1`] if the request was successful.
    ///
    /// # Errors
    /// [`SchemaRegistryError`] of kind [`InvalidArgument`](SchemaRegistryErrorKind::InvalidArgument)
    /// if the `id` is empty, the `timeout` is < 1 ms or > `u32::max`, or there is an error building the request.
    ///
    /// [`SchemaRegistryError`] of kind [`SerializationError`](SchemaRegistryErrorKind::SerializationError)
    /// if there is an error serializing the request.
    ///
    /// [`SchemaRegistryError`] of kind [`AIOProtocolError`](SchemaRegistryErrorKind::AIOProtocolError)
    /// if there are any underlying errors from the AIO RPC protocol.
    pub async fn get(
        &self,
        id: String,
        mut version: Option<String>,
        timeout: Duration,
    ) -> Result<Object_Ms_Adr_SchemaRegistry_Schema__1, SchemaRegistryError> {
        if id.is_empty() {
            return Err(SchemaRegistryError(
                SchemaRegistryErrorKind::InvalidArgument("id cannot be empty".to_string()),
            ));
        }

        if version.is_none() {
            version = Some(DEFAULT_SCHEMA_VERSION.to_string());
        }

        let get_request_payload = GetRequestPayloadBuilder::default()
            .get_schema_request(
                Object_Get_RequestBuilder::default()
                    .name(Some(id))
                    .version(version)
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
            .lock()
            .await
            .invoke(command_request)
            .await
            .map_err(SchemaRegistryErrorKind::from)?
            .payload
            .schema)
    }

    /// Put a schema into the Schema Registry service.
    ///
    /// # Arguments
    /// * `content` - The content of the schema to put.
    /// * `format` - The format of the schema to put.
    /// * `schema_type` - The type of the schema to put.
    /// * `tags` - The tags of the schema to put.
    /// * `version` - The version of the schema to put. If not provided, the default version is used.
    /// * `timeout` - The duration until the Schema Registry Client stops waiting for a response to the request.
    ///
    /// Returns the [`client::Object_Ms_Adr_SchemaRegistry_Schema__1`] that was put if the request was successful.
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
        content: String,
        format: Enum_Ms_Adr_SchemaRegistry_Format__1,
        schema_type: Enum_Ms_Adr_SchemaRegistry_SchemaType__1,
        tags: HashMap<String, String>,
        mut version: Option<String>,
        timeout: Duration,
    ) -> Result<Object_Ms_Adr_SchemaRegistry_Schema__1, SchemaRegistryError> {
        if version.is_none() {
            version = Some(DEFAULT_SCHEMA_VERSION.to_string());
        }

        let put_request_payload = PutRequestPayloadBuilder::default()
            .put_schema_request(
                Object_Put_RequestBuilder::default()
                    .format(Some(format))
                    .schema_content(Some(content))
                    .version(version)
                    .tags(Some(tags))
                    .schema_type(Some(schema_type))
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
            .lock()
            .await
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
    /// If the method returns an error, it may be called again to attempt unsubscribing again.
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

        // Obtain locks on the command invokers to shut them down.
        let get_command_invoker_ref = self.get_command_invoker.lock().await;
        let put_command_invoker_ref = self.put_command_invoker.lock().await;

        // If the get command invoker has not been shut down, shut it down.
        if !shutdown_handle.get_shutdown {
            get_command_invoker_ref
                .shutdown()
                .await
                .map_err(SchemaRegistryErrorKind::from)?;
            shutdown_handle.get_shutdown = true;
        }

        // If the put command invoker has not been shut down, shut it down.
        if !shutdown_handle.put_shutdown {
            put_command_invoker_ref
                .shutdown()
                .await
                .map_err(SchemaRegistryErrorKind::from)?;
            shutdown_handle.put_shutdown = true;
        }

        Ok(())
    }
}
