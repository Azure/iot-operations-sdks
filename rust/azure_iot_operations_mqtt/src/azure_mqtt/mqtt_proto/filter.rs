// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

use std::fmt::{Display, Formatter};
use std::iter::zip;

use crate::azure_mqtt::buffer_pool::{self, BytesAccumulator, Owned, Shared};
use crate::azure_mqtt::mqtt_proto::{
    ByteStr, DOLLAR_SIGN, DecodeError, EncodeError, MULTI_LEVEL_MATCH, MULTI_LEVEL_MATCH_STR,
    SEPARATOR, SHARED_SUBSCRIPTION_PREFIX, SINGLE_LEVEL_MATCH, SINGLE_LEVEL_MATCH_STR, Topic,
};

#[derive(Debug)]
pub enum ClassifiedFilter<'a> {
    Regular(&'a str),
    Dollar(&'a str),
    Shared {
        group_name: &'a str,
        filter: Filter<&'a str>,
    },
}

/// Represents an MQTT Topic Filter as described in
/// MQTT v5, section "4.7 Topic Names and Topic Filters".
#[derive(Clone, Debug, Eq, Ord, PartialEq, PartialOrd, Hash)]
pub struct Filter<S> {
    inner: S,
    kind: FilterKind,
}

#[derive(Clone, Copy, Debug, Eq, Ord, PartialEq, PartialOrd, Hash)]
pub enum FilterKind {
    Regular,
    Dollar,
    Shared { index_group_name_and_filter: usize },
}

impl<S> Filter<S>
where
    S: AsRef<str>,
{
    pub fn classify(&self) -> ClassifiedFilter<'_> {
        match self.kind {
            FilterKind::Regular => ClassifiedFilter::Regular(self.inner.as_ref()),
            FilterKind::Dollar => ClassifiedFilter::Dollar(self.inner.as_ref()),
            FilterKind::Shared {
                index_group_name_and_filter: index_group_name_and_topic,
            } => {
                // SAFETY: This is safe because the index and
                // the filter string have already been validated when
                // the filter was decoded.
                let filter = Filter {
                    inner: &self.inner.as_ref()[index_group_name_and_topic + 1..],
                    kind: FilterKind::Regular,
                };

                ClassifiedFilter::Shared {
                    group_name: &self.inner.as_ref()
                        [SHARED_SUBSCRIPTION_PREFIX.len()..index_group_name_and_topic],
                    filter,
                }
            }
        }
    }

    /// # Description
    /// Constructs a topic filter and validates it.
    ///
    /// # Errors
    /// Returns an error if the filter is invalid.
    /// See <https://docs.oasis-open.org/mqtt/mqtt/v5.0/os/mqtt-v5.0-os.html#_Toc3901241>.
    pub fn new(inner: S) -> Result<Self, DecodeError> {
        let inner_ref = inner.as_ref();

        // Ref[3.1.1]: [MQTT-4.7.3-1]
        // Ref[5.0]: [MQTT-4.7.3-1]
        if inner_ref.is_empty() {
            return Err(DecodeError::EmptyFilter);
        }

        let (kind, filter) = if inner_ref.as_bytes()[0] == DOLLAR_SIGN as u8 {
            // Ref[5.0]: [MQTT-4.8.2-1], [MQTT-4.8.2-2]
            if let Some(slice) = inner_ref.strip_prefix(SHARED_SUBSCRIPTION_PREFIX) {
                let mut index_group_name_and_topic = None;

                for (i, char) in slice.char_indices() {
                    match char {
                        SINGLE_LEVEL_MATCH | MULTI_LEVEL_MATCH => {
                            return Err(DecodeError::InvalidFilter(inner_ref.to_owned()));
                        }
                        SEPARATOR => {
                            index_group_name_and_topic = Some(i);
                            break;
                        }
                        _ => (),
                    }
                }

                // If group name is empty or if there is nothing besides group name
                // No separator or empty topic
                let index_group_name_and_topic = match index_group_name_and_topic {
                    Some(0) | None => return Err(DecodeError::InvalidFilter(inner_ref.to_owned())),
                    Some(i) => SHARED_SUBSCRIPTION_PREFIX.len() + i,
                };

                let filter = &inner_ref[index_group_name_and_topic + 1..];
                if filter.is_empty() {
                    return Err(DecodeError::EmptyFilter);
                }

                (
                    FilterKind::Shared {
                        index_group_name_and_filter: index_group_name_and_topic,
                    },
                    filter,
                )
            } else {
                (FilterKind::Dollar, inner_ref)
            }
        } else {
            (FilterKind::Regular, inner_ref)
        };

        {
            #[derive(Debug, Eq, PartialEq)]
            enum ParseState {
                Separator,
                Literal,
                Single,
                Multiple,
            }

            // Ref[3.1.1]: [MQTT-4.7.3-1]
            // Ref[5.0]: [MQTT-4.7.3-1]
            if filter.is_empty() {
                return Err(DecodeError::EmptyFilter);
            }

            let mut state = ParseState::Separator;
            // NOTE: Allowing identical match arms for clarity in
            // the transition table.
            #[expect(clippy::match_same_arms)]
            for char in filter.chars() {
                state = match (state, char) {
                    // Ref[3.1.1]: [MQTT-4.7.1-2]
                    // Ref[5.0]: [MQTT-4.7.1-1]
                    (ParseState::Multiple, _) => {
                        return Err(DecodeError::InvalidFilter(filter.to_owned()));
                    }

                    // Ref[3.1.1]: 4.7.1.1 Topic level separator
                    // Ref[5.0]: 4.7.1.1 Topic level separator
                    (_, SEPARATOR) => ParseState::Separator,

                    // Ref[3.1.1]: [MQTT-4.7.1-3]
                    // Ref[5.0]: [MQTT-4.7.1-2]
                    (ParseState::Separator, SINGLE_LEVEL_MATCH) => ParseState::Single,
                    // Ref[3.1.1]: [MQTT-4.7.1-2]
                    // Ref[5.0]: [MQTT-4.7.1-1]
                    (ParseState::Separator, MULTI_LEVEL_MATCH) => ParseState::Multiple,
                    (ParseState::Separator, _) => ParseState::Literal,

                    // Ref[3.1.1]: [MQTT-4.7.1-2], [MQTT-4.7.1-3]
                    // Ref[5.0]: [MQTT-4.7.1-1], [MQTT-4.7.1-2]
                    (ParseState::Literal, SINGLE_LEVEL_MATCH | MULTI_LEVEL_MATCH) => {
                        return Err(DecodeError::InvalidFilter(filter.to_owned()));
                    }
                    (ParseState::Literal, _) => ParseState::Literal,

                    _ => return Err(DecodeError::InvalidFilter(filter.to_owned())),
                };
            }
        }

        Ok(Self { inner, kind })
    }

    pub fn iter(&self) -> impl Iterator<Item = &str> + Clone {
        self.into_iter()
    }

    pub fn as_str(&self) -> &str {
        self.inner.as_ref()
    }

    pub fn is_wildcard(&self) -> bool {
        self.inner
            .as_ref()
            .contains([MULTI_LEVEL_MATCH, SINGLE_LEVEL_MATCH])
    }

    pub fn is_shared(&self) -> bool {
        matches!(self.kind, FilterKind::Shared { .. })
    }

    pub fn as_shared(&self) -> Filter<&str> {
        Filter {
            inner: self.inner.as_ref(),
            kind: self.kind,
        }
    }

    /// Returns true if the given `Topic` matches this `Filter`.
    pub fn matches_topic(&self, topic: &Topic<S>) -> bool {
        match self.classify() {
            ClassifiedFilter::Dollar(_) | ClassifiedFilter::Regular(_) => {
                match_levels(self.into_iter(), topic.into_iter())
            }
            ClassifiedFilter::Shared { filter, .. } => {
                match_levels(filter.into_iter(), topic.into_iter())
            }
        }
    }
}

