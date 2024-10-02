// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

//! Utilities for using the Azure IoT Operations Protocol over MQTT.

#![warn(missing_docs)]
#![allow(clippy::result_large_err)]

pub mod common;
pub mod rpc;
#[doc(hidden)]
pub mod telemetry;

#[macro_use]
extern crate derive_builder;

/// Include the README doc on a struct when running doctests to validate that the code in the
/// README can compile to verify that it has not rotted.
#[doc = include_str!("../README.md")]
#[cfg(doctest)]
struct ReadmeDoctests;
