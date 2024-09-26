// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
use std::{collections::HashMap, marker::PhantomData};

use azure_iot_operations_mqtt::interface::{MqttAck, MqttProvider, MqttPubReceiver, MqttPubSub};

use super::{TelemetryMessage, TelemetryMessageBuilder};
use crate::common::{aio_protocol_error::AIOProtocolError, payload_serialize::PayloadSerialize};

/// Telemetry Receiver Options struct
#[allow(unused)]
#[derive(Builder, Clone)]
#[builder(setter(into, strip_option))]
pub struct TelemetryReceiverOptions {
    /// Topic pattern for the telemetry message
    /// Must align with [topic-structure.md](https://github.com/microsoft/mqtt-patterns/blob/main/docs/specs/topic-structure.md)
    topic_pattern: String,
    /// Telemetry name
    #[builder(default = "None")]
    telemetry_name: Option<String>,
    /// Model ID if required by the topic patterns
    #[builder(default = "None")]
    model_id: Option<String>,
    /// Optional Topic namespace to be prepended to the topic pattern
    #[builder(default = "None")]
    topic_namespace: Option<String>,
    /// Custom topic token keys/values to be replaced in the topic pattern
    #[builder(default)]
    custom_topic_token_map: HashMap<String, String>,
    /// Service group ID
    #[builder(default = "None")]
    service_group_id: Option<String>,
}

/// Telemetry Receiver struct
///
/// # Example
/// ```
/// # use tokio_test::block_on;
/// # use azure_iot_operations_mqtt::MqttConnectionSettingsBuilder;
/// # use azure_iot_operations_mqtt::session::{Session, SessionOptionsBuilder};
/// # use azure_iot_operations_protocol::telemetry::telemetry_receiver::{TelemetryReceiver, TelemetryReceiverOptionsBuilder};
/// # use azure_iot_operations_protocol::telemetry::TelemetryMessageBuilder;
/// # use azure_iot_operations_protocol::common::payload_serialize::{PayloadSerialize, FormatIndicator};

/// # #[derive(Clone, Debug)]
/// # pub struct SamplePayload { }
/// # impl PayloadSerialize for SamplePayload {
/// #   type Error = String;
/// #   fn content_type() -> &'static str { "application/json" }
/// #   fn format_indicator() -> FormatIndicator { FormatIndicator::Utf8EncodedCharacterData }
/// #   fn serialize(&self) -> Result<Vec<u8>, String> { Ok(Vec::new()) }
/// #   fn deserialize(payload: &[u8]) -> Result<Self, String> { Ok(SamplePayload {}) }
/// # }
/// # let mut connection_settings = MqttConnectionSettingsBuilder::default()
/// #     .client_id("test_server")
/// #     .host_name("mqtt://localhost")
/// #     .tcp_port(1883u16)
/// #     .build().unwrap();
/// # let mut session_options = SessionOptionsBuilder::default()
/// #     .connection_settings(connection_settings)
/// #     .build().unwrap();
/// # let mut mqtt_session = Session::new(session_options).unwrap();
/// let receiver_options = TelemetryReceiverOptionsBuilder::default()
///  .topic_pattern("test/telemetry")
///  .build().unwrap();
/// let mut telemetry_receiver: TelemetryReceiver<SamplePayload, _, _> = TelemetryReceiver::new(&mut mqtt_session, receiver_options).unwrap();
/// # tokio_test::block_on(async {
/// telemetry_receiver.start().await.unwrap();
/// let telemetry_message = telemetry_receiver.recv().await.unwrap();
/// # });
/// ```
///
#[allow(unused)]
pub struct TelemetryReceiver<T, PS, PR>
where
    T: PayloadSerialize,
    PS: MqttPubSub + Clone + Send + Sync,
    PR: MqttPubReceiver + MqttAck + Send + Sync,
{
    ps_placeholder: PhantomData<PS>,
    pr_placeholder: PhantomData<PR>,
    message_payload_type: PhantomData<T>,
    telemetry_name: Option<String>,
}

/// Implementation of a Telemetry Sender
impl<T, PS, PR> TelemetryReceiver<T, PS, PR>
where
    T: PayloadSerialize,
    PS: MqttPubSub + Clone + Send + Sync,
    PR: MqttPubReceiver + MqttAck + Send + Sync,
{
    /// Creates a new [`TelemetryReceiver`].
    ///
    /// # Arguments
    /// * `mqtt_provider` - [`MqttProvider`] to use for telemetry communication.
    /// * `options` - [`TelemetryReceiverOptions`] to configure the telemetry receiver.
    ///
    /// Returns Ok([`TelemetryReceiver`]) on success, otherwise returns[`AIOProtocolError`].
    /// # Errors
    /// TODO: Add errors
    #[allow(unused)]
    pub fn new(
        mqtt_provider: &mut impl MqttProvider<PS, PR>,
        options: TelemetryReceiverOptions,
    ) -> Result<Self, AIOProtocolError> {
        Ok(Self {
            ps_placeholder: PhantomData,
            pr_placeholder: PhantomData,
            message_payload_type: PhantomData,
            telemetry_name: options.telemetry_name,
        })
    }

    /// Start the [`TelemetryReceiver`]. Subscribes to the telemetry topic.
    ///
    /// Returns Ok(()) on success, otherwise returns [`AIOProtocolError`].
    /// # Errors
    /// TODO: Add errors
    #[allow(clippy::unused_async)]
    pub async fn start(&mut self) -> Result<(), AIOProtocolError> {
        Ok(())
    }

    /// Stop the [`TelemetryReceiver`]. Unsubscribes from the telemetry topic.
    ///
    /// Returns Ok(()) on success, otherwise returns [`AIOProtocolError`].
    /// # Errors
    /// TODO: Add errors
    #[allow(clippy::unused_async)]
    pub async fn stop(&mut self) -> Result<(), AIOProtocolError> {
        Ok(())
    }

    /// Receive a telemetry message.
    ///
    /// Returns [`TelemetryMessage`] on success, otherwise returns [`AIOProtocolError`].
    /// # Errors
    /// TODO: Add errors
    ///
    /// # Panics
    /// TODO: Remove ability to panic
    #[allow(clippy::unused_async)]
    pub async fn recv(&mut self) -> Result<TelemetryMessage<T>, AIOProtocolError> {
        Ok(TelemetryMessageBuilder::default()
            .payload(T::deserialize(&[]).unwrap())
            .build()
            .unwrap())
    }
}

impl<T, PS, PR> Drop for TelemetryReceiver<T, PS, PR>
where
    T: PayloadSerialize,
    PS: MqttPubSub + Clone + Send + Sync,
    PR: MqttPubReceiver + MqttAck + Send + Sync,
{
    fn drop(&mut self) {}
}
