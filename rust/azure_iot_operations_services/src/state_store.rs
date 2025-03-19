// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

//! Types for State Store operations.

use core::fmt::Debug;

use azure_iot_operations_protocol::{
    common::{aio_protocol_error::AIOProtocolError, hybrid_logical_clock::HybridLogicalClock},
    rpc::command_invoker::CommandResponse,
};
use thiserror::Error;

/// State Store Client implementation
mod client;
/// Serialization and deserialization implementations for resp3 state store payloads
mod resp3;

pub use client::{Client, ClientOptions, ClientOptionsBuilder, KeyObservation};
pub use resp3::{Operation, SetCondition, SetOptions};

/// User Property Key for a [`HybridLogicalClock`] fencing token used to protect the object of the request from conflicting updates.
const FENCING_TOKEN_USER_PROPERTY: &str = "__ft";

/// Represents an error that occurred in the Azure IoT Operations State Store implementation.
#[derive(Debug, Error)]
#[error(transparent)]
pub struct StateStoreError(#[from] StateStoreErrorKind);

impl StateStoreError {
    /// Returns the [`StateStoreErrorKind`] of the error as a reference.
    #[must_use]
    pub fn kind(&self) -> &StateStoreErrorKind {
        &self.0
    }

    /// Returns the [`StateStoreErrorKind`] of the error.
    #[must_use]
    #[allow(dead_code)]
    pub(crate) fn consuming_kind(self) -> StateStoreErrorKind {
        self.0
    }
}

/// Represents the kinds of errors that occur in the Azure IoT Operations State Store implementation.
#[derive(Error, Debug)]
#[allow(clippy::large_enum_variant)]
pub enum StateStoreErrorKind {
    /// An error occurred in the AIO Protocol. See [`AIOProtocolError`] for more information.
    #[error(transparent)]
    AIOProtocolError(#[from] AIOProtocolError),
    /// An error occurred from the State Store Service. See [`ServiceError`] for more information.
    #[error(transparent)]
    ServiceError(#[from] ServiceError),
    /// The key length must not be zero.
    #[error("key length must not be zero")]
    KeyLengthZero,
    /// An error occurred during serialization of a request.
    #[error("{0}")]
    SerializationError(String),
    /// An argument provided for a request was invalid.
    #[error("{0}")]
    InvalidArgument(String),
    /// The payload of the response does not match the expected type for the request.
    #[error("Unexpected response payload for the request type: {0}")]
    UnexpectedPayload(String),
    /// A key may only have one [`KeyObservation`] at a time.
    #[error("key may only be observed once at a time")]
    DuplicateObserve,
}

/// Represents the errors that occur in the Azure IoT Operations State Store Service.
#[derive(Error, Debug)]
pub enum ServiceError {
    /// the request timestamp is too far in the future; ensure that the client and broker system clocks are synchronized.
    #[error("the request timestamp is too far in the future; ensure that the client and broker system clocks are synchronized")]
    TimestampSkew,
    /// A fencing token is required for this request. This happens if a key has been marked with a fencing token, but the client doesn't specify it
    #[error("a fencing token is required for this request")]
    MissingFencingToken,
    /// the request fencing token timestamp is too far in the future; ensure that the client and broker system clocks are synchronized.
    #[error("the request fencing token timestamp is too far in the future; ensure that the client and broker system clocks are synchronized")]
    FencingTokenSkew,
    /// The request fencing token is a lower version than the fencing token protecting the resource.
    #[error("the request fencing token is a lower version than the fencing token protecting the resource")]
    FencingTokenLowerVersion,
    /// The state store has a quota of how many keys it can store, which is based on the memory profile of the MQ broker that's specified.
    #[error("the quota has been exceeded")]
    KeyQuotaExceeded,
    /// The payload sent does not conform to state store's definition.
    #[error("syntax error")]
    SyntaxError,
    /// The client is not authorized to perform the operation.
    #[error("not authorized")]
    NotAuthorized,
    /// The command sent is not recognized by the state store.
    #[error("unknown command")]
    UnknownCommand,
    /// The number of arguments sent in the command is incorrect.
    #[error("wrong number of arguments")]
    WrongNumberOfArguments,
    /// The timestamp is missing on the request.
    #[error("missing timestamp")]
    TimestampMissing,
    /// The timestamp or fencing token is malformed.
    #[error("malformed timestamp")]
    TimestampMalformed,
    /// The key length is zero.
    #[error("the key length is zero")]
    KeyLengthZero,
    /// An unknown error was received from the State Store Service.
    #[error("{0}")]
    Unknown(String),
}

impl From<Vec<u8>> for ServiceError {
    fn from(s: Vec<u8>) -> Self {
        let s_bytes: &[u8] = &s;
        match s_bytes {
            b"the request timestamp is too far in the future; ensure that the client and broker system clocks are synchronized" => ServiceError::TimestampSkew,
            b"a fencing token is required for this request" => ServiceError::MissingFencingToken,
            b"the request fencing token timestamp is too far in the future; ensure that the client and broker system clocks are synchronized" => ServiceError::FencingTokenSkew,
            b"the request fencing token is a lower version than the fencing token protecting the resource" => ServiceError::FencingTokenLowerVersion,
            b"the quota has been exceeded" => ServiceError::KeyQuotaExceeded,
            b"syntax error" => ServiceError::SyntaxError,
            b"not authorized" => ServiceError::NotAuthorized,
            b"unknown command" => ServiceError::UnknownCommand,
            b"wrong number of arguments" => ServiceError::WrongNumberOfArguments,
            b"missing timestamp" => ServiceError::TimestampMissing,
            b"malformed timestamp" => ServiceError::TimestampMalformed,
            b"the key length is zero" => ServiceError::KeyLengthZero,
            other => ServiceError::Unknown(std::str::from_utf8(other).unwrap_or_default().to_string()),
        }
    }
}

/// State Store Operation Response struct.
#[derive(Debug)]
pub struct Response<T>
where
    T: Debug,
{
    /// The version of the key as a [`HybridLogicalClock`].
    pub version: Option<HybridLogicalClock>,
    /// The response for the request. Will vary per operation.
    pub response: T,
}

/// Convenience function to convert a `CommandResponse` into a `state_store::Response`
/// Takes in a closure that converts the payload into the desired type.
fn convert_response<T, F>(
    resp: CommandResponse<resp3::Response>,
    f: F,
) -> Result<Response<T>, StateStoreError>
where
    F: FnOnce(resp3::Response) -> Result<T, ()>,
    T: Debug,
{
    match resp.payload {
        resp3::Response::Error(e) => {
            Err(StateStoreError(StateStoreErrorKind::ServiceError(e.into())))
        }
        payload => match f(payload.clone()) {
            Ok(response) => Ok(Response {
                response,
                version: resp.timestamp,
            }),
            Err(()) => Err(StateStoreError(StateStoreErrorKind::UnexpectedPayload(
                format!("{payload:?}"),
            ))),
        },
    }
}

/// A notification about a state change on a key in the State Store Service
#[derive(Debug, Clone)]
pub struct KeyNotification {
    /// The Key that this notification is for
    pub key: Vec<u8>,
    /// The [`Operation`] that was performed on the key
    pub operation: Operation,
    /// The version of the key as a [`HybridLogicalClock`].
    pub version: HybridLogicalClock,
}
