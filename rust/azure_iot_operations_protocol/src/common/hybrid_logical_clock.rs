// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

use std::{
    fmt::{self, Display},
    str::FromStr,
    time::{Duration, SystemTime, UNIX_EPOCH},
};

use thiserror::Error;
use uuid::Uuid;

/// Recommended default value for max clock drift if not specified.
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
            timestamp: now_ms_precision(),
            counter: 0,
            node_id: Uuid::new_v4().to_string(),
        }
    }

    /// Updates the [`HybridLogicalClock`] based on another [`HybridLogicalClock`].
    /// Self will be set to the latest timestamp between itself, other, and the current time, and
    /// its counter will also be updated accordingly.
    ///
    /// Note: Update performed against another [`HybridLogicalClock`] with the same [`node_id`](HybridLogicalClock::node_id)
    /// is a no-op, and will not result in an error.
    ///
    /// # Errors
    /// [`HLCError`] of kind [`OverflowWarning`](HLCErrorKind::OverflowWarning) if
    /// the [`HybridLogicalClock`]'s counter would be set to a value that would overflow beyond [`u64::MAX`]
    ///
    /// [`HLCError`] of kind [`ClockDrift`](HLCErrorKind::ClockDrift) if the latest [`HybridLogicalClock`]
    /// (of `Self` or `other`)'s timestamp is too far in the future (determined by `max_clock_drift`)
    /// compared to [`SystemTime::now()`]
    pub fn update(
        &mut self,
        other: &HybridLogicalClock,
        max_clock_drift: Duration,
    ) -> Result<(), HLCError> {
        let now = now_ms_precision();
        // Don't update from the same node.
        if self.node_id == other.node_id {
            return Ok(());
        }

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
        // all cases are covered at this point, so there is no need for a final else
        Ok(())
    }

    /// Updates the [`HybridLogicalClock`] based on the current time
    ///
    /// # Errors
    /// [`HLCError`] of kind [`OverflowWarning`](HLCErrorKind::OverflowWarning) if
    /// the [`HybridLogicalClock`]'s counter would be set to a value that would overflow beyond [`u64::MAX`]
    ///
    /// [`HLCError`] of kind [`ClockDrift`](HLCErrorKind::ClockDrift) if the [`HybridLogicalClock`]
    /// timestamp is too far in the future (determined by `max_clock_drift`) compared to [`SystemTime::now()`]
    /// compared to [`SystemTime::now()`]
    pub fn update_now(&mut self, max_clock_drift: Duration) -> Result<(), HLCError> {
        let now = now_ms_precision();

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
    /// and that the counter will not overflow if it is increased.
    ///
    /// # Errors
    /// [`HLCError`] of kind [`OverflowWarning`](HLCErrorKind::OverflowWarning) if
    /// the [`HybridLogicalClock`]'s counter would be set to a value that would overflow beyond [`u64::MAX`]
    ///
    /// [`HLCError`] of kind [`ClockDrift`](HLCErrorKind::ClockDrift) if the [`HybridLogicalClock`]
    /// timestamp is too far in the future (determined by `max_clock_drift`) compared to [`SystemTime::now()`]
    fn validate(&self, now: SystemTime, max_clock_drift: Duration) -> Result<(), HLCError> {
        if self.counter == u64::MAX {
            return Err(HLCErrorKind::OverflowWarning)?;
        }
        if let Ok(diff) = self.timestamp.duration_since(now) {
            if diff > max_clock_drift {
                return Err(HLCErrorKind::ClockDrift)?;
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
    type Err = ParseHLCError;

    fn from_str(s: &str) -> Result<Self, ParseHLCError> {
        let parts: Vec<&str> = s.split(':').collect();
        if parts.len() != 3 {
            return Err(ParseHLCError {
                message: "Incorrect format".to_string(),
                input: s.to_string(),
            });
        }

        // Validate first part (timestamp)
        let ms_since_epoch = match parts[0].parse::<u64>() {
            Ok(ms) => ms,
            Err(e) => {
                return Err(ParseHLCError {
                    message: format!(
                        "Malformed HLC. Could not parse first segment as an integer: {e}"
                    ),
                    input: s.to_string(),
                })
            }
        };
        let Some(timestamp) = UNIX_EPOCH.checked_add(Duration::from_millis(ms_since_epoch)) else {
            return Err(ParseHLCError {
                message: "Malformed HLC. Timestamp is out of range.".to_string(),
                input: s.to_string(),
            });
        };

        // Validate second part (counter)
        let counter = match parts[1].parse::<u64>() {
            Ok(val) => val,
            Err(e) => {
                return Err(ParseHLCError {
                    message: format!(
                        "Malformed HLC. Could not parse second segment as an integer: {e}"
                    ),
                    input: s.to_string(),
                });
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

/// All HLCs are rounded to the nearest millisecond to avoid issues with
/// string comparison, so now should also be rounded to the nearest millisecond.
fn now_ms_precision() -> SystemTime {
    #[cfg(not(test))]
    let now = SystemTime::now();

    // allow setting an offset for testing
    #[cfg(test)]
    let now = {
        let mut offset_now = SystemTime::now();
        let offset = TIME_OFFSET.with(std::cell::Cell::get);
        let positive = TIME_OFFSET_POS.with(std::cell::Cell::get);
        if positive {
            offset_now = offset_now.checked_add(offset).unwrap();
        } else {
            offset_now = offset_now.checked_sub(offset).unwrap();
        }
        offset_now
    };

    if let Ok(dur_since_epoch) = now.duration_since(UNIX_EPOCH) {
        let sec_since_epoch = dur_since_epoch.as_secs();
        let ms_since_epoch = dur_since_epoch.subsec_millis();
        if let Some(now) =
            UNIX_EPOCH.checked_add(Duration::new(sec_since_epoch, ms_since_epoch * 1_000_000))
        {
            return now;
        }
    }
    // If any errors occur while rounding to the nearest millisecond, just return the current time
    log::warn!(
        "Error rounding SystemTime::now() to the nearest millisecond. Returning unrounded time."
    );
    now
}

/// Represents errors that occur in the use of an HLC
#[derive(Debug, Error)]
#[error("{0}")]
pub struct HLCError(#[from] HLCErrorKind);

impl HLCError {
    /// Returns the corresponding [`HLCErrorKind`] for this error
    #[must_use]
    pub fn kind(&self) -> &HLCErrorKind {
        &self.0
    }
}

/// A list specifying categories of HLC error
#[derive(Debug, Error)]
pub enum HLCErrorKind {
    /// The counter would be incremented to a value that would overflow beyond [`u64::MAX`]
    #[error("counter cannot be incremented")]
    OverflowWarning,
    /// The HLC's timestamp is too far in the future compared to the current time
    #[error("exceeds max clock drift")]
    ClockDrift,
}

/// Represents errors that occur when parsing an HLC from a string
#[derive(Debug, Error)]
#[error("{message}")]
pub struct ParseHLCError {
    /// The error message
    message: String,
    /// The input string that failed to parse
    // NOTE: This is only needed for AIOProtocolError compatibility
    pub(crate) input: String,
}

// Functions to allow manipulation of the system time for testing purposes
#[cfg(test)]
use std::cell::Cell;

#[cfg(test)]
thread_local! {
    static TIME_OFFSET: Cell<Duration> = const { Cell::new(Duration::from_secs(0)) };
    static TIME_OFFSET_POS: Cell<bool> = const { Cell::new(false) };
}

#[cfg(test)]
fn set_time_offset(offset: Duration, positive: bool) {
    TIME_OFFSET.with(|time_offset| time_offset.set(offset));
    TIME_OFFSET_POS.with(|time_offset_pos| time_offset_pos.set(positive));
}

#[cfg(test)]
mod tests {
    use super::*;
    use std::time::{Duration, UNIX_EPOCH};
    use test_case::test_case;
    use uuid::Uuid;

    // a new HLC should pass validation
    #[test]
    fn test_validate_default() {
        let hlc = HybridLogicalClock::new();
        assert!(hlc
            .validate(now_ms_precision(), DEFAULT_MAX_CLOCK_DRIFT)
            .is_ok());
    }

    // Test validate when the HLC is in the future or the past
    #[test_case(120, true, true; "hlc in past more than max drift - success")]
    #[test_case(30, true, true; "hlc in past less than max drift - success")]
    #[test_case(120, false, false; "hlc in future more than max drift - failure")]
    #[test_case(30, false, true; "hlc in future less than max drift - success")]
    fn test_validate_drift(offset_sec: u64, positive: bool, should_succeed: bool) {
        // create HLC at true current time
        let hlc = HybridLogicalClock::new();
        // set System clock forward or backward by offset_sec
        set_time_offset(Duration::from_secs(offset_sec), positive);

        match hlc.validate(now_ms_precision(), DEFAULT_MAX_CLOCK_DRIFT) {
            Ok(()) => assert!(should_succeed),
            Err(e) => {
                assert!(!should_succeed);
                matches!(e.kind(), HLCErrorKind::ClockDrift);
            }
        }
    }

    // Test validate with different max clock drift values to make sure they don't cause unexpected behavior
    #[test_case(Duration::from_secs(31_536_000), Duration::from_secs(5); "large drift 1 year")]
    #[test_case(Duration::from_millis(5), Duration::from_millis(2); "tiny drift 5 ms")]
    #[test_case(Duration::from_secs(30), Duration::from_secs(5); "normal sized drift less than default 30 sec")]
    #[test_case(Duration::from_secs(300), Duration::from_secs(5); "normal sized drift more than default 5 min")]
    fn test_max_drift(max_drift: Duration, test_offset: Duration) {
        // create HLC at true current time
        let mut hlc = HybridLogicalClock::new();

        assert!(hlc.validate(now_ms_precision(), max_drift).is_ok());

        // validate with offset in the future, but before max drift
        // offset should be max_drift - test_offset
        let offset_before = max_drift.checked_sub(test_offset).unwrap();
        hlc.timestamp = hlc.timestamp.checked_add(offset_before).unwrap();
        assert!(hlc.validate(now_ms_precision(), max_drift).is_ok());

        // validate with offset after max drift
        // offset should be max_drift + test_offset, so we can approximate this
        // by adding the offset twice to the timestamp which is currently max_drift - test_offset
        let offset_after = test_offset.checked_add(test_offset).unwrap();
        hlc.timestamp = hlc.timestamp.checked_add(offset_after).unwrap();
        match hlc.validate(now_ms_precision(), max_drift) {
            Ok(()) => panic!("Expected error"),
            Err(e) => {
                matches!(e.kind(), HLCErrorKind::ClockDrift);
            }
        }
    }

    // Test validate for large counter values
    #[test]
    fn test_validate_counter() {
        let mut hlc = HybridLogicalClock::new();
        // a counter value of u64::MAX should fail validation since it's in danger of rolling over
        hlc.counter = u64::MAX;
        match hlc.validate(now_ms_precision(), DEFAULT_MAX_CLOCK_DRIFT) {
            Ok(()) => panic!("Expected error"),
            Err(e) => {
                matches!(e.kind(), HLCErrorKind::OverflowWarning);
            }
        }

        // a sufficiently large counter value that isn't u64::MAX should pass validation
        hlc.counter = u64::MAX - 1;
        assert!(hlc
            .validate(now_ms_precision(), DEFAULT_MAX_CLOCK_DRIFT)
            .is_ok());
    }

    // Test update_now with default HLC
    #[test]
    fn test_update_now_default() {
        // create HLC at true current time
        let mut hlc = HybridLogicalClock::new();
        assert!(hlc.update_now(DEFAULT_MAX_CLOCK_DRIFT).is_ok());
        // it's possible and valid for the HLC or the current time to be the
        // latest, especially since the HLC will win in a tie, but either
        // scenario will reset the counter or increment it only by 1
        assert!(hlc.counter == 0 || hlc.counter == 1);
    }

    // Test update_now when the HLC is in the future or the past
    #[test_case(120, true, true, 0; "hlc in past more than max drift - success")]
    #[test_case(30, true, true, 0; "hlc in past less than max drift - success")]
    #[test_case(120, false, false, 1000; "hlc in future more than max drift - failure")] // counter value shouldn't be used in this test
    #[test_case(30, false, true, 1; "hlc in future less than max drift - success")]
    fn test_update_now_drift(
        offset_sec: u64,
        positive: bool,
        should_succeed: bool,
        expected_counter: u64,
    ) {
        // create HLC at true current time
        let mut hlc = HybridLogicalClock::new();
        // set System clock forward or backward by offset_sec
        set_time_offset(Duration::from_secs(offset_sec), positive);

        match hlc.update_now(DEFAULT_MAX_CLOCK_DRIFT) {
            Ok(()) => {
                assert!(should_succeed);
                assert_eq!(hlc.counter, expected_counter);
            }
            Err(e) => {
                assert!(!should_succeed);
                matches!(e.kind(), HLCErrorKind::ClockDrift);
            }
        }
    }

    // Test update against self
    // update shouldn't be performed against self, but there shouldn't be an error
    #[test_case(true; "update against self no time manipulation")]
    #[test_case(false; "self in past to verify time isn't updated")]
    fn test_update_against_self(offset: bool) {
        // Create Self
        if offset {
            // have HLC be created 30 seconds in the past
            set_time_offset(Duration::from_secs(30), false);
        }
        let mut self_hlc = HybridLogicalClock::new();
        let self_ts_copy = self_hlc.timestamp;
        let self_clone = self_hlc.clone();

        if offset {
            // reset system time to now
            set_time_offset(Duration::from_secs(0), true);
        }

        assert!(self_hlc
            .update(&self_clone, DEFAULT_MAX_CLOCK_DRIFT)
            .is_ok());
        // assert that no update occurred
        assert_eq!(self_hlc.timestamp, self_ts_copy);
        assert_eq!(self_hlc.counter, 0);
    }

    // Test update against another HLC with different combos of both being in the future, the past, or now
    // NOTE: Other counter is set to 3 to aid in determining what counter is used
    #[test_case(-120, -120, true, &[0]; "self_hlc and other_hlc equal and in past more than max drift - success")]
    #[test_case(-105, -120, true, &[0]; "self_hlc and other_hlc in past more than max drift, but self_hlc in future of other_hlc - success")]
    #[test_case(-120, -105, true, &[0]; "self_hlc and other_hlc in past more than max drift, but self_hlc in past of other_hlc - success")]
    #[test_case(-120, -30, true, &[0]; "self_hlc in past more than max drift, other_hlc in past less than max drift - success")]
    #[test_case(-120, 0, true, &[0,4]; "self_hlc in past more than max drift, other_hlc now - success")]
    #[test_case(-120, 30, true, &[4]; "self_hlc in past more than max drift, other_hlc in future less than max drift - success")]
    #[test_case(-120, 120, false, &[]; "self_hlc in past more than max drift, other_hlc in future more than max drift - failure")]
    #[test_case(-30, -120, true, &[0]; "self_hlc in past less than max drift, other_hlc in past more than max drift - success")]
    #[test_case(-30, -30, true, &[0]; "self_hlc and other_hlc equal and in past less than max drift - success")]
    #[test_case(-15, -30, true, &[0]; "self_hlc and other_hlc in past less than max drift, but self_hlc in future of other_hlc - success")]
    #[test_case(-30, -15, true, &[0]; "self_hlc and other_hlc in past less than max drift, but self_hlc in past of other_hlc - success")]
    #[test_case(-30, 0, true, &[0,4]; "self_hlc in past less than max drift, other_hlc now - success")]
    #[test_case(-30, 30, true, &[4]; "self_hlc in past less than max drift, other_hlc in future less than max drift - success")]
    #[test_case(-30, 120, false, &[]; "self_hlc in past less than max drift, other_hlc in future more than max drift - failure")]
    #[test_case(0, -120, true, &[0,1]; "self_hlc now, other_hlc in past more than max drift - success")]
    #[test_case(0, -30, true, &[0,1]; "self_hlc now, other_hlc in past less than max drift - success")]
    #[test_case(0, 0, true, &[0,4]; "self_hlc now, other_hlc now - success")]
    #[test_case(0, 30, true, &[4]; "self_hlc now, other_hlc in future less than max drift - success")]
    #[test_case(0, 120, false, &[]; "self_hlc now, other_hlc in future more than max drift - failure")]
    #[test_case(30, -120, true, &[1]; "self_hlc in future less than max drift, other_hlc in past more than max drift - success")]
    #[test_case(30, -30, true, &[1]; "self_hlc in future less than max drift, other_hlc in past less than max drift - success")]
    #[test_case(30, 0, true, &[1]; "self_hlc in future less than max drift, other_hlc now - success")]
    #[test_case(30, 30, true, &[4]; "self_hlc and other_hlc equal and in future less than max drift - success")]
    #[test_case(45, 30, true, &[1]; "self_hlc and other_hlc in future less than max drift, but self_hlc in future of other_hlc - success")]
    #[test_case(30, 45, true, &[4]; "self_hlc and other_hlc in future less than max drift, but self_hlc in past of other_hlc - success")]
    #[test_case(30, 120, false, &[]; "self_hlc in future less than max drift, other_hlc in future more than max drift - failure")]
    #[test_case(120, -120, false, &[]; "self_hlc in future more than max drift, other_hlc in past more than max drift - failure")]
    #[test_case(120, -30, false, &[]; "self_hlc in future more than max drift, other_hlc in past less than max drift - failure")]
    #[test_case(120, 0, false, &[]; "self_hlc in future more than max drift, other_hlc now - failure")]
    #[test_case(120, 30, false, &[]; "self_hlc in future more than max drift, other_hlc in future less than max drift - failure")]
    #[test_case(120, 120, false, &[]; "self_hlc and other_hlc equal and in future more than max drift - failure")]
    #[test_case(135, 120, false, &[]; "self_hlc and other_hlc in future more than max drift, but self_hlc in future of other_hlc - failure")]
    #[test_case(120, 135, false, &[]; "self_hlc and other_hlc in future more than max drift, but self_hlc in past of other_hlc - failure")]
    fn test_update_other_drift(
        self_offset_sec: i64,
        other_offset_sec: i64,
        should_succeed: bool,
        valid_counters: &[u64],
    ) {
        // ~~Create Self~~
        // set System clock for HLC creation forward or backward by offset_sec
        set_time_offset(
            Duration::from_secs(self_offset_sec.unsigned_abs()),
            self_offset_sec > 0,
        );
        // create self HLC at offset time
        let mut self_hlc = HybridLogicalClock::new();
        let self_ts_copy = self_hlc.timestamp;

        // ~~Create Other~~
        let mut other_hlc = if self_offset_sec == other_offset_sec {
            // if self and other should have the same offset, ensure it's the same time
            let mut other = HybridLogicalClock::new();
            other.timestamp = self_hlc.timestamp;
            other
        } else {
            // set System clock for HLC creation forward or backward by offset_sec
            set_time_offset(
                Duration::from_secs(other_offset_sec.unsigned_abs()),
                other_offset_sec > 0,
            );
            // create other HLC at offset  time
            HybridLogicalClock::new()
        };
        let other_ts_copy = other_hlc.timestamp;
        // Set other counter to 3 so it's clear which counter is used
        other_hlc.counter = 3;

        // ~~reset System Time to now~~
        set_time_offset(Duration::from_secs(0), true);

        match self_hlc.update(&other_hlc, DEFAULT_MAX_CLOCK_DRIFT) {
            Ok(()) => {
                assert!(should_succeed);
                // if either HLC is set to now, multiple counter values could be valid depending on race conditions when timestamps are grabbed
                assert!(valid_counters.contains(&self_hlc.counter));
                match self_hlc.counter {
                    0 => {
                        // Can't validate that the new timestamp value is equal to
                        // SystemTime::now(), but we can validate that the value
                        // isn't the same as either of the provided HLCs' timestamps
                        assert_ne!(self_hlc.timestamp, self_ts_copy);
                        assert_ne!(self_hlc.timestamp, other_ts_copy);
                    }
                    1 => assert_eq!(self_hlc.timestamp, self_ts_copy),
                    4 => assert_eq!(self_hlc.timestamp, other_ts_copy),
                    _ => panic!("Unexpected counter value"),
                }
            }
            Err(e) => {
                assert!(!should_succeed);
                matches!(e.kind(), HLCErrorKind::ClockDrift);
                // self hlc should not have been updated
                assert_eq!(self_hlc.counter, 0);
                assert_eq!(self_hlc.timestamp, self_ts_copy);
            }
        }
    }

    // Test update against another HLC where one or both have counters that could roll over
    // Test validates that update() only fails if the counter would actually roll over
    #[test_case(30, 15, false, true, true; "other max, but past of self - success")]
    #[test_case(15, 30, false, true, false; "other max, future of self - failure")]
    #[test_case(15, 30, true, false, true; "self max, but past of other - success")]
    #[test_case(30, 15, true, false, false; "self max, future of other - failure")]
    #[test_case(-30, -30, true, true, true; "both max, but in past - success")]
    #[test_case(30, 30, false, true, false; "other max, same time as self - failure")]
    #[test_case(30, 30, true, false, false; "self max, same time as other - failure")]
    fn test_update_other_counter(
        self_offset_sec: i64,
        other_offset_sec: i64,
        max_self: bool,
        max_other: bool,
        should_succeed: bool,
    ) {
        // ~~Create Self~~
        // set System clock for HLC creation forward or backward by offset_sec
        set_time_offset(
            Duration::from_secs(self_offset_sec.unsigned_abs()),
            self_offset_sec > 0,
        );
        // create self HLC at offset time
        let mut self_hlc = HybridLogicalClock::new();
        if max_self {
            self_hlc.counter = u64::MAX;
        }
        let self_ts_copy = self_hlc.timestamp;
        let self_counter_copy = self_hlc.counter;

        // ~~Create Other~~
        let mut other_hlc = if self_offset_sec == other_offset_sec {
            // if self and other should have the same offset, ensure it's the same time
            let mut other = HybridLogicalClock::new();
            other.timestamp = self_hlc.timestamp;
            other
        } else {
            // set System clock for HLC creation forward or backward by offset_sec
            set_time_offset(
                Duration::from_secs(other_offset_sec.unsigned_abs()),
                other_offset_sec > 0,
            );
            // create other HLC at offset  time
            HybridLogicalClock::new()
        };
        if max_other {
            other_hlc.counter = u64::MAX;
        }

        // ~~reset System Time to now~~
        set_time_offset(Duration::from_secs(0), true);

        match self_hlc.update(&other_hlc, DEFAULT_MAX_CLOCK_DRIFT) {
            Ok(()) => {
                assert!(should_succeed);
            }
            Err(e) => {
                assert!(!should_succeed);
                matches!(e.kind(), HLCErrorKind::OverflowWarning);
                // self hlc should not have been updated
                assert_eq!(self_hlc.counter, self_counter_copy);
                assert_eq!(self_hlc.timestamp, self_ts_copy);
            }
        }
    }

    #[test]
    fn test_new_defaults() {
        let hlc = HybridLogicalClock::new();
        assert_eq!(hlc.counter, 0);
        // verify that the timestamp is rounded to the nearest millisecond
        assert_eq!(
            hlc.timestamp.duration_since(UNIX_EPOCH).unwrap().as_nanos() % 1_000_000,
            0
        );
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
    fn test_to_from_str() {
        let hlc = HybridLogicalClock::new();
        let hlc_str = hlc.to_string();
        let parsed_hlc = hlc_str.parse::<HybridLogicalClock>().unwrap();
        assert_eq!(parsed_hlc, hlc);
    }
}
