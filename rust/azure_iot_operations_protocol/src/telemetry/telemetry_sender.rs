// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

use std::{collections::HashMap, marker::PhantomData, time::Duration};

use azure_iot_operations_mqtt::interface::{MqttAck, MqttProvider, MqttPubReceiver, MqttPubSub};

use super::TelemetryMessage;
use crate::common::{aio_protocol_error::AIOProtocolError, payload_serialize::PayloadSerialize};

/// Telemetry Sender Options struct
#[derive(Builder, Clone)]
#[builder(setter(into, strip_option))]
#[allow(unused)]
pub struct TelemetrySenderOptions {
    /// Topic pattern for the telemetry message
    /// Must align with [topic-structure.md](https://github.com/microsoft/mqtt-patterns/blob/main/docs/specs/topic-structure.md)
    topic_pattern: String,
    /// Telemetry name
    #[builder(default = "None")]
    telemetry_name: Option<String>,
    /// Model ID if required by the topic pattern
    #[builder(default = "None")]
    model_id: Option<String>,
    /// Optional Topic namespace to be prepended to the topic pattern
    #[builder(default = "None")]
    topic_namespace: Option<String>,
    /// Custom topic token keys/values to be replaced in the topic pattern
    #[builder(default)]
    custom_topic_token_map: HashMap<String, String>,
    /// Default telemetry message expiry to use if not specified on send
    default_telemetry_timeout: Duration,
}

/// Telemetry Sender struct
/// # Example
/// ```
/// # use std::{collections::HashMap, time::Duration};
/// # use tokio_test::block_on;
/// # use azure_iot_operations_mqtt::MqttConnectionSettingsBuilder;
/// # use azure_iot_operations_mqtt::control_packet::QoS;
/// # use azure_iot_operations_mqtt::session::{Session, SessionOptionsBuilder};
/// # use azure_iot_operations_protocol::telemetry::telemetry_sender::{TelemetrySender, TelemetrySenderOptionsBuilder};
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
/// #     .client_id("test_client")
/// #     .host_name("mqtt://localhost")
/// #     .tcp_port(1883u16)
/// #     .build().unwrap();
/// # let mut session_options = SessionOptionsBuilder::default()
/// #     .connection_settings(connection_settings)
/// #     .build().unwrap();
/// # let mut mqtt_session = Session::new(session_options).unwrap();
/// let sender_options = TelemetrySenderOptionsBuilder::default()
///   .topic_pattern("test/telemetry")
///   .telemetry_name("test_telemetry")
///   .model_id("test_model")
///   .topic_namespace("test_namespace")
///   .default_telemetry_timeout(Duration::from_secs(5))
///   .build().unwrap();
/// let telemetry_sender: TelemetrySender<SamplePayload, _> = TelemetrySender::new(&mqtt_session, sender_options).unwrap();
/// let telemetry_message = TelemetryMessageBuilder::default()
///   .payload(SamplePayload {})
///   .qos(QoS::AtLeastOnce)
///   .build().unwrap();
/// # tokio_test::block_on(async {
/// let result = telemetry_sender.send(telemetry_message, Some(Duration::from_secs(2))).await.unwrap();
/// # })
/// ```
///
#[allow(unused)]
pub struct TelemetrySender<T, PS>
where
    // TODO: Remove unnecessary PR bound here
    T: PayloadSerialize,
    PS: MqttPubSub + Clone + Send + Sync,
{
    pub_sub: PS,
    message_payload_type: PhantomData<T>,
    telemetry_name: Option<String>,
}

/// Implementation of Telemetry Sender
#[allow(unused)]
impl<T, PS> TelemetrySender<T, PS>
where
    T: PayloadSerialize,
    PS: MqttPubSub + Clone + Send + Sync,
{
    /// Creates a new [`TelemetrySender`].
    ///
    /// Returns Ok([`TelemetrySender`]) on success, otherwise returns [`AIOProtocolError`].
    /// # Errors
    /// TODO: Add errors
    pub fn new<PR: MqttPubReceiver + MqttAck + Send + Sync + 'static>(
        mqtt_provider: &impl MqttProvider<PS, PR>,
        sender_options: TelemetrySenderOptions,
    ) -> Result<Self, AIOProtocolError> {
        Ok(Self {
            pub_sub: mqtt_provider.pub_sub(),
            message_payload_type: PhantomData,
            telemetry_name: sender_options.telemetry_name,
        })
    }

    /// Sends a [`TelemetryMessage`].
    ///
    /// Returns `Ok(())` on success, otherwise returns [`AIOProtocolError`].
    /// # Arguments
    /// * `message` - [`TelemetryMessage`] to send
    /// * `timeout` - Timeout for the telemetry message. If not specified, the default timeout from the `TelemetrySenderOptions` will be used.
    /// # Errors
    /// TODO: Add errors
    #[allow(clippy::unused_async)]
    pub async fn send(
        &self,
        message: TelemetryMessage<T>,
        timeout: Option<Duration>,
    ) -> Result<(), AIOProtocolError> {
        Ok(())
    }
}
