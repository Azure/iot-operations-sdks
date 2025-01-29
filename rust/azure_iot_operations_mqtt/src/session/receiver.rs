// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

//! Tooling for sending/receiving publishes to/on multiple receivers which can be distributed in async contexts.

mod ordered_acker;
mod plenary_ack;

use std::collections::HashMap;
use std::string::FromUtf8Error;
use std::sync::{Arc, Mutex};

use thiserror::Error;
use tokio::sync::mpsc::{error::SendError, unbounded_channel, UnboundedReceiver, UnboundedSender};

use crate::control_packet::{Publish, QoS};
use crate::error::AckError;
use crate::interface::{CompletionToken, MqttAck};
use crate::session::receiver::{
    ordered_acker::{OrderedAcker, PkidAckQueue},
    plenary_ack::{PlenaryAck, PlenaryAckMember},
};
use crate::topic::{TopicFilter, TopicName, TopicParseError};

/// Token that can be used to acknowledge a received MQTT publish.
#[derive(Debug)] // TODO: necessary to have debug?
pub struct AckToken(PlenaryAckMember);

impl AckToken {
    /// Acknowledge the received Publish message adn return a [`CompletionToken`] for the completion
    /// of the acknowledgement process.
    ///
    /// # Errors
    /// Returns an [`AckError`] if the Publish message could not be acknowledged.
    pub async fn ack(self) -> Result<CompletionToken, AckError> {
        self.0.ack().await
    }
}

// NOTE: We need to use unbounded channels, because there is no way to know how many
// publishes may be in-flight. The MQTT client can specify a receive_maximum, yes,
// but that only applies to QoS1 and QoS2. There is no limit on QoS0.
// See 3.1.2.11.3 in the MQTT 5.0 spec.
pub type PublishTx = UnboundedSender<(Publish, Option<AckToken>)>;
pub type PublishRx = UnboundedReceiver<(Publish, Option<AckToken>)>;

