// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

//! Azure Device Registry Client that uses file mount to get names and create/delete notifications.

use std::path::PathBuf;
use std::sync::{Arc, Mutex};
use std::time::Duration;

use notify::RecommendedWatcher;
use tokio::sync::mpsc::UnboundedReceiver;
use tokio::sync::oneshot;

/// Environment variable name for the directory containing device and asset names.
const ADR_RESOURCES_NAME_MOUNT_PATH: &str = "ADR_RESOURCES_NAME_MOUNT_PATH";

/// A client that interacts with the file mount
///
/// This client provides functionality to retrieve device names and handle
/// create/delete notifications from the Azure Device Registry.
#[allow(dead_code)]
pub struct FileMountClient {
    /// The path to the file mount used by the client.
    mount_path: PathBuf,
    /// A file watcher used to monitor changes in the file mount.
    watcher: Arc<Mutex<RecommendedWatcher>>,
}

impl FileMountClient {
    /// Creates a new instance of the `FileMountClient`.
    ///
    /// # Returns
    /// A `Result` containing the initialized `FileMountClient` or a `FileMountError`.
    /// # Errors
    /// Returns an error if the file mount cannot be accessed or if there is an issue with the watcher.
    pub fn new() -> Result<Self, FileMountError> {
        // read env vars here directly without taking them, const at top of files
        let mount_path = PathBuf::from(ADR_RESOURCES_NAME_MOUNT_PATH);
        let watcher = notify::recommended_watcher(|_| {}).map_err(FileMountError::NotifyError)?;

        Ok(Self {
            mount_path,
            watcher: Arc::new(Mutex::new(watcher)),
        })
    }

    /// Gets names of all devices from the file mount.
    ///
    /// # Arguments
    /// * `_timeout` - The duration to wait for the operation to complete.
    ///
    /// # Returns
    /// A vector of [`DeviceEndpointRef`].
    ///
    /// # Errors
    /// Returns an error if the file mount cannot be accessed or if there is an issue with the watcher.
    pub fn get_device_endpoint_names(
        &self,
        _timeout: Duration,
    ) -> Result<Vec<DeviceEndpointRef>, FileMountError> {
        Ok(vec![])
    }

    /// Get names of all available assets from the [`DeviceEndpointRef`].
    ///
    /// # Arguments
    /// * `_device_endpoint` - A reference to the device endpoint for which to get asset names.
    /// * `_timeout` - The duration to wait for the operation to complete.
    ///
    /// # Returns
    /// A vector of [`AssetRef`].
    ///  
    /// # Errors
    /// Returns an error if the file mount cannot be accessed or if there is an issue with the watcher.
    pub fn get_asset_names(
        &self,
        _device_endpoint: DeviceEndpointRef,
        _timeout: Duration,
    ) -> Result<Vec<AssetRef>, FileMountError> {
        Ok(vec![])
    }

    /// Observes the creation of device endpoints.
    ///
    /// # Arguments
    /// * `_timeout` - The duration to wait for the observation to complete.
    ///
    /// # Returns
    /// Returns OK([`DeviceEndpointCreateObservation`]) if observation was success.
    /// The [`DeviceEndpointCreateObservation`] can be used to receive notifications.
    ///
    /// # Errors
    /// Returns an error if the file mount cannot be accessed or if there is an issue with the watcher.
    pub async fn observe_device_endpoint_create(
        &self,
        _timeout: Duration,
    ) -> Result<DeviceEndpointCreateObservation, FileMountError> {
        let () = tokio::task::yield_now().await;
        Ok(DeviceEndpointCreateObservation {
            receiver: tokio::sync::mpsc::unbounded_channel().1,
        })
    }

    /// Observes for the deletion of a device endpoint.
    ///
    /// # Arguments
    /// * `device_endpoint` - A reference to the device endpoint for which to observe deletion.
    /// * `_timeout` - The duration to wait for the observation to complete.
    ///
    /// # Returns
    /// Returns OK([`DeviceEndpointDeleteObservation`]) if observation was success.
    /// The [`DeviceEndpointDeleteObservation`] can be used to receive notifications.
    ///
    /// # Errors
    /// Returns an error if the file mount cannot be accessed or if there is an issue with the watcher.
    pub async fn observe_device_endpoint_delete(
        &self,
        _device_endpoint: DeviceEndpointRef,
        _timeout: Duration,
    ) -> Result<DeviceEndpointDeleteObservation, FileMountError> {
        let () = tokio::task::yield_now().await;
        Ok(DeviceEndpointDeleteObservation {
            receiver: tokio::sync::oneshot::channel().1,
        })
    }

    /// Observes the creation of assets for a specific device and endpoint.
    ///
    /// # Arguments
    /// * `_device_endpoint_ref` - A reference to the device endpoint for which to observe asset creation.
    /// * `_timeout` - The duration to wait for the observation to complete.
    ///
    /// # Returns
    /// Returns OK([`AssetCreateObservation`]) if observation was success.
    /// The [`AssetCreateObservation`] can be used to receive notifications.
    ///
    /// # Errors
    /// Returns an error if the file mount cannot be accessed or if there is an issue with the watcher.
    pub async fn observe_asset_create(
        &self,
        _device_endpoint_ref: DeviceEndpointRef,
        _timeout: Duration,
    ) -> Result<AssetCreateObservation, FileMountError> {
        let () = tokio::task::yield_now().await;
        Ok(AssetCreateObservation {
            receiver: tokio::sync::mpsc::unbounded_channel().1,
        })
    }

    /// Observes for the deletion of an asset.
    ///
    /// # Arguments
    /// * `_asset_ref` - A reference to the asset for which to observe deletion.
    /// * `_timeout` - The duration to wait for the observation to complete.
    ///
    /// # Returns
    /// Returns OK([`AssetDeleteObservation`]) if observation was success.
    /// The [`AssetDeleteObservation`] can be used to receive notifications.
    ///
    /// # Errors
    /// Returns an error if the file mount cannot be accessed or if there is an issue with the watcher.
    pub async fn observe_asset_delete(
        &self,
        _asset_ref: AssetRef,
        _timeout: Duration,
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

/// Represents an observation for device endpoint deletion events.
///
/// This struct contains an internal channel for receiving notifications
/// about deleted device endpoints.
pub struct DeviceEndpointDeleteObservation {
    /// The internal channel for receiving notifications for an device deletion event.
    receiver: oneshot::Receiver<DeviceEndpointRef>,
}

impl DeviceEndpointDeleteObservation {
    /// Receives a notification for a deleted device endpoint.
    ///
    /// # Returns
    /// An `Option` containing a `DeviceEndpointRef` if a notification is received, or `None` if the channel is closed.
    pub async fn recv_notification(self) -> Option<DeviceEndpointRef> {
        self.receiver.await.ok()
    }
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

/// Represents an observation for asset deletion events.
///
/// This struct contains an internal channel for receiving notifications
/// about deleted assets.
pub struct AssetDeleteObservation {
    /// The internal channel for receiving notifications for an asset deletion event.
    receiver: oneshot::Receiver<AssetRef>,
}

impl AssetDeleteObservation {
    /// Receives a notification for a deleted asset.
    ///
    /// # Returns
    /// An `Option` containing an `AssetRef` if a notification is received, or `None` if the channel is closed.
    pub async fn recv_notification(self) -> Option<AssetRef> {
        self.receiver.await.ok()
    }
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
