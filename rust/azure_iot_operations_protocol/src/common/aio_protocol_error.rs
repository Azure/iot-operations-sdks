// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

use std::error::Error;
use std::fmt;
use std::time::Duration;

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
    /// A struct or enum field, configuration file, or environment variable has an invalid value
    ConfigurationInvalid,
    /// A function was called with an invalid argument value
    ArgumentInvalid,
    /// The current program state is invalid vis-a-vis the function that was called
    StateInvalid,
    /// The client or service observed a condition that was thought to be impossible
    InternalLogicError,
    /// The client or service received an unexpected error from a dependent component
    UnknownError,
    /// The command processor identified an error in the request
    InvocationException,
    /// The command processor encountered an error while executing the command
    ExecutionException,
    /// The MQTT communication encountered an error and failed. The exception message should be inspected for additional information
    ClientError,
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
    /// True if the error occurred in user-supplied code rather than the SDK or its dependent components
    pub in_application: bool,
    /// True if the error was identified immediately after the API was called, prior to any attempted network communication
    pub is_shallow: bool,
    /// True if the error was detected by a remote component
    pub is_remote: bool,
    /// Error from a dependent component that caused this error
    pub nested_error: Option<Box<dyn Error>>,
    /// An HTTP status code received from a remote service that caused the error being reported
    pub http_status_code: Option<u16>,
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
                AIOProtocolErrorKind::ArgumentInvalid => {
                    if let Some(property_value) = &self.property_value {
                        write!(
                            f,
                            "The argument '{}' has an invalid value: {:?}",
                            self.property_name.as_deref().unwrap_or("Not Specified"),
                            property_value
                        )
                    } else {
                        write!(
                            f,
                            "The argument '{}' has an invalid value: 'Not Specified'",
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
                AIOProtocolErrorKind::InvocationException => write!(
                    f,
                    "The command processor identified an error in the request"
                ),
                AIOProtocolErrorKind::ExecutionException => write!(
                    f,
                    "The command processor encountered an error while executing the command"
                ),
                AIOProtocolErrorKind::ClientError => {
                    write!(f, "An MQTT communication error occurred")
                }
            }
        }
    }
}

impl Error for AIOProtocolError {
    fn source(&self) -> Option<&(dyn Error + 'static)> {
        self.nested_error.as_ref().map(std::convert::AsRef::as_ref)
    }
}

