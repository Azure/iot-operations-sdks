// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

use std::collections::HashMap;
use std::time::Duration;

use azure_iot_operations_mqtt::control_packet::QoS;
use azure_iot_operations_mqtt::interface::ManagedClient;

use crate::{
    application::ApplicationContext,
    common::{aio_protocol_error::AIOProtocolError, payload_serialize::PayloadSerialize},
    rpc_command::executor,
    telemetry::sender,
};

/// Property request type
pub type PropertyRequest<TReq, TResp> = executor::Request<TReq, TResp>;
/// Property response type
pub type PropertyResponse<T> = executor::Response<T>;
/// Property notification type
pub type PropertyNotification<T> = sender::Message<T>;

/// Property Maintainer Options struct
#[derive(Builder, Clone)]
#[builder(setter(into, strip_option))]
pub struct Options {
    /// Topic pattern for property messages.
    /// Must align with [topic-structure.md](https://github.com/Azure/iot-operations-sdks/blob/main/doc/reference/topic-structure.md)
    topic_pattern: String,
    /// Optional Topic namespace to be prepended to the topic pattern
    #[builder(default = "None")]
    topic_namespace: Option<String>,
    /// Topic token keys/values to be permanently replaced in the topic pattern
    #[builder(default)]
    topic_token_map: HashMap<String, String>,
}

/// Property request enumeration
pub enum PropertyReq<TProp, TBool>
where
    TProp: PayloadSerialize + Send + Sync + 'static,
    TBool: PayloadSerialize + Send + Sync + 'static,
{
    /// Request to write Property value
    Write(Option<Result<PropertyRequest<TProp, TBool>, AIOProtocolError>>),
    /// Request to read Property value
    Read(Option<Result<PropertyRequest<TBool, TProp>, AIOProtocolError>>),
    /// Request to watch Property value
    Watch(Option<Result<PropertyRequest<TBool, TBool>, AIOProtocolError>>),
    /// Request to unwatch Property value
    Unwatch(Option<Result<PropertyRequest<TBool, TBool>, AIOProtocolError>>),
}

#[derive(Default)]
/// Builder for Property responses
pub struct PropertyResponseBuilder<T>
where
    T: PayloadSerialize + Send + Sync + 'static,
{
    inner_builder: executor::ResponseBuilder<T>,
}

#[derive(Default)]
/// Builder for Property notifications
pub struct PropertyNotificationBuilder<T>
where
    T: PayloadSerialize + Send + Sync + 'static,
{
    inner_builder: sender::MessageBuilder<T>,
}

/// Responder for writing Property via Command executor
pub struct WriteResponder<TProp, TBool, C>(executor::Executor<TProp, TBool, C>)
where
    TProp: PayloadSerialize + Send + Sync + 'static,
    TBool: PayloadSerialize + Send + Sync + 'static,
    C: ManagedClient + Clone + Send + Sync + 'static,
    C::PubReceiver: Send + Sync + 'static;

/// Responder for reading Property via Command executor
pub struct ReadResponder<TProp, TBool, C>(executor::Executor<TBool, TProp, C>)
where
    TProp: PayloadSerialize + Send + Sync + 'static,
    TBool: PayloadSerialize + Send + Sync + 'static,
    C: ManagedClient + Clone + Send + Sync + 'static,
    C::PubReceiver: Send + Sync + 'static;

/// Responder for watching Property via Command executor
pub struct WatchResponder<TBool, C>(executor::Executor<TBool, TBool, C>)
where
    TBool: PayloadSerialize + Send + Sync + 'static,
    C: ManagedClient + Clone + Send + Sync + 'static,
    C::PubReceiver: Send + Sync + 'static;

/// Responder for unwatching Property via Command executor
pub struct UnwatchResponder<TBool, C>(executor::Executor<TBool, TBool, C>)
where
    TBool: PayloadSerialize + Send + Sync + 'static,
    C: ManagedClient + Clone + Send + Sync + 'static,
    C::PubReceiver: Send + Sync + 'static;

/// Notifier for sending Property notifications via Telemetry
pub struct Notifier<TProp, C>(sender::Sender<TProp, C>)
where
    TProp: PayloadSerialize + Send + Sync + 'static,
    C: ManagedClient + Clone + Send + Sync + 'static;

