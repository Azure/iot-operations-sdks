// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

use std::{collections::HashMap, sync::Arc, time::Duration};

use azure_iot_operations_mqtt::session::SessionManagedClient;
use azure_iot_operations_protocol::application::ApplicationContext;

use crate::edge_registry::{
    CreateSchemaVersionAttributes, CreateThingDescriptionVersionAttributes, Error, ErrorKind,
    Group, GroupAttributes, SchemaGroup, SchemaGroupAttributes, SchemaMetaAttributes,
    SchemaVersion, ServiceError, ThingDescriptionGroup, ThingDescriptionMetaAttributes,
    ThingDescriptionVersion,
    edge_registry_gen::{
        common_types::{b64::Bytes, options::CommandInvokerOptionsBuilder},
        edge_registry_client::client as er_client_gen,
    },
};

const GROUP_TYPE_TOPIC_TOKEN: &str = "groupType";
const RESOURCE_TYPE_TOPIC_TOKEN: &str = "resourceType";
const RESOURCE_ID_TOPIC_TOKEN: &str = "resourceId";
const VERSION_ID_TOPIC_TOKEN: &str = "versionId";

const SCHEMA_ID_TOPIC_TOKEN: &str = "schemaId";
const THING_DESCRIPTION_ID_TOPIC_TOKEN: &str = "thingDescriptionId";

/// Edge Registry client implementation.
#[derive(Clone)]
pub struct Client {
    // create_command_invoker: Arc<er_client_gen::CreateActionInvoker>,
    // get_command_invoker: Arc<er_client_gen::GetActionInvoker>,
    list_groups_command_invoker: Arc<er_client_gen::ListGroupsActionInvoker>,
    create_group_command_invoker: Arc<er_client_gen::CreateGroupActionInvoker>,
    get_group_command_invoker: Arc<er_client_gen::GetGroupActionInvoker>,
    // create_resource_command_invoker: Arc<er_client_gen::CreateResourceActionInvoker>,
    list_resources_command_invoker: Arc<er_client_gen::ListResourcesActionInvoker>,
    get_resource_command_invoker: Arc<er_client_gen::GetResourceActionInvoker>,
    // create_version_command_invoker: Arc<er_client_gen::CreateVersionActionInvoker>,
    list_versions_command_invoker: Arc<er_client_gen::ListVersionsActionInvoker>,
    get_version_command_invoker: Arc<er_client_gen::GetVersionActionInvoker>,
    // list_command_invoker: Arc<er_client_gen::ListActionInvoker>,
    // delete_command_invoker: Arc<er_client_gen::DeleteActionInvoker>,

    // schema extension
    // get_schema_group_command_invoker: Arc<er_client_gen::GetSchemaGroupActionInvoker>,
    // create_schema_group_command_invoker: Arc<er_client_gen::CreateSchemaGroupActionInvoker>,
    // get_schema_command_invoker: Arc<er_client_gen::GetSchemaActionInvoker>,
    // create_schema_command_invoker: Arc<er_client_gen::CreateSchemaActionInvoker>,
    get_schema_version_command_invoker: Arc<er_client_gen::GetSchemaVersionActionInvoker>,
    create_schema_version_command_invoker: Arc<er_client_gen::CreateSchemaVersionActionInvoker>,
    list_schema_versions_command_invoker: Arc<er_client_gen::ListSchemaVersionsActionInvoker>,

    // thing description extension
    // get_thing_description_group_command_invoker:
    //     Arc<er_client_gen::GetThingDescriptionGroupActionInvoker>,
    // create_thing_description_group_command_invoker:
    //     Arc<er_client_gen::CreateThingDescriptionGroupActionInvoker>,
    // get_thing_description_command_invoker: Arc<er_client_gen::GetThingDescriptionActionInvoker>,
    // create_thing_description_command_invoker:
    //     Arc<er_client_gen::CreateThingDescriptionActionInvoker>,
    get_thing_description_version_command_invoker:
        Arc<er_client_gen::GetThingDescriptionVersionActionInvoker>,
    create_thing_description_version_command_invoker:
        Arc<er_client_gen::CreateThingDescriptionVersionActionInvoker>,
}

