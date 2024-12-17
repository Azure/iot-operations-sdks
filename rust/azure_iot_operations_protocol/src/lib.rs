// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

//! Utilities for using the Azure IoT Operations Protocol over MQTT.

#![warn(missing_docs)]
#![allow(clippy::result_large_err)]

pub mod common;
pub mod rpc;
pub mod telemetry;

/// Protocol version used by all envoys in this crate.
pub(crate) const AIO_PROTOCOL_VERSION: ProtocolVersion = ProtocolVersion { major: 1, minor: 0 };

/// Assumed version if no version is provided.
pub(crate) const DEFAULT_AIO_PROTOCOL_VERSION: ProtocolVersion =
    ProtocolVersion { major: 1, minor: 0 };

/// Struct containing the major and minor version of the protocol.
pub struct ProtocolVersion {
    major: u16,
    minor: u16,
}

impl std::fmt::Display for ProtocolVersion {
    fn fmt(&self, f: &mut std::fmt::Formatter<'_>) -> std::fmt::Result {
        write!(f, "{}.{}", self.major, self.minor)
    }
}

impl ProtocolVersion {
    /// Parses a protocol version string into a [`ProtocolVersion`] struct. The string should be in the
    /// format "major.minor" where each part can be parsed into a u16.
    /// Returns [`None`] if the string is not in the correct format.
    pub(crate) fn parse_protocol_version(version: &str) -> Option<ProtocolVersion> {
        let mut protocol_version = DEFAULT_AIO_PROTOCOL_VERSION;
        if let Some((major, minor)) = version.split_once('.') {
            if let Ok(major) = major.parse::<u16>() {
                protocol_version.major = major;
                if let Ok(minor) = minor.parse::<u16>() {
                    protocol_version.minor = minor;
                    return Some(protocol_version);
                }
            }
        }
        None
    }

    /// Checks whether the major version is in the list of supported major versions.
    pub(crate) fn is_supported(&self, supported_versions: &[u16]) -> bool {
        supported_versions.contains(&self.major)
    }
}

/// Converts a vector of supported major protocol versions to a space-separated string.
/// Example: [1, 2, 3] -> "1 2 3"
pub(crate) fn supported_protocol_major_versions_to_string(supported_versions: &[u16]) -> String {
    supported_versions
        .iter()
        .map(u16::to_string)
        .collect::<Vec<String>>()
        .join(" ")
}

/// Converts a space-separated string of supported major protocol versions to a vector.
/// Example: "1 2 3" -> [1, 2, 3]
/// Ignores invalid versions and continues parsing other values.
pub(crate) fn parse_supported_protocol_major_versions(
    supported_versions: &str,
) -> std::vec::Vec<u16> {
    let versions = supported_versions
        .split_whitespace()
        .filter_map(|s| {
            if let Ok(v) = s.parse::<u16>() {
                Some(v)
            } else {
                log::warn!(
                    "Invalid major version in received supported major versions: '{}'",
                    s
                );
                None
            }
        })
        .collect();
    versions
}

#[macro_use]
extern crate derive_builder;

/// Include the README doc on a struct when running doctests to validate that the code in the
/// README can compile to verify that it has not rotted.
/// Note that any code that requires network or environment setup will not be able to run,
/// and thus should be annotated by "no_run" in the README.
#[doc = include_str!("../README.md")]
#[cfg(doctest)]
struct ReadmeDoctests;

#[cfg(test)]
mod tests {
    use crate::{
        parse_supported_protocol_major_versions, supported_protocol_major_versions_to_string,
        ProtocolVersion,
    };
    use test_case::test_case;

