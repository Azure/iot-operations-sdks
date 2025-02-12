// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

//! MQTT topic name and topic filter utilities

use std::cmp::{Eq, PartialEq};
use std::fmt;
use std::hash::{Hash, Hasher};
use std::iter::zip;
use std::str::FromStr;

use thiserror::Error;

// TODO: $ rules are not supported yet

/// MQTT topic level separator
const LEVEL_SEPARATOR: &str = "/";
/// MQTT topic multi-level wildcard
const MULTI_LEVEL_WILDCARD: &str = "#";
/// MQTT topic single-level wildcard
const SINGLE_LEVEL_WILDCARD: &str = "+";

/// Error when parsing a topic name or topic filter
#[derive(Error, Debug)]
pub enum TopicParseError {
    /// The topic name or topic filter is empty
    #[error("must be at least one character long")]
    Empty,
    /// The topic name contains a wildcard character (# or +)
    #[error("wildcard characters not allowed in topic name: {0}")]
    WildcardInTopicName(String),
    /// A wildcard character (# or +) does not occupy an entire level of the topic filter
    #[error("wildcard characters must occupy an entire level of the topic filter: {0}")]
    WildcardNotAlone(String),
    /// A multi-level wildcard (#) is not the last character of the topic filter
    #[error("multi-level wildcard must be the last character specified: {0}")]
    WildcardNotLast(String),
    /// The topic name's first level is $share when it is not a shared subscription topic filter
    #[error("first level of a topic name must not be $share/")]
    SharedSubscriptionNotAllowed(String),
    /// The share name of a shared subscription topic filter is empty or contains a wildcard character (# or +)
    #[error("share name must not be empty or contain wildcard characters: {0}")]
    InvalidShareName(String),
    /// A shared subscription topic filter must contain at least three levels
    #[error("shared subscription topic filter must contain at least three levels: {0}")]
    SharedSubscriptionTooShort(String),
}

/// Represents an MQTT topic name
#[derive(Debug, Clone)]
pub struct TopicName {
    /// The MQTT topic name
    topic_name: String,
    /// The levels of the MQTT topic name
    levels: Vec<String>,
}

impl TopicName {
    /// Create a new [`TopicName`] from a [`String`]
    ///
    /// # Arguments
    /// * `topic_name` - The MQTT topic name
    ///
    /// # Errors
    /// [`TopicParseError`] - If the topic name is invalid for an MQTT topic name
    pub fn from_string(topic_name: String) -> Result<TopicName, TopicParseError> {
        TopicName::check_topic_name(&topic_name)?;
        let levels = topic_name
            .split(LEVEL_SEPARATOR)
            .map(ToString::to_string)
            .collect();
        Ok(TopicName { topic_name, levels })
    }

    /// Get the [`TopicName`] formatted as a [`&str`]
    #[must_use]
    pub fn as_str(&self) -> &str {
        self.topic_name.as_str()
    }

    /// Check if the [`TopicName`] matches given [`TopicFilter`]
    ///
    /// # Arguments
    /// * `topic_filter` - The MQTT topic filter to match against
    #[must_use]
    pub fn matches_topic_filter(&self, topic_filter: &TopicFilter) -> bool {
        topic_matches(self, topic_filter)
    }

    /// Returns true if the MQTT topic name is valid
    ///
    /// # Arguments
    /// * `topic_name` - The MQTT topic name to check validity of
    #[must_use]
    pub fn is_valid_topic_name(topic_name: &str) -> bool {
        TopicName::check_topic_name(topic_name).is_ok()
    }

