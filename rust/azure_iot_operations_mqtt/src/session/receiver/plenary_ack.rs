// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

//! Plenary Acknowledgement synchronization tooling.
//! Allows for a single acknowledgement operation to block until approved by multiple threads/tasks
//! who each can then receive the result.

use std::future::Future;
use std::pin::Pin;
use std::sync::{Arc, Mutex};

use futures::future::{FutureExt, Shared};
use tokio::sync::Notify;

use crate::{
    error::{AckError, CompletionError},
    interface::CompletionToken,
};

// NOTE: It could be argued this module should not have the Ack semantics at all, and just let this
// be a generic synchronization tool.
// That *would* be a much cleaner design, but it's a bunch of extra work, and gives us basically
// nothing since this will probably not be used for anything other than Ack.
// Additionally, the complexities of the needing to propagate the CompletionTokens from acks to
// multiple members is fairly unique to the Ack use case.
// There probably is a way to deal with this in a more generic way, but it would require
// some significant work.

// NOTE: The PlenaryAckOpFuture type may be more complex than it needs to be. I suspect it could
// potentially be expressed more simply (and perhaps more performatively/flexibly) but in the
// interest of time, I'm sticking with what works.
type PlenaryAckOpFuture =
    Shared<Pin<Box<dyn Future<Output = Result<CompletionTokenFuture, AckError>> + Send + 'static>>>;
type CompletionTokenFuture =
    Shared<Pin<Box<dyn Future<Output = Result<(), CompletionError>> + Send>>>;

/// State of a plenary acking operation
#[derive(Default, Debug)]
struct PlenaryState {
    /// Number of required signals
    members: usize,
    /// Number of signals that have been reported
    signals: usize,
    /// Indicates plenary has commenced (i.e. the number of members cannot change)
    commenced: bool,
    /// Notify to trigger when all signals have been reported and the plenary has commenced
    approved: Arc<Notify>,
}

// NOTE: Some of these methods if used at the wrong time could lead to a broken state,
// however, this is fine because the other components of this module use it safely,
// and the `PlenaryState` is not exposed outside of this module.
impl PlenaryState {
    /// Indicate the number of members
    fn members(&mut self) -> usize {
        self.members
    }

    /// Increment the number of members
    fn add_member(&mut self) {
        self.members += 1;
    }

    /// Indicate a member has signalled
    fn signal(&mut self) {
        self.signals += 1;
        if self.signals == self.members && self.commenced {
            self.approved.notify_one();
        }
    }

    /// Indicate the plenary has commenced
    fn commence(&mut self) {
        self.commenced = true;
        if self.signals == self.members {
            self.approved.notify_one();
        }
    }

    /// Get the `Notify` that triggers when approval is given
    fn get_approved_notify(&self) -> Arc<Notify> {
        self.approved.clone()
    }
}

/// A member of a [`PlenaryAck`] that is required to issue an ack before the operation managed by the
/// [`PlenaryAck`] can complete
#[derive(Debug)]
pub struct PlenaryAckMember {
    state: Arc<Mutex<PlenaryState>>,
    plenary_op_f: PlenaryAckOpFuture,
    signaled: bool,
}

impl PlenaryAckMember {
    pub async fn ack(mut self) -> Result<CompletionToken, AckError> {
        // Signal the member has arrived
        self.state.lock().unwrap().signal();
        self.signaled = true;
        // Wait for the ack to be completed
        // NOTE: Cloning the future here isn't ideal, but is necessary under the current
        // implementation to allow for Drop, as the type does not support Copy.
        // I think this could probably be addressed my massaging the type of the future.
        // As mentioned elsewhere, I'm not sure this really needs to be as complex a type as it is.
        let ct_f = self.plenary_op_f.clone().await?;
        // The plenary_op_f returns another future that is the eventual result of the ack's
        // returned completion token. Use this shared future to create a new completion token
        // that triggers when the original completion token completes.
        Ok(CompletionToken(Box::new(ct_f)))
    }
}

