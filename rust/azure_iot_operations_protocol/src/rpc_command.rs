// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

//! Envoys for Remote Procedure Call (RPC) operations.

use std::str::FromStr;

use crate::ProtocolVersion;

/// This module contains the command invoker implementation.
pub mod invoker;

/// This module contains the command executor implementation.
pub mod executor;

/// Re-export the command invoker and executor for ease of use.
pub use executor::Executor;
pub use invoker::Invoker;

/// Protocol version used by all command envoys in this module
pub(crate) const RPC_COMMAND_PROTOCOL_VERSION: ProtocolVersion =
    ProtocolVersion { major: 1, minor: 0 };
/// Assumed version if no version is provided.
pub(crate) const DEFAULT_RPC_COMMAND_PROTOCOL_VERSION: ProtocolVersion =
    ProtocolVersion { major: 1, minor: 0 };

/// Represents the valid status codes for command responses.
#[repr(u16)]
#[derive(Debug, Copy, Clone, PartialEq)]
pub(crate) enum StatusCode {
    /// No error.
    Ok = 200,

    /// There is no content to send for this response.
    NoContent = 204,

    /// Header or payload is missing or invalid.
    BadRequest = 400,

    /// The request timed out before a response could be received from the command processor.
    RequestTimeout = 408,

    /// The content type specified in the request is not supported by this implementation.
    UnsupportedMediaType = 415,

    /// Unknown error, internal logic error, or command processor error.
    InternalServerError = 500,

    /// Invalid service state preventing command from executing properly.
    ServiceUnavailable = 503,
    /// The request failed because the remote party did not support the requested protocol version.
    VersionNotSupported = 505,
}

impl FromStr for StatusCode {
    type Err = StatusCodeParseError;

    fn from_str(s: &str) -> Result<Self, StatusCodeParseError> {
        match s.parse::<u16>() {
            Ok(status) => match status {
                x if x == StatusCode::Ok as u16 => Ok(StatusCode::Ok),
                x if x == StatusCode::NoContent as u16 => Ok(StatusCode::NoContent),
                x if x == StatusCode::BadRequest as u16 => Ok(StatusCode::BadRequest),
                x if x == StatusCode::RequestTimeout as u16 => Ok(StatusCode::RequestTimeout),
                x if x == StatusCode::UnsupportedMediaType as u16 => {
                    Ok(StatusCode::UnsupportedMediaType)
                }
                x if x == StatusCode::InternalServerError as u16 => {
                    Ok(StatusCode::InternalServerError)
                }
                x if x == StatusCode::ServiceUnavailable as u16 => {
                    Ok(StatusCode::ServiceUnavailable)
                }
                x if x == StatusCode::VersionNotSupported as u16 => {
                    Ok(StatusCode::VersionNotSupported)
                }
                _ => Err(StatusCodeParseError::UnknownStatusCode(status)),
            },
            Err(_) => Err(StatusCodeParseError::UnparsableStatusCode(s.to_string())),
        }
    }
}

#[derive(thiserror::Error, Debug)]
pub(crate) enum StatusCodeParseError {
    #[error("Unparsable status code: {0}")]
    UnparsableStatusCode(String),
    #[error("Unknown status code: {0}")]
    UnknownStatusCode(u16),
}

#[cfg(test)]
mod tests {
    use std::str::FromStr;

    use test_case::test_case;

    use super::*;

    #[test_case(StatusCode::Ok; "Ok")]
    #[test_case(StatusCode::NoContent; "NoContent")]
    #[test_case(StatusCode::BadRequest; "BadRequest")]
    #[test_case(StatusCode::RequestTimeout; "RequestTimeout")]
    #[test_case(StatusCode::UnsupportedMediaType; "UnsupportedMediaType")]
    #[test_case(StatusCode::InternalServerError; "InternalServerError")]
    #[test_case(StatusCode::ServiceUnavailable; "ServiceUnavailable")]
    #[test_case(StatusCode::VersionNotSupported; "VersionNotSupported")]
    fn test_to_from_string(status_code: StatusCode) {
        assert_eq!(
            status_code,
            StatusCode::from_str(&(status_code as u16).to_string()).unwrap()
        );
    }

    #[test]
    fn test_invalid_status_code() {
        let test_invalid_code = "not a number";
        let code_result = StatusCode::from_str(test_invalid_code);
        match code_result {
            Ok(_) => panic!("Expected error"),
            Err(StatusCodeParseError::UnparsableStatusCode(s)) => {
                assert_eq!(s, test_invalid_code.to_string());
            }
            Err(StatusCodeParseError::UnknownStatusCode(_)) => {
                panic!("Expected UnparsableStatusCode error")
            }
        }
    }

    #[test]
    fn test_unknown_status_code() {
        let test_unknown_code = 201;
        let code_result = StatusCode::from_str(&test_unknown_code.to_string());
        match code_result {
            Ok(_) => panic!("Expected error"),
            Err(StatusCodeParseError::UnknownStatusCode(s)) => {
                assert_eq!(s, test_unknown_code);
            }
            Err(StatusCodeParseError::UnparsableStatusCode(_)) => {
                panic!("Expected UnknownStatusCode error")
            }
        }
    }
}
