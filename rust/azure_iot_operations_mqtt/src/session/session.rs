// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

//! Internal implementation of [`Session`] and [`SessionExitHandle`].

use std::fs;
use std::sync::{Arc, Mutex};
use std::time::Duration;

use tokio::sync::Notify;
use tokio_util::sync::CancellationToken;

use crate::MqttConnectionSettings;
use crate::auth::{self, SatAuthContext};
use crate::session::managed_client::SessionManagedClient;
use crate::session::receiver::{IncomingPublishDispatcher, PublishReceiverManager};
use crate::session::reconnect_policy::{ExponentialBackoffWithJitter, ReconnectPolicy};
use crate::session::state::SessionState;
use crate::session::{
    SessionConfigError, SessionError, SessionErrorRepr, SessionExitError, SessionExitErrorKind,
};

/// Options for configuring a new [`Session`]
#[derive(Builder)]
#[builder(pattern = "owned")]
pub struct SessionOptions {
    /// MQTT Connection Settings for configuring the [`Session`]
    connection_settings: MqttConnectionSettings,
    /// Reconnect Policy to by used by the `Session`
    #[builder(default = "Box::new(ExponentialBackoffWithJitter::default())")]
    reconnect_policy: Box<dyn ReconnectPolicy>,
    /// Maximum number of queued outgoing messages not yet accepted by the MQTT Session
    #[builder(default = "100")]
    outgoing_max: usize,
    /// Indicates if the Session should use features specific for use with the AIO MQTT Broker
    #[builder(default = "Some(AIOBrokerFeaturesBuilder::default().build().unwrap())")]
    aio_broker_features: Option<AIOBrokerFeatures>,
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
    receiver: azure_mqtt::client::Receiver,
    /// Underlying MQTT connect handle
    connect_handle: Option<azure_mqtt::client::ConnectHandle>, // TODO: think about making an enum for this tied to session state to make it clearer that None is the state when this is owned somewhere else
    connect_properties: azure_mqtt::packet::ConnectProperties,
    auth_info: Option<azure_mqtt::packet::AuthenticationInfo>,
    clean_start: bool,
    connection_transport_config: azure_mqtt::client::ConnectionTransportConfig,
    /// Client ID of the underlying rumqttc client
    client_id: String,
    /// File path to the SAT token
    sat_file: Option<String>,
    /// Manager for the receivers of the Session
    receiver_manager: Arc<Mutex<PublishReceiverManager>>,
    /// Receiver dispatcher for incoming publishes
    incoming_pub_dispatcher: IncomingPublishDispatcher<azure_mqtt::client::Client>,
    /// Reconnect policy
    reconnect_policy: Box<dyn ReconnectPolicy>,
    /// Current state
    state: Arc<SessionState>,
    /// disconnect handle
    disconnect_handle: Option<azure_mqtt::client::DisconnectHandle>,
}

impl Session {
    /// Create a new [`Session`] with the provided options structure.
    ///
    /// # Errors
    /// Returns a [`SessionConfigError`] if there are errors using the session options.
    pub fn new(options: SessionOptions) -> Result<Self, SessionConfigError> {
        let client_id = options.connection_settings.client_id.clone();
        let sat_file = options.connection_settings.sat_file.clone();

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

        let client_options: azure_mqtt::client::ClientOptions =
            options.connection_settings.try_into()?;
        let connect_properties: azure_mqtt::packet::ConnectProperties =
            options.connection_settings.try_into()?;
        let auth_info: Option<azure_mqtt::packet::AuthenticationInfo> =
            options.connection_settings.try_into()?;
        let connection_transport_config: azure_mqtt::client::ConnectionTransportConfig =
            options.connection_settings.try_into()?;

        let (client, connect_handle, receiver) = crate::az_mqtt_adapter::client(
            // options.connection_settings,
            client_options,
            options.outgoing_max,
            true,
            user_properties,
        )?;
        Ok(Self::new_from_injection(
            client,
            receiver,
            connect_handle,
            connect_properties,
            auth_info,
            connection_transport_config,
            options.connection_settings.clean_start,
            options.reconnect_policy,
            client_id,
            sat_file,
        ))
    }