impl Drop for PlenaryAckMember {
    fn drop(&mut self) {
        // If the member is dropped before signalling, signal it now
        if !self.signaled {
            self.state.lock().unwrap().signal();
            // We also have to spawn a task for the plenary future op here to ensure it will
            // execute. If there were multiple members, this doesn't matter, but if this was the
            // only member, the plenary future would never execute.
            tokio::task::spawn({
                let plenary_op_f = self.plenary_op_f.clone();
                async move {
                    match plenary_op_f.await {
                        Ok(_) => {}
                        Err(e) => {
                            log::error!("Plenary member ack on drop reported failure: {e:?}");
                        }
                    }
                }
            });
        }
    }
}

/// Represents an acknowledgement operation that will only trigger once all the
/// [`PlenaryAckMember`]s that are created by it have acked.
/// The result of that operation will be reported to the individual [`PlenaryAckMember`]s.
pub struct PlenaryAck {
    /// Shared state among members of the plenary
    state: Arc<Mutex<PlenaryState>>,
    /// The operation that will be triggered once all members have acked
    plenary_op_f: PlenaryAckOpFuture,
}

impl PlenaryAck {
    /// Create a new [`PlenaryAck`] with the given ack future that will be triggered once all members ack
    pub fn new(
        ack_future: impl Future<Output = Result<CompletionToken, AckError>> + Send + 'static,
    ) -> Self {
        let state = PlenaryState::default();
        let approved = state.get_approved_notify();

        let ack_op_f = async move {
            // Wait for the ack to be approved by all members signal
            approved.notified().await;
            // Trigger the ack operation
            let ct = ack_future.await?;
            // Return the completion token as a boxed shared future so that it can be propagated to
            // the multiple members that may be waiting for it.
            Ok(ct.boxed().shared())
        };

        Self {
            state: Arc::new(Mutex::new(state)),
            plenary_op_f: ack_op_f.boxed().shared(),
        }
    }

    /// Create a [`PlenaryAckMember`] whose ack will be required to trigger the completion of the
    /// [`PlenaryAck`]
    pub fn create_member(&self) -> PlenaryAckMember {
        // NOTE: no need to worry about the case where a member is added after the plenary has
        // commenced because .commence() consumes the PlenaryAck
        self.state.lock().unwrap().add_member();

        PlenaryAckMember {
            state: self.state.clone(),
            plenary_op_f: self.plenary_op_f.clone(),
            signaled: false,
        }
    }

    /// Indicate the ack operation should begin when all associated [`PlenaryAckMember`]s have acked.
    /// This consumes the [`PlenaryAck`].
    pub fn commence(self) {
        // NOTE: This is necessary for "early ack" scenarios. Consider that you may be creating
        // members in a row, and sending them to other threads/tasks - you can't stop them from
        // acking before you have finished spawning all the members you want to. There would be
        // a concern that, without the concept of "commencement" the plenary operation could
        // trigger early - say you had 2 of 3 members created, and both acked before the third
        // could be made, the number of members in the state would be 2, as would be the number
        // of signals, thus without some concept of "commencement", the plenary operation would
        // trigger. This is our bulwark against that problem - the plenary operation cannot
        // trigger until the plenary has commenced, i.e. "we are done making members"
        self.state.lock().unwrap().commence();

        // If there are no members, the plenary operation can trigger immediately, as the
        // plenary_op_f will not run until awaited, and if there are no members, there is nobody
        // to await it.
        if self.state.lock().unwrap().members() == 0 {
            tokio::task::spawn({
                let plenary_op_f = self.plenary_op_f.clone();
                async move {
                    match plenary_op_f.await {
                        Ok(_) => {}
                        Err(e) => {
                            log::error!("0 member plenary ack reported failure: {e:?}");
                        }
                    }
                }
            });
        }
    }
}

