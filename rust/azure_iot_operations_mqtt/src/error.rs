// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

//! Common error types

use crate::control_packet::Request;
use thiserror::Error;

/// Error type for MQTT connection
pub type ConnectionError = rumqttc::v5::ConnectionError;
/// Error type for completion tokens
pub type CompletionError = rumqttc::NoticeError;
/// Error subtype for MQTT connection error caused by state
pub type StateError = rumqttc::v5::StateError;

// WAIT CAN THIS JUST PUT A PUBLISH IN THE DETACHED CLIENT ERROR?
// Should these be separate errors structs? I think yes

// #[derive(Debug, Error)]
// pub struct PublishError {
//     #[from]
//     kind: PublishErrorKind,
// }

// impl PublishError {
//     // pub fn new(kind: PublishErrorKind, publish: Publish) -> Self {
//     //     Self { publish, kind }
//     // }

//     pub fn kind(&self) -> &PublishErrorKind {
//         &self.kind
//     }
// }

// /// Error executing an MQTT publish
// #[derive(Debug, Error)]
// pub enum PublishErrorKind {
//     /// Client is detached from connection/event loop. Cannot send requests.
//     #[error("client is detached from connection/event loop")]
//     DetachedClient,
//     /// Invalid topic name provided
//     #[error("invalid topic name")]
//     InvalidTopicName,
// }

/// Error executing an MQTT publish
#[derive(Debug, Error)]
pub enum PublishError {
    /// Client is detached from connection/event loop. Cannot send requests.
    #[error("client is detached from connection/event loop")]
    DetachedClient(Request),
    /// Invalid topic name provided
    #[error("invalid topic name")]
    InvalidTopicName,
}

/// Error executing an MQTT subscribe
#[derive(Debug, Error)]
pub enum SubscribeError {
    /// Client is detached from connection/event loop. Cannot send requests.
    #[error("client is detached from connection/event loop")]
    DetachedClient(Request),
    /// Invalid topic filter provided
    #[error("invalid topic filter")]
    InvalidTopicFilter,
}

/// Error executing an MQTT unsubscribe
#[derive(Debug, Error)]
pub enum UnsubscribeError {
    /// Client is detached from connection/event loop. Cannot send requests.
    #[error("client is detached from connection/event loop")]
    DetachedClient(Request),
    /// Invalid topic filter provided
    #[error("invalid topic filter")]
    InvalidTopicFilter,
}

/// Error executing acknowledging an MQTT publish
#[derive(Debug, Error, Clone)]
pub enum AckError {
    /// Client is detached from connection/event loop. Cannot send requests.
    #[error("client is detached from connection/event loop")]
    DetachedClient(Request),
    /// The publish has already been sufficiently acknowledged
    #[error("publish already acknowledged")]
    AlreadyAcked,
}

/// Error executing an MQTT disconnect
#[derive(Debug, Error)]
pub enum DisconnectError {
    /// Client is detached from connection/event loop. Cannot send requests.
    #[error("client is detached from connection/event loop")]
    DetachedClient(Request),
}

/// Error executing an MQTT reauthentication
#[derive(Debug, Error)]
pub enum ReauthError {
    /// Client is detached from connection/event loop. Cannot send requests.
    #[error("client is detached from connection/event loop")]
    DetachedClient(Request),
}
