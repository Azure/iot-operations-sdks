// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

use std::collections::HashMap;
use std::time::Duration;

use azure_iot_operations_mqtt::session::{
    Session, SessionExitHandle, SessionManagedClient, SessionOptionsBuilder,
};
use azure_iot_operations_mqtt::MqttConnectionSettingsBuilder;
use azure_iot_operations_services::schema_registry::{
    self, Enum_Ms_Adr_SchemaRegistry_Format__1, Enum_Ms_Adr_SchemaRegistry_SchemaType__1,
};
use env_logger::Builder;
use tokio::sync::oneshot;

// FIN: Make sure this is the same one as Go and Dotnet samples.
const JSON_SCHEMA1: &str = r#"
{
  "$schema": "http://json-schema.org/draft-07/schema#",
  "type": "object",
  "properties": {
    "humidity": {
      "type": "integer"
    },
    "temperature": {
      "type": "number"
    }
  }
}
"#;

#[tokio::main(flavor = "current_thread")]
async fn main() {
    Builder::new()
        .filter_level(log::LevelFilter::max())
        .format_timestamp(None)
        .filter_module("rumqttc", log::LevelFilter::Warn)
        .init();

    // Create a session
    let connection_settings = MqttConnectionSettingsBuilder::default()
        .client_id("sampleSchemaRegistry")
        .hostname("localhost")
        .tcp_port(1883u16)
        .use_tls(false)
        .build()
        .unwrap();
    let session_options = SessionOptionsBuilder::default()
        .connection_settings(connection_settings)
        .build()
        .unwrap();
    let mut session = Session::new(session_options).unwrap();

    let (schema_id_tx, schema_id_rx) = oneshot::channel();

    let schema_registry_client = schema_registry::Client::new(&session.create_managed_client());

    tokio::task::spawn(schema_registry_put(
        schema_registry_client.clone(),
        schema_id_tx,
    ));

    tokio::task::spawn(schema_registry_get(
        schema_registry_client.clone(),
        schema_id_rx,
        session.create_exit_handle(),
    ));

    session.run().await.unwrap();
}

async fn schema_registry_put(
    client: schema_registry::Client<SessionManagedClient>,
    schema_id_tx: oneshot::Sender<String>,
) {
    let schema = client
        .put(
            JSON_SCHEMA1.to_string(),
            Enum_Ms_Adr_SchemaRegistry_Format__1::JsonSchemaDraft07,
            Enum_Ms_Adr_SchemaRegistry_SchemaType__1::MessageSchema,
            HashMap::new(),
            None,
            Duration::from_secs(10),
        )
        .await
        .unwrap();

    // Send the schema ID to the other task
    schema_id_tx.send(schema.name.unwrap()).unwrap();
}

async fn schema_registry_get(
    client: schema_registry::Client<SessionManagedClient>,
    schema_id_rx: oneshot::Receiver<String>,
    exit_handle: SessionExitHandle,
) {
    // Wait for the schema ID
    let schema_id = schema_id_rx.await.unwrap();
    let schema = client
        .get(schema_id, None, Duration::from_secs(10))
        .await
        .unwrap();

    log::info!("Schema: {:?}", schema);

    exit_handle.exit_force().await;
}