impl Drop for PlenaryAck {
    fn drop(&mut self) {
        // NOTE: This might seem a little strange, but it's necessary to not deadlock tasks that
        // are waiting on the PlenaryAckMembers to finish acking - something they can't do if the
        // PlenaryAck never invokes .commence().
        // Think about it like this - the point of .commence() in the first place is to allow for
        // the plenary operation to not trigger before all PlenaryAckMembers can be created
        // (see the note in the implementation of .commence()). If the PlenaryAck is dropped, that
        // is no longer a concern.
        self.state.lock().unwrap().commence();
    }
}

#[cfg(test)]
mod test {
    use super::*;
    use tokio::sync::oneshot;

    struct CompletionTokenTrigger(oneshot::Sender<()>);

    impl CompletionTokenTrigger {
        fn trigger(self) {
            self.0.send(()).unwrap();
        }
    }

    /// Return a completion token that will not return until triggered
    fn create_completion_token_and_trigger() -> (CompletionToken, CompletionTokenTrigger) {
        let (tx, rx) = oneshot::channel();
        let trigger = CompletionTokenTrigger(tx);
        let f = async {
            rx.await.unwrap();
            Ok(())
        };
        let ct = CompletionToken(Box::new(f.boxed()));
        (ct, trigger)
    }

    // Return a completion token that will immediately resolve
    fn create_completion_token() -> CompletionToken {
        let f = async { Ok(()) };
        CompletionToken(Box::new(f.boxed()))
    }

    #[tokio::test]
    async fn zero_member_commence() {
        let mock_ack_triggered = Arc::new(Mutex::new(false));
        let (mock_ack_ct, _) = create_completion_token_and_trigger();

        let mock_ack_f = {
            let mock_ack_triggered = mock_ack_triggered.clone();
            async move {
                *mock_ack_triggered.lock().unwrap() = true;
                Ok(mock_ack_ct)
            }
        };

        let plenary_ack = PlenaryAck::new(mock_ack_f);

        // Commence without creating any members
        plenary_ack.commence();

        // The mock ack was triggered
        tokio::time::sleep(tokio::time::Duration::from_secs(1)).await;
        assert!(*mock_ack_triggered.lock().unwrap());
    }

    #[tokio::test]
    async fn single_member_commence_before_ack() {
        let mock_ack_triggered = Arc::new(Mutex::new(false));
        let (mock_ack_ct, mock_ack_ct_trigger) = create_completion_token_and_trigger();

        let mock_ack_f = {
            let mock_ack_triggered = mock_ack_triggered.clone();
            async move {
                *mock_ack_triggered.lock().unwrap() = true;
                Ok(mock_ack_ct)
            }
        };

        let plenary_ack = PlenaryAck::new(mock_ack_f);
        let member1 = plenary_ack.create_member();

        // Commence, and then ack w/ plenary member
        plenary_ack.commence();
        let ct = member1.ack().await.unwrap();
        // The mock ack was triggered
        assert!(*mock_ack_triggered.lock().unwrap());
        // Returned completion token has not yet returned
        let jh = tokio::task::spawn(ct);
        tokio::time::sleep(tokio::time::Duration::from_secs(1)).await;
        assert!(!jh.is_finished());
        // After the mock completion token trigger, the completion token will return
        mock_ack_ct_trigger.trigger();
        jh.await.unwrap().unwrap();
    }

