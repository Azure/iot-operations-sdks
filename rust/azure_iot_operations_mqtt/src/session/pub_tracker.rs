// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

//! Inverse demultiplexing of publishes previously distributed to multiple receivers, in original receiving order.

use std::collections::VecDeque;
use std::sync::Mutex;

use thiserror::Error;
use tokio::sync::Notify;

use crate::control_packet::Publish;

#[derive(Error, Debug)]
pub enum RegisterError {
    #[error("publish already registered for pkid {0}")]
    AlreadyRegistered(u16),
}

#[derive(Error, Debug)]
pub enum AckError {
    #[error("cannot ack a publish more times than required")]
    AckOverflow,
}

#[derive(Error, Debug)]
pub enum TryNextReadyError {
    #[error("no registered pending publishes are ready")]
    NotReady,
    #[error("no registered pending publishes")]
    Empty,
}

/// Represents tracking data for a pending publish.
struct PendingPub {
    /// Publish that is pending, waiting for acks
    pub publish: Publish,
    /// Number of acks remaining before the publish is ready
    pub remaining_acks: usize,
}

/// Tracking structure for determining when a [`Publish`] has been acknowledged
/// locally the required number of times and is ready to ack back to the server.
///
/// Note that this is only designed for Quality of Service 1 at present moment
pub struct PubTracker {
    pending: Mutex<VecDeque<PendingPub>>,
    registration_notify: Notify,
    ready_notify: Notify,
}

impl PubTracker {
    /// Register a [`Publish`] as pending.
    ///
    /// When it is acked the required number of times on this tracker, it will be considered ready
    /// to ack back to the server.
    ///
    /// The [`Publish`] will not be registered if it has a PKID of 0, as this is reserved for
    /// Quality of Service 0 messages, which do not require acknowledgement.
    /// This is not considered an error.
    ///
    /// # Arguments
    /// * `publish` - The [`Publish`] to register as pending
    /// * `acks_required` - The number of acks required before the [`Publish`] is considered ready
    ///
    /// # Errors
    /// * [`RegisterError::AlreadyRegistered`] if a [`Publish`] with the same pkid is already registered.
    ///   This indicates a duplicate [`Publish`], and can be ignored.
    pub fn register_pending(
        &self,
        publish: &Publish,
        acks_required: usize,
    ) -> Result<(), RegisterError> {
        // Ignore PKID 0, as it is reserved for QoS 0 messages
        if publish.pkid == 0 {
            return Ok(());
        }
        let mut pending = self.pending.lock().unwrap();
        // Check for existing registration (invalid)
        if pending
            .iter()
            .any(|pending| pending.publish.pkid == publish.pkid)
        {
            // NOTE: If a publish with the same PKID is currently tracked in the `PubTracker`, this
            // means that a duplicate has been received. While a client IS required to treat received
            // duplicates as a new application message in QoS 1 (MQTTv5 4.3.2), this is ONLY true if
            // the original message has been acknowledged and ownership transferred back to the server.
            // By definition of being in the `PubTracker`, the message has NOT been acknowledged, and thus,
            // can be discarded.
            return Err(RegisterError::AlreadyRegistered(publish.pkid));
        }

        let pending_pub = PendingPub {
            publish: publish.clone(),
            remaining_acks: acks_required,
        };
        pending.push_back(pending_pub);
        self.registration_notify.notify_waiters();
        Ok(())
    }

