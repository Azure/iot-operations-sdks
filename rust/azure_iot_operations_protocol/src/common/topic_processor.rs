// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

use super::aio_protocol_error::{AIOProtocolError, Value};
use std::collections::HashMap;

/// Model ID token
pub const MODEL_ID: &str = "{modelId}";

/// Command name token
pub const COMMAND_NAME: &str = "{commandName}";

/// Command executor ID token
pub const COMMAND_EXECUTOR_ID: &str = "{executorId}";

/// Command invoker ID token
pub const COMMAND_INVOKER_ID: &str = "{invokerClientId}";

/// Telemetry name token
pub const TELEMETRY_NAME: &str = "{telemetryName}";

/// Telemetry sender ID token
pub const TELEMETRY_SENDER_ID: &str = "{senderId}";

/// Wildcard token
pub const WILDCARD: &str = "+";

const CUSTOM_TOKEN_START: &str = "{ex:";

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

/// Validates that a token replacement value is valid
///
/// Returns successfully if the replacement is valid, or an [`AIOProtocolError`] on failure
///
/// If the replacement value is a wildcard and the token is `COMMAND_EXECUTOR_ID`, `COMMAND_INVOKER_ID`,
/// or `TELEMETRY_SENDER_ID` then the replacement is considered valid.
///
/// # Arguments
/// * `token` - A string slice representing the token
/// * `replacement` - A string slice representing the replacement value
///
/// # Errors
/// Returns [`ConfigurationInvalid`](crate::common::aio_protocol_error::AIOProtocolErrorKind::ConfigurationInvalid) if the replacement
/// value is empty, contains invalid characters, or contains '/'
fn validate_token_replacement(
    token: &str,
    replacement: &str,
    command_name: Option<String>,
) -> Result<(), AIOProtocolError> {
    let param_name = token.trim_start_matches('{').trim_end_matches('}');

    if replacement.is_empty() {
        return Err(AIOProtocolError::new_configuration_invalid_error(
            None,
            param_name,
            Value::String(replacement.to_string()),
            Some(format!(
                "MQTT topic pattern contains token '{token}', but the replacement value provided is empty",
            )),
            command_name
        ));
    }

    if replacement == WILDCARD {
        match token {
            COMMAND_EXECUTOR_ID | COMMAND_INVOKER_ID | TELEMETRY_SENDER_ID => {
                return Ok(());
            }
            _ => {
                return Err(AIOProtocolError::new_configuration_invalid_error(
                    None,
                    param_name,
                    Value::String(replacement.to_string()),
                    Some(format!(
                        "Token '{token}' in MQTT topic pattern has replacement value '{replacement}' that is not valid",
                    )),
                    command_name
                ));
            }
        }
    }

    if contains_invalid_char(replacement) || replacement.contains('/') {
        return Err(AIOProtocolError::new_configuration_invalid_error(
            None,
            param_name,
            Value::String(replacement.to_string()),
            Some(format!(
                "Token '{token}' in MQTT topic pattern has replacement value '{replacement}' that is not valid",
            )),
            command_name
        ));
    }

    Ok(())
}

/// Validates that a custom token replacement value is valid
///
/// Returns the custom replacement value on success, or an [`AIOProtocolError`] on failure
///
/// # Arguments
/// * `pattern` - A string slice representing the MQTT topic pattern
/// * `custom_token` - A string slice representing the custom token
/// * `custom_token_map` - A reference to a hashmap containing custom token replacements
///
/// # Errors
/// Returns [`ArgumentInvalid`](crate::common::aio_protocol_error::AIOProtocolErrorKind::ArgumentInvalid) if the custom token is empty,
/// contains non-ASCII characters, the custom replacement map is empty, or contains an invalid
/// replacement value
fn try_custom_token_replacement(
    pattern: &str,
    custom_token: &str,
    custom_token_map: &HashMap<String, String>,
    command_name: Option<String>,
) -> Result<String, AIOProtocolError> {
    let custom_token = custom_token
        .trim_start_matches(CUSTOM_TOKEN_START)
        .trim_end_matches('}');
    if custom_token.trim().is_empty() {
        return Err(AIOProtocolError::new_argument_invalid_error(
            "pattern",
            Value::String(pattern.to_string()),
            Some(format!("Custom token '{custom_token}' in MQTT topic pattern is empty after '{CUSTOM_TOKEN_START}' prefix")),
            command_name
        ));
    }
    if !custom_token.is_ascii() {
        return Err(AIOProtocolError::new_argument_invalid_error(
            "pattern",
            Value::String(pattern.to_string()),
            Some(format!("Custom token '{custom_token}' in MQTT topic pattern must contain only ASCII letters after '{CUSTOM_TOKEN_START}' prefix")),
            command_name
        ));
    }
    if let Some(replacement) = custom_token_map.get(custom_token) {
        if !is_valid_replacement(replacement) {
            return Err(AIOProtocolError::new_argument_invalid_error(
                "custom_token",
                Value::String(replacement.to_string()),
                Some(format!("Custom token '{custom_token}' in MQTT topic pattern has replacement value '{replacement}' that is not valid")),
                command_name
            ));
        }
        Ok(replacement.to_string())
    } else {
        Err(AIOProtocolError::new_argument_invalid_error(
            "custom_token",
            Value::String(custom_token.to_string()),
            Some(format!("Custom token '{custom_token}' in MQTT topic pattern, but key '{custom_token}' not found in custom replacement map")),
            command_name
        ))
    }
}

