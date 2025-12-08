// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

//! MQTT client library

// Low-level modules
// TODO: Revisit the exposed API of these modules, and remove the linting suppressions as necessary
#[cfg(not(feature = "test-utils"))]
pub(crate) mod buffer_pool;
#[cfg(feature = "test-utils")]
pub mod buffer_pool;

pub(crate) mod io;

#[cfg(not(feature = "test-utils"))]
pub(crate) mod mqtt_proto;
#[cfg(feature = "test-utils")]
pub mod mqtt_proto;

mod opensslext;

// High-level modules
pub mod client;
pub mod error;
pub mod packet;
pub mod topic;

// NOTE: Any dispatching or connection management would be supplementary components.
// I am in favor of providing them, but they are built on top of these core components and would be optional.
// These core components have been designed to enable the future use of such additional components,
// e.g. PubAckToken is designed to support future usage in complex dispatching scenarios.