    /// Acknowledge a pending [`Publish`].
    ///
    /// Decrements the amount of remaining acks required for the [`Publish`] to be considered ready.
    ///
    /// Does nothing if the [`Publish`] has a PKID of 0, as this is reserved for
    /// Quality of Service 0 messages
    /// which do not require acknowledgement.
    ///
    /// # Arguments
    /// * `publish` - The [`Publish`] to acknowledge
    pub async fn ack(&self, publish: &Publish) -> Result<(), AckError> {
        // Ignore PKID 0, as it is reserved for QoS 0 messages
        if publish.pkid == 0 {
            return Ok(());
        }

        loop {
            // First, check if there is a matching PendingPub.
            // NOTE: Do this in a new scope so as not to hold the lock longer than necessary.
            {
                let mut pending = self.pending.lock().unwrap();

                // If PendingPub registered, decrement the remaining number of acks required.
                if let Some(pos) = pending
                    .iter()
                    .position(|pending| pending.publish.pkid == publish.pkid)
                {
                    let entry = &mut pending[pos];
                    if entry.remaining_acks == 0 {
                        return Err(AckError::AckOverflow);
                    }
                    entry.remaining_acks -= 1;

                    if entry.remaining_acks == 0 {
                        // Notify the waiter if:
                        // 1) The PendingPub has zero acks remaining
                        // 2) The PendingPub is at the front of the queue
                        if pos == 0 {
                            self.ready_notify.notify_one();
                        }
                    }
                    return Ok(());
                }
            }
            // Wait for a registration if the ack occurs before the corresponding PendingPub was
            // registered.
            // NOTE: This can happen because a PendingPub requires knowledge of how many acks to
            // wait for, but by the time the dispatcher reports this information, some of the
            // receivers dispatched to may have already acked.
            self.registration_notify.notified().await;
        }
    }

    /// Get the next [`Publish`] that is ready to ack back to the server.
    ///
    /// A [`Publish`] is considered ready to be acked to the server when:
    /// 1) It has been acked on this tracker the number of times specified when it was registered
    /// 2) It is the oldest registration in the tracker
    ///
    /// This method should not be called in parallel with itself.
    pub async fn next_ready(&self) -> Publish {
        loop {
            // Check if there is a PendingPub ready to ack to server, and return it.
            // If no PendingPub is ready, wait for one to be ready.
            match self.try_next_ready() {
                Ok(publish) => return publish,
                Err(_) => self.ready_notify.notified().await,
            }
        }
    }

    /// Get the next [`Publish`] that is ready to ack back to the server.
    ///
    /// If no [`Publish`] is ready returns a [`TryNextReadyError`].
    ///
    /// A [`Publish`] is considered ready to be acked to the server when:
    /// 1) It has been acked on this tracker the number of times specified when it was registered
    /// 2) It is the oldest registration in the tracker
    ///
    /// # Errors
    /// * [`TryNextReadyError::NotReady`] if no tracked publishes are ready
    /// * [`TryNextReadyError::Empty`] if there are no tracked publishes
    pub fn try_next_ready(&self) -> Result<Publish, TryNextReadyError> {
        let mut pending = self.pending.lock().unwrap();
        if let Some(next) = pending.front() {
            if next.remaining_acks == 0 {
                match pending.pop_front() {
                    Some(pending_ack) => return Ok(pending_ack.publish),
                    None => unreachable!("We already checked if there is a first element, and hold a lock on the VecDeque"),
                }
            }
            return Err(TryNextReadyError::NotReady);
        }
        Err(TryNextReadyError::Empty)
    }

    /// Check if a [`Publish`] is currently pending in the tracker.
    #[must_use]
    pub fn contains(&self, publish: &Publish) -> bool {
        self.pending
            .lock()
            .unwrap()
            .iter()
            .any(|pending| pending.publish.pkid == publish.pkid)
    }

    /// Clear all pending publishes from the tracker.
    ///
    /// Do not use this except for shutdown/cleanup
    pub fn reset(&self) {
        self.pending.lock().unwrap().clear();
        // Can this instead of clearing, update to indicate should ignore?

        // TODO: This is not a complete implementation.
        // For instance, the Notify structs survive.
        // Additionally, clearing may not even be the preferable behavior.
        // Out of scope for now.
    }
}

impl Default for PubTracker {
    fn default() -> Self {
        PubTracker {
            pending: Mutex::new(VecDeque::new()),
            registration_notify: Notify::new(),
            ready_notify: Notify::new(),
        }
    }
}

