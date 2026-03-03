// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

use core::fmt;

mod kind_labeled_units;
mod kind_system_units;
mod labeled_systems_of_units;

/// Canonical list of recognized systems of units as `(code, display_name)` tuples.
pub use labeled_systems_of_units::LABELED_SYSTEMS_OF_UNITS;

/// Canonical error type for unit selection operations in this library.
#[derive(Debug, Clone, PartialEq, Eq)]
pub enum SelectError {
    /// The quantity kind is not recognized.
    QuantityKindUnrecognized(String),
    /// The system of units is not recognized.
    UnitSystemUnrecognized(String),
    /// No default unit exists for the given quantity kind and system of units.
    NoUnitForKindAndSystem {
        quantity_kind: String,
        unit_system: String,
    },
}

/// Gets all known unit terms for a quantity kind.
///
/// # Arguments
/// * `quantity_kind` - Quantity kind key in bare form (`Temperature`), prefixed form
///   (`quantitykind:Temperature`), or QUDT URI form.
///
/// # Returns
/// A vector of unit terms such as `unit:DEG_C`.
///
/// # Errors
///
/// [`SelectError`] of kind [`SelectError::QuantityKindUnrecognized`] if `quantity_kind` is not recognized.
pub fn get_units_for_kind(quantity_kind: &str) -> Result<Vec<&'static str>, SelectError> {
    let lookup_key = normalize_quantity_kind(quantity_kind);

    kind_labeled_units::KIND_LABELED_UNITS
        .get(lookup_key)
        .map(|labeled_units| labeled_units.iter().map(|(unit, _)| *unit).collect())
        .ok_or_else(|| SelectError::QuantityKindUnrecognized(quantity_kind.to_string()))
}

/// Gets all known `(unit_term, label)` pairs for a quantity kind.
///
/// # Arguments
/// * `quantity_kind` - Quantity kind key in bare form (`Temperature`), prefixed form
///   (`quantitykind:Temperature`), or QUDT URI form.
///
/// # Returns
/// A vector of tuples where the first value is the unit term and the second value is the display label.
///
/// # Errors
///
/// [`SelectError`] of kind [`SelectError::QuantityKindUnrecognized`] if `quantity_kind` is not recognized.
pub fn get_labeled_units_for_kind(
    quantity_kind: &str,
) -> Result<Vec<(&'static str, &'static str)>, SelectError> {
    let lookup_key = normalize_quantity_kind(quantity_kind);

    kind_labeled_units::KIND_LABELED_UNITS
        .get(lookup_key)
        .cloned()
        .ok_or_else(|| SelectError::QuantityKindUnrecognized(quantity_kind.to_string()))
}

/// Gets the default unit term for a quantity kind in a given system of units.
///
/// # Arguments
/// * `quantity_kind` - Quantity kind key in bare form (`Temperature`), prefixed form
///   (`quantitykind:Temperature`), or QUDT URI form.
/// * `unit_system` - System of units code in bare form (`SI`), prefixed form (`sou:SI`),
///   or QUDT URI form.
///
/// # Returns
/// The selected unit term such as `unit:K`.
///
/// # Errors
///
/// [`SelectError`] of kind [`SelectError::QuantityKindUnrecognized`] if `quantity_kind` is not recognized.
/// [`SelectError`] of kind [`SelectError::UnitSystemUnrecognized`] if `unit_system` is not recognized.
/// [`SelectError`] of kind [`SelectError::NoUnitForKindAndSystem`] if both inputs are recognized but
/// no mapped unit exists for the pair.
pub fn get_unit_for_kind_and_system(
    quantity_kind: &str,
    unit_system: &str,
) -> Result<&'static str, SelectError> {
    let lookup_kind = normalize_quantity_kind(quantity_kind);
    let kind_exists = kind_labeled_units::KIND_LABELED_UNITS.contains_key(lookup_kind);

    if !kind_exists {
        return Err(SelectError::QuantityKindUnrecognized(
            quantity_kind.to_string(),
        ));
    }

    let lookup_system = normalize_system_of_units(unit_system);

    let system_exists = LABELED_SYSTEMS_OF_UNITS
        .iter()
        .any(|(system_code, _)| *system_code == lookup_system);

    if !system_exists {
        return Err(SelectError::UnitSystemUnrecognized(unit_system.to_string()));
    }

    if let Some(unit) = kind_system_units::KIND_SYSTEM_UNITS.get(&(lookup_kind, lookup_system)) {
        return Ok(*unit);
    }

    Err(SelectError::NoUnitForKindAndSystem {
        quantity_kind: quantity_kind.to_string(),
        unit_system: unit_system.to_string(),
    })
}

fn normalize_quantity_kind(quantity_kind: &str) -> &str {
    if let Some(value) = quantity_kind.strip_prefix("quantitykind:") {
        value
    } else if let Some(value) = quantity_kind.strip_prefix("http://qudt.org/vocab/quantitykind/") {
        value
    } else {
        quantity_kind
    }
}

