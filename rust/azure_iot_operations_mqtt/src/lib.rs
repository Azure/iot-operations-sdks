// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

#![warn(missing_docs)]

//! MQTT version 5.0 client library providing flexibility for decoupled asynchronous applications
//!
//! Use the components of the [`session`] module to communicate over MQTT with
//! an automatically managed connection across a single MQTT session.

pub use crate::connection_settings::{
    MqttConnectionSettings, MqttConnectionSettingsBuilder, MqttConnectionSettingsBuilderError,
};

mod connection_settings;
pub mod control_packet;
pub mod error;
pub mod session;
pub mod token;

mod azure_mqtt_adapter;

#[cfg(feature = "test-utils")]
pub mod test_utils;

// NOTE on `azure_mqtt` module inclusion:
// - Do NOT format or change anything in `azure_mqtt` module without also updating the
// repo where its maintained first.
// - Suppress all warnings so that this code can be kept in sync with source of truth
// with minimal changes.
// - Do not format the code either.
// - Treat this module as if it were an external dependency in terms of import structure
// (i.e. do not put it with the other `crate::` imports)
#[cfg(feature = "test-utils")]
#[allow(unused_imports)]
#[allow(dead_code)]
#[allow(missing_docs)]
#[allow(clippy::all)]
#[rustfmt::skip]
pub mod azure_mqtt;
#[cfg(not(feature = "test-utils"))]
#[allow(unused_imports)]
#[allow(dead_code)]
#[allow(missing_docs)]
#[allow(clippy::all)]
#[rustfmt::skip]
mod azure_mqtt;

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