    /// Check format of a string against topic name rules
    ///
    /// # Errors
    /// [`TopicParseError`] - If the string is invalid for an MQTT topic name
    fn check_topic_name(topic_name: &str) -> Result<(), TopicParseError> {
        // Topic names must be at least one character long (4.7.3)
        if topic_name.is_empty() {
            return Err(TopicParseError::Empty);
        }
        // Wildcard characters MUST NOT be used in Topic Names (4.7.1)
        if topic_name.contains(MULTI_LEVEL_WILDCARD) || topic_name.contains(SINGLE_LEVEL_WILDCARD) {
            return Err(TopicParseError::WildcardInTopicName(topic_name.to_string()));
        }

        // Shared subscriptions, identified by the $share prefix, are not allowed in topic names
        if is_shared_sub(topic_name) {
            return Err(TopicParseError::SharedSubscriptionNotAllowed(
                topic_name.to_string(),
            ));
        }

        // NOTE: Adjacent topic filter level separators ("/") are valid and indicate a zero length topic level (4.7.1.1)
        // NOTE: Topic filters can contain the space (" ") character (4.7.3)
        Ok(())
    }
}

impl FromStr for TopicName {
    type Err = TopicParseError;

    /// Create a new [`TopicName`] from a [`&str`]
    ///
    /// # Arguments
    /// * `s` - The MQTT topic name
    ///
    /// # Errors
    /// [`TopicParseError`] - If the topic name is invalid for an MQTT topic name
    fn from_str(s: &str) -> Result<Self, Self::Err> {
        let topic_name = s.to_string();
        TopicName::from_string(topic_name)
    }
}

impl Hash for TopicName {
    fn hash<H: Hasher>(&self, state: &mut H) {
        // Only need to hash the topic filter since the levels are derived from the topic filter
        self.topic_name.hash(state);
    }
}

impl PartialEq for TopicName {
    fn eq(&self, other: &Self) -> bool {
        self.topic_name == other.topic_name
    }
}

impl Eq for TopicName {}

impl fmt::Display for TopicName {
    fn fmt(&self, f: &mut fmt::Formatter<'_>) -> fmt::Result {
        write!(f, "{}", self.topic_name)
    }
}

/// Represents an MQTT topic filter
#[derive(Debug, Clone)]
pub struct TopicFilter {
    /// The MQTT topic filter
    topic_filter: String,
    /// The levels of the MQTT topic filter
    levels: Vec<String>,
}

impl TopicFilter {
    /// Create a new [`TopicFilter`] from a [`String`]
    ///
    /// # Arguments
    /// * `topic_filter` - The MQTT topic filter
    ///
    /// # Errors
    /// [`TopicParseError`] - If the topic filter is invalid for an MQTT topic filter
    pub fn from_string(topic_filter: String) -> Result<TopicFilter, TopicParseError> {
        TopicFilter::check_topic_filter(&topic_filter)?;
        let levels = topic_filter
            .split(LEVEL_SEPARATOR)
            .map(ToString::to_string)
            .collect();
        Ok(TopicFilter {
            topic_filter,
            levels,
        })
    }

    /// Get the [`TopicFilter`] formatted as a [`&str`]
    #[must_use]
    pub fn as_str(&self) -> &str {
        self.topic_filter.as_str()
    }

    /// Check if the [`TopicFilter`] matches given [`TopicName`]
    ///
    /// # Arguments
    /// * `topic_name` - The MQTT topic name to match against
    #[must_use]
    pub fn matches_topic_name(&self, topic_name: &TopicName) -> bool {
        topic_matches(topic_name, self)
    }

    /// Returns true if the MQTT topic filter is valid
    ///
    /// # Arguments
    /// * `topic_filter` - The MQTT topic filter to check validity of
    #[must_use]
    pub fn is_valid_topic_filter(topic_filter: &str) -> bool {
        TopicFilter::check_topic_filter(topic_filter).is_ok()
    }

