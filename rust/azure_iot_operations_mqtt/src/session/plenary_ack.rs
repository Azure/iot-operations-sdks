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

use azure_mqtt::{
    client::{
        ManualAcknowledgement,
        token::completion::{CompletionError, PubAckCompletionToken},
    },
    error::ClientError,
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

    pub async fn seal(&mut self) -> () {
        // TODO: consume self?
        if !self.state.is_sealed() {
            self.state.seal(self.members).await;
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
            let state = self.state.clone();
            let members = self.members;
            tokio::task::spawn(async move {
                state.seal(members).await;
            });
        }
    }
}

pub struct PlenaryAckMember {
    state: Arc<InnerState>,
    signaled: bool,
}

impl PlenaryAckMember {
    pub async fn ack(mut self) -> Result<PlenaryAckCompletionToken, ClientError> {
        self.signaled = true;
        self.state.member_ack().await
    }
}

impl Drop for PlenaryAckMember {
    fn drop(&mut self) {
        if !self.signaled {
            // Member is being dropped without acking, so we need to ack on its behalf
            let state = self.state.clone();
            tokio::spawn(async move {
                state.member_ack().await;
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
    manual_ack: Mutex<Option<ManualAcknowledgement>>, // TODO: can this be combined with Result? they're related.
    /// Holds the result of the ManualAcknowledgement
    result: OnceCell<Result<PlenaryAckCompletionToken, ClientError>>, // TODO: Can this be made generic to support QoS2?
    /// Notify waiters when result has been set
    notify: Notify,
}

impl InnerState {
    fn is_sealed(&self) -> bool {
        self.sealed.lock().unwrap().is_some()
    }

    async fn seal(self: &Arc<Self>, total_members: usize) {
        // TODO: validate not called twice?
        self.sealed.lock().unwrap().replace(total_members);
        // We have to do a potential trigger here in order to handle two cases:
        // 1) there are zero members, and thus nobody to trigger `member_ack()`
        // 2) all members have already acked prior to the seal, and thus there will be no further
        //   calls to `member_ack()`
        self.trigger_if_ready().await;
    }

    /// Called by PlenaryAckMember to indicate a member has acked
    async fn member_ack(self: &Arc<Self>) -> Result<PlenaryAckCompletionToken, ClientError> {
        self.counter.fetch_add(1, Ordering::SeqCst);
        self.trigger_if_ready().await;

        // Early exit if result is ready
        if let Some(result) = self.result.get() {
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

    async fn trigger_if_ready(self: &Arc<Self>) -> () {
        // Check if sealed
        if let Some(total) = *self.sealed.lock().unwrap() {
            // Check if all members have acked
            if self.counter.load(Ordering::SeqCst) == total {
                // Check if result is not yet set
                if self.result.get().is_none() {
                    // Trigger manual ack
                    let c = self.clone();
                    let manual_ack = self.manual_ack.lock().unwrap().take().unwrap(); // TODO: guarantee? Is Option really the best option?
                    tokio::spawn(async move {
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
                        let result =
                            result.map(|ct| PlenaryAckCompletionToken { inner: ct.shared() });

                        // TODO: what is the return type?
                        // TODO: clean up
                        c.result.set(result).unwrap();
                    });
                }
            }
        }
    }
}

#[cfg(test)]
mod test {
    use super::*;

    // TODO: adapt and re-enable tests

    //     struct CompletionTokenTrigger(oneshot::Sender<()>);

    //     impl CompletionTokenTrigger {
    //         fn trigger(self) {
    //             self.0.send(()).unwrap();
    //         }
    //     }

    //     /// Return a completion token that will not return until triggered
    //     fn create_completion_token_and_trigger() -> (CompletionToken, CompletionTokenTrigger) {
    //         let (tx, rx) = oneshot::channel();
    //         let trigger = CompletionTokenTrigger(tx);
    //         let f = async {
    //             rx.await.unwrap();
    //             Ok(())
    //         };
    //         let ct = CompletionToken(Box::new(f.boxed()));
    //         (ct, trigger)
    //     }

    //     // Return a completion token that will immediately resolve
    //     fn create_completion_token() -> CompletionToken {
    //         let f = async { Ok(()) };
    //         CompletionToken(Box::new(f.boxed()))
    //     }

    //     #[tokio::test]
    //     async fn zero_member_commence() {
    //         let mock_ack_triggered = Arc::new(Mutex::new(false));
    //         let (mock_ack_ct, _) = create_completion_token_and_trigger();

    //         let mock_ack_f = {
    //             let mock_ack_triggered = mock_ack_triggered.clone();
    //             async move {
    //                 *mock_ack_triggered.lock().unwrap() = true;
    //                 Ok(mock_ack_ct)
    //             }
    //         };

    //         let plenary_ack = PlenaryAck::new(mock_ack_f);

    //         // Commence without creating any members
    //         plenary_ack.commence();

    //         // The mock ack was triggered
    //         tokio::time::sleep(tokio::time::Duration::from_secs(1)).await;
    //         assert!(*mock_ack_triggered.lock().unwrap());
    //     }

    //     #[tokio::test]
    //     async fn single_member_commence_before_ack() {
    //         let mock_ack_triggered = Arc::new(Mutex::new(false));
    //         let (mock_ack_ct, mock_ack_ct_trigger) = create_completion_token_and_trigger();

    //         let mock_ack_f = {
    //             let mock_ack_triggered = mock_ack_triggered.clone();
    //             async move {
    //                 *mock_ack_triggered.lock().unwrap() = true;
    //                 Ok(mock_ack_ct)
    //             }
    //         };

    //         let plenary_ack = PlenaryAck::new(mock_ack_f);
    //         let member1 = plenary_ack.create_member();

    //         // Commence, and then ack w/ plenary member
    //         plenary_ack.commence();
    //         let ct = member1.ack().await.unwrap();
    //         // The mock ack was triggered
    //         assert!(*mock_ack_triggered.lock().unwrap());
    //         // Returned completion token has not yet returned
    //         let jh = tokio::task::spawn(ct);
    //         tokio::time::sleep(tokio::time::Duration::from_secs(1)).await;
    //         assert!(!jh.is_finished());
    //         // After the mock completion token trigger, the completion token will return
    //         mock_ack_ct_trigger.trigger();
    //         jh.await.unwrap().unwrap();
    //     }

    //     #[tokio::test]
    //     async fn single_member_ack_before_commence() {
    //         let mock_ack_triggered = Arc::new(Mutex::new(false));
    //         let (mock_ack_ct, mock_ack_ct_trigger) = create_completion_token_and_trigger();

    //         let mock_ack_f = {
    //             let mock_ack_triggered = mock_ack_triggered.clone();
    //             async move {
    //                 *mock_ack_triggered.lock().unwrap() = true;
    //                 Ok(mock_ack_ct)
    //             }
    //         };

    //         let plenary_ack = PlenaryAck::new(mock_ack_f);
    //         let member1 = plenary_ack.create_member();

    //         let m1_ack = tokio::task::spawn(member1.ack());
    //         // Even after a second, the mock ack has not triggered, nor has the member ack returned
    //         tokio::time::sleep(tokio::time::Duration::from_secs(1)).await;
    //         assert!(!m1_ack.is_finished());
    //         assert!(!*mock_ack_triggered.lock().unwrap());
    //         // Commence, and then the mock ack will trigger, and the member ack will return
    //         plenary_ack.commence();
    //         let ct = m1_ack.await.unwrap().unwrap();
    //         assert!(*mock_ack_triggered.lock().unwrap());
    //         // Returned completion token has not yet completed
    //         let jh = tokio::task::spawn(ct);
    //         tokio::time::sleep(tokio::time::Duration::from_secs(1)).await;
    //         assert!(!jh.is_finished());
    //         // After the mock completion token trigger, the completion token will return
    //         mock_ack_ct_trigger.trigger();
    //         jh.await.unwrap().unwrap();
    //     }

    //     #[tokio::test]
    //     async fn multiple_member_commence_before_ack() {
    //         let mock_ack_triggered = Arc::new(Mutex::new(false));
    //         let (mock_ack_ct, mock_ack_ct_trigger) = create_completion_token_and_trigger();

    //         let mock_ack_f = {
    //             let mock_ack_triggered = mock_ack_triggered.clone();
    //             async move {
    //                 *mock_ack_triggered.lock().unwrap() = true;
    //                 Ok(mock_ack_ct)
    //             }
    //         };

    //         let plenary_ack = PlenaryAck::new(mock_ack_f);
    //         let member1 = plenary_ack.create_member();
    //         let member2 = plenary_ack.create_member();
    //         let member3 = plenary_ack.create_member();

    //         // Commence before any members ack
    //         plenary_ack.commence();
    //         // Mock ack has not triggered after the first plenary member acks, nor has the plenary ack task returned
    //         let m1_ack = tokio::task::spawn(member1.ack());
    //         tokio::time::sleep(tokio::time::Duration::from_secs(1)).await;
    //         assert!(!*mock_ack_triggered.lock().unwrap());
    //         assert!(!m1_ack.is_finished());
    //         // Mock ack has not triggered after the second plenary member acks, nor has the plenary ack task returned
    //         let m2_ack = tokio::task::spawn(member2.ack());
    //         tokio::time::sleep(tokio::time::Duration::from_secs(1)).await;
    //         assert!(!*mock_ack_triggered.lock().unwrap());
    //         assert!(!m2_ack.is_finished());
    //         // After the third plenary member acks, the mock ack will trigger, and all plenary ack tasks return
    //         let m3_ack = tokio::task::spawn(member3.ack());
    //         let ct1 = m1_ack.await.unwrap().unwrap();
    //         let ct2 = m2_ack.await.unwrap().unwrap();
    //         let ct3 = m3_ack.await.unwrap().unwrap();
    //         assert!(*mock_ack_triggered.lock().unwrap());
    //         // Returned completion tokens have not yet completed
    //         let jh1 = tokio::task::spawn(ct1);
    //         let jh2 = tokio::task::spawn(ct2);
    //         let jh3 = tokio::task::spawn(ct3);
    //         tokio::time::sleep(tokio::time::Duration::from_secs(1)).await;
    //         assert!(!jh1.is_finished());
    //         assert!(!jh2.is_finished());
    //         assert!(!jh3.is_finished());
    //         // After the mock completion token trigger, the completion tokens will return
    //         mock_ack_ct_trigger.trigger();
    //         jh1.await.unwrap().unwrap();
    //         jh2.await.unwrap().unwrap();
    //         jh3.await.unwrap().unwrap();
    //     }

    //     #[tokio::test]
    //     async fn multiple_member_ack_before_commence() {
    //         let mock_ack_triggered = Arc::new(Mutex::new(false));
    //         let (mock_ack_ct, mock_ack_ct_trigger) = create_completion_token_and_trigger();

    //         let mock_ack_f = {
    //             let mock_ack_triggered = mock_ack_triggered.clone();
    //             async move {
    //                 *mock_ack_triggered.lock().unwrap() = true;
    //                 Ok(mock_ack_ct)
    //             }
    //         };

    //         let plenary_ack = PlenaryAck::new(mock_ack_f);
    //         let member1 = plenary_ack.create_member();
    //         let member2 = plenary_ack.create_member();
    //         let member3 = plenary_ack.create_member();

    //         // Trigger all members to ack before commencing
    //         let m1_ack = tokio::task::spawn(member1.ack());
    //         let m2_ack = tokio::task::spawn(member2.ack());
    //         let m3_ack = tokio::task::spawn(member3.ack());
    //         // Even after a second, the mock ack has not triggered, nor have the member acks returned
    //         tokio::time::sleep(tokio::time::Duration::from_secs(1)).await;
    //         assert!(!*mock_ack_triggered.lock().unwrap());
    //         assert!(!m1_ack.is_finished());
    //         assert!(!m2_ack.is_finished());
    //         assert!(!m3_ack.is_finished());
    //         // Commence, and then the mock ack will trigger, with all member acks returning
    //         plenary_ack.commence();
    //         let ct1 = m1_ack.await.unwrap().unwrap();
    //         let ct2 = m2_ack.await.unwrap().unwrap();
    //         let ct3 = m3_ack.await.unwrap().unwrap();
    //         assert!(*mock_ack_triggered.lock().unwrap());
    //         // Returned completion tokens have not yet completed
    //         let jh1 = tokio::task::spawn(ct1);
    //         let jh2 = tokio::task::spawn(ct2);
    //         let jh3 = tokio::task::spawn(ct3);
    //         tokio::time::sleep(tokio::time::Duration::from_secs(1)).await;
    //         assert!(!jh1.is_finished());
    //         assert!(!jh2.is_finished());
    //         assert!(!jh3.is_finished());
    //         // After the mock completion token trigger, the completion tokens will return
    //         mock_ack_ct_trigger.trigger();
    //         jh1.await.unwrap().unwrap();
    //         jh2.await.unwrap().unwrap();
    //         jh3.await.unwrap().unwrap();
    //     }

    //     #[tokio::test]
    //     async fn multiple_member_mixed_ack_timing() {
    //         let mock_ack_triggered = Arc::new(Mutex::new(false));
    //         let (mock_ack_ct, mock_ack_ct_trigger) = create_completion_token_and_trigger();

    //         let mock_ack_f = {
    //             let mock_ack_triggered = mock_ack_triggered.clone();
    //             async move {
    //                 *mock_ack_triggered.lock().unwrap() = true;
    //                 Ok(mock_ack_ct)
    //             }
    //         };

    //         let plenary_ack = PlenaryAck::new(mock_ack_f);
    //         let member1 = plenary_ack.create_member();
    //         let member2 = plenary_ack.create_member();
    //         let member3 = plenary_ack.create_member();

    //         // Trigger two of the members before commencing
    //         let m1_ack = tokio::task::spawn(member1.ack());
    //         let m2_ack = tokio::task::spawn(member2.ack());
    //         // Even after a second, the mock ack has not triggered, nor have the member acks returned
    //         tokio::time::sleep(tokio::time::Duration::from_secs(1)).await;
    //         assert!(!*mock_ack_triggered.lock().unwrap());
    //         assert!(!m1_ack.is_finished());
    //         assert!(!m2_ack.is_finished());
    //         // Commence, and the mock ack will still not trigger, nor will the member acks return
    //         plenary_ack.commence();
    //         tokio::time::sleep(tokio::time::Duration::from_secs(1)).await;
    //         assert!(!*mock_ack_triggered.lock().unwrap());
    //         assert!(!m1_ack.is_finished());
    //         assert!(!m2_ack.is_finished());
    //         // Trigger the third member to ack, and then the mock ack will trigger, with all member acks returning
    //         let m3_ack = tokio::task::spawn(member3.ack());
    //         let ct1 = m1_ack.await.unwrap().unwrap();
    //         let ct2 = m2_ack.await.unwrap().unwrap();
    //         let ct3 = m3_ack.await.unwrap().unwrap();
    //         assert!(*mock_ack_triggered.lock().unwrap());
    //         // Returned completion tokens have not yet completed
    //         let jh1 = tokio::task::spawn(ct1);
    //         let jh2 = tokio::task::spawn(ct2);
    //         let jh3 = tokio::task::spawn(ct3);
    //         tokio::time::sleep(tokio::time::Duration::from_secs(1)).await;
    //         assert!(!jh1.is_finished());
    //         assert!(!jh2.is_finished());
    //         assert!(!jh3.is_finished());
    //         // After the mock completion token trigger, the completion tokens will return
    //         mock_ack_ct_trigger.trigger();
    //         jh1.await.unwrap().unwrap();
    //         jh2.await.unwrap().unwrap();
    //         jh3.await.unwrap().unwrap();
    //     }

    //     #[tokio::test]
    //     async fn member_drop_before_ack() {
    //         let mock_ack_triggered = Arc::new(Mutex::new(false));

    //         let mock_ack_f = {
    //             let mock_ack_triggered = mock_ack_triggered.clone();
    //             async move {
    //                 *mock_ack_triggered.lock().unwrap() = true;
    //                 Ok(create_completion_token())
    //             }
    //         };

    //         let plenary_ack = PlenaryAck::new(mock_ack_f);
    //         let member1 = plenary_ack.create_member();

    //         // Commence, and then drop the member before it acks
    //         plenary_ack.commence();
    //         drop(member1);
    //         // The mock ack was still triggered
    //         tokio::time::sleep(tokio::time::Duration::from_secs(1)).await;
    //         assert!(*mock_ack_triggered.lock().unwrap());
    //     }

    //     #[tokio::test]
    //     async fn member_drop_after_ack() {
    //         let mock_ack_trigger_count = Arc::new(Mutex::new(0));

    //         let mock_ack_f = {
    //             let mock_ack_trigger_count = mock_ack_trigger_count.clone();
    //             async move {
    //                 *mock_ack_trigger_count.lock().unwrap() += 1;
    //                 Ok(create_completion_token())
    //             }
    //         };

    //         let plenary_ack = PlenaryAck::new(mock_ack_f);
    //         let member1 = plenary_ack.create_member();

    //         // Commence, and then ack w/ plenary member
    //         plenary_ack.commence();
    //         member1.ack().await.unwrap();
    //         // Mock ack was triggered once
    //         tokio::time::sleep(tokio::time::Duration::from_secs(1)).await;
    //         assert_eq!(*mock_ack_trigger_count.lock().unwrap(), 1);

    //         // NOTE: This test is probably not super clear to read, so let me explain:
    //         // when calling .ack(), the member will be consumed, and then dropped when no longer used.
    //         // We can't even force the drop in this test, because it was consumed, so we don't have it.
    //         // The point here is, if the drop ALWAYS triggered the ack, then the mock ack would have
    //         // been triggered twice - once for the actual .ack() invocation, and then once again upon
    //         // drop. But it was only triggered once - from the explicit ack.
    //     }

    //     #[tokio::test]
    //     async fn ack_drop_before_commence_with_member() {
    //         let mock_ack_triggered = Arc::new(Mutex::new(false));

    //         let mock_ack_f = {
    //             let mock_ack_triggered = mock_ack_triggered.clone();
    //             async move {
    //                 *mock_ack_triggered.lock().unwrap() = true;
    //                 Ok(create_completion_token())
    //             }
    //         };

    //         let plenary_ack = PlenaryAck::new(mock_ack_f);
    //         let member1 = plenary_ack.create_member();

    //         // Drop the plenary ack
    //         drop(plenary_ack);
    //         // The mock ack has not yet triggered
    //         tokio::time::sleep(tokio::time::Duration::from_secs(1)).await;
    //         assert!(!*mock_ack_triggered.lock().unwrap());
    //         // Now ack with the member, and the mock ack will trigger
    //         member1.ack().await.unwrap();
    //         assert!(*mock_ack_triggered.lock().unwrap());
    //     }

    //     #[tokio::test]
    //     async fn ack_drop_before_commence_no_members() {
    //         let mock_ack_triggered = Arc::new(Mutex::new(false));

    //         let mock_ack_f = {
    //             let mock_ack_triggered = mock_ack_triggered.clone();
    //             async move {
    //                 *mock_ack_triggered.lock().unwrap() = true;
    //                 Ok(create_completion_token())
    //             }
    //         };

    //         let plenary_ack = PlenaryAck::new(mock_ack_f);

    //         // Drop the plenary ack
    //         drop(plenary_ack);
    //         // The mock ack will not trigger, because no members were ever created
    //         tokio::time::sleep(tokio::time::Duration::from_secs(1)).await;
    //         assert!(!*mock_ack_triggered.lock().unwrap());
    //     }
}
