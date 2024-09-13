// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

//! Internal implementation of the [`Session`] type.

use std::collections::HashSet;
use std::str::FromStr;
use std::sync::{Arc, Mutex};
use std::time::{Duration, SystemTime, UNIX_EPOCH};

use async_trait::async_trait;
use base64::{engine::general_purpose::STANDARD_NO_PAD, Engine};
use bytes::Bytes;
use tokio::sync::mpsc::Receiver;
use tokio_util::sync::CancellationToken;

use crate::control_packet::{
    AuthProperties, Publish, PublishProperties, QoS, SubscribeProperties, UnsubscribeProperties,
};
use crate::error::{ClientError, ConnectionError, StateError};
use crate::interface::{
    InternalClient, MqttAck, MqttDisconnect, MqttEventLoop, MqttProvider, MqttPubReceiver,
    MqttPubSub,
};
use crate::session::dispatcher::IncomingPublishDispatcher;
use crate::session::pub_tracker::{PubTracker, RegisterError};
use crate::session::reconnect_policy::ReconnectPolicy;
use crate::session::{SessionError, SessionErrorKind};
use crate::topic::{TopicFilter, TopicParseError};
use crate::{CompletionToken, Event, Incoming, Outgoing};

/// Enum used to track the reason why client-side disconnect occurs
#[derive(PartialEq, Eq)]
enum DesireDisconnect {
    /// Indicates no disconnect is desired
    No,
    /// Indicates the user has requested a disconnect
    User,
    /// Indicates the session logic has requested a disconnect
    Internal,
}

/// Client that manages connections over a single MQTT session.
///
/// Use this centrally in an application to control the session and to create
/// any necessary [`SessionPubSub`], [`SessionPubReceiver`] and [`SessionExitHandle`].
pub struct Session<C, EL>
where
    C: InternalClient + Clone + Send + Sync + 'static,
    EL: MqttEventLoop,
{
    /// Underlying MQTT client
    client: C,
    /// Underlying MQTT event loop
    event_loop: EL,
    /// Client ID of the underlying rumqttc client
    client_id: String,
    /// File path to the SAT token
    sat_auth_file: Option<String>,
    /// Dispatcher for incoming publishes
    incoming_pub_dispatcher: IncomingPublishDispatcher,
    /// Tracker for unacked incoming publishes
    unacked_pubs: Arc<PubTracker>,
    /// Reconnect policy
    reconnect_policy: Box<dyn ReconnectPolicy>,
    /// Indicates if Session was previously run. Temporary until re-use is supported.
    previously_run: bool,
}