    /// Check format of a string against topic filter rules
    ///
    /// # Errors
    /// [`TopicParseError`] - If the string is invalid for an MQTT topic filter
    fn check_topic_filter(topic_filter: &str) -> Result<(), TopicParseError> {
        let mut prev_ml_wildcard = false;
        let mut levels = topic_filter.split(LEVEL_SEPARATOR);
        let mut filter_levels_length = 0;
        // Used to check if the first level of the topic filter is empty to account for the case
        // where a shared subscription topic filter is used with an empty topic filter, i.e "$share/consumer1/"
        let mut first_topic_filter_level_empty = false;

        if is_shared_sub(topic_filter) {
            // Skip $share
            levels.next();
            // The ShareName MUST NOT contain the characters "/", "+" or "#", but MUST be followed by a "/" character. This "/" character MUST be followed by a Topic Filter (4.8.2)
            match levels.next() {
                Some(share_name) => {
                    if share_name.contains(MULTI_LEVEL_WILDCARD)
                        || share_name.contains(SINGLE_LEVEL_WILDCARD)
                        || share_name.is_empty()
                    {
                        return Err(TopicParseError::InvalidShareName(topic_filter.to_string()));
                    }
                }
                None => {
                    // No share name
                    return Err(TopicParseError::SharedSubscriptionTooShort(
                        topic_filter.to_string(),
                    ));
                }
            }
        }

        // NOTE: Adjacent topic filter level separators ("/") are valid and indicate a zero length topic level (4.7.1.1)
        // NOTE: Topic filters can contain the space (" ") character (4.7.3)
        for level in levels {
            filter_levels_length += 1;

            // Check if the first level of the topic filter is empty
            if filter_levels_length == 1 && level.is_empty() {
                first_topic_filter_level_empty = true;
            }

            if prev_ml_wildcard {
                // Multi-level wildcard MUST be the last character specified (4.7.1.2)
                return Err(TopicParseError::WildcardNotLast(topic_filter.to_string()));
            }
            if level.contains(MULTI_LEVEL_WILDCARD) {
                // Multi-level wildcard MUST occupy an entire level of the topic filter (4.7.1.2)
                if level != MULTI_LEVEL_WILDCARD {
                    return Err(TopicParseError::WildcardNotAlone(topic_filter.to_string()));
                }
                prev_ml_wildcard = true;
            }
            if level.contains(SINGLE_LEVEL_WILDCARD) {
                // Single-level wildcard MUST occupy an entire level of the topic filter (4.7.1.3)
                if level != SINGLE_LEVEL_WILDCARD {
                    return Err(TopicParseError::WildcardNotAlone(topic_filter.to_string()));
                }
            }
        }
        // Topic filters must be at least one character long (4.7.3)
        if filter_levels_length == 0
            || (first_topic_filter_level_empty && filter_levels_length == 1)
        {
            return Err(TopicParseError::Empty);
        }
        Ok(())
    }
}

impl FromStr for TopicFilter {
    type Err = TopicParseError;

    /// Create a new [`TopicFilter`] from a [`&str`]
    ///
    /// # Arguments
    /// * `s` - The MQTT topic filter
    ///
    /// # Errors
    /// [`TopicParseError`] - If the topic filter is invalid for an MQTT topic filter
    fn from_str(s: &str) -> Result<Self, Self::Err> {
        let topic_filter = s.to_string();
        TopicFilter::from_string(topic_filter)
    }
}

impl Hash for TopicFilter {
    fn hash<H: Hasher>(&self, state: &mut H) {
        // Only need to hash the topic filter since the levels are derived from the topic filter
        self.topic_filter.hash(state);
    }
}

impl PartialEq for TopicFilter {
    fn eq(&self, other: &Self) -> bool {
        self.topic_filter == other.topic_filter
    }
}

impl Eq for TopicFilter {}

impl fmt::Display for TopicFilter {
    fn fmt(&self, f: &mut fmt::Formatter<'_>) -> fmt::Result {
        write!(f, "{}", self.topic_filter)
    }
}