    #[tokio::test]
    async fn single_member_ack_before_commence() {
        let mock_ack_triggered = Arc::new(Mutex::new(false));
        let (mock_ack_ct, mock_ack_ct_trigger) = create_completion_token_and_trigger();

        let mock_ack_f = {
            let mock_ack_triggered = mock_ack_triggered.clone();
            async move {
                *mock_ack_triggered.lock().unwrap() = true;
                Ok(mock_ack_ct)
            }
        };

        let plenary_ack = PlenaryAck::new(mock_ack_f);
        let member1 = plenary_ack.create_member();

        let m1_ack = tokio::task::spawn(member1.ack());
        // Even after a second, the mock ack has not triggered, nor has the member ack returned
        tokio::time::sleep(tokio::time::Duration::from_secs(1)).await;
        assert!(!m1_ack.is_finished());
        assert!(!*mock_ack_triggered.lock().unwrap());
        // Commence, and then the mock ack will trigger, and the member ack will return
        plenary_ack.commence();
        let ct = m1_ack.await.unwrap().unwrap();
        assert!(*mock_ack_triggered.lock().unwrap());
        // Returned completion token has not yet completed
        let jh = tokio::task::spawn(ct);
        tokio::time::sleep(tokio::time::Duration::from_secs(1)).await;
        assert!(!jh.is_finished());
        // After the mock completion token trigger, the completion token will return
        mock_ack_ct_trigger.trigger();
        jh.await.unwrap().unwrap();
    }

    #[tokio::test]
    async fn multiple_member_commence_before_ack() {
        let mock_ack_triggered = Arc::new(Mutex::new(false));
        let (mock_ack_ct, mock_ack_ct_trigger) = create_completion_token_and_trigger();

        let mock_ack_f = {
            let mock_ack_triggered = mock_ack_triggered.clone();
            async move {
                *mock_ack_triggered.lock().unwrap() = true;
                Ok(mock_ack_ct)
            }
        };

        let plenary_ack = PlenaryAck::new(mock_ack_f);
        let member1 = plenary_ack.create_member();
        let member2 = plenary_ack.create_member();
        let member3 = plenary_ack.create_member();

        // Commence before any members ack
        plenary_ack.commence();
        // Mock ack has not triggered after the first plenary member acks, nor has the plenary ack task returned
        let m1_ack = tokio::task::spawn(member1.ack());
        tokio::time::sleep(tokio::time::Duration::from_secs(1)).await;
        assert!(!*mock_ack_triggered.lock().unwrap());
        assert!(!m1_ack.is_finished());
        // Mock ack has not triggered after the second plenary member acks, nor has the plenary ack task returned
        let m2_ack = tokio::task::spawn(member2.ack());
        tokio::time::sleep(tokio::time::Duration::from_secs(1)).await;
        assert!(!*mock_ack_triggered.lock().unwrap());
        assert!(!m2_ack.is_finished());
        // After the third plenary member acks, the mock ack will trigger, and all plenary ack tasks return
        let m3_ack = tokio::task::spawn(member3.ack());
        let ct1 = m1_ack.await.unwrap().unwrap();
        let ct2 = m2_ack.await.unwrap().unwrap();
        let ct3 = m3_ack.await.unwrap().unwrap();
        assert!(*mock_ack_triggered.lock().unwrap());
        // Returned completion tokens have not yet completed
        let jh1 = tokio::task::spawn(ct1);
        let jh2 = tokio::task::spawn(ct2);
        let jh3 = tokio::task::spawn(ct3);
        tokio::time::sleep(tokio::time::Duration::from_secs(1)).await;
        assert!(!jh1.is_finished());
        assert!(!jh2.is_finished());
        assert!(!jh3.is_finished());
        // After the mock completion token trigger, the completion tokens will return
        mock_ack_ct_trigger.trigger();
        jh1.await.unwrap().unwrap();
        jh2.await.unwrap().unwrap();
        jh3.await.unwrap().unwrap();
    }

