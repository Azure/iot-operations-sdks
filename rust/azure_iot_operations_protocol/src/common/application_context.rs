// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

use std::sync::{Arc, RwLock};

use super::hybrid_logical_clock::HybridLogicalClock;

const DEFAULT_MAX_CLOCK_DRIFT: u64 = 60;

/// Options for creating an [`ApplicationContext`].
#[derive(Builder)]
pub struct ApplicationContextOptions {
    /// The maximum clock drift allowed for the Hybrid Logical Clock
    #[builder(default = DEFAULT_MAX_CLOCK_DRIFT)]
    pub max_clock_drift: u64,
}

/// Struct containing the application context for the Azure IoT Operations SDK.
///
/// <div class="warning"> There should only be one `ApplicationContext` per session and application. </div>
#[derive(Clone)]
pub struct ApplicationContext {
    /// The Hybrid Logical Clock used by the application
    #[allow(unused)]
    application_hlc: Arc<RwLock<HybridLogicalClock>>,
}

impl ApplicationContext {
    /// Creates a new `ApplicationContext` with the provided options.
    #[must_use]
    pub fn new(_options: ApplicationContextOptions) -> Self {
        // TODO: Implement max clock drift on HLC
        Self {
            application_hlc: Arc::new(RwLock::new(HybridLogicalClock::new())),
        }
    }

    /// Returns the Hybrid Logical Clock used by the application.
    pub fn get_hlc(&self) -> HybridLogicalClock {
        // TODO: Implement HLC read
        unimplemented!()
    }

    /// Updates the Hybrid Logical Clock used by the application.
    pub(crate) fn set_hlc(&self, _hlc: HybridLogicalClock) {
        // TODO: Implement HLC update
        unimplemented!()
    }
}