// TODO: deal with that Session re-use case
// TODO: send some kind of signal through `next_ready` to indicate drop/shutdown?

#[cfg(test)]
mod tests {
    use std::sync::Arc;

    use super::*;
    use crate::control_packet::QoS;

    use test_case::test_case;

    fn create_publish(topic_name: &str, payload: &str, pkid: u16) -> Publish {
        // NOTE: We use the TopicName here for convenience. No other reason.
        let mut publish = Publish::new(topic_name, QoS::AtLeastOnce, payload.to_string(), None);
        publish.pkid = pkid;
        publish
    }

    /// Test registering and acking publishes that only require a single ack to be ready,
    /// where the publishes are acked in the order they were registered.
    #[test_case(1, 2, 3; "Sequential PKIDs")]
    #[test_case(9, 10, 1; "Wrap-around PKIDs")]
    #[test_case(7, 3, 12; "Random PKIDs")]
    #[tokio::test]
    async fn register_and_single_ack_ordered(pub1_pkid: u16, pub2_pkid: u16, pub3_pkid: u16) {
        // Created empty
        let tracker = PubTracker::default();
        assert!(matches!(
            tracker.try_next_ready().err(),
            Some(TryNextReadyError::Empty)
        ));

        // Add a pending publish
        let publish1 = create_publish("test", "pub1", pub1_pkid);
        tracker.register_pending(&publish1, 1).unwrap();
        assert!(tracker.contains(&publish1));

        // Add a second pending publish
        let publish2 = create_publish("test", "pub2", pub2_pkid);
        tracker.register_pending(&publish2, 1).unwrap();
        assert!(tracker.contains(&publish2));

        // Add a third pending publish
        let publish3 = create_publish("test", "pub3", pub3_pkid);
        tracker.register_pending(&publish3, 1).unwrap();
        assert!(tracker.contains(&publish3));

        // No tracked publishes are ready yet
        assert!(matches!(
            tracker.try_next_ready().err(),
            Some(TryNextReadyError::NotReady)
        ));

        // Acknowledge the first publish
        tracker.ack(&publish1).await.unwrap();

        // The first publish is now ready, and is removed from the tracker,
        // but no further ones are.
        assert_eq!(tracker.try_next_ready().unwrap(), publish1);
        assert!(matches!(
            tracker.try_next_ready().err(),
            Some(TryNextReadyError::NotReady)
        ));
        assert!(!tracker.contains(&publish1));
        assert!(tracker.contains(&publish2));
        assert!(tracker.contains(&publish3));

        // Acknowledge the second and third publish
        tracker.ack(&publish2).await.unwrap();
        tracker.ack(&publish3).await.unwrap();

        // The second publish is now ready, and is removed from the tracker
        assert_eq!(tracker.try_next_ready().unwrap(), publish2);
        assert_eq!(tracker.pending.lock().unwrap().len(), 1);
        assert!(!tracker.contains(&publish2));
        assert!(tracker.contains(&publish3));

        // The third publish is also ready and can be removed
        assert_eq!(tracker.try_next_ready().unwrap(), publish3);
        assert!(!tracker.contains(&publish3));

        // No further publishes are pending
        assert!(matches!(
            tracker.try_next_ready().err(),
            Some(TryNextReadyError::Empty)
        ));
    }

