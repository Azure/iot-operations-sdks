// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License

//! Packet Identifier (PKID) management for MQTT session.

use std::{collections::HashSet, iter::Cycle};

use crate::azure_mqtt::mqtt_proto::PacketIdentifier;

// NOTE: `PkidRange` is required because `RangeInclusive` does not support `PacketIdentifier`
// and the required trait (`Step`) is unstable, so `PacketIdentifier` cannot implement it.
// So, by defining a bespoke range type, we can implement `Iterator` directly for it.

/// Iterator that yields `PacketIdentifier`s in a specified range, inclusive.
#[derive(Clone)]
struct PkidRange {
    current: PacketIdentifier,
    end: PacketIdentifier,
    finished: bool,
}

impl PkidRange {
    fn new(start: PacketIdentifier, end: PacketIdentifier) -> Self {
        Self {
            current: start,
            end,
            finished: false,
        }
    }
}

impl Iterator for PkidRange {
    type Item = PacketIdentifier;

    fn next(&mut self) -> Option<Self::Item> {
        if self.finished {
            return None;
        }
        let result = self.current;
        if self.current == self.end {
            self.finished = true;
        } else {
            self.current += 1;
        }
        Some(result)
    }
}

/// Manages leasing and releasing Packet Identifiers for MQTT operations.
pub struct PkidPool {
    leased: HashSet<PacketIdentifier>,
    cycle: Cycle<PkidRange>,
    max_pkid: PacketIdentifier,
}

impl PkidPool {
    /// Creates a new `PkidPool` with Packet Identifiers ranging from 1 to `max_pkid`.
    pub fn new(max_pkid: PacketIdentifier) -> Self {
        Self {
            leased: HashSet::new(),
            cycle: PkidRange::new(
                PacketIdentifier::new(1).expect("PacketIdentifier::new(1) is always valid"),
                max_pkid,
            )
            .cycle(),
            max_pkid,
        }
    }

    /// Attempts to lease the next available Packet Identifier.
    /// Returns `Some(PacketIdentifier)` if successful, or `None` if all identifiers are in use.
    pub fn lease_next_pkid(&mut self) -> Option<PacketIdentifier> {
        if self.leased.len() == self.max_pkid.get().into() {
            return None; // All leased
        }
        // NOTE: Infinite loop is safe here as we are guaranteed to find a free pkid because of
        // the lease check above.
        loop {
            let pkid = self.cycle.next().expect("Cycle is infinite and non-empty");
            if !self.leased.contains(&pkid) {
                self.leased.insert(pkid);
                return Some(pkid);
            }
        }
    }

    /// Releases a previously leased Packet Identifier, making it available for future leasing.
    /// Returns `true` if the identifier was successfully released, or `false` if it was not
    /// previously leased.
    pub fn release_pkid(&mut self, pkid: PacketIdentifier) -> bool {
        self.leased.remove(&pkid)
    }
}

#[cfg(test)]
mod test {
    use super::*;
    use test_case::test_case;

    #[test]
    fn pkid_lease_order() {
        let mut pool = PkidPool::new(super::PacketIdentifier::new(5).unwrap());
        // Pkids are leased in sequential order
        assert_eq!(
            pool.lease_next_pkid(),
            Some(PacketIdentifier::new(1).unwrap())
        );
        assert_eq!(
            pool.lease_next_pkid(),
            Some(PacketIdentifier::new(2).unwrap())
        );
        assert_eq!(
            pool.lease_next_pkid(),
            Some(PacketIdentifier::new(3).unwrap())
        );
        assert_eq!(
            pool.lease_next_pkid(),
            Some(PacketIdentifier::new(4).unwrap())
        );
        assert_eq!(
            pool.lease_next_pkid(),
            Some(PacketIdentifier::new(5).unwrap())
        );
        assert_eq!(pool.lease_next_pkid(), None); // All leased

        // Release the first pkid, and it will be leasable again
        assert!(pool.release_pkid(PacketIdentifier::new(1).unwrap()));
        assert_eq!(
            pool.lease_next_pkid(),
            Some(PacketIdentifier::new(1).unwrap())
        );

        // Still, no other pkids are available for lease
        assert_eq!(pool.lease_next_pkid(), None);

        // Release a non-sequential pkid, and it will be leasable again
        assert!(pool.release_pkid(PacketIdentifier::new(3).unwrap()));
        assert_eq!(
            pool.lease_next_pkid(),
            Some(PacketIdentifier::new(3).unwrap())
        );

        // Release a pkid both higher and lower than the previous lease.
        // The higher one will be leased first due to the circular nature of the pool.
        assert!(pool.release_pkid(PacketIdentifier::new(2).unwrap()));
        assert!(pool.release_pkid(PacketIdentifier::new(5).unwrap()));
        assert_eq!(
            pool.lease_next_pkid(),
            Some(PacketIdentifier::new(5).unwrap())
        );
        assert_eq!(
            pool.lease_next_pkid(),
            Some(PacketIdentifier::new(2).unwrap())
        );
        assert_eq!(pool.lease_next_pkid(), None); // All leased
    }

    #[test]
    fn pkid_release_nonexistent() {
        let mut pool = PkidPool::new(PacketIdentifier::new(5).unwrap());
        // Releasing a pkid that was never leased returns false
        assert!(!pool.release_pkid(PacketIdentifier::new(1).unwrap()));
        // Releasing a pkid that is out of bounds for the max PKID in pool returns false
        assert!(!pool.release_pkid(PacketIdentifier::new(6).unwrap()));
    }

    #[test_case(PacketIdentifier::new(1).unwrap(); "1 PKID")]
    #[test_case(PacketIdentifier::new(100).unwrap(); "100 PKIDs")]
    #[test_case(PacketIdentifier::MAX; "Max PKIDs")]
    fn pkid_pool_size(max_pkid: PacketIdentifier) {
        let mut pool = PkidPool::new(max_pkid);
        for i in 1..=max_pkid.get() {
            assert_eq!(
                pool.lease_next_pkid(),
                Some(PacketIdentifier::new(i).unwrap())
            );
        }
        assert_eq!(pool.lease_next_pkid(), None); // All leased
    }
}
