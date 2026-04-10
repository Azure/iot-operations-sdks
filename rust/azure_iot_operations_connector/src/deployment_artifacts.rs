// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

//! Utilities for extracting data from filemounts in an Akri deployment

mod atomic_writer_volume_debouncer;
pub mod azure_device_registry;
pub mod connector;
mod connector_configuration;
mod filemount;
mod secrets;

pub use connector::DeploymentArtifactError; // TODO: move implementation out here
// TODO: not sure if all this needs exposing, revisit
pub use connector_configuration::{
    ConnectorConfiguration, Diagnostics, Logs, MqttConnectionConfiguration, Protocol, Tls,
    TlsMode,
};
pub use filemount::FileMount;
pub use secrets::{Secret, Secrets};

#[cfg(test)]
mod test_utils;

// TODO: Add common artifact structs and helpers here once implementation is unified
