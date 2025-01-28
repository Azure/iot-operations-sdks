// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

//! MQTT client wrapper that provides ordered acking functionality.

use std::collections::{HashSet, VecDeque};
use std::sync::{Arc, Mutex};

use thiserror::Error;
use tokio::sync::Notify;

use crate::control_packet::Publish;
use crate::error::AckError;
use crate::interface::{CompletionToken, MqttAck};

/// Error related to PKID
#[derive(Error, Debug, PartialEq)]
pub enum PkidError {
    #[error("Pkid already in queue")]
    PkidDuplicate,
}

// NOTE: There is probably a more efficient implementation of the OrderedAcker that uses many
// Arc<Notify> instances to notify only the invocation for next pending PKID in the queue. Such
// implementation would be significantly more complex, and carries with it many additional edge
// cases that need to be considered. In the interest of time, I have gone with the simpler
// implementation here where all waiters check for their turn every time an ack occurs.
// However, if performance becomes a concern, this module is a prime candidate for runtime
// optimization.

/// Wrapper for an MQTT acker that ensures publishes are acked in the order that they were received.
#[derive(Clone)]
pub struct OrderedAcker<A>
where
    A: MqttAck,
{
    acker: A,
    // The queue of PKIDs representing the order in which they should be acked
    pkid_ack_queue: Arc<Mutex<PkidAckQueue>>,
    // PKIDs that are currently awaiting their turn for acking
    pending_acks: Arc<Mutex<HashSet<u16>>>,
    // Notifies every time an ack occurs, so that pending PKIDs can be checked for their turn
    notify: Arc<Notify>,
}

impl<A> OrderedAcker<A>
where
    A: MqttAck,
{
    /// Create and return a new [`OrderedAcker`] instance, that will use the provided acker to ack
    /// according to the order of PKIDs in the provided [`PkidAckQueue`].
    pub fn new(acker: A, pkid_ack_queue: Arc<Mutex<PkidAckQueue>>) -> Self {
        Self {
            acker,
            pkid_ack_queue,
            pending_acks: Arc::new(Mutex::new(HashSet::new())),
            notify: Arc::new(Notify::new()),
        }
    }

    /// Acknowledge a received publish, when it is this publish's turn to be acked.
    ///
    /// # Errors
    /// Returns an [`AckError`] if the publish cannot be acknowledged. Note that if ack fails,
    /// its position the queue will be relinquished.
    pub async fn ordered_ack(&self, publish: &Publish) -> Result<CompletionToken, AckError> {
        // No need to ack QoS0 publishes. Skip.
        if publish.pkid == 0 {
            return Ok(CompletionToken(Box::new(async { Ok(()) })));
        }

        // Add this publishes PKID as a "pending ack", as it may need to wait here for some amount
        // of time until it's turn to actually do the MQTT ack. Given that this OrderedAcker will be
        // cloned, we don't want it to be possible for multiple OrderedAckers to ack the same PKID.
        {
            let mut pending_acks = self.pending_acks.lock().unwrap();
            if pending_acks.contains(&publish.pkid) {
                // There is already a ordered ack invocation for this pkid that is pending
                // NOTE: This is an AckError since eventually we would want this error to come
                // directly from the underlying client via AckError, and this entire OrderedAcker
                // would be irrelevant.
                return Err(AckError::AlreadyAcked);
            }
            pending_acks.insert(publish.pkid);
        }

        loop {
            // Determine if this publish is the correct next ack. If so, pop the data so that
            // the PKID can be re-used.
            // NOTE: This is done before the ack itself so that the lock does not need to be held
            // through an await operation. Additionally, as soon as the ack
            let should_ack = {
                let mut pkid_ack_queue = self.pkid_ack_queue.lock().unwrap();
                let mut pending_acks = self.pending_acks.lock().unwrap();
                if let Some(next_ack_pkid) = pkid_ack_queue.check_next_ack_pkid() {
                    if next_ack_pkid == &publish.pkid {
                        // Publish PKID is the next ack, so pop data
                        pkid_ack_queue.pop_next_ack_pkid();
                        pending_acks.remove(&publish.pkid);
                        true
                    } else {
                        false
                    }
                } else {
                    false
                }
            };

            // Ack the publish if is is this publishes turn to be acked
            if should_ack {
                let ct = self.acker.ack(publish).await?;
                // NOTE: Only notify the waiters AFTER the ack is completed to ensure that no scheduling
                // shenanigans allow ack order to be altered.
                self.notify.notify_waiters();
                return Ok(ct);
            }
            // Otherwise, wait for the next ack if not yet this Publish's turn
            self.notify.notified().await;
        }
    }
}

/// Queue of PKIDs in the order they should be acked.
#[derive(Default)]
pub struct PkidAckQueue {
    /// The queue of PKIDs in ack order
    queue: VecDeque<u16>,
    /// The set of PKIDs that are currently in the queue
    tracked_pkids: HashSet<u16>,
}

impl PkidAckQueue {
    /// Insert a PKID into the back of the queue.
    ///
    /// Returns [`PkidError`] if the PKID is already in the queue
    pub fn insert(&mut self, pkid: u16) -> Result<(), PkidError> {
        if self.tracked_pkids.contains(&pkid) {
            return Err(PkidError::PkidDuplicate);
        }
        self.tracked_pkids.insert(pkid);
        self.queue.push_back(pkid);
        Ok(())
    }

    /// Return the next PKID in the queue, if there is one
    pub fn check_next_ack_pkid(&self) -> Option<&u16> {
        return self.queue.front();
    }

    /// Return the next PKID in the queue, if there is one, removing it from the queue
    pub fn pop_next_ack_pkid(&mut self) -> Option<u16> {
        match self.queue.pop_front() {
            Some(pkid) => {
                self.tracked_pkids.remove(&pkid);
                Some(pkid)
            }
            None => None,
        }
    }

    pub fn contains(&mut self, pkid: u16) -> bool {
        self.tracked_pkids.contains(&pkid)
    }

    // TODO: some kind of pop_if would be useful for the OrderedAcker
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn pkid_queue() {
        let mut pkid_queue = PkidAckQueue::default();
        pkid_queue.insert(1).unwrap();
        pkid_queue.insert(2).unwrap();
        pkid_queue.insert(3).unwrap();
        assert_eq!(pkid_queue.check_next_ack_pkid(), Some(&1));
        assert_eq!(pkid_queue.pop_next_ack_pkid(), Some(1));
        assert_eq!(pkid_queue.check_next_ack_pkid(), Some(&2));
        assert_eq!(pkid_queue.pop_next_ack_pkid(), Some(2));
        assert_eq!(pkid_queue.check_next_ack_pkid(), Some(&3));
        assert_eq!(pkid_queue.pop_next_ack_pkid(), Some(3));
        assert_eq!(pkid_queue.check_next_ack_pkid(), None);
        assert_eq!(pkid_queue.pop_next_ack_pkid(), None);
    }

    #[test]
    fn pkid_queue_duplicate() {
        let mut pkid_queue = PkidAckQueue::default();
        pkid_queue.insert(1).unwrap();
        assert_eq!(pkid_queue.insert(1).unwrap_err(), PkidError::PkidDuplicate);
    }

    // TODO: ordered acking tests. Needs enhanced mocking infrastructure.
}
