// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

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
use crate::session::{SessionError, SessionErrorKind};
use crate::topic::TopicParseError;
use crate::{CompletionToken, MqttConnectionSettings};

/// Client that manages connections over a single MQTT session.
///
/// Use this centrally in an application to control the session and to create
/// any necessary [`SessionPubSub`], [`SessionPubReceiver`] and [`SessionExitHandle`].
pub struct Session(internal::Session<adapter::ClientAlias, adapter::EventLoopAlias>);
#[derive(Clone)]
/// Handle used to end an MQTT session.
pub struct SessionExitHandle(internal::SessionExitHandle<adapter::ClientAlias>);
#[derive(Clone)]
/// Send outgoing MQTT messages for publish, subscribe and unsubscribe.
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
    /// End the session running in the [`Session`] that created this handle.
    ///
    /// # Errors
    /// Returns `ClientError` if there is a failure ending the session.
    /// This should not happen.
    pub async fn exit_session(&self) -> Result<(), ClientError> {
        self.0.exit_session().await
    }
}
