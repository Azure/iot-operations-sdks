// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

use std::collections::HashMap;

use regex::Regex;

use super::aio_protocol_error::{AIOProtocolError, Value};

/// Wildcard token
pub const WILDCARD: &str = "+";

/// Check if a string contains invalid characters specified in [topic-structure.md](https://github.com/microsoft/mqtt-patterns/blob/main/docs/specs/topic-structure.md)
///
/// Returns true if the string contains any of the following:
/// - Non-ASCII characters
/// - Characters outside the range of '!' to '~'
/// - Characters '+', '#', '{', '}'
///
/// # Arguments
/// * `s` - A string slice to check for invalid characters
#[must_use]
pub(crate) fn contains_invalid_char(s: &str) -> bool {
    s.chars().any(|c| {
        !c.is_ascii() || !('!'..='~').contains(&c) || c == '+' || c == '#' || c == '{' || c == '}'
    })
}

/// Determine whether a string is valid for use as a replacement string in a custom replacement map
/// or a topic namespace based on [topic-structure.md](https://github.com/microsoft/mqtt-patterns/blob/main/docs/specs/topic-structure.md)
///
/// Returns true if the string is not empty, does not contain invalid characters, does not start or
/// end with '/', and does not contain "//"
///
/// # Arguments
/// * `s` - A string slice to check for validity
#[must_use]
pub(crate) fn is_valid_replacement(s: &str) -> bool {
    !(s.is_empty()
        || contains_invalid_char(s)
        || s.starts_with('/')
        || s.ends_with('/')
        || s.contains("//"))
}

/// Represents a topic pattern for Azure IoT Operations Protocol topics
#[derive(Debug)]
pub struct TopicPattern {
    /// The topic pattern after initial replacement
    topic_pattern: String,
    /// The regex pattern to match tokens in the topic pattern
    pattern_regex: Regex,
}

