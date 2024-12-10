// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

//! Internal implementation of [`Session`] and [`SessionExitHandle`].

use std::fs;
use std::path::Path;
use std::sync::{Arc, Mutex};
use std::time::Duration;

use notify::RecommendedWatcher;
use notify_debouncer_full::{new_debouncer, RecommendedCache};
use rumqttc::v5::mqttbytes::v5::AuthReasonCode;
use tokio::sync::Notify;
use tokio_util::sync::CancellationToken;

use crate::control_packet::{AuthProperties, QoS};
use crate::error::ConnectionError;
use crate::interface::{Event, Incoming, MqttClient, MqttDisconnect, MqttEventLoop};
use crate::session::dispatcher::IncomingPublishDispatcher;
use crate::session::managed_client::SessionManagedClient;
use crate::session::pub_tracker::{PubTracker, RegisterError};
use crate::session::reconnect_policy::ReconnectPolicy;
use crate::session::state::SessionState;
use crate::session::{SessionError, SessionErrorKind, SessionExitError};

/// Used as the authentication method for the MQTT client when using SAT.
const SAT_AUTHENTICATION_METHOD: &str = "K8S-SAT";

/// Context for maintaining SAT token authentication.
struct SatAuthContext {
    /// File path to the SAT token
    file_location: String,
    /// SAT file watcher, held to keep the watcher alive
    _watcher: Option<notify_debouncer_full::Debouncer<RecommendedWatcher, RecommendedCache>>,
    /// Notifier for SAT file changes
    file_watcher_notify: Arc<Notify>,
    /// Channel for receiving auth change notifications
    auth_watcher_rx: tokio::sync::mpsc::UnboundedReceiver<AuthReasonCode>,
}

/// Result of a re-authentication attempt.
enum ReauthResult {
    /// Re-authentication occurred, with the given reason code.
    Auth(AuthReasonCode),
    /// Re-authentication timed out.
    Timeout,
    /// Error occurred during re-authentication.
    Error,
}

impl SatAuthContext {
    /// Create a new SAT auth context.
    ///
    /// Returns a [`SessionError`] if an error occurs.
    fn new(
        file_location: String,
        auth_watcher_rx: tokio::sync::mpsc::UnboundedReceiver<AuthReasonCode>,
    ) -> Result<Self, SessionError> {
        // Create a watcher notifier
        let file_watcher_notify = Arc::new(Notify::new());
        let file_watcher_notify_clone = file_watcher_notify.clone();

        // Create a SAT file watcher
        let _watcher = match new_debouncer(
            Duration::from_secs(10),
            None,
            move |res: Result<Vec<notify_debouncer_full::DebouncedEvent>, Vec<notify::Error>>| {
                match res {
                    Ok(events) => {
                        if events.iter().any(|e| {
                            // Only notify on events that are not file open events
                            !matches!(
                                e.event.kind,
                                notify::EventKind::Access(notify::event::AccessKind::Open(_))
                            )
                        }) {
                            file_watcher_notify_clone.notify_one();
                        }
                    }
                    Err(err) => {
                        log::error!("Error reading SAT file: {err:?}");
                    }
                }
            },
        ) {
            Ok(mut debouncer) => {
                // Start watching the SAT file
                debouncer
                    .watch(
                        Path::new(&file_location),
                        notify::RecursiveMode::NonRecursive,
                    )
                    .map_err(|e| match e.kind {
                        notify::ErrorKind::Io(e) => {
                            SessionError::from(SessionErrorKind::IoError(e))
                        }
                        _ => std::convert::Into::into(SessionErrorKind::SatTokenWatcherError(
                            e.to_string(),
                        )),
                    })?;
                Some(debouncer)
            }
            Err(e) => {
                log::error!("Error creating SAT file watcher: {e:?}");
                match e.kind {
                    notify::ErrorKind::Io(e) => {
                        return Err(std::convert::Into::into(SessionErrorKind::IoError(e)));
                    }
                    _ => {
                        return Err(std::convert::Into::into(
                            SessionErrorKind::SatTokenWatcherError(e.to_string()),
                        ));
                    }
                }
            }
        };

        Ok(Self {
            file_location,
            _watcher,
            file_watcher_notify,
            auth_watcher_rx,
        })
    }