impl<S> Filter<ByteStr<S>>
where
    S: Shared,
{
    pub fn new_shared<O>(owned: &mut O, s: impl AsRef<str>) -> Result<Self, DecodeError>
    where
        O: Owned<Shared = S>,
    {
        let s = ByteStr::new(owned, s)?;
        Filter::new(s)
    }

    pub fn decode(src: &mut S) -> Result<Option<Self>, DecodeError> {
        let Some(s) = ByteStr::decode(src)? else {
            return Ok(None);
        };
        let filter = Filter::new(s)?;
        Ok(Some(filter))
    }

    pub fn encode<B>(&self, dst: &mut B) -> Result<(), EncodeError>
    where
        B: BytesAccumulator<Shared = S>,
    {
        self.inner.encode(dst)?;
        Ok(())
    }

    pub fn to_owned(&self) -> Filter<String> {
        Filter {
            inner: self.inner.as_ref().to_owned(),
            kind: self.kind,
        }
    }

    /// Creates a copy of this `Filter` with another [`Shared`] type as the backing buffer.
    ///
    /// This is better than having the owner of `Filter<S1>` create `Filter<S2>` via `Filter::new_shared(owned, filter.as_str())?`
    /// because this is infallible, since the string was already validated when this `Filter` was constructed.
    pub fn to_shared<O2>(
        &self,
        owned: &mut O2,
    ) -> Result<Filter<ByteStr<O2::Shared>>, buffer_pool::Error>
    where
        O2: Owned,
    {
        let s = self.inner.to_shared(owned)?;
        Ok(Filter {
            inner: s,
            kind: self.kind,
        })
    }
}

