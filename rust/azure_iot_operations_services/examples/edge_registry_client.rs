// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

//! Command-line client for Thing Models and Thing Descriptions in Edge Registry.

use std::{
    fs,
    io::{self, Write},
    path::{Path, PathBuf},
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
        DeleteOptions, ThingDescriptionFormat, ThingDescriptionVersionAttributesBuilder,
        ThingModelFormat, ThingModelVersionAttributesBuilder, VersionXId,
    },
};
use bytes::Bytes;
use clap::{Parser, Subcommand, ValueEnum};
use env_logger::{Builder, Env};

const TIMEOUT: Duration = Duration::from_secs(10);

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
    about = "Manage WoT documents in Azure IoT Operations Edge Registry",
    after_help = "Resource kinds:\n  thing-model        A reusable WoT Thing Model\n  thing-description  A WoT Thing Description for a specific device",
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
        /// Kind of `WoT` document to add.
        #[arg(value_enum)]
        kind: ResourceKind,

        /// Registry identifier for the resource.
        resource_id: String,

        /// Path to the document to add.
        file: PathBuf,
    },

    /// Retrieve a document version.
    Get {
        /// Kind of `WoT` document to retrieve.
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
        /// Kind of `WoT` document to list.
        #[arg(value_enum)]
        kind: ResourceKind,

        /// Only list versions of this resource.
        resource_id: Option<String>,

        /// Only list versions with this document hash.
        #[arg(long)]
        document_hash: Option<String>,

        /// Only list versions carrying this label, expressed as KEY=VALUE.
        #[arg(long)]
        label: Option<LabelArgument>,
    },

    /// Delete a specific document version.
    Delete {
        /// Kind of `WoT` document to delete.
        #[arg(value_enum)]
        kind: ResourceKind,

        /// Registry identifier for the resource.
        resource_id: String,

        /// Numeric version identifier to delete.
        version_id: u64,

        /// Fail if the current entity epoch differs from this value.
        #[arg(long)]
        expected_epoch: Option<u64>,
    },

    /// Add the built-in demonstration documents if they are not already present.
    SeedDemo,
}

#[derive(Clone, Copy, Debug, ValueEnum)]
enum ResourceKind {
    #[value(name = "thing-model", alias = "model", alias = "tm")]
    ThingModel,

    #[value(name = "thing-description", alias = "description", alias = "td")]
    ThingDescription,
}

impl ResourceKind {
    const fn name(self) -> &'static str {
        match self {
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
async fn main() -> Result<(), Box<dyn std::error::Error>> {
    let cli = Cli::parse();
    let add_document = read_add_document_if_needed(&cli.command)?;

    Builder::from_env(Env::default().default_filter_or("warn"))
        .format_timestamp(None)
        .init();

    let connection_settings = MqttConnectionSettingsBuilder::default()
        .client_id("xregistrySample")
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

// Reads the WoT document when requested by an add command.
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
            kind, resource_id, ..
        } => {
            let document = add_document.expect("add document was read before connecting");
            add_document_version(client, kind, resource_id, document).await?;
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
            version_id,
            expected_epoch,
        } => {
            delete_document(client, kind, resource_id, version_id, expected_epoch).await?;
        }
        Command::SeedDemo => seed_demo(client).await?,
    }

    Ok(())
}

// Adds a WoT document as a new version of the selected resource.
async fn add_document_version(
    client: &Client,
    kind: ResourceKind,
    resource_id: String,
    document: Vec<u8>,
) -> Result<(), Box<dyn std::error::Error>> {
    match kind {
        ResourceKind::ThingModel => {
            let created = create_thing_model(client, resource_id, document).await?;
            println!(
                "Created Thing Model '{}' version {} ({})",
                created.resource_id, created.version_id, created.xid
            );
        }
        ResourceKind::ThingDescription => {
            let created = create_thing_description(client, resource_id, document).await?;
            println!(
                "Created Thing Description '{}' version {} ({})",
                created.resource_id, created.version_id, created.xid
            );
        }
    }

    Ok(())
}

