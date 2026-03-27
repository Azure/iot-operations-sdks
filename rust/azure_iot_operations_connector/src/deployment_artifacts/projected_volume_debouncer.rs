// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

//! A debouncer for Kubernetes projected volume mounts.
//!
//! Wraps [`notify_debouncer_full`] and detects changes via the atomic `..data` symlink
//! swap that Kubernetes performs when updating projected volumes (dir1s, ConfigMaps, etc.).
//!
//! Instead of exposing raw filesystem events (which include internal K8S plumbing like
//! `..data`, `..data_tmp`, and timestamped snapshot directories), this debouncer produces
//! synthetic events with clean relative paths by hashing file contents with SHA-256 and
//! diffing snapshots before and after each swap.
//!
//! # Why there is no user-configurable debounce window
//!
//! The kubelet already batches all projected volume changes into a single atomic symlink
//! swap per sync cycle (default: 60 seconds via `--sync-frequency`). Multiple dir1 or
//! ConfigMap key updates between sync ticks are delivered as one swap. Consecutive swaps
//! are therefore separated by tens of seconds at minimum, so there is nothing meaningful
//! for the caller to aggregate. The internal debounce window only needs to be long enough
//! to coalesce the ~20-30 raw inotify events produced by a single swap (~1-2ms of
//! filesystem activity) into one callback invocation.

// TODO: Remove after using for Secrets
#![allow(unused)]

use std::collections::HashMap;
use std::fs;
use std::path::{Path, PathBuf};
use std::sync::Mutex;
use std::time::{Duration, Instant};

use notify::event::{CreateKind, DataChange, ModifyKind, RemoveKind};
use notify::{EventKind, RecommendedWatcher};
use notify_debouncer_full::{
    DebounceEventResult, DebouncedEvent, Debouncer, RecommendedCache, new_debouncer,
};
use sha2::{Digest, Sha256};

/// Internal debounce window for coalescing raw inotify events from a single
/// Kubernetes atomic symlink swap. A single swap produces ~20-30 events over
/// ~1-2ms; one second is a generous buffer that adds negligible latency given
/// the kubelet's sync cycle (default 60s).
const DEBOUNCE_WINDOW: Duration = Duration::from_secs(1);

