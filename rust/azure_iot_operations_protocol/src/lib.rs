// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
#![allow(clippy::result_large_err)]

//! APIs for Azure IoT Operations Protocols to address the following scenarios: RPC Command, Telemetry, and Serialization.

/// This module contains common utilities.
pub mod common;

/// This module contains the command APIs.
pub mod rpc;

/// This module contains the telemetry APIs.
pub mod telemetry;

#[macro_use]
extern crate derive_builder;