// NOTE: These errors should almost never happen.
// - Closed receivers can only occur due to race condition since receivers are checked before dispatch.
// - Invalid publishes should not happen at all, since we shouldn't be receiving Publishes from the broker
//   that are invalid.
#[derive(Error, Debug)]
pub enum DispatchError {
    #[error("receiver closed")]
    ClosedReceiver(#[from] SendError<(Publish, Option<AckToken>)>),
    #[error("could not get topic from publish: {0}")]
    InvalidPublishTopic(#[from] InvalidPublish),
}

// NOTE: if/when Publish is reimplemented, this logic should probably move there.
#[derive(Error, Debug)]
pub enum InvalidPublish {
    #[error("invalid UTF-8")]
    TopicNameUtf8(#[from] FromUtf8Error),
    #[error("invalid topic: {0}")]
    TopicNameFormat(#[from] TopicParseError),
}

#[derive(Default)]
pub struct PublishReceiverManager {
    filtered_txs: HashMap<TopicFilter, Vec<PublishTx>>,
    unfiltered_txs: Vec<PublishTx>,
}

impl PublishReceiverManager {
    /// Create a new [`PublishReceiver`] that will receive dispatched [`Publish`]es that match the
    /// provided topic filter for as long as it is open.
    ///
    /// Multiple receivers can be created for the same topic filter, or with overlapping wildcard
    /// topic filters. Each receiver will receive all publishes that match the topic filter.
    ///
    /// # Arguments
    /// * `topic_filter` - The topic filter to match incoming publishes against
    pub fn create_filtered_receiver(&mut self, topic_filter: &TopicFilter) -> PublishRx {
        // NOTE: We prune the filtered txs before registering any more to ensure that closed
        // txs (or entire vectors of txs) don't stick around in the HashMap indefinitely, making
        // dispatching more expensive. We also due cleanup during a dispatch, but since dispatching
        // only looks at the elements of vectors that are relevant to a given dispatch (i.e. lazy pruning),
        // we still need to do a full pruning when registering new tx filters.
        self.prune_filtered_txs();

        let (tx, rx) = unbounded_channel();
        // If the topic filter is already in use, add to the associated vector
        if let Some(v) = self.filtered_txs.get_mut(topic_filter) {
            v.push(tx);
        // Otherwise, create a new vector and add
        } else {
            self.filtered_txs.insert(topic_filter.clone(), vec![tx]);
        }

        rx
    }

    /// Create a new [`PublishReceiver`] that will receive all dispatched [`Publish`]es that do not
    /// match the topic filters for any other filtered [`PublishReceiver`]s, for as long as it
    /// is open.
    ///
    /// Multiple unfiltered receivers can be created, and each will receive all publishes that are
    /// not matched by any filtered receiver.
    pub fn create_unfiltered_receiver(&mut self) -> PublishRx {
        // NOTE: unlike when creating a filtered receiver, we don't need to prune the
        // vector of any closed unfiltered txs here. Since there's not a HashMap, the lazy cleanup
        // during dispatch is sufficient.

        let (tx, rx) = unbounded_channel();
        self.unfiltered_txs.push(tx);
        rx
    }

    /// Remove any closed filter receivers.
    ///
    /// Call this before any register
    /// Note that the runtime is O(c * m) and not O(n * m) as it may seem.
    /// (c = capacity, m = max number of duplicate listeners on a filter, n = number of filters).
    fn prune_filtered_txs(&mut self) {
        self.filtered_txs.retain(|_, v| {
            v.retain(|tx| !tx.is_closed());
            !v.is_empty()
        });
    }
}

/// Manager for creating and dispatching messages to [`PublishReceiver`]s.
pub struct IncomingPublishDispatcher<A>
where
    A: MqttAck + Clone + Send + Sync + 'static,
{
    acker: OrderedAcker<A>,
    pkid_ack_queue: Arc<Mutex<PkidAckQueue>>,
    receiver_manager: Arc<Mutex<PublishReceiverManager>>,
}

impl<A> IncomingPublishDispatcher<A>
where
    A: MqttAck + Clone + Send + Sync + 'static,
{
    /// Create a new [`IncomingPublishDispatcher`] with the provided acker.
    ///
    /// # Arguments
    /// * `acker` - The acker to use for acknowledging incoming publishes
    pub fn new(acker: A) -> Self {
        let pkid_ack_queue = Arc::new(Mutex::new(PkidAckQueue::default()));
        let acker = OrderedAcker::new(acker, pkid_ack_queue.clone());
        Self {
            acker,
            pkid_ack_queue,
            receiver_manager: Arc::new(Mutex::new(PublishReceiverManager::default())),
        }
    }

    pub fn get_receiver_manager(&self) -> Arc<Mutex<PublishReceiverManager>> {
        self.receiver_manager.clone()
    }

    /// Dispatch a [`Publish`] to all relevant receivers.
    ///
    /// The [`Publish`] will be sent to any filtered receivers that correspond to the topic name.
    /// If no filtered receivers that match the [`Publish`] topic name are present, the [`Publish`]
    /// will be sent to all unfiltered receivers.
    ///
    /// Returns the number of receivers that the [`Publish`] was dispatched to.
    ///
    /// # Arguments
    /// * `publish` - The [`Publish`] to dispatch to receivers
    ///
    /// # Errors
    /// Returns a [`DispatchError`] if the dispatch fails.
    pub fn dispatch_publish(&mut self, publish: &Publish) -> Result<usize, DispatchError> {
        let topic_name = extract_publish_topic_name(publish)?;

        // Check if the incoming publish is a duplicate of a publish that is already in the
        // PKID queue.
        if publish.dup && self.pkid_ack_queue.lock().unwrap().contains(publish.pkid) {
            // NOTE: A client is required to treat received duplicates as a new application message
            // as per MQTTv5 spec 4.3.2. However, this is only true when a publish with the same PKID
            // has previously been acked, because it then becomes impossible to tell if this duplicate
            // was a redelivery of the previous message, or a redelivery of another message with the
            // same PKID that was lost.
            //
            // In this case, if we have that same PKID in the queue, we know that the duplicate
            // is for the same publish we are still waiting to ack, because the PKID would not
            // be available for re-use by the broker until that publish was acked.
            // Thus, we can safely discard the duplicate without dispatching.
            // In fact, this is necessary for the correct acking of publishes that were
            // previously dispatched to the receivers.

            log::debug!("Duplicate PUB received for PUB already owned. Discarding");
            return Ok(0);
        }

        // Prepare the PlenaryAck for distributed acking
        let plenary_ack = {
            if publish.qos == QoS::AtMostOnce {
                None
            } else {
                // TODO: handle this error. Should never happen in practice, but should be handled
                // for separation of concern.
                self.pkid_ack_queue
                    .lock()
                    .unwrap()
                    .insert(publish.pkid)
                    .unwrap();
                // Create an acking future for use with a PlenaryAck
                let ack_f = {
                    let acker = self.acker.clone();
                    let publish = publish.clone();
                    async move {
                        let result = acker.ordered_ack(&publish).await;
                        if result.is_ok() {
                            log::debug!("Sent ACK for PKID {}", publish.pkid);
                        } else {
                            // NOTE: can't get the error out of the option enum since we must
                            // return it to the caller.
                            log::error!("ACK failed for PKID {}", publish.pkid);
                        }
                        result
                    }
                };
                Some(PlenaryAck::new(ack_f))
            }
        };

        // Dispatch the publish to all relevant receivers
        let mut num_dispatches = 0;
        // First, dispatch to all filtered receivers that match the topic name
        num_dispatches += self.dispatch_filtered(&topic_name, publish, &plenary_ack)?;
        // Then, if no filters matched, dispatch to all unfiltered receivers (if present)
        if num_dispatches == 0 {
            num_dispatches += self.dispatch_unfiltered(publish, &plenary_ack)?;
        }

        // TODO: What should happen in the error cases above?
        // Because a message can be dispatched to multiple receivers, execution should probably not stop just
        // because one send failed. But, this would mean we need to change the error reporting paradigm.
        // Revisit this in the future and find a better way to handle this edge case.

        // Once all dispatches have been made, commence the plenary ack
        if let Some(plenary_ack) = plenary_ack {
            plenary_ack.commence();
        }

        Ok(num_dispatches)
    }

    /// Dispatch to filtered receivers
    fn dispatch_filtered(
        &mut self,
        topic_name: &TopicName,
        publish: &Publish,
        plenary_ack: &Option<PlenaryAck>,
    ) -> Result<usize, DispatchError> {
        let mut num_dispatches = 0;
        let mut closed = vec![]; // (topic filter, position in vector)

        let mut receiver_manager = self.receiver_manager.lock().unwrap();

        let filtered = receiver_manager
            .filtered_txs
            .iter()
            .filter(|(topic_filter, _)| topic_filter.matches_topic_name(topic_name));
        for (topic_filter, v) in filtered {
            for (pos, tx) in v.iter().enumerate() {
                // If the receiver is closed, add it to the list of closed receivers to remove after iteration.
                // NOTE: This must be done dynamically because the awaitable send allows for a channel to be closed
                // sometime during the execution of this loop. You cannot simply use .prune() before the loop.
                if tx.is_closed() {
                    closed.push((topic_filter.clone(), pos));
                    continue;
                }
                // Otherwise, send the publish to the receiver, along with an ack token
                tx.send((publish.clone(), create_ack_token(plenary_ack)))?;
                num_dispatches += 1;
            }
        }

        // Remove any closed receivers.
        // NOTE: Do this in reverse order to avoid index issues.
        for (topic_filter, pos) in closed.iter().rev() {
            if let Some(v) = receiver_manager.filtered_txs.get_mut(topic_filter) {
                v.remove(*pos);
                if v.is_empty() {
                    receiver_manager.filtered_txs.remove(topic_filter);
                }
            }
        }

        // TODO: is the error case really necessary here?

        Ok(num_dispatches)
    }

    /// Dispatch to unfiltered receivers
    fn dispatch_unfiltered(
        &mut self,
        publish: &Publish,
        plenary_ack: &Option<PlenaryAck>,
    ) -> Result<usize, DispatchError> {
        let mut num_dispatches = 0;
        let mut closed = vec![];

        let mut receiver_manager = self.receiver_manager.lock().unwrap();

        for (pos, tx) in receiver_manager.unfiltered_txs.iter().enumerate() {
            // If the receiver is closed, add it to the list of closed receivers to remove after iteration.
            // NOTE: This must be done dynamically because the awaitable send allows for a channel to be closed
            // sometime during the execution of this loop.
            if tx.is_closed() {
                closed.push(pos);
                continue;
            }
            // Otherwise, send the publish to the receiver, along with an ack token
            tx.send((publish.clone(), create_ack_token(plenary_ack)))?;
            num_dispatches += 1;
        }

        // Remove any closed receivers.
        // NOTE: Do this in reverse order to avoid index issues.
        for pos in closed.iter().rev() {
            receiver_manager.unfiltered_txs.remove(*pos);
        }

        Ok(num_dispatches)
    }
}

fn extract_publish_topic_name(publish: &Publish) -> Result<TopicName, InvalidPublish> {
    Ok(TopicName::from_string(String::from_utf8(
        publish.topic.to_vec(),
    )?)?)
}

fn create_ack_token(plenary_ack: &Option<PlenaryAck>) -> Option<AckToken> {
    plenary_ack
        .as_ref()
        .map(|plenary_ack| AckToken(plenary_ack.create_member()))
}

#[cfg(test)]
mod tests {
    use super::*;
    use crate::control_packet::QoS;
    use crate::interface_mocks::{MockClient, MockClientCall};
    use std::str::FromStr;
    use std::time::Duration;
    use test_case::test_case;
    use tokio::sync::mpsc::error::TryRecvError;

    fn create_publish(topic_name: &TopicName, payload: &str, pkid: u16) -> Publish {
        // NOTE: We use the TopicName here for convenience. No other reason.
        let mut publish = Publish::new(
            topic_name.as_str(),
            QoS::AtLeastOnce,
            payload.to_string(),
            None,
        );
        publish.pkid = pkid;
        publish
    }

    fn create_publish_qos(topic_name: &TopicName, payload: &str, pkid: u16, qos: QoS) -> Publish {
        // NOTE: We use the TopicName here for convenience. No other reason.
        // NOTE: If QoS is 0, this WILL OVERRIDE THE PKID (since pkid 0 for QoS 0)
        let mut publish = Publish::new(topic_name.as_str(), qos, payload.to_string(), None);
        if qos != QoS::AtMostOnce {
            publish.pkid = pkid;
        }
        publish
    }

    fn assert_expected_recv_value(
        recv_value: &(Publish, Option<AckToken>),
        expected_publish: &Publish,
    ) {
        let (publish, ack_token) = recv_value;
        // Received publish is expected publish
        assert_eq!(publish, expected_publish);
        // Ack token is present if QoS is not 0
        if publish.qos == QoS::AtMostOnce {
            assert!(ack_token.is_none());
        } else {
            assert!(ack_token.is_some());
        }
    }

    // NOTE: We need to use tokio::test here in all tests for safety, even when it might seem like we don't.
    // Some of the structs need to use tokio::task::spawn on cleanup, and will panic or have other strange
    // behavior if there is no tokio runtime.

    // NOTE: These tests don't (currently) cover ordered acking, or the resolution of ack tokens.
    // See the tests in managed_client::ordered_acker, and managed_client::plenary_ack for those tests.

    // TODO: drop tests for manager
    // TODO: duplicate publish tests

    #[test_case(QoS::AtMostOnce; "QoS 0")]
    #[test_case(QoS::AtLeastOnce; "QoS 1")]
    #[test_case(QoS::ExactlyOnce; "QoS 2")]
    #[tokio::test]
    async fn dispatch_no_receivers(qos: QoS) {
        let client = MockClient::new();
        let mut dispatcher = IncomingPublishDispatcher::new(client);

        // Dispatch without creating any receivers
        let topic_name = TopicName::from_str("sport/tennis/player1").unwrap();
        let publish = create_publish_qos(&topic_name, "publish 1", 1, qos);

        // Was sent to 0 receivers
        assert_eq!(dispatcher.dispatch_publish(&publish).unwrap(), 0);
    }

    #[test_case(QoS::AtMostOnce; "QoS 0")]
    #[test_case(QoS::AtLeastOnce; "QoS 1")]
    #[test_case(QoS::ExactlyOnce; "QoS 2")]
    #[tokio::test]
    async fn dispatch_one_unfiltered_receiver(qos: QoS) {
        let client = MockClient::new();
        let mut dispatcher = IncomingPublishDispatcher::new(client);
        let manager = dispatcher.get_receiver_manager();

        // Create an unfiltered receiver only (no filtered)
        let mut unfiltered_rx = manager.lock().unwrap().create_unfiltered_receiver();

        // Dispatched publish is received by the unfiltered receiver
        let topic_name = TopicName::from_str("sport/tennis/player1").unwrap();
        let publish = create_publish_qos(&topic_name, "publish 1", 1, qos);
        assert_eq!(dispatcher.dispatch_publish(&publish).unwrap(), 1);
        assert_expected_recv_value(&unfiltered_rx.try_recv().unwrap(), &publish);
    }

    #[test_case(QoS::AtMostOnce; "QoS 0")]
    #[test_case(QoS::AtLeastOnce; "QoS 1")]
    #[test_case(QoS::ExactlyOnce; "QoS 2")]
    #[tokio::test]
    async fn dispatch_multiple_unfiltered_receivers(qos: QoS) {
        let client = MockClient::new();
        let mut dispatcher = IncomingPublishDispatcher::new(client);
        let manager = dispatcher.get_receiver_manager();

        // Create multiple unfiltered receivers (no filtered)
        let mut unfiltered_rx1 = manager.lock().unwrap().create_unfiltered_receiver();
        let mut unfiltered_rx2 = manager.lock().unwrap().create_unfiltered_receiver();
        let mut unfiltered_rx3 = manager.lock().unwrap().create_unfiltered_receiver();

        // Dispatched publish is received by all unfiltered receivers
        let topic_name = TopicName::from_str("sport/tennis/player1").unwrap();
        let publish = create_publish_qos(&topic_name, "payload", 1, qos);
        assert_eq!(dispatcher.dispatch_publish(&publish).unwrap(), 3);
        assert_expected_recv_value(&unfiltered_rx1.try_recv().unwrap(), &publish);
        assert_expected_recv_value(&unfiltered_rx2.try_recv().unwrap(), &publish);
        assert_expected_recv_value(&unfiltered_rx3.try_recv().unwrap(), &publish);
    }

    #[test_case(QoS::AtMostOnce; "QoS 0")]
    #[test_case(QoS::AtLeastOnce; "QoS 1")]
    #[test_case(QoS::ExactlyOnce; "QoS 2")]
    #[tokio::test]
    async fn matching_filtered_receiver_supercedes_unfiltered_receiver(qos: QoS) {
        let client = MockClient::new();
        let mut dispatcher = IncomingPublishDispatcher::new(client);
        let manager = dispatcher.get_receiver_manager();
        let topic_name = TopicName::from_str("sport/tennis/player1").unwrap();

        // Create an unfiltered receiver
        let mut unfiltered_rx = manager.lock().unwrap().create_unfiltered_receiver();

        // Dispatched publish is received by the unfiltered_receiver
        let publish1 = create_publish_qos(&topic_name, "publish 1", 1, qos);
        assert_eq!(dispatcher.dispatch_publish(&publish1).unwrap(), 1);
        assert_expected_recv_value(&unfiltered_rx.try_recv().unwrap(), &publish1);

        // Create a filtered receiver that does NOT match the topic name
        let topic_filter1 = TopicFilter::from_str("finance/bonds/banker1").unwrap();
        assert!(!topic_filter1.matches_topic_name(&topic_name));
        let mut filtered_rx1 = manager
            .lock()
            .unwrap()
            .create_filtered_receiver(&topic_filter1);

        // Dispatching a publish to the same topic is still only received by the unfiltered receiver
        let publish2 = create_publish_qos(&topic_name, "publish 2", 2, qos);
        assert_eq!(dispatcher.dispatch_publish(&publish2).unwrap(), 1);
        assert_expected_recv_value(&unfiltered_rx.try_recv().unwrap(), &publish2);
        assert_eq!(filtered_rx1.try_recv().unwrap_err(), TryRecvError::Empty);

        // Create a filtered receiver that DOES match the topic name
        let topic_filter2 = TopicFilter::from_str("sport/tennis/player1").unwrap();
        assert!(topic_filter2.matches_topic_name(&topic_name));
        let mut filtered_rx2 = manager
            .lock()
            .unwrap()
            .create_filtered_receiver(&topic_filter2);

        // Dispatching a publish to the same topic is now received by the matching filtered receiver ONLY
        let publish3 = create_publish_qos(&topic_name, "publish 3", 3, qos);
        assert_eq!(dispatcher.dispatch_publish(&publish3).unwrap(), 1);
        assert_eq!(unfiltered_rx.try_recv().unwrap_err(), TryRecvError::Empty);
        assert_eq!(filtered_rx1.try_recv().unwrap_err(), TryRecvError::Empty);
        assert_expected_recv_value(&filtered_rx2.try_recv().unwrap(), &publish3);

        // Drop the matching filtered receiver
        drop(filtered_rx2);

        // Dispatching a publish to the same topic is now received by the unfiltered receiver once again
        let publish4 = create_publish_qos(&topic_name, "publish 4", 4, qos);
        assert_eq!(dispatcher.dispatch_publish(&publish4).unwrap(), 1);
        assert_expected_recv_value(&unfiltered_rx.try_recv().unwrap(), &publish4);
        assert_eq!(filtered_rx1.try_recv().unwrap_err(), TryRecvError::Empty);
    }

    // NOTE: for the sake of simplicity from here on out, we will only test with filtered receivers
    // for most cases, since the above tests have established the relationship between filtered and
    // unfiltered receivers (i.e. unfiltered receivers only receive publishes if there is no matching
    // filtered receiver).

    #[test_case(QoS::AtMostOnce; "QoS 0")]
    #[test_case(QoS::AtLeastOnce; "QoS 1")]
    #[test_case(QoS::ExactlyOnce; "QoS 2")]
    #[tokio::test]
    async fn dispatch_no_matching_filtered_receivers(qos: QoS) {
        let client = MockClient::new();
        let mut dispatcher = IncomingPublishDispatcher::new(client);
        let manager = dispatcher.get_receiver_manager();
        let topic_name = TopicName::from_str("sport/tennis/player1").unwrap();

        // Create a receiver that does not match the topic name
        let topic_filter = TopicFilter::from_str("finance/bonds/banker1").unwrap();
        assert!(!topic_filter.matches_topic_name(&topic_name));
        let mut filtered_rx = manager
            .lock()
            .unwrap()
            .create_filtered_receiver(&topic_filter);

        // Dispatched publish is not received
        let publish = create_publish_qos(&topic_name, "publish 1", 1, qos);
        assert_eq!(dispatcher.dispatch_publish(&publish).unwrap(), 0);
        assert_eq!(filtered_rx.try_recv().unwrap_err(), TryRecvError::Empty);
    }

    #[test_case(QoS::AtMostOnce; "QoS 0")]
    #[test_case(QoS::AtLeastOnce; "QoS 1")]
    #[test_case(QoS::ExactlyOnce; "QoS 2")]
    #[tokio::test]
    async fn dispatch_one_matching_filter_exact(qos: QoS) {
        let client = MockClient::new();
        let mut dispatcher = IncomingPublishDispatcher::new(client);
        let manager = dispatcher.get_receiver_manager();
        let topic_name = TopicName::from_str("sport/tennis/player1").unwrap();

        // Create a receiver that matches the topic name exactly (no wildcard)
        let topic_filter = TopicFilter::from_str("sport/tennis/player1").unwrap();
        assert!(topic_filter.matches_topic_name(&topic_name));
        let mut filtered_rx1 = manager
            .lock()
            .unwrap()
            .create_filtered_receiver(&topic_filter);

        // Create a filter that does not match the topic name
        let topic_filter2 = TopicFilter::from_str("sport/tennis/player2").unwrap();
        assert!(!topic_filter2.matches_topic_name(&topic_name));
        let mut filtered_rx2 = manager
            .lock()
            .unwrap()
            .create_filtered_receiver(&topic_filter2);

        // Dispatched publish is received by only the matching filtered receiver
        let publish = create_publish_qos(&topic_name, "publish 1", 1, qos);
        assert_eq!(dispatcher.dispatch_publish(&publish).unwrap(), 1);
        assert_expected_recv_value(&filtered_rx1.try_recv().unwrap(), &publish);
        assert_eq!(filtered_rx2.try_recv().unwrap_err(), TryRecvError::Empty);
    }

    #[test_case(QoS::AtMostOnce; "QoS 0")]
    #[test_case(QoS::AtLeastOnce; "QoS 1")]
    #[test_case(QoS::ExactlyOnce; "QoS 2")]
    #[tokio::test]
    async fn dispatch_one_matching_filter_wildcard(qos: QoS) {
        let client = MockClient::new();
        let mut dispatcher = IncomingPublishDispatcher::new(client);
        let manager = dispatcher.get_receiver_manager();
        let topic_name = TopicName::from_str("sport/tennis/player1").unwrap();

        // Create a receiver that matches the topic name with a wildcard
        let topic_filter = TopicFilter::from_str("sport/+/player1").unwrap();
        assert!(topic_filter.matches_topic_name(&topic_name));
        let mut filtered_rx1 = manager
            .lock()
            .unwrap()
            .create_filtered_receiver(&topic_filter);

        // Create a receiver that does not match topic name
        let topic_filter2 = TopicFilter::from_str("finance/#").unwrap();
        assert!(!topic_filter2.matches_topic_name(&topic_name));
        let mut filtered_rx2 = manager
            .lock()
            .unwrap()
            .create_filtered_receiver(&topic_filter2);

        // Dispatched publish is received by only the matching filtered receiver
        let publish = create_publish_qos(&topic_name, "publish 1", 1, qos);
        assert_eq!(dispatcher.dispatch_publish(&publish).unwrap(), 1);
        assert_expected_recv_value(&filtered_rx1.try_recv().unwrap(), &publish);
        assert_eq!(filtered_rx2.try_recv().unwrap_err(), TryRecvError::Empty);
    }

    #[test_case(QoS::AtMostOnce; "QoS 0")]
    #[test_case(QoS::AtLeastOnce; "QoS 1")]
    #[test_case(QoS::ExactlyOnce; "QoS 2")]
    #[tokio::test]
    async fn dispatch_multiple_matching_filters_overlapping(qos: QoS) {
        let client = MockClient::new();
        let mut dispatcher = IncomingPublishDispatcher::new(client);
        let manager = dispatcher.get_receiver_manager();
        let topic_name = TopicName::from_str("sport/tennis/player1").unwrap();

        // Create a receiver that matches the topic name with a wildcard
        let topic_filter1 = TopicFilter::from_str("sport/+/player1").unwrap();
        assert!(topic_filter1.matches_topic_name(&topic_name));
        let mut filtered_rx1 = manager
            .lock()
            .unwrap()
            .create_filtered_receiver(&topic_filter1);

        // Create a receiver that matches the topic name with a wildcard
        let topic_filter2 = TopicFilter::from_str("sport/tennis/#").unwrap();
        assert!(topic_filter2.matches_topic_name(&topic_name));
        let mut filtered_rx2 = manager
            .lock()
            .unwrap()
            .create_filtered_receiver(&topic_filter2);

        // Create a receiver that does not match the topic name
        let topic_filter3 = TopicFilter::from_str("finance/#").unwrap();
        assert!(!topic_filter3.matches_topic_name(&topic_name));
        let mut filtered_rx3 = manager
            .lock()
            .unwrap()
            .create_filtered_receiver(&topic_filter3);

        // Dispatched publish is received by all matching filtered receivers
        let publish = create_publish_qos(&topic_name, "publish 1", 1, qos);
        assert_eq!(dispatcher.dispatch_publish(&publish).unwrap(), 2);
        assert_expected_recv_value(&filtered_rx1.try_recv().unwrap(), &publish);
        assert_expected_recv_value(&filtered_rx2.try_recv().unwrap(), &publish);
        assert_eq!(filtered_rx3.try_recv().unwrap_err(), TryRecvError::Empty);
    }

    #[test_case(QoS::AtMostOnce; "QoS 0")]
    #[test_case(QoS::AtLeastOnce; "QoS 1")]
    #[test_case(QoS::ExactlyOnce; "QoS 2")]
    #[tokio::test]
    async fn dispatch_multiple_matching_filters_duplicate(qos: QoS) {
        let client = MockClient::new();
        let mut dispatcher = IncomingPublishDispatcher::new(client);
        let manager = dispatcher.get_receiver_manager();
        let topic_name = TopicName::from_str("sport/tennis/player1").unwrap();

        // Topic name matches multiple duplicate exact and wildcard filters
        let topic_filter1 = TopicFilter::from_str("sport/tennis/player1").unwrap(); // Exact match
        let topic_filter2 = TopicFilter::from_str("sport/tennis/player1").unwrap(); // Exact match duplicate
        let topic_filter3 = TopicFilter::from_str("sport/+/player1").unwrap(); // Wildcard match
        let topic_filter4 = TopicFilter::from_str("sport/+/player1").unwrap(); // Wildcard match duplicate
        assert!(topic_name.matches_topic_filter(&topic_filter1));
        assert!(topic_name.matches_topic_filter(&topic_filter2));
        assert!(topic_name.matches_topic_filter(&topic_filter3));
        assert!(topic_name.matches_topic_filter(&topic_filter4));

        // Topic name does not match other duplicate filters
        let topic_filter5 = TopicFilter::from_str("finance/bonds/banker1").unwrap();
        let topic_filter6 = TopicFilter::from_str("finance/bonds/banker1").unwrap();
        let topic_filter7 = TopicFilter::from_str("sport/hockey/+").unwrap();
        let topic_filter8 = TopicFilter::from_str("sport/hockey/+").unwrap();
        assert!(!topic_name.matches_topic_filter(&topic_filter5));
        assert!(!topic_name.matches_topic_filter(&topic_filter6));
        assert!(!topic_name.matches_topic_filter(&topic_filter7));
        assert!(!topic_name.matches_topic_filter(&topic_filter8));

        // Register the filters
        let mut filtered_rx1 = manager
            .lock()
            .unwrap()
            .create_filtered_receiver(&topic_filter1);
        let mut filtered_rx2 = manager
            .lock()
            .unwrap()
            .create_filtered_receiver(&topic_filter2);
        let mut filtered_rx3 = manager
            .lock()
            .unwrap()
            .create_filtered_receiver(&topic_filter3);
        let mut filtered_rx4 = manager
            .lock()
            .unwrap()
            .create_filtered_receiver(&topic_filter4);
        let mut filtered_rx5 = manager
            .lock()
            .unwrap()
            .create_filtered_receiver(&topic_filter5);
        let mut filtered_rx6 = manager
            .lock()
            .unwrap()
            .create_filtered_receiver(&topic_filter6);
        let mut filtered_rx7 = manager
            .lock()
            .unwrap()
            .create_filtered_receiver(&topic_filter7);
        let mut filtered_rx8 = manager
            .lock()
            .unwrap()
            .create_filtered_receiver(&topic_filter8);

        // Dispatched publish goes to the matching filtered receivers only
        let publish = create_publish_qos(&topic_name, "payload 1", 1, qos);
        assert_eq!(dispatcher.dispatch_publish(&publish).unwrap(), 4);
        assert_expected_recv_value(&filtered_rx1.try_recv().unwrap(), &publish);
        assert_expected_recv_value(&filtered_rx2.try_recv().unwrap(), &publish);
        assert_expected_recv_value(&filtered_rx3.try_recv().unwrap(), &publish);
        assert_expected_recv_value(&filtered_rx4.try_recv().unwrap(), &publish);
        assert_eq!(filtered_rx5.try_recv().unwrap_err(), TryRecvError::Empty);
        assert_eq!(filtered_rx6.try_recv().unwrap_err(), TryRecvError::Empty);
        assert_eq!(filtered_rx7.try_recv().unwrap_err(), TryRecvError::Empty);
        assert_eq!(filtered_rx8.try_recv().unwrap_err(), TryRecvError::Empty);
    }

    #[tokio::test]
    async fn create_and_drop_receivers() {
        let client = MockClient::new();
        let mut dispatcher = IncomingPublishDispatcher::new(client);
        let manager = dispatcher.get_receiver_manager();
        let topic_name = TopicName::from_str("sport/tennis/player1").unwrap();
        let topic_filter = TopicFilter::from_str("sport/tennis/player1").unwrap();
        assert!(topic_filter.matches_topic_name(&topic_name));

        // Dispatching publish with no receivers is dispatched 0 times
        let publish1 = create_publish(&topic_name, "publish 1", 1);
        assert_eq!(dispatcher.dispatch_publish(&publish1).unwrap(), 0);

        // Create an unfiltered receiver
        let mut unfiltered_rx1 = manager.lock().unwrap().create_unfiltered_receiver();

        // Dispatching a publish now goes to the newly created unfiltered receiver
        let publish2 = create_publish(&topic_name, "publish 2", 2);
        assert_eq!(dispatcher.dispatch_publish(&publish2).unwrap(), 1);
        assert_expected_recv_value(&unfiltered_rx1.try_recv().unwrap(), &publish2);

        // Create another unfiltered receiver
        let mut unfiltered_rx2 = manager.lock().unwrap().create_unfiltered_receiver();

        // Dispatching a publish now goes to both unfiltered receivers
        let publish3 = create_publish(&topic_name, "publish 3", 3);
        assert_eq!(dispatcher.dispatch_publish(&publish3).unwrap(), 2);
        assert_expected_recv_value(&unfiltered_rx1.try_recv().unwrap(), &publish3);
        assert_expected_recv_value(&unfiltered_rx2.try_recv().unwrap(), &publish3);

        // Create a filtered receiver
        let mut filtered_rx1 = manager
            .lock()
            .unwrap()
            .create_filtered_receiver(&topic_filter);

        // Dispatching a publish now goes only to the newly created filtered receiver
        let publish4 = create_publish(&topic_name, "publish 4", 4);
        assert_eq!(dispatcher.dispatch_publish(&publish4).unwrap(), 1);
        assert_expected_recv_value(&filtered_rx1.try_recv().unwrap(), &publish4);
        assert_eq!(unfiltered_rx1.try_recv().unwrap_err(), TryRecvError::Empty);
        assert_eq!(unfiltered_rx2.try_recv().unwrap_err(), TryRecvError::Empty);

        // Create another filtered receiver for the same topic
        let mut filtered_rx2 = manager
            .lock()
            .unwrap()
            .create_filtered_receiver(&topic_filter);

        // Dispatching a publish now goes only to both filtered receivers
        let publish5 = create_publish(&topic_name, "publish 5", 5);
        assert_eq!(dispatcher.dispatch_publish(&publish5).unwrap(), 2);
        assert_expected_recv_value(&filtered_rx1.try_recv().unwrap(), &publish5);
        assert_expected_recv_value(&filtered_rx2.try_recv().unwrap(), &publish5);
        assert_eq!(unfiltered_rx1.try_recv().unwrap_err(), TryRecvError::Empty);
        assert_eq!(unfiltered_rx2.try_recv().unwrap_err(), TryRecvError::Empty);

        // Drop one of the filtered receivers
        drop(filtered_rx2);

        // Dispatching a publish now goes to only the remaining filtered receiver
        let publish6 = create_publish(&topic_name, "publish 6", 6);
        assert_eq!(dispatcher.dispatch_publish(&publish6).unwrap(), 1);
        assert_expected_recv_value(&filtered_rx1.try_recv().unwrap(), &publish6);
        assert_eq!(unfiltered_rx1.try_recv().unwrap_err(), TryRecvError::Empty);
        assert_eq!(unfiltered_rx2.try_recv().unwrap_err(), TryRecvError::Empty);

        // Drop the remaining filtered receiver
        drop(filtered_rx1);

        // Dispatching a publish now goes to only the unfiltered receivers
        let publish7 = create_publish(&topic_name, "publish 7", 7);
        assert_eq!(dispatcher.dispatch_publish(&publish7).unwrap(), 2);
        assert_expected_recv_value(&unfiltered_rx1.try_recv().unwrap(), &publish7);
        assert_expected_recv_value(&unfiltered_rx2.try_recv().unwrap(), &publish7);

        // Drop one of the unfiltered receivers
        drop(unfiltered_rx2);

        // Dispatching a publish now goes to only the remaining unfiltered receiver
        let publish8 = create_publish(&topic_name, "publish 8", 8);
        assert_eq!(dispatcher.dispatch_publish(&publish8).unwrap(), 1);
        assert_expected_recv_value(&unfiltered_rx1.try_recv().unwrap(), &publish8);

        // Drop the remaining unfiltered receiver
        drop(unfiltered_rx1);

        // Dispatching a publish now goes to 0 receivers
        let publish9 = create_publish(&topic_name, "publish 9", 9);
        assert_eq!(dispatcher.dispatch_publish(&publish9).unwrap(), 0);
    }

    #[tokio::test]
    async fn full_filtered_receiver_cleanup_on_create() {
        let client = MockClient::new();
        let dispatcher = IncomingPublishDispatcher::new(client);
        let manager = dispatcher.get_receiver_manager();

        // Create several filtered receivers, including duplicates and wildcards
        let topic_filter1 = TopicFilter::from_str("sport/tennis/player1").unwrap(); // Type 1
        let topic_filter2 = topic_filter1.clone(); // Type 1
        let topic_filter3 = topic_filter1.clone(); // Type 1
        let topic_filter4 = TopicFilter::from_str("sport/+/player1").unwrap(); // Type 2
        let topic_filter5 = topic_filter4.clone(); // Type 2
        let topic_filter6 = TopicFilter::from_str("sport/#").unwrap(); // Type 3

        let filter_rx1 = manager
            .lock()
            .unwrap()
            .create_filtered_receiver(&topic_filter1); // Type 1
        let filter_rx2 = manager
            .lock()
            .unwrap()
            .create_filtered_receiver(&topic_filter2); // Type 1
        let filter_rx3 = manager
            .lock()
            .unwrap()
            .create_filtered_receiver(&topic_filter3); // Type 1
        let filter_rx4 = manager
            .lock()
            .unwrap()
            .create_filtered_receiver(&topic_filter4); // Type 2
        let filter_rx5 = manager
            .lock()
            .unwrap()
            .create_filtered_receiver(&topic_filter5); // Type 2
        let filter_rx6 = manager
            .lock()
            .unwrap()
            .create_filtered_receiver(&topic_filter6); // Type 3

        // There are three entires for the exact topic name, two for the single level wildcard, and one for the multi-level wildcard
        assert_eq!(manager.lock().unwrap().filtered_txs.len(), 3);
        assert_eq!(
            manager
                .lock()
                .unwrap()
                .filtered_txs
                .get(&topic_filter1)
                .unwrap()
                .len(),
            3
        ); // Type 1
        assert_eq!(
            manager
                .lock()
                .unwrap()
                .filtered_txs
                .get(&topic_filter4)
                .unwrap()
                .len(),
            2
        ); // Type 2
        assert_eq!(
            manager
                .lock()
                .unwrap()
                .filtered_txs
                .get(&topic_filter6)
                .unwrap()
                .len(),
            1
        ); // Type 3

        // Drop one of each type of receiver
        drop(filter_rx3); // Type 1
        drop(filter_rx5); // Type 2
        drop(filter_rx6); // Type 3

        // The entires are still the same after the drop
        assert_eq!(manager.lock().unwrap().filtered_txs.len(), 3);
        assert_eq!(
            manager
                .lock()
                .unwrap()
                .filtered_txs
                .get(&topic_filter1)
                .unwrap()
                .len(),
            3
        ); // Type 1
        assert_eq!(
            manager
                .lock()
                .unwrap()
                .filtered_txs
                .get(&topic_filter4)
                .unwrap()
                .len(),
            2
        ); // Type 2
        assert_eq!(
            manager
                .lock()
                .unwrap()
                .filtered_txs
                .get(&topic_filter6)
                .unwrap()
                .len(),
            1
        ); // Type 3

        // Register a new filter of a different type
        let topic_filter7 = TopicFilter::from_str("finance/bonds/banker1").unwrap(); // Type 4
        let filter_rx7 = manager
            .lock()
            .unwrap()
            .create_filtered_receiver(&topic_filter7); // Type 4

        // The entires now include the new filter, but all the dropped filters are removed.
        // When a vector of duplicate filters is empty, it is removed.
        // All remaining receiver entries are still open (implying the correct one was removed).
        assert_eq!(manager.lock().unwrap().filtered_txs.len(), 3);
        assert_eq!(
            manager
                .lock()
                .unwrap()
                .filtered_txs
                .get(&topic_filter1)
                .unwrap()
                .len(),
            2
        ); // Type 1
        assert!(manager
            .lock()
            .unwrap()
            .filtered_txs
            .get(&topic_filter1)
            .unwrap()
            .iter()
            .all(|tx| !tx.is_closed())); // Type 1
        assert_eq!(
            manager
                .lock()
                .unwrap()
                .filtered_txs
                .get(&topic_filter4)
                .unwrap()
                .len(),
            1
        ); // Type 2
        assert!(manager
            .lock()
            .unwrap()
            .filtered_txs
            .get(&topic_filter4)
            .unwrap()
            .iter()
            .all(|tx| !tx.is_closed())); // Type 2
        assert!(!manager
            .lock()
            .unwrap()
            .filtered_txs
            .contains_key(&topic_filter6)); // Type 3
        assert_eq!(
            manager
                .lock()
                .unwrap()
                .filtered_txs
                .get(&topic_filter7)
                .unwrap()
                .len(),
            1
        ); // Type 4
        assert!(manager
            .lock()
            .unwrap()
            .filtered_txs
            .get(&topic_filter7)
            .unwrap()
            .iter()
            .all(|tx| !tx.is_closed())); // Type 4

        // Drop the remaining receivers
        drop(filter_rx1); // Type 1
        drop(filter_rx2); // Type 1
        drop(filter_rx4); // Type 2
        drop(filter_rx7); // Type 4

        // The entries are still the same
        assert_eq!(manager.lock().unwrap().filtered_txs.len(), 3);
        assert_eq!(
            manager
                .lock()
                .unwrap()
                .filtered_txs
                .get(&topic_filter1)
                .unwrap()
                .len(),
            2
        ); // Type 1
        assert_eq!(
            manager
                .lock()
                .unwrap()
                .filtered_txs
                .get(&topic_filter4)
                .unwrap()
                .len(),
            1
        ); // Type 2
        assert!(!manager
            .lock()
            .unwrap()
            .filtered_txs
            .contains_key(&topic_filter6)); // Type 3
        assert_eq!(
            manager
                .lock()
                .unwrap()
                .filtered_txs
                .get(&topic_filter7)
                .unwrap()
                .len(),
            1
        ); // Type 4

        // Register a new filter again
        let topic_filter8 = topic_filter7.clone(); // Type 4
        let filter_rx8 = manager
            .lock()
            .unwrap()
            .create_filtered_receiver(&topic_filter8); // Type 4

        // Once again, all the dropped filters are now removed, with only the most recently
        // registered filter remaining.
        assert_eq!(manager.lock().unwrap().filtered_txs.len(), 1);
        assert_eq!(
            manager
                .lock()
                .unwrap()
                .filtered_txs
                .get(&topic_filter8)
                .unwrap()
                .len(),
            1
        ); // Type 4
        assert!(manager
            .lock()
            .unwrap()
            .filtered_txs
            .get(&topic_filter8)
            .unwrap()
            .iter()
            .all(|tx| !tx.is_closed())); // Type 4

        drop(filter_rx8);
    }

    #[tokio::test]
    async fn no_unfiltered_receiver_cleanup_on_create() {
        let client = MockClient::new();
        let dispatcher = IncomingPublishDispatcher::new(client);
        let manager = dispatcher.get_receiver_manager();

        // Create several unfiltered receivers
        let unfiltered_rx1 = manager.lock().unwrap().create_unfiltered_receiver();
        let unfiltered_rx2 = manager.lock().unwrap().create_unfiltered_receiver();
        let unfiltered_rx3 = manager.lock().unwrap().create_unfiltered_receiver();

        // There are three entries for the unfiltered receivers
        assert_eq!(manager.lock().unwrap().unfiltered_txs.len(), 3);

        // Drop one of the receivers
        drop(unfiltered_rx2);

        // The entries are still the same after the drop
        assert_eq!(manager.lock().unwrap().unfiltered_txs.len(), 3);

        // Create a new unfiltered receiver
        let unfiltered_rx4 = manager.lock().unwrap().create_unfiltered_receiver();

        // The entries now include the new receiver, but the dropped receiver is still there
        assert_eq!(manager.lock().unwrap().unfiltered_txs.len(), 4);

        // Drop the remaining receivers
        drop(unfiltered_rx1);
        drop(unfiltered_rx3);
        drop(unfiltered_rx4);
    }

    #[tokio::test]
    async fn lazy_filtered_receiver_cleanup_on_dispatch() {
        let client = MockClient::new();
        let mut dispatcher = IncomingPublishDispatcher::new(client);
        let manager = dispatcher.get_receiver_manager();

        // Register several filters, including duplicates and wildcards
        let topic_filter1 = TopicFilter::from_str("sport/#").unwrap(); // Type 1
        let topic_filter2 = topic_filter1.clone(); // Type 1
        let topic_filter3 = topic_filter1.clone(); // Type 1
        let topic_filter4 = TopicFilter::from_str("sport/+/player1").unwrap(); // Type 2
        let topic_filter5 = topic_filter4.clone(); // Type 2
        let topic_filter6 = TopicFilter::from_str("sport/tennis/player1").unwrap(); // Type 3

        let filter_rx1 = manager
            .lock()
            .unwrap()
            .create_filtered_receiver(&topic_filter1); // Type 1
        let filter_rx2 = manager
            .lock()
            .unwrap()
            .create_filtered_receiver(&topic_filter2); // Type 1
        let filter_rx3 = manager
            .lock()
            .unwrap()
            .create_filtered_receiver(&topic_filter3); // Type 1
        let filter_rx4 = manager
            .lock()
            .unwrap()
            .create_filtered_receiver(&topic_filter4); // Type 2
        let filter_rx5 = manager
            .lock()
            .unwrap()
            .create_filtered_receiver(&topic_filter5); // Type 2
        let filter_rx6 = manager
            .lock()
            .unwrap()
            .create_filtered_receiver(&topic_filter6); // Type 3

        // There are three entires for the multi-level wildcard, two for the single level wildcard, and one requiring exact match
        assert_eq!(manager.lock().unwrap().filtered_txs.len(), 3);
        assert_eq!(
            manager
                .lock()
                .unwrap()
                .filtered_txs
                .get(&topic_filter3)
                .unwrap()
                .len(),
            3
        ); // Type 1
        assert_eq!(
            manager
                .lock()
                .unwrap()
                .filtered_txs
                .get(&topic_filter5)
                .unwrap()
                .len(),
            2
        ); // Type 2
        assert_eq!(
            manager
                .lock()
                .unwrap()
                .filtered_txs
                .get(&topic_filter6)
                .unwrap()
                .len(),
            1
        ); // Type 3

        // Drop one of each type of receiver
        drop(filter_rx3); // Type 1
        drop(filter_rx5); // Type 2
        drop(filter_rx6); // Type 3

        // The entires are still the same after the drop
        assert_eq!(manager.lock().unwrap().filtered_txs.len(), 3);
        assert_eq!(
            manager
                .lock()
                .unwrap()
                .filtered_txs
                .get(&topic_filter3)
                .unwrap()
                .len(),
            3
        ); // Type 1
        assert_eq!(
            manager
                .lock()
                .unwrap()
                .filtered_txs
                .get(&topic_filter5)
                .unwrap()
                .len(),
            2
        ); // Type 2
        assert_eq!(
            manager
                .lock()
                .unwrap()
                .filtered_txs
                .get(&topic_filter6)
                .unwrap()
                .len(),
            1
        ); // Type 3

        // Dispatch a publish that matches all the filters for dropped receivers.
        let topic_name = TopicName::from_str("sport/tennis/player1").unwrap();
        assert!(topic_name.matches_topic_filter(&topic_filter3)); // Type 1
        assert!(topic_name.matches_topic_filter(&topic_filter5)); // Type 2
        assert!(topic_name.matches_topic_filter(&topic_filter6)); // Type 3
        let publish = create_publish(&topic_name, "payload 1", 1);
        dispatcher.dispatch_publish(&publish).unwrap();

        // The entries are now updated to remove the dropped filters if the dispatched publish topic name
        // matches the dropped filter.
        // Since this publish matched all filters, all dropped receiver entries are now removed.
        // When a vector of duplicate filters is empty, it is removed.
        // The remaining receiver entries are all still open (implying the correct one was removed).
        assert_eq!(manager.lock().unwrap().filtered_txs.len(), 2);
        assert_eq!(
            manager
                .lock()
                .unwrap()
                .filtered_txs
                .get(&topic_filter3)
                .unwrap()
                .len(),
            2
        ); // Type 1
        assert!(manager
            .lock()
            .unwrap()
            .filtered_txs
            .get(&topic_filter3)
            .unwrap()
            .iter()
            .all(|tx| !tx.is_closed())); // Type 1
        assert_eq!(
            manager
                .lock()
                .unwrap()
                .filtered_txs
                .get(&topic_filter5)
                .unwrap()
                .len(),
            1
        ); // Type 2
        assert!(manager
            .lock()
            .unwrap()
            .filtered_txs
            .get(&topic_filter5)
            .unwrap()
            .iter()
            .all(|tx| !tx.is_closed())); // Type 2
        assert!(!manager
            .lock()
            .unwrap()
            .filtered_txs
            .contains_key(&topic_filter6)); // Type 3

        // Drop one of each type of receiver remaining
        drop(filter_rx2); // Type 1
        drop(filter_rx4); // Type 2

        // The entries are still the same after the drop
        assert_eq!(manager.lock().unwrap().filtered_txs.len(), 2);
        assert_eq!(
            manager
                .lock()
                .unwrap()
                .filtered_txs
                .get(&topic_filter2)
                .unwrap()
                .len(),
            2
        ); // Type 1
        assert_eq!(
            manager
                .lock()
                .unwrap()
                .filtered_txs
                .get(&topic_filter4)
                .unwrap()
                .len(),
            1
        ); // Type 2
        assert!(!manager
            .lock()
            .unwrap()
            .filtered_txs
            .contains_key(&topic_filter6)); // Type 3

        // Dispatch a publish that only matches one of the filters for dropped receivers.
        let topic_name = TopicName::from_str("sport/tennis/player2").unwrap();
        assert!(topic_name.matches_topic_filter(&topic_filter2)); // Type 1
        assert!(!topic_name.matches_topic_filter(&topic_filter4)); // Type 2
        let publish = create_publish(&topic_name, "payload 2", 2);
        dispatcher.dispatch_publish(&publish).unwrap();

        // Only the dropped receiver entries filters with a filter that was matched by the dispatched
        // publish topic name were removed.
        // Topic filter 4 was not matched by the dispatched publish, so it remains despite having a dropped receiver.
        // Once again, the remaining receiver entries are all still open (implying the correct one was removed)
        assert_eq!(manager.lock().unwrap().filtered_txs.len(), 2);
        assert_eq!(
            manager
                .lock()
                .unwrap()
                .filtered_txs
                .get(&topic_filter2)
                .unwrap()
                .len(),
            1
        ); // Type 1
        assert!(manager
            .lock()
            .unwrap()
            .filtered_txs
            .get(&topic_filter2)
            .unwrap()
            .iter()
            .all(|tx| !tx.is_closed())); // Type 1
        assert_eq!(
            manager
                .lock()
                .unwrap()
                .filtered_txs
                .get(&topic_filter4)
                .unwrap()
                .len(),
            1
        ); // Type 2
        assert!(!manager
            .lock()
            .unwrap()
            .filtered_txs
            .get(&topic_filter4)
            .unwrap()
            .iter()
            .all(|tx| !tx.is_closed())); // Type 2

        // Drop the remaining receivers
        drop(filter_rx1);
    }

    #[tokio::test]
    async fn lazy_unfiltered_receiver_cleanup_on_dispatch() {
        let client = MockClient::new();
        let mut dispatcher = IncomingPublishDispatcher::new(client);
        let manager = dispatcher.get_receiver_manager();

        // Create several unfiltered receivers
        let unfiltered_rx1 = manager.lock().unwrap().create_unfiltered_receiver();
        let unfiltered_rx2 = manager.lock().unwrap().create_unfiltered_receiver();
        let unfiltered_rx3 = manager.lock().unwrap().create_unfiltered_receiver();

        // There are three entries for the unfiltered receivers
        assert_eq!(manager.lock().unwrap().unfiltered_txs.len(), 3);

        // Drop one of the receivers
        drop(unfiltered_rx2);

        // The entries are still the same after the drop
        assert_eq!(manager.lock().unwrap().unfiltered_txs.len(), 3);

        // Dispatch a publish
        let topic_name = TopicName::from_str("sport/tennis/player1").unwrap();
        let publish = create_publish(&topic_name, "payload 1", 1);
        dispatcher.dispatch_publish(&publish).unwrap();

        // The entries are now updated to remove the dropped receiver
        assert_eq!(manager.lock().unwrap().unfiltered_txs.len(), 2);

        // Drop the remaining receivers
        drop(unfiltered_rx1);
        drop(unfiltered_rx3);
    }

    #[test_case(QoS::AtLeastOnce; "QoS 1")]
    #[test_case(QoS::ExactlyOnce; "QoS 2")]
    #[tokio::test]
    async fn ack_token_single_dispatch(qos: QoS) {
        let client = MockClient::new();
        let mock_controller = client.mock_controller();
        let mut dispatcher = IncomingPublishDispatcher::new(client);
        let manager = dispatcher.get_receiver_manager();

        // Create an unfiltered receiver
        let mut unfiltered_rx = manager.lock().unwrap().create_unfiltered_receiver();

        // Dispatched publish is received by the unfiltered receiver
        let topic_name = TopicName::from_str("sport/tennis/player1").unwrap();
        let publish = create_publish_qos(&topic_name, "publish 1", 1, qos);
        assert_eq!(dispatcher.dispatch_publish(&publish).unwrap(), 1);
        let (r_publish, ack_token) = unfiltered_rx.try_recv().unwrap();

        // No ack has occurred for the publish yet
        assert_eq!(mock_controller.ack_count(), 0);

        // Acknowledge the received publish
        ack_token.unwrap().ack().await.unwrap();

        // Ack has now occurred for the publish
        assert_eq!(mock_controller.ack_count(), 1);
        let calls = mock_controller.call_sequence();
        assert_eq!(calls.len(), 1);
        match &calls[0] {
            MockClientCall::Ack(call) => {
                assert_eq!(call.publish, r_publish);
            }
            _ => panic!("Expected AcknowledgePublish"),
        }
    }

    #[test_case(QoS::AtLeastOnce; "QoS 1")]
    #[test_case(QoS::ExactlyOnce; "QoS 2")]
    #[tokio::test]
    async fn ack_token_multi_dispatch(qos: QoS) {
        let client = MockClient::new();
        let mock_controller = client.mock_controller();
        let mut dispatcher = IncomingPublishDispatcher::new(client);
        let manager = dispatcher.get_receiver_manager();

        // Create unfiltered receivers
        let mut unfiltered_rx1 = manager.lock().unwrap().create_unfiltered_receiver();
        let mut unfiltered_rx2 = manager.lock().unwrap().create_unfiltered_receiver();

        // Dispatched publish is received by the unfiltered receivers
        let topic_name = TopicName::from_str("sport/tennis/player1").unwrap();
        let publish = create_publish_qos(&topic_name, "payload", 1, qos);
        assert_eq!(dispatcher.dispatch_publish(&publish).unwrap(), 2);
        let (r_publish1, ack_token1) = unfiltered_rx1.try_recv().unwrap();
        let (r_publish2, ack_token2) = unfiltered_rx2.try_recv().unwrap();

        // No ack has occurred for the publish yet
        assert_eq!(mock_controller.ack_count(), 0);

        // Acknowledge the received publish with one of the ack tokens
        let jh1 = tokio::task::spawn(ack_token1.unwrap().ack());

        // Ack has not yet occurred on the mock client, nor has the task on the token been returned
        tokio::time::sleep(Duration::from_secs(1)).await;
        assert_eq!(mock_controller.ack_count(), 0);
        assert!(!jh1.is_finished());

        // Acknowledge the received publish with the other ack token
        let jh2 = tokio::task::spawn(ack_token2.unwrap().ack());

        // Ack has now occurred for the publish and both ack tasks returned
        tokio::time::sleep(Duration::from_secs(1)).await;
        assert_eq!(mock_controller.ack_count(), 1);
        let calls = mock_controller.call_sequence();
        assert_eq!(calls.len(), 1);
        match &calls[0] {
            MockClientCall::Ack(call) => {
                assert_eq!(call.publish, publish);
            }
            _ => panic!("Expected AcknowledgePublish"),
        }
        assert_eq!(r_publish1, publish);
        assert_eq!(r_publish2, publish);
        assert!(jh1.is_finished());
        assert!(jh2.is_finished());
    }

    #[test_case(QoS::AtLeastOnce; "QoS 1")]
    #[test_case(QoS::ExactlyOnce; "QoS 2")]
    #[tokio::test]
    async fn ack_token_drop_before_ack_single_dispatch(qos: QoS) {
        let client = MockClient::new();
        let mock_controller = client.mock_controller();
        let mut dispatcher = IncomingPublishDispatcher::new(client);
        let manager = dispatcher.get_receiver_manager();

        // Create an unfiltered receiver
        let mut unfiltered_rx = manager.lock().unwrap().create_unfiltered_receiver();

        // Dispatched publish is received by the unfiltered receiver
        let topic_name = TopicName::from_str("sport/tennis/player1").unwrap();
        let publish = create_publish_qos(&topic_name, "publish 1", 1, qos);
        assert_eq!(dispatcher.dispatch_publish(&publish).unwrap(), 1);
        let (r_publish, ack_token) = unfiltered_rx.try_recv().unwrap();

        // No ack has occurred for the publish yet
        assert_eq!(mock_controller.ack_count(), 0);

        // Drop the ack token without explicitly invoking ack
        drop(ack_token);

        // Ack has now occurred for the publish
        tokio::time::sleep(Duration::from_secs(1)).await;
        assert_eq!(mock_controller.ack_count(), 1);
        let calls = mock_controller.call_sequence();
        assert_eq!(calls.len(), 1);
        match &calls[0] {
            MockClientCall::Ack(call) => {
                assert_eq!(call.publish, r_publish);
            }
            _ => panic!("Expected AcknowledgePublish"),
        }
    }

    #[test_case(QoS::AtLeastOnce; "QoS 1")]
    #[test_case(QoS::ExactlyOnce; "QoS 2")]
    #[tokio::test]
    async fn ack_token_drop_before_ack_multi_dispatch(qos: QoS) {
        let client = MockClient::new();
        let mock_controller = client.mock_controller();
        let mut dispatcher = IncomingPublishDispatcher::new(client);
        let manager = dispatcher.get_receiver_manager();

        // Create an unfiltered receiver
        let mut unfiltered_rx1 = manager.lock().unwrap().create_unfiltered_receiver();
        let mut unfiltered_rx2 = manager.lock().unwrap().create_unfiltered_receiver();

        // Dispatched publish is received by the unfiltered receiver
        let topic_name = TopicName::from_str("sport/tennis/player1").unwrap();
        let publish = create_publish_qos(&topic_name, "publish 1", 1, qos);
        assert_eq!(dispatcher.dispatch_publish(&publish).unwrap(), 2);
        let (_, ack_token1) = unfiltered_rx1.try_recv().unwrap();
        let (_, ack_token2) = unfiltered_rx2.try_recv().unwrap();

        // No ack has occurred for the publish yet
        assert_eq!(mock_controller.ack_count(), 0);

        // Drop the first ack token without explicitly invoking ack
        drop(ack_token1);

        // Ack has not yet occurred for the publish
        tokio::time::sleep(Duration::from_secs(1)).await;
        assert_eq!(mock_controller.ack_count(), 0);

        // Ack the second ack token
        ack_token2.unwrap().ack().await.unwrap();

        // Ack has now occurred for the publish
        tokio::time::sleep(Duration::from_secs(1)).await;
        assert_eq!(mock_controller.ack_count(), 1);
        let calls = mock_controller.call_sequence();
        assert_eq!(calls.len(), 1);
        match &calls[0] {
            MockClientCall::Ack(call) => {
                assert_eq!(call.publish, publish);
            }
            _ => panic!("Expected AcknowledgePublish"),
        }
    }

    #[test_case(QoS::AtLeastOnce; "QoS 1")]
    #[test_case(QoS::ExactlyOnce; "QoS 2")]
    #[tokio::test]
    async fn receiver_drop_before_ack_single_dispatch(qos: QoS) {
        let client = MockClient::new();
        let mock_controller = client.mock_controller();
        let mut dispatcher = IncomingPublishDispatcher::new(client);
        let manager = dispatcher.get_receiver_manager();

        // Create an unfiltered receiver
        let unfiltered_rx = manager.lock().unwrap().create_unfiltered_receiver();

        // Dispatch to the receiver, but do not actually receive on the receiver
        let topic_name = TopicName::from_str("sport/tennis/player1").unwrap();
        let publish = create_publish_qos(&topic_name, "publish 1", 1, qos);
        assert_eq!(dispatcher.dispatch_publish(&publish).unwrap(), 1);

        // No ack has occurred for the publish yet
        assert_eq!(mock_controller.ack_count(), 0);

        // Drop the receiver before receiving/acknowledging the publish
        drop(unfiltered_rx);

        // Ack has now occurred for the publish
        tokio::time::sleep(Duration::from_secs(1)).await;
        assert_eq!(mock_controller.ack_count(), 1);
        let calls = mock_controller.call_sequence();
        assert_eq!(calls.len(), 1);
        match &calls[0] {
            MockClientCall::Ack(call) => {
                assert_eq!(call.publish, publish);
            }
            _ => panic!("Expected AcknowledgePublish"),
        }
    }

    #[test_case(QoS::AtLeastOnce; "QoS 1")]
    #[test_case(QoS::ExactlyOnce; "QoS 2")]
    #[tokio::test]
    async fn receiver_drop_before_ack_multi_dispatch(qos: QoS) {
        let client = MockClient::new();
        let mock_controller = client.mock_controller();
        let mut dispatcher = IncomingPublishDispatcher::new(client);
        let manager = dispatcher.get_receiver_manager();

        // Create an unfiltered receiver
        let unfiltered_rx1 = manager.lock().unwrap().create_unfiltered_receiver();
        let mut unfiltered_rx2 = manager.lock().unwrap().create_unfiltered_receiver();

        // Dispatch to the receiver, but do not actually receive on the receiver
        let topic_name = TopicName::from_str("sport/tennis/player1").unwrap();
        let publish = create_publish_qos(&topic_name, "publish 1", 1, qos);
        assert_eq!(dispatcher.dispatch_publish(&publish).unwrap(), 2);

        // No ack has occurred for the publish yet
        assert_eq!(mock_controller.ack_count(), 0);

        // Drop the receiver before receiving/acknowledging the publish
        drop(unfiltered_rx1);

        // Still, no ack has occurred for the publish yet
        tokio::time::sleep(Duration::from_secs(1)).await;
        assert_eq!(mock_controller.ack_count(), 0);

        // Acknowledge the other ack token normally
        let (_, ack_token) = unfiltered_rx2.try_recv().unwrap();
        ack_token.unwrap().ack().await.unwrap();

        // Ack has now occurred for the publish
        tokio::time::sleep(Duration::from_secs(1)).await;
        assert_eq!(mock_controller.ack_count(), 1);
        let calls = mock_controller.call_sequence();
        assert_eq!(calls.len(), 1);
        match &calls[0] {
            MockClientCall::Ack(call) => {
                assert_eq!(call.publish, publish);
            }
            _ => panic!("Expected AcknowledgePublish"),
        }
    }
}
