// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

//! MQTT client providing a managed connection with automatic reconnection across a single MQTT session.
//!
//! This module provides several key components for using an MQTT session:
//! * [`Session`] - Manages the lifetime of the MQTT session
//! * [`SessionManagedClient`] - Sends MQTT messages to the broker
//! * [`SessionPubReceiver`] - Receives MQTT messages from the broker
//! * [`SessionMonitor`] - Provides information about the MQTT session's state
//! * [`SessionExitHandle`] - Allows the user to exit the session gracefully
//!
//! # [`Session`] lifespan
//! Each instance of [`Session`] is single use - after configuring a [`Session`], and creating any
//! other necessary components from it, calling the [`run`](crate::session::Session::run) method
//! will consume the [`Session`] and block (asynchronously) until the MQTT session shared between
//! client and broker ends. Note that a MQTT session can span multiple connects and disconnects to
//! the broker.
//!
//! The MQTT session can be ended one of three ways:
//! 1. The MQTT broker ends the MQTT session
//! 2. The [`ReconnectPolicy`](crate::session::reconnect_policy::ReconnectPolicy) configured on the
//!    [`Session`] halts reconnection attempts, causing the [`Session`] to end the MQTT session.
//! 3. The user uses the [`SessionExitHandle`] to end the MQTT session.
//!    <div class="warning">The SessionExitHandle currently only causes the exit of the Session client
//!    not the end of the MQTT session shared with the broker. This limitation will be fixed in future
//!    updates.</div>
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
//! does not match any [`SessionPubReceiver`]s, it will be acknowledged to the MQTT broker and
//! discarded. Thus, in order to guarantee that messages will not be lost, you should create the
//! [`SessionPubReceiver`] *before* subscribing to the topic filter.

use std::{
    fmt,
    sync::{Arc, Mutex, Weak},
};

use azure_mqtt::{
    client::{
        ConnectEnhancedAuthResult, ConnectResult, Connection, DisconnectedEvent, ReauthResult,
    },
    packet::{AuthProperties, ConnAck, DisconnectProperties, SessionExpiryInterval},
};
use thiserror::Error;
use tokio::sync::Notify;

use crate::azure_mqtt_adapter as adapter;
use crate::error::{ClientError, ConnectError};
pub use crate::session::managed_client::{SessionManagedClient, SessionPubReceiver};
use crate::session::state::SessionState;
use crate::session::{
    auth_policy::{AuthPolicy, SatAuthFileMonitor},
    dispatcher::IncomingPublishDispatcher,
    reconnect_policy::{ConnectionLoss, ExponentialBackoffWithJitter, ReconnectPolicy},
};
#[cfg(feature = "test-utils")]
use crate::test_utils::InjectedPacketChannels;
use crate::{MqttConnectionSettings, azure_mqtt_adapter::AzureMqttConnectParameters};

mod auth_policy;
pub(crate) mod dispatcher;
mod managed_client;
pub(crate) mod plenary_ack;
pub mod reconnect_policy;
mod state;