impl Client {
    /// Create a new Edge Registry Client.
    ///
    /// # Panics
    /// Panics if the options for the underlying command invokers cannot be built. Not possible since
    /// the options are statically generated.
    #[must_use]
    pub fn new(application_context: ApplicationContext, client: SessionManagedClient) -> Self {
        let options = CommandInvokerOptionsBuilder::default()
            .build()
            .expect("Statically generated options should not fail.");

        Self {
            create_group_command_invoker: Arc::new(er_client_gen::CreateGroupActionInvoker::new(
                application_context.clone(),
                client.clone(),
                &options,
            )),
            // create_resource_command_invoker: Arc::new(
            //     er_client_gen::CreateResourceActionInvoker::new(
            //         application_context.clone(),
            //         client.clone(),
            //         &options,
            //     ),
            // ),
            // create_version_command_invoker: Arc::new(
            //     er_client_gen::CreateVersionActionInvoker::new(
            //         application_context.clone(),
            //         client.clone(),
            //         &options,
            //     ),
            // ),
            // create_command_invoker: Arc::new(er_client_gen::CreateActionInvoker::new(
            //     application_context.clone(),
            //     client.clone(),
            //     &options,
            // )),
            // get_command_invoker: Arc::new(er_client_gen::GetActionInvoker::new(
            //     application_context.clone(),
            //     client.clone(),
            //     &options,
            // )),
            list_groups_command_invoker: Arc::new(er_client_gen::ListGroupsActionInvoker::new(
                application_context.clone(),
                client.clone(),
                &options,
            )),
            get_group_command_invoker: Arc::new(er_client_gen::GetGroupActionInvoker::new(
                application_context.clone(),
                client.clone(),
                &options,
            )),
            list_resources_command_invoker: Arc::new(
                er_client_gen::ListResourcesActionInvoker::new(
                    application_context.clone(),
                    client.clone(),
                    &options,
                ),
            ),
            get_resource_command_invoker: Arc::new(er_client_gen::GetResourceActionInvoker::new(
                application_context.clone(),
                client.clone(),
                &options,
            )),
            list_versions_command_invoker: Arc::new(er_client_gen::ListVersionsActionInvoker::new(
                application_context.clone(),
                client.clone(),
                &options,
            )),
            get_version_command_invoker: Arc::new(er_client_gen::GetVersionActionInvoker::new(
                application_context.clone(),
                client.clone(),
                &options,
            )),
            // list_command_invoker: Arc::new(er_client_gen::ListActionInvoker::new(
            //     application_context.clone(),
            //     client.clone(),
            //     &options,
            // )),
            // delete_command_invoker: Arc::new(er_client_gen::DeleteActionInvoker::new(
            //     application_context.clone(),
            //     client.clone(),
            //     &options,
            // )),
            // schema extension
            // get_schema_group_command_invoker: Arc::new(
            //     er_client_gen::GetSchemaGroupActionInvoker::new(
            //         application_context.clone(),
            //         client.clone(),
            //         &options,
            //     ),
            // ),
            // create_schema_group_command_invoker: Arc::new(
            //     er_client_gen::CreateSchemaGroupActionInvoker::new(
            //         application_context.clone(),
            //         client.clone(),
            //         &options,
            //     ),
            // ),
            // get_schema_command_invoker: Arc::new(er_client_gen::GetSchemaActionInvoker::new(
            //     application_context.clone(),
            //     client.clone(),
            //     &options,
            // )),
            get_schema_version_command_invoker: Arc::new(
                er_client_gen::GetSchemaVersionActionInvoker::new(
                    application_context.clone(),
                    client.clone(),
                    &options,
                ),
            ),
            create_schema_version_command_invoker: Arc::new(
                er_client_gen::CreateSchemaVersionActionInvoker::new(
                    application_context.clone(),
                    client.clone(),
                    &options,
                ),
            ),
            // create_schema_command_invoker: Arc::new(er_client_gen::CreateSchemaActionInvoker::new(
            //     application_context.clone(),
            //     client.clone(),
            //     &options,
            // )),
            list_schema_versions_command_invoker: Arc::new(
                er_client_gen::ListSchemaVersionsActionInvoker::new(
                    application_context.clone(),
                    client.clone(),
                    &options,
                ),
            ),

            // thing description extension
            // get_thing_description_group_command_invoker: Arc::new(
            //     er_client_gen::GetThingDescriptionGroupActionInvoker::new(
            //         application_context.clone(),
            //         client.clone(),
            //         &options,
            //     ),
            // ),
            // create_thing_description_group_command_invoker: Arc::new(
            //     er_client_gen::CreateThingDescriptionGroupActionInvoker::new(
            //         application_context.clone(),
            //         client.clone(),
            //         &options,
            //     ),
            // ),
            // get_thing_description_command_invoker: Arc::new(
            //     er_client_gen::GetThingDescriptionActionInvoker::new(
            //         application_context.clone(),
            //         client.clone(),
            //         &options,
            //     ),
            // ),
            get_thing_description_version_command_invoker: Arc::new(
                er_client_gen::GetThingDescriptionVersionActionInvoker::new(
                    application_context.clone(),
                    client.clone(),
                    &options,
                ),
            ),
            // create_thing_description_command_invoker: Arc::new(
            //     er_client_gen::CreateThingDescriptionActionInvoker::new(
            //         application_context.clone(),
            //         client.clone(),
            //         &options,
            //     ),
            // ),
            create_thing_description_version_command_invoker: Arc::new(
                er_client_gen::CreateThingDescriptionVersionActionInvoker::new(
                    application_context.clone(),
                    client.clone(),
                    &options,
                ),
            ),
        }
    }

