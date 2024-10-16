// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

//! Client for State Store operations.

use std::time::Duration;

use azure_iot_operations_mqtt::interface::ManagedClient;
use azure_iot_operations_protocol::{
    common::hybrid_logical_clock::HybridLogicalClock,
    rpc::command_invoker::{CommandInvoker, CommandInvokerOptionsBuilder, CommandRequestBuilder},
    telemetry::telemetry_receiver::{AckToken, TelemetryReceiver, TelemetryReceiverOptionsBuilder},
};

use crate::state_store::{self, SetOptions, StateStoreError, StateStoreErrorKind};

const REQUEST_TOPIC_PATTERN: &str =
    "statestore/v1/FA9AE35F-2F64-47CD-9BFF-08E2B32A0FE8/command/invoke";
const RESPONSE_TOPIC_PREFIX: &str = "clients/{invokerClientId}/services";
const RESPONSE_TOPIC_SUFFIX: &str = "response";
const COMMAND_NAME: &str = "invoke";
// where the telemetryName is an upper-case hex encoded representation of the MQTT ClientId of the client that initiated the KEYNOTIFY request and senderId is a hex encoded representation of the key that changed
const NOTIFICATION_TOPIC_PATTERN: &str =
    "clients/statestore/v1/FA9AE35F-2F64-47CD-9BFF-08E2B32A0FE8/{telemetryName}/command/notify/{senderId}";

