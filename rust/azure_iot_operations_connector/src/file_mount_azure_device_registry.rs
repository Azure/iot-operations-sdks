// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

//!Azure Device Registry Client that uses file mount to get names and create/delete notifications.

use notify::RecommendedWatcher;
use std::path::PathBuf;
use std::sync::{Arc, Mutex};
use tokio::sync::mpsc::UnboundedReceiver;
use tokio_stream::Stream;
/// A client that interacts with the file mount
///
/// This client provides functionality to retrieve device names and handle
/// create/delete notifications from the Azure Device Registry.
#[allow(dead_code)]
pub struct FileMountClient {
    /// The path to the file mount used by the client.
    pub mount_path: PathBuf,
    /// A file watcher used to monitor changes in the file mount.
    pub watcher: Arc<Mutex<RecommendedWatcher>>,
}

impl FileMountClient {
    /// Creates a new instance of the `FileMountClient`.
    ///
    /// # Arguments
    /// * `mount_path_env_var` - The environment variable containing the path to the file mount.
    ///
    /// # Returns
    /// A `Result` containing the initialized `FileMountClient` or a `FileMountError`.
    /// # Errors
    /// Returns an error if the file mount cannot be accessed or if there is an issue with the watcher.
    pub fn new(mount_path_env_var: &str) -> Result<Self, FileMountError> {
        // read env vars here direclty without taking them, const at top of files
        let mount_path = PathBuf::from(mount_path_env_var);
        let watcher = notify::recommended_watcher(|_| {}).map_err(FileMountError::NotifyError)?;
        Ok(Self {
            mount_path,
            watcher: Arc::new(Mutex::new(watcher)),
        })
    }

    /// Gets names of all devices from the file mount.
    ///
    /// # Returns
    /// A vector of device names as strings.    
    /// # Errors
    /// Returns an error if the file mount cannot be accessed or if there is an issue with the watcher.
    pub fn get_device_names(&self) -> Result<Vec<String>, FileMountError> {
        Ok(vec![])
    }

    /// Get names of all available assets from the monitored device.
    ///
    /// # Returns
    ///  names of all available assets from the monitored directory    
    /// # Errors
    /// Returns an error if the file mount cannot be accessed or if there is an issue with the watcher.
    pub fn get_asset_names(&self) -> Result<Vec<String>, FileMountError> {
        Ok(vec![])
    }

    /// Observes the creation of device endpoints.
    ///
    /// # Returns
    /// A stream of `DeviceEndpoint` items representing newly created device endpoints.    
    /// # Errors
    /// Returns an error if the file mount cannot be accessed or if there is an issue with the watcher.
    pub async fn observe_device_endpoint_create(
        &self,
    ) -> Result<impl Stream<Item = DeviceEndpointRef> + Send + 'static, FileMountError> {
        // Monitor directory for new files
        // Parse filenames to extract device and endpoint names
        // Return stream of new device/endpoint combinations

        // Example implementation returning an empty stream
        tokio::task::yield_now().await;
        Ok(tokio_stream::empty())
    }

    /// Observes the deletion of device endpoints.
    ///
    /// # Returns
    /// A stream of `DeviceEndpoint` items representing removed device endpoints.    
    /// # Errors
    /// Returns an error if the file mount cannot be accessed or if there is an issue with the watcher.
    pub async fn observe_device_endpoint_delete(
        &self,
    ) -> Result<impl Stream<Item = DeviceEndpointRef> + Send + 'static, FileMountError> {
        // Monitor directory for deleted files
        // Parse filenames to extract device and endpoint info
        // Return stream of removed device/endpoint combinations

        // Example implementation returning an empty stream
        tokio::task::yield_now().await;
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
    /// # Errors
    /// Returns an error if the file mount cannot be accessed or if there is an issue with the watcher.
    pub async fn observe_asset_create(
        &self,
        _device: &str,
        _endpoint: &str,
    ) -> Result<impl Stream<Item = AssetRef> + Send + 'static, FileMountError> {
        // Monitor specific file content changes
        // Compare old and new content to detect added assets
        // Return stream of newly added assets

        // Example implementation returning an empty stream
        tokio::task::yield_now().await;
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
    /// # Errors
    /// Returns an error if the file mount cannot be accessed or if there is an issue with the watcher.
    pub async fn observe_asset_delete(
        &self,
        _device: &str,
        _endpoint: &str,
    ) -> Result<impl Stream<Item = AssetRef> + Send + 'static, FileMountError> {
        // Monitor specific file content changes
        // Compare old and new content to detect removed assets
        // Return stream of removed assets

        // Example implementation returning an empty stream
        tokio::task::yield_now().await;
        Ok(tokio_stream::empty())
    }
}

/// Represents an observation for asset creation events.
///
/// This struct contains an internal channel for receiving notifications
/// about newly created assets.
pub struct AssetCreateObservation {
    /// The internal channel for receiving notifications for an asset creation event.
    #[allow(dead_code)]
    receiver: UnboundedReceiver<AssetRef>,
}
/// Represents a device and its associated endpoint.
pub struct DeviceEndpointRef {
    /// The name of the device
    pub device_name: String,
    /// The name of the endpoint
    pub endpoint_name: String,
}

/// Represents an asset associated with a specific device and endpoint.
pub struct AssetRef {
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
    /// NOT retriable
    FilesystemError(#[from] std::io::Error),

    /// Error that occurs when there is an issue with the file watcher.
    /// retriable ??
    #[error("Watcher error: {0}")]
    NotifyError(#[from] notify::Error),

    /// Error that occurs when parsing file content fails.
    /// retriable
    #[error("Failed to parse file content: {0}")]
    ParseError(String),
    // Add other error variants as needed
}
