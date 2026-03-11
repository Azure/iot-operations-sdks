// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

#![allow(clippy::approx_constant)]
#![allow(clippy::unreadable_literal)]

pub mod unit_converter;
pub mod unit_selector;

pub use unit_converter::{ConvertError, UnitConverter, get_converter};
pub use unit_selector::{
    LABELED_SYSTEMS_OF_UNITS, SelectError, get_labeled_units_for_kind,
    get_unit_for_kind_and_system, get_units_for_kind,
};