/// Error from the [`ProjectedVolumeDebouncer`].
#[derive(Debug, thiserror::Error)]
pub enum ProjectedVolumeError {
    /// An error from the underlying filesystem watcher.
    #[error(transparent)]
    Notify(#[from] notify::Error),
    /// An I/O error occurred while scanning the projected volume directory.
    #[error("failed to snapshot projected volume: {0}")]
    Snapshot(std::io::Error),
}

/// A synthetic filesystem event representing a change in a projected volume.
///
/// Unlike raw [`notify`] events, the `path` field contains a clean relative path
/// within the projected volume (e.g., `my-dir1/my-key`), with all Kubernetes
/// internal entries filtered out.
///
/// # Event kinds
///
/// Only the following [`EventKind`] variants are emitted:
/// - [`EventKind::Create(CreateKind::File)`] — a file was added.
/// - [`EventKind::Create(CreateKind::Folder)`] — a directory was added.
/// - [`EventKind::Modify(ModifyKind::Data(DataChange::Any))`] — a file's content
///   changed (detected via SHA-256). This is the **only** `Modify` sub-variant
///   emitted; in particular, `Modify(Name(_))` (renames) are never produced.
/// - [`EventKind::Remove(RemoveKind::File)`] — a file was removed.
/// - [`EventKind::Remove(RemoveKind::Folder)`] — a directory was removed.
///
/// **Renames (`Modify(Name(_))`) are never emitted.** Kubernetes dir1s and
/// ConfigMaps have no concept of renaming a key — deleting a key and adding a
/// new one are independent operations even if the content is identical.
/// Accordingly, such changes are reported as a [`Remove`](EventKind::Remove)
/// followed by a [`Create`](EventKind::Create).
///
/// **Metadata changes (`Modify(Metadata(_))`) are never emitted.** Permissions
/// and ownership in a projected volume are set by the kubelet when it writes
/// the volume and do not change independently of content. File timestamps
/// always differ across updates regardless of whether content changed, so
/// reporting them would be noise rather than a meaningful signal.
#[derive(Debug, Clone)]
pub struct ProjectedVolumeEvent {
    /// The kind of change detected.
    pub kind: EventKind,
    /// The relative path of the affected file within the projected volume.
    pub path: PathBuf,
    /// When the change was detected.
    pub time: Instant,
}

/// Result type passed to the handler closure.
pub type ProjectedVolumeEventResult = Result<Vec<ProjectedVolumeEvent>, ProjectedVolumeError>;

/// A debouncer for Kubernetes projected volume mounts.
///
/// Monitors a projected volume directory and produces clean, synthetic filesystem events
/// when Kubernetes performs an atomic symlink swap to update the volume contents.
///
/// # How it works
///
/// 1. On construction, takes a SHA-256 snapshot of all user-visible files.
/// 2. Uses [`notify_debouncer_full`] to watch for filesystem events.
/// 3. When the `..data` symlink swap is detected, re-scans the directory.
/// 4. Diffs the new snapshot against the previous one.
/// 5. Calls the user's handler with synthetic `Created`, `Modified`, and `Removed` events.
///
/// # Example
///
/// ```ignore
/// use std::path::PathBuf;
/// use azure_iot_operations_connector::deployment_artifacts::projected_volume_debouncer::{
///     ProjectedVolumeDebouncer, ProjectedVolumeEventResult,
/// };
///
/// let _debouncer = ProjectedVolumeDebouncer::new(
///     PathBuf::from("/etc/akri/dir1s/connector_dir1s"),
///     |result: ProjectedVolumeEventResult| {
///         match result {
///             Ok(events) => {
///                 for event in events {
///                     println!("{:?}: {:?}", event.kind, event.path);
///                 }
///             }
///             Err(e) => eprintln!("Error: {e}"),
///         }
///     },
/// ).expect("failed to create debouncer");
/// ```
pub struct ProjectedVolumeDebouncer {
    _debouncer: Debouncer<RecommendedWatcher, RecommendedCache>,
}

impl ProjectedVolumeDebouncer {
    /// Creates a new [`ProjectedVolumeDebouncer`].
    ///
    /// Immediately snapshots all user-visible files under `root` as the baseline.
    /// The `event_handler` closure will be called on a background thread whenever a
    /// projected volume update is detected.
    ///
    /// # Arguments
    ///
    /// * `root` - Path to the projected volume mount directory.
    /// * `event_handler` - Closure invoked with the result of change detection. Runs on a
    ///   background OS thread.
    ///
    /// # Errors
    ///
    /// Returns an error if the initial directory snapshot fails or the filesystem watcher
    /// cannot be created.
    pub fn new<F>(root: PathBuf, mut event_handler: F) -> Result<Self, ProjectedVolumeError>
    where
        F: FnMut(ProjectedVolumeEventResult) + Send + 'static,
    {
        let initial_snapshot = snapshot_directory(&root).map_err(ProjectedVolumeError::Snapshot)?;
        let state = Mutex::new(initial_snapshot);
        let root_for_closure = root.clone();

        let mut debouncer = new_debouncer(
            DEBOUNCE_WINDOW,
            None,
            move |res: DebounceEventResult| match res {
                Ok(events) => {
                    let Some(swap_time) = symlink_swap_time(&events) else {
                        return;
                    };
                    match snapshot_directory(&root_for_closure) {
                        Ok(new_snapshot) => {
                            let mut prev = state.lock().unwrap_or_else(|e| e.into_inner());
                            let changes = diff_snapshots(&prev, &new_snapshot, swap_time);
                            *prev = new_snapshot;
                            drop(prev);

                            if !changes.is_empty() {
                                event_handler(Ok(changes));
                            }
                        }
                        Err(e) => {
                            event_handler(Err(ProjectedVolumeError::Snapshot(e)));
                        }
                    }
                }
                Err(errors) => {
                    if let Some(e) = errors.into_iter().next() {
                        event_handler(Err(ProjectedVolumeError::Notify(e)));
                    }
                }
            },
        )?;

        debouncer.watch(root, notify::RecursiveMode::NonRecursive)?;

        Ok(Self {
            _debouncer: debouncer,
        })
    }
}

/// Returns the timestamp of the symlink swap event, if one is present in the batch.
///
/// The swap is identified by any event whose path ends with `..data` or `..data_tmp`,
/// which are the symlinks Kubernetes uses during projected volume updates.
fn symlink_swap_time(events: &[DebouncedEvent]) -> Option<Instant> {
    events
        .iter()
        .find(|e| {
            e.event.paths.iter().any(|p| {
                p.file_name()
                    .is_some_and(|name| name == "..data" || name == "..data_tmp")
            })
        })
        .map(|e| e.time)
}

/// SHA-256 hash type for file content snapshots.
type FileHash = [u8; 32];

/// An entry in a projected volume snapshot.
#[derive(Debug, Clone, PartialEq, Eq)]
enum SnapshotEntry {
    /// A regular file, identified by the SHA-256 hash of its contents.
    File(FileHash),
    /// A directory (no hash — presence is what matters).
    Directory,
}

/// Walks the projected volume directory and builds a map of relative paths to snapshot entries.
///
/// Filters out all entries starting with `..` (Kubernetes internal plumbing) and follows
/// symlinks to reach actual file content. Both files and directories are recorded.
fn snapshot_directory(root: &Path) -> Result<HashMap<PathBuf, SnapshotEntry>, std::io::Error> {
    let mut map = HashMap::new();
    snapshot_recursive(root, root, &mut map)?;
    Ok(map)
}

fn snapshot_recursive(
    root: &Path,
    current: &Path,
    map: &mut HashMap<PathBuf, SnapshotEntry>,
) -> Result<(), std::io::Error> {
    for entry in fs::read_dir(current)? {
        let entry = entry?;
        let name = entry.file_name();

        // Skip Kubernetes internal entries (..data, ..data_tmp, ..<timestamp>)
        if name.as_encoded_bytes().starts_with(b"..") {
            continue;
        }

        let path = entry.path();
        // fs::metadata follows symlinks, giving us the resolved type
        let metadata = fs::metadata(&path)?;

        if metadata.is_dir() {
            let relative = path
                .strip_prefix(root)
                .expect("path is always under root")
                .to_path_buf();
            map.insert(relative, SnapshotEntry::Directory);
            snapshot_recursive(root, &path, map)?;
        } else if metadata.is_file() {
            let contents = fs::read(&path)?;
            let hash: FileHash = Sha256::digest(&contents).into();
            let relative = path
                .strip_prefix(root)
                .expect("path is always under root")
                .to_path_buf();
            map.insert(relative, SnapshotEntry::File(hash));
        }
    }
    Ok(())
}

/// Compares two snapshots and produces synthetic events for any differences.
fn diff_snapshots(
    old: &HashMap<PathBuf, SnapshotEntry>,
    new: &HashMap<PathBuf, SnapshotEntry>,
    time: Instant,
) -> Vec<ProjectedVolumeEvent> {
    let mut events = Vec::new();

    for (path, new_entry) in new {
        match old.get(path) {
            Some(old_entry) if old_entry == new_entry => {}
            Some(SnapshotEntry::File(_)) if matches!(new_entry, SnapshotEntry::File(_)) => {
                events.push(ProjectedVolumeEvent {
                    kind: EventKind::Modify(ModifyKind::Data(DataChange::Any)),
                    path: path.clone(),
                    time,
                });
            }
            Some(_) => {
                // Type changed (file <-> dir): report remove + create
                let remove_kind = match old.get(path).unwrap() {
                    SnapshotEntry::File(_) => EventKind::Remove(RemoveKind::File),
                    SnapshotEntry::Directory => EventKind::Remove(RemoveKind::Folder),
                };
                let create_kind = match new_entry {
                    SnapshotEntry::File(_) => EventKind::Create(CreateKind::File),
                    SnapshotEntry::Directory => EventKind::Create(CreateKind::Folder),
                };
                events.push(ProjectedVolumeEvent {
                    kind: remove_kind,
                    path: path.clone(),
                    time,
                });
                events.push(ProjectedVolumeEvent {
                    kind: create_kind,
                    path: path.clone(),
                    time,
                });
            }
            None => {
                let kind = match new_entry {
                    SnapshotEntry::File(_) => EventKind::Create(CreateKind::File),
                    SnapshotEntry::Directory => EventKind::Create(CreateKind::Folder),
                };
                events.push(ProjectedVolumeEvent {
                    kind: kind,
                    path: path.clone(),
                    time,
                });
            }
        }
    }

    for (path, old_entry) in old {
        if !new.contains_key(path) {
            let kind = match old_entry {
                SnapshotEntry::File(_) => EventKind::Remove(RemoveKind::File),
                SnapshotEntry::Directory => EventKind::Remove(RemoveKind::Folder),
            };
            events.push(ProjectedVolumeEvent {
                kind: kind,
                path: path.clone(),
                time,
            });
        }
    }

    events
}

#[cfg(test)]
mod tests {
    use super::*;
    use crate::deployment_artifacts::test_utils::TempProjectedVolume;

