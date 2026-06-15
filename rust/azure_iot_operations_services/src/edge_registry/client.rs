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
use crate::edge_registry::models::xregistry::{extensions_to_gen, labels_to_gen};
use crate::edge_registry::models::{
    GroupAttributes, GroupEntity, ResourceEntity, ResourceMetaAttributes, ResourceXId,
    VersionAttributes, VersionEntity, VersionXId,
};
use crate::edge_registry::{
    AnyGroupSelection, Error, ErrorKind, GetVersionId, GroupId, GroupQuery, GroupSelection, Label,
};

const GROUP_TYPE_TOPIC_TOKEN: &str = "groupType";
const RESOURCE_TYPE_TOPIC_TOKEN: &str = "resourceType";
const RESOURCE_ID_TOPIC_TOKEN: &str = "resourceId";
const VERSION_ID_TOPIC_TOKEN: &str = "versionId";

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
                application_context,
                client.clone(),
                &options,
            )),
        }
    }

    // ~~~~~~~~~~~~~~~~~ Group APIs ~~~~~~~~~~~~~~~~~~~~~

    /// Create a new xRegistry Group.
    ///
    /// # Arguments
    /// * `group_type` - The type of the Group to create.
    /// * `group_id` - The identifier of the Group to create. If [`CloudDefault`](GroupId::CloudDefault), create the default Group of the Group type.
    /// * `attributes` - The [`GroupAttributes`] for the new Group.
    /// * `timeout` - The duration until the client stops waiting for a response to the request, it is rounded up to the nearest second.
    ///
    /// Returns the created [`Group`] with epoch 1.
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
        attributes: GroupAttributes,
        timeout: Duration,
    ) -> Result<GroupEntity, Error> {
        let payload: client_gen::GroupAttributes = attributes.into(group_id.into());

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

    /// Retrieve an xRegistry [`Group`] entity.
    ///
    /// # Arguments
    /// * `group_type` - The type of the Group to retrieve.
    /// * `group_id` - The identifier of the Group to retrieve. If [`CloudDefault`](GroupId::CloudDefault), retrieve the default Group of the Group type.
    /// * `timeout` - The duration until the client stops waiting for a response to the request, it is rounded up to the nearest second.
    ///
    /// Returns the requested [`Group`].
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
    ) -> Result<GroupEntity, Error> {
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
    /// * `resource_meta_attributes` - The [`ResourceMetaAttributes`] for the Resource's `meta` sub-entity.
    /// * `resource_extensions` - Extension-specific attributes for the Resource.
    /// * `default_version_id` - The identifier for the Resource's default Version. If [`None`], the server determines the versionId.
    /// * `default_version_attributes` - The [`VersionAttributes`] of the Resource's default Version, created along with the Resource.
    /// * `timeout` - The duration until the client stops waiting for a response to the request, it is rounded up to the nearest second.
    ///
    /// Returns the created [`Resource`] with epoch 1.
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
        resource_meta_attributes: ResourceMetaAttributes,
        resource_extensions: HashMap<String, Bytes>,
        default_version_id: Option<String>,
        default_version_attributes: VersionAttributes,
        timeout: Duration,
    ) -> Result<ResourceEntity, Error> {
        let payload = client_gen::CreateResourceRequestPayload {
            group_id: group_id.into(),
            meta: resource_meta_attributes.into(),
            default_version: default_version_attributes.into(),
            default_version_id,
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

    /// Retrieve an xRegistry [`Resource`] entity.
    ///
    /// # Arguments
    /// * `group_type` - The type of the Group that owns the Resource.
    /// * `group_id` - The identifier of the Group that owns the Resource. If [`CloudDefault`](GroupId::CloudDefault), the default Group of the Group type is used.
    /// * `resource_type` - The type of the Resource to retrieve.
    /// * `resource_id` - The identifier of the Resource to retrieve.
    /// * `timeout` - The duration until the client stops waiting for a response to the request, it is rounded up to the nearest second.
    ///
    /// Returns the requested [`Resource`].
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
    ) -> Result<ResourceEntity, Error> {
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
    /// * `version_id` - The identifier of the Version to create. If [`None`], the server determines the versionId.
    /// * `version` - The [`VersionAttributes`] of the Version to create.
    /// * `timeout` - The duration until the client stops waiting for a response to the request, it is rounded up to the nearest second.
    ///
    /// Returns the created [`Version`].
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
        version_id: Option<String>,
        version: VersionAttributes,
        timeout: Duration,
    ) -> Result<VersionEntity, Error> {
        let payload = client_gen::CreateVersionRequestPayload {
            group_id: group_id.into(),
            version_id,
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

    /// Retrieve an xRegistry [`Version`] entity.
    ///
    /// # Arguments
    /// * `group_type` - The type of the Group that owns the Resource.
    /// * `group_id` - The identifier of the Group that owns the Resource. If [`CloudDefault`](GroupId::CloudDefault), the default Group of the Group type is used.
    /// * `resource_type` - The type of the Resource that owns the Version.
    /// * `resource_id` - The identifier of the Resource that owns the Version.
    /// * `version_id` - The [`GetVersionId`] selecting which Version to retrieve. If [`ResourceDefault`](GetVersionId::ResourceDefault), the default Version of the Resource is retrieved.
    /// * `timeout` - The duration until the client stops waiting for a response to the request, it is rounded up to the nearest second.
    ///
    /// Returns the requested [`Version`].
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
        version_id: GetVersionId,
        timeout: Duration,
    ) -> Result<VersionEntity, Error> {
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
    ) -> Result<Vec<VersionXId>, Error> {
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
}
