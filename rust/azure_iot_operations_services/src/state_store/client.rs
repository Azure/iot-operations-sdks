// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

//! Client for State Store operations.

use std::{collections::HashMap, sync::Arc, time::Duration};

use azure_iot_operations_mqtt::interface::ManagedClient;
use azure_iot_operations_protocol::{
    common::hybrid_logical_clock::HybridLogicalClock,
    rpc::command_invoker::{CommandInvoker, CommandInvokerOptionsBuilder, CommandRequestBuilder},
    telemetry::telemetry_receiver::{AckToken, TelemetryReceiver, TelemetryReceiverOptionsBuilder},
};
use data_encoding::HEXUPPER;
use derive_builder::Builder;
use tokio::{
    sync::{
        mpsc::{channel, Receiver, Sender},
        Mutex,
    },
    task,
};
use tokio_util::sync::CancellationToken;

use crate::state_store::{self, SetOptions, StateStoreError, StateStoreErrorKind};

const REQUEST_TOPIC_PATTERN: &str =
    "statestore/v1/FA9AE35F-2F64-47CD-9BFF-08E2B32A0FE8/command/invoke";
const RESPONSE_TOPIC_PREFIX: &str = "clients/{invokerClientId}/services";
const RESPONSE_TOPIC_SUFFIX: &str = "response";
const COMMAND_NAME: &str = "invoke";
// where the encodedClientId is an upper-case hex encoded representation of the MQTT ClientId of the client that initiated the KEYNOTIFY request and encodedKeyName is a hex encoded representation of the key that changed
const NOTIFICATION_TOPIC_PATTERN: &str = "clients/statestore/v1/FA9AE35F-2F64-47CD-9BFF-08E2B32A0FE8/{encodedClientId}/command/notify/{encodedKeyName}";

/// Type defined to repress clippy warning about very complex type
type ArcMutexHashmap<K, V> = Arc<Mutex<HashMap<K, V>>>;

/// A struct to manage receiving notifications for a key
#[derive(Debug)]
pub struct KeyObservation {
    /// The name of the key (for convenience)
    pub key: Vec<u8>,
    /// The internal channel for receiving notifications for this key
    receiver: Receiver<(state_store::KeyNotification, Option<AckToken>)>,
}
impl KeyObservation {
    /// Receives a [`state_store::KeyNotification`] or [`None`] if there will be no more notifications.
    ///
    /// If there are notifications:
    /// - Returns Some([`state_store::KeyNotification`], [`Option<AckToken>`]) on success
    ///     - If auto ack is disabled, the [`AckToken`] should be used or dropped when you want the ack to occur. If auto ack is enabled, you may use ([`state_store::KeyNotification`], _) to ignore the [`AckToken`].
    ///
    /// A received notification can be acknowledged via the [`AckToken`] by calling [`AckToken::ack`] or dropping the [`AckToken`].
    pub async fn recv_notification(
        &mut self,
    ) -> Option<(state_store::KeyNotification, Option<AckToken>)> {
        self.receiver.recv().await
    }

    // on drop, don't remove from hashmap so we can differentiate between a key
    // that was observed where the receiver was dropped and a key that was never observed
}

/// State Store Client Options struct
#[derive(Builder, Clone)]
#[builder(setter(into))]
pub struct ClientOptions {
    /// If true, key notifications are auto-acknowledged
    #[builder(default = "true")]
    key_notification_auto_ack: bool,
}

