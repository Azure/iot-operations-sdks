// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

//! Structs and types that together provide the MQTT client functionality.

// TODO: Remove when possible.
#![allow(unused_variables)]
#![allow(dead_code)]
#![allow(clippy::unused_async)]

use std::{future::Future, io, num::NonZeroU16, pin::pin, time::Duration};

use bytes::{Bytes, BytesMut};
use futures_util::future::{self, FutureExt as _};
use openssl::{
    pkey::{PKey, Private},
    ssl::{SslConnector, SslConnectorBuilder, SslMethod, SslVersion},
    x509::X509,
};
use thiserror::Error;

use crate::azure_mqtt::buffer_pool::{BufferPool, BytesPool};
use crate::azure_mqtt::client::{
    channel_data::{
        DisconnectRequest, IncomingPublishAndToken, PublishRequestQoS0, PublishRequestQoS1QoS2,
        ReauthRequest, SubscriptionRequest,
    },
    session::{CompletedOperation, Session},
    timer::Timer,
    token::{
        acknowledgement::{PubAckToken, PubRecToken},
        completion::buffered::completion_pair,
        completion::{
            PublishQoS0CompletionToken, PublishQoS1CompletionToken, PublishQoS2CompletionToken,
            ReauthCompletionToken, SubscribeCompletionToken, UnsubscribeCompletionToken,
        },
        reauth::ReauthToken,
    },
};
use crate::azure_mqtt::error::{ConnectError, DetachedError, ProtocolError, ProtocolErrorRepr};
use crate::azure_mqtt::io::{Reader, Writer};
use crate::azure_mqtt::mqtt_proto::{
    self,
    // TODO: this gets too confusing with packet types. Can we abstract these away somehow?
    Connect,
    DisconnectOtherProperties,
    DisconnectReasonCode,
    Packet,
    ProtocolVersion,
};
use crate::azure_mqtt::packet::{
    Auth, AuthProperties, AuthReason, AuthenticationInfo, ConnAck, ConnectProperties, Disconnect,
    DisconnectProperties, KeepAlive, PacketIdentifier, Publish, PublishProperties, QoS,
    RetainOptions, SubscribeProperties, UnsubscribeProperties, Will,
};
use crate::azure_mqtt::topic::{TopicFilter, TopicName};

// TODO: What should this module and factory function be called?
// The three components are the client collectively - so what should the outbound struct (currently called the Client) be?
// Should it be MqttSender or something? Or are we fine with the duplicate semantic?
// Alternatively, maybe we break up connect/disconnect/auth into a separate fourth component?

mod channel_data;
mod session;
mod timer;
pub mod token;

/// Creates the three components needed to run the MQTT client
#[allow(clippy::needless_pass_by_value)] // TODO: Remove when implemented
pub fn new_client(options: ClientOptions) -> (Client, ConnectHandle, Receiver) {
    let (o_pub_q12_tx, o_pub_q12_rx) =
        tokio::sync::mpsc::channel(options.publish_qos1_qos2_queue_size);
    let (o_pub_q0_tx, o_pub_q0_rx) = tokio::sync::mpsc::channel(options.publish_qos0_queue_size);
    // NOTE: We use size 1 channels for outgoing data to avoid buffering packets that are not yet
    // owned by the internal session state. If this becomes a performance bottleneck, revisit.
    let (sub_tx, sub_rx) = tokio::sync::mpsc::channel(1);
    let (ack_tx, ack_rx) = tokio::sync::mpsc::channel(1);
    let (auth_tx, auth_rx) = tokio::sync::mpsc::channel(1);
    // NOTE: We use an unbounded channel for incoming publishes, as messages read off the network must go
    // somewhere.
    let (i_pub_tx, i_pub_rx) = tokio::sync::mpsc::unbounded_channel();
    let client = Client {
        pub_qos0_tx: o_pub_q0_tx,
        pub_qos12_tx: o_pub_q12_tx,
        sub_tx,
    };
    let reader_pool = BytesPool;
    let writer_pool = BytesPool;
    let owned = writer_pool.take_empty_owned();
    let session = Session::new(
        sub_rx,
        o_pub_q0_rx,
        o_pub_q12_rx,
        ack_rx,
        auth_rx,
        i_pub_tx,
        ack_tx,
        auth_tx,
        options.max_packet_identifier,
        owned,
    );
    let connect_handle = ConnectHandle {
        session,
        reader_pool,
        writer_pool,
        cfg_client_id: options.client_id,
    };
    let receiver = Receiver { rx: i_pub_rx };
    (client, connect_handle, receiver)
}

/// Options for configuring the MQTT client
pub struct ClientOptions {
    /// MQTT Client Identifier. If None, the MQTT server will assign one.
    pub client_id: Option<String>,
    /// Maximum packet identifier
    pub max_packet_identifier: PacketIdentifier,
    /// Maximum size of the outgoing queue for QoS 0 PUBLISH packets.
    pub publish_qos0_queue_size: usize,
    /// Maximum size of the outgoing queue for QoS 1 and 2 PUBLISH packets.
    pub publish_qos1_qos2_queue_size: usize,
    // TODO: Consider using a Builder pattern?
}