impl Filter<String> {
    /// Creates a copy of this `Filter` with another [`Shared`] type as the backing buffer.
    ///
    /// This is better than having the owner of `Filter<S1>` create `Filter<S2>` via `Filter::new_shared(owned, filter.as_str())?`
    /// because this is infallible, since the string was already validated when this `Filter` was constructed.
    pub fn to_shared<O2>(
        &self,
        owned: &mut O2,
    ) -> Result<Filter<ByteStr<O2::Shared>>, buffer_pool::Error>
    where
        O2: Owned,
    {
        let s = ByteStr::new(owned, &self.inner)?;
        Ok(Filter {
            inner: s,
            kind: self.kind,
        })
    }

    pub fn encode<B, S>(&self, dst: &mut B) -> Result<(), EncodeError>
    where
        B: BytesAccumulator<Shared = S>,
        S: Shared,
    {
        let len: u16 = self.inner.len().try_into().expect("filter too long");
        dst.try_put_u16_be(len)
            .ok_or(EncodeError::InsufficientBuffer)?;
        dst.try_put_slice(self.inner.as_bytes())
            .ok_or(EncodeError::InsufficientBuffer)?;

        Ok(())
    }

    pub fn into_inner(self) -> String {
        self.inner
    }
}

impl Filter<&str> {
    pub fn to_owned(&self) -> Filter<String> {
        Filter {
            inner: self.inner.to_owned(),
            kind: self.kind,
        }
    }
}

impl<S> AsRef<str> for Filter<S>
where
    S: AsRef<str>,
{
    fn as_ref(&self) -> &str {
        self.inner.as_ref()
    }
}

impl<S> Display for Filter<S>
where
    S: AsRef<str>,
{
    fn fmt(&self, f: &mut Formatter<'_>) -> std::fmt::Result {
        write!(f, "{}", self.inner.as_ref())
    }
}

impl<'a, S> IntoIterator for &'a Filter<S>
where
    S: AsRef<str>,
{
    type Item = &'a str;
    type IntoIter = std::str::Split<'a, char>;

    fn into_iter(self) -> Self::IntoIter {
        self.inner.as_ref().split(SEPARATOR)
    }
}

impl<S> From<Filter<String>> for Filter<ByteStr<S>>
where
    S: Shared,
    for<'a> &'a str: Into<ByteStr<S>>,
{
    fn from(s: Filter<String>) -> Self {
        Self {
            inner: s.inner.as_str().into(),
            kind: s.kind,
        }
    }
}

