// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

#![allow(clippy::approx_constant)]
#![allow(clippy::unreadable_literal)]

pub mod unit_converter;

pub use unit_converter::{ConvertError, UnitConverter, get_converter};
