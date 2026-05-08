// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

// TODO: Revisit these suppressions
#![allow(dead_code, unused_imports)]

use std::{
    mem::{MaybeUninit, size_of},
    ptr,
};

#[derive(Eq, PartialEq, thiserror::Error)]
#[error("insufficient capacity in buffer pool")]
pub struct Error;

impl std::fmt::Debug for Error {
    fn fmt(&self, f: &mut std::fmt::Formatter<'_>) -> std::fmt::Result {
        f.write_str("buffer_pool::Error")
    }
}

/// A source of buffers.
///
/// Dropped buffers will automatically return to this pool.
pub trait BufferPool: Clone + Send + Sync + std::fmt::Debug + Unpin + 'static {
    type Shared: Shared;
    type Owned: Owned<Shared = Self::Shared>;

    /// Returns a buffer that is at least as long as the requested length.
    fn take_owned(&self, len: usize) -> Result<Self::Owned, Error>;

    /// A special case of `take_owned` that returns an empty `Owned`. This is infallible.
    fn take_empty_owned(&self) -> Self::Owned;
}

/// Owns a particular range of the backing buffer.
///
/// An `Owned` tracks what part of itself has been filled with data.
/// The filled region can be accessed with [`Owned::filled`], and bytes can be removed from the start of this region with [`Owned::drain`].
/// The unfilled region can be accessed with [`Owned::unfilled_mut`], and bytes can be moved from the start of this region
/// into the end of the filled region with [`Owned::fill`].
///
/// An `Owned` can be subdivided into smaller `Owned`s with `split_at` that each own
/// smaller splits of the backing buffer.
///
/// An `Owned` is not `Clone`. It can be converted to a [`Shared`] which is, via [`Owned::freeze`]
pub trait Owned: Send + std::fmt::Debug {
    type Shared: Shared;

    fn filled_len(&self) -> usize;

    fn filled_is_empty(&self) -> bool;

    fn filled(&self) -> &[u8];

    fn filled_mut(&mut self) -> &mut [u8];

    fn unfilled_len(&self) -> usize;

    /// # Safety
    ///
    /// Caller must not read from this slice, and must only write initialized elements to it.
    unsafe fn unfilled_mut(&mut self) -> &mut [MaybeUninit<u8>];

    /// Moves the given number of bytes from the start of the unfilled region to the end of the filled region.
    ///
    /// # Safety
    ///
    /// Caller must ensure that `n` bytes have already been initialized.
    unsafe fn fill(&mut self, n: usize);

    /// Removes the given number of bytes from the start of the filled region.
    fn drain(&mut self, n: usize);

    /// Retains the range i.. in self, and returns a new Owned for the range 0..i
    fn split_to(&mut self, i: usize) -> Self;

    fn freeze(self) -> Self::Shared;

    /// Append the given `u8` to this buffer, advancing the filled region.
    ///
    /// # Panics
    /// Panics if `self.unfilled_len()` is less than `src.len()`.
    fn put_slice(&mut self, src: &[u8]);

    /// Increase the size of the unfilled region to be at least `additional` bytes.
    ///
    /// # Errors
    /// Returns an error if the buffer pool is at capacity.
    fn reserve(&mut self, new_unfilled_len: usize) -> Result<(), Error>;
}

pub trait Shared:
    AsRef<[u8]> + Clone + std::fmt::Debug + Eq + std::hash::Hash + PartialEq + Send + Sync + 'static
{
    fn new<O>(owned: &mut O, value: &[u8]) -> Result<Self, Error>
    where
        Self: Sized,
        O: Owned<Shared = Self>,
    {
        assert!(owned.filled_is_empty());

        let len = value.len();

        owned.reserve(len)?;

        // SAFETY: Requirements of `unfilled_mut` and `fill` are upheld.
        unsafe {
            let buf = owned.unfilled_mut();
            maybe_uninit_copy_from_slice(&mut buf[..len], value);
            owned.fill(len);
        }

        let filled_len = owned.filled_len();
        let shared = owned.split_to(filled_len).freeze();
        Ok(shared)
    }

    /// This makes a deep copy of the current shared
    /// into a new shared provided by O2.
    fn copy_to_shared<O2>(&self, owned: &mut O2) -> Result<O2::Shared, Error>
    where
        O2: Owned,
    {
        O2::Shared::new(owned, self.as_ref())
    }

    fn len(&self) -> usize;

    fn is_empty(&self) -> bool;

    /// Retains the range i.. in self
    ///
    /// This is the same as [`Shared::split_to`] but does not require creating a new `Shared` for the range 0..i
    fn drain(&mut self, i: usize);

    /// Retains the range i.. in self, and returns a new Shared for the range 0..i
    fn split_to(&mut self, i: usize) -> Self;

    /// Gets a [`u8`] from this buffer. Returns `None` if there are insufficient bytes.
    fn get_u8(&mut self) -> Option<u8> {
        let b = self.as_ref().get(..size_of::<u8>())?;
        let n = u8::from_be_bytes(b.try_into().unwrap());
        self.drain(size_of::<u8>());
        Some(n)
    }

    /// Gets a big-endian [`u16`] from this buffer. Returns `None` if there are insufficient bytes.
    fn get_u16_be(&mut self) -> Option<u16> {
        let b = self.as_ref().get(..size_of::<u16>())?;
        let n = u16::from_be_bytes(b.try_into().unwrap());
        self.drain(size_of::<u16>());
        Some(n)
    }

    /// Gets a big-endian [`u32`] from this buffer. Returns `None` if there are insufficient bytes.
    fn get_u32_be(&mut self) -> Option<u32> {
        let b = self.as_ref().get(..size_of::<u32>())?;
        let n = u32::from_be_bytes(b.try_into().unwrap());
        self.drain(size_of::<u32>());
        Some(n)
    }

    /// Gets a big-endian [`u64`] from this buffer. Returns `None` if there are insufficient bytes.
    fn get_u64_be(&mut self) -> Option<u64> {
        let b = self.as_ref().get(..size_of::<u64>())?;
        let n = u64::from_be_bytes(b.try_into().unwrap());
        self.drain(size_of::<u64>());
        Some(n)
    }

    /// Gets a big-endian [`u128`] from this buffer. Returns `None` if there are insufficient bytes.
    fn get_u128_be(&mut self) -> Option<u128> {
        let b = self.as_ref().get(..size_of::<u128>())?;
        let n = u128::from_be_bytes(b.try_into().unwrap());
        self.drain(size_of::<u128>());
        Some(n)
    }
}

pub mod accumulators;
#[cfg(test)]
pub use accumulators::test::TestAccumulator;
pub use accumulators::{
    BytesAccumulator, Iovecs, either::EitherAccumulator, iovecs::IovecsAccumulator,
    single::SingleAccumulator,
};

mod bytes;
pub use bytes::BytesPool;

// TODO(rustup): Replace this with `<[T]>::write_copy_of_slice` when that is stabilized.
pub fn maybe_uninit_copy_from_slice<T>(this: &mut [MaybeUninit<T>], src: &[T])
where
    T: Copy,
{
    // SAFETY: &[T] and &[MaybeUninit<T>] have the same layout.
    //
    // This is the same code as libstd's unstable impl.
    let uninit_src: &[MaybeUninit<T>] =
        unsafe { &*(ptr::from_ref(src) as *const [MaybeUninit<T>]) };
    this.copy_from_slice(uninit_src);
}