impl<C, EL> Session<C, EL>
where
    C: InternalClient + Clone + Send + Sync + 'static,
    EL: MqttEventLoop,
{
    // TODO: get client id out of here
    // TODO: can we eliminate need for box on input?
    /// ----API NOT STABLE, INTERNAL USE ONLY FOR NOW----
    pub fn new_from_injection(
        client: C,
        event_loop: EL,
        reconnect_policy: Box<dyn ReconnectPolicy>,
        client_id: String,
        sat_auth_file: Option<String>,
        capacity: usize,
    ) -> Self {
        // NOTE: drop the unfiltered message receiver from the dispatcher here in order to force non-filtered
        // messages to fail to be dispatched. The .run() method will respond to this failure by acking.
        // This lets us retain correct functionality while waiting for a more elegant solution with ordered ack.
        let (incoming_pub_dispatcher, _) = IncomingPublishDispatcher::new(capacity);
        Self {
            client,
            event_loop,
            client_id,
            sat_auth_file,
            incoming_pub_dispatcher,
            unacked_pubs: Arc::new(PubTracker::new()),
            reconnect_policy,
            previously_run: false,
        }
    }

    /// Return an instance of [`SessionExitHandle`] that can be used to end
    /// this [`Session`]
    pub fn get_session_exit_handle(&self) -> SessionExitHandle<C> {
        SessionExitHandle {
            disconnector: self.client.clone(),
        }
    }

    /// Begin running the [`Session`].
    ///
    /// Blocks until either a session exit or a fatal connection error is encountered.
    ///
    /// # Errors
    /// Returns a [`SessionError`] if the session encounters a fatal error and ends.
    pub async fn run(&mut self) -> Result<(), SessionError> {
        // TODO: This is a temporary solution to prevent re-use of the session.
        // Re-use should be available in the future.
        if self.previously_run {
            log::error!("Session re-use is not currently supported. Ending session.");
            return Err(std::convert::Into::into(SessionErrorKind::InvalidState(
                "Session re-use is not currently supported".to_string(),
            )));
        }
        self.previously_run = true;

        // Reset the pending pub tracker so as not to carry over any state from previous sessions.
        // NOTE: Dispatcher does not need to be reset, as it prunes itself as necessary.
        // TODO: Find a solution for dealing with the case were a receiver may ack a publish after
        // session has been reset. This is out of scope right now (we don't currently support Session re-use)
        // but it would be nice to, and the library design implies that we can.
        // TODO: Perhaps instead of clearing, the entries in the tracker should update to indicate that any
        // acks to them from the surviving listeners should be ignored?
        self.unacked_pubs.reset();
        // NOTE: Another necessary change here to support re-use is to handle clean-start. It gets changed from its
        // original setting during the operation of .run(), and thus, the original setting is lost.

        // Background tasks
        let cancel_token = CancellationToken::new();
        tokio::spawn({
            let cancel_token = cancel_token.clone();
            let client = self.client.clone();
            let unacked_pubs = self.unacked_pubs.clone();
            run_background(
                client,
                unacked_pubs,
                self.sat_auth_file.clone(),
                cancel_token,
            )
        });

        // Indicates whether a disconnect is desired, and why
        let mut desire_disconnect = DesireDisconnect::No;
        // Indicates whether the session has been previously connected
        let mut prev_connected = false;
        // Number of previous reconnect attempts
        let mut prev_reconnect_attempts = 0;
        // Return value for the session indicating reason for exit
        let mut result = Ok(());

        // Handle events
        loop {
            let r = self.event_loop.poll().await;
            match r {
                Ok(Event::Incoming(Incoming::ConnAck(connack))) => {
                    // Reset the counter on reconnect attempts
                    prev_reconnect_attempts = 0;
                    log::debug!("Incoming CONNACK: {connack:?}");

                    // If the session is not present after a reconnect, end the session.
                    if prev_connected && !connack.session_present {
                        log::error!(
                            "Session state not present on broker after reconnect. Ending session."
                        );
                        result = Err(SessionErrorKind::SessionLost);
                        desire_disconnect = DesireDisconnect::Internal;
                        self.trigger_session_exit().await;
                    }
                    // Otherwise, connection was successful
                    else {
                        prev_connected = true;
                        // Set clean start to false for subsequent connections
                        self.event_loop.set_clean_start(false);
                        log::info!("Connected!");
                    }
                }
                Ok(Event::Incoming(Incoming::Publish(publish))) => {
                    log::debug!("Incoming PUB: {publish:?}");
                    // Check if the incoming publish is a duplicate of a publish already being tracked
                    //
                    // NOTE: A client is required to treat received duplicates as a new application message
                    // as per MQTTv5 spec 4.3.2. However, this is only true when a publish with the same PKID
                    // has previously been acked, because it then becomes impossible to tell if this duplicate
                    // was a redelivery of the previous message, or a redelivery of another message with the
                    // same PKID that was lost.
                    //
                    // In this case, if our `PubTracker` is currently tracking a publish with the same PKID,
                    // we know the duplicate message is for the message we have not yet acked, because the
                    // PKID would not be available for re-use by the broker until that publish was acked.
                    // Thus, we can safely discard the duplicate.
                    // In fact, this is necessary for correct tracking of publishes dispatched to the receivers.
                    if publish.dup && self.unacked_pubs.contains(&publish) {
                        log::debug!("Duplicate PUB received for PUB already owned. Discarding.");
                        continue;
                    }

                    // Dispatch the message to receivers
                    // TODO: Probably don't want to do this unnecessary clone of the publish here,
                    // ideally the error would contain it, but will revisit this as part of a broader
                    // error story rework. Could also make dispatch take a borrow, but I think the
                    // consumption semantics are probably better.
                    match self
                        .incoming_pub_dispatcher
                        .dispatch_publish(publish.clone())
                        .await
                    {
                        Ok(num_dispatches) => {
                            log::debug!("Dispatched PUB to {num_dispatches} receivers");
                            let manual_ack = self.client.get_manual_ack(&publish);
                            // Register the dispatched publish to track the acks
                            match self.unacked_pubs.register_pending(
                                &publish,
                                manual_ack,
                                num_dispatches,
                            ) {
                                Ok(()) => {
                                    log::debug!(
                                        "Registered PUB. Waiting for {num_dispatches} acks"
                                    );
                                }
                                Err(RegisterError::AlreadyRegistered(_)) => {
                                    // Technically this could be reachable if some other thread were manipulating the
                                    // pub tracker registrations, but at that point, everything is broken.
                                    // Perhaps panic is more idiomatic? If such scenario occurs, acking is now completely
                                    // broken, and it is likely that no further acks will be possible, so a panic seems
                                    // appropriate. Or perhaps exiting the session with failure is preferable?
                                    unreachable!("Already checked that the pub tracker does not contain the publish");
                                }
                            }
                        }
                        Err(e) => {
                            // TODO: This should be an error log. Change this once there is a better path for
                            // unfiltered messages to be acked.
                            log::warn!("Error dispatching PUB. Will auto-ack. Reason: {e:?}");

                            // Ack the message in a task to avoid blocking the MQTT event loop.
                            tokio::spawn({
                                let acker = self.client.clone();
                                async move {
                                    match acker.ack(&publish).await {
                                        Ok(()) => log::debug!("Auto-ack successful"),
                                        Err(e) => log::error!(
                                            "Auto-ack failed. Publish may be redelivered. Reason: {e:?}"
                                        ),
                                    };
                                }
                            });
                        }
                    }
                }

                // Disconnect is initiated on client-side
                Ok(Event::Outgoing(Outgoing::Disconnect)) => {
                    // Update the desire_disconnect state if necessary
                    match desire_disconnect {
                        // Session logic caused disconnect
                        // NOTE: No need to set the DesireDisconnect here, as it was set when the
                        // decision to disconnect was made.
                        DesireDisconnect::Internal => {
                            log::debug!("Session initiated disconnect");
                        }
                        // User triggered the disconnect.
                        // NOTE: If the user triggered it, the DesireDisconnect will not yet be
                        // set to User - it would be set to No. That's why we set it here.
                        DesireDisconnect::No => {
                            log::debug!("User initiated disconnect");
                            desire_disconnect = DesireDisconnect::User;
                        }
                        // NOTE: This case is invalid. DesireDisconnect should NOT already be set
                        // to User. However, we handle it as above, just in case.
                        DesireDisconnect::User => {
                            log::warn!(
                                "Disconnect initiated in unexpected state. Assuming user-initiated."
                            );
                            desire_disconnect = DesireDisconnect::User;
                        }
                    }
                }

                Ok(_) => {
                    // There could be additional incoming and outgoing event responses here if
                    // more filters like the above one are applied
                }

                // Desired disconnect completion
                Err(ConnectionError::MqttState(StateError::ConnectionAborted))
                    if desire_disconnect != DesireDisconnect::No =>
                {
                    match desire_disconnect {
                        DesireDisconnect::Internal => {
                            log::debug!("Internal disconnect complete");
                            break;
                        }
                        DesireDisconnect::User => {
                            log::debug!("User disconnect complete");
                            break;
                        }
                        // Unreachable because of arm with guard condition.
                        DesireDisconnect::No => unreachable!(),
                    }
                }

                // Connection refused by broker - unrecoverable
                Err(ConnectionError::ConnectionRefused(rc)) => {
                    log::error!("Connection Refused: rc: {rc:?}");
                    result = Err(SessionErrorKind::ConnectionError(r.unwrap_err()));
                    break;
                }

                // Other errors are passed to reconnect policy
                Err(e) => {
                    // Only log connection loss at info level the first time an error happens,
                    // so as not to spam the logs.
                    if prev_reconnect_attempts == 0 {
                        log::info!("Connection lost.");
                    }
                    // Always log the error itself at error level
                    log::error!("Error: {e:?}");

                    // Defer decision to reconnect policy
                    if let Some(delay) = self
                        .reconnect_policy
                        .next_reconnect_delay(prev_reconnect_attempts, &e)
                    {
                        log::info!("Attempting reconnect in {delay:?}");
                        tokio::time::sleep(delay).await;
                    } else {
                        log::info!("Reconnect attempts halted by reconnect policy");
                        result = Err(SessionErrorKind::ReconnectHalted);
                        break;
                    }
                    prev_reconnect_attempts += 1;
                }
            }
        }
        log::info!("Session ended");
        cancel_token.cancel();
        result.map_err(std::convert::Into::into)
    }

    async fn trigger_session_exit(&self) {
        let exit_handle = self.get_session_exit_handle();
        // TODO: Can this even fail? If not, this helper function is unnecessary
        match exit_handle.exit_session().await {
            Ok(()) => log::debug!("Session exit successful"),
            Err(e) => log::debug!("Session exit failed: {e:?}"),
        }
    }
}

