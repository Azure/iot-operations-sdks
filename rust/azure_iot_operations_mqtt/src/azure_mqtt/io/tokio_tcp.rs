// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

use std::{
    io::{self, IoSlice},
    mem::MaybeUninit,
    pin::Pin,
};

use tokio::io::{AsyncReadExt, AsyncWriteExt, ReadBuf, ReadHalf, WriteHalf};

use crate::azure_mqtt::buffer_pool::{BufferPool, EitherAccumulator};
use crate::azure_mqtt::transport::Proxy;

use crate::azure_mqtt::io::stream::TransportStream;
use crate::azure_mqtt::io::{ReadableStream, Reader, WritableStream, Writer};

/// Connect to the given address with a TCP connection, and use the given buffer pools
/// to initialize the buffers for the stream reader and writer.
// TODO: Also take max packet size and forward it to `Reader`.
pub async fn connect<BP>(
    hostname: &str,
    port: u16,
    proxy: Option<Proxy>,
    tcp_nodelay: bool,
    reader_pool: &BP,
    writer_pool: &BP,
) -> io::Result<(Reader<BP>, Writer<BP>)>
where
    BP: BufferPool,
{
    let stream = super::stream::connect(hostname, port, proxy, tcp_nodelay).await?;

    let (read, write) = tokio::io::split(stream);
    let read_buf = reader_pool.take_empty_owned();
    let write_buf = EitherAccumulator::Iovecs(writer_pool.take_empty_owned().into());

    Ok((
        Reader::new(Box::new(TransportStreamRead { inner: read }), read_buf),
        Writer::new(Box::new(TransportStreamWrite { inner: write }), write_buf),
    ))
}

struct TransportStreamRead {
    inner: ReadHalf<TransportStream>,
}

struct TransportStreamWrite {
    inner: WriteHalf<TransportStream>,
}

impl ReadableStream for TransportStreamRead {
    fn read<'a>(
        &'a mut self,
        buf: &'a mut [MaybeUninit<u8>],
    ) -> Pin<Box<dyn Future<Output = io::Result<usize>> + Send + 'a>> {
        let mut buf = ReadBuf::uninit(buf);
        Box::pin(async move { self.inner.read_buf(&mut buf).await })
    }
}

impl WritableStream for TransportStreamWrite {
    fn write_vectored<'a, 'buf>(
        &'a mut self,
        bufs: &'a [IoSlice<'buf>],
    ) -> Pin<Box<dyn Future<Output = io::Result<usize>> + Send + 'a>> {
        Box::pin(self.inner.write_vectored(bufs))
    }

    fn flush(&mut self) -> Pin<Box<dyn Future<Output = io::Result<()>> + Send + '_>> {
        Box::pin(self.inner.flush())
    }
}
