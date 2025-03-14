// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

use std::error::Error;
use std::fmt;
use std::time::Duration;

use crate::common::{
    hybrid_logical_clock::{HLCError, HLCErrorKind, ParseHLCError},
    topic_processor::{TopicPatternError, TopicPatternErrorKind},
};

/// Represents the kind of error that occurs in an Azure IoT Operations Protocol
#[derive(Debug, PartialEq)]
pub enum AIOProtocolErrorKind {
    /// A required MQTT header property is missing on a received message
    HeaderMissing,
    /// An MQTT header property has an invalid value on a received message
    HeaderInvalid,
    /// MQTT payload cannot be serialized or deserialized
    PayloadInvalid,
    /// An operation was aborted due to timeout
    Timeout,
    /// An operation was cancelled
    Cancellation,
    /// An argument, struct or enum field, configuration file, or environment variable has an invalid value
    ConfigurationInvalid,
    /// The current program state is invalid vis-a-vis the function that was called
    StateInvalid,
    /// The client or service observed a condition that was thought to be impossible
    InternalLogicError,
    /// The client or service received an unexpected error from a dependent component
    UnknownError,
    /// The command processor encountered an error while executing the command
    ExecutionException,
    /// The MQTT communication encountered an error and failed. The exception message should be inspected for additional information
    ClientError,
    /// A request or response was received containing a protocol version that is not supported
    UnsupportedVersion,
}

/// Represents the possible types of the value of a property
#[derive(Debug, PartialEq)]
pub enum Value {
    /// A 32-bit integer value
    Integer(i32),
    /// A 64-bit floating point value
    Float(f64),
    /// A String value
    String(String),
    /// A bool value
    Boolean(bool),
}

/// Represents an error that occurred in the Azure IoT Operations Protocol
#[derive(Debug)]
pub struct AIOProtocolError {
    /// The error message
    pub message: Option<String>,
    /// The specific kind of error that occurred
    pub kind: AIOProtocolErrorKind,
    /// True if the error was identified immediately after the API was called, prior to any attempted network communication
    pub is_shallow: bool,
    /// True if the error was detected by a remote component
    pub is_remote: bool,
    /// Error from a dependent component that caused this error
    pub nested_error: Option<Box<dyn Error + Send + Sync>>,
    /// The name of a MQTT header that is missing or has an invalid value
    pub header_name: Option<String>,
    /// The value of a MQTT header that is invalid
    pub header_value: Option<String>,
    /// The name of a timeout condition that elapsed
    pub timeout_name: Option<String>,
    /// The duration of a timeout condition that elapsed
    pub timeout_value: Option<Duration>,
    /// The name of a function argument or a field in a struct or enum, configuration file, or environment variable that is missing or has an invalid value
    pub property_name: Option<String>,
    /// The value of a function argument or a field in a struct or enum, configuration file, or environment variable that is invalid
    pub property_value: Option<Value>,
    /// The name of a command relevant to the error being reported
    pub command_name: Option<String>,
    /// The protocol version of the command request or response that was not supported.
    pub protocol_version: Option<String>,
    /// The acceptable major protocol versions for the command executor if it rejected the
    /// command request, or for the command invoker if it rejected the command response.
    pub supported_protocol_major_versions: Option<Vec<u16>>,
}