/// Check if the given [`TopicName`] is a match for the given [`TopicFilter`]
///
/// # Arguments
/// * `topic_name` - The MQTT topic name
/// * `topic_filter` - The MQTT topic filter
#[must_use]
pub fn topic_matches(topic_name: &TopicName, topic_filter: &TopicFilter) -> bool {
    let mut topic_filter_levels_len = topic_filter.levels.len();

    let topic_filter_levels_iterator = if is_shared_sub(&topic_filter.topic_filter) {
        // Subtract the two levels used for shared subscription
        topic_filter_levels_len -= 2;
        // A shared subscription topic filter must contain at least three levels
        topic_filter.levels[2..].iter()
    } else {
        topic_filter.levels.iter()
    };

    for (filter_level, name_level) in zip(topic_filter_levels_iterator, topic_name.levels.iter())
        .map(|(fl, nl)| (fl.as_str(), nl.as_str()))
    {
        match filter_level {
            MULTI_LEVEL_WILDCARD => return true,
            SINGLE_LEVEL_WILDCARD => continue,
            _ if name_level == filter_level => continue,
            _ => return false,
        }
    }
    if topic_filter_levels_len != topic_name.levels.len() {
        return false;
    }
    true
}

/// Check if the given topic filter is a shared subscription topic filter
///
/// # Arguments
/// * `topic_filter` - The MQTT topic filter
fn is_shared_sub(topic_filter: &str) -> bool {
    // $share is a literal string that marks the Topic Filter as being a Shared Subscription Topic Filter (4.8.2)
    topic_filter.starts_with("$share/") || topic_filter == "$share"
}

#[cfg(test)]
mod tests {
    use super::*;
    use test_case::test_case;

    // NOTE: Not being able to pass types as arguments makes testing the errors not ideal.
    // You can't bind them to a variable and then use them in a match statement.

    #[test_case("sport"; "Single-level topic name")]
    #[test_case("athletic competition"; "Single-level topic name with spaces")]
    #[test_case("sport/tennis/player1"; "Multi-level topic name")]
    #[test_case("sport/field hockey/player1"; "Multi-level topic name with spaces")]
    #[test_case("sport/tennis/player1/"; "Multi-level topic name with zero-length level at end")]
    #[test_case("/sport/tennis/player1"; "Multi-level topic name with zero-length level at start")]
    #[test_case("sport//tennis//player1"; "Multi-level topic name with zero-length levels in middle")]
    #[test_case("/"; "Multi-level topic name with only zero-length levels")]
    #[test_case("$shareholders/finance/bonds/banker1"; "Non-shared subscription topic containing $share")]

    fn valid_topic_name(topic_name: &str) {
        assert!(TopicName::is_valid_topic_name(topic_name));
        assert!(TopicName::from_str(topic_name).is_ok());
    }

    #[test_case(""; "Zero-length topic name")]
    #[test_case("sport/tennis/+"; "Topic name contains single-level wildcard")]
    #[test_case("sport/tennis/#"; "Topic name contains multi-level wildcard")]
    #[test_case("$share"; "Shared subscription topic name")]
    #[test_case("$share/"; "Shared subscription topic name with empty level")]
    #[test_case("$share/consumer1"; "Shared subscription topic name with share name only")]
    #[test_case("$share/consumer1/sport/tennis/player1"; "Shared subscription topic name with multiple levels")]
    fn invalid_topic_name(topic_name: &str) {
        assert!(!TopicName::is_valid_topic_name(topic_name));
        assert!(TopicName::from_str(topic_name).is_err());
    }

    #[test_case("sport"; "Single-level topic filter")]
    #[test_case("athletic competition"; "Single-level topic filter with spaces")]
    #[test_case("+"; "Single-level topic filter with single-level wildcard")]
    #[test_case("#"; "Single-level topic filter with multi-level wildcard")]
    #[test_case("sport/tennis/player1"; "Multi-level topic filter")]
    #[test_case("sport/field hockey/team1"; "Multi-level topic filter with spaces")]
    #[test_case("sport/+/player1"; "Multi-level topic filter with single-level wildcard")]
    #[test_case("sport/#"; "Multi-level topic filter with multi-level wildcard")]
    #[test_case("+/tennis/#"; "Multi-level topic filter with single-level wildcard and multi-level wildcard")]
    #[test_case("sport/tennis/player1/"; "Multi-level topic filter with zero-length level at end")]
    #[test_case("/sport/tennis/player1"; "Multi-level topic filter with zero-length level at start")]
    #[test_case("sport//tennis//player1"; "Multi-level topic filter with zero length levels in middle")]
    #[test_case("$share/consumer1/sport/tennis/player1"; "Shared subscription topic filter")]
    #[test_case("$share/consumer1/#"; "Shared subscription topic filter with multi-level wildcard")]
    #[test_case("$share/consumer1/+/tennis/player1"; "Shared subscription topic filter with single-level wildcard")]
    #[test_case("$share/consumer1//";"Shared subscription topic filter with two zero-length levels")]
    fn valid_topic_filter(topic_filter: &str) {
        assert!(TopicFilter::is_valid_topic_filter(topic_filter));
        assert!(TopicFilter::from_str(topic_filter).is_ok());
    }

