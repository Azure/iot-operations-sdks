// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

//! Types used for testing deployment artifacts

use std::path::{Path, PathBuf};
use tempfile::TempDir;

/// Simulates a file mount directory using a temporary directory.
pub struct TempMount {
    dir: TempDir,
}

impl TempMount {
    pub fn new(dir_name: &str) -> Self {
        let dir = tempfile::TempDir::with_prefix(dir_name).unwrap();
        Self { dir }
        // TODO: Add symlink simulation. Currently this doesn't work, because
        // trying to add a ".." file is interpreted as trying to go up a level
        // in the directory structure.
        //let ret = Self { dir };
        // Create a ".." file to simulate a symlink in a mounted directory
        //ret.add_file("..", "");
        //ret
    }

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


/// Simulates a Kubernetes projected volume using temporary directories for testing purposes.
/// This creates real data on the filesystem in a temporary directory, but when dropped, all data
/// will be removed from the filesystem.
pub struct TempProjectedVolume {
    dir: TempDir,
}

impl TempProjectedVolume {
    /// Create a new TempProjectedVolume with the given directory name.
    pub fn new(dir_name: &str) -> Self {
        let dir = tempfile::TempDir::with_prefix(dir_name).unwrap();
        Self { dir }
    }

    /// Return the path of the projected volume mount
    pub fn path(&self) -> &Path { self.dir.path() }

    /// Stage a file creation in the projected volume.
    /// If the relative path includes subdirectories, they must already exist.
    pub fn stage_file_create(&self, file_path: &Path, contents: &str) { unimplemented!() }

    /// Stage a file modification in the projected volume. The file must already exist.
    pub fn stage_file_modify(&self, file_path: &Path, contents: &str) { unimplemented!() }

    /// Stage a file removal in the projected volume. The file must already exist.
    pub fn stage_file_remove(&self, file_path: &Path) { unimplemented!() }

    /// Stage a directory creation in the projected volume. The directory must not already exist.
    pub fn stage_dir_create(&self, dir_path: &Path) { unimplemented!() }

    /// Stage a directory removal in the projected volume. The directory must already exist and be empty.
    pub fn stage_dir_remove(&self, dir_path: &Path) { unimplemented!() }

    /// Trigger an update of all staged changes to the projected volume
    pub fn execute_update(&self) { unimplemented!() }

}