// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

use std::mem::size_of;

use bytes::{BufMut as _, Bytes, BytesMut};

use crate::azure_mqtt::buffer_pool::{self, BytesAccumulator, Owned, Shared};
use crate::azure_mqtt::mqtt_proto::{DecodeError, EncodeError};

/// Binary data (not including payloads described by remaining length) are prefixed
/// with a two-byte big-endian length.
///
/// Ref: 1.5.6 Binary data
#[derive(Clone, Debug)]
pub struct BinaryData<S>(pub(crate) S)
where
    S: Shared;

impl<S> BinaryData<S>
where
    S: Shared,
{
    pub const EMPTY: &'static [u8] = b"\x00\x00";

    pub fn new<O>(owned: &mut O, s: impl AsRef<[u8]>) -> Result<Self, buffer_pool::Error>
    where
        O: Owned<Shared = S>,
    {
        let s = s.as_ref();

        assert!(owned.filled_is_empty());

        let len = s.len();

        owned.reserve(len + U16_SIZE)?;

        // SAFETY: Requirements of `unfilled_mut` and `fill` are upheld.
        unsafe {
            let len: u16 = len.try_into().expect("len is too big for u16");
            let len = len.to_be_bytes();
            let buf = owned.unfilled_mut();
            buffer_pool::maybe_uninit_copy_from_slice(&mut buf[..U16_SIZE], &len);
            owned.fill(U16_SIZE);
        }

        // SAFETY: Requirements of `unfilled_mut` and `fill` are upheld.
        unsafe {
            let buf = owned.unfilled_mut();
            buffer_pool::maybe_uninit_copy_from_slice(&mut buf[..len], s);
            owned.fill(len);
        }

        let filled_len = owned.filled_len();
        let shared = owned.split_to(filled_len).freeze();
        Ok(BinaryData(shared))
    }

    pub fn as_bytes(&self) -> &[u8] {
        self.0
            .as_ref()
            .get(size_of::<u16>()..)
            .expect("self.0 has length prefix")
    }

    pub fn len(&self) -> usize {
        // self.0 has a length prefix, so the data is 2 bytes shorter
        self.0.len() - size_of::<u16>()
    }

    pub fn is_empty(&self) -> bool {
        self.0.as_ref() == Self::EMPTY
    }

    pub fn into_shared(self) -> S {
        self.0
    }

    /// This function decodes a `BinaryData` out from the given `Shared`. It is meant to be used from a streaming decoder,
    /// and thus handles the cases where the `Shared` has too little data or too much data.
    ///
    /// See also [`from_shared`].
    ///
    /// # Returns
    ///
    /// - `Some(_)` if there is a complete binary data in `src`. `src` will be left with any trailing bytes.
    /// - `None` if there is an incomplete binary data in `src`.
    pub fn decode(src: &mut S) -> Option<Self>
    where
        S: Shared,
    {
        let (len, _) = src.as_ref().split_first_chunk()?;
        let len: usize = u16::from_be_bytes(*len).into();

        if src.len() < size_of::<u16>() + len {
            return None;
        }

        let s = src.split_to(size_of::<u16>() + len);

        Some(BinaryData(s))
    }

    /// This function converts the given `Shared` into a `BinaryData`. The `Shared` must contain
    /// the bytes of a valid Binary Data. It is meant to be used for decoding payloads that are known to consist
    /// entirely of a single binary data.
    ///
    /// See also [`decode`].
    ///
    /// # Returns
    ///
    /// - `Ok(_)` if there is a complete binary data in `src`.
    /// - `Err(_)` `src` is truncated, or `src` has extra garbage after the binary data.
    pub fn from_shared(mut src: S) -> Result<Self, DecodeError> {
        match Self::decode(&mut src) {
            Some(b) if src.is_empty() => Ok(b),
            Some(_) => Err(DecodeError::TrailingGarbage),
            None => Err(DecodeError::IncompletePacket),
        }
    }

    #[allow(clippy::unnecessary_wraps)]
    pub fn encode<B>(&self, dst: &mut B) -> Result<(), EncodeError>
    where
        B: BytesAccumulator<Shared = S>,
    {
        dst.put_shared(self.0.clone());
        Ok(())
    }

    /// Creates a copy of this `BinaryData` with another [`Shared`] type as the backing buffer.
    ///
    /// This is better than having the owner of `BinaryData<S1>` create `BinaryData<S2>` via `BinaryData::new(owned, byte_str.as_str())`
    /// because this does not need to recompute the length.
    pub fn to_shared<O2>(
        &self,
        owned: &mut O2,
    ) -> Result<BinaryData<O2::Shared>, buffer_pool::Error>
    where
        O2: Owned,
    {
        Ok(BinaryData(self.0.copy_to_shared(owned)?))
    }
}

impl<S> AsRef<[u8]> for BinaryData<S>
where
    S: Shared,
{
    fn as_ref(&self) -> &[u8] {
        self.as_bytes()
    }
}

