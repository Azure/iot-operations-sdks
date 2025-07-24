// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

use std::collections::HashMap;
use std::time::Duration;

use azure_iot_operations_mqtt::interface::{AckToken, ManagedClient};

use crate::{
    application::ApplicationContext,
    common::{aio_protocol_error::AIOProtocolError, payload_serialize::PayloadSerialize},
    rpc_command::invoker,
    telemetry::receiver,
};

/// Property request type
pub type PropertyRequest<T> = invoker::Request<T>;
/// Property response type
pub type PropertyResponse<T> = invoker::Response<T>;
/// Property notification type
pub type PropertyNotification<T> = receiver::Message<T>;

/// Property Consumer Options struct
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
    /// If true, notification messages are auto-acknowledged
    #[builder(default = "true")]
    auto_ack_notify: bool,
    /// Prefix for the response topic.
    /// If all response topic options are `None`, the response topic will be generated
    /// based on the request topic in the form: `clients/<client_id>/<request_topic>`
    #[builder(default = "None")]
    response_topic_prefix: Option<String>,
    /// Suffix for the response topic.
    /// If all response topic options are `None`, the response topic will be generated
    /// based on the request topic in the form: `clients/<client_id>/<request_topic>`
    #[builder(default = "None")]
    response_topic_suffix: Option<String>,
}

#[derive(Default)]
/// Builder for Property requests
pub struct PropertyRequestBuilder<T>
where
    T: PayloadSerialize + Send + Sync + 'static,
{
    inner_builder: invoker::RequestBuilder<T>,
}

/// Requester for writing Property via Command invocation
pub struct WriteRequester<TProp, TBool, C>(invoker::Invoker<TProp, TBool, C>)
where
    TProp: PayloadSerialize + Send + Sync + 'static,
    TBool: PayloadSerialize + Send + Sync + 'static,
    C: ManagedClient + Clone + Send + Sync + 'static,
    C::PubReceiver: Send + Sync + 'static;

/// Requester for reading Property via Command invocation
pub struct ReadRequester<TProp, TBool, C>(invoker::Invoker<TBool, TProp, C>)
where
    TProp: PayloadSerialize + Send + Sync + 'static,
    TBool: PayloadSerialize + Send + Sync + 'static,
    C: ManagedClient + Clone + Send + Sync + 'static,
    C::PubReceiver: Send + Sync + 'static;

/// Requester for observeing Property via Command invocation
pub struct ObserveRequester<TBool, C>(invoker::Invoker<TBool, TBool, C>)
where
    TBool: PayloadSerialize + Send + Sync + 'static,
    C: ManagedClient + Clone + Send + Sync + 'static,
    C::PubReceiver: Send + Sync + 'static;

/// Requester for unobserveing Property via Command invocation
pub struct UnobserveRequester<TBool, C>(invoker::Invoker<TBool, TBool, C>)
where
    TBool: PayloadSerialize + Send + Sync + 'static,
    C: ManagedClient + Clone + Send + Sync + 'static,
    C::PubReceiver: Send + Sync + 'static;

/// Listener for receiving Property notifications via Telemetry
pub struct Listener<TProp, C>(receiver::Receiver<TProp, C>)
where
    TProp: PayloadSerialize + Send + Sync + 'static,
    C: ManagedClient + Clone + Send + Sync + 'static,
    C::PubReceiver: Send + Sync + 'static;

/// Property Consumer struct
pub struct Consumer<TProp, TBool, C>
where
    TProp: PayloadSerialize + Send + Sync + 'static,
    TBool: PayloadSerialize + Send + Sync + 'static,
    C: ManagedClient + Clone + Send + Sync + 'static,
    C::PubReceiver: Send + Sync + 'static,
{
    write_requester: WriteRequester<TProp, TBool, C>,
    read_requester: ReadRequester<TProp, TBool, C>,
    observe_requester: ObserveRequester<TBool, C>,
    unobserve_requester: UnobserveRequester<TBool, C>,
}

