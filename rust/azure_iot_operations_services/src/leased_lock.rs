// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

//! Types for Leased Lock operations.

use core::fmt::Debug;

use thiserror::Error;

use crate::state_store::{
    KeyObservation, Response as StateStoreResponse, ServiceError as StateStoreServiceError,
    StateStoreError, StateStoreErrorKind,
};
use azure_iot_operations_protocol::common::{
    aio_protocol_error::AIOProtocolError, hybrid_logical_clock::HybridLogicalClock,
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
    #[error("lock is already held by another holder")]
    LockAlreadyHeld,
    /// An error occurred in the AIO Protocol. See [`AIOProtocolError`] for more information.
    #[error(transparent)]
    AIOProtocolError(#[from] AIOProtocolError),
    /// An error occurred from the State Store Service. See [`ServiceError`] for more information.
    #[error(transparent)]
    ServiceError(#[from] ServiceError),
    /// The lock name length must not be zero.
    #[error("lock name length must not be zero")]
    LockNameLengthZero,
    /// The lock holder name length must not be zero.
    #[error("lock holder name length must not be zero")]
    LockHolderNameLengthZero,
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
    /// The response for the request. Will vary per operation.
    pub response: T,
    /// Important: This should be used as the `fencing_token` when editing the locked key.
    /// Semantically, it is the version of the lock as a [`HybridLogicalClock`].
    pub fencing_token: Option<HybridLogicalClock>,
}

impl<T: Debug> Response<T> {
    /// Creates a new instance of Response<T>.
    pub fn new(response: T, fencing_token: Option<HybridLogicalClock>) -> Response<T> {
        Self {
            response,
            fencing_token,
        }
    }

    /// Creates a new instance of Response<T> out of the `response` and `version` of a `state_store::Response<T>`.
    pub fn from_response(state_store_response: StateStoreResponse<T>) -> Response<T> {
        Self {
            response: state_store_response.response,
            fencing_token: state_store_response.version,
        }
    }
}

impl From<StateStoreResponse<KeyObservation>> for Response<LockObservation> {
    fn from(state_store_response: StateStoreResponse<KeyObservation>) -> Self {
        Response::new(state_store_response.response, state_store_response.version)
    }
}

// #[derive(Error, Debug)]
// #[allow(clippy::large_enum_variant)]
/// Enumeration used as a response for `leased_lock::Client::acquire_lock_and_update_value`.
pub enum AcquireAndUpdateKeyOption {
    /// Indicates a State Store key shall be updated.
    /// The first argument is new value for the State Store key.
    Update(Vec<u8>),
    /// Indicates the State Store key shall not be updated nor deleted.
    DoNotUpdate,
    /// Indicates the State Store key shall be deleted.
    Delete,
}