/// Property Maintainer struct
pub struct Maintainer<TProp, TBool, C>
where
    TProp: PayloadSerialize + Send + Sync + 'static,
    TBool: PayloadSerialize + Send + Sync + 'static,
    C: ManagedClient + Clone + Send + Sync + 'static,
    C::PubReceiver: Send + Sync + 'static,
{
    write_responder: WriteResponder<TProp, TBool, C>,
    read_responder: ReadResponder<TProp, TBool, C>,
    watch_responder: WatchResponder<TBool, C>,
    unwatch_responder: UnwatchResponder<TBool, C>,
}

impl<T> PropertyResponseBuilder<T>
where
    T: PayloadSerialize + Send + Sync + 'static,
{
    /// Custom user data to set on the response
    pub fn custom_user_data(&mut self, custom_user_data: Vec<(String, String)>) -> &mut Self {
        self.inner_builder.custom_user_data(custom_user_data);
        self
    }

    /// Payload of the response
    ///
    /// # Errors
    /// If the payload cannot be serialized
    pub fn payload(&mut self, payload: T) -> Result<&mut Self, AIOProtocolError> {
        self.inner_builder.payload(payload)?;
        Ok(self)
    }

    /// Builds a new `Response<T>`
    ///
    /// # Errors
    /// If a required field has not been initialized
    #[allow(clippy::missing_panics_doc)] // The panic is not possible
    pub fn build(&mut self) -> Result<PropertyResponse<T>, executor::ResponseBuilderError> {
        self.inner_builder.build()
    }
}

impl<T> PropertyNotificationBuilder<T>
where
    T: PayloadSerialize + Send + Sync + 'static,
{
    /// Quality of Service of the notification message. Can only be `AtMostOnce` or `AtLeastOnce`.
    pub fn qos(&mut self, qos: QoS) -> &mut Self {
        self.inner_builder.qos(qos);
        self
    }

    /// Custom user data to set on the message
    pub fn custom_user_data(&mut self, custom_user_data: Vec<(String, String)>) -> &mut Self {
        self.inner_builder.custom_user_data(custom_user_data);
        self
    }

    /// Topic token keys/values to be replaced into the publish topic of the notification message.
    pub fn topic_tokens(&mut self, topic_tokens: HashMap<String, String>) -> &mut Self {
        self.inner_builder.topic_tokens(topic_tokens);
        self
    }

    /// Time before message expires
    pub fn message_expiry(&mut self, message_expiry: Duration) -> &mut Self {
        self.inner_builder.message_expiry(message_expiry);
        self
    }

    /// Cloud event for the message
    pub fn cloud_event(&mut self, cloud_event: Option<sender::CloudEvent>) -> &mut Self {
        self.inner_builder.cloud_event(cloud_event);
        self
    }

    /// Payload of the message
    ///
    /// # Errors
    /// If the payload cannot be serialized
    pub fn payload(&mut self, payload: T) -> Result<&mut Self, AIOProtocolError> {
        self.inner_builder.payload(payload)?;
        Ok(self)
    }

    /// Builds a new `PropertyNotification<T>`
    ///
    /// # Errors
    /// If a required field has not been initialized
    pub fn build(&mut self) -> Result<PropertyNotification<T>, sender::MessageBuilderError> {
        self.inner_builder.build()
    }
}