    /// Test registering and acking publishes that only require a single ack to be ready,
    /// where the publishes are acked out of registration order.
    #[test_case(1, 2, 3; "Sequential PKIDs")]
    #[test_case(9, 10, 1; "Wrap-around PKIDs")]
    #[test_case(7, 3, 12; "Random PKIDs")]
    #[tokio::test]
    async fn register_and_single_ack_unordered(pub1_pkid: u16, pub2_pkid: u16, pub3_pkid: u16) {
        // Created empty
        let tracker = PubTracker::default();
        assert!(matches!(
            tracker.try_next_ready().err(),
            Some(TryNextReadyError::Empty)
        ));

        // Add a pending publish
        let publish1 = create_publish("test", "pub1", pub1_pkid);
        tracker.register_pending(&publish1, 1).unwrap();
        assert!(tracker.contains(&publish1));

        // Add a second pending publish
        let publish2 = create_publish("test", "pub2", pub2_pkid);
        tracker.register_pending(&publish2, 1).unwrap();
        assert!(tracker.contains(&publish2));

        // Add a third pending publish
        let publish3 = create_publish("test", "pub3", pub3_pkid);
        tracker.register_pending(&publish3, 1).unwrap();
        assert!(tracker.contains(&publish3));

        // No tracked publishes are ready yet
        assert!(matches!(
            tracker.try_next_ready().err(),
            Some(TryNextReadyError::NotReady)
        ));

        // Acknowledge the third publish
        tracker.ack(&publish3).await.unwrap();

        // No tracked publishes are ready yet. All publishes remain in the tracker.
        assert!(matches!(
            tracker.try_next_ready().err(),
            Some(TryNextReadyError::NotReady)
        ));
        assert!(tracker.contains(&publish1));
        assert!(tracker.contains(&publish2));
        assert!(tracker.contains(&publish3));

        // Acknowledge the second publish
        tracker.ack(&publish2).await.unwrap();

        // Still no untracked publishes are ready yet. All publishes remain in the tracker.
        assert!(matches!(
            tracker.try_next_ready().err(),
            Some(TryNextReadyError::NotReady)
        ));
        assert!(tracker.contains(&publish1));
        assert!(tracker.contains(&publish2));
        assert!(tracker.contains(&publish3));

        // Acknowledge the first publish
        tracker.ack(&publish1).await.unwrap();

        // Now all three publishes are ready and can be removed from the tracker
        assert_eq!(tracker.try_next_ready().unwrap(), publish1);
        assert_eq!(tracker.try_next_ready().unwrap(), publish2);
        assert_eq!(tracker.try_next_ready().unwrap(), publish3);

        // No further publishes remain
        assert!(matches!(
            tracker.try_next_ready().err(),
            Some(TryNextReadyError::Empty)
        ));
    }

    /// Test registering and acking publishes that require a variable number of acks to be ready,
    /// where the publishes are acked in the order they were registered.
    #[test_case(1, 2, 3; "Sequential PKIDs")]
    #[test_case(9, 10, 1; "Wrap-around PKIDs")]
    #[test_case(7, 3, 12; "Random PKIDs")]
    #[tokio::test]
    async fn register_and_multi_ack_ordered(pub1_pkid: u16, pub2_pkid: u16, pub3_pkid: u16) {
        // Created empty
        let tracker = PubTracker::default();
        assert!(matches!(
            tracker.try_next_ready().err(),
            Some(TryNextReadyError::Empty)
        ));

        // Add a pending publish that requires 2 acks
        let publish1 = create_publish("test", "pub1", pub1_pkid);
        tracker.register_pending(&publish1, 2).unwrap();
        assert!(tracker.contains(&publish1));

        // Add a second pending publish that requires 1 ack
        let publish2 = create_publish("test", "pub2", pub2_pkid);
        tracker.register_pending(&publish2, 1).unwrap();
        assert!(tracker.contains(&publish2));

        // Add a third pending publish that requires 5 acks
        let publish3 = create_publish("test", "pub3", pub3_pkid);
        tracker.register_pending(&publish3, 5).unwrap();
        assert!(tracker.contains(&publish3));

        // No tracked publishes are ready yet.
        assert!(matches!(
            tracker.try_next_ready().err(),
            Some(TryNextReadyError::NotReady)
        ));

        // Acknowledge the first publish once, and it is still not ready
        // (Required 2 acks)
        tracker.ack(&publish1).await.unwrap();
        assert!(matches!(
            tracker.try_next_ready().err(),
            Some(TryNextReadyError::NotReady)
        ));

        // Acknowledge a second time, and it will become ready
        tracker.ack(&publish1).await.unwrap();
        assert_eq!(tracker.try_next_ready().unwrap(), publish1);
        // No further publishes are ready
        assert!(matches!(
            tracker.try_next_ready().err(),
            Some(TryNextReadyError::NotReady)
        ));

        // Acknowledge the second publish once, and the third publish twice.
        // The second publish is ready (required 1 ack),
        // but the third is not (requires 5 acks)
        tracker.ack(&publish2).await.unwrap();
        tracker.ack(&publish3).await.unwrap();
        tracker.ack(&publish3).await.unwrap();
        assert_eq!(tracker.try_next_ready().unwrap(), publish2);
        assert!(matches!(
            tracker.try_next_ready().err(),
            Some(TryNextReadyError::NotReady)
        ));

        // Acknowledge the third publish three more times, and it will become ready
        tracker.ack(&publish3).await.unwrap();
        tracker.ack(&publish3).await.unwrap();
        tracker.ack(&publish3).await.unwrap();
        assert_eq!(tracker.try_next_ready().unwrap(), publish3);

        // No further publishes are pending
        assert!(matches!(
            tracker.try_next_ready().err(),
            Some(TryNextReadyError::Empty)
        ));
    }

