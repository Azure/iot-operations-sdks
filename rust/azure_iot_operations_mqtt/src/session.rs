// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

//! MQTT client providing a managed connection with automatic reconnection across a single MQTT session.
//!
//! This module provides several key components for using an MQTT session:
//! * [`Session`] - Manages the lifetime of the MQTT session
//! * [`SessionManagedClient`] - Sends MQTT messages to the server
//! * [`SessionPubReceiver`] - Receives MQTT messages from the server
//! * [`SessionMonitor`] - Provides information about the MQTT session's state
//! * [`SessionExitHandle`] - Allows the user to exit the session gracefully
//!
//! # [`Session`] lifespan
//! Each instance of [`Session`] is single use - after configuring a [`Session`], and creating any
//! other necessary components from it, calling the [`run`](crate::session::Session::run) method
//! will consume the [`Session`] and block (asynchronously) until the MQTT session shared between
//! client and server ends. Note that a MQTT session can span multiple connects and disconnects to
//! the server.
//!
//! The MQTT session can be ended one of two ways:
//! 1. The [`ReconnectPolicy`] configured on the [`Session`] halts reconnection attempts, causing
//!    the [`Session`] to end the MQTT session.
//! 2. The user uses the [`SessionExitHandle`] to end the MQTT session.
//!
//! # Sending and receiving data over MQTT
//! A [`Session`] can be used to create a [`SessionManagedClient`] for sending data (i.e. outgoing
//! MQTT PUBLISH, MQTT SUBSCRIBE, MQTT UNSUBSCRIBE), and can in turn be used to create a
//! [`SessionPubReceiver`] for receiving incoming data (i.e. incoming MQTT PUBLISH).
//!
//! [`SessionPubReceiver`]s can be either filtered or unfiltered - a filtered receiver will only
//! receive messages that match a specific topic filter, while an unfiltered receiver will receive
//! all messages that do not match another existing filter.
//!
//! Note that in order to receive incoming data, you must both subscribe to the topic filter of
//! interest using the [`SessionManagedClient`] and create a [`SessionPubReceiver`] (filtered or
//! unfiltered). If an incoming message is received that
//! does not match any [`SessionPubReceiver`]s, it will be acknowledged to the MQTT server and
//! discarded. Thus, in order to guarantee that messages will not be lost, you should create the
//! [`SessionPubReceiver`] *before* subscribing to the topic filter.

use std::{
    fmt,
    sync::{Arc, Mutex, Weak},
    time::Duration,
};

use crate::azure_mqtt::{
    self,
    client::{
        ConnectEnhancedAuthResult, ConnectResult, Connection, ConnectionTransportConfig,
        DisconnectedEvent, ReauthResult,
    },
    packet::{AuthProperties, ConnAck, DisconnectProperties, SessionExpiryInterval},
};
use thiserror::Error;
use tokio::sync::Notify;

use crate::aio::{
    AIOBrokerFeatures, AIOBrokerFeaturesBuilder, connection_settings::MqttConnectionSettings,
};
use crate::azure_mqtt_adapter as adapter;
use crate::azure_mqtt_adapter::AzureMqttConnectParameters;
use crate::control_packet::PacketIdentifier;
use crate::error::DetachedError;
pub use crate::session::managed_client::{SessionManagedClient, SessionPubReceiver};
use crate::session::state::SessionState;
use crate::session::{
    dispatcher::IncomingPublishDispatcher,
    enhanced_auth_policy::{EnhancedAuthPolicy, K8sSatFileMonitor},
    reconnect_policy::{ConnectionLossReason, ExponentialBackoffWithJitter, ReconnectPolicy},
};
#[cfg(feature = "test-utils")]
use crate::test_utils::InjectedPacketChannels;

pub(crate) mod dispatcher;
pub mod enhanced_auth_policy;
mod managed_client;
pub(crate) mod plenary_ack;
pub mod reconnect_policy;
mod state;

/// Error describing why a [`Session`] ended prematurely
#[derive(Debug, Error)]
#[error("{kind}")]
pub struct SessionError {
    kind: SessionErrorKind,
    #[source]
    source: Option<Box<dyn std::error::Error + Send + 'static>>,
}