impl TopicPattern {
    /// Creates a new topic pattern from a pattern string
    ///
    /// Returns a new [`TopicPattern`] on success, or an [`AIOProtocolError`] on failure
    ///
    /// # Arguments
    /// * `pattern` - A string slice representing the topic pattern
    /// * `topic_namespace` - An optional string slice representing the topic namespace
    /// * `token_map` - A map of token replacements for initial replacement
    ///
    /// # Errors
    /// Returns [`ConfigurationInvalid`](crate::common::aio_protocol_error::AIOProtocolErrorKind::ConfigurationInvalid) if the pattern
    /// is empty or invalid, the topic namespace is invalid, or a token replacement is invalid
    ///
    /// # Panics
    /// If any regex fails to compile which is impossible given that the regex are pre-defined.
    /// If any regex group is not present when it is expected to be, which is impossible given
    /// that there is only one group in the regex pattern.
    pub fn new<'a>(
        pattern: &'a str,
        topic_namespace: Option<&str>,
        topic_token_map: &'a HashMap<String, String>, // FIN: Check that this is the correct name
    ) -> Result<Self, AIOProtocolError> {
        if pattern.trim().is_empty() {
            return Err(AIOProtocolError::new_configuration_invalid_error(
                None,
                "pattern",
                Value::String(pattern.to_string()),
                Some("MQTT topic pattern must not be empty".to_string()),
                None, // ASK: From now on we won't have a command name?
            ));
        }

        if pattern.starts_with('$') {
            return Err(AIOProtocolError::new_configuration_invalid_error(
                None,
                "pattern",
                Value::String(pattern.to_string()),
                Some("MQTT topic pattern starts with reserved character '$'".to_string()),
                None,
            ));
        }

        // Check for any non-ascii characters, needed to safely use pattern.as_bytes()
        if !pattern.is_ascii() {
            return Err(AIOProtocolError::new_configuration_invalid_error(
                None,
                "pattern",
                Value::String(pattern.to_string()),
                Some("MQTT topic pattern contains non-ASCII characters".to_string()),
                None,
            ));
        }

        // Needed to check for adjacent tokens, i.e {token}{token}, without using chars() which is O(n).
        let pattern_as_bytes = pattern.as_bytes();

        // Matches empty levels at the start, middle, or end of the string
        let empty_level_regex =
            Regex::new(r"((^\s*/)|(/\s*/)|(/\s*$))").expect("Static regex string should not fail");

        if empty_level_regex.is_match(pattern) {
            return Err(AIOProtocolError::new_configuration_invalid_error(
                None,
                "pattern",
                Value::String(pattern.to_string()),
                Some("MQTT topic pattern contains empty levels".to_string()),
                None,
            ));
        }

        // Used to accumulate the pattern as checks are made
        let mut acc_pattern = String::new();

        if let Some(topic_namespace) = topic_namespace {
            if !is_valid_replacement(topic_namespace) {
                return Err(AIOProtocolError::new_configuration_invalid_error(
                    None,
                    "topic_namespace",
                    Value::String(topic_namespace.to_string()),
                    Some("MQTT topic pattern contains invalid topic namespace".to_string()),
                    None,
                ));
            }
            acc_pattern.push_str(topic_namespace);
            acc_pattern.push('/');
        }

        // Matches any tokens in the pattern
        let pattern_regex =
            Regex::new(r"(\{[^}]+\})").expect("Static regex string should not fail");
        // Matches any invalid characters in the pattern
        let invalid_regex =
            Regex::new(r"([^\x21-\x7E]|[+#{}])").expect("Static regex string should not fail");

        // Marks the index of the last match in the pattern
        let mut last_match = 0;
        for caps in pattern_regex.captures_iter(pattern) {
            // Regex library guarantees that the capture group is always present when it is only one
            let token_capture = caps.get(0).unwrap();
            // Token is captured with surrounding curly braces as per the regex pattern
            let token_with_braces = token_capture.as_str();
            let token_without_braces = &token_with_braces[1..token_with_braces.len() - 1];

            if token_without_braces.trim().is_empty() {
                return Err(AIOProtocolError::new_configuration_invalid_error(
                    None,
                    "pattern",
                    Value::String(pattern.to_string()),
                    Some("MQTT topic pattern contains empty token".to_string()),
                    None,
                ));
            }

            if let Some(c) = pattern_as_bytes.get(token_capture.end()) {
                if *c == b'{' {
                    return Err(AIOProtocolError::new_configuration_invalid_error(
                        None,
                        "pattern",
                        Value::String(pattern.to_string()),
                        Some("MQTT topic pattern contains adjacent tokens".to_string()),
                        None,
                    ));
                }
            }

            // Accumulate the pattern up to the token
            let acc = &pattern[last_match..token_capture.start()];

            if invalid_regex.is_match(acc) {
                return Err(AIOProtocolError::new_configuration_invalid_error(
                    None,
                    "pattern",
                    Value::String(pattern.to_string()),
                    Some("MQTT topic pattern contains invalid characters".to_string()),
                    None,
                ));
            }

            acc_pattern.push_str(acc);

            if invalid_regex.is_match(token_without_braces) || token_without_braces.contains('/') {
                return Err(AIOProtocolError::new_configuration_invalid_error(
                    None,
                    "pattern",
                    Value::String(token_without_braces.to_string()),
                    Some(format!(
                        "MQTT topic pattern contains invalid characters in token '{token_without_braces}'",
                    )),
                    None,
                ));
            }

            if let Some(val) = topic_token_map.get(token_without_braces) {
                if !is_valid_replacement(val) {
                    return Err(AIOProtocolError::new_configuration_invalid_error(
                        None,
                        token_without_braces,
                        Value::String(val.to_string()),
                        Some(format!(
                            "MQTT topic pattern contains token '{token_without_braces}', but replacement value '{val}' is not valid",
                        )),
                        None,
                    ));
                }
                acc_pattern.push_str(val);
            } else {
                acc_pattern.push_str(token_with_braces);
            }
            last_match = token_capture.end();
        }

        // Check the last part of the pattern
        let acc = &pattern[last_match..];
        if invalid_regex.is_match(acc) {
            return Err(AIOProtocolError::new_configuration_invalid_error(
                None,
                "pattern",
                Value::String(pattern.to_string()),
                Some("MQTT topic pattern contains invalid characters".to_string()),
                None,
            ));
        }

        acc_pattern.push_str(acc);

        Ok(TopicPattern {
            topic_pattern: acc_pattern,
            pattern_regex,
        })
    }

    /// Get the subscribe topic for the pattern
    ///
    /// Returns the subscribe topic for the pattern
    #[must_use]
    pub fn as_subscribe_topic(&self) -> String {
        self.pattern_regex
            .replace_all(&self.topic_pattern, WILDCARD.to_string())
            .to_string()
    }

    /// Get the publish topic for the pattern
    ///
    /// Returns the publish topic on success, or an [`AIOProtocolError`] on failure
    ///
    /// # Arguments
    /// * `tokens` - A map of token replacements for the topic pattern
    ///
    /// # Errors
    /// Returns [`ConfigurationInvalid`](crate::common::aio_protocol_error::AIOProtocolErrorKind::ConfigurationInvalid)
    /// if the topic contains a token without a replacement, or if the replacement is invalid
    ///
    /// # Panics
    /// Panics if regex group is not present when it is expected to be, which is impossible given
    /// that there is only one group in the regex pattern.
    pub fn as_publish_topic(
        &self,
        tokens: &HashMap<String, String>, // FIN: Better name
    ) -> Result<String, AIOProtocolError> {
        let mut publish_topic = String::with_capacity(self.topic_pattern.len());
        let mut last_match = 0;

        for caps in self.pattern_regex.captures_iter(&self.topic_pattern) {
            // Regex library guarantees that the capture group is always present when it is only one
            let key_cap = caps.get(0).unwrap();
            let key = &key_cap.as_str()[1..key_cap.as_str().len() - 1];
            publish_topic.push_str(&self.topic_pattern[last_match..key_cap.start()]);
            if let Some(val) = tokens.get(key) {
                if !is_valid_replacement(val) {
                    return Err(AIOProtocolError::new_configuration_invalid_error(
                        None,
                        key,
                        Value::String(val.to_string()),
                        Some(format!(
                            "MQTT topic pattern contains token '{key}', but replacement value '{val}' is not valid",
                        )),
                        None,
                    ));
                }
                publish_topic.push_str(val);
            } else {
                return Err(AIOProtocolError::new_configuration_invalid_error(
                    None,
                    key,
                    Value::String(String::new()),
                    Some(format!(
                        "MQTT topic pattern contains token '{key}', but no replacement value provided"
                    )),
                    None,
                ));
            }
            last_match = key_cap.end();
        }

        publish_topic.push_str(&self.topic_pattern[last_match..]);

        Ok(publish_topic)
    }

    /// Compare an MQTT topic name to the [`TopicPattern`], identifying tokens in the topic name and
    /// returning the corresponding values.
    ///
    /// Returns a map of tokens to values in the topic name.
    #[must_use]
    pub fn parse_tokens(&self, topic: &str) -> HashMap<String, String> {
        let mut tokens = HashMap::new();

        let mut topic_ref = topic;
        let mut last_token_end = 0;

        for find in self.pattern_regex.find_iter(&self.topic_pattern) {
            let token_start = find.start();
            let token_end = find.end();

            let value_start = token_start - last_token_end;
            last_token_end = token_end + 1;

            topic_ref = &topic_ref[value_start..];
            let (value, rest) = topic_ref.split_once('/').unwrap_or((topic_ref, ""));
            topic_ref = rest;

            tokens.insert(
                find.as_str()[1..find.as_str().len() - 1].to_string(),
                value.to_string(),
            );
        }

        tokens
    }
}

