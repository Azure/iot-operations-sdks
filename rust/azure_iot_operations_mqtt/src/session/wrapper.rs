// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
use std::time::Duration;

use async_trait::async_trait;
use bytes::Bytes;

use crate::control_packet::{
    Publish, PublishProperties, QoS, SubscribeProperties, UnsubscribeProperties,
};
use crate::error::{PublishError, SubscribeError, UnsubscribeError};
use crate::interface::{AckToken, CompletionToken, ManagedClient, MqttPubSub, PubReceiver};
use crate::rumqttc_adapter as adapter;
use crate::session::managed_client;
use crate::session::reconnect_policy::{ExponentialBackoffWithJitter, ReconnectPolicy};
use crate::session::session;
use crate::session::{SessionError, SessionErrorKind, SessionExitError};
use crate::topic::TopicParseError;
use crate::MqttConnectionSettings;

/// Client that manages connections over a single MQTT session.
///
/// Use this centrally in an application to control the session and to create
/// instances of [`SessionManagedClient`] and [`SessionExitHandle`].
pub struct Session(session::Session<adapter::ClientAlias, adapter::EventLoopAlias>);

/// Handle used to end an MQTT session.
///
/// PLEASE NOTE WELL
/// This struct's API is designed around negotiating a graceful exit with the MQTT broker.
/// However, this is not actually possible right now due to a bug in underlying MQTT library.
#[derive(Clone)]
pub struct SessionExitHandle(session::SessionExitHandle<adapter::ClientAlias>);

/// Monitor for connection changes in the [`Session`].
///
/// This is largely for informational purposes.
#[derive(Clone)]
pub struct SessionConnectionMonitor(session::SessionConnectionMonitor);

/// An MQTT client that has it's connection state externally managed by a [`Session`].
/// Can be used to send messages and create receivers for incoming messages.
#[derive(Clone)]
pub struct SessionManagedClient(managed_client::SessionManagedClient<adapter::ClientAlias>);

/// Receive and acknowledge incoming MQTT messages.
pub struct SessionPubReceiver(managed_client::SessionPubReceiver);

/// Options for configuring a new [`Session`]
#[derive(Builder)]
#[builder(pattern = "owned", setter(into))]
pub struct SessionOptions {
    /// MQTT Connection Settings for configuring the [`Session`]
    pub connection_settings: MqttConnectionSettings,
    /// Reconnect Policy to by used by the `Session`
    #[builder(default = "Box::new(ExponentialBackoffWithJitter::default())")]
    pub reconnect_policy: Box<dyn ReconnectPolicy>,
    /// Maximum number of queued outgoing messages not yet accepted by the MQTT Session
    #[builder(default = "100")]
    pub outgoing_max: usize,
}

impl Session {
    /// Create a new [`Session`] with the provided options structure.
    ///
    /// # Errors
    /// Returns a [`SessionError`] if there are errors using the session options.
    pub fn new(options: SessionOptions) -> Result<Self, SessionError> {
        let client_id = options.connection_settings.client_id.clone();
        let sat_file = options.connection_settings.sat_file.clone();
        let (client, event_loop) =
            adapter::client(options.connection_settings, options.outgoing_max, true)
                .map_err(SessionErrorKind::from)?;
        Ok(Session(session::Session::new_from_injection(
            client,
            event_loop,
            options.reconnect_policy,
            client_id,
            sat_file,
        )))
    }

    /// Return a new instance of [`SessionExitHandle`] that can be used to end this [`Session`]
    pub fn create_exit_handle(&self) -> SessionExitHandle {
        SessionExitHandle(self.0.create_exit_handle())
    }

    /// Return a new instance of [`SessionConnectionMonitor`] that can be used to monitor the connection state
    pub fn create_connection_monitor(&self) -> SessionConnectionMonitor {
        SessionConnectionMonitor(self.0.create_connection_monitor())
    }

    /// Return a new instance of [`SessionManagedClient`] that can be used to send and receive messages
    pub fn create_managed_client(&self) -> SessionManagedClient {
        SessionManagedClient(self.0.create_managed_client())
    }