    // pub async fn list(&self, xid: String, timeout: Duration) -> Result<Vec<String>, Error> {
    //     let payload = er_client_gen::ListInputArguments { xid };
    //     let request = er_client_gen::ListRequestBuilder::default()
    //         .payload(payload)
    //         .map_err(ErrorKind::from)?
    //         .timeout(timeout)
    //         .build()
    //         .map_err(ErrorKind::from)?;
    //     Ok(self
    //         .list_command_invoker
    //         .invoke(request)
    //         .await
    //         .map_err(ErrorKind::from)?
    //         .map_err(|err_response| ErrorKind::from(ServiceError::from(err_response.payload)))?
    //         .payload
    //         .ids)
    // }

    // -----------------------------------------------------------------------
    // Groups
    // -----------------------------------------------------------------------

    pub async fn list_groups(
        &self,
        group_type: String,
        timeout: Duration,
    ) -> Result<Vec<String>, Error> {
        let request = er_client_gen::ListGroupsRequestBuilder::default()
            .topic_tokens(HashMap::from([(
                GROUP_TYPE_TOPIC_TOKEN.to_string(),
                group_type,
            )]))
            .timeout(timeout)
            .build()
            .map_err(ErrorKind::from)?;
        Ok(self
            .list_groups_command_invoker
            .invoke(request)
            .await
            .map_err(ErrorKind::from)?
            .map_err(|err_response| ErrorKind::from(ServiceError::from(err_response.payload)))?
            .payload
            .ids)
    }

    /// If group_id is None, it uses the default
    pub async fn get_group(
        &self,
        group_type: String,
        group_id: Option<String>,
        timeout: Duration,
    ) -> Result<Group, Error> {
        let payload = er_client_gen::GetGroupInputArguments {
            // group_type,
            group_id,
        };
        // let topic_tokens = HashMap::from([
        //     ("groupType".to_string(), group_type.to_string()),
        //     ("groupId".to_string(), group_id.to_string()),
        // ]);
        let request = er_client_gen::GetGroupRequestBuilder::default()
            // .topic_tokens(topic_tokens)
            .topic_tokens(HashMap::from([(
                GROUP_TYPE_TOPIC_TOKEN.to_string(),
                group_type,
            )]))
            .payload(payload)
            .map_err(ErrorKind::from)?
            .timeout(timeout)
            .build()
            .map_err(ErrorKind::from)?;
        Ok(self
            .get_group_command_invoker
            .invoke(request)
            .await
            .map_err(ErrorKind::from)?
            .map_err(|err_response| ErrorKind::from(ServiceError::from(err_response.payload)))?
            .payload
            .into())
    }

    /// If group_id is None, it uses the default
    pub async fn create_group(
        &self,
        group_type: String,
        group_id: Option<String>,
        create_attributes: GroupAttributes,
        timeout: Duration,
    ) -> Result<Group, Error> {
        let payload = er_client_gen::CreateGroupInputArguments {
            group_id,
            description: create_attributes.description,
            documentation: create_attributes.documentation,
            labels: create_attributes
                .labels
                .into_iter()
                .map(|label| label.into())
                .collect(),
            name: create_attributes.name,
            extensions: create_attributes
                .extensions
                .into_iter()
                .map(|(k, v)| (k, Bytes(v)))
                .collect(),
        };
        let request = er_client_gen::CreateGroupRequestBuilder::default()
            .payload(payload)
            .map_err(ErrorKind::from)?
            .topic_tokens(HashMap::from([(
                GROUP_TYPE_TOPIC_TOKEN.to_string(),
                group_type,
            )]))
            .timeout(timeout)
            .build()
            .map_err(ErrorKind::from)?;
        Ok(self
            .create_group_command_invoker
            .invoke(request)
            .await
            .map_err(ErrorKind::from)?
            .map_err(|err_response| ErrorKind::from(ServiceError::from(err_response.payload)))?
            .payload
            .into())
    }

    // -----------------------------------------------------------------------
    // Resources
    // -----------------------------------------------------------------------

    // List resources within a group
    pub async fn list_resources(
        &self,
        group_type: String,
        group_id: Option<String>,
        resource_type: String,
        timeout: Duration,
    ) -> Result<Vec<String>, Error> {
        let payload = er_client_gen::ListResourcesInputArguments {
            group_id,
        };
        let topic_tokens = HashMap::from([
            (GROUP_TYPE_TOPIC_TOKEN.to_string(), group_type),
            (RESOURCE_TYPE_TOPIC_TOKEN.to_string(), resource_type),
        ]);
        let request = er_client_gen::ListResourcesRequestBuilder::default()
            .payload(payload)
            .map_err(ErrorKind::from)?
            .topic_tokens(topic_tokens)
            .timeout(timeout)
            .build()
            .map_err(ErrorKind::from)?;
        Ok(self
            .list_resources_command_invoker
            .invoke(request)
            .await
            .map_err(ErrorKind::from)?
            .map_err(|err_response| ErrorKind::from(ServiceError::from(err_response.payload)))?
            .payload
            .ids)
    }