    /// Wait for SAT file changes.
    pub async fn notified(&self) {
        self.file_watcher_notify.notified().await;
    }

    /// Re-authenticate the client using the SAT token.
    ///
    /// Returns a [`ReauthResult`] indicating the result of the re-authentication.
    pub async fn reauth(&mut self, timeout: Duration, client: &impl MqttClient) -> ReauthResult {
        // Get SAT token
        let sat_token = match std::fs::read_to_string(&self.file_location) {
            Ok(token) => token,
            Err(e) => {
                log::error!("Error reading SAT token from file: {e:?}");
                return ReauthResult::Error;
            }
        };

        let props = AuthProperties {
            method: Some(SAT_AUTHENTICATION_METHOD.to_string()),
            data: Some(sat_token.into()),
            reason: None,
            user_properties: Vec::new(),
        };

        // Re-authenticate the client
        match client.reauth(props).await {
            Ok(()) => { /* Queued reauth */ }
            Err(e) => {
                log::error!("Error renewing SAT token: {e:?}");
                return ReauthResult::Error;
            }
        }

        // Wait for next auth change
        tokio::select! {
            auth = self.auth_watcher_rx.recv() => {
                if let Some(auth) = auth {
                    return ReauthResult::Auth(auth);
                }
                log::error!("Auth watcher channel closed");
                ReauthResult::Error
            }
            _ = tokio::time::sleep(timeout) => {
                log::error!("Reauth timed out");
                ReauthResult::Timeout
            },
        }
    }
}

/// Client that manages connections over a single MQTT session.
///
/// Use this centrally in an application to control the session and to create
/// instances of [`SessionManagedClient`] and [`SessionExitHandle`].
pub struct Session<C, EL>
where
    C: MqttClient + Clone + Send + Sync + 'static,
    EL: MqttEventLoop,
{
    /// Underlying MQTT client
    client: C,
    /// Underlying MQTT event loop
    event_loop: EL,
    /// Client ID of the underlying rumqttc client
    client_id: String,
    /// File path to the SAT token
    sat_file: Option<String>,
    /// Dispatcher for incoming publishes
    incoming_pub_dispatcher: Arc<Mutex<IncomingPublishDispatcher>>,
    /// Tracker for unacked incoming publishes
    unacked_pubs: Arc<PubTracker>,
    /// Reconnect policy
    reconnect_policy: Box<dyn ReconnectPolicy>,
    /// Current state
    state: Arc<SessionState>,
    /// Notifier for a force exit signal
    notify_force_exit: Arc<Notify>,
    /// Indicates if Session was previously run. Temporary until re-use is supported.
    previously_run: bool,
}