/// Run background tasks for [`Session.run()`]
async fn run_background(
    client: impl InternalClient + Clone,
    unacked_pubs: Arc<PubTracker>,
    sat_auth_file: Option<String>,
    cancel_token: CancellationToken,
) {
    /// Loop over the [`PubTracker`] to ack publishes that are ready to be acked.
    async fn ack_ready_publishes(unacked_pubs: Arc<PubTracker>, acker: impl InternalClient) -> ! {
        loop {
            // Get the next ready ack
            let (ack, pkid) = unacked_pubs.next_ready().await;
            // Ack the publish
            match acker.manual_ack(ack).await {
                Ok(()) => log::debug!("Sent ACK for PKID {pkid}"),
                Err(e) => log::error!("ACK failed for PKID {pkid}: {e:?}"),
                // TODO: how realistically can this fail? And how to respond if it does?
            }
        }
    }

    /// Maintain the SAT token authentication by renewing it before it expires
    async fn maintain_sat_auth(sat_auth_file: String, client: impl InternalClient) -> ! {
        let mut first_pass = true;
        let mut sleep_time = 5;
        loop {
            if !first_pass {
                tokio::time::sleep(tokio::time::Duration::from_secs(sleep_time)).await;
            }

            sleep_time = 5;

            // Get SAT token
            let sat_token = match std::fs::read_to_string(&sat_auth_file) {
                Ok(token) => token,
                Err(e) => {
                    log::error!("Error reading SAT token from file: {e:?}");
                    continue;
                }
            };

            // Get the expiry time of the SAT token
            let expiry = match get_jwt_expiry(&sat_token) {
                Ok(expiry) => expiry,
                Err(e) => {
                    log::error!("{e:?}");
                    continue;
                }
            };

            if !first_pass {
                let props = AuthProperties {
                    method: Some("K8S-SAT".to_string()),
                    data: Some(sat_token.into()),
                    reason: None,
                    user_properties: Vec::new(),
                };

                // Re-authenticate the client
                match client.reauth(props).await {
                    Ok(()) => log::debug!("SAT token renewed"),
                    Err(e) => {
                        log::error!("Error renewing SAT token: {e:?}");
                        continue;
                    }
                }
            }

            // Sleep until 5 seconds prior to the token expiry
            let target_time = UNIX_EPOCH + Duration::from_secs(expiry);
            let Ok(time_until_expiry) = target_time.duration_since(SystemTime::now()) else {
                log::error!("Error calculating SAT token expiry time");
                continue;
            };
            let time_until_expiry = time_until_expiry.as_secs();
            if time_until_expiry > 5 {
                sleep_time = time_until_expiry;
            }
            first_pass = false;
        }
    }

    // Run the background tasks
    if let Some(sat_auth_file) = sat_auth_file {
        tokio::select! {
            () = cancel_token.cancelled() => {
                log::debug!("Session background task cancelled");
            }
            () = ack_ready_publishes(unacked_pubs, client.clone()) => {
                log::error!("`ack_ready_publishes` task ended unexpectedly.");
            }
            () = maintain_sat_auth(sat_auth_file, client) => {
                log::error!("`maintain_sat_auth` task ended unexpectedly.");
            }
        }
    } else {
        tokio::select! {
            () = cancel_token.cancelled() => {
                log::debug!("Session background task cancelled");
            }
            () = ack_ready_publishes(unacked_pubs, client) => {
                log::error!("`ack_ready_publishes` task ended unexpectedly.");
            }
        }
    }
}

