// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

//! Common utilities for use with the protocol.

/// Implementation of hybrid logical clock.
pub mod hybrid_logical_clock;

/// This module contains a trait that payload structs should implement to be serializable.
pub mod payload_serialize;

/// This module contains the error type for the Azure IoT Operations Protocol.
pub mod aio_protocol_error;

/// This module contains the topic processor functions for the Azure IoT Operations Protocol
pub mod topic_processor;

/// This module contains string values for Azure IoT Operations Protocol defined user properties.
pub mod user_properties;

/// Used to validate that a string is well-formed UTF-8 per the [MQTT 5 spec](https://docs.oasis-open.org/mqtt/mqtt/v5.0/os/mqtt-v5.0-os.html#_UTF-8_Encoded_String)
#[must_use]
pub fn is_invalid_utf8(s: &str) -> bool {
    s.chars()
        .any(|c| ('\u{0000}'..='\u{001F}').contains(&c) || ('\u{007F}'..='\u{009F}').contains(&c))
}
