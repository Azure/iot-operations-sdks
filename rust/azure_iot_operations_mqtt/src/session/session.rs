// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

//! Internal implementation of [`Session`] and [`SessionExitHandle`].

use std::sync::{Arc, Mutex, Weak};
use std::time::Duration;

use azure_mqtt::{
    client::{Connection, DisconnectedEvent, ConnectResult, ConnectEnhancedAuthResult},
    packet::{ConnAck, DisconnectProperties, SessionExpiryInterval},
};
use tokio::sync::Notify;
use tokio_util::sync::CancellationToken;

use crate::session::dispatcher::IncomingPublishDispatcher;
use crate::session::managed_client::SessionManagedClient;
use crate::session::reconnect_policy::{ExponentialBackoffWithJitter, ReconnectPolicy, ConnectionLoss};
use crate::session::state2::SessionState;
use crate::session::{
    SessionConfigError, SessionError, SessionErrorRepr, SessionExitError, SessionExitErrorKind,
};
use crate::{MqttConnectionSettings, azure_mqtt_adapter::AzureMqttConnectParameters};
use crate::{
    auth::SatAuthContext,
    //error::{ConnectionError, ConnectionErrorKind},
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

        let (client_options, connect_parameters) = options
            .connection_settings
            .to_azure_mqtt_connect_parameters(
                user_properties,
                options.max_packet_identifier,
                options.publish_qos0_queue_size,
                options.publish_qos1_qos2_queue_size,
            )?;

        let (client, connect_handle, receiver) = azure_mqtt::client::new_client(client_options);
        let incoming_pub_dispatcher = Arc::new(Mutex::new(IncomingPublishDispatcher::default()));

        Ok(Self {
            client,
            receiver: receiver,
            connect_handle: Some(connect_handle),
            disconnect_handle: Arc::new(Mutex::new(None)),
            reauth_handle: None,
            connect_parameters,
            client_id,
            incoming_pub_dispatcher,
            reconnect_policy: options.reconnect_policy,
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
        let mut clean_start = self.connect_parameters.initial_clean_start;
        let mut prev_connected = false;
        let mut prev_reconnection_attempts = 0;

        // TODO: Start monitoring SAT auth here, keeping the value updated.

        // TODO: run receiver? How to get it out of the struct? It's gotta be Option, right?
        // Ah damn, there's the matter of the dispatcher too....
        let receiver = self.receiver;

        let jh = tokio::task::spawn(receive(&receiver));

        loop {
            let (connection, connack) = match self.connect(clean_start).await {
                Ok((connection, connack)) => (connection, connack),
                Err(e) => {
                    prev_reconnection_attempts += 1;
                    match self.reconnect_policy.connect_failure_reconnect_delay(prev_reconnection_attempts, &e) {
                        Some(delay) => {
                            tokio::time::sleep(delay).await;
                            continue;
                        },
                        None => return Err(SessionErrorRepr::ReconnectHalted.into()),
                    }
                },
            };

            // Check to see if the MQTT session has been lost
            if !connack.session_present && prev_connected {
                // TODO: try and disconnect here?
                return Err(SessionErrorRepr::SessionLost.into())
            }

            // Indicate we have established a connection at least once, and will now attempt
            // to maintain this MQTT session.
            clean_start = false;
            prev_connected = true;
            prev_reconnection_attempts = 0;

            // TODO: start reauth task here

            let (connect_handle, disconnected_event) = connection.run_until_disconnect().await;
            self.connect_handle = Some(connect_handle);
            let connection_loss = match disconnected_event {
                // User-initiated disconnect with exit handle
                // TODO: Is this truly the only way this happens? I think so, but double-check
                DisconnectedEvent::ApplicationDisconnect => return Ok(()),
                DisconnectedEvent::ServerDisconnect(disconnect) => ConnectionLoss::DisconnectByServer(disconnect),
                DisconnectedEvent::IoError(io_err) => ConnectionLoss::IoError(io_err),
                DisconnectedEvent::ProtocolError(proto_err) => ConnectionLoss::ProtocolError(proto_err),
                // TODO: how to handle force exit from exit handle?
            };
            match self.reconnect_policy.connection_loss_reconnect_delay(&connection_loss) {
                Some(delay) => tokio::time::sleep(delay).await,
                None => return Err(SessionErrorRepr::ReconnectHalted.into()),
            }
        }
    }

    /// Helper for connecting
    async fn connect(&mut self, clean_start: bool) -> Result<(Connection, ConnAck), azure_mqtt::error::ConnectError> {
        // TODO: pull this from a stored source of truth to avoid too many re-reads.
        let authentication_info = self.connect_parameters.fetch_authentication_info().unwrap(); // TODO: handle unwrap

        let ch = self
            .connect_handle
            .take()
            .expect("ConnectHandle should always be present for connect attempt");

        let result = if let Some(authentication_info) = authentication_info {
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
}


async fn receive(receiver: &azure_mqtt::client::Receiver) {
    unimplemented!()
}

//     /// TODO: acking here needs to be adjusted
//     async fn receive(&mut self) {
//         loop {
//             while let Some((publish, manual_ack)) = self.receiver.recv().await {
//                 log::debug!("Incoming PUBLISH: {publish:?}");
//                 // Dispatch the message to receivers
//                 if self
//                     .incoming_pub_dispatcher
//                     .lock()
//                     .unwrap()
//                     .dispatch_publish(&publish, manual_ack)
//                     == 0
//                 {
//                     // If there are no valid dispatch targets, the publish was auto-acked.
//                     match publish.qos {
//                         azure_mqtt::packet::DeliveryQoS::AtMostOnce => {
//                             log::warn!(
//                                 "No matching receivers for PUBLISH recieved at QoS 0. Discarding."
//                             );
//                         }
//                         azure_mqtt::packet::DeliveryQoS::AtLeastOnce(delivery_info)
//                         | azure_mqtt::packet::DeliveryQoS::ExactlyOnce(delivery_info) => {
//                             log::warn!(
//                                 "No matching receivers for PUBLISH with PKID {}. Auto-acked PUBLISH.",
//                                 delivery_info.packet_identifier
//                             );
//                         }
//                     }
//                 }
//             }
//         }
//     }
// }

// /// Run background tasks for [`Session.run()`]
// async fn run_background(
//     client: azure_mqtt::client::Client,
//     sat_auth_context: Option<SatAuthContext>,
//     cancel_token: CancellationToken,
// ) {
//     /// Maintain the SAT token authentication by renewing it when the SAT file changes
//     async fn maintain_sat_auth(
//         mut sat_auth_context: SatAuthContext,
//         client: azure_mqtt::client::Client,
//     ) -> ! {
//         let mut retrying = false;
//         loop {
//             // Wait for the SAT file to change if not retrying
//             if !retrying {
//                 sat_auth_context.notified().await;
//             }

//             // Re-authenticate the client
//             if sat_auth_context
//                 .reauth(Duration::from_secs(10), &client)
//                 .await
//                 .is_ok()
//             {
//                 log::debug!("SAT token renewed successfully");
//                 // Drain the notification so we don't re-auth again for a prior change to the SAT file
//                 sat_auth_context.drain_notify().await;
//                 retrying = false;
//                 continue;
//             }
//             log::error!("Error renewing SAT token, retrying...");
//             retrying = true;
//             // Wait before retrying
//             tokio::time::sleep(Duration::from_secs(10)).await;
//         }
//     }

//     // Run the background tasks
//     if let Some(sat_auth_context) = sat_auth_context {
//         tokio::select! {
//             () = cancel_token.cancelled() => {
//                 log::debug!("Session background task cancelled");
//             }
//             () = maintain_sat_auth(sat_auth_context, client) => {
//                 log::error!("`maintain_sat_auth` task ended unexpectedly.");
//             }
//         }
//     }
// }


/// Handle used to end an MQTT session.
#[derive(Clone)]
pub struct SessionExitHandle {
    /// The disconnector used to issue disconnect requests
    disconnect_handle: Weak<Mutex<Option<azure_mqtt::client::DisconnectHandle>>>,
    /// Notifier for force exit
    force_exit: Arc<Notify>,            // TODO: can this be a oneshot?
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
                attempted: true,
                kind: SessionExitErrorKind::Detached,
            }))
        
        // TODO: does the idea of an "attempted" exit even make sense anymore?
        // Should MQTT client wait for server to close connection?
    }



    // /// Forcefully end the MQTT session running in the [`Session`] that created this handle.
    // /// This will cause the [`Session::run()`] method to return.
    // ///
    // /// The [`Session`] will be granted a period of 1 second to attempt a graceful exit before
    // /// forcing the exit. If the exit is forced, the broker will not be aware the MQTT session
    // /// has ended.
    // ///
    // /// Returns true if the exit was graceful, and false if the exit was forced.
    // pub async fn exit_force(&self) -> bool {
    //     // TODO: There might be a way to optimize this a bit better if we know we're disconnected,
    //     // but I don't wanna mess around with this until we have mockable unit testing
    //     log::debug!("Attempting to exit session gracefully before force exiting");
    //     // Ignore the result here - we don't care
    //     let _ = self.trigger_exit_user();
    //     // 1 second grace period to gracefully complete
    //     tokio::select! {
    //         () = tokio::time::sleep(Duration::from_secs(1)) => {
    //             log::debug!("Grace period for graceful session exit expired. Force exiting session");
    //             // NOTE: There is only one waiter on this Notify at any time.
    //             self.force_exit.notify_one();
    //             false
    //         },
    //         () = self.state.condition_exited() => {
    //             log::debug!("Session exited gracefully without need for force exit");
    //             true
    //         }
    //     }
    // }

    // /// Trigger a session exit, specifying the end user as the issuer of the request
    // fn trigger_exit_user(&self) -> Result<(), SessionExitError> {
    //     self.state.transition_user_desire_exit();
    //     match self.disconnector.lock().unwrap().take() {
    //         Some(disconnector) => Ok(disconnector.disconnect(
    //             &azure_mqtt::packet::DisconnectProperties {
    //                 session_expiry_interval: Some(
    //                     azure_mqtt::packet::SessionExpiryInterval::Duration(0),
    //                 ),
    //                 ..Default::default()
    //             },
    //         )?),
    //         // currently no disconnect handle, so we aren't connected
    //         None => Err(SessionExitError {
    //             attempted: false,
    //             kind: SessionExitErrorKind::BrokerUnavailable,
    //         }),
    //     }
    // }

    // /// Trigger a session exit, specifying the internal session logic as the issuer of the request
    // fn trigger_exit_internal(&self) -> Result<(), SessionExitError> {
    //     self.state.transition_session_desire_exit();
    //     match self.disconnector.lock().unwrap().take() {
    //         Some(disconnector) => Ok(disconnector.disconnect(
    //             &azure_mqtt::packet::DisconnectProperties {
    //                 session_expiry_interval: Some(
    //                     azure_mqtt::packet::SessionExpiryInterval::Duration(0),
    //                 ),
    //                 ..Default::default()
    //             },
    //         )?),
    //         // currently no disconnect handle, so we aren't connected
    //         None => Err(SessionExitError {
    //             attempted: false,
    //             kind: SessionExitErrorKind::BrokerUnavailable,
    //         }),
    //     }
    // }
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