impl Default for ClientOptions {
    fn default() -> Self {
        Self {
            client_id: None,
            max_packet_identifier: PacketIdentifier::MAX,
            publish_qos0_queue_size: 100,
            publish_qos1_qos2_queue_size: 100,
        }
    }
}

/// Parameters for establishing a new connection.
pub struct ConnectionTransportConfig {
    pub transport_type: ConnectionTransportType,
    pub timeout: Option<Duration>,
}

/// The type of transport to use for the new connection.
pub enum ConnectionTransportType {
    Tcp {
        hostname: String,
        port: u16,
    },
    Tls {
        hostname: String,
        port: u16,
        config: ConnectionTransportTlsConfig,
    },
    #[cfg(feature = "test-utils")]
    Test {
        incoming_packets: tokio::sync::mpsc::UnboundedReceiver<Packet<Bytes>>,
        outgoing_packets: tokio::sync::mpsc::UnboundedSender<Packet<Bytes>>,
    },
}

/// Parameters for establishing a TLS connection.
pub struct ConnectionTransportTlsConfig(pub(crate) SslConnectorBuilder);

impl ConnectionTransportTlsConfig {
    /// Constructs a [`ConnectionTransportTlsConfig`] with the given client certificate and CA trust bundle.
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

impl From<SslConnectorBuilder> for ConnectionTransportTlsConfig {
    fn from(connector: SslConnectorBuilder) -> Self {
        Self(connector)
    }
}

#[derive(Clone, Copy, Debug, Eq, PartialEq)]
pub enum KeepAliveConfig {
    Infinite,
    Duration {
        ping_after: NonZeroU16,
        response_timeout: Duration,
    },
}

impl From<KeepAliveConfig> for KeepAlive {
    fn from(value: KeepAliveConfig) -> Self {
        match value {
            KeepAliveConfig::Infinite => KeepAlive::Infinite,
            KeepAliveConfig::Duration {
                ping_after,
                response_timeout,
            } => KeepAlive::Duration(ping_after),
        }
    }
}

// TODO: I don't like the naming of this as Client.
// MQTTHandle? Sender? OperationsInterface? Outgoing?

/// Sends outgoing data.
#[derive(Clone)]
#[allow(clippy::struct_field_names)]
pub struct Client {
    // NOTE: We use different channels for publishes vs. control packets to allow for
    // prioritization of operations by the receiver.
    /// Channel that transmits outgoing PUBLISH requests at QoS 0
    pub_qos0_tx: tokio::sync::mpsc::Sender<PublishRequestQoS0<Bytes>>,
    /// Channel that transmits outgoing PUBLISH requests as QoS 1 or 2
    pub_qos12_tx: tokio::sync::mpsc::Sender<PublishRequestQoS1QoS2<Bytes>>,
    /// Channel that transmits outgoing SUBSCRIBE/UNSUBSCRIBE requests
    sub_tx: tokio::sync::mpsc::Sender<SubscriptionRequest<Bytes>>,
}

impl Client {
    /// Sends a PUBLISH packet to the broker at QoS 0.
    ///
    /// Returns a token that can be awaited for confirmation of the PUBLISH being sent.
    pub async fn publish_qos0(
        &self,
        topic_name: TopicName,
        payload: Bytes,
        retain: bool,
        properties: PublishProperties,
    ) -> Result<PublishQoS0CompletionToken, DetachedError> {
        let (notifier, token) = completion_pair();
        self.pub_qos0_tx
            .send(PublishRequestQoS0(
                notifier,
                topic_name.into_inner().into(),
                payload,
                retain,
                properties.into(),
            ))
            .await
            .map_err(|_| DetachedError {})?;
        Ok(PublishQoS0CompletionToken(token))
    }

    /// Sends a PUBLISH packet to the broker at QoS 1
    ///
    /// Returns a token that can be awaited to receive the PUBACK response packet.
    pub async fn publish_qos1(
        &self,
        topic_name: TopicName,
        payload: Bytes,
        retain: bool,
        properties: PublishProperties,
    ) -> Result<PublishQoS1CompletionToken, DetachedError> {
        let (notifier, token) = completion_pair();
        self.pub_qos12_tx
            .send(PublishRequestQoS1QoS2::PublishQoS1(
                notifier,
                topic_name.into_inner().into(),
                payload,
                retain,
                properties.into(),
            ))
            .await
            .map_err(|_| DetachedError {})?;
        Ok(PublishQoS1CompletionToken(token))
    }

