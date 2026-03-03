// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

//! Secrets interface for deployment artifacts.

use std::{
    collections::HashMap,
    path::{self, Path, PathBuf},
    sync::{Arc, RwLock},
    time::Duration,
};

use notify::{EventKind, RecommendedWatcher, RecursiveMode, event::ModifyKind};
use notify_debouncer_full::{DebounceEventResult, Debouncer, RecommendedCache, new_debouncer};
use tokio::sync::watch;

// TODO: revisit this error
/// Error for secret
#[derive(Debug, thiserror::Error)]
#[error(transparent)]
pub struct Error(#[from] InnerError);

#[derive(Debug, thiserror::Error)]
#[error(transparent)]
enum InnerError {
    NotifyError(#[from] notify::Error),
    IoError(#[from] std::io::Error),
    #[error("Invalid")]
    Invalid,
}

pub struct Secrets {
    #[allow(dead_code)]
    metadata_debouncer: Arc<Debouncer<RecommendedWatcher, RecommendedCache>>, // TODO: probably need to keep this alive in child Secrets
    #[allow(dead_code)]
    data_debouncer: Arc<Debouncer<RecommendedWatcher, RecommendedCache>>,
    secret_tracker: Arc<RwLock<SecretTracker>>,
}

// If we only map alias to sender, then how can we report secret updates? Without storing context, no way to know
// which Secret to transmit to.

impl Secrets {
    /// Fails if paths are invalid
    pub(crate) fn new(
        metadata_path: PathBuf,
        data_path: PathBuf,
        aggregation_window: Duration,
    ) -> Result<Self, Error> {
        Self::new_inner(metadata_path, data_path, aggregation_window).map_err(Into::into)
    }

    fn new_inner(
        metadata_path: PathBuf,
        data_path: PathBuf,
        aggregation_window: Duration,
    ) -> Result<Self, InnerError> {
        let secret_tracker = Arc::new(RwLock::new(SecretTracker::new(
            metadata_path.clone(),
            data_path.clone(),
        )?));
        let secret_tracker_c1 = secret_tracker.clone();
        let secret_tracker_c2 = secret_tracker.clone();
        let data_path_c1 = data_path.clone(); // TODO: get rid of this by reversing debouncer defs

        // Set up the Secret Metadata mount debouncer.
        let mut metadata_debouncer = new_debouncer(
            aggregation_window.clone(),
            None,
            move |res: DebounceEventResult| {
                match res {
                    Ok(db_events) => {
                        db_events.iter().for_each(|db_event| {
                            match db_event.event.kind {
                                // Handle updates to existing aliases
                                // (i.e. secret alias now points to a different secret)
                                EventKind::Modify(ModifyKind::Data(_)) => {
                                    log::trace!("Secret metadata change detected: {:?}", db_event);
                                    // There is never more than one path in the vector for data modifications,
                                    // so it is safe to simply access the first element of event paths.
                                    let alias_pathbuf = &db_event.event.paths[0];
                                    if !alias_pathbuf.is_file() {
                                        // NOTE: This should not happen, violation of expected file mount structure.
                                        log::error!("Expected file path for secret alias, got directory: {:?}", alias_pathbuf);
                                        return;
                                    }
                                    // Alias is the filename. 
                                    let Some(file_name) = alias_pathbuf.file_name() else {
                                        // NOTE: This should not happen, violation of expected file mount structure.
                                        log::error!("Failed to get file name from path: {:?}", alias_pathbuf);
                                        return;
                                    };
                                    let Some(alias) = file_name.to_str() else {
                                        // NOTE: This should not happen, violation of expected file mount structure.
                                        log::error!("Failed to convert file name to str: {:?}", file_name);
                                        return;
                                    };

                                    // Read the alias file to get the new secret path.
                                    // Do this before notifying about updates.
                                    let secret_pathbuf = match std::fs::read_to_string(alias_pathbuf.as_path()) {
                                        Ok(file_content) => data_path_c1.join(file_content),
                                        Err(e) => {
                                            // NOTE: This should not happen, as alias files should not be added or removed dynamically.
                                            log::error!("Failed to read secret alias file {:?}: {e:?}", alias_pathbuf);
                                            return;
                                        }
                                    };

                                    // Update the secret tracker for the new secret path
                                    if let Err(_) = secret_tracker_c1.write().unwrap().update_secret_path(alias, secret_pathbuf) {
                                        // NOTE: This should not happen, violation of expected file mount structure
                                        log::error!("Attempted to update untracked secret alias: {alias}");
                                        return;
                                    }
                                }
                                // Alias files are not supposed to be able to be added or removed dynamically,
                                // nor should they be renamed
                                EventKind::Create(_) | EventKind::Remove(_) | EventKind::Modify(ModifyKind::Name(_)) => {
                                    log::error!("Unexpected debounce event for Secret metadata: {db_event:?}");
                                }
                                // All other events can be ignored silently
                                _ => {}
                            }
                        })
                    }
                    Err(errs) => {
                        for e in errs {
                            log::error!("Error processing Secret metadata debounce event: {e:?}");
                        }
                    }
                }
            },
        )?;
        // NOTE: Secret metadata is a flat directory, so we can watch non-recursively.
        metadata_debouncer.watch(&metadata_path, RecursiveMode::NonRecursive)?;

        let mut data_debouncer = new_debouncer(
            aggregation_window,
            None,
            move |res: DebounceEventResult| {
                match res {
                    Ok(db_events) => {
                        db_events.iter()
                            // Only process file changes, directories are irrelevant
                            .filter(|db_event| db_event.event.paths[0].is_file())
                            .for_each(|db_event| {
                            match db_event.event.kind {
                                // Handle updates to existing secret data.
                                EventKind::Modify(ModifyKind::Data(_)) => {
                                    log::trace!("Secret data change detected: {:?}", db_event);
                                    if let Err(_) = secret_tracker_c2
                                        .read()
                                        .unwrap()
                                        .report_secret_update(&db_event.event.paths[0]) {
                                            // NOTE: This should not happen, secrets are always supposed
                                            // to be tracked by an alias file. However, there is a small chance
                                            // of this happening if the secret file is updated after being created
                                            // but before the alias file is updated to point at it. This seems
                                            // very unlikely, so we log here at error instead of warn since the most
                                            // likely cause by far is a violation of expected file mount structure.
                                            log::error!("Attempted to report update for untracked secret path: {:?}", db_event.event.paths[0]);
                                        }
                                }
                                // Secret files can be created, but we don't need to do anything
                                // with them until an alias points at them, so log only.
                                EventKind::Create(_) => {
                                    log::trace!("Secret data creation detected: {:?}", db_event);
                                    // NOTE: We do not care about the result here.
                                    // If the path is not yet tracked (i.e. data is updated before alias file), then this will fail.
                                    // If the path is tracked (i.e. the alias file is already pointing at this file), then this will succeed.
                                    // Either way, the Secret will be notified upon the link being established, whether here or
                                    // via the alias debouncer.
                                    let _ = secret_tracker_c2
                                        .read()
                                        .unwrap()
                                        .report_secret_update(&db_event.event.paths[0]);
                                }
                                // Secret files can be deleted, but there's no need for anything
                                // to be done in response, since the Secret interface will handle
                                // the file no longer existing. Log only.
                                EventKind::Remove(_) => {
                                    log::trace!("Secret data removal detected: {:?}", db_event);
                                }
                                // Similar to deletion, this will be handled by the Secret interface.
                                // Log only.
                                EventKind::Modify(ModifyKind::Name(_)) => {
                                    log::trace!("Secret data rename detected: {:?}", db_event);
                                }
                                // All other events can be ignored
                                _ => {}
                            }
                        })
                    }
                    Err(errs) => {
                        for e in errs {
                            log::error!("Error processing Secret data debounce event: {e:?}");
                        }
                    }
                }
            },
        )?;
        data_debouncer.watch(&data_path, RecursiveMode::Recursive)?;

        Ok(Self {
            metadata_debouncer: Arc::new(metadata_debouncer),
            data_debouncer: Arc::new(data_debouncer),
            secret_tracker,
        })
    }

    /// Get a Secret corresponding to the given alias, if it exists.
    pub fn get_secret(&self, alias: &str) -> Option<Secret> {
        self.secret_tracker
            .read()
            .unwrap()
            .get_entry_by_alias(alias)
            .map(|entry| Secret {
                alias: alias.to_string(),
                path: entry.path.clone(),
                update_rx: entry.sender.subscribe(),
            })
    }
}

#[derive(Debug, Clone)] // TODO: do we need a custom clone implementation, or should the pending updates carry over?
pub struct Secret {
    alias: String,
    path: Arc<RwLock<PathBuf>>,
    update_rx: watch::Receiver<()>,
}

impl Secret {
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
                .expect("Secret update channel closed unexpectedly");
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
            // TODO: is this right?
            self.update_rx.mark_unchanged();
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

struct SecretTrackerEntry {
    path: Arc<RwLock<PathBuf>>,
    sender: watch::Sender<()>,
}

struct SecretTracker {
    by_alias: HashMap<String, Arc<SecretTrackerEntry>>,
    by_path: HashMap<PathBuf, Arc<SecretTrackerEntry>>,
}

impl SecretTracker {
    fn new(metadata_path: PathBuf, data_path: PathBuf) -> Result<Self, InnerError> {
        let mut by_alias = HashMap::new();
        let mut by_path = HashMap::new();

        // TODO: this needs to handle secrets that may be initially orphaned?

        // Initialize all secret aliases / paths
        for entry in std::fs::read_dir(&metadata_path)? {
            let entry = entry?;
            if entry.file_type()?.is_file() {
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
                by_path.insert(secret_file, entry);
            }
        }

        Ok(Self { by_alias, by_path })
    }

    /// Return an Arc reference to the SecretTrackerEntry corresponding to the given alias, if it exists.
    fn get_entry_by_alias(&self, alias: &str) -> Option<Arc<SecretTrackerEntry>> {
        self.by_alias.get(alias).cloned()
    }

    /// Update the path of the secret corresponding to the given alias, and notify of the update
    fn update_secret_path(&mut self, alias: &str, new_path: PathBuf) -> Result<(), InnerError> {
        if let Some(entry) = self.by_alias.get(alias) {
            // Update the path in the SecretTrackerEntry, and the by_path mapping.
            log::debug!("Updating secret path for alias {alias} to {new_path:?}");
            *entry.path.write().unwrap() = new_path.clone();
            
            
            // if let Some(old_entry) = self.by_path.insert(new_path, entry.clone()) {
            //     //log::

            // }


            // Notify the Secret of the update
            // We do not care about send errors here - they just mean nobody is currently
            // monitoring the secret, which is fine.
            let _ = entry.sender.send(());

            // TODO: PROBLEM: multiple aliases sharing same secret data is not handled!

            Ok(())
        } else {
            // Alias was invalid
            Err(InnerError::Invalid)
        }
    }

    // /// Add a new secret to be tracked with the given path.
    // fn add_secret(&mut self, path: PathBuf) {
    //     self.by_path.insert(path.clone(), Arc::new(SecretTrackerEntry {
    //         path: Arc::new(RwLock::new(path)),
    //         sender: watch::channel(()).0,
    //     }));

    //     // // Notify the Secret of the update
    //     // // We do not care about send errors here - they just mean nobody is currently
    //     // // monitoring the secret, which is fine.
    //     // let _ = entry.sender.send(());

    //     // TODO: Does this need to report update though?
    // }

    fn report_secret_update(&self, path: &Path) -> Result<(), InnerError> {
        log::debug!("Attempt to update secret");
        if let Some(entry) = self.by_path.get(path) {
            log::debug!("Reporting secret update for path {path:?}");
            // Notify the Secret of the update
            // We do not care about send errors here - they just mean nobody is currently
            // monitoring the secret, which is fine.
            let _ = entry.sender.send(());
            Ok(())
        } else {
            log::debug!("Invalid secret update path");
            // Path does not correspond to any known secrets
            Err(InnerError::Invalid)
        }
    }

    // // TODO: need to be able to report mismatch of given path/alias or send failure for logging.

    // TODO: what about cleanup/deletion?

    // TODO: clones, drops, cancel safety.
}

#[cfg(test)]
mod tests {
    use super::{Secret, Secrets};
    use crate::deployment_artifacts::test_utils::TempMount;
    use std::{path::Path, time::Duration};
    use tokio::time::Instant;

    // NOTE: We need to have two types of mount managers to handle the variant cases of
    // Secret Sync vs. non-Secret Sync scenarios. The `Secrets` and `Secret` structs are designed
    // to abstract this distinction from the end user, but it has ramifications on the filesystem
    // structure, so we need to test against both to validate that both scenarios are handled.
    // The only real impact is around recursive vs. non-recursive watching, but it's safer to just
    // test everything against both.

    trait SecretMountManager: Clone {
        fn add_secret_alias(&self, secret_alias: &str, secret_ref: &str, secret_key: &str);

        fn update_secret_alias(
            &self,
            secret_alias: &str,
            new_secret_ref: &str,
            new_secret_key: &str,
        );

        fn add_secret_data(&self, secret_ref: &str, secret_key: &str, secret_data: &str);

        fn update_secret_data(&self, secret_ref: &str, secret_key: &str, new_secret_data: &str);

        fn remove_secret_data(&self, secret_ref: &str, secret_key: &str);

        fn metadata_path(&self) -> &Path;

        fn data_path(&self) -> &Path;
    }

    //
    #[derive(Clone)]
    struct StandardSecretMountManager {
        metadata_mount: TempMount,
        data_mount: TempMount,
    }

    impl StandardSecretMountManager {
        fn new() -> Self {
            Self {
                metadata_mount: TempMount::new("metadata"),
                data_mount: TempMount::new("data"),
            }
        }
    }

    impl SecretMountManager for StandardSecretMountManager {
        fn add_secret_alias(&self, secret_alias: &str, secret_ref: &str, secret_key: &str) {
            self.metadata_mount
                .add_file(secret_alias, &format!("{secret_ref}/{secret_key}"));
        }

        fn update_secret_alias(
            &self,
            secret_alias: &str,
            new_secret_ref: &str,
            new_secret_key: &str,
        ) {
            self.metadata_mount
                .update_file(secret_alias, &format!("{new_secret_ref}/{new_secret_key}"));
        }

        fn add_secret_data(&self, secret_ref: &str, secret_key: &str, secret_data: &str) {
            std::fs::create_dir_all(self.data_mount.path().join(secret_ref)).unwrap();
            self.data_mount
                .add_file(&format!("{secret_ref}/{secret_key}"), secret_data);
        }

        fn update_secret_data(&self, secret_ref: &str, secret_key: &str, new_secret_data: &str) {
            self.data_mount
                .update_file(&format!("{secret_ref}/{secret_key}"), new_secret_data);
        }

        fn remove_secret_data(&self, secret_ref: &str, secret_key: &str) {
            self.data_mount
                .remove_file(&format!("{secret_ref}/{secret_key}"));
            if std::fs::read_dir(self.data_mount.path().join(secret_ref))
                .unwrap()
                .next()
                .is_none()
            {
                std::fs::remove_dir(self.data_mount.path().join(secret_ref)).unwrap();
            }
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
        metadata_mount: TempMount,
        data_mount: TempMount,
    }

    impl SecretSyncMountManager {
        fn new() -> Self {
            Self {
                metadata_mount: TempMount::new("metadata"),
                data_mount: TempMount::new("data"),
            }
        }
    }

    impl SecretMountManager for SecretSyncMountManager {
        fn add_secret_alias(&self, secret_alias: &str, secret_ref: &str, secret_key: &str) {
            self.metadata_mount
                .add_file(secret_alias, &format!("{secret_ref}_{secret_key}"));
        }

        fn update_secret_alias(
            &self,
            secret_alias: &str,
            new_secret_ref: &str,
            new_secret_key: &str,
        ) {
            self.metadata_mount
                .update_file(secret_alias, &format!("{new_secret_ref}_{new_secret_key}"));
        }

        fn add_secret_data(&self, secret_ref: &str, secret_key: &str, secret_data: &str) {
            self.data_mount
                .add_file(&format!("{secret_ref}_{secret_key}"), secret_data);
        }

        fn update_secret_data(&self, secret_ref: &str, secret_key: &str, new_secret_data: &str) {
            self.data_mount
                .update_file(&format!("{secret_ref}_{secret_key}"), new_secret_data);
        }

        fn remove_secret_data(&self, secret_ref: &str, secret_key: &str) {
            self.data_mount
                .remove_file(&format!("{secret_ref}_{secret_key}"));
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
    const ALIAS_4: &str = "alias4";
    const ALIAS_5: &str = "alias5";
    const REF_1: &str = "ref1";
    const REF_2: &str = "ref2";
    const REF_3: &str = "ref3";
    const KEY_1: &str = "key1";
    const KEY_2: &str = "key2";
    const KEY_3: &str = "key3";
    const DATA_1: &str = "data1";
    const DATA_2: &str = "data2";
    const DATA_3: &str = "data3";
    const DATA_4: &str = "data4";
    const DATA_5: &str = "data5";
    const DATA_6: &str = "data6";
    const DATA_7: &str = "data7";
    const DATA_8: &str = "data8";

    /// This test outlines basic updating of secrets in place.
    /// Exact timing of change notifications will be covered in other tests.
    #[tokio::test]
    async fn update_secret_data_in_place() {
        let standard_manager = StandardSecretMountManager::new();
        update_secret_data_in_place_logic(standard_manager).await;

        let sync_manager = SecretSyncMountManager::new();
        update_secret_data_in_place_logic(sync_manager).await;
    }

    async fn update_secret_data_in_place_logic(mount_manager: impl SecretMountManager) {
        // Use a short aggregation window to make test run quickly
        const AGGREGATION_WINDOW: Duration = Duration::from_millis(100);

        // Initialize five secret aliases on disk:
        // - two of which share the same secret reference (ALIAS_1 and ALIAS_2 both use REF_1)
        // - two of which share the same secret reference AND same secret key (i.e. same data file) (ALIAS_1 and ALIAS_4 both point at REF_1/KEY_1)
        // - two of which share the same secret key but NOT the same secret reference (ALIAS_1 and ALIAS_3 both use KEY_1 but different secret refs)
        // - one of which is completely distinct (ALIAS_5)
        mount_manager.add_secret_alias(ALIAS_1, REF_1, KEY_1);
        mount_manager.add_secret_data(REF_1, KEY_1, DATA_1);
        mount_manager.add_secret_alias(ALIAS_2, REF_1, KEY_2);
        mount_manager.add_secret_data(REF_1, KEY_2, DATA_2);
        mount_manager.add_secret_alias(ALIAS_3, REF_2, KEY_1);
        mount_manager.add_secret_data(REF_2, KEY_1, DATA_3);
        mount_manager.add_secret_alias(ALIAS_4, REF_1, KEY_1);
        mount_manager.add_secret_alias(ALIAS_5, REF_3, KEY_3);
        mount_manager.add_secret_data(REF_3, KEY_3, DATA_4);

        // Create the Secrets struct and get the individual Secret structs.
        let secrets = Secrets::new(
            mount_manager.metadata_path().to_path_buf(),
            mount_manager.data_path().to_path_buf(),
            AGGREGATION_WINDOW,
        )
        .unwrap();
        let mut secret1 = secrets.get_secret(ALIAS_1).unwrap();
        let mut secret2 = secrets.get_secret(ALIAS_2).unwrap();
        let mut secret3 = secrets.get_secret(ALIAS_3).unwrap();
        let mut secret4 = secrets.get_secret(ALIAS_4).unwrap();
        let mut secret5 = secrets.get_secret(ALIAS_5).unwrap();

        // All secrets are available immediately as the secret files already exist.
        assert!(secret1.is_available());
        assert!(secret2.is_available());
        assert!(secret3.is_available());
        assert!(secret4.is_available());
        assert!(secret5.is_available());

        // Secret values are the expected ones
        assert_eq!(secret1.value().await.unwrap(), DATA_1);
        assert_eq!(secret2.value().await.unwrap(), DATA_2);
        assert_eq!(secret3.value().await.unwrap(), DATA_3);
        assert_eq!(secret4.value().await.unwrap(), DATA_1);
        assert_eq!(secret5.value().await.unwrap(), DATA_4);

        // Update secret data in place, with only relevant secrets being updated.
        mount_manager.update_secret_data(REF_1, KEY_1, DATA_5);

        // Secret 1 and Secret 4 should have their values updated, the others should remain the same.
        // These updates should be available immeidately without any kind of wait for aggregation.
        // All secrets should remain available.
        assert!(secret1.is_available());
        assert!(secret2.is_available());
        assert!(secret3.is_available());
        assert!(secret4.is_available());
        assert!(secret5.is_available());
        assert_eq!(secret1.value().await.unwrap(), DATA_5);
        assert_eq!(secret2.value().await.unwrap(), DATA_2);
        assert_eq!(secret3.value().await.unwrap(), DATA_3);
        assert_eq!(secret4.value().await.unwrap(), DATA_5);
        assert_eq!(secret5.value().await.unwrap(), DATA_4);

        // Update more secret data in place (ALIAS_2 / secret 2)
        mount_manager.update_secret_data(REF_1, KEY_2, DATA_6);

        // Secret 2 should have its value updated, the others should remain the same.
        // These updates should be available immeidately without any kind of wait for aggregation.
        // All secrets should remain available.
        assert!(secret1.is_available());
        assert!(secret2.is_available());
        assert!(secret3.is_available());
        assert!(secret4.is_available());
        assert!(secret5.is_available());
        assert_eq!(secret1.value().await.unwrap(), DATA_5);
        assert_eq!(secret2.value().await.unwrap(), DATA_6);
        assert_eq!(secret3.value().await.unwrap(), DATA_3);
        assert_eq!(secret4.value().await.unwrap(), DATA_5);
        assert_eq!(secret5.value().await.unwrap(), DATA_4);

        // Update more secret data in place (ALIAS_3 / secret 3)
        mount_manager.update_secret_data(REF_2, KEY_1, DATA_7);

        // Secret 3 should have its value updated, the others should remain the same.
        // These updates should be available immeidately without any kind of wait for aggregation.
        // All secrets should remain available.
        assert!(secret1.is_available());
        assert!(secret2.is_available());
        assert!(secret3.is_available());
        assert!(secret4.is_available());
        assert!(secret5.is_available());
        assert_eq!(secret1.value().await.unwrap(), DATA_5);
        assert_eq!(secret2.value().await.unwrap(), DATA_6);
        assert_eq!(secret3.value().await.unwrap(), DATA_7);
        assert_eq!(secret4.value().await.unwrap(), DATA_5);
        assert_eq!(secret5.value().await.unwrap(), DATA_4);

        // Update more secert data in place (ALIAS_5 / secret 5)
        mount_manager.update_secret_data(REF_3, KEY_3, DATA_8);

        // Secret 5 should have its value updated, the others should remain the same.
        // These updates should be available immeidately without any kind of wait for aggregation.
        // All secrets should remain available.
        assert!(secret1.is_available());
        assert!(secret2.is_available());
        assert!(secret3.is_available());
        assert!(secret4.is_available());
        assert!(secret5.is_available());
        assert_eq!(secret1.value().await.unwrap(), DATA_5);
        assert_eq!(secret2.value().await.unwrap(), DATA_6);
        assert_eq!(secret3.value().await.unwrap(), DATA_7);
        assert_eq!(secret4.value().await.unwrap(), DATA_5);
        assert_eq!(secret5.value().await.unwrap(), DATA_8);

        // Update all secret data in place
        mount_manager.update_secret_data(REF_1, KEY_1, DATA_1);
        mount_manager.update_secret_data(REF_1, KEY_2, DATA_2);
        mount_manager.update_secret_data(REF_2, KEY_1, DATA_3);
        mount_manager.update_secret_data(REF_3, KEY_3, DATA_4);

        // All secrets should have their values updated back to the original ones.
        // (Demonstrate that multiple updates can be handled at once)
        // These updates should be available immeidately without any kind of wait for aggregation.
        // All secrets should remain available.
        assert!(secret1.is_available());
        assert!(secret2.is_available());
        assert!(secret3.is_available());
        assert!(secret4.is_available());
        assert!(secret5.is_available());
        assert_eq!(secret1.value().await.unwrap(), DATA_1);
        assert_eq!(secret2.value().await.unwrap(), DATA_2);
        assert_eq!(secret3.value().await.unwrap(), DATA_3);
        assert_eq!(secret4.value().await.unwrap(), DATA_1);
        assert_eq!(secret5.value().await.unwrap(), DATA_4);
    }

    /// Here we test the timing of new data availability and notifications when updating secret data in place
    #[tokio::test]
    async fn update_secret_data_in_place_notification_timing() {
        let standard_manager = StandardSecretMountManager::new();
        update_secret_data_in_place_notification_timing_logic(standard_manager).await;

        let sync_manager = SecretSyncMountManager::new();
        update_secret_data_in_place_notification_timing_logic(sync_manager).await;
    }

    async fn update_secret_data_in_place_notification_timing_logic(mount_manager: impl SecretMountManager) {
        // Use a longer aggregation window in this test to make sure that updates are not processed immediately
        const AGGREGATION_WINDOW: Duration = Duration::from_millis(500);

        // Initialize a secret alias on disk
        mount_manager.add_secret_alias(ALIAS_1, REF_1, KEY_1);
        mount_manager.add_secret_data(REF_1, KEY_1, DATA_1);

        // Create the Secrets struct and get two copies of the Secret struct
        let secrets = Secrets::new(
            mount_manager.metadata_path().to_path_buf(),
            mount_manager.data_path().to_path_buf(),
            AGGREGATION_WINDOW,
        )
        .unwrap();
        let mut secret1_c1 = secrets.get_secret(ALIAS_1).unwrap();
        let mut secret1_c2 = secrets.get_secret(ALIAS_1).unwrap();

        // Update the secret data in place, but do not wait for the aggregation window to pass.
        mount_manager.update_secret_data(REF_1, KEY_1, DATA_2);
        let t0 = Instant::now();

        let (t1, t2) = tokio::time::timeout(Duration::from_secs(1), async {
            tokio::join!(
                async move {
                    // The value can be retrieved immediately with both `value` and `value_if_available`,
                    // and should be the updated value.
                    assert_eq!(
                        secret1_c1.value_if_available().unwrap(),
                        Some(DATA_2.to_string())
                    );
                    assert_eq!(secret1_c1.value().await.unwrap(), DATA_2);
                    let retrieval_time = Instant::now();
                    // Despite the value being retrieved early, the notification will still be received after
                    // the aggregation window.
                    secret1_c1.changed().await;
                    retrieval_time
                },
                async move {
                    // The changed notification will not be received until after the aggregation window passes.
                    secret1_c2.changed().await;
                    let notification_time = Instant::now();
                    // But the value will still be the updated one
                    assert_eq!(
                        secret1_c2.value_if_available().unwrap(),
                        Some(DATA_2.to_string())
                    );
                    assert_eq!(secret1_c2.value().await.unwrap(), DATA_2);
                    notification_time
                }
            )
        })
        .await
        .expect("test timed out");
        // The value update was available immediately, but the change notification was not received
        // until after the aggregation window, so t1 should be before t2.
        assert!(t1 < t2);
        assert!(t1 < t0 + AGGREGATION_WINDOW);
        assert!(t2 >= t0 + AGGREGATION_WINDOW);
    }

    #[tokio::test]
    async fn update_secret_alias() {
        let standard_manager = StandardSecretMountManager::new();
        update_secret_alias_logic(standard_manager).await;

        let sync_manager = SecretSyncMountManager::new();
        update_secret_alias_logic(sync_manager).await;
    }

    async fn update_secret_alias_logic(mount_manager: impl SecretMountManager) {
        // Use a short aggregation window to make test run quickly
        const AGGREGATION_WINDOW: Duration = Duration::from_millis(100);
        // Use a slightly longer wait time to ensure there's no test race condition
        const WAIT_FOR_UPDATE: Duration = Duration::from_millis(150);

        // Initialize five secret aliases on disk:
        // - two of which share the same secret reference (ALIAS_1 and ALIAS_2 both use REF_1)
        // - two of which share the same secret reference AND same secret key (i.e. same data file) (ALIAS_1 and ALIAS_4 both point at REF_1/KEY_1)
        // - two of which share the same secret key but NOT the same secret reference (ALIAS_1 and ALIAS_3 both use KEY_1 but different secret refs)
        // - one of which is completely distinct (ALIAS_5)
        mount_manager.add_secret_alias(ALIAS_1, REF_1, KEY_1);
        mount_manager.add_secret_data(REF_1, KEY_1, DATA_1);
        mount_manager.add_secret_alias(ALIAS_2, REF_1, KEY_2);
        mount_manager.add_secret_data(REF_1, KEY_2, DATA_2);
        mount_manager.add_secret_alias(ALIAS_3, REF_2, KEY_1);
        mount_manager.add_secret_data(REF_2, KEY_1, DATA_3);
        mount_manager.add_secret_alias(ALIAS_4, REF_1, KEY_1);
        mount_manager.add_secret_alias(ALIAS_5, REF_3, KEY_3);
        mount_manager.add_secret_data(REF_3, KEY_3, DATA_4);

        // Create the Secrets struct and get the individual Secret structs.
        let secrets = Secrets::new(
            mount_manager.metadata_path().to_path_buf(),
            mount_manager.data_path().to_path_buf(),
            AGGREGATION_WINDOW,
        )
        .unwrap();
        let mut secret1 = secrets.get_secret(ALIAS_1).unwrap();
        let mut secret2 = secrets.get_secret(ALIAS_2).unwrap();
        let mut secret3 = secrets.get_secret(ALIAS_3).unwrap();
        let mut secret4 = secrets.get_secret(ALIAS_4).unwrap();
        let mut secret5 = secrets.get_secret(ALIAS_5).unwrap();

        // All secrets are available immediately as the secret files already exist.
        assert!(secret1.is_available());
        assert!(secret2.is_available());
        assert!(secret3.is_available());
        assert!(secret4.is_available());
        assert!(secret5.is_available());

        // Secret values are the expected ones
        assert_eq!(secret1.value().await.unwrap(), DATA_1);
        assert_eq!(secret2.value().await.unwrap(), DATA_2);
        assert_eq!(secret3.value().await.unwrap(), DATA_3);
        assert_eq!(secret4.value().await.unwrap(), DATA_1);
        assert_eq!(secret5.value().await.unwrap(), DATA_4);

        // Update ALIAS_1 to point at REF_2/KEY_1 instead of REF_1/KEY_1.
        // This should be reflected immediately after the aggregation window passes.
        mount_manager.update_secret_alias(ALIAS_1, REF_2, KEY_1);
        tokio::time::sleep(WAIT_FOR_UPDATE).await; // Wait for update to propagate

        // ALIAS_1 / secret 1 should have its value updated to DATA_3, while all other secrets should remain the same.
        // This means that secret 1 now should have the same value as secret 3 instead of secrert 4
        // All secrets should remain available.
        assert!(secret1.is_available());
        assert!(secret2.is_available());
        assert!(secret3.is_available());
        assert!(secret4.is_available());
        assert!(secret5.is_available());
        assert_eq!(secret1.value().await.unwrap(), DATA_3);
        assert_eq!(secret2.value().await.unwrap(), DATA_2);
        assert_eq!(secret3.value().await.unwrap(), DATA_3);
        assert_eq!(secret4.value().await.unwrap(), DATA_1);
        assert_eq!(secret5.value().await.unwrap(), DATA_4);

        // Swap ALIAS_2 and ALIAS_4 secret keys (they already share the same secret ref)
        mount_manager.update_secret_alias(ALIAS_2, REF_1, KEY_1);
        mount_manager.update_secret_alias(ALIAS_4, REF_1, KEY_2);
        tokio::time::sleep(WAIT_FOR_UPDATE).await; // Wait for update to propagate

        // The data was swapped.
        // All secrets should remain available.
        assert!(secret1.is_available());
        assert!(secret2.is_available());
        assert!(secret3.is_available());
        assert!(secret4.is_available());
        assert!(secret5.is_available());
        assert_eq!(secret1.value().await.unwrap(), DATA_3);
        assert_eq!(secret2.value().await.unwrap(), DATA_1);
        assert_eq!(secret3.value().await.unwrap(), DATA_3);
        assert_eq!(secret4.value().await.unwrap(), DATA_2);
        assert_eq!(secret5.value().await.unwrap(), DATA_4);

        // Swap ALIAS_3 and ALIAS_5 to point at each other's secret refs and keys (they share neither key nor ref)
        // This should be reflected immediately after the aggregation window passes.
        mount_manager.update_secret_alias(ALIAS_3, REF_3, KEY_3);
        mount_manager.update_secret_alias(ALIAS_5, REF_2, KEY_1);
        tokio::time::sleep(WAIT_FOR_UPDATE).await; // Wait for update to propagate

        // The data was swapped.
        // All secrets should remain available.
        assert!(secret1.is_available());
        assert!(secret2.is_available());
        assert!(secret3.is_available());
        assert!(secret4.is_available());
        assert!(secret5.is_available());
        assert_eq!(secret1.value().await.unwrap(), DATA_3);
        assert_eq!(secret2.value().await.unwrap(), DATA_1);
        assert_eq!(secret3.value().await.unwrap(), DATA_4);
        assert_eq!(secret4.value().await.unwrap(), DATA_2);
        assert_eq!(secret5.value().await.unwrap(), DATA_3);

        // Add new secret data and THEN redirect ALIAS_5/secret 5 to it.
        // Do not remove the old data, as ALIAS_1/secret 1 is still using it.
        mount_manager.add_secret_data(REF_3, KEY_2, DATA_5);
        mount_manager.update_secret_alias(ALIAS_5, REF_3, KEY_2);
        tokio::time::sleep(WAIT_FOR_UPDATE).await; // Wait for update to propagate

        // Secret 5 should now have the new data.
        // All secrets are available (because both the data and the alias were updated)
        assert!(secret1.is_available());
        assert!(secret2.is_available());
        assert!(secret3.is_available());
        assert!(secret4.is_available());
        assert!(secret5.is_available());
        assert_eq!(secret1.value().await.unwrap(), DATA_3);
        assert_eq!(secret2.value().await.unwrap(), DATA_1);
        assert_eq!(secret3.value().await.unwrap(), DATA_4);
        assert_eq!(secret4.value().await.unwrap(), DATA_2);
        assert_eq!(secret5.value().await.unwrap(), DATA_5);

        // Update ALIAS_4/secret 4 to point at a currently non-existent new secret
        mount_manager.update_secret_alias(ALIAS_4, REF_2, KEY_2);
        tokio::time::sleep(WAIT_FOR_UPDATE).await; // Wait for update to propagate

        // Secret 4 is now unavailable, while all other secrets remain the same.
        assert!(secret1.is_available());
        assert!(secret2.is_available());
        assert!(secret3.is_available());
        assert!(!secret4.is_available());
        assert!(secret5.is_available());
        assert_eq!(secret1.value().await.unwrap(), DATA_3);
        assert_eq!(secret2.value().await.unwrap(), DATA_1);
        assert_eq!(secret3.value().await.unwrap(), DATA_4);
        assert_eq!(secret5.value().await.unwrap(), DATA_5);

        // Add the new secret data, which should make secret 4 available with the new data after aggregation window.
        // Remove the unused old secret data.
        mount_manager.add_secret_data(REF_2, KEY_2, DATA_6);
        mount_manager.remove_secret_data(REF_1, KEY_2);
        tokio::time::sleep(WAIT_FOR_UPDATE).await; // Wait for update to propagate

        // Secret 4 should now be available with the new data, while all other secrets remain the same.
        assert!(secret1.is_available());
        assert!(secret2.is_available());
        assert!(secret3.is_available());
        assert!(secret4.is_available());
        assert!(secret5.is_available());
        assert_eq!(secret1.value().await.unwrap(), DATA_3);
        assert_eq!(secret2.value().await.unwrap(), DATA_1);
        assert_eq!(secret3.value().await.unwrap(), DATA_4);
        assert_eq!(secret4.value().await.unwrap(), DATA_6);
        assert_eq!(secret5.value().await.unwrap(), DATA_5);

        // Remove a secret data file, and the corresponding secret becomes unavailable
        mount_manager.remove_secret_data(REF_1, KEY_1);
        tokio::time::sleep(WAIT_FOR_UPDATE).await; // Wait for update to propagate

        // Secret 2 becomes unavailable, while all other secrets remain the same.
        assert!(secret1.is_available());
        assert!(!secret2.is_available());
        assert!(secret3.is_available());
        assert!(secret4.is_available());
        assert!(secret5.is_available());
        assert_eq!(secret1.value().await.unwrap(), DATA_3);
        assert_eq!(secret3.value().await.unwrap(), DATA_4);
        assert_eq!(secret4.value().await.unwrap(), DATA_6);
        assert_eq!(secret5.value().await.unwrap(), DATA_5);

        // Reset all secrets back to their original configuration and restore the removed data files,
        // removing the newly added ones.
        // (Demonstrate that multiple updates can be handled at once)
        mount_manager.add_secret_data(REF_1, KEY_1, DATA_1);
        mount_manager.add_secret_data(REF_1, KEY_2, DATA_2);
        mount_manager.remove_secret_data(REF_2, KEY_2);
        mount_manager.update_secret_alias(ALIAS_1, REF_1, KEY_1);
        mount_manager.update_secret_alias(ALIAS_2, REF_1, KEY_2);
        mount_manager.update_secret_alias(ALIAS_3, REF_2, KEY_1);
        mount_manager.update_secret_alias(ALIAS_4, REF_1, KEY_1);
        mount_manager.update_secret_alias(ALIAS_5, REF_3, KEY_3);
        tokio::time::sleep(WAIT_FOR_UPDATE).await; // Wait for update to propagate

        // All secrets should have their values updated back to the original ones.
        assert!(secret1.is_available());
        assert!(secret2.is_available());
        assert!(secret3.is_available());
        assert!(secret4.is_available());
        assert!(secret5.is_available());
        assert_eq!(secret1.value().await.unwrap(), DATA_1);
        assert_eq!(secret2.value().await.unwrap(), DATA_2);
        assert_eq!(secret3.value().await.unwrap(), DATA_3);
        assert_eq!(secret4.value().await.unwrap(), DATA_1);
        assert_eq!(secret5.value().await.unwrap(), DATA_4);
    }

    /// Here we test the timing of new data availability and notifications when updating secret data in place
    #[tokio::test]
    async fn update_secret_alias_notification_timing() {
        let _ = env_logger::builder()
            .is_test(true)
            .filter_module("notify::inotify", log::LevelFilter::Off)
            .try_init();

        // let standard_manager = StandardSecretMountManager::new();
        // update_secret_data_in_place_notification_timing_logic(standard_manager).await;

        let sync_manager = SecretSyncMountManager::new();
        update_secret_alias_notification_timing_logic(sync_manager).await;
    }

    async fn update_secret_alias_notification_timing_logic(mount_manager: impl SecretMountManager) {
        // Use a longer aggregation window in this test to make sure that updates are not processed immediately
        const AGGREGATION_WINDOW: Duration = Duration::from_millis(500);
        //const MANUAL_WAIT: Duration = Duration::from_millis(150);

        // Initialize a secret alias on disk
        mount_manager.add_secret_alias(ALIAS_1, REF_1, KEY_1);
        mount_manager.add_secret_data(REF_1, KEY_1, DATA_1);

        // Create the Secrets struct
        let secrets = Secrets::new(
            mount_manager.metadata_path().to_path_buf(),
            mount_manager.data_path().to_path_buf(),
            AGGREGATION_WINDOW,
        )
        .unwrap();
        // Get two copies of the Secret struct for ALIAS_1 for testing timing.
        let mut secret1_c1 = secrets.get_secret(ALIAS_1).unwrap();
        let mut secret1_c2 = secrets.get_secret(ALIAS_1).unwrap();

        // Add new secret data and THEN redirect ALIAS_1 to use it
        // Do not delete the existing secret data that becomes unused.
        mount_manager.add_secret_data(REF_1, KEY_2, DATA_2);
        mount_manager.update_secret_alias(ALIAS_1, REF_1, KEY_2);
        let t0 = Instant::now();

        // Data is available because nothing was deleted
        assert!(secret1_c1.is_available());
        assert!(secret1_c2.is_available());

        let (t1, t2) = tokio::time::timeout(Duration::from_secs(1), async {
            tokio::join!(
                async move {
                    // The value can be retrieved immediately with both `value` and `value_if_available`,
                    // but will be the old value until the aggregation window has passed
                    assert_eq!(secret1_c1.value_if_available().unwrap(), Some(DATA_1.to_string()));
                    assert_eq!(secret1_c1.value().await.unwrap(), DATA_1);
                    let retrieval_time = Instant::now();
                    // Despite the value being retrievable early, the notification will still be received after
                    // the aggregation window.
                    secret1_c1.changed().await;
                    retrieval_time
                },
                async move {
                    // The changed notification will not be received until after the aggregation window passes.
                    secret1_c2.changed().await;
                    let notification_time = Instant::now();
                    // But the value will now be the updated one
                    assert_eq!(secret1_c2.value_if_available().unwrap(), Some(DATA_2.to_string()));
                    assert_eq!(secret1_c2.value().await.unwrap(), DATA_2);
                    notification_time
                }
            )
        }).await.expect("test timed out");
        // The old value was only available prior to the change notification.
        assert!(t1 < t2);
        assert!(t1 < t0 + AGGREGATION_WINDOW);
        assert!(t2 >= t0 + AGGREGATION_WINDOW);

        // Get more copies of the secret struct
        let mut secret1_c3 = secrets.get_secret(ALIAS_1).unwrap();
        let mut secret1_c4 = secrets.get_secret(ALIAS_1).unwrap();

        // // This time, do delete the existing secret data that becomes unused 
        mount_manager.add_secret_data(REF_1, KEY_3, DATA_3);
        mount_manager.remove_secret_data(REF_1, KEY_2);
        mount_manager.update_secret_alias(ALIAS_1, REF_1, KEY_3);
        let t0 = Instant::now();

        // No value is currently retrievable due to the old data being deleted
        assert!(!secret1_c3.is_available());
        assert!(!secret1_c4.is_available());
        assert_eq!(secret1_c3.value_if_available().unwrap(), None);
        assert_eq!(secret1_c4.value_if_available().unwrap(), None);

        let (t1, t2) = tokio::time::timeout(Duration::from_secs(1), async {
            tokio::join!(
                async move {
                    // The new value will be retrieved as soon as it's available if waited on
                    assert_eq!(secret1_c3.value().await.unwrap(), DATA_3);
                    assert_eq!(secret1_c3.value_if_available().unwrap(), Some(DATA_3.to_string()));
                    Instant::now()
                },
                async move {
                    // The changed notification will not be received until after the aggregation window passes.
                    secret1_c4.changed().await;
                    let notification_time = Instant::now();
                    // But the value will now be the updated one
                    assert_eq!(secret1_c4.value_if_available().unwrap(), Some(DATA_3.to_string()));
                    assert_eq!(secret1_c4.value().await.unwrap(), DATA_3);
                    notification_time
                }
            )
        }).await.expect("test timed out");
        // Both times returned are roughly equivalent, and both are after the aggregation window,
        // i.e. the new value is not available at all until the aggregation window has passed.
        assert!(t1 > t0 + AGGREGATION_WINDOW);
        assert!(t2 > t0 + AGGREGATION_WINDOW);

        // Get two more copies of the secret struct
        let mut secret1_c5 = secrets.get_secret(ALIAS_1).unwrap();
        let mut secret1_c6 = secrets.get_secret(ALIAS_1).unwrap();

        log::warn!("BEFORE");

        // There will be no ability to get values at all if the alias is updated to point at non-existent data,
        // until that data is added and the aggregation window has passed.
        mount_manager.update_secret_alias(ALIAS_1, REF_2, KEY_2);
        mount_manager.remove_secret_data(REF_1, KEY_3);
        let t0 = Instant::now();

        // No value is currently retrievable due to the alias pointing at non-existent data
        assert!(!secret1_c5.is_available());
        assert!(!secret1_c6.is_available());
        assert_eq!(secret1_c5.value_if_available().unwrap(), None);
        assert_eq!(secret1_c6.value_if_available().unwrap(), None);

        let mount_manager_c = mount_manager.clone();
        let (t1, t2, t3) = tokio::time::timeout(Duration::from_secs(5), async {
            tokio::join!(
                async move {
                    // Wait for the aggregation window to pass, then add the new data.
                    tokio::time::sleep(AGGREGATION_WINDOW).await;
                    mount_manager_c.add_secret_data(REF_2, KEY_2, DATA_4);
                    let update_time = Instant::now();
                    log::warn!("task 1 done: {update_time:?}");
                    update_time
                },
                async move {
                    // The new value will be retrieved as soon as it's available if waited on
                    assert_eq!(secret1_c5.value().await.unwrap(), DATA_4);
                    log::warn!("tag 1");
                    assert_eq!(secret1_c5.value_if_available().unwrap(), Some(DATA_4.to_string()));
                    log::warn!("task 2 done");
                    Instant::now()
                },
                async move {
                    // The changed notification will not be received until after the aggregation window passes.
                    secret1_c6.changed().await;
                    let notification_time = Instant::now();
                    // But the value will now be the updated one
                    assert_eq!(secret1_c6.value_if_available().unwrap(), Some(DATA_4.to_string()));
                    log::warn!("tag 2");
                    assert_eq!(secret1_c6.value().await.unwrap(), DATA_4);
                    log::warn!("task 3 done");
                    notification_time
                },
            )
        }).await.expect("test timed out");
        // Values cannot be retrieved until after the aggregation window after the update.
        // T2 and T3 are roughly equivalent.
        assert!(t1 >= t0 + AGGREGATION_WINDOW);
        assert!(t2 >= t1 + AGGREGATION_WINDOW);
        assert!(t3 >= t1 + AGGREGATION_WINDOW);

        // TODO: why is the above timing out?

        // TODO: pure deletion timing test. Or does that belong in the data update?
        // Should that result in change notification?
        // what about two tier updates -> alias then file, shouldn't that notify twice?
    }





    // #[tokio::test]
    // async fn repro() {
    //     let _ = env_logger::builder().is_test(true).try_init();
    //     let mount_manager = SecretSyncMountManager::new();
        
    //     // Use a longer aggregation window in this test to make sure that updates are not processed immediately
    //     const AGGREGATION_WINDOW: Duration = Duration::from_millis(500);

    //     // Initialize a secret alias on disk
    //     mount_manager.add_secret_alias(ALIAS_1, REF_1, KEY_1);
    //     // mount_manager.add_secret_data(REF_1, KEY_1, DATA_1);

    //     // Create the Secrets struct
    //     let secrets = Secrets::new(
    //         mount_manager.metadata_path().to_path_buf(),
    //         mount_manager.data_path().to_path_buf(),
    //         AGGREGATION_WINDOW,
    //     )
    //     .unwrap();

    //     // // // This time, do delete the existing secret data that becomes unused 
    //     // mount_manager.add_secret_data(REF_1, KEY_3, DATA_3);

    //     // Get two more copies of the secret struct
    //     let mut secret1_c5 = secrets.get_secret(ALIAS_1).unwrap();
    //     let mut secret1_c6 = secrets.get_secret(ALIAS_1).unwrap();

    //     // There will be no ability to get values at all if the alias is updated to point at non-existent data,
    //     // until that data is added and the aggregation window has passed.
    //     mount_manager.update_secret_alias(ALIAS_1, REF_2, KEY_2);
    //     //mount_manager.remove_secret_data(REF_1, KEY_3);
    //     let t0 = Instant::now();

    //     // No value is currently retrievable due to the alias pointing at non-existent data
    //     assert!(!secret1_c5.is_available());
    //     assert!(!secret1_c6.is_available());
    //     assert_eq!(secret1_c5.value_if_available().unwrap(), None);
    //     assert_eq!(secret1_c6.value_if_available().unwrap(), None);

    //     log::warn!("BEFORE");

    //     // tokio::time::sleep(AGGREGATION_WINDOW).await;
    //     // mount_manager.add_secret_data(REF_2, KEY_2, DATA_4);

    //     let mm_c = mount_manager.clone();

    //     let (t1, t2, t3) = tokio::time::timeout(Duration::from_secs(3), async {
    //         tokio::join!(
    //             async move {
    //                 // Wait for the aggregation window to pass, then add the new data.
    //                 tokio::time::sleep(AGGREGATION_WINDOW).await;
    //                 mm_c.add_secret_data(REF_2, KEY_2, DATA_4);
    //                 let update_time = Instant::now();
    //                 log::warn!("task 1 done");
    //                 update_time
    //             },
    //             async move {
    //                 // The new value will be retrieved as soon as it's available if waited on
    //                 assert_eq!(secret1_c5.value().await.unwrap(), DATA_4);
    //                 assert_eq!(secret1_c5.value_if_available().unwrap(), Some(DATA_4.to_string()));
    //                 log::warn!("task 2 done");
    //                 Instant::now()
    //             },
    //             async move {
    //                 // The changed notification will not be received until after the aggregation window passes.
    //                 secret1_c6.changed().await;
    //                 let notification_time = Instant::now();
    //                 // But the value will now be the updated one
    //                 assert_eq!(secret1_c6.value_if_available().unwrap(), Some(DATA_4.to_string()));
    //                 assert_eq!(secret1_c6.value().await.unwrap(), DATA_4);
    //                 log::warn!("task 3 done");
    //                 notification_time
    //             },
    //         )
    //     }).await.map_err(|e| {
    //         log::warn!("TIMEOUT");
    //         e
    //     }).expect("test timed out");
    //     // Values cannot be retrieved until after the aggregation window after the update.
    //     // T2 and T3 are roughly equivalent.
    //     assert!(t1 >= t0 + AGGREGATION_WINDOW);
    //     assert!(t2 >= t1 + AGGREGATION_WINDOW);
    //     assert!(t3 >= t1 + AGGREGATION_WINDOW);

    //     // TODO: why is the above timing out?

    //     // TODO: pure deletion timing test. Or does that belong in the data update?
    //     // Should that result in change notification?
    //     // what about two tier updates -> alias then file, shouldn't that notify twice?
    // }
}


// TODO: Aggregation of updates
// TODO: Targeted notifications
// TODO: Change notifcations persistance