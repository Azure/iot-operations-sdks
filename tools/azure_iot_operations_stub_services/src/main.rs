// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

use azure_iot_operations_protocol::application::ApplicationContextBuilder;

use azure_iot_operations_stub_services::{
    OutputDirectoryManager, create_service_session,
    schema_registry::{self},
};
use log::LevelFilter;
use log4rs::{
    Config,
    append::console::ConsoleAppender,
    config::{Appender, Logger, Root},
    encode::pattern::PatternEncoder,
    init_config,
};

const HOSTNAME: &str = "localhost";
const PORT: u16 = 1883;
const LOGGING_FILE_SIZE: u64 = 1024 * 1024 * 10; // 10 MB
const LOGGING_PATTERN: &str = "[{h({l})} {M}] {m}{n}"; // Pattern for log messages, ex: [ERROR stub_service::schema_registry] message

/// Helper function to initialize the logger for the stub service.
#[cfg(feature = "enable-output")]
fn initialize_logger(output_directory_manager: &OutputDirectoryManager) {
    // Create a file appender for the schema registry service
    let sr_appender = output_directory_manager.create_new_service_log_appender(
        schema_registry::SERVICE_NAME,
        LOGGING_FILE_SIZE,
        LOGGING_PATTERN,
    );

    // Create config for logger
    let config = Config::builder()
        .appender(
            Appender::builder().build(
                "stdout",
                Box::new(
                    ConsoleAppender::builder()
                        .encoder(Box::new(PatternEncoder::new(LOGGING_PATTERN)))
                        .build(),
                ),
            ),
        )
        .appender(Appender::builder().build(schema_registry::SERVICE_NAME, Box::new(sr_appender)))
        .logger(
            Logger::builder()
                .appender(schema_registry::SERVICE_NAME)
                .additive(true)
                .build("stub_service::schema_registry", log::LevelFilter::Debug),
        )
        .logger(Logger::builder().build("azure_iot_operations_mqtt", LevelFilter::Error))
        .logger(Logger::builder().build("azure_iot_operations_protocol", LevelFilter::Error))
        .logger(Logger::builder().build("rumqttc", LevelFilter::Off))
        .build(
            Root::builder()
                .appender("stdout")
                .build(log::LevelFilter::Debug),
        )
        .unwrap();

    // Initialize the logger
    init_config(config).unwrap();
}

/// If the "enable-output" feature is not enabled, the logger will only log to the console.
#[cfg(not(feature = "enable-output"))]
fn initialize_logger(_output_directory_manager: &OutputDirectoryManager) {
    let config = Config::builder()
        .appender(
            Appender::builder().build(
                "stdout",
                Box::new(
                    ConsoleAppender::builder()
                        .encoder(Box::new(PatternEncoder::new(LOGGING_PATTERN)))
                        .build(),
                ),
            ),
        )
        .logger(Logger::builder().build("azure_iot_operations_mqtt", LevelFilter::Error))
        .logger(Logger::builder().build("azure_iot_operations_protocol", LevelFilter::Error))
        .logger(Logger::builder().build("rumqttc", LevelFilter::Off))
        .build(
            Root::builder()
                .appender("stdout")
                .build(log::LevelFilter::Debug),
        )
        .unwrap();

    // Initialize the logger
    init_config(config).unwrap();
}

#[tokio::main(flavor = "current_thread")]
async fn main() -> Result<(), Box<dyn std::error::Error>> {
    // Initialize the output directory manager
    let output_directory_manager = OutputDirectoryManager::new();

    // Initialize the logger
    initialize_logger(&output_directory_manager);

    // Create the application context
    let application_context = ApplicationContextBuilder::default().build()?;

    // Create the schema registry service session and stub
    let sr_service_session = create_service_session(
        schema_registry::CLIENT_ID.to_string(),
        HOSTNAME.to_string(),
        PORT,
    )?;
    let sr_service_stub = schema_registry::Service::new(
        application_context,
        sr_service_session.create_managed_client(),
        &output_directory_manager,
    );

    // Run the stub services and their sessions
    tokio::select! {
        r1 = sr_service_session.run() => r1?,
        r2 = sr_service_stub.run() => r2.map_err(|e| e as Box<dyn std::error::Error>)?,
    }

    Ok(())
}
