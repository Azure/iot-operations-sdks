// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

//! Command-line client for Schemas, Thing Models, and Thing Descriptions in Edge Registry.

use std::{
    fs,
    io::{self, Write},
    path::{Path, PathBuf},
    process::ExitCode,
    str::FromStr,
    time::Duration,
};

use azure_iot_operations_mqtt::{
    aio::connection_settings::MqttConnectionSettingsBuilder,
    session::{Session, SessionErrorKind, SessionExitHandle, SessionOptionsBuilder},
};
use azure_iot_operations_protocol::application::ApplicationContextBuilder;
use azure_iot_operations_services::edge_registry::{
    self, Client, GetVersionId, GroupId, GroupSelection, Label,
    models::{
        DeleteOptions, SchemaFormat, SchemaVersionAttributesBuilder, ThingDescriptionFormat,
        ThingDescriptionVersionAttributesBuilder, ThingModelFormat,
        ThingModelVersionAttributesBuilder, VersionXId,
    },
};
use bytes::Bytes;
use clap::{Parser, Subcommand, ValueEnum};
use env_logger::{Builder, Env};

const TIMEOUT: Duration = Duration::from_secs(10);
const DEMO_LABEL_KEY: &str = "xregistry-sample";
const DEMO_LABEL_VALUE: &str = "v1";

// Embedded documents make it easy to seed usable WoT data without external files.
const DEMO_THING_MODEL_ID: &str = "sample-thermostat";
const DEMO_THING_MODEL: &str = r#"{
  "@context": "https://www.w3.org/2022/wot/td/v1.1",
  "@type": "tm:ThingModel",
  "title": "Thermostat",
  "properties": {
    "temperature": {
      "type": "number"
    }
  }
}"#;

const DEMO_THING_DESCRIPTION_ID: &str = "sample-thermostat-01";
const DEMO_THING_DESCRIPTION: &str = r#"{
  "@context": "https://www.w3.org/2022/wot/td/v1.1",
  "id": "urn:sample:thermostat:01",
  "title": "Thermostat 01",
  "securityDefinitions": {
    "nosec_sc": {
      "scheme": "nosec"
    }
  },
  "security": "nosec_sc",
  "properties": {
    "temperature": {
      "type": "number"
    }
  }
}"#;

#[derive(Debug, Parser)]
#[command(
    version,
    about = "Manage documents in Azure IoT Operations Edge Registry",
    after_help = "Resource kinds:\n  schema             A JSON Schema Draft-07 document\n  thing-model        A reusable WoT Thing Model\n  thing-description  A WoT Thing Description for a specific device",
    arg_required_else_help = true
)]
struct Cli {
    #[command(subcommand)]
    command: Command,
}

#[derive(Debug, Subcommand)]
enum Command {
    /// Add a new document version, creating the resource if necessary.
    Add {
        /// Kind of document to add.
        #[arg(value_enum)]
        kind: ResourceKind,

        /// Registry identifier for the resource.
        resource_id: String,

        /// Path to the document to add.
        file: PathBuf,

        /// Label for the new version, expressed as KEY=VALUE. May be repeated.
        #[arg(long = "label", value_name = "KEY=VALUE")]
        labels: Vec<LabelArgument>,
    },

    /// Retrieve a document version.
    Get {
        /// Kind of document to retrieve.
        #[arg(value_enum)]
        kind: ResourceKind,

        /// Registry identifier for the resource.
        resource_id: String,

        /// Version to retrieve. The resource's default version is used when omitted.
        #[arg(long)]
        version: Option<u64>,

        /// Write the document to a file instead of standard output.
        #[arg(short, long)]
        output: Option<PathBuf>,
    },

    /// List document versions.
    List {
        /// Kind of document to list.
        #[arg(value_enum)]
        kind: ResourceKind,

        /// Only list versions of this resource.
        resource_id: Option<String>,

        /// Only list versions with this document hash.
        #[arg(long)]
        document_hash: Option<String>,

        /// Only list versions carrying this label, expressed as KEY=VALUE.
        #[arg(long, value_name = "KEY=VALUE")]
        label: Option<LabelArgument>,
    },

    /// Delete a specific document version.
    Delete {
        /// Kind of document to delete.
        #[arg(value_enum)]
        kind: ResourceKind,

        /// Registry identifier for the resource.
        resource_id: String,

        /// Numeric version identifier to delete.
        #[arg(long)]
        version: u64,

        /// Fail if the current entity epoch differs from this value.
        #[arg(long)]
        expected_epoch: Option<u64>,
    },

