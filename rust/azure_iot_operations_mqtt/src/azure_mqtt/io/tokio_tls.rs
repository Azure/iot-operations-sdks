// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

use std::{
    io::{self, IoSlice},
    mem::MaybeUninit,
    net::TcpStream as StdTcpStream,
    os::fd::AsFd as _,
    pin::Pin,
    sync::{
        Arc,
        atomic::{AtomicU8, Ordering},
    },
};

use either::Either;
use nix::{
    libc,
    sys::socket::{
        setsockopt,
        sockopt::{TcpTlsRx, TcpTlsTx, TcpUlp, TlsCryptoInfo},
    },
};
use openssl::ssl::{
    ErrorCode, HandshakeError, SslContextBuilder, SslRef, SslStream as StdSslStream, SslVersion,
};
use parking_lot::Mutex;
use tokio::{
    io::{AsyncReadExt, AsyncWriteExt, ReadBuf, ReadHalf, WriteHalf},
    net::TcpStream,
};
use tokio_openssl::SslStream;

use crate::azure_mqtt::buffer_pool::{BufferPool, EitherAccumulator};
use crate::azure_mqtt::client::ConnectionTransportTlsConfig;
use crate::azure_mqtt::io::{ReadableStream, Reader, WritableStream, Writer, tokio_tcp};
use crate::azure_mqtt::opensslext::ssl::{ConnectionTrafficSecrets, ExtractedSecrets};

/// Connect to the given address and port with a TLS connection, and use the given buffer pools
/// to initialize the buffers for the stream reader and writer.
///
/// The hostname will be matched against the server cert SAN.
pub async fn connect<BP>(
    hostname: &str,
    port: u16,
    config: ConnectionTransportTlsConfig,
    reader_pool: &BP,
    writer_pool: &BP,
) -> io::Result<(Reader<BP>, Writer<BP>)>
where
    BP: BufferPool,
{
    Ok(match connect_inner(hostname, port, config).await? {
        Either::Left(tcp_stream) => tokio_tcp::connect_inner(tcp_stream, reader_pool, writer_pool),

        Either::Right(ssl_stream) => {
            let (read, write) = tokio::io::split(ssl_stream);
            let read_buf = reader_pool.take_empty_owned();
            let write_buf = EitherAccumulator::Single(Default::default());

            (
                Reader::new(Box::new(OpensslStreamRead { inner: read }), read_buf),
                Writer::new(Box::new(OpensslStreamWrite { inner: write }), write_buf),
            )
        }
    })
}

pub(crate) async fn connect_inner(
    hostname: &str,
    port: u16,
    config: ConnectionTransportTlsConfig,
) -> io::Result<Either<TcpStream, SslStream<TcpStream>>> {
    /// We haven't attempted to create a TLS connection yet, so whether the kernel supports TLS or not
    /// is not yet known.
    const TLS_METHOD_UNKNOWN: u8 = 0;
    /// Use `tokio::net::TcpStream` with TLS ULP.
    const TLS_METHOD_KERNEL: u8 = 1;
    /// Use `tokio_openssl::SslStream`.
    const TLS_METHOD_USERSPACE: u8 = 2;

    static TLS_METHOD: AtomicU8 = AtomicU8::new(TLS_METHOD_UNKNOWN);

    let ConnectionTransportTlsConfig(mut connector) = config;

    let method = TLS_METHOD.load(Ordering::Relaxed);
    if method == TLS_METHOD_UNKNOWN {
        let traffic_secrets = prepare_for_ktls(&mut connector)?;

        let connector = connector.build();

        let tcp_stream = TcpStream::connect((hostname, port)).await?;

        let std_tcp_stream = {
            let fd = tcp_stream.as_fd();
            let fd = fd.try_clone_to_owned()?;
            StdTcpStream::from(fd)
        };

        let ssl_stream =
            handshake(&tcp_stream, connector.connect(hostname, &std_tcp_stream)).await?;

        let ssl = ssl_stream.ssl();

        match setup_ktls(&tcp_stream, ssl, &traffic_secrets) {
            Ok(()) => (),

            Err(err) if is_ktls_unsupported_err(&err) => {
                TLS_METHOD.store(TLS_METHOD_USERSPACE, Ordering::Relaxed);

                // We did a handshake manually so we can't reconstruct an `SslStream`
                // for the USERSPACE code path at this point.
                // Just fail this connection and wait for the client to reconnect.
                // That new connection will go into the USERSPACE code path from the start.
                return Err(io::ErrorKind::ConnectionRefused.into());
            }

            Err(err) => return Err(err),
        }

        TLS_METHOD.store(TLS_METHOD_KERNEL, Ordering::Relaxed);

        Ok(Either::Left(tcp_stream))
    } else if method == TLS_METHOD_KERNEL {
        let traffic_secrets = prepare_for_ktls(&mut connector)?;

        let connector = connector.build();

        let tcp_stream = TcpStream::connect((hostname, port)).await?;

        let std_tcp_stream = {
            let fd = tcp_stream.as_fd();
            let fd = fd.try_clone_to_owned()?;
            StdTcpStream::from(fd)
        };

        let ssl_stream =
            handshake(&tcp_stream, connector.connect(hostname, &std_tcp_stream)).await?;

        let ssl = ssl_stream.ssl();

        setup_ktls(&tcp_stream, ssl, &traffic_secrets)?;

        Ok(Either::Left(tcp_stream))
    } else {
        debug_assert_eq!(method, TLS_METHOD_USERSPACE);

        let tcp_stream = TcpStream::connect((hostname, port)).await?;

        let connector = connector.build().configure()?;

        let ssl = connector.into_ssl(hostname)?;
        let mut ssl_stream = SslStream::new(ssl, tcp_stream)?;

        Pin::new(&mut ssl_stream)
            .connect()
            .await
            .map_err(openssl_err_to_io_err)?;

        Ok(Either::Right(ssl_stream))
    }
}

