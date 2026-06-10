// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

//! Client for Edge Registry (xRegistry) operations.
//!
//! To use this client, the `edge_registry` feature must be enabled.

use std::sync::Arc;

use azure_iot_operations_mqtt::session::SessionManagedClient;
use azure_iot_operations_protocol::application::ApplicationContext;

use crate::edge_registry::edge_registry_gen::common_types::options::CommandInvokerOptionsBuilder;
use crate::edge_registry::edge_registry_gen::edge_registry::client as client_gen;

/// Edge Registry client implementation.
// Fields are constructed in `new` but not yet read; the per-API methods that use
// these invokers are pending (see the TODO below and the extension TODO).
#[allow(dead_code)]
#[allow(clippy::struct_field_names)]
#[derive(Clone)]
pub struct Client {
    // Generic xRegistry command invokers.
    create_group_command_invoker: Arc<client_gen::CreateGroupActionInvoker>,
    get_group_command_invoker: Arc<client_gen::GetGroupActionInvoker>,
    list_groups_command_invoker: Arc<client_gen::ListGroupsActionInvoker>,
    delete_group_command_invoker: Arc<client_gen::DeleteGroupActionInvoker>,
    create_resource_command_invoker: Arc<client_gen::CreateResourceActionInvoker>,
    get_resource_command_invoker: Arc<client_gen::GetResourceActionInvoker>,
    list_resources_command_invoker: Arc<client_gen::ListResourcesActionInvoker>,
    list_resources_with_label_command_invoker: Arc<client_gen::ListResourcesWithLabelActionInvoker>,
    delete_resource_command_invoker: Arc<client_gen::DeleteResourceActionInvoker>,
    create_version_command_invoker: Arc<client_gen::CreateVersionActionInvoker>,
    get_version_command_invoker: Arc<client_gen::GetVersionActionInvoker>,
    list_versions_command_invoker: Arc<client_gen::ListVersionsActionInvoker>,
    list_versions_with_label_command_invoker: Arc<client_gen::ListVersionsWithLabelActionInvoker>,
    delete_version_command_invoker: Arc<client_gen::DeleteVersionActionInvoker>,
    // TODO: Add the Schema, Thing Description, and Thing Model extension command
    // invokers once the corresponding extension APIs are implemented.
}

impl Client {
    /// Create a new Edge Registry Client.
    ///
    /// # Panics
    /// Panics if the options for the underlying command invokers cannot be built. Not possible since
    /// the options are statically generated.
    #[must_use]
    pub fn new(application_context: ApplicationContext, client: &SessionManagedClient) -> Self {
        let options = CommandInvokerOptionsBuilder::default()
            .build()
            .expect("Statically generated options should not fail.");

        Self {
            create_group_command_invoker: Arc::new(client_gen::CreateGroupActionInvoker::new(
                application_context.clone(),
                client.clone(),
                &options,
            )),
            get_group_command_invoker: Arc::new(client_gen::GetGroupActionInvoker::new(
                application_context.clone(),
                client.clone(),
                &options,
            )),
            list_groups_command_invoker: Arc::new(client_gen::ListGroupsActionInvoker::new(
                application_context.clone(),
                client.clone(),
                &options,
            )),
            delete_group_command_invoker: Arc::new(client_gen::DeleteGroupActionInvoker::new(
                application_context.clone(),
                client.clone(),
                &options,
            )),
            create_resource_command_invoker: Arc::new(
                client_gen::CreateResourceActionInvoker::new(
                    application_context.clone(),
                    client.clone(),
                    &options,
                ),
            ),
            get_resource_command_invoker: Arc::new(client_gen::GetResourceActionInvoker::new(
                application_context.clone(),
                client.clone(),
                &options,
            )),
            list_resources_command_invoker: Arc::new(client_gen::ListResourcesActionInvoker::new(
                application_context.clone(),
                client.clone(),
                &options,
            )),
            list_resources_with_label_command_invoker: Arc::new(
                client_gen::ListResourcesWithLabelActionInvoker::new(
                    application_context.clone(),
                    client.clone(),
                    &options,
                ),
            ),
            delete_resource_command_invoker: Arc::new(
                client_gen::DeleteResourceActionInvoker::new(
                    application_context.clone(),
                    client.clone(),
                    &options,
                ),
            ),
            create_version_command_invoker: Arc::new(client_gen::CreateVersionActionInvoker::new(
                application_context.clone(),
                client.clone(),
                &options,
            )),
            get_version_command_invoker: Arc::new(client_gen::GetVersionActionInvoker::new(
                application_context.clone(),
                client.clone(),
                &options,
            )),
            list_versions_command_invoker: Arc::new(client_gen::ListVersionsActionInvoker::new(
                application_context.clone(),
                client.clone(),
                &options,
            )),
            list_versions_with_label_command_invoker: Arc::new(
                client_gen::ListVersionsWithLabelActionInvoker::new(
                    application_context.clone(),
                    client.clone(),
                    &options,
                ),
            ),
            delete_version_command_invoker: Arc::new(client_gen::DeleteVersionActionInvoker::new(
                application_context,
                client.clone(),
                &options,
            )),
        }
    }
}
