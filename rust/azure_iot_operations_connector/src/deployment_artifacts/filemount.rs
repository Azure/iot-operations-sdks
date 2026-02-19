// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

//! File Mount interface for deployment artifacts.

use std::{
    ffi::OsStr,
    ops::Deref,
    path::{Path, PathBuf},
    time::Duration,
    sync::Arc,
};

use notify::{EventKind, RecommendedWatcher};
use notify_debouncer_full::{DebounceEventResult, Debouncer, RecommendedCache, new_debouncer};
use tokio::sync::watch;

/// Error with a FileMount
#[derive(Debug, thiserror::Error)]
#[error(transparent)]
pub struct Error(#[from] notify::Error);

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
    pub fn new(pathbuf: PathBuf, aggregation_window: Duration) -> Result<Self, Error> {
        // Internal update infrastructure
        let (update_tx, update_rx) = watch::channel(());
        // NOTE: If there's a need to be able to "subscribe" to the update_tx, will need to wrap it
        // in an Arc and clone it for the debouncer closure and the struct.
    
        let mut debouncer = new_debouncer(
            aggregation_window,
            None,
            move |res: DebounceEventResult| {
                match res {
                    Ok(events) => {
                        if events.iter().any(|e| {
                            // When an asset is added or removed, kubernetes does a series of events:
                            // Create Folder, Create File and Remove Folder
                            // If any of these events are triggered, issue a notifcation.
                            !matches!(
                                e.event.kind,
                                EventKind::Remove(_) | EventKind::Create(_) | EventKind::Modify(_)
                            )
                        }) {
                            if let Err(e) = update_tx.send(()) {
                                // NOTE: This should not happen except under extremely tight timing
                                // circumstances, such as the contents of the mount changing during
                                // cleanup of the struct.
                                log::warn!("FileMount update notification without receivers: {e:?}");
                            }
                        }
                    }
                    Err(err) => {
                        for e in &err {
                            log::error!("Error processing FileMount debounce event: {e:?}");
                        }
                    }
                }
            },
        )?;
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
    pub async fn changed(&mut self) {
        // NOTE: It is impossible for this watch channel to be closed while this struct exists,
        // so we can unwrap the result without issue.
        self.update_rx.changed().await.unwrap();
    }

    /// Coerces to a [`Path`] slice.
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