// Retrieves a WoT document and writes it to the requested destination.
async fn get_document(
    client: &Client,
    kind: ResourceKind,
    resource_id: String,
    version: Option<u64>,
    output: Option<&Path>,
) -> Result<(), Box<dyn std::error::Error>> {
    let version_id = version.map_or(GetVersionId::ResourceDefault, GetVersionId::Specified);
    let (document, retrieved_version) = match kind {
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
        io::stdout().write_all(&document)?;
    }

    Ok(())
}

// Lists WoT document versions matching the requested filters.
async fn list_documents(
    client: &Client,
    kind: ResourceKind,
    resource_id: Option<String>,
    document_hash: Option<String>,
    label: Option<Label>,
) -> Result<(), edge_registry::Error> {
    let versions = match kind {
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

// Seeds the built-in Thing Model unless an identical version already exists.
async fn seed_thing_model(client: &Client) -> Result<(), edge_registry::Error> {
    let document = DEMO_THING_MODEL.as_bytes();
    let versions = client
        .list_thing_model_versions(
            GroupSelection::Default,
            Some(DEMO_THING_MODEL_ID.to_string()),
            None,
            None,
            TIMEOUT,
        )
        .await?;

    for version in versions {
        let entity = client
            .get_thing_model_version(
                GroupId::CloudDefault,
                DEMO_THING_MODEL_ID.to_string(),
                GetVersionId::Specified(version.version_id),
                TIMEOUT,
            )
            .await?;
        if entity.document.as_ref() == document {
            println!(
                "Thing Model '{DEMO_THING_MODEL_ID}' already has demo version {}.",
                entity.version_id
            );
            return Ok(());
        }
    }

    let created =
        create_thing_model(client, DEMO_THING_MODEL_ID.to_string(), document.to_vec()).await?;
    println!(
        "Created demo Thing Model '{DEMO_THING_MODEL_ID}' version {}.",
        created.version_id
    );
    Ok(())
}

// Seeds the built-in Thing Description unless an identical version already exists.
async fn seed_thing_description(client: &Client) -> Result<(), edge_registry::Error> {
    let document = DEMO_THING_DESCRIPTION.as_bytes();
    let versions = client
        .list_thing_description_versions(
            GroupSelection::Default,
            Some(DEMO_THING_DESCRIPTION_ID.to_string()),
            None,
            None,
            TIMEOUT,
        )
        .await?;

    for version in versions {
        let entity = client
            .get_thing_description_version(
                GroupId::CloudDefault,
                DEMO_THING_DESCRIPTION_ID.to_string(),
                GetVersionId::Specified(version.version_id),
                TIMEOUT,
            )
            .await?;
        if entity.document.as_ref() == document {
            println!(
                "Thing Description '{DEMO_THING_DESCRIPTION_ID}' already has demo version {}.",
                entity.version_id
            );
            return Ok(());
        }
    }

    let created = create_thing_description(
        client,
        DEMO_THING_DESCRIPTION_ID.to_string(),
        document.to_vec(),
    )
    .await?;
    println!(
        "Created demo Thing Description '{DEMO_THING_DESCRIPTION_ID}' version {}.",
        created.version_id
    );
    Ok(())
}

// Creates a Thing Model version from raw WoT document bytes.
async fn create_thing_model(
    client: &Client,
    resource_id: String,
    document: Vec<u8>,
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
        ])
        .expect("command should parse");

        assert!(matches!(
            cli.command,
            Command::Add {
                kind: ResourceKind::ThingModel,
                resource_id,
                file,
            } if resource_id == "counter" && file == PathBuf::from("Counter.TM.json")
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
        };

        let error = read_add_document_if_needed(&command).expect_err("missing file should fail");
        assert!(error.to_string().contains("does-not-exist.json"));
    }

    #[test]
    fn top_level_help_lists_resource_kinds() {
        let error = Cli::try_parse_from(["edge_registry_client", "--help"])
            .expect_err("--help exits after rendering help");
        let help = error.to_string();

        assert!(help.contains("thing-model        A reusable WoT Thing Model"));
        assert!(help.contains("thing-description  A WoT Thing Description for a specific device"));
    }
}