    #[test_case(""; "Zero-length topic filter")]
    #[test_case("sport+"; "Single-level wildcard does not occupy entire level of topic filter")]
    #[test_case("sport/tennis#"; "Multi-level wildcard does not occupy entire level of topic filter")]
    #[test_case("sport/tennis/#/ranking"; "Multi-level wildcard is not last character of topic filter")]
    #[test_case("$share"; "Shared subscription topic filter without share name and topic filter")]
    #[test_case("$share/consumer1"; "Shared subscription topic filter without topic filter")]
    #[test_case("$share/consumer1/"; "Shared subscription topic filter with empty topic filter")]
    #[test_case("$share//sport/tennis/player1"; "Shared subscription topic filter with zero-length level in share name")]
    #[test_case("$share/#/sport/tennis/player1"; "Shared subscription topic filter with multi-level wildcard in share name")]
    #[test_case("$share/+/sport/tennis/player1"; "Shared subscription topic filter with single-level wildcard in share name")]
    fn invalid_topic_filter(topic_filter: &str) {
        assert!(!TopicFilter::is_valid_topic_filter(topic_filter));
        assert!(TopicFilter::from_str(topic_filter).is_err());
    }

    #[test_case("sport", vec!["sport"]; "Exact match (single level topic)")]
    #[test_case("sport/tennis/player1", vec!["sport/tennis/player1"]; "Exact match (multi-level topic)")]
    #[test_case("sport/tennis/+", vec!["sport/tennis/player1", "sport/tennis/player2"]; "Single-level wildcard match (single wildcard)")]
    #[test_case("sport/+/+", vec!["sport/tennis/player1", "sport/tennis/player2", "sport/badminton/player1", "sport/badminton/player2"]; "Single-level wildcard match (multiple wildcards)")]
    #[test_case("sport/tennis/#", vec!["sport/tennis/player1", "sport/tennis/player1/ranking", "sport/tennis/player2", "sport/tennis/player2/ranking"]; "Multi-level wildcard match")]
    #[test_case("sport/+/#", vec!["sport/tennis/player1", "sport/tennis/player1/ranking", "sport/tennis/player2", "sport/tennis/player2/ranking", "sport/badminton/player1", "sport/badminton/player1/ranking", "sport/badminton/player2", "sport/badminton/player2/ranking"]; "Single-level and multi-level wildcard match")]
    #[test_case("$share/consumer1/sport/tennis/player1", vec!["sport/tennis/player1"]; "Shared subscription match")]
    #[test_case("$share/consumer1/#", vec!["sport/tennis", "sport/tennis/player1", "sport/tennis/player1/ranking"]; "Shared subscription multi-level wildcard match")]
    #[test_case("$share/consumer1/+/+/+", vec!["sport/tennis/player1", "finance/bonds/banker1"]; "Shared subscription single-level wildcard match")]
    fn normative_topic_match(topic_filter: &str, topic_names: Vec<&str>) {
        let topic_filter = TopicFilter::from_str(topic_filter).unwrap();
        for topic_name in topic_names {
            let topic_name = TopicName::from_str(topic_name).unwrap();
            assert!(topic_matches(&topic_name, &topic_filter));
            assert!(topic_name.matches_topic_filter(&topic_filter));
            assert!(topic_filter.matches_topic_name(&topic_name));
        }
    }