    pub fn new_for_tests(
        options: SessionOptions,
        connection_transport_config: azure_mqtt::client::ConnectionTransportConfig,
    ) -> Result<Self, SessionConfigError> {
        let client_id = options.connection_settings.client_id.clone();
        let sat_file = options.connection_settings.sat_file.clone();

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

        let client_options: azure_mqtt::client::ClientOptions =
            options.connection_settings.try_into()?;
        let connect_properties: azure_mqtt::packet::ConnectProperties =
            options.connection_settings.try_into()?;
        let auth_info: Option<azure_mqtt::packet::AuthenticationInfo> =
            options.connection_settings.try_into()?;
        // let connection_transport_config: azure_mqtt::client::ConnectionTransportConfig = options.connection_settings.try_into()?;

        let (client, connect_handle, receiver) = crate::az_mqtt_adapter::client(
            // options.connection_settings,
            client_options,
            options.outgoing_max,
            true,
            user_properties,
        )?;
        Ok(Self::new_from_injection(
            client,
            receiver,
            connect_handle,
            connect_properties,
            auth_info,
            connection_transport_config,
            options.connection_settings.clean_start,
            options.reconnect_policy,
            client_id,
            sat_file,
        ))
    }

    // TODO: get client id out of here
    // TODO: can we eliminate need for box on input?
    /// ----API NOT STABLE, INTERNAL USE ONLY FOR NOW----
    pub fn new_from_injection(
        client: azure_mqtt::client::Client,
        receiver: azure_mqtt::client::Receiver,
        connect_handle: azure_mqtt::client::ConnectHandle,
        connect_properties: azure_mqtt::packet::ConnectProperties,
        auth_info: Option<azure_mqtt::packet::AuthenticationInfo>,
        connection_transport_config: azure_mqtt::client::ConnectionTransportConfig,
        clean_start: bool,
        reconnect_policy: Box<dyn ReconnectPolicy>,
        client_id: String,
        sat_file: Option<String>,
    ) -> Self {
        let incoming_pub_dispatcher = IncomingPublishDispatcher::new(client.clone());
        let receiver_manager = incoming_pub_dispatcher.get_receiver_manager();

        Self {
            client,
            receiver,
            connect_handle: Some(connect_handle),
            connect_properties,
            auth_info,
            connection_transport_config,
            clean_start,
            client_id,
            sat_file,
            receiver_manager,
            incoming_pub_dispatcher,
            reconnect_policy,
            state: Arc::new(SessionState::default()),
            disconnect_handle: None,
        }
    }

