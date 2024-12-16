// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

//! Clients for using services of Azure IoT Operations

#![warn(missing_docs)]
#![allow(clippy::result_large_err)]

#[cfg(feature = "schema_registry")]
pub mod schema_registry;
#[cfg(feature = "state_store")]
pub mod state_store;