impl<TProp, TBool, C> WriteResponder<TProp, TBool, C>
where
    TProp: PayloadSerialize + Send + Sync + 'static,
    TBool: PayloadSerialize + Send + Sync + 'static,
    C: ManagedClient + Clone + Send + Sync + 'static,
    C::PubReceiver: Send + Sync + 'static,
{
    /// Creates a new [`WriteResponder`]
    ///
    /// # Errors
    /// [`AIOProtocolError`] if there is a failure to create the wrapped executor
    /// # Panics
    /// if the `action_topic_token` value is invalid
    pub fn new(
        application_context: ApplicationContext,
        client: C,
        action_topic_token: String,
        options: &Options,
    ) -> Result<Self, AIOProtocolError> {
        let mut executor_options_builder = executor::OptionsBuilder::default();
        if let Some(topic_namespace) = &options.topic_namespace {
            executor_options_builder.topic_namespace(topic_namespace.clone());
        }

        let mut topic_token_map: HashMap<String, String> = options.topic_token_map.clone();
        topic_token_map.insert(action_topic_token, "write".to_string());

        let executor_options = executor_options_builder
            .request_topic_pattern(options.topic_pattern.clone())
            .command_name("write")
            .is_idempotent(false)
            .topic_token_map(topic_token_map)
            .build()
            .unwrap();

        Ok(Self(executor::Executor::new(
            application_context,
            client,
            executor_options,
        )?))
    }

    /// Receive the next Property write or [`None`] if there will be no more requests
    ///
    /// # Errors
    /// [`AIOProtocolError`] if there is a failure receiving a request
    pub async fn recv(
        &mut self,
    ) -> Option<Result<PropertyRequest<TProp, TBool>, AIOProtocolError>> {
        self.0.recv().await
    }

    /// Shutdown the [`WriteResponder`]. Unsubscribes from the response topic and cancels the receiver loop to drop the receiver and to prevent the task from looping indefinitely.
    ///
    /// Returns Ok(()) on success, otherwise returns [`AIOProtocolError`].
    /// # Errors
    /// [`AIOProtocolError`] of kind [`ClientError`](azure_iot_operations_protocol::common::aio_protocol_error::AIOProtocolErrorKind::ClientError) if the unsubscribe fails or if the unsuback reason code doesn't indicate success.
    pub async fn shutdown(&mut self) -> Result<(), AIOProtocolError> {
        self.0.shutdown().await
    }
}

impl<TProp, TBool, C> ReadResponder<TProp, TBool, C>
where
    TProp: PayloadSerialize + Send + Sync + 'static,
    TBool: PayloadSerialize + Send + Sync + 'static,
    C: ManagedClient + Clone + Send + Sync + 'static,
    C::PubReceiver: Send + Sync + 'static,
{
    /// Creates a new [`ReadResponder`]
    ///
    /// # Errors
    /// [`AIOProtocolError`] if there is a failure to create the wrapped executor
    /// # Panics
    /// if the `action_topic_token` value is invalid
    pub fn new(
        application_context: ApplicationContext,
        client: C,
        action_topic_token: String,
        options: &Options,
    ) -> Result<Self, AIOProtocolError> {
        let mut executor_options_builder = executor::OptionsBuilder::default();
        if let Some(topic_namespace) = &options.topic_namespace {
            executor_options_builder.topic_namespace(topic_namespace.clone());
        }

        let mut topic_token_map: HashMap<String, String> = options.topic_token_map.clone();
        topic_token_map.insert(action_topic_token, "read".to_string());

        let executor_options = executor_options_builder
            .request_topic_pattern(options.topic_pattern.clone())
            .command_name("read")
            .is_idempotent(true)
            .topic_token_map(topic_token_map)
            .build()
            .unwrap();

        Ok(Self(executor::Executor::new(
            application_context,
            client,
            executor_options,
        )?))
    }

    /// Receive the next Property read or [`None`] if there will be no more requests
    ///
    /// # Errors
    /// [`AIOProtocolError`] if there is a failure receiving a request
    pub async fn recv(
        &mut self,
    ) -> Option<Result<PropertyRequest<TBool, TProp>, AIOProtocolError>> {
        self.0.recv().await
    }

    /// Shutdown the [`ReadResponder`]. Unsubscribes from the response topic and cancels the receiver loop to drop the receiver and to prevent the task from looping indefinitely.
    ///
    /// Returns Ok(()) on success, otherwise returns [`AIOProtocolError`].
    /// # Errors
    /// [`AIOProtocolError`] of kind [`ClientError`](azure_iot_operations_protocol::common::aio_protocol_error::AIOProtocolErrorKind::ClientError) if the unsubscribe fails or if the unsuback reason code doesn't indicate success.
    pub async fn shutdown(&mut self) -> Result<(), AIOProtocolError> {
        self.0.shutdown().await
    }
}

