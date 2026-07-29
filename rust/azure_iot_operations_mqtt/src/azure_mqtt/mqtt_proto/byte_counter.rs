// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

use std::{io::IoSlice, marker::PhantomData};

use derive_where::derive_where;

use crate::azure_mqtt::buffer_pool::{self, BytesAccumulator, Iovecs, Shared};

#[derive(Debug)]
#[derive_where(Eq, PartialEq)]
pub struct ByteCounter<S, const COUNT_SHARED: bool> {
    count: usize,
    shared: PhantomData<S>,
}

impl<S, const COUNT_SHARED: bool> ByteCounter<S, COUNT_SHARED> {
    pub fn new() -> Self {
        Self {
            count: 0,
            shared: Default::default(),
        }
    }

    pub fn into_count(self) -> usize {
        self.count
    }
}

impl<S, const COUNT_SHARED: bool> BytesAccumulator for ByteCounter<S, COUNT_SHARED>
where
    S: Shared,
{
    type Shared = S;

    fn can_accept_more(&self) -> bool {
        true
    }

    fn reserve(&mut self, _additional: usize) -> Result<(), buffer_pool::Error> {
        Ok(())
    }

    fn put_shared(&mut self, src: Self::Shared) {
        if COUNT_SHARED {
            self.count += src.len();
        }
    }

    fn put_done(&mut self) {}

    fn try_put_slice(&mut self, src: &[u8]) -> Option<()> {
        self.count += src.len();
        Some(())
    }

    fn to_iovecs<'a>(&'a self, _iovecs: &mut [IoSlice<'a>]) -> Iovecs {
        unreachable!("ByteCounter is never used for actually writing");
    }

    fn drain(&mut self, _n: usize) {
        unreachable!("ByteCounter is never used for actually writing");
    }

    fn is_empty(&self) -> bool {
        unreachable!("ByteCounter is never used in situations where this is called")
    }
}

impl<S, const COUNT_SHARED: bool> Default for ByteCounter<S, COUNT_SHARED> {
    fn default() -> Self {
        Self::new()
    }
}

#[cfg(test)]
mod tests {
    use bytes::Bytes;

    use super::ByteCounter;
    use crate::azure_mqtt::buffer_pool::BytesAccumulator as _;

    #[test]
    fn can_accept_more_always_true() {
        let counter = ByteCounter::<Bytes, true>::default();
        assert!(counter.can_accept_more());

        let counter = ByteCounter::<Bytes, false>::default();
        assert!(counter.can_accept_more());
    }

    #[test]
    fn count_shared() {
        let mut counter = ByteCounter::<_, true>::default();
        counter.try_put_u8(1).unwrap();
        counter.put_shared(Bytes::from_static(b"foo"));
        counter.try_put_u8(2).unwrap();
        assert_eq!(counter.into_count(), 1 + 3 + 1);
    }

    #[test]
    fn dont_count_shared() {
        let mut counter = ByteCounter::<_, false>::default();
        counter.try_put_u8(1).unwrap();
        counter.put_shared(Bytes::from_static(b"foo"));
        counter.try_put_u8(2).unwrap();
        #[expect(clippy::identity_op)]
        {
            assert_eq!(counter.into_count(), 1 + 0 + 1);
        }
    }
}
