// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

use std::{collections::VecDeque, io::IoSlice};

use crate::azure_mqtt::buffer_pool::{self, BytesAccumulator, Error, Iovecs, Owned, Shared};

#[derive(Debug)]
pub struct IovecsAccumulator<O>
where
    O: Owned,
{
    owned: O,
    chunks: VecDeque<O::Shared>,
    /// Total number of bytes in `self.chunks`.
    total_size: usize,
}

impl<O> IovecsAccumulator<O>
where
    O: Owned,
{
    pub fn new(owned: O) -> Self {
        IovecsAccumulator {
            owned,
            chunks: Default::default(),
            total_size: 0,
        }
    }
}

impl<O> BytesAccumulator for IovecsAccumulator<O>
where
    O: Owned,
{
    type Shared = O::Shared;

    fn can_accept_more(&self) -> bool {
        // TODO: More structured way of calculating this.
        // This depends on the number of iovecs the Writer uses, but knowing that here is a layering violation.
        // Also, this length includes file chunks which the Writer does not write via iovecs.
        // Also, the `- 10` is because we want the last packet that gets encoded to not push `self.chunks.len()`
        // above the number of iovecs the Writer uses, but knowing how many Shared's the packet will need is also
        // a layering violation.
        self.chunks.len() < 128 - 10
    }

    fn reserve(&mut self, additional: usize) -> Result<(), Error> {
        self.owned.reserve(additional)
    }

    fn put_shared(&mut self, src: Self::Shared) {
        // An empty Shared will manifest as an empty iovec in the Writer, which is not only wasteful but also
        // lead to the Writer issuing 0-byte writes and interpreting the resulting 0 from writev as an EOF.
        // So ignore empty Shared's.
        if src.is_empty() {
            return;
        }

        self.put_done();
        self.total_size += src.len();
        self.chunks.push_back(src);
    }

    fn try_put_slice(&mut self, src: &[u8]) -> Option<()> {
        // SAFETY: Requirements of `unfilled_mut` and `fill` are upheld.
        unsafe {
            let dst = self.owned.unfilled_mut();
            if dst.len() >= src.len() {
                let dst = dst.get_mut(..src.len())?;
                buffer_pool::maybe_uninit_copy_from_slice(dst, src);
                self.owned.fill(src.len());
                Some(())
            } else {
                None
            }
        }
    }

    fn put_done(&mut self) {
        if !self.owned.filled_is_empty() {
            let shared = self.owned.split_to(self.owned.filled_len()).freeze();
            self.total_size += shared.len();
            self.chunks.push_back(shared);
        }
    }

    fn to_iovecs<'a>(&'a self, iovecs: &mut [IoSlice<'a>]) -> Iovecs {
        assert!(self.owned.filled_is_empty());

        let mut num_iovecs = 0;
        let mut total_len = 0;
        for (iovec, chunk) in iovecs.iter_mut().zip(&self.chunks) {
            *iovec = IoSlice::new(chunk.as_ref());
            num_iovecs += 1;
            total_len += chunk.len();
        }
        Iovecs {
            num_iovecs,
            total_len,
        }
    }

    fn drain(&mut self, n: usize) {
        // We observed that most of the time the vectored write in `Writer` is able to
        // write all the iovecs we gave it, so we have a fast path for clearing the queue of Shared's
        // for that case. The slow path handles the case of partial writes by dropping
        // only those Shared's that were completely written and partially draining
        // the one that wasn't.

        #[cold]
        fn drain_slow<O>(this: &mut IovecsAccumulator<O>, mut n: usize)
        where
            O: Owned,
        {
            while let Some(mut chunk) = this.chunks.pop_front() {
                this.total_size -= chunk.as_ref().len();

                if let Some(n_) = n.checked_sub(chunk.as_ref().len()) {
                    n = n_;
                } else {
                    chunk.drain(n);
                    n = 0;
                    this.total_size += chunk.as_ref().len();
                    this.chunks.push_front(chunk);
                    break;
                }
            }
            assert_eq!(n, 0);
        }

        if self.total_size == n {
            self.total_size = 0;
            self.chunks.clear();
            return;
        }

        drain_slow(self, n);
    }

    fn is_empty(&self) -> bool {
        self.owned.filled_is_empty() && self.chunks.iter().all(Self::Shared::is_empty)
    }
}

impl<O> From<O> for IovecsAccumulator<O>
where
    O: Owned,
{
    fn from(owned: O) -> Self {
        Self::new(owned)
    }
}
