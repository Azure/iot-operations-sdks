// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

//! MQTT Topic Name and Topic Filter types

use std::fmt;

use thiserror::Error;

use crate::azure_mqtt::mqtt_proto;

// Implementation note:
// - Use wrapped `mqtt_proto` types to avoid validation duplication.
// - Use `String` as the backing store for parity with other user-facing conversions and flexibility
//   when converting to an arbitrary buffer at lower levels

/// Error type for validating topics.
#[derive(Debug, Error)]
#[error(transparent)]
pub struct TopicError(#[from] mqtt_proto::DecodeError);

/// MQTT Topic Name as described in MQTT v5, section "4.7 Topic Names and Topic Filters".
#[derive(Debug, Clone, Eq, PartialEq, Hash)]
pub struct TopicName(mqtt_proto::Topic<String>);

impl TopicName {
    /// Constructs a new `TopicName` after validating the input string.
    ///
    /// # Errors
    /// Returns an error if the topic is invalid.
    /// See <https://docs.oasis-open.org/mqtt/mqtt/v5.0/os/mqtt-v5.0-os.html#_Toc3901241>.
    pub fn new<S>(s: S) -> Result<Self, TopicError>
    where
        S: AsRef<str>,
    {
        let inner = mqtt_proto::Topic::new(s.as_ref().to_owned())?;
        Ok(TopicName(inner))
    }

    /// Returns the topic name as a string slice.
    pub fn as_str(&self) -> &str {
        self.0.as_str()
    }

    /// Returns true if the given `TopicFilter` matches this `TopicName`.
    pub fn matches_topic_filter(&self, filter: &TopicFilter) -> bool {
        filter.0.matches_topic(&self.0)
    }

    /// Returns the inner `mqtt_proto::Topic<String>`.
    #[allow(dead_code)]
    pub(crate) fn into_inner(self) -> mqtt_proto::Topic<String> {
        self.0
    }
}

impl fmt::Display for TopicName {
    fn fmt(&self, f: &mut fmt::Formatter<'_>) -> fmt::Result {
        write!(f, "{}", self.0.as_str())
    }
}

impl From<mqtt_proto::Topic<String>> for TopicName {
    fn from(value: mqtt_proto::Topic<String>) -> Self {
        TopicName(value)
    }
}

/// MQTT Topic Filter as described in MQTT v5, section "4.7 Topic Names and Topic Filters".
#[derive(Debug, Clone, Eq, PartialEq, Hash)]
pub struct TopicFilter(mqtt_proto::Filter<String>);

impl TopicFilter {
    /// Constructs a new `TopicFilter` after validating the input string.
    ///
    /// # Errors
    /// Returns an error if the topic filter is invalid.
    /// See <https://docs.oasis-open.org/mqtt/mqtt/v5.0/os/mqtt-v5.0-os.html#_Toc3901241>.
    pub fn new<S>(s: S) -> Result<Self, TopicError>
    where
        S: AsRef<str>,
    {
        let inner = mqtt_proto::Filter::new(s.as_ref().to_owned())?;
        Ok(TopicFilter(inner))
    }

    /// Returns the topic filter as a string slice.
    pub fn as_str(&self) -> &str {
        self.0.as_str()
    }

    /// Returns true if the given `TopicName` matches this `TopicFilter`.
    pub fn matches_topic_name(&self, topic: &TopicName) -> bool {
        self.0.matches_topic(&topic.0)
    }

    /// Returns the inner `mqtt_proto::Filter<String>`.
    #[allow(dead_code)]
    pub(crate) fn into_inner(self) -> mqtt_proto::Filter<String> {
        self.0
    }
}

impl From<mqtt_proto::Filter<String>> for TopicFilter {
    fn from(value: mqtt_proto::Filter<String>) -> Self {
        TopicFilter(value)
    }
}

impl fmt::Display for TopicFilter {
    fn fmt(&self, f: &mut fmt::Formatter<'_>) -> fmt::Result {
        write!(f, "{}", self.0.as_str())
    }
}

#[cfg(test)]
#[allow(clippy::similar_names)] // clippy doesn't like tn/tf variables
mod tests {
    use bytes::Bytes;

    use super::{TopicFilter, TopicName};
    use crate::azure_mqtt::mqtt_proto;

    #[test]
    fn convert_to_buffered_proto_types() {
        let tn = TopicName::new("a/b/c").unwrap();
        let tf = TopicFilter::new("a/b/+").unwrap();

        let tn_inner = tn.clone().into_inner(); // clone to retain original for assert
        let tn_buffered = mqtt_proto::Topic::<mqtt_proto::ByteStr<Bytes>>::from(tn_inner);
        assert_eq!(tn.as_str(), tn_buffered.as_str());

        let tf_inner = tf.clone().into_inner(); // clone to retain original for assert
        let tf_buffered = mqtt_proto::Filter::<mqtt_proto::ByteStr<Bytes>>::from(tf_inner);
        assert_eq!(tf.as_str(), tf_buffered.as_str());
    }

    #[test]
    fn convert_from_buffered_proto_types() {
        let tn_buffered =
            mqtt_proto::Topic::new(mqtt_proto::ByteStr::<Bytes>::from("a/b/c")).unwrap();
        let tf_buffered =
            mqtt_proto::Filter::new(mqtt_proto::ByteStr::<Bytes>::from("a/b/+")).unwrap();

        let tn = TopicName::from(tn_buffered.to_owned());
        let tf = TopicFilter::from(tf_buffered.to_owned());

        assert_eq!(tn.as_str(), tn_buffered.as_str());
        assert_eq!(tf.as_str(), tf_buffered.as_str());
    }
}
