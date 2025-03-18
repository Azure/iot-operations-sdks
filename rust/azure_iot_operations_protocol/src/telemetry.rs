// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

//! Envoys for Telemetry operations.
use crate::ProtocolVersion;

/// This module contains the telemetry sender implementation.
pub mod sender;

/// This module contains the telemetry receiver implementation.
pub mod receiver;

/// This module contains the cloud events enum for the Azure IoT Operations Protocol.
pub mod cloud_event;

/// Re-export the telemetry sender and receiver for ease of use.
pub use receiver::Receiver;
pub use sender::Sender;

/// Protocol version used by all envoys in this module
pub(crate) const TELEMETRY_PROTOCOL_VERSION: ProtocolVersion =
    ProtocolVersion { major: 1, minor: 0 };
/// Assumed version if no version is provided.
pub(crate) const DEFAULT_TELEMETRY_PROTOCOL_VERSION: ProtocolVersion =
    ProtocolVersion { major: 1, minor: 0 };
