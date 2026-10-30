// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

use std::{
    borrow::Borrow,
    fmt::{Display, Formatter},
};

use super::byte_str::validate_mqtt_string;
use crate::azure_mqtt::buffer_pool::{self, BytesAccumulator, Owned, Shared};
use crate::azure_mqtt::mqtt_proto::{
    ByteStr, DecodeError, EncodeError, MULTI_LEVEL_MATCH, SEPARATOR, SINGLE_LEVEL_MATCH,
};

/// Represents an MQTT Topic Name as described in
/// MQTT v5, section "4.7 Topic Names and Topic Filters".
#[derive(Clone, Debug, Eq, Hash, Ord, PartialEq, PartialOrd)]
pub struct Topic<S>(S);

impl<S> Topic<S>
where
    S: AsRef<str>,
{
    /// # Description
    /// Constructs a topic and validates it.
    ///
    /// # Errors
    /// Returns an error if the topic is invalid.
    /// See <https://docs.oasis-open.org/mqtt/mqtt/v5.0/os/mqtt-v5.0-os.html#_Toc3901241>.
    pub fn new(inner: S) -> Result<Self, DecodeError> {
        {
            let inner = inner.as_ref();

            if inner.is_empty() {
                return Err(DecodeError::EmptyTopic);
            }

            // Ref[3.1.1]: [MQTT-4.7.3-3]
            // Ref[5.0]: [MQTT-4.7.3-3]
            // NOTE: String-backed values can come from public API input, so enforce this constraint
            // here. `InvalidByteStr` is the closest existing error variant; its use on a `String` path
            // reflects the original assumption that such values were already valid.
            if inner.len() > usize::from(u16::MAX) {
                return Err(DecodeError::InvalidByteStr("longer than 65,535 bytes"));
            }

            // Ref[3.1.1]: [MQTT-4.7.3-2], 1.5.3 UTF-8 encoded strings
            // Ref[5.0]: [MQTT-4.7.3-2], 1.5.4 UTF-8 Encoded String
            // NOTE: String-backed values can come from public API input, so enforce these constraints
            // here. `InvalidByteStr` is the closest existing error variant; its use on a `String` path
            // reflects the original assumption that such values were already valid.
            validate_mqtt_string(inner.as_bytes())?;

            if inner.contains(|c| [MULTI_LEVEL_MATCH, SINGLE_LEVEL_MATCH].contains(&c)) {
                return Err(DecodeError::InvalidTopic(inner.to_owned()));
            }
        }

        Ok(Self(inner))
    }

    pub fn iter(&self) -> impl Iterator<Item = &str> + Clone {
        self.into_iter()
    }

    pub fn as_str(&self) -> &str {
        self.0.as_ref()
    }

    pub fn as_bytes(&self) -> &[u8] {
        self.0.as_ref().as_bytes()
    }

    /// This is used for dissociating from the buffer pool in case this Topic needs to be stored in long-term storage,
    /// such as for retained messages.
    pub fn to_owned(&self) -> Topic<String> {
        Topic(self.0.as_ref().to_owned())
    }
}

impl<S> Topic<ByteStr<S>>
where
    S: Shared,
{
    pub fn new_shared<O>(owned: &mut O, s: impl AsRef<str>) -> Result<Self, DecodeError>
    where
        O: Owned<Shared = S>,
    {
        let s = ByteStr::new(owned, s)?;
        Topic::new(s)
    }

    pub fn decode(src: &mut S) -> Result<Option<Self>, DecodeError> {
        let Some(s) = ByteStr::decode(src)? else {
            return Ok(None);
        };
        let topic = Topic::new(s)?;
        Ok(Some(topic))
    }

    pub fn encode<B>(&self, dst: &mut B) -> Result<(), EncodeError>
    where
        B: BytesAccumulator<Shared = S>,
    {
        self.0.encode(dst)?;
        Ok(())
    }

    pub fn into_inner(self) -> ByteStr<S> {
        self.0
    }

    /// Creates a copy of this `Topic` with another [`Shared`] type as the backing buffer.
    ///
    /// This is better than having the owner of `Topic<S1>` create `Topic<S2>` via `Topic::new_shared(owned, topic.as_str())?`
    /// because this is infallible, since the string was already validated when this `Topic` was constructed.
    pub fn to_shared<O2>(
        &self,
        owned: &mut O2,
    ) -> Result<Topic<ByteStr<O2::Shared>>, buffer_pool::Error>
    where
        O2: Owned,
    {
        let s = self.0.to_shared(owned)?;
        Ok(Topic(s))
    }
}

