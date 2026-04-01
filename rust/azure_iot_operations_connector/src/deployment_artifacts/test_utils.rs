// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

//! Types used for testing deployment artifacts

use std::cell::{Cell, RefCell};
use std::collections::{HashMap, HashSet};
use std::os::unix::fs::symlink;
use std::path::{Component, Path, PathBuf};
use std::sync::Arc;
use std::time::SystemTime;

use tempfile::TempDir;

/// Simulates a file mount directory using a temporary directory.
#[derive(Clone)]
pub struct TempMount {
    dir: Arc<TempDir>, // TODO: This should not be arc! it makes drop behavior ambiguous?
                       // TODO: or not, idk
}

impl TempMount {
    pub fn new(dir_name: &str) -> Self {
        let dir = Arc::new(tempfile::TempDir::with_prefix(dir_name).unwrap());
        Self { dir }
        // TODO: Add symlink simulation. Currently this doesn't work, because
        // trying to add a ".." file is interpreted as trying to go up a level
        // in the directory structure.
        //let ret = Self { dir };
        // Create a ".." file to simulate a symlink in a mounted directory
        //ret.add_file("..", "");
        //ret
    }

    // TODO: do we need to consider directories/subdirectories here?

    pub fn add_file(&self, file_name: &str, contents: &str) {
        let file_path = self.dir.path().join(file_name);
        std::fs::write(file_path, contents).unwrap();
    }

    pub fn remove_file(&self, file_name: &str) {
        let file_path = self.dir.path().join(file_name);
        std::fs::remove_file(file_path).unwrap();
    }

    pub fn update_file(&self, file_name: &str, contents: &str) {
        // NOTE: This is the same implementation as add_file, but it's semantically clearer
        // to have a separate method.
        let file_path = self.dir.path().join(file_name);
        std::fs::write(file_path, contents).unwrap();
    }

    pub fn path(&self) -> &Path {
        self.dir.path()
    }
}

/// Simulates persistent volume mounts using temporary directories.
/// An admittedly funny name.
pub struct TempPersistentVolumeManager {
    volumes: Vec<TempMount>,
}

impl TempPersistentVolumeManager {
    pub fn new() -> Self {
        Self {
            volumes: Vec::new(),
        }
    }

    pub fn add_mount(&mut self, mount_name: &str) {
        let mount = TempMount::new(mount_name);
        self.volumes.push(mount);
    }

    pub fn index_file_contents(&self) -> String {
        let mut contents = String::new();
        for mount in &self.volumes {
            let mount_str = format!("{}\n", mount.path().to_str().unwrap());
            contents.push_str(&mount_str);
        }
        contents
    }

    pub fn volume_path_bufs(&self) -> Vec<PathBuf> {
        self.volumes
            .iter()
            .map(|m| m.path().to_path_buf())
            .collect()
    }
}

/// Staged operation for a projected volume update.
enum StagedOp {
    FileCreate { path: PathBuf, contents: String },
    FileModify { path: PathBuf, contents: String },
    FileRemove { path: PathBuf },
    DirCreate { path: PathBuf },
    DirRemove { path: PathBuf },
}

/// Simulates a Kubernetes projected volume using temporary directories for testing purposes.
/// This creates real data on the filesystem in a temporary directory, but when dropped, all data
/// will be removed from the filesystem.
///
/// Updates are performed using the same atomic symlink swap mechanism that the kubelet uses:
/// staged changes are written to a new timestamped directory, then `..data` is atomically
/// swapped to point to it. Top-level entries are relative symlinks through `..data`.
pub struct TempProjectedVolume {
    dir: TempDir,
    /// Current files in the projected volume (relative path to contents).
    files: RefCell<HashMap<PathBuf, String>>,
    /// Explicitly created directories (that may be empty).
    dirs: RefCell<HashSet<PathBuf>>,
    /// Pending operations to apply on next [`Self::execute_update`].
    staged_ops: RefCell<Vec<StagedOp>>,
    /// Name of the current timestamped snapshot directory.
    current_timestamp_dir: RefCell<Option<String>>,
    /// Monotonic counter to ensure unique snapshot directory names.
    counter: Cell<u64>,
}