    /// Sends a PUBLISH packet to the broker at QoS 2
    ///
    /// Returns a token that can be awaited to receive the PUBREC response packet and optionally a
    /// `PubRelToken` for sending a PUBREL packet if the PUBREC response indicates a success.
    pub async fn publish_qos2(
        &self,
        topic_name: TopicName,
        payload: Bytes,
        retain: bool,
        properties: PublishProperties,
    ) -> Result<PublishQoS2CompletionToken, DetachedError> {
        let (notifier, token) = completion_pair();
        self.pub_qos12_tx
            .send(PublishRequestQoS1QoS2::PublishQoS2(
                notifier,
                topic_name.into_inner().into(),
                payload,
                retain,
                properties.into(),
            ))
            .await
            .map_err(|_| DetachedError {})?;
        Ok(PublishQoS2CompletionToken(token))
    }

    /// Send a SUBSCRIBE packet to the broker.
    ///
    /// Returns a token that can be awaited to receive the SUBACK response packet.
    pub async fn subscribe(
        &self,
        topic_filter: TopicFilter,
        max_qos: QoS,
        no_local: bool,
        retain_options: RetainOptions,
        properties: SubscribeProperties,
    ) -> Result<SubscribeCompletionToken, DetachedError> {
        let (notifier, token) = completion_pair();

        let options = mqtt_proto::SubscribeOptions {
            maximum_qos: max_qos.into(),
            other_properties: mqtt_proto::SubscribeOptionsOtherProperties {
                no_local,
                retain_as_published: retain_options.retain_as_published,
                retain_handling: retain_options.retain_handling,
            },
        };

        self.sub_tx
            .send(SubscriptionRequest::Subscribe(
                notifier,
                topic_filter.into_inner().into(),
                options,
                properties.into(),
            ))
            .await
            .map_err(|_| DetachedError {})?;
        Ok(SubscribeCompletionToken(token))
    }

    /// Send an UNSUBSCRIBE packet to the broker.
    ///
    /// Returns a token that can be awaited to receive the UNSUBACK response packet.
    pub async fn unsubscribe(
        &self,
        topic_filter: TopicFilter,
        properties: UnsubscribeProperties,
    ) -> Result<UnsubscribeCompletionToken, DetachedError> {
        let (notifier, token) = completion_pair();
        self.sub_tx
            .send(SubscriptionRequest::Unsubscribe(
                notifier,
                topic_filter.into_inner().into(),
                properties.into(),
            ))
            .await
            .map_err(|_| DetachedError {})?;
        Ok(UnsubscribeCompletionToken(token))
    }
}

/// Receives incoming Application Messages as `Publish`es.
pub struct Receiver {
    /// Channel for receiving incoming PUBLISH packets
    rx: tokio::sync::mpsc::UnboundedReceiver<IncomingPublishAndToken<Bytes>>,
}

impl Receiver {
    /// Receive an incoming `Publish`, and any `AckToken` that may be associated with it.
    ///
    /// `AckToken` will only be present if the Publish has a QoS of 1 or 2.
    ///
    /// Receiving None indicates that the client has been dropped, and no more messages will be received.
    pub async fn recv(&mut self) -> Option<(Publish, ManualAcknowledgement)> {
        self.rx.recv().await.map(Into::into)
    }
}

/// Handle providing MQTT CONNECT functionality.
pub struct ConnectHandle {
    session: Session<BytesMut>,
    reader_pool: BytesPool,
    writer_pool: BytesPool,
    cfg_client_id: Option<String>,
}

impl ConnectHandle {
    /// Connect to an MQTT server using standard authentication.
    ///
    /// Returns a [`ConnectResult`] indicating the status of the connection attempt,
    /// and any further handles needed to operate the connection or re-attempt.
    ///
    /// # Arguments
    /// - `connection_transport`: Configuration for the transport to use for the connection.
    /// - `clean_start`: Whether to request a new MQTT session from the broker
    /// - `keep_alive`: Keep-alive configuration for the connection.
    /// - `will`: Optional Last Will and Testament to be sent on unexpected disconnect.
    /// - `username`: Optional username for authentication.
    /// - `password`: Optional password for authentication.
    /// - `properties`: Properties to include in the CONNECT packet.
    /// - `response_timeout`: Optional timeout for the MQTT CONNECT operation.
    #[allow(clippy::too_many_arguments)] // Reducing the number of arguments creates semantic confusion
    pub async fn connect(
        mut self,
        connection_transport: ConnectionTransportConfig,
        clean_start: bool,
        keep_alive: KeepAliveConfig,
        will: Option<Will>,
        username: Option<String>,
        password: Option<Bytes>,
        properties: ConnectProperties,
        response_timeout: Option<Duration>,
    ) -> ConnectResult {
        let (mut reader, mut writer) = match self.transport_connect(connection_transport).await {
            Ok(streams) => streams,
            Err(err) => {
                return ConnectResult::Failure(self, err.into());
            }
        };

        if let Err(err) = self
            .mqtt_connect(
                &mut writer,
                clean_start,
                keep_alive.into(),
                will,
                username,
                password,
                properties,
                None,
            )
            .await
        {
            return ConnectResult::Failure(self, err);
        }

        let connack = match maybe_timeout(response_timeout, mqtt_receive(&mut reader)).await {
            Ok(Ok(Packet::ConnAck(connack))) => {
                if !connack.is_success() {
                    return ConnectResult::Failure(self, ConnectError::Rejected(connack.into()));
                }
                connack
            }
            Ok(Ok(_)) => {
                return ConnectResult::Failure(
                    self,
                    ConnectError::Protocol(ProtocolErrorRepr::UnexpectedPacket.into()),
                );
            }
            Ok(Err(err)) => return ConnectResult::Failure(self, err.into()),
            Err(_) => return ConnectResult::Failure(self, ConnectError::ResponseTimeout),
        };

        self.session
            .incoming_connack(connack.clone(), keep_alive.into());

        let (disconnect_tx, disconnect_rx) = tokio::sync::oneshot::channel();
        self.session.ch.disconnect_rx = Some(disconnect_rx);
        let cfg_pingresp_timeout = match keep_alive {
            KeepAliveConfig::Duration {
                ping_after,
                response_timeout,
            } => Some(response_timeout),
            KeepAliveConfig::Infinite => None,
        };
        ConnectResult::Success(
            Connection {
                session: self.session,
                reader_pool: self.reader_pool,
                writer_pool: self.writer_pool,
                reader,
                writer,
                cfg_client_id: self.cfg_client_id,
                cfg_pingresp_timeout,
            },
            connack.into(),
            DisconnectHandle(disconnect_tx),
        )
    }