impl<C, EL> Session<C, EL>
where
    C: MqttClient + Clone + Send + Sync + 'static,
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
        sat_file: Option<String>,
    ) -> Self {
        // NOTE: drop the unfiltered message receiver from the dispatcher here in order to force non-filtered
        // messages to fail to be dispatched. The .run() method will respond to this failure by acking.
        // This lets us retain correct functionality while waiting for a more elegant solution with ordered ack.
        let (incoming_pub_dispatcher, _) = IncomingPublishDispatcher::new();
        let incoming_pub_dispatcher = Arc::new(Mutex::new(incoming_pub_dispatcher));
        Self {
            client,
            event_loop,
            client_id,
            sat_file,
            incoming_pub_dispatcher,
            unacked_pubs: Arc::new(PubTracker::default()),
            reconnect_policy,
            state: Arc::new(SessionState::default()),
            notify_force_exit: Arc::new(Notify::new()),
            previously_run: false,
        }
    }

    /// Return a new instance of [`SessionExitHandle`] that can be used to end this [`Session`]
    pub fn create_exit_handle(&self) -> SessionExitHandle<C> {
        SessionExitHandle {
            disconnector: self.client.clone(),
            state: self.state.clone(),
            force_exit: self.notify_force_exit.clone(),
        }
    }

    /// Return a new instance of [`SessionConnectionMonitor`] that can be used to monitor the connection state
    pub fn create_connection_monitor(&self) -> SessionConnectionMonitor {
        SessionConnectionMonitor {
            state: self.state.clone(),
        }
    }

    /// Return a new instance of [`SessionManagedClient`] that can be used to send and receive messages
    pub fn create_managed_client(&self) -> SessionManagedClient<C> {
        SessionManagedClient {
            client_id: self.client_id.clone(),
            pub_sub: self.client.clone(),
            incoming_pub_dispatcher: self.incoming_pub_dispatcher.clone(),
            unacked_pubs: self.unacked_pubs.clone(),
        }
    }

    /// Begin running the [`Session`].
    ///
    /// Blocks until either a session exit or a fatal connection error is encountered.
    ///
    /// # Errors
    /// Returns a [`SessionError`] if the session encounters a fatal error and ends.
    // TODO: Suppressing this clippy lint is a temporary solution to a much bigger problem.
    // Currently the pub dispatcher is locked by a Mutex, which is not supposed to be held
    // across an await point. Ideally we would just use an async-aware Mutex here (e.g. the
    // tokio Mutex), but that would break some other internal APIs by forcing them to be
    // async where we don't want them to be. The correct solution is to eliminate the
    // Mutex altogether but that is a much larger refactoring task. In the meantime,
    // just suppress the lint - this technically creates a race condition around a case
    // where the dispatcher is being used to dispatch and to register simultaneously, but
    // this is unlikely, and worst case one message gets lost. Fix ASAP though.
    #[allow(clippy::await_holding_lock)]
    pub async fn run(&mut self) -> Result<(), SessionError> {
        self.state.transition_running();
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

        let mut sat_auth_context = None;
        let mut sat_auth_tx = None;

        if let Some(sat_file) = &self.sat_file {
            // Set the authentication method
            self.event_loop
                .set_authentication_method(Some(SAT_AUTHENTICATION_METHOD.to_string()));

            // Read the SAT auth file
            match fs::read(sat_file) {
                Ok(sat_auth_data) => {
                    // Set the authentication data
                    self.event_loop
                        .set_authentication_data(Some(sat_auth_data.into()));
                }
                Err(e) => {
                    log::error!("Cannot read SAT auth file: {sat_file}");
                    return Err(std::convert::Into::into(SessionErrorKind::IoError(e)));
                }
            }

            let (auth_watch_channel_tx, auth_watch_channel_rx) =
                tokio::sync::mpsc::unbounded_channel();
            sat_auth_tx = Some(auth_watch_channel_tx);

            sat_auth_context = Some(SatAuthContext::new(
                sat_file.clone(),
                auth_watch_channel_rx,
            )?);
        }

        // Background tasks
        let cancel_token = CancellationToken::new();
        tokio::spawn({
            let cancel_token = cancel_token.clone();
            let client = self.client.clone();
            let unacked_pubs = self.unacked_pubs.clone();
            run_background(client, unacked_pubs, sat_auth_context, cancel_token)
        });

        // Indicates whether this session has been previously connected
        let mut prev_connected = false;
        // Number of previous reconnect attempts
        let mut prev_reconnect_attempts = 0;
        // Return value for the session indicating reason for exit
        let mut result = Ok(());

        // Handle events
        loop {
            // Poll the next event/error unless a force exit occurs.
            let next = tokio::select! {
                // Ensure that the force exit signal is checked first.
                biased;
                () = self.notify_force_exit.notified() => { break },
                next = self.event_loop.poll() => { next },
            };

            match next {
                Ok(Event::Incoming(Incoming::ConnAck(connack))) => {
                    // Update connection state
                    self.state.transition_connected();
                    // Reset the counter on reconnect attempts
                    prev_reconnect_attempts = 0;
                    log::debug!("Incoming CONNACK: {connack:?}");

                    // If the session is not present after a reconnect, end the session.
                    if prev_connected && !connack.session_present {
                        log::error!(
                            "Session state not present on broker after reconnect. Ending session."
                        );
                        result = Err(SessionErrorKind::SessionLost);
                        if self.state.desire_exit() {
                            // NOTE: this could happen if the user was exiting when the connection was dropped,
                            // while the Session was not aware of the connection drop. Then, the drop has to last
                            // long enough for the MQTT session expiry interval to cause the broker to discard the
                            // MQTT session, and thus, you would enter this case.
                            // NOTE: The reason that the misattribution of cause may occur in logs is due to the
                            // (current) loose matching of received disconnects on account of an rumqttc bug.
                            // See the error cases below in this match statement for more information.
                            log::debug!("Session-initiated exit triggered when User-initiated exit was already in-progress. There may be two disconnects, both attributed to Session");
                        }
                        self.trigger_session_exit().await;
                    }
                    // Otherwise, connection was successful
                    else {
                        prev_connected = true;
                        // Set clean start to false for subsequent connections
                        self.event_loop.set_clean_start(false);
                    }
                }
                Ok(Event::Incoming(Incoming::Auth(auth))) => {
                    log::debug!("Incoming AUTH: {auth:?}");

                    if let Some(sat_auth_tx) = &sat_auth_tx {
                        // Notify the background task that the auth data has changed
                        // TODO: This is a bit of a hack, but it works for now. Ideally, the reauth
                        // method on rumqttc would return a completion token and we could use that
                        // in the background task to know when the reauth is complete.
                        match sat_auth_tx.send(auth.code) {
                            Ok(()) => {}
                            Err(e) => {
                                // This should never happen unless the background task has exited
                                // in which case the session is already in a bad state and we should
                                // have already exited.
                                log::error!("Error sending auth code to SAT auth task: {e:?}");
                            }
                        }
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
                        .lock()
                        .unwrap()
                        .dispatch_publish(publish.clone())
                    {
                        Ok(num_dispatches) => {
                            log::debug!("Dispatched PUB to {num_dispatches} receivers");

                            match publish.qos {
                                QoS::AtMostOnce => {
                                    log::debug!("No ack required for QoS 0 PUB");
                                }
                                // QoS 1 or 2
                                _ => {
                                    // Register the dispatched publish to track the acks
                                    match self
                                        .unacked_pubs
                                        .register_pending(&publish, num_dispatches)
                                    {
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

                Ok(_e) => {
                    // There could be additional incoming and outgoing event responses here if
                    // more filters like the above one are applied
                }

                // Desired disconnect completion
                // NOTE: This normally is StateError::ConnectionAborted, but rumqttc sometimes
                // can deliver something else in this case. For now, we'll accept any
                // MqttState variant when trying to disconnect.
                // TODO: However, this has the side-effect of falsely reporting disconnects that are the
                // result of network failure as client-side disconnects if there is an outstanding
                // DesireExit value. This is not harmful, but it is bad for logging, and should
                // probably be fixed.
                Err(ConnectionError::MqttState(_)) if self.state.desire_exit() => {
                    self.state.transition_disconnected();
                    break;
                }

                // Connection refused by broker - unrecoverable
                Err(ConnectionError::ConnectionRefused(rc)) => {
                    log::error!("Connection Refused: rc: {rc:?}");
                    result = Err(SessionErrorKind::ConnectionError(next.unwrap_err()));
                    break;
                }

                // Other errors are passed to reconnect policy
                Err(e) => {
                    self.state.transition_disconnected();

                    // Always log the error itself at error level
                    log::error!("Error: {e:?}");

                    // Defer decision to reconnect policy
                    if let Some(delay) = self
                        .reconnect_policy
                        .next_reconnect_delay(prev_reconnect_attempts, &e)
                    {
                        log::info!("Attempting reconnect in {delay:?}");
                        // Wait for either the reconnect delay time, or a force exit signal
                        tokio::select! {
                            () = tokio::time::sleep(delay) => {}
                            () = self.notify_force_exit.notified() => {
                                log::info!("Reconnect attempts halted by force exit");
                                result = Err(SessionErrorKind::ForceExit);
                                break;
                            }
                        }
                    } else {
                        log::info!("Reconnect attempts halted by reconnect policy");
                        result = Err(SessionErrorKind::ReconnectHalted);
                        break;
                    }
                    prev_reconnect_attempts += 1;
                }
            }
        }
        self.state.transition_exited();
        cancel_token.cancel();
        result.map_err(std::convert::Into::into)
    }

    /// Helper for triggering a session exit and logging the result
    async fn trigger_session_exit(&self) {
        let exit_handle = self.create_exit_handle();
        match exit_handle.trigger_exit_internal().await {
            Ok(()) => log::debug!("Internal session exit successful"),
            Err(e) => log::debug!("Internal session exit failed: {e:?}"),
        }
    }
}

/// Run background tasks for [`Session.run()`]
async fn run_background(
    client: impl MqttClient + Clone,
    unacked_pubs: Arc<PubTracker>,
    sat_auth_context: Option<SatAuthContext>,
    cancel_token: CancellationToken,
) {
    /// Loop over the [`PubTracker`] to ack publishes that are ready to be acked.
    async fn ack_ready_publishes(unacked_pubs: Arc<PubTracker>, acker: impl MqttClient) -> ! {
        loop {
            // Get the next ready publish
            let publish = unacked_pubs.next_ready().await;
            // Ack the publish
            match acker.ack(&publish).await {
                Ok(()) => log::debug!("Sent ACK for PKID {}", publish.pkid),
                Err(e) => log::error!("ACK failed for PKID {}: {e:?}", publish.pkid),
                // TODO: how realistically can this fail? And how to respond if it does?
            }
        }
    }

    /// Maintain the SAT token authentication by renewing it when the SAT file changes
    async fn maintain_sat_auth(mut sat_auth_context: SatAuthContext, client: impl MqttClient) -> ! {
        let mut retrying = false;
        loop {
            // Wait for the SAT file to change if not retrying
            if !retrying {
                sat_auth_context.notified().await;
            }

            // Re-authenticate the client
            if let ReauthResult::Auth(AuthReasonCode::Success) = sat_auth_context
                .reauth(Duration::from_secs(10), &client)
                .await
            {
                log::debug!("SAT token renewed successfully");
                retrying = false;
                continue;
            }
            log::error!("Error renewing SAT token, retrying...");
            retrying = true;
            // Wait before retrying
            tokio::time::sleep(Duration::from_secs(10)).await;
        }
    }

    // Run the background tasks
    if let Some(sat_auth_context) = sat_auth_context {
        tokio::select! {
            () = cancel_token.cancelled() => {
                log::debug!("Session background task cancelled");
            }
            () = ack_ready_publishes(unacked_pubs, client.clone()) => {
                log::error!("`ack_ready_publishes` task ended unexpectedly.");
            }
            () = maintain_sat_auth(sat_auth_context, client) => {
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

/// Handle used to end an MQTT session.
///
/// PLEASE NOTE WELL
/// This struct's API is designed around negotiating a graceful exit with the MQTT broker.
/// However, this is not actually possible right now due to a bug in underlying MQTT library.
#[derive(Clone)]
pub struct SessionExitHandle<D>
where
    D: MqttDisconnect + Clone + Send + Sync,
{
    /// The disconnector used to issue disconnect requests
    disconnector: D,
    /// Session state information
    state: Arc<SessionState>,
    /// Notifier for force exit
    force_exit: Arc<Notify>,
}

impl<D> SessionExitHandle<D>
where
    D: MqttDisconnect + Clone + Send + Sync,
{
    /// Attempt to gracefully end the MQTT session running in the [`Session`] that created this handle.
    /// This will cause the [`Session::run()`] method to return.
    ///
    /// Note that a graceful exit requires the [`Session`] to be connected to the broker.
    /// If the [`Session`] is not connected, this method will return an error.
    /// If the [`Session`] connection has been recently lost, the [`Session`] may not yet realize this,
    /// and it can take until up to the keep-alive interval for the [`Session`] to realize it is disconnected,
    /// after which point this method will return an error. Under this circumstance, the attempt was still made,
    /// and may eventually succeed even if this method returns the error
    ///
    /// # Errors
    /// * [`SessionExitError::Dropped`] if the Session no longer exists.
    /// * [`SessionExitError::BrokerUnavailable`] if the Session is not connected to the broker.
    pub async fn try_exit(&self) -> Result<(), SessionExitError> {
        log::debug!("Attempting to exit session gracefully");
        // Check if the session is connected (to best of knowledge)
        if !self.state.is_connected() {
            return Err(SessionExitError::BrokerUnavailable { attempted: false });
        }
        // Initiate the exit
        self.trigger_exit_user().await?;
        // Wait for the exit to complete, or until the session realizes it was already disconnected.
        tokio::select! {
            // NOTE: These two conditions here are functionally almost identical for now, due to the
            // very loose matching of disconnect events in [`Session::run()`] (as a result of bugs and
            // unreliable behavior in rumqttc). These would be less identical conditions if we tightened
            // that matching back up, and that's why they're here.
            () = self.state.condition_exited() => Ok(()),
            () = self.state.condition_disconnected() => Err(SessionExitError::BrokerUnavailable{attempted: true})
        }
    }

    /// Attempt to gracefully end the MQTT session running in the [`Session`] that created this handle.
    /// This will cause the [`Session::run()`] method to return.
    ///
    /// Note that a graceful exit requires the [`Session`] to be connected to the broker.
    /// If the [`Session`] is not connected, this method will return an error.
    /// If the [`Session`] connection has been recently lost, the [`Session`] may not yet realize this,
    /// and it can take until up to the keep-alive interval for the [`Session`] to realize it is disconnected,
    /// after which point this method will return an error. Under this circumstance, the attempt was still made,
    /// and may eventually succeed even if this method returns the error
    /// If the graceful [`Session`] exit attempt does not complete within the specified timeout, this method
    /// will return an error indicating such.
    ///
    /// # Arguments
    /// * `timeout` - The duration to wait for the graceful exit to complete before returning an error.
    ///
    /// # Errors
    /// * [`SessionExitError::Dropped`] if the Session no longer exists.
    /// * [`SessionExitError::BrokerUnavailable`] if the Session is not connected to the broker.
    /// * [`SessionExitError::Timeout`] if the graceful exit attempt does not complete within the specified timeout.
    pub async fn try_exit_timeout(&self, timeout: Duration) -> Result<(), SessionExitError> {
        tokio::time::timeout(timeout, self.try_exit()).await?
    }

    /// Forcefully end the MQTT session running in the [`Session`] that created this handle.
    /// This will cause the [`Session::run()`] method to return.
    ///
    /// The [`Session`] will be granted a period of 1 second to attempt a graceful exit before
    /// forcing the exit. If the exit is forced, the broker will not be aware the MQTT session
    /// has ended.
    ///
    /// Returns true if the exit was graceful, and false if the exit was forced.
    pub async fn exit_force(&self) -> bool {
        // TODO: There might be a way to optimize this a bit better if we know we're disconnected,
        // but I don't wanna mess around with this until we have mockable unit testing
        log::debug!("Attempting to exit session gracefully before force exiting");
        // Ignore the result here - we don't care
        let _ = self.trigger_exit_user().await;
        // 1 second grace period to gracefully complete
        tokio::select! {
            () = tokio::time::sleep(Duration::from_secs(1)) => {
                log::debug!("Grace period for graceful session exit expired. Force exiting session");
                // NOTE: There is only one waiter on this Notify at any time.
                self.force_exit.notify_one();
                false
            },
            () = self.state.condition_exited() => {
                log::debug!("Session exited gracefully without need for force exit");
                true
            }
        }
    }

    /// Trigger a session exit, specifying the end user as the issuer of the request
    async fn trigger_exit_user(&self) -> Result<(), SessionExitError> {
        self.state.transition_user_desire_exit();
        // TODO: This doesn't actually end the MQTT session because rumqttc doesn't allow
        // us to manually set the session expiry interval to 0 on a reconnect.
        // Need to work with Shanghai to drive this feature.
        Ok(self.disconnector.disconnect().await?)
    }

    /// Trigger a session exit, specifying the internal session logic as the issuer of the request
    async fn trigger_exit_internal(&self) -> Result<(), SessionExitError> {
        self.state.transition_session_desire_exit();
        // TODO: This doesn't actually end the MQTT session because rumqttc doesn't allow
        // us to manually set the session expiry interval to 0 on a reconnect.
        // Need to work with Shanghai to drive this feature.
        Ok(self.disconnector.disconnect().await?)
    }
}

/// Monitor for connection changes in the [`Session`].
///
/// This is largely for informational purposes.
#[derive(Clone)]
pub struct SessionConnectionMonitor {
    state: Arc<SessionState>,
}

impl SessionConnectionMonitor {
    /// Returns true if the [`Session`] is currently connected.
    /// Note that this may not be accurate if connection has been recently lost.
    #[must_use]
    pub fn is_connected(&self) -> bool {
        self.state.is_connected()
    }

    /// Wait until the [`Session`] is connected.
    /// Returns immediately if already connected.
    pub async fn connected(&self) {
        self.state.condition_connected().await;
    }

    /// Wait until the [`Session`] is disconnected.
    /// Returns immediately if already disconnected.
    pub async fn disconnected(&self) {
        self.state.condition_disconnected().await;
    }
}
