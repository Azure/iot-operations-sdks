// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

use std::collections::HashMap;
use std::sync::{Arc, Mutex};

use thiserror::Error;
use tokio::sync::Notify;

#[derive(Error, Debug)]
pub enum RegisterError {
    #[error("publish already registered for pkid {0}")]
    AlreadyRegistered(u16),
}

#[derive(Error, Debug)] // Necessary?
pub enum LocalAckError {
    #[error("cannot ack a publish more times than required")]
    AckOverflow,
}

struct PendingAck {
    remaining_local_acks: usize,
    notify: Notify,
}


pub struct AckTracker {
    ack_table: Arc<Mutex<HashMap<u16, PendingAck>>>
}

impl AckTracker {
    // TODO: should this return some kind of notify?
    pub fn register_pending(&self, pkid: u16, local_acks_required: usize) -> Result<(), RegisterError>{
        // Ignore PKID 0, as it is reserved for QoS 0 messages
        if pkid == 0 {
            // TODO: Register error?
            return Ok(());
        }

        let mut ack_table = self.ack_table.lock().unwrap();

        if ack_table.contains_key(&pkid) {
            // NOTE: If a publish with the same PKID is currently tracked in the `PubTracker`, this
            // means that a duplicate has been received. While a client IS required to treat received
            // duplicates as a new application message in QoS 1 (MQTTv5 4.3.2), this is ONLY true if
            // the original message has been acknowledged and ownership transferred back to the server.
            // By definition of being in the `AckTracker`, the message has NOT been acknowledged, and thus,
            // can be discarded.
            return Err(RegisterError::AlreadyRegistered(pkid));
        }

        let entry = PendingAck {
            remaining_local_acks: local_acks_required,
            notify: Notify::new(),
        };
        ack_table.insert(pkid, entry);
        Ok(())
    }

    //TODO: Need to do the loop check?
    pub async fn local_ack(&self, pkid: u16) -> Result<(), LocalAckError> {
        if pkid == 0 {
            return Ok(())
        }

        let mut ack_table = self.ack_table.lock().unwrap();

        // If PKID registered, decrement the remaining number of acks required
        if let Some(entry) = ack_table.get_mut(&pkid) {
            if *entry.remaining_local_acks == 0 {
                return Err(LocalAckError::AckOverflow);
            }
            *entry.remaining_local_acks -= 1;
            if *entry.remaining_local_acks == 0 {
                // TODO: do we need to enable the notify at some point? Probably depends on implementation
                // TODO: notify one or all waiters?
                entry.notify.notify_one();
            }
            // TODO: if we aren't doing the looping check on the timing issue, there needs to be another type of error for that case
        }
        Ok(())
    }
}