    /// Connect to an MQTT server using enhanced authentication.
    ///
    /// Returns a [`ConnectEnhancedAuthResult`] indicating the status of the connection attempt,
    /// and any further handles needed to operate the connection, continue the authentication
    /// process, or re-attempt.
    ///
    /// # Arguments
    /// - `connection_transport`: Configuration for the transport to use for the connection.
    /// - `clean_start`: Whether to request a new MQTT session from the broker
    /// - `keep_alive`: Keep-alive configuration for the connection.
    /// - `will`: Optional Last Will and Testament to be sent on unexpected disconnect.
    /// - `username`: Optional username for authentication.
    /// - `password`: Optional password for authentication.
    /// - `properties`: Properties to include in the CONNECT packet.
    /// - `authentication_info`: Initial authentication information for enhanced authentication.
    /// - `response_timeout`: Optional timeout for the MQTT CONNECT operation.
    #[allow(clippy::too_many_arguments)] // Reducing the number of arguments creates semantic confusion
    pub async fn connect_enhanced_auth(
        mut self,
        connection_transport: ConnectionTransportConfig,
        clean_start: bool,
        keep_alive: KeepAliveConfig,
        will: Option<Will>,
        username: Option<String>,
        password: Option<Bytes>,
        properties: ConnectProperties,
        authentication_info: AuthenticationInfo,
        response_timeout: Option<Duration>,
    ) -> ConnectEnhancedAuthResult {
        let auth_method = authentication_info.method.clone();
        let (mut reader, mut writer) = match self.transport_connect(connection_transport).await {
            Ok(streams) => streams,
            Err(err) => return ConnectEnhancedAuthResult::Failure(self, err.into()),
        };
        if let Err(err) = self
            .mqtt_connect(
                &mut writer,
                clean_start,
                keep_alive.into(),
                will,
                username,
                password,
                properties,
                Some(authentication_info),
            )
            .await
        {
            return ConnectEnhancedAuthResult::Failure(self, err);
        }

        let packet = match maybe_timeout(response_timeout, mqtt_receive(&mut reader)).await {
            Ok(Ok(packet)) => packet,
            Ok(Err(err)) => return ConnectEnhancedAuthResult::Failure(self, err.into()),
            Err(_) => {
                return ConnectEnhancedAuthResult::Failure(self, ConnectError::ResponseTimeout);
            }
        };

        match packet {
            Packet::ConnAck(connack) => {
                self.session
                    .incoming_connack(connack.clone(), keep_alive.into());
                if connack.is_success() {
                    let (disconnect_tx, disconnect_rx) = tokio::sync::oneshot::channel();
                    let auth_tx = self.session.ch.auth_tx.clone();
                    self.session.ch.disconnect_rx = Some(disconnect_rx);
                    let cfg_pingresp_timeout = match keep_alive {
                        KeepAliveConfig::Duration {
                            ping_after,
                            response_timeout,
                        } => Some(response_timeout),
                        KeepAliveConfig::Infinite => None,
                    };
                    ConnectEnhancedAuthResult::Success(
                        Connection {
                            session: self.session,
                            reader_pool: self.reader_pool,
                            writer_pool: self.writer_pool,
                            reader,
                            writer,
                            cfg_client_id: self.cfg_client_id,
                            cfg_pingresp_timeout,
                        },
                        connack.into(),
                        DisconnectHandle(disconnect_tx),
                        ReauthHandle {
                            method: auth_method,
                            tx: auth_tx,
                        },
                    )
                } else {
                    ConnectEnhancedAuthResult::Failure(self, ConnectError::Rejected(connack.into()))
                }
            }

            Packet::Auth(auth) => {
                let auth_handle = EnhancedAuthHandle {
                    session: self.session,
                    reader_pool: self.reader_pool,
                    writer_pool: self.writer_pool,
                    reader,
                    writer,
                    auth_method,
                    cfg_client_id: self.cfg_client_id,
                    cfg_keep_alive: keep_alive,
                };
                ConnectEnhancedAuthResult::Continue(auth.into(), auth_handle)
            }

            _ => ConnectEnhancedAuthResult::Failure(
                self,
                ConnectError::Protocol(ProtocolErrorRepr::UnexpectedPacket.into()),
            ),
        }
    }

