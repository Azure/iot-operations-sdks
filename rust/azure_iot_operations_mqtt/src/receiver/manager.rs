use std::collections::HashMap;
use std::string::FromUtf8Error;
use std::future::Future;
use std::pin::Pin;
use std::sync::Arc;

use futures::future::{Shared, FutureExt, BoxFuture};
use rumqttc::tokio_rustls::rustls::crypto::hash::Output;
use thiserror::Error;
use tokio::sync::mpsc::{error::SendError, unbounded_channel, UnboundedSender, UnboundedReceiver};

use crate::control_packet::{Publish, QoS};
use crate::error::AckError;
use crate::interface::{CompletionToken, MqttAck};
use crate::topic::{TopicFilter, TopicName, TopicParseError};
use crate::receiver::ack_tracker::AckTracker;
use crate::receiver::ordered_acker::OrderedAcker;

// NOTE: These errors should almost never happen.
// - Closed receivers can only occur due to race condition since receivers are checked before dispatch.
// - Invalid publishes should not happen at all, since we shouldn't be receiving Publishes from the broker
//   that are invalid.
#[derive(Error, Debug)]
pub enum DispatchError {
    #[error("receiver closed")]
    ClosedReceiver(#[from] SendError<(Publish, Option<AckToken>)>),
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


// NOTE: Do NOT clone this. This is user facing, it cannot be clonable!
// What if there was an InnerAckToken that was though?
pub struct AckToken {
    tracker: Arc<AckTracker>,
    shared: Shared<Pin<Box<dyn std::future::Future<Output = Result<(), AckError>> + Send>>>,
}

impl AckToken {
    pub async fn ack(&self) -> Result<CompletionToken, AckError> {
        // self.notify.notify_one();
        // self.result = Some(CompletionToken::new());
        Ok(CompletionToken(Box::new(async { Ok(()) })))
    }
}

// TODO: semantics change to ReceiverManager?
pub struct InboundPublishManager<A>
where A: MqttAck + Clone + Send + 'static,
{
    acker: OrderedAcker<A>,
    ack_tracker: Arc<AckTracker>,   // should the mutexes be on the outside of this?
    filtered_txs: HashMap<TopicFilter, Vec<UnboundedSender<(Publish, Option<AckToken>)>>>,
}

impl <A> InboundPublishManager<A>
where A: MqttAck + Clone + Send + 'static,
{
    pub async fn dispatch_publish(&mut self, publish: Publish) -> Result<(), DispatchError>{
        //let mut num_dispatches = 0;
        let mut closed = vec![]; // (Topic filter, position in vector)

        let topic_name = extract_publish_topic_name(&publish)?;

        // Register the publish as received in the OrderedAcker so that it can later be acked in the
        // correct order.
        // TODO: can this fail? Is this an .expect() situation? Or should some of that Session logic come in here?
        // (I think it's the latter)
        self.acker.register(&publish).await.unwrap();

        // Determine which receivers to dispatch to
        let filtered = self
            .filtered_txs
            .iter()
            .filter(|(topic_filter, _)| topic_filter.matches_topic_name(&topic_name));

        // AHA! The reason why looking up the number of dispatches beforehand is bad is runtime complexity.
        // There can be many matching filters...
        // Need some kind of "get matching filter" helper
        // ..... except that requires additional allocations...

        // // Register the incoming publish for acking
        // match self.filtered_txs.get(&topic_name) {
        //     Some(v) => {
        //         // filtered dispatch
        //     }
        //     None => {
        //         // unfiltered dispatch
        //     }
        // }

        self.ack_tracker.register_pending(publish.pkid(), num_dispatches);

        // First, get the number of receivers to dispatch to
        // - Is this necessary?
        // - It would be nice to avoid the looping in the ack tracker, yes
        // - Does it get us anything else?
        // - It doesn't really help with AckTokens afaik


        // // TODO: ack future

        // // let mut ack_token = None;
        // if publish.qos != QoS::AtMostOnce {
        //     let publish_c = publish.clone();
        //     let acker_c = self.acker.clone();
        //     let ack_f = async move {
        //         // TODO: wait for notification that all acks are complete / msg is ready for ack
        //         acker_c.ordered_ack(&publish_c).await
        //     };

        //     let ack_token = Some(AckToken {
        //         tracker: self.ack_tracker.clone(),
        //         shared: ack_f.boxed().shared()
        //     });
        // }


        Ok(())





        // Check registered filters
        // prepare the ack future
        // dispatch n times (with shared futures for ack)
    }

    // TODO: this should make a Receiver?

    /// Register a topic filter for dispatching.
    ///
    /// Returns a receiver that will receive incoming publishes published to the topic filter.
    /// Multiple receivers can be registered for the same topic filter.
    /// If a receiver is closed or dropped, it will be removed from the list of receivers.
    /// If all receivers for a topic filter are closed, the topic filter will be unregistered.
    ///
    /// # Arguments
    /// * `topic_filter` - The [`TopicFilter`] to listen for incoming publishes on.
    pub fn register_filter(&mut self, topic_filter: &TopicFilter) -> UnboundedReceiver<(Publish, Option<AckToken>)> {
        self.prune_txs();

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

    /// Remove any transmitters for closed filter receivers.
    ///
    /// Call this before any register
    /// Note that the runtime is O(c * m) and not O(n * m) as it may seem.
    /// (c = capacity, m = max number of duplicate listeners on a filter, n = number of filters).
    fn prune_txs(&mut self) {
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
