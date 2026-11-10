// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

//! Common error types

pub use ms_mqtt_client::{
    error::{CompletionError, ConnectError, DetachedError, OperationFailure, ProtocolError},
    topic::TopicError,
};

pub use crate::session::{
    SessionConfigError, SessionError, SessionErrorKind, SessionExitError, SessionExitErrorKind,
};