impl AIOProtocolError {
    /// Creates a new [`AIOProtocolError`] for a missing MQTT header
    #[must_use]
    pub fn new_header_missing_error(
        header_name: &str,
        is_remote: bool,
        http_status_code: Option<u16>,
        message: Option<String>,
        command_name: Option<String>,
    ) -> AIOProtocolError {
        let mut e = AIOProtocolError {
            message,
            kind: AIOProtocolErrorKind::HeaderMissing,
            in_application: false,
            is_shallow: false,
            is_remote,
            nested_error: None,
            http_status_code,
            header_name: Some(header_name.to_string()),
            header_value: None,
            timeout_name: None,
            timeout_value: None,
            property_name: None,
            property_value: None,
            command_name,
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
        http_status_code: Option<u16>,
        message: Option<String>,
        command_name: Option<String>,
    ) -> AIOProtocolError {
        let mut e = AIOProtocolError {
            message,
            kind: AIOProtocolErrorKind::HeaderInvalid,
            in_application: false,
            is_shallow: false,
            is_remote,
            nested_error: None,
            http_status_code,
            header_name: Some(header_name.to_string()),
            header_value: Some(header_value.to_string()),
            timeout_name: None,
            timeout_value: None,
            property_name: None,
            property_value: None,
            command_name,
        };
        e.ensure_error_message();
        e
    }

    /// Creates a new [`AIOProtocolError`] for an invalid MQTT payload
    #[must_use]
    pub fn new_payload_invalid_error(
        is_shallow: bool,
        is_remote: bool,
        nested_error: Option<Box<dyn Error>>,
        http_status_code: Option<u16>,
        message: Option<String>,
        command_name: Option<String>,
    ) -> AIOProtocolError {
        let mut e = AIOProtocolError {
            message,
            kind: AIOProtocolErrorKind::PayloadInvalid,
            in_application: false,
            is_shallow,
            is_remote,
            nested_error,
            http_status_code,
            header_name: None,
            header_value: None,
            timeout_name: None,
            timeout_value: None,
            property_name: None,
            property_value: None,
            command_name,
        };
        e.ensure_error_message();
        e
    }

    /// Creates a new [`AIOProtocolError`] for a timeout
    #[must_use]
    pub fn new_timeout_error(
        is_remote: bool,
        nested_error: Option<Box<dyn Error>>,
        http_status_code: Option<u16>,
        timeout_name: &str,
        timeout_value: Duration,
        message: Option<String>,
        command_name: Option<String>,
    ) -> AIOProtocolError {
        let mut e = AIOProtocolError {
            message,
            kind: AIOProtocolErrorKind::Timeout,
            in_application: false,
            is_shallow: false,
            is_remote,
            nested_error,
            http_status_code,
            header_name: None,
            header_value: None,
            timeout_name: Some(timeout_name.to_string()),
            timeout_value: Some(timeout_value),
            property_name: None,
            property_value: None,
            command_name,
        };
        e.ensure_error_message();
        e
    }

    /// Creates a new [`AIOProtocolError`] for a cancellation error
    #[must_use]
    pub fn new_cancellation_error(
        is_remote: bool,
        nested_error: Option<Box<dyn Error>>,
        http_status_code: Option<u16>,
        message: Option<String>,
        command_name: Option<String>,
    ) -> AIOProtocolError {
        let mut e = AIOProtocolError {
            message,
            kind: AIOProtocolErrorKind::Cancellation,
            in_application: false,
            is_shallow: false,
            is_remote,
            nested_error,
            http_status_code,
            header_name: None,
            header_value: None,
            timeout_name: None,
            timeout_value: None,
            property_name: None,
            property_value: None,
            command_name,
        };
        e.ensure_error_message();
        e
    }

    /// Creates a new [`AIOProtocolError`] for an invalid configuration error
    #[must_use]
    pub fn new_configuration_invalid_error(
        nested_error: Option<Box<dyn Error>>,
        property_name: &str,
        property_value: Value,
        message: Option<String>,
        command_name: Option<String>,
    ) -> AIOProtocolError {
        let mut e = AIOProtocolError {
            message,
            kind: AIOProtocolErrorKind::ConfigurationInvalid,
            in_application: false,
            is_shallow: true,
            is_remote: false,
            nested_error,
            http_status_code: None,
            header_name: None,
            header_value: None,
            timeout_name: None,
            timeout_value: None,
            property_name: Some(property_name.to_string()),
            property_value: Some(property_value),
            command_name,
        };
        e.ensure_error_message();
        e
    }

    /// Creates a new [`AIOProtocolError`] for an invalid argument error
    #[must_use]
    pub fn new_argument_invalid_error(
        property_name: &str,
        property_value: Value,
        message: Option<String>,
        command_name: Option<String>,
    ) -> AIOProtocolError {
        let mut e = AIOProtocolError {
            message,
            kind: AIOProtocolErrorKind::ArgumentInvalid,
            in_application: false,
            is_shallow: true,
            is_remote: false,
            nested_error: None,
            http_status_code: None,
            header_name: None,
            header_value: None,
            timeout_name: None,
            timeout_value: None,
            property_name: Some(property_name.to_string()),
            property_value: Some(property_value),
            command_name,
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
            in_application: false,
            is_shallow: true,
            is_remote: false,
            nested_error: None,
            http_status_code: None,
            header_name: None,
            header_value: None,
            timeout_name: None,
            timeout_value: None,
            property_name: Some(property_name.to_string()),
            property_value,
            command_name,
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
        nested_error: Option<Box<dyn Error>>,
        http_status_code: Option<u16>,
        property_name: &str,
        property_value: Option<Value>,
        message: Option<String>,
        command_name: Option<String>,
    ) -> AIOProtocolError {
        let mut e = AIOProtocolError {
            message,
            kind: AIOProtocolErrorKind::InternalLogicError,
            in_application: false,
            is_shallow,
            is_remote,
            nested_error,
            http_status_code,
            header_name: None,
            header_value: None,
            timeout_name: None,
            timeout_value: None,
            property_name: Some(property_name.to_string()),
            property_value,
            command_name,
        };
        e.ensure_error_message();
        e
    }

    /// Creates a new [`AIOProtocolError`] for an unknown error
    #[must_use]
    pub fn new_unknown_error(
        is_remote: bool,
        is_shallow: bool,
        nested_error: Option<Box<dyn Error>>,
        http_status_code: Option<u16>,
        message: Option<String>,
        command_name: Option<String>,
    ) -> AIOProtocolError {
        let mut e = AIOProtocolError {
            message,
            kind: AIOProtocolErrorKind::UnknownError,
            in_application: false,
            is_shallow,
            is_remote,
            nested_error,
            http_status_code,
            header_name: None,
            header_value: None,
            timeout_name: None,
            timeout_value: None,
            property_name: None,
            property_value: None,
            command_name,
        };
        e.ensure_error_message();
        e
    }

    /// Creates a new [`AIOProtocolError`] for an invocation exception
    #[must_use]
    pub fn new_invocation_exception_error(
        http_status_code: u16,
        property_name: Option<&str>,
        property_value: Option<Value>,
        message: Option<String>,
        command_name: Option<String>,
    ) -> AIOProtocolError {
        let mut e = AIOProtocolError {
            message,
            kind: AIOProtocolErrorKind::InvocationException,
            in_application: true,
            is_shallow: false,
            is_remote: true,
            nested_error: None,
            http_status_code: Some(http_status_code),
            header_name: None,
            header_value: None,
            timeout_name: None,
            timeout_value: None,
            property_name: property_name.map(std::string::ToString::to_string),
            property_value,
            command_name,
        };
        e.ensure_error_message();
        e
    }

    /// Creates a new [`AIOProtocolError`] for an execution exception error
    #[must_use]
    pub fn new_execution_exception_error(
        http_status_code: u16,
        property_name: Option<&str>,
        property_value: Option<Value>,
        message: Option<String>,
        command_name: Option<String>,
    ) -> AIOProtocolError {
        let mut e = AIOProtocolError {
            message,
            kind: AIOProtocolErrorKind::ExecutionException,
            in_application: true,
            is_shallow: false,
            is_remote: true,
            nested_error: None,
            http_status_code: Some(http_status_code),
            header_name: None,
            header_value: None,
            timeout_name: None,
            timeout_value: None,
            property_name: property_name.map(std::string::ToString::to_string),
            property_value,
            command_name,
        };
        e.ensure_error_message();
        e
    }

    /// Creates a new [`AIOProtocolError`] for an MQTT communication error
    #[must_use]
    pub fn new_mqtt_error(
        message: Option<String>,
        nested_error: Box<dyn Error>,
        command_name: Option<String>,
    ) -> AIOProtocolError {
        let mut e = AIOProtocolError {
            message,
            kind: AIOProtocolErrorKind::ClientError,
            in_application: false,
            is_shallow: false,
            is_remote: false,
            nested_error: Some(nested_error),
            http_status_code: None,
            header_name: None,
            header_value: None,
            timeout_name: None,
            timeout_value: None,
            property_name: None,
            property_value: None,
            command_name,
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
