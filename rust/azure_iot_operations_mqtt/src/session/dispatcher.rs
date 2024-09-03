// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
use std::{collections::HashMap, string::FromUtf8Error};

use thiserror::Error;
use tokio::sync::mpsc::{channel, error::SendError, Receiver, Sender};

use crate::control_packet::Publish;
use crate::topic::{TopicFilter, TopicName, TopicParseError};

// NOTE: These errors should almost never happen.
// - Closed receivers can only occur due to race condition since receivers are checked before dispatch.
// - Invalid publishes should not happen at all, since we shouldn't be receiving Publishes from the broker
//   that are invalid.
#[derive(Error, Debug)]
pub enum DispatchError {
    #[error("receiver closed")]
    ClosedReceiver(#[from] SendError<Publish>),
    #[error("could not get topic from publish: {0}")]
    InvalidPublish(#[from] InvalidPublish),
}

// NOTE: if/when Publish is reimplemented, this logic should probably move there.
#[derive(Error, Debug)]
pub enum InvalidPublish {
    #[error("invalid UTF-8")]
    TopicNameUtf8(#[from] FromUtf8Error),
    #[error("invalid topic: {0}")]
    TopicNameFormat(#[from] TopicParseError),
}

pub struct IncomingPublishDispatcher {
    channel_capacity: usize,
    filtered_txs: HashMap<TopicFilter, Vec<Sender<Publish>>>,
    unfiltered_tx: Sender<Publish>,
}

impl IncomingPublishDispatcher {
    pub fn new(capacity: usize) -> (Self, Receiver<Publish>) {
        let (tx, rx) = channel(capacity);
        (
            IncomingPublishDispatcher {
                channel_capacity: capacity,
                filtered_txs: HashMap::new(),
                unfiltered_tx: tx,
            },
            rx,
        )
        // NOTE: There's a case where the unfiltered receiver drops and the dispatcher is still alive.
        // This will cause the dispatch to fail. This is fine, but there's probably a more elegant way to handle this.
        // TODO: Reconsider this after ordered ack implementation.
    }

    /// Register a topic filter for dispatching.
    ///
    /// Returns a receiver that will receive incoming publishes published to the topic filter.
    /// Multiple receivers can be registered for the same topic filter.
    /// If a receiver is closed or dropped, it will be removed from the list of receivers.
    /// If all receivers for a topic filter are closed, the topic filter will be unregistered.
    ///
    /// # Arguments
    /// * `topic_filter` - The [`TopicFilter`] to listen for incoming publishes on.
    pub fn register_filter(&mut self, topic_filter: &TopicFilter) -> Receiver<Publish> {
        self.prune();

        let (tx, rx) = channel(self.channel_capacity);
        // If the topic filter is already in use, add to the associated vector
        if let Some(v) = self.filtered_txs.get_mut(topic_filter) {
            v.push(tx);
        // Otherwise, create a new vector and add
        } else {
            self.filtered_txs.insert(topic_filter.clone(), vec![tx]);
        }
        rx
    }

    /// Dispatch a [`Publish`] to all registered filters that match its topic name.
    ///
    /// The [`Publish`] will be sent to the corresponding receiver(s) for the filters.
    /// If no filters are registered for the topic, the unfiltered receiver will receive the [`Publish`].
    /// Returns the number of receivers that the [`Publish`] was dispatched to.
    ///
    /// # Arguments
    /// * `publish` - The [`Publish`] to dispatch.
    ///
    /// # Errors
    /// Returns a [`DispatchError`] if dispatching fails.
    pub async fn dispatch_publish(&mut self, publish: Publish) -> Result<usize, DispatchError> {
        let mut num_dispatches = 0;
        let mut closed = vec![]; // (Topic filter, position in vector)

        let topic_name = extract_publish_topic_name(&publish)?;

        // First, dispatch to all receivers filters that match the topic name
        let filtered = self
            .filtered_txs
            .iter()
            .filter(|(topic_filter, _)| topic_filter.matches_topic_name(&topic_name));
        for (topic_filter, v) in filtered {
            for (pos, tx) in v.iter().enumerate() {
                // If the receiver is closed, add it to the list of closed receivers to remove after iteration.
                // NOTE: This must be done dynamically because the awaitable send allows for a channel to be closed
                // sometime during the execution of this loop. You cannot simply use .prune() before the loop.
                if tx.is_closed() {
                    closed.push((topic_filter.clone(), pos));
                    continue;
                }
                // Otherwise, send the publish to the receiver
                tx.send(publish.clone()).await?;
                num_dispatches += 1;
            }
        }
        // Then, if no filters matched, dispatch to the unfiltered receiver
        if num_dispatches == 0 {
            self.unfiltered_tx.send(publish).await?;
            num_dispatches += 1;
        }

        // Remove any closed receivers.
        // NOTE: Do this in reverse order to avoid index issues.
        for (topic_filter, pos) in closed.iter().rev() {
            if let Some(v) = self.filtered_txs.get_mut(topic_filter) {
                v.remove(*pos);
                if v.is_empty() {
                    self.filtered_txs.remove(topic_filter);
                }
            }
        }

        // TODO: What should happen in the error cases above?
        // Because a message can be dispatched to multiple receivers, execution should probably not stop just
        // because one send failed. But, this would mean we need to change the error reporting paradigm.
        // Revisit this in the future and find a better way to handle this edge case.

        Ok(num_dispatches)
    }

    /// Remove any closed filter receivers.
    ///
    /// Call this before any register
    /// Note that the runtime is O(c * m) and not O(n * m) as it may seem.
    /// (c = capacity, m = max number of duplicate listeners on a filter, n = number of filters).
    fn prune(&mut self) {
        self.filtered_txs.retain(|_, v| {
            v.retain(|tx| !tx.is_closed());
            !v.is_empty()
        });
    }
}

fn extract_publish_topic_name(publish: &Publish) -> Result<TopicName, InvalidPublish> {
    Ok(TopicName::from_string(String::from_utf8(
        publish.topic.to_vec(),
    )?)?)
}

#[cfg(test)]
mod tests {
    use std::str::FromStr;

    use tokio::sync::mpsc::error::TryRecvError;

    use super::*;
    use crate::control_packet::QoS;

    fn create_publish(topic_name: &TopicName, payload: &str) -> Publish {
        // NOTE: We use the TopicName here for convenience. No other reason.
        Publish::new(
            topic_name.as_str(),
            QoS::AtLeastOnce,
            payload.to_string(),
            None,
        )
    }

    #[tokio::test]
    async fn dispatch_no_filters() {
        let (mut dispatcher, mut unfiltered_rx) = IncomingPublishDispatcher::new(10);

        // Dispatch without registering any filters
        let topic_name = TopicName::from_str("sport/tennis/player1").unwrap();
        let publish = create_publish(&topic_name, "payload 1");

        // Received on the unfiltered receiver
        assert_eq!(
            dispatcher.dispatch_publish(publish.clone()).await.unwrap(),
            1
        );
        assert_eq!(unfiltered_rx.try_recv().unwrap(), publish);
    }

    #[tokio::test]
    async fn dispatch_no_matching_filters() {
        let (mut dispatcher, mut unfiltered_rx) = IncomingPublishDispatcher::new(10);
        let topic_name = TopicName::from_str("sport/tennis/player1").unwrap();

        // Register a filter that does not match the topic name
        let topic_filter = TopicFilter::from_str("finance/banking/banker1").unwrap();
        assert!(!topic_filter.matches_topic_name(&topic_name));
        let mut filtered_rx = dispatcher.register_filter(&topic_filter);

        // Dispatched publish goes to the unfiltered receiver only
        let publish = create_publish(&topic_name, "payload 1");
        assert_eq!(
            dispatcher.dispatch_publish(publish.clone()).await.unwrap(),
            1
        );
        assert_eq!(unfiltered_rx.try_recv().unwrap(), publish);
        assert_eq!(filtered_rx.try_recv().unwrap_err(), TryRecvError::Empty);
    }

    #[tokio::test]
    async fn dispatch_one_matching_filter_exact() {
        let (mut dispatcher, mut unfiltered_rx) = IncomingPublishDispatcher::new(10);
        let topic_s = "sport/tennis/player1";
        let topic_name = TopicName::from_str(topic_s).unwrap();

        // Register a filter that matches topic name exactly (no wildcard)
        let topic_filter1 = TopicFilter::from_str(topic_s).unwrap();
        assert!(topic_filter1.matches_topic_name(&topic_name));
        let mut filtered_rx1 = dispatcher.register_filter(&topic_filter1);

        // Register a filter that does not match topic name
        let topic_filter2 = TopicFilter::from_str("sport/tennis/player2").unwrap();
        assert!(!topic_filter2.matches_topic_name(&topic_name));
        let mut filtered_rx2 = dispatcher.register_filter(&topic_filter2);

        // Dispatched publish goes to the matching filtered receiver only
        let publish = create_publish(&topic_name, "payload 1");
        assert_eq!(
            dispatcher.dispatch_publish(publish.clone()).await.unwrap(),
            1
        );
        assert_eq!(filtered_rx1.try_recv().unwrap(), publish);
        assert_eq!(filtered_rx2.try_recv().unwrap_err(), TryRecvError::Empty);
        assert_eq!(unfiltered_rx.try_recv().unwrap_err(), TryRecvError::Empty);
    }

    #[tokio::test]
    async fn dispatch_one_matching_filter_wildcard() {
        let (mut dispatcher, mut unfiltered_rx) = IncomingPublishDispatcher::new(10);
        let topic_name = TopicName::from_str("sport/tennis/player1").unwrap();

        // Register a filter that matches topic name with a wildcard
        let topic_filter1 = TopicFilter::from_str("sport/+/player1").unwrap();
        assert!(topic_filter1.matches_topic_name(&topic_name));
        let mut filtered_rx1 = dispatcher.register_filter(&topic_filter1);

        // Register a filter that does not match topic name
        let topic_filter2 = TopicFilter::from_str("finance/#").unwrap();
        assert!(!topic_filter2.matches_topic_name(&topic_name));
        let mut filtered_rx2 = dispatcher.register_filter(&topic_filter2);

        // Dispatched publish goes to the filtered receiver only
        let publish = create_publish(&topic_name, "payload 1");
        assert_eq!(
            dispatcher.dispatch_publish(publish.clone()).await.unwrap(),
            1
        );
        assert_eq!(filtered_rx1.try_recv().unwrap(), publish);
        assert_eq!(filtered_rx2.try_recv().unwrap_err(), TryRecvError::Empty);
        assert_eq!(unfiltered_rx.try_recv().unwrap_err(), TryRecvError::Empty);
    }

    #[tokio::test]
    async fn dispatch_multiple_matching_filters_overlapping() {
        let (mut dispatcher, mut unfiltered_rx) = IncomingPublishDispatcher::new(10);
        let topic_name = TopicName::from_str("sport/tennis/player1").unwrap();

        // Topic name matches various different exact and wildcard filters
        let topic_filter1 = TopicFilter::from_str("sport/tennis/player1").unwrap(); // Exact match
        let topic_filter2 = TopicFilter::from_str("sport/+/player1").unwrap(); //Single level wildcard match
        let topic_filter3 = TopicFilter::from_str("sport/#").unwrap(); // Multi-level wildcard match
        assert!(topic_name.matches_topic_filter(&topic_filter1));
        assert!(topic_name.matches_topic_filter(&topic_filter2));
        assert!(topic_name.matches_topic_filter(&topic_filter3));

        // Topic name does not match other various filters
        let topic_filter4 = TopicFilter::from_str("finance/banking/banker1").unwrap();
        let topic_filter5 = TopicFilter::from_str("sport/hockey/+").unwrap();
        let topic_filter6 = TopicFilter::from_str("finance/#").unwrap();
        assert!(!topic_name.matches_topic_filter(&topic_filter4));
        assert!(!topic_name.matches_topic_filter(&topic_filter5));
        assert!(!topic_name.matches_topic_filter(&topic_filter6));

        // Register the filters
        let mut filtered_rx1 = dispatcher.register_filter(&topic_filter1);
        let mut filtered_rx2 = dispatcher.register_filter(&topic_filter2);
        let mut filtered_rx3 = dispatcher.register_filter(&topic_filter3);
        let mut filtered_rx4 = dispatcher.register_filter(&topic_filter4);
        let mut filtered_rx5 = dispatcher.register_filter(&topic_filter5);
        let mut filtered_rx6 = dispatcher.register_filter(&topic_filter6);

        // Dispatched publish goes to the matching filtered receivers only
        let publish = create_publish(&topic_name, "payload 1");
        assert_eq!(
            dispatcher.dispatch_publish(publish.clone()).await.unwrap(),
            3
        );
        assert_eq!(filtered_rx1.try_recv().unwrap(), publish);
        assert_eq!(filtered_rx2.try_recv().unwrap(), publish);
        assert_eq!(filtered_rx3.try_recv().unwrap(), publish);
        assert_eq!(filtered_rx4.try_recv().unwrap_err(), TryRecvError::Empty);
        assert_eq!(filtered_rx5.try_recv().unwrap_err(), TryRecvError::Empty);
        assert_eq!(filtered_rx6.try_recv().unwrap_err(), TryRecvError::Empty);
        assert_eq!(unfiltered_rx.try_recv().unwrap_err(), TryRecvError::Empty);
    }

    #[tokio::test]
    async fn dispatch_multiple_matching_filters_duplicate() {
        let (mut dispatcher, mut unfiltered_rx) = IncomingPublishDispatcher::new(10);
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
        let topic_filter5 = TopicFilter::from_str("finance/banking/banker1").unwrap();
        let topic_filter6 = TopicFilter::from_str("finance/banking/banker1").unwrap();
        let topic_filter7 = TopicFilter::from_str("sport/hockey/+").unwrap();
        let topic_filter8 = TopicFilter::from_str("sport/hockey/+").unwrap();
        assert!(!topic_name.matches_topic_filter(&topic_filter5));
        assert!(!topic_name.matches_topic_filter(&topic_filter6));
        assert!(!topic_name.matches_topic_filter(&topic_filter7));
        assert!(!topic_name.matches_topic_filter(&topic_filter8));

        // Register the filters
        let mut filtered_rx1 = dispatcher.register_filter(&topic_filter1);
        let mut filtered_rx2 = dispatcher.register_filter(&topic_filter2);
        let mut filtered_rx3 = dispatcher.register_filter(&topic_filter3);
        let mut filtered_rx4 = dispatcher.register_filter(&topic_filter4);
        let mut filtered_rx5 = dispatcher.register_filter(&topic_filter5);
        let mut filtered_rx6 = dispatcher.register_filter(&topic_filter6);
        let mut filtered_rx7 = dispatcher.register_filter(&topic_filter7);
        let mut filtered_rx8 = dispatcher.register_filter(&topic_filter8);

        // Dispatched publish goes to the matching filtered receivers only
        let publish = create_publish(&topic_name, "payload 1");
        assert_eq!(
            dispatcher.dispatch_publish(publish.clone()).await.unwrap(),
            4
        );
        assert_eq!(filtered_rx1.try_recv().unwrap(), publish);
        assert_eq!(filtered_rx2.try_recv().unwrap(), publish);
        assert_eq!(filtered_rx3.try_recv().unwrap(), publish);
        assert_eq!(filtered_rx4.try_recv().unwrap(), publish);
        assert_eq!(filtered_rx5.try_recv().unwrap_err(), TryRecvError::Empty);
        assert_eq!(filtered_rx6.try_recv().unwrap_err(), TryRecvError::Empty);
        assert_eq!(filtered_rx7.try_recv().unwrap_err(), TryRecvError::Empty);
        assert_eq!(filtered_rx8.try_recv().unwrap_err(), TryRecvError::Empty);
        assert_eq!(unfiltered_rx.try_recv().unwrap_err(), TryRecvError::Empty);
    }

    #[tokio::test]
    async fn register_unregister_filters() {
        let (mut dispatcher, mut unfiltered_rx) = IncomingPublishDispatcher::new(10);
        let topic_s = "sport/tennis/player1";
        let topic_name = TopicName::from_str(topic_s).unwrap();
        let topic_filter = TopicFilter::from_str(topic_s).unwrap();
        assert!(topic_filter.matches_topic_name(&topic_name));

        // Publish with no filter registered goes to the unfiltered receiver
        let publish = create_publish(&topic_name, "publish #1");
        let num_dispatches = dispatcher.dispatch_publish(publish.clone()).await.unwrap();
        assert_eq!(num_dispatches, 1);
        assert_eq!(unfiltered_rx.try_recv().unwrap(), publish);

        // Register filter
        let mut filter_rx1 = dispatcher.register_filter(&topic_filter);

        // Publish goes to the filtered receiver and not the unfiltered receiver
        let publish = create_publish(&topic_name, "publish #2");
        let num_dispatches = dispatcher.dispatch_publish(publish.clone()).await.unwrap();
        assert_eq!(num_dispatches, 1);
        assert_eq!(filter_rx1.try_recv().unwrap(), publish);
        assert_eq!(unfiltered_rx.try_recv().unwrap_err(), TryRecvError::Empty);

        // Register the same filter again
        let mut filter_rx2 = dispatcher.register_filter(&topic_filter);

        // Publish goes to both filtered receivers, and still not the unfiltered receiver
        let publish = create_publish(&topic_name, "publish #3");
        let num_dispatches = dispatcher.dispatch_publish(publish.clone()).await.unwrap();
        assert_eq!(num_dispatches, 2);
        assert_eq!(filter_rx1.try_recv().unwrap(), publish);
        assert_eq!(filter_rx2.try_recv().unwrap(), publish);
        assert_eq!(unfiltered_rx.try_recv().unwrap_err(), TryRecvError::Empty);

        // Drop one of the receivers to unregister
        drop(filter_rx2);

        // Publish goes to the remaining filtered receiver and not the unfiltered receiver
        let publish = create_publish(&topic_name, "publish #4");
        let num_dispatches = dispatcher.dispatch_publish(publish.clone()).await.unwrap();
        assert_eq!(num_dispatches, 1);
        assert_eq!(filter_rx1.try_recv().unwrap(), publish);
        assert_eq!(unfiltered_rx.try_recv().unwrap_err(), TryRecvError::Empty);

        // Drop the remaining receiver to unregister
        drop(filter_rx1);

        // Publish goes to the unfiltered receiver
        let publish = create_publish(&topic_name, "publish #5");
        let num_dispatches = dispatcher.dispatch_publish(publish.clone()).await.unwrap();
        assert_eq!(num_dispatches, 1);
    }

    #[tokio::test]
    async fn full_unregister_on_register() {
        let (mut dispatcher, _) = IncomingPublishDispatcher::new(10);

        // Register several filters, including duplicates and wildcards
        let topic_filter1 = TopicFilter::from_str("sport/tennis/player1").unwrap(); // Type 1
        let topic_filter2 = topic_filter1.clone(); // Type 1
        let topic_filter3 = topic_filter1.clone(); // Type 1
        let topic_filter4 = TopicFilter::from_str("sport/+/player1").unwrap(); // Type 2
        let topic_filter5 = topic_filter4.clone(); // Type 2
        let topic_filter6 = TopicFilter::from_str("sport/#").unwrap(); // Type 3

        let filter_rx1 = dispatcher.register_filter(&topic_filter1); // Type 1
        let filter_rx2 = dispatcher.register_filter(&topic_filter2); // Type 1
        let filter_rx3 = dispatcher.register_filter(&topic_filter3); // Type 1
        let filter_rx4 = dispatcher.register_filter(&topic_filter4); // Type 2
        let filter_rx5 = dispatcher.register_filter(&topic_filter5); // Type 2
        let filter_rx6 = dispatcher.register_filter(&topic_filter6); // Type 3

        // There are three entires for the exact topic name, two for the single level wildcard, and one for the multi-level wildcard
        assert_eq!(dispatcher.filtered_txs.len(), 3);
        assert_eq!(
            dispatcher.filtered_txs.get(&topic_filter1).unwrap().len(),
            3
        ); // Type 1
        assert_eq!(
            dispatcher.filtered_txs.get(&topic_filter4).unwrap().len(),
            2
        ); // Type 2
        assert_eq!(
            dispatcher.filtered_txs.get(&topic_filter6).unwrap().len(),
            1
        ); // Type 3

        // Drop one of each type of receiver
        drop(filter_rx3); // Type 1
        drop(filter_rx5); // Type 2
        drop(filter_rx6); // Type 3

        // The entires are still the same after the drop
        assert_eq!(dispatcher.filtered_txs.len(), 3);
        assert_eq!(
            dispatcher.filtered_txs.get(&topic_filter1).unwrap().len(),
            3
        ); // Type 1
        assert_eq!(
            dispatcher.filtered_txs.get(&topic_filter4).unwrap().len(),
            2
        ); // Type 2
        assert_eq!(
            dispatcher.filtered_txs.get(&topic_filter6).unwrap().len(),
            1
        ); // Type 3

        // Register a new filter of a different type
        let topic_filter7 = TopicFilter::from_str("finance/banking/banker1").unwrap(); // Type 4
        let filter_rx7 = dispatcher.register_filter(&topic_filter7); // Type 4

        // The entires now include the new filter, but all the dropped filters are removed.
        // When a vector of duplicate filters is empty, it is removed.
        // All remaining receiver entries are still open (implying the correct one was removed).
        assert_eq!(dispatcher.filtered_txs.len(), 3);
        assert_eq!(
            dispatcher.filtered_txs.get(&topic_filter1).unwrap().len(),
            2
        ); // Type 1
        assert!(dispatcher
            .filtered_txs
            .get(&topic_filter1)
            .unwrap()
            .iter()
            .all(|tx| !tx.is_closed())); // Type 1
        assert_eq!(
            dispatcher.filtered_txs.get(&topic_filter4).unwrap().len(),
            1
        ); // Type 2
        assert!(dispatcher
            .filtered_txs
            .get(&topic_filter4)
            .unwrap()
            .iter()
            .all(|tx| !tx.is_closed())); // Type 2
        assert!(!dispatcher.filtered_txs.contains_key(&topic_filter6)); // Type 3
        assert_eq!(
            dispatcher.filtered_txs.get(&topic_filter7).unwrap().len(),
            1
        ); // Type 4
        assert!(dispatcher
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
        assert_eq!(dispatcher.filtered_txs.len(), 3);
        assert_eq!(
            dispatcher.filtered_txs.get(&topic_filter1).unwrap().len(),
            2
        ); // Type 1
        assert_eq!(
            dispatcher.filtered_txs.get(&topic_filter4).unwrap().len(),
            1
        ); // Type 2
        assert!(!dispatcher.filtered_txs.contains_key(&topic_filter6)); // Type 3
        assert_eq!(
            dispatcher.filtered_txs.get(&topic_filter7).unwrap().len(),
            1
        ); // Type 4

        // Register a new filter again
        let topic_filter8 = topic_filter7.clone(); // Type 4
        let filter_rx8 = dispatcher.register_filter(&topic_filter8); // Type 4

        // Once again, all the dropped filters are now removed, with only the most recently
        // registered filter remaining.
        assert_eq!(dispatcher.filtered_txs.len(), 1);
        assert_eq!(
            dispatcher.filtered_txs.get(&topic_filter8).unwrap().len(),
            1
        ); // Type 4
        assert!(dispatcher
            .filtered_txs
            .get(&topic_filter8)
            .unwrap()
            .iter()
            .all(|tx| !tx.is_closed())); // Type 4

        drop(filter_rx8);
    }

    #[tokio::test]
    async fn lazy_unregister_on_dispatch() {
        let (mut dispatcher, _) = IncomingPublishDispatcher::new(10);

        // Register several filters, including duplicates and wildcards
        let topic_filter1 = TopicFilter::from_str("sport/#").unwrap(); // Type 1
        let topic_filter2 = topic_filter1.clone(); // Type 1
        let topic_filter3 = topic_filter1.clone(); // Type 1
        let topic_filter4 = TopicFilter::from_str("sport/+/player1").unwrap(); // Type 2
        let topic_filter5 = topic_filter4.clone(); // Type 2
        let topic_filter6 = TopicFilter::from_str("sport/tennis/player1").unwrap(); // Type 3

        let filter_rx1 = dispatcher.register_filter(&topic_filter1); // Type 1
        let filter_rx2 = dispatcher.register_filter(&topic_filter2); // Type 1
        let filter_rx3 = dispatcher.register_filter(&topic_filter3); // Type 1
        let filter_rx4 = dispatcher.register_filter(&topic_filter4); // Type 2
        let filter_rx5 = dispatcher.register_filter(&topic_filter5); // Type 2
        let filter_rx6 = dispatcher.register_filter(&topic_filter6); // Type 3

        // There are three entires for the multi-level wildcard, two for the single level wildcard, and one requiring exact match
        assert_eq!(dispatcher.filtered_txs.len(), 3);
        assert_eq!(
            dispatcher.filtered_txs.get(&topic_filter3).unwrap().len(),
            3
        ); // Type 1
        assert_eq!(
            dispatcher.filtered_txs.get(&topic_filter5).unwrap().len(),
            2
        ); // Type 2
        assert_eq!(
            dispatcher.filtered_txs.get(&topic_filter6).unwrap().len(),
            1
        ); // Type 3

        // Drop one of each type of receiver
        drop(filter_rx3); // Type 1
        drop(filter_rx5); // Type 2
        drop(filter_rx6); // Type 3

        // The entires are still the same after the drop
        assert_eq!(dispatcher.filtered_txs.len(), 3);
        assert_eq!(
            dispatcher.filtered_txs.get(&topic_filter3).unwrap().len(),
            3
        ); // Type 1
        assert_eq!(
            dispatcher.filtered_txs.get(&topic_filter5).unwrap().len(),
            2
        ); // Type 2
        assert_eq!(
            dispatcher.filtered_txs.get(&topic_filter6).unwrap().len(),
            1
        ); // Type 3

        // Dispatch a publish that matches all the filters for dropped receivers.
        let topic_name = TopicName::from_str("sport/tennis/player1").unwrap();
        assert!(topic_name.matches_topic_filter(&topic_filter3)); // Type 1
        assert!(topic_name.matches_topic_filter(&topic_filter5)); // Type 2
        assert!(topic_name.matches_topic_filter(&topic_filter6)); // Type 3
        let publish = create_publish(&topic_name, "payload 1");
        dispatcher.dispatch_publish(publish.clone()).await.unwrap();

        // The entries are now updated to remove the dropped filters if the dispatched publish topic name
        // matches the dropped filter.
        // Since this publish matched all filters, all dropped receiver entries are now removed.
        // When a vector of duplicate filters is empty, it is removed.
        // The remaining receiver entries are all still open (implying the correct one was removed).
        assert_eq!(dispatcher.filtered_txs.len(), 2);
        assert_eq!(
            dispatcher.filtered_txs.get(&topic_filter3).unwrap().len(),
            2
        ); // Type 1
        assert!(dispatcher
            .filtered_txs
            .get(&topic_filter3)
            .unwrap()
            .iter()
            .all(|tx| !tx.is_closed())); // Type 1
        assert_eq!(
            dispatcher.filtered_txs.get(&topic_filter5).unwrap().len(),
            1
        ); // Type 2
        assert!(dispatcher
            .filtered_txs
            .get(&topic_filter5)
            .unwrap()
            .iter()
            .all(|tx| !tx.is_closed())); // Type 2
        assert!(!dispatcher.filtered_txs.contains_key(&topic_filter6)); // Type 3

        // Drop one of each type of receiver remaining
        drop(filter_rx2); // Type 1
        drop(filter_rx4); // Type 2

        // The entries are still the same after the drop
        assert_eq!(dispatcher.filtered_txs.len(), 2);
        assert_eq!(
            dispatcher.filtered_txs.get(&topic_filter2).unwrap().len(),
            2
        ); // Type 1
        assert_eq!(
            dispatcher.filtered_txs.get(&topic_filter4).unwrap().len(),
            1
        ); // Type 2
        assert!(!dispatcher.filtered_txs.contains_key(&topic_filter6)); // Type 3

        // Dispatch a publish that only matches one of the filters for dropped receivers.
        let topic_name = TopicName::from_str("sport/tennis/player2").unwrap();
        assert!(topic_name.matches_topic_filter(&topic_filter2)); // Type 1
        assert!(!topic_name.matches_topic_filter(&topic_filter4)); // Type 2
        let publish = create_publish(&topic_name, "payload 2");
        dispatcher.dispatch_publish(publish.clone()).await.unwrap();

        // Only the dropped receiver entries filters with a filter that was matched by the dispatched
        // publish topic name were removed.
        // Topic filter 4 was not matched by the dispatched publish, so it remains despite having a dropped receiver.
        // Once again, the remaining receiver entries are all still open (implying the correct one was removed)
        assert_eq!(dispatcher.filtered_txs.len(), 2);
        assert_eq!(
            dispatcher.filtered_txs.get(&topic_filter2).unwrap().len(),
            1
        ); // Type 1
        assert!(dispatcher
            .filtered_txs
            .get(&topic_filter2)
            .unwrap()
            .iter()
            .all(|tx| !tx.is_closed())); // Type 1
        assert_eq!(
            dispatcher.filtered_txs.get(&topic_filter4).unwrap().len(),
            1
        ); // Type 2
        assert!(!dispatcher
            .filtered_txs
            .get(&topic_filter4)
            .unwrap()
            .iter()
            .all(|tx| !tx.is_closed())); // Type 2

        // Drop the remaining receivers
        drop(filter_rx1);
    }

    #[tokio::test]
    async fn drop_unfiltered_receiver() {
        let (mut dispatcher, mut unfiltered_rx) = IncomingPublishDispatcher::new(10);
        let topic_name = TopicName::from_str("sport/tennis/player1").unwrap();
        // Dispatch publish to unfiltered receiver
        let publish = create_publish(&topic_name, "payload 1");
        assert_eq!(
            dispatcher.dispatch_publish(publish.clone()).await.unwrap(),
            1
        );
        assert_eq!(unfiltered_rx.try_recv().unwrap(), publish);
        // Drop the unfiltered receiver
        drop(unfiltered_rx);
        // Dispatching another publish will fail.
        // That's why you shouldn't drop the unfiltered receiver :)
        let publish = create_publish(&topic_name, "payload 2");
        assert!(matches!(
            dispatcher
                .dispatch_publish(publish.clone())
                .await
                .unwrap_err(),
            DispatchError::ClosedReceiver(_)
        ));
    }
}
