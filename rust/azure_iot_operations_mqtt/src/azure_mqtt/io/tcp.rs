// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

//! Internal helper for establishing TCP streams, with optional proxy support.

use std::io;

use tokio::{
    io::{AsyncBufReadExt, AsyncWriteExt, BufReader},
    net::TcpStream,
};

use crate::azure_mqtt::transport::{Proxy, ProxyAuthorization, ProxyEndpoint};

/// Obtain a [`TcpStream`] connected to the given target, optionally through a proxy.
///
/// If `proxy` is `None`, this is equivalent to [`TcpStream::connect`].
/// If `proxy` is `Some`, an HTTP CONNECT tunnel is established through the proxy
/// before returning the stream.
///
/// `tcp_nodelay` sets the `TCP_NODELAY` option (Nagle's algorithm) on the returned stream.
pub async fn connect(
    hostname: &str,
    port: u16,
    proxy: Option<&Proxy>,
    tcp_nodelay: bool,
) -> io::Result<TcpStream> {
    let stream = match proxy {
        None => TcpStream::connect((hostname, port)).await?,
        Some(proxy) => http_connect_tunnel(proxy, hostname, port).await?,
    };
    stream.set_nodelay(tcp_nodelay)?;
    Ok(stream)
}

/// Establish an HTTP CONNECT tunnel through the given proxy to the target host and port.
///
/// Connects to the proxy endpoint, sends an HTTP CONNECT request (with optional
/// Proxy-Authorization header), validates a 200 response, and returns the tunneled stream.
async fn http_connect_tunnel(
    proxy: &Proxy,
    target_host: &str,
    target_port: u16,
) -> io::Result<TcpStream> {
    let stream = match &proxy.endpoint {
        ProxyEndpoint::Http { hostname, port } => {
            TcpStream::connect((hostname.as_str(), *port)).await?
        }
        ProxyEndpoint::Https { .. } => {
            // TODO: TLS connection to the proxy itself
            return Err(io::Error::other(
                "HTTPS proxy endpoints are not yet supported",
            ));
        }
    };

    // Build the HTTP CONNECT request
    let mut request = format!(
        "CONNECT {target_host}:{target_port} HTTP/1.1\r\n\
         Host: {target_host}:{target_port}\r\n"
    );

    match &proxy.auth {
        ProxyAuthorization::None => {}
        ProxyAuthorization::Basic { username, password } => {
            let credentials = openssl::base64::encode_block(
                format!("{username}:{password}").as_bytes(),
            );
            request.push_str(&format!("Proxy-Authorization: Basic {credentials}\r\n"));
        }
    }

    request.push_str("\r\n");

    // Send the CONNECT request
    let (reader, mut writer) = stream.into_split();
    writer.write_all(request.as_bytes()).await?;

    // Read the HTTP response status line
    let mut buf_reader = BufReader::new(reader);
    let mut status_line = String::new();
    buf_reader.read_line(&mut status_line).await?;

    // Validate the response status
    if !status_line.starts_with("HTTP/1.1 200") && !status_line.starts_with("HTTP/1.0 200") {
        return Err(io::Error::other(format!(
            "proxy CONNECT failed: {}", status_line.trim()
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

    // Reunite the split stream — it is now a transparent tunnel to the target
    let reader = buf_reader.into_inner();
    let stream = reader.reunite(writer).map_err(|e| {
        io::Error::other(format!("failed to reunite proxy stream: {e}"))
    })?;

    Ok(stream)
}