impl<T> PropertyRequestBuilder<T>
where
    T: PayloadSerialize + Send + Sync + 'static,
{
    /// Custom user data to set on the request
    pub fn custom_user_data(&mut self, custom_user_data: Vec<(String, String)>) -> &mut Self {
        self.inner_builder.custom_user_data(custom_user_data);
        self
    }

    /// Topic token keys/values to be replaced into the publish topic of the request message.
    pub fn topic_tokens(&mut self, topic_tokens: HashMap<String, String>) -> &mut Self {
        self.inner_builder.topic_tokens(topic_tokens);
        self
    }

    /// Timeout for the request
    pub fn timeout(&mut self, timeout: Duration) -> &mut Self {
        self.inner_builder.timeout(timeout);
        self
    }

    /// Payload of the request
    ///
    /// # Errors
    /// If the payload cannot be serialized
    pub fn payload(&mut self, payload: T) -> Result<&mut Self, AIOProtocolError> {
        self.inner_builder.payload(payload)?;
        Ok(self)
    }

    /// Builds a new `Request<T>`
    ///
    /// # Errors
    /// If a required field has not been initialized
    #[allow(clippy::missing_panics_doc)] // The panic is not possible
    pub fn build(&mut self) -> Result<PropertyRequest<T>, invoker::RequestBuilderError> {
        self.inner_builder.build()
    }
}

impl<TProp, TBool, C> WriteRequester<TProp, TBool, C>
where
    TProp: PayloadSerialize + Send + Sync + 'static,
    TBool: PayloadSerialize + Send + Sync + 'static,
    C: ManagedClient + Clone + Send + Sync + 'static,
    C::PubReceiver: Send + Sync + 'static,
{
    /// Creates a new [`WriteRequester`]
    ///
    /// # Errors
    /// [`AIOProtocolError`] if there is a failure to create the wrapped invoker
    /// # Panics
    /// if the `action_topic_token` value is invalid
    pub fn new(
        application_context: ApplicationContext,
        client: C,
        action_topic_token: String,
        options: &Options,
    ) -> Result<Self, AIOProtocolError> {
        let mut invoker_options_builder = invoker::OptionsBuilder::default();
        if let Some(topic_namespace) = &options.topic_namespace {
            invoker_options_builder.topic_namespace(topic_namespace.clone());
        }

        let mut topic_token_map: HashMap<String, String> = options.topic_token_map.clone();
        topic_token_map.insert(action_topic_token, "write".to_string());

        let invoker_options = invoker_options_builder
            .request_topic_pattern(options.topic_pattern.clone())
            .command_name("write")
            .topic_token_map(topic_token_map)
            .response_topic_prefix(options.response_topic_prefix.clone())
            .response_topic_suffix(options.response_topic_suffix.clone())
            .build()
            .unwrap();

        Ok(Self(invoker::Invoker::new(
            application_context,
            client,
            invoker_options,
        )?))
    }

    /// Requests a Property write
    ///
    /// # Errors
    /// [`AIOProtocolError`] if there is a failure invoking the request
    pub async fn invoke(
        &self,
        request: PropertyRequest<TProp>,
    ) -> Result<PropertyResponse<TBool>, AIOProtocolError> {
        self.0.invoke(request).await
    }

    /// Shutdown the [`WriteRequester`]. Unsubscribes from the response topic and cancels the receiver loop to drop the receiver and to prevent the task from looping indefinitely.
    ///
    /// Returns Ok(()) on success, otherwise returns [`AIOProtocolError`].
    /// # Errors
    /// [`AIOProtocolError`] of kind [`ClientError`](azure_iot_operations_protocol::common::aio_protocol_error::AIOProtocolErrorKind::ClientError) if the unsubscribe fails or if the unsuback reason code doesn't indicate success.
    pub async fn shutdown(&self) -> Result<(), AIOProtocolError> {
        self.0.shutdown().await
    }
}

