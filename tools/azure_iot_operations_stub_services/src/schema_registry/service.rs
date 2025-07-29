// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

//! Stub Schema Registry service.

use std::{
    collections::{BTreeSet, HashMap},
    sync::{Arc, Mutex},
};

use azure_iot_operations_mqtt::interface::ManagedClient;
use azure_iot_operations_protocol::{
    application::ApplicationContext, common::aio_protocol_error::AIOProtocolError, rpc_command,
};

use crate::{
    OutputDirectoryManager,
    schema_registry::schema_registry_gen::common_types::options::CommandExecutorOptionsBuilder,
};
use crate::{
    ServiceStateOutputManager,
    schema_registry::{SERVICE_NAME, Schema, service_gen},
};

/// Schema Registry service implementation.
pub struct Service<C>
where
    C: ManagedClient + Clone + Send + Sync + 'static,
    C::PubReceiver: Send + Sync + 'static,
{
    schemas: Arc<Mutex<HashMap<String, BTreeSet<Schema>>>>,
    get_command_executor: service_gen::GetCommandExecutor<C>,
    put_command_executor: service_gen::PutCommandExecutor<C>,
    service_output_manager: ServiceStateOutputManager,
}

impl<C> Service<C>
where
    C: ManagedClient + Clone + Send + Sync + 'static,
    C::PubReceiver: Send + Sync + 'static,
{
    /// Creates a new stub Schema Registry Service.
    pub fn new(
        application_context: ApplicationContext,
        client: C,
        output_directory_manager: &OutputDirectoryManager,
    ) -> Self {
        log::info!("Schema Registry Stub Service created");

        Self {
            schemas: Arc::new(Mutex::new(HashMap::new())),
            get_command_executor: service_gen::GetCommandExecutor::new(
                application_context.clone(),
                client.clone(),
                &CommandExecutorOptionsBuilder::default()
                    .build()
                    .expect("Default command executor options should be valid"),
            ),
            put_command_executor: service_gen::PutCommandExecutor::new(
                application_context,
                client,
                &CommandExecutorOptionsBuilder::default()
                    .build()
                    .expect("Default command executor options should be valid"),
            ),
            service_output_manager: output_directory_manager
                .create_new_service_output_manager(SERVICE_NAME),
        }
    }

    /// Runs the Schema Registry stub service.
    pub async fn run(self) -> Result<(), Box<dyn std::error::Error + Send + Sync>> {
        let get_schema_runner_handle = tokio::spawn(Self::get_schema_runner(
            self.get_command_executor,
            self.schemas.clone(),
        ));
        let put_schema_runner_handle = tokio::spawn(Self::put_schema_runner(
            self.put_command_executor,
            self.schemas,
            self.service_output_manager,
        ));

        tokio::select! {
            r1 = get_schema_runner_handle => {
                if let Err(e) = r1 {
                    log::error!("Error in get_schema_runner: {e:?}");
                    return Err(Box::<dyn std::error::Error + Send + Sync>::from(e));
                }
            },
            r2 = put_schema_runner_handle => {
                if let Err(e) = r2 {
                    log::error!("Error in put_schema_runner: {e:?}");
                    return Err(Box::<dyn std::error::Error + Send + Sync>::from(e));
                }
            }
        };

        Ok(())
    }

    /// Processes a get request and returns a response that can be used with `get_request.complete()`.
    fn process_get_request(
        payload: &service_gen::GetRequestSchema,
        schemas: &Arc<Mutex<HashMap<String, BTreeSet<Schema>>>>,
    ) -> rpc_command::executor::Response<service_gen::GetResponseSchema> {
        // Extract the schema name
        let schema_name = &payload.name;

        // Extract and validate the schema version
        let schema_version: u32 = match payload.version.parse() {
            Ok(version) => {
                // Validate version is between 0-9
                if version > 9 {
                    log::error!("Invalid schema version {version}, must be between 0-9");
                    let service_error = service_gen::SchemaRegistryError {
                        code: service_gen::SchemaRegistryErrorCode::BadRequest,
                        details: None,
                        inner_error: None,
                        message: format!(
                            "Schema version '{version}' is invalid. Version must be between 0-9."
                        ),
                        target: Some(service_gen::SchemaRegistryErrorTarget::VersionProperty),
                    };
                    return rpc_command::executor::ResponseBuilder::default()
                        .payload(service_gen::GetResponseSchema {
                            error: Some(service_error),
                            schema: None,
                        })
                        .expect("Error response payload should be valid")
                        .build()
                        .expect("Error response should not fail to build");
                }
                version
            }
            Err(_) => {
                log::error!("Invalid schema version format: '{}'", payload.version);
                let service_error = service_gen::SchemaRegistryError {
                    code: service_gen::SchemaRegistryErrorCode::BadRequest,
                    details: None,
                    inner_error: None,
                    message: format!(
                        "Schema version '{}' has invalid format. Version must be a number between 0-9.",
                        payload.version
                    ),
                    target: Some(service_gen::SchemaRegistryErrorTarget::VersionProperty),
                };
                return rpc_command::executor::ResponseBuilder::default()
                    .payload(service_gen::GetResponseSchema {
                        error: Some(service_error),
                        schema: None,
                    })
                    .expect("Error response payload should be valid")
                    .build()
                    .expect("Error response should not fail to build");
            }
        };

        // Retrieve the schema from the request
        let result = {
            let schemas = match schemas.lock() {
                Ok(schemas) => schemas,
                Err(_) => {
                    log::error!("Failed to acquire mutex lock on schemas");
                    let service_error = service_gen::SchemaRegistryError {
                        code: service_gen::SchemaRegistryErrorCode::InternalError,
                        details: None,
                        inner_error: None,
                        message: "Internal server error occurred while accessing schemas."
                            .to_string(),
                        target: None,
                    };
                    return rpc_command::executor::ResponseBuilder::default()
                        .payload(service_gen::GetResponseSchema {
                            error: Some(service_error),
                            schema: None,
                        })
                        .expect("Error response payload should be valid")
                        .build()
                        .expect("Error response should not fail to build");
                }
            };

            match schemas.get(schema_name) {
                Some(schema_set) => {
                    // We need to iterate through to find the schema with the correct version
                    let find_res = schema_set.iter().find(|s| s.version == schema_version);
                    match find_res {
                        Some(schema) => {
                            // We found the schema with the correct version
                            log::debug!("Schema {schema_name:?} version {schema_version:?} found");
                            Ok(schema.clone())
                        }
                        None => {
                            // We found the schema but not the version
                            log::debug!(
                                "Schema {schema_name:?} found but version {schema_version:?} not found"
                            );
                            Err(service_gen::SchemaRegistryError {
                                code: service_gen::SchemaRegistryErrorCode::NotFound,
                                details: None,
                                inner_error: None,
                                message: format!(
                                    "Schema '{schema_name}' version '{schema_version}' not found"
                                ),
                                target: Some(
                                    service_gen::SchemaRegistryErrorTarget::VersionProperty,
                                ),
                            })
                        }
                    }
                }
                None => {
                    // Schema not found
                    log::debug!("Schema {schema_name:?} not found");
                    Err(service_gen::SchemaRegistryError {
                        code: service_gen::SchemaRegistryErrorCode::NotFound,
                        details: None,
                        inner_error: None,
                        message: format!("Schema '{schema_name}' not found"),
                        target: Some(service_gen::SchemaRegistryErrorTarget::SchemaArmResource),
                    })
                }
            }
        };

        // Create the response
        match result {
            Ok(schema) => rpc_command::executor::ResponseBuilder::default()
                .payload(service_gen::GetResponseSchema {
                    error: None,
                    schema: Some(schema.into()),
                })
                .expect("Get response payload should be valid")
                .build()
                .expect("Get response should not fail to build"),
            Err(service_error) => rpc_command::executor::ResponseBuilder::default()
                .payload(service_gen::GetResponseSchema {
                    error: Some(service_error),
                    schema: None,
                })
                .expect("Error response payload should be valid")
                .build()
                .expect("Error response should not fail to build"),
        }
    }

    /// Processes a put request and returns a response that can be used with `put_request.complete()`.
    fn process_put_request(
        payload: &service_gen::PutRequestSchema,
        schemas: &Arc<Mutex<HashMap<String, BTreeSet<Schema>>>>,
        service_state_manager: &ServiceStateOutputManager,
    ) -> rpc_command::executor::Response<service_gen::PutResponseSchema> {
        // Validate and convert the PUT request schema to internal Schema
        let schema: Schema = match Schema::try_from(payload.clone()) {
            Ok(schema) => {
                // Additional validation for version range (0-9)
                if schema.version > 9 {
                    log::error!(
                        "Invalid schema version {}, must be between 0-9",
                        schema.version
                    );
                    let service_error = service_gen::SchemaRegistryError {
                        code: service_gen::SchemaRegistryErrorCode::BadRequest,
                        details: None,
                        inner_error: None,
                        message: format!(
                            "Schema version '{}' is invalid. Version must be between 0-9.",
                            schema.version
                        ),
                        target: Some(service_gen::SchemaRegistryErrorTarget::VersionProperty),
                    };
                    return rpc_command::executor::ResponseBuilder::default()
                        .payload(service_gen::PutResponseSchema {
                            error: Some(service_error),
                            schema: None,
                        })
                        .expect("Error response payload should be valid")
                        .build()
                        .expect("Error response should not fail to build");
                }

                // Additional validation for schema content
                if payload.schema_content.trim().is_empty() {
                    log::error!("Schema content cannot be empty");
                    let service_error = service_gen::SchemaRegistryError {
                        code: service_gen::SchemaRegistryErrorCode::BadRequest,
                        details: None,
                        inner_error: None,
                        message: "Schema content cannot be empty.".to_string(),
                        target: Some(service_gen::SchemaRegistryErrorTarget::SchemaContentProperty),
                    };
                    return rpc_command::executor::ResponseBuilder::default()
                        .payload(service_gen::PutResponseSchema {
                            error: Some(service_error),
                            schema: None,
                        })
                        .expect("Error response payload should be valid")
                        .build()
                        .expect("Error response should not fail to build");
                }

                schema
            }
            Err(e) => {
                log::error!("Failed to convert PUT request schema: {e}");
                let service_error = service_gen::SchemaRegistryError {
                    code: service_gen::SchemaRegistryErrorCode::BadRequest,
                    details: None,
                    inner_error: None,
                    message: format!("Invalid schema format: {e}"),
                    target: Some(service_gen::SchemaRegistryErrorTarget::VersionProperty), // Most likely version parsing error
                };
                return rpc_command::executor::ResponseBuilder::default()
                    .payload(service_gen::PutResponseSchema {
                        error: Some(service_error),
                        schema: None,
                    })
                    .expect("Error response payload should be valid")
                    .build()
                    .expect("Error response should not fail to build");
            }
        };

        // Extract schema information for logging
        let schema_name = &schema.name;
        let schema_version = schema.version;

        // Store the schema in the HashMap
        let schemas_list = {
            let mut schemas = match schemas.lock() {
                Ok(schemas) => schemas,
                Err(_) => {
                    log::error!("Failed to acquire mutex lock on schemas");
                    let service_error = service_gen::SchemaRegistryError {
                        code: service_gen::SchemaRegistryErrorCode::InternalError,
                        details: None,
                        inner_error: None,
                        message: "Internal server error occurred while accessing schemas."
                            .to_string(),
                        target: None,
                    };
                    return rpc_command::executor::ResponseBuilder::default()
                        .payload(service_gen::PutResponseSchema {
                            error: Some(service_error),
                            schema: None,
                        })
                        .expect("Error response payload should be valid")
                        .build()
                        .expect("Error response should not fail to build");
                }
            };

            schemas
                .entry(schema_name.clone())
                .and_modify(|schema_set| {
                    // Case in which the schema already exists
                    // Replace the schema with the new one or add it to the set if it doesn't exist
                    let old_schema = schema_set.replace(schema.clone());

                    match old_schema {
                        Some(old_schema) => {
                            // Version of the schema already existed and was replaced
                            log::debug!("Schema {schema_name} version {schema_version} updated",);
                            log::debug!("Previous schema: {old_schema:?}");
                        }
                        None => {
                            // This version of the schema didn't exist and was added
                            log::debug!("Schema {schema_name} version {schema_version} added");
                        }
                    }
                })
                .or_insert(BTreeSet::from([{
                    // Case in which the schema doesn't exist
                    log::debug!("New Schema {schema_name} created, version {schema_version} added");

                    // Create a new schema set with the new schema
                    schema.clone()
                }]));

            // Get the Schema set for the schema name
            schemas
                .get(schema_name)
                .expect("Schema key should be present in the HashMap")
                .clone()
        };

        // Output schemas to state file
        match serde_json::to_string_pretty(&schemas_list) {
            Ok(serialized_schemas) => {
                service_state_manager.write_state(schema_name, serialized_schemas);
            }
            Err(e) => {
                log::error!("Failed to serialize schemas for state output: {e}");
                let service_error = service_gen::SchemaRegistryError {
                    code: service_gen::SchemaRegistryErrorCode::InternalError,
                    details: None,
                    inner_error: None,
                    message: "Internal server error occurred while saving schema state."
                        .to_string(),
                    target: None,
                };
                return rpc_command::executor::ResponseBuilder::default()
                    .payload(service_gen::PutResponseSchema {
                        error: Some(service_error),
                        schema: None,
                    })
                    .expect("Error response payload should be valid")
                    .build()
                    .expect("Error response should not fail to build");
            }
        }

        // Create successful response
        rpc_command::executor::ResponseBuilder::default()
            .payload(service_gen::PutResponseSchema {
                error: None,
                schema: Some(schema.into()),
            })
            .expect("Put response payload should be valid")
            .build()
            .expect("Put response should not fail to build")
    }

    async fn get_schema_runner(
        mut get_command_executor: service_gen::GetCommandExecutor<C>,
        schemas: Arc<Mutex<HashMap<String, BTreeSet<Schema>>>>,
    ) -> Result<(), AIOProtocolError> {
        loop {
            // Wait for a new get request
            match get_command_executor.recv().await {
                Some(incoming_request) => match incoming_request {
                    Ok(get_request) => {
                        log::debug!("Get request received: {:?}", get_request.payload);

                        let schema_name = get_request.payload.name.clone();
                        let schema_version = get_request.payload.version.clone();
                        let response = Self::process_get_request(&get_request.payload, &schemas);

                        match get_request.complete(response).await {
                            Ok(_) => {
                                log::debug!(
                                    "Get request completed successfully for Schema {schema_name}, version {schema_version}"
                                );
                            }
                            Err(e) => {
                                log::error!("Failed to complete Get request: {e:?}");
                            }
                        }
                    }
                    Err(e) => {
                        log::error!("Error receiving Get request: {e:?}");
                        return Err(e);
                    }
                },
                None => {
                    log::info!("Get command executor closed");
                    return Ok(());
                }
            }
        }
    }

    async fn put_schema_runner(
        mut put_command_executor: service_gen::PutCommandExecutor<C>,
        schemas: Arc<Mutex<HashMap<String, BTreeSet<Schema>>>>,
        service_state_manager: ServiceStateOutputManager,
    ) -> Result<(), AIOProtocolError> {
        loop {
            // Wait for a new put request
            match put_command_executor.recv().await {
                Some(incoming_request) => match incoming_request {
                    Ok(put_request) => {
                        log::debug!("Put request received: {:?}", put_request.payload);

                        let response = Self::process_put_request(
                            &put_request.payload,
                            &schemas,
                            &service_state_manager,
                        );

                        match put_request.complete(response).await {
                            Ok(_) => {
                                log::debug!("Put request completed successfully");
                            }
                            Err(e) => {
                                log::error!("Failed to complete Put request: {e:?}");
                            }
                        }
                    }
                    Err(e) => {
                        log::error!("Error receiving Put request: {e:?}");
                        return Err(e);
                    }
                },
                None => {
                    log::info!("Put command executor closed");
                    return Ok(());
                }
            }
        }
    }
}