    mod snapshot {
        use super::*;

        #[test]
        fn captures_files_and_dirs() {
            let vol = TempProjectedVolume::new("snapshot_captures");
            vol.stage_dir_create(Path::new("dir1"));
            vol.stage_file_create(Path::new("dir1/file1"), "value1");
            vol.stage_file_create(Path::new("dir1/file2"), "value2");
            vol.stage_dir_create(Path::new("dir2"));
            vol.stage_file_create(Path::new("dir2/file1"), "value3");
            vol.stage_dir_create(Path::new("dir3"));
            vol.stage_file_create(Path::new("dir3/file1"), "value4");
            vol.stage_dir_create(Path::new("dir3/subdir1"));
            vol.stage_file_create(Path::new("dir3/subdir1/file1"), "value5");
            vol.stage_dir_create(Path::new("dir3/subdir2"));
            vol.execute_update();

            let snapshot = snapshot_directory(vol.path()).unwrap();
            assert_eq!(snapshot.len(), 10); // 5 dir + 5 files
            assert!(matches!(
                snapshot.get(Path::new("dir1")),
                Some(SnapshotEntry::Directory)
            ));
            assert!(matches!(
                snapshot.get(Path::new("dir2")),
                Some(SnapshotEntry::Directory)
            ));
            assert!(matches!(
                snapshot.get(Path::new("dir3")),
                Some(SnapshotEntry::Directory)
            ));
            assert!(matches!(
                snapshot.get(Path::new("dir3/subdir1")),
                Some(SnapshotEntry::Directory)
            ));
            assert!(matches!(
                snapshot.get(Path::new("dir3/subdir2")),
                Some(SnapshotEntry::Directory)
            ));
            assert!(matches!(
                snapshot.get(Path::new("dir1/file1")),
                Some(SnapshotEntry::File(_))
            ));
            assert!(matches!(
                snapshot.get(Path::new("dir1/file2")),
                Some(SnapshotEntry::File(_))
            ));
            assert!(matches!(
                snapshot.get(Path::new("dir2/file1")),
                Some(SnapshotEntry::File(_))
            ));
            assert!(matches!(
                snapshot.get(Path::new("dir3/file1")),
                Some(SnapshotEntry::File(_))
            ));
            assert!(matches!(
                snapshot.get(Path::new("dir3/subdir1/file1")),
                Some(SnapshotEntry::File(_))
            ));
        }