impl<TProp, TBool, C> ReadRequester<TProp, TBool, C>
where
    TProp: PayloadSerialize + Send + Sync + 'static,
    TBool: PayloadSerialize + Send + Sync + 'static,
    C: ManagedClient + Clone + Send + Sync + 'static,
    C::PubReceiver: Send + Sync + 'static,
{
    /// Creates a new [`ReadRequester`]
    ///
    /// # Errors
    /// [`AIOProtocolError`] if there is a failure to create the wrapped invoker
    /// # Panics
    /// if the `action_topic_token` value is invalid
    pub fn new(
        application_context: ApplicationContext,
        client: C,
        action_topic_token: String,
        options: &Options,
    ) -> Result<Self, AIOProtocolError> {
        let mut invoker_options_builder = invoker::OptionsBuilder::default();
        if let Some(topic_namespace) = &options.topic_namespace {
            invoker_options_builder.topic_namespace(topic_namespace.clone());
        }

        let mut topic_token_map: HashMap<String, String> = options.topic_token_map.clone();
        topic_token_map.insert(action_topic_token, "read".to_string());

        let invoker_options = invoker_options_builder
            .request_topic_pattern(options.topic_pattern.clone())
            .command_name("read")
            .topic_token_map(topic_token_map)
            .response_topic_prefix(options.response_topic_prefix.clone())
            .response_topic_suffix(options.response_topic_suffix.clone())
            .build()
            .unwrap();

        Ok(Self(invoker::Invoker::new(
            application_context,
            client,
            invoker_options,
        )?))
    }

    /// Requests a Property read
    ///
    /// # Errors
    /// [`AIOProtocolError`] if there is a failure invoking the request
    pub async fn invoke(
        &self,
        request: PropertyRequest<TBool>,
    ) -> Result<PropertyResponse<TProp>, AIOProtocolError> {
        self.0.invoke(request).await
    }

    /// Shutdown the [`ReadRequester`]. Unsubscribes from the response topic and cancels the receiver loop to drop the receiver and to prevent the task from looping indefinitely.
    ///
    /// Returns Ok(()) on success, otherwise returns [`AIOProtocolError`].
    /// # Errors
    /// [`AIOProtocolError`] of kind [`ClientError`](azure_iot_operations_protocol::common::aio_protocol_error::AIOProtocolErrorKind::ClientError) if the unsubscribe fails or if the unsuback reason code doesn't indicate success.
    pub async fn shutdown(&self) -> Result<(), AIOProtocolError> {
        self.0.shutdown().await
    }
}

impl<TBool, C> ObserveRequester<TBool, C>
where
    TBool: PayloadSerialize + Send + Sync + 'static,
    C: ManagedClient + Clone + Send + Sync + 'static,
    C::PubReceiver: Send + Sync + 'static,
{
    /// Creates a new [`ObserveRequester`]
    ///
    /// # Errors
    /// [`AIOProtocolError`] if there is a failure to create the wrapped invoker
    /// # Panics
    /// if the `action_topic_token` value is invalid
    pub fn new(
        application_context: ApplicationContext,
        client: C,
        action_topic_token: String,
        options: &Options,
    ) -> Result<Self, AIOProtocolError> {
        let mut invoker_options_builder = invoker::OptionsBuilder::default();
        if let Some(topic_namespace) = &options.topic_namespace {
            invoker_options_builder.topic_namespace(topic_namespace.clone());
        }

        let mut topic_token_map: HashMap<String, String> = options.topic_token_map.clone();
        topic_token_map.insert(action_topic_token, "observe".to_string());

        let invoker_options = invoker_options_builder
            .request_topic_pattern(options.topic_pattern.clone())
            .command_name("observe")
            .topic_token_map(topic_token_map)
            .response_topic_prefix(options.response_topic_prefix.clone())
            .response_topic_suffix(options.response_topic_suffix.clone())
            .build()
            .unwrap();

        Ok(Self(invoker::Invoker::new(
            application_context,
            client,
            invoker_options,
        )?))
    }

    /// Requests a Property observe
    ///
    /// # Errors
    /// [`AIOProtocolError`] if there is a failure invoking the request
    pub async fn invoke(
        &self,
        request: PropertyRequest<TBool>,
    ) -> Result<PropertyResponse<TBool>, AIOProtocolError> {
        self.0.invoke(request).await
    }

    /// Shutdown the [`ObserveRequester`]. Unsubscribes from the response topic and cancels the receiver loop to drop the receiver and to prevent the task from looping indefinitely.
    ///
    /// Returns Ok(()) on success, otherwise returns [`AIOProtocolError`].
    /// # Errors
    /// [`AIOProtocolError`] of kind [`ClientError`](azure_iot_operations_protocol::common::aio_protocol_error::AIOProtocolErrorKind::ClientError) if the unsubscribe fails or if the unsuback reason code doesn't indicate success.
    pub async fn shutdown(&self) -> Result<(), AIOProtocolError> {
        self.0.shutdown().await
    }
}