    /// Test registering and acking publishes that require a variable number of acks to be ready,
    /// where the publishes are acked out of registration order.
    #[test_case(1, 2, 3; "Sequential PKIDs")]
    #[test_case(9, 10, 1; "Wrap-around PKIDs")]
    #[test_case(7, 3, 12; "Random PKIDs")]
    #[tokio::test]
    async fn register_and_multi_ack_unordered(pub1_pkid: u16, pub2_pkid: u16, pub3_pkid: u16) {
        // Created empty
        let tracker = PubTracker::default();
        assert!(matches!(
            tracker.try_next_ready().err(),
            Some(TryNextReadyError::Empty)
        ));

        // Add a pending publish
        let publish1 = create_publish("test", "pub1", pub1_pkid);
        tracker.register_pending(&publish1, 2).unwrap();
        assert!(tracker.contains(&publish1));

        // Add a second pending publish
        let publish2 = create_publish("test", "pub2", pub2_pkid);
        tracker.register_pending(&publish2, 1).unwrap();
        assert!(tracker.contains(&publish2));

        // Add a third pending publish
        let publish3 = create_publish("test", "pub3", pub3_pkid);
        tracker.register_pending(&publish3, 5).unwrap();
        assert!(tracker.contains(&publish3));

        // No tracked publishes are ready yet.
        assert!(matches!(
            tracker.try_next_ready().err(),
            Some(TryNextReadyError::NotReady)
        ));

        // Acknowledge the third publish thrice (required 5 acks)
        tracker.ack(&publish3).await.unwrap();
        tracker.ack(&publish3).await.unwrap();
        tracker.ack(&publish3).await.unwrap();

        // No tracked publishes are ready yet. All publishes remain in the tracker.
        assert!(matches!(
            tracker.try_next_ready().err(),
            Some(TryNextReadyError::NotReady)
        ));
        assert!(tracker.contains(&publish1));
        assert!(tracker.contains(&publish2));
        assert!(tracker.contains(&publish3));

        // Acknowledge the first publish once (required 2 acks)
        tracker.ack(&publish1).await.unwrap();

        // No tracked publishes are ready yet. All publishes remain in the tracker.
        assert!(matches!(
            tracker.try_next_ready().err(),
            Some(TryNextReadyError::NotReady)
        ));
        assert!(tracker.contains(&publish1));
        assert!(tracker.contains(&publish2));
        assert!(tracker.contains(&publish3));

        // Acknowledge the second publish once (required 1 ack)
        tracker.ack(&publish2).await.unwrap();

        // Still no untracked publishes are ready yet. All publishes remain in the tracker.
        assert!(matches!(
            tracker.try_next_ready().err(),
            Some(TryNextReadyError::NotReady)
        ));
        assert!(tracker.contains(&publish1));
        assert!(tracker.contains(&publish2));
        assert!(tracker.contains(&publish3));

        // Acknowledge the first publish a second time, and it is now ready
        tracker.ack(&publish1).await.unwrap();

        // Now the first two publishes are ready and can be removed from the tracker
        assert_eq!(tracker.try_next_ready().unwrap(), publish1);
        assert_eq!(tracker.try_next_ready().unwrap(), publish2);
        assert!(matches!(
            tracker.try_next_ready().err(),
            Some(TryNextReadyError::NotReady)
        ));
        assert!(tracker.contains(&publish3));

        // Acknowledge the third publish twice more, and it is now ready
        tracker.ack(&publish3).await.unwrap();
        tracker.ack(&publish3).await.unwrap();
        assert_eq!(tracker.try_next_ready().unwrap(), publish3);

        // No further publishes remain
        assert!(matches!(
            tracker.try_next_ready().err(),
            Some(TryNextReadyError::Empty)
        ));
    }