    // pub async fn create_resource() {
    //     todo!()
    // }

    // -----------------------------------------------------------------------
    // Versions
    // -----------------------------------------------------------------------

    // List versions within a resource
    pub async fn list_versions(
        &self,
        group_type: String,
        group_id: Option<String>,
        resource_type: String,
        resource_id: String,
        timeout: Duration,
    ) -> Result<Vec<String>, Error> {
        let payload = er_client_gen::ListVersionsInputArguments {
            group_id,
        };
        let topic_tokens = HashMap::from([
            (GROUP_TYPE_TOPIC_TOKEN.to_string(), group_type),
            (RESOURCE_TYPE_TOPIC_TOKEN.to_string(), resource_type),
            (RESOURCE_ID_TOPIC_TOKEN.to_string(), resource_id),
        ]);
        let request = er_client_gen::ListVersionsRequestBuilder::default()
            .payload(payload)
            .map_err(ErrorKind::from)?
            .topic_tokens(topic_tokens)
            .timeout(timeout)
            .build()
            .map_err(ErrorKind::from)?;
        Ok(self
            .list_versions_command_invoker
            .invoke(request)
            .await
            .map_err(ErrorKind::from)?
            .map_err(|err_response| ErrorKind::from(ServiceError::from(err_response.payload)))?
            .payload
            .ids)
    }

    // -----------------------------------------------------------------------
    // Schema Extension
    // -----------------------------------------------------------------------

    pub async fn list_schema_groups(&self, timeout: Duration) -> Result<Vec<String>, Error> {
        self.list_groups(er_client_gen::SCHEMA_GROUP_TYPE.to_string(), timeout)
            .await
    }

    pub async fn get_schema_group(
        &self,
        group_id: Option<String>,
        timeout: Duration,
    ) -> Result<SchemaGroup, Error> {
        self.get_group(
            er_client_gen::SCHEMA_GROUP_TYPE.to_string(),
            group_id,
            timeout,
        )
        .await
    }

    // pub async fn create_schema_group(
    //     &self,
    //     group_attributes: SchemaGroupAttributes,
    //     timeout: Duration,
    // ) -> Result<SchemaGroup, Error> {
    //     self.create_group(
    //         er_client_gen::SCHEMA_GROUP_TYPE.to_string(),
    //         None,
    //         group_attributes,
    //         timeout,
    //     )
    //     .await
    //     // let payload = er_client_gen::CreateSchemaGroupInputArguments {
    //     //     group_id: self.schema_group_id.clone(),
    //     //     name: group_attributes.name,
    //     //     description: group_attributes.description,
    //     //     documentation: group_attributes.documentation,
    //     //     labels: group_attributes.labels,
    //     //     extensions: HashMap::new(), // TODO: should this be providable? probs
    //     // };
    //     // let request = er_client_gen::CreateSchemaGroupRequestBuilder::default()
    //     //     .payload(payload)
    //     //     .map_err(ErrorKind::from)?
    //     //     .timeout(timeout)
    //     //     .build()
    //     //     .map_err(ErrorKind::from)?;
    //     // Ok(self
    //     //     .create_schema_group_command_invoker
    //     //     .invoke(request)
    //     //     .await
    //     //     .map_err(ErrorKind::from)?
    //     //     .map_err(|err_response| ErrorKind::from(ServiceError::from(err_response.payload)))?
    //     //     .payload
    //     //     .into())
    // }

    /// List resources
    pub async fn list_schemas(
        &self,
        group_id: Option<String>,
        timeout: Duration,
    ) -> Result<Vec<String>, Error> {
        self.list_resources(
            er_client_gen::SCHEMA_GROUP_TYPE.to_string(),
            group_id,
            er_client_gen::SCHEMA_RESOURCE_TYPE.to_string(),
            timeout,
        )
        .await
    }

    // pub async fn get_schema(&self, schema_id: String, timeout: Duration) -> Result<Schema, Error> {
    //     let payload = er_client_gen::GetSchemaInputArguments { schema_id };
    //     let request = er_client_gen::GetSchemaRequestBuilder::default()
    //         .payload(payload)
    //         .map_err(ErrorKind::from)?
    //         .timeout(timeout)
    //         .build()
    //         .map_err(ErrorKind::from)?;
    //     Ok(self
    //         .get_schema_command_invoker
    //         .invoke(request)
    //         .await
    //         .map_err(ErrorKind::from)?
    //         .map_err(|err_response| ErrorKind::from(ServiceError::from(err_response.payload)))?
    //         .payload
    //         .into())
    //     // let request = rpc_command::invoker::RequestBuilder::default()
    //     //     .topic_tokens(HashMap::from([
    //     //         ("groupType".to_string(), er_client_gen::SCHEMA_GROUP_TYPE.to_string()),
    //     //         ("groupId".to_string(), self.schema_group_id.clone()),
    //     //         ("resourceType".to_string(), er_client_gen::SCHEMA_RESOURCE_TYPE.to_string()),
    //     //         ("resourceId".to_string(), schema_id.to_string()),
    //     //     ]))
    //     //     .timeout(timeout)
    //     //     .build()
    //     //     .map_err(ErrorKind::from)?;
    //     // Ok(self
    //     //     .get_resource_command_invoker
    //     //     .invoke(request)
    //     //     .await
    //     //     .map_err(ErrorKind::from)?
    //     //     .map_err(|err_response| ErrorKind::from(ServiceError::from(err_response.payload)))?
    //     //     .payload
    //     //     .into())
    // }

