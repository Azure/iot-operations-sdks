// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

use regex::Regex;

use super::aio_protocol_error::{AIOProtocolError, Value};
use std::collections::HashMap;

/// Wildcard token
pub const WILDCARD: &str = "+";

/// Regex checking if a string contains empty levels. Empty levels are levels that contain only
/// whitespace characters or no characters at all.
///
/// Matches if the string contains any of the following:
/// - Empty level at the start of the string
/// - Empty level in the middle of the string
/// - Empty level at the end of the string
const EMPTY_LEVEL_REGEX: &str = r"(^\s*\/)|(\/\s*\/)|(\/\s*$)";

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
pub fn contains_invalid_char(s: &str) -> bool {
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
pub fn is_valid_replacement(s: &str) -> bool {
    !(s.is_empty()
        || contains_invalid_char(s)
        || s.starts_with('/')
        || s.ends_with('/')
        || s.contains("//"))
}

/// Represents a topic pattern for Azure IoT Operations Protocol topics
#[derive(Debug)]
pub struct TopicPattern {
    topic_pattern: String,
    pattern_regex: Regex,
}

impl TopicPattern {
    // FIN: Make sure to write the docs for this, save it for last.
    pub fn new<'a>(
        pattern: &'a str,
        topic_namespace: Option<&str>,
        token_map: &'a HashMap<String, String>, // FIN: Check that this is the correct name
    ) -> Result<Self, AIOProtocolError> {
        // FIN: Add error checking
        // FIN: Tests
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

        let mut working_pattern = String::new();

        if let Some(topic_namespace) = topic_namespace {
            if is_valid_replacement(topic_namespace) {
                return Err(AIOProtocolError::new_configuration_invalid_error(
                    None,
                    "topic_namespace",
                    Value::String(topic_namespace.to_string()),
                    Some("MQTT topic pattern contains invalid topic namespace".to_string()),
                    None,
                ));
            }
            working_pattern.push_str(topic_namespace);
            working_pattern.push('/');
        }

        if Regex::new(EMPTY_LEVEL_REGEX)
            .expect("Static regex string should not fail")
            .is_match(&pattern)
        {
            return Err(AIOProtocolError::new_configuration_invalid_error(
                None,
                "pattern",
                Value::String(pattern.to_string()),
                Some("MQTT topic pattern contains empty level".to_string()),
                None,
            ));
        }

        let pattern_regex =
            Regex::new(r"\{(?<token>[^}]+)\}").expect("Static regex string should not fail"); // FIN: Add this to a test

        let mut last_match = 0;
        for caps in pattern_regex.captures_iter(&pattern) {
            let token_capture = caps.name("token").expect("Token should always be present");
            let token = token_capture.as_str();

            if token.trim().is_empty() {
                return Err(AIOProtocolError::new_configuration_invalid_error(
                    None,
                    "pattern",
                    Value::String(pattern.to_string()),
                    Some("MQTT topic pattern contains empty token".to_string()),
                    None,
                ));
            }

            if !token.is_ascii() {
                return Err(AIOProtocolError::new_configuration_invalid_error(
                    None,
                    "pattern",
                    Value::String(pattern.to_string()),
                    Some("MQTT topic pattern contains non-ASCII token".to_string()),
                    None,
                ));
            }

            working_pattern.push_str(&pattern[last_match..token_capture.start()]);
            if let Some(val) = token_map.get(token_capture.as_str()) {
                if !is_valid_replacement(val) {
                    return Err(AIOProtocolError::new_configuration_invalid_error(
                        None,
                        token,
                        Value::String(val.to_string()),
                        Some(format!(
                            "MQTT topic pattern contains token '{token}', but replacement value '{val}' is not valid",
                        )),
                        None,
                    ));
                }

                working_pattern.push_str(val);
            } else {
                token.to_string();
            }
            last_match = token_capture.end();
        }

        working_pattern.push_str(&pattern[last_match..]);

        Ok(TopicPattern {
            topic_pattern: working_pattern,
            pattern_regex,
        })
    }

    // Convenience function to create a no replacement error
    fn no_replacement_error(token: &str, command_name: Option<String>) -> AIOProtocolError {
        let param_name = token.trim_start_matches('{').trim_end_matches('}');
        AIOProtocolError::new_configuration_invalid_error(
            None,
            param_name,
            Value::String(String::new()),
            Some(format!(
                "MQTT topic pattern contains token '{token}', but no replacement value provided",
            )),
            command_name,
        )
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

    /// FIN: Update the docs
    /// Get the publish topic for the pattern
    ///
    /// If the pattern has a wildcard, the replacement value (`executor_id`) will be used to replace
    /// it. If the pattern is known to not have a wildcard (i.e a Telemetry topic), `None` may be
    /// passed in as the `executor_id` value
    ///
    /// Returns the publish topic on success, or an [`AIOProtocolError`] on failure
    ///
    /// # Arguments
    /// * `executor_id` - An optional string slice representing the executor ID to replace the wildcard
    ///
    /// # Errors
    /// Returns [`ConfigurationInvalid`](crate::common::aio_protocol_error::AIOProtocolErrorKind::ConfigurationInvalid) if the topic
    /// contains a wildcard and `id` is `None`, the wildcard value, or invalid
    pub fn as_publish_topic(
        &self,
        tokens: &HashMap<String, String>, // FIN: Better name
    ) -> Result<String, AIOProtocolError> {
        let mut publish_topic = String::with_capacity(self.topic_pattern.len());
        let mut last_match = 0;

        for caps in self.pattern_regex.captures_iter(&self.topic_pattern) {
            let key = caps.name("token").expect("Token should always be present");
            publish_topic.push_str(&self.topic_pattern[last_match..key.start()]);
            if let Some(val) = tokens.get(key.as_str()) {
                publish_topic.push_str(val);
            } else {
                return Err(AIOProtocolError::new_configuration_invalid_error(
                    None,
                    key.as_str(),
                    Value::String(String::new()),
                    Some(format!(
                        "MQTT topic pattern contains token '{}', but no replacement value provided",
                        key.as_str(),
                    )),
                    None,
                ));
            }
            last_match = key.end();
        }

        publish_topic.push_str(&self.topic_pattern[last_match..]);

        Ok(publish_topic)
    }

    // FIN: Update documentation
    /// Compare an MQTT topic name to the [`TopicPattern`], identifying the wildcard level in the
    /// pattern, and returning the corresponding value in the MQTT topic name.
    ///
    /// Returns value corresponding to the wildcard level in the pattern, or `None` if the topic
    /// does not match the pattern or the pattern does not contain a wildcard.
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

            tokens.insert(find.as_str().to_string(), value.to_string());
        }

        tokens
    }
}