fn match_levels<'a, I, J>(mut filter_iter: I, mut topic_iter: J) -> bool
where
    I: Iterator<Item = &'a str>,
    J: Iterator<Item = &'a str>,
{
    // Validate that all levels match according to MQTT rules
    // NOTE: We *cannot* use iter::zip() here because if the first iterator yields more
    // values than the second, we need to be able to detect that, and zip cannot support that
    // scenario.
    loop {
        match (filter_iter.next(), topic_iter.next()) {
            // Both iterators have more elements to process
            (Some(filter_level), Some(topic_level)) => {
                // First check for wildcard matches (unless using $, which cannot use wildcards)
                if !topic_level.starts_with('$') {
                    match filter_level {
                        MULTI_LEVEL_MATCH_STR => {
                            return true;
                        }
                        SINGLE_LEVEL_MATCH_STR => {
                            continue;
                        }
                        _ => {
                            // This case will no-op and be handled below
                        }
                    }
                }
                // Check for regular matches
                if filter_level != topic_level {
                    return false;
                }
            }
            // Both iterators are exhausted (i.e. a match)
            (None, None) => return true,
            // One of the iterators has more elements than the other (i.e. not a match)
            _ => return false,
        }
    }
}

#[cfg(any(test, feature = "test-utils"))]
pub fn filter(s: impl AsRef<str>) -> Filter<crate::azure_mqtt::mqtt_proto::ByteStr<bytes::Bytes>> {
    let t = crate::azure_mqtt::mqtt_proto::ByteStr::from(s.as_ref());
    Filter::new(t).unwrap()
}

#[cfg(test)]
pub fn filter_owned(s: impl AsRef<str>) -> Filter<String> {
    Filter::new(s.as_ref().to_owned()).unwrap()
}

#[cfg(test)]
pub fn filter_str<S>(s: S) -> Filter<S>
where
    S: AsRef<str>,
{
    Filter::new(s).unwrap()
}

#[cfg(test)]
mod tests {
    use test_case::test_case;

    use super::super::topic::topic_str;
    use super::{ClassifiedFilter, DecodeError, Filter, FilterKind, filter, filter_str};

    #[test_case("", DecodeError::EmptyFilter ; "empty")]
    #[test_case("foo/#baz", DecodeError::InvalidFilter("foo/#baz".to_owned()); "# in front > 1")]
    #[test_case("foo/baz#", DecodeError::InvalidFilter("foo/baz#".to_owned()); "# at end > 1")]
    #[test_case("foo/#/baz", DecodeError::InvalidFilter("foo/#/baz".to_owned()); "# in middle")]
    #[test_case("$share/mygroup", DecodeError::InvalidFilter("$share/mygroup".to_owned()); "no separator after group")]
    #[test_case("$share//mytopic", DecodeError::InvalidFilter("$share//mytopic".to_owned()); "empty share name")]
    #[test_case("$share/mygroup/", DecodeError::EmptyFilter; "empty topic")]
    #[test_case("$share/mygroup+/", DecodeError::InvalidFilter("$share/mygroup+/".to_owned()); "group name with plus character")]
    #[test_case("$share/#/", DecodeError::InvalidFilter("$share/#/".to_owned()); "group name with pound character")]
    fn invalid(filter: &str, err: DecodeError) {
        // DecodeError cannot impl PartialEq as std::io::Error cannot,
        // checking the error message instead.
        assert_eq!(
            format!("{:?}", Filter::new(filter)),
            format!("{:?}", Err::<Filter<String>, DecodeError>(err))
        );
    }

    #[test_case("a", &["a"]; "single")]
    #[test_case("foo/+/baz", &["foo", "+", "baz"]; "single level match")]
    #[test_case("foo/+/#", &["foo", "+", "#"]; "long single level match and multi level match")]
    #[test_case("#", &["#"] ; "multi level match")]
    #[test_case("+/#", &["+", "#"] ; "single level match and multi level match")]
    fn valid(filter: &str, components: &[&str]) {
        let filter = super::filter(filter);
        assert!(filter.iter().eq(components.iter().copied()));
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
        let left = filter(left);
        assert!(left.iter().eq(expected_left_comp.iter().copied()));
        let right = filter(right);
        assert!(right.iter().eq(expected_right_comp.iter().copied()));
        assert_ne!(left, right);
    }

