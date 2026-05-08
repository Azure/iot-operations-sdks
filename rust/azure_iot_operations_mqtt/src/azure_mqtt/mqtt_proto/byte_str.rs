// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

use unicode_properties::{GeneralCategory, UnicodeGeneralCategory as _};

use crate::azure_mqtt::buffer_pool::{self, BytesAccumulator, Owned, Shared};
use crate::azure_mqtt::mqtt_proto::{BinaryData, DecodeError, EncodeError};

/// Strings are prefixed with a two-byte big-endian length and are encoded as utf-8.
///
/// Ref: 1.5.3 UTF-8 encoded strings
#[derive(Clone)]
pub struct ByteStr<S>(BinaryData<S>)
where
    S: Shared;

impl<S> ByteStr<S>
where
    S: Shared,
{
    pub const EMPTY: &'static [u8] = BinaryData::<S>::EMPTY;

    pub fn new<O>(owned: &mut O, s: impl AsRef<str>) -> Result<Self, buffer_pool::Error>
    where
        O: Owned<Shared = S>,
    {
        Ok(Self(BinaryData::new(owned, s.as_ref().as_bytes())?))
    }

    pub fn len(&self) -> usize {
        self.0.len()
    }

    pub fn is_empty(&self) -> bool {
        self.0.is_empty()
    }

    pub fn into_binary_data(self) -> BinaryData<S> {
        self.0
    }

    /// This function decodes a `ByteStr` out from the given `Shared`. It is meant to be used from a streaming decoder,
    /// and thus handles the cases where the `Shared` has too little data or too much data.
    ///
    /// See also [`from_utf8_shared`].
    ///
    /// # Returns
    ///
    /// - `Ok(Some(_))` if there is a complete string in `src`.
    ///   `src` will be left with any bytes after the end of the string.
    /// - `Ok(None)` if there is an incomplete string in `src`.
    /// - `Err(_)` if there is a complete string in `src` but it is not valid UTF-8 or contains disallowed codepoints.
    ///   `src` will be left with any bytes after the end of the string.
    pub fn decode(src: &mut S) -> Result<Option<Self>, DecodeError> {
        let Some(b) = BinaryData::decode(src) else {
            return Ok(None);
        };

        // Validate the data bytes are valid UTF-8, and that none of the UTF-8 chars are \0.
        {
            let mut b = b.as_bytes();
            while !b.is_empty() {
                match bstr::decode_utf8(b) {
                    (Some(c), c_len) => {
                        // The spec says that implementations MUST NOT allow strings that contain U+0000, and additionally
                        // SHOULD NOT allow "Disallowed Unicode code points", which it defines as:
                        //
                        // - U+0001..U+001F control characters
                        // - U+007F..U+009F control characters
                        // - Code points defined in the Unicode specification to be non-characters (for example U+0FFFF)
                        //
                        // We implement both the MUST and the SHOULD here by checking the general category of the codepoint.
                        // In particular, note that U+0000 is also a `Control` character so it is covered by that check,
                        // and there are no other `Control` characters other than the two ranges the spec says so there is no need
                        // to check for those two ranges specifically. The `Unassigned` category corresponds to non-characters.
                        let general_category = c.general_category();
                        if general_category == GeneralCategory::Control {
                            return Err(DecodeError::InvalidByteStr("contains control codepoint"));
                        }
                        if general_category == GeneralCategory::Unassigned {
                            return Err(DecodeError::InvalidByteStr(
                                "contains non-character codepoint",
                            ));
                        }

                        b = &b[c_len..];
                    }

                    (None, _) => return Err(DecodeError::InvalidByteStr("invalid UTF-8")),
                }
            }
        }

        Ok(Some(Self(b)))
    }

    /// This function converts the given `Shared` into a `ByteStr`. The `Shared` must contain
    /// the bytes of a valid UTF-8 string with a length prefix. It is meant to be used for decoding payloads that are known to consist
    /// entirely of a single string.
    ///
    /// See also [`decode`].
    ///
    /// # Returns
    ///
    /// - `Ok(_)` if there is a complete string in `src`.
    /// - `Err(_)` `src` is truncated, or `src` has extra garbage after the string, or the string is not valid UTF-8.
    pub fn from_utf8_shared(mut src: S) -> Result<Self, DecodeError> {
        match Self::decode(&mut src) {
            Ok(Some(s)) if src.is_empty() => Ok(s),
            Ok(Some(_)) => Err(DecodeError::TrailingGarbage),
            Ok(None) => Err(DecodeError::IncompletePacket),
            Err(err) => Err(err),
        }
    }

    pub fn encode<B>(&self, dst: &mut B) -> Result<(), EncodeError>
    where
        B: BytesAccumulator<Shared = S>,
    {
        self.0.encode(dst)
    }

    /// Creates a copy of this `ByteStr` with another [`Shared`] type as the backing buffer.
    ///
    /// This is better than having the owner of `ByteStr<S1>` create `ByteStr<S2>` via `ByteStr::new(owned, byte_str.as_str())`
    /// because this does not need to recompute the length.
    pub fn to_shared<O2>(&self, owned: &mut O2) -> Result<ByteStr<O2::Shared>, buffer_pool::Error>
    where
        O2: Owned,
    {
        Ok(ByteStr(self.0.to_shared(owned)?))
    }
}

