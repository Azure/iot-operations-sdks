// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

//! Utilities for interacting with the file mount of Azure Device Registry.

use std::collections::{HashMap, HashSet};
use std::env;
use std::fmt::{Display, Formatter};
use std::path::{Path, PathBuf};
use std::time::Duration;

use notify::{RecommendedWatcher, event::EventKind};
use notify_debouncer_full::{RecommendedCache, new_debouncer};
use thiserror::Error;
use tokio::sync::mpsc::{self, UnboundedReceiver, UnboundedSender};
use tokio_util::sync::{CancellationToken, DropGuard};

use crate::DataOperationRef;

/// Environment variable name for the mount path of the Azure Device Registry resources.
const ADR_RESOURCES_NAME_MOUNT_PATH: &str = "ADR_RESOURCES_NAME_MOUNT_PATH";

/// Represents an error that occurred while interacting with the file mount of Azure Device Registry.
#[derive(Debug, Error)]
#[error(transparent)]
pub struct Error(#[from] ErrorKind);

/// Represents the kinds of errors that may occur while interacting with the file mount of Azure Device Registry.
#[derive(Error, Debug)]
pub enum ErrorKind {
    /// An error occurred when parsing the environment variable for the mount path.
    #[error(transparent)]
    EnvironmentVariableError(#[from] env::VarError),

    /// An error occurred while accessing the file mount.
    #[error(transparent)]
    IoError(#[from] std::io::Error),

    /// An error occurred while creating the file mount watcher.
    #[error(transparent)]
    WatcherError(#[from] notify::Error),

    /// An error occurred while parsing the files in the file mount.
    #[error("{0}")]
    ParseError(String),
}

// ~~~~~~~~~~~~~~~~~ Observations ~~~~~~~~~~~~~~~~~~~~~

/// Represents an observation for device endpoint creation events.
pub struct DeviceEndpointCreateObservation {
    /// A file watcher used to monitor changes in the file mount.
    #[allow(dead_code)]
    debouncer: notify_debouncer_full::Debouncer<RecommendedWatcher, RecommendedCache>,
    /// A channel for receiving notifications about device endpoint creation events.
    create_device_rx: UnboundedReceiver<(DeviceEndpointRef, AssetCreateObservation)>,
}

impl DeviceEndpointCreateObservation {
    /// Creates an instance of [`DeviceEndpointCreateObservation`] to observe device endpoint creation events.
    ///
    /// Returns Ok([`DeviceEndpointCreateObservation`]) if the observation is successfully created, otherwise returns an [`struct@Error`].
    ///
    /// # Arguments
    /// * `debounce_duration` - The duration to debounce incoming I/O events. Note that the value depends on the
    ///   file system and the number of events being generated. A value of 1s is a good starting point.
    ///
    /// # Errors
    /// - [`struct@Error`] of kind [`ErrorKind::EnvironmentVariableError`] if the environment variable was not able to be read.
    /// - [`struct@Error`] of kind [`ErrorKind::WatcherError`] if the watcher could not be created.
    /// - [`struct@Error`] of kind [`ErrorKind::IoError`] if there are issues accessing the file mount.
    /// - [`struct@Error`] of kind [`ErrorKind::ParseError`] if there are issues parsing the file names and content.
    pub fn new(debounce_duration: Duration) -> Result<DeviceEndpointCreateObservation, Error> {
        let mount_path = get_mount_path()?;

        // This channel is used to send notifications about device endpoint creation
        let (create_device_tx, create_device_rx) = mpsc::unbounded_channel();

        // Tracks devices and assets in the file mount.
        let mut file_mount_map = FileMountMap::new(create_device_tx, mount_path.clone());

        // Get all the current devices
        let device_endpoints = get_device_endpoint_names(&mount_path)?;

        // Create the file mount map with the current devices
        file_mount_map.update_device_endpoints(&device_endpoints);

        // Copy the mount path to the closure so we can use it in the watcher
        let mount_path_clone = mount_path.clone();

        // TODO: When the number of files being tracked is large, the watcher might not be able to keep up with the events.
        // In the future we should consider adding redundancy checks (like hashing device endpoint files)
        // to ensure that the file mount map is in sync with the file mount.
        let mut debouncer = new_debouncer(
            debounce_duration,
            None,
            move |res: Result<Vec<notify_debouncer_full::DebouncedEvent>, Vec<notify::Error>>| {
                match res {
                    Ok(events) => {
                        // When an asset is added or removed, kubernetes does a series of events:
                        // Create Folder, Create File and Remove Folder
                        // If any of those events are triggered we need to update the file mount map
                        // with the new device endpoints and assets since none of those events
                        // point to a specific file.
                        if events.iter().any(|e| {
                            matches!(
                                e.kind,
                                EventKind::Remove(_) | EventKind::Create(_) | EventKind::Modify(_) // Keep this for now in case it is needed
                            )
                        }) {
                            match get_device_endpoint_names(&mount_path_clone) {
                                Ok(device_endpoints) => {
                                    // Check if the device endpoints have changed and update the file mount map
                                    // accordingly
                                    file_mount_map.update_device_endpoints(&device_endpoints);
                                }
                                Err(err) => {
                                    log::warn!("Failed to get device endpoint names: {err:?}");
                                }
                            }
                        }
                    }
                    Err(err) => {
                        // TODO: There should be a way for us to surface this error to the user
                        for e in &err {
                            log::error!("Error processing events from watcher: {e:?}");
                        }
                    }
                }
            },
        )
        .map_err(ErrorKind::from)?;

        // Start watching the file mount path for create, modify and remove events
        debouncer
            .watch(&mount_path, notify::RecursiveMode::NonRecursive)
            .map_err(ErrorKind::from)?;

        Ok(Self {
            debouncer,
            create_device_rx,
        })
    }

    /// Receives a notification for a newly created device endpoint.
    ///
    /// Returns Some(([`DeviceEndpointRef`], [`AssetCreateObservation`])) if a notification is received or `None`
    /// if there will be no more notifications (i.e. the channel is closed). This should not happen unless the
    /// [`DeviceEndpointCreateObservation`] is dropped.
    ///
    /// The [`AssetCreateObservation`] should be used to receive notifications for asset creation events
    /// associated with the device endpoint. If it returns None, it means the device endpoint was deleted.
    pub async fn recv_notification(
        &mut self,
    ) -> Option<(DeviceEndpointRef, AssetCreateObservation)> {
        self.create_device_rx.recv().await
    }
}

/// Represents an observation for asset creation events.
#[derive(Debug)]
pub struct AssetCreateObservation {
    /// A channel for receiving notifications about asset creation events.
    asset_creation_rx: UnboundedReceiver<(AssetRef, CancellationToken)>,
}

impl AssetCreateObservation {
    /// Creates an instance of [`AssetCreateObservation`] to observe asset creation events.
    ///
    /// Returns a new [`AssetCreateObservation`] instance.
    pub(crate) fn new(
        asset_creation_rx: UnboundedReceiver<(AssetRef, CancellationToken)>,
    ) -> AssetCreateObservation {
        Self { asset_creation_rx }
    }

    /// Receives a notification for a newly created asset.
    ///
    /// Returns Some(([`AssetRef`], [`CancellationToken`])) if a notification is received or `None`
    /// if there will be no more notifications (i.e. the device endpoint was deleted).
    ///
    /// The [`CancellationToken`] can be used to wait for the deletion of the asset.
    pub async fn recv_notification(&mut self) -> Option<(AssetRef, CancellationToken)> {
        self.asset_creation_rx.recv().await
    }
}

// ~~~~~~~~~~~~~~~~~ Helper Functions and Structs ~~~~~~~~~~~~~~~~~~~~~

/// Gets the mount path of the Azure Device Registry resources.
///
/// Returns Ok([`PathBuf`]) if the mount path is successfully retrieved, otherwise returns an [`struct@Error`].
///
/// # Errors
///
/// - [`struct@Error`] of kind [`ErrorKind::EnvironmentVariableError`] if the environment variable was not able to be read.
pub fn get_mount_path() -> Result<PathBuf, Error> {
    Ok(env::var(ADR_RESOURCES_NAME_MOUNT_PATH)
        .map_err(ErrorKind::from)?
        .into())
}

/// Get names of all available device endpoints from the file mount.
///
/// Returns Ok([`HashSet<DeviceEndpointRef>`]) if the device endpoint names are successfully retrieved, otherwise returns an [`struct@Error`].
///
/// # Errors
///
/// - [`struct@Error`] of kind [`ErrorKind::IoError`] if there are issues accessing the file mount.
/// - [`struct@Error`] of kind [`ErrorKind::ParseError`] if the file names cannot be parsed into [`DeviceEndpointRef`].
pub fn get_device_endpoint_names(mount_path: &Path) -> Result<HashSet<DeviceEndpointRef>, Error> {
    // TODO: There is probably a way to do this without needing the below for loop.
    let mut device_endpoint_refs = HashSet::new();
    for entry in std::fs::read_dir(mount_path).map_err(ErrorKind::from)? {
        match entry.map_err(ErrorKind::from)?.path().file_name() {
            Some(file_name) => {
                // TODO: Handle case where file name has invalid UTF-8 characters (remove need for to_string_lossy)
                // TODO: Handle case where file name is not a file but a directory
                let device_endpoint_string = file_name.to_string_lossy().to_string();
                if device_endpoint_string.starts_with("..") {
                    // There are files with the path starting in .. that are not device endpoints.
                    // This is never a valid device endpoint name due to kubernetes enforcing resources
                    // needing to start with alphanumeric characters so it can be safely ignored.
                    continue;
                }
                match device_endpoint_string.try_into() {
                    Ok(device_endpoint) => {
                        device_endpoint_refs.insert(device_endpoint);
                    }
                    Err(err) => {
                        log::warn!("Failed to parse device endpoint from file name: {err:?}");
                    }
                }
            }
            None => {
                // TODO: Happens when the path ends with "..", skip it and log a warning for now
                log::warn!(
                    "Failed to get file name from device endpoint directory, path ends in .."
                );
            }
        }
    }
    Ok(device_endpoint_refs)
}

/// Gets the names of all assets associated with a [`DeviceEndpointRef`] from the file mount.
///
/// Returns Ok([`HashSet<AssetRef>`]) if the asset names are successfully retrieved, otherwise returns an [`struct@Error`].
///
/// # Errors
///
/// - [`struct@Error`] of kind [`ErrorKind::IoError`] if there are issues accessing the file mount.
/// - [`struct@Error`] of kind [`ErrorKind::ParseError`] if the device endpoint's file content cannot be parsed.
fn get_asset_names(
    mount_path: &Path,
    device_endpoint: &DeviceEndpointRef,
) -> Result<HashSet<AssetRef>, Error> {
    // Create the file path for the device endpoint
    let file_path = mount_path.join(device_endpoint.to_string());

    // Get the content of the file
    let file_content = String::from_utf8(std::fs::read(file_path).map_err(ErrorKind::from)?)
        .map_err(|e| ErrorKind::ParseError(e.to_string()))?;

    // If the file is empty, return an empty vector
    if file_content.is_empty() {
        return Ok(HashSet::new());
    }

    // Split the file content by newline and create a HashSet of AssetRef
    Ok(file_content
        .lines()
        .map(|asset_name| AssetRef {
            name: asset_name.to_string(),
            device_name: device_endpoint.device_name.clone(),
            inbound_endpoint_name: device_endpoint.inbound_endpoint_name.clone(),
        })
        .collect())
}

/// A map that tracks device endpoints and their associated assets.
///
/// This struct contains a map of device endpoints to their associated assets and a channel for
/// sending notifications about device endpoint creation.
///
/// Each device endpoint is associated with a tuple containing an unbounded sender for asset creation
/// notifications and a hash map of asset references to their associated deletion token drop guards.
struct FileMountMap {
    // TODO: This is a complex type, need to simplify it later
    file_mount_path: PathBuf,
    #[allow(clippy::type_complexity)]
    file_mount_hashmap: HashMap<
        DeviceEndpointRef,
        (
            UnboundedSender<(AssetRef, CancellationToken)>,
            HashMap<AssetRef, DropGuard>,
        ),
    >,
    create_device_tx: UnboundedSender<(DeviceEndpointRef, AssetCreateObservation)>,
}

impl FileMountMap {
    /// Creates a new instance of [`FileMountMap`].
    pub fn new(
        create_device_tx: UnboundedSender<(DeviceEndpointRef, AssetCreateObservation)>,
        file_mount_path: PathBuf,
    ) -> FileMountMap {
        FileMountMap {
            file_mount_hashmap: HashMap::new(),
            create_device_tx,
            file_mount_path,
        }
    }

    /// Updates the device endpoints in the file mount map.
    ///
    /// This function takes a set of device endpoints and updates the file mount map with the new
    /// device endpoints. It also cleans up any device endpoints that are no longer present.
    ///
    /// If a device endpoint is not already in the file mount map, it creates a new entry for it.
    /// It also retrieves the assets associated with the device endpoint and updates them in the map
    /// for all the devices.
    pub fn update_device_endpoints(&mut self, devices: &HashSet<DeviceEndpointRef>) {
        // Remove devices that are no longer present in the file mount
        self.file_mount_hashmap
            .retain(|device_ref, _| devices.contains(device_ref));

        // Iterate over the devices and add them to the file mount map
        for device in devices {
            // If the device is not already in the file mount map, create a new entry
            if !self.file_mount_hashmap.contains_key(device) {
                // Create a new channel for asset creation notifications for this device
                let (asset_creation_tx, asset_creation_rx) = mpsc::unbounded_channel();

                // Create a new entry in the file mount map for the device
                log::info!("New device: {device:?}");
                self.file_mount_hashmap
                    .insert(device.clone(), (asset_creation_tx, HashMap::new()));

                // Notify on device creation
                if self
                    .create_device_tx
                    .send((
                        device.clone(),
                        AssetCreateObservation::new(asset_creation_rx),
                    ))
                    .is_err()
                {
                    // TODO: Decide if this error should be returned.
                    // TODO: The cases in which this fails are:
                    // 1. The receiver is closed which means the `FileMountMap` is dropped, this should not happen.
                    // 2. The receiver is full which means we are not receiving notifications fast enough or
                    //    are out of space, this should be handled by the caller via a retry.
                    log::error!("Failed to send device creation notification");
                    panic!("Failed to send device creation notification");
                }
            }

            // Get the assets in the file mount for this device
            let assets = match get_asset_names(&self.file_mount_path, device) {
                Ok(assets) => assets,
                Err(err) => {
                    log::warn!("Failed to get asset names: {err:?}");
                    continue;
                }
            };

            // Add the assets
            self.update_assets(device, assets);
        }
    }

    /// Updates the assets associated with a device endpoint.
    ///
    /// This function takes a device endpoint and a set of assets, and updates the file mount map
    /// with the new assets. It also cleans up any assets that are no longer present. If the device
    /// endpoint does not exist in the file mount map, it does nothing.
    pub fn update_assets(&mut self, device: &DeviceEndpointRef, mut assets: HashSet<AssetRef>) {
        // Get the asset creation channel and the tracked assets for this device
        let Some((create_asset_tx, tracked_assets)) = self.file_mount_hashmap.get_mut(device)
        else {
            // If the device is non-existent we can't update the assets. Most likely a create
            // notification has not been parsed yet but this function will be called again once
            // the device is created.
            return;
        };
        // When an asset is not retained it gets dropped and its cancellation token is triggered when
        // the channel associated with it is dropped.
        tracked_assets.retain(|tracked_asset, _| assets.remove(tracked_asset));

        // Iterate over the assets and check if they are already tracked, if not, add them
        for asset in assets {
            if !tracked_assets.contains_key(&asset) {
                // Create a cancellation token for asset deletion
                let asset_deletion_token = CancellationToken::new();

                // Add the new asset to the tracked assets with the cancellation token drop guard so
                // when the asset is deleted the cancellation token is cancelled.
                log::info!("New asset: {asset:?}");
                tracked_assets.insert(asset.clone(), asset_deletion_token.clone().drop_guard());

                // Notify that an asset has been created
                if create_asset_tx
                    .send((asset.clone(), asset_deletion_token))
                    .is_err()
                {
                    // TODO: The cases in which this fails are:
                    // 1. The receiver is closed which means the `FileMountMap` is dropped, this should not happen.
                    // 2. The receiver is full which means we are not receiving notifications fast enough or
                    //    are out of space, this should be handled by the caller via a retry.
                    log::error!("Failed to send device creation notification");
                    panic!("Failed to send device creation notification");
                }
            }
        }
    }
}

// ~~~~~~~~~~~~~~~~~ Device and Asset References ~~~~~~~~~~~~~~~~~~~~~

/// Represents a device and its associated endpoint.
#[derive(Debug, Clone, PartialEq, Eq, Hash, PartialOrd, Ord)]
pub struct DeviceEndpointRef {
    // TODO: This structure should be DeviceRef from the services crate
    /// The name of the device
    pub device_name: String,
    /// The name of the inbound endpoint
    pub inbound_endpoint_name: String,
}

impl TryFrom<String> for DeviceEndpointRef {
    type Error = Error;

    fn try_from(value: String) -> Result<Self, Self::Error> {
        // The below assumes the format is always {device_name}_{inbound_endpoint_name} with no additional
        // `_` in the device name. The inbound endpoint name may have additional `_`.
        // Kubernetes does not allow `_` in the device name so this should be safe.

        // TODO: Add a warning in case the format is not as expected
        match value.split_once('_') {
            Some((device_name, inbound_endpoint_name)) => Ok(Self {
                device_name: device_name.to_string(),
                inbound_endpoint_name: inbound_endpoint_name.to_string(),
            }),
            None => Err(Error(ErrorKind::ParseError(
                "Failed to parse DeviceEndpointRef from string".to_string(),
            ))),
        }
    }
}

impl TryFrom<&PathBuf> for DeviceEndpointRef {
    type Error = Error;

    fn try_from(value: &PathBuf) -> Result<Self, Self::Error> {
        // TODO: Handle case where file name is not a file but a directory
        // TODO: Handle case where file name has invalid UTF-8 characters (remove need for to_string_lossy)
        value
            .file_name()
            .ok_or(ErrorKind::ParseError("File path ends in ..".to_string()))?
            .to_string_lossy()
            .to_string()
            .try_into()
    }
}

impl Display for DeviceEndpointRef {
    fn fmt(&self, f: &mut Formatter<'_>) -> std::fmt::Result {
        write!(f, "{}_{}", self.device_name, self.inbound_endpoint_name)
    }
}

/// Represents an asset associated with a specific device and endpoint.
#[derive(Debug, Clone, PartialEq, Eq, Hash)]
pub struct AssetRef {
    // TODO: This structure should be obtained from the services crate.
    /// The name of the asset
    pub name: String,
    /// The name of the device
    pub device_name: String,
    /// The name of the endpoint
    pub inbound_endpoint_name: String,
}

impl From<&DataOperationRef> for AssetRef {
    fn from(data_operation_ref: &DataOperationRef) -> Self {
        AssetRef {
            name: data_operation_ref.asset_name.clone(),
            device_name: data_operation_ref.device_name.clone(),
            inbound_endpoint_name: data_operation_ref.inbound_endpoint_name.clone(),
        }
    }
}

impl From<AssetRef> for azure_iot_operations_services::azure_device_registry::AssetRef {
    fn from(asset_ref: AssetRef) -> Self {
        azure_iot_operations_services::azure_device_registry::AssetRef {
            name: asset_ref.name,
            device_name: asset_ref.device_name,
            inbound_endpoint_name: asset_ref.inbound_endpoint_name,
        }
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    use std::collections::HashSet;
    use std::fs;
    use std::io::Write;

    const DEBOUNCE_DURATION: Duration = Duration::from_millis(500);

    // Macro to create a device endpoint reference and associated asset references
    macro_rules! device_with_assets {
        ($device_name:expr, $inbound_endpoint_name:expr $(, $asset_name:expr)*) => {
            {
                let device_endpoint = DeviceEndpointRef {
                    device_name: $device_name.to_string(),
                    inbound_endpoint_name: $inbound_endpoint_name.to_string(),
                };
                let assets = vec![
                    $(
                        AssetRef {
                            name: $asset_name.to_string(),
                            device_name: $device_name.to_string(),
                            inbound_endpoint_name: $inbound_endpoint_name.to_string(),
                        }
                    ),*
                ];
                (device_endpoint, assets)
            }
        };
    }

    struct TempFileMountManager {
        dir: tempfile::TempDir,
    }

    impl TempFileMountManager {
        fn new(dir_name: &str) -> Self {
            Self {
                dir: tempfile::TempDir::with_prefix(dir_name).unwrap(),
            }
        }

        fn add_device_endpoint(
            &self,
            device_endpoint: &DeviceEndpointRef,
            asset_names: &[AssetRef],
        ) {
            let file_path = self.dir.path().join(device_endpoint.to_string());
            let mut file = fs::File::options()
                .append(true)
                .create(true)
                .open(file_path)
                .unwrap();
            for asset in asset_names {
                // Write the asset name to the file
                writeln!(&mut file, "{}", asset.name).unwrap();
            }
        }

        fn remove_device_endpoint(&self, device_endpoint: &DeviceEndpointRef) {
            let file_path = self.dir.path().join(device_endpoint.to_string());
            fs::remove_file(file_path).unwrap();
        }

        fn add_asset(&self, device_endpoint: &DeviceEndpointRef, asset: &AssetRef) {
            let file_path = self.dir.path().join(device_endpoint.to_string());

            // Make sure the asset name is not already present
            if fs::read_to_string(&file_path)
                .unwrap()
                .contains(asset.name.as_str())
            {
                return;
            }

            let mut file = fs::File::options().append(true).open(file_path).unwrap();
            // Append the asset name to the file
            writeln!(&mut file, "{}", asset.name).unwrap();
        }

        fn remove_asset(&self, device_endpoint: &DeviceEndpointRef, asset: &AssetRef) {
            let file_path = self.dir.path().join(device_endpoint.to_string());
            let content = fs::read_to_string(&file_path).unwrap();

            // Write the assets back to the file
            let mut file = fs::File::options()
                .write(true)
                .truncate(true)
                .open(file_path)
                .unwrap();

            for line in content.lines() {
                // If the line is not the asset name, write it back to the file
                if line != asset.name.as_str() {
                    writeln!(&mut file, "{line}").unwrap();
                }
            }
        }

        fn path(&self) -> &Path {
            self.dir.path()
        }
    }

    #[test]
    fn test_get_device_endpoint_names() {
        let file_mount_manager = TempFileMountManager::new("test_mount");

        let (device1_endpoint1, device1_endpoint1_assets) =
            device_with_assets!("device1", "endpoint1", "asset1");
        let (device1_endpoint2, device1_endpoint2_assets) =
            device_with_assets!("device1", "endpoint2", "asset2", "asset3");
        let (device2_endpoint3, device2_endpoint3_assets) =
            device_with_assets!("device2", "endpoint3", "asset3");

        file_mount_manager.add_device_endpoint(&device1_endpoint1, &device1_endpoint1_assets);
        file_mount_manager.add_device_endpoint(&device1_endpoint2, &device1_endpoint2_assets);
        file_mount_manager.add_device_endpoint(&device2_endpoint3, &device2_endpoint3_assets);

        temp_env::with_vars(
            [(
                ADR_RESOURCES_NAME_MOUNT_PATH,
                Some(file_mount_manager.path()),
            )],
            || {
                let mount_path = get_mount_path().unwrap();

                let device_endpoints = get_device_endpoint_names(mount_path.as_path()).unwrap();

                assert_eq!(device_endpoints.len(), 3);
                assert!(device_endpoints.contains(&device1_endpoint1));
                assert!(device_endpoints.contains(&device1_endpoint2));
                assert!(device_endpoints.contains(&device2_endpoint3));
            },
        );
    }

    #[test]
    fn test_get_asset_names() {
        let file_mount_manager = TempFileMountManager::new("test_mount");

        let (device1_endpoint1, device1_endpoint1_assets) =
            device_with_assets!("device1", "endpoint1", "asset1", "asset2", "asset3");

        file_mount_manager.add_device_endpoint(&device1_endpoint1, &device1_endpoint1_assets);

        let device1_endpoint1_assets_set: HashSet<AssetRef> =
            HashSet::from_iter(device1_endpoint1_assets);

        temp_env::with_vars(
            [(
                ADR_RESOURCES_NAME_MOUNT_PATH,
                Some(file_mount_manager.path()),
            )],
            || {
                let mount_path = get_mount_path().unwrap();

                let mounted_device1_endpoint1_assets_set =
                    get_asset_names(mount_path.as_path(), &device1_endpoint1).unwrap();

                assert_eq!(
                    mounted_device1_endpoint1_assets_set,
                    device1_endpoint1_assets_set
                );
            },
        );
    }

    #[tokio::test]
    async fn test_device_endpoint_create_observation_pre_mounted_success() {
        let file_mount_manager = TempFileMountManager::new("test_mount");

        let (device1_endpoint1, device1_endpoint1_assets) =
            device_with_assets!("device1", "endpoint1");
        let (device1_endpoint2, device1_endpoint2_assets) =
            device_with_assets!("device1", "endpoint2");
        let (device2_endpoint3, device2_endpoint3_assets) =
            device_with_assets!("device2", "endpoint3");

        file_mount_manager.add_device_endpoint(&device1_endpoint1, &device1_endpoint1_assets);
        file_mount_manager.add_device_endpoint(&device1_endpoint2, &device1_endpoint2_assets);
        file_mount_manager.add_device_endpoint(&device2_endpoint3, &device2_endpoint3_assets);

        let mut device_endpoints =
            HashSet::from([device1_endpoint1, device1_endpoint2, device2_endpoint3]);

        temp_env::async_with_vars(
            [(
                ADR_RESOURCES_NAME_MOUNT_PATH,
                Some(file_mount_manager.path()),
            )],
            async {
                let mut test_device_endpoint_create_observation =
                    DeviceEndpointCreateObservation::new(
                        DEBOUNCE_DURATION,
                    )
                    .unwrap();

                while !device_endpoints.is_empty() {
                    tokio::select! {
                        Some((device_endpoint, _)) = test_device_endpoint_create_observation.recv_notification() => {
                            assert!(device_endpoints.remove(&device_endpoint));
                        },
                        () = tokio::time::sleep(DEBOUNCE_DURATION * 2) => {
                            panic!("Failed to receive device endpoint creation notification");
                        }
                    };
                }
            },
        )
        .await;
    }

    #[tokio::test]
    async fn test_device_endpoint_create_observation_live_mount_success() {
        let file_mount_manager = TempFileMountManager::new("test_mount");

        temp_env::async_with_vars(
            [(
                ADR_RESOURCES_NAME_MOUNT_PATH,
                Some(file_mount_manager.path()),
            )],
            async {
                let mut test_device_endpoint_create_observation =
                    DeviceEndpointCreateObservation::new(
                        DEBOUNCE_DURATION,
                    )
                    .unwrap();

                let (
                    device1_endpoint1,
                    device1_endpoint1_assets,
                ) = device_with_assets!("device1", "endpoint1");
                let (
                    device1_endpoint2,
                    device1_endpoint2_assets,
                ) = device_with_assets!("device1", "endpoint2");
                let (
                    device2_endpoint3,
                    device2_endpoint3_assets,
                ) = device_with_assets!("device2", "endpoint3");

                file_mount_manager.add_device_endpoint(&device1_endpoint1, &device1_endpoint1_assets);
                file_mount_manager.add_device_endpoint(&device1_endpoint2, &device1_endpoint2_assets);
                file_mount_manager.add_device_endpoint(&device2_endpoint3, &device2_endpoint3_assets);
                let mut device_endpoints = HashSet::from([
                    device1_endpoint1,
                    device1_endpoint2,
                    device2_endpoint3,
                ]);
                while !device_endpoints.is_empty() {
                    tokio::select! {
                        Some((device_endpoint, _)) = test_device_endpoint_create_observation.recv_notification() => {
                            assert!(device_endpoints.remove(&device_endpoint));
                        },
                        () = tokio::time::sleep(DEBOUNCE_DURATION * 2) => {
                            panic!("Failed to receive device endpoint creation notification");
                        }
                    };
                }
            },
        )
        .await;
    }

    #[tokio::test]
    async fn test_asset_create_observation_pre_mounted_success() {
        let file_mount_manager = TempFileMountManager::new("test_mount");

        let (device1_endpoint1, device1_endpoint1_assets) =
            device_with_assets!("device1", "endpoint1", "asset1", "asset2");

        file_mount_manager.add_device_endpoint(&device1_endpoint1, &device1_endpoint1_assets);

        let mut assets: HashSet<AssetRef> = HashSet::from_iter(device1_endpoint1_assets);

        temp_env::async_with_vars(
            [(
                ADR_RESOURCES_NAME_MOUNT_PATH,
                Some(file_mount_manager.path()),
            )],
            async {
                let mut test_device_endpoint_create_observation =
                    DeviceEndpointCreateObservation::new(
                        DEBOUNCE_DURATION,
                    )
                    .unwrap();

                tokio::select! {
                    Some((device_endpoint, mut asset_observation)) = test_device_endpoint_create_observation.recv_notification() => {
                        assert_eq!(device_endpoint, device1_endpoint1);
                        // Observe asset creation
                        while !assets.is_empty() {
                            tokio::select! {
                                Some((asset, _)) = asset_observation.recv_notification() => {
                                    assert!(assets.remove(&asset));
                                },
                                () = tokio::time::sleep(DEBOUNCE_DURATION * 2) => {
                                    panic!("Failed to receive asset creation notification");
                                }
                            };
                        }
                    },
                    () = tokio::time::sleep(DEBOUNCE_DURATION * 2) => {
                        panic!("Failed to receive device endpoint creation notification");
                    }
                }
            },
        )
        .await;
    }

    #[tokio::test]
    async fn test_asset_create_observation_live_mount_success() {
        let file_mount_manager = TempFileMountManager::new("test_mount");

        temp_env::async_with_vars(
            [(
                ADR_RESOURCES_NAME_MOUNT_PATH,
                Some(file_mount_manager.path()),
            )],
            async {
                let mut test_device_endpoint_create_observation =
                    DeviceEndpointCreateObservation::new(
                        DEBOUNCE_DURATION,
                    )
                    .unwrap();

                let (
                    device1_endpoint1,
                    device1_endpoint1_assets,
                ) = device_with_assets!("device1", "endpoint1", "asset1", "asset2");
                file_mount_manager.add_device_endpoint(&device1_endpoint1, &device1_endpoint1_assets);
                let mut assets: HashSet<AssetRef> = HashSet::from_iter(device1_endpoint1_assets);

                tokio::select! {
                    Some((device_endpoint, mut asset_observation)) = test_device_endpoint_create_observation.recv_notification() => {
                        assert_eq!(device_endpoint, device1_endpoint1);
                        // Observe asset creation
                        while !assets.is_empty() {
                            tokio::select! {
                                Some((asset, _)) = asset_observation.recv_notification() => {
                                    assert!(assets.remove(&asset));
                                },
                                () = tokio::time::sleep(DEBOUNCE_DURATION * 2) => {
                                    panic!("Failed to receive asset creation notification");
                                }
                            };
                        }
                    },
                    () = tokio::time::sleep(DEBOUNCE_DURATION * 2) => {
                        panic!("Failed to receive device endpoint creation notification");
                    }
                }
            },
        )
        .await;
    }

    #[tokio::test]
    async fn test_device_endpoint_remove_triggers_asset_deletion_tokens_success() {
        let file_mount_manager = TempFileMountManager::new("test_mount");

        let (device1_endpoint1, device1_endpoint1_assets) =
            device_with_assets!("device1", "endpoint1", "asset1", "asset2");

        file_mount_manager.add_device_endpoint(&device1_endpoint1, &device1_endpoint1_assets);

        temp_env::async_with_vars(
            [(
                ADR_RESOURCES_NAME_MOUNT_PATH,
                Some(file_mount_manager.path()),
            )],
            async {
                let mut test_device_endpoint_create_observation =
                    DeviceEndpointCreateObservation::new(
                        DEBOUNCE_DURATION,
                    )
                    .unwrap();

                let mut asset_deletion_tokens = Vec::new();

                tokio::select! {
                    Some((device_endpoint, mut asset_observation)) = test_device_endpoint_create_observation.recv_notification() => {
                        assert_eq!(device_endpoint, device1_endpoint1);

                        // Collect asset deletion tokens
                        for _ in 0..device1_endpoint1_assets.len() {
                            tokio::select! {
                                Some((_, deletion_token)) = asset_observation.recv_notification() => {
                                    asset_deletion_tokens.push(deletion_token);
                                },
                                () = tokio::time::sleep(DEBOUNCE_DURATION * 2) => {
                                    panic!("Failed to receive asset creation notification");
                                }
                            };
                        }

                        // Remove the device endpoint
                        file_mount_manager.remove_device_endpoint(&device1_endpoint1);

                        // Wait for the device endpoint create observation to return None 
                        tokio::select! {
                            res = asset_observation.recv_notification() => {
                                assert!(res.is_none(), "Device endpoint create observation should return None after device endpoint removal");
                            },
                            () = tokio::time::sleep(DEBOUNCE_DURATION * 2) => {
                                panic!("Failed to receive device endpoint deletion notification");
                            }
                        }
                    },
                    () = tokio::time::sleep(DEBOUNCE_DURATION * 2) => {
                        panic!("Failed to receive device endpoint creation notification");
                    }
                }

                // Ensure all asset deletion tokens are triggered
                for deletion_token in asset_deletion_tokens {
                    tokio::select! {
                        () = deletion_token.cancelled() => {
                            // Token triggered successfully
                        },
                        () = tokio::time::sleep(DEBOUNCE_DURATION * 2) => {
                            panic!("Asset deletion token was not triggered");
                        }
                    }
                }
            },
        )
        .await;
    }

    #[tokio::test]
    async fn test_single_asset_removal_triggers_deletion_token_success() {
        let file_mount_manager = TempFileMountManager::new("test_mount");

        let (device1_endpoint1, device1_endpoint1_assets) =
            device_with_assets!("device1", "endpoint1", "asset1", "asset2");

        file_mount_manager.add_device_endpoint(&device1_endpoint1, &device1_endpoint1_assets);

        temp_env::async_with_vars(
            [(
                ADR_RESOURCES_NAME_MOUNT_PATH,
                Some(file_mount_manager.path()),
            )],
            async {
                let mut test_device_endpoint_create_observation =
                    DeviceEndpointCreateObservation::new(
                        DEBOUNCE_DURATION,
                    )
                    .unwrap();

                let mut asset_deletion_tokens = HashMap::new();

                tokio::select! {
                    Some((device_endpoint, mut asset_observation)) = test_device_endpoint_create_observation.recv_notification() => {
                        assert_eq!(device_endpoint, device1_endpoint1);

                        // Collect asset deletion tokens
                        for _ in 0..device1_endpoint1_assets.len() {
                            tokio::select! {
                                Some((asset, deletion_token)) = asset_observation.recv_notification() => {
                                    asset_deletion_tokens.insert(asset, deletion_token);
                                },
                                () = tokio::time::sleep(DEBOUNCE_DURATION * 2) => {
                                    panic!("Failed to receive asset creation notification");
                                }
                            };
                        }

                        // Remove a single asset
                        let asset_to_remove = &device1_endpoint1_assets[0];
                        file_mount_manager.remove_asset(&device1_endpoint1, asset_to_remove);

                        // Wait for the asset deletion token to be triggered
                        if let Some(deletion_token) = asset_deletion_tokens.remove(asset_to_remove) {
                            tokio::select! {
                                () = deletion_token.cancelled() => {
                                    // Token triggered successfully
                                },
                                () = tokio::time::sleep(DEBOUNCE_DURATION * 2) => {
                                    panic!("Asset deletion token was not triggered");
                                }
                            }
                        } else {
                            panic!("Asset deletion token not found for the removed asset");
                        }

                        // Check that the other asset's deletion token is still valid
                        let remaining_asset = &device1_endpoint1_assets[1];

                        if let Some(deletion_token) = asset_deletion_tokens.remove(remaining_asset) {
                            tokio::select! {
                                () = deletion_token.cancelled() => {
                                    panic!("Asset deletion token was triggered for the remaining asset");
                                },
                                () = tokio::time::sleep(DEBOUNCE_DURATION * 2) => {
                                    // Token not triggered, which is expected
                                }
                            }
                        } else {
                            panic!("Asset deletion token not found for the remaining asset");
                        }
                    },
                    () = tokio::time::sleep(DEBOUNCE_DURATION * 2) => {
                        panic!("Failed to receive device endpoint creation notification");
                    }
                }
            },
        )
        .await;
    }

    #[tokio::test]
    async fn test_single_asset_addition_triggers_creation_notification_success() {
        let file_mount_manager = TempFileMountManager::new("test_mount");

        let (device1_endpoint1, device1_endpoint1_assets) =
            device_with_assets!("device1", "endpoint1", "asset1");

        file_mount_manager.add_device_endpoint(&device1_endpoint1, &device1_endpoint1_assets);

        temp_env::async_with_vars(
            [(
                ADR_RESOURCES_NAME_MOUNT_PATH,
                Some(file_mount_manager.path()),
            )],
            async {
                let mut test_device_endpoint_create_observation =
                    DeviceEndpointCreateObservation::new(
                        DEBOUNCE_DURATION,
                    )
                    .unwrap();

                tokio::select! {
                    Some((device_endpoint, mut asset_observation)) = test_device_endpoint_create_observation.recv_notification() => {
                        assert_eq!(device_endpoint, device1_endpoint1);

                        // Collect initial asset creation notifications
                        for _ in 0..device1_endpoint1_assets.len() {
                            tokio::select! {
                                Some((asset, _)) = asset_observation.recv_notification() => {
                                    assert!(device1_endpoint1_assets.contains(&asset));
                                },
                                () = tokio::time::sleep(DEBOUNCE_DURATION * 2) => {
                                    panic!("Failed to receive initial asset creation notification");
                                }
                            };
                        }

                        // Add a new asset
                        let new_asset = AssetRef {
                            name: "asset2".to_string(),
                            device_name: device1_endpoint1.device_name.clone(),
                            inbound_endpoint_name: device1_endpoint1.inbound_endpoint_name.clone(),
                        };
                        file_mount_manager.add_asset(&device1_endpoint1, &new_asset);

                        // Wait for the new asset creation notification
                        tokio::select! {
                            Some((asset, _)) = asset_observation.recv_notification() => {
                                assert_eq!(asset, new_asset);
                            },
                            () = tokio::time::sleep(DEBOUNCE_DURATION * 2) => {
                                panic!("Failed to receive new asset creation notification");
                            }
                        }
                    },
                    () = tokio::time::sleep(DEBOUNCE_DURATION * 2) => {
                        panic!("Failed to receive device endpoint creation notification");
                    }
                }
            },
        )
        .await;
    }
}