impl Topic<String> {
    pub fn combine<S, S2>(first: S, second: &Topic<S2>) -> Result<Self, DecodeError>
    where
        S: AsRef<str>,
        S2: AsRef<str>,
    {
        let first = first.as_ref();
        if first.contains(|c| [MULTI_LEVEL_MATCH, SINGLE_LEVEL_MATCH].contains(&c)) {
            return Err(DecodeError::InvalidTopic(first.to_owned()));
        }

        Topic::new(format!("{}{}", first, second.as_str()))
    }

    /// Creates a copy of this `Topic` with another [`Shared`] type as the backing buffer.
    ///
    /// This is better than having the owner of `Topic<S1>` create `Topic<S2>` via `Topic::new_shared(owned, topic.as_str())?`
    /// because this is infallible, since the string was already validated when this `Topic` was constructed.
    pub fn to_shared<O2>(
        &self,
        owned: &mut O2,
    ) -> Result<Topic<ByteStr<O2::Shared>>, buffer_pool::Error>
    where
        O2: Owned,
    {
        let s = ByteStr::new(owned, &self.0)?;
        Ok(Topic(s))
    }

    pub fn encode<B, S>(&self, dst: &mut B) -> Result<(), EncodeError>
    where
        B: BytesAccumulator<Shared = S>,
        S: Shared,
    {
        let len: u16 = self.0.len().try_into().expect("topic too long");
        dst.try_put_u16_be(len)
            .ok_or(EncodeError::InsufficientBuffer)?;
        dst.try_put_slice(self.0.as_bytes())
            .ok_or(EncodeError::InsufficientBuffer)?;

        Ok(())
    }

    pub fn into_inner(self) -> String {
        self.0
    }
}

impl<S> Borrow<str> for Topic<S>
where
    S: AsRef<str>,
{
    fn borrow(&self) -> &str {
        self.as_str()
    }
}

impl<S> AsRef<str> for Topic<S>
where
    S: AsRef<str>,
{
    fn as_ref(&self) -> &str {
        self.0.as_ref()
    }
}

impl<S> Display for Topic<S>
where
    S: AsRef<str>,
{
    fn fmt(&self, f: &mut Formatter<'_>) -> std::fmt::Result {
        write!(f, "{}", self.0.as_ref())
    }
}

impl<'a, S> IntoIterator for &'a Topic<S>
where
    S: AsRef<str>,
{
    type Item = &'a str;
    type IntoIter = std::str::Split<'a, char>;

    fn into_iter(self) -> Self::IntoIter {
        self.0.as_ref().split(SEPARATOR)
    }
}

impl<S> PartialEq<str> for Topic<S>
where
    S: AsRef<str>,
{
    fn eq(&self, other: &str) -> bool {
        self.as_str() == other
    }
}

impl<S> From<Topic<String>> for Topic<ByteStr<S>>
where
    S: Shared,
    for<'a> &'a str: Into<ByteStr<S>>,
{
    fn from(s: Topic<String>) -> Self {
        Self(s.0.as_str().into())
    }
}

#[cfg(any(test, feature = "test-utils"))]
pub fn topic(s: impl AsRef<str>) -> Topic<crate::azure_mqtt::mqtt_proto::ByteStr<bytes::Bytes>> {
    let t = crate::azure_mqtt::mqtt_proto::ByteStr::from(s.as_ref());
    Topic::new(t).unwrap()
}