    // pub async fn create_schema(
    //     &self,
    //     schema_id: String,
    //     schema_meta_attributes: SchemaMetaAttributes,
    //     create_initial_version_attributes: CreateSchemaVersionAttributes,
    //     timeout: Duration,
    // ) -> Result<Schema, Error> {
    //     let payload = er_client_gen::CreateSchemaInputArguments {
    //         schema_meta_attributes: er_client_gen::ResourceMetaAttributes {
    //             extensions: schema_meta_attributes
    //                 .extensions
    //                 .into_iter()
    //                 .map(|(k, v)| (k, Bytes(v)))
    //                 .collect(),
    //             labels: schema_meta_attributes.labels,
    //             id: schema_id.clone(),
    //         },
    //         create_schema_version_attributes: er_client_gen::CreateSchemaVersionAttributes {
    //             ancestor: create_initial_version_attributes.ancestor,
    //             content_type: create_initial_version_attributes.content_type,
    //             description: create_initial_version_attributes.description,
    //             documentation: create_initial_version_attributes.documentation,
    //             format: Some(create_initial_version_attributes.format.into()),
    //             labels: create_initial_version_attributes.labels,
    //             name: create_initial_version_attributes.name,
    //             schema_document: Bytes(create_initial_version_attributes.schema_document),
    //             schema_id: schema_id,
    //         },
    //     };
    //     let request = er_client_gen::CreateSchemaRequestBuilder::default()
    //         .payload(payload)
    //         .map_err(ErrorKind::from)?
    //         .timeout(timeout)
    //         .build()
    //         .map_err(ErrorKind::from)?;
    //     Ok(self
    //         .create_schema_command_invoker
    //         .invoke(request)
    //         .await
    //         .map_err(ErrorKind::from)?
    //         .map_err(|err_response| ErrorKind::from(ServiceError::from(err_response.payload)))?
    //         .payload
    //         .into())
    // }

    // pub async fn delete_schema(&self, schema_id: &str, epoch: Option<u64>) {}

    pub async fn list_schema_versions(
        &self,
        group_id: Option<String>,
        schema_id: String,
        timeout: Duration,
    ) -> Result<Vec<u64>, Error> {
        // self.list_versions(
        //     er_client_gen::SCHEMA_GROUP_TYPE,
        //     &self.schema_group_id,
        //     er_client_gen::SCHEMA_RESOURCE_TYPE,
        //     schema_id,
        //     timeout,
        // )
        // .await
        let payload = er_client_gen::ListSchemaVersionsInputArguments {
            group_id,
        };
        let request = er_client_gen::ListSchemaVersionsRequestBuilder::default()
            .payload(payload)
            .map_err(ErrorKind::from)?
            .topic_tokens(HashMap::from([(
                SCHEMA_ID_TOPIC_TOKEN.to_string(),
                schema_id,
            )]))
            .timeout(timeout)
            .build()
            .map_err(ErrorKind::from)?;
        Ok(self
            .list_schema_versions_command_invoker
            .invoke(request)
            .await
            .map_err(ErrorKind::from)?
            .map_err(|err_response| ErrorKind::from(ServiceError::from(err_response.payload)))?
            .payload
            .ids)
    }

    pub async fn get_schema_version(
        &self,
        group_id: Option<String>,
        schema_id: String,
        version_id: Option<u64>,
        timeout: Duration,
    ) -> Result<SchemaVersion, Error> {
        let payload = er_client_gen::GetSchemaVersionInputArguments {
            group_id,
            version_id,
        };
        let request = er_client_gen::GetSchemaVersionRequestBuilder::default()
            .payload(payload)
            .map_err(ErrorKind::from)?
            .topic_tokens(HashMap::from([(
                SCHEMA_ID_TOPIC_TOKEN.to_string(),
                schema_id,
            )]))
            .timeout(timeout)
            .build()
            .map_err(ErrorKind::from)?;
        Ok(self
            .get_schema_version_command_invoker
            .invoke(request)
            .await
            .map_err(ErrorKind::from)?
            .map_err(|err_response| ErrorKind::from(ServiceError::from(err_response.payload)))?
            .payload
            .into())
    }

