// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

//! Types for Edge Registry (xRegistry) operations.

use thiserror::Error;

/// Edge Registry generated code
mod edge_registry_gen;

/// Edge Registry Client implementation wrapper
mod client;
pub mod models;

pub use client::Client;

/// Represents an error that occurred in the Azure IoT Operations Edge Registry Client implementation.
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

// TODO: This is a placeholder error surface. Expand with the remaining error
// kinds (protocol errors, service errors, validation errors, etc.) as the
// Edge Registry client is implemented.
/// Represents the kinds of errors that occur in the Azure IoT Operations Edge Registry implementation.
#[derive(Debug, Error)]
pub enum ErrorKind {
    /// A response received from the Edge Registry service did not satisfy the
    /// model's invariants and could not be interpreted.
    #[error("invalid response from the Edge Registry service: {0}")]
    InvalidResponse(String),
}