    /// Add the built-in demonstration documents if they are not already present.
    SeedDemo,
}

#[derive(Clone, Copy, Debug, ValueEnum)]
enum ResourceKind {
    #[value(name = "schema")]
    Schema,

    #[value(name = "thing-model", alias = "model", alias = "tm")]
    ThingModel,

    #[value(name = "thing-description", alias = "description", alias = "td")]
    ThingDescription,
}

impl ResourceKind {
    const fn name(self) -> &'static str {
        match self {
            Self::Schema => "schema",
            Self::ThingModel => "thing-model",
            Self::ThingDescription => "thing-description",
        }
    }
}

#[derive(Clone, Debug)]
struct LabelArgument(Label);

impl FromStr for LabelArgument {
    type Err = String;

    fn from_str(value: &str) -> Result<Self, Self::Err> {
        let (key, label_value) = value
            .split_once('=')
            .ok_or_else(|| "labels must use the form KEY=VALUE".to_string())?;
        if key.is_empty() {
            return Err("label keys cannot be empty".to_string());
        }

        Ok(Self(Label {
            key: key.to_string(),
            value: label_value.to_string(),
        }))
    }
}

#[tokio::main(flavor = "current_thread")]
async fn main() -> ExitCode {
    match run().await {
        Ok(()) => ExitCode::SUCCESS,
        Err(error) => {
            eprintln!("error: {error}");
            ExitCode::FAILURE
        }
    }
}

// Parses and runs one CLI command against Edge Registry.
async fn run() -> Result<(), Box<dyn std::error::Error>> {
    let cli = Cli::parse();
    let add_document = read_add_document_if_needed(&cli.command)?;

    Builder::from_env(Env::default().default_filter_or("warn"))
        .format_timestamp(None)
        .init();

    let connection_settings = MqttConnectionSettingsBuilder::default()
        .client_id(format!("xregistrySample-{}", uuid::Uuid::new_v4().simple()))
        .hostname("localhost")
        .tcp_port(1883u16)
        .use_tls(false)
        .build()?;
    let session_options = SessionOptionsBuilder::default()
        .connection_settings(connection_settings)
        .build()?;
    let session = Session::new(session_options)?;

    let application_context = ApplicationContextBuilder::default().build()?;
    let client = edge_registry::Client::new(application_context, &session.create_managed_client());

    let (command_result, session_result) = tokio::join!(
        run_command(
            client,
            cli.command,
            add_document,
            session.create_exit_handle()
        ),
        session.run(),
    );

    command_result?;
    match session_result {
        Ok(()) => Ok(()),
        Err(error) if error.kind() == SessionErrorKind::ForceExit => Ok(()),
        Err(error) => Err(error.into()),
    }
}

// Reads the document when requested by an add command.
fn read_add_document_if_needed(command: &Command) -> Result<Option<Vec<u8>>, io::Error> {
    let Command::Add { file, .. } = command else {
        return Ok(None);
    };

    fs::read(file).map(Some).map_err(|error| {
        io::Error::new(
            error.kind(),
            format!("failed to read '{}': {error}", file.display()),
        )
    })
}

// Runs the requested command and closes its Edge Registry session.
async fn run_command(
    client: Client,
    command: Command,
    add_document: Option<Vec<u8>>,
    exit_handle: SessionExitHandle,
) -> Result<(), Box<dyn std::error::Error>> {
    let result = execute_command(&client, command, add_document).await;
    exit_session(&exit_handle);
    result
}

// Dispatches a parsed CLI command to its Edge Registry operation.
async fn execute_command(
    client: &Client,
    command: Command,
    add_document: Option<Vec<u8>>,
) -> Result<(), Box<dyn std::error::Error>> {
    match command {
        Command::Add {
            kind,
            resource_id,
            labels,
            ..
        } => {
            let document = add_document.expect("add document was read before connecting");
            add_document_version(
                client,
                kind,
                resource_id,
                document,
                labels.into_iter().map(|argument| argument.0).collect(),
            )
            .await?;
        }
        Command::Get {
            kind,
            resource_id,
            version,
            output,
        } => get_document(client, kind, resource_id, version, output.as_deref()).await?,
        Command::List {
            kind,
            resource_id,
            document_hash,
            label,
        } => {
            list_documents(
                client,
                kind,
                resource_id,
                document_hash,
                label.map(|argument| argument.0),
            )
            .await?;
        }
        Command::Delete {
            kind,
            resource_id,
            version,
            expected_epoch,
        } => {
            delete_document(client, kind, resource_id, version, expected_epoch).await?;
        }
        Command::SeedDemo => seed_demo(client).await?,
    }

    Ok(())
}

