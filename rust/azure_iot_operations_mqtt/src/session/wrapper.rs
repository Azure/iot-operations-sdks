// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
use std::time::Duration;

use async_trait::async_trait;
use bytes::Bytes;

use crate::control_packet::{
    Publish, PublishProperties, QoS, SubscribeProperties, UnsubscribeProperties,
};
use crate::error::ClientError;
use crate::interface::{MqttAck, MqttProvider, MqttPubReceiver, MqttPubSub};
use crate::rumqttc_adapter as adapter;
use crate::session::internal;
use crate::session::reconnect_policy::{ExponentialBackoffWithJitter, ReconnectPolicy};
use crate::session::{SessionError, SessionErrorKind, SessionExitError};
use crate::topic::TopicParseError;
use crate::{CompletionToken, MqttConnectionSettings};

/// Client that manages connections over a single MQTT session.
///
/// Use this centrally in an application to control the session and to create
/// any necessary [`SessionPubSub`], [`SessionPubReceiver`] and [`SessionExitHandle`].
pub struct Session(internal::Session<adapter::ClientAlias, adapter::EventLoopAlias>);

#[derive(Clone)]
/// Handle used to end an MQTT session.
///
/// PLEASE NOTE WELL
/// This struct's API is designed around negotiating a graceful exit with the MQTT broker.
/// However, this is not actually possible right now due to a bug in underlying MQTT library.
pub struct SessionExitHandle(internal::SessionExitHandle<adapter::ClientAlias>);

/// Send outgoing MQTT messages for publish, subscribe and unsubscribe.
#[derive(Clone)]
pub struct SessionPubSub(internal::SessionPubSub<adapter::ClientAlias>);

/// Receive and acknowledge incoming MQTT messages.
pub struct SessionPubReceiver(internal::SessionPubReceiver);

/// Options for configuring a new [`Session`]
#[derive(Builder)]
#[builder(pattern = "owned", setter(into))]
pub struct SessionOptions {
    /// MQTT Connection Settings for configuring the [`Session`]
    pub connection_settings: MqttConnectionSettings,
    #[builder(default = "Box::new(ExponentialBackoffWithJitter::default())")]
    /// Reconnect Policy to by used by the `Session`
    pub reconnect_policy: Box<dyn ReconnectPolicy>,
    // TODO: Channel capacity replacement(s). The previous incarnation of this value
    // was not used correctly. It needs to be split up into separate values with
    // separate semantics. For now, it has been hardcoded to 100 in the wrapper.
}

impl Session {
    /// Create a new [`Session`] with the provided options structure.
    ///
    /// # Errors
    /// Returns a [`SessionError`] if there are errors using the session options.
    pub fn new(options: SessionOptions) -> Result<Self, SessionError> {
        let client_id = options.connection_settings.client_id.clone();
        let sat_auth_file = options.connection_settings.sat_auth_file.clone();
        // TODO: capacities have been hardcoded to 100. Will make these settable in the future.
        let (client, event_loop) = adapter::client(options.connection_settings, 100, true)
            .map_err(SessionErrorKind::from)?;
        Ok(Session(internal::Session::new_from_injection(
            client,
            event_loop,
            options.reconnect_policy,
            client_id,
            sat_auth_file,
            100,
        )))
    }

    /// Return an instance of [`SessionExitHandle`] that can be used to end
    /// this [`Session`]
    pub fn get_session_exit_handle(&self) -> SessionExitHandle {
        SessionExitHandle(self.0.get_session_exit_handle())
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

impl MqttProvider<SessionPubSub, SessionPubReceiver> for Session {
    fn client_id(&self) -> &str {
        self.0.client_id()
    }

    fn pub_sub(&self) -> SessionPubSub {
        SessionPubSub(self.0.pub_sub())
    }

    fn filtered_pub_receiver(
        &mut self,
        topic_filter: &str,
        auto_ack: bool,
    ) -> Result<SessionPubReceiver, TopicParseError> {
        Ok(SessionPubReceiver(
            self.0.filtered_pub_receiver(topic_filter, auto_ack)?,
        ))
    }
}

#[async_trait]
impl MqttPubSub for SessionPubSub {
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
        self.0.subscribe(topic, qos).await
    }

    async fn subscribe_with_properties(
        &self,
        topic: impl Into<String> + Send,
        qos: QoS,
        properties: SubscribeProperties,
    ) -> Result<CompletionToken, ClientError> {
        self.0
            .subscribe_with_properties(topic, qos, properties)
            .await
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

#[async_trait]
impl MqttPubReceiver for SessionPubReceiver {
    async fn recv(&mut self) -> Option<Publish> {
        self.0.recv().await
    }
}

#[async_trait]
impl MqttAck for SessionPubReceiver {
    async fn ack(&self, msg: &Publish) -> Result<(), ClientError> {
        self.0.ack(msg).await
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
