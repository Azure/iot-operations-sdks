// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

//! Envoys for Remote Procedure Call (RPC) operations.

use std::str::FromStr;

use crate::common::aio_protocol_error::AIOProtocolError;

/// This module contains the command invoker implementation.
pub mod command_invoker;

/// This module contains the command executor implementation.
pub mod command_executor;

/// Represents the valid status codes for command responses.
#[repr(u16)]
#[derive(Debug, Copy, Clone, PartialEq)]
pub enum StatusCode {
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

    /// The request was well-formed but was unable to be followed due to semantic errors, as indicated via an [`InvocationException`](crate::common::aio_protocol_error::AIOProtocolErrorKind::InvocationException).
    UnprocessableContent = 422,

    /// Unknown error, internal logic error, or command processor error other than [`InvocationException`](crate::common::aio_protocol_error::AIOProtocolErrorKind::InvocationException).
    InternalServerError = 500,

    /// Invalid service state preventing command from executing properly.
    ServiceUnavailable = 503,
    /// The request failed because the remote party did not support the requested protocol version.
    VersionNotSupported = 505,
}

impl FromStr for StatusCode {
    type Err = AIOProtocolError;

    fn from_str(s: &str) -> Result<Self, AIOProtocolError> {
        match s.parse::<u16>() {
            Ok(status) => match status {
                x if x == StatusCode::Ok as u16 => Ok(StatusCode::Ok),
                x if x == StatusCode::NoContent as u16 => Ok(StatusCode::NoContent),
                x if x == StatusCode::BadRequest as u16 => Ok(StatusCode::BadRequest),
                x if x == StatusCode::RequestTimeout as u16 => Ok(StatusCode::RequestTimeout),
                x if x == StatusCode::UnsupportedMediaType as u16 => {
                    Ok(StatusCode::UnsupportedMediaType)
                }
                x if x == StatusCode::UnprocessableContent as u16 => {
                    Ok(StatusCode::UnprocessableContent)
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
                _ => Err(AIOProtocolError::new_unknown_error(
                    true,
                    false,
                    None,
                    Some(status),
                    Some(format!("Unknown status code: {s}")),
                    None,
                )),
            },
            Err(e) => Err(AIOProtocolError::new_header_invalid_error(
                "status",
                s,
                false,
                None,
                Some(format!(
                    "Could not parse status in response '{s}' as an integer: {e}"
                )),
                None,
            )),
        }
    }
}

#[cfg(test)]
mod tests {
    use std::str::FromStr;

    use test_case::test_case;

    use crate::{common::aio_protocol_error::AIOProtocolErrorKind, rpc::StatusCode};

    #[test_case(StatusCode::Ok; "Ok")]
    #[test_case(StatusCode::NoContent; "NoContent")]
    #[test_case(StatusCode::BadRequest; "BadRequest")]
    #[test_case(StatusCode::RequestTimeout; "RequestTimeout")]
    #[test_case(StatusCode::UnsupportedMediaType; "UnsupportedMediaType")]
    #[test_case(StatusCode::UnprocessableContent; "UnprocessableContent")]
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
            Err(e) => {
                assert_eq!(e.kind, AIOProtocolErrorKind::HeaderInvalid);
                assert!(!e.in_application);
                assert!(!e.is_shallow);
                assert!(!e.is_remote);
                assert!(e.nested_error.is_none());
                assert_eq!(e.http_status_code, None);
                assert_eq!(e.header_name, Some("status".to_string()));
                assert_eq!(e.header_value, Some(test_invalid_code.to_string()));
            }
        }
    }

    #[test]
    fn test_unknown_status_code() {
        let test_unknown_code = 201;
        let code_result = StatusCode::from_str(&test_unknown_code.to_string());
        match code_result {
            Ok(_) => panic!("Expected error"),
            Err(e) => {
                assert_eq!(e.kind, AIOProtocolErrorKind::UnknownError);
                assert!(!e.in_application);
                assert!(!e.is_shallow);
                assert!(e.is_remote);
                assert!(e.nested_error.is_none());
                assert_eq!(e.http_status_code, Some(test_unknown_code));
            }
        }
    }
}
