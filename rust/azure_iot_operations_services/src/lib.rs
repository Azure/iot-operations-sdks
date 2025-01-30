// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

//! Clients for using services of Azure IoT Operations
//!
//! ## Feature flags
//!
//! The services are divided into separate features that are off by default. You can enable them
//! using the following feature flags:
//!
//! - `all`: Enables all features.
//! - `schema_registry`: Enabled the Schema Registry Client.
//! - `state_store`: Enabled the State Store Client.
//!
//! This example shows how you could import features for only the Schema Registry Client:
//!
//! ```toml
//! azure_iot_operations_services = { version = "<version>", features = ["schema_registry"] }
//! ```

#![warn(missing_docs)]
#![allow(clippy::result_large_err)]

#[cfg(feature = "schema_registry")]
pub mod schema_registry;
#[cfg(feature = "state_store")]
pub mod state_store;