// Adds a document as a new version of the selected resource.
async fn add_document_version(
    client: &Client,
    kind: ResourceKind,
    resource_id: String,
    document: Vec<u8>,
    labels: Vec<Label>,
) -> Result<(), Box<dyn std::error::Error>> {
    match kind {
        ResourceKind::Schema => {
            let created = create_schema(client, resource_id, document, labels).await?;
            println!(
                "Created Schema '{}' version {} ({})",
                created.resource_id, created.version_id, created.xid
            );
        }
        ResourceKind::ThingModel => {
            let created = create_thing_model(client, resource_id, document, labels).await?;
            println!(
                "Created Thing Model '{}' version {} ({})",
                created.resource_id, created.version_id, created.xid
            );
        }
        ResourceKind::ThingDescription => {
            let created = create_thing_description(client, resource_id, document, labels).await?;
            println!(
                "Created Thing Description '{}' version {} ({})",
                created.resource_id, created.version_id, created.xid
            );
        }
    }

    Ok(())
}

// Retrieves a document and writes it to the requested destination.
async fn get_document(
    client: &Client,
    kind: ResourceKind,
    resource_id: String,
    version: Option<u64>,
    output: Option<&Path>,
) -> Result<(), Box<dyn std::error::Error>> {
    let version_id = version.map_or(GetVersionId::ResourceDefault, GetVersionId::Specified);
    let (document, retrieved_version) = match kind {
        ResourceKind::Schema => {
            let entity = client
                .get_schema_version(GroupId::CloudDefault, resource_id, version_id, TIMEOUT)
                .await?;
            (entity.document, entity.version_id)
        }
        ResourceKind::ThingModel => {
            let entity = client
                .get_thing_model_version(GroupId::CloudDefault, resource_id, version_id, TIMEOUT)
                .await?;
            (entity.document, entity.version_id)
        }
        ResourceKind::ThingDescription => {
            let entity = client
                .get_thing_description_version(
                    GroupId::CloudDefault,
                    resource_id,
                    version_id,
                    TIMEOUT,
                )
                .await?;
            (entity.document, entity.version_id)
        }
    };

    if let Some(path) = output {
        fs::write(path, &document)?;
        eprintln!("Wrote version {retrieved_version} to {}", path.display());
    } else {
        let mut stdout = io::stdout().lock();
        stdout.write_all(&document)?;
        stdout.flush()?;
    }

    Ok(())
}

// Lists document versions matching the requested filters.
async fn list_documents(
    client: &Client,
    kind: ResourceKind,
    resource_id: Option<String>,
    document_hash: Option<String>,
    label: Option<Label>,
) -> Result<(), edge_registry::Error> {
    let versions = match kind {
        ResourceKind::Schema => {
            client
                .list_schema_versions(
                    GroupSelection::Default,
                    resource_id,
                    document_hash,
                    label,
                    TIMEOUT,
                )
                .await?
        }
        ResourceKind::ThingModel => {
            client
                .list_thing_model_versions(
                    GroupSelection::Default,
                    resource_id,
                    document_hash,
                    label,
                    TIMEOUT,
                )
                .await?
        }
        ResourceKind::ThingDescription => {
            client
                .list_thing_description_versions(
                    GroupSelection::Default,
                    resource_id,
                    document_hash,
                    label,
                    TIMEOUT,
                )
                .await?
        }
    };

    print_versions(&versions);
    Ok(())
}

// Prints listed versions in a stable, machine-readable format.
fn print_versions(versions: &[VersionXId<u64>]) {
    let value: Vec<_> = versions
        .iter()
        .map(|version| {
            serde_json::json!({
                "groupType": version.group_type,
                "groupId": version.group_id,
                "resourceType": version.resource_type,
                "resourceId": version.resource_id,
                "versionId": version.version_id,
            })
        })
        .collect();
    println!(
        "{}",
        serde_json::to_string_pretty(&value).expect("JSON values are serializable")
    );
}

