// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

use std::collections::{VecDeque, HashMap};
use std::sync::Arc;
//use std::sync::{Arc, Mutex};

use thiserror::Error;
use tokio::sync::{Notify, Mutex};

use crate::control_packet::Publish;
use crate::error::AckError;
use crate::interface::MqttAck;

#[derive(Error, Debug)]
pub enum RegisterError {
    #[error("publish already registered for pkid {0}")]
    AlreadyRegistered(u16),
}

#[derive(Error, Debug)]
pub enum OrderedAckError {
    #[error(transparent)]
    AckError(#[from] AckError),
    #[error("Publish being acked was not previously registered")]
    Unregistered
}

/// Wrapper that ensures correct ordering of
#[derive(Clone)]
pub struct OrderedAcker<A>
where
    A: MqttAck
{
    acker: A,
    ack_queue: Arc<Mutex<VecDeque<u16>>>,       //Notify in here?
    //ack_queue: Arc<Mutex<VecDeque<u16, Notify>>>,   // Notify needs to be Arc?
    notify_map: Arc<Mutex<HashMap<u16, Notify>>>,
    //notify: Arc<Notify>,

}

impl <A> OrderedAcker<A>
where
    A: MqttAck
{
    pub fn new(acker: A) -> Self {
        Self {
            acker,
            ack_queue: Arc::new(Mutex::new(VecDeque::new())),
            notify_map: Arc::new(Mutex::new(HashMap::new())),       //RwLock?
        }
    }

    // TODO: tokio mutex here creates an issue - register ends up being async when it shouldn't be.
    // This opens up the possibility for 

    pub async fn register(&self, publish: &Publish) -> Result<(), RegisterError> {        
        // Ignore PKID 0, as it is reserved for QoS 0 messages
        if publish.pkid == 0 {
            return Ok(());
        }
        // Acquire locks
        let mut ack_queue = self.ack_queue.lock().await;    //blocking lock?
        let mut notify_map = self.notify_map.lock().await;
        // Check if the pkid is already in the queue
        if ack_queue.contains(&publish.pkid) {
            return Err(RegisterError::AlreadyRegistered(publish.pkid));
        }
        // Create a Notify struct for notifying when it is this PKID's turn to be acked
        let notify = Notify::new();
        // If there are no entries in the queue, store a permit in the Notify to indicate
        // that it is the next permitted ack.
        if ack_queue.len() == 0 {
            notify.notify_one();
        }
        // Store the Notify in the map
        notify_map.insert(publish.pkid, notify);
        // Add pkid to the queue
        ack_queue.push_back(publish.pkid);

        Ok(())
    }

    pub async fn ordered_ack(&self, publish: Publish) -> Result<(), OrderedAckError> {  // return CT?
        // No need to ack QoS0 publishes. Skip.
        if publish.pkid == 0 {
            return Ok(());
        }
        // Validate registration
        if !self.ack_queue.lock().await.contains(&publish.pkid) {
            // NOTE: It's not terribly efficient to check this, but it does allow for more accurate
            // reporting. Consider optimizing later.
            return Err(OrderedAckError::Unregistered);
        }

        // Get the notify for ack permission, removing it from the notify map
        if let Some(notify) = self.notify_map.lock().await.remove(&publish.pkid) {
            // Wait for ack permission
            notify.notified().await;
            let ack_result = self.acker.ack(&publish).await;

            return Ok(())
        }
        else {
            return Err(OrderedAckError::AckError(AckError::AlreadyAcked));
        }

        //Ok(())
    }

    // pub async fn ordered_ack(&self, publish: &Publish) -> Result<(), OrderedAckError> { // return CT?
    //     if publish.pkid == 0 {
    //         // No need to ack QoS0 publishes
    //         return Ok(());
    //     }


    //     // loop {
    //     //     {
    //     //         // New scope to hold lock
    //     //         let mut ack_queue = self.ack_queue.lock().await;
    //     //         match ack_queue.front() {
    //     //             Some((pkid, notify)) => {
    //     //                 if *pkid == publish.pkid {
    //     //                     ack_queue.pop_front();
    //     //                     let ack_result = self.acker.ack(publish).await;
                            


    //     //                     return ack_result.map_err(|e| e.into());
    //     //                 }
    //     //             }
    //     //             // Some(pkid) => {
    //     //             //     // If the pkid of the publish matches the next pkid in the queue, send the ack()
    //     //             //     if *pkid == publish.pkid {
    //     //             //         ack_queue.pop_front();
    //     //             //         let ack_result = self.acker.ack(publish).await;
    //     //             //         return ack_result.map_err(|e| e.into());
    //     //             //         // NOTE: if the ack fails, that means we have become disconnected from the Session.
    //     //             //     }
    //     //             //     // Otherwise continue below, waiting upon a notification
    //     //             // }
    //     //             // None => {
    //     //             //     // No items in queue
    //     //             //     return Err(OrderedAckError::Unregistered);
    //     //             // }
    //     //         }
    //     //     }
    //     //     self.notify.notified().await;
    //     // }
    // }

}

// NOTE: tokio Mutex must be used, because we need to hold the lock past the internal acker .ack()
// call. This is to ensure that the order of acks popped from the queue is the same that actually
// go into the internal client - no other solution would provide this guarantee.