// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

use std::error::Error;
use std::time::Duration;

use azure_iot_operations_mqtt::aio::connection_settings::MqttConnectionSettingsBuilder;
use azure_iot_operations_mqtt::session::{Session, SessionExitHandle, SessionOptionsBuilder};
use azure_iot_operations_protocol::application::ApplicationContextBuilder;
use azure_iot_operations_services::edge_registry::models::{
    SchemaFormat, SchemaVersionAttributesBuilder, ThingDescriptionFormat,
    ThingDescriptionVersionAttributesBuilder, ThingModelFormat, ThingModelVersionAttributesBuilder,
};
use azure_iot_operations_services::edge_registry::{
    self, Client, GetVersionId, GroupId, GroupSelection, Label,
};
use bytes::Bytes;
use env_logger::Builder;

const TIMEOUT: Duration = Duration::from_secs(10);
const SCHEMA_V1: &str = r#"{"$schema":"http://json-schema.org/draft-07/schema#","type":"object","properties":{"temperature":{"type":"number"}}}"#;
const SCHEMA_V2: &str = r#"{"$schema":"http://json-schema.org/draft-07/schema#","type":"object","properties":{"temperature":{"type":"number"},"humidity":{"type":"number"}}}"#;
const THING_DESCRIPTION: &str = r#"{"@context":"https://www.w3.org/2022/wot/td/v1.1","id":"urn:sample:thing-description","title":"Sample Thing Description","securityDefinitions":{"nosec_sc":{"scheme":"nosec"}},"security":["nosec_sc"]}"#;
const THING_MODEL: &str = r#"{"@context":"https://www.w3.org/2022/wot/td/v1.1","@type":"tm:ThingModel","id":"urn:sample:thing-model","title":"Sample Thing Model"}"#;

#[tokio::main(flavor = "current_thread")]
async fn main() -> Result<(), Box<dyn Error>> {
    Builder::new()
        .filter_level(log::LevelFilter::Info)
        .format_timestamp(None)
        .filter_module("azure_mqtt", log::LevelFilter::Warn)
        .init();

    let connection_settings = MqttConnectionSettingsBuilder::from_environment()?.build()?;
    let session_options = SessionOptionsBuilder::default()
        .connection_settings(connection_settings)
        .build()?;
    let session = Session::new(session_options)?;

    let application_context = ApplicationContextBuilder::default().build()?;
    let mqtt_client = session.create_managed_client();
    let client = edge_registry::Client::new(application_context, &mqtt_client);

    let (program_result, session_result) = tokio::join!(
        run_program(client, session.create_exit_handle()),
        session.run(),
    );
    session_result?;
    program_result
}

async fn run_program(client: Client, exit_handle: SessionExitHandle) -> Result<(), Box<dyn Error>> {
    let operation_result = resource_list_demo(&client).await;
    let shutdown_result = client.shutdown().await;

    if exit_handle.try_exit().is_err() {
        exit_handle.force_exit();
    }

    operation_result?;
    shutdown_result?;
    Ok(())
}

fn label(key: &str, value: &str) -> Label {
    Label {
        key: key.to_string(),
        value: value.to_string(),
    }
}

async fn resource_list_demo(client: &Client) -> Result<(), Box<dyn Error>> {
    let schema_id = "sample-schema".to_string();
    let schema_label = label("managed-by", "sample");

    let first = client
        .create_schema_version(
            GroupId::CloudDefault,
            schema_id.clone(),
            vec![schema_label.clone()],
            SchemaVersionAttributesBuilder::default()
                .format(SchemaFormat::JsonSchemaDraft07)
                .labels(vec![label("revision", "1")])
                .document(Bytes::from_static(SCHEMA_V1.as_bytes()))
                .build()
                .expect("required attributes are set"),
            TIMEOUT,
        )
        .await?;

    let fetched = client
        .get_schema_version(
            GroupId::CloudDefault,
            schema_id.clone(),
            GetVersionId::Specified(first.version_id),
            TIMEOUT,
        )
        .await?;
    log::info!("Fetched Schema Version: {fetched:?}");

    client
        .create_schema_version(
            GroupId::CloudDefault,
            schema_id.clone(),
            vec![schema_label.clone()],
            SchemaVersionAttributesBuilder::default()
                .format(SchemaFormat::JsonSchemaDraft07)
                .labels(vec![label("revision", "2")])
                .document(Bytes::from_static(SCHEMA_V2.as_bytes()))
                .build()
                .expect("required attributes are set"),
            TIMEOUT,
        )
        .await?;

    let schema_versions = client
        .list_schema_versions(
            GroupSelection::Default,
            Some(schema_id.clone()),
            None,
            None,
            TIMEOUT,
        )
        .await?;
    for schema_version in schema_versions {
        log::info!("Schema Version: {schema_version:?}");
    }

    let schemas = client
        .list_schemas(GroupSelection::Default, Some(schema_label), TIMEOUT)
        .await?;
    for schema in schemas {
        log::info!("Schema: {schema:?}");
    }

    let thing_description_id = "sample-thing-description".to_string();
    let thing_description_label = label("managed-by", "sample");
    client
        .create_thing_description_version(
            GroupId::CloudDefault,
            thing_description_id.clone(),
            vec![thing_description_label.clone()],
            ThingDescriptionVersionAttributesBuilder::default()
                .format(ThingDescriptionFormat::JsonLd11)
                .document(Bytes::from_static(THING_DESCRIPTION.as_bytes()))
                .build()
                .expect("required attributes are set"),
            TIMEOUT,
        )
        .await?;

    let thing_descriptions = client
        .list_thing_descriptions(
            GroupSelection::Default,
            Some(thing_description_label),
            TIMEOUT,
        )
        .await?;
    for thing_description in thing_descriptions {
        log::info!("Thing Description: {thing_description:?}");
    }

    let thing_model_id = "sample-thing-model".to_string();
    let thing_model_label = label("managed-by", "sample");
    client
        .create_thing_model_version(
            GroupId::CloudDefault,
            thing_model_id.clone(),
            vec![thing_model_label.clone()],
            ThingModelVersionAttributesBuilder::default()
                .format(ThingModelFormat::JsonLd11)
                .document(Bytes::from_static(THING_MODEL.as_bytes()))
                .build()
                .expect("required attributes are set"),
            TIMEOUT,
        )
        .await?;

    let thing_models = client
        .list_thing_models(GroupSelection::Default, Some(thing_model_label), TIMEOUT)
        .await?;
    for thing_model in thing_models {
        log::info!("Thing Model: {thing_model:?}");
    }

    Ok(())
}
