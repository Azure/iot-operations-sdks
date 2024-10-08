// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

//! Client for State Store operations.

use std::time::Duration;

use azure_iot_operations_mqtt::interface::ManagedClient;
use azure_iot_operations_protocol::{
    common::hybrid_logical_clock::HybridLogicalClock,
    rpc::command_invoker::{CommandInvoker, CommandInvokerOptionsBuilder, CommandRequestBuilder},
};

use crate::state_store::{self, SetOptions, StateStoreError, StateStoreErrorKind};

const REQUEST_TOPIC_PATTERN: &str =
    "statestore/v1/FA9AE35F-2F64-47CD-9BFF-08E2B32A0FE8/command/invoke";
const RESPONSE_TOPIC_PREFIX: &str = "clients/{invokerClientId}/services";
const RESPONSE_TOPIC_SUFFIX: &str = "response";
const COMMAND_NAME: &str = "invoke";

pub struct Client<C>
where
    C: ManagedClient + Clone + Send + Sync + 'static,
    C::PubReceiver: Send + Sync,
{
    command_invoker: CommandInvoker<state_store::resp3::Request, state_store::resp3::Response, C>,
    // notification_receiver: TelemetryReceiver<state_store::resp3::Operation, C>,
}

impl<C> Client<C>
where
    C: ManagedClient + Clone + Send + Sync,
    C::PubReceiver: Send + Sync,
{
    /// Create a new State Store Client
    /// # Errors
    /// [`StateStoreError`] of kind [`AIOProtocolError`](StateStoreErrorKind::AIOProtocolError) is possible if
    ///     there are any errors creating the underlying command invoker or telemetry receiver, but it should not happen
    ///
    /// # Panics
    /// Possible panics when building options for the underlying command invoker or telemetry receiver,
    /// but they should be unreachable because we control the static parameters that go into these calls.
    pub fn new(client: C) -> Result<Self, StateStoreError> {
        // create invoker for commands
        let command_invoker_options = CommandInvokerOptionsBuilder::default()
            .request_topic_pattern(REQUEST_TOPIC_PATTERN)
            .response_topic_prefix(Some(RESPONSE_TOPIC_PREFIX.into()))
            .response_topic_suffix(Some(RESPONSE_TOPIC_SUFFIX.into()))
            .command_name(COMMAND_NAME)
            .build()
            .expect("Unreachable because all parameters that could cause errors are statically provided");

        let command_invoker: CommandInvoker<
            state_store::resp3::Request,
            state_store::resp3::Response,
            C,
        > = CommandInvoker::new(client, command_invoker_options)
            .map_err(StateStoreErrorKind::from)?;

        Ok(Self {
            command_invoker,
            // notification_receiver,
        })
    }

    /// Sets a key value pair in the State Store Service
    ///
    /// Note: timeout refers to the duration until the State Store Client stops
    /// waiting for a `Set` response from the Service. This value is not linked
    /// to the key in the State Store.
    ///
    /// Returns `true` if the `Set` completed successfully, or `false` if the `Set` did not occur because of values specified in `SetOptions`
    /// # Errors
    /// [`StateStoreError`] of kind [`KeyLengthZero`](StateStoreErrorKind::KeyLengthZero) if the `key` is empty
    ///
    /// [`StateStoreError`] of kind [`InvalidArgument`](StateStoreErrorKind::InvalidArgument) if the `timeout` is < 1 ms or > `u32::max`
    ///
    /// [`StateStoreError`] of kind [`ServiceError`](StateStoreErrorKind::ServiceError) if the State Store returns an Error response
    ///
    /// [`StateStoreError`] of kind [`UnexpectedPayload`](StateStoreErrorKind::UnexpectedPayload) if the State Store returns a response that isn't valid for a `Set` request
    ///
    /// [`StateStoreError`] of kind [`AIOProtocolError`](StateStoreErrorKind::AIOProtocolError) if there are any underlying errors from [`CommandInvoker::invoke`]
    pub async fn set(
        &self,
        key: Vec<u8>,
        value: Vec<u8>,
        timeout: Duration,
        fencing_token: Option<HybridLogicalClock>,
        options: SetOptions,
    ) -> Result<state_store::Response<bool>, StateStoreError> {
        if key.is_empty() {
            return Err(StateStoreError(StateStoreErrorKind::KeyLengthZero));
        }
        let request = CommandRequestBuilder::default()
            .payload(&state_store::resp3::Request::Set {
                key,
                value,
                options: options.clone(),
            })
            .map_err(|e| StateStoreErrorKind::SerializationError(e.to_string()))? // this can't fail
            .timeout(timeout)
            .fencing_token(fencing_token)
            .build()
            .map_err(|e| StateStoreErrorKind::InvalidArgument(e.to_string()))?;
        state_store::convert_response(
            self.command_invoker
                .invoke(request)
                .await
                .map_err(StateStoreErrorKind::from)?,
            |payload| match payload {
                state_store::resp3::Response::NotApplied => Ok(false),
                state_store::resp3::Response::Ok => Ok(true),
                _ => Err(()),
            },
        )
    }

    /// Gets the value of a key in the State Store Service
    ///
    /// Note: timeout refers to the duration until the State Store Client stops
    /// waiting for a `Get` response from the Service. This value is not linked
    /// to the key in the State Store.
    ///
    /// Returns `Some(<value of the key>)` if the key is found or `None` if the key was not found
    /// # Errors
    /// [`StateStoreError`] of kind [`KeyLengthZero`](StateStoreErrorKind::KeyLengthZero) if the `key` is empty
    ///
    /// [`StateStoreError`] of kind [`InvalidArgument`](StateStoreErrorKind::InvalidArgument) if the `timeout` is < 1 ms or > `u32::max`
    ///
    /// [`StateStoreError`] of kind [`ServiceError`](StateStoreErrorKind::ServiceError) if the State Store returns an Error response
    ///
    /// [`StateStoreError`] of kind [`UnexpectedPayload`](StateStoreErrorKind::UnexpectedPayload) if the State Store returns a response that isn't valid for a `Get` request
    ///
    /// [`StateStoreError`] of kind [`AIOProtocolError`](StateStoreErrorKind::AIOProtocolError) if there are any underlying errors from [`CommandInvoker::invoke`]
    pub async fn get(
        &self,
        key: Vec<u8>,
        timeout: Duration,
    ) -> Result<state_store::Response<Option<Vec<u8>>>, StateStoreError> {
        if key.is_empty() {
            return Err(StateStoreError(StateStoreErrorKind::KeyLengthZero));
        }
        let request = CommandRequestBuilder::default()
            .payload(&state_store::resp3::Request::Get { key })
            .map_err(|e| StateStoreErrorKind::SerializationError(e.to_string()))? // this can't fail
            .timeout(timeout)
            .build()
            .map_err(|e| StateStoreErrorKind::InvalidArgument(e.to_string()))?;
        state_store::convert_response(
            self.command_invoker
                .invoke(request)
                .await
                .map_err(StateStoreErrorKind::from)?,
            |payload| match payload {
                state_store::resp3::Response::Value(value) => Ok(Some(value)),
                state_store::resp3::Response::NotFound => Ok(None),
                _ => Err(()),
            },
        )
    }

    /// Deletes a key from the State Store Service
    ///
    /// Note: timeout refers to the duration until the State Store Client stops
    /// waiting for a `Delete` response from the Service. This value is not linked
    /// to the key in the State Store.
    ///
    /// Returns the number of keys deleted. Will be `0` if the key was not found, otherwise `1`
    /// # Errors
    /// [`StateStoreError`] of kind [`KeyLengthZero`](StateStoreErrorKind::KeyLengthZero) if the `key` is empty
    ///
    /// [`StateStoreError`] of kind [`InvalidArgument`](StateStoreErrorKind::InvalidArgument) if the `timeout` is < 1 ms or > `u32::max`
    ///
    /// [`StateStoreError`] of kind [`ServiceError`](StateStoreErrorKind::ServiceError) if the State Store returns an Error response
    ///
    /// [`StateStoreError`] of kind [`UnexpectedPayload`](StateStoreErrorKind::UnexpectedPayload) if the State Store returns a response that isn't valid for a `Delete` request
    ///
    /// [`StateStoreError`] of kind [`AIOProtocolError`](StateStoreErrorKind::AIOProtocolError) if there are any underlying errors from [`CommandInvoker::invoke`]
    pub async fn del(
        &self,
        key: Vec<u8>,
        fencing_token: Option<HybridLogicalClock>,
        timeout: Duration,
    ) -> Result<state_store::Response<i64>, StateStoreError> {
        // ) -> Result<state_store::Response<bool>, StateStoreError> {
        if key.is_empty() {
            return Err(StateStoreError(StateStoreErrorKind::KeyLengthZero));
        }
        self.del_internal(
            state_store::resp3::Request::Del { key },
            fencing_token,
            timeout,
        )
        .await
    }

    /// Deletes a key from the State Store Service if and only if the value matches the one provided
    ///
    /// Note: timeout refers to the duration until the State Store Client stops
    /// waiting for a `V Delete` response from the Service. This value is not linked
    /// to the key in the State Store.
    ///
    /// Returns the number of keys deleted. Will be `0` if the key was not found, `-1` if the value did not match, otherwise `1`
    /// # Errors
    /// [`StateStoreError`] of kind [`KeyLengthZero`](StateStoreErrorKind::KeyLengthZero) if the `key` is empty
    ///
    /// [`StateStoreError`] of kind [`InvalidArgument`](StateStoreErrorKind::InvalidArgument) if the `timeout` is < 1 ms or > `u32::max`
    ///
    /// [`StateStoreError`] of kind [`ServiceError`](StateStoreErrorKind::ServiceError) if the State Store returns an Error response
    ///
    /// [`StateStoreError`] of kind [`UnexpectedPayload`](StateStoreErrorKind::UnexpectedPayload) if the State Store returns a response that isn't valid for a `V Delete` request
    ///
    /// [`StateStoreError`] of kind [`AIOProtocolError`](StateStoreErrorKind::AIOProtocolError) if there are any underlying errors from [`CommandInvoker::invoke`]
    pub async fn vdel(
        &self,
        key: Vec<u8>,
        value: Vec<u8>,
        fencing_token: Option<HybridLogicalClock>,
        timeout: Duration,
    ) -> Result<state_store::Response<i64>, StateStoreError> {
        if key.is_empty() {
            return Err(StateStoreError(StateStoreErrorKind::KeyLengthZero));
        }
        self.del_internal(
            state_store::resp3::Request::VDel { key, value },
            fencing_token,
            timeout,
        )
        .await
    }

    async fn del_internal(
        &self,
        request: state_store::resp3::Request,
        fencing_token: Option<HybridLogicalClock>,
        timeout: Duration,
    ) -> Result<state_store::Response<i64>, StateStoreError> {
        let request = CommandRequestBuilder::default()
            .payload(&request)
            .map_err(|e| StateStoreErrorKind::SerializationError(e.to_string()))? // this can't fail
            .timeout(timeout)
            .fencing_token(fencing_token)
            .build()
            .map_err(|e| StateStoreErrorKind::InvalidArgument(e.to_string()))?;
        state_store::convert_response(
            self.command_invoker
                .invoke(request)
                .await
                .map_err(StateStoreErrorKind::from)?,
            |payload| match payload {
                state_store::resp3::Response::NotFound => Ok(0),
                state_store::resp3::Response::NotApplied => Ok(-1),
                state_store::resp3::Response::ValuesDeleted(value) => Ok(value),
                _ => Err(()),
            },
        )
    }
}