    /// Return a new instance of [`SessionExitHandle`] that can be used to end this [`Session`]
    pub fn create_exit_handle(&self) -> SessionExitHandle {
        SessionExitHandle {
            disconnector: self.client.clone(),
            state: self.state.clone(),
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
            receiver_manager: self.receiver_manager.clone(),
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
        self.state.transition_running();
        let mut authentication_info = None;
        if let Some(sat_file) = &self.sat_file {
            // Read the SAT auth file
            let auth_data = match fs::read(sat_file) {
                Ok(sat_auth_data) => {
                    // Set the authentication data
                    sat_auth_data.into()
                }
                Err(e) => {
                    log::error!("Cannot read SAT auth file: {sat_file}");
                    // TODO: This should happen in the auth module, it should be fixed in the future.
                    return Err(SessionErrorRepr::IoError(e))?;
                }
            };
            authentication_info = Some(azure_mqtt::packet::AuthenticationInfo {
                method: auth::SAT_AUTHENTICATION_METHOD.to_string(),
                data: Some(auth_data),
            });
        }

        // TODO: not sure if we need some of this still
        // // Background tasks
        // let cancel_token = CancellationToken::new();
        // tokio::spawn({
        //     let cancel_token = cancel_token.clone();
        //     let client = self.client.clone();
        //     run_background(client, sat_auth_context, cancel_token)
        // });

        // Indicates whether this session has been previously connected
        let mut prev_connected = false;
        // Number of previous reconnect attempts
        let mut prev_reconnect_attempts = 0;
        // Return value for the session indicating reason for exit
        let mut result = Ok(());

        // Handle events
        loop {
            let (connection, connack, disconnect_handle) =
                if let Some(authentication_info) = authentication_info {
                    unimplemented!()
                    // self.connect_handle.connect_with_auth(
                    // log::debug!("Incoming AUTH: {auth:?}");
                    //
                    // if let Some(sat_auth_tx) = &sat_auth_tx {
                    //     // Notify the background task that the auth data has changed
                    //     // TODO: This is a bit of a hack, but it works for now. Ideally, the reauth
                    //     // method on rumqttc would return a completion token and we could use that
                    //     // in the background task to know when the reauth is complete.
                    //     match sat_auth_tx.send(auth.code) {
                    //         Ok(()) => {}
                    //         Err(e) => {
                    //             // This should never happen unless the background task has exited
                    //             // in which case the session is already in a bad state and we should
                    //             // have already exited.
                    //             log::error!("Error sending auth code to SAT auth task: {e:?}");
                    //         }
                    //     }
                    // }
                } else {
                    // TODO: handle failure once that's implemented on mqtt client
                    self.connect_handle
                        .take()
                        .unwrap()
                        .connect(
                            self.connection_transport_config.clone(),
                            self.connect_properties,
                        )
                        .await
                };
            // Update connection state
            self.state.transition_connected();
            // Reset the counter on reconnect attempts
            prev_reconnect_attempts = 0;
            log::debug!("Incoming CONNACK: {connack:?}");
            // If the session is not present after a reconnect, end the session.
            if prev_connected && !connack.session_present {
                log::error!("Session state not present on broker after reconnect. Ending session.");
                result = Err(SessionErrorRepr::SessionLost);
                if self.state.desire_exit() {
                    // NOTE: this could happen if the user was exiting when the connection was dropped,
                    // while the Session was not aware of the connection drop. Then, the drop has to last
                    // long enough for the MQTT session expiry interval to cause the broker to discard the
                    // MQTT session, and thus, you would enter this case.
                    // NOTE: The reason that the misattribution of cause may occur in logs is due to the
                    // (current) loose matching of received disconnects on account of an rumqttc bug.
                    // See the error cases below in this match statement for more information.
                    log::debug!(
                        "Session-initiated exit triggered when user-initiated exit was already in-progress. There may be two disconnects, both attributed to Session"
                    );
                }
                // self.trigger_session_exit().await;
            }
            // Otherwise, connection was successful
            else {
                prev_connected = true;
                // Set clean start to false for subsequent connections
                self.clean_start = false;
            }

            self.disconnect_handle = Some(disconnect_handle);
            tokio::select! {
                (new_connect_handle, disconnect_event, session_ended) = Self::connection_runner(connection) => {
                    self.state.transition_disconnected();
                    self.connect_handle = Some(new_connect_handle);
                    if session_ended {
                        break;
                    }
                }
                _ = self.receive() => {
                    // do anything here? If this ends, it should mean that the client has been dropped
                }
            }
        }

        // TODO: this is what was in the original run that hasn't been handled yet. Ensure these scenarios are handled and then remove
        // Poll the next event/error unless a force exit occurs.
        // let next = tokio::select! {
        //     // Ensure that the force exit signal is checked first.
        //     biased;
        //     () = self.notify_force_exit.notified() => { break },
        //     next = self.event_loop.poll() => { next },
        // };

        //     match next {
        //         Ok(_e) => {
        //             // There could be additional incoming and outgoing event responses here if
        //             // more filters like the above one are applied
        //         }

        //         // Desired disconnect completion
        //         // NOTE: This normally is StateError::ConnectionAborted, but rumqttc sometimes
        //         // can deliver something else in this case. For now, we'll accept any
        //         // MqttState variant when trying to disconnect.
        //         // TODO: However, this has the side-effect of falsely reporting disconnects that are the
        //         // result of network failure as client-side disconnects if there is an outstanding
        //         // DesireExit value. This is not harmful, but it is bad for logging, and should
        //         // probably be fixed.
        //         Err(ConnectionError::MqttState(_)) if self.state.desire_exit() => {
        //             self.state.transition_disconnected();
        //             break;
        //         }

        //         // Connection refused by broker - unrecoverable
        //         // NOTE: We carve out an exception for quota exceeded, as we wish to recover from that
        //         // NOTE: The carve-out does not actually work due to a bug in rumqttc where QuotaExceeded does not correctly surface.
        //         // Instead it surfaces as a deserialization error, which we cannot accurately match. This implementation exists here
        //         // as documentation of the desire for this behavior.
        //         Err(ConnectionError::ConnectionRefused(rc))
        //             if !matches!(rc, ConnectReturnCode::QuotaExceeded) =>
        //         {
        //             log::error!("Connection Refused: rc: {rc:?}");
        //             result = Err(SessionErrorRepr::ConnectionError(next.unwrap_err()));
        //             break;
        //         }

        //         // Other errors are passed to reconnect policy
        //         Err(e) => {
        //             self.state.transition_disconnected();

        //             // Always log the error itself at error level
        //             log::error!("Error: {e:?}");

        //             // Defer decision to reconnect policy
        //             if let Some(delay) = self
        //                 .reconnect_policy
        //                 .next_reconnect_delay(prev_reconnect_attempts, &e)
        //             {
        //                 log::info!("Attempting reconnect in {delay:?}");
        //                 // Wait for either the reconnect delay time, or a force exit signal
        //                 tokio::select! {
        //                     () = tokio::time::sleep(delay) => {}
        //                     () = self.notify_force_exit.notified() => {
        //                         log::info!("Reconnect attempts halted by force exit");
        //                         result = Err(SessionErrorRepr::ForceExit);
        //                         break;
        //                     }
        //                 }
        //             } else {
        //                 log::info!("Reconnect attempts halted by reconnect policy");
        //                 result = Err(SessionErrorRepr::ReconnectHalted);
        //                 break;
        //             }
        //             prev_reconnect_attempts += 1;
        //         }
        //     }
        // }
        self.state.transition_exited();
        // cancel_token.cancel();
        result.map_err(std::convert::Into::into)
    }

    /// Helper for triggering a session exit and logging the result
    fn trigger_session_exit(&self) {
        let exit_handle = self.create_exit_handle();
        match exit_handle.trigger_exit_internal() {
            Ok(()) => log::debug!("Internal session exit successful"),
            Err(e) => log::debug!("Internal session exit failed: {e:?}"),
        }
    }

    ///  returns new connect handle, disconnected event, and whether session has ended or not
    async fn connection_runner(
        connection: azure_mqtt::client::Connection,
    ) -> (
        azure_mqtt::client::ConnectHandle,
        azure_mqtt::client::DisconnectedEvent,
        bool,
    ) {
        let (connect_handle, disconnected_event) = connection.run_until_disconnect().await;
        let session_ended = match disconnected_event {
            azure_mqtt::client::DisconnectedEvent::Transport => {
                false // can't know, must connect again to find out
            }
            azure_mqtt::client::DisconnectedEvent::UserRequested => true,
            azure_mqtt::client::DisconnectedEvent::ServerRequested(disconnect) => {
                disconnect.properties.session_expiry_interval == 0
            }
        };
        (connect_handle, disconnected_event, session_ended)
    }

    /// TODO: acking here needs to be adjusted
    async fn receive(&mut self) {
        loop {
            while let Some((publish, ack_handle)) = self.receiver.recv().await {
                log::debug!("Incoming PUB: {publish:?}");
                // Dispatch the message to receivers
                match self.incoming_pub_dispatcher.dispatch_publish(&publish) {
                    Ok(_) => {}
                    Err(e) => {
                        // If the dispatch fails, we must be responsible for acking.
                        // However, failure here should never happen in valid MQTT scenarios.
                        match publish.qos {
                            azure_mqtt::packet::DeliveryQoS::AtLeastOnce(delivery_info)
                            | azure_mqtt::packet::DeliveryQoS::ExactlyOnce(delivery_info) => {
                                log::error!(
                                    "Could not dispatch PUB with PKID {}. Will be auto-acked. Reason: {e:?}",
                                    delivery_info.packet_identifier
                                );
                                log::warn!(
                                    "Auto-ack of PKID {} may not be correctly ordered",
                                    delivery_info.packet_identifier
                                );
                                match ack_handle {
                                    azure_mqtt::client::AckHandle::QoS0 => todo!(),
                                    azure_mqtt::client::AckHandle::QoS1(pub_ack_token) => {
                                        tokio::spawn({
                                            // let acker = self.client.clone();
                                            async move {
                                                match pub_ack_token.accept(azure_mqtt::packet::PubAckProperties::default()).await {
                                                    Ok(ct) => {
                                                        let _ = ct.await;
                                                        log::debug!(
                                                            "Auto-ack for failed dispatch PKID {} successful",
                                                            delivery_info.packet_identifier
                                                        );
                                                    }
                                                    Err(e) => {
                                                        log::error!(
                                                            "Auto-ack for failed dispatch PKID {} failed: {e:?}",
                                                            delivery_info.packet_identifier
                                                        );
                                                    }
                                                }
                                            }
                                        });
                                    }
                                    azure_mqtt::client::AckHandle::QoS2(pub_rec_token) => {
                                        tokio::spawn({
                                            // let acker = self.client.clone();
                                            async move {
                                                match pub_rec_token.accept(azure_mqtt::packet::PubRecProperties::default()).await {
                                                    Ok(ct) => {
                                                        let _ = ct.await;
                                                        log::debug!(
                                                            "Auto-ack for failed dispatch PKID {} successful",
                                                            delivery_info.packet_identifier
                                                        );
                                                    }
                                                    Err(e) => {
                                                        log::error!(
                                                            "Auto-ack for failed dispatch PKID {} failed: {e:?}",
                                                            delivery_info.packet_identifier
                                                        );
                                                    }
                                                }
                                            }
                                        });
                                    }
                                }
                            }
                            azure_mqtt::packet::DeliveryQoS::AtMostOnce => {
                                // No ack needed for QoS 0
                                log::error!("Could not dispatch QoS 0 PUB. Reason: {e:?}");
                            }
                        }
                    }
                }
            }
        }
    }
}

/// Run background tasks for [`Session.run()`]
async fn run_background(
    client: impl MqttClient + Clone,
    sat_auth_context: Option<SatAuthContext>,
    cancel_token: CancellationToken,
) {
    /// Maintain the SAT token authentication by renewing it when the SAT file changes
    async fn maintain_sat_auth(mut sat_auth_context: SatAuthContext, client: impl MqttClient) -> ! {
        let mut retrying = false;
        loop {
            // Wait for the SAT file to change if not retrying
            if !retrying {
                sat_auth_context.notified().await;
            }

            // Re-authenticate the client
            if sat_auth_context
                .reauth(Duration::from_secs(10), &client)
                .await
                .is_ok()
            {
                log::debug!("SAT token renewed successfully");
                // Drain the notification so we don't re-auth again for a prior change to the SAT file
                sat_auth_context.drain_notify().await;
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
            () = maintain_sat_auth(sat_auth_context, client) => {
                log::error!("`maintain_sat_auth` task ended unexpectedly.");
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
pub struct SessionExitHandle {
    /// The disconnector used to issue disconnect requests
    disconnector: azure_mqtt::client::DisconnectHandle,
    /// Session state information
    state: Arc<SessionState>,
    /// Notifier for force exit
    force_exit: Arc<Notify>,
}

impl SessionExitHandle {
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
    /// * [`SessionExitError`] of kind [`SessionExitErrorKind::Detached`] if the Session no longer exists.
    /// * [`SessionExitError`] of kind [`SessionExitErrorKind::BrokerUnavailable`] if the Session is not connected to the broker.
    pub async fn try_exit(&self) -> Result<(), SessionExitError> {
        log::debug!("Attempting to exit session gracefully");
        // Check if the session has already exited
        if self.state.has_exited() {
            return Err(SessionExitError {
                attempted: false,
                kind: SessionExitErrorKind::Detached,
            });
        }

        // Check if the session is connected (to best of knowledge)
        if !self.state.is_connected() {
            return Err(SessionExitError {
                attempted: false,
                kind: SessionExitErrorKind::BrokerUnavailable,
            });
        }
        // Initiate the exit
        self.trigger_exit_user()?;
        // Wait for the exit to complete, or until the session realizes it was already disconnected.
        tokio::select! {
            // NOTE: Adding biased protects from the case where we called try_exit while connected
            // and because select alternates between the two branches below, we would return an error
            // when we should have returned Ok(()).
            biased;
            // NOTE: These two conditions here are functionally almost identical for now, due to the
            // very loose matching of disconnect events in [`Session::run()`] (as a result of bugs and
            // unreliable behavior in rumqttc). These would be less identical conditions if we tightened
            // that matching back up, and that's why they're here.
            () = self.state.condition_exited() => Ok(()),
            () = self.state.condition_disconnected() => Err(SessionExitError {
                attempted: true,
                kind: SessionExitErrorKind::BrokerUnavailable,
            }),
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
    /// will return an error.
    ///
    /// # Arguments
    /// * `timeout` - The duration to wait for the graceful exit to complete before returning an error.
    ///
    /// # Errors
    /// * [`SessionExitError`] of kind [`SessionExitErrorKind::Detached`] if the Session no longer exists.
    /// * [`SessionExitError`] of kind [`SessionExitErrorKind::BrokerUnavailable`] if the Session is not connected to the broker within the specified timeout interval.
    ///   within the timeout interval.
    pub async fn try_exit_timeout(&self, timeout: Duration) -> Result<(), SessionExitError> {
        tokio::time::timeout(timeout, self.try_exit())
            .await
            .map_err(|_| SessionExitError {
                attempted: true,
                kind: SessionExitErrorKind::BrokerUnavailable,
            })?
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
    fn trigger_exit_user(&self) -> Result<(), SessionExitError> {
        self.state.transition_user_desire_exit();
        // TODO: This doesn't actually end the MQTT session because rumqttc doesn't allow
        // us to manually set the session expiry interval to 0 on a reconnect.
        // Need to work with Shanghai to drive this feature.
        // TODO: set disconnect properties properly
        // TODO: set session expiry interval to 0
        self.disconnector
            .disconnect(&azure_mqtt::packet::DisconnectProperties::default())
    }

    /// Trigger a session exit, specifying the internal session logic as the issuer of the request
    fn trigger_exit_internal(&self) -> Result<(), SessionExitError> {
        self.state.transition_session_desire_exit();
        // TODO: This doesn't actually end the MQTT session because rumqttc doesn't allow
        // us to manually set the session expiry interval to 0 on a reconnect.
        // Need to work with Shanghai to drive this feature.
        // TODO: set session expiry interval to 0?
        self.disconnector
            .disconnect(&azure_mqtt::packet::DisconnectProperties::default())
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
