// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

use azure_iot_operations_mqtt::control_packet::QoS;

use crate::common::{
    hybrid_logical_clock::HybridLogicalClock, payload_serialize::PayloadSerialize,
};

/// This module contains the telemetry sender implementation.
pub mod telemetry_sender;

/// This module contains the telemetry receiver implementation.
pub mod telemetry_receiver;

/// Telemetry Message struct
/// Used by the telemetry sender and telemetry receiver.
#[derive(Builder, Clone)]
#[builder(setter(into))]
pub struct TelemetryMessage<T: PayloadSerialize> {
    /// Payload of the telemetry message. Must implement `PayloadSerialize`.
    pub payload: T,
    /// Quality of Service of the telemetry message. Can only be `AtMostOnce` or `AtLeastOnce`.
    #[builder(default = "QoS::AtLeastOnce")]
    pub qos: QoS,
    /// User data that will be set as custom MQTT User Properties on the telemetry message.
    /// Can be used to pass additional metadata to the receiver.
    /// Default is an empty `HashMap`.
    #[builder(default)]
    pub custom_user_data: Vec<(String, String)>,
    /// Timestamp of the telemetry message. Not to be set by the application.
    #[builder(default)]
    #[builder(private)]
    pub timestamp: HybridLogicalClock,
    /// Sender ID of the telemetry message. Not to be set by the application.
    #[builder(default = "None")]
    #[builder(private)]
    pub sender_id: Option<String>,
}