    #[test_case("1.0", &ProtocolVersion{major: 1, minor: 0}; "default")]
    #[test_case("2.5", &ProtocolVersion{major: 2, minor: 5}; "both_not_default")]
    #[test_case("65535.65535", &ProtocolVersion{major: 65535, minor: 65535}; "u16_max")]
    #[test_case("9999.9999", &ProtocolVersion{major: 9999, minor: 9999}; "max_9s")]
    #[test_case("0.0", &ProtocolVersion{major: 0, minor: 0}; "0_version")]
    #[test_case("100.100", &ProtocolVersion{major: 100, minor: 100}; "trailing_zeroes")]
    fn test_parse_protocol_version(version: &str, expected: &ProtocolVersion) {
        // parse and verify successful parsing
        let parsed_result = ProtocolVersion::parse_protocol_version(version);
        assert!(parsed_result.is_some());
        let parsed_version = parsed_result.unwrap();
        // Check that the parsed version is the same as the expected version
        assert_eq!(parsed_version.major, expected.major);
        assert_eq!(parsed_version.minor, expected.minor);

        // Check that converting the parsed version back to a string gives the same string
        assert_eq!(version, parsed_version.to_string());
    }

    #[test_case("nonNumeric"; "non-numeric")]
    #[test_case("non.numeric"; "non-numeric_correct_format")]
    #[test_case("1.2.3"; "extra_parts")]
    #[test_case("2.0.0"; "extra_zero_parts")]
    #[test_case("1.a"; "first_part_correct")]
    #[test_case("a.0"; "second_part_correct")]
    #[test_case(".1"; "first_part_missing")]
    #[test_case("1."; "second_part_missing")]
    #[test_case("2.3 "; "space_after")]
    #[test_case("65536.65536"; "too_big")]
    #[test_case("-1.-1"; "negative")]
    #[test_case(""; "empty")]
    fn test_parse_protocol_version_invalid(version: &str) {
        assert!(ProtocolVersion::parse_protocol_version(version).is_none());
    }

    #[test_case("1", &[1]; "default")]
    #[test_case("1 2 3", &[1, 2, 3]; "multiple")]
    #[test_case("65534 65535", &[65534, 65535]; "max_values")]
    #[test_case("1 1", &[1, 1]; "duplicates")]
    #[test_case("4 52 1", &[4, 52, 1]; "not_sorted_not_neighbors")]
    #[test_case("0", &[0]; "zero")]
    #[test_case("100 200 300", &[100, 200, 300]; "trailing_zeros")]
    fn test_parse_protocol_major_versions(versions: &str, expected: &[u16]) {
        // parse and verify successful parsing
        let parsed_versions = parse_supported_protocol_major_versions(versions);
        assert_eq!(parsed_versions, expected);

        // Check that converting the parsed versions back to a string gives the same string
        assert_eq!(
            versions,
            supported_protocol_major_versions_to_string(&parsed_versions)
        );
    }

    #[test_case("1 abc 3", &[1, 3]; "skip_invalid_values")]
    #[test_case("a b c", &[]; "all_invalid_values_correct_format")]
    #[test_case("This is a sentence!", &[]; "wrong_format")]
    #[test_case("We've had 4 inches of rain this week.", &[4]; "hidden_value")]
    #[test_case("\n \t 4 $ % ^ &", &[4]; "weird_characters")]
    fn test_parse_protocol_major_versions_invalid(versions: &str, expected: &[u16]) {
        // parse and verify successful parsing
        let parsed_versions = parse_supported_protocol_major_versions(versions);
        assert_eq!(parsed_versions, expected);

        // Since all values provided aren't valid, we can't convert it back to a string and have it match the input
    }

    #[test_case(&ProtocolVersion{major: 1, minor: 0}, &[1], true; "default")]
    #[test_case(&ProtocolVersion{major: 65535, minor: 65533}, &[65534, 65535], true; "max_values")]
    #[test_case(&ProtocolVersion{major: 5, minor: 9}, &[1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20], true; "long_list")]
    #[test_case(&ProtocolVersion{major: 2, minor: 0}, &[1], false; "not_in_list")]
    #[test_case(&ProtocolVersion{major: 0, minor: 1}, &[1], false; "minor_version_in_list")]
    fn test_supported_version(
        version: &ProtocolVersion,
        supported_versions: &[u16],
        expectation: bool,
    ) {
        assert_eq!(version.is_supported(supported_versions), expectation);
    }
}