        #[test]
        fn skips_dotdot_entries() {
            let vol = TempProjectedVolume::new("snapshot_dotdot");
            vol.stage_dir_create(Path::new("dir"));
            vol.stage_file_create(Path::new("dir/file1"), "value1");
            vol.execute_update();

            let snapshot = snapshot_directory(vol.path()).unwrap();
            for key in snapshot.keys() {
                assert!(
                    !key.to_string_lossy().contains(".."),
                    "snapshot should not contain K8S internal entries: {key:?}"
                );
            }
        }

        #[test]
        fn hashes_are_deterministic() {
            let vol = TempProjectedVolume::new("snapshot_deterministic");
            vol.stage_dir_create(Path::new("dir"));
            vol.stage_file_create(Path::new("dir/file1"), "value1");
            vol.execute_update();

            let snap1 = snapshot_directory(vol.path()).unwrap();
            let snap2 = snapshot_directory(vol.path()).unwrap();
            assert_eq!(snap1, snap2);
        }
    }

    mod diff {
        use super::*;

        #[test]
        fn detects_file_create() {
            let old = HashMap::new();
            let mut new = HashMap::new();
            new.insert(PathBuf::from("dir1/file1"), SnapshotEntry::File([0u8; 32]));

            let events = diff_snapshots(&old, &new, Instant::now());
            assert_eq!(events.len(), 1);
            assert!(matches!(
                events[0].kind,
                EventKind::Create(CreateKind::File)
            ));
            assert_eq!(events[0].path, Path::new("dir1/file1"));
        }

        #[test]
        fn detects_dir_create() {
            let old = HashMap::new();
            let mut new = HashMap::new();
            new.insert(PathBuf::from("dir1"), SnapshotEntry::Directory);

            let events = diff_snapshots(&old, &new, Instant::now());
            assert_eq!(events.len(), 1);
            assert!(matches!(
                events[0].kind,
                EventKind::Create(CreateKind::Folder)
            ));
            assert_eq!(events[0].path, Path::new("dir1"));
        }

        #[test]
        fn detects_file_modify() {
            let mut old = HashMap::new();
            old.insert(PathBuf::from("dir1/file1"), SnapshotEntry::File([0u8; 32]));
            let mut new = HashMap::new();
            new.insert(PathBuf::from("dir1/file1"), SnapshotEntry::File([1u8; 32]));

            let events = diff_snapshots(&old, &new, Instant::now());
            assert_eq!(events.len(), 1);
            assert!(matches!(
                events[0].kind,
                EventKind::Modify(ModifyKind::Data(_))
            ));
        }