impl<TBool, C> WatchResponder<TBool, C>
where
    TBool: PayloadSerialize + Send + Sync + 'static,
    C: ManagedClient + Clone + Send + Sync + 'static,
    C::PubReceiver: Send + Sync + 'static,
{
    /// Creates a new [`WatchResponder`]
    ///
    /// # Errors
    /// [`AIOProtocolError`] if there is a failure to create the wrapped executor
    /// # Panics
    /// if the `action_topic_token` value is invalid
    pub fn new(
        application_context: ApplicationContext,
        client: C,
        action_topic_token: String,
        options: &Options,
    ) -> Result<Self, AIOProtocolError> {
        let mut executor_options_builder = executor::OptionsBuilder::default();
        if let Some(topic_namespace) = &options.topic_namespace {
            executor_options_builder.topic_namespace(topic_namespace.clone());
        }

        let mut topic_token_map: HashMap<String, String> = options.topic_token_map.clone();
        topic_token_map.insert(action_topic_token, "watch".to_string());

        let executor_options = executor_options_builder
            .request_topic_pattern(options.topic_pattern.clone())
            .command_name("watch")
            .is_idempotent(true)
            .topic_token_map(topic_token_map)
            .build()
            .unwrap();

        Ok(Self(executor::Executor::new(
            application_context,
            client,
            executor_options,
        )?))
    }

    /// Receive the next Property watch or [`None`] if there will be no more requests
    ///
    /// # Errors
    /// [`AIOProtocolError`] if there is a failure receiving a request
    pub async fn recv(
        &mut self,
    ) -> Option<Result<PropertyRequest<TBool, TBool>, AIOProtocolError>> {
        self.0.recv().await
    }

    /// Shutdown the [`WatchResponder`]. Unsubscribes from the response topic and cancels the receiver loop to drop the receiver and to prevent the task from looping indefinitely.
    ///
    /// Returns Ok(()) on success, otherwise returns [`AIOProtocolError`].
    /// # Errors
    /// [`AIOProtocolError`] of kind [`ClientError`](azure_iot_operations_protocol::common::aio_protocol_error::AIOProtocolErrorKind::ClientError) if the unsubscribe fails or if the unsuback reason code doesn't indicate success.
    pub async fn shutdown(&mut self) -> Result<(), AIOProtocolError> {
        self.0.shutdown().await
    }
}

impl<TBool, C> UnwatchResponder<TBool, C>
where
    TBool: PayloadSerialize + Send + Sync + 'static,
    C: ManagedClient + Clone + Send + Sync + 'static,
    C::PubReceiver: Send + Sync + 'static,
{
    /// Creates a new [`UnwatchResponder`]
    ///
    /// # Errors
    /// [`AIOProtocolError`] if there is a failure to create the wrapped executor
    /// # Panics
    /// if the `action_topic_token` value is invalid
    pub fn new(
        application_context: ApplicationContext,
        client: C,
        action_topic_token: String,
        options: &Options,
    ) -> Result<Self, AIOProtocolError> {
        let mut executor_options_builder = executor::OptionsBuilder::default();
        if let Some(topic_namespace) = &options.topic_namespace {
            executor_options_builder.topic_namespace(topic_namespace.clone());
        }

        let mut topic_token_map: HashMap<String, String> = options.topic_token_map.clone();
        topic_token_map.insert(action_topic_token, "unwatch".to_string());

        let executor_options = executor_options_builder
            .request_topic_pattern(options.topic_pattern.clone())
            .command_name("unwatch")
            .is_idempotent(true)
            .topic_token_map(topic_token_map)
            .build()
            .unwrap();

        Ok(Self(executor::Executor::new(
            application_context,
            client,
            executor_options,
        )?))
    }

    /// Receive the next Property unwatch or [`None`] if there will be no more requests
    ///
    /// # Errors
    /// [`AIOProtocolError`] if there is a failure receiving a request
    pub async fn recv(
        &mut self,
    ) -> Option<Result<PropertyRequest<TBool, TBool>, AIOProtocolError>> {
        self.0.recv().await
    }

    /// Shutdown the [`UnwatchResponder`]. Unsubscribes from the response topic and cancels the receiver loop to drop the receiver and to prevent the task from looping indefinitely.
    ///
    /// Returns Ok(()) on success, otherwise returns [`AIOProtocolError`].
    /// # Errors
    /// [`AIOProtocolError`] of kind [`ClientError`](azure_iot_operations_protocol::common::aio_protocol_error::AIOProtocolErrorKind::ClientError) if the unsubscribe fails or if the unsuback reason code doesn't indicate success.
    pub async fn shutdown(&mut self) -> Result<(), AIOProtocolError> {
        self.0.shutdown().await
    }
}