    #[test_case("sport", vec!["finance", "sport/tennis"]; "Exact match (single-level filter)")]
    #[test_case("sport/tennis/player1", vec!["sport/tennis/player2", "sport/tennis", "sport/tennis/player1/ranking"]; "Exact match (multi-level filter)")]
    #[test_case("sport/tennis/+", vec!["sport/tennis/player1/ranking", "sport/badminton/player1", "sport/tennis"]; "Single-level wildcard mismatch (single wildcard)")]
    #[test_case("sport/+/+", vec!["sport/tennis/player1/ranking", "finance/banking/banker1", "sport"]; "Single-level wildcard mismatch (multiple wildcards)")]
    #[test_case("sport/tennis/#", vec!["sport/tennis", "sport/badminton", "finance/banking/banker1"]; "Multi-level wildcard mismatch")]
    #[test_case("sport/+/#", vec!["sport/tennis", "sport/badminton", "finance/banking/banker1"]; "Single-level and multi-level wildcard mismatch")]
    #[test_case("$share/consumer1/sport", vec!["finance/banking/banker1", "sport/tennis/player1"]; "Shared subscription mismatch")]
    fn normative_topic_mismatch(topic_filter: &str, topic_names: Vec<&str>) {
        let topic_filter = TopicFilter::from_str(topic_filter).unwrap();
        for topic_name in topic_names {
            let topic_name = TopicName::from_str(topic_name).unwrap();
            assert!(!topic_matches(&topic_name, &topic_filter));
            assert!(!topic_name.matches_topic_filter(&topic_filter));
            assert!(!topic_filter.matches_topic_name(&topic_name));
        }
    }

    #[test_case("+", vec!["sport", "finance"]; "Single-level wildcard match (single wildcard)")]
    #[test_case("+/+", vec!["sport/tennis", "/sport", "sport/", "/"]; "Single-level wildcard match (multiple wildcards)")]
    #[test_case("#", vec!["sport", "sport/tennis", "sport/tennis/player1", "sport/tennis/player1/ranking", "sport/", "sport/", "/sport/", "/", "//"]; "Multi-level wildcard match")]
    #[test_case("+/#", vec!["sport/tennis", "sport/tennis/player1", "finance/banking", "finance/banking/banker1", "/", "//"]; "Single-level and multi-level wildcard match")]
    #[test_case("$share/consumer1//finance", vec!["/finance"]; "Shared subscription match with zero-length level")]
    fn non_normative_topic_match(topic_filter: &str, topic_names: Vec<&str>) {
        let topic_filter = TopicFilter::from_str(topic_filter).unwrap();
        for topic_name in topic_names {
            let topic_name = TopicName::from_str(topic_name).unwrap();
            assert!(topic_matches(&topic_name, &topic_filter));
            assert!(topic_name.matches_topic_filter(&topic_filter));
            assert!(topic_filter.matches_topic_name(&topic_name));
        }
    }

    #[test_case("+", vec!["/sport", "sport/", "/sport/", "/", "//"]; "Single-level wildcard mismatch (single wildcard)")]
    #[test_case("+/+", vec!["/sport/tennis", "sport/tennis/", "/tennis/", "//"]; "Single-level wildcard mismatch (multiple wildcards)")]
    #[test_case("+/#", vec!["sport"]; "Single-level and multi-level wildcard mismatch")]
    // NOTE: There are no valid topics that do not match a single multi-level wildcard, so there are no test cases for this scenario
    fn non_normative_topic_mismatch(topic_filter: &str, topic_names: Vec<&str>) {
        let topic_filter = TopicFilter::from_str(topic_filter).unwrap();
        for topic_name in topic_names {
            let topic_name = TopicName::from_str(topic_name).unwrap();
            assert!(!topic_matches(&topic_name, &topic_filter));
            assert!(!topic_name.matches_topic_filter(&topic_filter));
            assert!(!topic_filter.matches_topic_name(&topic_name));
        }
    }
}