    /// Test waiting for the next ordered ready publishes
    #[test_case(1, 2, 3; "Sequential PKIDs")]
    #[test_case(9, 10, 1; "Wrap-around PKIDs")]
    #[test_case(7, 3, 12; "Random PKIDs")]
    #[tokio::test]
    async fn next_ready(pub1_pkid: u16, pub2_pkid: u16, pub3_pkid: u16) {
        // Created empty
        let tracker = PubTracker::default();
        assert!(matches!(
            tracker.try_next_ready().err(),
            Some(TryNextReadyError::Empty)
        ));

        // Add a pending publish
        let publish1 = create_publish("test", "pub1", pub1_pkid);
        tracker.register_pending(&publish1, 1).unwrap();
        assert!(tracker.contains(&publish1));

        // Add a second pending publish
        let publish2 = create_publish("test", "pub2", pub2_pkid);
        tracker.register_pending(&publish2, 1).unwrap();
        assert!(tracker.contains(&publish2));

        // Add a third pending publish
        let publish3 = create_publish("test", "pub3", pub3_pkid);
        tracker.register_pending(&publish3, 1).unwrap();
        assert!(tracker.contains(&publish3));

        // Create a task that waits for the next ready publishes
        let tracker = Arc::new(tracker);
        let tracker_clone = Arc::clone(&tracker);
        let jh = tokio::task::spawn(async move {
            let next_pub = tracker_clone.next_ready().await;
            assert_eq!(next_pub.pkid, publish1.pkid);
            let next_pub = tracker_clone.next_ready().await;
            assert_eq!(next_pub.pkid, publish2.pkid);
            let next_pub = tracker_clone.next_ready().await;
            assert_eq!(next_pub.pkid, publish3.pkid);
        });

        tokio::time::sleep(tokio::time::Duration::from_millis(100)).await;
        assert!(!jh.is_finished());

        // Acknowledge the publishes
        tracker.ack(&publish1).await.unwrap();
        tracker.ack(&publish2).await.unwrap();
        tracker.ack(&publish3).await.unwrap();

        // Waiting task completes
        jh.await.unwrap();
    }

