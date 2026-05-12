// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

use std::time::Duration;

use azure_iot_operations_mqtt::aio::connection_settings::MqttConnectionSettingsBuilder;
use azure_iot_operations_mqtt::session::{Session, SessionExitHandle, SessionOptionsBuilder};
use azure_iot_operations_protocol::application::ApplicationContextBuilder;
use azure_iot_operations_services::edge_registry::{
    Client, CreateSchemaVersionAttributesBuilder, CreateThingDescriptionVersionAttributesBuilder,
    GroupAttributesBuilder, SchemaFormat, SchemaMetaAttributes, ThingDescriptionFormat,
    ThingDescriptionMetaAttributes,
};

const JSON_SCHEMA: &str = r#"
{
  "$schema": "http://json-schema.org/draft-07/schema#",
  "type": "object",
  "properties": {
    "humidity": {
      "type": "integer"
    },
    "temperature": {
      "type": "number"
    },
    "pressure": {
      "type": "integer"
    }
  }
}
"#;

const THING_DESCRIPTION: &str = r#"
{
  "@context": [
    "https://www.w3.org/2022/wot/td/v1.1",
    {
      "dov": "http://azure.com/IoT/operations/tm#",
      "ov": "http://azure.com/IoT/operations/ontology#",
      "adr": "http://azure.com/IoT/operations/deviceregistry#"
    }
  ],

  "id": "urn:uuid:00000000-1111-2222-3333-444444444444",
  "title": "sampleThingDescription",

  "securityDefinitions": {
    "nosec_sc": {
      "scheme": "nosec"
    }
  },
  "security": ["nosec_sc"],

  "links": [
    {
      "rel": "adr:asset",
      "href": "urn:uuid:00000000-1111-2222-3333-444444444444"
    },
    {
      "rel": "dov:dataset",
      "href": "\#ALERT",
      "title": "ALERT"
    }
  ],

  "properties": {
    "ALERT/ALERT": {
      "dov:memberOf": "ALERT",
      "@type": ["ov:measurement"],
      "dov:dataSource": "ALERT",
      "dov:propertyIRI": "\#ALERT/ALERT",
      "title": "ALERT",
      "forms": []
    }
  }
}
"#;

#[tokio::main(flavor = "current_thread")]
async fn main() -> Result<(), Box<dyn std::error::Error>> {
    env_logger::Builder::new()
        .filter_level(log::LevelFilter::Trace)
        .format_timestamp(None)
        .init();

    // Create a Session and exit handle
    let mqtt_client_id = "sampleEdgeRegistrySdkClient";
    let connection_settings = MqttConnectionSettingsBuilder::default()
        .client_id(mqtt_client_id)
        .hostname("localhost")
        .tcp_port(1883u16)
        .use_tls(false)
        .build()?;
    let session_options = SessionOptionsBuilder::default()
        .connection_settings(connection_settings)
        .build()?;
    let session = Session::new(session_options)?;
    let exit_handle = session.create_exit_handle();

    // Create an ApplicationContext
    let application_context = ApplicationContextBuilder::default().build()?;

    // Create an Edge Registry Client
    let edge_registry_client = Client::new(
        application_context,
        session.create_managed_client(),
        "azure_iot_operations".to_string(),
    );

    // Run the Session and the State Store operations concurrently
    let results = tokio::join! {
        async {
            edge_registry_operations(edge_registry_client).await;
            exit(&exit_handle);
        },
        session.run(),
    };
    results.1?;
    Ok(())
}