impl SessionError {
    /// Return the corresponding [`SessionErrorKind`] for this error
    #[must_use]
    pub fn kind(&self) -> SessionErrorKind {
        self.kind
    }
}

/// An enumeration of categories of [`SessionError`]
#[derive(Debug, Eq, PartialEq, Clone, Copy)]
#[non_exhaustive]
pub enum SessionErrorKind {
    /// MQTT session was discarded by the server
    SessionLost,
    /// Reconnect attempts were halted by the reconnect policy, ending the MQTT session
    ReconnectHalted,
    /// The [`Session`] was ended by a user-initiated force exit. The server may still retain the MQTT session.
    ForceExit,
    /// Something went wrong with configured values
    Config,
}

impl fmt::Display for SessionErrorKind {
    fn fmt(&self, f: &mut fmt::Formatter<'_>) -> fmt::Result {
        match self {
            SessionErrorKind::SessionLost => {
                write!(f, "session state not present on server after reconnect")
            }
            SessionErrorKind::ReconnectHalted => {
                write!(f, "reconnection halted by reconnect policy")
            }
            SessionErrorKind::ForceExit => write!(f, "session ended by force exit"),
            SessionErrorKind::Config => {
                write!(f, "configuration became invalid during session operation")
            }
        }
    }
}

impl From<SessionErrorKind> for SessionError {
    fn from(kind: SessionErrorKind) -> Self {
        Self { kind, source: None }
    }
}

