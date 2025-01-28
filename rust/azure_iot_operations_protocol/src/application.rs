// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

//! Application-wide utilities for use with the Azure IoT Operations SDK.

use std::sync::{Arc, Mutex};

use super::common::hybrid_logical_clock::HybridLogicalClock;

const DEFAULT_MAX_CLOCK_DRIFT: u64 = 60;

/// Struct containing the application-level [`HybridLogicalClock`].
pub struct ApplicationHybridLogicalClock {
    /// The [`HybridLogicalClock`] used by the application, wrapped in a Mutex to allow for concurrent access.
    #[allow(unused)] // TODO: Remove once HybridLogicalClock is implemented
    hlc: Mutex<HybridLogicalClock>,
}

// TODO: Pending implementation, dependent on the HybridLogicalClock full implementation
impl ApplicationHybridLogicalClock {
    /// Creates a new [`ApplicationHybridLogicalClock`] with the provided maximum clock drift.
    #[must_use]
    pub fn new(_max_clock_drift: u64) -> Self {
        Self {
            hlc: Mutex::new(HybridLogicalClock::new()),
        }
    }

    /// Reads the current value of the [`ApplicationHybridLogicalClock`].
    ///
    /// Returns an instant of the current [`HybridLogicalClock`] on success.
    ///
    /// # Errors
    /// TODO: Add errors once [`HybridLogicalClock`] is implemented
    #[allow(unused, clippy::unused_self)]
    pub fn read(&mut self) -> Result<HybridLogicalClock, String> {
        unimplemented!();
    }

    /// Updates the [`ApplicationHybridLogicalClock`] with the provided [`HybridLogicalClock`].
    ///
    /// Returns `Ok(())` on success.
    ///
    /// # Errors
    /// TODO: Add errors once [`HybridLogicalClock`] is implemented
    #[allow(unused, clippy::unused_self)]
    pub(crate) fn update(&mut self, _hlc: &HybridLogicalClock) -> Result<(), String> {
        unimplemented!();
    }
}

/// Options for creating an [`ApplicationContext`].
#[derive(Builder)]
pub struct ApplicationContextOptions {
    /// The maximum clock drift allowed for the [`ApplicationHybridLogicalClock`].
    #[builder(default = DEFAULT_MAX_CLOCK_DRIFT)]
    pub max_clock_drift: u64,
}

/// Struct containing the application context for the Azure IoT Operations SDK.
///
/// <div class="warning"> There must be a max of one per session and there should only be one per application (which may contain multiple sessions). </div>
#[derive(Clone)]
pub struct ApplicationContext {
    /// The [`ApplicationHybridLogicalClock`] used by the application.
    #[allow(unused)]
    pub application_hlc: Arc<ApplicationHybridLogicalClock>,
}

impl ApplicationContext {
    /// Creates a new [`ApplicationContext`] with the provided options.
    #[must_use]
    #[allow(clippy::needless_pass_by_value)]
    pub fn new(options: ApplicationContextOptions) -> Self {
        Self {
            application_hlc: Arc::new(ApplicationHybridLogicalClock::new(options.max_clock_drift)),
        }
    }
}