#[cfg(test)]
mod tests {
    use std::time::Duration;

    // TODO: This dependency on MqttConnectionSettingsBuilder should be removed in lieu of using a true mock
    use azure_iot_operations_mqtt::session::{Session, SessionOptionsBuilder};
    use azure_iot_operations_mqtt::MqttConnectionSettingsBuilder;

    use crate::state_store::{SetOptions, StateStoreError, StateStoreErrorKind};

    // TODO: This should return a mock ManagedClient instead.
    // Until that's possible, need to return a Session so that the Session doesn't go out of
    // scope and render the ManagedClient unable to to be used correctly.
    fn create_session() -> Session {
        // TODO: Make a real mock that implements MqttProvider
        let connection_settings = MqttConnectionSettingsBuilder::default()
            .host_name("localhost")
            .client_id("test_client")
            .build()
            .unwrap();
        let session_options = SessionOptionsBuilder::default()
            .connection_settings(connection_settings)
            .build()
            .unwrap();
        Session::new(session_options).unwrap()
    }

    #[tokio::test]
    async fn test_set_empty_key() {
        let session = create_session();
        let managed_client = session.create_managed_client();
        let state_store_client = super::Client::new(managed_client).unwrap();
        let response = state_store_client
            .set(
                vec![],
                b"testValue".to_vec(),
                Duration::from_secs(1),
                None,
                SetOptions::default(),
            )
            .await;
        assert!(matches!(
            response.unwrap_err(),
            StateStoreError(StateStoreErrorKind::KeyLengthZero)
        ));
    }

