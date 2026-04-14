// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

//! A generic wrapper for values that can be monitored for live updates.

use std::sync::Arc;

use tokio::sync::watch;

/// Error returned by [`Watched::changed()`] when the sender has been dropped.
#[derive(Debug, thiserror::Error)]
#[error("watched value sender was dropped — no further updates will arrive")]
pub struct WatchedClosedError;

/// A handle to a value that may be updated asynchronously.
///
/// Provides synchronous access to the current value via [`borrow()`](Watched::borrow) and
/// asynchronous notification of changes via [`changed()`](Watched::changed).
///
/// Follows the same read-lock semantics as [`std::sync::RwLock`]: multiple [`borrow()`](Watched::borrow)
/// calls can be held concurrently, but an update will block until all outstanding borrows
/// are dropped. Keep borrows short-lived.
///
/// Cloning a `Watched<T>` creates an independent handle with its own change-tracking
/// state — if one clone consumes a notification, the other still has it pending.
#[derive(Clone)]
pub struct Watched<T> {
    rx: watch::Receiver<T>,
    /// Optional handle to a resource that must remain alive for this `Watched` to
    /// continue receiving updates. The held value is never accessed — only its
    /// lifetime matters.
    _resource_keepalive: Option<Arc<dyn Send + Sync>>,
}

impl<T> Watched<T> {
    /// Creates a new `Watched<T>` from a [`watch::Receiver`] and an optional keepalive handle.
    ///
    /// If a `resource_keepalive` is provided, it will be held alive for as long as any
    /// clone of this `Watched<T>` exists.
    pub(crate) fn new(
        rx: watch::Receiver<T>,
        resource_keepalive: Option<Arc<dyn Send + Sync>>,
    ) -> Self {
        Self {
            rx,
            _resource_keepalive: resource_keepalive,
        }
    }

    /// Returns a reference guard to the current value.
    ///
    /// The returned [`WatchedRef`] dereferences to `T` and holds a read lock on the
    /// inner value. Drop it promptly to avoid blocking updates.
    #[must_use]
    pub fn borrow(&self) -> WatchedRef<'_, T> {
        WatchedRef(self.rx.borrow())
    }

    /// Waits until the value has been updated since the last call to `changed()` or
    /// since this handle was created.
    ///
    /// # Errors
    ///
    /// Returns `Err` if the sender half has been dropped, meaning no further updates
    /// will ever arrive.
    pub async fn changed(&mut self) -> Result<(), WatchedClosedError> {
        self.rx.changed().await.map_err(|_| WatchedClosedError)
    }
}

/// A reference guard to the current value inside a [`Watched<T>`].
///
/// Dereferences to `T`. Holds a read lock — drop promptly.
pub struct WatchedRef<'a, T>(watch::Ref<'a, T>);

impl<T> std::ops::Deref for WatchedRef<'_, T> {
    type Target = T;

    fn deref(&self) -> &T {
        &self.0
    }
}
