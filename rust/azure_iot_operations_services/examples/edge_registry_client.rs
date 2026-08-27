// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

use std::error::Error;
use std::future::Future;
use std::io;
use std::time::Duration;

use azure_iot_operations_mqtt::aio::connection_settings::MqttConnectionSettingsBuilder;
use azure_iot_operations_mqtt::control_packet::{
    PublishProperties, QoS, RetainOptions, SubscribeProperties, TopicFilter, TopicName,
};
use azure_iot_operations_mqtt::session::{
    Session, SessionExitHandle, SessionManagedClient, SessionOptionsBuilder,
};
use azure_iot_operations_protocol::application::ApplicationContextBuilder;
use azure_iot_operations_protocol::common::aio_protocol_error::AIOProtocolErrorKind;
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
const LIST_RETRY_TIMEOUT: Duration = Duration::from_secs(30);
const LIST_RETRY_DELAY: Duration = Duration::from_secs(1);
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
        run_program(client, mqtt_client, session.create_exit_handle()),
        session.run(),
    );
    session_result?;
    program_result
}

async fn run_program(
    client: Client,
    mqtt_client: SessionManagedClient,
    exit_handle: SessionExitHandle,
) -> Result<(), Box<dyn Error>> {
    let operation_result = async {
        resource_list_demo(&client).await?;
        graph_round_trip_if_configured(&mqtt_client).await
    }
    .await;
    let shutdown_result = client.shutdown().await;

    if exit_handle.try_exit().is_err() {
        exit_handle.force_exit();
    }

    operation_result?;
    shutdown_result?;
    Ok(())
}

async fn graph_round_trip_if_configured(
    client: &SessionManagedClient,
) -> Result<(), Box<dyn Error>> {
    let Ok(input_topic) = std::env::var("EDGE_REGISTRY_E2E_INPUT_TOPIC") else {
        return Ok(());
    };
    let output_topic = std::env::var("EDGE_REGISTRY_E2E_OUTPUT_TOPIC")?;
    let payload = std::env::var("EDGE_REGISTRY_E2E_PAYLOAD")?;

    let output_filter = TopicFilter::new(output_topic)?;
    let mut receiver = client.create_filtered_pub_receiver(output_filter.clone());
    client
        .subscribe(
            output_filter,
            QoS::AtLeastOnce,
            false,
            RetainOptions::default(),
            SubscribeProperties::default(),
        )
        .await?
        .await?;

    client
        .publish_qos1(
            TopicName::new(input_topic)?,
            false,
            payload.clone(),
            PublishProperties::default(),
        )
        .await?
        .await?;

    let output = tokio::time::timeout(Duration::from_secs(60), receiver.recv())
        .await?
        .ok_or_else(|| io::Error::other("graph output subscription closed"))?;
    if output.payload.as_ref() != payload.as_bytes() {
        return Err(io::Error::other("graph output payload did not match input").into());
    }

    log::info!("EDGE_REGISTRY_RESOURCE_LIST_ROUND_TRIP_PASSED");
    Ok(())
}

fn label(key: &str, value: &str) -> Label {
    Label {
        key: key.to_string(),
        value: value.to_string(),
    }
}

fn is_retryable_list_error(error: &edge_registry::Error) -> bool {
    match error.kind() {
        edge_registry::ErrorKind::AIOProtocolError(protocol_error) => matches!(
            protocol_error.kind,
            AIOProtocolErrorKind::Timeout | AIOProtocolErrorKind::ClientError
        ),
        edge_registry::ErrorKind::ServiceError(service_error) => {
            matches!(service_error.code, 408 | 429 | 500..=599)
        }
        _ => false,
    }
}

async fn retry_list<T, F, Fut, P>(
    mut operation: F,
    mut is_complete: P,
    incomplete_message: &'static str,
    retry_timeout: Duration,
) -> Result<T, Box<dyn Error>>
where
    F: FnMut() -> Fut,
    Fut: Future<Output = Result<T, edge_registry::Error>>,
    P: FnMut(&T) -> bool,
{
    let mut attempt = 1;
    let result = tokio::time::timeout(retry_timeout, async {
        loop {
            match operation().await {
                Ok(value) if is_complete(&value) => return Ok(value),
                Ok(_) => {
                    log::warn!(
                        "List request returned incomplete registry state (attempt {attempt})"
                    );
                }
                Err(error) if is_retryable_list_error(&error) => {
                    log::warn!(
                        "List request failed during registry reconciliation (attempt {attempt}): {error}"
                    );
                }
                Err(error) => return Err(error),
            }

            attempt += 1;
            tokio::time::sleep(LIST_RETRY_DELAY).await;
        }
    })
    .await;

    match result {
        Ok(Ok(value)) => Ok(value),
        Ok(Err(error)) => Err(error.into()),
        Err(_) => {
            Err(io::Error::other(format!("{incomplete_message} within {retry_timeout:?}")).into())
        }
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
    if fetched.version_id != first.version_id {
        return Err(io::Error::other("fetched Schema Version did not match").into());
    }

    let second = client
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

    retry_list(
        || {
            client.list_schema_versions(
                GroupSelection::Default,
                Some(schema_id.clone()),
                None,
                None,
                TIMEOUT,
            )
        },
        |versions| {
            versions
                .iter()
                .any(|xid| xid.version_id == first.version_id)
                && versions
                    .iter()
                    .any(|xid| xid.version_id == second.version_id)
        },
        "created Schema Versions were not listed",
        LIST_RETRY_TIMEOUT,
    )
    .await?;

    retry_list(
        || client.list_schemas(GroupSelection::Default, Some(schema_label.clone()), TIMEOUT),
        |schemas| schemas.iter().any(|xid| xid.resource_id == schema_id),
        "labeled Schema was not listed",
        LIST_RETRY_TIMEOUT,
    )
    .await?;

    log::info!(
        "Listed Schema {schema_id} with Versions {} and {}",
        first.version_id,
        second.version_id
    );

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
    retry_list(
        || {
            client.list_thing_descriptions(
                GroupSelection::Default,
                Some(thing_description_label.clone()),
                TIMEOUT,
            )
        },
        |thing_descriptions| {
            thing_descriptions
                .iter()
                .any(|xid| xid.resource_id == thing_description_id)
        },
        "labeled Thing Description was not listed",
        LIST_RETRY_TIMEOUT,
    )
    .await?;

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
    retry_list(
        || {
            client.list_thing_models(
                GroupSelection::Default,
                Some(thing_model_label.clone()),
                TIMEOUT,
            )
        },
        |thing_models| {
            thing_models
                .iter()
                .any(|xid| xid.resource_id == thing_model_id)
        },
        "labeled Thing Model was not listed",
        LIST_RETRY_TIMEOUT,
    )
    .await?;

    log::info!("Listed Thing Description {thing_description_id} and Thing Model {thing_model_id}");
    Ok(())
}

#[cfg(test)]
mod tests {
    use super::*;

    #[tokio::test]
    async fn retry_list_stops_at_overall_timeout() {
        let result = retry_list(
            || async { Ok::<bool, edge_registry::Error>(false) },
            |is_complete| *is_complete,
            "resource was not listed",
            Duration::from_millis(20),
        )
        .await;

        assert_eq!(
            result.unwrap_err().to_string(),
            "resource was not listed within 20ms"
        );
    }
}
