// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

//!Azure Device Registry Client that uses file mount to get names and create/delete notifications.

use notify::{Config, Event, EventKind, RecommendedWatcher, RecursiveMode, Watcher};
use std::fs::File;
use std::path::{Path, PathBuf};
use std::sync::{Arc, Mutex};
use std::time::Duration;
use tokio::sync::mpsc;
use tokio::sync::mpsc::Receiver;
use tokio_stream::{Stream, StreamExt};
/// A client that interacts with the file mount
///
/// This client provides functionality to retrieve device names and handle
/// create/delete notifications from the Azure Device Registry.
pub struct FileMountClient {
    mount_path: PathBuf,
    watcher: Arc<Mutex<RecommendedWatcher>>,
}

impl FileMountClient {
    /// Returns the mount path.
    pub fn get_mount_path(&self) -> &Path {
        &self.mount_path
    }

    /// Starts watching the mount path for changes.
    pub fn start_watching(&self) -> Result<(), FileMountError> {
        let mut watcher = self.watcher.lock().map_err(|_| {
            FileMountError::NotifyError(notify::Error::generic("Failed to lock watcher"))
        })?;
        watcher
            .watch(&self.mount_path, RecursiveMode::Recursive)
            .map_err(FileMountError::NotifyError)?;
        Ok(())
    }
}

impl FileMountClient {
    /// Creates a new instance of the `FileMountClient`.
    ///
    /// # Arguments
    /// * `mount_path_env_var` - The environment variable containing the path to the file mount.
    ///
    /// # Returns
    /// A `Result` containing the initialized `FileMountClient` or a `FileMountError`.
    pub async fn new(mount_path_env_var: &str) -> Result<Self, FileMountError> {
        let mount_path = PathBuf::from(mount_path_env_var);
        let watcher = notify::recommended_watcher(|_| {}).map_err(FileMountError::NotifyError)?;
        Ok(Self {
            mount_path: mount_path,
            watcher: Arc::new(Mutex::new(watcher)),
        })
    }

    /// Gets names of all devices from the file mount.
    ///
    /// # Returns
    /// A vector of device names as strings.
    pub async fn get_device_names(&self) -> Result<Vec<String>, Box<FileMountError>> {
        Ok(vec![])
    }

    /// Get names of all available assets from the monitored device.
    ///
    /// # Returns
    /// Get names of all available assets from the monitored directory
    pub async fn get_asset_names(&self) -> Result<Vec<String>, Box<FileMountError>> {
        Ok(vec![])
    }

    /// Observes the creation of device endpoints.
    ///
    /// # Returns
    /// A stream of `DeviceEndpoint` items representing newly created device endpoints.
    pub async fn observe_device_endpoint_create(
        &self,
    ) -> Result<impl Stream<Item = DeviceEndpoint> + Send + 'static, FileMountError> {
        // Monitor directory for new files
        // Parse filenames to extract device and endpoint names
        // Return stream of new device/endpoint combinations
        // Example implementation returning an empty stream
        Ok(tokio_stream::empty())
    }

    /// Observes the deletion of device endpoints.
    ///
    /// # Returns
    /// A stream of `DeviceEndpoint` items representing removed device endpoints.
    pub async fn observe_device_endpoint_delete(
        &self,
    ) -> Result<impl Stream<Item = DeviceEndpoint> + Send + 'static, FileMountError> {
        // Monitor directory for deleted files
        // Parse filenames to extract device and endpoint info
        // Return stream of removed device/endpoint combinations
        Ok(tokio_stream::empty())
    }

    /// Observes the creation of assets for a specific device and endpoint.
    ///
    /// # Arguments
    /// * `device` - The name of the device to monitor.
    /// * `endpoint` - The name of the endpoint to monitor.
    ///
    /// # Returns
    /// A stream of `Asset` items representing newly created assets.
    pub async fn observe_asset_create(
        &self,
        _device: &str,
        _endpoint: &str,
    ) -> Result<impl Stream<Item = Asset> + Send + 'static, FileMountError> {
        // Monitor specific file content changes
        // Compare old and new content to detect added assets
        // Return stream of newly added assets
        Ok(tokio_stream::empty())
    }

    /// Observes the deletion of assets for a specific device and endpoint.
    ///
    /// # Arguments
    /// * `device` - The name of the device to monitor.
    /// * `endpoint` - The name of the endpoint to monitor.
    ///
    /// # Returns
    /// A stream of `Asset` items representing removed assets.
    pub async fn observe_asset_delete(
        &self,
        _device: &str,
        _endpoint: &str,
    ) -> Result<impl Stream<Item = Asset> + Send + 'static, FileMountError> {
        // Monitor specific file content changes
        // Compare old and new content to detect removed assets
        // Return stream of removed assets
        Ok(tokio_stream::empty())
    }
}

/// Represents a device and its associated endpoint.
pub struct DeviceEndpoint {
    /// The name of the device
    pub device_name: String,
    /// The name of the endpoint
    pub endpoint_name: String,
}

/// Represents an asset associated with a specific device and endpoint.
pub struct Asset {
    /// The name of the asset
    pub name: String,
    /// The name of the device
    pub device_name: String,
    /// The name of the endpoint
    pub endpoint_name: String,
}

// Error type for your API
#[derive(Debug, thiserror::Error)]
/// Represents errors that can occur while interacting with the file mount.
pub enum FileMountError {
    #[error("Failed to access filesystem: {0}")]
    /// Error that occurs when accessing the filesystem.
    FilesystemError(#[from] std::io::Error),

    /// Error that occurs when there is an issue with the file watcher.
    #[error("Watcher error: {0}")]
    NotifyError(#[from] notify::Error),

    /// Error that occurs when parsing file content fails.
    #[error("Failed to parse file content: {0}")]
    ParseError(String),
    // Add other error variants as needed
}