struct OpensslStreamRead {
    inner: ReadHalf<SslStream<TcpStream>>,
}

struct OpensslStreamWrite {
    inner: WriteHalf<SslStream<TcpStream>>,
}

impl ReadableStream for OpensslStreamRead {
    fn read<'a>(
        &'a mut self,
        buf: &'a mut [MaybeUninit<u8>],
    ) -> Pin<Box<dyn Future<Output = io::Result<usize>> + Send + 'a>> {
        let mut buf = ReadBuf::uninit(buf);
        Box::pin(async move { self.inner.read_buf(&mut buf).await })
    }
}

impl WritableStream for OpensslStreamWrite {
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

fn prepare_for_ktls(
    context_builder: &mut SslContextBuilder,
) -> io::Result<Arc<Mutex<(Option<Vec<u8>>, Option<Vec<u8>>)>>> {
    // Only support TLS 1.2 and 1.3
    context_builder
        .set_min_proto_version(Some(SslVersion::TLS1_2))
        .map_err(openssl_err_to_io_err)?;
    context_builder
        .set_max_proto_version(Some(SslVersion::TLS1_3))
        .map_err(openssl_err_to_io_err)?;

    // kTLS supports a limited set of ciphersuites, of which only AES-128-GCM, AES-256-GCM and ChaCha20-Poly1305
    // are relevant for a modern implementation. So we only negotiate AES-256-GCM, CHACHA20-POLY1305 and AES-128-GCM, in that order.
    // We also only support ECDHE, not DHE, because again DHE is not relevant for a modern implementation.
    //
    // The ordering of AES-256-GCM -> Chacha20-Poly1305 -> AES-128-GCM is is in line with openssl's default (`openssl ciphers`),
    // though notably not in line with rustls which does AES-256-GCM -> AES-128-GCM -> Chacha20-Poly1305 instead. [1] [2]
    //
    // [1]: https://github.com/rustls/rustls/issues/509
    //
    // [2]: https://github.com/rustls/rustls/commit/7117a805e0104705da50259357d8effa7d599e37
    context_builder
        .set_cipher_list("ECDHE-ECDSA-AES256-GCM-SHA384:ECDHE-RSA-AES256-GCM-SHA384:ECDHE-ECDSA-CHACHA20-POLY1305:ECDHE-RSA-CHACHA20-POLY1305:ECDHE-ECDSA-AES128-GCM-SHA256:ECDHE-RSA-AES128-GCM-SHA256")
        .map_err(openssl_err_to_io_err)?;
    context_builder
        .set_ciphersuites(
            "TLS_AES_256_GCM_SHA384:TLS_CHACHA20_POLY1305_SHA256:TLS_AES_128_GCM_SHA256",
        )
        .map_err(openssl_err_to_io_err)?;

    // For TLS 1.2, we can derive traffic secrets ourselves using the master key.
    //
    // For TLS 1.3, the derivation uses the handshake hash which openssl does not expose.
    // However openssl does expose the traffic secrets through the keylog callback, so we use that.
    let traffic_secrets = Arc::new(Mutex::new((None, None)));
    context_builder.set_keylog_callback({
        let traffic_secrets = traffic_secrets.clone();
        move |_ssl, line| {
            // Ref: https://web.archive.org/web/20230425034128/https://firefox-source-docs.mozilla.org/security/nss/legacy/key_log_format/index.html
            let Some((name, rest)) = line.split_once(' ') else {
                // keylog callback didn't log in expected format. kTLS cannot work.
                return;
            };
            match name {
                "CLIENT_TRAFFIC_SECRET_0" => {
                    let Some((_client_random, value_hex)) = rest.split_once(' ') else {
                        // keylog callback didn't log in expected format. kTLS cannot work.
                        return;
                    };
                    let value = hex::decode(value_hex);
                    traffic_secrets.lock().0 = Some(value);
                }

                "SERVER_TRAFFIC_SECRET_0" => {
                    let Some((_client_random, value_hex)) = rest.split_once(' ') else {
                        // keylog callback didn't log in expected format. kTLS cannot work.
                        return;
                    };
                    let value = hex::decode(value_hex);
                    traffic_secrets.lock().1 = Some(value);
                }

                _ => (),
            }
        }
    });

    Ok(traffic_secrets)
}

async fn handshake<'a>(
    tcp_stream: &TcpStream,
    mut result: Result<StdSslStream<&'a StdTcpStream>, HandshakeError<&'a StdTcpStream>>,
) -> io::Result<StdSslStream<&'a StdTcpStream>> {
    loop {
        match result {
            Ok(ssl_stream) => break Ok(ssl_stream),

            Err(HandshakeError::WouldBlock(mid_handshake_stream)) => {
                let err = mid_handshake_stream.error();
                match err.code() {
                    ErrorCode::WANT_READ => tcp_stream.readable().await?,
                    ErrorCode::WANT_WRITE => tcp_stream.writable().await?,
                    _ => break Err(io::Error::other(err.to_string())),
                }

                result = mid_handshake_stream.handshake();
            }

            Err(err) => break Err(io::Error::other(err.to_string())),
        }
    }
}