/// Represents a topic pattern for Azure IoT Operations Protocol topics
#[derive(Debug)]
pub struct TopicPattern {
    /// Contains the levels
    levels: Vec<String>,
    /// Command name - used for errors
    command_name: Option<String>,
}

impl TopicPattern {
    fn new() -> Self {
        Self {
            levels: Vec::new(),
            command_name: None,
        }
    }

    /// Add a level to the topic pattern
    ///
    /// # Arguments
    /// * `level` - A string slice representing the level to add
    pub fn add(&mut self, level: &str) {
        self.levels.push(level.to_string());
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

    /// Create a new topic pattern for a command
    ///
    /// Returns a new [`TopicPattern`] on success, or an [`AIOProtocolError`] on failure
    ///
    /// # Arguments
    /// * `pattern` - A string slice representing the MQTT topic pattern
    /// * `command_name` - A string slice representing the command name
    /// * `executor_id` - A string slice representing the executor ID or the WILDCARD value if it will be supplied later
    /// * `invoker_id` - A string slice representing the invoker ID or the WILDCARD value if it will be supplied later
    /// * `model_id` - An optional string slice representing the model ID
    /// * `topic_namespace` - An optional string slice representing the topic namespace
    /// * `custom_token_map` - A reference to a hashmap containing custom token replacements
    ///
    /// # Errors
    /// Returns [`ConfigurationInvalid`](crate::common::aio_protocol_error::AIOProtocolErrorKind::ConfigurationInvalid) if the command name
    /// is empty, the executor ID and invoker ID are both wildcards, the topic namespace is invalid,
    /// the pattern is invalid, or a token replacement is invalid
    pub fn new_command_pattern(
        pattern: &str,
        command_name: &str,
        executor_id: &str,
        invoker_id: &str,
        model_id: Option<&str>,
        topic_namespace: Option<&str>,
        custom_token_map: &HashMap<String, String>,
    ) -> Result<Self, AIOProtocolError> {
        if executor_id == WILDCARD && invoker_id == WILDCARD {
            return Err(AIOProtocolError::new_configuration_invalid_error(
                None,
                "executor_id",
                Value::String(executor_id.to_string()),
                Some("Executor ID and Invoker ID cannot both be wildcards".to_string()),
                Some(command_name.to_string()),
            ));
        }

        if pattern.trim().is_empty() {
            return Err(AIOProtocolError::new_configuration_invalid_error(
                None,
                "pattern",
                Value::String(pattern.to_string()),
                Some("MQTT topic pattern must not be empty".to_string()),
                Some(command_name.to_string()),
            ));
        }
        if pattern.starts_with('$') {
            return Err(AIOProtocolError::new_configuration_invalid_error(
                None,
                "pattern",
                Value::String(pattern.to_string()),
                Some("MQTT topic pattern starts with reserved character '$'".to_string()),
                Some(command_name.to_string()),
            ));
        }
        let mut topic_pattern = Self::new();
        if let Some(topic_namespace) = topic_namespace {
            if !is_valid_replacement(topic_namespace) {
                return Err(AIOProtocolError::new_configuration_invalid_error(
                    None,
                    "topic_namespace",
                    Value::String(topic_namespace.to_string()),
                    None,
                    Some(command_name.to_string()),
                ));
            }
            topic_pattern.add(topic_namespace);
        }

        let pattern_split = pattern.split('/');
        for pattern_level in pattern_split {
            if pattern_level.trim().is_empty() {
                return Err(AIOProtocolError::new_configuration_invalid_error(
                    None,
                    "pattern",
                    Value::String(pattern.to_string()),
                    Some("MQTT topic pattern contains empty level".to_string()),
                    Some(command_name.to_string()),
                ));
            }
            // Check if it is a token
            if pattern_level.starts_with('{') && pattern_level.ends_with('}') {
                // Check if it is a custom token
                if pattern_level.starts_with(CUSTOM_TOKEN_START) {
                    topic_pattern.add(&try_custom_token_replacement(
                        pattern,
                        pattern_level,
                        custom_token_map,
                        Some(command_name.to_string()),
                    )?);
                } else {
                    match pattern_level {
                        MODEL_ID => {
                            if let Some(model_id) = model_id {
                                validate_token_replacement(
                                    pattern_level,
                                    model_id,
                                    Some(command_name.to_string()),
                                )?;
                                topic_pattern.add(model_id);
                            } else {
                                return Err(Self::no_replacement_error(
                                    MODEL_ID,
                                    Some(command_name.to_string()),
                                ));
                            }
                        }
                        COMMAND_NAME => {
                            validate_token_replacement(
                                pattern_level,
                                command_name,
                                Some(command_name.to_string()),
                            )?;
                            topic_pattern.add(command_name);
                        }
                        COMMAND_EXECUTOR_ID => {
                            validate_token_replacement(
                                pattern_level,
                                executor_id,
                                Some(command_name.to_string()),
                            )?;
                            topic_pattern.add(executor_id);
                        }
                        COMMAND_INVOKER_ID => {
                            validate_token_replacement(
                                pattern_level,
                                invoker_id,
                                Some(command_name.to_string()),
                            )?;
                            topic_pattern.add(invoker_id);
                        }
                        _ => {
                            return Err(AIOProtocolError::new_configuration_invalid_error(
                                None,
                                "pattern_level",
                                Value::String(pattern_level.to_string()),
                                Some(format!(
                                    "Command pattern token {pattern_level} not recognized"
                                )),
                                Some(command_name.to_string()),
                            ))
                        }
                    }
                }
            } else {
                if contains_invalid_char(pattern_level) {
                    return Err(AIOProtocolError::new_configuration_invalid_error(
                        None,
                        "pattern",
                        Value::String(pattern.to_string()),
                        Some(format!("Level {pattern_level} in MQTT topic pattern contains invalid characters")),
                        Some(command_name.to_string())
                    ));
                }
                topic_pattern.add(pattern_level);
            }
        }
        topic_pattern.command_name = Some(command_name.to_string());

        Ok(topic_pattern)
    }

    /// Create a new topic pattern for telemetry
    ///
    /// Returns a new [`TopicPattern`] on success, or an [`AIOProtocolError`] on failure
    ///
    /// # Arguments
    /// * `pattern` - A string slice representing the MQTT topic pattern
    /// * `sender_id` - A string slice representing the sender ID
    /// * `telemetry_name` - An optional string slice representing the telemetry name
    /// * `model_id` - An optional string slice representing the model ID
    /// * `topic_namespace` - An optional string slice representing the topic namespace
    /// * `custom_token_map` - A reference to a hashmap containing custom token replacements
    ///
    /// # Errors
    /// Returns [`ConfigurationInvalid`](crate::common::aio_protocol_error::AIOProtocolErrorKind::ConfigurationInvalid) if the pattern is
    /// invalid, the topic namespace is invalid, or a token replacement is invalid.
    pub fn new_telemetry_pattern(
        pattern: &str,
        sender_id: &str,
        telemetry_name: Option<&str>,
        model_id: Option<&str>,
        topic_namespace: Option<&str>,
        custom_token_map: &HashMap<String, String>,
    ) -> Result<Self, AIOProtocolError> {
        if pattern.trim().is_empty() {
            return Err(AIOProtocolError::new_configuration_invalid_error(
                None,
                "pattern",
                Value::String(pattern.to_string()),
                Some("MQTT topic pattern must not be empty".to_string()),
                None,
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
        let mut topic_pattern = Self::new();
        if let Some(topic_namespace) = topic_namespace {
            if !is_valid_replacement(topic_namespace) {
                return Err(AIOProtocolError::new_configuration_invalid_error(
                    None,
                    "topic_namespace",
                    Value::String(topic_namespace.to_string()),
                    None,
                    None,
                ));
            }
            topic_pattern.add(topic_namespace);
        }

        let pattern_split = pattern.split('/');
        for pattern_level in pattern_split {
            if pattern_level.trim().is_empty() {
                return Err(AIOProtocolError::new_configuration_invalid_error(
                    None,
                    "pattern",
                    Value::String(pattern.to_string()),
                    Some("MQTT topic pattern contains empty level".to_string()),
                    None,
                ));
            }
            // Check if it is a token
            if pattern_level.starts_with('{') && pattern_level.ends_with('}') {
                // Check if it is a custom token
                if pattern_level.starts_with(CUSTOM_TOKEN_START) {
                    topic_pattern.add(&try_custom_token_replacement(
                        pattern,
                        pattern_level,
                        custom_token_map,
                        None,
                    )?);
                } else {
                    match pattern_level {
                        MODEL_ID => {
                            if let Some(model_id) = model_id {
                                validate_token_replacement(pattern_level, model_id, None)?;
                                topic_pattern.add(model_id);
                            } else {
                                return Err(Self::no_replacement_error(MODEL_ID, None));
                            }
                        }
                        TELEMETRY_NAME => {
                            if let Some(telemetry_name) = telemetry_name {
                                validate_token_replacement(pattern_level, telemetry_name, None)?;
                                topic_pattern.add(telemetry_name);
                            } else {
                                return Err(Self::no_replacement_error(TELEMETRY_NAME, None));
                            }
                        }
                        TELEMETRY_SENDER_ID => {
                            validate_token_replacement(pattern_level, sender_id, None)?;
                            topic_pattern.add(sender_id);
                        }
                        _ => {
                            return Err(AIOProtocolError::new_configuration_invalid_error(
                                None,
                                "pattern_level",
                                Value::String(pattern_level.to_string()),
                                Some(format!(
                                    "Telemetry pattern token {pattern_level} not recognized"
                                )),
                                None,
                            ))
                        }
                    }
                }
            } else {
                if contains_invalid_char(pattern_level) {
                    return Err(AIOProtocolError::new_configuration_invalid_error(
                        None,
                        "pattern",
                        Value::String(pattern.to_string()),
                        Some(format!("Level {pattern_level} in MQTT topic pattern contains invalid characters")),
                        None
                    ));
                }
                topic_pattern.add(pattern_level);
            }
        }

        Ok(topic_pattern)
    }

    /// Get the subscribe topic for the pattern
    ///
    /// Returns the subscribe topic for the pattern
    #[must_use]
    pub fn as_subscribe_topic(&self) -> String {
        self.levels.join("/")
    }

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
    pub fn as_publish_topic(&self, executor_id: Option<&str>) -> Result<String, AIOProtocolError> {
        // Executor ID is the only token that can be replaced
        if self.levels.contains(&WILDCARD.to_string()) {
            let param_name = COMMAND_EXECUTOR_ID
                .trim_start_matches('{')
                .trim_end_matches('}');
            if let Some(id) = executor_id {
                if id == WILDCARD {
                    return Err(AIOProtocolError::new_configuration_invalid_error(
                    None,
                    param_name,
                    Value::String(id.to_string()),
                    Some(format!("Token '{COMMAND_EXECUTOR_ID}' in MQTT topic pattern has replacement value '{id}' that is not valid"),
                    ),
                    self.command_name.clone()
                ));
                }
                validate_token_replacement(COMMAND_EXECUTOR_ID, id, self.command_name.clone())?;

                let mut result = String::new();
                for (i, level) in self.levels.iter().enumerate() {
                    if level == WILDCARD {
                        result.push_str(id);
                    } else {
                        result.push_str(level);
                    }
                    if i < self.levels.len() - 1 {
                        result.push('/');
                    }
                }

                Ok(result)
            } else {
                Err(AIOProtocolError::new_configuration_invalid_error(
                None,
                param_name,
                Value::String(String::new()),
                Some(format!("MQTT topic pattern contains token '{COMMAND_EXECUTOR_ID}', but no replacement value provided"),
                ),
                self.command_name.clone()
            ))
            }
        } else {
            Ok(self.levels.join("/"))
        }
    }

    /// Compare an MQTT topic name to the [`TopicPattern`], identifying the wildcard level in the
    /// pattern, and returning the corresponding value in the MQTT topic name.
    ///
    /// Returns value corresponding to the wildcard level in the pattern, or `None` if the topic
    /// does not match the pattern or the pattern does not contain a wildcard.
    #[must_use]
    pub fn parse_wildcard(&self, topic: &str) -> Option<String> {
        let mut topic_iter = topic.split('/');

        for pattern_level in &self.levels {
            match topic_iter.next() {
                Some(topic_level) => {
                    if pattern_level == WILDCARD {
                        return Some(topic_level.to_string());
                    }
                }
                None => return None,
            }
        }

        None
    }

    // TODO: Remove this function. It's functionality is covered by crate::mqtt::topic, keeping for testing purposes for now
    /// Check if a topic string matches the pattern
    ///
    /// This is a simple implementation that checks against a topic filter with a single wildcard.
    /// If support for more complex filters is needed, a more advanced implementation will be
    /// required
    ///
    /// Returns true if the topic matches the pattern, otherwise returns false
    ///
    /// # Arguments
    /// * `topic` - A string slice representing the topic to check
    #[must_use]
    pub fn is_match(&self, topic: &str) -> bool {
        let recv_levels = topic.split('/').collect::<Vec<&str>>();

        if recv_levels.len() != self.levels.len() {
            return false;
        }

        for (pattern_level, recv_level) in self.levels.iter().zip(recv_levels.iter()) {
            if pattern_level == WILDCARD {
                continue;
            }
            if pattern_level != recv_level {
                return false;
            }
        }

        true
    }
}

#[cfg(test)]
mod tests {
    use test_case::test_case;

    use super::*;
    use crate::common::aio_protocol_error::AIOProtocolErrorKind;

    const TEST_COMMAND_NAME: &str = "testCommand";
    const TEST_EXECUTOR_ID: &str = "testExecutor";
    const TEST_INVOKER_ID: &str = "testInvoker";
    const TEST_MODEL_ID: &str = "testModel";
    const TEST_SENDER_ID: &str = "testSender";
    const TEST_TELEMETRY_NAME: &str = "testTelemetry";

    #[test]
    fn test_command_pattern_command_name_empty() {
        let request_pattern = "test/{commandName}";
        let command_name = "";
        let custom_token_map = HashMap::new();

        let err = TopicPattern::new_command_pattern(
            request_pattern,
            command_name,
            TEST_EXECUTOR_ID,
            WILDCARD,
            None,
            None,
            &custom_token_map,
        )
        .unwrap_err();
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
        let custom_token_map = HashMap::new();

        let err = TopicPattern::new_command_pattern(
            request_pattern,
            TEST_COMMAND_NAME,
            TEST_EXECUTOR_ID,
            WILDCARD,
            None,
            Some(topic_namespace),
            &custom_token_map,
        )
        .unwrap_err();

        assert_eq!(err.kind, AIOProtocolErrorKind::ConfigurationInvalid);
        assert_eq!(err.property_name, Some("topic_namespace".to_string()));
        assert_eq!(
            err.property_value,
            Some(Value::String(topic_namespace.to_string()))
        );

        let err = TopicPattern::new_telemetry_pattern(
            request_pattern,
            TEST_SENDER_ID,
            None,
            None,
            Some(topic_namespace),
            &custom_token_map,
        )
        .unwrap_err();

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
        let custom_token_map = HashMap::new();
        let err = TopicPattern::new_command_pattern(
            pattern,
            TEST_COMMAND_NAME,
            TEST_EXECUTOR_ID,
            WILDCARD,
            Some(TEST_MODEL_ID),
            None,
            &custom_token_map,
        )
        .unwrap_err();
        assert_eq!(err.kind, AIOProtocolErrorKind::ConfigurationInvalid);
        assert_eq!(err.property_name, Some("pattern".to_string()));
        assert_eq!(err.property_value, Some(Value::String(pattern.to_string())));

        let err = TopicPattern::new_telemetry_pattern(
            pattern,
            TEST_SENDER_ID,
            None,
            Some(TEST_MODEL_ID),
            None,
            &custom_token_map,
        )
        .unwrap_err();
        assert_eq!(err.kind, AIOProtocolErrorKind::ConfigurationInvalid);
        assert_eq!(err.property_name, Some("pattern".to_string()));
        assert_eq!(err.property_value, Some(Value::String(pattern.to_string())));
    }

    #[test_case("test/noWildcard", "id", "test/noWildcard", true; "prefix match")]
    #[test_case("test/noWildcard", "id", "test/noWildcards", false; "prefix no match same level length")]
    #[test_case("test/noWildcard", "id", "test/noWildcard/other", false; "prefix no match different level length")]
    #[test_case("{executorId}", "id", "id", true; "id match")]
    #[test_case("{executorId}", "id", "other", false; "id no match")]
    #[test_case("{executorId}", "+", "id", true; "wildcard match")]
    #[test_case("test/{executorId}", "id", "test/id", true; "prefix match id match")]
    #[test_case("test/{executorId}", "id", "tests/id", false; "prefix no match same level length id match")]
    #[test_case("test/{executorId}", "id", "tests/other/id", false; "prefix no match different level length id match")]
    #[test_case("test/{executorId}", "+", "test/id", true; "prefix match wildcard match")]
    #[test_case("test/{executorId}", "+", "tests/id", false; "prefix no match same level length wildcard match")]
    #[test_case("test/{executorId}", "+", "tests/other/id", false; "prefix no match different level length wildcard match")]
    #[test_case("test/{executorId}", "id", "test/other", false; "prefix match id no match")]
    #[test_case("{executorId}/test", "id", "id/test", true; "id match suffix match")]
    #[test_case("{executorId}/test", "id", "other/test", false; "id no match suffix match")]
    #[test_case("{executorId}/test", "id", "id/tests", false; "id match suffix no match same level length")]
    #[test_case("{executorId}/test", "id", "id/test/other", false; "id match suffix no match different level length")]
    #[test_case("{executorId}/test", "+", "id/test", true; "wildcard match suffix match")]
    #[test_case("{executorId}/test", "+", "id/tests", false; "wildcard match suffix no match same level length")]
    #[test_case("{executorId}/test", "+", "id/test/other", false; "wildcard match suffix no match different level length")]
    #[test_case("test/{executorId}/test", "id", "test/id/test", true; "prefix match id match suffix match")]
    #[test_case("test/{executorId}/test", "id", "tests/id/test", false; "prefix no match same level length id match suffix match")]
    #[test_case("test/{executorId}/test", "id", "test/other/id/test", false; "prefix no match different level length id match suffix match")]
    #[test_case("test/{executorId}/test", "id", "test/id/tests", false; "prefix match id match suffix no match same level length")]
    #[test_case("test/{executorId}/test", "id", "test/id/test/other", false; "prefix match id match suffix no match different level length")]
    #[test_case("test/{executorId}/test", "+", "test/id/test", true; "prefix match wildcard match suffix match")]
    #[test_case("test/{executorId}/test", "+", "tests/id/test", false; "prefix no match same level length wildcard match suffix match")]
    #[test_case("test/{executorId}/test", "+", "test/other/id/test", false; "prefix no match different level length wildcard match suffix match")]
    #[test_case("test/{executorId}/test", "+", "test/id/tests", false; "prefix match wildcard match suffix no match same level length")]
    #[test_case("test/{executorId}/test", "+", "test/id/test/other", false; "prefix match wildcard match suffix no match different level length")]
    #[test_case("{executorId}", "id", "test/id/other", false; "prefix nonexistent id match suffix nonexistent")]
    #[test_case("test/{executorId}", "+", "test/id/other", false; "prefix match wildcard match suffix nonexistent")]
    #[test_case("{executorId}/test", "+", "test/id/other", false; "wildcard match suffix no match prefix nonexistent")]
    #[test_case("{executorId}/test/{executorId}", "+", "id/test/id", true; "prefix match wildcard suffix match wildcard")]
    #[test_case("{executorId}/test/{executorId}", "+", "id/test/other/id", false; "prefix match wildcard suffix match wildcard no match")]
    fn test_topic_processor_match(
        pattern: &str,
        id_or_wildcard: &str,
        test_match: &str,
        expected_match: bool,
    ) {
        // To avoid the double wildcard check
        let invoker_id = if id_or_wildcard == WILDCARD {
            "id"
        } else {
            WILDCARD
        };
        let custom_token_map = HashMap::new();

        let command_pattern = TopicPattern::new_command_pattern(
            pattern,
            TEST_COMMAND_NAME,
            id_or_wildcard,
            invoker_id,
            None,
            None,
            &custom_token_map,
        )
        .unwrap();

        assert_eq!(command_pattern.is_match(test_match), expected_match);
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
        let custom_token_map = HashMap::new();

        let err = TopicPattern::new_command_pattern(
            request_pattern,
            TEST_COMMAND_NAME,
            TEST_EXECUTOR_ID,
            WILDCARD,
            Some(replacement),
            None,
            &custom_token_map,
        )
        .unwrap_err();
        assert_eq!(err.kind, AIOProtocolErrorKind::ConfigurationInvalid);
        assert_eq!(err.property_name, Some("modelId".to_string()));
        assert_eq!(
            err.property_value,
            Some(Value::String(replacement.to_string()))
        );

        let err = TopicPattern::new_telemetry_pattern(
            request_pattern,
            TEST_SENDER_ID,
            None,
            Some(replacement),
            None,
            &custom_token_map,
        )
        .unwrap_err();
        assert_eq!(err.kind, AIOProtocolErrorKind::ConfigurationInvalid);
        assert_eq!(err.property_name, Some("modelId".to_string()));
        assert_eq!(
            err.property_value,
            Some(Value::String(replacement.to_string()))
        );
    }

    #[test]
    fn test_topic_processor_executor_pattern_valid() {
        let executor_pattern =
            "command/{commandName}/{executorId}/{invokerClientId}/{executorId}/{modelId}/{invokerClientId}";
        let custom_token_map = HashMap::new();

        let executor_pattern = TopicPattern::new_command_pattern(
            executor_pattern,
            TEST_COMMAND_NAME,
            TEST_EXECUTOR_ID,
            WILDCARD,
            Some(TEST_MODEL_ID),
            None,
            &custom_token_map,
        )
        .unwrap();

        let expected_match = format!(
            "command/{TEST_COMMAND_NAME}/{TEST_EXECUTOR_ID}/{TEST_INVOKER_ID}/{TEST_EXECUTOR_ID}/{TEST_MODEL_ID}/{TEST_INVOKER_ID}"
        );
        assert!(executor_pattern.is_match(&expected_match));

        let expected_no_match =
            format!("command/{TEST_COMMAND_NAME}/otherExecutor/{TEST_INVOKER_ID}/otherExecutor/{TEST_MODEL_ID}/{TEST_INVOKER_ID}");
        assert!(!executor_pattern.is_match(&expected_no_match));

        let subscribe_request_topic = executor_pattern.as_subscribe_topic();
        assert_eq!(
            subscribe_request_topic,
            format!(
                "command/{TEST_COMMAND_NAME}/{TEST_EXECUTOR_ID}/+/{TEST_EXECUTOR_ID}/testModel/+"
            )
        );
    }

    #[test]
    fn test_topic_processor_invoker_pattern_valid() {
        let invoker_pattern = "command/{commandName}/{executorId}/{invokerClientId}/{modelId}";
        let executor_response_pattern =
            "command/{commandName}/{executorId}/{invokerClientId}/{modelId}/response";
        let custom_token_map = HashMap::new();

        let invoker_pattern = TopicPattern::new_command_pattern(
            invoker_pattern,
            TEST_COMMAND_NAME,
            WILDCARD,
            TEST_INVOKER_ID,
            Some(TEST_MODEL_ID),
            None,
            &custom_token_map,
        )
        .unwrap();

        let executor_response_pattern = TopicPattern::new_command_pattern(
            executor_response_pattern,
            TEST_COMMAND_NAME,
            WILDCARD,
            TEST_INVOKER_ID,
            Some(TEST_MODEL_ID),
            None,
            &custom_token_map,
        )
        .unwrap();

        let publish_request_topic = invoker_pattern
            .as_publish_topic(Some(TEST_EXECUTOR_ID))
            .unwrap();
        assert_eq!(
            publish_request_topic,
            format!(
                "command/{TEST_COMMAND_NAME}/{TEST_EXECUTOR_ID}/{TEST_INVOKER_ID}/{TEST_MODEL_ID}"
            )
        );

        let expected_match = format!("command/{TEST_COMMAND_NAME}/{TEST_EXECUTOR_ID}/{TEST_INVOKER_ID}/{TEST_MODEL_ID}/response");
        assert!(executor_response_pattern.is_match(&expected_match));

        let expected_no_match = format!(
            "command/{TEST_COMMAND_NAME}/{TEST_EXECUTOR_ID}/otherInvoker/{TEST_MODEL_ID}/response"
        );
        assert!(!executor_response_pattern.is_match(&expected_no_match));

        let subscribe_response_topic = executor_response_pattern.as_subscribe_topic();
        assert_eq!(
            subscribe_response_topic,
            format!(
                "command/{TEST_COMMAND_NAME}/{WILDCARD}/{TEST_INVOKER_ID}/{TEST_MODEL_ID}/response"
            )
        );

        let publish_response_topic = executor_response_pattern
            .as_publish_topic(Some(TEST_EXECUTOR_ID))
            .unwrap();
        assert_eq!(
            publish_response_topic,
            format!("command/{TEST_COMMAND_NAME}/{TEST_EXECUTOR_ID}/{TEST_INVOKER_ID}/{TEST_MODEL_ID}/response")
        );
    }

    #[test]
    fn test_topic_processor_sender_pattern_valid() {
        let telemetry_pattern = "test/telemetry/{telemetryName}/{senderId}/{modelId}";
        let custom_token_map = HashMap::new();

        let telemetry_topic_pattern = TopicPattern::new_telemetry_pattern(
            telemetry_pattern,
            TEST_SENDER_ID,
            Some(TEST_TELEMETRY_NAME),
            Some(TEST_MODEL_ID),
            None,
            &custom_token_map,
        )
        .unwrap();

        let telemetry_publish_topic = telemetry_topic_pattern.as_publish_topic(None).unwrap();
        assert_eq!(
            telemetry_publish_topic,
            format!("test/telemetry/{TEST_TELEMETRY_NAME}/{TEST_SENDER_ID}/{TEST_MODEL_ID}")
        );
    }

    #[test]
    fn test_topic_processor_receiver_pattern_valid() {
        let telemetry_pattern = "test/telemetry/{telemetryName}/{senderId}/{modelId}";
        let custom_token_map = HashMap::new();

        let telemetry_topic_pattern = TopicPattern::new_telemetry_pattern(
            telemetry_pattern,
            WILDCARD,
            Some(TEST_TELEMETRY_NAME),
            Some(TEST_MODEL_ID),
            None,
            &custom_token_map,
        )
        .unwrap();

        let telemetry_subscribe_topic = telemetry_topic_pattern.as_subscribe_topic();
        assert_eq!(
            telemetry_subscribe_topic,
            format!("test/telemetry/{TEST_TELEMETRY_NAME}/+/{TEST_MODEL_ID}")
        );

        let telemetry_publish_topic =
            format!("test/telemetry/{TEST_TELEMETRY_NAME}/{TEST_SENDER_ID}/{TEST_MODEL_ID}");
        let extracted_sender_id = telemetry_topic_pattern.parse_wildcard(&telemetry_publish_topic);
        assert_eq!(extracted_sender_id, Some(TEST_SENDER_ID.to_string()));
    }

    #[test]
    fn test_topic_processor_custom_token_pattern_valid() {
        let telemetry_pattern = "test/{telemetryName}/{ex:customToken}/{modelId}";
        let custom_token_map = [("customToken".to_string(), "testCustom".to_string())]
            .iter()
            .cloned()
            .collect();

        let telemetry_topic_pattern = TopicPattern::new_telemetry_pattern(
            telemetry_pattern,
            TEST_SENDER_ID,
            Some(TEST_TELEMETRY_NAME),
            Some(TEST_MODEL_ID),
            None,
            &custom_token_map,
        )
        .unwrap();

        let telemetry_subscribe_topic = telemetry_topic_pattern.as_subscribe_topic();
        assert_eq!(
            telemetry_subscribe_topic,
            format!("test/{TEST_TELEMETRY_NAME}/testCustom/{TEST_MODEL_ID}")
        );

        let command_pattern = "test/{commandName}/{ex:customToken}/{modelId}";
        let custom_token_map = [("customToken".to_string(), "testCustom".to_string())]
            .iter()
            .cloned()
            .collect();

        let command_topic_pattern = TopicPattern::new_command_pattern(
            command_pattern,
            TEST_COMMAND_NAME,
            TEST_EXECUTOR_ID,
            WILDCARD,
            Some(TEST_MODEL_ID),
            None,
            &custom_token_map,
        )
        .unwrap();

        let command_subscribe_topic = command_topic_pattern.as_subscribe_topic();
        assert_eq!(
            command_subscribe_topic,
            format!("test/{TEST_COMMAND_NAME}/testCustom/{TEST_MODEL_ID}")
        );
    }
}
