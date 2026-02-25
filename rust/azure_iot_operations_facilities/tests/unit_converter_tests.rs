// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

use azure_iot_operations_facilities::{ConvertError, get_converter};

#[test]
fn test_ece_to_ece_integration() {
    let converter = get_converter("FAH", "CEL");
    assert!(converter.is_ok());

    let converted_value = converter.unwrap().convert(212.0);
    assert!(converted_value.is_ok());
    assert!((converted_value.unwrap() - 100.0).abs() < 1e-9);
}

#[test]
fn test_term_to_term_integration() {
    let converter = get_converter("unit:DEG_F", "unit:DEG_C");
    assert!(converter.is_ok());

    let converted_value = converter.unwrap().convert(212.0);
    assert!(converted_value.is_ok());
    assert!((converted_value.unwrap() - 100.0).abs() < 1e-9);
}

#[test]
fn test_uri_to_uri_integration() {
    let converter = get_converter(
        "http://qudt.org/vocab/unit/DEG_F",
        "http://qudt.org/vocab/unit/DEG_C",
    );
    assert!(converter.is_ok());

    let converted_value = converter.unwrap().convert(212.0);
    assert!(converted_value.is_ok());
    assert!((converted_value.unwrap() - 100.0).abs() < 1e-9);
}

#[test]
fn test_get_converter_returns_err_for_mismatched_kinds_integration() {
    let result = get_converter("unit:DEG_C", "unit:M");
    assert_eq!(
        result,
        Err(ConvertError::UnitsOfDifferentKinds {
            source_kind: "Temperature".to_string(),
            target_kind: "Length".to_string(),
        })
    );
}

#[test]
fn test_get_converter_returns_err_for_unrecognized_source_unit_integration() {
    let result = get_converter("XYZ", "CEL");
    assert_eq!(
        result,
        Err(ConvertError::SourceUnitUnrecognized("XYZ".to_string()))
    );
}

#[test]
fn test_get_converter_returns_err_for_unrecognized_target_unit_integration() {
    let result = get_converter("CEL", "XYZ");
    assert_eq!(
        result,
        Err(ConvertError::TargetUnitUnrecognized("XYZ".to_string()))
    );
}