    #[tokio::test]
    async fn multiple_member_ack_before_commence() {
        let mock_ack_triggered = Arc::new(Mutex::new(false));
        let (mock_ack_ct, mock_ack_ct_trigger) = create_completion_token_and_trigger();

        let mock_ack_f = {
            let mock_ack_triggered = mock_ack_triggered.clone();
            async move {
                *mock_ack_triggered.lock().unwrap() = true;
                Ok(mock_ack_ct)
            }
        };

        let plenary_ack = PlenaryAck::new(mock_ack_f);
        let member1 = plenary_ack.create_member();
        let member2 = plenary_ack.create_member();
        let member3 = plenary_ack.create_member();

        // Trigger all members to ack before commencing
        let m1_ack = tokio::task::spawn(member1.ack());
        let m2_ack = tokio::task::spawn(member2.ack());
        let m3_ack = tokio::task::spawn(member3.ack());
        // Even after a second, the mock ack has not triggered, nor have the member acks returned
        tokio::time::sleep(tokio::time::Duration::from_secs(1)).await;
        assert!(!*mock_ack_triggered.lock().unwrap());
        assert!(!m1_ack.is_finished());
        assert!(!m2_ack.is_finished());
        assert!(!m3_ack.is_finished());
        // Commence, and then the mock ack will trigger, with all member acks returning
        plenary_ack.commence();
        let ct1 = m1_ack.await.unwrap().unwrap();
        let ct2 = m2_ack.await.unwrap().unwrap();
        let ct3 = m3_ack.await.unwrap().unwrap();
        assert!(*mock_ack_triggered.lock().unwrap());
        // Returned completion tokens have not yet completed
        let jh1 = tokio::task::spawn(ct1);
        let jh2 = tokio::task::spawn(ct2);
        let jh3 = tokio::task::spawn(ct3);
        tokio::time::sleep(tokio::time::Duration::from_secs(1)).await;
        assert!(!jh1.is_finished());
        assert!(!jh2.is_finished());
        assert!(!jh3.is_finished());
        // After the mock completion token trigger, the completion tokens will return
        mock_ack_ct_trigger.trigger();
        jh1.await.unwrap().unwrap();
        jh2.await.unwrap().unwrap();
        jh3.await.unwrap().unwrap();
    }

    #[tokio::test]
    async fn multiple_member_mixed_ack_timing() {
        let mock_ack_triggered = Arc::new(Mutex::new(false));
        let (mock_ack_ct, mock_ack_ct_trigger) = create_completion_token_and_trigger();

        let mock_ack_f = {
            let mock_ack_triggered = mock_ack_triggered.clone();
            async move {
                *mock_ack_triggered.lock().unwrap() = true;
                Ok(mock_ack_ct)
            }
        };

        let plenary_ack = PlenaryAck::new(mock_ack_f);
        let member1 = plenary_ack.create_member();
        let member2 = plenary_ack.create_member();
        let member3 = plenary_ack.create_member();

        // Trigger two of the members before commencing
        let m1_ack = tokio::task::spawn(member1.ack());
        let m2_ack = tokio::task::spawn(member2.ack());
        // Even after a second, the mock ack has not triggered, nor have the member acks returned
        tokio::time::sleep(tokio::time::Duration::from_secs(1)).await;
        assert!(!*mock_ack_triggered.lock().unwrap());
        assert!(!m1_ack.is_finished());
        assert!(!m2_ack.is_finished());
        // Commence, and the mock ack will still not trigger, nor will the member acks return
        plenary_ack.commence();
        tokio::time::sleep(tokio::time::Duration::from_secs(1)).await;
        assert!(!*mock_ack_triggered.lock().unwrap());
        assert!(!m1_ack.is_finished());
        assert!(!m2_ack.is_finished());
        // Trigger the third member to ack, and then the mock ack will trigger, with all member acks returning
        let m3_ack = tokio::task::spawn(member3.ack());
        let ct1 = m1_ack.await.unwrap().unwrap();
        let ct2 = m2_ack.await.unwrap().unwrap();
        let ct3 = m3_ack.await.unwrap().unwrap();
        assert!(*mock_ack_triggered.lock().unwrap());
        // Returned completion tokens have not yet completed
        let jh1 = tokio::task::spawn(ct1);
        let jh2 = tokio::task::spawn(ct2);
        let jh3 = tokio::task::spawn(ct3);
        tokio::time::sleep(tokio::time::Duration::from_secs(1)).await;
        assert!(!jh1.is_finished());
        assert!(!jh2.is_finished());
        assert!(!jh3.is_finished());
        // After the mock completion token trigger, the completion tokens will return
        mock_ack_ct_trigger.trigger();
        jh1.await.unwrap().unwrap();
        jh2.await.unwrap().unwrap();
        jh3.await.unwrap().unwrap();
    }

