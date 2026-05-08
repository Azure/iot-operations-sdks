// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

// TODO: Revisit this suppression
#![allow(dead_code)]

use std::{
    io::{self, IoSlice},
    mem::MaybeUninit,
    pin::Pin,
};

mod reader;
pub use reader::Reader;

#[cfg(feature = "test-utils")]
pub mod test;

pub mod tokio_tcp;

pub mod tokio_tls;

mod writer;
pub use writer::Writer;

pub(crate) trait ReadableStream: Send {
    fn read<'a>(
        &'a mut self,
        buf: &'a mut [MaybeUninit<u8>],
    ) -> Pin<Box<dyn Future<Output = io::Result<usize>> + Send + 'a>>;
}

pub(crate) trait WritableStream: Send {
    fn write_vectored<'a, 'buf>(
        &'a mut self,
        bufs: &'a [IoSlice<'buf>],
    ) -> Pin<Box<dyn Future<Output = io::Result<usize>> + Send + 'a>>;

    fn flush(&mut self) -> Pin<Box<dyn Future<Output = io::Result<()>> + Send + '_>>;
}