#[cfg(test)]
mod tests {
    use test_case::test_case;

    use super::*;
    use crate::common::aio_protocol_error::AIOProtocolErrorKind;

    #[test_case("test", "test"; "no token")]
    #[test_case("test/test", "test/test"; "no token multiple levels")]
    #[test_case("{wildToken}", "{wildToken}"; "only wildcard")]
    #[test_case("{testToken}", "testRepl"; "only token")]
    #[test_case("test/{testToken}", "test/testRepl"; "token at end")]
    #[test_case("{testToken}/test", "testRepl/test"; "token at start")]
    #[test_case("test/{testToken}/test", "test/testRepl/test"; "token in middle")]
    #[test_case("test/{testToken}/test/{testToken}", "test/testRepl/test/testRepl"; "multiple tokens")]
    #[test_case("{wildToken}/{testToken}", "{wildToken}/testRepl"; "wildcard token")]
    #[test_case("test/{testToken}/{wildToken}", "test/testRepl/{wildToken}"; "wildcard token at end")]
    #[test_case("{wildToken}/test/{testToken}", "{wildToken}/test/testRepl"; "wildcard token at start")]
    #[test_case("test/{testToken}/{wildToken}/test", "test/testRepl/{wildToken}/test"; "wildcard token in middle")]
    fn test_topic_pattern_new_pattern_valid(pattern: &str, result: &str) {
        let token_map = HashMap::from([("testToken".to_string(), "testRepl".to_string())]);

        let pattern = TopicPattern::new(pattern, None, &token_map).unwrap();

        assert_eq!(pattern.topic_pattern, result);
    }