impl<S> std::borrow::Borrow<[u8]> for BinaryData<S>
where
    S: Shared,
{
    fn borrow(&self) -> &[u8] {
        self.as_ref()
    }
}

impl<S> std::fmt::Display for BinaryData<S>
where
    S: Shared,
{
    fn fmt(&self, f: &mut std::fmt::Formatter<'_>) -> std::fmt::Result {
        std::fmt::Debug::fmt(self, f)
    }
}

impl<S> std::hash::Hash for BinaryData<S>
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

impl<S> PartialEq for BinaryData<S>
where
    S: Shared,
{
    fn eq(&self, other: &Self) -> bool {
        let s: &[u8] = self.as_ref();
        let other: &[u8] = other.as_ref();
        s.eq(other)
    }
}

impl<S> PartialEq<[u8]> for BinaryData<S>
where
    S: Shared,
{
    fn eq(&self, other: &[u8]) -> bool {
        self.as_ref().eq(other)
    }
}

impl<S> Eq for BinaryData<S> where S: Shared {}

impl<S> PartialOrd for BinaryData<S>
where
    S: Shared,
{
    fn partial_cmp(&self, other: &Self) -> Option<std::cmp::Ordering> {
        Some(self.cmp(other))
    }
}

impl<S> Ord for BinaryData<S>
where
    S: Shared,
{
    fn cmp(&self, other: &Self) -> std::cmp::Ordering {
        let s: &[u8] = self.as_ref();
        let other: &[u8] = other.as_ref();
        s.cmp(other)
    }
}

// This impl is seemingly redundant given the PartialEq<[u8]> impl, but using `==` between a BinaryData and a [u8] literal requires this one.
//
// Vec<u8> also impls both PartialEq<[u8]> and PartialEq<&'_ [u8]>, so this is consistent with libstd.
impl<S> PartialEq<&'_ [u8]> for BinaryData<S>
where
    S: Shared,
{
    fn eq(&self, other: &&[u8]) -> bool {
        self == *other
    }
}

impl From<&[u8]> for BinaryData<Bytes> {
    fn from(s: &[u8]) -> Self {
        let mut result = BytesMut::with_capacity(U16_SIZE + s.len());
        result.put(&u16::try_from(s.len()).unwrap().to_be_bytes()[..]);
        result.put(s);
        Self(result.freeze())
    }
}

impl<const N: usize> From<&[u8; N]> for BinaryData<Bytes> {
    fn from(s: &[u8; N]) -> Self {
        s[..].into()
    }
}

const U16_SIZE: usize = size_of::<u16>();

#[cfg(test)]
mod tests {
    use std::{
        cmp::Ordering,
        collections::hash_map::DefaultHasher,
        hash::{Hash, Hasher},
    };

    use bytes::Bytes;
    use matches::assert_matches;
    use test_case::test_case;

    use crate::azure_mqtt::buffer_pool::{BufferPool as _, Shared as _};

    use super::{BinaryData, DecodeError};

    #[test_case(b"cats", b"cats", true)]
    #[test_case(b"cats", b"dogs", false)]
    fn test_binary_data_hash(l: &[u8], r: &[u8], expect: bool) {
        let l = BinaryData::from(l);
        let r = BinaryData::from(r);
        let mut hasher = DefaultHasher::new();
        l.hash(&mut hasher);
        let l_hash = hasher.finish();
        hasher = DefaultHasher::new();
        r.hash(&mut hasher);
        let r_hash = hasher.finish();
        assert_eq!(l_hash == r_hash, expect);
    }

    #[test_case(b"cats", b"cats", Ordering::Equal)]
    #[test_case(b"cats", b"dogs", Ordering::Less)]
    #[test_case(b"dogs", b"cats", Ordering::Greater)]
    fn test_binary_data_ord(l: &[u8], r: &[u8], expect: Ordering) {
        let l = BinaryData::from(l);
        let r = BinaryData::from(r);
        assert_eq!(l.cmp(&r), expect);
    }

    #[test]
    fn test_binary_data_from_shared_with_trailing_garbage() {
        let s = Bytes::from_static(b"\x00\x01hello world");
        let actual = BinaryData::from_shared(s);
        assert_matches!(actual, Err(DecodeError::TrailingGarbage));
    }

    #[test]
    fn test_binary_data_from_shared_with_incomplete_packet() {
        let s = Bytes::from_static(b"\x01\x00hello world");
        let actual = BinaryData::from_shared(s);
        assert_matches!(actual, Err(DecodeError::IncompletePacket));
    }

    #[test]
    fn test_binary_data_from_shared_with_empty() {
        let s = Bytes::from_static(b"\x00\x00");
        let actual = BinaryData::from_shared(s);
        assert_matches!(actual, Ok(_));
    }

    #[test]
    fn test_binary_data_from_shared_simple() {
        let s = Bytes::from_static(b"\x00\x0bhello world");
        let actual = BinaryData::from_shared(s);
        assert_matches!(actual, Ok(_));
    }
}
