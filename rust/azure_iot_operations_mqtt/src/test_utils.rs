// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

#![allow(clippy::missing_panics_doc)]

//! Utilities for testing MQTT operations by injecting and capturing packets.
//! Note that these test utilities are provided AS IS without any guarantee of stability

use std::path::{Path, PathBuf};
use std::sync::{Arc, Mutex};
use std::time::Duration;

use crate::azure_mqtt::mqtt_proto;
use bytes::Bytes;
use futures::FutureExt;
use rand::Rng;
use tempfile::TempDir;
use tokio::sync::{
    Notify,
    mpsc::{UnboundedReceiver, UnboundedSender},
};

use crate::control_packet::AuthenticationInfo;
use crate::error::ConnectError;
use crate::session::{
    enhanced_auth_policy::EnhancedAuthPolicy,
    reconnect_policy::{ConnectionLossReason, ReconnectPolicy},
};

/// Generate random bytes of length between 1 and 256 for testing purposes
#[must_use]
pub fn random_bytes() -> Bytes {
    let mut rng = rand::thread_rng();
    let len: usize = rng.gen_range(1..=256);
    let mut buf = vec![0u8; len];
    rng.fill(&mut buf[..]);
    Bytes::from(buf)
}

/// Struct containing channels for injecting incoming packets and capturing outgoing packets
/// for testing purposes.
#[derive(Clone)]
pub struct InjectedPacketChannels {
    /// Channel transmitter for injecting incoming packets to the session
    pub incoming_packets_tx: IncomingPacketsTx,
    /// Channel receiver for capturing outgoing packets from the session
    pub outgoing_packets_rx: OutgoingPacketsRx,
}

/// Wrapper around incoming packets channel transmitter for test purposes to allow
/// tests to not need to coordinate new channels on each connect attempt
#[derive(Clone)]
pub struct IncomingPacketsTx {
    incoming_packets_tx: Arc<Mutex<UnboundedSender<mqtt_proto::Packet<Bytes>>>>,
}

impl Default for IncomingPacketsTx {
    fn default() -> Self {
        // session side is dropped immediately, so this will act like a normal disconnect until connected
        let (incoming_packets_tx, _) = tokio::sync::mpsc::unbounded_channel();
        IncomingPacketsTx {
            incoming_packets_tx: Arc::new(Mutex::new(incoming_packets_tx)),
        }
    }
}

impl IncomingPacketsTx {
    /// Send a test packet as an incoming packet
    /// If the connection is currently disconnected, this will log an error and the message won't be sent
    pub fn send(&self, packet: mqtt_proto::Packet<Bytes>) {
        // NOTE: this will fail to send if the connection is currently disconnected and the rx has been dropped
        // Not handling this for now, because that would cause this fn to have to be async or return an error
        // METL tests currently don't try to send incoming packets while disconnected
        let res = self.incoming_packets_tx.lock().unwrap().send(packet);
        if res.is_err() {
            log::error!("Currently disconnected, so failed to send incoming test packet");
        }
    }

    /// Used to swap out the underlying channel on new connects
    pub(crate) fn set_new_tx(&self, new_tx: UnboundedSender<mqtt_proto::Packet<Bytes>>) {
        let mut curr_tx = self.incoming_packets_tx.lock().unwrap();
        *curr_tx = new_tx;
    }
}

/// Wrapper around outgoing packets channel receiver for test purposes to allow
/// tests to not need to coordinate new channels on each connect attempt
#[derive(Clone)]
pub struct OutgoingPacketsRx {
    outgoing_packets_rx: Arc<Mutex<UnboundedReceiver<mqtt_proto::Packet<Bytes>>>>,
}

impl Default for OutgoingPacketsRx {
    fn default() -> Self {
        // session side is dropped immediately, so this will act like a normal disconnect until connected
        let (_, outgoing_packets_rx) = tokio::sync::mpsc::unbounded_channel();
        OutgoingPacketsRx {
            outgoing_packets_rx: Arc::new(Mutex::new(outgoing_packets_rx)),
        }
    }
}

impl OutgoingPacketsRx {
    /// Receive next outgoing packet for testing
    /// If the connection is currently disconnected, this will wait until reconnected and the next packet is available
    pub async fn recv(&self) -> Option<mqtt_proto::Packet<Bytes>> {
        // recv() will return an Err() if we're currently disconnected or there's nothing in the channel.
        // Wait for next packet or reconnection with a delay to allow the mutex to be released, so that
        // connection changes can take the mutex while we wait for the next packet (either a
        // new packet being sent or the next packet after reconnection)
        loop {
            if let Ok(packet) = self.outgoing_packets_rx.lock().unwrap().try_recv() {
                return Some(packet);
            }
            tokio::time::sleep(Duration::from_millis(100)).await;
        }
    }

