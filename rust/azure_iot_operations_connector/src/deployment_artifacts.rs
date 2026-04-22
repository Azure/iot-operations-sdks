// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

//! Utilities for extracting data from filemounts in an Akri deployment

mod atomic_writer_volume_debouncer;
pub mod azure_device_registry;
pub mod connector;
mod connector_configuration;
mod filemount;
mod secrets;
mod watched;

pub use connector::DeploymentArtifactError; // TODO: move implementation out here
pub use filemount::FileMount;
pub use secrets::{Secret, Secrets};
pub use watched::{Watched, WatchedRef};

#[cfg(test)]
mod test_utils;

// TODO: Add common artifact structs and helpers here once implementation is unified
