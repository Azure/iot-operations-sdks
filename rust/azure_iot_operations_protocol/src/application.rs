// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

//! Application-wide utilities for use with the Azure IoT Operations SDK.

use std::{sync::Arc, time::Duration};

use tokio::sync::Mutex;

use crate::common::{
    aio_protocol_error::AIOProtocolError,
    hybrid_logical_clock::{HybridLogicalClock, DEFAULT_MAX_CLOCK_DRIFT},
};

/// Struct containing the application-level [`HybridLogicalClock`].
pub struct ApplicationHybridLogicalClock {
    /// The [`HybridLogicalClock`] used by the application, wrapped in a Mutex to allow for concurrent access.
    hlc: Mutex<HybridLogicalClock>,
    /// The maximum clock drift allowed for the [`HybridLogicalClock`].
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

    /// Reads the current value of the [`ApplicationHybridLogicalClock`] and returns a copy.
    pub async fn read(&self) -> HybridLogicalClock {
        self.hlc.lock().await.clone()
    }

    /// Updates the [`ApplicationHybridLogicalClock`] with the provided [`HybridLogicalClock`].
    ///
    /// Returns `Ok(())` on success.
    /// TODO: Errors
    pub(crate) async fn update(
        &self,
        other_hlc: &HybridLogicalClock,
    ) -> Result<(), AIOProtocolError> {
        self.hlc
            .lock()
            .await
            .update(other_hlc, self.max_clock_drift)
    }

    /// Updates the [`ApplicationHybridLogicalClock`] with the current time and returns a string representation of the updated HLC.
    ///
    /// TODO: Errors
    pub(crate) async fn update_now(&self) -> Result<String, AIOProtocolError> {
        let mut hlc = self.hlc.lock().await;
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
