// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

use std::{
    fmt::{self, Display},
    str::FromStr,
    time::{Duration, SystemTime, UNIX_EPOCH},
};

use uuid::Uuid;

use super::aio_protocol_error::AIOProtocolError;

pub const DEFAULT_MAX_CLOCK_DRIFT: Duration = Duration::from_secs(60);

/// Hybrid Logical Clock (HLC) generating unique timestamps
#[derive(Clone, Debug, PartialEq)]
pub struct HybridLogicalClock {
    /// Current timestamp.
    pub timestamp: SystemTime,
    /// Counter is used to coordinate ordering of events within a distributed system where each
    /// device may have slightly different system clock times.
    pub counter: u64,
    /// Unique identifier for this node.
    pub node_id: String,
}

impl Default for HybridLogicalClock {
    fn default() -> Self {
        Self::new()
    }
}

impl HybridLogicalClock {
    /// Creates a new [`HybridLogicalClock`] with the current timestamp, a counter of 0,
    /// and a unique identifier
    #[must_use]
    pub fn new() -> Self {
        Self {
            timestamp: SystemTime::now(),
            counter: 0,
            node_id: Uuid::new_v4().to_string(),
        }
    }

    // (G?) Get - syncs the HLC to the current time and returns it
    // (G - only adds locking) Set - syncs the HLC to the given HLC

    // compare
    // equals?

    // Update - updates the HLC based on another one
    pub fn update(
        &mut self,
        other: &HybridLogicalClock,
        max_clock_drift: Duration,
    ) -> Result<(), AIOProtocolError> {
        // Don't update from the same node.
        if self.node_id != other.node_id {
            return Ok(());
        }

        let now = SystemTime::now();

        // if now is the latest timestamp in the future, set the time to that and reset the counter
        if now > self.timestamp && now > other.timestamp {
            self.timestamp = now;
            self.counter = 0;
        }
        // if the timestamps are equal, take the max of the counters and increment by 1
        else if other.timestamp == self.timestamp {
            if self.counter >= other.counter {
                self.validate(now, max_clock_drift)?;
                self.counter += 1;
            } else {
                // timestamp matches, so validating other implicitly validates self.timestamp
                other.validate(now, max_clock_drift)?;
                self.counter = other.counter + 1;
            }
        }
        // if this timestamp is the latest, increase the counter by 1
        else if self.timestamp > other.timestamp {
            self.validate(now, max_clock_drift)?;
            self.counter += 1;
        }
        // if the other timestamp is the latest, set the time to that use the other counter + 1
        else if other.timestamp > self.timestamp {
            other.validate(now, max_clock_drift)?;
            self.timestamp = other.timestamp;
            self.counter = other.counter + 1;
        }

        Ok(())
        // // Don't update from the same node.
        // if self.node_id != other.node_id {
        //     return Ok(());
        // }

        // let now = SystemTime::now();

        // // Validate both timestamps prior to updating; this guarantees that neither
        // // will cause an integer overflow, and because the later timestamp will
        // // always be chosen by the update, it also preemptively verifies the final
        // // clock skew.
        // self.validate(now)?;
        // other.validate(now)?;

        // // if now is the latest timestamp in the future, set the time to that and reset the counter
        // if now > self.timestamp && now > other.timestamp {
        //     self.timestamp = now;
        //     self.counter = 0;
        // }
        // // if the timestamps are equal, take the max of the counters and increment by 1
        // else if other.timestamp == self.timestamp {
        //     self.counter = self.counter.max(other.counter) + 1;
        // }
        // // if the this timestamp is the latest, increase the counter by 1
        // else if other.timestamp > self.timestamp {
        //     self.counter += 1;
        // }
        // // if the other timestamp is the latest, set the time to that use the other counter + 1
        // else if other.timestamp > self.timestamp {
        //     self.timestamp = other.timestamp;
        //     self.counter = other.counter + 1;
        // }

        // self.validate(now)?;

        // Ok(())
        // let mut new_hlc = self.clone();
        // if other.timestamp > self.timestamp {
        //     new_hlc.timestamp = other.timestamp;
        //     new_hlc.counter = 0;
        // } else if other.timestamp == self.timestamp {
        //     new_hlc.counter = self.counter.max(other.counter) + 1;
        // } else {
        //     new_hlc.counter = 0;
        // }
        // new_hlc
    }

