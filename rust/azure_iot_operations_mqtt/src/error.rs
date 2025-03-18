// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

//! Common error types

use std::fmt;

use thiserror::Error;

/// Error type for MQTT connection
pub type ConnectionError = rumqttc::v5::ConnectionError;
/// Error type for completion tokens
pub type CompletionError = rumqttc::NoticeError;
/// Error subtype for MQTT connection error caused by state
pub type StateError = rumqttc::v5::StateError;

// NOTE: While these errors may seem redundant and candidates for consolidation, we need this
// flexibility because the same error types are used in both the low-level and high-level APIs.
// If the Client/ManagedClient/PubReceiver traits were concretized, we could simplify this.

/// Error executing an MQTT publish
#[derive(Debug, Error, Clone)]
#[error("{kind}")]
pub struct PublishError {
    kind: PublishErrorKind,
}

impl PublishError {
    /// Create a new [`PublishError`]
    #[must_use]
    pub fn new(kind: PublishErrorKind) -> Self {
        Self { kind }
    }

    /// Return the corresponding [`PublishErrorKind`] for this error
    #[must_use]
    pub fn kind(&self) -> &PublishErrorKind {
        &self.kind
    }
}

/// An enumeration of categories of [`PublishError`]
#[derive(Clone, Copy, Debug, Eq, PartialEq)]
pub enum PublishErrorKind {
    /// Client is detached from connection/event loop. Cannot send requests.
    DetachedClient,
    /// Invalid topic name provided
    InvalidTopicName,
}

impl fmt::Display for PublishErrorKind {
    fn fmt(&self, f: &mut fmt::Formatter<'_>) -> fmt::Result {
        match self {
            PublishErrorKind::DetachedClient => {
                write!(f, "client is detached from connection/event loop")
            }
            PublishErrorKind::InvalidTopicName => write!(f, "invalid topic name"),
        }
    }
}

/// Error executing an MQTT subscribe
#[derive(Debug, Error, Clone)]
#[error("{kind}")]
pub struct SubscribeError {
    kind: SubscribeErrorKind,
}

impl SubscribeError {
    /// Create a new [`SubscribeError`]
    #[must_use]
    pub fn new(kind: SubscribeErrorKind) -> Self {
        Self { kind }
    }

    /// Return the corresponding [`SubscribeErrorKind`] for this error
    #[must_use]
    pub fn kind(&self) -> &SubscribeErrorKind {
        &self.kind
    }
}

/// An enumeration of categories of [`SubscribeError`]
#[derive(Clone, Copy, Debug, Eq, PartialEq)]
pub enum SubscribeErrorKind {
    /// Client is detached from connection/event loop. Cannot send requests.
    DetachedClient,
    /// Invalid topic filter provided
    InvalidTopicFilter,
}

impl fmt::Display for SubscribeErrorKind {
    fn fmt(&self, f: &mut fmt::Formatter<'_>) -> fmt::Result {
        match self {
            SubscribeErrorKind::DetachedClient => {
                write!(f, "client is detached from connection/event loop")
            }
            SubscribeErrorKind::InvalidTopicFilter => write!(f, "invalid topic filter"),
        }
    }
}

/// Error executing an MQTT unsubscribe
#[derive(Debug, Error, Clone)]
#[error("{kind}")]
pub struct UnsubscribeError {
    kind: UnsubscribeErrorKind,
}

impl UnsubscribeError {
    /// Create a new [`UnsubscribeError`]
    #[must_use]
    pub fn new(kind: UnsubscribeErrorKind) -> Self {
        Self { kind }
    }

    /// Return the corresponding [`UnsubscribeErrorKind`] for this error
    #[must_use]
    pub fn kind(&self) -> &UnsubscribeErrorKind {
        &self.kind
    }
}

/// An enumeration of categories of [`UnsubscribeError`]
#[derive(Clone, Copy, Debug, Eq, PartialEq)]
pub enum UnsubscribeErrorKind {
    /// Client is detached from connection/event loop. Cannot send requests.
    DetachedClient,
    /// Invalid topic filter provided
    InvalidTopicFilter,
}