impl<C, EL> MqttProvider<SessionPubSub<C>, SessionPubReceiver> for Session<C, EL>
where
    C: InternalClient + Clone + Send + Sync + 'static,
    EL: MqttEventLoop,
{
    /// Return the client ID of the MQTT client being used in this [`Session`]
    fn client_id(&self) -> &str {
        &self.client_id
    }

    /// Return an instance of [`SessionPubSub`] that can be used to execute MQTT operations
    /// using this [`Session`]
    fn pub_sub(&self) -> SessionPubSub<C> {
        SessionPubSub(self.client.clone())
    }

    /// Return an instance of [`SessionPubReceiver`] that can be used to receive incoming publishes
    /// on a particular topic using this [`Session`]
    ///
    /// # Arguments
    /// * `topic_filter` - The topic filter to use for the receiver
    /// * `auto_ack` - Whether the receiver should automatically ack incoming publishes
    ///
    /// # Errors
    /// Returns a [`TopicParseError`] if the pub receiver cannot be registered.
    fn filtered_pub_receiver(
        &mut self,
        topic_filter: &str,
        auto_ack: bool,
    ) -> Result<SessionPubReceiver, TopicParseError> {
        let topic_filter = TopicFilter::from_str(topic_filter)?;
        let rx = self.incoming_pub_dispatcher.register_filter(&topic_filter);
        Ok(SessionPubReceiver::new(
            rx,
            self.unacked_pubs.clone(),
            auto_ack,
        ))
    }
}