/// Error configuring a [`Session`].
#[derive(Error, Debug)]
#[error(transparent)]
pub struct SessionConfigError(#[from] adapter::ConnectionSettingsAdapterError);

// NOTE: Retain a struct/kind pattern for `SessionExitError` for future-proofing
/// Error type for exiting a [`Session`] using the [`SessionExitHandle`].
#[derive(Error, Debug)]
#[error("{kind}")]
pub struct SessionExitError {
    kind: SessionExitErrorKind,
}

impl SessionExitError {
    /// Return the corresponding [`SessionExitErrorKind`] for this error
    #[must_use]
    pub fn kind(&self) -> SessionExitErrorKind {
        self.kind
    }
}

impl From<DetachedError> for SessionExitError {
    fn from(_err: DetachedError) -> Self {
        SessionExitError {
            kind: SessionExitErrorKind::Detached,
        }
    }
}

/// An enumeration of categories of [`SessionExitError`]
#[derive(Debug, Clone, Copy, Eq, PartialEq)]
#[non_exhaustive]
pub enum SessionExitErrorKind {
    /// The exit handle was detached from the session
    Detached,
    /// The server could not be reached
    ServerUnavailable,
}

impl fmt::Display for SessionExitErrorKind {
    fn fmt(&self, f: &mut fmt::Formatter<'_>) -> fmt::Result {
        match self {
            SessionExitErrorKind::Detached => {
                write!(f, "Detached from Session")
            }
            SessionExitErrorKind::ServerUnavailable => write!(f, "Could not contact server"),
        }
    }
}

/// Options for configuring a new [`Session`]
#[derive(Builder)]
#[builder(pattern = "owned")]
pub struct SessionOptions {
    /// MQTT Connection Settings for configuring the [`Session`]
    connection_settings: MqttConnectionSettings,
    /// Reconnect Policy to by used by the `Session`
    #[builder(default = "Box::new(ExponentialBackoffWithJitter::default())")]
    reconnect_policy: Box<dyn ReconnectPolicy>,
    /// Authentication Policy to be used by the `Session`
    #[builder(default = "None")]
    enhanced_auth_policy: Option<Box<dyn EnhancedAuthPolicy>>,
    /// Maximum packet identifier
    #[builder(default = "PacketIdentifier::MAX")]
    max_packet_identifier: PacketIdentifier,
    /// Maximum number of queued outgoing QoS 0 PUBLISH packets not yet accepted by the MQTT Session
    #[builder(default = "100")]
    publish_qos0_queue_size: usize,
    /// Maximum number of queued outgoing QoS 1 and 2 PUBLISH packets not yet accepted by the MQTT Session
    #[builder(default = "100")]
    publish_qos1_qos2_queue_size: usize,
    /// Indicates if the Session should use features specific for use with the AIO MQTT Broker
    #[builder(default = "Some(AIOBrokerFeaturesBuilder::default().build().unwrap())")]
    aio_broker_features: Option<AIOBrokerFeatures>,
    /// Injected packet channels for testing purposes
    #[cfg(feature = "test-utils")]
    #[builder(default)]
    injected_packet_channels: Option<InjectedPacketChannels>,
}

/// Client that manages connections over a single MQTT session.
///
/// Use this centrally in an application to control the session and to create
/// instances of [`SessionManagedClient`] and [`SessionExitHandle`].
pub struct Session {
    /// Underlying MQTT client
    client: azure_mqtt::client::Client,
    /// Underlying MQTT Receiver
    receiver: Option<azure_mqtt::client::Receiver>,
    /// Underlying MQTT connect handle
    connect_handle: Option<azure_mqtt::client::ConnectHandle>,
    /// disconnect handle
    disconnect_handle: Arc<Mutex<Option<azure_mqtt::client::DisconnectHandle>>>,
    /// Underlying MQTT reauthorization handle
    reauth_handle: Option<azure_mqtt::client::ReauthHandle>,
    /// Parameters for establishing an MQTT connection using the `azure_mqtt` crate
    connect_parameters: AzureMqttConnectParameters,
    /// Client ID of the underlying MQTT client
    client_id: String,
    /// Receiver dispatcher for incoming publishes
    incoming_pub_dispatcher: Arc<Mutex<IncomingPublishDispatcher>>,
    /// Reconnect policy
    reconnect_policy: Box<dyn ReconnectPolicy>,
    /// Enhanced authentication policy
    enhanced_auth_policy: Option<Arc<dyn EnhancedAuthPolicy>>,
    /// Current state
    state: Arc<SessionState>,
    /// Notifier for a force exit signal
    notify_force_exit: Arc<Notify>,
}

impl Session {
    /// Create a new [`Session`] with the provided options structure.
    ///
    /// # Errors
    /// Returns a [`SessionConfigError`] if there are errors using the session options.
    #[allow(clippy::missing_panics_doc)] // TODO: Remove once a better way to handle auth policy failure
    pub fn new(options: SessionOptions) -> Result<Self, SessionConfigError> {
        let client_id = options.connection_settings.client_id.clone();

        // Add AIO metric and features to user properties when using AIO MQTT broker features
        // CONSIDER: user properties from being supported on SessionOptions or ConnectionSettings
        let user_properties = if let Some(features) = options.aio_broker_features {
            let mut user_properties =
                vec![("metriccategory".to_string(), "aiosdk-rust".to_string())];
            if features.persistence {
                user_properties.push(("aio-persistence".to_string(), true.to_string()));
            }
            user_properties
        } else {
            vec![]
        };

        // Create EnhancedAuthPolicy if provided in options or SAT file is provided via ConnectionSettings
        // NOTE: prioritize the one in SessionOptions over the one in the connection settings
        let enhanced_auth_policy = if let Some(enhanced_auth_policy) = options.enhanced_auth_policy
        {
            Some(Arc::from(enhanced_auth_policy))
        } else {
            options
                .connection_settings
                .sat_file
                .as_ref()
                .map(|sat_file| {
                    K8sSatFileMonitor::new(
                        std::path::PathBuf::from(sat_file),
                        Duration::from_secs(10),
                        // NOTE: It's not really ideal to use ConnectionSettingsAdapterError, but it's
                        // the best that can be done without a config rework
                    )
                    .map_err(|e| adapter::ConnectionSettingsAdapterError {
                        msg: "Failed to create K8sSatFileMonitor for SAT file".to_string(),
                        field: adapter::ConnectionSettingsField::SatFile(sat_file.clone()),
                        source: Some(Box::new(e)),
                    })
                })
                .transpose()?
                .map(|eap| Arc::from(eap) as Arc<dyn EnhancedAuthPolicy>)
        };

        let (client_options, connect_parameters) = options
            .connection_settings
            .into_azure_mqtt_connect_parameters(
                user_properties,
                options.max_packet_identifier,
                options.publish_qos0_queue_size,
                options.publish_qos1_qos2_queue_size,
                #[cfg(feature = "test-utils")]
                options.injected_packet_channels,
            )?;

        let (client, connect_handle, receiver) = azure_mqtt::client::new_client(client_options);
        let incoming_pub_dispatcher = Arc::new(Mutex::new(IncomingPublishDispatcher::default()));

        Ok(Self {
            client,
            // CONSIDER: Ideally, receiver would not be Option, but it's done this way to keep the borrow checker happy.
            // The more correct solution is to add internal substructs to Session that allow mutability to be scoped better.
            receiver: Some(receiver),
            connect_handle: Some(connect_handle),
            disconnect_handle: Arc::new(Mutex::new(None)),
            reauth_handle: None,
            connect_parameters,
            client_id,
            incoming_pub_dispatcher,
            reconnect_policy: options.reconnect_policy,
            enhanced_auth_policy,
            state: Arc::new(SessionState::default()),
            notify_force_exit: Arc::new(Notify::new()),
        })
    }

    /// Return a new instance of [`SessionExitHandle`] that can be used to end this [`Session`]
    pub fn create_exit_handle(&self) -> SessionExitHandle {
        SessionExitHandle {
            disconnect_handle: Arc::downgrade(&self.disconnect_handle),
            force_exit: self.notify_force_exit.clone(),
        }
    }

    /// Return a new instance of [`SessionMonitor`] that can be used to monitor the session's state
    pub fn create_session_monitor(&self) -> SessionMonitor {
        SessionMonitor {
            state: self.state.clone(),
        }
    }

    /// Return a new instance of [`SessionManagedClient`] that can be used to send and receive messages
    pub fn create_managed_client(&self) -> SessionManagedClient {
        SessionManagedClient {
            client_id: self.client_id.clone(),
            client: self.client.clone(),
            dispatcher: self.incoming_pub_dispatcher.clone(),
        }
    }

    /// Begin running the [`Session`].
    ///
    /// Consumes the [`Session`] and blocks until either a session exit or a fatal connection
    /// error is encountered.
    ///
    /// # Errors
    /// Returns a [`SessionError`] if the session encounters a fatal error and ends.
    ///
    /// # Panics
    /// Panics if internal state is invalid (this should not be possible)
    pub async fn run(mut self) -> Result<(), SessionError> {
        // NOTE: This task does not need to be cleaned up. It exits gracefully on its own,
        // without the need for explicit cancellation after Session is dropped at the end
        // of this method.
        let receive_jh = tokio::task::spawn(Session::receive(
            self.receiver
                .take()
                .expect("Receiver should always be present at start of run"),
            self.incoming_pub_dispatcher.clone(),
        ));

        // NOTE: We have to clone this to access it after we send the rest of `self` into
        // the connection runner task.
        // Consider factoring out connection-related components into their own substruct
        // to avoid this pattern, and some others (e.g. semantically odd Option fields, etc.)
        let notify_force_exit = self.notify_force_exit.clone();

        tokio::select! {
            res = self.connection_runner() => {
                res
            }
            _ = receive_jh => {
                unreachable!("Receive task is not able to exit")
            }
            () = notify_force_exit.notified() => {
                log::info!("Exiting Session non-gracefully due to application-issued force exit command");
                log::info!("Note that the MQTT server may still retain the MQTT session");
                Err(SessionErrorKind::ForceExit.into())
            }
        }
    }

    /// Keeps the connection alive until exit by session loss or reconnect policy halt.
    async fn connection_runner(&mut self) -> Result<(), SessionError> {
        let mut clean_start = self.connect_parameters.initial_clean_start;
        let mut prev_connected = false;
        let mut prev_reconnection_attempts = 0;
        loop {
            log::debug!("Attempting to connect MQTT session (clean_start={clean_start})");
            let connection_transport_config = self
                .connect_parameters
                .connection_transport_config()
                .map_err(|e| SessionError {
                    kind: SessionErrorKind::Config,
                    source: Some(Box::new(e)),
                })?;

            let (connection, connack) =
                match self.connect(connection_transport_config, clean_start).await {
                    Ok((connection, connack)) => (connection, connack),
                    Err(e) => {
                        log::warn!("Failed to connect MQTT session: {e:?}");
                        prev_reconnection_attempts += 1;

                        if let Some(delay) = self
                            .reconnect_policy
                            .connect_failure_reconnect_delay(prev_reconnection_attempts, &e)
                        {
                            log::debug!("Retrying connect in {delay:?}...");
                            tokio::time::sleep(delay).await;
                            continue;
                        }
                        log::info!("Reconnect policy has halted reconnection attempts");
                        log::info!("Exiting Session due to reconnection halt");
                        return Err(SessionErrorKind::ReconnectHalted.into());
                    }
                };

            // Check to see if the MQTT session has been lost
            if !connack.session_present && prev_connected {
                // TODO: try and disconnect here?
                log::info!("MQTT session not present on connection");
                log::info!("Exiting Session due to MQTT session loss");
                return Err(SessionErrorKind::SessionLost.into());
            }

            self.state.transition_connected();

            // Indicate we have established a connection at least once, and will now attempt
            // to maintain this MQTT session.
            clean_start = false;
            prev_connected = true;
            prev_reconnection_attempts = 0;

            let reauth_jh = if let Some(enhanced_auth_policy) = &self.enhanced_auth_policy {
                Some(tokio::task::spawn(Session::reauth_monitor(
                    enhanced_auth_policy.clone(),
                    self.reauth_handle.take().expect(
                        "ReauthHandle should always be present after connect with EnhancedAuthPolicy",
                    ),
                )))
            } else {
                None
            };

            let (connect_handle, disconnected_event) = connection.run_until_disconnect().await;
            self.connect_handle = Some(connect_handle);
            *self.disconnect_handle.lock().unwrap() = None;
            self.reauth_handle = None;
            self.state.transition_disconnected();
            if let Some(reauth_jh) = reauth_jh {
                reauth_jh.abort();
            }
            let connection_loss = match disconnected_event {
                // User-initiated disconnect with exit handle
                DisconnectedEvent::ApplicationDisconnect => {
                    log::info!("Exiting Session gracefully due to application-issued exit command");
                    return Ok(());
                }
                DisconnectedEvent::ServerDisconnect(disconnect) => {
                    ConnectionLossReason::DisconnectByServer(disconnect)
                }
                DisconnectedEvent::PingTimeout => ConnectionLossReason::PingTimeout,
                DisconnectedEvent::IoError(io_err) => ConnectionLossReason::IoError(io_err),
                DisconnectedEvent::ProtocolError(proto_err) => {
                    ConnectionLossReason::ProtocolError(proto_err)
                }
            };
            if let Some(delay) = self
                .reconnect_policy
                .connection_loss_reconnect_delay(&connection_loss)
            {
                log::debug!("Reconnecting in {delay:?}...");
                tokio::time::sleep(delay).await;
            } else {
                log::info!("Reconnect policy has halted reconnection attempts");
                log::info!("Exiting Session due to reconnection halt");
                return Err(SessionErrorKind::ReconnectHalted.into());
            }
        }
    }

    /// Helper for connecting
    async fn connect(
        &mut self,
        connection_transport: ConnectionTransportConfig,
        clean_start: bool,
    ) -> Result<(Connection, ConnAck), azure_mqtt::error::ConnectError> {
        let ch = self
            .connect_handle
            .take()
            .expect("ConnectHandle should always be present for connect attempt");

        if let Some(authentication_info) = self
            .enhanced_auth_policy
            .as_ref()
            .map(|ap| ap.authentication_info())
        {
            log::debug!("Using enhanced authentication for MQTT connect");
            match ch
                .connect_enhanced_auth(
                    connection_transport,
                    clean_start,
                    self.connect_parameters.keep_alive,
                    self.connect_parameters.will.clone(),
                    self.connect_parameters.username.clone(),
                    self.connect_parameters.password.clone(),
                    self.connect_parameters.connect_properties.clone(),
                    authentication_info,
                    Some(self.connect_parameters.connection_timeout),
                )
                .await
            {
                ConnectEnhancedAuthResult::Continue(..) => {
                    // TODO: Implement this anyway
                    unimplemented!("This should not occur in AIO MQTT scenarios")
                }
                ConnectEnhancedAuthResult::Success(
                    connection,
                    connack,
                    disconnect_handle,
                    reauth_handle,
                ) => {
                    self.disconnect_handle
                        .lock()
                        .unwrap()
                        .replace(disconnect_handle);
                    self.reauth_handle.replace(reauth_handle);
                    Ok((connection, connack))
                }
                ConnectEnhancedAuthResult::Failure(connect_handle, connect_error) => {
                    self.connect_handle.replace(connect_handle);
                    Err(connect_error)
                }
            }
        } else {
            log::debug!("Using standard authentication for MQTT connect");
            match ch
                .connect(
                    connection_transport,
                    clean_start,
                    self.connect_parameters.keep_alive,
                    self.connect_parameters.will.clone(),
                    self.connect_parameters.username.clone(),
                    self.connect_parameters.password.clone(),
                    self.connect_parameters.connect_properties.clone(),
                    Some(self.connect_parameters.connection_timeout),
                )
                .await
            {
                ConnectResult::Success(connection, connack, disconnect_handle) => {
                    self.disconnect_handle
                        .lock()
                        .unwrap()
                        .replace(disconnect_handle);
                    Ok((connection, connack))
                }
                ConnectResult::Failure(connect_handle, connect_error) => {
                    self.connect_handle = Some(connect_handle);
                    Err(connect_error)
                }
            }
        }
    }

    /// Receive incoming PUBLISH packets and dispatch them to receivers.
    async fn receive(
        mut receiver: azure_mqtt::client::Receiver,
        dispatcher: Arc<Mutex<IncomingPublishDispatcher>>,
    ) {
        while let Some((publish, manual_ack)) = receiver.recv().await {
            // Dispatch the message to receivers
            if dispatcher
                .lock()
                .unwrap()
                .dispatch_publish(&publish, manual_ack)
                == 0
            {
                // If there are no valid dispatch targets, the publish was auto-acked.
                match publish.qos {
                    azure_mqtt::packet::DeliveryQoS::AtMostOnce => {
                        log::warn!(
                            "No matching receivers for PUBLISH received at QoS 0. Discarding."
                        );
                    }
                    azure_mqtt::packet::DeliveryQoS::AtLeastOnce(delivery_info)
                    | azure_mqtt::packet::DeliveryQoS::ExactlyOnce(delivery_info) => {
                        log::warn!(
                            "No matching receivers for PUBLISH with PKID {}. Auto-acked PUBLISH.",
                            delivery_info.packet_identifier
                        );
                    }
                }
            }
        }
    }

    /// Perform MQTT enhanced auth reauthentication as dictated by the `EnhancedAuthPolicy`.
    /// This function runs indefinitely and must be cancelled upon MQTT client disconnect.
    async fn reauth_monitor(
        enhanced_auth_policy: Arc<dyn EnhancedAuthPolicy>,
        reauth_handle: azure_mqtt::client::ReauthHandle,
    ) {
        loop {
            log::debug!("Waiting for reauthentication notification from EnhancedAuthPolicy...");
            let auth_data = enhanced_auth_policy.reauth_notified().await;
            log::debug!("EnhancedAuthPolicy indicates reauthentication is required. Attempting...");

            let mut result = if let Ok(ct) = reauth_handle
                .reauth(auth_data, AuthProperties::default())
                .await
            {
                if let Ok(r) = ct.await {
                    r
                } else {
                    log::warn!("Reauth operation cancelled. Exiting reauthentication monitor.");
                    // NOTE: This only could really happen if an MQTT disconnect happened while
                    // waiting for the reauth response. Completely harmless.
                    return;
                }
            } else {
                log::warn!("Reauth handle detached. Exiting reauthentication monitor.");
                // NOTE: This only could really happen if an MQTT disconnect AND a reauth notification
                // occur at the same time, which is extremely unlikely, and also completely harmless.
                return;
            };

            loop {
                match result {
                    ReauthResult::Continue(auth, reauth_token) => {
                        log::debug!("Reauth requires additional steps");
                        let auth_data = enhanced_auth_policy.auth_challenge(&auth);

                        result = if let Ok(ct) = reauth_token
                            .continue_reauth(auth_data, AuthProperties::default())
                            .await
                        {
                            if let Ok(r) = ct.await {
                                r
                            } else {
                                log::warn!(
                                    "Reauth operation cancelled. Exiting reauthentication monitor."
                                );
                                // NOTE: This only could really happen if an MQTT disconnect happened while
                                // waiting for the reauth response. Completely harmless.
                                return;
                            }
                        } else {
                            log::warn!("Reauth handle detached. Exiting reauthentication monitor.");
                            // NOTE: This only could really happen if an MQTT disconnect AND a reauth notification
                            // occur at the same time, which is extremely unlikely, and also completely harmless.
                            return;
                        };
                    }
                    ReauthResult::Success(_) => {
                        log::debug!("Reauthentication successful.");
                        break;
                    }
                    ReauthResult::Failure => {
                        log::warn!("Reauthentication failed");
                        log::warn!(
                            "Server expected to close the connection due to reauthentication failure."
                        );
                        break;
                    }
                }
            }
        }
    }
}
/// Handle used to end an MQTT session.
#[derive(Clone)]
pub struct SessionExitHandle {
    /// The disconnector used to issue disconnect requests
    disconnect_handle: Weak<Mutex<Option<azure_mqtt::client::DisconnectHandle>>>,
    /// Notifier for force exit
    force_exit: Arc<Notify>,
}

impl SessionExitHandle {
    /// Attempt to gracefully end the MQTT session running in the [`Session`] that created this handle.
    /// This will cause the [`Session::run()`] method to return.
    ///
    /// Note that a graceful exit requires the [`Session`] to be connected to the server.
    ///
    /// # Errors
    /// * [`SessionExitError`] of kind [`SessionExitErrorKind::Detached`] if the Session no longer exists.
    /// * [`SessionExitError`] of kind [`SessionExitErrorKind::ServerUnavailable`] if the Session is not connected to the server.
    ///
    /// # Panics
    /// Panics if internal state is invalid (this should not be possible).
    pub fn try_exit(&self) -> Result<(), SessionExitError> {
        self.disconnect_handle
            .upgrade()
            // Unable to upgrade weak reference -> Session has been detached
            .ok_or(SessionExitError {
                kind: SessionExitErrorKind::Detached,
            })?
            .lock()
            .unwrap()
            .take()
            // No disconnect handle -> Already disconnected
            .ok_or(SessionExitError {
                kind: SessionExitErrorKind::ServerUnavailable,
            })?
            .disconnect(&DisconnectProperties {
                session_expiry_interval: Some(SessionExpiryInterval::Duration(0)),
                ..Default::default()
            })
            // NOTE: This error is likely impossible because we already were able to take the
            // disconnect handle from the weak reference, meaning the Session (and the client
            // inside it has not dropped
            .or(Err(SessionExitError {
                kind: SessionExitErrorKind::Detached,
            }))
    }

    /// Forcefully end the MQTT session running in the [`Session`] that created this handle.
    /// This will cause the [`Session::run()`] method to return.
    ///
    /// The [`Session`] will attempt a graceful exit before forcing the exit.
    /// If the exit is forced, the server will not be aware the MQTT session has ended.
    ///
    /// Returns true if the exit was graceful, and false if the exit was forced.
    #[allow(clippy::must_use_candidate)]
    pub fn force_exit(&self) -> bool {
        log::debug!("Attempting to exit session gracefully");
        match self.try_exit() {
            Ok(()) => {
                log::debug!("Session exited gracefully");
                true
            }
            Err(e) => {
                if e.kind() == SessionExitErrorKind::Detached {
                    log::debug!("Session already detached, no action needed");
                } else {
                    log::debug!("Session not connected, forcing exit immediately");
                    self.force_exit.notify_one();
                }
                false
            }
        }
    }
}

/// Monitor for session state changes in the [`Session`].
///
/// This is largely for informational purposes.
#[derive(Clone)]
pub struct SessionMonitor {
    state: Arc<SessionState>,
}

impl SessionMonitor {
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