        #[test]
        fn detects_file_remove() {
            let mut old = HashMap::new();
            old.insert(PathBuf::from("dir1/file1"), SnapshotEntry::File([0u8; 32]));
            let new = HashMap::new();

            let events = diff_snapshots(&old, &new, Instant::now());
            assert_eq!(events.len(), 1);
            assert!(matches!(
                events[0].kind,
                EventKind::Remove(RemoveKind::File)
            ));
        }

        #[test]
        fn detects_dir_remove() {
            let mut old = HashMap::new();
            old.insert(PathBuf::from("dir1"), SnapshotEntry::Directory);
            let new = HashMap::new();

            let events = diff_snapshots(&old, &new, Instant::now());
            assert_eq!(events.len(), 1);
            assert!(matches!(
                events[0].kind,
                EventKind::Remove(RemoveKind::Folder)
            ));
        }

        #[test]
        fn ignores_file1() {
            let hash: FileHash = Sha256::digest(b"same").into();
            let mut old = HashMap::new();
            old.insert(PathBuf::from("dir1/file1"), SnapshotEntry::File(hash));
            let mut new = HashMap::new();
            new.insert(PathBuf::from("dir1/file1"), SnapshotEntry::File(hash));

            let events = diff_snapshots(&old, &new, Instant::now());
            assert!(events.is_empty());
        }

        #[test]
        fn ignores_dir1() {
            let mut old = HashMap::new();
            old.insert(PathBuf::from("dir1"), SnapshotEntry::Directory);
            let mut new = HashMap::new();
            new.insert(PathBuf::from("dir1"), SnapshotEntry::Directory);

            let events = diff_snapshots(&old, &new, Instant::now());
            assert!(events.is_empty());
        }

        #[test]
        fn detects_mixed_changes() {
            let file1_hash: FileHash = Sha256::digest(b"hash1").into();
            let file2_hash_old: FileHash = Sha256::digest(b"hash2-old").into();
            let file2_hash_new: FileHash = Sha256::digest(b"hash2-new").into();
            let file3_hash: FileHash = Sha256::digest(b"hash3").into();
            let file4_hash: FileHash = Sha256::digest(b"hash4").into();

            let mut old = HashMap::new();
            old.insert(PathBuf::from("file1"), SnapshotEntry::File(file1_hash));
            old.insert(PathBuf::from("dir1"), SnapshotEntry::Directory);
            old.insert(PathBuf::from("file2"), SnapshotEntry::File(file2_hash_old));
            old.insert(PathBuf::from("file3"), SnapshotEntry::File(file3_hash));
            old.insert(PathBuf::from("dir2"), SnapshotEntry::Directory);

            let mut new = HashMap::new();
            new.insert(PathBuf::from("file1"), SnapshotEntry::File(file1_hash));
            new.insert(PathBuf::from("dir1"), SnapshotEntry::Directory);
            new.insert(PathBuf::from("file2"), SnapshotEntry::File(file2_hash_new));
            new.insert(PathBuf::from("file4"), SnapshotEntry::File(file4_hash));
            new.insert(PathBuf::from("dir3"), SnapshotEntry::Directory);

            let events = diff_snapshots(&old, &new, Instant::now());
            assert_eq!(events.len(), 5);

            let has_create_file = events.iter().any(|e| {
                matches!(e.kind, EventKind::Create(CreateKind::File))
                    && e.path == Path::new("file4")
            });
            let has_create_dir = events.iter().any(|e| {
                matches!(e.kind, EventKind::Create(CreateKind::Folder))
                    && e.path == Path::new("dir3")
            });
            let has_modify = events.iter().any(|e| {
                matches!(e.kind, EventKind::Modify(ModifyKind::Data(_)))
                    && e.path == Path::new("file2")
            });
            let has_remove_file = events.iter().any(|e| {
                matches!(e.kind, EventKind::Remove(RemoveKind::File))
                    && e.path == Path::new("file3")
            });
            let has_remove_dir = events.iter().any(|e| {
                matches!(e.kind, EventKind::Remove(RemoveKind::Folder))
                    && e.path == Path::new("dir2")
            });

            assert!(has_create_file, "should detect created file");
            assert!(has_create_dir, "should detect created directory");
            assert!(has_modify, "should detect modified file");
            assert!(has_remove_file, "should detect removed file");
            assert!(has_remove_dir, "should detect removed directory");
        }
    }