    #[test]
    fn of_byte_str() {
        let filter = filter("/a/b/c");
        assert_eq!(format!("{filter}"), "/a/b/c");

        let mut parts = filter.iter();
        assert_eq!(parts.nth(2).unwrap(), "b");
    }

    #[test]
    fn shared_subscription_valid() {
        let filter = filter_str("$share/group1/mytopic");
        let classified_filter = filter.classify();
        match classified_filter {
            ClassifiedFilter::Shared { group_name, filter } => {
                assert_eq!(group_name, "group1");
                assert_eq!(filter.as_str(), "mytopic");
                assert_eq!(filter.kind, FilterKind::Regular);
            }
            _ => panic!("Expected filter of type: Shared"),
        }
    }

    #[test_case("$share/group1/mytopic", &FilterKind::Shared { index_group_name_and_filter: 13 })]
    #[test_case("$share/group1/m", &FilterKind::Shared { index_group_name_and_filter: 13 })]
    #[test_case("$sharetest/group1/mytopic", &FilterKind::Dollar)]
    #[test_case("$test/group1/mytopic", &FilterKind::Dollar)]
    #[test_case("share/group1/mytopic", &FilterKind::Regular)]
    #[test_case("$share/✔/foo", &FilterKind::Shared { index_group_name_and_filter: 10 }; "validation does not panic on multi-byte codepoints")]
    fn filter_kind_test(filter: &str, kind: &FilterKind) {
        let filter = filter_str(filter);

        assert_eq!(&filter.kind, kind);
    }

    #[test_case("sport", &["sport"]; "Single-level filter, no wildcards")]
    #[test_case("+", &["sport", "finance"]; "Single-level filter, single-level wildcard")]
    #[test_case("#", &["sport", "sport/tennis", "sport/tennis/player1", "sport/tennis/player1/ranking", "sport/", "sport/", "/sport/", "/", "//"]; "Single-level filter, multi-level wildcard")]
    #[test_case("$share/consumer1/sport", &["sport"]; "Single-level filter with shared subscription, no wildcards")]
    #[test_case("$share/consumer1/+", &["sport", "finance"]; "Single-level filter with shared subscription, single-level wildcard")]
    #[test_case("$share/consumer1/#", &["sport/tennis", "sport/tennis/player1", "sport/tennis/player1/ranking", "finance/bonds/banker1", "/", "//"]; "Single-level filter with shared subscription, multi-level wildcard")]
    #[test_case("$SYS", &["$SYS"]; "Single-level system filter")]
    #[test_case("sport/tennis/player1", &["sport/tennis/player1"]; "Multi-level filter, no wildcards")]
    #[test_case("sport/tennis/+", &["sport/tennis/player1", "sport/tennis/player2", "sport/tennis/"]; "Multi-level filter, one single-level wildcard")]
    #[test_case("sport/+/+", &["sport/tennis/player1", "sport/tennis/player2", "sport/badminton/player1", "sport/badminton/player2", "sport//player1"]; "Multi-level filter, multiple single-level wildcards")]
    #[test_case("sport/tennis/#", &["sport/tennis/player1", "sport/tennis/player1/ranking", "sport/tennis/player2", "sport/tennis/player2/ranking"]; "Multi-level filter, multi-level wildcard")]
    #[test_case("sport/+/#", &["sport/tennis/player1", "sport/tennis/player1/ranking", "sport/tennis/player2", "sport/tennis/player2/ranking", "sport/badminton/player1", "sport/badminton/player1/ranking", "sport/badminton/player2", "sport/badminton/player2/ranking", "sport/tennis/", "sport//"]; "Multi-level filter, single-level and multi-level wildcards")]
    #[test_case("+/+", &["sport/tennis", "/sport", "sport/", "/"]; "Multi-level filter, single-level wildcards only")]
    #[test_case("+/#", &["sport/tennis", "sport/tennis/player1", "finance/banking", "finance/banking/banker1", "/", "//"]; "Multi-level filter, single-level and multi-level wildcards only")]
    #[test_case("/finance", &["/finance"]; "Multi-level filter, zero-length level")]
    #[test_case("$share/consumer1/sport/tennis/player1", &["sport/tennis/player1"]; "Multi-level filter with shared subscription, no wildcards")]
    #[test_case("$share/consumer1/sport/+/player1", &["sport/tennis/player1", "sport/badminton/player1", "sport//player1"]; "Multi-level filter with shared subscription, one single-level wildcard")]
    #[test_case("$share/consumer1/sport/#", &["sport/tennis", "sport/tennis/player1", "sport/tennis/player1/ranking", "sport/badminton", "sport/", "sport//"]; "Multi-level filter with shared subscription, multi-level wildcard")]
    #[test_case("$share/consumer1//finance", &["/finance"]; "Multi-level filter with shared subscription, zero-length level")]
    #[test_case("$SYS/monitor/Clients", &["$SYS/monitor/Clients"]; "Multi-level system filter, no wildcards")]
    #[test_case("$SYS/monitor/+", &["$SYS/monitor/Clients", "$SYS/monitor/Connections"]; "Multi-level system filter, single-level wildcard")]
    #[test_case("$SYS/#", &["$SYS/monitor/Clients", "$SYS/monitor/Connections", "$SYS/broker/log"]; "Multi-level system filter, multi-level wildcard")]
    #[test_case("$SYS//Clients", &["$SYS//Clients"]; "Multi-level system filter, zero-length level")]
    fn filter_match(filter: &str, topics: &[&str]) {
        let filter = filter_str(filter);
        for topic in topics.iter().map(|t| topic_str(*t)) {
            assert!(filter.matches_topic(&topic));
        }
    }

