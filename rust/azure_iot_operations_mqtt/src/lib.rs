// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

pub use crate::connection_settings::{
    MqttConnectionSettings, MqttConnectionSettingsBuilder, MqttConnectionSettingsBuilderError,
};

mod connection_settings;
pub mod control_packet;
pub mod error;
pub mod interface;
pub mod session;
pub mod topic;

mod rumqttc_adapter;

#[macro_use]
extern crate derive_builder;

pub struct CompletionToken(pub rumqttc::NoticeFuture);

// NOTE: Ideally, this would impl Future instead, but the rumqttc NoticeFuture does not implement Future
impl CompletionToken {
    /// Wait for the ack to be received
    ///
    /// # Errors
    /// Returns a [`CompletionError`](error::CompletionError) if the response indicates the operation failed.
    pub async fn wait(self) -> Result<(), error::CompletionError> {
        self.0.wait_async().await
    }
}

//----------------------------------------------------------------------

/// Re-export rumqttc types to avoid user code taking the dependency.
// TODO: Re-implement these instead of just aliasing / add to rumqttc adapter
// Only once there are non-rumqttc implementations of these can we allow non-rumqttc compilations

/// Event yielded by the event loop
pub type Event = rumqttc::v5::Event;
/// Incoming data on the event loop
pub type Incoming = rumqttc::v5::Incoming;
/// Outgoing data on the event loop
pub type Outgoing = rumqttc::Outgoing;

//----------------------------------------------------------------------
