// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

//! Application-wide utilities for use with the Azure IoT Operations SDK.

use std::{
    sync::{Arc, Mutex},
    time::Duration,
};

use crate::common::{
    aio_protocol_error::AIOProtocolError,
    hybrid_logical_clock::{HybridLogicalClock, DEFAULT_MAX_CLOCK_DRIFT},
};

/// Struct containing the application-level [`HybridLogicalClock`].
pub struct ApplicationHybridLogicalClock {
    /// The [`HybridLogicalClock`] used by the application, wrapped in a Mutex to allow for concurrent access.
    #[allow(unused)] // TODO: Remove once HybridLogicalClock is implemented
    hlc: Mutex<HybridLogicalClock>,
    /// The maximum clock drift allowed for the [`HybridLogicalClock`].
    max_clock_drift: Duration,
}

// TODO: Pending implementation, dependent on the HybridLogicalClock full implementation
impl ApplicationHybridLogicalClock {
    /// Creates a new [`ApplicationHybridLogicalClock`] with the provided maximum clock drift.
    #[must_use]
    pub fn new(max_clock_drift: Duration) -> Self {
        Self {
            hlc: Mutex::new(HybridLogicalClock::new()),
            max_clock_drift,
        }
    }

    /// Reads the current value of the [`ApplicationHybridLogicalClock`] and returns a copy.
    ///
    /// Returns an instant of the current [`HybridLogicalClock`] on success.
    ///
    /// # Errors
    /// TODO: Add errors once [`HybridLogicalClock`] is implemented
    /// # Panics
    /// TODO
    pub fn read(&self) -> HybridLogicalClock {
        self.hlc.lock().unwrap().clone()
    }

    /// Updates the [`ApplicationHybridLogicalClock`] with the provided [`HybridLogicalClock`].
    ///
    /// Returns `Ok(())` on success.
    /// TODO: Errors
    pub(crate) fn update(&self, other_hlc: &HybridLogicalClock) -> Result<(), AIOProtocolError> {
        self.hlc
            .lock()
            .unwrap()
            .update(other_hlc, self.max_clock_drift)
    }

    /// Updates the [`ApplicationHybridLogicalClock`] with the current time and returns a string representation of the updated HLC.
    ///
    /// TODO: Errors
    pub(crate) fn update_now(&self) -> Result<String, AIOProtocolError> {
        let mut hlc = self.hlc.lock().unwrap();
        hlc.update_now(self.max_clock_drift)?;
        Ok(hlc.to_string())
    }
}

/// Options for creating an [`ApplicationContext`].
#[derive(Builder)]
pub struct ApplicationContextOptions {
    /// The maximum clock drift allowed for the [`ApplicationHybridLogicalClock`].
    #[builder(default = "DEFAULT_MAX_CLOCK_DRIFT")]
    pub max_clock_drift: Duration,
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
