// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

//! Demonstrates the Edge Registry (xRegistry) APIs.
//!
//! Each of the three extensions (Schema, Thing Description, Thing Model) walks through the same
//! flow against its own Resource: create a first Version, get it back, create a second Version,
//! then list the Resource's Versions (which now shows both). The owning Group is the cloud default,
//! and the server assigns each Version's identifier.

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

// Sample documents. The second revision of each Resource differs from the first.
const SCHEMA_V1: &str = r#"{"$schema":"http://json-schema.org/draft-07/schema#","type":"object","properties":{"temperature":{"type":"number"}}}"#;
const SCHEMA_V2: &str = r#"{"$schema":"http://json-schema.org/draft-07/schema#","type":"object","properties":{"temperature":{"type":"number"},"humidity":{"type":"number"}}}"#;
const THING_DESCRIPTION_V1: &str = r#"{"@context":"https://www.w3.org/2022/wot/td/v1.1","title":"Thermostat","properties":{"temperature":{"type":"number"}}}"#;
const THING_DESCRIPTION_V2: &str = r#"{"@context":"https://www.w3.org/2022/wot/td/v1.1","title":"Thermostat","properties":{"temperature":{"type":"number"},"humidity":{"type":"number"}}}"#;
const THING_MODEL_V1: &str = r#"{"@context":"https://www.w3.org/2022/wot/td/v1.1","@type":"tm:ThingModel","title":"Thermostat","properties":{"temperature":{"type":"number"}}}"#;
const THING_MODEL_V2: &str = r#"{"@context":"https://www.w3.org/2022/wot/td/v1.1","@type":"tm:ThingModel","title":"Thermostat","properties":{"temperature":{"type":"number"},"humidity":{"type":"number"}}}"#;

#[tokio::main(flavor = "current_thread")]
async fn main() -> Result<(), Box<dyn std::error::Error>> {
    Builder::new()
        .filter_level(log::LevelFilter::Info)
        .format_timestamp(None)
        .filter_module("azure_mqtt", log::LevelFilter::Warn)
        .init();

    // Create a Session.
    let connection_settings = MqttConnectionSettingsBuilder::default()
        .client_id("sampleEdgeRegistry")
        .hostname("localhost")
        .tcp_port(1883u16)
        .use_tls(false)
        .build()?;
    let session_options = SessionOptionsBuilder::default()
        .connection_settings(connection_settings)
        .build()?;
    let session = Session::new(session_options)?;

    // Create an ApplicationContext and the Edge Registry Client.
    let application_context = ApplicationContextBuilder::default().build()?;
    let client = edge_registry::Client::new(application_context, &session.create_managed_client());

    // Run the Session and the Edge Registry operations concurrently.
    let r = tokio::join!(
        run_program(client, session.create_exit_handle()),
        session.run(),
    );
    r.1?;
    Ok(())
}

async fn run_program(client: Client, exit_handle: SessionExitHandle) {
    if let Err(e) = schema_demo(&client).await {
        log::error!("Schema extension demo failed: {e}");
    }
    if let Err(e) = thing_description_demo(&client).await {
        log::error!("Thing Description extension demo failed: {e}");
    }
    if let Err(e) = thing_model_demo(&client).await {
        log::error!("Thing Model extension demo failed: {e}");
    }

    log::info!("Exiting session");
    match exit_handle.try_exit() {
        Ok(()) => log::info!("Session exited gracefully"),
        Err(e) => {
            log::error!("Graceful session exit failed: {e}");
            log::warn!("Forcing session exit");
            exit_handle.force_exit();
        }
    }
}

/// Builds a single label key/value pair.
fn label(key: &str, value: &str) -> Label {
    Label {
        key: key.to_string(),
        value: value.to_string(),
    }
}

/// Schema extension: create -> get -> create -> list.
async fn schema_demo(client: &Client) -> Result<(), edge_registry::Error> {
    let schema_id = "sample-schema".to_string();
    log::info!("--- Schema extension ---");

    // Create the first Version (implicitly creating the Schema Resource).
    let first = client
        .create_schema_version(
            GroupId::CloudDefault,
            schema_id.clone(),
            vec![label("managed-by", "sample")],
            SchemaVersionAttributesBuilder::default()
                .format(SchemaFormat::JsonSchemaDraft07)
                .labels(vec![label("revision", "1")])
                .document(Bytes::from_static(SCHEMA_V1.as_bytes()))
                .build()
                .expect("required attributes are set"),
            TIMEOUT,
        )
        .await?;
    log::info!(
        "Created Schema Version {} (xid {})",
        first.version_id,
        first.xid
    );

    // Get the Version that was just created.
    let got = client
        .get_schema_version(
            GroupId::CloudDefault,
            schema_id.clone(),
            GetVersionId::Specified(first.version_id),
            TIMEOUT,
        )
        .await?;
    log::info!(
        "Got Schema Version {} (is_default: {})",
        got.version_id,
        got.is_default
    );

    // Create a second, different Version of the same Schema.
    let second = client
        .create_schema_version(
            GroupId::CloudDefault,
            schema_id.clone(),
            vec![label("managed-by", "sample")],
            SchemaVersionAttributesBuilder::default()
                .format(SchemaFormat::JsonSchemaDraft07)
                .labels(vec![label("revision", "2")])
                .document(Bytes::from_static(SCHEMA_V2.as_bytes()))
                .build()
                .expect("required attributes are set"),
            TIMEOUT,
        )
        .await?;
    log::info!("Created Schema Version {}", second.version_id);

    // List the Versions of this Schema (now two).
    let versions = client
        .list_schema_versions(GroupSelection::Default, Some(schema_id), None, TIMEOUT)
        .await?;
    let ids: Vec<u64> = versions.iter().map(|xid| xid.version_id).collect();
    log::info!("Listed {} Schema Version(s): {ids:?}", versions.len());
    Ok(())
}