    #[test_case("sport", &["finance", "sport/tennis"]; "Single-level filter, no wildcards")]
    #[test_case("+", &["/sport", "sport/", "/sport/", "/", "//", "$SYS"]; "Single-level filter, single-level wildcard")]
    #[test_case("#", &["$SYS", "$SYS/monitor/Clients"]; "Single-level filter, multi-level wildcard")]
    #[test_case("$share/consumer1/sport", &["finance/banking/banker1", "sport/tennis/player1"]; "Single-level filter with shared subscription, no wildcards")]
    #[test_case("sport/tennis/player1", &["sport/tennis/player2", "sport/tennis", "sport/tennis/player1/ranking"]; "Multi-level filter, no wildcards")]
    #[test_case("+/tennis/player1", &["sport/badminton/player1", "$SYS/tennis/player1", "sport/two/tennis/player1"]; "Multi-level filter, one single-level wildcard at beginning")]
    #[test_case("sport/tennis/+", &["sport/tennis/player1/ranking", "sport/badminton/player1", "sport/tennis"]; "Multi-level filter, one single-level wildcard at end")]
    #[test_case("sport/+/+", &["sport/tennis/player1/ranking", "finance/banking/banker1", "sport"]; "Single-level filter, multiple single-level wildcards")]
    #[test_case("sport/tennis/#", &["sport/tennis", "sport/badminton", "finance/banking/banker1"]; "Multi-level filter, multi-level wildcard")]
    #[test_case("sport/+/#", &["sport/tennis", "sport/badminton", "finance/banking/banker1"]; "Multi-level filter, single-level and multi-level wildcards")]
    #[test_case("+/+", &["/sport/tennis", "sport/tennis/", "/tennis/", "//", "$SYS/monitor"]; "Multi-level filter, single-level wildcards only")]
    #[test_case("+/#", &["sport", "$SYS/monitor", "$SYS/monitor/Clients"]; "Multi-level filter, single-level and multi-level wildcards only")]
    #[test_case("$share/consumer1/sport", &["finance/banking/banker1", "sport/tennis/player1"]; "Multi-level filter with shared subscription")]
    fn filter_mismatch(filter: &str, topics: &[&str]) {
        let filter = filter_str(filter);
        for topic in topics.iter().map(|t| topic_str(*t)) {
            assert!(!filter.matches_topic(&topic));
        }
    }
}
