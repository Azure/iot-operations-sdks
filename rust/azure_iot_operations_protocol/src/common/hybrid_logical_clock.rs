// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

use std::{
    fmt::{self, Display},
    str::FromStr,
    time::{Duration, SystemTime, UNIX_EPOCH},
};

use uuid::Uuid;

use super::aio_protocol_error::AIOProtocolError;

// TODO: Placeholder. Implement
/// Hybrid Logical Clock (HLC) generating unique timestamps
#[derive(Clone, Debug, PartialEq)]
pub struct HybridLogicalClock {
    /// Current timestamp.
    pub timestamp: SystemTime,
    /// Counter is used to coordinate ordering of events within a distributed system where each
    /// device may have slightly different system clock times.
    pub counter: u64,
    /// Unique identifier for this node.
    pub node_id: Uuid,
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
            node_id: Uuid::new_v4(),
        }
    }
    // ...
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

        // Validate third part (node_id)
        let node_id = match Uuid::try_parse(parts[2]) {
            Ok(uuid) => uuid,
            Err(e) => {
                return Err(AIOProtocolError::new_header_invalid_error(
                    "HybridLogicalClock",
                    s,
                    false,
                    None,
                    Some(format!(
                        "Malformed HLC. Could not parse third segment as a UUID: {e}"
                    )),
                    None,
                ));
            }
        };

        Ok(Self {
            timestamp,
            counter,
            node_id,
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
            node_id: Uuid::nil(),
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
