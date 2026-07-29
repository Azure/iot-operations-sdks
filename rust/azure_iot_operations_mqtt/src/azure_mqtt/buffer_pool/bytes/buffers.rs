// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

use std::{
    cmp::Ordering,
    mem::{self, MaybeUninit},
};

use bytes::{Buf as _, BufMut, Bytes, BytesMut};

use crate::azure_mqtt::buffer_pool::{Error, Owned, Shared};

impl Owned for BytesMut {
    type Shared = Bytes;

    fn filled_len(&self) -> usize {
        self.len()
    }

    fn filled_is_empty(&self) -> bool {
        self.is_empty()
    }

    fn filled(&self) -> &[u8] {
        self.chunk()
    }

    fn filled_mut(&mut self) -> &mut [u8] {
        self.as_mut()
    }

    fn unfilled_len(&self) -> usize {
        self.capacity() - self.len()
    }

    /// # Safety
    ///
    /// Caller must not read from this slice, and must only write initialized elements to it.
    unsafe fn unfilled_mut(&mut self) -> &mut [MaybeUninit<u8>] {
        unsafe { self.chunk_mut().as_uninit_slice_mut() }
    }

    /// # Safety
    ///
    /// Caller must ensure that `n` bytes have already been initialized.
    unsafe fn fill(&mut self, n: usize) {
        unsafe {
            self.advance_mut(n);
        }
    }

    fn drain(&mut self, n: usize) {
        self.advance(n);
    }

    fn split_to(&mut self, i: usize) -> Self {
        // Use `BytesMut::split_off` instead of `BytesMut::split_to` because
        // the former works if len <= i <= capacity as `Owned::split_to` allows,
        // whereas the latter requires i <= len
        let mut other = self.split_off(i);
        mem::swap(self, &mut other);
        other
    }

    fn put_slice(&mut self, src: &[u8]) {
        BufMut::put_slice(self, src);
    }

    fn freeze(self) -> Self::Shared {
        self.freeze()
    }

    fn reserve(&mut self, new_unfilled_len: usize) -> Result<(), Error> {
        self.reserve(new_unfilled_len);
        Ok(())
    }
}

impl Shared for Bytes {
    fn len(&self) -> usize {
        self.len()
    }

    fn is_empty(&self) -> bool {
        self.is_empty()
    }

    fn drain(&mut self, i: usize) {
        self.advance(i);
    }

    fn split_to(&mut self, i: usize) -> Self {
        self.split_to(i)
    }
}
