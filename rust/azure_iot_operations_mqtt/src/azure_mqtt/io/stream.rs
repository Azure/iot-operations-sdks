// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

//! Establishes and adapts the base byte stream to the target: direct TCP, HTTP `CONNECT` proxy
//! tunneling, and TLS. Everything above this layer consumes an `AsyncRead + AsyncWrite` and does
//! not care how the stream was obtained.
//!
//! The unit of this module is the [`TransportStream`] it produces; keep new stream-establishment
//! concerns here, but note that [`tls_handshake`] is the natural extraction point should the TLS
//! primitive ever need to be shared more widely.

use std::{
    io::{self, IoSlice},
    pin::Pin,
    task::{Context, Poll},
};

use tokio::{
    io::{AsyncBufReadExt, AsyncRead, AsyncWrite, AsyncWriteExt, BufReader, ReadBuf},
    net::TcpStream,
};
use tokio_openssl::SslStream;

use crate::azure_mqtt::transport::{Proxy, ProxyAuthorization, ProxyEndpoint, TlsConfig};

/// An established base transport byte stream.
///
/// This is intentionally opaque to consumers: they only require `AsyncRead + AsyncWrite`.
/// Whether the underlying connection is plain TCP or a TLS connection to an HTTPS proxy is an
/// internal detail of [`connect`].
pub(crate) struct TransportStream(TransportStreamInner);

enum TransportStreamInner {
    /// A plain TCP connection (direct, or tunneled through an HTTP proxy).
    Plain(TcpStream),
    /// A TLS connection to an HTTPS proxy, carrying the `CONNECT` tunnel to the target.
    Tls(SslStream<TcpStream>),
}

/// Obtain a [`TransportStream`] connected to the given target, optionally through a proxy.
///
/// If `proxy` is `None`, this connects directly to the target.
/// If `proxy` is `Some`, an HTTP `CONNECT` tunnel is established through the proxy before
/// returning the stream. For an [`ProxyEndpoint::Https`] proxy, the connection to the proxy
/// itself is wrapped in TLS.
///
/// `tcp_nodelay` sets the `TCP_NODELAY` option (Nagle's algorithm) on the underlying TCP socket.
pub(crate) async fn connect(
    hostname: &str,
    port: u16,
    proxy: Option<Proxy>,
    tcp_nodelay: bool,
) -> io::Result<TransportStream> {
    match proxy {
        None => {
            let stream = TcpStream::connect((hostname, port)).await?;
            stream.set_nodelay(tcp_nodelay)?;
            Ok(TransportStream(TransportStreamInner::Plain(stream)))
        }
        Some(proxy) => http_connect_tunnel(proxy, hostname, port, tcp_nodelay).await,
    }
}

/// Establish an HTTP CONNECT tunnel through the given proxy to the target host and port.
///
/// Connects to the proxy endpoint (wrapping the connection in TLS for an
/// [`ProxyEndpoint::Https`] proxy), performs the HTTP `CONNECT` exchange, and returns the
/// resulting transparent tunnel to the target.
async fn http_connect_tunnel(
    proxy: Proxy,
    target_host: &str,
    target_port: u16,
    tcp_nodelay: bool,
) -> io::Result<TransportStream> {
    let Proxy { endpoint, auth } = proxy;
    match endpoint {
        ProxyEndpoint::Http { hostname, port } => {
            let stream = TcpStream::connect((hostname.as_str(), port)).await?;
            stream.set_nodelay(tcp_nodelay)?;
            let stream = http_connect_exchange(stream, target_host, target_port, &auth).await?;
            Ok(TransportStream(TransportStreamInner::Plain(stream)))
        }
        ProxyEndpoint::Https {
            hostname,
            port,
            tls_config,
        } => {
            let stream = TcpStream::connect((hostname.as_str(), port)).await?;
            stream.set_nodelay(tcp_nodelay)?;
            // Wrap the connection to the proxy itself in TLS before tunneling.
            let stream = tls_handshake(stream, tls_config, &hostname).await?;
            let stream = http_connect_exchange(stream, target_host, target_port, &auth).await?;
            Ok(TransportStream(TransportStreamInner::Tls(stream)))
        }
    }
}