fn setup_ktls(
    tcp_stream: &TcpStream,
    ssl: &SslRef,
    traffic_secrets: &Mutex<(Option<Vec<u8>>, Option<Vec<u8>>)>,
) -> io::Result<()> {
    let protocol_version = ssl
        .version2()
        .expect("SslRef::version2 only fails if called before completing handshake");

    let traffic_secrets = &*traffic_secrets.lock();
    let ExtractedSecrets { client, server } = crate::azure_mqtt::opensslext::ssl::extract_secrets(
        ssl,
        traffic_secrets.0.as_deref(),
        traffic_secrets.1.as_deref(),
    )
    .map_err(io::Error::other)?;

    let (tx, rx) = if ssl.is_server() {
        let tx = make_tls_crypto_info(protocol_version, server);
        let rx = make_tls_crypto_info(protocol_version, client);

        (tx, rx)
    } else {
        let tx = make_tls_crypto_info(protocol_version, client);
        let rx = make_tls_crypto_info(protocol_version, server);

        (tx, rx)
    };

    () = setsockopt(&tcp_stream, TcpUlp::default(), b"tls\0")?;
    () = setsockopt(&tcp_stream, TcpTlsTx, &tx)?;
    () = setsockopt(&tcp_stream, TcpTlsRx, &rx)?;

    Ok(())
}

fn is_ktls_unsupported_err(err: &io::Error) -> bool {
    let kind = err.kind();

    // `setsockopt(TCP_ULP)` returns `ENOEND` when the kernel doesn't support
    // the TLS ULP.
    let tls_ulp_unsupported = kind == io::ErrorKind::NotFound;

    // `setsockopt(TLS_TX)` / `setsockopt(TLS_RX)` return `ENOPROTOOPT`
    // when the kernel doesn't support the sockopt. `TLS_TX` has always been supported
    // but `TLS_RX` support was added later.
    let sockopt_unsupported = kind == io::ErrorKind::InvalidInput;

    // `setsockopt(TLS_TX)` / `setsockopt(TLS_RX)` return `EINVAL`
    // when the kernel doesn't support the ciphersuite.
    let ciphersuite_unsupported = kind == io::ErrorKind::InvalidInput;

    tls_ulp_unsupported || sockopt_unsupported || ciphersuite_unsupported
}

