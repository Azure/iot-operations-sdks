// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

//! Types for Leased Lock operations.

use core::fmt::Debug;

use thiserror::Error;

use azure_iot_operations_protocol::common::{
    aio_protocol_error::AIOProtocolError, hybrid_logical_clock::HybridLogicalClock,
};

pub use crate::state_store::resp3::Operation;

use crate::state_store::{
    KeyObservation, Response as StateStoreResponse, ServiceError as StateStoreServiceError,
    StateStoreError, StateStoreErrorKind,
};

type LockObservation = KeyObservation;

/// Leased Lock Client implementation
mod client;

pub use client::Client;

/// Represents an error that occurred in the Azure IoT Operations Leased Lock implementation.
#[derive(Debug, Error)]
#[error(transparent)]
pub struct Error(#[from] ErrorKind);

impl Error {
    /// Returns the [`ErrorKind`] of the error.
    #[must_use]
    pub fn kind(&self) -> &ErrorKind {
        &self.0
    }
}

impl From<StateStoreError> for Error {
    fn from(error: StateStoreError) -> Self {
        error.into() // TODO: this leads to a recursive conversion. Needs to be fixed.
    }
}

/// Represents the kinds of errors that occur in the Azure IoT Operations Leased Lock implementation.
#[derive(Error, Debug)]
#[allow(clippy::large_enum_variant)]
pub enum ErrorKind {
    /// The lock is already in use by another holder.
    #[error("lock is already in use by another holder")]
    LockAlreadyInUse,
    /// An error occurred in the AIO Protocol. See [`AIOProtocolError`] for more information.
    #[error(transparent)]
    AIOProtocolError(#[from] AIOProtocolError),
    /// An error occurred from the State Store Service. See [`ServiceError`] for more information.
    #[error(transparent)]
    ServiceError(#[from] ServiceError),
    /// The lock name length must not be zero.
    #[error("lock name length must not be zero")]
    LockNameLengthZero,
    /// An error occurred during serialization of a request.
    #[error("{0}")]
    SerializationError(String),
    /// An argument provided for a request was invalid.
    #[error("{0}")]
    InvalidArgument(String),
    /// The payload of the response does not match the expected type for the request.
    #[error("Unexpected response payload for the request type: {0}")]
    UnexpectedPayload(String),
    /// A lock may only have one [`KeyObservation`] at a time.
    #[error("lock may only be observed once at a time")]
    DuplicateObserve,
}

impl From<StateStoreErrorKind> for ErrorKind {
    fn from(error: StateStoreErrorKind) -> Self {
        match error {
            
            StateStoreErrorKind::AIOProtocolError(_) => error.into(), // TODO: is this an infinite recurse?
            StateStoreErrorKind::ServiceError(_) => error.into(),
            StateStoreErrorKind::KeyLengthZero => ErrorKind::LockNameLengthZero,
            StateStoreErrorKind::SerializationError(error_string) => ErrorKind::SerializationError(error_string),
            StateStoreErrorKind::InvalidArgument(argument) => ErrorKind::InvalidArgument(argument),
            StateStoreErrorKind::UnexpectedPayload(payload) => ErrorKind::UnexpectedPayload(payload),
            StateStoreErrorKind::DuplicateObserve => ErrorKind::DuplicateObserve
        }
    }
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
    LockQuotaExceeded,
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
    #[error("the lock name length is zero")]
    LockNameLengthZero,
    /// An unknown error was received from the State Store Service.
    #[error("{0}")]
    Unknown(String),
}

impl From<StateStoreServiceError> for ServiceError {
    fn from(error: StateStoreServiceError) -> Self {
        match error {
            StateStoreServiceError::TimestampSkew => ServiceError::TimestampSkew,
            StateStoreServiceError::MissingFencingToken => ServiceError::MissingFencingToken,
            StateStoreServiceError::FencingTokenSkew => ServiceError::FencingTokenSkew,
            StateStoreServiceError::FencingTokenLowerVersion => ServiceError::FencingTokenLowerVersion,
            StateStoreServiceError::KeyQuotaExceeded => ServiceError::LockQuotaExceeded,
            StateStoreServiceError::SyntaxError => ServiceError::SyntaxError,
            StateStoreServiceError::NotAuthorized => ServiceError::NotAuthorized,
            StateStoreServiceError::UnknownCommand => ServiceError::UnknownCommand,
            StateStoreServiceError::WrongNumberOfArguments => ServiceError::WrongNumberOfArguments,
            StateStoreServiceError::TimestampMissing => ServiceError::TimestampMissing,
            StateStoreServiceError::TimestampMalformed => ServiceError::TimestampMalformed,
            StateStoreServiceError::KeyLengthZero => ServiceError::LockNameLengthZero,
            StateStoreServiceError::Unknown(error_string) => ServiceError::Unknown(error_string),
        }
    }
}

/// Leased Lock Operation Response struct.
#[derive(Debug)]
pub struct Response<T>
where
    T: Debug,
{
    /// The version of the lock as a [`HybridLogicalClock`].
    pub version: Option<HybridLogicalClock>,
    /// The response for the request. Will vary per operation.
    pub response: T,
}

impl<T: Debug> Response<T> {
    /// Creates a new instance of Response<T>.
    pub fn new(response: T, version: Option<HybridLogicalClock>) -> Response<T> {
        Self { version, response }
    }

    /// Creates a new instance of Response<T> out of the `response` and `version` of a state_store::Response<T>.
    pub fn from_response(state_store_response: StateStoreResponse<T>) -> Response<T> {
        Self {
            version: state_store_response.version,
            response: state_store_response.response,
        }
    }
}

impl From<StateStoreResponse<KeyObservation>> for Response<LockObservation> {
    fn from(state_store_response: StateStoreResponse<KeyObservation>) -> Self {
        Response::new(
            state_store_response.response.into(),
            state_store_response.version,
        )
    }
}