    pub async fn create_schema_version(
        &self,
        group_id: Option<String>,
        schema_id: String,
        create_version_attributes: CreateSchemaVersionAttributes,
        timeout: Duration,
    ) -> Result<SchemaVersion, Error> {
        let payload = er_client_gen::CreateSchemaVersionInputArguments {
            group_id,
            ancestor: create_version_attributes.ancestor,
            content_type: create_version_attributes.content_type,
            description: create_version_attributes.description,
            documentation: create_version_attributes.documentation,
            format: Some(create_version_attributes.format.into()),
            labels: create_version_attributes
                .labels
                .into_iter()
                .map(|label| label.into())
                .collect(),
            schema_labels: create_version_attributes
                .schema_labels
                .into_iter()
                .map(|label| label.into())
                .collect(),
            name: create_version_attributes.name,
            schema_document: Bytes(create_version_attributes.schema_document),
        };
        // let payload = er_client_gen::CreateSchemaVersionInputArguments {
        //     version_attributes: er_client_gen::CreateVersionAttributes {
        //         group_id: self.schema_group_id.clone(),
        //         ancestor: create_version_attributes
        //             .ancestor
        //             .map(|ancestor| ancestor.to_string()),
        //         description: create_version_attributes.description,
        //         documentation: create_version_attributes.documentation,
        //         labels: create_version_attributes.labels,
        //         name: create_version_attributes.name,
        //         resource_id: schema_id,
        //         version_id: None,
        //         group_type: er_client_gen::SCHEMA_GROUP_TYPE.to_string(),
        //         resource_type: er_client_gen::SCHEMA_RESOURCE_TYPE.to_string(),
        //         extensions: HashMap::new(), // TODO
        //     },
        //     extensions: er_client_gen::CreateSchemaVersionAttributesExtensions {
        //         schema_document: Bytes(create_version_attributes.schema_document),
        //         ancestor: create_version_attributes.ancestor,
        //         content_type: create_version_attributes.content_type,
        //         format: Some(create_version_attributes.format.into()),
        //     },
        // };
        let request = er_client_gen::CreateSchemaVersionRequestBuilder::default()
            .payload(payload)
            .map_err(ErrorKind::from)?
            .topic_tokens(HashMap::from([(
                SCHEMA_ID_TOPIC_TOKEN.to_string(),
                schema_id,
            )]))
            .timeout(timeout)
            .build()
            .map_err(ErrorKind::from)?;
        Ok(self
            .create_schema_version_command_invoker
            .invoke(request)
            .await
            .map_err(ErrorKind::from)?
            .map_err(|err_response| ErrorKind::from(ServiceError::from(err_response.payload)))?
            .payload
            .into())
    }

    // pub async fn delete_schema_version(
    //     &self,
    //     schema_id: &str,
    //     version_id: u64,
    //     epoch: Option<u64>,
    // ) {
    // }

    // -----------------------------------------------------------------------
    // Thing Description Extension
    // -----------------------------------------------------------------------

    pub async fn list_thing_description_groups(
        &self,
        timeout: Duration,
    ) -> Result<Vec<String>, Error> {
        self.list_groups(
            er_client_gen::THING_DESCRIPTION_GROUP_TYPE.to_string(),
            timeout,
        )
        .await
    }

    pub async fn get_thing_description_group(
        &self,
        timeout: Duration,
        group_id: Option<String>,
    ) -> Result<ThingDescriptionGroup, Error> {
        self.get_group(
            er_client_gen::THING_DESCRIPTION_GROUP_TYPE.to_string(),
            group_id,
            timeout,
        )
        .await
        // let payload = er_client_gen::GetThingDescriptionGroupInputArguments {
        //     group_id: self.thing_description_group_id.clone(),
        // };
        // let request = er_client_gen::GetThingDescriptionGroupRequestBuilder::default()
        //     .payload(payload)
        //     .map_err(ErrorKind::from)?
        //     .timeout(timeout)
        //     .build()
        //     .map_err(ErrorKind::from)?;
        // Ok(self
        //     .get_thing_description_group_command_invoker
        //     .invoke(request)
        //     .await
        //     .map_err(ErrorKind::from)?
        //     .map_err(|err_response| ErrorKind::from(ServiceError::from(err_response.payload)))?
        //     .payload
        //     .into())
    }

    // pub async fn create_thing_description_group(
    //     &self,
    //     create_attributes: GroupAttributes,
    //     timeout: Duration,
    // ) -> Result<ThingDescriptionGroup, Error> {
    //     self.create_group(
    //         er_client_gen::THING_DESCRIPTION_GROUP_TYPE.to_string(),
    //         None,
    //         create_attributes,
    //         timeout,
    //     )
    //     .await
    //     // let payload = er_client_gen::CreateThingDescriptionGroupInputArguments {
    //     //     group_id: self.thing_description_group_id.clone(),
    //     //     description: create_attributes.description,
    //     //     documentation: create_attributes.documentation,
    //     //     labels: create_attributes.labels,
    //     //     name: create_attributes.name,
    //     //     extensions: HashMap::new(), // TODO: should this be providable? probs
    //     // };
    //     // let request = er_client_gen::CreateThingDescriptionGroupRequestBuilder::default()
    //     //     .payload(payload)
    //     //     .map_err(ErrorKind::from)?
    //     //     .timeout(timeout)
    //     //     .build()
    //     //     .map_err(ErrorKind::from)?;
    //     // Ok(self
    //     //     .create_thing_description_group_command_invoker
    //     //     .invoke(request)
    //     //     .await
    //     //     .map_err(ErrorKind::from)?
    //     //     .map_err(|err_response| ErrorKind::from(ServiceError::from(err_response.payload)))?
    //     //     .payload
    //     //     .into())
    // }