impl fmt::Display for UnsubscribeErrorKind {
    fn fmt(&self, f: &mut fmt::Formatter<'_>) -> fmt::Result {
        match self {
            UnsubscribeErrorKind::DetachedClient => {
                write!(f, "client is detached from connection/event loop")
            }
            UnsubscribeErrorKind::InvalidTopicFilter => write!(f, "invalid topic filter"),
        }
    }
}

/// Error executing an MQTT ack
#[derive(Debug, Error, Clone)]
#[error("{kind}")]
pub struct AckError {
    kind: AckErrorKind,
}

impl AckError {
    /// Create a new [`AckError`]
    #[must_use]
    pub fn new(kind: AckErrorKind) -> Self {
        Self { kind }
    }

    /// Return the corresponding [`AckErrorKind`] for this error
    #[must_use]
    pub fn kind(&self) -> &AckErrorKind {
        &self.kind
    }
}

/// An enumeration of categories of [`AckError`]
#[derive(Clone, Copy, Debug, Eq, PartialEq)]
pub enum AckErrorKind {
    /// Client is detached from connection/event loop. Cannot send requests.
    DetachedClient,
    /// The publish has already been sufficiently acknowledged
    AlreadyAcked,
}

impl fmt::Display for AckErrorKind {
    fn fmt(&self, f: &mut fmt::Formatter<'_>) -> fmt::Result {
        match self {
            AckErrorKind::DetachedClient => {
                write!(f, "client is detached from connection/event loop")
            }
            AckErrorKind::AlreadyAcked => write!(f, "publish already acknowledged"),
        }
    }
}

/// Error executing an MQTT disconnect
#[derive(Debug, Error, Clone)]
#[error("{kind}")]
pub struct DisconnectError {
    kind: DisconnectErrorKind,
}

impl DisconnectError {
    /// Create a new [`DisconnectError`]
    #[must_use]
    pub fn new(kind: DisconnectErrorKind) -> Self {
        Self { kind }
    }

    /// Return the corresponding [`DisconnectErrorKind`] for this error
    #[must_use]
    pub fn kind(&self) -> &DisconnectErrorKind {
        &self.kind
    }
}

/// An enumeration of categories of [`DisconnectError`]
#[derive(Clone, Copy, Debug, Eq, PartialEq)]
pub enum DisconnectErrorKind {
    /// Client is detached from connection/event loop. Cannot send requests.
    DetachedClient,
}

impl fmt::Display for DisconnectErrorKind {
    fn fmt(&self, f: &mut fmt::Formatter<'_>) -> fmt::Result {
        match self {
            DisconnectErrorKind::DetachedClient => {
                write!(f, "client is detached from connection/event loop")
            }
        }
    }
}

/// Error executing an MQTT reauth
#[derive(Debug, Error)]
#[error("{kind}")]
pub struct ReauthError {
    kind: ReauthErrorKind,
}

impl ReauthError {
    /// Create a new [`ReauthError`]
    #[must_use]
    pub fn new(kind: ReauthErrorKind) -> Self {
        Self { kind }
    }

    /// Return the corresponding [`ReauthErrorKind`] for this error
    #[must_use]
    pub fn kind(&self) -> &ReauthErrorKind {
        &self.kind
    }
}

/// An enumeration of categories of [`ReauthError`]
#[derive(Clone, Copy, Debug, Eq, PartialEq)]
pub enum ReauthErrorKind {
    /// Client is detached from connection/event loop. Cannot send requests.
    DetachedClient,
}

impl fmt::Display for ReauthErrorKind {
    fn fmt(&self, f: &mut fmt::Formatter<'_>) -> fmt::Result {
        match self {
            ReauthErrorKind::DetachedClient => {
                write!(f, "client is detached from connection/event loop")
            }
        }
    }
}
