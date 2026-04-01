// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

//! Secrets interface for deployment artifacts.

use std::{
    collections::HashMap,
    path::{Path, PathBuf},
    sync::{Arc, RwLock},
};

use tokio::sync::watch;

use crate::deployment_artifacts::projected_volume_debouncer::{
    ProjectedVolumeDebouncer, ProjectedVolumeError, ProjectedVolumeEventKind,
    ProjectedVolumeEventResult,
};

/// Error for secret
#[derive(Debug, thiserror::Error)]
#[error(transparent)]
pub struct Error(#[from] InnerError);

#[derive(Debug, thiserror::Error)]
#[error(transparent)]
enum InnerError {
    ProjectedVolumeError(#[from] ProjectedVolumeError),
    IoError(#[from] std::io::Error), // TODO: still necessary?
    #[error("Invalid")]
    Invalid,
}

/// Holds the file watchers (debouncers) that must remain alive as long as any
/// `Secrets` or `Secret` handle exists.
struct FileWatchers {
    _metadata_debouncer: ProjectedVolumeDebouncer,
    _data_debouncer: ProjectedVolumeDebouncer,
}

/// Manager for Secrets deployed in the connector application
/// Use this to access individual secrets.
#[derive(Clone)]
pub struct Secrets {
    file_watchers: Arc<FileWatchers>,
    secret_tracker: Arc<RwLock<SecretTracker>>,
}

// If we only map alias to sender, then how can we report secret updates? Without storing context, no way to know
// which Secret to transmit to.

impl Secrets {
    /// - metadata_path: path the Secret Metadata mount is located at
    /// - data_path: path the Secret Data mount is located at
    /// Fails if paths are invalid
    pub(crate) fn new(metadata_path: PathBuf, data_path: PathBuf) -> Result<Self, Error> {
        Self::new_inner(metadata_path, data_path).map_err(Into::into)
    }

    fn new_inner(metadata_path: PathBuf, data_path: PathBuf) -> Result<Self, InnerError> {
        let secret_tracker = Arc::new(RwLock::new(SecretTracker::new(
            metadata_path.clone(),
            data_path.clone(),
        )?));
        let secret_tracker_c1 = secret_tracker.clone();
        let secret_tracker_c2 = secret_tracker.clone();
        let data_path_c1 = data_path.clone();

        // Set up the Secret Metadata mount debouncer.
        let metadata_debouncer = ProjectedVolumeDebouncer::new(
            metadata_path,
            move |res: ProjectedVolumeEventResult| {
                match res {
                    Ok(events) => {
                        for event in &events {
                            match event.kind {
                                // Handle updates to existing aliases
                                // (i.e. secret alias now points to a different secret)
                                ProjectedVolumeEventKind::FileModified => {
                                    log::trace!("Secret metadata change detected: {:?}", event);
                                    // Alias is the filename.
                                    let Some(file_name) = event.path.file_name() else {
                                        // NOTE: This should not happen, violation of expected file mount structure.
                                        log::error!(
                                            "Failed to get file name from path: {:?}",
                                            event.path
                                        );
                                        continue;
                                    };
                                    let Some(alias) = file_name.to_str() else {
                                        // NOTE: This should not happen, violation of expected file mount structure.
                                        log::error!(
                                            "Failed to convert file name to str: {:?}",
                                            file_name
                                        );
                                        continue;
                                    };

                                    // Read the alias file to get the new secret path.
                                    // Do this before notifying about updates.
                                    let secret_pathbuf =
                                        match std::fs::read_to_string(event.path.as_path()) {
                                            Ok(file_content) => data_path_c1.join(file_content),
                                            Err(e) => {
                                                // NOTE: This should not happen, as alias files should not be added or removed dynamically.
                                                log::error!(
                                                    "Failed to read secret alias file {:?}: {e:?}",
                                                    event.path
                                                );
                                                continue;
                                            }
                                        };

                                    // Update the secret tracker for the new secret path
                                    if let Err(_) = secret_tracker_c1
                                        .write()
                                        .unwrap()
                                        .update_secret_path(alias, secret_pathbuf)
                                    {
                                        // NOTE: This should not happen, violation of expected file mount structure
                                        log::error!(
                                            "Attempted to update untracked secret alias: {alias}"
                                        );
                                        continue;
                                    }
                                }
                                // Alias files are not supposed to be able to be added or removed dynamically,
                                // nor do we expect any directories here.
                                _ => {
                                    log::error!("Unexpected event for Secret metadata: {event:?}");
                                }
                            }
                        }
                    }
                    Err(e) => {
                        log::error!("Error processing Secret metadata event: {e:?}");
                    }
                }
            },
        )?;

        // Set up the Secret Data mount debouncer.
        let data_debouncer =
            ProjectedVolumeDebouncer::new(data_path, move |res: ProjectedVolumeEventResult| {
                match res {
                    Ok(events) => {
                        for event in &events {
                            match event.kind {
                                // Handle updates to existing secret data.
                                ProjectedVolumeEventKind::FileModified => {
                                    log::trace!("Secret data change detected: {:?}", event);
                                    secret_tracker_c2
                                        .read()
                                        .unwrap()
                                        .report_secret_change(&event.path);
                                }
                                // Secret files can be created, but we don't need to do anything
                                // with them until an alias points at them, so log and report.
                                ProjectedVolumeEventKind::FileCreated => {
                                    log::trace!("Secret data creation detected: {:?}", event);
                                    secret_tracker_c2
                                        .read()
                                        .unwrap()
                                        .report_secret_change(&event.path);
                                }
                                // Secret files can be deleted, but there's no need for anything
                                // to be done in response, since the Secret interface will handle
                                // the file no longer existing. Log only.
                                ProjectedVolumeEventKind::FileRemoved => {
                                    log::trace!("Secret data removal detected: {:?}", event);
                                }
                                // Directory events can be ignored
                                _ => {}
                            }
                        }
                    }
                    Err(e) => {
                        log::error!("Error processing Secret data event: {e:?}");
                    }
                }
            })?;

        Ok(Self {
            file_watchers: Arc::new(FileWatchers {
                _metadata_debouncer: metadata_debouncer,
                _data_debouncer: data_debouncer,
            }),
            secret_tracker,
        })
    }

    /// Get a Secret corresponding to the given secret alias, if it exists.
    pub fn get_secret(&self, alias: &str) -> Option<Secret> {
        self.secret_tracker
            .read()
            .unwrap()
            .get_entry_by_alias(alias)
            .map(|entry| Secret {
                alias: alias.to_string(),
                path: entry.path.clone(),
                update_rx: entry.sender.subscribe(),
                _file_watchers: self.file_watchers.clone(),
            })
    }
}

/// Provides access to a deployed secret.
/// Can provide value and be monitored for changes.
/// Note that the secret value may not always be available in cases where the secret alias is being
/// remapped to new secret content, but the new secret content has not yet been deployed.
#[derive(Clone)] // TODO: do we need a custom clone implementation, or should the pending updates carry over?
pub struct Secret {
    alias: String,
    path: Arc<RwLock<PathBuf>>,
    update_rx: watch::Receiver<()>,
    _file_watchers: Arc<FileWatchers>,
}

impl Secret {
    // CONSIDER: perhaps add path as well?

    /// Returns the alias of the secret
    pub fn alias(&self) -> &str {
        &self.alias
    }

    /// Wait for a notification that the secret has been updated, or return immediately if there is
    /// already a pending update that has not been seen.
    pub async fn changed(&mut self) {
        loop {
            // Wait for an update
            self.update_rx
                .changed()
                .await
                .expect("Secret update channel closed unexpectedly"); // TODO: can this happen?
            // After being notified of an update, make sure the updated secret exists,
            // or keep waiting for additional updates.
            if self.path.read().unwrap().exists() {
                break;
            } else {
                continue;
            };
        }
    }

    /// Indicates if the secret is currently available for retrieval.
    pub fn is_available(&self) -> bool {
        self.path.read().unwrap().exists()
    }

    /// Attempt to read the value of the secret if it is currently available.
    /// Returns Ok(Some(value)) if the secret is available
    /// Returns Ok(None) if the secret is not currently available
    /// Returns Err if an error occurs while trying to read the secret.
    pub fn value_if_available(&mut self) -> Result<Option<String>, Error> {
        let path = self.path.read().unwrap();
        if path.exists() {
            // Mark the secret as unchanged since we are reading its current value
            self.update_rx.mark_unchanged();
            eprintln!("Reading secret value from path: {:?}", *path); // TODO: remove
            Ok(Some(
                std::fs::read_to_string(&*path).map_err(InnerError::from)?,
            ))
        } else {
            Ok(None)
        }
    }

    /// Return the value of the secret now if it is avaialble, or waits for it if it is not yet
    /// available.
    /// Returns Err if an error occurs while trying to read the secret.
    pub async fn value(&mut self) -> Result<String, Error> {
        loop {
            match self.value_if_available() {
                Ok(Some(value)) => return Ok(value),
                Ok(None) => {
                    // Wait for the next update notification and then try again
                    self.changed().await;
                }
                Err(e) => return Err(e),
            }
        }
    }
}

/// Represents a tracked secret with a notification mechanism for it.
/// This corresponds 1:1 with an alias.
#[derive(Debug)]
struct SecretTrackerEntry {
    path: Arc<RwLock<PathBuf>>,
    sender: watch::Sender<()>,
}

struct SecretTracker {
    by_alias: HashMap<String, Arc<SecretTrackerEntry>>,
    /// Mapping of secret data paths to the SecretTrackerEntry(s) that point at them.
    /// This is a one to manyh relationship because multiple aliases can point at the same secret data.
    /// Note that secrets are only tracked in here if an alias points at them, there may be secret data
    /// files that exist on disk, but are not tracked here.
    by_path: HashMap<PathBuf, Vec<Arc<SecretTrackerEntry>>>,
}

impl SecretTracker {
    fn new(metadata_path: PathBuf, data_path: PathBuf) -> Result<Self, InnerError> {
        let mut by_alias = HashMap::new();
        let mut by_path = HashMap::new();

        // Initialize all secret aliases / paths
        for entry in std::fs::read_dir(&metadata_path)? {
            let entry = entry?;

            // TOOD: remove
            eprintln!(
                "SETUP: Processing secret metadata entry: {:?}",
                entry.path()
            );
            eprintln!(
                "SETUP: Is file? {:?}, Is dir? {:?}, Is Symlink: {:?}",
                entry.path().is_file(),
                entry.path().is_dir(),
                entry.path().is_symlink()
            );

            // NOTE: Must use entry.path().is_file() instead of entry.file_type()?.is_file()
            // In Kubernetes projected volumes, all files are also symlinks, and entry.file_type()
            // only returns a mutually-exclusive single type, which is always symlink.
            if entry.path().is_file() {
                let secret_alias = entry
                    .file_name()
                    .into_string()
                    .map_err(|_| InnerError::Invalid)?;
                let secret_file = data_path.join(std::fs::read_to_string(entry.path())?);
                let entry = Arc::new(SecretTrackerEntry {
                    path: Arc::new(RwLock::new(secret_file.clone())),
                    sender: watch::channel(()).0,
                });
                by_alias.insert(secret_alias, entry.clone());
                by_path
                    .entry(secret_file)
                    .or_insert_with(Vec::new)
                    .push(entry);
            }
        }

        // NOTE: There may be secret data files currently unused if no alias points at them.
        // This is not supposed to happen, but it's not an issue if it does.

        Ok(Self { by_alias, by_path })
    }

    /// Return an Arc reference to the SecretTrackerEntry corresponding to the given alias, if it exists.
    fn get_entry_by_alias(&self, alias: &str) -> Option<Arc<SecretTrackerEntry>> {
        self.by_alias.get(alias).cloned()
    }

    /// Update the path of the secret corresponding to the given alias, and notify of the update
    fn update_secret_path(&mut self, alias: &str, new_path: PathBuf) -> Result<(), InnerError> {
        // If path exists in the tracker, point alias entry at the entry that matches the path
        // If path does not exist,

        if let Some(entry) = self.by_alias.get(alias) {
            // Get a write lock on the entry's path to ensure there's no timing issues.
            let mut entry_path_wg = entry.path.write().unwrap();

            // First, detach the alias from the old path in the by_path mapping (if it exists).
            // This is how we prevent staleness in the map.
            if let Some(entries) = self.by_path.get_mut(&*entry_path_wg) {
                entries.retain(|e| !Arc::ptr_eq(e, entry));
                // If there are no more entries pointing at this path, remove the path from the mapping
                if entries.is_empty() {
                    self.by_path.remove(&*entry_path_wg);
                }
            }

            // Next, update the path in the SecretTrackerEntry that corresponds to the alias
            log::debug!("Updating secret path for alias {alias} to {new_path:?}");
            *entry_path_wg = new_path.clone();

            // Finally, add an entry in the by_path map for the new path.
            self.by_path
                .entry(new_path)
                .or_insert_with(Vec::new)
                .push(entry.clone());

            // Notify the Secret of the update
            // We do not care about send errors here - they just mean nobody is currently
            // monitoring the secret, which is fine.
            let _ = entry.sender.send(());
            Ok(())
        } else {
            // Alias was invalid
            Err(InnerError::Invalid)
        }
    }

    fn report_secret_change(&self, path: &Path) {
        if let Some(entries) = self.by_path.get(path) {
            log::debug!(
                "Reporting secret change for path {path:?} to {} secret(s)",
                entries.len()
            );
            // Notify all corresponding secrets of the change
            // We do not care about send errors here - they just mean nobody is currently
            // monitoring the secret, which is fine.
            for entry in entries {
                let _ = entry.sender.send(());
            }
        } else {
            log::debug!("Secret changed with no affiliated aliases for path {path:?}");
        }
    }

    // TODO: what about cleanup/deletion?

    // TODO: clones, drops, cancel safety.
}

#[cfg(test)]
mod tests {
    use super::{Secret, Secrets};
    use crate::deployment_artifacts::test_utils::TempProjectedVolume;
    use futures_util::FutureExt;
    use std::cell::RefCell;
    use std::collections::{HashMap, HashSet};
    use std::sync::{Arc, LazyLock};
    use std::{path::Path, time::Duration};
    use test_case::test_case;

    // NOTE: Many tests use manual sleeps for testing timing of async notifications.
    // The underlying debouncer used for Projected Volumes uses a 1 second debounce window,
    // so we need to wait at least that long + some buffer for notifications to be issued.
    // Use 1.5x debounce window to be sure, as that's a very generous buffer.
    const UPDATE_WINDOW: Duration = Duration::from_millis(1500);

    // NOTE: We need to have two types of mount managers to handle the variant cases of
    // Secret Sync vs. non-Secret Sync scenarios. The `Secrets` and `Secret` structs are designed
    // to abstract this distinction from the end user, but it has ramifications on the filesystem
    // structure, so we need to test against both to validate that both scenarios are handled.
    // The only real impact is around recursive vs. non-recursive watching, but it's safer to just
    // test everything against both.

    trait SecretMountManager: Clone {
        fn stage_secret_alias_create(&self, secret_alias: &str, secret_ref: &str, secret_key: &str);

        fn stage_secret_alias_modify(
            &self,
            secret_alias: &str,
            new_secret_ref: &str,
            new_secret_key: &str,
        );

        fn stage_secret_data_create(&self, secret_ref: &str, secret_key: &str, secret_data: &str);

        fn stage_secret_data_modify(
            &self,
            secret_ref: &str,
            secret_key: &str,
            new_secret_data: &str,
        );

        fn stage_secret_data_remove(&self, secret_ref: &str, secret_key: &str);

        fn execute_update_alias(&self);

        fn execute_update_data(&self);

        fn execute_update_all(&self) {
            self.execute_update_alias();
            self.execute_update_data();
        }

        fn metadata_path(&self) -> &Path;

        fn data_path(&self) -> &Path;
    }

    #[derive(Clone)]
    struct StandardSecretMountManager {
        metadata_mount: Arc<TempProjectedVolume>,
        data_mount: Arc<TempProjectedVolume>,
        /// Tracks which secret_ref directories have been created in the data mount.
        data_dirs: Arc<RefCell<HashSet<String>>>,
    }

    impl StandardSecretMountManager {
        fn new() -> Self {
            Self {
                metadata_mount: Arc::new(TempProjectedVolume::new("metadata")),
                data_mount: Arc::new(TempProjectedVolume::new("data")),
                data_dirs: Arc::new(RefCell::new(HashSet::new())),
            }
        }
    }

    impl SecretMountManager for StandardSecretMountManager {
        fn stage_secret_alias_create(
            &self,
            secret_alias: &str,
            secret_ref: &str,
            secret_key: &str,
        ) {
            self.metadata_mount.stage_file_create(
                Path::new(secret_alias),
                &format!("{secret_ref}/{secret_key}"),
            );
        }

        fn stage_secret_alias_modify(
            &self,
            secret_alias: &str,
            new_secret_ref: &str,
            new_secret_key: &str,
        ) {
            self.metadata_mount.stage_file_modify(
                Path::new(secret_alias),
                &format!("{new_secret_ref}/{new_secret_key}"),
            );
        }

        fn stage_secret_data_create(&self, secret_ref: &str, secret_key: &str, secret_data: &str) {
            if self.data_dirs.borrow_mut().insert(secret_ref.to_string()) {
                self.data_mount.stage_dir_create(Path::new(secret_ref));
            }
            self.data_mount
                .stage_file_create(&Path::new(secret_ref).join(secret_key), secret_data);
        }

        fn stage_secret_data_modify(
            &self,
            secret_ref: &str,
            secret_key: &str,
            new_secret_data: &str,
        ) {
            self.data_mount
                .stage_file_modify(&Path::new(secret_ref).join(secret_key), new_secret_data);
        }

        fn stage_secret_data_remove(&self, secret_ref: &str, secret_key: &str) {
            self.data_mount
                .stage_file_remove(&Path::new(secret_ref).join(secret_key));
        }

        fn execute_update_alias(&self) {
            self.metadata_mount.execute_update();
        }

        fn execute_update_data(&self) {
            self.data_mount.execute_update();
        }

        fn metadata_path(&self) -> &Path {
            self.metadata_mount.path()
        }

        fn data_path(&self) -> &Path {
            self.data_mount.path()
        }
    }

    #[derive(Clone)]
    struct SecretSyncMountManager {
        metadata_mount: Arc<TempProjectedVolume>,
        data_mount: Arc<TempProjectedVolume>,
    }

    impl SecretSyncMountManager {
        fn new() -> Self {
            Self {
                metadata_mount: Arc::new(TempProjectedVolume::new("metadata")),
                data_mount: Arc::new(TempProjectedVolume::new("data")),
            }
        }
    }

    impl SecretMountManager for SecretSyncMountManager {
        fn stage_secret_alias_create(
            &self,
            secret_alias: &str,
            secret_ref: &str,
            secret_key: &str,
        ) {
            self.metadata_mount.stage_file_create(
                Path::new(secret_alias),
                &format!("{secret_ref}_{secret_key}"),
            );
        }

        fn stage_secret_alias_modify(
            &self,
            secret_alias: &str,
            new_secret_ref: &str,
            new_secret_key: &str,
        ) {
            self.metadata_mount.stage_file_modify(
                Path::new(secret_alias),
                &format!("{new_secret_ref}_{new_secret_key}"),
            );
        }

        fn stage_secret_data_create(&self, secret_ref: &str, secret_key: &str, secret_data: &str) {
            self.data_mount.stage_file_create(
                Path::new(&format!("{secret_ref}_{secret_key}")),
                secret_data,
            );
        }

        fn stage_secret_data_modify(
            &self,
            secret_ref: &str,
            secret_key: &str,
            new_secret_data: &str,
        ) {
            self.data_mount.stage_file_modify(
                Path::new(&format!("{secret_ref}_{secret_key}")),
                new_secret_data,
            );
        }

        fn stage_secret_data_remove(&self, secret_ref: &str, secret_key: &str) {
            self.data_mount
                .stage_file_remove(Path::new(&format!("{secret_ref}_{secret_key}")));
        }

        fn execute_update_alias(&self) {
            self.metadata_mount.execute_update();
        }

        fn execute_update_data(&self) {
            self.data_mount.execute_update();
        }

        fn metadata_path(&self) -> &Path {
            self.metadata_mount.path()
        }

        fn data_path(&self) -> &Path {
            self.data_mount.path()
        }
    }

    const ALIAS_1: &str = "alias1";
    const ALIAS_2: &str = "alias2";
    const ALIAS_3: &str = "alias3";

    const REF_1: &str = "ref1";
    const REF_2: &str = "ref2";
    const REF_3: &str = "ref3";

    const KEY_1: &str = "key1";
    const KEY_2: &str = "key2";
    const KEY_3: &str = "key3";

    const DATA_1: &str = "data1";
    const DATA_2: &str = "data2";
    const DATA_3: &str = "data3";

    const NEW_REF: &str = "new_ref";
    const NEW_KEY: &str = "new_key";
    const NEW_DATA: &str = "new_data";

    macro_rules! secret_test {
        // Async with #[test_case] attributes
        ($(#[$attr:meta])+ async $name:ident, |$mm:ident $(, $param:ident: $ty:ty)*| $body:block) => {
            $(#[$attr])*
            #[tokio::test]
            async fn $name($($param: $ty),*) {
                // let _ = env_logger::Builder::new()
                //     .filter_level(log::LevelFilter::Trace)
                //     .filter_module("notify::inotify", log::LevelFilter::Off)
                //     .is_test(true)
                //     .try_init();
                {
                    let $mm = StandardSecretMountManager::new();
                    $body
                }
                {
                    let $mm = SecretSyncMountManager::new();
                    $body
                }
            }
        };
        // Async without attributes
        (async $name:ident, |$mm:ident| $body:block) => {
            #[tokio::test]
            async fn $name() {
                // let _ = env_logger::Builder::new()
                //     .filter_level(log::LevelFilter::Trace)
                //     .filter_module("notify::inotify", log::LevelFilter::Off)
                //     .is_test(true)
                //     .try_init();
                {
                    let $mm = StandardSecretMountManager::new();
                    $body
                }
                {
                    let $mm = SecretSyncMountManager::new();
                    $body
                }
            }
        };
        // Sync with #[test_case] attributes
        ($(#[$attr:meta])+ $name:ident, |$mm:ident $(, $param:ident: $ty:ty)*| $body:block) => {
            $(#[$attr])*
            fn $name($($param: $ty),*) {
                // let _ = env_logger::Builder::new()
                //     .filter_level(log::LevelFilter::Trace)
                //     .filter_module("notify::inotify", log::LevelFilter::Off)
                //     .is_test(true)
                //     .try_init();
                {
                    let $mm = StandardSecretMountManager::new();
                    $body
                }
                {
                    let $mm = SecretSyncMountManager::new();
                    $body
                }
            }
        };
        // Sync without attributes
        ($name:ident, |$mm:ident| $body:block) => {
            #[test]
            fn $name() {
                // let _ = env_logger::Builder::new()
                //     .filter_level(log::LevelFilter::Trace)
                //     .filter_module("notify::inotify", log::LevelFilter::Off)
                //     .is_test(true)
                //     .try_init();
                {
                    let $mm = StandardSecretMountManager::new();
                    $body
                }
                {
                    let $mm = SecretSyncMountManager::new();
                    $body
                }
            }
        };
    }

    type SecretRefKey = (&'static str, &'static str);

    /// Describes the initial filesystem scenario for a test case, providing helper functions
    /// for a test to use that scenario.
    struct SecretTestCase {
        /// (REF, KEY) -> DATA
        initial_data: HashMap<SecretRefKey, &'static str>,
        /// ALIAS -> (REF, KEY)
        initial_alias_map: HashMap<&'static str, SecretRefKey>,
    }

    impl SecretTestCase {
        fn initialize_mount_manager(&self, mount_manager: &impl SecretMountManager) {
            for ((secret_ref, secret_key), data) in &self.initial_data {
                mount_manager.stage_secret_data_create(secret_ref, secret_key, data);
            }
            for (alias, (secret_ref, secret_key)) in &self.initial_alias_map {
                mount_manager.stage_secret_alias_create(alias, secret_ref, secret_key);
            }
            mount_manager.execute_update_all();
        }

        fn initial_aliases(&self) -> Vec<&'static str> {
            self.initial_alias_map.keys().cloned().collect()
        }

        fn initial_data_for_alias(&self, alias: &str) -> &'static str {
            let (secret_ref, secret_key) = self
                .initial_alias_map
                .get(alias)
                .unwrap_or_else(|| panic!("alias {alias:?} not found in initial_alias_map"));
            self.initial_data.get(&(*secret_ref, *secret_key))
                .unwrap_or_else(|| panic!("no initial data for ({secret_ref:?}, {secret_key:?}) referenced by alias {ALIAS_1:?}"))
        }

        fn initial_data_for_ref_key(&self, secret_ref: &str, secret_key: &str) -> &'static str {
            self.initial_data.get(&(secret_ref, secret_key))
                .unwrap_or_else(|| panic!("no initial data for ({secret_ref:?}, {secret_key:?})"))
        }
    }

    /// Test case with two aliases that have completely unique secret refs and keys.
    static TEST_CASE_UNIQUE_REFS_AND_KEYS: LazyLock<SecretTestCase> =
        LazyLock::new(|| SecretTestCase {
            initial_data: HashMap::from([((REF_1, KEY_1), DATA_1), ((REF_2, KEY_2), DATA_2)]),
            initial_alias_map: HashMap::from([
                (ALIAS_1, (REF_1, KEY_1)),
                (ALIAS_2, (REF_2, KEY_2)),
            ]),
        });

    /// Test case where two aliases (ALIAS_1 and ALIAS_2) share the same secret ref but have
    /// different secret keys (and therefore, different data).
    static TEST_CASE_SHARED_REF: LazyLock<SecretTestCase> = LazyLock::new(|| SecretTestCase {
        initial_data: HashMap::from([
            ((REF_1, KEY_1), DATA_1),
            ((REF_1, KEY_2), DATA_2),
            ((REF_2, KEY_3), DATA_3),
        ]),
        initial_alias_map: HashMap::from([
            (ALIAS_1, (REF_1, KEY_1)),
            (ALIAS_2, (REF_1, KEY_2)),
            (ALIAS_3, (REF_2, KEY_3)),
        ]),
    });

    /// Test case where two aliases (ALIAS_1 and ALIAS_2) share the same secret key but have
    /// different secret refs (and therefore, different data).
    static TEST_CASE_SHARED_KEY: LazyLock<SecretTestCase> = LazyLock::new(|| SecretTestCase {
        initial_data: HashMap::from([
            ((REF_1, KEY_1), DATA_1),
            ((REF_2, KEY_1), DATA_2),
            ((REF_3, KEY_2), DATA_3),
        ]),
        initial_alias_map: HashMap::from([
            (ALIAS_1, (REF_1, KEY_1)),
            (ALIAS_2, (REF_2, KEY_1)),
            (ALIAS_3, (REF_3, KEY_2)),
        ]),
    });

    /// Test case where two aliases (ALIAS_1 and ALIAS_2) share the same secret key and secret ref
    /// and theremore share the same secret data.
    static TEST_CASE_SHARED_REF_AND_KEY: LazyLock<SecretTestCase> =
        LazyLock::new(|| SecretTestCase {
            initial_data: HashMap::from([((REF_1, KEY_1), DATA_1), ((REF_2, KEY_2), DATA_2)]),
            initial_alias_map: HashMap::from([
                (ALIAS_1, (REF_1, KEY_1)),
                (ALIAS_2, (REF_1, KEY_1)),
                (ALIAS_3, (REF_2, KEY_2)),
            ]),
            //target_aliases: vec![ALIAS_1, ALIAS_2],
        });


    #[track_caller]
    fn assert_secret_has_initial_data_now(secret: &mut Secret, test_case: &SecretTestCase) {
        assert!(secret.is_available());
        assert_eq!(
            secret
                .value_if_available()
                .expect("Couldn't access secret")
                .expect("Secret not available"),
            test_case.initial_data_for_alias(secret.alias())
        );
        assert_eq!(
            secret
                .value()
                .now_or_never()
                .expect("Secret not available")
                .expect("Couldn't access secret"),
            test_case.initial_data_for_alias(secret.alias())
        );
    }

    #[track_caller]
    fn assert_secrets_have_initial_data_now(
        secret_array: &mut [Secret],
        test_case: &SecretTestCase,
    ) {
        for secret in secret_array {
            assert_secret_has_initial_data_now(secret, test_case);
        }
    }

    #[track_caller]
    fn assert_secret_has_expected_data_now(secret: &mut Secret, expected_data: &str) {
        assert!(secret.is_available());
        assert_eq!(
            secret
                .value_if_available()
                .expect("Couldn't access secret")
                .expect("Secret not available"),
            expected_data
        );
        assert_eq!(
            secret
                .value()
                .now_or_never()
                .expect("Secret not available")
                .expect("Couldn't access secret"),
            expected_data
        );
    }

    #[track_caller]
    fn assert_secrets_have_expected_data_now(secret_array: &mut [Secret], expected_data: &str) {
        for secret in secret_array {
            assert_secret_has_expected_data_now(secret, expected_data);
        }
    }

    #[track_caller]
    fn assert_secret_unavailable_now(secret: &mut Secret) {
        assert!(!secret.is_available());
        assert!(secret.value_if_available().expect("Couldn't access secret").is_none());
        assert!(secret.value().now_or_never().is_none());
    }

    #[track_caller]
    fn assert_secrets_unavailable_now(secret_array: &mut [Secret]) {
        for secret in secret_array {
            assert_secret_unavailable_now(secret);
        }
    }

    async fn all_secrets_get_changed_notification(secrets: Vec<Secret>) {
        let mut set = tokio::task::JoinSet::new();
        for mut secret in secrets {
            set.spawn(async move { secret.changed().await });
        }
        while set.join_next().await.is_some() {}
    }

    secret_test!(secret_reports_alias, |mount_manager| {
        // Initialize an alias on disk
        mount_manager.stage_secret_alias_create(ALIAS_1, REF_1, KEY_1);
        mount_manager.stage_secret_data_create(REF_1, KEY_1, DATA_1);
        mount_manager.execute_update_all();

        let secrets = Secrets::new(
            mount_manager.metadata_path().to_path_buf(),
            mount_manager.data_path().to_path_buf(),
        )
        .unwrap();
        let secret = secrets.get_secret(ALIAS_1).unwrap();
        assert_eq!(secret.alias(), ALIAS_1)
    });

    // Tests that a secret can have its value modified in place when the underlying secret data file is modified,
    // and that the change will be reported and propagated only to the affected secret.
    secret_test!(
        #[test_case(&*TEST_CASE_UNIQUE_REFS_AND_KEYS, REF_1, KEY_1, vec![ALIAS_1]; "No ref or key overlap")]
        #[test_case(&*TEST_CASE_SHARED_REF, REF_1, KEY_1, vec![ALIAS_1]; "Alias has shared ref, unique key")]
        #[test_case(&*TEST_CASE_SHARED_KEY, REF_1, KEY_1, vec![ALIAS_1]; "Alias has shared key, unique ref")]
        #[test_case(&*TEST_CASE_SHARED_REF_AND_KEY, REF_1, KEY_1, vec![ALIAS_1, ALIAS_2]; "Aliases share ref and key")]
        async modify_secret_data_in_place,
        |mount_manager, test_case: &SecretTestCase, target_ref: &str, target_key: &str, affected_aliases: Vec<&str>| {

            // SETUP --------------------------------------------------------------------

            test_case.initialize_mount_manager(&mount_manager);

            // Create the Secrets struct and get the individual Secret structs.
            let secrets = Secrets::new(
                mount_manager.metadata_path().to_path_buf(),
                mount_manager.data_path().to_path_buf(),
            )
            .expect("Failed to create Secrets struct");

            // Get the secrets
            let mut affected_secrets = affected_aliases
                .iter()
                .map(|alias| secrets.get_secret(alias).expect("Failed to get secret"))
                .collect::<Vec<_>>();
            let mut bystander_secrets = test_case.initial_aliases()
                .into_iter()
                .filter(|alias| !affected_aliases.contains(alias))
                .map(|alias| secrets.get_secret(alias).expect("Failed to get secret"))
                .collect::<Vec<_>>();

            // All secrets are available immediately and have the exepected initial data
            assert_secrets_have_initial_data_now(&mut affected_secrets, test_case);
            assert_secrets_have_initial_data_now(&mut bystander_secrets, test_case);

            // END SETUP ----------------------------------------------------------------

            // Start listening for change notifications
            let affected_change_notifications = tokio::task::spawn(all_secrets_get_changed_notification(affected_secrets.clone()));
            let bystander_change_notifications = tokio::task::spawn(all_secrets_get_changed_notification(bystander_secrets.clone()));

            // Update secret data
            mount_manager.stage_secret_data_modify(target_ref, target_key, NEW_DATA);
            mount_manager.execute_update_data();

            // Changes are immediately reflected for the target secret, while the bystander secrets
            // stay at the initial values.
            assert_secrets_have_expected_data_now(&mut affected_secrets, NEW_DATA);
            assert_secrets_have_initial_data_now(&mut bystander_secrets, test_case);

            // Change notifications are not immediately issued
            assert!(!affected_change_notifications.is_finished());
            assert!(!bystander_change_notifications.is_finished());

            // Wait for changes to be reported for affected secrets
            tokio::time::timeout(UPDATE_WINDOW, affected_change_notifications)
                .await
                .expect("Timed out waiting for affected secret change notification")
                .expect("Affected secrets change notification task panicked");

            // Change notifications were not issued for bystanders
            assert!(!bystander_change_notifications.is_finished());
        }
    );

    // Tests that a secret can have its value become unavailable when the underlying secret data file is removed
    // and that the change will be propagated only to the affected secret. No notification will be issued.
    // When the data file is recreated with the same value, the value will return to being available with the same data,
    // and again, it will only be propagated (and reported) to the affected secret.
    secret_test!(
        #[test_case(&*TEST_CASE_UNIQUE_REFS_AND_KEYS, REF_1, KEY_1, vec![ALIAS_1]; "No ref or key overlap")]
        #[test_case(&*TEST_CASE_SHARED_REF, REF_1, KEY_1, vec![ALIAS_1]; "Alias has shared ref, unique key")]
        #[test_case(&*TEST_CASE_SHARED_KEY, REF_1, KEY_1, vec![ALIAS_1]; "Alias has shared key, unique ref")]
        #[test_case(&*TEST_CASE_SHARED_REF_AND_KEY, REF_1, KEY_1, vec![ALIAS_1, ALIAS_2]; "Aliases share ref and key")]
        async delete_and_recreate_secret_data_in_place,
        |mount_manager, test_case: &SecretTestCase, target_ref: &str, target_key: &str, affected_aliases: Vec<&str>| {

            // SETUP --------------------------------------------------------------------

            test_case.initialize_mount_manager(&mount_manager);

            // Create the Secrets struct and get the individual Secret structs.
            let secrets = Secrets::new(
                mount_manager.metadata_path().to_path_buf(),
                mount_manager.data_path().to_path_buf(),
            )
            .expect("Failed to create Secrets struct");

            // Get the secrets
            let mut affected_secrets = affected_aliases
                .iter()
                .map(|alias| secrets.get_secret(alias).expect("Failed to get secret"))
                .collect::<Vec<_>>();
            let mut bystander_secrets = test_case.initial_aliases()
                .into_iter()
                .filter(|alias| !affected_aliases.contains(alias))
                .map(|alias| secrets.get_secret(alias).expect("Failed to get secret"))
                .collect::<Vec<_>>();

            // All secrets are available immediately and have the exepected initial data
            assert_secrets_have_initial_data_now(&mut affected_secrets, test_case);
            assert_secrets_have_initial_data_now(&mut bystander_secrets, test_case);

            // Cache the initial target secret data for later
            let initial_target_data = test_case.initial_data_for_ref_key(target_ref, target_key);

            // END SETUP ----------------------------------------------------------------

            // Start listening for change notifications
            let affected_change_notifications = tokio::task::spawn(all_secrets_get_changed_notification(affected_secrets.clone()));
            let bystander_change_notifications = tokio::task::spawn(all_secrets_get_changed_notification(bystander_secrets.clone()));

            // Remove secret data
            mount_manager.stage_secret_data_remove(target_ref, target_key);
            mount_manager.execute_update_data();

            // The target secret immediately becomes unavailable, but the bystander secrets remain
            // available and at their initial values.
            assert_secrets_unavailable_now(&mut affected_secrets);  // TODO: this is kind of surprising
            assert_secrets_have_initial_data_now(&mut bystander_secrets, test_case);

            // Change notifications are not immediately issued
            assert!(!affected_change_notifications.is_finished());
            assert!(!bystander_change_notifications.is_finished());

            // Change notifications are still not issued after appropriate update time.
            tokio::time::sleep(UPDATE_WINDOW).await;
            assert!(!affected_change_notifications.is_finished());
            assert!(!bystander_change_notifications.is_finished());

            // Create secret data on the same REF and KEY, but with new data
            mount_manager.stage_secret_data_create(target_ref, target_key, initial_target_data);
            mount_manager.execute_update_data();

            // The target secret immediately becomes available with the new data, while the bystander
            // secrets remain available at their initial values
            assert_secrets_have_expected_data_now(&mut affected_secrets, initial_target_data);
            assert_secrets_have_initial_data_now(&mut bystander_secrets, test_case);

            // Change notifications are still not immediately issued
            assert!(!affected_change_notifications.is_finished());
            assert!(!bystander_change_notifications.is_finished());

            // Wait for changes to be reported for affected secrets
            tokio::time::timeout(UPDATE_WINDOW, affected_change_notifications)
                .await
                .expect("Timed out waiting for affected secret change notification")
                .expect("Affected secrets change notification task panicked");

            // Change notifications were not issued for bystanders
            assert!(!bystander_change_notifications.is_finished());
        }
    );

    // Tests that a secret can have its value become unavailable when the underlying secret data file is removed
    // and that the change will be propagated only to the affected secret. No notification will be issued.
    // When the data file is recreated with a new value, the value will return to being available with the new data,
    // and again, it will only be propagated (and reported) to the affected secret.
    secret_test!(
        #[test_case(&*TEST_CASE_UNIQUE_REFS_AND_KEYS, REF_1, KEY_1, vec![ALIAS_1]; "No ref or key overlap")]
        #[test_case(&*TEST_CASE_SHARED_REF, REF_1, KEY_1, vec![ALIAS_1]; "Alias has shared ref, unique key")]
        #[test_case(&*TEST_CASE_SHARED_KEY, REF_1, KEY_1, vec![ALIAS_1]; "Alias has shared key, unique ref")]
        #[test_case(&*TEST_CASE_SHARED_REF_AND_KEY, REF_1, KEY_1, vec![ALIAS_1, ALIAS_2]; "Aliases share ref and key")]
        async delete_and_create_new_secret_data_in_place,
        |mount_manager, test_case: &SecretTestCase, target_ref: &str, target_key: &str, affected_aliases: Vec<&str>| {

            // SETUP --------------------------------------------------------------------

            test_case.initialize_mount_manager(&mount_manager);

            // Create the Secrets struct and get the individual Secret structs.
            let secrets = Secrets::new(
                mount_manager.metadata_path().to_path_buf(),
                mount_manager.data_path().to_path_buf(),
            )
            .expect("Failed to create Secrets struct");

            // Get the secrets
            let mut affected_secrets = affected_aliases
                .iter()
                .map(|alias| secrets.get_secret(alias).expect("Failed to get secret"))
                .collect::<Vec<_>>();
            let mut bystander_secrets = test_case.initial_aliases()
                .into_iter()
                .filter(|alias| !affected_aliases.contains(alias))
                .map(|alias| secrets.get_secret(alias).expect("Failed to get secret"))
                .collect::<Vec<_>>();

            // All secrets are available immediately and have the exepected initial data
            assert_secrets_have_initial_data_now(&mut affected_secrets, test_case);
            assert_secrets_have_initial_data_now(&mut bystander_secrets, test_case);

            // END SETUP ----------------------------------------------------------------

            // Start listening for change notifications
            let affected_change_notifications = tokio::task::spawn(all_secrets_get_changed_notification(affected_secrets.clone()));
            let bystander_change_notifications = tokio::task::spawn(all_secrets_get_changed_notification(bystander_secrets.clone()));

            // Remove secret data
            mount_manager.stage_secret_data_remove(target_ref, target_key);
            mount_manager.execute_update_data();

            // The target secret immediately becomes unavailable, but the bystander secrets remain
            // available and at their initial values.
            assert_secrets_unavailable_now(&mut affected_secrets);  // TODO: this is kind of surprising
            assert_secrets_have_initial_data_now(&mut bystander_secrets, test_case);

            // Change notifications are not immediately issued
            assert!(!affected_change_notifications.is_finished());
            assert!(!bystander_change_notifications.is_finished());

            // Change notifications are still not issued after appropriate update time.
            tokio::time::sleep(UPDATE_WINDOW).await;
            assert!(!affected_change_notifications.is_finished());
            assert!(!bystander_change_notifications.is_finished());

            // Create secret data on the same REF and KEY, but with new data
            mount_manager.stage_secret_data_create(target_ref, target_key, NEW_DATA);
            mount_manager.execute_update_data();

            // The target secret immediately becomes available with the new data, while the bystander
            // secrets remain available at their initial values
            assert_secrets_have_expected_data_now(&mut affected_secrets, NEW_DATA);
            assert_secrets_have_initial_data_now(&mut bystander_secrets, test_case);

            // Change notifications are still not immediately issued
            assert!(!affected_change_notifications.is_finished());
            assert!(!bystander_change_notifications.is_finished());

            // Wait for changes to be reported for affected secrets
            tokio::time::timeout(UPDATE_WINDOW, affected_change_notifications)
                .await
                .expect("Timed out waiting for affected secret change notification")
                .expect("Affected secrets change notification task panicked");

            // Change notifications were not issued for bystanders
            assert!(!bystander_change_notifications.is_finished());
        }
    );

    // Tests that a secret can have its alias remapped to point at a new secret data file
    // when the new secret data file is created at the same time as the alias update, with
    // the change only being reported and propagated to the target secret.
    secret_test!(
        #[test_case(&*TEST_CASE_UNIQUE_REFS_AND_KEYS, ALIAS_1 ; "No initial ref or key overlap")]
        #[test_case(&*TEST_CASE_SHARED_REF, ALIAS_1; "Alias initially has shared ref, unique key")]
        #[test_case(&*TEST_CASE_SHARED_KEY, ALIAS_1; "Alias initially has shared key, unique ref")]
        #[test_case(&*TEST_CASE_SHARED_REF_AND_KEY, ALIAS_1; "Alias initially has shared ref and key")]
        async update_secret_alias_to_point_at_new_secret_data_atomic,
        |mount_manager, test_case: &SecretTestCase, target_alias: &str| {

            // SETUP --------------------------------------------------------------------

            test_case.initialize_mount_manager(&mount_manager);

            // Create the Secrets struct and get the individual Secret structs.
            let secrets = Secrets::new(
                mount_manager.metadata_path().to_path_buf(),
                mount_manager.data_path().to_path_buf(),
            )
            .expect("Failed to create Secrets struct");

            // Get the secrets
            let mut target_secret = secrets.get_secret(target_alias).expect("Failed to get secret");
            let mut bystander_secrets = test_case.initial_aliases()
                .into_iter()
                .filter(|alias| *alias != target_alias)
                .map(|alias| secrets.get_secret(alias).expect("Failed to get secret"))
                .collect::<Vec<_>>();

            // All secrets are available immediately and have the exepected initial data
            assert_secret_has_initial_data_now(&mut target_secret, test_case);
            assert_secrets_have_initial_data_now(&mut bystander_secrets, test_case);

            // END SETUP ----------------------------------------------------------------

            // Start listening for change notifications
            let target_change_notification = tokio::task::spawn({
                let mut target_secret = target_secret.clone();
                async move { target_secret.changed().await }    // TODO: use a vec for consistency?
            });
            let bystander_change_notifications = tokio::task::spawn(all_secrets_get_changed_notification(bystander_secrets.clone()));

            // Update secret alias to point at new secret data and create that data
            mount_manager.stage_secret_alias_modify(target_alias, NEW_REF, NEW_KEY);
            mount_manager.stage_secret_data_create(NEW_REF, NEW_KEY, NEW_DATA);
            mount_manager.execute_update_all();

            // Changes are NOT immediately reflected
            assert_secret_has_initial_data_now(&mut target_secret, test_case);
            assert_secrets_have_initial_data_now(&mut bystander_secrets, test_case);

            // Change notifications are not immediately issued
            assert!(!target_change_notification.is_finished());
            assert!(!bystander_change_notifications.is_finished());

            // Wait for change to be reported for target secret
            tokio::time::timeout(UPDATE_WINDOW, target_change_notification)
                .await
                .expect("Timed out waiting for target secret change notification")
                .expect("Target secret change notification task panicked");

            // Bystander secrets did not receive change notifications
            assert!(!bystander_change_notifications.is_finished());

            // Changes are now reflected for the target secret, but bystanders still have original values
            assert_secret_has_expected_data_now(&mut target_secret, NEW_DATA);
            assert_secrets_have_initial_data_now(&mut bystander_secrets, test_case);
        }
    );

    // Tests that a secret can have its alias remapped to point at a new secret data file
    // when the new secret data file is created at the same time as the alias update, with
    // the change only being reported and propagated to the target secret.
    secret_test!(
        #[test_case(&*TEST_CASE_UNIQUE_REFS_AND_KEYS, ALIAS_1 ; "No initial ref or key overlap")]
        #[test_case(&*TEST_CASE_SHARED_REF, ALIAS_1; "Alias initially has shared ref, unique key")]
        #[test_case(&*TEST_CASE_SHARED_KEY, ALIAS_1; "Alias initially has shared key, unique ref")]
        #[test_case(&*TEST_CASE_SHARED_REF_AND_KEY, ALIAS_1; "Alias initially has shared ref and key")]
        async update_secret_alias_to_point_at_new_secret_data_incremental,
        |mount_manager, test_case: &SecretTestCase, target_alias: &str| {

            // SETUP --------------------------------------------------------------------

            test_case.initialize_mount_manager(&mount_manager);

            // Create the Secrets struct and get the individual Secret structs.
            let secrets = Secrets::new(
                mount_manager.metadata_path().to_path_buf(),
                mount_manager.data_path().to_path_buf(),
            )
            .expect("Failed to create Secrets struct");

            // Get the secrets
            let mut target_secret = secrets.get_secret(target_alias).expect("Failed to get secret");
            let mut bystander_secrets = test_case.initial_aliases()
                .into_iter()
                .filter(|alias| *alias != target_alias)
                .map(|alias| secrets.get_secret(alias).expect("Failed to get secret"))
                .collect::<Vec<_>>();

            // All secrets are available immediately and have the exepected initial data
            assert_secret_has_initial_data_now(&mut target_secret, test_case);
            assert_secrets_have_initial_data_now(&mut bystander_secrets, test_case);

            // END SETUP ----------------------------------------------------------------

            // Start listening for change notifications
            let target_change_notification = tokio::task::spawn({
                let mut target_secret = target_secret.clone();
                async move { target_secret.changed().await }    // TODO: use a vec for consistency?
            });
            let bystander_change_notifications = tokio::task::spawn(all_secrets_get_changed_notification(bystander_secrets.clone()));

            // Update secret alias to point at new secret data
            mount_manager.stage_secret_alias_modify(target_alias, NEW_REF, NEW_KEY);
            mount_manager.execute_update_alias();

            // Changes are NOT immediately reflected
            assert_secret_has_initial_data_now(&mut target_secret, test_case);
            assert_secrets_have_initial_data_now(&mut bystander_secrets, test_case);

            // Change notifications are not issued after an appropriate update time
            tokio::time::sleep(UPDATE_WINDOW).await;
            assert!(!target_change_notification.is_finished());
            assert!(!bystander_change_notifications.is_finished());

            // Target secret value is no longer available
            assert_secret_unavailable_now(&mut target_secret);

            // Bystander secrets retain original values and availability
            assert_secrets_have_initial_data_now(&mut bystander_secrets, test_case);

            // Add new secret data
            mount_manager.stage_secret_data_create(NEW_REF, NEW_KEY, NEW_DATA);
            mount_manager.execute_update_data();

            // Changes are STILL NOT immediately reflected
            // Target remains unavailable, bystanders remain unchanged
            assert_secret_unavailable_now(&mut target_secret);
            assert_secrets_have_initial_data_now(&mut bystander_secrets, test_case);

            // Wait for change to be reported for the target secret
            tokio::time::timeout(UPDATE_WINDOW, target_change_notification)
                .await
                .expect("Timed out waiting for target secret change notification")
                .expect("Target secret change notification task panicked");

            // Bystander secrets did not receive change notifications
            assert!(!bystander_change_notifications.is_finished());

            // Changes are now reflected for the target secret, but bystanders still have original values
            assert_secret_has_expected_data_now(&mut target_secret, NEW_DATA);
            assert_secrets_have_initial_data_now(&mut bystander_secrets, test_case);

        }
    );


    // secret_test!(
    //     async secret_survives_secrets_drop, |mount_manager| {

    //     }
    // )
}