    #[tokio::test]
    async fn test_get_empty_key() {
        let session = create_session();
        let managed_client = session.create_managed_client();
        let state_store_client = super::Client::new(managed_client).unwrap();
        let response = state_store_client.get(vec![], Duration::from_secs(1)).await;
        assert!(matches!(
            response.unwrap_err(),
            StateStoreError(StateStoreErrorKind::KeyLengthZero)
        ));
    }

    #[tokio::test]
    async fn test_del_empty_key() {
        let session = create_session();
        let managed_client = session.create_managed_client();
        let state_store_client = super::Client::new(managed_client).unwrap();
        let response = state_store_client
            .del(vec![], None, Duration::from_secs(1))
            .await;
        assert!(matches!(
            response.unwrap_err(),
            StateStoreError(StateStoreErrorKind::KeyLengthZero)
        ));
    }

    #[tokio::test]
    async fn test_vdel_empty_key() {
        let session = create_session();
        let managed_client = session.create_managed_client();
        let state_store_client = super::Client::new(managed_client).unwrap();
        let response = state_store_client
            .vdel(vec![], b"testValue".to_vec(), None, Duration::from_secs(1))
            .await;
        assert!(matches!(
            response.unwrap_err(),
            StateStoreError(StateStoreErrorKind::KeyLengthZero)
        ));
    }

