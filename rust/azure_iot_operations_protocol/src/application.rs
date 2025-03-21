// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

//! Application-wide utilities for use with the Azure IoT Operations SDK.

use std::{
    sync::{Arc, Mutex},
    time::Duration,
};

use crate::common::hybrid_logical_clock::{DEFAULT_MAX_CLOCK_DRIFT, HLCError, HybridLogicalClock};

/// Struct containing the application-level [`HybridLogicalClock`].
pub struct ApplicationHybridLogicalClock {
    /// The [`HybridLogicalClock`] used by the application, wrapped in a Mutex to allow for concurrent access.
    hlc: Mutex<HybridLogicalClock>,
    /// The maximum clock drift allowed for the application's [`HybridLogicalClock`] validations.
    max_clock_drift: Duration,
}

impl ApplicationHybridLogicalClock {
    /// Creates a new [`ApplicationHybridLogicalClock`] with the provided maximum clock drift.
    #[must_use]
    pub fn new(max_clock_drift: Duration) -> Self {
        Self {
            hlc: Mutex::new(HybridLogicalClock::new()),
            max_clock_drift,
        }
    }

    /// Reads the current value of the [`ApplicationHybridLogicalClock`]
    /// and returns a new [`HybridLogicalClock`] that is a snapshot of
    /// the current value of the [`ApplicationHybridLogicalClock`].
    ///
    /// # Panics
    /// if the lock on the [`ApplicationHybridLogicalClock`] is poisoned,
    /// which should not be possible
    pub fn read(&self) -> HybridLogicalClock {
        self.hlc.lock().unwrap().clone()
    }

    /// Updates the [`ApplicationHybridLogicalClock`] based on the provided other [`HybridLogicalClock`].
    /// The [`ApplicationHybridLogicalClock`] will be set to the latest timestamp between itself, the
    /// other [`HybridLogicalClock`], and the current time, and its counter will also be updated accordingly.
    ///
    /// # Errors
    /// [`HLCError`] of kind [`OverflowWarning`](crate::common::hybrid_logical_clock::HLCErrorKind::OverflowWarning) if
    /// the [`ApplicationHybridLogicalClock`]'s counter would be set to a value that would overflow beyond [`u64::MAX`]
    ///
    /// [`HLCError`] of kind [`ClockDrift`](crate::common::hybrid_logical_clock::HLCErrorKind::ClockDrift) if
    /// the latest [`HybridLogicalClock`] (of [`ApplicationHybridLogicalClock`] or `other`)'s timestamp is too far in
    /// the future (determined by [`max_clock_drift`](ApplicationHybridLogicalClock::max_clock_drift)) compared to `SystemTime::now()`
    pub(crate) fn update(&self, other_hlc: &HybridLogicalClock) -> Result<(), HLCError> {
        self.hlc
            .lock()
            .unwrap()
            .update(other_hlc, self.max_clock_drift)
    }

    /// Updates the [`ApplicationHybridLogicalClock`] with the current time and returns a `String` representation of the updated [`ApplicationHybridLogicalClock`].
    ///
    /// # Errors
    /// [`HLCError`] of kind [`OverflowWarning`](crate::common::hybrid_logical_clock::HLCErrorKind::OverflowWarning) if
    /// the [`HybridLogicalClock`]'s counter would be incremented and overflow beyond [`u64::MAX`]
    ///
    /// [`HLCError`] of kind [`ClockDrift`](crate::common::hybrid_logical_clock::HLCErrorKind::ClockDrift) if
    /// the [`ApplicationHybridLogicalClock`]'s timestamp is too far in the future (determined
    /// by [`max_clock_drift`](ApplicationHybridLogicalClock::max_clock_drift)) compared to `SystemTime::now()`
    pub(crate) fn update_now(&self) -> Result<String, HLCError> {
        let mut hlc = self.hlc.lock().unwrap();
        hlc.update_now(self.max_clock_drift)?;
        Ok(hlc.to_string())
    }
}

/// Struct containing the application context for the Azure IoT Operations SDK.
///
/// <div class="warning"> There must be a max of one per session and there should only be one per application (which may contain multiple sessions). </div>
#[derive(Builder, Clone)]
pub struct ApplicationContext {
    /// The [`ApplicationHybridLogicalClock`] used by the application.
    #[builder(default = "Arc::new(ApplicationHybridLogicalClock::new(DEFAULT_MAX_CLOCK_DRIFT))")]
    pub application_hlc: Arc<ApplicationHybridLogicalClock>,
}
