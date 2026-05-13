// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

use std::fs;
use std::time::Duration;

use azure_iot_operations_mqtt::aio::connection_settings::MqttConnectionSettingsBuilder;
use azure_iot_operations_mqtt::session::{Session, SessionExitHandle, SessionOptionsBuilder};
use azure_iot_operations_protocol::application::ApplicationContextBuilder;
use azure_iot_operations_services::edge_registry::{
    Client, CreateThingDescriptionVersionAttributesBuilder, GroupAttributesBuilder,
    ThingDescriptionFormat, ThingDescriptionMetaAttributes,
};

// WoT TDs to upload. (display_name, description, file_path, asset_uuid)
// asset_uuid is the value extracted from the TD's `id` field
// (`urn:uuid:<uuid>`) and is used as the xRegistry thing-description id.
const WOT_TDS: &[(&str, &str, &str, &str)] = &[
    (
        "wot_td_MachineTool",
        "WoT TD for MachineTool asset",
        "wot_td_MachineTool.jsonld",
        "f8f88cbb-7b5b-4ab4-92ef-16b9fc837a87",
    ),
    (
        "wot_td_GMSTool",
        "WoT TD for GMSTool (WenzelLH87) asset",
        "wot_td_GMSTool.jsonld",
        "d7c48b94-836d-4aa8-ac1c-ffccc9aa50bc",
    ),
];

const TD_GROUP_NAME: &str = "AIO WoT TDs";
const TD_CONTENT_TYPE: &str = "application/td+json";
const TD_VERSION_ID: &str = "1";

#[tokio::main(flavor = "current_thread")]
async fn main() -> Result<(), Box<dyn std::error::Error>> {
    env_logger::Builder::new()
        .filter_level(log::LevelFilter::Info)
        .format_timestamp(None)
        .init();

    let connection_settings = MqttConnectionSettingsBuilder::default()
        .client_id("samplexRegistrySdkClient")
        .hostname("localhost")
        .tcp_port(1883u16)
        .use_tls(false)
        .build()?;
    let session_options = SessionOptionsBuilder::default()
        .connection_settings(connection_settings)
        .build()?;
    let session = Session::new(session_options)?;
    let exit_handle = session.create_exit_handle();

    let application_context = ApplicationContextBuilder::default().build()?;

    let xregistry_client = Client::new(
        application_context,
        session.create_managed_client(),
        "azure_iot_operations".to_string(),
    );

    let results = tokio::join! {
        run_program(xregistry_client, exit_handle),
        session.run(),
    };
    results.1?;
    Ok(())
}

async fn run_program(client: Client, exit_handle: SessionExitHandle) {
    // Ensure the TD group exists. Idempotent: if it already exists the call
    // will fail and we just log it.
    let group_attributes = GroupAttributesBuilder::default()
        .name(TD_GROUP_NAME)
        .build()
        .expect("Failed to build GroupAttributes");
    match client
        .create_thing_description_group(group_attributes, Duration::from_secs(10))
        .await
    {
        Ok(response) => log::info!("Create TD group response: {response}"),
        Err(e) => log::warn!("Create TD group error (may already exist): {e:?}"),
    }

    for (display_name, description, file_path, asset_uuid) in WOT_TDS {
        let content = match fs::read_to_string(file_path) {
            Ok(c) => c,
            Err(e) => {
                log::error!("Failed to read {file_path}: {e}");
                continue;
            }
        };

        create_thing_description(
            &client,
            asset_uuid,
            display_name,
            description,
            content.into_bytes(),
        )
        .await;

        get_thing_description(&client, asset_uuid).await;
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

async fn create_thing_description(
    client: &Client,
    td_id: &str,
    display_name: &str,
    description: &str,
    document: Vec<u8>,
) {

    let attributes = CreateThingDescriptionVersionAttributesBuilder::default()
        .thing_description_document(document)
        .version_id(TD_VERSION_ID.to_string())
        .content_type(TD_CONTENT_TYPE)
        .name(display_name.to_string())
        .description(description.to_string())
        .format(ThingDescriptionFormat::WotTd11)
        .build()
        .expect("Failed to build CreateThingDescriptionVersionAttributes");

    

    match client
        .create_thing_description(
            td_id.to_string(),
            ThingDescriptionMetaAttributes::default(),
            attributes,
            Duration::from_secs(10),
        )
        .await
    {
        Ok(response) => {
            log::info!("Create TD '{display_name}' response: {response}");
            // Print the id+version so it can be wired into the module configuration.
            println!(
                "UPLOADED  display_name='{display_name}'  td_id='{td_id}'  version='{TD_VERSION_ID}'"
            );
        }
        Err(e) => log::error!("Create TD '{display_name}' error: {e:?}"),
    }
}

async fn get_thing_description(client: &Client, td_id: &str) {
    match client
        .get_thing_description(td_id, Duration::from_secs(10))
        .await
    {
        Ok(response) => log::info!("Get TD '{td_id}' response: {response}"),
        Err(e) => log::error!("Get TD '{td_id}' error: {e:?}"),
    }
}
