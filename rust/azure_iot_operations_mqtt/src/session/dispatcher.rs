// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

//! Structures for dispatching incoming MQTT publishes to receivers

use std::{
    cell::RefCell,
    collections::HashMap,
    pin::Pin,
    task::{Context, Poll},
};

use azure_mqtt::{client::ManualAcknowledgement, packet::Publish, topic::TopicFilter};
use tokio::sync::mpsc::{UnboundedReceiver, UnboundedSender, unbounded_channel};

use crate::error::{ClientError, CompletionError};
use crate::session::plenary_ack::{PlenaryAck, PlenaryAckCompletionToken, PlenaryAckMember};

pub struct AckToken(PlenaryAckMember);

impl AckToken {
    pub async fn ack(self) -> Result<AckCompletionToken, ClientError> {
        self.0.ack().await.map(|token| AckCompletionToken(token))
    }
}

pub struct AckCompletionToken(PlenaryAckCompletionToken);

impl Future for AckCompletionToken {
    type Output = Result<(), CompletionError>;

    fn poll(mut self: Pin<&mut Self>, cx: &mut Context<'_>) -> Poll<Self::Output> {
        Pin::new(&mut self.0).poll(cx)
    }
}

pub type PublishTx = UnboundedSender<(Publish, Option<AckToken>)>;
pub type PublishRx = UnboundedReceiver<(Publish, Option<AckToken>)>;

#[derive(Default)]
pub struct IncomingPublishDispatcher {
    // TODO: name?
    filtered_txs: HashMap<TopicFilter, Vec<PublishTx>>,
    unfiltered_txs: Vec<PublishTx>,
}

impl IncomingPublishDispatcher {
    /// Create a new [`PublishRx`] that will receive dispatched [`Publish`]es that match the
    /// provided topic filter for as long as it is open.
    ///
    /// Multiple receivers can be created for the same topic filter, or with overlapping wildcard
    /// topic filters. Each receiver will receive all publishes that match the topic filter.
    ///
    /// # Arguments
    /// * `topic_filter` - The topic filter to match incoming publishes against
    pub fn create_filtered_receiver(&mut self, topic_filter: TopicFilter) -> PublishRx {
        // NOTE: We prune the filtered txs before registering any more to ensure that closed
        // txs (or entire vectors of txs) don't stick around in the HashMap indefinitely, making
        // dispatching more expensive. We also do cleanup during a dispatch, but since dispatching
        // only looks at the elements of vectors that are relevant to a given dispatch (i.e. lazy pruning),
        // we still need to do a full pruning when registering new tx filters.
        self.prune_filtered_txs();

        let (tx, rx) = unbounded_channel();
        match self.filtered_txs.get_mut(&topic_filter) {
            // If the topic filter is already in use, add to the associated vector
            Some(v) => {
                v.push(tx);
                // Otherwise, create a new vector and add
            }
            _ => {
                self.filtered_txs.insert(topic_filter, vec![tx]);
            }
        }

        rx
    }

    /// Create a new [`PublishRx`] that will receive all dispatched [`Publish`]es that do not
    /// match the topic filters for any other filtered [`PublishRx`]s, for as long as it
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

    pub fn dispatch_publish(&mut self, publish: &Publish, ack: ManualAcknowledgement) -> usize {
        // Use a PlenaryAck to distribute acknowledgement responsibility among all recipients.
        // RefCell is used here to assist with the mutable borrows in the dispatching loops,
        // as Option<&mut PlenaryAck> has issues with the borrow checker.
        let plenary_ack = match ack {
            ManualAcknowledgement::QoS0 => None,
            _ => Some(RefCell::new(PlenaryAck::new(ack))),
        };

        // Dispatch the publish to all relevant receivers
        let mut num_dispatches = 0;
        // First, dispatch to all filtered receivers that match the topic name
        num_dispatches += self.dispatch_filtered(publish, plenary_ack.as_ref());
        // Then, if no filters matched, dispatch to all unfiltered receivers (if present)
        if num_dispatches == 0 {
            num_dispatches += self.dispatch_unfiltered(publish, plenary_ack.as_ref());
        }

        log::debug!(
            "Dispatched publish on topic '{}' to {} receivers",
            publish.topic_name,
            num_dispatches
        );

        // Once all dispatches have been made, seal the PlenaryAck to allow acknowledgements to proceed.
        if let Some(cell) = plenary_ack {
            cell.borrow_mut().seal();
        }

        num_dispatches
    }

    /// Dispatch to filtered receivers
    fn dispatch_filtered(
        &mut self,
        publish: &Publish,
        plenary_ack: Option<&RefCell<PlenaryAck>>,
    ) -> usize {
        let mut num_dispatches = 0;
        let mut closed = vec![]; // (topic filter, position in vector)

        let filtered = self
            .filtered_txs
            .iter()
            .filter(|(topic_filter, _)| topic_filter.matches_topic_name(&publish.topic_name));
        for (topic_filter, v) in filtered {
            for (pos, tx) in v.iter().enumerate() {
                // Send the publish to the receiver, along with an ack token
                // If the receiver is closed, add it to the list of closed receivers to remove after iteration.
                // NOTE: Removing closed receivers must be done dynamically because the awaitable send allows
                // for a channel to be closed sometime during the execution of this loop. You cannot simply
                // use .prune() before the loop.
                let acktoken = plenary_ack.map(|cell| AckToken(cell.borrow_mut().create_member()));
                match tx.send((publish.clone(), acktoken)) {
                    Ok(()) => num_dispatches += 1,
                    Err(_) => closed.push((topic_filter.clone(), pos)),
                }
            }
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

        num_dispatches
    }

    /// Dispatch to unfiltered receivers
    fn dispatch_unfiltered(
        &mut self,
        publish: &Publish,
        plenary_ack: Option<&RefCell<PlenaryAck>>,
    ) -> usize {
        let mut num_dispatches = 0;
        let mut closed = vec![];

        for (pos, tx) in self.unfiltered_txs.iter().enumerate() {
            // Send the publish to the receiver, along with an ack token
            // If the receiver is closed, add it to the list of closed receivers to remove after iteration.
            // NOTE: Removing closed receivers must be done dynamically because the awaitable send allows
            // for a channel to be closed sometime during the execution of this loop
            let acktoken = plenary_ack.map(|cell| AckToken(cell.borrow_mut().create_member()));
            match tx.send((publish.clone(), acktoken)) {
                Ok(()) => num_dispatches += 1,
                Err(_) => closed.push(pos),
            }
        }

        // Remove any closed receivers.
        // NOTE: Do this in reverse order to avoid index issues.
        for pos in closed.iter().rev() {
            self.unfiltered_txs.remove(*pos);
        }

        num_dispatches
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
