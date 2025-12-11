// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

//! Plenary Acknowledgement synchronization tooling.
//! Allows for a single acknowledgement operation to be gated on approval of multiple "members"
//! who each can then receive the result of the acknowledgement.

use std::future::Future;
use std::pin::Pin;
use std::sync::{
    Arc, Mutex,
    atomic::{AtomicUsize, Ordering},
};
use std::task::{Context, Poll};

use crate::azure_mqtt::{
    client::{
        ManualAcknowledgement,
        token::completion::{CompletionError, PubAckCompletionToken},
    },
    error::DetachedError,
    packet::PubAckProperties,
};
use futures::future::{FutureExt, Shared};
use tokio::sync::{Notify, OnceCell};

pub struct PlenaryAck {
    state: Arc<InnerState>,
    members: usize,
}

impl PlenaryAck {
    pub fn new(manual_ack: ManualAcknowledgement) -> Self {
        Self {
            state: Arc::new(InnerState {
                counter: AtomicUsize::new(0),
                sealed: Mutex::new(None),
                manual_ack: Mutex::new(Some(manual_ack)),
                result: OnceCell::new(),
                notify: Notify::new(),
            }),
            members: 0,
        }
    }

    pub fn seal(&mut self) {
        // TODO: consume self?
        if !self.state.is_sealed() {
            self.state.seal(self.members);
        }
    }

    pub fn create_member(&mut self) -> PlenaryAckMember {
        self.members += 1;
        PlenaryAckMember {
            state: self.state.clone(),
            signaled: false,
        }
    }
}

impl Drop for PlenaryAck {
    fn drop(&mut self) {
        if !self.state.is_sealed() {
            log::debug!("PlenaryAck dropped without being sealed, sealing now");
            let state = self.state.clone();
            let members = self.members;
            state.seal(members);
        }
    }
}

pub struct PlenaryAckMember {
    state: Arc<InnerState>,
    signaled: bool,
}

impl PlenaryAckMember {
    pub async fn ack(mut self) -> Result<PlenaryAckCompletionToken, DetachedError> {
        self.signaled = true;
        self.state.member_ack().await
    }
}

impl Drop for PlenaryAckMember {
    fn drop(&mut self) {
        if !self.signaled {
            log::debug!("PlenaryAckMember being dropped without acking, issuing member ack now");
            let state = self.state.clone();
            tokio::spawn(async move {
                let _ = state.member_ack().await;
                // TODO: handle errors with log
            });
        }
    }
}

#[derive(Clone, Debug)]
pub struct PlenaryAckCompletionToken {
    inner: Shared<PubAckCompletionToken>,
}

impl Future for PlenaryAckCompletionToken {
    type Output = Result<(), CompletionError>;

    fn poll(self: Pin<&mut Self>, cx: &mut Context<'_>) -> Poll<Self::Output> {
        Pin::new(&mut self.get_mut().inner).poll(cx)
    }
}

struct InnerState {
    /// Number of members that have acked
    counter: AtomicUsize,
    /// None => not sealed yet, Some(n) => sealed with n members
    sealed: Mutex<Option<usize>>,
    /// Will be used to trigger acknowledgement when all members have acked
    manual_ack: Mutex<Option<ManualAcknowledgement>>,
    /// Holds the result of the `ManualAcknowledgement`
    result: OnceCell<Result<PlenaryAckCompletionToken, DetachedError>>,
    /// Notify waiters when result has been set
    notify: Notify,
}

impl InnerState {
    fn is_sealed(&self) -> bool {
        self.sealed.lock().unwrap().is_some()
    }

    fn seal(self: &Arc<Self>, total_members: usize) {
        log::debug!("Sealing PlenaryAck with {total_members} members");
        // TODO: validate not called twice?
        self.sealed.lock().unwrap().replace(total_members);
        // We have to do a potential trigger here in order to handle two cases:
        // 1) there are zero members, and thus nobody to trigger `member_ack()`
        // 2) all members have already acked prior to the seal, and thus there will be no further
        //   calls to `member_ack()`
        tokio::task::spawn({
            let c = self.clone();
            async move {
                c.trigger_if_ready().await;
            }
        });
    }

    /// Called by `PlenaryAckMember` to indicate a member has acked
    async fn member_ack(self: &Arc<Self>) -> Result<PlenaryAckCompletionToken, DetachedError> {
        self.counter.fetch_add(1, Ordering::SeqCst);
        self.trigger_if_ready().await;

        // Early exit if result is ready
        if let Some(result) = self.result.get() {
            log::debug!("Member ack: result already ready, returning");
            return result.clone();
        }

        // Wait for result to be ready and return
        loop {
            self.notify.notified().await;
            if let Some(result) = self.result.get() {
                return result.clone();
            }
            // spurious wake -> loop again (this shouldn't happen though)
        }
    }

    async fn trigger_if_ready(self: &Arc<Self>) {
        // Check if sealed
        let sealed = *self.sealed.lock().unwrap();
        if let Some(total) = sealed {
            // Check if all members have acked
            if self.counter.load(Ordering::SeqCst) == total {
                // Check if result is not yet set
                if self.result.get().is_none() {
                    // Trigger manual ack
                    let manual_ack = self.manual_ack.lock().unwrap().take().unwrap(); // TODO: guarantee? Is Option really the best option?
                    let result = match manual_ack {
                        ManualAcknowledgement::QoS0 => {
                            unimplemented!("no ack on qos 0") // TODO: better error
                        }
                        ManualAcknowledgement::QoS1(token) => {
                            token.accept(PubAckProperties::default()).await
                        }
                        ManualAcknowledgement::QoS2(_token) => {
                            unimplemented!("QoS2 not yet supported")
                        }
                    };

                    // Map the token result to a PlenaryAckCompletionToken
                    let result = result.map(|ct| PlenaryAckCompletionToken { inner: ct.shared() });

                    // TODO: what is the return type?
                    // TODO: clean up
                    self.result.set(result).unwrap();
                    self.notify.notify_waiters();
                }
            }
        }
    }
}
