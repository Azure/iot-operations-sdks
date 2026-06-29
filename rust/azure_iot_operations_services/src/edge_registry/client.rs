// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

//! Client for Edge Registry (xRegistry) operations.
//!
//! To use this client, the `edge_registry` feature must be enabled.

use std::collections::HashMap;
use std::sync::Arc;
use std::time::Duration;

use azure_iot_operations_mqtt::session::SessionManagedClient;
use azure_iot_operations_protocol::application::ApplicationContext;
use bytes::Bytes;

use crate::edge_registry::edge_registry_gen::common_types::options::CommandInvokerOptionsBuilder;
use crate::edge_registry::edge_registry_gen::edge_registry::client::{self as client_gen};
use crate::edge_registry::models::{
    CoreGroupAttributes, CoreGroupEntity, CoreResourceEntity, CoreResourceMetaAttributes,
    CoreVersionAttributes, CoreVersionEntity, ResourceXId, SchemaVersionAttributes,
    SchemaVersionEntity, ThingDescriptionVersionAttributes, ThingDescriptionVersionEntity,
    ThingModelVersionAttributes, ThingModelVersionEntity, VersionXId, extensions_to_gen,
    labels_to_gen,
};
use crate::edge_registry::{
    AnyGroupSelection, CreateVersionId, Error, ErrorKind, GetVersionId, GroupId, GroupQuery,
    GroupSelection, Label,
};

// Topic token keys for the xRegistry command topics.
const GROUP_TYPE_TOPIC_TOKEN: &str = "groupType";
const RESOURCE_TYPE_TOPIC_TOKEN: &str = "resourceType";
const RESOURCE_ID_TOPIC_TOKEN: &str = "resourceId";
const VERSION_ID_TOPIC_TOKEN: &str = "versionId";
const SCHEMA_ID_TOPIC_TOKEN: &str = "schemaId";
const THING_DESCRIPTION_ID_TOPIC_TOKEN: &str = "thingDescriptionId";
const THING_MODEL_ID_TOPIC_TOKEN: &str = "thingModelId";

// XID constants.
const SCHEMA_GROUP_TYPE: &str = client_gen::SCHEMA_GROUP_TYPE;
const SCHEMA_RESOURCE_TYPE: &str = client_gen::SCHEMA_RESOURCE_TYPE;
const THING_DESCRIPTION_GROUP_TYPE: &str = client_gen::THING_DESCRIPTION_GROUP_TYPE;
const THING_DESCRIPTION_RESOURCE_TYPE: &str = client_gen::THING_DESCRIPTION_RESOURCE_TYPE;
const THING_MODEL_GROUP_TYPE: &str = client_gen::THING_MODEL_GROUP_TYPE;
const THING_MODEL_RESOURCE_TYPE: &str = client_gen::THING_MODEL_RESOURCE_TYPE;