// Deletes a specific version of the selected WoT resource.
async fn delete_document(
    client: &Client,
    kind: ResourceKind,
    resource_id: String,
    version_id: u64,
    expected_epoch: Option<u64>,
) -> Result<(), edge_registry::Error> {
    let options = DeleteOptions { expected_epoch };
    let deleted_resource_id = resource_id.clone();
    match kind {
        ResourceKind::Schema => {
            client
                .delete_schema_version(
                    GroupId::CloudDefault,
                    resource_id,
                    version_id,
                    options,
                    TIMEOUT,
                )
                .await?;
        }
        ResourceKind::ThingModel => {
            client
                .delete_thing_model_version(
                    GroupId::CloudDefault,
                    resource_id,
                    version_id,
                    options,
                    TIMEOUT,
                )
                .await?;
        }
        ResourceKind::ThingDescription => {
            client
                .delete_thing_description_version(
                    GroupId::CloudDefault,
                    resource_id,
                    version_id,
                    options,
                    TIMEOUT,
                )
                .await?;
        }
    }

    println!(
        "Deleted {} '{}' version {version_id}.",
        kind.name(),
        deleted_resource_id
    );
    Ok(())
}

// Seeds one Thing Model and one Thing Description for immediate experimentation.
async fn seed_demo(client: &Client) -> Result<(), edge_registry::Error> {
    seed_thing_model(client).await?;
    seed_thing_description(client).await?;
    Ok(())
}

// Seeds the built-in Thing Model unless this demo version was already added.
async fn seed_thing_model(client: &Client) -> Result<(), edge_registry::Error> {
    let document = DEMO_THING_MODEL.as_bytes();
    let versions = client
        .list_thing_model_versions(
            GroupSelection::Default,
            Some(DEMO_THING_MODEL_ID.to_string()),
            None,
            Some(demo_label()),
            TIMEOUT,
        )
        .await?;

    if let Some(version) = versions.first() {
        println!(
            "Thing Model '{DEMO_THING_MODEL_ID}' already has demo version {}.",
            version.version_id
        );
        return Ok(());
    }

    let created = create_thing_model(
        client,
        DEMO_THING_MODEL_ID.to_string(),
        document.to_vec(),
        vec![demo_label()],
    )
    .await?;
    println!(
        "Created demo Thing Model '{DEMO_THING_MODEL_ID}' version {}.",
        created.version_id
    );
    Ok(())
}

// Seeds the built-in Thing Description unless this demo version was already added.
async fn seed_thing_description(client: &Client) -> Result<(), edge_registry::Error> {
    let document = DEMO_THING_DESCRIPTION.as_bytes();
    let versions = client
        .list_thing_description_versions(
            GroupSelection::Default,
            Some(DEMO_THING_DESCRIPTION_ID.to_string()),
            None,
            Some(demo_label()),
            TIMEOUT,
        )
        .await?;

    if let Some(version) = versions.first() {
        println!(
            "Thing Description '{DEMO_THING_DESCRIPTION_ID}' already has demo version {}.",
            version.version_id
        );
        return Ok(());
    }

    let created = create_thing_description(
        client,
        DEMO_THING_DESCRIPTION_ID.to_string(),
        document.to_vec(),
        vec![demo_label()],
    )
    .await?;
    println!(
        "Created demo Thing Description '{DEMO_THING_DESCRIPTION_ID}' version {}.",
        created.version_id
    );
    Ok(())
}

// Identifies versions created by the built-in seed command.
fn demo_label() -> Label {
    Label {
        key: DEMO_LABEL_KEY.to_string(),
        value: DEMO_LABEL_VALUE.to_string(),
    }
}

// Creates a JSON Schema Draft-07 version from raw document bytes.
async fn create_schema(
    client: &Client,
    resource_id: String,
    document: Vec<u8>,
    labels: Vec<Label>,
) -> Result<
    azure_iot_operations_services::edge_registry::models::SchemaVersionEntity,
    edge_registry::Error,
> {
    client
        .create_schema_version(
            GroupId::CloudDefault,
            resource_id,
            Vec::new(),
            SchemaVersionAttributesBuilder::default()
                .content_type(Some("application/schema+json".to_string()))
                .format(SchemaFormat::JsonSchemaDraft07)
                .labels(labels)
                .document(Bytes::from(document))
                .build()
                .expect("format and document are set"),
            TIMEOUT,
        )
        .await
}