    /// Used to swap out the underlying channel on new connects
    /// NOTE: We could keep a clone of the tx and return it instead of swapping the underlying rx.
    /// This would remove the possibility of the tx ever being closed. However, we still need the
    /// rx to be under an Arc<Mutex> to allow cloning the [`OutgoingPacketsRx`] struct, so this seems simpler for now.
    pub(crate) fn set_new_rx(&self, new_rx: UnboundedReceiver<mqtt_proto::Packet<Bytes>>) {
        let mut curr_rx = self.outgoing_packets_rx.lock().unwrap();
        *curr_rx = new_rx;
    }
}

/// Mock MQTT server for testing purposes
pub struct MockServer {
    to_client_tx: IncomingPacketsTx,
    from_client_rx: OutgoingPacketsRx,
}

impl MockServer {
    /// Create a new `MockServer`
    #[must_use]
    pub fn new(to_client_tx: IncomingPacketsTx, from_client_rx: OutgoingPacketsRx) -> MockServer {
        MockServer {
            to_client_tx,
            from_client_rx,
        }
    }

    /// Panic if the next packet received is not a CONNECT packet.
    /// Return the received CONNECT packet for further inspection.
    /// Send a CONNACK packet with Success reason code in response, with the provided
    /// `session_present` flag.
    pub async fn expect_connect_and_accept(
        &self,
        session_present: bool,
    ) -> mqtt_proto::Connect<Bytes> {
        self.expect_connect_and_respond(mqtt_proto::ConnAck {
            reason_code: mqtt_proto::ConnectReasonCode::Success { session_present },
            other_properties: mqtt_proto::ConnAckOtherProperties::default(),
        })
        .await
    }

    /// Panic if the next packet received is not a CONNECT packet.
    /// Send the provided CONNACK packet in response.
    pub async fn expect_connect_and_respond(
        &self,
        connack: mqtt_proto::ConnAck<Bytes>,
    ) -> mqtt_proto::Connect<Bytes> {
        match self.from_client_rx.recv().await {
            Some(mqtt_proto::Packet::Connect(connect)) => {
                self.to_client_tx.send(mqtt_proto::Packet::ConnAck(connack));
                connect
            }
            Some(other) => {
                panic!("Expected CONNECT packet, but received different packet: {other:?}",);
            }
            None => {
                panic!("Expected CONNECT packet, but connection was closed");
            }
        }
    }

    /// Panic if the next packet received is not a CONNECT packet.
    pub async fn expect_connect(&self) -> mqtt_proto::Connect<Bytes> {
        match self.from_client_rx.recv().await {
            Some(mqtt_proto::Packet::Connect(connect)) => connect,
            Some(other) => {
                panic!("Expected CONNECT packet, but received different packet: {other:?}",);
            }
            None => {
                panic!("Expected CONNECT packet, but connection was closed");
            }
        }
    }

    /// Panic if the next packet received is not a DISCONNECT packet.
    /// Return the received DISCONNECT packet for further inspection.
    pub async fn expect_disconnect(&self) -> mqtt_proto::Disconnect<Bytes> {
        match self.from_client_rx.recv().await {
            Some(mqtt_proto::Packet::Disconnect(disconnect)) => disconnect,
            Some(other) => {
                panic!("Expected DISCONNECT packet, but received different packet: {other:?}",);
            }
            None => {
                panic!("Expected DISCONNECT packet, but connection was closed");
            }
        }
    }

    /// Panic if the next packet received is not a SUBSCRIBE packet.
    /// Send a SUBACK packet granting the requested QoS in response.
    pub async fn expect_subscribe_and_accept(&self) {
        match self.from_client_rx.recv().await {
            Some(mqtt_proto::Packet::Subscribe(subscribe)) => {
                //let granted_qos = match subscribe.
                let rc_vec = subscribe
                    .subscribe_to
                    .iter()
                    .map(|st| match st.options.maximum_qos {
                        mqtt_proto::QoS::AtMostOnce => mqtt_proto::SubscribeReasonCode::GrantedQoS0,
                        mqtt_proto::QoS::AtLeastOnce => {
                            mqtt_proto::SubscribeReasonCode::GrantedQoS1
                        }
                        mqtt_proto::QoS::ExactlyOnce => {
                            mqtt_proto::SubscribeReasonCode::GrantedQoS2
                        }
                    })
                    .collect();

                self.to_client_tx
                    .send(mqtt_proto::Packet::SubAck(mqtt_proto::SubAck {
                        packet_identifier: subscribe.packet_identifier,
                        reason_codes: rc_vec,
                        other_properties: mqtt_proto::SubAckOtherProperties::default(),
                    }));
            }
            Some(other) => {
                panic!("Expected SUBSCRIBE packet, but received different packet: {other:?}",);
            }
            None => {
                panic!("Expected SUBSCRIBE packet, but connection was closed");
            }
        }
    }

