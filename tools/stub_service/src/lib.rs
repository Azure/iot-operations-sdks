// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

//! The Stub Service provides a local implementation of services that typically require a full
//! deployment of AIO. It is designed for inner loop development and testing, allowing developers to
//! also visualize the state of the service. The stub service is not intended for production use.
//!
//! To run the stub service, set the `STUB_SERVICE_OUTPUT_DIR` environment variable to the desired
//! output directory.
//!
//! An example of running the stub service is as follows:
//!
//! ```bash
//! export STUB_SERVICE_OUTPUT_DIR=/path/to/output/dir
//! cargo run # From the root of the crate
//! ```

use std::{
    path::Path,
    time::{Duration, SystemTime, UNIX_EPOCH},
};

use azure_iot_operations_mqtt::{
    MqttConnectionSettingsBuilder,
    session::{Session, SessionOptionsBuilder},
};

/// Module for the schema registry stub service.
pub mod schema_registry;

const HOSTNAME: &str = "localhost";
const PORT: u16 = 1883;
const STUB_SERVICE_OUTPUT_DIR_NAME: &str = "stub_service";
const STUB_SERVICE_ENVIRONMENT_VARIABLE: &str = "STUB_SERVICE_OUTPUT_DIR";

/// Helper function to create a new service session with the given client ID.
pub fn create_service_session(client_id: String) -> Result<Session, Box<dyn std::error::Error>> {
    let connection_settings = MqttConnectionSettingsBuilder::default()
        .client_id(client_id)
        .hostname(HOSTNAME)
        .tcp_port(PORT)
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

impl OutputDirectoryManager {
    /// Creates a new [`OutputDirectoryManager`] instance based on the environment variable. The
    /// output directory is named with the current timestamp.
    #[cfg(feature = "enable-output")]
    pub fn new() -> Self {
        // Read output directory from environment variable
        let output_dir = std::env::var(STUB_SERVICE_ENVIRONMENT_VARIABLE).expect(&format!(
            "{} must be set",
            STUB_SERVICE_ENVIRONMENT_VARIABLE
        ));

        // Create output directory for the stub service
        let output_stub_service_path = Path::new(&output_dir).join(format!(
            "{}_{}",
            STUB_SERVICE_OUTPUT_DIR_NAME,
            SystemTime::now()
                .duration_since(UNIX_EPOCH)
                .unwrap()
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
    pub fn new() -> Self {
        // If the feature is not enabled, return a dummy instance
        Self {
            output_stub_service_path: String::new(),
        }
    }

    /// Creates a new [`ServiceOutputManager`] for the given service name.
    ///
    /// The output directory for the service is created under the main output directory.
    #[cfg(feature = "enable-output")]
    fn create_new_service_output_manager(&self, service_name: &str) -> ServiceOutputManager {
        let service_dir = Path::new(&self.output_stub_service_path).join(service_name);

        std::fs::create_dir_all(Path::new(&service_dir))
            .expect("Failed to create service directory");

        // Create the service state directory
        ServiceOutputManager::new(
            service_dir
                .to_str()
                .expect("Created path is valid")
                .to_string(),
        )
    }

    /// Creates a new dummy [`ServiceOutputManager`] for the given service name if the output feature is not enabled.
    #[cfg(not(feature = "enable-output"))]
    fn create_new_service_output_manager(&self, _service_name: &str) -> ServiceOutputManager {
        // If the feature is not enabled, return a dummy instance
        ServiceOutputManager::new(String::new())
    }
}

/// Helper struct to manage the output directory for a specific service.
struct ServiceOutputManager {
    pub service_dir: String,
}

impl ServiceOutputManager {
    /// Creates a new [`ServiceOutputManager`] instance for the given service output directory.
    pub fn new(service_dir: String) -> Self {
        Self { service_dir }
    }

    /// Writes the state to a file in the service output directory.
    #[cfg(feature = "enable-output")]
    pub fn write_state(&self, file_name: &str, state: String) {
        let file_path = Path::new(&self.service_dir).join(file_name);

        // Overwrite the file if it exists
        std::fs::write(&file_path, state).expect("Failed to write state to file");
    }

    /// Dummy function to write the state if the output feature is not enabled.
    #[cfg(not(feature = "enable-output"))]
    pub fn write_state(&self, _file_name: &str, _state: String) {
        // If the feature is not enabled, do nothing
    }
}