pub struct Client<C>
where
    C: ManagedClient + Clone + Send + Sync + 'static,
    C::PubReceiver: Send + Sync,
{
    command_invoker: CommandInvoker<state_store::resp3::Request, state_store::resp3::Response, C>,
    notification_receiver: TelemetryReceiver<state_store::resp3::Operation, C>,
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
        > = CommandInvoker::new(client.clone(), command_invoker_options)
            .map_err(StateStoreErrorKind::from)?;

        // create telemetry receiver for notifications
        let telemetry_receiver_options = TelemetryReceiverOptionsBuilder::default()
            .topic_pattern(NOTIFICATION_TOPIC_PATTERN)
            .telemetry_name(client.client_id())
            .auto_ack(false) // TODO: come back to whether this should be settable
            .build()
            .expect("Unreachable because all parameters that could cause errors are statically provided");

        let notification_receiver: TelemetryReceiver<state_store::resp3::Operation, C> =
            TelemetryReceiver::new(client, telemetry_receiver_options)
                .map_err(StateStoreErrorKind::from)?;

        Ok(Self {
            command_invoker,
            notification_receiver,
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

    pub struct Observation {
        pub key: Vec<u8>,
    }
    impl Observation {
        pub fn receive_notification(&self) {
        }
        pub fn unobserve(&self) {
        }
    }

    /// Starts observation of any changes on a key from the State Store Service
    ///
    /// Returns `OK(())` if the key is now being observed
    /// # Errors
    /// [`StateStoreError`] of kind [`KeyLengthZero`](StateStoreErrorKind::KeyLengthZero) if
    /// - the `key` is empty
    ///
    /// [`StateStoreError`] of kind [`InvalidArgument`](StateStoreErrorKind::InvalidArgument) if
    /// - the `timeout` is < 1 ms or > `u32::max`
    ///
    /// [`StateStoreError`] of kind [`ServiceError`](StateStoreErrorKind::ServiceError) if
    /// - the State Store returns an Error response
    /// - the State Store returns a response that isn't valid for an `Observe` request
    ///
    /// [`StateStoreError`] of kind [`AIOProtocolError`](StateStoreErrorKind::AIOProtocolError) if
    /// - there are any underlying errors from [`CommandInvoker::invoke`]
    /// - there are any underlying errors from [`TelemetryReceiver::start`] // TODO: change this to whatever it should be
    pub async fn observe(
        &self,
        key: Vec<u8>,
        timeout: Duration,
    ) -> Result<state_store::Response<()>, StateStoreError> {
        if key.is_empty() {
            return Err(std::convert::Into::into(StateStoreErrorKind::KeyLengthZero));
        }
        // add key to list of observed keys
        // {
        //     let mut observed_keys = self.observed_keys.lock().await;
        //     if (*observed_keys).is_empty() {
        //         // Start telemetry receiver if it hasn't been started yet
        //         // sends a subscribe to notification topic filter
        //         self.notification_receiver
        //             .start()
        //             .await
        //             .map_err(StateStoreErrorKind::from)?;
        //     }
        //     (*observed_keys).insert(key.clone());
        //     // Allow other concurrent operations to acquire the observed_keys lock
        // }
        // TODO: track is_subscribed by first call to recv and make sure that is called before this
        // Send invoke request for observe
        let request = CommandRequestBuilder::default()
            .payload(&state_store::resp3::Request::KeyNotify {
                key,
                options: state_store::resp3::KeyNotifyOptions { stop: false },
            })
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
                state_store::resp3::Response::Ok => Ok(()),
                _ => Err(()),
            },
        )
    }

    /// Stops observation of any changes on a key from the State Store Service
    ///
    /// Returns `true` if the key is no longer being observed or `false` if the key wasn't being observed
    /// # Errors
    /// [`StateStoreError`] of kind [`KeyLengthZero`](StateStoreErrorKind::KeyLengthZero) if
    /// - the `key` is empty
    ///
    /// [`StateStoreError`] of kind [`InvalidArgument`](StateStoreErrorKind::InvalidArgument) if
    /// - the `timeout` is < 1 ms or > `u32::max`
    ///
    /// [`StateStoreError`] of kind [`ServiceError`](StateStoreErrorKind::ServiceError) if
    /// - the State Store returns an Error response
    /// - the State Store returns a response that isn't valid for an `Unobserve` request
    ///
    /// [`StateStoreError`] of kind [`AIOProtocolError`](StateStoreErrorKind::AIOProtocolError) if
    /// - there are any underlying errors from [`CommandInvoker::invoke`]
    /// - there are any underlying errors from [`TelemetryReceiver::stop`] // TODO: change this to whatever it should be
    pub async fn unobserve(
        &self,
        key: Vec<u8>,
        timeout: Duration,
    ) -> Result<state_store::Response<bool>, StateStoreError> {
        if key.is_empty() {
            return Err(std::convert::Into::into(StateStoreErrorKind::KeyLengthZero));
        }
        // // remove key from hashmap of observed keys
        // {
        //     let mut observed_keys = self.observed_keys.lock().await;
        //     (*observed_keys).remove(&(key));
        //     if (*observed_keys).is_empty() {
        //         // Stop telemetry receiver if there are no more observed keys
        //         // sends an unsubscribe to notification topic filter
        //         self.notification_receiver
        //             .stop()
        //             .await
        //             .map_err(StateStoreErrorKind::from)?;
        //     }
        //     // Allow other concurrent operations to acquire the observed_keys lock
        // }
        // Send invoke request for unobserve
        let request = CommandRequestBuilder::default()
            .payload(&state_store::resp3::Request::KeyNotify {
                key,
                options: state_store::resp3::KeyNotifyOptions { stop: true },
            })
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
                state_store::resp3::Response::Ok => Ok(true),
                state_store::resp3::Response::NotFound => Ok(false),
                _ => Err(()),
            },
        )
    }

    /// Receives a key notification or [`None`] if there are no more notifications.
    ///
    /// If there are notifications:
    /// - Returns Ok([`state_store::KeyNotification`], [`AckToken`]) on success
    ///     - If manual ack is enabled, the [`AckToken`] should be used or dropped when you want the ack to occur. If manual ack is disabled, you may use ([`state_store::KeyNotification`], _) to ignore the [`AckToken`].
    /// - Returns [`StateStoreError`] on error.
    ///
    /// A received notification can be acknowledged via the [`AckToken`] by calling [`AckToken::ack`] or dropping the [`AckToken`].
    pub async fn recv_notification(
        &mut self,
    ) -> Option<Result<(state_store::KeyNotification, Option<AckToken>), StateStoreError>> {
        // check if observing any keys, otherwise return error? Or just wait for a notification? Probably the latter
        // loop {
        if let Some(notification_result) = self.notification_receiver.recv().await {
            match notification_result {
                Ok((notification, ack_token)) => {
                    // if let Some(key) = notification.sender_id {
                    return Some(Ok((
                        state_store::KeyNotification {
                            key: notification.sender_id.into(),
                            operation: notification.payload,
                        },
                        ack_token,
                    )));
                    // }
                    // log::error!("Key name not present on key notification");
                }
                Err(e) => {
                    // let err = e;

                    // log::error!("Error receiving key notifications: {e}");
                    return Some(Err(state_store::StateStoreError(
                        StateStoreErrorKind::from(e),
                    )));
                }
            }
        }
        None
        // }
    }

    /// Shutdown the [`state_store::Client`]. Unsubscribes from any relevant topics.
    ///
    /// Returns Ok(()) on success, otherwise returns [`StateStoreError`].
    /// # Errors
    /// [`StateStoreError`] of kind [`AIOProtocolError`](StateStoreErrorKind::AIOProtocolError) if the unsubscribe fails or if the unsuback reason code doesn't indicate success.
    pub async fn shutdown(&mut self) -> Result<(), StateStoreError> {
        // TODO: check if subscribed first?
        self.notification_receiver
            .shutdown()
            .await
            .map_err(StateStoreErrorKind::from)?;
        // self.command_invoker.shutdown().await.map_err(StateStoreErrorKind::from)?;
        Ok(())
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
