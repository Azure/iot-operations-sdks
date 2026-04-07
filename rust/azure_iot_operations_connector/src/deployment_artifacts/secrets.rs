// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

//! Secrets interface for deployment artifacts.

use std::{
    collections::HashMap,
    path::{Path, PathBuf},
    sync::{
        Arc, Condvar, Mutex, RwLock,
        atomic::{AtomicBool, Ordering},
    },
    thread::JoinHandle,
    time::Duration,
};

use tokio::sync::watch;

use crate::deployment_artifacts::projected_volume_debouncer::{
    ProjectedVolumeDebouncer, ProjectedVolumeError, ProjectedVolumeEventKind,
    ProjectedVolumeEventResult, TICK_RATE as PVDB_TICK_RATE,
};

/// Error for secret
#[derive(Debug, thiserror::Error)]
#[error(transparent)]
pub struct Error(#[from] InnerError);

#[derive(Debug, thiserror::Error)]
#[error(transparent)]
enum InnerError {
    ProjectedVolumeError(#[from] ProjectedVolumeError),
    IoError(#[from] std::io::Error),
    #[error("Invalid secret configuration")]
    Invalid,
}

/// Window for coalescing notifications from multiple debouncers into a single
/// notification per secret. This absorbs the case where the metadata and data
/// debouncers fire at slightly different times for the same logical update.
/// Set to 2x the debouncer tick rate to guarantee both debouncer callbacks
/// are absorbed into a single notification.
#[allow(clippy::cast_possible_truncation)] // PVDB_TICK_RATE is always small enough for u64
const COALESCE_WINDOW: Duration = Duration::from_millis(PVDB_TICK_RATE.as_millis() as u64 * 2);

/// Holds the file watchers (debouncers) that must remain alive as long as any
/// `Secrets` or `Secret` handle exists.
struct Watchers {
    _metadata_debouncer: ProjectedVolumeDebouncer,
    _data_debouncer: ProjectedVolumeDebouncer,
}

/// Manager for Secrets deployed in the connector application
/// Use this to access individual secrets.
#[derive(Clone)]
pub struct Secrets {
    file_watchers: Arc<Watchers>,
    secret_tracker: SecretTracker,
}

impl Secrets {
    /// # Arguments
    /// - `metadata_path`: path the Secret Metadata mount is located at
    /// - `data_path`: path the Secret Data mount is located at
    ///
    /// Fails if paths are invalid
    pub(crate) fn new(metadata_path: PathBuf, data_path: PathBuf) -> Result<Self, Error> {
        Self::new_inner(metadata_path, data_path).map_err(Into::into)
    }

    fn new_inner(metadata_path: PathBuf, data_path: PathBuf) -> Result<Self, InnerError> {
        let secret_tracker = SecretTracker::new(&metadata_path, &data_path)?;
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
                                    log::trace!("Secret metadata change detected: {event:?}");
                                    // Alias is the filename.
                                    let Some(file_name) = event.path.file_name() else {
                                        // NOTE: This should not happen, violation of expected file mount structure.
                                        log::error!(
                                            "Failed to get file name from path: {}",
                                            event.path.display()
                                        );
                                        continue;
                                    };
                                    let Some(alias) = file_name.to_str() else {
                                        // NOTE: This should not happen, violation of expected file mount structure.
                                        log::error!(
                                            "Failed to convert file name to str: {}",
                                            file_name.display()
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
                                                    "Failed to read secret alias file {}: {e:?}",
                                                    event.path.display()
                                                );
                                                continue;
                                            }
                                        };

                                    // Update the secret tracker for the new secret path
                                    if secret_tracker_c1
                                        .update_secret_path(alias, secret_pathbuf)
                                        .is_err()
                                    {
                                        // NOTE: This should not happen, violation of expected file mount structure
                                        log::error!(
                                            "Attempted to update untracked secret alias: {alias}"
                                        );
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
                                    log::trace!("Secret data change detected: {event:?}");
                                    secret_tracker_c2.report_secret_change(&event.path);
                                }
                                // Secret files can be created, but we don't need to do anything
                                // with them until an alias points at them, so log and report.
                                ProjectedVolumeEventKind::FileCreated => {
                                    log::trace!("Secret data creation detected: {event:?}");
                                    secret_tracker_c2.report_secret_change(&event.path);
                                }
                                // Secret files can be deleted, but there's no need for anything
                                // to be done in response, since the Secret interface will handle
                                // the file no longer existing. Log only.
                                ProjectedVolumeEventKind::FileRemoved => {
                                    log::trace!("Secret data removal detected: {event:?}");
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
            file_watchers: Arc::new(Watchers {
                _metadata_debouncer: metadata_debouncer,
                _data_debouncer: data_debouncer,
            }),
            secret_tracker,
        })
    }

    /// Get a Secret corresponding to the given secret alias, if it exists.
    #[must_use]
    pub fn get_secret(&self, alias: &str) -> Option<Secret> {
        self.secret_tracker
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
///
/// Note that cloning a Secret creates a new handle to the same underlying secret, and retains any
/// pending 'changed' notifications. To get a handle without any pending notifications,
/// use `[Secrets::get_secret]` again to get a new handle.
#[derive(Clone)]
pub struct Secret {
    alias: String,
    path: Arc<RwLock<PathBuf>>,
    update_rx: watch::Receiver<()>,
    _file_watchers: Arc<Watchers>,
}

impl Secret {
    /// Returns the alias of the secret
    #[must_use]
    pub fn alias(&self) -> &str {
        &self.alias
    }

    /// Wait for a notification that the secret has been updated, or return immediately if there is
    /// already a pending update that has not been seen.
    pub async fn changed(&mut self) {
        loop {
            // Wait for an update
            let Ok(()) = self.update_rx.changed().await else {
                unreachable!(
                    "Secret update channel closed unexpectedly — channel is maintained by _file_watchers"
                );
            };
            // After being notified of an update, make sure the updated secret exists,
            // or keep waiting for additional updates.
            if self
                .path
                .read()
                .unwrap_or_else(|e| unreachable!("RwLock poisoned: {e}"))
                .exists()
            {
                break;
            }
        }
    }

    /// Indicates if the secret is currently available for retrieval.
    #[must_use]
    pub fn is_available(&self) -> bool {
        self.path
            .read()
            .unwrap_or_else(|e| unreachable!("RwLock poisoned: {e}"))
            .exists()
    }

    /// Attempt to read the value of the secret if it is currently available.
    /// Returns Ok(Some(value)) if the secret is available
    /// Returns Ok(None) if the secret is not currently available
    ///
    /// # Errors
    /// Returns Err if an error occurs while trying to read the secret.
    pub fn value_if_available(&mut self) -> Result<Option<String>, Error> {
        let path = self
            .path
            .read()
            .unwrap_or_else(|e| unreachable!("RwLock poisoned: {e}"));
        if path.exists() {
            // Mark the secret as unchanged since we are reading its current value
            self.update_rx.mark_unchanged();
            Ok(Some(
                std::fs::read_to_string(&*path).map_err(InnerError::from)?,
            ))
        } else {
            Ok(None)
        }
    }

    /// Return the value of the secret now if it is available, or waits for it if it is not yet
    /// available.
    ///
    /// # Errors
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
struct SecretTrackerEntry {
    path: Arc<RwLock<PathBuf>>,
    sender: watch::Sender<()>,
    pending_signal: AtomicBool,
}

/// Handle for tracking secrets. Encapsulates locking and the coalescing background task.
/// Clones share a state.
#[derive(Clone)]
struct SecretTracker {
    state: Arc<RwLock<SecretTrackerState>>,
    _coalesce_thread: Arc<CoalesceThread>,
}

impl SecretTracker {
    fn new(metadata_path: &Path, data_path: &Path) -> Result<Self, InnerError> {
        let state = Arc::new(RwLock::new(SecretTrackerState::new(
            metadata_path,
            data_path,
        )?));

        let coalesce_signal = state.read().unwrap().coalesce_signal.clone();
        let coalesce_thread = Arc::new(CoalesceThread::spawn(coalesce_signal, state.clone()));

        Ok(Self {
            state,
            _coalesce_thread: coalesce_thread,
        })
    }

    fn get_entry_by_alias(&self, alias: &str) -> Option<Arc<SecretTrackerEntry>> {
        self.state.read().unwrap().by_alias.get(alias).cloned()
    }

    fn update_secret_path(&self, alias: &str, new_path: PathBuf) -> Result<(), InnerError> {
        self.state
            .write()
            .unwrap()
            .update_secret_path(alias, new_path)
    }

    fn report_secret_change(&self, path: &Path) {
        self.state.read().unwrap().report_secret_change(path);
    }
}

struct SecretTrackerState {
    by_alias: HashMap<String, Arc<SecretTrackerEntry>>,
    /// Mapping of secret data paths to the SecretTrackerEntry(s) that point at them.
    /// This is a one to many relationship because multiple aliases can point at the same secret data.
    /// Note that secrets are only tracked in here if an alias points at them, there may be secret data
    /// files that exist on disk, but are not tracked here.
    by_path: HashMap<PathBuf, Vec<Arc<SecretTrackerEntry>>>,
    /// Shared signal used to wake the coalescing thread when any entry is signaled.
    coalesce_signal: Arc<CoalesceSignal>,
}

impl SecretTrackerState {
    fn new(metadata_path: &Path, data_path: &Path) -> Result<Self, InnerError> {
        let mut by_alias = HashMap::new();
        let mut by_path = HashMap::new();

        // Initialize all secret aliases / paths
        for entry in std::fs::read_dir(metadata_path)? {
            let entry = entry?;

            // NOTE: Must use entry.path().is_file() instead of entry.file_type()?.is_file()
            // In Kubernetes projected volumes, all files are also symlinks, and entry.file_type()
            // only returns a mutually-exclusive single type, which is always symlink.
            if entry.path().is_file() {
                let secret_alias = entry
                    .file_name()
                    .into_string()
                    .map_err(|_| InnerError::Invalid)?;
                let secret_file = data_path.join(std::fs::read_to_string(entry.path())?);
                let (sender, _rx) = watch::channel(());
                let entry = Arc::new(SecretTrackerEntry {
                    path: Arc::new(RwLock::new(secret_file.clone())),
                    sender,
                    pending_signal: AtomicBool::new(false),
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

        Ok(Self {
            by_alias,
            by_path,
            coalesce_signal: Arc::new(CoalesceSignal::new()),
        })
    }

    /// Update the path of the secret corresponding to the given alias, and notify of the update
    fn update_secret_path(&mut self, alias: &str, new_path: PathBuf) -> Result<(), InnerError> {
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
            log::debug!(
                "Updating secret path for alias {alias} to {}",
                new_path.display()
            );
            (*entry_path_wg).clone_from(&new_path);

            // Finally, add an entry in the by_path map for the new path.
            self.by_path
                .entry(new_path)
                .or_default()
                .push(entry.clone());

            // Signal that the secret has been updated. The coalescing task will
            // flush this into an actual notification after the coalesce window.
            self.signal_entry(entry);
            Ok(())
        } else {
            // Alias was invalid
            Err(InnerError::Invalid)
        }
    }

    fn report_secret_change(&self, path: &Path) {
        if let Some(entries) = self.by_path.get(path) {
            log::debug!(
                "Reporting secret change for path {} to {} secret(s)",
                path.display(),
                entries.len()
            );
            // Notify all corresponding secrets of the change
            // We do not care about send errors here - they just mean nobody is currently
            // monitoring the secret, which is fine.
            for entry in entries {
                self.signal_entry(entry);
            }
        } else {
            log::debug!(
                "Secret changed with no affiliated aliases for path {}",
                path.display()
            );
        }
    }

    /// Flush all pending signals into actual watch notifications.
    fn flush_pending(&self) {
        for entry in self.by_alias.values() {
            if entry.pending_signal.swap(false, Ordering::AcqRel) {
                let _ = entry.sender.send(());
            }
        }
    }
}

// Internal helpers — used by the coalescing mechanism
// Do not invoke these methods from outside of SecretTrackerState
impl SecretTrackerState {
    /// Mark a single entry as having a pending notification and wake the coalescing thread.
    fn signal_entry(&self, entry: &SecretTrackerEntry) {
        entry.pending_signal.store(true, Ordering::Release);
        self.coalesce_signal.wake();
    }
}

/// Condvar-based signal for waking the coalescing thread.
struct CoalesceSignal {
    mutex: Mutex<bool>,
    condvar: Condvar,
}

impl CoalesceSignal {
    fn new() -> Self {
        Self {
            mutex: Mutex::new(false),
            condvar: Condvar::new(),
        }
    }

    /// Wake the coalescing thread.
    fn wake(&self) {
        *self.mutex.lock().unwrap() = true;
        self.condvar.notify_one();
    }

    /// Wait until signaled. Resets the signal state.
    fn wait(&self) {
        let mut signaled = self.mutex.lock().unwrap();
        while !*signaled {
            signaled = self.condvar.wait(signaled).unwrap();
        }
        *signaled = false;
    }
}

/// Background OS thread that coalesces pending secret notifications.
/// Automatically shuts down when dropped.
struct CoalesceThread {
    shutdown: Arc<AtomicBool>,
    signal: Arc<CoalesceSignal>,
    handle: Option<JoinHandle<()>>,
}

impl CoalesceThread {
    fn spawn(signal: Arc<CoalesceSignal>, state: Arc<RwLock<SecretTrackerState>>) -> Self {
        let shutdown = Arc::new(AtomicBool::new(false));
        let shutdown_clone = shutdown.clone();
        let signal_clone = signal.clone();

        let handle = std::thread::spawn(move || {
            while !shutdown_clone.load(Ordering::Relaxed) {
                signal_clone.wait();
                if shutdown_clone.load(Ordering::Relaxed) {
                    break;
                }
                std::thread::sleep(COALESCE_WINDOW);
                state.read().unwrap().flush_pending();
            }
        });

        Self {
            shutdown,
            signal,
            handle: Some(handle),
        }
    }
}

impl Drop for CoalesceThread {
    fn drop(&mut self) {
        self.shutdown.store(true, Ordering::Relaxed);
        self.signal.wake();
        if let Some(handle) = self.handle.take() {
            let _ = handle.join();
        }
    }
}

#[cfg(test)]
mod tests {
    use super::{COALESCE_WINDOW, Secret, Secrets};
    use crate::deployment_artifacts::projected_volume_debouncer::{DEBOUNCE_WINDOW, TICK_RATE};
    use crate::deployment_artifacts::test_utils::TempProjectedVolume;
    use futures_util::FutureExt;
    use std::cell::RefCell;
    use std::collections::{HashMap, HashSet};
    use std::sync::{Arc, LazyLock};
    use std::{path::Path, time::Duration};
    use test_case::test_case;

    // Worst case: DEBOUNCE_WINDOW + TICK_RATE (jitter) + COALESCE_WINDOW + margin.
    // Expressed as the sum of the components with an extra TICK_RATE for safety.
    // Use this value for timeouts or manual waits when waiting for updates to Secrets.
    #[allow(clippy::cast_possible_truncation)] // All values here are small enough for u64
    const UPDATE_WINDOW: Duration = Duration::from_millis(
        DEBOUNCE_WINDOW.as_millis() as u64
            + TICK_RATE.as_millis() as u64
            + COALESCE_WINDOW.as_millis() as u64
            + TICK_RATE.as_millis() as u64,
    );

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
        /// Tracks which `secret_ref` directories have been created in the data mount.
        data_dirs: Arc<RefCell<HashSet<String>>>,
    }

    impl StandardSecretMountManager {
        #[allow(clippy::arc_with_non_send_sync)]
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
        #[allow(clippy::arc_with_non_send_sync)]
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
    const KEY_4: &str = "key4";

    const DATA_1: &str = "data1";
    const DATA_2: &str = "data2";
    const DATA_3: &str = "data3";
    const DATA_4: &str = "data4";
    const DATA_5: &str = "data5";

    const NEW_REF: &str = "new_ref";
    const NEW_KEY: &str = "new_key";
    const NEW_DATA: &str = "new_data";

    macro_rules! secret_test {
        // With #[test_case] attributes
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
        // Without attributes
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
            self.initial_alias_map.keys().copied().collect()
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
            self.initial_data
                .get(&(secret_ref, secret_key))
                .unwrap_or_else(|| panic!("no initial data for ({secret_ref:?}, {secret_key:?})"))
        }

        fn initial_ref_key_for_alias(&self, alias: &str) -> SecretRefKey {
            *self
                .initial_alias_map
                .get(alias)
                .unwrap_or_else(|| panic!("alias {alias:?} not found in initial_alias_map"))
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

    /// Test case where two aliases (`ALIAS_1` and `ALIAS_2`) share the same secret ref but have
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

    /// Test case where two aliases (`ALIAS_1` and `ALIAS_2`) share the same secret key but have
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

    /// Test case where two aliases (`ALIAS_1` and `ALIAS_2`) share the same secret key and secret ref
    /// and therefore share the same secret data.
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

    /// Test case with just one alias and one data file.
    /// Used for tests that don't care about notification routing and validation of
    /// affected vs. unaffected secrets, just want a simple scenario to test more complex
    /// higher-level behavior against.
    static TEST_CASE_SIMPLE: LazyLock<SecretTestCase> = LazyLock::new(|| SecretTestCase {
        initial_data: HashMap::from([((REF_1, KEY_1), DATA_1)]),
        initial_alias_map: HashMap::from([(ALIAS_1, (REF_1, KEY_1))]),
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
        assert!(
            secret
                .value_if_available()
                .expect("Couldn't access secret")
                .is_none()
        );
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

    secret_test!(async secret_reports_alias, |mount_manager| {
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
        assert_eq!(secret.alias(), ALIAS_1);
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

            // Create the Secrets struct
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

            // All secrets are available immediately and have the expected initial data
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

    // Tests that a secret will not report a change when the underlying secret data file is modified but the content
    // does not change, i.e. write the same content that was already there.
    secret_test!(
        #[test_case(&*TEST_CASE_UNIQUE_REFS_AND_KEYS, REF_1, KEY_1, vec![ALIAS_1]; "No ref or key overlap")]
        #[test_case(&*TEST_CASE_SHARED_REF, REF_1, KEY_1, vec![ALIAS_1]; "Alias has shared ref, unique key")]
        #[test_case(&*TEST_CASE_SHARED_KEY, REF_1, KEY_1, vec![ALIAS_1]; "Alias has shared key, unique ref")]
        #[test_case(&*TEST_CASE_SHARED_REF_AND_KEY, REF_1, KEY_1, vec![ALIAS_1, ALIAS_2]; "Aliases share ref and key")]
        async modify_secret_data_in_place_with_same_value,
        |mount_manager, test_case: &SecretTestCase, target_ref: &str, target_key: &str, affected_aliases: Vec<&str>| {

            // SETUP --------------------------------------------------------------------

            test_case.initialize_mount_manager(&mount_manager);

            // Create the Secrets struct
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

            // All secrets are available immediately and have the expected initial data
            assert_secrets_have_initial_data_now(&mut affected_secrets, test_case);
            assert_secrets_have_initial_data_now(&mut bystander_secrets, test_case);

            // END SETUP ----------------------------------------------------------------

            // Start listening for change notifications
            let affected_change_notifications = tokio::task::spawn(all_secrets_get_changed_notification(affected_secrets.clone()));
            let bystander_change_notifications = tokio::task::spawn(all_secrets_get_changed_notification(bystander_secrets.clone()));

            // Update secret data to the same value (i.e. no content change on disk, but a file write nonetheless)
            mount_manager.stage_secret_data_modify(target_ref, target_key, test_case.initial_data_for_ref_key(target_ref, target_key));
            mount_manager.execute_update_data();

            // There will be no changes for the target secret, nor any notifications received,
            // even after waiting for the appropriate update time.
            tokio::time::sleep(UPDATE_WINDOW).await;
            assert_secrets_have_initial_data_now(&mut affected_secrets, test_case);
            assert_secrets_have_initial_data_now(&mut bystander_secrets, test_case);
            assert!(!affected_change_notifications.is_finished());
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

            // Create the Secrets struct
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

            // All secrets are available immediately and have the expected initial data
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
            assert_secrets_unavailable_now(&mut affected_secrets);
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

            // Create the Secrets struct
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

            // All secrets are available immediately and have the expected initial data
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
            assert_secrets_unavailable_now(&mut affected_secrets);
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

            // Create the Secrets struct
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

            // All secrets are available immediately and have the expected initial data
            assert_secret_has_initial_data_now(&mut target_secret, test_case);
            assert_secrets_have_initial_data_now(&mut bystander_secrets, test_case);

            // END SETUP ----------------------------------------------------------------

            // Start listening for change notifications
            let target_change_notification = tokio::task::spawn({
                let mut target_secret = target_secret.clone();
                async move { target_secret.changed().await }
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

            // Create the Secrets struct
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

            // All secrets are available immediately and have the expected initial data
            assert_secret_has_initial_data_now(&mut target_secret, test_case);
            assert_secrets_have_initial_data_now(&mut bystander_secrets, test_case);

            // END SETUP ----------------------------------------------------------------

            // Start listening for change notifications
            let target_change_notification = tokio::task::spawn({
                let mut target_secret = target_secret.clone();
                async move { target_secret.changed().await }
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

            // Changes are immediately reflected for the target secret while the bystanders remain unchanged
            assert_secret_has_expected_data_now(&mut target_secret, NEW_DATA);
            assert_secrets_have_initial_data_now(&mut bystander_secrets, test_case);

            // Wait for change to be reported for the target secret
            tokio::time::timeout(UPDATE_WINDOW, target_change_notification)
                .await
                .expect("Timed out waiting for target secret change notification")
                .expect("Target secret change notification task panicked");

            // Bystander secrets did not receive change notifications
            assert!(!bystander_change_notifications.is_finished());

        }
    );

    // Tests that a secret can have its alias remapped to point at a new secret data file that contains
    // the same data as the old secret data file, with the change only being reported and propagated to
    // the target secret.
    // NOTE: This may not actually be desirable, since it's not providing anything useful to the user,
    // and diverges from how there is no change notification when a file is modified in place to the
    // same value, but it *is* how it is expected to work under the current implementation, and probably
    // not worth the implementation complexity to avoid. If we want to change this behavior, change this
    // test
    secret_test!(
        #[test_case(&*TEST_CASE_UNIQUE_REFS_AND_KEYS, ALIAS_1 ; "No initial ref or key overlap")]
        #[test_case(&*TEST_CASE_SHARED_REF, ALIAS_1; "Alias initially has shared ref, unique key")]
        #[test_case(&*TEST_CASE_SHARED_KEY, ALIAS_1; "Alias initially has shared key, unique ref")]
        #[test_case(&*TEST_CASE_SHARED_REF_AND_KEY, ALIAS_1; "Alias initially has shared ref and key")]
        async update_secret_alias_to_point_at_new_secret_data_with_same_value,
        |mount_manager, test_case: &SecretTestCase, target_alias: &str| {

            // SETUP --------------------------------------------------------------------

            test_case.initialize_mount_manager(&mount_manager);

            // Create the Secrets struct
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

            // All secrets are available immediately and have the expected initial data
            assert_secret_has_initial_data_now(&mut target_secret, test_case);
            assert_secrets_have_initial_data_now(&mut bystander_secrets, test_case);

            // END SETUP ----------------------------------------------------------------

            // Start listening for change notifications
            let target_change_notification = tokio::task::spawn({
                let mut target_secret = target_secret.clone();
                async move { target_secret.changed().await }
            });
            let bystander_change_notifications = tokio::task::spawn(all_secrets_get_changed_notification(bystander_secrets.clone()));

            // Update secret alias to point at new secret data file containing the same data as the old secret data file
            mount_manager.stage_secret_alias_modify(target_alias, NEW_REF, NEW_KEY);
            mount_manager.stage_secret_data_create(NEW_REF, NEW_KEY, test_case.initial_data_for_alias(target_alias));
            mount_manager.execute_update_all();

            // Secrets all still retain their initial data immediately after the update
            assert_secret_has_initial_data_now(&mut target_secret, test_case);
            assert_secrets_have_initial_data_now(&mut bystander_secrets, test_case);

            // A notification was issued within the update window for the target secret,
            // but not for the bystanders
            tokio::time::timeout(UPDATE_WINDOW, target_change_notification)
                .await
                .expect("Timed out waiting for target secret change notification")
                .expect("Target secret change notification task panicked");
            assert!(!bystander_change_notifications.is_finished());

            // However, all secrets still have their initial data
            assert_secret_has_initial_data_now(&mut target_secret, test_case);
            assert_secrets_have_initial_data_now(&mut bystander_secrets, test_case);
        }
    );

    // Tests that a secret can have its alias remapped to point at an existing secret data file
    // with the change only being reported and propagated to the target secret.
    secret_test!(
        #[test_case(&*TEST_CASE_UNIQUE_REFS_AND_KEYS, ALIAS_1, REF_2, KEY_2; "No initial ref or key overlap")]
        #[test_case(&*TEST_CASE_SHARED_REF, ALIAS_1, REF_2, KEY_3; "Alias initially has shared ref, unique key")]
        #[test_case(&*TEST_CASE_SHARED_KEY, ALIAS_1, REF_3, KEY_2; "Alias initially has shared key, unique ref")]
        #[test_case(&*TEST_CASE_SHARED_REF_AND_KEY, ALIAS_1, REF_2, KEY_2; "Alias initially has shared ref and key")]
        async update_secret_alias_to_point_at_existing_secret_data,
        |mount_manager, test_case: &SecretTestCase, target_alias: &str, target_ref: &str, target_key: &str| {

            // SETUP --------------------------------------------------------------------

            test_case.initialize_mount_manager(&mount_manager);

            // Create the Secrets struct
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

            // All secrets are available immediately and have the expected initial data
            assert_secret_has_initial_data_now(&mut target_secret, test_case);
            assert_secrets_have_initial_data_now(&mut bystander_secrets, test_case);

            // END SETUP ----------------------------------------------------------------

            // Start listening for change notifications
            let target_change_notification = tokio::task::spawn({
                let mut target_secret = target_secret.clone();
                async move { target_secret.changed().await }
            });
            let bystander_change_notifications = tokio::task::spawn(all_secrets_get_changed_notification(bystander_secrets.clone()));

            // Update secret alias to point at new secret data and create that data
            mount_manager.stage_secret_alias_modify(target_alias, target_ref, target_key);
            mount_manager.execute_update_alias();

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
            assert_secret_has_expected_data_now(&mut target_secret, test_case.initial_data_for_ref_key(target_ref, target_key));
            assert_secrets_have_initial_data_now(&mut bystander_secrets, test_case);
        }
    );

    // Tests that a secret can receive in-place updates to its underlying secret data after having its alias remapped to point at that secret data
    secret_test!(
        #[test_case(&*TEST_CASE_UNIQUE_REFS_AND_KEYS, ALIAS_1, REF_2, KEY_2; "No initial ref or key overlap")]
        #[test_case(&*TEST_CASE_SHARED_REF, ALIAS_1, REF_2, KEY_3; "Alias initially has shared ref, unique key")]
        #[test_case(&*TEST_CASE_SHARED_KEY, ALIAS_1, REF_3, KEY_2; "Alias initially has shared key, unique ref")]
        #[test_case(&*TEST_CASE_SHARED_REF_AND_KEY, ALIAS_1, REF_2, KEY_2; "Alias initially has shared ref and key")]
        async secret_receives_in_place_updates_after_alias_remap, |mount_manager, test_case: &SecretTestCase, target_alias: &str, target_ref: &str, target_key: &str | {
            // SETUP --------------------------------------------------------------------

            test_case.initialize_mount_manager(&mount_manager);

            // Create the Secrets struct
            let secrets = Secrets::new(
                mount_manager.metadata_path().to_path_buf(),
                mount_manager.data_path().to_path_buf(),
            )
            .expect("Failed to create Secrets struct");

            // Get the secrets
            let mut target_secret = secrets.get_secret(target_alias).expect("Failed to get secret");

            // All secrets are available immediately and have the expected initial data
            assert_secret_has_initial_data_now(&mut target_secret, test_case);

            // END SETUP ----------------------------------------------------------------

            // Update secret alias to point at new secret data and create that data
            mount_manager.stage_secret_alias_modify(target_alias, target_ref, target_key);
            mount_manager.execute_update_alias();

            // Wait for change to be reported for target secret and validate the expected value
            // Wait for change to be reported for target secret
            let target_change_notification = tokio::task::spawn({
                let mut target_secret = target_secret.clone();
                async move { target_secret.changed().await }
            });
            tokio::time::timeout(UPDATE_WINDOW, target_change_notification)
                .await
                .expect("Timed out waiting for target secret change notification")
                .expect("Target secret change notification task panicked");
            assert_secret_has_expected_data_now(&mut target_secret, test_case.initial_data_for_ref_key(target_ref, target_key));

            // Listen for another change notification for the target secret
            let target_change_notification = tokio::task::spawn({
                let mut target_secret = target_secret.clone();
                async move { target_secret.changed().await }
            });

            // Update the secret data in place
            mount_manager.stage_secret_data_modify(target_ref, target_key, NEW_DATA);
            mount_manager.execute_update_data();

            // Change notifications are not immediately issued
            assert!(!target_change_notification.is_finished());

            // Wait for change to be reported for target secret and validate the expected value
            tokio::time::timeout(UPDATE_WINDOW, target_change_notification)
                .await
                .expect("Timed out waiting for target secret change notification")
                .expect("Target secret change notification task panicked");
            assert_secret_has_expected_data_now(&mut target_secret, NEW_DATA);
        }
    );

    // Test that a series of updates to secret data in place only trigger a single notification for a given secret.
    secret_test!(
        async secret_data_modify_in_place_notification_aggregation, |mount_manager| {
            let test_case = &*TEST_CASE_SIMPLE;
            let target_alias = ALIAS_1;

            // SETUP --------------------------------------------------------------------

            test_case.initialize_mount_manager(&mount_manager);

            // Create the Secrets struct and get the target Secret
            let secrets = Secrets::new(
                mount_manager.metadata_path().to_path_buf(),
                mount_manager.data_path().to_path_buf(),
            )
            .expect("Failed to create Secrets struct");
            let mut target_secret = secrets.get_secret(target_alias).expect("Failed to get secret");
            let target_secret_initial_refky = test_case.initial_ref_key_for_alias(target_alias);

            // Secret is available immediately and have the expected initial data
            assert_secret_has_initial_data_now(&mut target_secret, test_case);

            // END SETUP ----------------------------------------------------------------

            // Start listening for change notifications
            let target_change_notification = tokio::task::spawn({
                let mut target_secret = target_secret.clone();
                async move { target_secret.changed().await }
            });

            // Do a series of in-place modifications that will be aggregated together into a single notification
            mount_manager.stage_secret_data_modify(target_secret_initial_refky.0, target_secret_initial_refky.1, "newdata1");
            mount_manager.stage_secret_data_modify(target_secret_initial_refky.0, target_secret_initial_refky.1, "newdata2");
            mount_manager.stage_secret_data_modify(target_secret_initial_refky.0, target_secret_initial_refky.1, "newdata3");
            mount_manager.execute_update_data();

            // Change notification is not immediately issued
            assert!(!target_change_notification.is_finished());

            // However, if you access the secret value, you will find the latest versions of the secret data as the changes
            // have been made on the filesystem.
            assert_secret_has_expected_data_now(&mut target_secret, "newdata3");

            // Wait for change to be reported for target secret
            tokio::time::timeout(UPDATE_WINDOW, target_change_notification)
                .await
                .expect("Timed out waiting for target secret change notification")
                .expect("Target secret change notification task panicked");

            // We'll now find the same updated value we saw before
            assert_secret_has_expected_data_now(&mut target_secret, "newdata3");

            // Once again start listening for change notifications
            let target_change_notification = tokio::task::spawn({
                let mut target_secret = target_secret.clone();
                async move { target_secret.changed().await }
            });

            // Wait for the update window to pass, and there should be no more notifications because all prior
            // changes were aggregated.
            tokio::time::sleep(UPDATE_WINDOW).await;
            assert!(!target_change_notification.is_finished());
        }
    );

    // Test that a series of alias remaps trigger only a single notification for a given secret.
    secret_test!(
        async secret_alias_remap_notification_aggregation, |mount_manager| {

            // Use a test case with many pieces of initial data to avoid complicating the test logic
            // with secret data creation, which should be saved for the mixed notification aggregation test.
            // Use variant refs and keys to validate overlapping scenarios.
            // Define the test case locally since the test is so hyper-dependent on this test case, it
            // should not be extracted for re-use.
            let test_case = SecretTestCase {
                initial_data: HashMap::from([
                    ((REF_1, KEY_1), DATA_1),
                    ((REF_1, KEY_2), DATA_2),
                    ((REF_1, KEY_3), DATA_3),
                    ((REF_2, KEY_1), DATA_4),
                    ((REF_2, KEY_4), DATA_5),
                ]),
                initial_alias_map: HashMap::from([(ALIAS_1, (REF_1, KEY_1))]),
            };
            let target_alias = ALIAS_1;

            // SETUP --------------------------------------------------------------------

            test_case.initialize_mount_manager(&mount_manager);

            // Create the Secrets struct and get the target Secret
            let secrets = Secrets::new(
                mount_manager.metadata_path().to_path_buf(),
                mount_manager.data_path().to_path_buf(),
            )
            .expect("Failed to create Secrets struct");
            let mut target_secret = secrets.get_secret(target_alias).expect("Failed to get secret");

            // Secret is available immediately and have the expected initial data
            assert_secret_has_initial_data_now(&mut target_secret, &test_case);

            // END SETUP ----------------------------------------------------------------

            // Start listening for change notifications
            let target_change_notification = tokio::task::spawn({
                let mut target_secret = target_secret.clone();
                async move { target_secret.changed().await }
            });

            // Do a series of alias remaps (including to a non-existent key) that will be aggregated together
            // into a single notification. Ensure this covers a variety of overlapping refs and key combinations.
            mount_manager.stage_secret_alias_modify(target_alias, REF_1, KEY_2);
            mount_manager.stage_secret_alias_modify(target_alias, REF_1, "nonexistentkey");
            mount_manager.stage_secret_alias_modify(target_alias, REF_2, KEY_1);
            mount_manager.stage_secret_alias_modify(target_alias, REF_1, KEY_3);
            mount_manager.stage_secret_alias_modify(target_alias, REF_2, KEY_4);
            mount_manager.execute_update_alias();

            // Change notification is not immediately issued
            assert!(!target_change_notification.is_finished());

            // If you access the secret value, you will still find the initial secret data
            assert_secret_has_initial_data_now(&mut target_secret, &test_case);

            // Wait for change to be reported for target secret
            tokio::time::timeout(UPDATE_WINDOW, target_change_notification)
                .await
                .expect("Timed out waiting for target secret change notification")
                .expect("Target secret change notification task panicked");

            // We'll now find the updated value from the final remap.
            assert_secret_has_expected_data_now(&mut target_secret, test_case.initial_data_for_ref_key(REF_2, KEY_4));

            // Once again start listening for change notifications
            let target_change_notification = tokio::task::spawn({
                let mut target_secret = target_secret.clone();
                async move { target_secret.changed().await }
            });

            // Wait for the update window to pass, and there should be no more notifications because all prior
            // changes were aggregated.
            tokio::time::sleep(UPDATE_WINDOW).await;
            assert!(!target_change_notification.is_finished());
        }
    );

    // Test that a mixed set of secret filesystem modifications of all types only trigger a single notification
    // for a given secret.
    secret_test!(
        async mixed_secret_notification_aggregation, |mount_manager| {

            // Use variant refs and keys to validate overlapping scenarios
            // Define the test case locally since the test is so hyper-dependent on this test case, it
            // should not be extracted for re-use.
            let test_case = SecretTestCase {
                initial_data: HashMap::from([
                    ((REF_1, KEY_1), DATA_1),
                    ((REF_1, KEY_2), DATA_2),
                    ((REF_1, KEY_3), DATA_3),
                    ((REF_2, KEY_1), DATA_4),
                    ((REF_2, KEY_4), DATA_5),
                ]),
                initial_alias_map: HashMap::from([(ALIAS_1, (REF_1, KEY_1))]),
            };
            let target_alias = ALIAS_1;

            // SETUP --------------------------------------------------------------------

            test_case.initialize_mount_manager(&mount_manager);

            // Create the Secrets struct and get the target Secret
            let secrets = Secrets::new(
                mount_manager.metadata_path().to_path_buf(),
                mount_manager.data_path().to_path_buf(),
            )
            .expect("Failed to create Secrets struct");
            let mut target_secret = secrets.get_secret(target_alias).expect("Failed to get secret");

            // Secret is available immediately and have the expected initial data
            assert_secret_has_initial_data_now(&mut target_secret, &test_case);

            // END SETUP ----------------------------------------------------------------

            // Start listening for change notifications
            let target_change_notification = tokio::task::spawn({
                let mut target_secret = target_secret.clone();
                async move { target_secret.changed().await }
            });

            // Do a series of alias remaps, data modifies, data removes, and data creates that will be aggregated together
            // into a single notification. Covers a variety of refs and keys.
            mount_manager.stage_secret_data_modify(REF_1, KEY_1, "newdata1");
            mount_manager.stage_secret_alias_modify(target_alias, REF_1, "nonexistentkey");
            mount_manager.stage_secret_data_remove(REF_1, KEY_1);
            mount_manager.stage_secret_data_modify(REF_1, KEY_2, "newdata2");
            mount_manager.stage_secret_alias_modify(target_alias, REF_1, KEY_2);
            mount_manager.stage_secret_data_remove(REF_1, KEY_2);
            mount_manager.stage_secret_alias_modify(target_alias, REF_2, KEY_1);
            mount_manager.stage_secret_data_remove(REF_2, KEY_1);
            mount_manager.stage_secret_data_create(REF_2, KEY_1, "newdata3");
            mount_manager.stage_secret_alias_modify(target_alias, REF_2, KEY_4);
            mount_manager.stage_secret_data_modify(REF_2, KEY_4, "newdata4");
            mount_manager.stage_secret_data_modify(REF_2, KEY_4, "newdata5");
            mount_manager.execute_update_all();

            // Change notification is not immediately issued
            assert!(!target_change_notification.is_finished());

            // If you access the secret value, you will find it unavailable due to the underlying data
            // having been removed
            assert_secret_unavailable_now(&mut target_secret);

            // Wait for change to be reported for target secret
            tokio::time::timeout(UPDATE_WINDOW, target_change_notification)
                .await
                .expect("Timed out waiting for target secret change notification")
                .expect("Target secret change notification task panicked");

            // We'll now find the updated value from the final remap.
            assert_secret_has_expected_data_now(&mut target_secret, "newdata5");

            // Once again start listening for change notifications
            let target_change_notification = tokio::task::spawn({
                let mut target_secret = target_secret.clone();
                async move { target_secret.changed().await }
            });

            // Wait for the update window to pass, and there should be no more notifications because all prior
            // changes were aggregated.
            tokio::time::sleep(UPDATE_WINDOW).await;
            assert!(!target_change_notification.is_finished());
        }
    );

    // Tests that multiple Secret handles with the same alias all receive notifications and see
    // updated data
    secret_test!(
        async duplicate_secrets_receive_notifications, |mount_manager| {
            let test_case = &*TEST_CASE_SIMPLE;
            let target_alias = ALIAS_1;

            // SETUP --------------------------------------------------------------------

            test_case.initialize_mount_manager(&mount_manager);

            // Create the Secrets struct
            let secrets = Secrets::new(
                mount_manager.metadata_path().to_path_buf(),
                mount_manager.data_path().to_path_buf(),
            )
            .expect("Failed to create Secrets struct");

            // Create three secrets with the same alias
            let mut target_secrets = vec![
                secrets.get_secret(target_alias).expect("Failed to get secret"),
                secrets.get_secret(target_alias).expect("Failed to get secret"),
                secrets.get_secret(target_alias).expect("Failed to get secret"),
            ];
            target_secrets.push(target_secrets[0].clone());
            target_secrets.push(target_secrets[1].clone());


            // Secret is available immediately and have the expected initial data
            assert_secrets_have_initial_data_now(&mut target_secrets, test_case);

            // END SETUP ----------------------------------------------------------------

            let target_change_notifications = tokio::task::spawn(all_secrets_get_changed_notification(target_secrets.clone()));

            // Update secret data in place
            let target_refky = test_case.initial_ref_key_for_alias(target_alias);
            mount_manager.stage_secret_data_modify(target_refky.0, target_refky.1, NEW_DATA);
            mount_manager.execute_update_data();

            // All three handles should receive change notifications and see the updated data
            tokio::time::timeout(UPDATE_WINDOW, target_change_notifications)
                .await
                .expect("Timed out waiting for secret change notifications")
                .expect("Secret change notification task panicked");

            assert_secrets_have_expected_data_now(&mut target_secrets, NEW_DATA);
        }
    );

    // Tests that clones of a Secret handle receive the same notifications as the original.
    secret_test!(
        async cloned_secrets_receive_notifications, | mount_manager| {
            let test_case = &*TEST_CASE_SIMPLE;
            let target_alias = ALIAS_1;

            // SETUP --------------------------------------------------------------------

            test_case.initialize_mount_manager(&mount_manager);

            // Create the Secrets struct
            let secrets = Secrets::new(
                mount_manager.metadata_path().to_path_buf(),
                mount_manager.data_path().to_path_buf(),
            )
            .expect("Failed to create Secrets struct");
            let mut original_secret = secrets.get_secret(target_alias).expect("Failed to get secret");
            let mut cloned_secret = original_secret.clone();

            // Secret is available immediately and have the expected initial data
            assert_secret_has_initial_data_now(&mut original_secret, test_case);
            assert_secret_has_initial_data_now(&mut cloned_secret, test_case);

            // END SETUP ----------------------------------------------------------------

            // NOTE: Avoid using any test infra that requires the use of .clone() so as not to create
            // confounds. This makes this test use a bit of a different pattern than most others.

            // Update secret data in place
            let target_refky = test_case.initial_ref_key_for_alias(target_alias);
            mount_manager.stage_secret_data_modify(target_refky.0, target_refky.1, NEW_DATA);
            mount_manager.execute_update_data();

            // Wait for the change notification to be delivered to the original secret and verify
            // the new data is available and the expected value.
            let original_change_f = original_secret.changed();
            tokio::time::timeout(UPDATE_WINDOW, original_change_f)
                .await
                .expect("Timed out waiting for original secret change notification");
            assert_secret_has_expected_data_now(&mut original_secret, NEW_DATA);

            // At the same time, the clone should also have it's notification immediately available
            // with the same data.
            cloned_secret.changed().now_or_never()
                .expect("Cloned secret change notification not immediately available after original secret received notification");
            assert_secret_has_expected_data_now(&mut cloned_secret, NEW_DATA);
        }
    );

    // Verifies that cloning a Secret copies the pending notification state.
    // A clone made while a notification is pending should also see that notification
    // immediately via .changed(), without needing to wait for a new one.
    // Tests both in-place data updates and alias remaps.
    secret_test!(
        async cloned_secret_retains_pending_changed_state, |mount_manager| {
            let test_case = SecretTestCase {
                initial_data: HashMap::from([
                    ((REF_1, KEY_1), DATA_1),
                    ((REF_2, KEY_2), DATA_2),
                ]),
                initial_alias_map: HashMap::from([(ALIAS_1, (REF_1, KEY_1))]),
            };
            let target_alias = ALIAS_1;

            // SETUP --------------------------------------------------------------------

            test_case.initialize_mount_manager(&mount_manager);

            // Create the Secrets struct and get the target Secret
            let secrets = Secrets::new(
                mount_manager.metadata_path().to_path_buf(),
                mount_manager.data_path().to_path_buf(),
            )
            .expect("Failed to create Secrets struct");
            let mut original_secret = secrets.get_secret(target_alias).expect("Failed to get secret");

            // Secret is available immediately and have the expected initial data
            assert_secret_has_initial_data_now(&mut original_secret, &test_case);

            // END SETUP ----------------------------------------------------------------

            // --- Part 1: Clone copies pending state from in-place data update ---

            // Modify the secret data and wait for the notification to be delivered
            let target_refky = test_case.initial_ref_key_for_alias(target_alias);
            mount_manager.stage_secret_data_modify(target_refky.0, target_refky.1, NEW_DATA);
            mount_manager.execute_update_data();
            tokio::time::sleep(UPDATE_WINDOW).await;

            // The original secret now has a pending notification that hasn't been consumed.
            // Clone it — the clone should inherit the pending state.
            let mut cloned_secret = original_secret.clone();

            // Both should consume the pending notification immediately
            original_secret.changed().now_or_never()
                .expect("Original secret should have had a pending notification from data update");
            cloned_secret.changed().now_or_never()
                .expect("Cloned secret should have inherited the pending notification from data update");

            // Both should see the updated data
            assert_secret_has_expected_data_now(&mut original_secret, NEW_DATA);
            assert_secret_has_expected_data_now(&mut cloned_secret, NEW_DATA);

            // --- Part 2: Clone copies pending state from alias remap ---

            // Remap the alias and wait for the notification to be delivered
            mount_manager.stage_secret_alias_modify(target_alias, REF_2, KEY_2);
            mount_manager.execute_update_alias();
            tokio::time::sleep(UPDATE_WINDOW).await;

            // Clone while notification is pending
            let mut cloned_secret_2 = original_secret.clone();

            // Both should consume the pending notification immediately
            original_secret.changed().now_or_never()
                .expect("Original secret should have had a pending notification from alias remap");
            cloned_secret_2.changed().now_or_never()
                .expect("Cloned secret should have inherited the pending notification from alias remap");

            // Both should see the remapped data
            assert_secret_has_expected_data_now(&mut original_secret, DATA_2);
            assert_secret_has_expected_data_now(&mut cloned_secret_2, DATA_2);
        }
    );

    secret_test!(
        async secret_survives_secrets_drop, |mount_manager| {
            let test_case = &*TEST_CASE_SIMPLE;
            let target_alias = ALIAS_1;

            // SETUP --------------------------------------------------------------------

            test_case.initialize_mount_manager(&mount_manager);

            // Create the Secrets struct and get the target Secret
            let secrets = Secrets::new(
                mount_manager.metadata_path().to_path_buf(),
                mount_manager.data_path().to_path_buf(),
            )
            .expect("Failed to create Secrets struct");
            let mut target_secret = secrets.get_secret(target_alias).expect("Failed to get secret");

            // Secret is available immediately and have the expected initial data
            assert_secret_has_initial_data_now(&mut target_secret, test_case);

            // END SETUP ----------------------------------------------------------------

            // Drop the Secrets manager — only the individual Secret handles remain
            drop(secrets);

            // In place data update after Secrets is dropped
            let target_refky = test_case.initial_ref_key_for_alias(target_alias);
            mount_manager.stage_secret_data_modify(target_refky.0, target_refky.1, "newdata1");
            mount_manager.execute_update_data();

            // Wait for the change notification to be delivered, and that the new data is available,
            // which proves that the Secret is still receiving secret data updates after Secrets is dropped
            tokio::time::timeout(UPDATE_WINDOW, target_secret.changed())
                .await
                .expect("Timed out waiting for secret change notification after Secrets was dropped");
            assert_secret_has_expected_data_now(&mut target_secret, "newdata1");

            // Alias update to new secret data
            mount_manager.stage_secret_data_create(NEW_REF, NEW_KEY, "newdata2");
            mount_manager.stage_secret_alias_modify(target_alias, NEW_REF, NEW_KEY);
            mount_manager.execute_update_all();

            // Wait for the change notification to be delivered, and that the new data is available,
            // which proves that the Secret is still receiving alias update notifications after Secrets is dropped
            tokio::time::timeout(UPDATE_WINDOW, target_secret.changed())
                .await
                .expect("Timed out waiting for secret change notification after Secrets was dropped");
            assert_secret_has_expected_data_now(&mut target_secret, "newdata2");

        }
    );

    // Verifies that .value() remains functional after a previous call was cancelled.
    // Uses an alias remap to ensure the second .value() call must go through the
    // notification path (.changed()) rather than finding data on disk directly,
    // which would mask a cancel-safety issue.
    secret_test!(
        async cancel_safety_secret_value, |mount_manager| {
            // Use a test case with extra data so we have somewhere to remap to
            let test_case = SecretTestCase {
                initial_data: HashMap::from([
                    ((REF_1, KEY_1), DATA_1),
                    ((REF_2, KEY_2), NEW_DATA),
                ]),
                initial_alias_map: HashMap::from([(ALIAS_1, (REF_1, KEY_1))]),
            };
            let target_alias = ALIAS_1;

            // SETUP --------------------------------------------------------------------

            test_case.initialize_mount_manager(&mount_manager);

            let secrets = Secrets::new(
                mount_manager.metadata_path().to_path_buf(),
                mount_manager.data_path().to_path_buf(),
            )
            .expect("Failed to create Secrets struct");
            let mut target_secret = secrets.get_secret(target_alias).expect("Failed to get secret");

            // Remove the secret data so that .value() will have to wait
            // NOTE: We have to have initial values even though we delete them to make sure
            // setup goes smoothly.
            let target_refky = test_case.initial_ref_key_for_alias(target_alias);
            mount_manager.stage_secret_data_remove(target_refky.0, target_refky.1);
            mount_manager.execute_update_data();
            tokio::time::sleep(UPDATE_WINDOW).await;
            assert_secret_unavailable_now(&mut target_secret);

            // END SETUP ----------------------------------------------------------------

            // Call .value() with a short timeout to force cancellation.
            // The value is not available, so .value() awaits .changed() internally.
            // The timeout fires first, cancelling the .value() future.
            let result = tokio::time::timeout(
                Duration::from_millis(10),
                target_secret.value(),
            ).await;
            assert!(result.is_err(), "Expected timeout (cancellation), but value() returned");

            // Now remap the alias to point at existing data at a different path.
            // The data exists on disk at (REF_2, KEY_2), but the secret's internal
            // path still points at (REF_1, KEY_1) which was removed. The secret can only
            // find the new data after receiving the alias-remap notification via .changed().
            // If cancellation corrupted the watch receiver, this notification would be
            // missed and .value() would hang.
            mount_manager.stage_secret_alias_modify(target_alias, REF_2, KEY_2);
            mount_manager.execute_update_alias();

            // .value() must wait for the alias remap notification to update the path,
            // then read the data from the new location.
            let result = tokio::time::timeout(
                UPDATE_WINDOW,
                target_secret.value(),
            ).await;
            let value = result
                .expect("Timed out waiting for value() after cancellation — possible cancel-safety issue")
                .expect("Failed to read secret value after cancellation");
            assert_eq!(value, NEW_DATA);
        }
    );

    // Verifies that .changed() remains functional after a previous call was cancelled.
    // Uses an alias remap to ensure the second .changed() call must receive a real
    // notification rather than returning due to stale state, which would mask a
    // cancel-safety issue.
    secret_test!(
        async cancel_safety_secret_changed, |mount_manager| {
            let test_case = SecretTestCase {
                initial_data: HashMap::from([
                    ((REF_1, KEY_1), DATA_1),
                    ((REF_2, KEY_2), DATA_2),
                ]),
                initial_alias_map: HashMap::from([(ALIAS_1, (REF_1, KEY_1))]),
            };
            let target_alias = ALIAS_1;

            // SETUP --------------------------------------------------------------------

            test_case.initialize_mount_manager(&mount_manager);

            let secrets = Secrets::new(
                mount_manager.metadata_path().to_path_buf(),
                mount_manager.data_path().to_path_buf(),
            )
            .expect("Failed to create Secrets struct");
            let mut target_secret = secrets.get_secret(target_alias).expect("Failed to get secret");

            assert_secret_has_initial_data_now(&mut target_secret, &test_case);

            // END SETUP ----------------------------------------------------------------

            // Call .changed() with a short timeout to force cancellation.
            // No changes have been made, so .changed() will block indefinitely.
            // The timeout fires first, cancelling the future.
            let result = tokio::time::timeout(
                Duration::from_millis(10),
                target_secret.changed(),
            ).await;
            assert!(result.is_err(), "Expected timeout (cancellation), but changed() returned");

            // The secret should still have its initial data and be fully functional
            assert_secret_has_initial_data_now(&mut target_secret, &test_case);

            // Now remap the alias. The secret's internal path still points at the old
            // location, so .changed() must receive the alias-remap notification to update
            // the path. If cancellation corrupted the watch receiver, the notification
            // would be missed and .changed() would hang.
            mount_manager.stage_secret_alias_modify(target_alias, REF_2, KEY_2);
            mount_manager.execute_update_alias();

            // .changed() must successfully receive the notification after cancellation
            tokio::time::timeout(UPDATE_WINDOW, target_secret.changed())
                .await
                .expect("Timed out waiting for changed() after cancellation — possible cancel-safety issue");

            // The secret should now reflect the remapped data
            assert_secret_has_expected_data_now(&mut target_secret, DATA_2);
        }
    );
}
