// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

//! Utilities for using the Azure IoT Operations Protocol over MQTT.

#![warn(missing_docs)]
#![allow(clippy::result_large_err)]

pub mod common;
pub mod rpc;
pub mod telemetry;

#[macro_use]
extern crate derive_builder;

/// Include the README doc on a struct when running doctests to validate that the code in the
/// README can compile to verify that it has not rotted.
/// Note that any code that requires network or environment setup will not be able to run,
/// and thus should be annotated by "no_run" in the README.
#[doc = include_str!("../README.md")]
#[cfg(doctest)]
struct ReadmeDoctests;