/// Thing Description extension: create -> get -> create -> list.
async fn thing_description_demo(client: &Client) -> Result<(), edge_registry::Error> {
    let thing_description_id = "sample-thing-description".to_string();
    log::info!("--- Thing Description extension ---");

    let first = client
        .create_thing_description_version(
            GroupId::CloudDefault,
            thing_description_id.clone(),
            vec![label("managed-by", "sample")],
            ThingDescriptionVersionAttributesBuilder::default()
                .format(ThingDescriptionFormat::JsonLd11)
                .labels(vec![label("revision", "1")])
                .document(Bytes::from_static(THING_DESCRIPTION_V1.as_bytes()))
                .build()
                .expect("required attributes are set"),
            TIMEOUT,
        )
        .await?;
    log::info!(
        "Created Thing Description Version {} (xid {})",
        first.version_id,
        first.xid
    );

    let got = client
        .get_thing_description_version(
            GroupId::CloudDefault,
            thing_description_id.clone(),
            GetVersionId::Specified(first.version_id),
            TIMEOUT,
        )
        .await?;
    log::info!(
        "Got Thing Description Version {} (is_default: {})",
        got.version_id,
        got.is_default
    );

    let second = client
        .create_thing_description_version(
            GroupId::CloudDefault,
            thing_description_id.clone(),
            vec![label("managed-by", "sample")],
            ThingDescriptionVersionAttributesBuilder::default()
                .format(ThingDescriptionFormat::JsonLd11)
                .labels(vec![label("revision", "2")])
                .document(Bytes::from_static(THING_DESCRIPTION_V2.as_bytes()))
                .build()
                .expect("required attributes are set"),
            TIMEOUT,
        )
        .await?;
    log::info!("Created Thing Description Version {}", second.version_id);

    let versions = client
        .list_thing_description_versions(
            GroupSelection::Default,
            Some(thing_description_id),
            None,
            TIMEOUT,
        )
        .await?;
    let ids: Vec<u64> = versions.iter().map(|xid| xid.version_id).collect();
    log::info!(
        "Listed {} Thing Description Version(s): {ids:?}",
        versions.len()
    );
    Ok(())
}

/// Thing Model extension: create -> get -> create -> list.
async fn thing_model_demo(client: &Client) -> Result<(), edge_registry::Error> {
    let thing_model_id = "sample-thing-model".to_string();
    log::info!("--- Thing Model extension ---");

    let first = client
        .create_thing_model_version(
            GroupId::CloudDefault,
            thing_model_id.clone(),
            vec![label("managed-by", "sample")],
            ThingModelVersionAttributesBuilder::default()
                .format(ThingModelFormat::JsonLd11)
                .labels(vec![label("revision", "1")])
                .document(Bytes::from_static(THING_MODEL_V1.as_bytes()))
                .build()
                .expect("required attributes are set"),
            TIMEOUT,
        )
        .await?;
    log::info!(
        "Created Thing Model Version {} (xid {})",
        first.version_id,
        first.xid
    );

    let got = client
        .get_thing_model_version(
            GroupId::CloudDefault,
            thing_model_id.clone(),
            GetVersionId::Specified(first.version_id),
            TIMEOUT,
        )
        .await?;
    log::info!(
        "Got Thing Model Version {} (is_default: {})",
        got.version_id,
        got.is_default
    );

    let second = client
        .create_thing_model_version(
            GroupId::CloudDefault,
            thing_model_id.clone(),
            vec![label("managed-by", "sample")],
            ThingModelVersionAttributesBuilder::default()
                .format(ThingModelFormat::JsonLd11)
                .labels(vec![label("revision", "2")])
                .document(Bytes::from_static(THING_MODEL_V2.as_bytes()))
                .build()
                .expect("required attributes are set"),
            TIMEOUT,
        )
        .await?;
    log::info!("Created Thing Model Version {}", second.version_id);

    let versions = client
        .list_thing_model_versions(GroupSelection::Default, Some(thing_model_id), None, TIMEOUT)
        .await?;
    let ids: Vec<u64> = versions.iter().map(|xid| xid.version_id).collect();
    log::info!("Listed {} Thing Model Version(s): {ids:?}", versions.len());
    Ok(())
}
