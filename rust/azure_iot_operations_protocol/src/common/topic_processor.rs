// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

use std::collections::HashMap;

use regex::Regex;

/// Wildcard token
pub const WILDCARD: &str = "+";

// NOTE: This error design is less than ideal as detailed messages are only provided for the
// InvalidPattern kind. This is because the other error kinds have logic that validates many
// things at once, thus not allowing an easy way to report granular detail without reworking
// substantial structure and organization of this module.
//
// It has been suggested that namespaces and share names should be validated
// separately before being provided to the constructor as well, as they are distinct from the
// pattern, and having a TopicPatternError for something that is not a topic pattern is
// semantically strange, which may help in improving error implementation here.
//
// This would also probably allow for better semantic separation of pattern failures from
// token replacement failures, which would improve the experience of using this module.

/// Represents an error that occurred when creating a [`TopicPattern`]
#[derive(thiserror::Error, Debug)]
pub struct TopicPatternError {
    msg: Option<String>,
    kind: TopicPatternErrorKind,
}

impl TopicPatternError {
    /// Get the kind of error that occurred when creating a [`TopicPattern`]
    #[must_use]
    pub fn kind(&self) -> &TopicPatternErrorKind {
        &self.kind
    }
}

impl std::fmt::Display for TopicPatternError {
    fn fmt(&self, f: &mut std::fmt::Formatter<'_>) -> std::fmt::Result {
        if let Some(msg) = &self.msg {
            write!(f, "{} - {}", self.kind, msg)?;
        }
        write!(f, "{}", self.kind)
    }
}

/// Represents the kind of error that occurred when creating a [`TopicPattern`]
#[derive(thiserror::Error, Debug)]
pub enum TopicPatternErrorKind {
    /// The topic pattern is invalid
    #[error("Topic pattern is invalid")]
    InvalidPattern(String),
    /// The share name is invalid
    #[error("Share name '{0}' is invalid")]
    InvalidShareName(String),
    /// The topic namespace is invalid
    #[error("Topic namespace '{0}' is invalid")]
    InvalidNamespace(String),
    /// Could not replace a token in the topic pattern
    #[error("Token '{0}' replacement value '{1}' is invalid")]
    InvalidTokenReplacement(String, String),
}

/// Check if a string contains invalid characters specified in [topic-structure.md](https://github.com/Azure/iot-operations-sdks/blob/main/doc/reference/topic-structure.md)
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
/// or a topic namespace based on [topic-structure.md](https://github.com/Azure/iot-operations-sdks/blob/main/doc/reference/topic-structure.md)
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
    /// The topic pattern before the initial replacements have been made
    static_pattern: String,
    /// The topic pattern after the initial replacements have been made
    dynamic_pattern: String,
    /// The regex pattern to match tokens in the topic pattern
    pattern_regex: Regex,
    /// The share name for the topic pattern
    share_name: Option<String>,
}