impl fmt::Display for AIOProtocolError {
    fn fmt(&self, f: &mut fmt::Formatter<'_>) -> fmt::Result {
        if let Some(message) = &self.message {
            write!(f, "{message}")
        } else {
            match self.kind {
                AIOProtocolErrorKind::HeaderMissing => write!(
                    f,
                    "The MQTT header '{}' is missing",
                    self.header_name.as_deref().unwrap_or("Not Specified")
                ),
                AIOProtocolErrorKind::HeaderInvalid => write!(
                    f,
                    "The MQTT header '{}' has an invalid value: '{}'",
                    self.header_name.as_deref().unwrap_or("Not Specified"),
                    self.header_value.as_deref().unwrap_or("Not Specified")
                ),
                AIOProtocolErrorKind::PayloadInvalid => write!(
                    f,
                    "Serialization or deserialization of the MQTT payload failed"
                ),
                AIOProtocolErrorKind::Timeout => write!(
                    f,
                    "The timeout '{}' elapsed after {} ms",
                    self.timeout_name.as_deref().unwrap_or("Not Specified"),
                    self.timeout_value.map_or_else(
                        || "Not Specified".to_string(),
                        |d| d.as_millis().to_string()
                    )
                ),
                AIOProtocolErrorKind::Cancellation => write!(f, "The operation was cancelled"),
                AIOProtocolErrorKind::ConfigurationInvalid => {
                    if let Some(property_value) = &self.property_value {
                        write!(
                            f,
                            "The property '{}' has an invalid value: {:?}",
                            self.property_name.as_deref().unwrap_or("Not Specified"),
                            property_value
                        )
                    } else {
                        write!(
                            f,
                            "The property '{}' has an invalid value: 'Not Specified'",
                            self.property_name.as_deref().unwrap_or("Not Specified")
                        )
                    }
                }
                AIOProtocolErrorKind::StateInvalid => write!(
                    f,
                    "Invalid state in property '{}'",
                    self.property_name.as_deref().unwrap_or("Not Specified")
                ),
                AIOProtocolErrorKind::InternalLogicError => write!(
                    f,
                    "Internal logic error in property '{}'",
                    self.property_name.as_deref().unwrap_or("Not Specified")
                ),
                AIOProtocolErrorKind::UnknownError => write!(f, "An unknown error occurred"),
                AIOProtocolErrorKind::ExecutionException => write!(
                    f,
                    "The command processor encountered an error while executing the command"
                ),
                AIOProtocolErrorKind::ClientError => {
                    write!(f, "An MQTT communication error occurred")
                }
                AIOProtocolErrorKind::UnsupportedVersion => {
                    write!(f, "Received data with an unsupported protocol version '{}', but only major protocol versions '{:?}' are supported.",
                    self.protocol_version.as_deref().unwrap_or("Not Specified"),
                    self.supported_protocol_major_versions.as_deref().unwrap_or(&[]))
                }
            }
        }
    }
}

