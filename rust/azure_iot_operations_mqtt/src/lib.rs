// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

#![warn(missing_docs)]

//! MQTT version 5.0 client library providing flexibility for decoupled asynchronous applications

pub use crate::connection_settings::{
    MqttConnectionSettings, MqttConnectionSettingsBuilder, MqttConnectionSettingsBuilderError,
};

mod connection_settings;
pub mod control_packet;
pub mod error;
pub mod interface;
pub mod session;
pub mod topic;

mod rumqttc_adapter;

#[macro_use]
extern crate derive_builder;

//----------------------------------------------------------------------

/// Include the README doc on a struct when running doctests to validate that the code in the
/// README can compile to verify that it has not rotted.
/// Note that any code that requires network or environment setup will not be able to run,
/// and thus should be annotated by "no_run" in the README.
#[doc = include_str!("../README.md")]
#[cfg(doctest)]
struct ReadmeDoctests;
