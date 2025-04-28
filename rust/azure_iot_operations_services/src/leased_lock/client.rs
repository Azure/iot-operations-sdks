// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

//! Client for Lease Lock operations.

use std::{sync::Arc, time::Duration};

use crate::leased_lock::{
    AcquireAndUpdateKeyOption, Error, ErrorKind, Response, lease::Client as LeaseClient,
};
use crate::state_store::{self};
use azure_iot_operations_mqtt::interface::ManagedClient;
use azure_iot_operations_protocol::common::hybrid_logical_clock::HybridLogicalClock;

/// Key Lease client struct.
pub struct Client<C>
where
    C: ManagedClient + Clone + Send + Sync + 'static,
    C::PubReceiver: Send + Sync,
{
    state_store: Arc<state_store::Client<C>>,
    lease_client: LeaseClient<C>,
}

/// Lock client implementation
///
/// Notes:
/// Do not call any of the methods of this client after the `state_store` parameter is shutdown.
/// Calling any of the methods in this implementation after the `state_store` is shutdown results in undefined behavior.
impl<C> Client<C>
where
    C: ManagedClient + Clone + Send + Sync,
    C::PubReceiver: Send + Sync,
{
    /// Create a new Lock Client.
    ///
    /// Notes:
    /// - `lock_holder_name` is expected to be the client ID used in the underlying MQTT connection settings.
    /// - There must be one instance of `leased_lock::Client` per lock.
    ///
    /// # Errors
    /// [`struct@Error`] of kind [`LockNameLengthZero`](ErrorKind::LockNameLengthZero) if the `lock_name` is empty
    ///
    /// [`struct@Error`] of kind [`LeaseHolderNameLengthZero`](ErrorKind::LeaseHolderNameLengthZero) if the `lock_holder_name` is empty
    pub fn new(
        state_store: Arc<state_store::Client<C>>,
        lock_name: Vec<u8>,
        lock_holder_name: Vec<u8>,
    ) -> Result<Self, Error> {
        if lock_name.is_empty() {
            return Err(Error(ErrorKind::LockNameLengthZero));
        }

        if lock_holder_name.is_empty() {
            return Err(Error(ErrorKind::LeaseHolderNameLengthZero));
        }

        let lease_client = LeaseClient::new(state_store.clone(), lock_name, lock_holder_name)?;

        Ok(Self {
            state_store,
            lease_client,
        })
    }

    /// Waits until a lock is available (if not already) and attempts to acquire it.
    ///
    /// Note: `request_timeout` is rounded up to the nearest second.
    ///
    /// Returns Ok with a fencing token (`HybridLogicalClock`) if completed successfully, or an Error if any failure occurs.
    /// # Errors
    /// [`struct@Error`] of kind [`InvalidArgument`](ErrorKind::InvalidArgument) if the `request_timeout` is zero or > `u32::max`
    ///
    /// [`struct@Error`] of kind [`ServiceError`](ErrorKind::ServiceError) if the State Store returns an Error response
    ///
    /// [`struct@Error`] of kind [`UnexpectedPayload`](ErrorKind::UnexpectedPayload) if the State Store returns a response that isn't valid for the request
    ///
    /// [`struct@Error`] of kind [`AIOProtocolError`](ErrorKind::AIOProtocolError) if there are any underlying errors from the command invoker
    /// # Panics
    /// Possible panic if, for some error, the fencing token (a.k.a. `version`) of the acquired lease is None.
    pub async fn lock(
        &self,
        lock_expiration: Duration,
        request_timeout: Duration,
    ) -> Result<HybridLogicalClock, Error> {
        self.lease_client
            .acquire(lock_expiration, request_timeout)
            .await
    }

    /// Releases a lock.
    ///
    /// Note: `request_timeout` is rounded up to the nearest second.
    ///
    /// Returns `Ok()` if lease is no longer held by this `lock holder`, or `Error` otherwise.
    /// # Errors
    /// [`struct@Error`] of kind [`InvalidArgument`](ErrorKind::InvalidArgument) if the `request_timeout` is zero or > `u32::max`
    ///
    /// [`struct@Error`] of kind [`ServiceError`](ErrorKind::ServiceError) if the State Store returns an Error response
    ///
    /// [`struct@Error`] of kind [`UnexpectedPayload`](ErrorKind::UnexpectedPayload) if the State Store returns a response that isn't valid for a `V Delete` request
    ///
    /// [`struct@Error`] of kind [`AIOProtocolError`](ErrorKind::AIOProtocolError) if there are any underlying errors from the command invoker
    pub async fn unlock(&self, request_timeout: Duration) -> Result<(), Error> {
        self.lease_client.release(request_timeout).await
    }

    /// Waits until a lock is acquired, sets/updates/deletes a key in the State Store (depending on `update_value_function` result) and releases the lock.
    ///
    /// `lock_expiration` should be long enough to last through underlying key operations, otherwise it's possible for updating the value to fail if the lock is no longer held.
    ///
    /// `update_value_function` is a function with signature:
    ///     fn `should_update_key(key_current_value`: `Vec<u8>`) -> `AcquireAndUpdateKeyOption`
    /// Where `key_current_value` is the current value of `key` in the State Store (right after the lock is acquired).
    /// If the return is `AcquireAndUpdateKeyOption::Update(key_new_value)` it must contain the new value of the State Store key.
    ///
    /// The same `request_timeout` is used for all the individual network calls within `lock_and_update_value`.
    ///
    /// Note: `request_timeout` is rounded up to the nearest second.
    ///
    /// Returns `true` if the key is successfully set or deleted, or `false` if it is not.
    /// # Errors
    /// [`struct@Error`] of kind [`InvalidArgument`](ErrorKind::InvalidArgument) if the `request_timeout` is zero or > `u32::max`
    ///
    /// [`struct@Error`] of kind [`ServiceError`](ErrorKind::ServiceError) if the State Store returns an Error response
    ///
    /// [`struct@Error`] of kind [`UnexpectedPayload`](ErrorKind::UnexpectedPayload) if the State Store returns a response that isn't valid for the request
    ///
    /// [`struct@Error`] of kind [`AIOProtocolError`](ErrorKind::AIOProtocolError) if there are any underlying errors from the command invoker
    pub async fn lock_and_update_value(
        &self,
        lock_expiration: Duration,
        request_timeout: Duration,
        key: Vec<u8>,
        update_value_function: impl Fn(Option<Vec<u8>>) -> AcquireAndUpdateKeyOption,
    ) -> Result<Response<bool>, Error> {
        let fencing_token = self
            .lease_client
            .acquire(lock_expiration, request_timeout)
            .await?;

        /* lock acquired, let's proceed. */
        let get_result = self.state_store.get(key.clone(), request_timeout).await?;

        match update_value_function(get_result.response) {
            AcquireAndUpdateKeyOption::Update(new_value, set_options) => {
                let set_response = self
                    .state_store
                    .set(
                        key,
                        new_value,
                        request_timeout,
                        Some(fencing_token),
                        set_options,
                    )
                    .await;

                let _ = self.lease_client.release(request_timeout).await;

                Ok(set_response?)
            }
            AcquireAndUpdateKeyOption::DoNotUpdate => {
                let _ = self.lease_client.release(request_timeout).await;
                Ok(Response {
                    response: true,
                    version: None,
                })
            }
            AcquireAndUpdateKeyOption::Delete => {
                match self
                    .state_store
                    .del(key, Some(fencing_token), request_timeout)
                    .await
                {
                    Ok(delete_response) => {
                        let _ = self.lease_client.release(request_timeout).await;
                        Ok(Response {
                            response: (delete_response.response > 0),
                            version: delete_response.version,
                        })
                    }
                    Err(delete_error) => {
                        let _ = self.lease_client.release(request_timeout).await;
                        Err(delete_error.into())
                    }
                }
            }
        }
    }
}
