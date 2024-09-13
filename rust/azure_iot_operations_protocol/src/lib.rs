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
