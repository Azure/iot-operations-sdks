// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

//! Structs and types related to transport configuration

use std::{
    io,
    time::Duration,
};

use bytes::Bytes;
use openssl::{
    pkey::{PKey, Private},
    ssl::{SslConnector, SslConnectorBuilder, SslMethod, SslVersion},
    x509::X509,
};

use crate::azure_mqtt::mqtt_proto::Packet;

// Re-export some types from `async_tungstenite` for use in the current API.
// TODO: Consider a more elegant solution in the future.
#[cfg(feature = "test-utils")]
pub use async_tungstenite::tungstenite::{
    handshake::client::Request as WsRequest,
    client::{
        ClientRequestBuilder as WsRequestBuilder,
        IntoClientRequest as IntoWsRequest,
    },
    http::Uri as WsUri,
};

/// Parameters for establishing a new MQTT connection at transport layer.
pub struct ConnectionTransportConfig {
    pub transport_type: ConnectionTransportType,
    pub timeout: Option<Duration>,
    pub proxy: Option<Proxy>,
    /// Whether to disable Nagle's algorithm (`TCP_NODELAY`) on the underlying TCP socket.
    /// Setting this to `true` reduces latency for small, frequent packets at the cost of slightly
    /// more packet overhead.
    pub tcp_nodelay: bool, // TODO: Should this be some kind of options struct with timeout and other cfg options?
}

/// The type of transport to use for the new MQTT connection.
pub enum ConnectionTransportType {
    Tcp {
        hostname: String,
        port: u16,
    },
    Tls {
        hostname: String,
        port: u16,
        tls_config: TlsConfig,
    },
    #[cfg(feature = "test-utils")]
    Ws {
        request: WsRequest,
        tls_config: Option<TlsConfig>,
    },
    #[cfg(feature = "test-utils")]
    Test {
        incoming_packets: tokio::sync::mpsc::UnboundedReceiver<Packet<Bytes>>,
        outgoing_packets: tokio::sync::mpsc::UnboundedSender<Packet<Bytes>>,
    },
}

/// Proxy configuration for the connection.
/// Only supports static authentication, not challenge-based authentication
pub struct Proxy {
    pub endpoint: ProxyEndpoint,
    pub auth: ProxyAuthorization,
}


// Proxy endpoint configuration, indicating the protocol to use to connect to the proxy
pub enum ProxyEndpoint {
    Http {
        hostname: String,
        port: u16,
    },
    Https {
        hostname: String,
        port: u16,
        tls_config: TlsConfig,
    },
    // TODO: SOCKS5?
}

/// Value that will be sent in the Proxy-Authorization header when connecting through a proxy
pub enum ProxyAuthorization {
    None,
    Basic { username: String, password: String },
    // TODO: custom
}

/// Parameters for establishing a TLS connection.
pub struct TlsConfig(pub(crate) SslConnectorBuilder);

impl TlsConfig {
    /// Constructs a [`TlsConfig`] with the given client certificate and CA trust bundle.
    ///
    /// The client certificate is specified as a tuple of the main client cert, its private key,
    /// and a list of zero or more chain certs that should be sent along with the main cert.
    pub fn new(
        client_cert: Option<(X509, PKey<Private>, Vec<X509>)>,
        ca_trust_bundle: Vec<X509>,
    ) -> io::Result<Self> {
        let mut connector = SslConnector::builder(SslMethod::tls_client())?;

        connector.set_min_proto_version(Some(SslVersion::TLS1_2))?;

        if let Some((cert, pkey, cert_chain)) = client_cert {
            connector.set_certificate(&cert)?;
            connector.set_private_key(&pkey)?;
            for cert in cert_chain {
                connector.add_extra_chain_cert(cert)?;
            }
        }

        if !ca_trust_bundle.is_empty() {
            let cert_store = connector.cert_store_mut();
            for cert in ca_trust_bundle {
                cert_store.add_cert(cert)?;
            }
        }

        Ok(Self(connector))
    }

    /// Constructs a [`ConnectionTransportTlsConfig`] with the client certificate and CA trust bundle
    /// parsed from the given PEM blobs.
    ///
    /// The client certificate is specified as a one blob containing the PEM-encoded cert chain
    /// (main cert followed by other certs in the chain) and one blob containing the PEM-encoded private key.
    pub fn from_pem(
        client_cert: Option<(&[u8], &[u8])>,
        ca_trust_bundle: &[u8],
    ) -> io::Result<Self> {
        let client_cert = if let Some((cert, pkey)) = client_cert {
            let mut client_cert_chain = X509::stack_from_pem(cert)?;
            if client_cert_chain.is_empty() {
                return Err(io::Error::other(
                    "client cert PEM does not contain any certificates",
                ));
            }
            let client_cert = client_cert_chain.remove(0);

            let pkey = PKey::private_key_from_pem(pkey)?;

            Some((client_cert, pkey, client_cert_chain))
        } else {
            None
        };

        let ca_trust_bundle = X509::stack_from_pem(ca_trust_bundle)?;

        Self::new(client_cert, ca_trust_bundle)
    }
}

impl From<SslConnectorBuilder> for TlsConfig {
    fn from(connector: SslConnectorBuilder) -> Self {
        Self(connector)
    }
}