    /// Panic if the next packet received is not a PUBACK packet.
    /// Return the received PUBACK packet for further inspection.
    pub async fn expect_puback(&self) -> mqtt_proto::PubAck<Bytes> {
        match self.from_client_rx.recv().await {
            Some(mqtt_proto::Packet::PubAck(puback)) => puback,
            Some(other) => {
                panic!("Expected PUBACK packet, but received different packet: {other:?}",);
            }
            None => {
                panic!("Expected PUBACK packet, but connection was closed");
            }
        }
    }

    /// Panic if the next packet received is not an AUTH packet.
    /// Return the received AUTH packet for further inspection.
    pub async fn expect_auth_and_accept(&self) -> mqtt_proto::Auth<Bytes> {
        match self.from_client_rx.recv().await {
            Some(mqtt_proto::Packet::Auth(auth)) => {
                self.to_client_tx
                    .send(mqtt_proto::Packet::Auth(mqtt_proto::Auth {
                        reason_code: mqtt_proto::AuthenticateReasonCode::Success,
                        authentication: None, // TODO: is this right?
                        reason_string: None,
                        user_properties: vec![],
                    }));
                auth
            }
            Some(other) => {
                panic!("Expected AUTH packet, but received different packet: {other:?}",);
            }
            None => {
                panic!("Expected AUTH packet, but connection was closed");
            }
        }
    }

    /// Panic if the next packet received is not an AUTH packet.
    pub async fn expect_auth(&self) -> mqtt_proto::Auth<Bytes> {
        match self.from_client_rx.recv().await {
            Some(mqtt_proto::Packet::Auth(auth)) => auth,
            Some(other) => {
                panic!("Expected AUTH packet, but received different packet: {other:?}",);
            }
            None => {
                panic!("Expected AUTH packet, but connection was closed");
            }
        }
    }

    /// Panic if any packet is ready to be received
    #[allow(clippy::manual_assert)]
    pub fn expect_no_packet(&self) {
        if let Some(Some(packet)) = self.from_client_rx.recv().now_or_never() {
            panic!("Expected no packet, but received a packet: {packet:?}");
        }
    }

    /// Send a CONNACK packet to the client
    pub fn send_connack(&self, connack: mqtt_proto::ConnAck<Bytes>) {
        self.to_client_tx.send(mqtt_proto::Packet::ConnAck(connack));
    }

    /// Send a PUBLISH packet to the client
    pub fn send_publish(&self, publish: mqtt_proto::Publish<Bytes>) {
        self.to_client_tx.send(mqtt_proto::Packet::Publish(publish));
    }

    /// Send a DISCONNECT packet to the client
    pub fn send_disconnect(&self, disconnect: mqtt_proto::Disconnect<Bytes>) {
        self.to_client_tx
            .send(mqtt_proto::Packet::Disconnect(disconnect));
    }

    /// Send an AUTH packet to the client
    pub fn send_auth(&self, auth: mqtt_proto::Auth<Bytes>) {
        self.to_client_tx.send(mqtt_proto::Packet::Auth(auth));
    }
}

/// Mock SAT file for testing purposes
pub struct MockSatFile {
    /// Parent directory for the SAT file
    /// Keep this here even though it's unused to ensure the temp dir isn't deleted
    _parent_dir: TempDir,
    /// Path to the SAT file
    file_path: PathBuf,
}

impl MockSatFile {
    /// Create a new mock SAT file for testing purposes
    #[must_use]
    pub fn new() -> Self {
        // Create parent directory as SAT files have their own dir
        let dir = TempDir::new().unwrap();
        // Create mock SAT file inside the dir
        let fp = dir.path().join("sat_file.sat");
        // Write arbitrary mock contents to the SAT file
        let mut buf = Vec::new();
        fill_utf8(&mut buf, 16);
        std::fs::write(&fp, buf).unwrap();

        MockSatFile {
            _parent_dir: dir,
            file_path: fp,
        }
    }

