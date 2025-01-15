// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

use std::collections::VecDeque;
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
    ack_queue: Arc<Mutex<VecDeque<u16>>>,   //u16, Notify?
    notify: Arc<Notify>,

}

impl <A> OrderedAcker<A>
where
    A: MqttAck
{
    pub fn new(acker: A) -> Self {
        Self {
            acker,
            ack_queue: Arc::new(Mutex::new(VecDeque::new())),
            notify: Arc::new(Notify::new()),
        }
    }

    pub async fn register(&self, publish: &Publish) -> Result<(), RegisterError> {        
        // Ignore PKID 0, as it is reserved for QoS 0 messages
        if publish.pkid == 0 {
            return Ok(());
        }
        let mut ack_queue = self.ack_queue.lock().await;
        // Check if the pkid is already in the queue
        if ack_queue.contains(&publish.pkid) {
            return Err(RegisterError::AlreadyRegistered(publish.pkid));
        }
        // Add publish to the ordered acking queue
        ack_queue.push_back(publish.pkid);
        Ok(())
    }

    pub async fn ordered_ack(&self, publish: &Publish) -> Result<(), OrderedAckError> { // return CT?
        if publish.pkid == 0 {
            return self.acker.ack(publish).await.map_err(|e| e.into());
        }
        loop {
            {
                // New scope to hold lock
                let mut ack_queue = self.ack_queue.lock().await;
                match ack_queue.front() {
                    Some(pkid) => {
                        // If the pkid of the publish matches the next pkid in the queue, send the ack()
                        if *pkid == publish.pkid {
                            ack_queue.pop_front();
                            let ack_result = self.acker.ack(publish).await;
                            self.notify.notify_waiters();
                            return ack_result.map_err(|e| e.into());
                            // NOTE: if the ack fails, that means we have become disconnected from the Session.
                        }
                        // Otherwise continue below, waiting upon a notification
                    }
                    None => {
                        // No items in queue
                        return Err(OrderedAckError::Unregistered);
                    }
                }
            }
            self.notify.notified().await;
        }
    }

}

// NOTE: tokio Mutex must be used, because we need to hold the lock past the internal acker .ack()
// call. This is to ensure that the order of acks popped from the queue is the same that actually
// go into the internal client - no other solution would provide this guarantee.