    mod symlink_swap_time {
        use super::*;

        #[test]
        fn detects_data_rename() {
            use notify::Event;

            let expected_time = Instant::now();
            let event = DebouncedEvent {
                event: Event {
                    kind: EventKind::Modify(ModifyKind::Name(notify::event::RenameMode::Both)),
                    paths: vec![
                        PathBuf::from("/mnt/vol/..data_tmp"),
                        PathBuf::from("/mnt/vol/..data"),
                    ],
                    attrs: Default::default(),
                },
                time: expected_time,
            };
            assert_eq!(symlink_swap_time(&[event]), Some(expected_time));
        }

        #[test]
        fn returns_none_for_unrelated_events() {
            use notify::Event;

            let event = DebouncedEvent {
                event: Event {
                    kind: EventKind::Modify(ModifyKind::Data(DataChange::Any)),
                    paths: vec![PathBuf::from("/mnt/vol/some_file")],
                    attrs: Default::default(),
                },
                time: Instant::now(),
            };
            assert_eq!(symlink_swap_time(&[event]), None);
        }
    }

    mod debouncer {
        use super::*;
        use std::sync::{Arc, Condvar};

        /// Timeout for waiting for events to arrive. Must be comfortably longer
        /// than `DEBOUNCE_WINDOW` to account for the tick rate and thread scheduling.
        const EVENT_TIMEOUT: Duration = Duration::from_secs(DEBOUNCE_WINDOW.as_secs() * 2);

        /// Timeout for asserting no events arrived. Longer than [`EVENT_TIMEOUT`]
        /// to reduce the risk of false passes.
        const EMPTY_TIMEOUT: Duration = Duration::from_secs(DEBOUNCE_WINDOW.as_secs() * 3);

        /// Collector for debouncer events, using a condvar to allow tests to wait
        /// for results with a timeout.
        struct EventCollector {
            inner: Arc<(Mutex<Vec<ProjectedVolumeEventResult>>, Condvar)>,
        }

        impl EventCollector {
            fn new() -> Self {
                Self {
                    inner: Arc::new((Mutex::new(Vec::new()), Condvar::new())),
                }
            }

            /// Returns a closure suitable for passing to [`ProjectedVolumeDebouncer::new`].
            fn handler(&self) -> impl FnMut(ProjectedVolumeEventResult) + Send + 'static {
                let inner = Arc::clone(&self.inner);
                move |result| {
                    let (lock, cvar) = &*inner;
                    lock.lock().unwrap().push(result);
                    cvar.notify_one();
                }
            }

            /// Wait for the latest [`ProjectedVolumeEventResult`] to arrive.
            ///
            /// Panics if no event arrives within [`EVENT_TIMEOUT`].
            fn events_batch(&self) -> ProjectedVolumeEventResult {
                let (lock, cvar) = &*self.inner;
                let (mut guard, wait_result) = cvar
                    .wait_timeout_while(lock.lock().unwrap(), EVENT_TIMEOUT, |events| {
                        events.is_empty()
                    })
                    .unwrap();
                assert!(
                    !wait_result.timed_out(),
                    "timed out waiting for debouncer events",
                );
                guard.drain(..).last().unwrap()
            }

            /// Assert that no events arrive within [`EMPTY_TIMEOUT`].
            fn assert_empty(&self) {
                let (lock, cvar) = &*self.inner;
                let (guard, _) = cvar
                    .wait_timeout_while(lock.lock().unwrap(), EMPTY_TIMEOUT, |events| {
                        events.is_empty()
                    })
                    .unwrap();
                assert!(
                    guard.is_empty(),
                    "expected no events but got {}",
                    guard.len(),
                );
            }
        }

        #[test]
        fn detects_file_modification() {
            let vol = TempProjectedVolume::new("debouncer_modify");
            vol.stage_file_create(Path::new("file1"), "value1");
            vol.stage_dir_create(Path::new("subdir"));
            vol.stage_file_create(Path::new("subdir/file2"), "value2");
            vol.execute_update();

            let collector = EventCollector::new();
            let _debouncer =
                ProjectedVolumeDebouncer::new(vol.path().to_path_buf(), collector.handler())
                    .unwrap();

            vol.stage_file_modify(Path::new("file1"), "value1-updated");
            vol.stage_file_modify(Path::new("subdir/file2"), "value2-updated");
            vol.execute_update();

            let events = collector.events_batch().unwrap();
            assert_eq!(events.len(), 2);
            let has_root = events.iter().any(|e| {
                matches!(e.kind, EventKind::Modify(ModifyKind::Data(_)))
                    && e.path == Path::new("file1")
            });
            let has_nested = events.iter().any(|e| {
                matches!(e.kind, EventKind::Modify(ModifyKind::Data(_)))
                    && e.path == Path::new("subdir/file2")
            });
            assert!(has_root, "should detect root file modification");
            assert!(has_nested, "should detect nested file modification");
        }

