// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

use core::fmt;

mod ece_codes;
mod unit_infos;

#[derive(Debug, Clone, Copy, PartialEq)]
pub struct UnitConverter {
    pub multiplier: f64,
    pub offset: f64,
}

/// Canonical error type for conversion operations in this library.
#[derive(Debug, Clone, PartialEq, Eq)]
pub enum ConvertError {
    /// The source unit is not recognized.
    SourceUnitUnrecognized(String),
    /// The target unit is not recognized.
    TargetUnitUnrecognized(String),
    /// The source and target units are of different kinds.
    UnitsOfDifferentKinds {
        source_kind: String,
        target_kind: String,
    },
    /// The value is infinity or NaN.
    UnrepresentableValue,
}

impl UnitConverter {
    /// Converts a value using this converter.
    ///
    /// # Arguments
    /// * `val` - The value to convert from source units.
    ///
    /// # Returns
    /// The converted value in target units.
    ///
    /// # Errors
    ///
    /// [`ConvertError`] of kind [`UnrepresentableValue`] if the converted value is infinity or NaN.
    pub fn convert(&self, val: f64) -> Result<f64, ConvertError> {
        let converted_value = val * self.multiplier + self.offset;
        if !converted_value.is_finite() {
            return Err(ConvertError::UnrepresentableValue);
        }

        Ok(converted_value)
    }
}

impl fmt::Display for ConvertError {
    fn fmt(&self, f: &mut fmt::Formatter<'_>) -> fmt::Result {
        match self {
            Self::SourceUnitUnrecognized(unit) => {
                write!(f, "source unit unrecognized: {unit}")
            }
            Self::TargetUnitUnrecognized(unit) => {
                write!(f, "target unit unrecognized: {unit}")
            }
            Self::UnitsOfDifferentKinds {
                source_kind,
                target_kind,
            } => {
                write!(
                    f,
                    "units are of different kinds: source={source_kind}, target={target_kind}"
                )
            }
            Self::UnrepresentableValue => {
                write!(f, "converted value is infinity or NaN")
            }
        }
    }
}

impl std::error::Error for ConvertError {}

/// Gets a [`UnitConverter`] for converting between two units.
///
/// # Arguments
/// * `source_unit` - The unit to convert from.
/// * `target_unit` - The unit to convert to.
///
/// # Returns
/// A [`UnitConverter`] if the units are compatible, otherwise an error.
///
/// # Errors
///
/// [`ConvertError`] of kind [`SourceUnitUnrecognized`] if the source unit is not recognized.
/// [`ConvertError`] of kind [`TargetUnitUnrecognized`] if the target unit is not recognized.
/// [`ConvertError`] of kind [`UnitsOfDifferentKinds`] if the units are of different kinds.
/// [`ConvertError`] of kind [`UnrepresentableValue`] if either conversion coefficient is infinity or NaN.
pub fn get_converter(source_unit: &str, target_unit: &str) -> Result<UnitConverter, ConvertError> {
    let source_info = get_unit_info(source_unit)
        .map_err(|()| ConvertError::SourceUnitUnrecognized(source_unit.to_string()))?;
    let target_info = get_unit_info(target_unit)
        .map_err(|()| ConvertError::TargetUnitUnrecognized(target_unit.to_string()))?;

    if source_info.kind != target_info.kind {
        return Err(ConvertError::UnitsOfDifferentKinds {
            source_kind: source_info.kind.to_string(),
            target_kind: target_info.kind.to_string(),
        });
    }

    let multiplier = source_info.multiplier / target_info.multiplier;
    let offset = source_info.offset * multiplier - target_info.offset;

    if !multiplier.is_finite() || !offset.is_finite() {
        return Err(ConvertError::UnrepresentableValue);
    }

    Ok(UnitConverter { multiplier, offset })
}

#[derive(Debug, Clone, Copy, PartialEq)]
struct UnitInfo {
    pub kind: &'static str,
    pub multiplier: f64,
    pub offset: f64,
}