#[cfg(test)]
mod tests {
    use test_case::test_case;

    use super::*;
    use crate::common::aio_protocol_error::AIOProtocolErrorKind;

    #[test]
    fn test_topic_pattern_new_pattern_valid() {
        let pattern = "test/{testToken}";
        let token_map = HashMap::from([("testToken".to_string(), "testRepl".to_string())]);

        TopicPattern::new(pattern, None, &token_map).unwrap();
    }

    #[test_case(""; "empty")]
    #[test_case(" "; "whitespace")]
    #[test_case("$invalidPattern"; "starts with dollar")]
    #[test_case("/invalidPattern"; "starts with slash")]
    #[test_case("invalidPattern/"; "ends with slash")]
    #[test_case("invalid//Pattern"; "contains double slash")]
    #[test_case(" /invalidPattern"; "level starts with space")]
    #[test_case("invalidPattern/ "; "level ends with space")]
    #[test_case("invalidPattern/ /invalidPattern"; "level contains space")]
    fn test_topic_pattern_new_pattern_invalid(pattern: &str) {
        let token_map = HashMap::new();

        let err = TopicPattern::new(pattern, None, &token_map).unwrap_err();
        assert_eq!(err.kind, AIOProtocolErrorKind::ConfigurationInvalid);
        assert_eq!(err.property_name, Some("pattern".to_string()));
        assert_eq!(err.property_value, Some(Value::String(pattern.to_string())));
    }

    #[test]
    fn test_command_pattern_command_name_empty() {
        let request_pattern = "test/{commandName}";
        let command_name = "";
        let mut token_map = HashMap::new();
        token_map.insert("commandName".to_string(), "".to_string());

        let err = TopicPattern::new(request_pattern, None, &token_map).unwrap_err();
        assert_eq!(err.kind, AIOProtocolErrorKind::ConfigurationInvalid);
        assert_eq!(err.property_name, Some("commandName".to_string()));
        assert_eq!(
            err.property_value,
            Some(Value::String(command_name.to_string()))
        );
    }

