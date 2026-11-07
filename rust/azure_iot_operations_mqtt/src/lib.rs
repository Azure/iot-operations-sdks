// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

#![warn(missing_docs)]

//! MQTT version 5.0 client library providing flexibility for decoupled asynchronous applications
//!
//! Use the components of the [`session`] module to communicate over MQTT with
//! an automatically managed connection across a single MQTT session.

pub mod aio;
pub mod control_packet;
pub mod error;
pub mod session;
pub mod token;

mod azure_mqtt_adapter;

// These modules are currently unused, but will be in the future.
#[allow(dead_code, clippy::type_complexity)]
mod ktls;
#[allow(dead_code, clippy::type_complexity)]
mod opensslext;

#[cfg(feature = "test-utils")]
pub mod test_utils;

/// Low-level client re-export retained for integration-test compatibility.
///
/// The protocol internals exposed by this alias are not part of the stable SDK API.
#[cfg(feature = "test-utils")]
pub use ms_mqtt_client as azure_mqtt;

#[macro_use]
extern crate derive_builder;
#[macro_use]
extern crate derive_getters;

//----------------------------------------------------------------------

/// Include the README doc on a struct when running doctests to validate that the code in the
/// README can compile to verify that it has not rotted.
/// Note that any code that requires network or environment setup will not be able to run,
/// and thus should be annotated by "no_run" in the README.
#[doc = include_str!("../README.md")]
#[cfg(doctest)]
struct ReadmeDoctests;
