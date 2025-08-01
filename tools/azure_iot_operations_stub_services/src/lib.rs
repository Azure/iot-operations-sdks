// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

//! The Stub Service provides a local implementation of services that typically require a full
//! deployment of AIO. It is designed for inner loop development and testing, allowing developers to
//! also visualize the state of the service. The stub service is not intended for production use.
//!
//! To run the stub service, set the `STUB_SERVICE_OUTPUT_DIR` environment variable to the desired
//! output directory or disable the `enable-output` feature.
//!
//! An example of running the stub service is as follows:
//!
//! ```bash
//! export STUB_SERVICE_OUTPUT_DIR=/path/to/output/dir
//! cargo run # From the root of the crate
//! ```
//!

use std::time::Duration;
#[cfg(feature = "enable-output")]
use std::{
    path::Path,
    time::{SystemTime, UNIX_EPOCH},
};

use azure_iot_operations_mqtt::{
    MqttConnectionSettingsBuilder,
    session::{Session, SessionOptionsBuilder},
};
#[cfg(feature = "enable-output")]
use log4rs::{
    append::rolling_file::{
        RollingFileAppender,
        policy::compound::{
            CompoundPolicy, roll::delete::DeleteRoller, trigger::size::SizeTrigger,
        },
    },
    encode::pattern::PatternEncoder,
};

/// Module for the schema registry stub service.
pub mod schema_registry;

#[cfg(feature = "enable-output")]
const STUB_SERVICE_OUTPUT_DIR_NAME: &str = "stub_service";
#[cfg(feature = "enable-output")]
const STUB_SERVICE_ENVIRONMENT_VARIABLE: &str = "STUB_SERVICE_OUTPUT_DIR";

/// Helper function to create a new service session with the given client ID.
pub fn create_service_session(
    client_id: String,
    hostname: String,
    port: u16,
) -> Result<Session, Box<dyn std::error::Error>> {
    let connection_settings = MqttConnectionSettingsBuilder::default()
        .client_id(client_id)
        .hostname(hostname)
        .tcp_port(port)
        .keep_alive(Duration::from_secs(5))
        .use_tls(false)
        .build()?;

    let session_options = SessionOptionsBuilder::default()
        .connection_settings(connection_settings)
        .build()?;

    Ok(Session::new(session_options)?)
}

/// Helper struct to manage the output directory for the stub service.
pub struct OutputDirectoryManager {
    pub output_stub_service_path: String,
}

impl Default for OutputDirectoryManager {
    /// Creates a new [`OutputDirectoryManager`] instance based on the environment variable. The
    /// output directory is named with the current timestamp.
    #[cfg(feature = "enable-output")]
    fn default() -> Self {
        // Read output directory from environment variable
        let output_dir = std::env::var(STUB_SERVICE_ENVIRONMENT_VARIABLE)
            .unwrap_or_else(|_| panic!("{STUB_SERVICE_ENVIRONMENT_VARIABLE} must be set"));

        // Create output directory for the stub service
        let output_stub_service_path = Path::new(&output_dir).join(format!(
            "{}_{}",
            STUB_SERVICE_OUTPUT_DIR_NAME,
            SystemTime::now()
                .duration_since(UNIX_EPOCH)
                .expect("Current time can't be before UNIX EPOCH")
                .as_secs()
        ));

        // Create the directory
        std::fs::create_dir_all(&output_stub_service_path)
            .expect("Failed to create output directory");

        Self {
            output_stub_service_path: output_stub_service_path
                .to_str()
                .expect("Failed to convert path to string")
                .to_string(),
        }
    }

    /// Creates a new [`OutputDirectoryManager`] instance with a dummy path if the output feature is not enabled.
    #[cfg(not(feature = "enable-output"))]
    fn default() -> Self {
        // If the feature is not enabled, return a dummy instance
        Self {
            output_stub_service_path: String::new(),
        }
    }
}

impl OutputDirectoryManager {
    /// Creates a new [`ServiceStateOutputManager`] for the given service name.
    ///
    /// The output directory for the service is created under the main output directory specified by
    /// the environment variable.
    #[cfg(feature = "enable-output")]
    fn create_new_service_output_manager(&self, service_name: &str) -> ServiceStateOutputManager {
        let service_state_dir = Path::new(&self.output_stub_service_path)
            .join(service_name)
            .join("state"); // Directory for service state

        std::fs::create_dir_all(Path::new(&service_state_dir))
            .expect("Failed to create service directory");

        // Create the service state directory
        ServiceStateOutputManager::new(
            service_state_dir
                .to_str()
                .expect("Created path is valid")
                .to_string(),
        )
    }

    /// Creates a new dummy [`ServiceStateOutputManager`] for the given service name if the output feature is not enabled.
    #[cfg(not(feature = "enable-output"))]
    fn create_new_service_output_manager(&self, _service_name: &str) -> ServiceStateOutputManager {
        // If the feature is not enabled, return a dummy instance
        ServiceStateOutputManager::new(String::new())
    }

    /// Creates a new [`RollingFileAppender`] for the given service name and returns it.
    ///
    /// The appender is configured to append logs to a file in the service's log directory with a
    /// rolling policy based on size.
    #[cfg(feature = "enable-output")]
    pub fn create_new_service_log_appender(
        &self,
        service_name: &str,
        size_limit: u64,
        logging_partner: &str,
    ) -> RollingFileAppender {
        let service_log_dir = Path::new(&self.output_stub_service_path)
            .join(service_name)
            .join("logs"); // Directory for service logs

        std::fs::create_dir_all(Path::new(&service_log_dir))
            .expect("Failed to create service directory");

        // Create a policy for rolling the log file based on size
        let compound_policy = CompoundPolicy::new(
            Box::new(SizeTrigger::new(size_limit)),
            Box::new(DeleteRoller::new()),
        );

        RollingFileAppender::builder()
            .append(true)
            .encoder(Box::new(PatternEncoder::new(logging_partner)))
            .build(
                service_log_dir
                    .join("log.log")
                    .to_str()
                    .expect("Created path is valid"),
                Box::new(compound_policy),
            )
            .expect("Creating file appender should not fail")
    }
}

/// Helper struct to manage the output directory for a specific service's state.
struct ServiceStateOutputManager {
    #[cfg(feature = "enable-output")]
    pub service_dir: String,
}

impl ServiceStateOutputManager {
    /// Creates a new [`ServiceStateOutputManager`] instance for the given service state output directory.
    #[cfg(feature = "enable-output")]
    pub fn new(service_dir: String) -> Self {
        Self { service_dir }
    }

    /// Creates a new dummy [`ServiceStateOutputManager`] instance if the output feature is not enabled.
    #[cfg(not(feature = "enable-output"))]
    pub fn new(_service_dir: String) -> Self {
        Self {}
    }

    /// Writes the state to a JSON file in the service state output directory.
    #[cfg(feature = "enable-output")]
    pub fn write_state(&self, file_name: &str, state: String) {
        // Append JSON extension to the file name
        let file_name = format!("{file_name}.json");

        let file_path = Path::new(&self.service_dir).join(file_name);

        // Overwrite the file if it exists
        std::fs::write(&file_path, state).expect("Writing state to file should not fail");
    }

    /// Dummy function to write the state if the output feature is not enabled.
    #[cfg(not(feature = "enable-output"))]
    pub fn write_state(&self, _file_name: &str, _state: String) {
        // If the feature is not enabled, do nothing
    }
}