impl<S> AsRef<str> for ByteStr<S>
where
    S: Shared,
{
    fn as_ref(&self) -> &str {
        // SAFETY: All API for constructing a `ByteStr` validate that it was constructed from
        // a valid UTF-8 string.
        unsafe { std::str::from_utf8_unchecked(self.0.as_ref()) }
    }
}

impl<S> std::borrow::Borrow<str> for ByteStr<S>
where
    S: Shared,
{
    fn borrow(&self) -> &str {
        self.as_ref()
    }
}

impl<S> std::fmt::Debug for ByteStr<S>
where
    S: Shared,
{
    fn fmt(&self, f: &mut std::fmt::Formatter<'_>) -> std::fmt::Result {
        self.as_ref().fmt(f)
    }
}

impl<S> std::fmt::Display for ByteStr<S>
where
    S: Shared,
{
    fn fmt(&self, f: &mut std::fmt::Formatter<'_>) -> std::fmt::Result {
        self.as_ref().fmt(f)
    }
}

impl<S> std::hash::Hash for ByteStr<S>
where
    S: Shared,
{
    fn hash<H>(&self, state: &mut H)
    where
        H: std::hash::Hasher,
    {
        self.as_ref().hash(state);
    }
}

impl<S> PartialEq for ByteStr<S>
where
    S: Shared,
{
    fn eq(&self, other: &Self) -> bool {
        let s: &str = self.as_ref();
        let other: &str = other.as_ref();
        s.eq(other)
    }
}

impl<S> Eq for ByteStr<S> where S: Shared {}

impl<S> PartialOrd for ByteStr<S>
where
    S: Shared,
{
    fn partial_cmp(&self, other: &Self) -> Option<std::cmp::Ordering> {
        Some(self.cmp(other))
    }
}

impl<S> Ord for ByteStr<S>
where
    S: Shared,
{
    fn cmp(&self, other: &Self) -> std::cmp::Ordering {
        let s: &str = self.as_ref();
        let other: &str = other.as_ref();
        s.cmp(other)
    }
}

impl<S> PartialEq<str> for ByteStr<S>
where
    S: Shared,
{
    fn eq(&self, other: &str) -> bool {
        self.as_ref().eq(other)
    }
}

// This impl is seemingly redundant given the PartialEq<str> impl, but using `==` between a ByteStr and a str literal requires this one.
//
// String also impls both PartialEq<str> and PartialEq<&'_ str>, so this is consistent with libstd.
impl<S> PartialEq<&'_ str> for ByteStr<S>
where
    S: Shared,
{
    fn eq(&self, other: &&str) -> bool {
        self == *other
    }
}

impl<S> From<&str> for ByteStr<S>
where
    S: Shared,
    for<'a> &'a [u8]: Into<BinaryData<S>>,
{
    fn from(s: &str) -> Self {
        Self(s.as_bytes().into())
    }
}

#[cfg(test)]
mod tests {
    use std::ops::RangeInclusive;

    use test_case::test_case;

    use buffer_pool::{BufferPool as _, BytesPool};

    use super::*;

    #[test]
    fn new() {
        let mut buffer = BytesPool.take_empty_owned();

        let val = ByteStr::new(&mut buffer, "dummy").unwrap();

        assert_eq!(val.to_string(), "dummy");
        assert_eq!(val.len(), 5);
    }

    #[test]
    fn ord() {
        let mut buffer = BytesPool.take_empty_owned();

        let val1 = ByteStr::new(&mut buffer, "dummy1").unwrap();

        let val2 = ByteStr::new(&mut buffer, "dummy2").unwrap();

        assert!(val1 < val2);
    }

    #[test]
    fn debug() {
        let mut buffer = BytesPool.take_empty_owned();

        let val = ByteStr::new(&mut buffer, "dummy").unwrap();

        assert_eq!(format!("{val:?}"), "\"dummy\"");
    }

    #[test]
    fn into_shared_and_from_utf8_shared() {
        let mut buffer = BytesPool.take_empty_owned();

        // when 398 (len) is encoded as firs two bytes,
        // it is an invalid UTF-8 sequence, so will fail
        // if there is a bug in decode.
        let test_string = "t".repeat(398);

        let val = ByteStr::new(&mut buffer, &test_string).unwrap();

        let src = val.0.into_shared();

        let new_val = ByteStr::from_utf8_shared(src).unwrap();

        assert_eq!(new_val.to_string(), test_string);
    }

    #[test_case("abc")]
    #[test_case("A𪛔")]
    fn decode_unicode_ok(input: &str) {
        let mut src = BinaryData::from(input.as_bytes()).into_shared();

        let actual = ByteStr::decode(&mut src).unwrap().unwrap();
        assert_eq!(actual.as_ref(), input);
    }

    #[test_case(0x0000..=0x0000)]
    #[test_case(0x0001..=0x001f)]
    #[test_case(0x007f..=0x009f)]
    #[test_case(0xffff..=0xffff)]
    fn decode_disallowed_unicode_codepoints_err(inputs: RangeInclusive<u32>) {
        for input in inputs {
            let input: char = input.try_into().unwrap();
            let input = input.to_string();

            let mut src = BinaryData::from(input.as_bytes()).into_shared();

            _ = ByteStr::decode(&mut src).unwrap_err();
        }
    }
}