    // Update now - updates the HLC based on the current time
    pub fn update_now(&mut self, max_clock_drift: Duration) -> Result<(), AIOProtocolError> {
        let now = SystemTime::now();

        // if now later than self, set the time to that and reset the counter
        if now > self.timestamp {
            self.timestamp = now;
            self.counter = 0;
        } else {
            self.validate(now, max_clock_drift)?;
            self.counter += 1;
        }
        Ok(())
    }

    /// Validates that the HLC is not too far in the future compared to the current time,
    /// and that the counter has not overflowed.
    pub fn validate(
        &self,
        now: SystemTime,
        max_clock_drift: Duration,
    ) -> Result<(), AIOProtocolError> {
        if self.counter == u64::MAX {
            return Err(AIOProtocolError::new_internal_logic_error(
                true,
                false,
                None,
                None,
                "Counter",
                None,
                Some("Integer overflow on HybridLogicalClock counter".to_string()),
                None,
            ));
        }
        if let Ok(diff) = self.timestamp.duration_since(now) {
            if diff > max_clock_drift {
                return Err(AIOProtocolError::new_state_invalid_error(
                    "MaxClockDrift",
                    None,
                    Some(
                        "HybridLogicalClock drift is greater than the maximum allowed drift"
                            .to_string(),
                    ),
                    None,
                ));
            }
        } // else negative time difference is ok, we only care if the HLC is too far in the future

        Ok(())
    }
}

impl Display for HybridLogicalClock {
    fn fmt(&self, f: &mut fmt::Formatter<'_>) -> fmt::Result {
        let ms_since_epoch = self
            .timestamp
            .duration_since(UNIX_EPOCH)
            .unwrap()
            .as_millis();
        write!(
            f,
            "{:0>15}:{:0>5}:{}",
            ms_since_epoch, self.counter, self.node_id
        )
    }
}

impl FromStr for HybridLogicalClock {
    type Err = AIOProtocolError;

    fn from_str(s: &str) -> Result<Self, AIOProtocolError> {
        let parts: Vec<&str> = s.split(':').collect();
        if parts.len() != 3 {
            return Err(AIOProtocolError::new_header_invalid_error(
                "HybridLogicalClock",
                s,
                false,
                None,
                None,
                None,
            ));
        }

        // Validate first part (timestamp)
        let ms_since_epoch = match parts[0].parse::<u64>() {
            Ok(ms) => ms,
            Err(e) => {
                return Err(AIOProtocolError::new_header_invalid_error(
                    "HybridLogicalClock",
                    s,
                    false,
                    None,
                    Some(format!(
                        "Malformed HLC. Could not parse first segment as an integer: {e}"
                    )),
                    None,
                ));
            }
        };
        let Some(timestamp) = UNIX_EPOCH.checked_add(Duration::from_millis(ms_since_epoch)) else {
            return Err(AIOProtocolError::new_header_invalid_error(
                "HybridLogicalClock",
                s,
                false,
                None,
                Some("Malformed HLC. Timestamp is out of range.".to_string()),
                None,
            ));
        };

        // Validate second part (counter)
        let counter = match parts[1].parse::<u64>() {
            Ok(val) => val,
            Err(e) => {
                return Err(AIOProtocolError::new_header_invalid_error(
                    "HybridLogicalClock",
                    s,
                    false,
                    None,
                    Some(format!(
                        "Malformed HLC. Could not parse second segment as an integer: {e}"
                    )),
                    None,
                ));
            }
        };

        // The node_id is just the third section as a string

        Ok(Self {
            timestamp,
            counter,
            node_id: parts[2].to_string(),
        })
    }
}

#[cfg(test)]
mod tests {
    use crate::common::hybrid_logical_clock::HybridLogicalClock;
    use std::time::UNIX_EPOCH;
    use uuid::Uuid;

    #[test]
    fn test_new_defaults() {
        let hlc = HybridLogicalClock::new();
        assert_eq!(hlc.counter, 0);
    }

    #[test]
    fn test_display() {
        let hlc = HybridLogicalClock {
            timestamp: UNIX_EPOCH,
            counter: 0,
            node_id: Uuid::nil().to_string(),
        };
        assert_eq!(
            hlc.to_string(),
            "000000000000000:00000:00000000-0000-0000-0000-000000000000"
        );
    }

    #[test]
    #[ignore] // currently HLC::new() doesn't round to the millisecond, but to_string() does. This test will fail until that is fixed.
    fn test_to_from_str() {
        let hlc = HybridLogicalClock::new();
        let hlc_str = hlc.to_string();
        let parsed_hlc = hlc_str.parse::<HybridLogicalClock>().unwrap();
        assert_eq!(parsed_hlc, hlc);
    }
}