impl Error for AIOProtocolError {
    fn source(&self) -> Option<&(dyn Error + 'static)> {
        self.nested_error
            .as_ref()
            .map(|e| e.as_ref() as &(dyn Error + 'static))
    }
}

impl AIOProtocolError {
    pub(crate) fn config_invalid_from_topic_pattern_error(
        error: TopicPatternError,
        pattern_var_name: &str,
    ) -> AIOProtocolError {
        // NOTE: There is some inefficiency here with string allocations.
        // This will be sorted out when AIOProtocolError is replaced in the near future.
        let err_msg = format!("{error}");
        match error.kind() {
            TopicPatternErrorKind::InvalidPattern(pattern) => {
                let pattern = pattern.to_string();
                AIOProtocolError::new_configuration_invalid_error(
                    Some(Box::new(error)),
                    pattern_var_name,
                    Value::String(pattern),
                    Some(err_msg),
                    None,
                )
            }
            TopicPatternErrorKind::InvalidShareName(share_name) => {
                let share_name = share_name.to_string();
                AIOProtocolError::new_configuration_invalid_error(
                    Some(Box::new(error)),
                    "share_name",
                    Value::String(share_name),
                    Some(err_msg),
                    None,
                )
            }
            TopicPatternErrorKind::InvalidNamespace(namespace) => {
                let namespace = namespace.to_string();
                AIOProtocolError::new_configuration_invalid_error(
                    Some(Box::new(error)),
                    "topic_namespace",
                    Value::String(namespace),
                    Some(err_msg),
                    None,
                )
            }
            TopicPatternErrorKind::InvalidTokenReplacement(token, replacement) => {
                let token = token.clone();
                let replacement = replacement.to_string();
                AIOProtocolError::new_configuration_invalid_error(
                    Some(Box::new(error)),
                    &token,
                    Value::String(replacement.to_string()),
                    Some(err_msg),
                    None,
                )
            }
        }
    }

    /// Creates a new [`AIOProtocolError`] for a missing MQTT header
    #[must_use]
    pub fn new_header_missing_error(
        header_name: &str,
        is_remote: bool,
        message: Option<String>,
        command_name: Option<String>,
    ) -> AIOProtocolError {
        let mut e = AIOProtocolError {
            message,
            kind: AIOProtocolErrorKind::HeaderMissing,
            is_shallow: false,
            is_remote,
            nested_error: None,
            header_name: Some(header_name.to_string()),
            header_value: None,
            timeout_name: None,
            timeout_value: None,
            property_name: None,
            property_value: None,
            command_name,
            protocol_version: None,
            supported_protocol_major_versions: None,
        };
        e.ensure_error_message();
        e
    }

    /// Creates a new [`AIOProtocolError`] for an invalid MQTT header value
    #[must_use]
    pub fn new_header_invalid_error(
        header_name: &str,
        header_value: &str,
        is_remote: bool,
        message: Option<String>,
        command_name: Option<String>,
    ) -> AIOProtocolError {
        let mut e = AIOProtocolError {
            message,
            kind: AIOProtocolErrorKind::HeaderInvalid,
            is_shallow: false,
            is_remote,
            nested_error: None,
            header_name: Some(header_name.to_string()),
            header_value: Some(header_value.to_string()),
            timeout_name: None,
            timeout_value: None,
            property_name: None,
            property_value: None,
            command_name,
            protocol_version: None,
            supported_protocol_major_versions: None,
        };
        e.ensure_error_message();
        e
    }

    /// Creates a new [`AIOProtocolError`] for an invalid MQTT payload
    #[must_use]
    pub fn new_payload_invalid_error(
        is_shallow: bool,
        is_remote: bool,
        nested_error: Option<Box<dyn Error + Send + Sync>>,
        message: Option<String>,
        command_name: Option<String>,
    ) -> AIOProtocolError {
        let mut e = AIOProtocolError {
            message,
            kind: AIOProtocolErrorKind::PayloadInvalid,
            is_shallow,
            is_remote,
            nested_error,
            header_name: None,
            header_value: None,
            timeout_name: None,
            timeout_value: None,
            property_name: None,
            property_value: None,
            command_name,
            protocol_version: None,
            supported_protocol_major_versions: None,
        };
        e.ensure_error_message();
        e
    }

    /// Creates a new [`AIOProtocolError`] for a timeout
    #[must_use]
    pub fn new_timeout_error(
        is_remote: bool,
        nested_error: Option<Box<dyn Error + Send + Sync>>,
        timeout_name: &str,
        timeout_value: Duration,
        message: Option<String>,
        command_name: Option<String>,
    ) -> AIOProtocolError {
        let mut e = AIOProtocolError {
            message,
            kind: AIOProtocolErrorKind::Timeout,
            is_shallow: false,
            is_remote,
            nested_error,
            header_name: None,
            header_value: None,
            timeout_name: Some(timeout_name.to_string()),
            timeout_value: Some(timeout_value),
            property_name: None,
            property_value: None,
            command_name,
            protocol_version: None,
            supported_protocol_major_versions: None,
        };
        e.ensure_error_message();
        e
    }

    /// Creates a new [`AIOProtocolError`] for a cancellation error
    #[must_use]
    pub fn new_cancellation_error(
        is_remote: bool,
        nested_error: Option<Box<dyn Error + Send + Sync>>,
        message: Option<String>,
        command_name: Option<String>,
    ) -> AIOProtocolError {
        let mut e = AIOProtocolError {
            message,
            kind: AIOProtocolErrorKind::Cancellation,
            is_shallow: false,
            is_remote,
            nested_error,
            header_name: None,
            header_value: None,
            timeout_name: None,
            timeout_value: None,
            property_name: None,
            property_value: None,
            command_name,
            protocol_version: None,
            supported_protocol_major_versions: None,
        };
        e.ensure_error_message();
        e
    }

    /// Creates a new [`AIOProtocolError`] for an invalid configuration error
    #[must_use]
    pub fn new_configuration_invalid_error(
        nested_error: Option<Box<dyn Error + Send + Sync>>,
        property_name: &str,
        property_value: Value,
        message: Option<String>,
        command_name: Option<String>,
    ) -> AIOProtocolError {
        let mut e = AIOProtocolError {
            message,
            kind: AIOProtocolErrorKind::ConfigurationInvalid,
            is_shallow: true,
            is_remote: false,
            nested_error,
            header_name: None,
            header_value: None,
            timeout_name: None,
            timeout_value: None,
            property_name: Some(property_name.to_string()),
            property_value: Some(property_value),
            command_name,
            protocol_version: None,
            supported_protocol_major_versions: None,
        };
        e.ensure_error_message();
        e
    }

    /// Creates a new [`AIOProtocolError`] for an invalid state error
    #[must_use]
    pub fn new_state_invalid_error(
        property_name: &str,
        property_value: Option<Value>,
        message: Option<String>,
        command_name: Option<String>,
    ) -> AIOProtocolError {
        let mut e = AIOProtocolError {
            message,
            kind: AIOProtocolErrorKind::StateInvalid,
            is_shallow: true,
            is_remote: false,
            nested_error: None,
            header_name: None,
            header_value: None,
            timeout_name: None,
            timeout_value: None,
            property_name: Some(property_name.to_string()),
            property_value,
            command_name,
            protocol_version: None,
            supported_protocol_major_versions: None,
        };
        e.ensure_error_message();
        e
    }

    /// Creates a new [`AIOProtocolError`] for an internal logic error
    #[must_use]
    #[allow(clippy::too_many_arguments)]
    pub fn new_internal_logic_error(
        is_shallow: bool,
        is_remote: bool,
        nested_error: Option<Box<dyn Error + Send + Sync>>,
        property_name: &str,
        property_value: Option<Value>,
        message: Option<String>,
        command_name: Option<String>,
    ) -> AIOProtocolError {
        let mut e = AIOProtocolError {
            message,
            kind: AIOProtocolErrorKind::InternalLogicError,
            is_shallow,
            is_remote,
            nested_error,
            header_name: None,
            header_value: None,
            timeout_name: None,
            timeout_value: None,
            property_name: Some(property_name.to_string()),
            property_value,
            command_name,
            protocol_version: None,
            supported_protocol_major_versions: None,
        };
        e.ensure_error_message();
        e
    }

    /// Creates a new [`AIOProtocolError`] for an unknown error
    #[must_use]
    pub fn new_unknown_error(
        is_remote: bool,
        is_shallow: bool,
        nested_error: Option<Box<dyn Error + Send + Sync>>,
        message: Option<String>,
        command_name: Option<String>,
    ) -> AIOProtocolError {
        let mut e = AIOProtocolError {
            message,
            kind: AIOProtocolErrorKind::UnknownError,
            is_shallow,
            is_remote,
            nested_error,
            header_name: None,
            header_value: None,
            timeout_name: None,
            timeout_value: None,
            property_name: None,
            property_value: None,
            command_name,
            protocol_version: None,
            supported_protocol_major_versions: None,
        };
        e.ensure_error_message();
        e
    }

    /// Creates a new [`AIOProtocolError`] for an execution exception error
    #[must_use]
    pub fn new_execution_exception_error(
        property_name: Option<&str>,
        property_value: Option<Value>,
        message: Option<String>,
        command_name: Option<String>,
    ) -> AIOProtocolError {
        let mut e = AIOProtocolError {
            message,
            kind: AIOProtocolErrorKind::ExecutionException,
            is_shallow: false,
            is_remote: true,
            nested_error: None,
            header_name: None,
            header_value: None,
            timeout_name: None,
            timeout_value: None,
            property_name: property_name.map(std::string::ToString::to_string),
            property_value,
            command_name,
            protocol_version: None,
            supported_protocol_major_versions: None,
        };
        e.ensure_error_message();
        e
    }

    /// Creates a new [`AIOProtocolError`] for an MQTT communication error
    #[must_use]
    pub fn new_mqtt_error(
        message: Option<String>,
        nested_error: Box<dyn Error + Send + Sync>,
        command_name: Option<String>,
    ) -> AIOProtocolError {
        let mut e = AIOProtocolError {
            message,
            kind: AIOProtocolErrorKind::ClientError,
            is_shallow: false,
            is_remote: false,
            nested_error: Some(nested_error),
            header_name: None,
            header_value: None,
            timeout_name: None,
            timeout_value: None,
            property_name: None,
            property_value: None,
            command_name,
            protocol_version: None,
            supported_protocol_major_versions: None,
        };
        e.ensure_error_message();
        e
    }

    /// Creates a new [`AIOProtocolError`] for an unsupported request version error
    #[must_use]
    pub fn new_unsupported_version_error(
        message: Option<String>,
        protocol_version: String,
        supported_protocol_major_versions: Vec<u16>,
        command_name: Option<String>,
        is_shallow: bool,
        is_remote: bool,
    ) -> AIOProtocolError {
        let mut e = AIOProtocolError {
            message,
            kind: AIOProtocolErrorKind::UnsupportedVersion,
            is_shallow,
            is_remote,
            nested_error: None,
            header_name: None,
            header_value: None,
            timeout_name: None,
            timeout_value: None,
            property_name: None,
            property_value: None,
            command_name,
            protocol_version: Some(protocol_version),
            supported_protocol_major_versions: Some(supported_protocol_major_versions),
        };
        e.ensure_error_message();
        e
    }

    /// Sets the error's message to a default value if a custom message is not already set
    pub fn ensure_error_message(&mut self) {
        if self.message.is_none() {
            self.message = Some(self.to_string());
        }
    }
}

impl From<HLCError> for AIOProtocolError {
    fn from(error: HLCError) -> Self {
        let (property_name, message) = match error.kind() {
            HLCErrorKind::OverflowWarning => {
                ("Counter", "Integer overflow on HybridLogicalClock counter")
            }
            HLCErrorKind::ClockDrift => (
                "MaxClockDrift",
                "HybridLogicalClock drift is greater than the maximum allowed drift",
            ),
        };

        AIOProtocolError::new_state_invalid_error(
            property_name,
            None,
            Some(message.to_string()),
            None,
        )
    }
}

impl From<ParseHLCError> for AIOProtocolError {
    fn from(error: ParseHLCError) -> Self {
        AIOProtocolError::new_header_invalid_error(
            "HybridLogicalClock",
            error.input.as_str(),
            false,
            Some(format!("{error}")),
            None,
        )
    }
}
