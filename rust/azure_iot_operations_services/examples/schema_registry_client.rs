// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

use std::time::Duration;

use azure_iot_operations_mqtt::MqttConnectionSettingsBuilder;
use azure_iot_operations_mqtt::session::{
    Session, SessionExitHandle, SessionManagedClient, SessionOptionsBuilder,
};
use azure_iot_operations_protocol::application::ApplicationContextBuilder;
use azure_iot_operations_services::schema_registry::{
    self, Format, GetRequestBuilder, PutRequestBuilder, SchemaType,
};
use env_logger::Builder;
use tokio::sync::oneshot;

// To learn more about defining schemas see: https://learn.microsoft.com/azure/iot-operations/connect-to-cloud/concept-schema-registry
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
    }
  }
}
"#;

#[tokio::main(flavor = "current_thread")]
async fn main() -> Result<(), Box<dyn std::error::Error>> {
    Builder::new()
        .filter_level(log::LevelFilter::max())
        .format_timestamp(None)
        .filter_module("rumqttc", log::LevelFilter::Warn)
        .init();

    // Create a Session
    let connection_settings = MqttConnectionSettingsBuilder::default()
        .client_id("sampleSchemaRegistry")
        .hostname("localhost")
        .tcp_port(1883u16)
        .use_tls(false)
        .build()?;
    let session_options = SessionOptionsBuilder::default()
        .connection_settings(connection_settings)
        .build()?;
    let session = Session::new(session_options)?;

    // Create an ApplicationContext
    let application_context = ApplicationContextBuilder::default().build()?;

    // Create a Schema Registry Client
    let schema_registry_client =
        schema_registry::Client::new(application_context, &session.create_managed_client());

    // Run the Session and the Schema Registry operations concurrently
    let r = tokio::join!(
        run_program(schema_registry_client, session.create_exit_handle()),
        session.run(),
    );
    r.1?;
    Ok(())
}

async fn run_program(
    schema_registry_client: schema_registry::Client<SessionManagedClient>,
    exit_handle: SessionExitHandle,
) {
    // Create a channel to send the schema ID from the put task to the get task
    let (schema_id_tx, schema_id_rx) = oneshot::channel();

    tokio::join!(
        schema_registry_put(schema_registry_client.clone(), schema_id_tx),
        schema_registry_get(schema_registry_client.clone(), schema_id_rx),
    );

    log::info!("Exiting session");
    match exit_handle.try_exit().await {
        Ok(()) => log::error!("Session exited gracefully"),
        Err(e) => {
            log::error!("Graceful session exit failed: {e}");
            log::error!("Forcing session exit");
            exit_handle.exit_force().await;
        }
    };
}

async fn schema_registry_put(
    client: schema_registry::Client<SessionManagedClient>,
    schema_id_tx: oneshot::Sender<String>,
) {
    match client
        .put(
            PutRequestBuilder::default()
                .content(JSON_SCHEMA.to_string())
                .format(Format::JsonSchemaDraft07)
                .schema_type(SchemaType::MessageSchema)
                .build()
                .unwrap(),
            Duration::from_secs(10),
        )
        .await
    {
        Ok(schema) => {
            log::info!("Put request succeeded: {:?}", schema);
            // Send the schema ID to the other task
            schema_id_tx.send(schema.name.unwrap()).unwrap();
        }
        Err(e) => {
            log::error!("Put request failed: {:?}", e);
        }
    }
}

async fn schema_registry_get(
    client: schema_registry::Client<SessionManagedClient>,
    schema_id_rx: oneshot::Receiver<String>,
) {
    // Wait for the schema ID
    match schema_id_rx.await {
        Ok(schema_id) => {
            match client
                .get(
                    GetRequestBuilder::default().id(schema_id).build().unwrap(),
                    Duration::from_secs(10),
                )
                .await
            {
                Ok(schema) => {
                    log::info!("Got schema: {:?}", schema);
                }
                Err(e) => {
                    log::error!("Failed to get schema: {:?}", e);
                }
            }
        }
        Err(_) => {
            log::error!("Failed to receive schema ID from task");
        }
    }
}