        #[test]
        fn detects_file_addition() {
            let vol = TempProjectedVolume::new("debouncer_add");
            vol.stage_file_create(Path::new("file1"), "value1");
            vol.stage_dir_create(Path::new("subdir"));
            vol.execute_update();

            let collector = EventCollector::new();
            let _debouncer =
                ProjectedVolumeDebouncer::new(vol.path().to_path_buf(), collector.handler())
                    .unwrap();

            vol.stage_file_create(Path::new("file2"), "value2");
            vol.stage_file_create(Path::new("subdir/file3"), "value3");
            vol.execute_update();

            let events = collector.events_batch().unwrap();
            assert_eq!(events.len(), 2);
            let has_root = events.iter().any(|e| {
                matches!(e.kind, EventKind::Create(CreateKind::File))
                    && e.path == Path::new("file2")
            });
            let has_nested = events.iter().any(|e| {
                matches!(e.kind, EventKind::Create(CreateKind::File))
                    && e.path == Path::new("subdir/file3")
            });
            assert!(has_root, "should detect root file addition");
            assert!(has_nested, "should detect nested file addition");
        }

        #[test]
        fn detects_file_removal() {
            let vol = TempProjectedVolume::new("debouncer_remove");
            vol.stage_file_create(Path::new("file1"), "value1");
            vol.stage_dir_create(Path::new("subdir"));
            vol.stage_file_create(Path::new("subdir/file2"), "value2");
            vol.execute_update();

            let collector = EventCollector::new();
            let _debouncer =
                ProjectedVolumeDebouncer::new(vol.path().to_path_buf(), collector.handler())
                    .unwrap();

            vol.stage_file_remove(Path::new("file1"));
            vol.stage_file_remove(Path::new("subdir/file2"));
            vol.execute_update();

            let events = collector.events_batch().unwrap();
            assert_eq!(events.len(), 2);
            let has_root = events.iter().any(|e| {
                matches!(e.kind, EventKind::Remove(RemoveKind::File))
                    && e.path == Path::new("file1")
            });
            let has_nested = events.iter().any(|e| {
                matches!(e.kind, EventKind::Remove(RemoveKind::File))
                    && e.path == Path::new("subdir/file2")
            });
            assert!(has_root, "should detect root file removal");
            assert!(has_nested, "should detect nested file removal");
        }

        #[test]
        fn detects_dir_creation() {
            let vol = TempProjectedVolume::new("debouncer_dir_create");
            vol.stage_file_create(Path::new("file1"), "value1");
            vol.execute_update();

            let collector = EventCollector::new();
            let _debouncer =
                ProjectedVolumeDebouncer::new(vol.path().to_path_buf(), collector.handler())
                    .unwrap();

            vol.stage_dir_create(Path::new("newdir1"));
            vol.stage_file_create(Path::new("newdir1/file1"), "newvalue");
            vol.execute_update();

            let events = collector.events_batch().unwrap();
            let has_dir_create = events.iter().any(|e| {
                matches!(e.kind, EventKind::Create(CreateKind::Folder))
                    && e.path == Path::new("newdir1")
            });
            let has_file_create = events.iter().any(|e| {
                matches!(e.kind, EventKind::Create(CreateKind::File))
                    && e.path == Path::new("newdir1/file1")
            });
            assert!(has_dir_create, "should detect directory creation");
            assert!(has_file_create, "should detect file creation in new dir");
        }

        #[test]
        fn detects_dir_removal() {
            let vol = TempProjectedVolume::new("debouncer_dir_remove");
            vol.stage_file_create(Path::new("file1"), "value1");
            vol.stage_dir_create(Path::new("ephemeral"));
            vol.stage_file_create(Path::new("ephemeral/file1"), "temp");
            vol.execute_update();

            let collector = EventCollector::new();
            let _debouncer =
                ProjectedVolumeDebouncer::new(vol.path().to_path_buf(), collector.handler())
                    .unwrap();

            vol.stage_file_remove(Path::new("ephemeral/file1"));
            vol.stage_dir_remove(Path::new("ephemeral"));
            vol.execute_update();

            let events = collector.events_batch().unwrap();
            let has_dir_remove = events.iter().any(|e| {
                matches!(e.kind, EventKind::Remove(RemoveKind::Folder))
                    && e.path == Path::new("ephemeral")
            });
            let has_file_remove = events.iter().any(|e| {
                matches!(e.kind, EventKind::Remove(RemoveKind::File))
                    && e.path == Path::new("ephemeral/file1")
            });
            assert!(has_dir_remove, "should detect directory removal");
            assert!(
                has_file_remove,
                "should detect file removal from removed dir"
            );
        }

