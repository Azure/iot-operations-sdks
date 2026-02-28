// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

use azure_iot_operations_facilities::{
    LABELED_SYSTEMS_OF_UNITS, SelectError, get_labeled_units_for_kind,
    get_unit_for_kind_and_system, get_units_for_kind,
};

#[test]
fn labeled_systems_of_units_is_publicly_accessible() {
    assert!(!LABELED_SYSTEMS_OF_UNITS.is_empty());
    assert!(LABELED_SYSTEMS_OF_UNITS.iter().any(|(code, _)| *code == "CGS"));
}

#[test]
fn get_units_for_kind_returns_units_on_success() {
    let result = get_units_for_kind("Temperature");

    assert!(result.is_ok());
    let units = result.unwrap();

    assert!(units.contains(&"unit:DEG_C"));
    assert!(units.contains(&"unit:DEG_F"));
    assert!(units.contains(&"unit:K"));
}

#[test]
fn get_units_for_kind_returns_quantity_kind_unrecognized_error() {
    let result = get_units_for_kind("quantitykind:UnknownKind");

    assert_eq!(
        result,
        Err(SelectError::QuantityKindUnrecognized(
            "quantitykind:UnknownKind".to_string()
        ))
    );
}

#[test]
fn get_labeled_units_for_kind_returns_labeled_units_on_success() {
    let result = get_labeled_units_for_kind("Temperature");

    assert!(result.is_ok());
    let units = result.unwrap();

    assert!(units.contains(&("unit:DEG_C", "Degree Celsius")));
    assert!(units.contains(&("unit:DEG_F", "Degree Fahrenheit")));
    assert!(units.contains(&("unit:K", "Kelvin")));
}

#[test]
fn get_labeled_units_for_kind_returns_quantity_kind_unrecognized_error() {
    let result = get_labeled_units_for_kind("quantitykind:UnknownKind");

    assert_eq!(
        result,
        Err(SelectError::QuantityKindUnrecognized(
            "quantitykind:UnknownKind".to_string()
        ))
    );
}

#[test]
fn get_unit_for_kind_and_system_returns_unit_on_success() {
    let result = get_unit_for_kind_and_system("Temperature", "CGS");

    assert_eq!(result, Ok("unit:DEG_C"));
}

#[test]
fn get_unit_for_kind_and_system_returns_quantity_kind_unrecognized_error() {
    let result = get_unit_for_kind_and_system("UnknownKind", "CGS");

    assert_eq!(
        result,
        Err(SelectError::QuantityKindUnrecognized(
            "UnknownKind".to_string()
        ))
    );
}

#[test]
fn get_unit_for_kind_and_system_returns_unit_system_unrecognized_error() {
    let result = get_unit_for_kind_and_system("Temperature", "UNKNOWN");

    assert_eq!(
        result,
        Err(SelectError::UnitSystemUnrecognized("UNKNOWN".to_string()))
    );
}

#[test]
fn get_unit_for_kind_and_system_returns_no_unit_for_kind_and_system_error() {
    let result = get_unit_for_kind_and_system("AmountOfSubstance", "CGS");

    assert_eq!(
        result,
        Err(SelectError::NoUnitForKindAndSystem {
            quantity_kind: "AmountOfSubstance".to_string(),
            unit_system: "CGS".to_string(),
        })
    );
}