/// Error describing why a [`Session`] ended prematurely
#[derive(Debug, Error)]
#[error(transparent)]
pub struct SessionError(#[from] SessionErrorRepr);

/// Internal error for [`Session`] runs.
#[derive(Error, Debug)]
enum SessionErrorRepr {
    /// MQTT session was lost due to a connection error.
    #[error("session state not present on broker after reconnect")]
    SessionLost,
    /// MQTT session was ended due to an unrecoverable connection error
    #[error(transparent)]
    ConnectionError(#[from] ConnectError),
    /// Reconnect attempts were halted by the reconnect policy, ending the MQTT session
    #[error("reconnection halted by reconnect policy")]
    ReconnectHalted,
    /// The [`Session`] was ended by a user-initiated force exit. The broker may still retain the MQTT session.
    #[error("session ended by force exit")]
    ForceExit,
    /// The [`Session`] was ended by an IO error.
    #[error("{0}")]
    IoError(#[from] std::io::Error),
}

/// Error configuring a [`Session`].
#[derive(Error, Debug)]
#[error(transparent)]
pub struct SessionConfigError(#[from] adapter::ConnectionSettingsAdapterError);

/// Error type for exiting a [`Session`] using the [`SessionExitHandle`].
#[derive(Error, Debug)]
#[error("{kind} (network attempt = {attempted})")]
pub struct SessionExitError {
    attempted: bool,
    kind: SessionExitErrorKind,
}

impl SessionExitError {
    /// Return the corresponding [`SessionExitErrorKind`] for this error
    #[must_use]
    pub fn kind(&self) -> SessionExitErrorKind {
        self.kind
    }

    /// Return whether a network attempt was made before the error occurred
    #[must_use]
    pub fn attempted(&self) -> bool {
        self.attempted
    }
}

impl From<ClientError> for SessionExitError {
    fn from(_err: ClientError) -> Self {
        SessionExitError {
            attempted: false,
            kind: SessionExitErrorKind::Detached,
        }
    }
}

/// An enumeration of categories of [`SessionExitError`]
#[derive(Debug, Clone, Copy, Eq, PartialEq)]
pub enum SessionExitErrorKind {
    /// The exit handle was detached from the session
    Detached,
    /// The broker could not be reached
    BrokerUnavailable,
}

impl fmt::Display for SessionExitErrorKind {
    fn fmt(&self, f: &mut fmt::Formatter<'_>) -> fmt::Result {
        match self {
            SessionExitErrorKind::Detached => {
                write!(f, "Detached from Session")
            }
            SessionExitErrorKind::BrokerUnavailable => write!(f, "Could not contact broker"),
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
    /// Maximum packet identifier
    #[builder(default = "azure_mqtt::packet::PacketIdentifier::MAX")]
    max_packet_identifier: azure_mqtt::packet::PacketIdentifier,
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

/// Options for configuring features on a [`Session`] that are specific to the AIO broker
#[derive(Builder)]
pub struct AIOBrokerFeatures {
    /// Indicates if the Session should use AIO persistence
    #[builder(default = "false")]
    persistence: bool,
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
    /// Client ID of the underlying rumqttc client
    client_id: String,
    /// Receiver dispatcher for incoming publishes
    incoming_pub_dispatcher: Arc<Mutex<IncomingPublishDispatcher>>,
    /// Reconnect policy
    reconnect_policy: Box<dyn ReconnectPolicy>,
    /// Enhanced authentication policy
    auth_policy: Option<Arc<dyn AuthPolicy>>,
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
    pub fn new(options: SessionOptions) -> Result<Self, SessionConfigError> {
        let client_id = options.connection_settings.client_id.clone();

        // Add AIO metric and features to user properties when using AIO MQTT broker features
        // TODO: consider user properties from being supported on SessionOptions or ConnectionSettings
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

        // Create AuthPolicy if SAT file is provided
        // TODO: This would ideally come directly from the SessionOptions instead of MQTT connection settings
        let auth_policy = if let Some(sat_file) = options.connection_settings.sat_file.as_ref() {
            Some(
                // TODO: This error should propagate, however currently `SessionConfigError` is tightly coupled to the
                // `azure_mqtt_adapter`, so it cannot.
                Arc::new(SatAuthFileMonitor::new(std::path::PathBuf::from(sat_file)).unwrap())
                    as Arc<dyn AuthPolicy>,
            )
        } else {
            None
        };

        let (client_options, connect_parameters) = options
            .connection_settings
            .to_azure_mqtt_connect_parameters(
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
            // TODO: Ideally, receiver would not be Option, but it's done this way to keep the borrow checker happy.
            // The more correct solution is to add internal substructs to Session that allow mutability to be scoped better.
            receiver: Some(receiver),
            connect_handle: Some(connect_handle),
            disconnect_handle: Arc::new(Mutex::new(None)),
            reauth_handle: None,
            connect_parameters,
            client_id,
            incoming_pub_dispatcher,
            reconnect_policy: options.reconnect_policy,
            auth_policy,
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
    pub async fn run(mut self) -> Result<(), SessionError> {
        // NOTE: This task does not need to be cleaned up. It exits gracefully on its own,
        // without the need for explicit cancellation after Session is dropped at the end
        // of this method.
        tokio::task::spawn(Session::receive(
            self.receiver
                .take()
                .expect("Receiver should always be present at start of run"),
            self.incoming_pub_dispatcher.clone(),
        ));

        let mut clean_start = self.connect_parameters.initial_clean_start;
        let mut prev_connected = false;
        let mut prev_reconnection_attempts = 0;
        loop {
            log::debug!("Attempting to connect MQTT session (clean_start={clean_start})");
            let (connection, connack) = match self.connect(clean_start).await {
                Ok((connection, connack)) => (connection, connack),
                Err(e) => {
                    log::debug!("Failed to connect MQTT session: {e:?}");
                    prev_reconnection_attempts += 1;
                    match self
                        .reconnect_policy
                        .connect_failure_reconnect_delay(prev_reconnection_attempts, &e)
                    {
                        Some(delay) => {
                            log::debug!("Retrying connect in {delay:?}...");
                            tokio::time::sleep(delay).await;
                            continue;
                        }
                        None => return Err(SessionErrorRepr::ReconnectHalted.into()),
                    }
                }
            };

            // Check to see if the MQTT session has been lost
            if !connack.session_present && prev_connected {
                // TODO: try and disconnect here?
                return Err(SessionErrorRepr::SessionLost.into());
            }

            log::debug!("Connected MQTT session: {connack:?}");
            self.state.transition_connected();

            // Indicate we have established a connection at least once, and will now attempt
            // to maintain this MQTT session.
            clean_start = false;
            prev_connected = true;
            prev_reconnection_attempts = 0;

            if let Some(auth_policy) = &self.auth_policy {
                tokio::task::spawn(Session::reauth_monitor(
                    auth_policy.clone(),
                    self.reauth_handle.take().expect(
                        "ReauthHandle should always be present after connect with AuthPolicy",
                    ),
                ));
            }

            let (connect_handle, disconnected_event) = connection.run_until_disconnect().await;
            self.connect_handle = Some(connect_handle);
            *self.disconnect_handle.lock().unwrap() = None;
            self.reauth_handle = None;
            self.state.transition_disconnected();
            let connection_loss = match disconnected_event {
                // User-initiated disconnect with exit handle
                // TODO: Is this truly the only way this happens? I think so, but double-check
                DisconnectedEvent::ApplicationDisconnect => return Ok(()),
                DisconnectedEvent::ServerDisconnect(disconnect) => {
                    ConnectionLoss::DisconnectByServer(disconnect)
                }
                DisconnectedEvent::IoError(io_err) => ConnectionLoss::IoError(io_err),
                DisconnectedEvent::ProtocolError(proto_err) => {
                    ConnectionLoss::ProtocolError(proto_err)
                } // TODO: how to handle force exit from exit handle?
            };
            match self
                .reconnect_policy
                .connection_loss_reconnect_delay(&connection_loss)
            {
                Some(delay) => tokio::time::sleep(delay).await,
                None => return Err(SessionErrorRepr::ReconnectHalted.into()),
            }
        }
    }

    /// Helper for connecting
    async fn connect(
        &mut self,
        clean_start: bool,
    ) -> Result<(Connection, ConnAck), azure_mqtt::error::ConnectError> {
        let ch = self
            .connect_handle
            .take()
            .expect("ConnectHandle should always be present for connect attempt");

        let result = if let Some(authentication_info) =
            self.auth_policy.as_ref().map(|ap| ap.authentication_info())
        {
            match ch.connect_enhanced_auth(
                    // TODO: maybe add something about certs expiring can fail this and why it's ok to panic? Or change this to not panic if it fails and instead end the session
                self.connect_parameters.connection_transport_config().expect("connection transport config has already been validated and inputs can't change"),
                clean_start,
                self.connect_parameters.keep_alive,
                self.connect_parameters.will.clone(),
                self.connect_parameters.username.clone(),
                self.connect_parameters.password.clone(),
                self.connect_parameters.connect_properties.clone(),
                authentication_info,
                Some(self.connect_parameters.connection_timeout),
            )
            .await {
                ConnectEnhancedAuthResult::Continue(..) => {
                    // TODO: Implement this anyway
                    unimplemented!("This should not occur in AIO MQTT scenarios")
                }
                ConnectEnhancedAuthResult::Success(
                    connection,
                    connack,
                    disconnect_handle,
                    reauth_handle
                ) => {
                    self.disconnect_handle
                        .lock()
                        .unwrap()
                        .replace(disconnect_handle);
                    self.reauth_handle.replace(reauth_handle);
                    Ok((connection, connack))
                },
                ConnectEnhancedAuthResult::Failure(connect_handle, connect_error) => {
                    self.connect_handle.replace(connect_handle);
                    Err(connect_error)
                }
            }
        } else {
            match ch.connect(
                // TODO: maybe add something about certs expiring can fail this and why it's ok to panic? Or change this to not panic if it fails and instead end the session
                self.connect_parameters.connection_transport_config().expect("connection transport config has already been validated and inputs can't change"),
                clean_start,
                self.connect_parameters.keep_alive,
                self.connect_parameters.will.clone(),
                self.connect_parameters.username.clone(),
                self.connect_parameters.password.clone(),
                self.connect_parameters.connect_properties.clone(),
                Some(self.connect_parameters.connection_timeout),
            )
            .await {
                ConnectResult::Success(
                    connection,
                    connack,
                    disconnect_handle,
                ) => {
                    self.disconnect_handle
                        .lock()
                        .unwrap()
                        .replace(disconnect_handle);
                    Ok((connection, connack))
                },
                ConnectResult::Failure(connect_handle, connect_error) => {
                    self.connect_handle = Some(connect_handle);
                    Err(connect_error)
                }
            }
        };
        result
    }

    async fn receive(
        mut receiver: azure_mqtt::client::Receiver,
        dispatcher: Arc<Mutex<IncomingPublishDispatcher>>,
    ) {
        while let Some((publish, manual_ack)) = receiver.recv().await {
            log::debug!("Incoming PUBLISH: {publish:?}");
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
                            "No matching receivers for PUBLISH recieved at QoS 0. Discarding."
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

    /// Perform MQTT enhanced auth reauthentication as dictated by the `AuthPolicy`.
    /// This function runs indefinitely and must be cancelled upon MQTT client disconnect.
    async fn reauth_monitor(
        auth_policy: Arc<dyn AuthPolicy>,
        reauth_handle: azure_mqtt::client::ReauthHandle,
    ) {
        loop {
            log::debug!("Waiting for reauthentication notification from AuthPolicy...");
            let auth_data = auth_policy.reauth_notified().await;
            log::debug!("AuthPolicy indicates reauthentication is required. Attempting...");

            let mut result = match reauth_handle
                .reauth(auth_data, AuthProperties::default())
                .await
            {
                Ok(ct) => {
                    match ct.await {
                        Ok(r) => r,
                        Err(_) => {
                            log::warn!(
                                "Reauth operation cancelled. Exiting reauthentication monitor."
                            );
                            // NOTE: This only could really happen if an MQTT disconnect happened while
                            // waiting for the reauth response. Completely harmless.
                            return;
                        }
                    }
                }
                Err(_) => {
                    log::warn!("Reauth handle detached. Exiting reauthentication monitor.");
                    // NOTE: This only could really happen if an MQTT disconnect AND a reauth notification
                    // occurr at the same time, which is extremely unlikely, and also completely harmless.
                    return;
                }
            };

            loop {
                match result {
                    ReauthResult::Continue(auth, reauth_token) => {
                        log::debug!("Reauth requires additional steps");
                        let auth_data = auth_policy.auth_challenge(&auth);
                        result = match reauth_token
                            .continue_reauth(auth_data, AuthProperties::default())
                            .await
                        {
                            Ok(ct) => {
                                match ct.await {
                                    Ok(r) => r,
                                    Err(_) => {
                                        log::warn!(
                                            "Reauth operation cancelled. Exiting reauthentication monitor."
                                        );
                                        // NOTE: This only could really happen if an MQTT disconnect happened while
                                        // waiting for the reauth response. Completely harmless.
                                        return;
                                    }
                                }
                            }
                            Err(_) => {
                                log::warn!(
                                    "Reauth handle detached. Exiting reauthentication monitor."
                                );
                                // NOTE: This only could really happen if an MQTT disconnect AND a reauth notification
                                // occurr at the same time, which is extremely unlikely, and also completely harmless.
                                return;
                            }
                        };
                        continue;
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
    force_exit: Arc<Notify>, // TODO: can this be a oneshot?
}

impl SessionExitHandle {
    /// Attempt to gracefully end the MQTT session running in the [`Session`] that created this handle.
    /// This will cause the [`Session::run()`] method to return.
    ///
    /// Note that a graceful exit requires the [`Session`] to be connected to the broker.
    ///
    /// # Errors
    /// * [`SessionExitError`] of kind [`SessionExitErrorKind::Detached`] if the Session no longer exists.
    /// * [`SessionExitError`] of kind [`SessionExitErrorKind::BrokerUnavailable`] if the Session is not connected to the broker.
    pub fn try_exit(&self) -> Result<(), SessionExitError> {
        log::debug!("Attempting to exit session gracefully");

        self.disconnect_handle
            .upgrade()
            // Unable to upgrade weak reference -> Session has been detached
            .ok_or(SessionExitError {
                attempted: false,
                kind: SessionExitErrorKind::Detached,
            })?
            .lock()
            .unwrap()
            .take()
            // No disconnect handle -> Already disconnected
            .ok_or(SessionExitError {
                attempted: false,
                kind: SessionExitErrorKind::BrokerUnavailable,
            })?
            .disconnect(&DisconnectProperties {
                session_expiry_interval: Some(SessionExpiryInterval::Duration(0)),
                ..Default::default()
            })
            // NOTE: This error is likely impossible because we already were able to take the
            // disconnect handle from the weak reference, meaning the Session (and the client
            // inside it has not dropped
            .or(Err(SessionExitError {
                attempted: false,
                kind: SessionExitErrorKind::Detached,
            }))

        // TODO: does the idea of an "attempted" exit even make sense anymore?
        // Should MQTT client wait for server to close connection?
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
        // TODO: once this is implemented, change METL tests back to using this instead of try_exit().unwrap()
        unimplemented!()
    }
}

/// Monitor for session state changes in the [`Session`].
///
/// This is largely for informational purposes.
#[derive(Clone)]
pub struct SessionMonitor {
    state: Arc<SessionState>, // TODO: should this be a weakref?
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