fn normalize_system_of_units(system_of_units: &str) -> &str {
    if let Some(value) = system_of_units.strip_prefix("sou:") {
        value
    } else if let Some(value) = system_of_units.strip_prefix("http://qudt.org/vocab/sou/") {
        value
    } else {
        system_of_units
    }
}

impl fmt::Display for SelectError {
    fn fmt(&self, f: &mut fmt::Formatter<'_>) -> fmt::Result {
        match self {
            Self::QuantityKindUnrecognized(kind) => write!(f, "quantity kind unrecognized: {kind}"),
            Self::UnitSystemUnrecognized(system) => write!(f, "unit system unrecognized: {system}"),
            Self::NoUnitForKindAndSystem {
                quantity_kind,
                unit_system,
            } => write!(
                f,
                "no unit for quantity kind {quantity_kind} in system {unit_system}"
            ),
        }
    }
}

impl std::error::Error for SelectError {}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn get_unit_for_kind_and_system_returns_unknown_system_error() {
        let result = get_unit_for_kind_and_system("Temperature", "UNKNOWN");

        assert_eq!(
            result,
            Err(SelectError::UnitSystemUnrecognized("UNKNOWN".to_string()))
        );
    }

    #[test]
    fn get_unit_for_kind_and_system_returns_unit_for_known_kind_and_system() {
        let result = get_unit_for_kind_and_system("Temperature", "CGS");

        assert_eq!(result, Ok("unit:DEG_C"));
    }

    #[test]
    fn get_unit_for_kind_and_system_returns_no_unit_error_for_missing_kind_system_pair() {
        let result = get_unit_for_kind_and_system("AmountOfSubstance", "CGS");

        assert_eq!(
            result,
            Err(SelectError::NoUnitForKindAndSystem {
                quantity_kind: "AmountOfSubstance".to_string(),
                unit_system: "CGS".to_string(),
            })
        );
    }

    #[test]
    fn get_unit_for_kind_and_system_accepts_prefixed_system_of_units() {
        let result = get_unit_for_kind_and_system("Temperature", "sou:CGS");

        assert_eq!(result, Ok("unit:DEG_C"));
    }

    #[test]
    fn get_unit_for_kind_and_system_accepts_uri_system_of_units() {
        let result = get_unit_for_kind_and_system("Temperature", "http://qudt.org/vocab/sou/CGS");

        assert_eq!(result, Ok("unit:DEG_C"));
    }

    #[test]
    fn get_labeled_units_for_kind_returns_value_for_prefixed_term() {
        let result = get_labeled_units_for_kind("quantitykind:Temperature");

        assert!(result.is_ok());
        let units = result.unwrap();

        assert!(units.contains(&("unit:DEG_C", "Degree Celsius")));
        assert!(units.contains(&("unit:DEG_F", "Degree Fahrenheit")));
        assert!(units.contains(&("unit:K", "Kelvin")));
    }

    #[test]
    fn get_labeled_units_for_kind_returns_value_for_uri() {
        let result = get_labeled_units_for_kind("http://qudt.org/vocab/quantitykind/Temperature");

        assert!(result.is_ok());
        let units = result.unwrap();

        assert!(units.contains(&("unit:DEG_C", "Degree Celsius")));
        assert!(units.contains(&("unit:DEG_F", "Degree Fahrenheit")));
        assert!(units.contains(&("unit:K", "Kelvin")));
    }

    #[test]
    fn get_labeled_units_for_kind_returns_value_for_bare_key() {
        let result = get_labeled_units_for_kind("Temperature");

        assert!(result.is_ok());
        let units = result.unwrap();

        assert!(units.contains(&("unit:DEG_C", "Degree Celsius")));
        assert!(units.contains(&("unit:DEG_F", "Degree Fahrenheit")));
        assert!(units.contains(&("unit:K", "Kelvin")));
    }

    #[test]
    fn get_labeled_units_for_kind_returns_unrecognized_for_missing_key() {
        let result = get_labeled_units_for_kind("quantitykind:UnknownKind");

        assert_eq!(
            result,
            Err(SelectError::QuantityKindUnrecognized(
                "quantitykind:UnknownKind".to_string()
            ))
        );
    }

    #[test]
    fn get_units_for_kind_returns_units_for_uri() {
        let result = get_units_for_kind("http://qudt.org/vocab/quantitykind/Temperature");

        assert!(result.is_ok());
        let units = result.unwrap();

        assert!(units.contains(&"unit:DEG_C"));
        assert!(units.contains(&"unit:DEG_F"));
        assert!(units.contains(&"unit:K"));
    }

    #[test]
    fn get_units_for_kind_returns_unrecognized_for_missing_key() {
        let result = get_units_for_kind("quantitykind:UnknownKind");

        assert_eq!(
            result,
            Err(SelectError::QuantityKindUnrecognized(
                "quantitykind:UnknownKind".to_string()
            ))
        );
    }
}