impl TopicPattern {
    /// Creates a new topic pattern from a pattern string
    ///
    /// Returns a new [`TopicPattern`] on success, or [`TopicPatternError`] on failure
    ///
    /// # Arguments
    /// * `property_name` - A string slice representing the name of the property that provides the topic pattern
    /// * `pattern` - A string slice representing the topic pattern
    /// * `share_name` - An optional string representing the share name for the topic pattern
    /// * `topic_namespace` - An optional string slice representing the topic namespace
    /// * `token_map` - A map of token replacements for initial replacement
    ///
    /// # Errors
    /// The kind of error is determined by which argument is invalid:
    /// - Has kind [`TopicPatternErrorKind::InvalidPattern`] if the pattern is invalid
    /// - Has kind [`TopicPatternErrorKind::InvalidShareName`] if the share name is invalid
    /// - Has kind [`TopicPatternErrorKind::InvalidNamespace`] if the topic namespace is invalid
    /// - Has kind [`TopicPatternErrorKind::InvalidTokenReplacement`] if the token replacement is invalid
    ///
    /// # Panics
    /// If any regex fails to compile which is impossible given that the regex are pre-defined.
    ///
    /// If any regex group is not present when it is expected to be, which is impossible given
    /// that there is only one group in the regex pattern.
    pub fn new<'a>(
        pattern: &'a str,
        share_name: Option<String>,
        topic_namespace: Option<&str>,
        topic_token_map: &'a HashMap<String, String>,
    ) -> Result<Self, TopicPatternError> {
        if pattern.trim().is_empty() {
            return Err(TopicPatternError {
                msg: Some("Pattern is empty".to_string()),
                kind: TopicPatternErrorKind::InvalidPattern(pattern.to_string()),
            });
        }

        if pattern.starts_with('$') {
            return Err(TopicPatternError {
                msg: Some("Pattern must not start with '$'".to_string()),
                kind: TopicPatternErrorKind::InvalidPattern(pattern.to_string()),
            });
        }

        if let Some(share_name) = &share_name {
            if share_name.trim().is_empty()
                || contains_invalid_char(share_name)
                || share_name.contains('/')
            {
                return Err(TopicPatternError {
                    msg: None,
                    kind: TopicPatternErrorKind::InvalidShareName(share_name.to_string()),
                });
            }
        }

        // Matches empty levels at the start, middle, or end of the pattern
        let empty_level_regex =
            Regex::new(r"((^\s*/)|(/\s*/)|(/\s*$))").expect("Static regex string should not fail");

        if empty_level_regex.is_match(pattern) {
            return Err(TopicPatternError {
                msg: Some("Contains empty level(s)".to_string()),
                kind: TopicPatternErrorKind::InvalidPattern(pattern.to_string()),
            });
        }

        // Used to accumulate the pattern as checks and replacements are made
        let mut acc_pattern = String::new();

        if let Some(topic_namespace) = topic_namespace {
            if !is_valid_replacement(topic_namespace) {
                return Err(TopicPatternError {
                    msg: None,
                    kind: TopicPatternErrorKind::InvalidNamespace(topic_namespace.to_string()),
                });
            }
            acc_pattern.push_str(topic_namespace);
            acc_pattern.push('/');
        }

        // Matches any tokens in the pattern, i.e foo/{bar} would match {bar}
        let pattern_regex =
            Regex::new(r"(\{[^}]+\})").expect("Static regex string should not fail");
        // Matches any invalid characters in the pattern
        let invalid_regex =
            Regex::new(r"([^\x21-\x7E]|[+#{}])").expect("Static regex string should not fail");

        // Marks the index of the last match in the pattern
        let mut last_match = 0;
        let mut last_end_index = 0;
        for caps in pattern_regex.captures_iter(pattern) {
            // Regex library guarantees that the capture group is always present when it is only one
            let token_capture = caps.get(0).unwrap();
            // Token is captured with surrounding curly braces as per the regex pattern
            let token_with_braces = token_capture.as_str();
            let token_without_braces = &token_with_braces[1..token_with_braces.len() - 1];

            if token_without_braces.trim().is_empty() {
                return Err(TopicPatternError {
                    msg: Some("Contains empty token".to_string()),
                    kind: TopicPatternErrorKind::InvalidPattern(pattern.to_string()),
                });
            }

            if last_end_index != 0 && last_end_index == token_capture.start() {
                return Err(TopicPatternError {
                    msg: Some("Contains adjacent tokens".to_string()),
                    kind: TopicPatternErrorKind::InvalidPattern(pattern.to_string()),
                });
            }

            last_end_index = token_capture.end();
            // Accumulate the pattern up to the token
            let acc = &pattern[last_match..token_capture.start()];

            // Check if the accumulated part of the pattern is valid
            if invalid_regex.is_match(acc) {
                return Err(TopicPatternError {
                    msg: Some("Contains invalid characters".to_string()),
                    kind: TopicPatternErrorKind::InvalidPattern(pattern.to_string()),
                });
            }

            acc_pattern.push_str(acc);

            // Check if the token is valid
            if invalid_regex.is_match(token_without_braces) || token_without_braces.contains('/') {
                return Err(TopicPatternError {
                    msg: Some(format!(
                        "Contains invalid characters in token {token_without_braces}"
                    )),
                    kind: TopicPatternErrorKind::InvalidPattern(pattern.to_string()),
                });
            }

            // Check if the replacement is valid
            if let Some(val) = topic_token_map.get(token_without_braces) {
                if !is_valid_replacement(val) {
                    return Err(TopicPatternError {
                        msg: None,
                        kind: TopicPatternErrorKind::InvalidTokenReplacement(
                            token_without_braces.to_string(),
                            val.to_string(),
                        ),
                    });
                }
                acc_pattern.push_str(val);
            } else {
                // Token is not replaced, so append the token with braces
                acc_pattern.push_str(token_with_braces);
            }
            last_match = token_capture.end();
        }

        // Check the last part of the pattern
        let acc = &pattern[last_match..];
        if invalid_regex.is_match(acc) {
            return Err(TopicPatternError {
                msg: Some("Contains invalid characters".to_string()),
                kind: TopicPatternErrorKind::InvalidPattern(pattern.to_string()),
            });
        }

        acc_pattern.push_str(acc);

        Ok(TopicPattern {
            static_pattern: pattern.to_string(),
            dynamic_pattern: acc_pattern,
            pattern_regex,
            share_name,
        })
    }

    /// Get the subscribe topic for the pattern
    ///
    /// If a share name is present, it is prepended to the topic pattern
    ///
    /// Returns the subscribe topic for the pattern
    #[must_use]
    pub fn as_subscribe_topic(&self) -> String {
        let topic = self
            .pattern_regex
            .replace_all(&self.dynamic_pattern, WILDCARD)
            .to_string();
        if let Some(share_name) = &self.share_name {
            format!("$share/{share_name}/{topic}")
        } else {
            topic
        }
    }

    /// Get the publish topic for the pattern
    ///
    /// Returns the publish topic as a String on success, or an [`TopicPatternError`] on failure
    ///
    /// # Arguments
    /// * `tokens` - A map of token replacements for the topic pattern, can be empty if there are
    ///     no replacements to be made
    ///
    /// # Errors
    /// The error kind will be [`TopicPatternErrorKind::InvalidTokenReplacement`] if the topic
    /// contains a token that was not provided in the replacement map, or if the replacement is
    /// invalid.
    ///
    /// # Panics
    /// Panics if regex group is not present when it is expected to be, which is impossible given
    /// that there is only one group in the regex pattern.
    pub fn as_publish_topic(
        &self,
        tokens: &HashMap<String, String>,
    ) -> Result<String, TopicPatternError> {
        // Initialize the publish topic with the same capacity as the pattern to avoid reallocations
        let mut publish_topic = String::with_capacity(self.dynamic_pattern.len());

        // Marks the index of the last match in the pattern
        let mut last_match = 0;
        for caps in self.pattern_regex.captures_iter(&self.dynamic_pattern) {
            // Regex library guarantees that the capture group is always present when it is only one
            let key_cap = caps.get(0).unwrap();

            // Token is captured with surrounding curly braces as per the regex pattern, removed here
            let key = &key_cap.as_str()[1..key_cap.as_str().len() - 1];

            // Accumulate the pattern up to the token
            publish_topic.push_str(&self.dynamic_pattern[last_match..key_cap.start()]);

            // Check if the replacement is valid
            if let Some(val) = tokens.get(key) {
                if !is_valid_replacement(val) {
                    return Err(TopicPatternError {
                        msg: None,
                        kind: TopicPatternErrorKind::InvalidTokenReplacement(
                            key.to_string(),
                            val.to_string(),
                        ),
                    });
                }
                publish_topic.push_str(val);
            } else {
                return Err(TopicPatternError {
                    msg: None,
                    kind: TopicPatternErrorKind::InvalidTokenReplacement(
                        key.to_string(),
                        String::new(),
                    ),
                });
            }
            last_match = key_cap.end();
        }

        publish_topic.push_str(&self.dynamic_pattern[last_match..]);

        Ok(publish_topic)
    }

    /// Compare an MQTT topic name to the [`TopicPattern`], identifying tokens in the topic name and
    /// returning the corresponding values.
    ///
    /// Returns a map of tokens to values in the topic name.
    #[must_use]
    pub fn parse_tokens(&self, topic: &str) -> HashMap<String, String> {
        let mut tokens = HashMap::new();

        // Create a mutable reference to the topic string
        let mut topic_ref = topic;

        // Marks the index of the last match in the topic
        let mut last_token_end = 0;

        // Find all the tokens in the pattern
        for find in self.pattern_regex.find_iter(&self.static_pattern) {
            // Get the start and end indices of the current match
            let token_start = find.start();
            let token_end = find.end();

            // Calculate the start index of the value in the topic
            let value_start = token_start - last_token_end;
            // Update the last_token_end to the end of the current match + 1 to skip the '/'
            // Note: We won't have an out of bounds error here because if this is the last token,
            // we won't have another match
            last_token_end = token_end + 1;

            // Slice the topic string to start from the start of the token
            topic_ref = &topic_ref[value_start..];

            // Split the topic string at the next '/' to get the value of the token and the rest of the topic
            let (value, rest) = topic_ref.split_once('/').unwrap_or((topic_ref, ""));
            // Update topic_ref to the rest of the topic
            topic_ref = rest;

            // Insert the token and value into the tokens map
            tokens.insert(
                find.as_str()[1..find.as_str().len() - 1].to_string(), // Remove the curly braces
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

    fn create_topic_tokens() -> HashMap<String, String> {
        HashMap::from([
            ("testToken1".to_string(), "testRepl1".to_string()),
            ("testToken2".to_string(), "testRepl2".to_string()),
            ("testToken3".to_string(), "testRepl3".to_string()),
        ])
    }

    #[test_case("test", "test"; "no token")]
    #[test_case("test/test", "test/test"; "no token multiple levels")]
    #[test_case("{wildToken}", "{wildToken}"; "only wildcard")]
    #[test_case("{testToken1}", "testRepl1"; "only token")]
    #[test_case("test/{testToken1}", "test/testRepl1"; "token at end")]
    #[test_case("{testToken1}/test", "testRepl1/test"; "token at start")]
    #[test_case("test/{testToken1}/test", "test/testRepl1/test"; "token in middle")]
    #[test_case("test/{testToken1}/test/{testToken1}", "test/testRepl1/test/testRepl1"; "multiple identical tokens")]
    #[test_case("{wildToken}/{testToken1}", "{wildToken}/testRepl1"; "wildcard token")]
    #[test_case("test/{testToken1}/{wildToken}", "test/testRepl1/{wildToken}"; "wildcard token at end")]
    #[test_case("{wildToken}/test/{testToken1}", "{wildToken}/test/testRepl1"; "wildcard token at start")]
    #[test_case("test/{testToken1}/{wildToken}/test", "test/testRepl1/{wildToken}/test"; "wildcard token in middle")]
    #[test_case("test/{testToken1}/{testToken2}/{testToken3}", "test/testRepl1/testRepl2/testRepl3"; "multiple varied tokens")]
    fn test_topic_pattern_new_pattern_valid(pattern: &str, result: &str) {
        let pattern = TopicPattern::new(pattern, None, None, &create_topic_tokens()).unwrap();

        assert_eq!(pattern.dynamic_pattern, result);
    }

    #[test_case(""; "empty")]
    #[test_case(" "; "whitespace")]
    #[test_case("$invalidPattern/{testToken1}"; "starts with dollar")]
    #[test_case("/invalidPattern/{testToken1}"; "starts with slash")]
    #[test_case("{testToken1}/invalidPattern/"; "ends with slash")]
    #[test_case("invalid//Pattern/{testToken1}"; "contains double slash")]
    #[test_case(" /invalidPattern/{testToken1}"; "starts with whitespace")]
    #[test_case("{testToken1}/invalidPattern/ "; "ends with whitespace")]
    #[test_case("invalidPattern/ /invalidPattern/{testToken1}"; "level contains only whitespace")]
    #[test_case("invalidPattern/invalid Pattern/invalidPattern/{testToken1}"; "level contains whitespace")]
    #[test_case("invalidPattern/invalid+Pattern/invalidPattern/{testToken1}"; "level contains plus")]
    #[test_case("invalidPattern/invalid#Pattern/invalidPattern/{testToken1}"; "level contains hash")]
    #[test_case("invalidPattern/invalid}Pattern/invalidPattern/{testToken1}"; "level contains close brace")]
    #[test_case("invalidPattern/invalid\u{0000}Pattern/invalidPattern/{testToken1}"; "level contains non-ASCII")]
    #[test_case("invalidPattern/{testToken1}/invalid\u{0000}Pattern/invalidPattern/{testToken2}"; "level contains non-ASCII varied token")]
    #[test_case("{testToken1}{testToken1}"; "adjacent tokens")]
    #[test_case("{testToken1} {testToken1}"; "adjacent spaced tokens")]
    #[test_case("{testToken1}{}"; "one adjacent empty")]
    #[test_case("{}{}"; "two adjacent empty")]
    #[test_case("test/{testToken1}}"; "curly brace end")]
    fn test_topic_pattern_new_pattern_invalid(pattern: &str) {
        let err = TopicPattern::new(pattern, None, None, &create_topic_tokens()).unwrap_err();
        matches!(err.kind(), TopicPatternErrorKind::InvalidPattern(p) if p == pattern);
    }

    #[test_case("validNamespace"; "single level")]
    #[test_case("validNamespace/validNamespace"; "multiple levels")]
    fn test_topic_pattern_new_pattern_valid_topic_namespace(topic_namespace: &str) {
        let pattern = "test/{testToken1}";

        TopicPattern::new(pattern, None, Some(topic_namespace), &create_topic_tokens()).unwrap();
    }

    #[test_case(""; "empty")]
    #[test_case(" "; "whitespace")]
    #[test_case("invalid Namespace"; "contains space")]
    #[test_case("invalid+Namespace"; "contains plus")]
    #[test_case("invalid#Namespace"; "contains hash")]
    #[test_case("invalid{Namespace"; "contains open brace")]
    #[test_case("invalid}Namespace"; "contains close brace")]
    #[test_case("invalid\u{0000}Namespace"; "contains non-ASCII")]
    #[test_case("/invalidNamespace"; "namespace starts with slash")]
    #[test_case("invalidNamespace/"; "namespace ends with slash")]
    fn test_topic_pattern_new_pattern_invalid_topic_namespace(topic_namespace: &str) {
        let pattern = "test/{testToken1}";

        let err = TopicPattern::new(pattern, None, Some(topic_namespace), &create_topic_tokens())
            .unwrap_err();
        matches!(err.kind(), TopicPatternErrorKind::InvalidNamespace(n) if n == topic_namespace);
    }

    #[test_case("test/{{testToken1}"; "open brace")]
    #[test_case("test/{test+Token}"; "plus")]
    #[test_case("test/{test#Token}"; "hash")]
    #[test_case("test/{test/Token}"; "slash")]
    #[test_case("test/{test\u{0000}Token}"; "non-ASCII")]
    fn test_topic_pattern_new_pattern_invalid_token(pattern: &str) {
        let err = TopicPattern::new(pattern, None, None, &HashMap::new()).unwrap_err();
        matches!(err.kind(), TopicPatternErrorKind::InvalidPattern(p) if p == pattern);
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

        let err = TopicPattern::new(
            pattern,
            None,
            None,
            &HashMap::from([("testToken".to_string(), replacement.to_string())]),
        )
        .unwrap_err();
        matches!(err.kind(), TopicPatternErrorKind::InvalidTokenReplacement(t, r) if t == "testToken" && r == replacement);
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
        let pattern = TopicPattern::new(pattern, None, None, &HashMap::new()).unwrap();

        assert_eq!(pattern.as_subscribe_topic(), result);
    }

    #[test_case("invalid ShareName"; "contains space")]
    #[test_case("invalid+ShareName"; "contains plus")]
    #[test_case("invalid#ShareName"; "contains hash")]
    #[test_case("invalid{ShareName"; "contains open brace")]
    #[test_case("invalid}ShareName"; "contains close brace")]
    #[test_case("invalid/ShareName"; "contains slash")]
    #[test_case("invalid\u{0000}ShareName"; "contains non-ASCII")]
    fn test_topic_pattern_new_pattern_invalid_share_name(share_name: &str) {
        let err = TopicPattern::new("test", Some(share_name.to_string()), None, &HashMap::new())
            .unwrap_err();
        matches!(err.kind(), TopicPatternErrorKind::InvalidShareName(s) if s == share_name);
    }

    #[test]
    fn test_topic_pattern_methods_with_share_name() {
        let share_name = "validShareName";
        let pattern = "test/{testToken1}";
        let result = "$share/validShareName/test/testRepl1";

        let pattern = TopicPattern::new(
            pattern,
            Some(share_name.to_string()),
            None,
            &create_topic_tokens(),
        )
        .unwrap();

        assert_eq!(pattern.as_subscribe_topic(), result);
        assert_eq!(
            pattern.as_publish_topic(&HashMap::new()).unwrap(),
            "test/testRepl1"
        );
    }

    #[test_case("test", &HashMap::new(), "test"; "no token")]
    #[test_case("{testToken}", &HashMap::from([("testToken".to_string(), "testRepl".to_string())]), "testRepl"; "single token")]
    #[test_case("{testToken}", &HashMap::from([("testToken".to_string(), "testReplLonger".to_string())]), "testReplLonger"; "single token long replacement")]
    #[test_case("{testToken}/test", &HashMap::from([("testToken".to_string(), "testRepl".to_string())]), "testRepl/test"; "token at start")]
    #[test_case("test/{testToken}", &HashMap::from([("testToken".to_string(), "testRepl".to_string())]), "test/testRepl"; "token at end")]
    #[test_case("test/{testToken}/test", &HashMap::from([("testToken".to_string(), "testRepl".to_string())]), "test/testRepl/test"; "token in middle")]
    #[test_case("{testToken1}/{testToken2}", &HashMap::from([("testToken1".to_string(), "testRepl1".to_string()), ("testToken2".to_string(), "testRepl2".to_string())]), "testRepl1/testRepl2"; "multiple tokens")]
    fn test_topic_pattern_as_publish_topic_valid(
        pattern: &str,
        tokens: &HashMap<String, String>,
        result: &str,
    ) {
        let pattern = TopicPattern::new(pattern, None, None, tokens).unwrap();

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
        expected_token: &str,
        expected_replacement: &str,
    ) {
        let pattern = TopicPattern::new(pattern, None, None, &HashMap::new()).unwrap();

        let err = pattern.as_publish_topic(tokens).unwrap_err();
        matches!(err.kind(), TopicPatternErrorKind::InvalidTokenReplacement(t, r) if t == expected_token && r == expected_replacement);
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
        let pattern = TopicPattern::new(pattern, None, None, &HashMap::new()).unwrap();

        assert_eq!(pattern.parse_tokens(topic), *result);
    }
}