    #[test_case(""; "empty")]
    #[test_case(" "; "whitespace")]
    #[test_case("$invalidPattern/{testToken}"; "starts with dollar")]
    #[test_case("/invalidPattern/{testToken}"; "starts with slash")]
    #[test_case("{testToken}/invalidPattern/"; "ends with slash")]
    #[test_case("invalid//Pattern/{testToken}"; "contains double slash")]
    #[test_case(" /invalidPattern/{testToken}"; "starts with whitespace")]
    #[test_case("{testToken}/invalidPattern/ "; "ends with whitespace")]
    #[test_case("invalidPattern/ /invalidPattern/{testToken}"; "level contains only whitespace")]
    #[test_case("invalidPattern/invalid Pattern/invalidPattern/{testToken}"; "level contains whitespace")]
    #[test_case("invalidPattern/invalid+Pattern/invalidPattern/{testToken}"; "level contains plus")]
    #[test_case("invalidPattern/invalid#Pattern/invalidPattern/{testToken}"; "level contains hash")]
    #[test_case("invalidPattern/invalid}Pattern/invalidPattern/{testToken}"; "level contains close brace")]
    #[test_case("invalidPattern/invalid\u{0000}Pattern/invalidPattern/{testToken}"; "level contains non-ASCII")]
    #[test_case("{testToken}{testToken}"; "adjacent tokens")]
    #[test_case("{testToken}{}"; "one adjacent empty")]
    #[test_case("{}{}"; "two adjacent empty")]
    #[test_case("test/{testToken}}"; "curly brace end")]
    fn test_topic_pattern_new_pattern_invalid(pattern: &str) {
        let token_map = HashMap::from([("testToken".to_string(), "testRepl".to_string())]);

        let err = TopicPattern::new(pattern, None, &token_map).unwrap_err();
        assert_eq!(err.kind, AIOProtocolErrorKind::ConfigurationInvalid);
        assert_eq!(err.property_name, Some("pattern".to_string()));
        assert_eq!(err.property_value, Some(Value::String(pattern.to_string())));
    }

    #[test_case("validNamespace"; "single level")]
    #[test_case("validNamespace/validNamespace"; "multiple levels")]
    fn test_topic_pattern_new_pattern_valid_topic_namespace(topic_namespace: &str) {
        let pattern = "test/{testToken}";
        let token_map = HashMap::from([("testToken".to_string(), "testRepl".to_string())]);

        TopicPattern::new(pattern, Some(topic_namespace), &token_map).unwrap();
    }

    #[test_case(""; "empty")]
    #[test_case(" "; "whitespace")]
    #[test_case("invalid Namespace"; "contains space")]
    #[test_case("invalid+Namespace"; "contains plus")]
    #[test_case("invalid#Namespace"; "contains hash")]
    #[test_case("invalid{Namespace"; "contains open brace")]
    #[test_case("invalid}Namespace"; "contains close brace")]
    #[test_case("invalid\u{0000}Namespace"; "contains non-ASCII")]
    fn test_topic_pattern_new_pattern_invalid_topic_namespace(topic_namespace: &str) {
        let pattern = "test/{testToken}";
        let token_map = HashMap::from([("testToken".to_string(), "testRepl".to_string())]);

        let err = TopicPattern::new(pattern, Some(topic_namespace), &token_map).unwrap_err();
        assert_eq!(err.kind, AIOProtocolErrorKind::ConfigurationInvalid);
        assert_eq!(err.property_name, Some("topic_namespace".to_string()));
        assert_eq!(
            err.property_value,
            Some(Value::String(topic_namespace.to_string()))
        );
    }