/// Send outgoing MQTT messages for publish, subscribe and unsubscribe.
#[derive(Clone)]
pub struct SessionPubSub<PS>(PS)
where
    PS: MqttPubSub + Clone + Send + Sync;

#[async_trait]
impl<PS> MqttPubSub for SessionPubSub<PS>
where
    PS: MqttPubSub + Clone + Send + Sync,
{
    async fn publish(
        &self,
        topic: impl Into<String> + Send,
        qos: QoS,
        retain: bool,
        payload: impl Into<Bytes> + Send,
    ) -> Result<CompletionToken, ClientError> {
        self.0.publish(topic, qos, retain, payload).await
    }

    async fn publish_with_properties(
        &self,
        topic: impl Into<String> + Send,
        qos: QoS,
        retain: bool,
        payload: impl Into<Bytes> + Send,
        properties: PublishProperties,
    ) -> Result<CompletionToken, ClientError> {
        self.0
            .publish_with_properties(topic, qos, retain, payload, properties)
            .await
    }

    async fn subscribe(
        &self,
        topic: impl Into<String> + Send,
        qos: QoS,
    ) -> Result<CompletionToken, ClientError> {
        match qos {
            QoS::AtMostOnce => {
                unimplemented!("QoS 0 is not yet supported for subscribe operations")
            }
            QoS::AtLeastOnce => self.0.subscribe(topic, qos).await,
            QoS::ExactlyOnce => {
                unimplemented!("QoS 2 is not yet supported for subscribe operations")
            }
        }
    }

    async fn subscribe_with_properties(
        &self,
        topic: impl Into<String> + Send,
        qos: QoS,
        properties: SubscribeProperties,
    ) -> Result<CompletionToken, ClientError> {
        match qos {
            QoS::AtMostOnce => {
                unimplemented!("QoS 0 is not yet supported for subscribe operations")
            }
            QoS::AtLeastOnce => {
                self.0
                    .subscribe_with_properties(topic, qos, properties)
                    .await
            }
            QoS::ExactlyOnce => {
                unimplemented!("QoS 2 is not yet supported for subscribe operations")
            }
        }
    }

    async fn unsubscribe(
        &self,
        topic: impl Into<String> + Send,
    ) -> Result<CompletionToken, ClientError> {
        self.0.unsubscribe(topic).await
    }

    async fn unsubscribe_with_properties(
        &self,
        topic: impl Into<String> + Send,
        properties: UnsubscribeProperties,
    ) -> Result<CompletionToken, ClientError> {
        self.0.unsubscribe_with_properties(topic, properties).await
    }
}

/// Receive and acknowledge incoming MQTT messages.
pub struct SessionPubReceiver {
    /// Receiver for incoming publishes
    pub_rx: Receiver<Publish>,
    /// Tracker for acks of incoming publishes
    unacked_pubs: Arc<PubTracker>,
    /// Controls whether incoming publishes are auto-acked
    auto_ack: bool,
    /// Set of PKIDs for incoming publishes that have not yet been acked.
    /// Ensures publishes cannot be acked twice.
    /// (only used if `auto_ack` == false)
    unacked_pkids: Mutex<HashSet<u16>>,
}

impl SessionPubReceiver {
    fn new(pub_rx: Receiver<Publish>, unacked_pubs: Arc<PubTracker>, auto_ack: bool) -> Self {
        Self {
            pub_rx,
            unacked_pubs,
            auto_ack,
            unacked_pkids: Mutex::new(HashSet::new()),
        }
    }
}

#[async_trait]
impl MqttPubReceiver for SessionPubReceiver {
    async fn recv(&mut self) -> Option<Publish> {
        let result = self.pub_rx.recv().await;
        if let Some(publish) = &result {
            if self.auto_ack {
                // Ack immediately if auto-ack is enabled
                // TODO: This ack failure should probably be unreachable and cause panic.
                // Reconsider in error PR.
                self.unacked_pubs.ack(publish).await.unwrap();
            } else {
                // Otherwise, track the PKID for manual acking
                self.unacked_pkids.lock().unwrap().insert(publish.pkid);
            }
        }

        result
    }
}