    async fn transport_connect(
        &self,
        transport_config: ConnectionTransportConfig,
    ) -> io::Result<(Reader<BytesPool>, Writer<BytesPool>)> {
        let ConnectionTransportConfig {
            transport_type,
            timeout,
        } = transport_config;
        Ok(match transport_type {
            ConnectionTransportType::Tcp { hostname, port } => {
                maybe_timeout(
                    timeout,
                    crate::azure_mqtt::io::tokio_tcp::connect(
                        (hostname, port),
                        &self.reader_pool,
                        &self.writer_pool,
                    ),
                )
                .await??
            }

            ConnectionTransportType::Tls {
                hostname,
                port,
                config,
            } => {
                maybe_timeout(
                    timeout,
                    crate::azure_mqtt::io::tokio_tls::connect(
                        &hostname,
                        port,
                        config,
                        &self.reader_pool,
                        &self.writer_pool,
                    ),
                )
                .await??
            }

            #[cfg(feature = "test-utils")]
            ConnectionTransportType::Test {
                incoming_packets,
                outgoing_packets,
            } => crate::azure_mqtt::io::test::connect(
                incoming_packets,
                outgoing_packets,
                &self.reader_pool,
                &self.writer_pool,
            ),
        })
    }

    #[allow(clippy::too_many_arguments)]
    async fn mqtt_connect(
        &self,
        writer: &mut Writer<BytesPool>,
        clean_start: bool,
        keep_alive: KeepAlive,
        will: Option<Will>,
        username: Option<String>,
        password: Option<Bytes>,
        properties: ConnectProperties,
        authentication_info: Option<AuthenticationInfo>,
    ) -> Result<(), ConnectError> {
        // Transport has been established. Send CONNECT and wait for CONNACK.

        let mut properties: mqtt_proto::ConnectOtherProperties<Bytes> = properties.into();
        properties.authentication = authentication_info.map(Into::into);

        let connect = Packet::Connect(Connect {
            username: username.as_deref().map(Into::into),
            password: password.as_deref().map(Into::into),
            will: will.map(Into::into),
            client_id: self.cfg_client_id.as_deref().map(Into::into),
            clean_start,
            keep_alive,
            other_properties: properties,
        });
        writer.write(&connect, ProtocolVersion::V5).await?;
        writer.flush().await?;
        Ok(())
    }
}

impl std::fmt::Debug for ConnectHandle {
    fn fmt(&self, f: &mut std::fmt::Formatter<'_>) -> std::fmt::Result {
        f.debug_struct("ConnectHandle").finish_non_exhaustive()
    }
}

/// Handle for the intermediate step of an MQTT CONNECT with enhanced authentication.
pub struct EnhancedAuthHandle {
    session: Session<BytesMut>,
    reader_pool: BytesPool,
    writer_pool: BytesPool,
    reader: Reader<BytesPool>,
    writer: Writer<BytesPool>,
    auth_method: String,
    cfg_client_id: Option<String>,
    cfg_keep_alive: KeepAliveConfig,
}

impl EnhancedAuthHandle {
    pub async fn continue_auth(
        mut self,
        authentication_data: Option<Bytes>,
        properties: AuthProperties,
        response_timeout: Option<Duration>,
    ) -> ConnectEnhancedAuthResult {
        // Send auth
        let auth = Packet::Auth(
            Auth {
                reason: AuthReason::ContinueAuthentication,
                authentication_info: Some(AuthenticationInfo {
                    method: self.auth_method.clone(),
                    data: authentication_data,
                }),
                properties,
            }
            .into(),
        );
        if let Err(err) = self.writer.write(&auth, ProtocolVersion::V5).await {
            let connect_handle = ConnectHandle {
                session: self.session,
                reader_pool: self.reader_pool,
                writer_pool: self.writer_pool,
                cfg_client_id: self.cfg_client_id,
            };
            return ConnectEnhancedAuthResult::Failure(connect_handle, err.into());
        }
        if let Err(err) = self.writer.flush().await {
            let connect_handle = ConnectHandle {
                session: self.session,
                reader_pool: self.reader_pool,
                writer_pool: self.writer_pool,
                cfg_client_id: self.cfg_client_id,
            };
            return ConnectEnhancedAuthResult::Failure(connect_handle, err.into());
        }

        // Wait for next response
        let packet = match maybe_timeout(response_timeout, mqtt_receive(&mut self.reader)).await {
            Ok(Ok(packet)) => packet,
            Ok(Err(err)) => {
                let connect_handle = ConnectHandle {
                    session: self.session,
                    reader_pool: self.reader_pool,
                    writer_pool: self.writer_pool,
                    cfg_client_id: self.cfg_client_id,
                };
                return ConnectEnhancedAuthResult::Failure(connect_handle, err.into());
            }
            Err(_) => {
                let connect_handle = ConnectHandle {
                    session: self.session,
                    reader_pool: self.reader_pool,
                    writer_pool: self.writer_pool,
                    cfg_client_id: self.cfg_client_id,
                };
                return ConnectEnhancedAuthResult::Failure(
                    connect_handle,
                    ConnectError::ResponseTimeout,
                );
            }
        };

        match packet {
            Packet::ConnAck(connack) => {
                self.session
                    .incoming_connack(connack.clone(), self.cfg_keep_alive.into());

                if connack.is_success() {
                    let (disconnect_tx, disconnect_rx) = tokio::sync::oneshot::channel();
                    let auth_tx = self.session.ch.auth_tx.clone();
                    self.session.ch.disconnect_rx = Some(disconnect_rx);
                    let cfg_pingresp_timeout = match self.cfg_keep_alive {
                        KeepAliveConfig::Duration {
                            ping_after,
                            response_timeout,
                        } => Some(response_timeout),
                        KeepAliveConfig::Infinite => None,
                    };
                    let connection = Connection {
                        session: self.session,
                        reader_pool: self.reader_pool,
                        writer_pool: self.writer_pool,
                        reader: self.reader,
                        writer: self.writer,
                        cfg_client_id: self.cfg_client_id,
                        cfg_pingresp_timeout,
                    };
                    ConnectEnhancedAuthResult::Success(
                        connection,
                        connack.into(),
                        DisconnectHandle(disconnect_tx),
                        ReauthHandle {
                            method: self.auth_method.clone(),
                            tx: auth_tx,
                        },
                    )
                } else {
                    let connect_handle = ConnectHandle {
                        session: self.session,
                        reader_pool: self.reader_pool,
                        writer_pool: self.writer_pool,
                        cfg_client_id: self.cfg_client_id,
                    };
                    ConnectEnhancedAuthResult::Failure(
                        connect_handle,
                        ConnectError::Rejected(connack.into()),
                    )
                }
            }

            Packet::Auth(auth) => ConnectEnhancedAuthResult::Continue(auth.into(), self),

            _ => {
                let connect_handle = ConnectHandle {
                    session: self.session,
                    reader_pool: self.reader_pool,
                    writer_pool: self.writer_pool,
                    cfg_client_id: self.cfg_client_id,
                };
                ConnectEnhancedAuthResult::Failure(
                    connect_handle,
                    ConnectError::Protocol(ProtocolErrorRepr::UnexpectedPacket.into()),
                )
            }
        }
    }
}

/// Runs the MQTT client event loop, keeping the client operational.
pub struct Connection {
    session: Session<BytesMut>,
    reader_pool: BytesPool,
    writer_pool: BytesPool,
    reader: Reader<BytesPool>,
    writer: Writer<BytesPool>,
    cfg_client_id: Option<String>,
    cfg_pingresp_timeout: Option<Duration>,
}

impl Connection {
    /// Drives this connection until it is disconnected.
    /// Packets will only be sent and received while this future is running.
    pub async fn run_until_disconnect(mut self) -> (ConnectHandle, DisconnectedEvent) {
        let event = match self.run_until_disconnect_inner().await {
            Ok(InnerDisconnect::Application) => DisconnectedEvent::ApplicationDisconnect,
            Ok(InnerDisconnect::Server(disconnect)) => {
                DisconnectedEvent::ServerDisconnect(disconnect)
            }
            Ok(InnerDisconnect::PingTimeout) => DisconnectedEvent::PingTimeout,
            Err(InnerConnectionError::Io(e)) => DisconnectedEvent::IoError(e),
            Err(InnerConnectionError::Protocol(e)) => DisconnectedEvent::ProtocolError(e),
        };
        let connect_handle = ConnectHandle {
            session: self.session,
            reader_pool: self.reader_pool,
            writer_pool: self.writer_pool,
            cfg_client_id: self.cfg_client_id,
        };
        // NOTE: By returning here, we implicitly drop the `reader` and `writer` stored on the
        // `Connection`, implicitly closing the underlying transport.
        (connect_handle, event)
    }