async fn edge_registry_operations(client: Client) {
    // Schema Extension
    let schema_id = "exampleSchema";

    let group_attributes = GroupAttributesBuilder::default()
        .name("SDK Schemas")
        .build()
        .unwrap();
    match client
        .create_schema_group(group_attributes, Duration::from_secs(10))
        .await
    {
        Ok(response) => log::info!("Create schema group response: {response}"),
        Err(e) => log::error!("Create schema group error: {e:?}"),
    }

    match client.get_schema_group(Duration::from_secs(10)).await {
        Ok(response) => log::info!("Get schema group response: {response}"),
        Err(e) => log::error!("Get schema group error: {e:?}"),
    }

    let create_initial_version_attributes = CreateSchemaVersionAttributesBuilder::default()
        .schema_document(JSON_SCHEMA.as_bytes().to_vec())
        .content_type("application/json")
        .name("Example Schema Version 1")
        .description("An example schema version")
        .format(SchemaFormat::JsonSchemaDraft07)
        .build()
        .expect("Failed to build CreateSchemaVersionRequest");

    let version_id_1 = match client
        .create_schema(
            schema_id.to_string(),
            SchemaMetaAttributes::default(),
            create_initial_version_attributes,
            Duration::from_secs(10),
        )
        .await
    {
        Ok(response) => {
            log::info!("Create schema response: {response}");
            Some(response.meta.default_version_id)
        }
        Err(e) => {
            log::error!("Create schema error: {e:?}");
            None
        }
    };

    match client
        .get_schema(schema_id.to_string(), Duration::from_secs(10))
        .await
    {
        Ok(response) => log::info!("Get schema response: {response}"),
        Err(e) => log::error!("Get schema error: {e:?}"),
    }

    if let Some(version_id) = version_id_1 {
        match client
            .get_schema_version(schema_id.to_string(), version_id, Duration::from_secs(10))
            .await
        {
            Ok(response) => log::info!("Get version 1 response: {response}"),
            Err(e) => log::error!("Get version 1 error: {e:?}"),
        }
    }

    let mut create_version_attributes_builder = CreateSchemaVersionAttributesBuilder::default();
    create_version_attributes_builder
        .schema_document(JSON_SCHEMA.as_bytes().to_vec())
        .content_type("application/json")
        .name("Example Schema Version 2")
        .description("An example schema version")
        .format(SchemaFormat::JsonSchemaDraft07);

    if let Some(ancestor_version_id) = version_id_1 {
        create_version_attributes_builder.ancestor(ancestor_version_id);
    }

    let create_version_attributes = create_version_attributes_builder
        // CreateSchemaVersionAttributesBuilder::default()
        //     .schema_document(JSON_SCHEMA.as_bytes().to_vec())
        //     .content_type("application/json")
        //     .name("Example Schema Version 2")
        //     .description("An example schema version")
        //     .format(SchemaFormat::JsonSchemaDraft07)
        //     .ancestor(version_id_1)
        .build()
        .expect("Failed to build CreateSchemaVersionRequest");

    let schema_version_2 = match client
        .create_schema_version(
            schema_id.to_string(),
            create_version_attributes,
            Duration::from_secs(10),
        )
        .await
    {
        Ok(response) => {
            log::info!("Create schema version response: {response}");
            Some(response.version_id)
        }
        Err(e) => {
            log::error!("Create schema version error: {e:?}");
            None
        }
    };

    match client
        .get_schema(schema_id.to_string(), Duration::from_secs(10))
        .await
    {
        Ok(response) => log::info!("Get schema response: {response}"),
        Err(e) => log::error!("Get schema error: {e:?}"),
    }

    if let Some(version_id) = schema_version_2 {
        match client
            .get_schema_version(schema_id.to_string(), version_id, Duration::from_secs(10))
            .await
        {
            Ok(response) => log::info!("Get version 2 response: {response}"),
            Err(e) => log::error!("Get version 2 error: {e:?}"),
        }
    }

    match client.list_schema_groups(Duration::from_secs(10)).await {
        Ok(response) => log::info!("List schema groups response: {response:?}"),
        Err(e) => log::error!("List schema groups error: {e:?}"),
    }

    match client.list_schemas(Duration::from_secs(10)).await {
        Ok(response) => log::info!("List schemas response: {response:?}"),
        Err(e) => log::error!("List schemas error: {e:?}"),
    }

    match client
        .list_schema_versions(schema_id.to_string(), Duration::from_secs(10))
        .await
    {
        Ok(response) => log::info!("List schema versions response: {response:?}"),
        Err(e) => log::error!("List schema versions error: {e:?}"),
    }

    // Thing Description Extension
    let thing_description_id = "exampleThingDescription";
    let thing_description_version_id_1 = "1.0.1";
    let thing_description_version_id_2 = "1.5.3";

    let group_attributes = GroupAttributesBuilder::default()
        .name("SDK Thing Descriptions")
        .build()
        .unwrap();
    match client
        .create_thing_description_group(group_attributes, Duration::from_secs(10))
        .await
    {
        Ok(response) => log::info!("Create thing description group response: {response}"),
        Err(e) => log::error!("Create thing description group error: {e:?}"),
    }

    match client
        .get_thing_description_group(Duration::from_secs(10))
        .await
    {
        Ok(response) => log::info!("Get thing description group response: {response}"),
        Err(e) => log::error!("Get thing description group error: {e:?}"),
    }

    let create_initial_version_attributes =
        CreateThingDescriptionVersionAttributesBuilder::default()
            .thing_description_document(THING_DESCRIPTION.as_bytes().to_vec())
            .version_id(thing_description_version_id_1.to_string())
            .content_type("application/json")
            .name("Example Thing Description Version 1")
            .description("An example thing description version")
            .format(ThingDescriptionFormat::WotTd11)
            .build()
            .expect("Failed to build CreateThingDescriptionVersionRequest");

    match client
        .create_thing_description(
            thing_description_id.to_string(),
            ThingDescriptionMetaAttributes::default(),
            create_initial_version_attributes,
            Duration::from_secs(10),
        )
        .await
    {
        Ok(response) => log::info!("Create thing description response: {response}"),
        Err(e) => log::error!("Create thing description error: {e:?}"),
    }

    match client
        .get_thing_description(thing_description_id, Duration::from_secs(10))
        .await
    {
        Ok(response) => log::info!("Get thing description response: {response}"),
        Err(e) => log::error!("Get thing description error: {e:?}"),
    }

    match client
        .get_thing_description_version(
            thing_description_id,
            &thing_description_version_id_1,
            Duration::from_secs(10),
        )
        .await
    {
        Ok(response) => log::info!("Get version 1 response: {response}"),
        Err(e) => log::error!("Get version 1 error: {e:?}"),
    }

    let create_version_attributes = CreateThingDescriptionVersionAttributesBuilder::default()
        .thing_description_document(THING_DESCRIPTION.as_bytes().to_vec())
        .content_type("application/json")
        .name("Example Thing Description Version 2")
        .description("An example thing description version")
        .format(ThingDescriptionFormat::WotTd11)
        .ancestor(thing_description_version_id_1.to_string())
        .version_id(thing_description_version_id_2.to_string())
        .build()
        .expect("Failed to build CreateThingDescriptionVersionRequest");

    match client
        .create_thing_description_version(
            thing_description_id,
            create_version_attributes,
            Duration::from_secs(10),
        )
        .await
    {
        Ok(response) => log::info!("Create thing description version response: {response}"),
        Err(e) => log::error!("Create thing description version error: {e:?}"),
    }

    match client
        .get_thing_description(thing_description_id, Duration::from_secs(10))
        .await
    {
        Ok(response) => log::info!("Get thing description response: {response}"),
        Err(e) => log::error!("Get thing description error: {e:?}"),
    }

    match client
        .get_thing_description_version(
            thing_description_id,
            thing_description_version_id_2,
            Duration::from_secs(10),
        )
        .await
    {
        Ok(response) => log::info!("Get version 2 response: {response}"),
        Err(e) => log::error!("Get version 2 error: {e:?}"),
    }

    match client
        .list_thing_description_groups(Duration::from_secs(10))
        .await
    {
        Ok(response) => log::info!("List thing description groups response: {response:?}"),
        Err(e) => log::error!("List thing description groups error: {e:?}"),
    }

    match client
        .list_thing_descriptions(Duration::from_secs(10))
        .await
    {
        Ok(response) => log::info!("List thing descriptions response: {response:?}"),
        Err(e) => log::error!("List thing descriptions error: {e:?}"),
    }

    match client
        .list_thing_description_versions(thing_description_id, Duration::from_secs(10))
        .await
    {
        Ok(response) => log::info!("List thing description versions response: {response:?}"),
        Err(e) => log::error!("List thing description versions error: {e:?}"),
    }

    match client.shutdown().await {
        Ok(()) => log::info!("xRegistry client shutdown successfully"),
        Err(e) => log::error!("xRegistry client shutdown error: {e:?}"),
    }
}

// Exit the Session
fn exit(exit_handle: &SessionExitHandle) {
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
