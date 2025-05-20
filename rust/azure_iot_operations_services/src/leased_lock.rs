// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

//! Types for Lease and Lock operations.

use core::fmt::Debug;

use azure_iot_operations_protocol::common::aio_protocol_error::AIOProtocolError;
use thiserror::Error;

use crate::state_store::{self, KeyObservation, ServiceError as StateStoreServiceError};
pub use crate::state_store::{Response, SetCondition, SetOptions};

/// A struct to manage receiving notifications for a lease
pub type LeaseObservation = KeyObservation;

/// Represents the errors that occur in the Azure IoT Operations State Store Service.
pub type ServiceError = StateStoreServiceError;

/// Lease Client implementation
pub mod lease;
/// Lock Client implementation
pub mod lock;

/// Represents an error that occurred in the Azure IoT Operations Lease and Lock implementation.
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

impl From<state_store::Error> for Error {
    fn from(error: state_store::Error) -> Self {
        let kind: ErrorKind = (error.consuming_kind()).into();
        kind.into()
    }
}

/// Represents the kinds of errors that occur in the Azure IoT Operations Lease and Lock implementation.
#[derive(Error, Debug)]
#[allow(clippy::large_enum_variant)]
pub enum ErrorKind {
    /// The lease is already in use by another holder.
    #[error("lease is already held by another holder")]
    LeaseAlreadyHeld,
    /// An error occurred in the AIO Protocol. See [`AIOProtocolError`] for more information.
    #[error(transparent)]
    AIOProtocolError(#[from] AIOProtocolError),
    /// An error occurred from the State Store Service. See [`ServiceError`] for more information.
    #[error(transparent)]
    ServiceError(#[from] ServiceError),
    /// Fencing token not received from service.
    #[error("Fencing token not received from service")]
    MissingFencingToken,
    /// An error occurred during serialization of a request.
    #[error("{0}")]
    SerializationError(String),
    /// An argument provided for a request was invalid.
    #[error("{0}")]
    InvalidArgument(String),
    /// The payload of the response does not match the expected type for the request.
    #[error("Unexpected response payload for the request type: {0}")]
    UnexpectedPayload(String),
    /// A lease may only have one [`LeaseObservation`] at a time.
    #[error("lease may only be observed once at a time")]
    DuplicateObserve,
}

impl From<state_store::ErrorKind> for ErrorKind {
    fn from(kind: state_store::ErrorKind) -> Self {
        match kind {
            state_store::ErrorKind::AIOProtocolError(protocol_error) => {
                ErrorKind::AIOProtocolError(protocol_error)
            }
            state_store::ErrorKind::ServiceError(service_error) => {
                ErrorKind::ServiceError(service_error)
            }
            state_store::ErrorKind::SerializationError(error_string) => {
                ErrorKind::SerializationError(error_string)
            }
            state_store::ErrorKind::InvalidArgument(argument) => {
                ErrorKind::InvalidArgument(argument)
            }
            state_store::ErrorKind::UnexpectedPayload(payload) => {
                ErrorKind::UnexpectedPayload(payload)
            }
            state_store::ErrorKind::DuplicateObserve => ErrorKind::DuplicateObserve,
        }
    }
}