    /// Get the path to the mock SAT file
    #[must_use]
    pub fn path(&self) -> &Path {
        self.file_path.as_path()
    }

    /// Get the string representation of the path to the mock SAT file
    #[must_use]
    pub fn path_as_str(&self) -> &str {
        self.file_path.to_str().unwrap()
    }

    /// Update the contents of the mock SAT file with arbitrary bytes
    /// This can be used to trigger reauthentication in tests.
    pub fn update_contents(&self) {
        let mut buf = Vec::new();
        fill_utf8(&mut buf, 16);
        std::fs::write(&self.file_path, buf).unwrap();
    }
}

impl Default for MockSatFile {
    fn default() -> Self {
        Self::new()
    }
}

/// Fill the provided buffer with random UTF-8 characters up to the specified length
fn fill_utf8(buf: &mut Vec<u8>, len: usize) {
    let mut rng = rand::thread_rng();
    buf.clear();
    for _ in 0..len {
        let c: char = rng.gen_range(0x20u32..0x10_FFFF).try_into().unwrap_or('�');
        buf.extend(c.to_string().as_bytes());
    }
}

/// Mock reconnect policy for testing purposes
pub struct MockReconnectPolicy {
    connect_failure_notify: Arc<Notify>,
    connection_loss_notify: Arc<Notify>,
    default_delay: Option<Duration>,
    next_delay: Arc<Mutex<Option<Duration>>>,
    manual_mode: Arc<Mutex<bool>>,
}

impl MockReconnectPolicy {
    /// Create a new `MockReconnectPolicy` and its controller
    #[must_use]
    pub fn new() -> (Self, MockReconnectPolicyController) {
        let connect_failure_notify = Arc::new(Notify::new());
        let connection_loss_notify = Arc::new(Notify::new());
        let manual_mode = Arc::new(Mutex::new(false));
        let next_delay = Arc::new(Mutex::new(None));

        let rp_controller = MockReconnectPolicyController {
            connect_failure_notify: connect_failure_notify.clone(),
            connection_loss_notify: connection_loss_notify.clone(),
            manual_mode: manual_mode.clone(),
            next_delay: next_delay.clone(),
        };

        let rp = Self {
            connect_failure_notify,
            connection_loss_notify,
            default_delay: Some(Duration::from_secs(1)),
            next_delay,
            manual_mode,
        };

        (rp, rp_controller)
    }
}

impl ReconnectPolicy for MockReconnectPolicy {
    fn connect_failure_reconnect_delay(
        &self,
        _prev_attempts: u32,
        _error: &ConnectError,
    ) -> Option<Duration> {
        // NOTE: only notifies those already waiting, so make sure to wait before triggering this
        self.connect_failure_notify.notify_waiters();

        if *self.manual_mode.lock().unwrap() {
            *self.next_delay.lock().unwrap()
        } else {
            self.default_delay
        }
    }

    fn connection_loss_reconnect_delay(&self, _reason: &ConnectionLossReason) -> Option<Duration> {
        // NOTE: only notifies those already waiting, so make sure to wait before triggering this
        self.connection_loss_notify.notify_waiters();

        if *self.manual_mode.lock().unwrap() {
            *self.next_delay.lock().unwrap()
        } else {
            self.default_delay
        }
    }
}

/// Controller for the mock reconnect policy to allow tests to control its behavior
#[derive(Clone)]
pub struct MockReconnectPolicyController {
    connect_failure_notify: Arc<Notify>,
    connection_loss_notify: Arc<Notify>,
    manual_mode: Arc<Mutex<bool>>,
    next_delay: Arc<Mutex<Option<Duration>>>,
}

impl MockReconnectPolicyController {
    /// Enable or disable manual mode for the mock reconnect policy
    pub fn manual_mode(&self, active: bool) {
        *self.manual_mode.lock().unwrap() = active;
    }

    /// Wait until a connect failure reconnect delay is requested
    /// Note that you must already be waiting on this future before triggering the reconnect delay
    pub async fn connect_failure_notified(&self) {
        self.connect_failure_notify.notified().await;
    }

    /// Wait until a connection loss reconnect delay is requested
    /// Note that you must already be waiting on this future before triggering the reconnect delay
    pub async fn connection_loss_notified(&self) {
        self.connection_loss_notify.notified().await;
    }

    /// Set the next reconnect delay to return from the mock reconnect policy
    pub fn set_next_delay(&self, delay: Option<Duration>) {
        *self.next_delay.lock().unwrap() = delay;
    }

