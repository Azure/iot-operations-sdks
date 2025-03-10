// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

//! MQTT client wrapper that provides ordered acking functionality.

use std::collections::{HashSet, VecDeque};
use std::sync::{Arc, Mutex};

use thiserror::Error;
use tokio::sync::Notify;

use crate::control_packet::Publish;
use crate::error::{AckError, AckErrorKind};
use crate::interface::{CompletionToken, MqttAck};

/// Error related to PKID
#[derive(Error, Debug, PartialEq)]
pub enum PkidError {
    #[error("Pkid already in queue")]
    PkidDuplicate,
    #[error("Pkid 0 not allowed")]
    PkidZero,
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
    // The underlying MQTT acker that will be used to send PUBACKs
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
                return Err(AckError::new(AckErrorKind::AlreadyAcked));
            }
            pending_acks.insert(publish.pkid);
        }

        loop {
            // Determine if this publish is the correct next ack. If so, pop the data so that
            // the PKID can be re-used.
            // NOTE: This is done before the ack itself so that the lock does not need to be held
            // through an await operation.
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
                    // NOTE: This should not happen when used correctly, as the PKID should always be
                    // inserted into the PKID queue before being acked. However, the implementation
                    // handles this by waiting until the next PKID is inserted into the queue.
                    log::warn!(
                        "Attempted ordered ack for PKID {} but no PKIDs in queue",
                        publish.pkid
                    );
                    false
                }
            };

            // Ack the publish if it is this publish's turn to be acked
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
        if pkid == 0 {
            return Err(PkidError::PkidZero);
        }
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

    /// Returns whether the PKID is already in the queue
    pub fn contains(&mut self, pkid: u16) -> bool {
        self.tracked_pkids.contains(&pkid)
    }

    // Number of PKIDs in the queue
    #[allow(dead_code)]
    pub fn len(&self) -> usize {
        self.queue.len()
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use crate::{
        control_packet::QoS,
        interface_mocks::{MockClient, MockClientCall},
        topic::TopicName,
    };
    use std::{str::FromStr, time::Duration};
    use test_case::test_case;

    fn create_publish_qos(topic_name: &TopicName, payload: &str, pkid: u16, qos: QoS) -> Publish {
        // NOTE: We use the TopicName here for convenience. No other reason.
        // NOTE: If QoS is 0, this WILL OVERRIDE THE PKID (since pkid 0 for QoS 0)
        let mut publish = Publish::new(topic_name.as_str(), qos, payload.to_string(), None);
        if qos != QoS::AtMostOnce {
            publish.pkid = pkid;
        }
        publish
    }

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

    #[test]
    fn pkid_queue_zero() {
        let mut pkid_queue = PkidAckQueue::default();
        assert_eq!(pkid_queue.insert(0).unwrap_err(), PkidError::PkidZero);
    }

    #[test_case(QoS::AtLeastOnce; "QoS 1")]
    #[test_case(QoS::ExactlyOnce; "QoS 2")]
    #[tokio::test]
    async fn ack_ordered_invokes(qos: QoS) {
        // NOTE: This test does NOT include QoS 0 - that has separate behavior covered in other tests
        let mut pkid_queue = PkidAckQueue::default();
        pkid_queue.insert(1).unwrap();
        pkid_queue.insert(2).unwrap();
        pkid_queue.insert(3).unwrap();

        let mock_client = MockClient::new();
        let mock_client_controller = mock_client.mock_controller();
        let acker = OrderedAcker::new(mock_client, Arc::new(Mutex::new(pkid_queue)));

        let topic_name = TopicName::from_str("test").unwrap();
        let publish1 = create_publish_qos(&topic_name, "publish 1", 1, qos);
        let publish2 = create_publish_qos(&topic_name, "publish 2", 2, qos);
        let publish3 = create_publish_qos(&topic_name, "publish 3", 3, qos);

        // No acks yet
        assert_eq!(mock_client_controller.ack_count(), 0);

        // Acking the publishes in the same order of the PKID queue will result in immediate acks
        // on each publish
        acker.ordered_ack(&publish1).await.unwrap();
        assert_eq!(mock_client_controller.ack_count(), 1);
        acker.ordered_ack(&publish2).await.unwrap();
        assert_eq!(mock_client_controller.ack_count(), 2);
        acker.ordered_ack(&publish3).await.unwrap();
        assert_eq!(mock_client_controller.ack_count(), 3);

        // Validate order
        let calls = mock_client_controller.call_sequence();
        assert_eq!(calls.len(), 3);

        match &calls[0] {
            MockClientCall::Ack(call) => {
                assert_eq!(call.publish.pkid, 1);
            }
            _ => panic!("Unexpected call"),
        }

        match &calls[1] {
            MockClientCall::Ack(call) => {
                assert_eq!(call.publish.pkid, 2);
            }
            _ => panic!("Unexpected call"),
        }

        match &calls[2] {
            MockClientCall::Ack(call) => {
                assert_eq!(call.publish.pkid, 3);
            }
            _ => panic!("Unexpected call"),
        }
    }

    #[test_case(QoS::AtLeastOnce; "QoS 1")]
    #[test_case(QoS::ExactlyOnce; "QoS 2")]
    #[tokio::test]
    async fn ack_unordered_invokes(qos: QoS) {
        // NOTE: This test does NOT include QoS 0 - that has separate behavior covered in other tests
        let mut pkid_queue = PkidAckQueue::default();
        pkid_queue.insert(1).unwrap();
        pkid_queue.insert(2).unwrap();
        pkid_queue.insert(3).unwrap();

        let mock_client = MockClient::new();
        let mock_client_controller = mock_client.mock_controller();
        let acker = OrderedAcker::new(mock_client, Arc::new(Mutex::new(pkid_queue)));

        let topic_name = TopicName::from_str("test").unwrap();
        let publish1 = create_publish_qos(&topic_name, "publish 1", 1, qos);
        let publish2 = create_publish_qos(&topic_name, "publish 2", 2, qos);
        let publish3 = create_publish_qos(&topic_name, "publish 3", 3, qos);

        // No acks yet
        assert_eq!(mock_client_controller.ack_count(), 0);

        // Acking the publishes in a different order than the PKID queue will result in the
        // client ack being delayed until the expected ack based on ordering occurs.
        let jh3 = tokio::task::spawn({
            let acker = acker.clone();
            async move {
                acker.ordered_ack(&publish3).await.unwrap();
            }
        });
        let jh2 = tokio::task::spawn({
            let acker = acker.clone();
            async move {
                acker.ordered_ack(&publish2).await.unwrap();
            }
        });
        tokio::time::sleep(Duration::from_secs(1)).await;
        assert!(!jh3.is_finished());
        assert!(!jh2.is_finished());
        assert_eq!(mock_client_controller.ack_count(), 0);

        // Only after finally acking the first publish in the PKID queue will the acks trigger
        acker.ordered_ack(&publish1).await.unwrap();
        tokio::time::sleep(Duration::from_secs(1)).await;
        assert!(jh3.is_finished());
        assert!(jh2.is_finished());
        assert_eq!(mock_client_controller.ack_count(), 3);

        // Validate order
        let calls = mock_client_controller.call_sequence();
        assert_eq!(calls.len(), 3);

        match &calls[0] {
            MockClientCall::Ack(call) => {
                assert_eq!(call.publish.pkid, 1);
            }
            _ => panic!("Unexpected call"),
        }

        match &calls[1] {
            MockClientCall::Ack(call) => {
                assert_eq!(call.publish.pkid, 2);
            }
            _ => panic!("Unexpected call"),
        }

        match &calls[2] {
            MockClientCall::Ack(call) => {
                assert_eq!(call.publish.pkid, 3);
            }
            _ => panic!("Unexpected call"),
        }
    }

    #[tokio::test]
    async fn qos0() {
        let mock_client = MockClient::new();
        let mock_client_controller = mock_client.mock_controller();
        let pkid_queue = PkidAckQueue::default();
        let acker = OrderedAcker::new(mock_client, Arc::new(Mutex::new(pkid_queue)));

        let topic_name = TopicName::from_str("test").unwrap();
        let publish = create_publish_qos(&topic_name, "publish 1", 0, QoS::AtMostOnce);

        assert_eq!(mock_client_controller.ack_count(), 0);

        acker.ordered_ack(&publish).await.unwrap();

        // Even after doing the ordered ack, no ack was actually sent (b/c QoS 0)
        assert_eq!(mock_client_controller.ack_count(), 0);
    }

    #[test_case(QoS::AtLeastOnce; "QoS 1")]
    #[test_case(QoS::ExactlyOnce; "QoS 2")]
    #[tokio::test]
    async fn failure_with_duplicate_pkid(qos: QoS) {
        let mut pkid_queue = PkidAckQueue::default();
        pkid_queue.insert(1).unwrap();
        pkid_queue.insert(2).unwrap();
        pkid_queue.insert(3).unwrap();

        let mock_client = MockClient::new();
        let mock_client_controller = mock_client.mock_controller();
        let acker = OrderedAcker::new(mock_client, Arc::new(Mutex::new(pkid_queue)));

        // Ack a publish that is not yet available for acking due to being behind another PKID in the queue
        let topic_name = TopicName::from_str("test").unwrap();
        let publish = create_publish_qos(&topic_name, "publish", 2, qos);
        let publish_copy = publish.clone();
        let jh1 = tokio::task::spawn({
            let acker = acker.clone();
            async move {
                acker.ordered_ack(&publish).await.unwrap();
            }
        });
        tokio::time::sleep(Duration::from_secs(1)).await;
        assert!(!jh1.is_finished());
        assert!(mock_client_controller.ack_count() == 0);

        // Now ack another publish with the same PKID. This one will fail.
        let result = acker.ordered_ack(&publish_copy).await;
        assert!(result.is_err());
    }
}
