// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

/// Error type for MQTT client
pub type ClientError = rumqttc::v5::ClientError;
/// Error type for MQTT connection
pub type ConnectionError = rumqttc::v5::ConnectionError;
/// Error type for completion tokens
pub type CompletionError = rumqttc::NoticeError;
/// Error subtype for MQTT connection error caused by state
pub type StateError = rumqttc::v5::StateError;