    /// Test that acks made before the registration of a publish will still be counted
    /// when the publish is registered. This is important to prevent race conditions,
    /// as registrations can only be made after dispatches.
    #[test_case(1, 2, 3; "Sequential PKIDs")]
    #[test_case(9, 10, 1; "Wrap-around PKIDs")]
    #[test_case(7, 3, 12; "Random PKIDs")]
    #[tokio::test]
    async fn early_ack(pub1_pkid: u16, pub2_pkid: u16, pub3_pkid: u16) {
        // Created empty
        let tracker = Arc::new(PubTracker::default());
        assert!(matches!(
            tracker.try_next_ready().err(),
            Some(TryNextReadyError::Empty)
        ));

        // Create publishes
        let publish1 = create_publish("test", "pub1", pub1_pkid);
        let publish2 = create_publish("test", "pub2", pub2_pkid);
        let publish3 = create_publish("test", "pub3", pub3_pkid);

        // Acknowledge publish 2, then publish 1, then publish 3 before registering them

        // Acknowledge publish 2 twice consecutively in the same task
        let p2_jh = tokio::task::spawn({
            let tracker = tracker.clone();
            let publish2 = publish2.clone();
            async move {
                tracker.ack(&publish2).await.unwrap();
                tracker.ack(&publish2).await.unwrap();
            }
        });

        tokio::time::sleep(tokio::time::Duration::from_millis(100)).await;

        // Acknowledge publish 1 once
        let p1_jh = tokio::task::spawn({
            let tracker = tracker.clone();
            let publish1 = publish1.clone();
            async move {
                tracker.ack(&publish1).await.unwrap();
            }
        });

        tokio::time::sleep(tokio::time::Duration::from_millis(100)).await;

        // Acknowledge publish 3 twice in two separate tasks
        let p3_jh1 = tokio::task::spawn({
            let tracker = tracker.clone();
            let publish3 = publish3.clone();
            async move {
                tracker.ack(&publish3).await.unwrap();
            }
        });
        let p3_jh2 = tokio::task::spawn({
            let tracker = tracker.clone();
            let publish3 = publish3.clone();
            async move {
                tracker.ack(&publish3).await.unwrap();
            }
        });

        // Acks are not complete yet
        tokio::time::sleep(tokio::time::Duration::from_millis(100)).await;
        assert!(!p1_jh.is_finished());
        assert!(!p2_jh.is_finished());
        assert!(!p3_jh1.is_finished());
        assert!(!p3_jh2.is_finished());

        // Register publish 1 requiring 1 ack
        tracker.register_pending(&publish1, 1).unwrap();

        // The task acking publish 1 can now complete, and publish 1 is the "next ready"
        p1_jh.await.unwrap();
        assert_eq!(tracker.try_next_ready().unwrap(), publish1);
        assert!(!tracker.contains(&publish1));

        // Other tasks are not yet completed
        assert!(!p2_jh.is_finished());
        assert!(!p3_jh1.is_finished());
        assert!(!p3_jh2.is_finished());

        // Register publish 2 requiring 2 acks
        tracker.register_pending(&publish2, 2).unwrap();

        // The task acking publish 2 can now complete, and publish 2 is the "next ready"
        p2_jh.await.unwrap();
        assert_eq!(tracker.try_next_ready().unwrap(), publish2);
        assert!(!tracker.contains(&publish2));

        // Other tasks are not yet completed
        assert!(!p3_jh1.is_finished());
        assert!(!p3_jh2.is_finished());

        // Register publish 3 requiring 3 acks
        tracker.register_pending(&publish3, 3).unwrap();

        // Both tasks acking publish 3 can now complete, but publish 3 will not yet be ready,
        // since it requires 3 acks.
        p3_jh1.await.unwrap();
        p3_jh2.await.unwrap();
        assert!(matches!(
            tracker.try_next_ready().err(),
            Some(TryNextReadyError::NotReady)
        ));

        // Final ack for publish 3
        tracker.ack(&publish3).await.unwrap();

        // Publish 3 is now the "next ready", and the tracker is empty
        assert_eq!(tracker.try_next_ready().unwrap(), publish3);
        assert!(matches!(
            tracker.try_next_ready().err(),
            Some(TryNextReadyError::Empty)
        ));
    }

