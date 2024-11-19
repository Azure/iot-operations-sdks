// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

use core::str;
use std::time::Duration;
use std::fs;

use clap::{Subcommand, Parser};
use env_logger::Builder;

use azure_iot_operations_mqtt::session::{
    Session, SessionExitHandle, SessionManagedClient, SessionOptionsBuilder,
};
use azure_iot_operations_mqtt::MqttConnectionSettingsBuilder;
use azure_iot_operations_services::state_store::{self, SetOptions};

const TOOL_NAME : &str = "statestore-cli";
const TOOL_VERSION : &str = "0.0.1";
const TOOL_ABOUT_SHORT : &str = "Azure Device State Store CLI";
const TOOL_ABOUT_LONG : &str = "Allows managing key/value pairs in the Azure State Store.";

#[derive(Parser)]
#[command(version = TOOL_VERSION, about = TOOL_ABOUT_SHORT, long_about = TOOL_ABOUT_LONG)]
struct Cli {
    #[command(subcommand)]
    cmd: Commands,
    /// MQ broker hostname.
    #[arg(short = 'n', long = "hostname", default_value = "localhost", global = true)]
    hostname: String,
    /// MQ broker port number.
    #[arg(short, long, default_value_t = 8883, global = true)]
    port: u16,
    /// Do not use TLS for connection with MQ broker.
    #[arg(short = None, long, default_value_t = false, global = true)]
    notls: bool,
    /// Trusted certificate bundle for TLS connection. 
    #[arg(short = 'T', long, default_value = None, global = true)]
    cafile: Option<String>,
    /// Client authentication certificate file.
    #[arg(short = 'C', long, default_value = None, global = true)]
    certfile: Option<String>,
    /// Client authentication private key file.
    #[arg(short = 'K', long, default_value = None, global = true)]
    keyfile: Option<String>,
    /// Password for private key file.
    #[arg(short = 'P', long, default_value = None, global = true)]
    keypasswordfile: Option<String>,
    /// Verbose logging (errors).
    #[arg(short = None, long, default_value_t = false, global = true)]
    verbose: bool,
}

#[derive(Subcommand, Debug)]
enum Commands {
    /// Gets the value of an existing key.
    Get {
        /// Device State Store key name to retrieve.
        #[arg(short = 'k', long)]
        key: String,
        /// File where to write the key value.
        /// If not provided, the value is written to stdout.
        #[arg(short = 'f', long)]
        valuefile: Option<String>,
    },
    /// Sets a key and value.
    Set {
        /// Device State Store key name to update.
        #[arg(short = 'k', long)]
        key: String,
        /// File with content to set as value of the key.
        #[arg(short = None, long, conflicts_with = "valuefile")]
        value: Option<String>,
        /// File with content to set as value of the key.
        #[arg(short = 'f', long, conflicts_with = "value")]
        valuefile: Option<String>,
    },
    /// Deletes an existing key and value.
    Delete {
        /// Device State Store key name to delete.
        #[arg(short = 'k', long)]
        key: String,
    },
}