#[cfg(test)]
pub fn topic_str<S>(s: S) -> Topic<S>
where
    S: AsRef<str>,
{
    Topic::new(s).unwrap()
}

#[cfg(test)]
mod tests {
    use matches::assert_matches;
    use test_case::test_case;

    use super::{Topic, topic};

    use crate::azure_mqtt::mqtt_proto::{ByteStr, DecodeError};

    #[test]
    fn create() {
        let topic = topic("a/b/c");
        assert!(topic.iter().eq(["a", "b", "c"]));
    }

    #[test_case("a", &["a"])]
    #[test_case("a cat", &["a cat"])]
    #[test_case("a/b/c", &["a", "b", "c"])]
    #[test_case("$a/b/c", &["$a", "b", "c"]; "system topic")]
    fn valid(topic: &str, components: &[&str]) {
        let topic = super::topic(topic);
        assert!(topic.iter().eq(components.iter().copied()));
    }

    #[test]
    fn maximum_length() {
        let topic = Topic::new("a".repeat(usize::from(u16::MAX))).unwrap();

        assert_eq!(topic.as_bytes().len(), usize::from(u16::MAX));
    }

    #[test_case("+")]
    #[test_case("a/+")]
    #[test_case("b/#")]
    fn invalid(topic: &str) {
        assert_matches!(Topic::new(ByteStr::from(topic)), Err(DecodeError::InvalidTopic(t)) if t == topic);
    }

    #[test_case("a\0b".to_owned(); "null")]
    #[test_case("a\u{1}b".to_owned(); "C0 control")]
    #[test_case("a\u{7f}b".to_owned(); "C1 control")]
    #[test_case("a\u{ffff}b".to_owned(); "non-character")]
    #[test_case("a".repeat(usize::from(u16::MAX) + 1); "overlong ascii")]
    #[test_case("é".repeat(usize::from(u16::MAX) / 2 + 1); "overlong multibyte")]
    fn invalid_mqtt_string(topic: String) {
        assert_matches!(Topic::new(topic), Err(DecodeError::InvalidByteStr(_)));
    }

    #[test_case("/b", &["", "b"], "b", &["b"])]
    #[test_case("a/", &["a", ""], "a", &["a"])]
    #[test_case("C", &["C"], "c", &["c"])]
    fn uniqueness(
        left: &str,
        expected_left_comp: &[&str],
        right: &str,
        expected_right_comp: &[&str],
    ) {
        let left = topic(left);
        assert!(left.iter().eq(expected_left_comp.iter().copied()));
        let right = topic(right);
        assert!(right.iter().eq(expected_right_comp.iter().copied()));
        assert_ne!(left, right);
    }

    #[test]
    fn of_byte_str() {
        let topic = topic("/a/b/c");
        assert_eq!(format!("{topic}"), "/a/b/c");

        let mut parts = topic.iter();
        assert_eq!(parts.nth(2).unwrap(), "b");
    }

    #[test]
    fn combine_maximum_length() {
        let second = Topic::new("b").unwrap();
        let maximum_prefix = "a".repeat(usize::from(u16::MAX) - second.as_bytes().len());
        let maximum_length = Topic::combine(&maximum_prefix, &second).unwrap();

        assert_eq!(maximum_length.as_bytes().len(), usize::from(u16::MAX));
    }

    #[test_case("a\0".to_owned(); "null")]
    #[test_case("a\u{1}".to_owned(); "C0 control")]
    #[test_case("a\u{7f}".to_owned(); "C1 control")]
    #[test_case("a\u{ffff}".to_owned(); "non-character")]
    #[test_case("a".repeat(usize::from(u16::MAX)); "overlong")]
    fn combine_invalid_mqtt_string(first: String) {
        let second = Topic::new("b").unwrap();

        assert_matches!(
            Topic::combine(first, &second),
            Err(DecodeError::InvalidByteStr(_))
        );
    }
}
