// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

//! A debouncer for Kubernetes projected volume mounts.
//!
//! Wraps [`notify_debouncer_full`] and detects changes via the atomic `..data` symlink
//! swap that Kubernetes performs when updating projected volumes (Secrets, ConfigMaps, etc.).
//!
//! Instead of exposing raw filesystem events (which include internal K8S plumbing like
//! `..data`, `..data_tmp`, and timestamped snapshot directories), this debouncer produces
//! synthetic events with clean relative paths by hashing file contents with SHA-256 and
//! diffing snapshots before and after each swap.
//!
//! # Why there is no user-configurable debounce window
//!
//! The kubelet already batches all projected volume changes into a single atomic symlink
//! swap per sync cycle (default: 60 seconds via `--sync-frequency`). Multiple Secret or
//! ConfigMap key updates between sync ticks are delivered as one swap. Consecutive swaps
//! are therefore separated by tens of seconds at minimum, so there is nothing meaningful
//! for the caller to aggregate. The internal debounce window only needs to be long enough
//! to coalesce the ~20-30 raw inotify events produced by a single swap (~1-2ms of
//! filesystem activity) into one callback invocation.

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
/// within the projected volume (e.g., `my-secret/my-key`), with all Kubernetes
/// internal entries filtered out.
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
///     PathBuf::from("/etc/akri/secrets/connector_secrets"),
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

/// Walks the projected volume directory and builds a map of relative paths to SHA-256 hashes.
///
/// Filters out all entries starting with `..` (Kubernetes internal plumbing) and follows
/// symlinks to reach actual file content.
fn snapshot_directory(root: &Path) -> Result<HashMap<PathBuf, FileHash>, std::io::Error> {
    let mut map = HashMap::new();
    snapshot_recursive(root, root, &mut map)?;
    Ok(map)
}

fn snapshot_recursive(
    root: &Path,
    current: &Path,
    map: &mut HashMap<PathBuf, FileHash>,
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
            snapshot_recursive(root, &path, map)?;
        } else if metadata.is_file() {
            let contents = fs::read(&path)?;
            let hash: FileHash = Sha256::digest(&contents).into();
            let relative = path
                .strip_prefix(root)
                .expect("path is always under root")
                .to_path_buf();
            map.insert(relative, hash);
        }
    }
    Ok(())
}

