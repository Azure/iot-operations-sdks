// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

//!Azure Device Registry Client that uses file mount to get names and create/delete notifications.

use notify::RecommendedWatcher;
use std::path::PathBuf;
use std::sync::{Arc, Mutex};
use tokio::sync::mpsc::UnboundedReceiver;

const ADR_RESOURCES_NAME_MOUNT_PATH: &str = "/etc/akri/config/adr_resources_names";

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
    pub fn new() -> Result<Self, FileMountError> {
        // read env vars here direclty without taking them, const at top of files
        let mount_path = PathBuf::from(ADR_RESOURCES_NAME_MOUNT_PATH);
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
    pub fn get_device_names(&self) -> Result<Vec<DeviceEndpointRef>, FileMountError> {
        Ok(vec![])
    }

    /// Get names of all available assets from the monitored device.
    ///
    /// # Returns
    ///  names of all available assets from the monitored directory    
    /// # Errors
    /// Returns an error if the file mount cannot be accessed or if there is an issue with the watcher.
    pub fn get_asset_names(
        &self,
        _device_endpoint: DeviceEndpointRef,
    ) -> Result<Vec<AssetRef>, FileMountError> {
        Ok(vec![])
    }

    /// Observes the creation of device endpoints.
    ///
    /// # Returns
    /// `DeviceEndpointCreateObservation`.
    /// # Errors
    /// Returns an error if the file mount cannot be accessed or if there is an issue with the watcher.
    pub async fn observe_device_endpoint_create(
        &self,
    ) -> Result<DeviceEndpointCreateObservation, FileMountError> {
        //todo!("Implement device endpoint creation observation");
        let () = tokio::task::yield_now().await;
        Ok(DeviceEndpointCreateObservation {
            receiver: tokio::sync::mpsc::unbounded_channel().1,
        })
    }

    /// Observes the deletion of device endpoints.
    ///
    /// # Errors
    /// Returns an error if the file mount cannot be accessed or if there is an issue with the watcher.
    pub async fn observe_device_endpoint_delete(
        &self,
        _device_endpoint: DeviceEndpointRef,
    ) -> Result<DeviceEndpointDeleteObservation, FileMountError> {
        let () = tokio::task::yield_now().await;
        Ok(DeviceEndpointDeleteObservation {
            receiver: tokio::sync::oneshot::channel().1,
        })
    }

    /// Observes the creation of assets for a specific device and endpoint.
    ///
    /// # Arguments
    /// * `device` - The name of the device to monitor.
    /// * `endpoint` - The name of the endpoint to monitor.
    ///
    /// # Returns
    /// `AssetCreateObservation`
    /// # Errors
    /// Returns an error if the file mount cannot be accessed or if there is an issue with the watcher.
    pub async fn observe_asset_create(
        &self,
        _device_endpoint_ref: DeviceEndpointRef,
    ) -> Result<AssetCreateObservation, FileMountError> {
        let () = tokio::task::yield_now().await;
        Ok(AssetCreateObservation {
            receiver: tokio::sync::mpsc::unbounded_channel().1,
        })
    }

    /// Observes the deletion of assets for a specific device and endpoint.
    ///
    /// # Arguments
    /// * `device` - The name of the device to monitor.
    /// * `endpoint` - The name of the endpoint to monitor.
    ///
    /// # Errors
    /// Returns an error if the file mount cannot be accessed or if there is an issue with the watcher.
    pub async fn observe_asset_delete(
        &self,
        _asset_ref: AssetRef,
    ) -> Result<AssetDeleteObservation, FileMountError> {
        let () = tokio::task::yield_now().await;
        Ok(AssetDeleteObservation {
            receiver: tokio::sync::oneshot::channel().1,
        })
    }
}

/// Represents an observation for device endpoint creation events.
///
/// This struct contains an internal channel for receiving notifications
/// about newly created device endpoints.
pub struct DeviceEndpointCreateObservation {
    receiver: UnboundedReceiver<DeviceEndpointRef>,
}

impl DeviceEndpointCreateObservation {
    /// Receives a notification for a newly created device endpoint.
    ///
    /// # Returns
    /// An `Option` containing a `DeviceEndpointRef` if a notification is received, or `None` if the channel is closed.
    pub async fn recv_notification(&mut self) -> Option<DeviceEndpointRef> {
        self.receiver.recv().await
    }
}

pub struct DeviceEndpointDeleteObservation {
    receiver: tokio::sync::oneshot::Receiver<DeviceEndpointRef>,
}

/// Represents a device and its associated endpoint.
pub struct DeviceEndpointRef {
    /// The name of the device
    pub device_name: String,
    /// The name of the endpoint
    pub endpoint_name: String,
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

impl AssetCreateObservation {
    /// Receives a notification for a newly created asset.
    ///
    /// # Returns
    /// An `Option` containing an `AssetRef` if a notification is received, or `None` if the channel is closed.
    pub async fn recv_notification(&mut self) -> Option<AssetRef> {
        self.receiver.recv().await
    }
}

pub struct AssetDeleteObservation {
    /// The internal channel for receiving notifications for an asset creation event.
    #[allow(dead_code)]
    receiver: tokio::sync::oneshot::Receiver<AssetRef>,
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