        #[test]
        fn detects_no_change() {
            let vol = TempProjectedVolume::new("debouncer_nochange");
            vol.stage_file_create(Path::new("file1"), "value1");
            vol.stage_dir_create(Path::new("subdir"));
            vol.stage_file_create(Path::new("subdir/file2"), "value2");
            vol.execute_update();

            let collector = EventCollector::new();
            let _debouncer =
                ProjectedVolumeDebouncer::new(vol.path().to_path_buf(), collector.handler())
                    .unwrap();

            // Re-stage identical content (modify with same values)
            vol.stage_file_modify(Path::new("file1"), "value1");
            vol.stage_file_modify(Path::new("subdir/file2"), "value2");
            vol.execute_update();

            collector.assert_empty();
        }

        #[test]
        fn detects_mixed_changes() {
            let vol = TempProjectedVolume::new("debouncer_mixed");
            vol.stage_file_create(Path::new("unchanged"), "keep");
            vol.stage_file_create(Path::new("modified"), "old");
            vol.stage_file_create(Path::new("removed"), "gone");
            vol.stage_dir_create(Path::new("subdir"));
            vol.stage_file_create(Path::new("subdir/unchanged"), "keep");
            vol.stage_file_create(Path::new("subdir/modified"), "old");
            vol.stage_file_create(Path::new("subdir/removed"), "gone");
            vol.stage_dir_create(Path::new("removed_dir"));
            vol.stage_file_create(Path::new("removed_dir/file1"), "temp");
            vol.execute_update();

            let collector = EventCollector::new();
            let _debouncer =
                ProjectedVolumeDebouncer::new(vol.path().to_path_buf(), collector.handler())
                    .unwrap();

            vol.stage_file_modify(Path::new("modified"), "new");
            vol.stage_file_remove(Path::new("removed"));
            vol.stage_file_create(Path::new("created"), "fresh");
            vol.stage_file_modify(Path::new("subdir/modified"), "new");
            vol.stage_file_remove(Path::new("subdir/removed"));
            vol.stage_file_create(Path::new("subdir/created"), "fresh");
            vol.stage_file_remove(Path::new("removed_dir/file1"));
            vol.stage_dir_remove(Path::new("removed_dir"));
            vol.stage_dir_create(Path::new("created_dir"));
            vol.stage_file_create(Path::new("created_dir/file1"), "new");
            vol.execute_update();

            let events = collector.events_batch().unwrap();
            assert_eq!(events.len(), 10);

            // Root-level events
            let has = |kind: EventKind, path: &str| {
                events
                    .iter()
                    .any(|e| e.kind == kind && e.path == Path::new(path))
            };
            assert!(
                has(
                    EventKind::Modify(ModifyKind::Data(DataChange::Any)),
                    "modified"
                ),
                "should detect root file modification"
            );
            assert!(
                has(EventKind::Remove(RemoveKind::File), "removed"),
                "should detect root file removal"
            );
            assert!(
                has(EventKind::Create(CreateKind::File), "created"),
                "should detect root file creation"
            );

            // Subdirectory events
            assert!(
                has(
                    EventKind::Modify(ModifyKind::Data(DataChange::Any)),
                    "subdir/modified"
                ),
                "should detect nested file modification"
            );
            assert!(
                has(EventKind::Remove(RemoveKind::File), "subdir/removed"),
                "should detect nested file removal"
            );
            assert!(
                has(EventKind::Create(CreateKind::File), "subdir/created"),
                "should detect nested file creation"
            );

            // Directory-level events
            assert!(
                has(EventKind::Remove(RemoveKind::Folder), "removed_dir"),
                "should detect directory removal"
            );
            assert!(
                has(EventKind::Remove(RemoveKind::File), "removed_dir/file1"),
                "should detect file removal from removed dir"
            );
            assert!(
                has(EventKind::Create(CreateKind::Folder), "created_dir"),
                "should detect directory creation"
            );
            assert!(
                has(EventKind::Create(CreateKind::File), "created_dir/file1"),
                "should detect file creation in new dir"
            );
        }
    }
}