/// Edge Registry client implementation.
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
    delete_resource_command_invoker: Arc<client_gen::DeleteResourceActionInvoker>,
    create_version_command_invoker: Arc<client_gen::CreateVersionActionInvoker>,
    get_version_command_invoker: Arc<client_gen::GetVersionActionInvoker>,
    list_versions_command_invoker: Arc<client_gen::ListVersionsActionInvoker>,
    delete_version_command_invoker: Arc<client_gen::DeleteVersionActionInvoker>,
    // Schema extension command invokers.
    create_schema_version_command_invoker: Arc<client_gen::CreateSchemaVersionActionInvoker>,
    get_schema_version_command_invoker: Arc<client_gen::GetSchemaVersionActionInvoker>,
    list_schema_versions_command_invoker: Arc<client_gen::ListSchemaVersionsActionInvoker>,
    delete_schema_version_command_invoker: Arc<client_gen::DeleteSchemaVersionActionInvoker>,
    // Thing Description extension command invokers.
    create_thing_description_version_command_invoker:
        Arc<client_gen::CreateThingDescriptionVersionActionInvoker>,
    get_thing_description_version_command_invoker:
        Arc<client_gen::GetThingDescriptionVersionActionInvoker>,
    list_thing_description_versions_command_invoker:
        Arc<client_gen::ListThingDescriptionVersionsActionInvoker>,
    delete_thing_description_version_command_invoker:
        Arc<client_gen::DeleteThingDescriptionVersionActionInvoker>,
    // Thing Model extension command invokers.
    create_thing_model_version_command_invoker:
        Arc<client_gen::CreateThingModelVersionActionInvoker>,
    get_thing_model_version_command_invoker: Arc<client_gen::GetThingModelVersionActionInvoker>,
    list_thing_model_versions_command_invoker: Arc<client_gen::ListThingModelVersionsActionInvoker>,
    delete_thing_model_version_command_invoker:
        Arc<client_gen::DeleteThingModelVersionActionInvoker>,
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
            delete_version_command_invoker: Arc::new(client_gen::DeleteVersionActionInvoker::new(
                application_context.clone(),
                client.clone(),
                &options,
            )),
            create_schema_version_command_invoker: Arc::new(
                client_gen::CreateSchemaVersionActionInvoker::new(
                    application_context.clone(),
                    client.clone(),
                    &options,
                ),
            ),
            get_schema_version_command_invoker: Arc::new(
                client_gen::GetSchemaVersionActionInvoker::new(
                    application_context.clone(),
                    client.clone(),
                    &options,
                ),
            ),
            list_schema_versions_command_invoker: Arc::new(
                client_gen::ListSchemaVersionsActionInvoker::new(
                    application_context.clone(),
                    client.clone(),
                    &options,
                ),
            ),
            delete_schema_version_command_invoker: Arc::new(
                client_gen::DeleteSchemaVersionActionInvoker::new(
                    application_context.clone(),
                    client.clone(),
                    &options,
                ),
            ),
            create_thing_description_version_command_invoker: Arc::new(
                client_gen::CreateThingDescriptionVersionActionInvoker::new(
                    application_context.clone(),
                    client.clone(),
                    &options,
                ),
            ),
            get_thing_description_version_command_invoker: Arc::new(
                client_gen::GetThingDescriptionVersionActionInvoker::new(
                    application_context.clone(),
                    client.clone(),
                    &options,
                ),
            ),
            list_thing_description_versions_command_invoker: Arc::new(
                client_gen::ListThingDescriptionVersionsActionInvoker::new(
                    application_context.clone(),
                    client.clone(),
                    &options,
                ),
            ),
            delete_thing_description_version_command_invoker: Arc::new(
                client_gen::DeleteThingDescriptionVersionActionInvoker::new(
                    application_context.clone(),
                    client.clone(),
                    &options,
                ),
            ),
            create_thing_model_version_command_invoker: Arc::new(
                client_gen::CreateThingModelVersionActionInvoker::new(
                    application_context.clone(),
                    client.clone(),
                    &options,
                ),
            ),
            get_thing_model_version_command_invoker: Arc::new(
                client_gen::GetThingModelVersionActionInvoker::new(
                    application_context.clone(),
                    client.clone(),
                    &options,
                ),
            ),
            list_thing_model_versions_command_invoker: Arc::new(
                client_gen::ListThingModelVersionsActionInvoker::new(
                    application_context.clone(),
                    client.clone(),
                    &options,
                ),
            ),
            delete_thing_model_version_command_invoker: Arc::new(
                client_gen::DeleteThingModelVersionActionInvoker::new(
                    application_context,
                    client.clone(),
                    &options,
                ),
            ),
        }
    }

    // ~~~~~~~~~~~~~~~~~ Group APIs ~~~~~~~~~~~~~~~~~~~~~

    /// Create a new xRegistry Group.
    ///
    /// # Arguments
    /// * `group_type` - The type of the Group to create.
    /// * `group_id` - The identifier of the Group to create. If [`CloudDefault`](GroupId::CloudDefault), create the default Group of the Group type.
    /// * `attributes` - The [`CoreGroupAttributes`] for the new Group.
    /// * `timeout` - The duration until the client stops waiting for a response to the request, it is rounded up to the nearest second.
    ///
    /// Returns the created [`CoreGroupEntity`] with epoch 1.
    ///
    /// # Errors
    /// [`struct@Error`] of kind [`ValidationError`](ErrorKind::ValidationError) if `timeout` is 0 or > `u32::max`.
    ///
    /// [`struct@Error`] of kind [`AIOProtocolError`](ErrorKind::AIOProtocolError) if there are
    /// any underlying errors from the AIO RPC protocol.
    ///
    /// [`struct@Error`] of kind [`ServiceError`](ErrorKind::ServiceError) if an error is returned
    /// by the Edge Registry service.
    pub async fn create_group(
        &self,
        group_type: String,
        group_id: GroupId,
        attributes: CoreGroupAttributes,
        timeout: Duration,
    ) -> Result<CoreGroupEntity, Error> {
        let payload: client_gen::GroupAttributes = attributes.into_gen(group_id.into());

        let request = client_gen::CreateGroupRequestBuilder::default()
            .payload(payload)
            .map_err(ErrorKind::from)?
            .topic_tokens(Self::group_topic_tokens(group_type))
            .timeout(timeout)
            .build()
            .map_err(ErrorKind::from)?;

        let response = self
            .create_group_command_invoker
            .invoke(request)
            .await
            .map_err(ErrorKind::from)?
            .map_err(ErrorKind::from)?;
        Ok(response.payload.into())
    }

    /// Retrieve an xRegistry [`CoreGroupEntity`] entity.
    ///
    /// # Arguments
    /// * `group_type` - The type of the Group to retrieve.
    /// * `group_id` - The identifier of the Group to retrieve. If [`CloudDefault`](GroupId::CloudDefault), retrieve the default Group of the Group type.
    /// * `timeout` - The duration until the client stops waiting for a response to the request, it is rounded up to the nearest second.
    ///
    /// Returns the requested [`CoreGroupEntity`].
    ///
    /// # Errors
    /// [`struct@Error`] of kind [`ValidationError`](ErrorKind::ValidationError) if `timeout` is 0 or > `u32::max`.
    ///
    /// [`struct@Error`] of kind [`AIOProtocolError`](ErrorKind::AIOProtocolError) if there are
    /// any underlying errors from the AIO RPC protocol.
    ///
    /// [`struct@Error`] of kind [`ServiceError`](ErrorKind::ServiceError) if an error is returned
    /// by the Edge Registry service.
    pub async fn get_group(
        &self,
        group_type: String,
        group_id: GroupId,
        timeout: Duration,
    ) -> Result<CoreGroupEntity, Error> {
        let payload = client_gen::GetGroupInputArguments {
            group_id: group_id.into(),
        };

        let request = client_gen::GetGroupRequestBuilder::default()
            .payload(payload)
            .map_err(ErrorKind::from)?
            .topic_tokens(Self::group_topic_tokens(group_type))
            .timeout(timeout)
            .build()
            .map_err(ErrorKind::from)?;

        let response = self
            .get_group_command_invoker
            .invoke(request)
            .await
            .map_err(ErrorKind::from)?
            .map_err(ErrorKind::from)?;
        Ok(response.payload.into())
    }

    /// List the identifiers of the xRegistry Groups of the given type.
    ///
    /// # Arguments
    /// * `group_type` - The type of the Groups to list.
    /// * `timeout` - The duration until the client stops waiting for a response to the request, it is rounded up to the nearest second.
    ///
    /// Returns the identifiers of the Groups of the given type.
    ///
    /// # Errors
    /// [`struct@Error`] of kind [`ValidationError`](ErrorKind::ValidationError) if `timeout` is 0 or > `u32::max`.
    ///
    /// [`struct@Error`] of kind [`AIOProtocolError`](ErrorKind::AIOProtocolError) if there are
    /// any underlying errors from the AIO RPC protocol.
    ///
    /// [`struct@Error`] of kind [`ServiceError`](ErrorKind::ServiceError) if an error is returned
    /// by the Edge Registry service.
    pub async fn list_groups(
        &self,
        group_type: String,
        timeout: Duration,
    ) -> Result<Vec<String>, Error> {
        let request = client_gen::ListGroupsRequestBuilder::default()
            .topic_tokens(Self::group_topic_tokens(group_type))
            .timeout(timeout)
            .build()
            .map_err(ErrorKind::from)?;

        let response = self
            .list_groups_command_invoker
            .invoke(request)
            .await
            .map_err(ErrorKind::from)?
            .map_err(ErrorKind::from)?;
        Ok(response.payload.ids)
    }

    /// Delete an xRegistry Group entity. Deletes cascade: all Resources contained in the Group
    /// and all of their Versions are deleted.
    ///
    /// # Arguments
    /// * `group_type` - The type of the Group to delete.
    /// * `group_id` - The identifier of the Group to delete. If [`CloudDefault`](GroupId::CloudDefault), delete the default Group of the Group type.
    /// * `timeout` - The duration until the client stops waiting for a response to the request, it is rounded up to the nearest second.
    ///
    /// # Errors
    /// [`struct@Error`] of kind [`ValidationError`](ErrorKind::ValidationError) if `timeout` is 0 or > `u32::max`.
    ///
    /// [`struct@Error`] of kind [`AIOProtocolError`](ErrorKind::AIOProtocolError) if there are
    /// any underlying errors from the AIO RPC protocol.
    ///
    /// [`struct@Error`] of kind [`ServiceError`](ErrorKind::ServiceError) if an error is returned
    /// by the Edge Registry service.
    pub async fn delete_group(
        &self,
        group_type: String,
        group_id: GroupId,
        timeout: Duration,
    ) -> Result<(), Error> {
        let payload = client_gen::DeleteGroupInputArguments {
            group_id: group_id.into(),
        };

        let request = client_gen::DeleteGroupRequestBuilder::default()
            .payload(payload)
            .map_err(ErrorKind::from)?
            .topic_tokens(Self::group_topic_tokens(group_type))
            .timeout(timeout)
            .build()
            .map_err(ErrorKind::from)?;

        self.delete_group_command_invoker
            .invoke(request)
            .await
            .map_err(ErrorKind::from)?
            .map_err(ErrorKind::from)?;
        Ok(())
    }

    // ~~~~~~~~~~~~~~~~~ Resource APIs ~~~~~~~~~~~~~~~~~~~~~

    /// Create a new xRegistry Resource entity along with its default Version.
    ///
    /// # Arguments
    /// * `group_type` - The type of the Group that owns the Resource.
    /// * `group_id` - The identifier of the Group that owns the Resource. If [`CloudDefault`](GroupId::CloudDefault), the default Group of the Group type is used.
    /// * `resource_type` - The type of the Resource to create.
    /// * `resource_id` - The identifier of the Resource to create.
    /// * `resource_meta_attributes` - The [`CoreResourceMetaAttributes`] for the Resource's `meta` sub-entity.
    /// * `resource_extensions` - Extension-specific attributes for the Resource.
    /// * `default_version_id` - The identifier for the Resource's default Version. If [`ServerAssigned`](CreateVersionId::ServerAssigned), the server assigns the Version identifier.
    /// * `default_version_attributes` - The [`CoreVersionAttributes`] of the Resource's default Version, created along with the Resource.
    /// * `timeout` - The duration until the client stops waiting for a response to the request, it is rounded up to the nearest second.
    ///
    /// Returns the created [`CoreResourceEntity`] with epoch 1.
    ///
    /// # Errors
    /// [`struct@Error`] of kind [`ValidationError`](ErrorKind::ValidationError) if `timeout` is 0 or > `u32::max`.
    ///
    /// [`struct@Error`] of kind [`AIOProtocolError`](ErrorKind::AIOProtocolError) if there are
    /// any underlying errors from the AIO RPC protocol.
    ///
    /// [`struct@Error`] of kind [`ServiceError`](ErrorKind::ServiceError) if an error is returned
    /// by the Edge Registry service.
    #[allow(clippy::too_many_arguments)]
    pub async fn create_resource(
        &self,
        group_type: String,
        group_id: GroupId,
        resource_type: String,
        resource_id: String,
        resource_meta_attributes: CoreResourceMetaAttributes,
        resource_extensions: HashMap<String, Bytes>,
        default_version_id: CreateVersionId,
        default_version_attributes: CoreVersionAttributes,
        timeout: Duration,
    ) -> Result<CoreResourceEntity, Error> {
        let payload = client_gen::CreateResourceRequestPayload {
            group_id: group_id.into(),
            meta: resource_meta_attributes.into(),
            default_version: default_version_attributes.into(),
            default_version_id: default_version_id.into(),
            extensions: extensions_to_gen(resource_extensions),
        };

        let request = client_gen::CreateResourceRequestBuilder::default()
            .payload(payload)
            .map_err(ErrorKind::from)?
            .topic_tokens(Self::resource_topic_tokens(
                group_type,
                resource_type,
                resource_id,
            ))
            .timeout(timeout)
            .build()
            .map_err(ErrorKind::from)?;

        let response = self
            .create_resource_command_invoker
            .invoke(request)
            .await
            .map_err(ErrorKind::from)?
            .map_err(ErrorKind::from)?;
        Ok(response.payload.into())
    }

    /// Retrieve an xRegistry [`CoreResourceEntity`] entity.
    ///
    /// # Arguments
    /// * `group_type` - The type of the Group that owns the Resource.
    /// * `group_id` - The identifier of the Group that owns the Resource. If [`CloudDefault`](GroupId::CloudDefault), the default Group of the Group type is used.
    /// * `resource_type` - The type of the Resource to retrieve.
    /// * `resource_id` - The identifier of the Resource to retrieve.
    /// * `timeout` - The duration until the client stops waiting for a response to the request, it is rounded up to the nearest second.
    ///
    /// Returns the requested [`CoreResourceEntity`].
    ///
    /// # Errors
    /// [`struct@Error`] of kind [`ValidationError`](ErrorKind::ValidationError) if `timeout` is
    /// 0 or > `u32::max`.
    ///
    /// [`struct@Error`] of kind [`AIOProtocolError`](ErrorKind::AIOProtocolError) if there are
    /// any underlying errors from the AIO RPC protocol.
    ///
    /// [`struct@Error`] of kind [`ServiceError`](ErrorKind::ServiceError) if an error is returned
    /// by the Edge Registry service.
    pub async fn get_resource(
        &self,
        group_type: String,
        group_id: GroupId,
        resource_type: String,
        resource_id: String,
        timeout: Duration,
    ) -> Result<CoreResourceEntity, Error> {
        let payload = client_gen::GetResourceInputArguments {
            group_id: group_id.into(),
        };

        let request = client_gen::GetResourceRequestBuilder::default()
            .payload(payload)
            .map_err(ErrorKind::from)?
            .topic_tokens(Self::resource_topic_tokens(
                group_type,
                resource_type,
                resource_id,
            ))
            .timeout(timeout)
            .build()
            .map_err(ErrorKind::from)?;

        let response = self
            .get_resource_command_invoker
            .invoke(request)
            .await
            .map_err(ErrorKind::from)?
            .map_err(ErrorKind::from)?;
        Ok(response.payload.into())
    }

    /// List the XIDs of the xRegistry Resources matching the provided constraints.
    ///
    /// # Arguments
    /// * `group_query` - The [`GroupQuery`] selecting which Groups to search.
    /// * `resource_type` - If provided, only Resources of this type are listed; otherwise Resources of all types are listed.
    /// * `label` - If provided, only Resources carrying this [`Label`] are listed.
    /// * `timeout` - The duration until the client stops waiting for a response to the request, it is rounded up to the nearest second.
    ///
    /// Returns the [`ResourceXId`]s of the Resources matching the constraints.
    ///
    /// # Errors
    /// [`struct@Error`] of kind [`ValidationError`](ErrorKind::ValidationError) if `timeout` is 0
    /// or > `u32::max`.
    ///
    /// [`struct@Error`] of kind [`AIOProtocolError`](ErrorKind::AIOProtocolError) if there are
    /// any underlying errors from the AIO RPC protocol.
    ///
    /// [`struct@Error`] of kind [`ServiceError`](ErrorKind::ServiceError) if an error is returned
    /// by the Edge Registry service.
    pub async fn list_resources(
        &self,
        group_query: GroupQuery,
        resource_type: Option<String>,
        label: Option<Label>,
        timeout: Duration,
    ) -> Result<Vec<ResourceXId>, Error> {
        let (group_type, group_id, all_groups) = Self::group_query_scope(group_query);

        let payload = client_gen::ListResourcesRequestPayload {
            group_type,
            group_id,
            all_groups,
            resource_type,
            label: label.map(Into::into),
        };

        let request = client_gen::ListResourcesRequestBuilder::default()
            .payload(payload)
            .map_err(ErrorKind::from)?
            .timeout(timeout)
            .build()
            .map_err(ErrorKind::from)?;

        let response = self
            .list_resources_command_invoker
            .invoke(request)
            .await
            .map_err(ErrorKind::from)?
            .map_err(ErrorKind::from)?;
        Ok(response.payload.into())
    }

    /// Delete an xRegistry Resource entity. Deletes cascade: all Versions of the Resource are
    /// deleted.
    ///
    /// # Arguments
    /// * `group_type` - The type of the Group that owns the Resource.
    /// * `group_id` - The identifier of the Group that owns the Resource. If [`CloudDefault`](GroupId::CloudDefault), the default Group of the Group type is used.
    /// * `resource_type` - The type of the Resource to delete.
    /// * `resource_id` - The identifier of the Resource to delete.
    /// * `timeout` - The duration until the client stops waiting for a response to the request, it is rounded up to the nearest second.
    ///
    /// # Errors
    /// [`struct@Error`] of kind [`ValidationError`](ErrorKind::ValidationError) if `timeout` is 0 or > `u32::max`.
    ///
    /// [`struct@Error`] of kind [`AIOProtocolError`](ErrorKind::AIOProtocolError) if there are
    /// any underlying errors from the AIO RPC protocol.
    ///
    /// [`struct@Error`] of kind [`ServiceError`](ErrorKind::ServiceError) if an error is returned
    /// by the Edge Registry service.
    pub async fn delete_resource(
        &self,
        group_type: String,
        group_id: GroupId,
        resource_type: String,
        resource_id: String,
        timeout: Duration,
    ) -> Result<(), Error> {
        let payload = client_gen::DeleteResourceInputArguments {
            group_id: group_id.into(),
        };

        let request = client_gen::DeleteResourceRequestBuilder::default()
            .payload(payload)
            .map_err(ErrorKind::from)?
            .topic_tokens(Self::resource_topic_tokens(
                group_type,
                resource_type,
                resource_id,
            ))
            .timeout(timeout)
            .build()
            .map_err(ErrorKind::from)?;

        self.delete_resource_command_invoker
            .invoke(request)
            .await
            .map_err(ErrorKind::from)?
            .map_err(ErrorKind::from)?;
        Ok(())
    }

    // ~~~~~~~~~~~~~~~~~ Version APIs ~~~~~~~~~~~~~~~~~~~~~

    /// Create a new xRegistry Version entity under the specified Resource. The
    /// parent Resource is implicitly created if it doesn't already exist.
    ///
    /// # Arguments
    /// * `group_type` - The type of the Group that owns the Resource.
    /// * `group_id` - The identifier of the Group that owns the Resource. If [`CloudDefault`](GroupId::CloudDefault), the default Group of the Group type is used.
    /// * `resource_type` - The type of the Resource that owns the Version.
    /// * `resource_id` - The identifier of the Resource that owns the Version.
    /// * `resource_labels` - Queryable key/value pairs to be added to the parent Resource (which is implicitly created if it doesn't already exist).
    /// * `version_id` - The identifier of the Version to create. If [`ServerAssigned`](CreateVersionId::ServerAssigned), the server assigns the Version identifier.
    /// * `version` - The [`CoreVersionAttributes`] of the Version to create.
    /// * `timeout` - The duration until the client stops waiting for a response to the request, it is rounded up to the nearest second.
    ///
    /// Returns the created [`CoreVersionEntity`].
    ///
    /// # Errors
    /// [`struct@Error`] of kind [`ValidationError`](ErrorKind::ValidationError) if `timeout` is 0 or > `u32::max`.
    ///
    /// [`struct@Error`] of kind [`AIOProtocolError`](ErrorKind::AIOProtocolError) if there are
    /// any underlying errors from the AIO RPC protocol.
    ///
    /// [`struct@Error`] of kind [`ServiceError`](ErrorKind::ServiceError) if an error is returned
    /// by the Edge Registry service.
    #[allow(clippy::too_many_arguments)]
    pub async fn create_version(
        &self,
        group_type: String,
        group_id: GroupId,
        resource_type: String,
        resource_id: String,
        resource_labels: Vec<Label>,
        version_id: CreateVersionId,
        version: CoreVersionAttributes,
        timeout: Duration,
    ) -> Result<CoreVersionEntity, Error> {
        let payload = client_gen::CreateVersionRequestPayload {
            group_id: group_id.into(),
            version_id: version_id.into(),
            version: version.into(),
            resource_labels: labels_to_gen(resource_labels),
        };

        let request = client_gen::CreateVersionRequestBuilder::default()
            .payload(payload)
            .map_err(ErrorKind::from)?
            .topic_tokens(Self::resource_topic_tokens(
                group_type,
                resource_type,
                resource_id,
            ))
            .timeout(timeout)
            .build()
            .map_err(ErrorKind::from)?;

        let response = self
            .create_version_command_invoker
            .invoke(request)
            .await
            .map_err(ErrorKind::from)?
            .map_err(ErrorKind::from)?;
        Ok(response.payload.into())
    }

    /// Retrieve an xRegistry [`CoreVersionEntity`] entity.
    ///
    /// # Arguments
    /// * `group_type` - The type of the Group that owns the Resource.
    /// * `group_id` - The identifier of the Group that owns the Resource. If [`CloudDefault`](GroupId::CloudDefault), the default Group of the Group type is used.
    /// * `resource_type` - The type of the Resource that owns the Version.
    /// * `resource_id` - The identifier of the Resource that owns the Version.
    /// * `version_id` - The [`GetVersionId`] selecting which Version to retrieve. If [`ResourceDefault`](GetVersionId::ResourceDefault), the default Version of the Resource is retrieved.
    /// * `timeout` - The duration until the client stops waiting for a response to the request, it is rounded up to the nearest second.
    ///
    /// Returns the requested [`CoreVersionEntity`].
    ///
    /// # Errors
    /// [`struct@Error`] of kind [`ValidationError`](ErrorKind::ValidationError) if `timeout` is 0 or > `u32::max`.
    ///
    /// [`struct@Error`] of kind [`AIOProtocolError`](ErrorKind::AIOProtocolError) if there are
    /// any underlying errors from the AIO RPC protocol.
    ///
    /// [`struct@Error`] of kind [`ServiceError`](ErrorKind::ServiceError) if an error is returned
    /// by the Edge Registry service.
    pub async fn get_version(
        &self,
        group_type: String,
        group_id: GroupId,
        resource_type: String,
        resource_id: String,
        version_id: GetVersionId<String>,
        timeout: Duration,
    ) -> Result<CoreVersionEntity, Error> {
        let payload = client_gen::GetVersionInputArguments {
            group_id: group_id.into(),
            version_id: version_id.into(),
        };

        let request = client_gen::GetVersionRequestBuilder::default()
            .payload(payload)
            .map_err(ErrorKind::from)?
            .topic_tokens(Self::resource_topic_tokens(
                group_type,
                resource_type,
                resource_id,
            ))
            .timeout(timeout)
            .build()
            .map_err(ErrorKind::from)?;

        let response = self
            .get_version_command_invoker
            .invoke(request)
            .await
            .map_err(ErrorKind::from)?
            .map_err(ErrorKind::from)?;
        Ok(response.payload.into())
    }

    /// List the XIDs of the xRegistry Versions matching the provided constraints.
    ///
    /// # Arguments
    /// * `group_query` - The [`GroupQuery`] selecting which Groups to search.
    /// * `resource_type` - If provided, only Versions of Resources of this type are listed; otherwise Versions of Resources of all types are listed.
    /// * `resource_id` - If provided, only Versions of the Resource with this identifier are listed; otherwise Versions of all Resources are listed.
    /// * `label` - If provided, only Versions carrying this [`Label`] are listed.
    /// * `timeout` - The duration until the client stops waiting for a response to the request, it is rounded up to the nearest second.
    ///
    /// Returns the [`VersionXId`]s of the Versions matching the constraints.
    ///
    /// # Errors
    /// [`struct@Error`] of kind [`ValidationError`](ErrorKind::ValidationError) if `timeout` is 0
    /// or > `u32::max`.
    ///
    /// [`struct@Error`] of kind [`AIOProtocolError`](ErrorKind::AIOProtocolError) if there are
    /// any underlying errors from the AIO RPC protocol.
    ///
    /// [`struct@Error`] of kind [`ServiceError`](ErrorKind::ServiceError) if an error is returned
    /// by the Edge Registry service.
    pub async fn list_versions(
        &self,
        group_query: GroupQuery,
        resource_type: Option<String>,
        resource_id: Option<String>,
        label: Option<Label>,
        timeout: Duration,
    ) -> Result<Vec<VersionXId<String>>, Error> {
        let (group_type, group_id, all_groups) = Self::group_query_scope(group_query);

        let payload = client_gen::ListVersionsRequestPayload {
            group_type,
            group_id,
            all_groups,
            resource_type,
            resource_id,
            label: label.map(Into::into),
        };

        let request = client_gen::ListVersionsRequestBuilder::default()
            .payload(payload)
            .map_err(ErrorKind::from)?
            .timeout(timeout)
            .build()
            .map_err(ErrorKind::from)?;

        let response = self
            .list_versions_command_invoker
            .invoke(request)
            .await
            .map_err(ErrorKind::from)?
            .map_err(ErrorKind::from)?;
        Ok(response.payload.into())
    }

    /// Delete an xRegistry Version entity.
    ///
    /// # Arguments
    /// * `group_type` - The type of the Group that owns the Resource.
    /// * `group_id` - The identifier of the Group that owns the Resource. If [`CloudDefault`](GroupId::CloudDefault), the default Group of the Group type is used.
    /// * `resource_type` - The type of the Resource that owns the Version.
    /// * `resource_id` - The identifier of the Resource that owns the Version.
    /// * `version_id` - The identifier of the Version to delete.
    /// * `timeout` - The duration until the client stops waiting for a response to the request, it is rounded up to the nearest second.
    ///
    /// # Errors
    /// [`struct@Error`] of kind [`ValidationError`](ErrorKind::ValidationError) if `timeout` is 0 or
    /// > `u32::max`.
    ///
    /// [`struct@Error`] of kind [`AIOProtocolError`](ErrorKind::AIOProtocolError) if there are
    /// any underlying errors from the AIO RPC protocol.
    ///
    /// [`struct@Error`] of kind [`ServiceError`](ErrorKind::ServiceError) if an error is returned
    /// by the Edge Registry service.
    pub async fn delete_version(
        &self,
        group_type: String,
        group_id: GroupId,
        resource_type: String,
        resource_id: String,
        version_id: String,
        timeout: Duration,
    ) -> Result<(), Error> {
        let payload = client_gen::DeleteVersionInputArguments {
            group_id: group_id.into(),
        };

        let request = client_gen::DeleteVersionRequestBuilder::default()
            .payload(payload)
            .map_err(ErrorKind::from)?
            .topic_tokens(Self::version_topic_tokens(
                group_type,
                resource_type,
                resource_id,
                version_id,
            ))
            .timeout(timeout)
            .build()
            .map_err(ErrorKind::from)?;

        self.delete_version_command_invoker
            .invoke(request)
            .await
            .map_err(ErrorKind::from)?
            .map_err(ErrorKind::from)?;
        Ok(())
    }

    // ~~~~~~~~~~~~~~~~~ Schema extension APIs ~~~~~~~~~~~~~~~~~~~~~

    /// Create a new xRegistry Schema Version entity under the specified Schema. The parent Schema is
    /// implicitly created if it doesn't already exist.
    ///
    /// # Arguments
    /// * `group_id` - The identifier of the Group that owns the Schema. If [`CloudDefault`](GroupId::CloudDefault), the default Group is used.
    /// * `schema_id` - The identifier of the Schema that owns the Version.
    /// * `schema_labels` - Queryable key/value pairs to be added to the parent Schema.
    /// * `version` - The [`SchemaVersionAttributes`] of the Version to create.
    /// * `timeout` - The duration until the client stops waiting for a response to the request, it is rounded up to the nearest second.
    ///
    /// Returns the created [`SchemaVersionEntity`] with epoch 1.
    ///
    /// # Errors
    /// [`struct@Error`] of kind [`ValidationError`](ErrorKind::ValidationError) if `timeout` is 0
    /// or > `u32::max`.
    ///
    /// [`struct@Error`] of kind [`AIOProtocolError`](ErrorKind::AIOProtocolError) if there are
    /// any underlying errors from the AIO RPC protocol.
    ///
    /// [`struct@Error`] of kind [`ServiceError`](ErrorKind::ServiceError) if an error is returned
    /// by the Edge Registry service.
    pub async fn create_schema_version(
        &self,
        group_id: GroupId,
        schema_id: String,
        schema_labels: Vec<Label>,
        version: SchemaVersionAttributes,
        timeout: Duration,
    ) -> Result<SchemaVersionEntity, Error> {
        let payload = version.into_gen(group_id.into(), schema_labels);

        let request = client_gen::CreateSchemaVersionRequestBuilder::default()
            .payload(payload)
            .map_err(ErrorKind::from)?
            .topic_tokens(Self::extension_resource_topic_tokens(
                SCHEMA_ID_TOPIC_TOKEN,
                schema_id,
            ))
            .timeout(timeout)
            .build()
            .map_err(ErrorKind::from)?;

        let response = self
            .create_schema_version_command_invoker
            .invoke(request)
            .await
            .map_err(ErrorKind::from)?
            .map_err(ErrorKind::from)?;
        Ok(response.payload.into())
    }

    /// Retrieve an xRegistry [`SchemaVersionEntity`].
    ///
    /// # Arguments
    /// * `group_id` - The identifier of the Group that owns the Schema. If [`CloudDefault`](GroupId::CloudDefault), the default Group is used.
    /// * `schema_id` - The identifier of the Schema that owns the Version.
    /// * `version_id` - The [`GetVersionId`] selecting which Version to retrieve. If [`ResourceDefault`](GetVersionId::ResourceDefault), the default Version of the Resource is retrieved.
    /// * `timeout` - The duration until the client stops waiting for a response to the request, it is rounded up to the nearest second.
    ///
    /// Returns the requested [`SchemaVersionEntity`].
    ///
    /// # Errors
    /// [`struct@Error`] of kind [`ValidationError`](ErrorKind::ValidationError) if `timeout` is 0
    /// or > `u32::max`.
    ///
    /// [`struct@Error`] of kind [`AIOProtocolError`](ErrorKind::AIOProtocolError) if there are
    /// any underlying errors from the AIO RPC protocol.
    ///
    /// [`struct@Error`] of kind [`ServiceError`](ErrorKind::ServiceError) if an error is returned
    /// by the Edge Registry service.
    pub async fn get_schema_version(
        &self,
        group_id: GroupId,
        schema_id: String,
        version_id: GetVersionId<u64>,
        timeout: Duration,
    ) -> Result<SchemaVersionEntity, Error> {
        let payload = client_gen::GetSchemaVersionInputArguments {
            group_id: group_id.into(),
            version_id: version_id.into(),
        };

        let request = client_gen::GetSchemaVersionRequestBuilder::default()
            .payload(payload)
            .map_err(ErrorKind::from)?
            .topic_tokens(Self::extension_resource_topic_tokens(
                SCHEMA_ID_TOPIC_TOKEN,
                schema_id,
            ))
            .timeout(timeout)
            .build()
            .map_err(ErrorKind::from)?;

        let response = self
            .get_schema_version_command_invoker
            .invoke(request)
            .await
            .map_err(ErrorKind::from)?
            .map_err(ErrorKind::from)?;
        Ok(response.payload.into())
    }

    /// List the XIDs of the xRegistry Schema Versions matching the provided constraints.
    ///
    /// # Arguments
    /// * `groups` - Which Groups to list across: [`All`](GroupSelection::All), the
    ///   [`Default`](GroupSelection::Default) (cloud default) Group, or a specific
    ///   [`GroupId`](GroupSelection::GroupId).
    /// * `schema_id` - If provided, only Versions of this Schema are listed; otherwise Versions of
    ///   all Schemas in the selected Group(s) are listed.
    /// * `label` - If provided, only Versions carrying this [`Label`] are listed.
    /// * `timeout` - The duration until the client stops waiting for a response to the request, it is rounded up to the nearest second.
    ///
    /// Returns the [`VersionXId`]s of the Versions matching the constraints.
    ///
    /// # Errors
    /// [`struct@Error`] of kind [`ValidationError`](ErrorKind::ValidationError) if `timeout` is 0
    /// or > `u32::max`.
    ///
    /// [`struct@Error`] of kind [`AIOProtocolError`](ErrorKind::AIOProtocolError) if there are
    /// any underlying errors from the AIO RPC protocol.
    ///
    /// [`struct@Error`] of kind [`ServiceError`](ErrorKind::ServiceError) if an error is returned
    /// by the Edge Registry service.
    pub async fn list_schema_versions(
        &self,
        groups: GroupSelection,
        schema_id: Option<String>,
        label: Option<Label>,
        timeout: Duration,
    ) -> Result<Vec<VersionXId<u64>>, Error> {
        let (group_id, all_groups) = Self::group_selection_scope(groups);
        let payload = client_gen::ListVersionsRequestPayload {
            group_type: Some(SCHEMA_GROUP_TYPE.to_string()),
            group_id,
            all_groups,
            resource_type: Some(SCHEMA_RESOURCE_TYPE.to_string()),
            resource_id: schema_id,
            label: label.map(Into::into),
        };

        let request = client_gen::ListSchemaVersionsRequestBuilder::default()
            .payload(payload)
            .map_err(ErrorKind::from)?
            .timeout(timeout)
            .build()
            .map_err(ErrorKind::from)?;

        let response = self
            .list_schema_versions_command_invoker
            .invoke(request)
            .await
            .map_err(ErrorKind::from)?
            .map_err(ErrorKind::from)?;
        Ok(response.payload.into())
    }

    /// Delete an xRegistry Schema Version entity.
    ///
    /// # Arguments
    /// * `group_id` - The identifier of the Group that owns the Schema. If [`CloudDefault`](GroupId::CloudDefault), the default Group is used.
    /// * `schema_id` - The identifier of the Schema that owns the Version.
    /// * `version_id` - The identifier of the Version to delete.
    /// * `timeout` - The duration until the client stops waiting for a response to the request, it is rounded up to the nearest second.
    ///
    /// # Errors
    /// [`struct@Error`] of kind [`ValidationError`](ErrorKind::ValidationError) if `timeout` is 0
    /// or > `u32::max`.
    ///
    /// [`struct@Error`] of kind [`AIOProtocolError`](ErrorKind::AIOProtocolError) if there are
    /// any underlying errors from the AIO RPC protocol.
    ///
    /// [`struct@Error`] of kind [`ServiceError`](ErrorKind::ServiceError) if an error is returned
    /// by the Edge Registry service.
    pub async fn delete_schema_version(
        &self,
        group_id: GroupId,
        schema_id: String,
        version_id: u64,
        timeout: Duration,
    ) -> Result<(), Error> {
        let payload = client_gen::DeleteSchemaVersionInputArguments {
            group_id: group_id.into(),
        };

        let request = client_gen::DeleteSchemaVersionRequestBuilder::default()
            .payload(payload)
            .map_err(ErrorKind::from)?
            .topic_tokens(Self::extension_version_topic_tokens(
                SCHEMA_ID_TOPIC_TOKEN,
                schema_id,
                version_id,
            ))
            .timeout(timeout)
            .build()
            .map_err(ErrorKind::from)?;

        self.delete_schema_version_command_invoker
            .invoke(request)
            .await
            .map_err(ErrorKind::from)?
            .map_err(ErrorKind::from)?;
        Ok(())
    }

    // ~~~~~~~~~~~~~~~~~ Thing Description extension APIs ~~~~~~~~~~~~~~~~~~~~~

    /// Create a new xRegistry Thing Description Version entity under the specified Thing Description. The
    /// parent Thing Description is implicitly created if it doesn't already exist.
    ///
    /// # Arguments
    /// * `group_id` - The identifier of the Group that owns the Thing Description. If [`CloudDefault`](GroupId::CloudDefault), the default Group is used.
    /// * `thing_description_id` - The identifier of the Thing Description that owns the Version.
    /// * `thing_description_labels` - Queryable key/value pairs to be added to the parent Thing Description.
    /// * `version` - The [`ThingDescriptionVersionAttributes`] of the Version to create.
    /// * `timeout` - The duration until the client stops waiting for a response to the request, it is rounded up to the nearest second.
    ///
    /// Returns the created [`ThingDescriptionVersionEntity`] with epoch 1.
    ///
    /// # Errors
    /// [`struct@Error`] of kind [`ValidationError`](ErrorKind::ValidationError) if `timeout` is 0
    /// or > `u32::max`.
    ///
    /// [`struct@Error`] of kind [`AIOProtocolError`](ErrorKind::AIOProtocolError) if there are
    /// any underlying errors from the AIO RPC protocol.
    ///
    /// [`struct@Error`] of kind [`ServiceError`](ErrorKind::ServiceError) if an error is returned
    /// by the Edge Registry service.
    pub async fn create_thing_description_version(
        &self,
        group_id: GroupId,
        thing_description_id: String,
        thing_description_labels: Vec<Label>,
        version: ThingDescriptionVersionAttributes,
        timeout: Duration,
    ) -> Result<ThingDescriptionVersionEntity, Error> {
        let payload = version.into_gen(group_id.into(), thing_description_labels);

        let request = client_gen::CreateThingDescriptionVersionRequestBuilder::default()
            .payload(payload)
            .map_err(ErrorKind::from)?
            .topic_tokens(Self::extension_resource_topic_tokens(
                THING_DESCRIPTION_ID_TOPIC_TOKEN,
                thing_description_id,
            ))
            .timeout(timeout)
            .build()
            .map_err(ErrorKind::from)?;

        let response = self
            .create_thing_description_version_command_invoker
            .invoke(request)
            .await
            .map_err(ErrorKind::from)?
            .map_err(ErrorKind::from)?;
        Ok(response.payload.into())
    }

    /// Retrieve an xRegistry [`ThingDescriptionVersionEntity`].
    ///
    /// # Arguments
    /// * `group_id` - The identifier of the Group that owns the Thing Description. If [`CloudDefault`](GroupId::CloudDefault), the default Group is used.
    /// * `thing_description_id` - The identifier of the Thing Description that owns the Version.
    /// * `version_id` - The [`GetVersionId`] selecting which Version to retrieve. If [`ResourceDefault`](GetVersionId::ResourceDefault), the default Version of the Resource is retrieved.
    /// * `timeout` - The duration until the client stops waiting for a response to the request, it is rounded up to the nearest second.
    ///
    /// Returns the requested [`ThingDescriptionVersionEntity`].
    ///
    /// # Errors
    /// [`struct@Error`] of kind [`ValidationError`](ErrorKind::ValidationError) if `timeout` is 0
    /// or > `u32::max`.
    ///
    /// [`struct@Error`] of kind [`AIOProtocolError`](ErrorKind::AIOProtocolError) if there are
    /// any underlying errors from the AIO RPC protocol.
    ///
    /// [`struct@Error`] of kind [`ServiceError`](ErrorKind::ServiceError) if an error is returned
    /// by the Edge Registry service.
    pub async fn get_thing_description_version(
        &self,
        group_id: GroupId,
        thing_description_id: String,
        version_id: GetVersionId<u64>,
        timeout: Duration,
    ) -> Result<ThingDescriptionVersionEntity, Error> {
        let payload = client_gen::GetThingDescriptionVersionInputArguments {
            group_id: group_id.into(),
            version_id: version_id.into(),
        };

        let request = client_gen::GetThingDescriptionVersionRequestBuilder::default()
            .payload(payload)
            .map_err(ErrorKind::from)?
            .topic_tokens(Self::extension_resource_topic_tokens(
                THING_DESCRIPTION_ID_TOPIC_TOKEN,
                thing_description_id,
            ))
            .timeout(timeout)
            .build()
            .map_err(ErrorKind::from)?;

        let response = self
            .get_thing_description_version_command_invoker
            .invoke(request)
            .await
            .map_err(ErrorKind::from)?
            .map_err(ErrorKind::from)?;
        Ok(response.payload.into())
    }

    /// List the XIDs of xRegistry Thing Description Versions matching the provided constraints.
    ///
    /// # Arguments
    /// * `groups` - Which Groups to list across: [`All`](GroupSelection::All), the
    ///   [`Default`](GroupSelection::Default) (cloud default) Group, or a specific
    ///   [`GroupId`](GroupSelection::GroupId).
    /// * `thing_description_id` - If provided, only Versions of this Thing Description are listed;
    ///   otherwise Versions of all Thing Descriptions in the selected Group(s) are listed.
    /// * `label` - If provided, only Versions carrying this [`Label`] are listed.
    /// * `timeout` - The duration until the client stops waiting for a response to the request, it is rounded up to the nearest second.
    ///
    /// Returns the [`VersionXId`]s of the Versions matching the constraints.
    ///
    /// # Errors
    /// [`struct@Error`] of kind [`ValidationError`](ErrorKind::ValidationError) if `timeout` is 0
    /// or > `u32::max`.
    ///
    /// [`struct@Error`] of kind [`AIOProtocolError`](ErrorKind::AIOProtocolError) if there are
    /// any underlying errors from the AIO RPC protocol.
    ///
    /// [`struct@Error`] of kind [`ServiceError`](ErrorKind::ServiceError) if an error is returned
    /// by the Edge Registry service.
    pub async fn list_thing_description_versions(
        &self,
        groups: GroupSelection,
        thing_description_id: Option<String>,
        label: Option<Label>,
        timeout: Duration,
    ) -> Result<Vec<VersionXId<u64>>, Error> {
        let (group_id, all_groups) = Self::group_selection_scope(groups);
        let payload = client_gen::ListVersionsRequestPayload {
            group_type: Some(THING_DESCRIPTION_GROUP_TYPE.to_string()),
            group_id,
            all_groups,
            resource_type: Some(THING_DESCRIPTION_RESOURCE_TYPE.to_string()),
            resource_id: thing_description_id,
            label: label.map(Into::into),
        };

        let request = client_gen::ListThingDescriptionVersionsRequestBuilder::default()
            .payload(payload)
            .map_err(ErrorKind::from)?
            .timeout(timeout)
            .build()
            .map_err(ErrorKind::from)?;

        let response = self
            .list_thing_description_versions_command_invoker
            .invoke(request)
            .await
            .map_err(ErrorKind::from)?
            .map_err(ErrorKind::from)?;
        Ok(response.payload.into())
    }

    /// Delete an xRegistry Thing Description Version entity.
    ///
    /// # Arguments
    /// * `group_id` - The identifier of the Group that owns the Thing Description. If [`CloudDefault`](GroupId::CloudDefault), the default Group is used.
    /// * `thing_description_id` - The identifier of the Thing Description that owns the Version.
    /// * `version_id` - The identifier of the Version to delete.
    /// * `timeout` - The duration until the client stops waiting for a response to the request, it is rounded up to the nearest second.
    ///
    /// # Errors
    /// [`struct@Error`] of kind [`ValidationError`](ErrorKind::ValidationError) if `timeout` is 0
    /// or > `u32::max`.
    ///
    /// [`struct@Error`] of kind [`AIOProtocolError`](ErrorKind::AIOProtocolError) if there are
    /// any underlying errors from the AIO RPC protocol.
    ///
    /// [`struct@Error`] of kind [`ServiceError`](ErrorKind::ServiceError) if an error is returned
    /// by the Edge Registry service.
    pub async fn delete_thing_description_version(
        &self,
        group_id: GroupId,
        thing_description_id: String,
        version_id: u64,
        timeout: Duration,
    ) -> Result<(), Error> {
        let payload = client_gen::DeleteThingDescriptionVersionInputArguments {
            group_id: group_id.into(),
        };

        let request = client_gen::DeleteThingDescriptionVersionRequestBuilder::default()
            .payload(payload)
            .map_err(ErrorKind::from)?
            .topic_tokens(Self::extension_version_topic_tokens(
                THING_DESCRIPTION_ID_TOPIC_TOKEN,
                thing_description_id,
                version_id,
            ))
            .timeout(timeout)
            .build()
            .map_err(ErrorKind::from)?;

        self.delete_thing_description_version_command_invoker
            .invoke(request)
            .await
            .map_err(ErrorKind::from)?
            .map_err(ErrorKind::from)?;
        Ok(())
    }

    // ~~~~~~~~~~~~~~~~~ Thing Model extension APIs ~~~~~~~~~~~~~~~~~~~~~

    /// Create a new xRegistry Thing Model Version entity under the specified Thing Model. The parent
    /// Thing Model is implicitly created if it doesn't already exist.
    ///
    /// # Arguments
    /// * `group_id` - The identifier of the Group that owns the Thing Model. If [`CloudDefault`](GroupId::CloudDefault), the default Group is used.
    /// * `thing_model_id` - The identifier of the Thing Model that owns the Version.
    /// * `thing_model_labels` - Queryable key/value pairs to be added to the parent Thing Model.
    /// * `version` - The [`ThingModelVersionAttributes`] of the Version to create.
    /// * `timeout` - The duration until the client stops waiting for a response to the request, it is rounded up to the nearest second.
    ///
    /// Returns the created [`ThingModelVersionEntity`] with epoch 1.
    ///
    /// # Errors
    /// [`struct@Error`] of kind [`ValidationError`](ErrorKind::ValidationError) if `timeout` is 0
    /// or > `u32::max`.
    ///
    /// [`struct@Error`] of kind [`AIOProtocolError`](ErrorKind::AIOProtocolError) if there are
    /// any underlying errors from the AIO RPC protocol.
    ///
    /// [`struct@Error`] of kind [`ServiceError`](ErrorKind::ServiceError) if an error is returned
    /// by the Edge Registry service.
    pub async fn create_thing_model_version(
        &self,
        group_id: GroupId,
        thing_model_id: String,
        thing_model_labels: Vec<Label>,
        version: ThingModelVersionAttributes,
        timeout: Duration,
    ) -> Result<ThingModelVersionEntity, Error> {
        let payload = version.into_gen(group_id.into(), thing_model_labels);

        let request = client_gen::CreateThingModelVersionRequestBuilder::default()
            .payload(payload)
            .map_err(ErrorKind::from)?
            .topic_tokens(Self::extension_resource_topic_tokens(
                THING_MODEL_ID_TOPIC_TOKEN,
                thing_model_id,
            ))
            .timeout(timeout)
            .build()
            .map_err(ErrorKind::from)?;

        let response = self
            .create_thing_model_version_command_invoker
            .invoke(request)
            .await
            .map_err(ErrorKind::from)?
            .map_err(ErrorKind::from)?;
        Ok(response.payload.into())
    }

    /// Retrieve an xRegistry [`ThingModelVersionEntity`] entity.
    ///
    /// # Arguments
    /// * `group_id` - The identifier of the Group that owns the Thing Model. If [`CloudDefault`](GroupId::CloudDefault), the default Group is used.
    /// * `thing_model_id` - The identifier of the Thing Model that owns the Version.
    /// * `version_id` - The [`GetVersionId`] selecting which Version to retrieve. If [`ResourceDefault`](GetVersionId::ResourceDefault), the default Version of the Resource is retrieved.
    /// * `timeout` - The duration until the client stops waiting for a response to the request, it is rounded up to the nearest second.
    ///
    /// Returns the requested [`ThingModelVersionEntity`].
    ///
    /// # Errors
    /// [`struct@Error`] of kind [`ValidationError`](ErrorKind::ValidationError) if `timeout` is 0
    /// or > `u32::max`.
    ///
    /// [`struct@Error`] of kind [`AIOProtocolError`](ErrorKind::AIOProtocolError) if there are
    /// any underlying errors from the AIO RPC protocol.
    ///
    /// [`struct@Error`] of kind [`ServiceError`](ErrorKind::ServiceError) if an error is returned
    /// by the Edge Registry service.
    pub async fn get_thing_model_version(
        &self,
        group_id: GroupId,
        thing_model_id: String,
        version_id: GetVersionId<u64>,
        timeout: Duration,
    ) -> Result<ThingModelVersionEntity, Error> {
        let payload = client_gen::GetThingModelVersionInputArguments {
            group_id: group_id.into(),
            version_id: version_id.into(),
        };

        let request = client_gen::GetThingModelVersionRequestBuilder::default()
            .payload(payload)
            .map_err(ErrorKind::from)?
            .topic_tokens(Self::extension_resource_topic_tokens(
                THING_MODEL_ID_TOPIC_TOKEN,
                thing_model_id,
            ))
            .timeout(timeout)
            .build()
            .map_err(ErrorKind::from)?;

        let response = self
            .get_thing_model_version_command_invoker
            .invoke(request)
            .await
            .map_err(ErrorKind::from)?
            .map_err(ErrorKind::from)?;
        Ok(response.payload.into())
    }

    /// List the XIDs of xRegistry Thing Model Versions matching the provided constraints.
    ///
    /// # Arguments
    /// * `groups` - Which Groups to list across: [`All`](GroupSelection::All), the
    ///   [`Default`](GroupSelection::Default) (cloud default) Group, or a specific
    ///   [`GroupId`](GroupSelection::GroupId).
    /// * `thing_model_id` - If provided, only Versions of this Thing Model are listed; otherwise
    ///   Versions of all Thing Models in the selected Group(s) are listed.
    /// * `label` - If provided, only Versions carrying this [`Label`] are listed.
    /// * `timeout` - The duration until the client stops waiting for a response to the request, it is rounded up to the nearest second.
    ///
    /// Returns the [`VersionXId`]s of the Versions matching the constraints.
    ///
    /// # Errors
    /// [`struct@Error`] of kind [`ValidationError`](ErrorKind::ValidationError) if `timeout` is 0
    /// or > `u32::max`.
    ///
    /// [`struct@Error`] of kind [`AIOProtocolError`](ErrorKind::AIOProtocolError) if there are
    /// any underlying errors from the AIO RPC protocol.
    ///
    /// [`struct@Error`] of kind [`ServiceError`](ErrorKind::ServiceError) if an error is returned
    /// by the Edge Registry service.
    pub async fn list_thing_model_versions(
        &self,
        groups: GroupSelection,
        thing_model_id: Option<String>,
        label: Option<Label>,
        timeout: Duration,
    ) -> Result<Vec<VersionXId<u64>>, Error> {
        let (group_id, all_groups) = Self::group_selection_scope(groups);
        let payload = client_gen::ListVersionsRequestPayload {
            group_type: Some(THING_MODEL_GROUP_TYPE.to_string()),
            group_id,
            all_groups,
            resource_type: Some(THING_MODEL_RESOURCE_TYPE.to_string()),
            resource_id: thing_model_id,
            label: label.map(Into::into),
        };

        let request = client_gen::ListThingModelVersionsRequestBuilder::default()
            .payload(payload)
            .map_err(ErrorKind::from)?
            .timeout(timeout)
            .build()
            .map_err(ErrorKind::from)?;

        let response = self
            .list_thing_model_versions_command_invoker
            .invoke(request)
            .await
            .map_err(ErrorKind::from)?
            .map_err(ErrorKind::from)?;
        Ok(response.payload.into())
    }

    /// Delete an xRegistry Thing Model Version entity.
    ///
    /// # Arguments
    /// * `group_id` - The identifier of the Group that owns the Thing Model. If [`CloudDefault`](GroupId::CloudDefault), the default Group is used.
    /// * `thing_model_id` - The identifier of the Thing Model that owns the Version.
    /// * `version_id` - The identifier of the Version to delete.
    /// * `timeout` - The duration until the client stops waiting for a response to the request, it is rounded up to the nearest second.
    ///
    /// # Errors
    /// [`struct@Error`] of kind [`ValidationError`](ErrorKind::ValidationError) if `timeout` is 0
    /// or > `u32::max`.
    ///
    /// [`struct@Error`] of kind [`AIOProtocolError`](ErrorKind::AIOProtocolError) if there are
    /// any underlying errors from the AIO RPC protocol.
    ///
    /// [`struct@Error`] of kind [`ServiceError`](ErrorKind::ServiceError) if an error is returned
    /// by the Edge Registry service.
    pub async fn delete_thing_model_version(
        &self,
        group_id: GroupId,
        thing_model_id: String,
        version_id: u64,
        timeout: Duration,
    ) -> Result<(), Error> {
        let payload = client_gen::DeleteThingModelVersionInputArguments {
            group_id: group_id.into(),
        };

        let request = client_gen::DeleteThingModelVersionRequestBuilder::default()
            .payload(payload)
            .map_err(ErrorKind::from)?
            .topic_tokens(Self::extension_version_topic_tokens(
                THING_MODEL_ID_TOPIC_TOKEN,
                thing_model_id,
                version_id,
            ))
            .timeout(timeout)
            .build()
            .map_err(ErrorKind::from)?;

        self.delete_thing_model_version_command_invoker
            .invoke(request)
            .await
            .map_err(ErrorKind::from)?
            .map_err(ErrorKind::from)?;
        Ok(())
    }

    // ~~~~~~~~~~~~~~~~~ General APIs ~~~~~~~~~~~~~~~~~~~~~

    /// Shutdown the [`Client`]. Shuts down the underlying command invokers.
    ///
    /// Note: If this method is called, the [`Client`] should not be used again.
    /// If the method returns an error, it may be called again to re-attempt unsubscribing.
    ///
    /// Returns Ok(()) on success, otherwise returns [`struct@Error`].
    ///
    /// # Errors
    /// [`struct@Error`] of kind [`ShutdownError`](ErrorKind::ShutdownError)
    /// if any of the invoker unsubscribes fail or if the unsuback reason code doesn't indicate success.
    /// This will be a vector of any shutdown errors, all invokers will attempt to be shutdown.
    pub async fn shutdown(&self) -> Result<(), Error> {
        let (
            create_group,
            get_group,
            list_groups,
            delete_group,
            create_resource,
            get_resource,
            list_resources,
            delete_resource,
            create_version,
            get_version,
            list_versions,
            delete_version,
            create_schema_version,
            get_schema_version,
            list_schema_versions,
            delete_schema_version,
            create_thing_description_version,
            get_thing_description_version,
            list_thing_description_versions,
            delete_thing_description_version,
            create_thing_model_version,
            get_thing_model_version,
            list_thing_model_versions,
            delete_thing_model_version,
        ) = tokio::join!(
            self.create_group_command_invoker.shutdown(),
            self.get_group_command_invoker.shutdown(),
            self.list_groups_command_invoker.shutdown(),
            self.delete_group_command_invoker.shutdown(),
            self.create_resource_command_invoker.shutdown(),
            self.get_resource_command_invoker.shutdown(),
            self.list_resources_command_invoker.shutdown(),
            self.delete_resource_command_invoker.shutdown(),
            self.create_version_command_invoker.shutdown(),
            self.get_version_command_invoker.shutdown(),
            self.list_versions_command_invoker.shutdown(),
            self.delete_version_command_invoker.shutdown(),
            self.create_schema_version_command_invoker.shutdown(),
            self.get_schema_version_command_invoker.shutdown(),
            self.list_schema_versions_command_invoker.shutdown(),
            self.delete_schema_version_command_invoker.shutdown(),
            self.create_thing_description_version_command_invoker
                .shutdown(),
            self.get_thing_description_version_command_invoker
                .shutdown(),
            self.list_thing_description_versions_command_invoker
                .shutdown(),
            self.delete_thing_description_version_command_invoker
                .shutdown(),
            self.create_thing_model_version_command_invoker.shutdown(),
            self.get_thing_model_version_command_invoker.shutdown(),
            self.list_thing_model_versions_command_invoker.shutdown(),
            self.delete_thing_model_version_command_invoker.shutdown(),
        );

        let mut errors = Vec::new();
        for result in [
            create_group,
            get_group,
            list_groups,
            delete_group,
            create_resource,
            get_resource,
            list_resources,
            delete_resource,
            create_version,
            get_version,
            list_versions,
            delete_version,
            create_schema_version,
            get_schema_version,
            list_schema_versions,
            delete_schema_version,
            create_thing_description_version,
            get_thing_description_version,
            list_thing_description_versions,
            delete_thing_description_version,
            create_thing_model_version,
            get_thing_model_version,
            list_thing_model_versions,
            delete_thing_model_version,
        ] {
            if let Err(e) = result {
                errors.push(e);
            }
        }

        if errors.is_empty() {
            log::info!("Edge Registry Client shutdown done gracefully");
            Ok(())
        } else {
            Err(Error::from(ErrorKind::ShutdownError(errors)))
        }
    }

    // ~~~~~~~~~~~~~~~~~ Helpers ~~~~~~~~~~~~~~~~~~~~~

    /// Builds the topic tokens for a Group-scoped request.
    fn group_topic_tokens(group_type: String) -> HashMap<String, String> {
        HashMap::from([(GROUP_TYPE_TOPIC_TOKEN.to_string(), group_type)])
    }

    /// Builds the topic tokens for a Resource-scoped request.
    fn resource_topic_tokens(
        group_type: String,
        resource_type: String,
        resource_id: String,
    ) -> HashMap<String, String> {
        HashMap::from([
            (GROUP_TYPE_TOPIC_TOKEN.to_string(), group_type),
            (RESOURCE_TYPE_TOPIC_TOKEN.to_string(), resource_type),
            (RESOURCE_ID_TOPIC_TOKEN.to_string(), resource_id),
        ])
    }

    /// Builds the topic tokens for a Version-scoped request that carries the Version id in the topic.
    fn version_topic_tokens(
        group_type: String,
        resource_type: String,
        resource_id: String,
        version_id: String,
    ) -> HashMap<String, String> {
        let mut tokens = Self::resource_topic_tokens(group_type, resource_type, resource_id);
        tokens.insert(VERSION_ID_TOPIC_TOKEN.to_string(), version_id);
        tokens
    }

    /// Builds the topic tokens for an extension Resource-scoped request, keyed by the extension's
    /// Resource-identifier token (e.g. `schemaId`, `thingDescriptionId`, `thingModelId`).
    fn extension_resource_topic_tokens(
        resource_id_token: &str,
        resource_id: String,
    ) -> HashMap<String, String> {
        HashMap::from([(resource_id_token.to_string(), resource_id)])
    }

    /// Builds the topic tokens for an extension Version request that carries the Version Id in the
    /// topic, keyed by the extension's Resource-identifier token.
    fn extension_version_topic_tokens(
        resource_id_token: &str,
        resource_id: String,
        version_id: u64,
    ) -> HashMap<String, String> {
        let mut tokens = Self::extension_resource_topic_tokens(resource_id_token, resource_id);
        tokens.insert(VERSION_ID_TOPIC_TOKEN.to_string(), version_id.to_string());
        tokens
    }

    /// Resolves a [`GroupQuery`] into the `(group_type, group_id, all_groups)` scope fields used by
    /// the list request payloads.
    fn group_query_scope(query: GroupQuery) -> (Option<String>, Option<String>, bool) {
        match query {
            GroupQuery::AllGroupTypes(AnyGroupSelection::All) => (None, None, true),
            GroupQuery::AllGroupTypes(AnyGroupSelection::GroupId(group_id)) => {
                (None, Some(group_id), false)
            }
            GroupQuery::GroupType { group_type, groups } => match groups {
                GroupSelection::All => (Some(group_type), None, true),
                GroupSelection::GroupId(group_id) => (Some(group_type), Some(group_id), false),
                GroupSelection::Default => (Some(group_type), None, false),
            },
        }
    }

    /// Resolves a [`GroupSelection`] into the `(group_id, all_groups)` scope fields for an extension
    /// list request, whose Group type is fixed by the extension.
    fn group_selection_scope(groups: GroupSelection) -> (Option<String>, bool) {
        match groups {
            GroupSelection::All => (None, true),
            GroupSelection::GroupId(group_id) => (Some(group_id), false),
            GroupSelection::Default => (None, false),
        }
    }
}