    #[test_case("test/{{testToken}", "{testToken"; "open brace")]
    #[test_case("test/{test+Token}", "test+Token"; "plus")]
    #[test_case("test/{test#Token}", "test#Token"; "hash")]
    #[test_case("test/{test/Token}", "test/Token"; "slash")]
    #[test_case("test/{test\u{0000}Token}", "test\u{0000}Token"; "non-ASCII")]
    fn test_topic_pattern_new_pattern_invalid_token(pattern: &str, property_value: &str) {
        let token_map = HashMap::new();
        let err = TopicPattern::new(pattern, None, &token_map).unwrap_err();
        assert_eq!(err.kind, AIOProtocolErrorKind::ConfigurationInvalid);
        assert_eq!(err.property_name, Some("pattern".to_string()));
        assert_eq!(
            err.property_value,
            Some(Value::String(property_value.to_string()))
        );
    }

    #[test_case("invalid replacement"; "replacement contains space")]
    #[test_case("invalid+replacement"; "replacement contains plus")]
    #[test_case("invalid#replacement"; "replacement contains hash")]
    #[test_case("invalid{replacement"; "replacement contains open brace")]
    #[test_case("invalid}replacement"; "replacement contains close brace")]
    #[test_case("invalid//replacement"; "replacement contains double slash")]
    #[test_case("invalid\u{0000}replacement"; "replacement contains non ASCII character")]
    #[test_case("/invalidReplacement"; "replacement starts with slash")]
    #[test_case("invalidReplacement/"; "replacement ends with slash")]
    #[test_case(""; "replacement is empty")]
    #[test_case(" "; "replacement contains only space")]
    fn test_topic_pattern_new_pattern_invalid_replacement(replacement: &str) {
        let pattern = "test/{testToken}/test";
        let token_map = HashMap::from([("testToken".to_string(), replacement.to_string())]);

        let err = TopicPattern::new(pattern, None, &token_map).unwrap_err();
        assert_eq!(err.kind, AIOProtocolErrorKind::ConfigurationInvalid);
        assert_eq!(err.property_name, Some("testToken".to_string()));
        assert_eq!(
            err.property_value,
            Some(Value::String(replacement.to_string()))
        );
    }

    #[test_case("test", "test"; "no token")]
    #[test_case("{wildToken}", "+"; "single token")]
    #[test_case("{wildToken}/test", "+/test"; "token at start")]
    #[test_case("test/{wildToken}", "test/+"; "token at end")]
    #[test_case("test/{wildToken}/test", "test/+/test"; "token in middle")]
    #[test_case("{wildToken}/{wildToken}", "+/+"; "multiple tokens")]
    #[test_case("{wildToken}/test/{wildToken}", "+/test/+"; "token at start and end")]
    #[test_case("{wildToken1}/{wildToken2}", "+/+"; "multiple wildcards")]
    fn test_topic_pattern_as_subscribe_topic(pattern: &str, result: &str) {
        let token_map = HashMap::new();
        let pattern = TopicPattern::new(pattern, None, &token_map).unwrap();

        assert_eq!(pattern.as_subscribe_topic(), result);
    }

    #[test_case("test", &HashMap::new(), "test"; "no token")]
    #[test_case("{testToken}", &HashMap::from([("testToken".to_string(), "testRepl".to_string())]), "testRepl"; "single token")]
    #[test_case("{testToken}/test", &HashMap::from([("testToken".to_string(), "testRepl".to_string())]), "testRepl/test"; "token at start")]
    #[test_case("test/{testToken}", &HashMap::from([("testToken".to_string(), "testRepl".to_string())]), "test/testRepl"; "token at end")]
    #[test_case("test/{testToken}/test", &HashMap::from([("testToken".to_string(), "testRepl".to_string())]), "test/testRepl/test"; "token in middle")]
    #[test_case("{testToken1}/{testToken2}", &HashMap::from([("testToken1".to_string(), "testRepl1".to_string()), ("testToken2".to_string(), "testRepl2".to_string())]), "testRepl1/testRepl2"; "multiple tokens")]
    fn test_topic_pattern_as_publish_topic_valid(
        pattern: &str,
        tokens: &HashMap<String, String>,
        result: &str,
    ) {
        let pattern = TopicPattern::new(pattern, None, tokens).unwrap();

        assert_eq!(pattern.as_publish_topic(tokens).unwrap(), result);
    }

