// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

use std::{
    future,
    io::{self, IoSlice},
    mem::MaybeUninit,
    pin::Pin,
    sync::Arc,
};

use tokio::net::{TcpStream, ToSocketAddrs};

use crate::azure_mqtt::buffer_pool::{BufferPool, EitherAccumulator};

use crate::azure_mqtt::io::{ReadableStream, Reader, WritableStream, Writer};

/// Connect to the given address with a TCP connection, and use the given buffer pools
/// to initialize the buffers for the stream reader and writer.
// TODO: Also take max packet size and forward it to `Reader`.
pub async fn connect<BP>(
    addr: impl ToSocketAddrs,
    reader_pool: &BP,
    writer_pool: &BP,
) -> io::Result<(Reader<BP>, Writer<BP>)>
where
    BP: BufferPool,
{
    let stream = TcpStream::connect(addr).await?;
    Ok(connect_inner(stream, reader_pool, writer_pool))
}

pub(crate) fn connect_inner<BP>(
    stream: TcpStream,
    reader_pool: &BP,
    writer_pool: &BP,
) -> (Reader<BP>, Writer<BP>)
where
    BP: BufferPool,
{
    let read = Arc::new(stream);
    let read_buf = reader_pool.take_empty_owned();
    let write = read.clone();
    let write_buf = EitherAccumulator::Iovecs(writer_pool.take_empty_owned().into());

    (
        Reader::new(Box::new(TcpStreamRead { inner: read }), read_buf),
        Writer::new(Box::new(TcpStreamWrite { inner: write }), write_buf),
    )
}

struct TcpStreamRead {
    inner: Arc<TcpStream>,
}

struct TcpStreamWrite {
    inner: Arc<TcpStream>,
}

impl ReadableStream for TcpStreamRead {
    fn read<'a>(
        &'a mut self,
        buf: &'a mut [MaybeUninit<u8>],
    ) -> Pin<Box<dyn Future<Output = io::Result<usize>> + Send + 'a>> {
        Box::pin(async move {
            loop {
                self.inner.readable().await?;

                // TcpStream::try_read requires a &mut [u8] but in practice doesn't read from it, only writes to it.
                //
                // SAFETY: tokio::net::TcpStream internally uses the same hack in its implementation of AsyncReadExt::read_buf
                // to convert the UninitSlice returned by BufMut::chunk_mut to &mut [u8], with the assumption that
                // the inner mio::net::TcpStream::read doesn't read from the [u8], only writes to it.
                let buf: &'a mut [u8] =
                    unsafe { &mut *(std::ptr::from_mut::<[MaybeUninit<u8>]>(buf) as *mut [u8]) };

                match self.inner.try_read(buf) {
                    Err(err) if err.kind() == io::ErrorKind::WouldBlock => (),
                    result => return result,
                }
            }
        })
    }
}

impl WritableStream for TcpStreamWrite {
    fn write_vectored<'a, 'buf>(
        &'a mut self,
        bufs: &'a [IoSlice<'buf>],
    ) -> Pin<Box<dyn Future<Output = io::Result<usize>> + Send + 'a>> {
        Box::pin(async move {
            loop {
                self.inner.writable().await?;
                match self.inner.try_write_vectored(bufs) {
                    Err(err) if err.kind() == io::ErrorKind::WouldBlock => (),
                    result => return result,
                }
            }
        })
    }

    fn flush(&mut self) -> Pin<Box<dyn Future<Output = io::Result<()>> + Send + '_>> {
        // Ideally we'd call `<TcpStream as AsyncWriteExt>::flush()` here but we don't have a `TcpStream`, only an `Rc<TcpStream>`.
        // That said, as of tokio 1.33.0, `<TcpStream as AsyncWriteExt>::flush()` is itself a no-op that returns `future::ready(Ok(()))`,
        // so we can just do that ourselves.
        Box::pin(future::ready(Ok(())))
    }
}