/// Compares two snapshots and produces synthetic events for any differences.
fn diff_snapshots(
    old: &HashMap<PathBuf, FileHash>,
    new: &HashMap<PathBuf, FileHash>,
    time: Instant,
) -> Vec<ProjectedVolumeEvent> {
    let mut events = Vec::new();

    for (path, new_hash) in new {
        match old.get(path) {
            Some(old_hash) if old_hash == new_hash => {}
            Some(_) => {
                events.push(ProjectedVolumeEvent {
                    kind: EventKind::Modify(ModifyKind::Data(DataChange::Any)),
                    path: path.clone(),
                    time,
                });
            }
            None => {
                events.push(ProjectedVolumeEvent {
                    kind: EventKind::Create(CreateKind::File),
                    path: path.clone(),
                    time,
                });
            }
        }
    }

    for path in old.keys() {
        if !new.contains_key(path) {
            events.push(ProjectedVolumeEvent {
                kind: EventKind::Remove(RemoveKind::File),
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
    use std::sync::{Arc, Condvar, Mutex as StdMutex};

    // -- snapshot tests --

    #[test]
    fn snapshot_captures_files() {
        let vol = TempProjectedVolume::new("snapshot_captures");
        vol.stage_dir_create(Path::new("secret"));
        vol.stage_file_create(Path::new("secret/key1"), "value1");
        vol.stage_file_create(Path::new("secret/key2"), "value2");
        vol.execute_update();

        let snapshot = snapshot_directory(vol.path()).unwrap();
        assert_eq!(snapshot.len(), 2);
        assert!(snapshot.contains_key(Path::new("secret/key1")));
        assert!(snapshot.contains_key(Path::new("secret/key2")));
    }

    #[test]
    fn snapshot_skips_dotdot_entries() {
        let vol = TempProjectedVolume::new("snapshot_dotdot");
        vol.stage_dir_create(Path::new("secret"));
        vol.stage_file_create(Path::new("secret/key1"), "value1");
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
    fn snapshot_hashes_are_deterministic() {
        let vol = TempProjectedVolume::new("snapshot_deterministic");
        vol.stage_dir_create(Path::new("secret"));
        vol.stage_file_create(Path::new("secret/key1"), "value1");
        vol.execute_update();

        let snap1 = snapshot_directory(vol.path()).unwrap();
        let snap2 = snapshot_directory(vol.path()).unwrap();
        assert_eq!(snap1, snap2);
    }

    // -- diff tests --

    #[test]
    fn diff_detects_create() {
        let old = HashMap::new();
        let mut new = HashMap::new();
        new.insert(PathBuf::from("secret/key1"), [0u8; 32]);

        let events = diff_snapshots(&old, &new, Instant::now());
        assert_eq!(events.len(), 1);
        assert!(matches!(events[0].kind, EventKind::Create(_)));
        assert_eq!(events[0].path, Path::new("secret/key1"));
    }

    #[test]
    fn diff_detects_modify() {
        let mut old = HashMap::new();
        old.insert(PathBuf::from("secret/key1"), [0u8; 32]);
        let mut new = HashMap::new();
        new.insert(PathBuf::from("secret/key1"), [1u8; 32]);

        let events = diff_snapshots(&old, &new, Instant::now());
        assert_eq!(events.len(), 1);
        assert!(matches!(
            events[0].kind,
            EventKind::Modify(ModifyKind::Data(_))
        ));
    }

    #[test]
    fn diff_detects_remove() {
        let mut old = HashMap::new();
        old.insert(PathBuf::from("secret/key1"), [0u8; 32]);
        let new = HashMap::new();

        let events = diff_snapshots(&old, &new, Instant::now());
        assert_eq!(events.len(), 1);
        assert!(matches!(events[0].kind, EventKind::Remove(_)));
    }

    #[test]
    fn diff_ignores_unchanged() {
        let hash: FileHash = Sha256::digest(b"same").into();
        let mut old = HashMap::new();
        old.insert(PathBuf::from("secret/key1"), hash);
        let mut new = HashMap::new();
        new.insert(PathBuf::from("secret/key1"), hash);

        let events = diff_snapshots(&old, &new, Instant::now());
        assert!(events.is_empty());
    }

    #[test]
    fn diff_mixed_changes() {
        let hash_a: FileHash = Sha256::digest(b"a").into();
        let hash_b: FileHash = Sha256::digest(b"b").into();

        let mut old = HashMap::new();
        old.insert(PathBuf::from("unchanged"), hash_a);
        old.insert(PathBuf::from("modified"), hash_a);
        old.insert(PathBuf::from("removed"), hash_a);

        let mut new = HashMap::new();
        new.insert(PathBuf::from("unchanged"), hash_a);
        new.insert(PathBuf::from("modified"), hash_b);
        new.insert(PathBuf::from("created"), hash_a);

        let events = diff_snapshots(&old, &new, Instant::now());
        assert_eq!(events.len(), 3);

        let has_create = events
            .iter()
            .any(|e| matches!(e.kind, EventKind::Create(_)) && e.path == Path::new("created"));
        let has_modify = events.iter().any(|e| {
            matches!(e.kind, EventKind::Modify(ModifyKind::Data(_)))
                && e.path == Path::new("modified")
        });
        let has_remove = events
            .iter()
            .any(|e| matches!(e.kind, EventKind::Remove(_)) && e.path == Path::new("removed"));

        assert!(has_create, "should detect created file");
        assert!(has_modify, "should detect modified file");
        assert!(has_remove, "should detect removed file");
    }

    // -- symlink_swap_time tests --

    #[test]
    fn symlink_swap_time_detects_data_rename() {
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
    fn symlink_swap_time_returns_none_for_unrelated_events() {
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

    // -- debouncer integration tests --

    /// Collector for debouncer events, using a condvar to allow tests to wait
    /// for results with a timeout.
    struct EventCollector {
        inner: Arc<(StdMutex<Vec<ProjectedVolumeEventResult>>, Condvar)>,
    }

    impl EventCollector {
        fn new() -> Self {
            Self {
                inner: Arc::new((StdMutex::new(Vec::new()), Condvar::new())),
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

        /// Wait up to `timeout` for at least one event batch to arrive.
        fn wait(&self, timeout: Duration) -> Vec<ProjectedVolumeEventResult> {
            let (lock, cvar) = &*self.inner;
            let mut guard = cvar
                .wait_timeout_while(lock.lock().unwrap(), timeout, |events| events.is_empty())
                .unwrap()
                .0;
            guard.drain(..).collect()
        }

        /// Wait `timeout` and assert that no events arrived.
        fn assert_empty(&self, timeout: Duration) {
            let (lock, cvar) = &*self.inner;
            let guard = cvar
                .wait_timeout_while(lock.lock().unwrap(), timeout, |events| events.is_empty())
                .unwrap()
                .0;
            assert!(
                guard.is_empty(),
                "expected no events but got {}",
                guard.len()
            );
        }
    }

    #[test]
    fn debouncer_detects_modification() {
        let vol = TempProjectedVolume::new("debouncer_modify");
        vol.stage_dir_create(Path::new("secret"));
        vol.stage_file_create(Path::new("secret/key1"), "value1");
        vol.execute_update();

        let collector = EventCollector::new();
        let _debouncer =
            ProjectedVolumeDebouncer::new(vol.path().to_path_buf(), collector.handler()).unwrap();

        vol.stage_file_modify(Path::new("secret/key1"), "value2");
        vol.execute_update();

        let batches = collector.wait(Duration::from_secs(5));
        assert!(!batches.is_empty(), "debouncer should have fired");
        let events = batches[0].as_ref().unwrap();
        assert_eq!(events.len(), 1);
        assert!(matches!(
            events[0].kind,
            EventKind::Modify(ModifyKind::Data(_))
        ));
        assert_eq!(events[0].path, Path::new("secret/key1"));
    }

    #[test]
    fn debouncer_detects_addition() {
        let vol = TempProjectedVolume::new("debouncer_add");
        vol.stage_dir_create(Path::new("secret"));
        vol.stage_file_create(Path::new("secret/key1"), "value1");
        vol.execute_update();

        let collector = EventCollector::new();
        let _debouncer =
            ProjectedVolumeDebouncer::new(vol.path().to_path_buf(), collector.handler()).unwrap();

        vol.stage_file_create(Path::new("secret/key2"), "value2");
        vol.execute_update();

        let batches = collector.wait(Duration::from_secs(5));
        assert!(!batches.is_empty(), "debouncer should have fired");
        let events = batches[0].as_ref().unwrap();
        assert_eq!(events.len(), 1);
        assert!(matches!(events[0].kind, EventKind::Create(_)));
        assert_eq!(events[0].path, Path::new("secret/key2"));
    }

    #[test]
    fn debouncer_detects_removal() {
        let vol = TempProjectedVolume::new("debouncer_remove");
        vol.stage_dir_create(Path::new("secret"));
        vol.stage_file_create(Path::new("secret/key1"), "value1");
        vol.stage_file_create(Path::new("secret/key2"), "value2");
        vol.execute_update();

        let collector = EventCollector::new();
        let _debouncer =
            ProjectedVolumeDebouncer::new(vol.path().to_path_buf(), collector.handler()).unwrap();

        vol.stage_file_remove(Path::new("secret/key2"));
        vol.execute_update();

        let batches = collector.wait(Duration::from_secs(5));
        assert!(!batches.is_empty(), "debouncer should have fired");
        let events = batches[0].as_ref().unwrap();
        assert_eq!(events.len(), 1);
        assert!(matches!(events[0].kind, EventKind::Remove(_)));
        assert_eq!(events[0].path, Path::new("secret/key2"));
    }

    #[test]
    fn debouncer_silent_on_no_change() {
        let vol = TempProjectedVolume::new("debouncer_nochange");
        vol.stage_dir_create(Path::new("secret"));
        vol.stage_file_create(Path::new("secret/key1"), "value1");
        vol.execute_update();

        let collector = EventCollector::new();
        let _debouncer =
            ProjectedVolumeDebouncer::new(vol.path().to_path_buf(), collector.handler()).unwrap();

        // Re-stage identical content (modify with same value)
        vol.stage_file_modify(Path::new("secret/key1"), "value1");
        vol.execute_update();

        collector.assert_empty(Duration::from_secs(3));
    }
}
