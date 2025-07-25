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

use crate::{OutputDirectoryManager, schema_registry::service_gen};
use crate::{
    ServiceStateOutputManager,
    schema_registry::{
        ErrorCode, SERVICE_NAME, Schema, ServiceError,
        schema_registry_gen::{
            common_types::options::CommandExecutorOptionsBuilder,
            schema_registry::service::{GetResponseSchema, PutResponseSchema},
        },
    },
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
                    log::error!("Error in get_schema_runner: {:?}", e);
                    return Err(Box::<dyn std::error::Error + Send + Sync>::from(e));
                }
            },
            r2 = put_schema_runner_handle => {
                if let Err(e) = r2 {
                    log::error!("Error in put_schema_runner: {:?}", e);
                    return Err(Box::<dyn std::error::Error + Send + Sync>::from(e));
                }
            }
        };

        Ok(())
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

                        // Extract the schema name
                        let schema_name = get_request.payload.name.clone();
                        // Extract the schema version
                        let schema_version: u32 = match get_request.payload.version.parse() {
                            Ok(version) => version,
                            Err(_) => {
                                // TODO: Implement error handling for incorrect version number
                                log::error!("Invalid schema version, skipping request");
                                continue;
                            }
                        };

                        // Retrieve the schema from the request
                        let result = {
                            let schemas = schemas.lock().expect("Mutex management should be safe");

                            match schemas.get(&schema_name) {
                                Some(schema_set) => {
                                    // We need to iterate through to find the schema with the correct version, to use get we would have to create a new schema object with the version and hash matching the request
                                    let find_res =
                                        schema_set.iter().find(|s| s.version == schema_version);
                                    match find_res {
                                        Some(schema) => {
                                            // We found the schema with the correct version
                                            log::debug!(
                                                "Schema {:?} version {:?} found",
                                                schema_name,
                                                schema_version
                                            );
                                            Ok(schema.clone())
                                        }
                                        None => {
                                            // We found the schema but not the version
                                            log::debug!(
                                                "Schema {:?} found but version {:?} not found",
                                                schema_name,
                                                schema_version
                                            );
                                            Err(ServiceError {
                                                code: ErrorCode::NotFound,
                                                details: None,
                                                inner_error: None,
                                                message: format!(
                                                    "Schema '{}' version '{}' not found",
                                                    schema_name, schema_version
                                                ),
                                                target: None,
                                            })
                                        }
                                    }
                                }
                                None => {
                                    // Schema not found
                                    log::debug!("Schema {:?} not found", schema_name);
                                    Err(ServiceError {
                                        code: ErrorCode::NotFound,
                                        details: None,
                                        inner_error: None,
                                        message: format!("Schema '{}' not found", schema_name),
                                        target: None,
                                    })
                                }
                            }
                        };

                        // Send the response
                        let response = match result {
                            Ok(schema) => rpc_command::executor::ResponseBuilder::default()
                                .payload(GetResponseSchema {
                                    error: None,
                                    schema: Some(schema.into()),
                                })
                                .expect("Get response payload should be valid")
                                .build()
                                .expect("Get response should not fail to build"),
                            Err(service_error) => rpc_command::executor::ResponseBuilder::default()
                                .payload(GetResponseSchema {
                                    error: Some(service_error.into()),
                                    schema: None,
                                })
                                .expect("Error response payload should be valid")
                                .build()
                                .expect("Error response should not fail to build"),
                        };

                        match get_request.complete(response).await {
                            Ok(_) => {
                                log::debug!(
                                    "Get request completed successfully for Schema {}, version {}",
                                    schema_name,
                                    schema_version
                                );
                            }
                            Err(e) => {
                                log::error!("Failed to complete Get request: {:?}", e);
                            }
                        }
                    }
                    Err(e) => {
                        log::error!("Error receiving Get request: {:?}", e);
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

                        // Extract the schema from the request
                        let schema: Schema = match put_request.payload.clone().try_into() {
                            Ok(schema) => schema,
                            Err(e) => {
                                log::error!("{e}"); // TODO: Implement error handling for incorrect schema
                                continue;
                            }
                        };

                        // TODO: Add verification of schema

                        // Extract the schema name
                        let schema_name = &schema.name;
                        // Extract the schema version
                        let schema_version: u32 = schema.version;

                        // Store the schema in the HashMap
                        {
                            let mut schemas =
                                schemas.lock().expect("Mutex management should be safe");

                            schemas
                                .entry(schema_name.clone())
                                .and_modify(|schema_set| {
                                    // Case in which the schema already exists

                                    // Replace the schema with the new one or add it to the set if it doesn't exist
                                    let old_schema = schema_set.replace(schema.clone());

                                    match old_schema {
                                        Some(old_schema) => {
                                            // Version of the schema already existed and was replaced
                                            log::debug!(
                                                "Schema {} version {} updated",
                                                schema_name,
                                                schema_version,
                                            );
                                            log::debug!("Previous schema: {:?}", old_schema);
                                        }
                                        None => {
                                            // This version of the schema didn't exist and was added
                                            log::debug!(
                                                "Schema {} version {} added",
                                                schema_name,
                                                schema_version
                                            );
                                        }
                                    }
                                })
                                .or_insert(BTreeSet::from([{
                                    // Case in which the schema doesn't exist
                                    log::debug!(
                                        "New Schema {} created, version {} added",
                                        schema_name,
                                        schema_version
                                    );

                                    // Create a new schema set with the new schema
                                    schema.clone()
                                }]));

                            // Get the Schema set for the schema name
                            let schemas_list = schemas
                                .get(schema_name)
                                .expect("Schema key should be present in the HashMap");

                            // Output schemas
                            service_state_manager.write_state(
                                &schema_name,
                                serde_json::to_string_pretty(schemas_list)
                                    .expect("Schemas should be serializable"),
                            );
                        }

                        // Send the response
                        let response = rpc_command::executor::ResponseBuilder::default()
                            .payload(PutResponseSchema {
                                error: None,
                                schema: Some(schema.clone().into()),
                            })
                            .expect("Put response payload should be valid")
                            .build()
                            .expect("Put response should not fail to build");

                        match put_request.complete(response).await {
                            Ok(_) => { /* Success */ }
                            Err(e) => {
                                log::error!("Failed to complete Put request: {:?}", e);
                            }
                        }
                    }
                    Err(e) => {
                        log::error!("Error receiving Put request: {:?}", e);
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
