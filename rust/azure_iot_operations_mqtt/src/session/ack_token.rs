// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

use std::sync::Arc;

use async_trait::async_trait;

use crate::control_packet::Publish;
use crate::error::AckError;
use crate::interface;
use crate::session::pub_tracker;

/// Awaitable token indicating completion of MQTT message acknowledgement.
pub struct AckToken {
    /// Tracker for unacked incoming publishes
    pub(crate) pub_tracker: Arc<pub_tracker::PubTracker>,
    /// Publish to be acknowledged
    pub(crate) publish: Publish,
}

#[async_trait]
impl interface::AckToken for AckToken {
    /// Acknowledge the received Publish message and return a `[CompletionToken]` for the
    /// completion of the acknowledgement process.
    ///
    /// # Errors
    /// Returns an [`AckError`] if the Publish message could not be acknowledged.
    async fn ack(self) -> Result<interface::CompletionToken, AckError> {
        self.pub_tracker.ack(&self.publish).await?;
        // TODO: This CompletionToken is spurious. We don't (yet) have a way to
        // generate a CompletionToken at MQTT client level for the ack.
        Ok(interface::CompletionToken(Box::new(async { Ok(()) })))
    }
}

impl Drop for AckToken {
    fn drop(&mut self) {
        tokio::task::spawn({
            let pub_tracker = self.pub_tracker.clone();
            let publish = self.publish.clone();
            async move {
                if let Err(e) = pub_tracker.ack(&publish).await {
                    log::error!("Failed to ack incoming publish: {:?}", e);
                }
            }
        });
    }
}