    pub async fn list_thing_descriptions(
        &self,
        group_id: Option<String>,
        timeout: Duration,
    ) -> Result<Vec<String>, Error> {
        self.list_resources(
            er_client_gen::THING_DESCRIPTION_GROUP_TYPE.to_string(),
            group_id,
            er_client_gen::THING_DESCRIPTION_RESOURCE_TYPE.to_string(),
            timeout,
        )
        .await
    }

    // pub async fn get_thing_description(
    //     &self,
    //     thing_description_id: &str,
    //     timeout: Duration,
    // ) -> Result<ThingDescription, Error> {
    //     let payload = er_client_gen::GetThingDescriptionInputArguments {
    //         thing_description_id: thing_description_id.to_string(),
    //     };
    //     let request = er_client_gen::GetThingDescriptionRequestBuilder::default()
    //         .payload(payload)
    //         .map_err(ErrorKind::from)?
    //         .timeout(timeout)
    //         .build()
    //         .map_err(ErrorKind::from)?;
    //     Ok(self
    //         .get_thing_description_command_invoker
    //         .invoke(request)
    //         .await
    //         .map_err(ErrorKind::from)?
    //         .map_err(|err_response| ErrorKind::from(ServiceError::from(err_response.payload)))?
    //         .payload
    //         .into())
    // }

    // pub async fn create_thing_description(
    //     &self,
    //     thing_description_id: String,
    //     thing_description_meta_attributes: ThingDescriptionMetaAttributes,
    //     create_initial_version_attributes: CreateThingDescriptionVersionAttributes,
    //     timeout: Duration,
    // ) -> Result<ThingDescription, Error> {
    //     let payload = er_client_gen::CreateThingDescriptionInputArguments {
    //         thing_description_meta_attributes: er_client_gen::ResourceMetaAttributes {
    //             extensions: thing_description_meta_attributes
    //                 .extensions
    //                 .into_iter()
    //                 .map(|(k, v)| (k, Bytes(v)))
    //                 .collect(),
    //             labels: thing_description_meta_attributes.labels,
    //             id: thing_description_id.clone(),
    //         },
    //         create_thing_description_version_attributes:
    //             er_client_gen::CreateThingDescriptionVersionAttributes {
    //                 thing_description_id,
    //                 description: create_initial_version_attributes.description,
    //                 documentation: create_initial_version_attributes.documentation,
    //                 labels: create_initial_version_attributes.labels,
    //                 name: create_initial_version_attributes.name,
    //                 version_id: create_initial_version_attributes.version_id,
    //                 ancestor: create_initial_version_attributes.ancestor,
    //                 content_type: create_initial_version_attributes.content_type,
    //                 format: Some(create_initial_version_attributes.format.into()),
    //                 thing_description_document: Bytes(
    //                     create_initial_version_attributes.thing_description_document,
    //                 ),
    //             },
    //     };
    //     let request = er_client_gen::CreateThingDescriptionRequestBuilder::default()
    //         .payload(payload)
    //         .map_err(ErrorKind::from)?
    //         .timeout(timeout)
    //         .build()
    //         .map_err(ErrorKind::from)?;
    //     Ok(self
    //         .create_thing_description_command_invoker
    //         .invoke(request)
    //         .await
    //         .map_err(ErrorKind::from)?
    //         .map_err(|err_response| ErrorKind::from(ServiceError::from(err_response.payload)))?
    //         .payload
    //         .into())
    // }

    pub async fn list_thing_description_versions(
        &self,
        group_id: Option<String>,
        thing_description_id: String,
        timeout: Duration,
    ) -> Result<Vec<String>, Error> {
        self.list_versions(
            er_client_gen::THING_DESCRIPTION_GROUP_TYPE.to_string(),
            group_id,
            er_client_gen::THING_DESCRIPTION_RESOURCE_TYPE.to_string(),
            thing_description_id,
            timeout,
        )
        .await
    }

    pub async fn get_thing_description_version(
        &self,
        group_id: Option<String>,
        thing_description_id: String,
        version_id: Option<String>,
        timeout: Duration,
    ) -> Result<ThingDescriptionVersion, Error> {
        let payload = er_client_gen::GetThingDescriptionVersionInputArguments {
            group_id,
            version_id,
        };
        let request = er_client_gen::GetThingDescriptionVersionRequestBuilder::default()
            .payload(payload)
            .map_err(ErrorKind::from)?
            .topic_tokens(HashMap::from([(
                THING_DESCRIPTION_ID_TOPIC_TOKEN.to_string(),
                thing_description_id,
            )]))
            .timeout(timeout)
            .build()
            .map_err(ErrorKind::from)?;
        Ok(self
            .get_thing_description_version_command_invoker
            .invoke(request)
            .await
            .map_err(ErrorKind::from)?
            .map_err(|err_response| ErrorKind::from(ServiceError::from(err_response.payload)))?
            .payload
            .into())
    }

