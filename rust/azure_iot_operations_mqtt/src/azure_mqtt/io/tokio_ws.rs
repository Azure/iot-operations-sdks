// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

use std::{
    io::{self, Error as IoError, IoSlice, Result as IoResult},
    mem::MaybeUninit,
    pin::Pin,
    task::{Context, Poll},
};

use async_tungstenite::{
    WebSocketStream as TungsteniteWebSocketStream,
    tokio::TokioAdapter,
    tungstenite::{self, Bytes, Message, client::IntoClientRequest, http::HeaderValue},
};
use either::Either;
use futures_util::{Sink, Stream};
use tokio::io::{AsyncRead, AsyncReadExt, AsyncWrite, AsyncWriteExt, ReadBuf, ReadHalf, WriteHalf};

use crate::azure_mqtt::buffer_pool::{BufferPool, EitherAccumulator};
use crate::azure_mqtt::transport::{Proxy, TlsConfig};
use crate::azure_mqtt::io::{ReadableStream, Reader, WritableStream, Writer};

/// Establish a WebSocket connection using the given request parameters,
/// and use the given buffer pools to initialize the buffers for the stream reader and writer.
pub async fn connect<BP>(
    request: impl IntoClientRequest,
    tls_config: Option<TlsConfig>,
    proxy: Option<Proxy>,
    tcp_nodelay: bool,
    reader_pool: &BP,
) -> io::Result<(Reader<BP>, Writer<BP>)>
where
    BP: BufferPool,
{
    let mut request = request
        .into_client_request()
        .map_err(tungstenite_err_to_io_err)?;
    request
        .headers_mut()
        .insert("Sec-WebSocket-Protocol", HeaderValue::from_static("mqtt"));

    let Some(addr) = request.uri().host() else {
        return Err(io::Error::other(
            "request URI does not contain a host component",
        ));
    };
    let port = request.uri().port_u16();
    let Some(scheme) = request.uri().scheme_str() else {
        return Err(io::Error::other(
            "request URI does not contain a scheme component",
        ));
    };

    if !["http", "https", "ws", "wss"].contains(&scheme) {
        return Err(IoError::new(
            io::ErrorKind::InvalidInput,
            format!("unsupported WebSocket URI scheme: {scheme}"),
        ));
    }

    // Guard against the two sources of truth for "is this TLS" disagreeing: the scheme
    // (`wss`/`https`) and the presence of `tls_config`. Without this, a `wss` URI with no
    // `tls_config` would silently connect in plaintext (and `ws` with a `tls_config` would use TLS),
    // since the TLS decision below is driven by `tls_config` rather than the scheme.
    let scheme_is_tls = matches!(scheme, "wss" | "https");
    if scheme_is_tls != tls_config.is_some() {
        return Err(IoError::new(
            io::ErrorKind::InvalidInput,
            format!("`tls_config` must be provided for a `{scheme}` URI and omitted otherwise"),
        ));
    }

    let stream = if let Some(tls_config) = tls_config {
        Either::Right(
            super::stream::connect_tls(addr, port.unwrap_or(443), tls_config, proxy, tcp_nodelay)
                .await?,
        )
    } else {
        let stream = super::stream::connect(addr, port.unwrap_or(80), proxy, tcp_nodelay).await?;
        Either::Left(stream)
    };

    match stream {
        Either::Left(stream) => {
            let (stream, _response) = async_tungstenite::tokio::client_async(request, stream)
                .await
                .map_err(tungstenite_err_to_io_err)?;
            let stream = WebSocketStream {
                inner: stream,
                next_read: Default::default(),
            };
            let (read, write) = tokio::io::split(stream);
            let read_buf = reader_pool.take_empty_owned();
            let write_buf = EitherAccumulator::Single(Default::default());
            Ok((
                Reader::new(Box::new(WebSocketStreamRead { inner: read }), read_buf),
                Writer::new(Box::new(WebSocketStreamWrite { inner: write }), write_buf),
            ))
        }

        Either::Right(stream) => {
            let (stream, _response) = async_tungstenite::tokio::client_async(request, stream)
                .await
                .map_err(tungstenite_err_to_io_err)?;
            let stream = WebSocketStream {
                inner: stream,
                next_read: Default::default(),
            };
            let (read, write) = tokio::io::split(stream);
            let read_buf = reader_pool.take_empty_owned();
            let write_buf = EitherAccumulator::Single(Default::default());
            Ok((
                Reader::new(Box::new(WebSocketStreamRead { inner: read }), read_buf),
                Writer::new(Box::new(WebSocketStreamWrite { inner: write }), write_buf),
            ))
        }
    }
}

#[derive(Debug)]
struct WebSocketStream<S> {
    inner: TungsteniteWebSocketStream<TokioAdapter<S>>,
    next_read: Bytes,
}

#[derive(Debug)]
struct WebSocketStreamRead<S> {
    inner: ReadHalf<WebSocketStream<S>>,
}

#[derive(Debug)]
struct WebSocketStreamWrite<S> {
    inner: WriteHalf<WebSocketStream<S>>,
}