    #[test_case("/invalidNamespace"; "namespace starts with slash")]
    #[test_case("invalidNamespace/"; "namespace ends with slash")]
    #[test_case("invalid//Namespace"; "namespace contains double_slash")]
    #[test_case("invalid Namespace"; "namespace contains space")]
    #[test_case("invalid+Namespace"; "namespace contains plus")]
    #[test_case("invalid#Namespace"; "namespace contains hash")]
    #[test_case("invalid{Namespace"; "namespace contains open brace")]
    #[test_case("invalid}Namespace"; "namespace contains close brace")]
    #[test_case("invalidNamespace\u{0000}"; "namespace contains non ASCII character")]
    #[test_case(" "; "namespace contains only space")]
    #[test_case(""; "namespace is empty")]
    fn test_topic_processor_topic_namespace_invalid(topic_namespace: &str) {
        let request_pattern = "test";
        let mut token_map = HashMap::new();

        let err =
            TopicPattern::new(request_pattern, Some(topic_namespace), &token_map).unwrap_err();

        assert_eq!(err.kind, AIOProtocolErrorKind::ConfigurationInvalid);
        assert_eq!(err.property_name, Some("topic_namespace".to_string()));
        assert_eq!(
            err.property_value,
            Some(Value::String(topic_namespace.to_string()))
        );
    }

    #[test_case("$invalidPattern/{modelId}"; "pattern starts with dollar")]
    #[test_case("/invalidPattern/{modelId}"; "pattern starts with slash")]
    #[test_case("invalidPattern/{modelId}/"; "pattern ends with slash")]
    #[test_case("invalid//pattern/{modelId}"; "pattern contains double slash")]
    #[test_case("invalid pattern/{modelId}"; "pattern contains space")]
    #[test_case("invalid+pattern/{modelId}"; "pattern contains plus")]
    #[test_case("invalid#pattern/{modelId}"; "pattern contains hash")]
    #[test_case("invalid{pattern/{modelId}"; "pattern contains open brace")]
    #[test_case("invalid}pattern/{modelId}"; "pattern contains close brace")]
    #[test_case("invalid\u{0000}pattern/{modelId}"; "pattern contains non ASCII character")]
    #[test_case(" "; "pattern contains only space")]
    #[test_case(""; "pattern is empty")]
    fn test_topic_processor_pattern_invalid(pattern: &str) {
        let token_map = HashMap::new();
        let err = TopicPattern::new(pattern, None, &token_map).unwrap_err();
        assert_eq!(err.kind, AIOProtocolErrorKind::ConfigurationInvalid);
        assert_eq!(err.property_name, Some("pattern".to_string()));
        assert_eq!(err.property_value, Some(Value::String(pattern.to_string())));
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
    fn test_topic_processor_replacement_invalid(replacement: &str) {
        let request_pattern = "test/{modelId}";
        let mut token_map = HashMap::new();
        token_map.insert("modelId".to_string(), replacement.to_string());

        let err = TopicPattern::new(request_pattern, None, &token_map).unwrap_err();
        assert_eq!(err.kind, AIOProtocolErrorKind::ConfigurationInvalid);
        assert_eq!(err.property_name, Some("modelId".to_string()));
        assert_eq!(
            err.property_value,
            Some(Value::String(replacement.to_string()))
        );
    }

    #[test]
    fn test_topic_processor_as_subscribe_topic_valid() {
        let executor_pattern =
            "command/{commandName}/{executorId}/{invokerClientId}/{executorId}/{modelId}/{invokerClientId}";
        let mut token_map = HashMap::new();
        token_map.insert("commandName".to_string(), "testCommand".to_string());
        token_map.insert("modelId".to_string(), "testModel".to_string());
        token_map.insert("executorId".to_string(), "testExecutor".to_string());

        let executor_pattern = TopicPattern::new(executor_pattern, None, &token_map).unwrap();

        let subscribe_request_topic = executor_pattern.as_subscribe_topic();
        assert_eq!(
            subscribe_request_topic,
            format!("command/testCommand/testExecutor/+/testExecutor/testModel/+")
        );
    }

    #[test]
    fn test_topic_processor_as_publish_topic_valid() {
        let invoker_pattern = "command/{commandName}/{executorId}/{invokerClientId}/{modelId}";
        let mut token_map = HashMap::new();
        token_map.insert("commandName".to_string(), "testCommand".to_string());
        token_map.insert("modelId".to_string(), "testModel".to_string());
        token_map.insert("executorId".to_string(), "testExecutor".to_string());

        let invoker_pattern = TopicPattern::new(invoker_pattern, None, &token_map).unwrap();

        let mut publish_token_map = HashMap::new();
        publish_token_map.insert("invokerClientId".to_string(), "testInvoker".to_string());

        let publish_request_topic = invoker_pattern
            .as_publish_topic(&publish_token_map)
            .unwrap();

        assert_eq!(
            publish_request_topic,
            format!("command/testCommand/testExecutor/testInvoker/testModel")
        );
    }
}