    #[test_case("{testToken}", &HashMap::new(), "testToken", ""; "no replacement")]
    #[test_case("{testToken}", &HashMap::from([("testToken".to_string(), "invalid Replacement".to_string())]), "testToken", "invalid Replacement"; "replacement contains space")]
    #[test_case("{testToken}", &HashMap::from([("testToken".to_string(), "invalid+Replacement".to_string())]), "testToken", "invalid+Replacement"; "replacement contains plus")]
    #[test_case("{testToken}", &HashMap::from([("testToken".to_string(), "invalid#Replacement".to_string())]), "testToken", "invalid#Replacement"; "replacement contains hash")]
    #[test_case("{testToken}", &HashMap::from([("testToken".to_string(), "invalid{Replacement".to_string())]), "testToken", "invalid{Replacement"; "replacement contains open brace")]
    #[test_case("{testToken}", &HashMap::from([("testToken".to_string(), "invalid}Replacement".to_string())]), "testToken", "invalid}Replacement"; "replacement contains close brace")]
    #[test_case("{testToken}", &HashMap::from([("testToken".to_string(), "invalid//Replacement".to_string())]), "testToken", "invalid//Replacement"; "replacement contains double slash")]
    #[test_case("{testToken}", &HashMap::from([("testToken".to_string(), "invalid\u{0000}Replacement".to_string())]), "testToken", "invalid\u{0000}Replacement"; "replacement contains non ASCII character")]
    #[test_case("{testToken}", &HashMap::from([("testToken".to_string(), "/invalidReplacement".to_string())]), "testToken", "/invalidReplacement"; "replacement starts with slash")]
    #[test_case("{testToken}", &HashMap::from([("testToken".to_string(), "invalidReplacement/".to_string())]), "testToken", "invalidReplacement/"; "replacement ends with slash")]
    #[test_case("{testToken}", &HashMap::from([("testToken".to_string(), String::new())]), "testToken", ""; "replacement is empty")]
    #[test_case("{testToken}", &HashMap::from([("testToken".to_string(), " ".to_string())]), "testToken", " "; "replacement contains only space")]
    fn test_topic_pattern_as_publish_topic_invalid(
        pattern: &str,
        tokens: &HashMap<String, String>,
        property_name: &str,
        property_value: &str,
    ) {
        let pattern = TopicPattern::new(pattern, None, &HashMap::new()).unwrap();

        let err = pattern.as_publish_topic(tokens).unwrap_err();
        assert_eq!(err.kind, AIOProtocolErrorKind::ConfigurationInvalid);
        assert_eq!(err.property_name, Some(property_name.to_string()));
        assert_eq!(
            err.property_value,
            Some(Value::String(property_value.to_string()))
        );
    }

    #[test_case("test", "test", &HashMap::new(); "no token")]
    #[test_case("{testToken}", "testRepl", &HashMap::from([("testToken".to_string(), "testRepl".to_string())]); "single token")]
    #[test_case("{testToken}/test", "testRepl/test", &HashMap::from([("testToken".to_string(), "testRepl".to_string())]); "token at start")]
    #[test_case("test/{testToken}", "test/testRepl", &HashMap::from([("testToken".to_string(), "testRepl".to_string())]); "token at end")]
    #[test_case("test/{testToken}/test", "test/testRepl/test", &HashMap::from([("testToken".to_string(), "testRepl".to_string())]); "token in middle")]
    #[test_case("{testToken1}/{testToken2}", "testRepl1/testRepl2", &HashMap::from([("testToken1".to_string(), "testRepl1".to_string()),("testToken2".to_string(), "testRepl2".to_string())]); "multiple tokens")]
    fn test_topic_pattern_parse_tokens(
        pattern: &str,
        topic: &str,
        result: &HashMap<String, String>,
    ) {
        let pattern = TopicPattern::new(pattern, None, &HashMap::new()).unwrap();

        assert_eq!(pattern.parse_tokens(topic), *result);
    }
}
