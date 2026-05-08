// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

//! Common error types

pub use crate::azure_mqtt::{
    error::{CompletionError, ConnectError, DetachedError, OperationFailure, ProtocolError},
    topic::TopicError,
};

pub use crate::session::{
    SessionConfigError, SessionError, SessionErrorKind, SessionExitError, SessionExitErrorKind,
};