impl<S> AsyncRead for WebSocketStream<S>
where
    TungsteniteWebSocketStream<TokioAdapter<S>>:
        Stream<Item = Result<Message, tungstenite::Error>> + Unpin,
{
    fn poll_read(
        mut self: Pin<&mut Self>,
        cx: &mut Context<'_>,
        buf: &mut ReadBuf<'_>,
    ) -> Poll<IoResult<()>> {
        loop {
            if self.next_read.is_empty() {
                match Pin::new(&mut self.inner).poll_next(cx) {
                    Poll::Ready(Some(Ok(tungstenite::Message::Binary(next_read)))) => {
                        self.next_read = next_read;
                    }

                    Poll::Ready(Some(Ok(tungstenite::Message::Text(next_read)))) => {
                        self.next_read = next_read.into();
                    }

                    Poll::Ready(Some(Ok(tungstenite::Message::Close(_)))) => {
                        return Poll::Ready(Ok(()));
                    }

                    Poll::Ready(Some(Ok(
                        tungstenite::Message::Ping(_) | tungstenite::Message::Pong(_),
                    ))) => (),

                    Poll::Ready(Some(Ok(tungstenite::Message::Frame(_)))) => {
                        unreachable!("reading from tungstenite stream never yields raw frames");
                    }

                    Poll::Ready(Some(Err(tungstenite::Error::Io(err)))) => {
                        return Poll::Ready(Err(err));
                    }

                    Poll::Ready(Some(Err(err))) => {
                        return Poll::Ready(Err(IoError::other(err)));
                    }

                    Poll::Ready(None) => return Poll::Ready(Ok(())),

                    Poll::Pending => return Poll::Pending,
                }
            } else {
                let to_read = buf.remaining().min(self.next_read.len());
                let read = self.next_read.split_to(to_read);
                buf.put_slice(&read);
                return Poll::Ready(Ok(()));
            }
        }
    }
}

impl<S> AsyncWrite for WebSocketStream<S>
where
    TungsteniteWebSocketStream<TokioAdapter<S>>: Sink<Message, Error = tungstenite::Error> + Unpin,
{
    fn poll_write(
        mut self: Pin<&mut Self>,
        cx: &mut Context<'_>,
        buf: &[u8],
    ) -> Poll<IoResult<usize>> {
        match Pin::new(&mut self.inner).poll_ready(cx) {
            Poll::Ready(Ok(())) => (),

            Poll::Ready(Err(tungstenite::Error::Io(err))) => return Poll::Ready(Err(err)),

            Poll::Ready(Err(err)) => {
                return Poll::Ready(Err(IoError::other(err)));
            }

            Poll::Pending => return Poll::Pending,
        }

        match Pin::new(&mut self.inner)
            .start_send(tungstenite::Message::Binary(Bytes::copy_from_slice(buf)))
        {
            Ok(()) => Poll::Ready(Ok(buf.len())),

            Err(tungstenite::Error::Io(err)) => Poll::Ready(Err(err)),

            Err(err) => Poll::Ready(Err(IoError::other(err))),
        }
    }

    fn poll_flush(mut self: Pin<&mut Self>, cx: &mut Context<'_>) -> Poll<IoResult<()>> {
        match Pin::new(&mut self.inner).poll_flush(cx) {
            Poll::Ready(Ok(())) => Poll::Ready(Ok(())),

            Poll::Ready(Err(tungstenite::Error::Io(err))) => Poll::Ready(Err(err)),

            Poll::Ready(Err(err)) => Poll::Ready(Err(IoError::other(err))),

            Poll::Pending => Poll::Pending,
        }
    }

    fn poll_shutdown(mut self: Pin<&mut Self>, cx: &mut Context<'_>) -> Poll<IoResult<()>> {
        match Pin::new(&mut self.inner).poll_close(cx) {
            Poll::Ready(Ok(())) => Poll::Ready(Ok(())),

            Poll::Ready(Err(tungstenite::Error::Io(err))) => Poll::Ready(Err(err)),

            Poll::Ready(Err(err)) => Poll::Ready(Err(IoError::other(err))),

            Poll::Pending => Poll::Pending,
        }
    }
}

impl<S> ReadableStream for WebSocketStreamRead<S>
where
    S: Send,
    WebSocketStream<S>: AsyncRead,
{
    fn read<'a, 'buf>(
        &'a mut self,
        buf: &'a mut [MaybeUninit<u8>],
    ) -> Pin<Box<dyn Future<Output = io::Result<usize>> + Send + 'a>> {
        let mut buf = ReadBuf::uninit(buf);
        Box::pin(async move { self.inner.read_buf(&mut buf).await })
    }
}

impl<S> WritableStream for WebSocketStreamWrite<S>
where
    S: Send,
    WebSocketStream<S>: AsyncWrite,
{
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

fn tungstenite_err_to_io_err(err: tungstenite::Error) -> io::Error {
    match err {
        tungstenite::Error::Io(err) => err,
        err => io::Error::other(err),
    }
}
