// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

//! Reconnect policies for a [`Session`](crate::session::Session).

use std::time::Duration;

use rand::Rng;

use crate::control_packet::Disconnect;
use crate::error::{ConnectError, ProtocolError};

/// Reason for connection loss.
pub enum ConnectionLossReason {
    /// Disconnected by server with DISCONNECT packet.
    DisconnectByServer(Disconnect),
    /// Disconnected due to an I/O error.
    IoError(std::io::Error),
    /// Disconnected due to a protocol error committed by the server.
    ProtocolError(ProtocolError),
}

/// Trait defining interface for reconnect policies.
pub trait ReconnectPolicy: Send {
    /// Get the next reconnect delay after a failure to connect.
    /// Returns None if no reconnect should be attempted.
    fn connect_failure_reconnect_delay(
        &self,
        prev_attempts: u32,
        error: &ConnectError,
    ) -> Option<Duration>;

    /// Get the next reconnect delay after a connection loss.
    /// Returns None if no reconnect should be attempted.
    fn connection_loss_reconnect_delay(&self, reason: &ConnectionLossReason) -> Option<Duration>;
}

/// A reconnect policy that will exponentially backoff the the delay between reconnect attempts.
///
/// Reconnects will range from 128ms to the specified max wait time, before applying jitter.
//  Jitter can subtract up to 10% of the delay
#[derive(Clone)]
pub struct ExponentialBackoffWithJitter {
    /// The longest possible time to wait between reconnect attempts.
    pub max_wait: Duration,
    /// The max number of reconnect attempts before giving up.
    pub max_reconnect_attempts: Option<u32>,
}

impl ExponentialBackoffWithJitter {
    const MIN_EXPONENT: u32 = 7;
    const BASE_DELAY_MS: u64 = 2;

    /// Determine if a reconnect should be attempted.
    fn should_reconnect(&self, prev_attempts: u32, _error: &ConnectError) -> bool {
        if let Some(max_attempts) = self.max_reconnect_attempts {
            prev_attempts < max_attempts
        } else {
            true
        }
    }

    /// Calculate the delay for the next reconnect attempt.
    fn calculate_delay(&self, prev_attempts: u32) -> Duration {
        // Exponent cannot be less than 7
        // This is to prevent the delay from being too short.
        let exponent = prev_attempts.saturating_add(Self::MIN_EXPONENT);
        let interval =
            Duration::from_millis(Self::BASE_DELAY_MS.saturating_pow(exponent)).min(self.max_wait);

        // Add jitter to prevent multiple clients from reconnecting at the same time
        // NOTE: This number may biased. If this is an issue, look at different ways to generate jitter.
        let jitter_multiplier = rand::thread_rng().gen_range(0.90..=1.0);
        interval.mul_f64(jitter_multiplier)
    }
}

impl Default for ExponentialBackoffWithJitter {
    /// Indefinite reconnect, with a max wait time of 60 seconds.
    fn default() -> Self {
        Self {
            max_wait: Duration::from_secs(60),
            max_reconnect_attempts: None,
        }
    }
}

impl ReconnectPolicy for ExponentialBackoffWithJitter {
    fn connect_failure_reconnect_delay(
        &self,
        prev_attempts: u32,
        error: &ConnectError,
    ) -> Option<Duration> {
        if self.should_reconnect(prev_attempts, error) {
            Some(self.calculate_delay(prev_attempts))
        } else {
            None
        }
    }

    fn connection_loss_reconnect_delay(&self, _reason: &ConnectionLossReason) -> Option<Duration> {
        Some(Duration::from_secs(0))
    }
}