impl TempProjectedVolume {
    /// Create a new `TempProjectedVolume` with the given directory name.
    pub fn new(dir_name: &str) -> Self {
        let dir = tempfile::TempDir::with_prefix(dir_name).unwrap();
        Self {
            dir,
            files: RefCell::new(HashMap::new()),
            dirs: RefCell::new(HashSet::new()),
            staged_ops: RefCell::new(Vec::new()),
            current_timestamp_dir: RefCell::new(None),
            counter: Cell::new(0),
        }
    }

    /// Return the path of the projected volume mount.
    pub fn path(&self) -> &Path {
        self.dir.path()
    }

    /// Validate that a path is safe to use inside the projected volume.
    ///
    /// Panics if the path is absolute or contains any `..`, root, or Windows
    /// prefix components, which could cause operations to escape the temp dir.
    fn validate_path(path: &Path) {
        assert!(
            path.is_relative(),
            "projected volume path must be relative: {path:?}"
        );
        for component in path.components() {
            assert!(
                matches!(component, Component::Normal(_)),
                "projected volume path must not contain '..' or root components: {path:?}"
            );
        }
    }

    /// Stage a file creation in the projected volume.
    /// If the relative path includes subdirectories, they must already exist.
    pub fn stage_file_create(&self, file_path: &Path, contents: &str) {
        Self::validate_path(file_path);
        self.staged_ops.borrow_mut().push(StagedOp::FileCreate {
            path: file_path.to_path_buf(),
            contents: contents.to_string(),
        });
    }

    /// Stage a file modification in the projected volume. The file must already exist.
    pub fn stage_file_modify(&self, file_path: &Path, contents: &str) {
        Self::validate_path(file_path);
        self.staged_ops.borrow_mut().push(StagedOp::FileModify {
            path: file_path.to_path_buf(),
            contents: contents.to_string(),
        });
    }

    /// Stage a file removal in the projected volume. The file must already exist.
    pub fn stage_file_remove(&self, file_path: &Path) {
        Self::validate_path(file_path);
        self.staged_ops.borrow_mut().push(StagedOp::FileRemove {
            path: file_path.to_path_buf(),
        });
    }

    /// Stage a directory creation in the projected volume. The directory must not already exist.
    pub fn stage_dir_create(&self, dir_path: &Path) {
        Self::validate_path(dir_path);
        self.staged_ops.borrow_mut().push(StagedOp::DirCreate {
            path: dir_path.to_path_buf(),
        });
    }

    /// Stage a directory removal in the projected volume. The directory must already exist and be empty.
    pub fn stage_dir_remove(&self, dir_path: &Path) {
        Self::validate_path(dir_path);
        self.staged_ops.borrow_mut().push(StagedOp::DirRemove {
            path: dir_path.to_path_buf(),
        });
    }