    // TODO: It would be useful to have a way to know the details of the disconnect in the
    // notification
}

/// Mock enhanced auth policy for testing purposes
pub struct MockEnhancedAuthPolicy {
    method: String,
    auth_info_data: Arc<Mutex<Option<Bytes>>>,
    auth_challenge_data: Arc<Mutex<Option<Bytes>>>,
    reauth_data: Arc<Mutex<Option<Bytes>>>,
    reauth_notify: Arc<Notify>,
}

impl MockEnhancedAuthPolicy {
    /// Create a new `MockEnhancedAuthPolicy` and its controller
    #[must_use]
    pub fn new() -> (Self, MockEnhancedAuthPolicyController) {
        let ap_controller = MockEnhancedAuthPolicyController {
            auth_info_data: Arc::new(Mutex::new(Some(random_bytes()))),
            auth_challenge_data: Arc::new(Mutex::new(Some(random_bytes()))),
            reauth_data: Arc::new(Mutex::new(Some(random_bytes()))),
            reauth_notify: Arc::new(Notify::new()),
        };

        let ap = MockEnhancedAuthPolicy {
            method: "mock_method".to_string(),
            auth_info_data: ap_controller.auth_info_data.clone(),
            auth_challenge_data: ap_controller.auth_challenge_data.clone(),
            reauth_data: ap_controller.reauth_data.clone(),
            reauth_notify: ap_controller.reauth_notify.clone(),
        };

        (ap, ap_controller)
    }
}

#[async_trait::async_trait]
impl EnhancedAuthPolicy for MockEnhancedAuthPolicy {
    fn authentication_info(&self) -> AuthenticationInfo {
        AuthenticationInfo {
            method: self.method.clone(),
            data: self.auth_info_data.lock().unwrap().clone(),
        }
    }

    fn auth_challenge(&self, _auth: &crate::control_packet::Auth) -> Option<Bytes> {
        self.auth_challenge_data.lock().unwrap().clone()
    }

    async fn reauth_notified(&self) -> Option<Bytes> {
        self.reauth_notify.notified().await;
        self.reauth_data.lock().unwrap().clone()
    }
}

/// Controller for the mock enhanced auth policy to allow tests to control its behavior
pub struct MockEnhancedAuthPolicyController {
    auth_info_data: Arc<Mutex<Option<Bytes>>>,
    auth_challenge_data: Arc<Mutex<Option<Bytes>>>,
    reauth_data: Arc<Mutex<Option<Bytes>>>,
    reauth_notify: Arc<Notify>,
}

impl MockEnhancedAuthPolicyController {
    /// Get the method to be returned in the `method` field of the `AuthenticationInfo` struct
    #[must_use]
    pub fn method(&self) -> &'static str {
        "mock_method"
    }

    /// Get the data to be returned in the `data` field of the `AuthenticationInfo` struct
    #[must_use]
    pub fn auth_info_data(&self) -> Option<Bytes> {
        self.auth_info_data.lock().unwrap().clone()
    }

    /// Set the data to be returned in the `data` field of the `AuthenticationInfo` struct
    /// returned by the `authentication_info()` method of the `MockEnhancedAuthPolicy`
    pub fn set_auth_info_data(&self, data: Option<Bytes>) {
        *self.auth_info_data.lock().unwrap() = data;
    }

    /// Get the data to be returned by the `auth_challenge()` method of the `MockEnhancedAuthPolicy`
    #[must_use]
    pub fn auth_challenge_data(&self) -> Option<Bytes> {
        self.auth_challenge_data.lock().unwrap().clone()
    }

    /// Set the data to be returned by the `auth_challenge()` method of the `MockEnhancedAuthPolicy`
    pub fn set_auth_challenge_data(&self, data: Option<Bytes>) {
        *self.auth_challenge_data.lock().unwrap() = data;
    }

    /// Get the data to be returned by the `reauth_notified()` method of the `MockEnhancedAuthPolicy`
    #[must_use]
    pub fn reauth_data(&self) -> Option<Bytes> {
        self.reauth_data.lock().unwrap().clone()
    }

    /// Set the data to be returned by the `reauth_notified()` method of the `MockEnhancedAuthPolicy`
    pub fn set_reauth_data(&self, data: Option<Bytes>) {
        *self.reauth_data.lock().unwrap() = data;
    }

    /// Trigger the reauthentication notification indicating the `reauth_notified()` method of the
    /// `MockEnhancedAuthPolicy` should return data.
    pub fn reauth_notify(&self) {
        self.reauth_notify.notify_waiters();
    }
}
