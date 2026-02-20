// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

//! Utilities for extracting data from filemounts in an Akri deployment

pub mod azure_device_registry;
pub mod connector;
mod filemount;

pub use connector::DeploymentArtifactError; // TODO: move implementation out here
pub use filemount::FileMount;

#[cfg(test)]
mod test_utils;

// TODO: Add common artifact structs and helpers here once implementation is unified