    #[tokio::test]
    async fn test_set_invalid_timeout() {
        let session = create_session();
        let managed_client = session.create_managed_client();
        let state_store_client = super::Client::new(managed_client).unwrap();
        let response = state_store_client
            .set(
                b"testKey".to_vec(),
                b"testValue".to_vec(),
                Duration::from_nanos(50),
                None,
                SetOptions::default(),
            )
            .await;
        assert!(matches!(
            response.unwrap_err(),
            StateStoreError(StateStoreErrorKind::InvalidArgument(_))
        ));
    }

    #[tokio::test]
    async fn test_get_invalid_timeout() {
        let session = create_session();
        let managed_client = session.create_managed_client();
        let state_store_client = super::Client::new(managed_client).unwrap();
        let response = state_store_client
            .get(b"testKey".to_vec(), Duration::from_nanos(50))
            .await;
        assert!(matches!(
            response.unwrap_err(),
            StateStoreError(StateStoreErrorKind::InvalidArgument(_))
        ));
    }

    #[tokio::test]
    async fn test_del_invalid_timeout() {
        let session = create_session();
        let managed_client = session.create_managed_client();
        let state_store_client = super::Client::new(managed_client).unwrap();
        let response = state_store_client
            .del(b"testKey".to_vec(), None, Duration::from_nanos(50))
            .await;
        assert!(matches!(
            response.unwrap_err(),
            StateStoreError(StateStoreErrorKind::InvalidArgument(_))
        ));
    }

    #[tokio::test]
    async fn test_vdel_invalid_timeout() {
        let session = create_session();
        let managed_client = session.create_managed_client();
        let state_store_client = super::Client::new(managed_client).unwrap();
        let response = state_store_client
            .vdel(
                b"testKey".to_vec(),
                b"testValue".to_vec(),
                None,
                Duration::from_nanos(50),
            )
            .await;
        assert!(matches!(
            response.unwrap_err(),
            StateStoreError(StateStoreErrorKind::InvalidArgument(_))
        ));
    }
}

// TODO: Live network tests
//     - set("somekey", "somevalue", timeout, SetOptions::default())
//         - default setOptions
//             - valid new key/value
//             - valid existing key/value
//         - with/without fencing token where fencing_token required
//         - with/without fencing token where fencing_token not required
//         - with expires set (wait and then validate key can no longer be gotten?)
//         - setCondition OnlyIfDoesNotExist where key doesn't exist
//         - setCondition OnlyIfDoesNotExist where key exists
//         - setCondition OnlyIfEqualOrDoesNotExist where key exists and is equal
//         - setCondition OnlyIfEqualOrDoesNotExist where key exists and isn't equal
//         - setCondition OnlyIfEqualOrDoesNotExist where key doesn't exist and is equal
//         - setCondition OnlyIfEqualOrDoesNotExist where key doesn't exist and isn't equal
//    - get("somekey", timeout) where "somekey" exists
//         - non-existent key
//    - del
//         - valid key
//         - non-existent key
//         - with/without fencing token where fencing_token required
//         - with/without fencing token where fencing_token not required
//     - vdel
//         - valid key/value
//         - non-existent key
//         - value doesn't match
//         - with/without fencing token where fencing_token required
//         - with/without fencing token where fencing_token not required