impl<TProp, C> Notifier<TProp, C>
where
    TProp: PayloadSerialize + Send + Sync + 'static,
    C: ManagedClient + Clone + Send + Sync + 'static,
{
    /// Creates a new [`Notifier`]
    /// # Panics
    /// if the `action_topic_token` value is invalid
    pub fn new(
        application_context: ApplicationContext,
        client: C,
        action_topic_token: String,
        options: &Options,
    ) -> Self {
        let mut sender_options_builder = sender::OptionsBuilder::default();
        if let Some(topic_namespace) = &options.topic_namespace {
            sender_options_builder.topic_namespace(topic_namespace.clone());
        }

        let mut topic_token_map: HashMap<String, String> = options.topic_token_map.clone();
        topic_token_map.insert(action_topic_token, "notify".to_string());

        let sender_options = sender_options_builder
            .topic_pattern(options.topic_pattern.clone())
            .topic_token_map(topic_token_map)
            .build()
            .unwrap();

        Self(sender::Sender::new(application_context, client, sender_options).unwrap())
    }

    /// Sends a [`Message`]
    ///
    /// # Errors
    /// [`AIOProtocolError`] if there is a failure sending the message
    pub async fn send(&self, message: PropertyNotification<TProp>) -> Result<(), AIOProtocolError> {
        self.0.send(message).await
    }
}

impl<TProp, TBool, C> Maintainer<TProp, TBool, C>
where
    TProp: PayloadSerialize + Send + Sync + 'static,
    TBool: PayloadSerialize + Send + Sync + 'static,
    C: ManagedClient + Clone + Send + Sync + 'static,
    C::PubReceiver: Send + Sync + 'static,
{
    /// Creates a new [`Maintainer`]
    ///
    /// # Errors
    /// [`AIOProtocolError`] if there is a failure to create a responder
    pub fn new(
        application_context: &ApplicationContext,
        client: &C,
        action_topic_token: &str,
        options: &Options,
    ) -> Result<Self, AIOProtocolError> {
        Ok(Self {
            write_responder: WriteResponder::new(
                application_context.clone(),
                client.clone(),
                action_topic_token.to_string(),
                options,
            )?,
            read_responder: ReadResponder::new(
                application_context.clone(),
                client.clone(),
                action_topic_token.to_string(),
                options,
            )?,
            watch_responder: WatchResponder::new(
                application_context.clone(),
                client.clone(),
                action_topic_token.to_string(),
                options,
            )?,
            unwatch_responder: UnwatchResponder::new(
                application_context.clone(),
                client.clone(),
                action_topic_token.to_string(),
                options,
            )?,
        })
    }

    /// Receive the next Property request or [`None`] if there will be no more requests
    ///
    /// # Errors
    /// [`AIOProtocolError`] if there is a failure receiving a request
    pub async fn recv(&mut self) -> PropertyReq<TProp, TBool> {
        tokio::select! {
            write = self.write_responder.recv() => PropertyReq::Write(write),
            read = self.read_responder.recv() => PropertyReq::Read(read),
            watch = self.watch_responder.recv() => PropertyReq::Watch(watch),
            unwatch = self.unwatch_responder.recv() => PropertyReq::Unwatch(unwatch),
        }
    }

    /// Shut down the [`Maintainer`]
    ///
    /// # Errors
    /// [`AIOProtocolError`] if there is a failure in graceful shutdown
    pub async fn shutdown(&mut self) -> Result<(), AIOProtocolError> {
        self.write_responder.shutdown().await?;
        self.read_responder.shutdown().await?;
        self.watch_responder.shutdown().await?;
        self.unwatch_responder.shutdown().await?;
        Ok(())
    }
}
