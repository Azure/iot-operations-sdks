// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

//! Utilities for testing MQTT operations by injecting and capturing packets.

use std::sync::{Arc, Mutex};
use std::time::Duration;

use bytes::Bytes;
use tokio::sync::mpsc::{UnboundedReceiver, UnboundedSender};

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
    incoming_packets_tx: Arc<Mutex<UnboundedSender<azure_mqtt::mqtt_proto::Packet<Bytes>>>>,
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
    #[allow(clippy::missing_panics_doc)]
    pub fn send(&self, packet: azure_mqtt::mqtt_proto::Packet<Bytes>) {
        // NOTE: this will fail to send if the connection is currently disconnected and the rx has been dropped
        // Not handling this for now, because that would cause this fn to have to be async or return an error
        // METL tests currently don't try to send incoming packets while disconnected
        let res = self.incoming_packets_tx.lock().unwrap().send(packet);
        if res.is_err() {
            log::error!("Currently disconnected, so failed to send incoming test packet");
        }
    }

    /// Used to swap out the underlying channel on new connects
    pub(crate) fn set_new_tx(
        &self,
        new_tx: UnboundedSender<azure_mqtt::mqtt_proto::Packet<Bytes>>,
    ) {
        let mut curr_tx = self.incoming_packets_tx.lock().unwrap();
        *curr_tx = new_tx;
    }
}

/// Wrapper around outgoing packets channel receiver for test purposes to allow
/// tests to not need to coordinate new channels on each connect attempt
#[derive(Clone)]
pub struct OutgoingPacketsRx {
    outgoing_packets_rx: Arc<Mutex<UnboundedReceiver<azure_mqtt::mqtt_proto::Packet<Bytes>>>>,
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
    #[allow(clippy::missing_panics_doc)]
    pub async fn recv(&self) -> Option<azure_mqtt::mqtt_proto::Packet<Bytes>> {
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
    pub(crate) fn set_new_rx(
        &self,
        new_rx: UnboundedReceiver<azure_mqtt::mqtt_proto::Packet<Bytes>>,
    ) {
        let mut curr_rx = self.outgoing_packets_rx.lock().unwrap();
        *curr_rx = new_rx;
    }
}
