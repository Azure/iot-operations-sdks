// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

use std::{io::IoSlice, marker::PhantomData};

use bytes::{Buf as _, BytesMut};

use crate::azure_mqtt::buffer_pool::{BytesAccumulator, Error, Iovecs, Shared};

/// Unlike [`SingleAccumulator`](crate::azure_mqtt::buffer_pool::SingleAccumulator),
/// this `TestAccumulator` enforces that `reserve` was called before `try_put_*`.
#[derive(Debug, Eq, PartialEq)]
pub struct TestAccumulator<S> {
    inner: BytesMut,

    // Used to enforce that the BytesMut does not have more capacity what it was asked to reserve,
    // since tests rely on this to assert that encoding fails when the buffer is too small.
    // We can't use `inner.capacity() - inner.len()` because `inner.reserve(additional)` can (and in practice, does)
    // reserve more than `additional`. So we need to track it ourselves.
    unfilled_len: usize,

    // Used to verify that the caller has called `put_done` as they're supposed to.
    put_done_required: bool,

    shared: PhantomData<S>,
}

impl<S> TestAccumulator<S> {
    pub fn with_capacity(len: usize) -> Self {
        TestAccumulator {
            inner: BytesMut::with_capacity(len),
            unfilled_len: len,
            put_done_required: false,
            shared: Default::default(),
        }
    }
}

impl<S> BytesAccumulator for TestAccumulator<S>
where
    S: Shared,
{
    type Shared = S;

    fn can_accept_more(&self) -> bool {
        true
    }

    fn reserve(&mut self, additional: usize) -> Result<(), Error> {
        self.inner.reserve(additional);
        self.unfilled_len = self.unfilled_len.max(additional);
        Ok(())
    }

    fn put_shared(&mut self, src: Self::Shared) {
        self.put_done();

        self.inner.extend_from_slice(src.as_ref());

        // The contract of BytesAccumulator is that Shared's should not count towards reserved space,
        // so re-reserve the space that was previously reserved.
        self.inner.reserve(self.unfilled_len);
    }

    fn put_done(&mut self) {
        self.put_done_required = false;
    }

    fn try_put_slice(&mut self, src: &[u8]) -> Option<()> {
        if self.unfilled_len < src.len() {
            return None;
        }

        self.inner.extend_from_slice(src);
        self.unfilled_len = self.unfilled_len.checked_sub(src.len()).unwrap();
        self.put_done_required = true;
        Some(())
    }

    fn to_iovecs<'a>(&'a self, iovecs: &mut [IoSlice<'a>]) -> Iovecs {
        assert!(!self.put_done_required);

        if let Some(iovec) = iovecs.first_mut() {
            let chunk = self.inner.chunk();
            if !chunk.is_empty() {
                *iovec = IoSlice::new(chunk);
                return Iovecs {
                    num_iovecs: 1,
                    total_len: chunk.len(),
                };
            }
        }

        Iovecs {
            num_iovecs: 0,
            total_len: 0,
        }
    }

    fn drain(&mut self, n: usize) {
        self.inner.advance(n);
    }

    fn is_empty(&self) -> bool {
        self.inner.is_empty()
    }
}
