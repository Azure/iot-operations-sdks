// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

//! File Mount interface for deployment artifacts.

use std::{
    ffi::{OsStr, OsString},
    ops::Deref,
    path::{Path, PathBuf},
    sync::Arc,
    time::Duration,
};

use notify::{EventKind, RecommendedWatcher};
use notify_debouncer_full::{DebounceEventResult, Debouncer, RecommendedCache, new_debouncer};
use tokio::sync::watch;

/// Error with a `FileMount`
#[derive(Debug, thiserror::Error)]
#[error(transparent)]
pub(crate) struct Error(#[from] notify::Error);

/// A path to a file mount that is being monitored for changes.
/// When a change is detected, it will be reported.
#[derive(Clone, Debug)]
pub struct FileMount {
    pathbuf: PathBuf,
    #[allow(dead_code)]
    debouncer: Arc<Debouncer<RecommendedWatcher, RecommendedCache>>,
    update_rx: watch::Receiver<()>,
}

impl FileMount {
    /// Creates a new [`FileMount`] from a given [`PathBuf`] and an aggregation window for
    /// debouncing file system events.
    pub(crate) fn new(pathbuf: PathBuf, aggregation_window: Duration) -> Result<Self, Error> {
        // Internal update infrastructure
        let (update_tx, update_rx) = watch::channel(());
        // NOTE: If there's a need to be able to "subscribe" to the update_tx, will need to wrap it
        // in an Arc and clone it for the debouncer closure and the struct.

        let mut debouncer =
            new_debouncer(aggregation_window, None, move |res: DebounceEventResult| {
                match res {
                    Ok(events) => {
                        if events.iter().any(|e| {
                            // When an asset is added or removed, kubernetes does a series of events:
                            // Create Folder, Create File and Remove Folder
                            // If any of these events are triggered, issue a notifcation.
                            matches!(
                                e.event.kind,
                                EventKind::Remove(_) | EventKind::Create(_) | EventKind::Modify(_)
                            )
                        }) && let Err(e) = update_tx.send(())
                        {
                            // NOTE: This should not happen except perhaps under extremely
                            // tight timing circumstances, such as the contents of the mount
                            // changing during cleanup of the struct.
                            log::warn!("FileMount update notification without receivers: {e:?}");
                        }
                    }
                    Err(err) => {
                        for e in &err {
                            log::error!("Error processing FileMount debounce event: {e:?}");
                        }
                    }
                }
            })?;
        // Begin watching the filepath
        debouncer.watch(pathbuf.clone(), notify::RecursiveMode::NonRecursive)?;

        Ok(Self {
            pathbuf,
            debouncer: Arc::new(debouncer),
            update_rx,
        })
    }

    /// Waits for a change to be detected on the `FileMount`.
    /// Note that this will not report what the change was, only that a change occurred.
    /// Once the change notification is provided, it will be reset until another change is detected.
    pub async fn changed(&mut self) {
        match self.update_rx.changed().await {
            Ok(()) => {}
            Err(_) => unreachable!("FileMount watch channel sender is co-owned by this struct"),
        }
    }

    /// Checks if a change has been detected on the `FileMount` since the last time it was checked.
    /// Note that this will not report what the change was, only that a change occurred.
    /// This will *not* reset the change notification.
    #[must_use]
    pub fn has_changed(&self) -> bool {
        match self.update_rx.has_changed() {
            Ok(changed) => changed,
            Err(_) => unreachable!("FileMount watch channel sender is co-owned by this struct"),
        }
    }

    /// Marks any file changes in the mount as having been seen, resetting the change notification
    /// until another change occurs.
    pub fn mark_changes_seen(&mut self) {
        self.update_rx.mark_unchanged();
    }

    /// Coerces to a [`Path`] slice.
    #[must_use]
    pub fn as_path(&self) -> &Path {
        self
    }
}

impl Deref for FileMount {
    type Target = Path;

    fn deref(&self) -> &Self::Target {
        self.pathbuf.as_path()
    }
}

impl AsRef<Path> for FileMount {
    fn as_ref(&self) -> &Path {
        self.pathbuf.as_path()
    }
}

impl AsRef<OsStr> for FileMount {
    fn as_ref(&self) -> &OsStr {
        self.pathbuf.as_os_str()
    }
}

impl PartialEq<Path> for FileMount {
    fn eq(&self, other: &Path) -> bool {
        self.pathbuf.as_path() == other
    }
}

impl PartialEq<&Path> for FileMount {
    fn eq(&self, other: &&Path) -> bool {
        self.pathbuf.as_path() == *other
    }
}

impl PartialEq<PathBuf> for FileMount {
    fn eq(&self, other: &PathBuf) -> bool {
        self.pathbuf == *other
    }
}

impl PartialEq<OsStr> for FileMount {
    fn eq(&self, other: &OsStr) -> bool {
        self.pathbuf.as_os_str() == other
    }
}

impl PartialEq<&OsStr> for FileMount {
    fn eq(&self, other: &&OsStr) -> bool {
        self.pathbuf.as_os_str() == *other
    }
}

impl PartialEq<OsString> for FileMount {
    fn eq(&self, other: &OsString) -> bool {
        self.pathbuf.as_os_str() == other.as_os_str()
    }
}

impl PartialEq for FileMount {
    fn eq(&self, other: &Self) -> bool {
        self.pathbuf == other.pathbuf
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use crate::deployment_artifacts::test_utils::TempMount;

    #[test]
    fn ref_deref() {
        let fs_mount = TempMount::new("some_path");
        let file_mount = FileMount::new(
            fs_mount.path().to_path_buf(),
            std::time::Duration::from_secs(1),
        )
        .unwrap();

        // Test deref coercion to Path
        let path_ref: &Path = &file_mount;
        assert_eq!(path_ref, fs_mount.path());
        // Test as_ref to Path
        let path_as_ref: &Path = file_mount.as_ref();
        assert_eq!(path_as_ref, fs_mount.path());
        // Test as_ref to OsStr
        let os_str_as_ref: &OsStr = file_mount.as_ref();
        assert_eq!(os_str_as_ref, fs_mount.path().as_os_str());
        // Test as_path method
        assert_eq!(file_mount.as_path(), fs_mount.path());
    }

    #[test]
    fn partial_eq() {
        let fs_mount = TempMount::new("some_path");
        let file_mount = FileMount::new(
            fs_mount.path().to_path_buf(),
            std::time::Duration::from_secs(1),
        )
        .unwrap();

        // Test PartialEq with Path
        assert_eq!(file_mount, *fs_mount.path());
        // Test PartialEq with &Path
        assert_eq!(file_mount, fs_mount.path());
        // Test PartialEq with PathBuf
        assert_eq!(file_mount, fs_mount.path().to_path_buf());
        // Test PartialEq with OsStr
        assert_eq!(file_mount, *fs_mount.path().as_os_str());
        // Test PartialEq with &OsStr
        assert_eq!(file_mount, fs_mount.path().as_os_str());
        // Test PartialEq with OsString
        assert_eq!(file_mount, fs_mount.path().as_os_str().to_os_string());
    }

    #[test]
    fn change_detection_sync() {
        let fs_mount = TempMount::new("some_path");
        let mut file_mount =
            FileMount::new(fs_mount.path().to_path_buf(), Duration::from_millis(100)).unwrap();

        // Prove has_changed() is false without a modification
        assert!(
            !file_mount.has_changed(),
            "has_changed() is true before any filesystem modification"
        );

        // Now modify the filesystem — this should trigger the notification
        fs_mount.add_file("test_file", "test_content");

        // has_changed() should now be true (after a short delay for the notification to be processed)
        std::thread::sleep(Duration::from_millis(200));
        assert!(
            file_mount.has_changed(),
            "has_changed() is false after file modification"
        );

        // Mark changes as seen
        file_mount.mark_changes_seen();

        // Prove has_changed() is false after marking changes as seen
        assert!(
            !file_mount.has_changed(),
            "has_changed() is true after marking changes as seen"
        );

        // Update the file contents to trigger another change
        fs_mount.update_file("test_file", "updated_content");

        // has_changed() should now be true
        std::thread::sleep(Duration::from_millis(200));
        assert!(
            file_mount.has_changed(),
            "has_changed() is false after file update"
        );

        // Mark changes as seen
        file_mount.mark_changes_seen();
        assert!(
            !file_mount.has_changed(),
            "has_changed() is true after marking changes as seen"
        );

        // Remove the file to trigger another change
        fs_mount.remove_file("test_file");

        // has_changed() should now be true
        std::thread::sleep(Duration::from_millis(200));
        assert!(
            file_mount.has_changed(),
            "has_changed() is false after file removal"
        );

        // Mark changes as seen
        file_mount.mark_changes_seen();
        assert!(
            !file_mount.has_changed(),
            "has_changed() is true after marking changes as seen"
        );
    }

    #[tokio::test]
    async fn change_detection_async() {
        let fs_mount = TempMount::new("some_path");
        let mut file_mount =
            FileMount::new(fs_mount.path().to_path_buf(), Duration::from_millis(100)).unwrap();

        // Prove changed() does NOT resolve without a modification
        let result = tokio::time::timeout(Duration::from_millis(200), file_mount.changed()).await;
        assert!(
            result.is_err(),
            "changed() resolved before any filesystem modification"
        );

        // Now modify the filesystem — this should trigger the notification
        fs_mount.add_file("test_file", "test_content");

        // changed() should now resolve
        tokio::time::timeout(Duration::from_secs(5), file_mount.changed())
            .await
            .expect("timed out waiting for change notification");

        // Prove changed() does NOT resolve without another modification
        let result = tokio::time::timeout(Duration::from_millis(200), file_mount.changed()).await;
        assert!(
            result.is_err(),
            "changed() resolved before file modification"
        );

        // Update the file contents to trigger another change
        fs_mount.update_file("test_file", "updated_content");

        // changed() should resolve after modification
        tokio::time::timeout(Duration::from_secs(5), file_mount.changed())
            .await
            .expect("timed out waiting for modification notification");

        // Prove changed() does NOT resolve without another modification
        let result = tokio::time::timeout(Duration::from_millis(200), file_mount.changed()).await;
        assert!(result.is_err(), "changed() resolved before file removal");

        // Remove the file to trigger another change
        fs_mount.remove_file("test_file");

        // changed() should resolve again
        tokio::time::timeout(Duration::from_secs(5), file_mount.changed())
            .await
            .expect("timed out waiting for removal notification");
    }

    #[test]
    fn change_detection_clones() {
        let fs_mount = TempMount::new("some_path");
        let mut file_mount =
            FileMount::new(fs_mount.path().to_path_buf(), Duration::from_millis(100)).unwrap();

        // Clone the FileMount
        let file_mount_clone = file_mount.clone();

        // Now modify the filesystem — this should trigger the notification for both the original and the clone
        fs_mount.add_file("test_file", "test_content");

        // has_changed() should now be true for both the original and the clone (after a short
        // delay for the notification to be processed).
        // Marking the change on the original has no bearing on the clone's detection of the change.
        std::thread::sleep(Duration::from_millis(200));
        assert!(
            file_mount.has_changed(),
            "Original FileMount did not detect change after file modification"
        );
        file_mount.mark_changes_seen();
        assert!(
            file_mount_clone.has_changed(),
            "Cloned FileMount did not detect change after file modification"
        );
    }

    #[test]
    fn clones_copy_pending_changes() {
        let fs_mount = TempMount::new("some_path");
        let mut file_mount =
            FileMount::new(fs_mount.path().to_path_buf(), Duration::from_millis(100)).unwrap();

        // Now modify the filesystem — this should trigger the notification after a delay
        fs_mount.add_file("test_file", "test_content");
        std::thread::sleep(Duration::from_millis(200));
        assert!(
            file_mount.has_changed(),
            "FileMount did not detect change after file modification"
        );

        // Clone the FileMount after the change has been detected but before it has been marked as seen
        let file_mount_clone = file_mount.clone();
        // The new FileMount is identified as having been changed
        assert!(
            file_mount_clone.has_changed(),
            "Cloned FileMount did not detect change that was pending when it was cloned"
        );

        // But marking the change on the original does not mark it as seen for the clone
        file_mount.mark_changes_seen();
        assert!(
            !file_mount.has_changed(),
            "Original FileMount did not mark change as seen"
        );
        assert!(
            file_mount_clone.has_changed(),
            "Cloned FileMount did not maintain pending change status independent of original"
        );
    }
}
