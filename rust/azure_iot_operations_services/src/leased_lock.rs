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
type ServiceError = StateStoreServiceError;

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
        let kind: ErrorKind = (error.consuming_kind()).into();
        kind.into()
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
    fn from(kind: StateStoreErrorKind) -> Self {
        match kind {
            StateStoreErrorKind::AIOProtocolError(protocol_error) => {
                ErrorKind::AIOProtocolError(protocol_error)
            }
            StateStoreErrorKind::ServiceError(service_error) => {
                ErrorKind::ServiceError(service_error)
            }
            StateStoreErrorKind::KeyLengthZero => ErrorKind::LockNameLengthZero,
            StateStoreErrorKind::SerializationError(error_string) => {
                ErrorKind::SerializationError(error_string)
            }
            StateStoreErrorKind::InvalidArgument(argument) => ErrorKind::InvalidArgument(argument),
            StateStoreErrorKind::UnexpectedPayload(payload) => {
                ErrorKind::UnexpectedPayload(payload)
            }
            StateStoreErrorKind::DuplicateObserve => ErrorKind::DuplicateObserve,
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

    /// Creates a new instance of Response<T> out of the `response` and `version` of a `state_store::Response<T>`.
    pub fn from_response(state_store_response: StateStoreResponse<T>) -> Response<T> {
        Self {
            version: state_store_response.version,
            response: state_store_response.response,
        }
    }
}

impl From<StateStoreResponse<KeyObservation>> for Response<LockObservation> {
    fn from(state_store_response: StateStoreResponse<KeyObservation>) -> Self {
        Response::new(state_store_response.response, state_store_response.version)
    }
}