/// State store client implementation
pub struct Client<C>
where
    C: ManagedClient + Clone + Send + Sync + 'static,
    C::PubReceiver: Send + Sync,
{
    command_invoker: CommandInvoker<state_store::resp3::Request, state_store::resp3::Response, C>,
    observed_keys:
        ArcMutexHashmap<String, Sender<(state_store::KeyNotification, Option<AckToken>)>>,
    recv_cancellation_token: CancellationToken,
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
    #[allow(clippy::needless_pass_by_value)]
    pub fn new(client: C, options: ClientOptions) -> Result<Self, StateStoreError> {
        // create invoker for commands
        let command_invoker_options = CommandInvokerOptionsBuilder::default()
            .request_topic_pattern(REQUEST_TOPIC_PATTERN)
            .response_topic_prefix(Some(RESPONSE_TOPIC_PREFIX.into()))
            .response_topic_suffix(Some(RESPONSE_TOPIC_SUFFIX.into()))
            .topic_token_map(HashMap::from([("invokerClientId".to_string(), client.client_id().to_string())]))
            .command_name(COMMAND_NAME)
            .build()
            .expect("Unreachable because all parameters that could cause errors are statically provided");

        let command_invoker: CommandInvoker<
            state_store::resp3::Request,
            state_store::resp3::Response,
            C,
        > = CommandInvoker::new(client.clone(), command_invoker_options)
            .map_err(StateStoreErrorKind::from)?;

        // Create the uppercase hex encoded version of the client ID that is used in the key notification topic
        let encoded_client_id = HEXUPPER.encode(client.client_id().as_bytes());

        // create telemetry receiver for notifications
        let telemetry_receiver_options = TelemetryReceiverOptionsBuilder::default()
            .topic_pattern(NOTIFICATION_TOPIC_PATTERN)
            .topic_token_map(HashMap::from([(
                "encodedClientId".to_string(),
                encoded_client_id),
                ]))
            .auto_ack(options.key_notification_auto_ack)
            .build()
            .expect("Unreachable because all parameters that could cause errors are statically provided");

        // Create the cancellation token for the receiver loop
        let recv_cancellation_token = CancellationToken::new();

        // Create a hashmap of keys being observed and channels to send their notifications to
        let observed_keys = Arc::new(Mutex::new(HashMap::new()));

        // Start the receive key notification loop
        task::spawn({
            let notification_receiver: TelemetryReceiver<state_store::resp3::Operation, C> =
                TelemetryReceiver::new(client, telemetry_receiver_options)
                    .map_err(StateStoreErrorKind::from)?;
            let recv_cancellation_token_clone = recv_cancellation_token.clone();
            let observed_keys_clone = observed_keys.clone();
            async move {
                Self::receive_key_notification_loop(
                    recv_cancellation_token_clone,
                    notification_receiver,
                    observed_keys_clone,
                )
                .await;
            }
        });

        Ok(Self {
            command_invoker,
            observed_keys,
            recv_cancellation_token,
        })
    }

    // TODO: Finish implementing shutdown logic
    /// Shutdown the [`state_store::Client`]. Shuts down the command invoker and telemetry receiver
    /// and cancels the receiver loop to drop the receiver and to prevent the task from looping indefinitely.
    ///
    /// Note: If this method is called, the [`state_store::Client`] should not be used again.
    /// If the method returns an error, it may be called again to attempt the unsubscribe again.
    ///
    /// Returns Ok(()) on success, otherwise returns [`StateStoreError`].
    /// # Errors
    /// [`StateStoreError`] of kind [`AIOProtocolError`](StateStoreErrorKind::AIOProtocolError) if the unsubscribe fails or if the unsuback reason code doesn't indicate success.
    pub async fn shutdown(&self) -> Result<(), StateStoreError> {
        // Cancel the receiver loop to drop the receiver and to prevent the task from looping indefinitely
        self.recv_cancellation_token.cancel();

        self.command_invoker
            .shutdown()
            .await
            .map_err(StateStoreErrorKind::from)?;

        log::info!("Shutdown");
        Ok(())
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

    /// Internal function calling invoke for observe command to allow all errors to be captured in one place
    async fn invoke_observe(
        &self,
        key: Vec<u8>,
        timeout: Duration,
    ) -> Result<state_store::Response<()>, StateStoreError> {
        // Send invoke request for observe
        let request = CommandRequestBuilder::default()
            .payload(&state_store::resp3::Request::KeyNotify {
                key: key.clone(),
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

    /// Starts observation of any changes on a key from the State Store Service
    ///
    /// Returns OK([`state_store::Response<KeyObservation>`]) if the key is now being observed.
    /// The [`KeyObservation`] can be used to receive key notifications for this key
    ///
    /// <div class="warning">
    ///
    /// If a client disconnects, it must resend the Observe for any keys
    /// it needs to continue monitoring. Unlike MQTT subscriptions, which can be
    /// persisted across a nonclean session, the state store internally removes
    /// any key observations when a given client disconnects. This is a known
    /// limitation of the service, see [here](https://learn.microsoft.com/azure/iot-operations/create-edge-apps/concept-about-state-store-protocol#keynotify-notification-topics-and-lifecycle)
    /// for more information
    ///
    /// </div>
    ///
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
    pub async fn observe(
        &self,
        key: Vec<u8>,
        timeout: Duration,
    ) -> Result<state_store::Response<KeyObservation>, StateStoreError> {
        if key.is_empty() {
            return Err(std::convert::Into::into(StateStoreErrorKind::KeyLengthZero));
        }

        // add to observed keys before sending command to prevent missing any notifications.
        // If the observe request fails, this entry will be removed before the function returns
        let encoded_key_name = HEXUPPER.encode(&key);
        let (tx, rx) = channel(100);

        {
            let mut observed_keys_mutex_guard = self.observed_keys.lock().await;

            match observed_keys_mutex_guard.get_mut(&encoded_key_name) {
                Some(sender) if sender.is_closed() => {
                    // KeyObservation has been dropped, so we can give out a new receiver
                }
                Some(_) => {
                    log::info!("key already is being observed");
                    return Err(StateStoreError(StateStoreErrorKind::DuplicateObserve));
                }
                None => {
                    // There is no KeyObservation for this key, so we can create it
                }
            }
            log::info!("inserting key into observed list {encoded_key_name:?}");
            observed_keys_mutex_guard.insert(encoded_key_name.clone(), tx);
            // release the observed_keys_mutex_guard
        }

        // Capture any errors from the command invoke so we can remove the key from the observed_keys hashmap
        match self.invoke_observe(key.clone(), timeout).await {
            Ok(r) => Ok(state_store::Response {
                response: KeyObservation { key, receiver: rx },
                version: r.version,
            }),
            Err(e) => {
                // if the observe request wasn't successful, remove it from our internal map of observed keys
                let mut observed_keys_mutex_guard = self.observed_keys.lock().await;
                if observed_keys_mutex_guard
                    .remove(&encoded_key_name)
                    .is_some()
                {
                    log::debug!("key removed from observed list: {encoded_key_name:?}");
                } else {
                    log::debug!("key not in observed list: {encoded_key_name:?}");
                }
                Err(e)
            }
        }
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
    pub async fn unobserve(
        &self,
        key: Vec<u8>,
        timeout: Duration,
    ) -> Result<state_store::Response<bool>, StateStoreError> {
        if key.is_empty() {
            return Err(std::convert::Into::into(StateStoreErrorKind::KeyLengthZero));
        }
        // Send invoke request for unobserve
        let request = CommandRequestBuilder::default()
            .payload(&state_store::resp3::Request::KeyNotify {
                key: key.clone(),
                options: state_store::resp3::KeyNotifyOptions { stop: true },
            })
            .map_err(|e| StateStoreErrorKind::SerializationError(e.to_string()))? // this can't fail
            .timeout(timeout)
            .build()
            .map_err(|e| StateStoreErrorKind::InvalidArgument(e.to_string()))?;
        match state_store::convert_response(
            self.command_invoker
                .invoke(request)
                .await
                .map_err(StateStoreErrorKind::from)?,
            |payload| match payload {
                state_store::resp3::Response::Ok => Ok(true),
                state_store::resp3::Response::NotFound => Ok(false),
                _ => Err(()),
            },
        ) {
            Ok(r) => {
                // remove key from observed_keys hashmap
                let encoded_key_name = HEXUPPER.encode(&key);

                let mut observed_keys_mutex_guard = self.observed_keys.lock().await;
                if observed_keys_mutex_guard
                    .remove(&encoded_key_name)
                    .is_some()
                {
                    log::debug!("key removed from observed list: {key:?}");
                } else {
                    log::debug!("key not in observed list: {key:?}");
                }
                Ok(r)
            }
            Err(e) => Err(e),
        }
    }

    async fn receive_key_notification_loop(
        recv_cancellation_token: CancellationToken,
        mut telemetry_receiver: TelemetryReceiver<state_store::resp3::Operation, C>,
        observed_keys: ArcMutexHashmap<
            String,
            Sender<(state_store::KeyNotification, Option<AckToken>)>,
        >,
    ) {
        loop {
            tokio::select! {
                  // on shutdown, this cancellation token will be called so this
                  // loop can exit and the telemetry receiver can be cleaned up
                  () = recv_cancellation_token.cancelled() => {
                    break;
                  },
                  msg = telemetry_receiver.recv() => {
                    if let Some(m) = msg {
                        match m {
                            Ok((notification, ack_token)) => {
                                let Some(key_name) = notification.topic_tokens.get("encodedKeyName") else {
                                    log::error!("Key Notification missing encodedKeyName topic token.");
                                    continue;
                                };
                                let decoded_key_name = HEXUPPER.decode(key_name.as_bytes()).unwrap();
                                let Some(notification_timestamp) = notification.timestamp else {
                                    log::error!("Received key notification with no version. Ignoring.");
                                    continue;
                                };
                                let key_notification = state_store::KeyNotification {
                                    key: decoded_key_name,
                                    operation: notification.payload.clone(),
                                    version: notification_timestamp,
                                };

                                let mut observed_keys_mutex_guard = observed_keys.lock().await;

                                // if key is in the hashmap of observed keys
                                if let Some(sender) = observed_keys_mutex_guard.get_mut(key_name) {

                                        if sender.is_closed() {
                                            log::info!("Key Notification Receiver has been dropped. Received Notification: {key_notification:?}",);
                                        }
                                        else {
                                            // Otherwise, send the notification to the receiver
                                            if let Err(e) = sender.send((key_notification.clone(), ack_token)).await {
                                                log::error!("Error delivering key notification {key_notification:?}: {e}");
                                            }
                                        }
                                } else {
                                    log::info!("Key is not being observed. Received Notification: {key_notification:?}");
                                }
                            }
                            Err(e) => {
                                // This should only happen on errors subscribing, but it's likely not recoverable
                                log::error!("Error receiving key notifications: {e}");
                                break;
                            }
                        }
                    } else {
                        log::error!("Telemetry Receiver closed, no more Key Notifications can be received");
                        break;
                    }
                }
            }
        }
        let result = telemetry_receiver.shutdown().await;
        log::info!("Receive key notification loop cancelled: {result:?}");
    }
}

impl<C> Drop for Client<C>
where
    C: ManagedClient + Clone + Send + Sync,
    C::PubReceiver: Send + Sync,
{
    fn drop(&mut self) {
        self.recv_cancellation_token.cancel();
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
            .hostname("localhost")
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
        let state_store_client = super::Client::new(
            managed_client,
            super::ClientOptionsBuilder::default().build().unwrap(),
        )
        .unwrap();
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
        let state_store_client = super::Client::new(
            managed_client,
            super::ClientOptionsBuilder::default().build().unwrap(),
        )
        .unwrap();
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
        let state_store_client = super::Client::new(
            managed_client,
            super::ClientOptionsBuilder::default().build().unwrap(),
        )
        .unwrap();
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
        let state_store_client = super::Client::new(
            managed_client,
            super::ClientOptionsBuilder::default().build().unwrap(),
        )
        .unwrap();
        let response = state_store_client
            .vdel(vec![], b"testValue".to_vec(), None, Duration::from_secs(1))
            .await;
        assert!(matches!(
            response.unwrap_err(),
            StateStoreError(StateStoreErrorKind::KeyLengthZero)
        ));
    }

    #[tokio::test]
    async fn test_observe_empty_key() {
        let session = create_session();
        let managed_client = session.create_managed_client();
        let state_store_client = super::Client::new(
            managed_client,
            super::ClientOptionsBuilder::default().build().unwrap(),
        )
        .unwrap();
        let response = state_store_client
            .observe(vec![], Duration::from_secs(1))
            .await;
        assert!(matches!(
            response.unwrap_err(),
            StateStoreError(StateStoreErrorKind::KeyLengthZero)
        ));
    }

    #[tokio::test]
    async fn test_unobserve_empty_key() {
        let session = create_session();
        let managed_client = session.create_managed_client();
        let state_store_client = super::Client::new(
            managed_client,
            super::ClientOptionsBuilder::default().build().unwrap(),
        )
        .unwrap();
        let response = state_store_client
            .unobserve(vec![], Duration::from_secs(1))
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
        let state_store_client = super::Client::new(
            managed_client,
            super::ClientOptionsBuilder::default().build().unwrap(),
        )
        .unwrap();
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
        let state_store_client = super::Client::new(
            managed_client,
            super::ClientOptionsBuilder::default().build().unwrap(),
        )
        .unwrap();
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
        let state_store_client = super::Client::new(
            managed_client,
            super::ClientOptionsBuilder::default().build().unwrap(),
        )
        .unwrap();
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
        let state_store_client = super::Client::new(
            managed_client,
            super::ClientOptionsBuilder::default().build().unwrap(),
        )
        .unwrap();
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

    #[tokio::test]
    async fn test_observe_invalid_timeout() {
        let session = create_session();
        let managed_client = session.create_managed_client();
        let state_store_client = super::Client::new(
            managed_client,
            super::ClientOptionsBuilder::default().build().unwrap(),
        )
        .unwrap();
        let response = state_store_client
            .observe(b"testKey".to_vec(), Duration::from_nanos(50))
            .await;
        assert!(matches!(
            response.unwrap_err(),
            StateStoreError(StateStoreErrorKind::InvalidArgument(_))
        ));
    }

    #[tokio::test]
    async fn test_unobserve_invalid_timeout() {
        let session = create_session();
        let managed_client = session.create_managed_client();
        let state_store_client = super::Client::new(
            managed_client,
            super::ClientOptionsBuilder::default().build().unwrap(),
        )
        .unwrap();
        let response = state_store_client
            .unobserve(b"testKey".to_vec(), Duration::from_nanos(50))
            .await;
        assert!(matches!(
            response.unwrap_err(),
            StateStoreError(StateStoreErrorKind::InvalidArgument(_))
        ));
    }
}