fn get_unit_info(unit: &str) -> Result<UnitInfo, ()> {
    let lookup_key = if unit.len() <= 3 {
        ece_codes::ECE_CODES.get(unit).copied().ok_or(())?
    } else if let Some(unit_name) = unit.strip_prefix("unit:") {
        unit_name
    } else if let Some(unit_name) = unit.strip_prefix("http://qudt.org/vocab/unit/") {
        unit_name
    } else {
        return Err(());
    };

    unit_infos::UNIT_INFOS.get(lookup_key).copied().ok_or(())
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn test_source_unit_unrecognized_display_includes_value() {
        let wrapped = "Kelvins".to_string();
        let error = ConvertError::SourceUnitUnrecognized(wrapped.clone());
        let error_string = error.to_string();

        assert!(error_string.contains("source"));
        assert!(error_string.contains(&wrapped));
    }

    #[test]
    fn test_target_unit_unrecognized_display_includes_value() {
        let wrapped = "Rankine".to_string();
        let error = ConvertError::TargetUnitUnrecognized(wrapped.clone());
        let error_string = error.to_string();

        assert!(error_string.contains("target"));
        assert!(error_string.contains(&wrapped));
    }

    #[test]
    fn test_units_of_different_kinds_display_includes_both_values() {
        let source_kind = "Temperature".to_string();
        let target_kind = "Length".to_string();
        let error = ConvertError::UnitsOfDifferentKinds {
            source_kind: source_kind.clone(),
            target_kind: target_kind.clone(),
        };
        let error_string = error.to_string();

        assert!(error_string.contains(&source_kind));
        assert!(error_string.contains(&target_kind));
    }

    #[test]
    #[allow(clippy::float_cmp)]
    fn test_get_unit_info_from_ece_code() {
        let info = get_unit_info("CEL").unwrap();
        assert_eq!(info.kind, "Temperature");
        assert_eq!(info.multiplier, 1.0);
        assert_eq!(info.offset, 273.15);
    }

    #[test]
    #[allow(clippy::float_cmp)]
    fn test_get_unit_info_from_unit_term() {
        let info = get_unit_info("unit:DEG_F").unwrap();
        assert_eq!(info.kind, "Temperature");
        assert_eq!(info.multiplier, 5.0 / 9.0);
        assert_eq!(info.offset, 459.67);
    }

    #[test]
    #[allow(clippy::float_cmp)]
    fn test_get_unit_info_from_unit_uri() {
        let info = get_unit_info("http://qudt.org/vocab/unit/K").unwrap();
        assert_eq!(info.kind, "Temperature");
        assert_eq!(info.multiplier, 1.0);
        assert_eq!(info.offset, 0.0);
    }

    #[test]
    fn test_get_unit_info_returns_err_for_unrecognized_short_code() {
        assert_eq!(get_unit_info("XYZ"), Err(()));
    }

    #[test]
    fn test_get_unit_info_returns_err_for_unprefixed_long_code() {
        assert_eq!(get_unit_info("DEG_C"), Err(()));
    }

    #[test]
    fn test_get_converter_returns_err_for_mismatched_kinds() {
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
    fn test_ece_to_ece() {
        let converter = get_converter("FAH", "CEL");
        assert!(converter.is_ok());

        let converted_value = converter.unwrap().convert(212.0);
        assert!(converted_value.is_ok());
        assert!((converted_value.unwrap() - 100.0).abs() < 1e-9);
    }

    #[test]
    fn test_term_to_term() {
        let converter = get_converter("unit:DEG_F", "unit:DEG_C");
        assert!(converter.is_ok());

        let converted_value = converter.unwrap().convert(212.0);
        assert!(converted_value.is_ok());
        assert!((converted_value.unwrap() - 100.0).abs() < 1e-9);
    }

    #[test]
    fn test_uri_to_uri() {
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
    fn test_ece_to_term() {
        let converter = get_converter("FAH", "unit:DEG_C");
        assert!(converter.is_ok());

        let converted_value = converter.unwrap().convert(212.0);
        assert!(converted_value.is_ok());
        assert!((converted_value.unwrap() - 100.0).abs() < 1e-9);
    }

    #[test]
    fn test_ece_to_uri() {
        let converter = get_converter("FAH", "http://qudt.org/vocab/unit/DEG_C");
        assert!(converter.is_ok());

        let converted_value = converter.unwrap().convert(212.0);
        assert!(converted_value.is_ok());
        assert!((converted_value.unwrap() - 100.0).abs() < 1e-9);
    }

    #[test]
    fn test_term_to_ece() {
        let converter = get_converter("unit:DEG_F", "CEL");
        assert!(converter.is_ok());

        let converted_value = converter.unwrap().convert(212.0);
        assert!(converted_value.is_ok());
        assert!((converted_value.unwrap() - 100.0).abs() < 1e-9);
    }

    #[test]
    fn test_term_to_uri() {
        let converter = get_converter("unit:DEG_F", "http://qudt.org/vocab/unit/DEG_C");
        assert!(converter.is_ok());

        let converted_value = converter.unwrap().convert(212.0);
        assert!(converted_value.is_ok());
        assert!((converted_value.unwrap() - 100.0).abs() < 1e-9);
    }

    #[test]
    fn test_uri_to_ece() {
        let converter = get_converter("http://qudt.org/vocab/unit/DEG_F", "CEL");
        assert!(converter.is_ok());

        let converted_value = converter.unwrap().convert(212.0);
        assert!(converted_value.is_ok());
        assert!((converted_value.unwrap() - 100.0).abs() < 1e-9);
    }

    #[test]
    fn test_uri_to_term() {
        let converter = get_converter("http://qudt.org/vocab/unit/DEG_F", "unit:DEG_C");
        assert!(converter.is_ok());

        let converted_value = converter.unwrap().convert(212.0);
        assert!(converted_value.is_ok());
        assert!((converted_value.unwrap() - 100.0).abs() < 1e-9);
    }
}