impl<TBool, C> UnobserveRequester<TBool, C>
where
    TBool: PayloadSerialize + Send + Sync + 'static,
    C: ManagedClient + Clone + Send + Sync + 'static,
    C::PubReceiver: Send + Sync + 'static,
{
    /// Creates a new [`UnobserveRequester`]
    ///
    /// # Errors
    /// [`AIOProtocolError`] if there is a failure to create the wrapped invoker
    /// # Panics
    /// if the `action_topic_token` value is invalid
    pub fn new(
        application_context: ApplicationContext,
        client: C,
        action_topic_token: String,
        options: &Options,
    ) -> Result<Self, AIOProtocolError> {
        let mut invoker_options_builder = invoker::OptionsBuilder::default();
        if let Some(topic_namespace) = &options.topic_namespace {
            invoker_options_builder.topic_namespace(topic_namespace.clone());
        }

        let mut topic_token_map: HashMap<String, String> = options.topic_token_map.clone();
        topic_token_map.insert(action_topic_token, "unobserve".to_string());

        let invoker_options = invoker_options_builder
            .request_topic_pattern(options.topic_pattern.clone())
            .command_name("unobserve")
            .topic_token_map(topic_token_map)
            .response_topic_prefix(options.response_topic_prefix.clone())
            .response_topic_suffix(options.response_topic_suffix.clone())
            .build()
            .unwrap();

        Ok(Self(invoker::Invoker::new(
            application_context,
            client,
            invoker_options,
        )?))
    }

    /// Requests a Property unobserve
    ///
    /// # Errors
    /// [`AIOProtocolError`] if there is a failure invoking the request
    pub async fn invoke(
        &self,
        request: PropertyRequest<TBool>,
    ) -> Result<PropertyResponse<TBool>, AIOProtocolError> {
        self.0.invoke(request).await
    }

    /// Shutdown the [`UnobserveRequester`]. Unsubscribes from the response topic and cancels the receiver loop to drop the receiver and to prevent the task from looping indefinitely.
    ///
    /// Returns Ok(()) on success, otherwise returns [`AIOProtocolError`].
    /// # Errors
    /// [`AIOProtocolError`] of kind [`ClientError`](azure_iot_operations_protocol::common::aio_protocol_error::AIOProtocolErrorKind::ClientError) if the unsubscribe fails or if the unsuback reason code doesn't indicate success.
    pub async fn shutdown(&self) -> Result<(), AIOProtocolError> {
        self.0.shutdown().await
    }
}