    pub async fn create_thing_description_version(
        &self,
        group_id: Option<String>,
        thing_description_id: String,
        create_version_attributes: CreateThingDescriptionVersionAttributes,
        timeout: Duration,
    ) -> Result<ThingDescriptionVersion, Error> {
        let payload = er_client_gen::CreateThingDescriptionVersionInputArguments {
            group_id,
            description: create_version_attributes.description,
            documentation: create_version_attributes.documentation,
            labels: create_version_attributes
                .labels
                .into_iter()
                .map(|label| label.into())
                .collect(),
            thing_description_labels: create_version_attributes
                .thing_description_labels
                .into_iter()
                .map(|label| label.into())
                .collect(),
            name: create_version_attributes.name,
            ancestor: create_version_attributes.ancestor,
            content_type: create_version_attributes.content_type,
            format: Some(create_version_attributes.format.into()),
            thing_description_document: Bytes(create_version_attributes.thing_description_document),
        };
        let request = er_client_gen::CreateThingDescriptionVersionRequestBuilder::default()
            .payload(payload)
            .map_err(ErrorKind::from)?
            .topic_tokens(HashMap::from([
                (
                    THING_DESCRIPTION_ID_TOPIC_TOKEN.to_string(),
                    thing_description_id,
                ),
                (
                    VERSION_ID_TOPIC_TOKEN.to_string(),
                    create_version_attributes.version_id,
                ),
            ]))
            .timeout(timeout)
            .build()
            .map_err(ErrorKind::from)?;
        Ok(self
            .create_thing_description_version_command_invoker
            .invoke(request)
            .await
            .map_err(ErrorKind::from)?
            .map_err(|err_response| ErrorKind::from(ServiceError::from(err_response.payload)))?
            .payload
            .into())
    }

    /// Shutdown the [`Client`]. Shuts down the underlying command invokers for create/get/list/delete operations.
    ///
    /// Note: If this method is called, the [`Client`] should not be used again.
    /// If the method returns an error, it may be called again to re-attempt unsubscribing.
    ///
    /// Returns Ok(()) on success, otherwise returns [`struct@Error`].
    /// # Errors
    /// [`struct@Error`] of kind [`AIOProtocolError`](ErrorKind::AIOProtocolError)
    /// if the unsubscribe fails or if the unsuback reason code doesn't indicate success.
    pub async fn shutdown(&self) -> Result<(), Error> {
        // Shutdown the command invokers
        self.list_groups_command_invoker
            .shutdown()
            .await
            .map_err(ErrorKind::from)?;
        self.create_group_command_invoker
            .shutdown()
            .await
            .map_err(ErrorKind::from)?;
        self.get_group_command_invoker
            .shutdown()
            .await
            .map_err(ErrorKind::from)?;
        self.list_resources_command_invoker
            .shutdown()
            .await
            .map_err(ErrorKind::from)?;
        self.get_resource_command_invoker
            .shutdown()
            .await
            .map_err(ErrorKind::from)?;
        self.list_versions_command_invoker
            .shutdown()
            .await
            .map_err(ErrorKind::from)?;
        self.get_version_command_invoker
            .shutdown()
            .await
            .map_err(ErrorKind::from)?;
        // self.list_command_invoker
        //     .shutdown()
        //     .await
        //     .map_err(ErrorKind::from)?;
        // self.get_schema_command_invoker
        //     .shutdown()
        //     .await
        //     .map_err(ErrorKind::from)?;
        // self.create_schema_command_invoker
        //     .shutdown()
        //     .await
        //     .map_err(ErrorKind::from)?;
        self.get_schema_version_command_invoker
            .shutdown()
            .await
            .map_err(ErrorKind::from)?;
        self.create_schema_version_command_invoker
            .shutdown()
            .await
            .map_err(ErrorKind::from)?;
        self.list_schema_versions_command_invoker
            .shutdown()
            .await
            .map_err(ErrorKind::from)?;
        // self.get_thing_description_command_invoker
        //     .shutdown()
        //     .await
        //     .map_err(ErrorKind::from)?;
        // self.create_thing_description_command_invoker
        //     .shutdown()
        //     .await
        //     .map_err(ErrorKind::from)?;
        self.get_thing_description_version_command_invoker
            .shutdown()
            .await
            .map_err(ErrorKind::from)?;
        self.create_thing_description_version_command_invoker
            .shutdown()
            .await
            .map_err(ErrorKind::from)?;
        Ok(())
        // TODO: add the rest
    }
}