#[tokio::main(flavor = "current_thread")]
async fn main() {
    let exit_code : i32;

    let args = Cli::parse();

    let logging_level : log::LevelFilter =
        if args.verbose { log::LevelFilter::Trace }
           else { log::LevelFilter::Off };

    Builder::new()
        .filter_level(logging_level)
        .format_timestamp(None)
        .filter_module("rumqttc", log::LevelFilter::Warn)
        .init();

    // Create a session
    let connection_settings = MqttConnectionSettingsBuilder::default()
        .client_id(format!("{}-{}", TOOL_NAME, TOOL_VERSION))
        .hostname(args.hostname)
        .tcp_port(args.port)
        .keep_alive(Duration::from_secs(5))
        .use_tls(!args.notls)
        .ca_file(args.cafile)
        .cert_file(args.certfile)
        .key_file(args.keyfile)
        .key_password_file(args.keypasswordfile)
        .build()
        .unwrap();
    let session_options = SessionOptionsBuilder::default()
        .connection_settings(connection_settings)
        .build()
        .unwrap();
    let mut session = Session::new(session_options).unwrap();
    
    match args.cmd {
        Commands::Get { key, valuefile } => {
            let get_join_handle = tokio::task::spawn(state_store_get_value(
                session.create_managed_client(),
                session.create_exit_handle(),
                key,
                valuefile,
            ));

            session.run().await.unwrap();

            exit_code = get_join_handle.await.unwrap();
        },
        Commands::Set { key, value , valuefile } => {
            let actual_value = match value {
                Some(option_value) => option_value,
                None => fs::read_to_string(valuefile.unwrap()).expect("Could not open/read file")
            };

            let set_join_handle = tokio::task::spawn(state_store_set_value(
                session.create_managed_client(),
                session.create_exit_handle(),
                key,
                actual_value,
            ));

            session.run().await.unwrap();

            exit_code = set_join_handle.await.unwrap();
        },
        Commands::Delete { key } => {
            let delete_join_handle = tokio::task::spawn(state_store_delete_key(
                session.create_managed_client(),
                session.create_exit_handle(),
                key,
            ));

            session.run().await.unwrap();

            exit_code = delete_join_handle.await.unwrap();
        }
    }

    std::process::exit(exit_code);
}

async fn state_store_get_value(client: SessionManagedClient,
    exit_handle: SessionExitHandle, key: String, valuefile: Option<String>)
        -> i32 {
    let result;
    let state_store_key = key.as_bytes();
    let timeout = Duration::from_secs(10);

    let state_store_client = state_store::Client::new(
        client,
        state_store::ClientOptionsBuilder::default()
            .build()
            .unwrap(),
    )
    .unwrap();

    let get_response = state_store_client
        .get(state_store_key.to_vec(), timeout)
        .await
        .unwrap();

    match get_response.response {
        Some(response_body) => {
            if valuefile.is_none() {
                println!("{}", String::from_utf8(response_body).unwrap());
            } else {
                fs::write(valuefile.unwrap(), response_body)
                    .expect("Could not open/write to file.");
            }
            result = 0;
        },
        None => {
            result = 1;
        }
    };

    match exit_handle.try_exit().await {
        Ok(_exit_result) => {},
        Err(_exit_error) => {}
    };

    result
}

async fn state_store_set_value(client: SessionManagedClient,
    exit_handle: SessionExitHandle, key: String, value: String)
       -> i32 {
    let result;
    let state_store_key = key.as_bytes();
    let state_store_value = value.as_bytes();
    let timeout = Duration::from_secs(10);

    let state_store_client = state_store::Client::new(
        client,
        state_store::ClientOptionsBuilder::default()
            .build()
            .unwrap(),
    )
    .unwrap();

    let set_response = state_store_client
        .set(
            state_store_key.to_vec(),
            state_store_value.to_vec(),
            timeout,
            None,
            SetOptions {
                expires: None,
                ..SetOptions::default()
            },
        )
        .await
        .unwrap();

    match set_response.response {
      true => { result = 0; },  
      false => { result = 1; } 
    };

    match exit_handle.try_exit().await {
        Ok(_exit_result) => {},
        Err(_exit_error) => {}
    };

    result
}

async fn state_store_delete_key(client: SessionManagedClient,
    exit_handle: SessionExitHandle, key: String)
        -> i32 {
    let result;
    let state_store_key = key.as_bytes();
    let timeout = Duration::from_secs(10);

    let state_store_client = state_store::Client::new(
        client,
        state_store::ClientOptionsBuilder::default()
            .build()
            .unwrap(),
    )
    .unwrap();

    let delete_response = state_store_client
        .del(state_store_key.to_vec(), None, timeout)
        .await
        .unwrap();

    result = if delete_response.response == 1 { 0 } else { 1 };
  
    match exit_handle.try_exit().await {
        Ok(_exit_result) => {},
        Err(_exit_error) => {}
    };

    result
}
