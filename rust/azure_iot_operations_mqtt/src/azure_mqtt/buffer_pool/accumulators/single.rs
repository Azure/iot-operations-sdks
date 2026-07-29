// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

use std::{io::IoSlice, marker::PhantomData};

use bytes::{Buf as _, BytesMut};

use crate::azure_mqtt::buffer_pool::{BytesAccumulator, Error, Iovecs, Shared};

/// This type impls [`BytesAccumulator`] with a target of a single [`BytesMut`].
///
/// `SingleAccumulator` does not require `reserve` to be called and will grow its inner `BytesMut` anyway.
#[derive(Debug, PartialEq, Eq)]
pub struct SingleAccumulator<S>(BytesMut, PhantomData<S>);

impl<S> SingleAccumulator<S> {
    pub fn new() -> Self {
        Default::default()
    }
}

impl<S> BytesAccumulator for SingleAccumulator<S>
where
    S: Shared,
{
    type Shared = S;

    fn can_accept_more(&self) -> bool {
        true
    }

    fn reserve(&mut self, additional: usize) -> Result<(), Error> {
        self.0.reserve(additional);
        Ok(())
    }

    fn put_shared(&mut self, src: Self::Shared) {
        self.0.extend_from_slice(src.as_ref());
    }

    fn try_put_slice(&mut self, src: &[u8]) -> Option<()> {
        self.0.extend_from_slice(src);
        Some(())
    }

    fn put_done(&mut self) {}

    fn to_iovecs<'a>(&'a self, iovecs: &mut [IoSlice<'a>]) -> Iovecs {
        if let Some(iovec) = iovecs.first_mut() {
            let chunk = self.0.chunk();
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
        self.0.advance(n);
    }

    fn is_empty(&self) -> bool {
        self.0.is_empty()
    }
}

impl<S> AsRef<[u8]> for SingleAccumulator<S> {
    fn as_ref(&self) -> &[u8] {
        self.0.as_ref()
    }
}

impl<S> Default for SingleAccumulator<S> {
    fn default() -> Self {
        Self(BytesMut::new(), PhantomData)
    }
}