impl<TProp, C> Listener<TProp, C>
where
    TProp: PayloadSerialize + Send + Sync + 'static,
    C: ManagedClient + Clone + Send + Sync + 'static,
    C::PubReceiver: Send + Sync + 'static,
{
    /// Creates a new [`Listener`]
    /// # Panics
    /// if the `action_topic_token` value is invalid
    pub fn new(
        application_context: ApplicationContext,
        client: C,
        action_topic_token: String,
        options: &Options,
    ) -> Self {
        let mut receiver_options_builder = receiver::OptionsBuilder::default();
        if let Some(topic_namespace) = &options.topic_namespace {
            receiver_options_builder.topic_namespace(topic_namespace.clone());
        }

        let mut topic_token_map: HashMap<String, String> = options.topic_token_map.clone();
        topic_token_map.insert(action_topic_token, "notify".to_string());

        let receiver_options = receiver_options_builder
            .topic_pattern(options.topic_pattern.clone())
            .topic_token_map(topic_token_map)
            .auto_ack(options.auto_ack_notify)
            .build()
            .unwrap();

        Self(receiver::Receiver::new(application_context, client, receiver_options).unwrap())
    }

    /// Receive the next [`PropertyNotification`]
    ///
    /// # Errors
    /// [`AIOProtocolError`] if there is a failure receiving a message
    pub async fn recv(
        &mut self,
    ) -> Option<Result<(PropertyNotification<TProp>, Option<AckToken>), AIOProtocolError>> {
        self.0.recv().await
    }

    /// Shut down the [`Listener`]
    ///
    /// # Errors
    /// [`AIOProtocolError`] if there is a failure in graceful shutdown
    pub async fn shutdown(&mut self) -> Result<(), AIOProtocolError> {
        self.0.shutdown().await
    }
}

impl<TProp, TBool, C> Consumer<TProp, TBool, C>
where
    TProp: PayloadSerialize + Send + Sync + 'static,
    TBool: PayloadSerialize + Send + Sync + 'static,
    C: ManagedClient + Clone + Send + Sync + 'static,
    C::PubReceiver: Send + Sync + 'static,
{
    /// Creates a new [`Consumer`]
    ///
    /// # Errors
    /// [`AIOProtocolError`] if there is a failure to create a requester
    pub fn new(
        application_context: &ApplicationContext,
        client: &C,
        action_topic_token: &str,
        options: &Options,
    ) -> Result<Self, AIOProtocolError> {
        Ok(Self {
            write_requester: WriteRequester::new(
                application_context.clone(),
                client.clone(),
                action_topic_token.to_string(),
                options,
            )?,
            read_requester: ReadRequester::new(
                application_context.clone(),
                client.clone(),
                action_topic_token.to_string(),
                options,
            )?,
            observe_requester: ObserveRequester::new(
                application_context.clone(),
                client.clone(),
                action_topic_token.to_string(),
                options,
            )?,
            unobserve_requester: UnobserveRequester::new(
                application_context.clone(),
                client.clone(),
                action_topic_token.to_string(),
                options,
            )?,
        })
    }

    /// Requests a Property write
    ///
    /// # Errors
    /// [`AIOProtocolError`] if there is a failure invoking the request
    pub async fn write(
        &self,
        request: PropertyRequest<TProp>,
    ) -> Result<PropertyResponse<TBool>, AIOProtocolError> {
        self.write_requester.invoke(request).await
    }

    /// Requests a Property read
    ///
    /// # Errors
    /// [`AIOProtocolError`] if there is a failure invoking the request
    pub async fn read(
        &self,
        request: PropertyRequest<TBool>,
    ) -> Result<PropertyResponse<TProp>, AIOProtocolError> {
        self.read_requester.invoke(request).await
    }

    /// Requests a Property observe
    ///
    /// # Errors
    /// [`AIOProtocolError`] if there is a failure invoking the request
    pub async fn observe(
        &self,
        request: PropertyRequest<TBool>,
    ) -> Result<PropertyResponse<TBool>, AIOProtocolError> {
        self.observe_requester.invoke(request).await
    }

    /// Requests a Property unobserve
    ///
    /// # Errors
    /// [`AIOProtocolError`] if there is a failure invoking the request
    pub async fn unobserve(
        &self,
        request: PropertyRequest<TBool>,
    ) -> Result<PropertyResponse<TBool>, AIOProtocolError> {
        self.unobserve_requester.invoke(request).await
    }

    /// Shut down the [`Consumer`]
    ///
    /// # Errors
    /// [`AIOProtocolError`] if there is a failure in graceful shutdown
    pub async fn shutdown(&mut self) -> Result<(), AIOProtocolError> {
        self.write_requester.shutdown().await?;
        self.read_requester.shutdown().await?;
        self.observe_requester.shutdown().await?;
        self.unobserve_requester.shutdown().await?;
        Ok(())
    }
}