    /// Begin running the [`Session`].
    ///
    /// Blocks until either a session exit or a fatal connection error is encountered.
    ///
    /// # Errors
    /// Returns a [`SessionError`] if the session encounters a fatal error and ends.
    pub async fn run(&mut self) -> Result<(), SessionError> {
        self.0.run().await
    }
}

impl ManagedClient for SessionManagedClient {
    type PubReceiver = SessionPubReceiver;

    fn client_id(&self) -> &str {
        self.0.client_id()
    }

    fn create_filtered_pub_receiver(
        &self,
        topic_filter: &str,
    ) -> Result<SessionPubReceiver, TopicParseError> {
        Ok(SessionPubReceiver(
            self.0.create_filtered_pub_receiver(topic_filter)?,
        ))
    }

    fn create_unfiltered_pub_receiver(&self) -> SessionPubReceiver {
        SessionPubReceiver(self.0.create_unfiltered_pub_receiver())
    }
}

#[async_trait]
impl MqttPubSub for SessionManagedClient {
    async fn publish(
        &self,
        topic: impl Into<String> + Send,
        qos: QoS,
        retain: bool,
        payload: impl Into<Bytes> + Send,
    ) -> Result<CompletionToken, PublishError> {
        self.0.publish(topic, qos, retain, payload).await
    }

    async fn publish_with_properties(
        &self,
        topic: impl Into<String> + Send,
        qos: QoS,
        retain: bool,
        payload: impl Into<Bytes> + Send,
        properties: PublishProperties,
    ) -> Result<CompletionToken, PublishError> {
        self.0
            .publish_with_properties(topic, qos, retain, payload, properties)
            .await
    }

    async fn subscribe(
        &self,
        topic: impl Into<String> + Send,
        qos: QoS,
    ) -> Result<CompletionToken, SubscribeError> {
        self.0.subscribe(topic, qos).await
    }

    async fn subscribe_with_properties(
        &self,
        topic: impl Into<String> + Send,
        qos: QoS,
        properties: SubscribeProperties,
    ) -> Result<CompletionToken, SubscribeError> {
        self.0
            .subscribe_with_properties(topic, qos, properties)
            .await
    }

    async fn unsubscribe(
        &self,
        topic: impl Into<String> + Send,
    ) -> Result<CompletionToken, UnsubscribeError> {
        self.0.unsubscribe(topic).await
    }

    async fn unsubscribe_with_properties(
        &self,
        topic: impl Into<String> + Send,
        properties: UnsubscribeProperties,
    ) -> Result<CompletionToken, UnsubscribeError> {
        self.0.unsubscribe_with_properties(topic, properties).await
    }
}

#[async_trait]
impl PubReceiver for SessionPubReceiver {
    async fn recv(&mut self) -> Option<Publish> {
        self.0.recv().await
    }

    async fn recv_manual_ack(&mut self) -> Option<(Publish, Option<AckToken>)> {
        self.0.recv_manual_ack().await
    }

    fn close(&mut self) {
        self.0.close();
    }
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
    /// * [`SessionExitError::Dropped`] if the Session no longer exists.
    /// * [`SessionExitError::BrokerUnavailable`] if the Session is not connected to the broker.
    pub async fn try_exit(&self) -> Result<(), SessionExitError> {
        self.0.try_exit().await
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
        self.0.try_exit_timeout(timeout).await
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
        self.0.exit_force().await
    }
}

impl SessionConnectionMonitor {
    /// Returns true if the [`Session`] is currently connected.
    /// Note that this may not be accurate if connection has been recently lost.
    #[must_use]
    pub fn is_connected(&self) -> bool {
        self.0.is_connected()
    }

    /// Wait until the [`Session`] is connected.
    /// Returns immediately if already connected.
    pub async fn connected(&self) {
        self.0.connected().await;
    }

    /// Wait until the [`Session`] is disconnected.
    /// Returns immediately if already disconnected.
    pub async fn disconnected(&self) {
        self.0.disconnected().await;
    }
}