    /// Trigger an update of all staged changes to the projected volume.
    ///
    /// This simulates the kubelet's atomic symlink swap:
    /// 1. Applies staged operations to the in-memory file state.
    /// 2. Creates a new timestamped directory with the full file tree.
    /// 3. Creates `..data_tmp` symlink pointing to the new timestamped dir.
    /// 4. Renames `..data_tmp` to `..data` (atomic POSIX rename).
    /// 5. Updates top-level symlinks (each pointing through `..data`).
    /// 6. Removes old timestamped directory.
    pub fn execute_update(&self) {
        let mut files = self.files.borrow_mut();
        let mut dirs = self.dirs.borrow_mut();
        let mut staged_ops = self.staged_ops.borrow_mut();
        let mut current_ts = self.current_timestamp_dir.borrow_mut();

        // Apply staged operations to in-memory state
        for op in staged_ops.drain(..) {
            match op {
                StagedOp::FileCreate { path, contents } => {
                    assert!(
                        !files.contains_key(&path),
                        "staged file create but file already exists: {path:?}"
                    );
                    if let Some(parent) = path.parent()
                        && !parent.as_os_str().is_empty()
                    {
                        assert!(
                            dirs.contains(parent),
                            "staged file create but parent directory does not exist: {parent:?}"
                        );
                    }
                    files.insert(path, contents);
                }
                StagedOp::FileModify { path, contents } => {
                    assert!(
                        files.contains_key(&path),
                        "staged file modify but file does not exist: {path:?}"
                    );
                    files.insert(path, contents);
                }
                StagedOp::FileRemove { path } => {
                    assert!(
                        files.remove(&path).is_some(),
                        "staged file remove but file does not exist: {path:?}"
                    );
                }
                StagedOp::DirCreate { path } => {
                    assert!(
                        dirs.insert(path.clone()),
                        "staged dir create but directory already exists: {path:?}"
                    );
                }
                StagedOp::DirRemove { path } => {
                    assert!(
                        !files.keys().any(|f| f.starts_with(&path)),
                        "staged dir remove but directory contains files: {path:?}"
                    );
                    assert!(
                        !dirs.iter().any(|d| d != &path && d.starts_with(&path)),
                        "staged dir remove but directory contains subdirectories: {path:?}"
                    );
                    assert!(
                        dirs.remove(&path),
                        "staged dir remove but directory does not exist: {path:?}"
                    );
                }
            }
        }

        let root = self.dir.path();

        // Generate a unique timestamped directory name
        let count = self.counter.get();
        self.counter.set(count + 1);
        let now = SystemTime::now()
            .duration_since(SystemTime::UNIX_EPOCH)
            .unwrap();
        let new_ts_name = format!(
            "..{secs}_{nanos:09}_{count}",
            secs = now.as_secs(),
            nanos = now.subsec_nanos()
        );
        let new_ts_path = root.join(&new_ts_name);

        // Create new timestamped directory and populate it
        std::fs::create_dir(&new_ts_path).unwrap();

        for dir_path in dirs.iter() {
            std::fs::create_dir_all(new_ts_path.join(dir_path)).unwrap();
        }

        for (file_path, contents) in files.iter() {
            let full_path = new_ts_path.join(file_path);
            std::fs::write(&full_path, contents).unwrap();
        }

        // Atomic symlink swap
        let data_tmp = root.join("..data_tmp");
        let data_link = root.join("..data");

        // Create ..data_tmp symlink pointing to new timestamped dir (relative)
        symlink(&new_ts_name, &data_tmp).unwrap();

        // Atomic rename: ..data_tmp -> ..data
        std::fs::rename(&data_tmp, &data_link).unwrap();

        // Update top-level symlinks (each points through ..data)
        let new_top_level: HashSet<String> = files
            .keys()
            .chain(dirs.iter())
            .filter_map(|p| {
                p.components()
                    .next()
                    .map(|c| c.as_os_str().to_string_lossy().into_owned())
            })
            .collect();

        let existing_top_level: HashSet<String> = std::fs::read_dir(root)
            .unwrap()
            .filter_map(Result::ok)
            .map(|e| e.file_name().to_string_lossy().into_owned())
            .filter(|name| !name.starts_with(".."))
            .collect();

        // Remove stale top-level symlinks
        for name in existing_top_level.difference(&new_top_level) {
            match std::fs::remove_file(root.join(name)) {
                Ok(()) => {}
                Err(e) if e.kind() == std::io::ErrorKind::NotFound => {}
                Err(e) => panic!("failed to remove stale symlink {name:?}: {e}"),
            }
        }

        // Create new top-level symlinks
        for name in new_top_level.difference(&existing_top_level) {
            symlink(format!("..data/{name}"), root.join(name)).unwrap();
        }

        // Delete old timestamped directory
        if let Some(ref old_ts) = *current_ts {
            let old_path = root.join(old_ts);
            if old_path.exists() {
                std::fs::remove_dir_all(&old_path).unwrap();
            }
        }

        *current_ts = Some(new_ts_name);
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    // TODO: Write tests for other mounts.

    mod projected_volume {
        use super::*;
        use std::sync::atomic::{AtomicU64, Ordering};
        use test_case::test_case;

        static VOLUME_COUNTER: AtomicU64 = AtomicU64::new(0);

        /// Generate a unique volume name based on the given prefix.
        fn unique_name(prefix: &str) -> String {
            let id = VOLUME_COUNTER.fetch_add(1, Ordering::Relaxed);
            format!("{prefix}_{id}")
        }

        /// Helper: read a file via the canonical symlink path (i.e. through `..data`).
        fn read_via_data_link(vol: &TempProjectedVolume, rel: &str) -> String {
            std::fs::read_to_string(vol.path().join("..data").join(rel)).unwrap()
        }

        /// Helper: read a file via the top-level symlink (e.g. `<mount>/my_dir/file`).
        fn read_via_top_level(vol: &TempProjectedVolume, rel: &str) -> String {
            std::fs::read_to_string(vol.path().join(rel)).unwrap()
        }

        /// Helper: stage the parent directory for a file path if needed.
        fn stage_parent_dirs(vol: &TempProjectedVolume, path: &Path) {
            if let Some(parent) = path.parent()
                && !parent.as_os_str().is_empty()
            {
                vol.stage_dir_create(parent);
            }
        }

        // NOTE: Ideally we would run a debouncer, and compare the sequence of events matches what we expect,
        // for a Projected Volume in K8S, but that's not really viable due to non-determinism surrounding
        // events in new subdirectories, as there is a race condition in the inotify watcher. Sometimes the
        // watcher will register before the file operation that occurs inside the new directory, and sometimes
        // after. Depending on which happens, different events may be or not be reported.
        //
        // This does mean that the tests are of slightly lesser value than we might like, and thus we should make
        // sure to update the TempProjectedVolume implementation if we notice any discrepencies between it and
        // actual K8S behavior in production.

        // -- basic operations --

        #[test_case(Path::new("key"); "root")]
        #[test_case(Path::new("sub/key"); "subdirectory")]
        fn create_file(file_path: &Path) {
            let vol = TempProjectedVolume::new(&unique_name("test"));
            stage_parent_dirs(&vol, file_path);
            vol.stage_file_create(file_path, "value");
            vol.execute_update();

            let rel = file_path.to_str().unwrap();
            assert_eq!(read_via_data_link(&vol, rel), "value");
            assert_eq!(read_via_top_level(&vol, rel), "value");
        }

        #[test_case(Path::new("key"); "root")]
        #[test_case(Path::new("sub/key"); "subdirectory")]
        fn modify_file(file_path: &Path) {
            let vol = TempProjectedVolume::new(&unique_name("test"));
            stage_parent_dirs(&vol, file_path);
            vol.stage_file_create(file_path, "old");
            vol.execute_update();

            vol.stage_file_modify(file_path, "new");
            vol.execute_update();

            let rel = file_path.to_str().unwrap();
            assert_eq!(read_via_data_link(&vol, rel), "new");
            assert_eq!(read_via_top_level(&vol, rel), "new");
        }

        #[test_case(Path::new("key"); "root")]
        #[test_case(Path::new("sub/key"); "subdirectory")]
        fn remove_file(file_path: &Path) {
            let vol = TempProjectedVolume::new(&unique_name("test"));
            stage_parent_dirs(&vol, file_path);
            vol.stage_file_create(file_path, "value");
            vol.execute_update();

            vol.stage_file_remove(file_path);
            vol.execute_update();

            assert!(!vol.path().join(file_path).exists());
            assert!(!vol.path().join("..data").join(file_path).exists());
        }

        #[test]
        fn create_and_remove_directory() {
            let vol = TempProjectedVolume::new(&unique_name("test"));
            vol.stage_dir_create(Path::new("mydir"));
            vol.execute_update();

            assert!(vol.path().join("..data").join("mydir").is_dir());
            assert!(vol.path().join("mydir").exists());

            vol.stage_dir_remove(Path::new("mydir"));
            vol.execute_update();

            assert!(!vol.path().join("..data").join("mydir").exists());
            assert!(!vol.path().join("mydir").exists());
        }

        // -- symlink structure --

        #[test_case(Path::new("f"); "root")]
        #[test_case(Path::new("sub/f"); "subdirectory")]
        fn data_symlink_points_to_timestamped_dir(file_path: &Path) {
            let vol = TempProjectedVolume::new(&unique_name("test"));
            stage_parent_dirs(&vol, file_path);
            vol.stage_file_create(file_path, "x");
            vol.execute_update();

            let data_link = vol.path().join("..data");
            assert!(data_link.is_symlink());
            let target = std::fs::read_link(&data_link).unwrap();
            let target_str = target.to_string_lossy();
            assert!(
                target_str.starts_with(".."),
                "..data should point to a ..-prefixed timestamped \
                 dir, got: {target_str}"
            );
        }

        #[test]
        fn top_level_symlinks_point_through_data() {
            let vol = TempProjectedVolume::new(&unique_name("test"));
            vol.stage_dir_create(Path::new("secrets"));
            vol.stage_file_create(Path::new("secrets/key"), "val");
            vol.execute_update();

            let link = vol.path().join("secrets");
            assert!(link.is_symlink());
            let target = std::fs::read_link(&link).unwrap();
            assert_eq!(target, PathBuf::from("..data/secrets"));
        }

        // -- atomic swap --

        #[test_case(Path::new("f"); "root")]
        #[test_case(Path::new("sub/f"); "subdirectory")]
        fn old_timestamped_dir_removed_after_swap(file_path: &Path) {
            let vol = TempProjectedVolume::new(&unique_name("test"));
            stage_parent_dirs(&vol, file_path);
            vol.stage_file_create(file_path, "v1");
            vol.execute_update();

            let first_ts_target = std::fs::read_link(vol.path().join("..data")).unwrap();

            vol.stage_file_modify(file_path, "v2");
            vol.execute_update();

            assert!(
                !vol.path().join(&first_ts_target).exists(),
                "old timestamped directory should be removed after swap"
            );
            let rel = file_path.to_str().unwrap();
            assert_eq!(read_via_data_link(&vol, rel), "v2");
        }

        #[test_case(Path::new("f"); "root")]
        #[test_case(Path::new("sub/f"); "subdirectory")]
        fn multiple_updates_produce_unique_timestamped_dirs(file_path: &Path) {
            let vol = TempProjectedVolume::new(&unique_name("test"));
            stage_parent_dirs(&vol, file_path);

            vol.stage_file_create(file_path, "v1");
            vol.execute_update();
            let ts1 = std::fs::read_link(vol.path().join("..data")).unwrap();

            vol.stage_file_modify(file_path, "v2");
            vol.execute_update();
            let ts2 = std::fs::read_link(vol.path().join("..data")).unwrap();

            assert_ne!(
                ts1, ts2,
                "each update should produce a unique timestamped \
                 directory"
            );
        }

        // -- stale symlink cleanup --

        #[test]
        fn stale_top_level_symlinks_removed() {
            let vol = TempProjectedVolume::new(&unique_name("test"));
            vol.stage_file_create(Path::new("a"), "1");
            vol.stage_file_create(Path::new("b"), "2");
            vol.execute_update();

            // Both should be symlinks through ..data
            let a_link = vol.path().join("a");
            let b_link = vol.path().join("b");
            assert!(a_link.is_symlink());
            assert!(b_link.is_symlink());
            assert_eq!(
                std::fs::read_link(&a_link).unwrap(),
                PathBuf::from("..data/a")
            );
            assert_eq!(
                std::fs::read_link(&b_link).unwrap(),
                PathBuf::from("..data/b")
            );

            // Remove file "a", keep "b"
            vol.stage_file_remove(Path::new("a"));
            vol.execute_update();

            // "a" symlink should no longer exist on disk at all
            assert!(!a_link.is_symlink(), "stale symlink 'a' should be removed");
            // "b" should still be a valid symlink through ..data
            assert!(b_link.is_symlink(), "symlink 'b' should still exist");
            assert_eq!(
                std::fs::read_link(&b_link).unwrap(),
                PathBuf::from("..data/b")
            );
        }

        // -- multi-file / realistic scenario --

        #[test]
        fn realistic_secret_mount() {
            let vol = TempProjectedVolume::new(&unique_name("connector_secrets"));

            // Initial mount: dir with two keys
            vol.stage_dir_create(Path::new("fake-ss"));
            vol.stage_file_create(Path::new("fake-ss/testkey"), "secret1");
            vol.stage_file_create(Path::new("fake-ss/anothertestkey"), "secret2");
            vol.execute_update();

            assert_eq!(read_via_top_level(&vol, "fake-ss/testkey"), "secret1");
            assert_eq!(
                read_via_top_level(&vol, "fake-ss/anothertestkey"),
                "secret2"
            );

            // Update one secret
            vol.stage_file_modify(Path::new("fake-ss/testkey"), "updated_secret1");
            vol.execute_update();

            assert_eq!(
                read_via_top_level(&vol, "fake-ss/testkey"),
                "updated_secret1"
            );
            assert_eq!(
                read_via_top_level(&vol, "fake-ss/anothertestkey"),
                "secret2",
                "unmodified secret should be unchanged"
            );
        }

        // -- validation / panic tests --

        #[test_case(Path::new("f"); "root")]
        #[test_case(Path::new("sub/f"); "subdirectory")]
        #[should_panic(expected = "file already exists")]
        fn create_duplicate_file_panics(file_path: &Path) {
            let vol = TempProjectedVolume::new(&unique_name("test"));
            stage_parent_dirs(&vol, file_path);
            vol.stage_file_create(file_path, "v1");
            vol.execute_update();

            vol.stage_file_create(file_path, "v2");
            vol.execute_update();
        }

        #[test]
        #[should_panic(expected = "file does not exist")]
        fn modify_nonexistent_file_panics() {
            let vol = TempProjectedVolume::new(&unique_name("test"));
            vol.stage_file_modify(Path::new("nope"), "val");
            vol.execute_update();
        }

        #[test]
        #[should_panic(expected = "file does not exist")]
        fn remove_nonexistent_file_panics() {
            let vol = TempProjectedVolume::new(&unique_name("test"));
            vol.stage_file_remove(Path::new("nope"));
            vol.execute_update();
        }

        #[test]
        #[should_panic(expected = "directory already exists")]
        fn create_duplicate_dir_panics() {
            let vol = TempProjectedVolume::new(&unique_name("test"));
            vol.stage_dir_create(Path::new("d"));
            vol.execute_update();

            vol.stage_dir_create(Path::new("d"));
            vol.execute_update();
        }

        #[test]
        #[should_panic(expected = "directory does not exist")]
        fn remove_nonexistent_dir_panics() {
            let vol = TempProjectedVolume::new(&unique_name("test"));
            vol.stage_dir_remove(Path::new("nope"));
            vol.execute_update();
        }

        #[test]
        #[should_panic(expected = "directory contains files")]
        fn remove_nonempty_dir_panics() {
            let vol = TempProjectedVolume::new(&unique_name("test"));
            vol.stage_dir_create(Path::new("d"));
            vol.stage_file_create(Path::new("d/f"), "val");
            vol.execute_update();

            vol.stage_dir_remove(Path::new("d"));
            vol.execute_update();
        }

        #[test]
        #[should_panic(expected = "parent directory does not exist")]
        fn create_file_without_parent_dir_panics() {
            let vol = TempProjectedVolume::new(&unique_name("test"));
            vol.stage_file_create(Path::new("nonexistent_dir/file"), "val");
            vol.execute_update();
        }
    }
}