#[async_trait]
impl MqttAck for SessionPubReceiver {
    async fn ack(&self, publish: &Publish) -> Result<(), ClientError> {
        {
            let mut unacked_pkids_g = self.unacked_pkids.lock().unwrap();
            // TODO: don't panic here. This is bad.
            // Will be addressed in next PR about errors, but don't want to expand
            // the scope of this one.
            assert!(!self.auto_ack, "Auto-ack is enabled. Cannot manually ack.");
            assert!(unacked_pkids_g.contains(&publish.pkid), "");
            unacked_pkids_g.remove(&publish.pkid);
        }
        // TODO: Convert this error into the correct type
        self.unacked_pubs.ack(publish).await.unwrap();
        Ok(())
    }
}

impl Drop for SessionPubReceiver {
    fn drop(&mut self) {
        // Close the receiver channel to ensure no more publishes are dispatched
        // while we clean up.
        self.pub_rx.close();

        // Drain and ack any remaining publishes that are in flight so as not to
        // hold up the ack ordering.
        //
        // NOTE: We MUST do this because if not, the pub tracker can enter a bad state.
        // Consider a SessionPubReceiver that drops while the Session remains alive,
        // where there are dispatched messages in the pub_rx channel. This puts the
        // PubTracker (and thus the Session) in a bad state. There will be an item in
        // it awaiting acks that will never come, thus blocking all other acks from being
        // able to be sent due to ordering rules. Once a publish is dispatched to a
        // SessionPubReceiver, the SessionPubReceiver MUST ack them all.
        while let Ok(publish) = self.pub_rx.try_recv() {
            // NOTE: Not ideal to spawn these tasks in a drop, but it can be safely
            // done here by moving the necessary values.
            log::warn!(
                "Dropping SessionPubReceiver with unacked publish (PKID {}). Auto-acking.",
                publish.pkid
            );
            tokio::task::spawn({
                let unacked_pubs = self.unacked_pubs.clone();
                let publish = publish;
                async move {
                    match unacked_pubs.ack(&publish).await {
                        Ok(()) => log::debug!("Auto-ack of PKID {} successful", publish.pkid),
                        Err(e) => log::error!(
                            "Auto-ack failed for {}. Publish may be redelivered. Reason: {e:?}",
                            publish.pkid
                        ),
                        // TODO: if this ack failed, the Session is now in a broken state. Consider adding an
                        // emergency mechanism of some kind to get us out of it.
                    };
                }
            });
        }
    }
}

/// Handle used to end an MQTT session.
#[derive(Clone)]
pub struct SessionExitHandle<D>
where
    D: MqttDisconnect + Clone + Send + Sync,
{
    disconnector: D,
}

impl<D> SessionExitHandle<D>
where
    D: MqttDisconnect + Clone + Send + Sync,
{
    /// End the session running in the [`Session`] that created this handle.
    ///
    /// # Errors
    /// Returns `ClientError` if there is a failure ending the session.
    /// This should not happen.
    pub async fn exit_session(&self) -> Result<(), ClientError> {
        // TODO: can this fail? I don't think it actually can.
        // TODO: This doesn't actually end the MQTT session because rumqttc doesn't allow
        // us to manually set the session expiry interval to 0 on a reconnect.
        // Need to work with Shanghai to drive this feature.
        self.disconnector.disconnect().await
    }
}

fn get_jwt_expiry(token: &str) -> Result<u64, String> {
    let parts: Vec<_> = token.split('.').collect();

    if parts.len() != 3 {
        return Err("Invalid JWT token".to_string());
    }

    match STANDARD_NO_PAD.decode(parts[1]) {
        Ok(payload) => match std::str::from_utf8(&payload) {
            Ok(payload) => match serde_json::from_str::<serde_json::Value>(payload) {
                Ok(payload_json) => match payload_json.get("exp") {
                    Some(exp_time) => match exp_time.as_u64() {
                        Some(exp_time) => Ok(exp_time),
                        None => Err("Unable to parse JWT token expiry time".to_string()),
                    },
                    None => Err("JWT token does not contain expiry time".to_string()),
                },
                Err(e) => Err(format!("Unable to parse JWT token: {e:?}")),
            },
            Err(e) => Err(format!("Unable to parse JWT token: {e:?}")),
        },
        Err(e) => Err(format!("Unable to decode JWT token: {e:?}")),
    }
}