    /// Test the `contains` method
    #[tokio::test]
    async fn contains() {
        // Created empty
        let tracker = PubTracker::default();
        assert!(tracker.pending.lock().unwrap().is_empty());

        // Newly created publish is not inside the tracker
        let publish1 = create_publish("test", "pub1", 1);
        assert!(!tracker.contains(&publish1));
        assert!(tracker
            .pending
            .lock()
            .unwrap()
            .iter()
            .all(|pending| pending.publish.pkid != publish1.pkid));

        // Add the pending publish to the tracker
        tracker.register_pending(&publish1, 1).unwrap();
        assert!(tracker.contains(&publish1));
        assert!(tracker
            .pending
            .lock()
            .unwrap()
            .iter()
            .any(|pending| pending.publish.pkid == publish1.pkid));

        // Another newly created publish is still not inside the tracker
        let publish2 = create_publish("test", "pub2", 2);
        assert!(!tracker.contains(&publish2));
    }

    /// Test the accuracy of the [`TryNextReadyError`] reporting
    #[tokio::test]
    async fn next_ready_errors() {
        // Created empty
        let tracker = PubTracker::default();
        assert!(tracker.pending.lock().unwrap().is_empty());
        assert!(matches!(
            tracker.try_next_ready().err(),
            Some(TryNextReadyError::Empty)
        ));

        // Add a pending publish
        let publish1 = create_publish("test", "pub1", 1);
        tracker.register_pending(&publish1, 1).unwrap();

        // Not ready, but not empty
        {
            let pending_g = tracker.pending.lock().unwrap();
            assert!(!pending_g.is_empty());
            assert_eq!(pending_g.len(), 1);
            let entry = pending_g.front().unwrap();
            assert!(entry.publish.pkid == 1);
            assert!(entry.remaining_acks == 1);
        }
        assert!(matches!(
            tracker.try_next_ready().err(),
            Some(TryNextReadyError::NotReady)
        ));

        // Add another pending publish and ack it, so it is ready
        let publish2 = create_publish("test", "pub2", 2);
        tracker.register_pending(&publish2, 1).unwrap();
        tracker.ack(&publish2).await.unwrap();

        // Next ready still reports the first publish is not ready
        {
            let pending_g = tracker.pending.lock().unwrap();
            assert!(!pending_g.is_empty());
            assert_eq!(pending_g.len(), 2);
            let entry1 = pending_g.front().unwrap();
            assert!(entry1.publish.pkid == 1);
            assert!(entry1.remaining_acks == 1);
            let entry2 = pending_g.back().unwrap();
            assert!(entry2.publish.pkid == 2);
            assert!(entry2.remaining_acks == 0);
        }
        assert!(matches!(
            tracker.try_next_ready().err(),
            Some(TryNextReadyError::NotReady)
        ));
    }

    #[tokio::test]
    async fn ack_overflow() {
        // Created empty
        let tracker = PubTracker::default();
        assert!(tracker.pending.lock().unwrap().is_empty());
        assert!(matches!(
            tracker.try_next_ready().err(),
            Some(TryNextReadyError::Empty)
        ));

        // Register a publish that requires one ack
        let publish = create_publish("test", "pub", 1);
        tracker.register_pending(&publish, 1).unwrap();

        // Acknowledge the publish twice before removing it
        assert!(tracker.ack(&publish).await.is_ok());
        assert!(matches!(
            tracker.ack(&publish).await,
            Err(AckError::AckOverflow)
        ));
    }

    #[tokio::test]
    async fn pkid_0() {
        let tracker = PubTracker::default();
        let publish = create_publish("test", "pub1", 0);

        // Registration succeeds, but does not actually register anything
        assert!(tracker.register_pending(&publish, 1).is_ok());
        assert!(!tracker.contains(&publish));

        // Acknowledging the publish does nothing
        assert!(tracker.ack(&publish).await.is_ok());
        assert!(!tracker.contains(&publish));
        assert!(matches!(
            tracker.try_next_ready().err(),
            Some(TryNextReadyError::Empty)
        ));
    }

    // TODO: tests for reset
    // Reset is not yet implemented properly
}
