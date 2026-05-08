// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

use std::io::IoSlice;

use crate::azure_mqtt::buffer_pool::{Error, Shared};

/// An accumulator of buffers. An encoder can put buffers into this accumulator, and then
/// a writer can access the buffers as [`std::io::IoSlice`]s for writing.
pub trait BytesAccumulator: std::fmt::Debug {
    type Shared: Shared;

    /// Returns whether this accumulator will accept future calls to [`reserve`](Self::reserve) and [`put_shared`](Self::put_shared)
    /// to add more buffers to itself. If it returns `false`, the accumulator must be [`drain`](Self::drain)ed first.
    fn can_accept_more(&self) -> bool;

    /// Reserve some additional capacity so that future `try_put_*` calls are expected to succeed.
    ///
    /// This does *not* need to be called before appending `Shared`s with [`put_shared`](Self::put_shared);
    /// `put_shared` always succeeds.
    fn reserve(&mut self, additional: usize) -> Result<(), Error>;

    /// Append the given `u8` to this accumulator.
    ///
    /// The byte is uncommitted until `put_done` or `put_shared` are called.
    /// Uncommitted bytes are not included in the output of [`to_iovecs`](Self::to_iovecs).
    fn try_put_u8(&mut self, n: u8) -> Option<()> {
        self.try_put_slice(&n.to_be_bytes())
    }

    /// Append the given `u16` to this accumulator in big-endian encoding.
    ///
    /// The bytes are uncommitted until `put_done` or `put_shared` are called.
    /// Uncommitted bytes are not included in the output of [`to_iovecs`](Self::to_iovecs).
    fn try_put_u16_be(&mut self, n: u16) -> Option<()> {
        self.try_put_slice(&n.to_be_bytes())
    }

    /// Append the given `u32` to this accumulator in big-endian encoding.
    ///
    /// The bytes are uncommitted until `put_done` or `put_shared` are called.
    /// Uncommitted bytes are not included in the output of [`to_iovecs`](Self::to_iovecs).
    fn try_put_u32_be(&mut self, n: u32) -> Option<()> {
        self.try_put_slice(&n.to_be_bytes())
    }

    /// Append the given `u64` to this accumulator in big-endian encoding.
    ///
    /// The bytes are uncommitted until `put_done` or `put_shared` are called.
    /// Uncommitted bytes are not included in the output of [`to_iovecs`](Self::to_iovecs).
    fn try_put_u64_be(&mut self, n: u64) -> Option<()> {
        self.try_put_slice(&n.to_be_bytes())
    }

    /// Append the given `u128` to this accumulator in big-endian encoding.
    ///
    /// The bytes are uncommitted until `put_done` or `put_shared` are called.
    /// Uncommitted bytes are not included in the output of [`to_iovecs`](Self::to_iovecs).
    fn try_put_u128_be(&mut self, n: u128) -> Option<()> {
        self.try_put_slice(&n.to_be_bytes())
    }

    /// Append the given slice to this accumulator.
    ///
    /// The bytes are uncommitted until `put_done` or `put_shared` are called.
    /// Uncommitted bytes are not included in the output of [`to_iovecs`](Self::to_iovecs).
    fn try_put_slice(&mut self, src: &[u8]) -> Option<()>;

    /// Append the given `Shared` to this accumulator.
    ///
    /// This operation always succeeds. If there are any uncommitted bytes, they are committed before this `Shared` is appended.
    fn put_shared(&mut self, src: Self::Shared);

    /// Commits any uncommitted bytes to this accumulator.
    fn put_done(&mut self);

    /// Fill the given [`IoSlice`]s with the buffers held by this accumulator.
    ///
    /// Returns the number of `IoSlice`s that were set in the given slice.
    fn to_iovecs<'a>(&'a self, iovecs: &mut [IoSlice<'a>]) -> Iovecs;

    /// Removes the given number of bytes from the start of the accumulator.
    fn drain(&mut self, n: usize);

    fn is_empty(&self) -> bool;
}

#[derive(Debug)]
pub struct Iovecs {
    pub num_iovecs: usize,
    pub total_len: usize,
}

pub(crate) mod either;

pub(crate) mod iovecs;

pub(crate) mod single;

#[cfg(test)]
pub(crate) mod test;