    async fn run_until_disconnect_inner(
        &mut self,
    ) -> Result<InnerDisconnect, InnerConnectionError> {
        let (reader, writer) = (&mut self.reader, &mut self.writer);
        let mut pingresp_timer: Option<Timer> = None;

        loop {
            // Check for outgoing packets from the session or incoming packets from the reader.
            let next = {
                let next_outgoing_packet_f = pin!(self.session.next_outgoing_packet());
                let read_f = pin!(mqtt_receive(reader));
                let io_f = future::select(next_outgoing_packet_f, read_f);

                // If there is a ping timer, use its remaining duration as a timeout for the I/O future.
                let timeout = pingresp_timer.as_ref().map(Timer::remaining_duration);
                match maybe_timeout(timeout, io_f).await {
                    Ok(future::Either::Left((packet, _))) => {
                        log::trace!("OUTGOING: {packet:?}");
                        future::Either::Left(packet)
                    }
                    Ok(future::Either::Right((Ok(raw_packet), _))) => {
                        log::trace!("INCOMING: {raw_packet:?}");
                        future::Either::Right(Ok(raw_packet))
                    }
                    Ok(future::Either::Right((Err(err), _))) => future::Either::Right(Err(err)),
                    Err(_) => return Ok(InnerDisconnect::PingTimeout),
                }
            };
            match next {
                // Outgoing packet from session
                future::Either::Left(packet) => {
                    let mut disconnect = false;
                    let mut op_packet = Some(packet);
                    while let Some(packet_) = op_packet {
                        if let Packet::Disconnect(disconnect_) = &packet_ {
                            disconnect = true;
                            self.session.client_disconnect(disconnect_);
                        }
                        if let Packet::PingReq(_) = &packet_
                            && let Some(timeout) = self.cfg_pingresp_timeout
                        {
                            pingresp_timer = Some(Timer::new(timeout));
                        }
                        writer.write(&packet_, ProtocolVersion::V5).await?;
                        if disconnect {
                            break;
                        }
                        op_packet = self.session.next_outgoing_packet().now_or_never();
                    }
                    writer.flush().await?;
                    // If we wrote a DISCONNECT packet, also close the connection.
                    if disconnect {
                        return Ok(InnerDisconnect::Application);
                    }
                }

                // Incoming packet from reader
                future::Either::Right(Ok(packet)) => match packet {
                    Packet::Auth(auth) => self.session.incoming_auth(auth)?,

                    Packet::SubAck(suback) => self
                        .session
                        .complete_inflight(CompletedOperation::Subscribe(suback))?,

                    Packet::UnsubAck(unsuback) => self
                        .session
                        .complete_inflight(CompletedOperation::Unsubscribe(unsuback))?,

                    Packet::PubAck(puback) => self
                        .session
                        .complete_inflight(CompletedOperation::PublishQoS1(puback))?,

                    Packet::PubRec(pubrec) => self
                        .session
                        .complete_inflight(CompletedOperation::PublishQoS2(pubrec))?,

                    Packet::Disconnect(disconnect) => {
                        self.session.server_disconnect(&disconnect);
                        return Ok(InnerDisconnect::Server(disconnect.into()));
                    }

                    Packet::Publish(publish) => self.session.incoming_publish(publish),

                    Packet::PingResp(_) => {
                        // Remove ping response timer as we have successfully received a PINGRESP.
                        pingresp_timer = None;
                    }

                    packet => {
                        let err = ProtocolError::from(ProtocolErrorRepr::UnexpectedPacket).into();
                        self.session.transport_disconnect(&err);
                        return Err(err);
                    }
                },

                future::Either::Right(Err(err)) => {
                    self.session.transport_disconnect(&err);
                    return Err(err);
                }
            }
        }
    }
}

pub struct DisconnectHandle(tokio::sync::oneshot::Sender<DisconnectRequest<Bytes>>);

impl DisconnectHandle {
    pub fn disconnect(self, properties: &DisconnectProperties) -> Result<(), DetachedError> {
        let DisconnectProperties {
            session_expiry_interval,
            reason_string,
            user_properties,
            server_reference,
        } = properties;
        let req = DisconnectRequest(crate::azure_mqtt::mqtt_proto::Disconnect {
            reason_code: DisconnectReasonCode::Normal, // TODO: Get from DisconnectProperties
            other_properties: DisconnectOtherProperties {
                session_expiry_interval: *session_expiry_interval,
                reason_string: reason_string.as_deref().map(Into::into),
                user_properties: user_properties
                    .iter()
                    .map(|(key, value)| (key.as_str().into(), value.as_str().into()))
                    .collect(),
                server_reference: server_reference.as_deref().map(Into::into),
            },
        });

        self.0.send(req).map_err(|_| DetachedError {})
    }
}

// TODO: Determine where some of these auth structures should live, and what a token vs. handle is semantically.

pub struct ReauthHandle {
    method: String,
    tx: tokio::sync::mpsc::Sender<ReauthRequest<Bytes>>,
}

impl ReauthHandle {
    pub async fn reauth(
        &self,
        authentication_data: Option<Bytes>,
        properties: AuthProperties,
    ) -> Result<ReauthCompletionToken, DetachedError> {
        let (notifier, token) = completion_pair();
        let auth = Auth {
            reason: AuthReason::Reauthenticate,
            authentication_info: Some(AuthenticationInfo {
                method: self.method.clone(),
                data: authentication_data,
            }),
            properties,
        };
        self.tx
            .send(ReauthRequest(notifier, auth.into()))
            .await
            .map_err(|_| DetachedError {})?;
        Ok(ReauthCompletionToken(token))
    }
}

/// Indicates the result of an MQTT CONNECT.
pub enum ConnectResult {
    Success(Connection, ConnAck, DisconnectHandle),
    Failure(ConnectHandle, ConnectError),
}

/// Indicates the result of an MQTT CONNECT with enhanced authentication.
pub enum ConnectEnhancedAuthResult {
    Continue(Auth, EnhancedAuthHandle),
    Success(Connection, ConnAck, DisconnectHandle, ReauthHandle),
    Failure(ConnectHandle, ConnectError),
}

/// Indicates the result of an MQTT AUTH operation on an existing connection.
#[derive(Debug)]
pub enum ReauthResult {
    Continue(Auth, ReauthToken),
    Success(Auth),
    Failure, // Cannot provide Disconnect packet here because it is not guaranteed to be sent by server
}

impl From<buffered::ReauthResult<Bytes>> for ReauthResult {
    fn from(value: buffered::ReauthResult<Bytes>) -> Self {
        match value {
            buffered::ReauthResult::Continue(auth, token) => {
                Self::Continue(auth.into(), ReauthToken(token))
            }
            buffered::ReauthResult::Success(auth) => Self::Success(auth.into()),
            buffered::ReauthResult::Failure => Self::Failure,
        }
    }
}

/// Details about a client disconnect
#[derive(Debug)]
pub enum DisconnectedEvent {
    ApplicationDisconnect,
    ServerDisconnect(Disconnect),
    IoError(io::Error),
    ProtocolError(ProtocolError),
    PingTimeout,
}

/// Internal error type for propagating connection errors
#[derive(Error, Debug)]
#[error(transparent)]
pub(crate) enum InnerConnectionError {
    Io(#[from] io::Error),
    Protocol(#[from] ProtocolError),
}

impl From<InnerConnectionError> for ConnectError {
    fn from(err: InnerConnectionError) -> Self {
        match err {
            InnerConnectionError::Io(err) => Self::Io(err),
            InnerConnectionError::Protocol(err) => Self::Protocol(err),
        }
    }
}

/// Internal enum for distinguishing disconnect types
enum InnerDisconnect {
    Application,
    Server(Disconnect),
    PingTimeout,
}

pub enum ManualAcknowledgement {
    QoS0,
    QoS1(PubAckToken),
    QoS2(PubRecToken),
}

impl From<channel_data::IncomingPublishAndToken<Bytes>> for (Publish, ManualAcknowledgement) {
    fn from(inner: channel_data::IncomingPublishAndToken<Bytes>) -> Self {
        match inner {
            channel_data::IncomingPublishAndToken::QoS0(publish) => {
                (publish.into(), ManualAcknowledgement::QoS0)
            }
            channel_data::IncomingPublishAndToken::QoS1(publish, token) => (
                publish.into(),
                ManualAcknowledgement::QoS1(PubAckToken(token)),
            ),
            channel_data::IncomingPublishAndToken::QoS2(publish, token) => (
                publish.into(),
                ManualAcknowledgement::QoS2(PubRecToken(token)),
            ),
        }
    }
}

async fn maybe_timeout<F>(
    timeout: Option<Duration>,
    f: F,
) -> Result<F::Output, tokio::time::error::Elapsed>
where
    F: Future,
{
    match timeout {
        Some(timeout) => tokio::time::timeout(timeout, f).await,
        None => Ok(f.await),
    }
}

async fn mqtt_receive(
    reader: &mut Reader<BytesPool>,
) -> Result<Packet<Bytes>, InnerConnectionError> {
    let mut raw_packet = reader.read().await?;
    let packet = Packet::decode(
        raw_packet.first_byte,
        &mut raw_packet.rest,
        ProtocolVersion::V5,
    )
    .map_err(|e| ProtocolError::from(ProtocolErrorRepr::from(e)))?;
    Ok(packet)
}

mod buffered {
    use crate::azure_mqtt::buffer_pool::Shared;
    use crate::azure_mqtt::client::token::reauth::buffered::ReauthToken;
    use crate::azure_mqtt::mqtt_proto::Auth;

    /// Indicates the result of an MQTT AUTH operation on an existing connection.
    #[derive(Debug)]
    pub enum ReauthResult<S>
    where
        S: Shared,
    {
        Continue(Auth<S>, ReauthToken<S>),
        Success(Auth<S>),
        Failure, // Cannot provide Disconnect packet here because it is not guaranteed to be sent by server
    }
}