/// Perform the HTTP `CONNECT` request/response exchange over an established stream to a proxy,
/// returning the same stream — now a transparent tunnel to the target.
async fn http_connect_exchange<S>(
    mut stream: S,
    target_host: &str,
    target_port: u16,
    auth: &ProxyAuthorization,
) -> io::Result<S>
where
    S: AsyncRead + AsyncWrite + Unpin,
{
    // Build the HTTP CONNECT request
    let mut request = format!(
        "CONNECT {target_host}:{target_port} HTTP/1.1\r\n\
         Host: {target_host}:{target_port}\r\n"
    );

    match auth {
        ProxyAuthorization::None => {}
        ProxyAuthorization::Basic { username, password } => {
            let credentials =
                openssl::base64::encode_block(format!("{username}:{password}").as_bytes());
            request.push_str(&format!("Proxy-Authorization: Basic {credentials}\r\n"));
        }
    }

    request.push_str("\r\n");

    // Send the CONNECT request
    stream.write_all(request.as_bytes()).await?;

    // Read the HTTP response status line
    let mut buf_reader = BufReader::new(stream);
    let mut status_line = String::new();
    buf_reader.read_line(&mut status_line).await?;

    // Validate the response status
    if !status_line.starts_with("HTTP/1.1 200") && !status_line.starts_with("HTTP/1.0 200") {
        return Err(io::Error::other(format!(
            "proxy CONNECT failed: {}",
            status_line.trim()
        )));
    }

    // Consume remaining headers until the empty line
    let mut header_line = String::new();
    loop {
        header_line.clear();
        buf_reader.read_line(&mut header_line).await?;
        if header_line == "\r\n" || header_line == "\n" || header_line.is_empty() {
            break;
        }
    }

    // Unwrap the buffered reader to recover the raw stream — it is now a transparent tunnel.
    // LIMITATION: `BufReader::into_inner` discards any bytes it has already buffered past the
    // header terminator. This is safe for `CONNECT` because the client speaks first (TLS
    // ClientHello / MQTT CONNECT), so a well-behaved proxy sends nothing after the blank line.
    // However, a proxy that coalesces the `200` response with early target bytes into one segment
    // would cause those bytes to be silently lost here. Revisit if this ever proves a problem.
    Ok(buf_reader.into_inner())
}

/// Wrap an established stream in a client-side TLS session, returning the encrypted stream.
///
/// The hostname is used for SNI and to match against the server cert SAN.
pub(crate) async fn tls_handshake<S>(
    stream: S,
    config: TlsConfig,
    hostname: &str,
) -> io::Result<SslStream<S>>
where
    S: AsyncRead + AsyncWrite + Unpin,
{
    let TlsConfig(connector) = config;
    let connector = connector.build().configure()?;

    let ssl = connector.into_ssl(hostname)?;
    let mut ssl_stream = SslStream::new(ssl, stream)?;

    Pin::new(&mut ssl_stream)
        .connect()
        .await
        .map_err(openssl_err_to_io_err)?;

    Ok(ssl_stream)
}

fn openssl_err_to_io_err(err: impl Into<openssl::ssl::Error>) -> io::Error {
    match err.into().into_io_error() {
        Ok(err) => err,
        Err(err) => io::Error::other(err),
    }
}

impl AsyncRead for TransportStream {
    fn poll_read(
        self: Pin<&mut Self>,
        cx: &mut Context<'_>,
        buf: &mut ReadBuf<'_>,
    ) -> Poll<io::Result<()>> {
        match &mut self.get_mut().0 {
            TransportStreamInner::Plain(s) => Pin::new(s).poll_read(cx, buf),
            TransportStreamInner::Tls(s) => Pin::new(s).poll_read(cx, buf),
        }
    }
}

impl AsyncWrite for TransportStream {
    fn poll_write(
        self: Pin<&mut Self>,
        cx: &mut Context<'_>,
        buf: &[u8],
    ) -> Poll<io::Result<usize>> {
        match &mut self.get_mut().0 {
            TransportStreamInner::Plain(s) => Pin::new(s).poll_write(cx, buf),
            TransportStreamInner::Tls(s) => Pin::new(s).poll_write(cx, buf),
        }
    }

    fn poll_write_vectored(
        self: Pin<&mut Self>,
        cx: &mut Context<'_>,
        bufs: &[IoSlice<'_>],
    ) -> Poll<io::Result<usize>> {
        match &mut self.get_mut().0 {
            TransportStreamInner::Plain(s) => Pin::new(s).poll_write_vectored(cx, bufs),
            TransportStreamInner::Tls(s) => Pin::new(s).poll_write_vectored(cx, bufs),
        }
    }

    fn is_write_vectored(&self) -> bool {
        match &self.0 {
            TransportStreamInner::Plain(s) => s.is_write_vectored(),
            TransportStreamInner::Tls(s) => s.is_write_vectored(),
        }
    }

    fn poll_flush(self: Pin<&mut Self>, cx: &mut Context<'_>) -> Poll<io::Result<()>> {
        match &mut self.get_mut().0 {
            TransportStreamInner::Plain(s) => Pin::new(s).poll_flush(cx),
            TransportStreamInner::Tls(s) => Pin::new(s).poll_flush(cx),
        }
    }

    fn poll_shutdown(self: Pin<&mut Self>, cx: &mut Context<'_>) -> Poll<io::Result<()>> {
        match &mut self.get_mut().0 {
            TransportStreamInner::Plain(s) => Pin::new(s).poll_shutdown(cx),
            TransportStreamInner::Tls(s) => Pin::new(s).poll_shutdown(cx),
        }
    }
}
