use azure_iot_operations_protocol::application::ApplicationContextBuilder;
use env_logger::Builder;
use stub_service::{
    OutputDirectoryManager, create_service_session,
    schema_registry::{self},
};

#[tokio::main(flavor = "current_thread")]
async fn main() -> Result<(), Box<dyn std::error::Error>> {
    Builder::new()
        .filter_level(log::LevelFilter::Debug)
        .format_timestamp(None)
        .filter_module("rumqttc", log::LevelFilter::Warn)
        .filter_module("azure_iot_operations_protocol", log::LevelFilter::Warn)
        .filter_module("azure_iot_operations_mqtt", log::LevelFilter::Warn)
        .init();

    // Initialize the output directory manager
    let output_directory_manager = OutputDirectoryManager::new();

    // Create the application context
    let application_context = ApplicationContextBuilder::default().build().unwrap();

    // Create the schema registry service session and stub
    let sr_service_session =
        create_service_session(schema_registry::CLIENT_ID.to_string()).unwrap();
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