// Creates a Thing Model version from raw WoT document bytes.
async fn create_thing_model(
    client: &Client,
    resource_id: String,
    document: Vec<u8>,
    labels: Vec<Label>,
) -> Result<
    azure_iot_operations_services::edge_registry::models::ThingModelVersionEntity,
    edge_registry::Error,
> {
    client
        .create_thing_model_version(
            GroupId::CloudDefault,
            resource_id,
            Vec::new(),
            ThingModelVersionAttributesBuilder::default()
                .content_type(Some("application/tm+json".to_string()))
                .format(ThingModelFormat::JsonLd11)
                .labels(labels)
                .document(Bytes::from(document))
                .build()
                .expect("format and document are set"),
            TIMEOUT,
        )
        .await
}

// Creates a Thing Description version from raw WoT document bytes.
async fn create_thing_description(
    client: &Client,
    resource_id: String,
    document: Vec<u8>,
    labels: Vec<Label>,
) -> Result<
    azure_iot_operations_services::edge_registry::models::ThingDescriptionVersionEntity,
    edge_registry::Error,
> {
    client
        .create_thing_description_version(
            GroupId::CloudDefault,
            resource_id,
            Vec::new(),
            ThingDescriptionVersionAttributesBuilder::default()
                .content_type(Some("application/td+json".to_string()))
                .format(ThingDescriptionFormat::JsonLd11)
                .labels(labels)
                .document(Bytes::from(document))
                .build()
                .expect("format and document are set"),
            TIMEOUT,
        )
        .await
}

// Requests graceful shutdown and forces shutdown when the server is unavailable.
fn exit_session(exit_handle: &SessionExitHandle) {
    if let Err(error) = exit_handle.try_exit() {
        log::warn!("Graceful session exit failed: {error}; forcing session exit");
        exit_handle.force_exit();
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn parses_add_command() {
        let cli = Cli::try_parse_from([
            "xregistry",
            "add",
            "thing-model",
            "counter",
            "Counter.TM.json",
            "--label",
            "environment=test",
            "--label",
            "owner=iot",
        ])
        .expect("command should parse");

        let Command::Add {
            kind,
            resource_id,
            file,
            labels,
        } = cli.command
        else {
            panic!("expected add command");
        };
        assert!(matches!(kind, ResourceKind::ThingModel));
        assert_eq!(resource_id, "counter");
        assert_eq!(file, PathBuf::from("Counter.TM.json"));
        assert_eq!(labels.len(), 2);
        assert_eq!(labels[0].0.key, "environment");
        assert_eq!(labels[0].0.value, "test");
        assert_eq!(labels[1].0.key, "owner");
        assert_eq!(labels[1].0.value, "iot");
    }

    #[test]
    fn parses_schema_command() {
        let cli = Cli::try_parse_from([
            "xregistry",
            "add",
            "schema",
            "temperature-event",
            "temperature.schema.json",
        ])
        .expect("command should parse");

        assert!(matches!(
            cli.command,
            Command::Add {
                kind: ResourceKind::Schema,
                resource_id,
                file,
                labels,
            } if resource_id == "temperature-event"
                && file == PathBuf::from("temperature.schema.json")
                && labels.is_empty()
        ));
    }

    #[test]
    fn parses_label() {
        let label = LabelArgument::from_str("environment=test").expect("label should parse");
        assert_eq!(label.0.key, "environment");
        assert_eq!(label.0.value, "test");
    }

    #[test]
    fn rejects_invalid_label() {
        assert!(LabelArgument::from_str("missing-separator").is_err());
        assert!(LabelArgument::from_str("=missing-key").is_err());
    }

    #[test]
    fn reports_add_file_path_on_read_failure() {
        let command = Command::Add {
            kind: ResourceKind::ThingModel,
            resource_id: "counter".to_string(),
            file: PathBuf::from("does-not-exist.json"),
            labels: Vec::new(),
        };

        let error = read_add_document_if_needed(&command).expect_err("missing file should fail");
        assert!(error.to_string().contains("does-not-exist.json"));
    }

    #[test]
    fn parses_delete_version_option() {
        let cli = Cli::try_parse_from([
            "xregistry",
            "delete",
            "thing-model",
            "counter",
            "--version",
            "3",
        ])
        .expect("command should parse");

        assert!(matches!(
            cli.command,
            Command::Delete {
                kind: ResourceKind::ThingModel,
                resource_id,
                version: 3,
                expected_epoch: None,
            } if resource_id == "counter"
        ));
    }
}
