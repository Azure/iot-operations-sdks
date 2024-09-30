// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

//! Envoys for State Store operations.

use core::fmt::Debug;

use azure_iot_operations_protocol::{
    common::{aio_protocol_error::AIOProtocolError, hybrid_logical_clock::HybridLogicalClock},
    rpc::command_invoker::CommandResponse,
};
use thiserror::Error;

mod client;
mod resp3;

pub use client::Client;
pub use resp3::{SetCondition, SetOptions};

#[derive(Debug, Error)]
#[error(transparent)]
pub struct StateStoreError(#[from] StateStoreErrorKind);

#[derive(Error, Debug)]
pub enum StateStoreErrorKind {
    #[error(transparent)]
    AIOProtocolError(#[from] AIOProtocolError),
    #[error(transparent)]
    ServiceError(#[from] ServiceError),
    #[error("key length must not be zero")]
    KeyLengthZero,
    #[error("{0}")]
    SerializationError(String),
    #[error("{0}")]
    InvalidArgument(String),
}

#[derive(Error, Debug)]
pub enum ServiceError {
    #[error("the requested timestamp is too far in the future; ensure that the client and broker system clocks are synchronized")]
    TimestampSkew,
    #[error("a fencing token is required for this request")]
    MissingFencingToken,
    #[error("the requested fencing token timestamp is too far in the future; ensure that the client and broker system clocks are synchronized")]
    FencingTokenSkew,
    #[error("the requested fencing token is a lower version that the fencing token protecting the resource")]
    FencingTokenLowerVersion,
    #[error("the quota has been exceeded")]
    QuotaExceeded,
    #[error("syntax error")]
    SyntaxError,
    #[error("not authorized")]
    NotAuthorized,
    #[error("unknown command")]
    UnknownCommand,
    #[error("wrong number of arguments")]
    WrongNumberOfArguments,
    #[error("missing timestamp")]
    TimestampMissing,
    #[error("malformed timestamp")]
    TimestampMalformed,
    #[error("the key length is zero")]
    KeyLengthZero,
    #[error("{0}")]
    Unknown(String),
}

impl From<Vec<u8>> for ServiceError {
    fn from(s: Vec<u8>) -> Self {
        let s_bytes: &[u8] = &s;
        match s_bytes {
            b"the requested timestamp is too far in the future; ensure that the client and broker system clocks are synchronized" => ServiceError::TimestampSkew,
            b"a fencing token is required for this request" => ServiceError::MissingFencingToken,
            b"the requested fencing token timestamp is too far in the future; ensure that the client and broker system clocks are synchronized" => ServiceError::FencingTokenSkew,
            b"the requested fencing token is a lower version that the fencing token protecting the resource" => ServiceError::FencingTokenLowerVersion,
            b"the quota has been exceeded" => ServiceError::QuotaExceeded,
            b"syntax error" => ServiceError::SyntaxError,
            b"not authorized" => ServiceError::NotAuthorized,
            b"unknown command" => ServiceError::UnknownCommand,
            b"wrong number of arguments" => ServiceError::WrongNumberOfArguments,
            b"missing timestamp" => ServiceError::TimestampMissing,
            b"malformed timestamp" => ServiceError::TimestampMalformed,
            b"the key length is zero" => ServiceError::KeyLengthZero,
            other => ServiceError::Unknown(std::str::from_utf8(other).unwrap_or_default().to_string()),
        }
    }
}

#[derive(Debug)]
pub struct Response<T>
where
    T: Debug,
{
    pub version: Option<HybridLogicalClock>,
    pub response: T,
}

fn convert_response<T, F>(
    resp: CommandResponse<resp3::Response>,
    f: F,
) -> Result<Response<T>, StateStoreError>
where
    F: FnOnce(resp3::Response) -> Result<T, ()>,
    T: Debug,
{
    match resp.payload {
        resp3::Response::Error(e) => Err(std::convert::Into::into(
            StateStoreErrorKind::ServiceError(e.into()),
        )),
        payload => match f(payload.clone()) {
            Ok(response) => Ok(Response {
                response,
                version: resp.timestamp,
            }),
            Err(()) => Err(std::convert::Into::into(StateStoreErrorKind::ServiceError(
                ServiceError::Unknown(format!("Unexpected response payload: {payload:?}")),
            ))),
        },
    }
}