    #[tokio::test]
    async fn member_drop_before_ack() {
        let mock_ack_triggered = Arc::new(Mutex::new(false));

        let mock_ack_f = {
            let mock_ack_triggered = mock_ack_triggered.clone();
            async move {
                *mock_ack_triggered.lock().unwrap() = true;
                Ok(create_completion_token())
            }
        };

        let plenary_ack = PlenaryAck::new(mock_ack_f);
        let member1 = plenary_ack.create_member();

        // Commence, and then drop the member before it acks
        plenary_ack.commence();
        drop(member1);
        // The mock ack was still triggered
        tokio::time::sleep(tokio::time::Duration::from_secs(1)).await;
        assert!(*mock_ack_triggered.lock().unwrap());
    }

    #[tokio::test]
    async fn member_drop_after_ack() {
        let mock_ack_trigger_count = Arc::new(Mutex::new(0));

        let mock_ack_f = {
            let mock_ack_trigger_count = mock_ack_trigger_count.clone();
            async move {
                *mock_ack_trigger_count.lock().unwrap() += 1;
                Ok(create_completion_token())
            }
        };

        let plenary_ack = PlenaryAck::new(mock_ack_f);
        let member1 = plenary_ack.create_member();

        // Commence, and then ack w/ plenary member
        plenary_ack.commence();
        member1.ack().await.unwrap();
        // Mock ack was triggered once
        tokio::time::sleep(tokio::time::Duration::from_secs(1)).await;
        assert_eq!(*mock_ack_trigger_count.lock().unwrap(), 1);

        // NOTE: This test is probably not super clear to read, so let me explain:
        // when calling .ack(), the member will be consumed, and then dropped when no longer used.
        // We can't even force the drop in this test, because it was consumed, so we don't have it.
        // The point here is, if the drop ALWAYS triggered the ack, then the mock ack would have
        // been triggered twice - once for the actual .ack() invocation, and then once again upon
        // drop. But it was only triggered once - from the explicit ack.
    }

    #[tokio::test]
    async fn ack_drop_before_commence_with_member() {
        let mock_ack_triggered = Arc::new(Mutex::new(false));

        let mock_ack_f = {
            let mock_ack_triggered = mock_ack_triggered.clone();
            async move {
                *mock_ack_triggered.lock().unwrap() = true;
                Ok(create_completion_token())
            }
        };

        let plenary_ack = PlenaryAck::new(mock_ack_f);
        let member1 = plenary_ack.create_member();

        // Drop the plenary ack
        drop(plenary_ack);
        // The mock ack has not yet triggered
        tokio::time::sleep(tokio::time::Duration::from_secs(1)).await;
        assert!(!*mock_ack_triggered.lock().unwrap());
        // Now ack with the member, and the mock ack will trigger
        member1.ack().await.unwrap();
        assert!(*mock_ack_triggered.lock().unwrap());
    }

    #[tokio::test]
    async fn ack_drop_before_commence_no_members() {
        let mock_ack_triggered = Arc::new(Mutex::new(false));

        let mock_ack_f = {
            let mock_ack_triggered = mock_ack_triggered.clone();
            async move {
                *mock_ack_triggered.lock().unwrap() = true;
                Ok(create_completion_token())
            }
        };

        let plenary_ack = PlenaryAck::new(mock_ack_f);

        // Drop the plenary ack
        drop(plenary_ack);
        // The mock ack will not trigger, because no members were ever created
        tokio::time::sleep(tokio::time::Duration::from_secs(1)).await;
        assert!(!*mock_ack_triggered.lock().unwrap());
    }
}