#[expect(clippy::needless_pass_by_value)] // Clippy wants `extracted_secret` to be a borrow because all its fields are Copy.
fn make_tls_crypto_info(
    protocol_version: SslVersion,
    extracted_secret: (u64, ConnectionTrafficSecrets),
) -> TlsCryptoInfo {
    let tls_crypto_info = libc::tls_crypto_info {
        version: match protocol_version {
            SslVersion::TLS1_2 => libc::TLS_1_2_VERSION,
            SslVersion::TLS1_3 => libc::TLS_1_3_VERSION,
            protocol_version => unreachable!(
                "expected OpenSSL to negotiate TLS 1.2 or 1.3 because of prepare_for_ktls but it negotiated {protocol_version:?}"
            ),
        },

        cipher_type: match &extracted_secret.1 {
            ConnectionTrafficSecrets::Aes128Gcm { .. } => libc::TLS_CIPHER_AES_GCM_128,
            ConnectionTrafficSecrets::Aes256Gcm { .. } => libc::TLS_CIPHER_AES_GCM_256,
            ConnectionTrafficSecrets::Chacha20Poly1305 { .. } => libc::TLS_CIPHER_CHACHA20_POLY1305,
        },
    };

    match extracted_secret {
        (rec_seq, ConnectionTrafficSecrets::Aes128Gcm { key, salt, iv }) => {
            TlsCryptoInfo::Aes128Gcm(libc::tls12_crypto_info_aes_gcm_128 {
                info: tls_crypto_info,
                iv,
                key,
                salt,
                rec_seq: rec_seq.to_be_bytes(),
            })
        }

        (rec_seq, ConnectionTrafficSecrets::Aes256Gcm { key, salt, iv }) => {
            TlsCryptoInfo::Aes256Gcm(libc::tls12_crypto_info_aes_gcm_256 {
                info: tls_crypto_info,
                iv,
                key,
                salt,
                rec_seq: rec_seq.to_be_bytes(),
            })
        }

        (rec_seq, ConnectionTrafficSecrets::Chacha20Poly1305 { key, salt, iv }) => {
            TlsCryptoInfo::Chacha20Poly1305(libc::tls12_crypto_info_chacha20_poly1305 {
                info: tls_crypto_info,
                iv,
                key,
                salt,
                rec_seq: rec_seq.to_be_bytes(),
            })
        }
    }
}

fn openssl_err_to_io_err(err: impl Into<openssl::ssl::Error>) -> io::Error {
    match err.into().into_io_error() {
        Ok(err) => err,
        Err(err) => io::Error::other(err),
    }
}

mod hex {
    /// Map of ASCII hex char -> corresponding numeric value
    #[rustfmt::skip] // Prevent rustfmt from mangling the grid layout.
    const NIBBLE_TO_HEX_LOOKUP_TABLE: [u8; 256] = [
        0,  0,  0,  0,  0,  0,  0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
        0,  0,  0,  0,  0,  0,  0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
        0,  0,  0,  0,  0,  0,  0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
        0,  1,  2,  3,  4,  5,  6, 7, 8, 9, 0, 0, 0, 0, 0, 0, /* 0x30: b'0'.. */
        0,  0,  0,  0,  0,  0,  0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
        0,  0,  0,  0,  0,  0,  0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
        0, 10, 11, 12, 13, 14, 15, 0, 0, 0, 0, 0, 0, 0, 0, 0, /* 0x61: b'a'.. */
        0,  0,  0,  0,  0,  0,  0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
        0,  0,  0,  0,  0,  0,  0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
        0,  0,  0,  0,  0,  0,  0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
        0,  0,  0,  0,  0,  0,  0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
        0,  0,  0,  0,  0,  0,  0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
        0,  0,  0,  0,  0,  0,  0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
        0,  0,  0,  0,  0,  0,  0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
        0,  0,  0,  0,  0,  0,  0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
        0,  0,  0,  0,  0,  0,  0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
    ];

    /// Decoded the given hex string.
    pub(super) fn decode(s: &str) -> Vec<u8> {
        let len = s.len() / 2;
        let mut result = Vec::with_capacity(len);

        // TODO(rustup): Use `slice::array_chunks` when that is stabilized.
        for (dst, src) in result.spare_capacity_mut()[..len]
            .iter_mut()
            .zip(s.as_bytes().chunks_exact(2))
        {
            dst.write(
                (NIBBLE_TO_HEX_LOOKUP_TABLE[usize::from(src[0])] << 4)
                    | NIBBLE_TO_HEX_LOOKUP_TABLE[usize::from(src[1])],
            );
        }

        // SAFETY: Above loop is guaranteed to have written `len` bytes.
        unsafe {
            result.set_len(len);
        }

        result
    }
}
