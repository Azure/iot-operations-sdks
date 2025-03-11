// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

//! Client for Lease Lock operations.

use std::{sync::Arc, time::Duration};

use crate::leased_lock::{
    AcquireAndUpdateKeyOption, Error, ErrorKind, LockObservation, Response, SetCondition,
    SetOptions,
};
use crate::state_store::{self};
use azure_iot_operations_mqtt::interface::ManagedClient;
use azure_iot_operations_protocol::common::hybrid_logical_clock::HybridLogicalClock;

/// Leased Lock client struct.
pub struct Client<C>
where
    C: ManagedClient + Clone + Send + Sync + 'static,
    C::PubReceiver: Send + Sync,
{
    state_store: Arc<state_store::Client<C>>,
    lock_name: Vec<u8>,
    lock_holder_name: Vec<u8>,
}

/// Leased Lock client implementation
///
/// Notes:
/// Do not call any of the methods of this client after the `state_store` parameter is shutdown.
/// Calling any of the methods in this implementation after the `state_store` is shutdown results in undefined behavior.
impl<C> Client<C>
where
    C: ManagedClient + Clone + Send + Sync,
    C::PubReceiver: Send + Sync,
{
    /// Create a new Leased Lock Client.
    ///
    /// Notes:
    /// - `lock_holder_name` is expected to be the client ID used in the underlying MQTT connection settings.
    /// - There must be one instance of `leased_lock::Client` per lock.
    ///
    /// # Errors
    /// [`struct@Error`] of kind [`LockNameLengthZero`](ErrorKind::LockNameLengthZero) if the `lock_name` is empty
    ///
    /// [`struct@Error`] of kind [`LockHolderNameLengthZero`](ErrorKind::LockHolderNameLengthZero) if the `lock_holder_name` is empty
    pub fn new(
        state_store: Arc<state_store::Client<C>>,
        lock_name: Vec<u8>,
        lock_holder_name: Vec<u8>,
    ) -> Result<Self, Error> {
        if lock_name.is_empty() {
            return Err(Error(ErrorKind::LockNameLengthZero));
        }

        if lock_holder_name.is_empty() {
            return Err(Error(ErrorKind::LockHolderNameLengthZero));
        }

        Ok(Self {
            state_store,
            lock_name,
            lock_holder_name,
        })
    }

    /// Attempts to acquire a lock, returning if it cannot be acquired after one attempt.
    ///
    /// `lock_expiration` is how long the lock will remain held in the State Store after acquired, if not released before then.
    /// `request_timeout` is the maximum time the function will wait for receiving a response from the State Store service, it is rounded up to the nearest second.
    ///
    /// Returns Ok with a fencing token (`HybridLogicalClock`) if completed successfully, or Error(LockAlreadyHeld) if lock is not acquired.
    /// # Errors
    /// [`struct@Error`] of kind [`InvalidArgument`](ErrorKind::InvalidArgument) if the `request_timeout` is zero or > `u32::max`
    ///
    /// [`struct@Error`] of kind [`ServiceError`](ErrorKind::ServiceError) if the State Store returns an Error response
    ///
    /// [`struct@Error`] of kind [`UnexpectedPayload`](ErrorKind::UnexpectedPayload) if the State Store returns a response that isn't valid for a `Set` request
    ///
    /// [`struct@Error`] of kind [`AIOProtocolError`](ErrorKind::AIOProtocolError) if there are any underlying errors from the command invoker
    ///
    /// [`struct@Error`] of kind [`LockAlreadyHeld`](ErrorKind::LockAlreadyHeld) if the `lock` is already in use by another holder
    /// # Panics
    /// Possible panic if, for some error, the fencing token (a.k.a. `version`) of the acquired lock is None.
    pub async fn try_acquire_lock(
        &self,
        lock_expiration: Duration,
        request_timeout: Duration,
    ) -> Result<HybridLogicalClock, Error> {
        let state_store_response = self
            .state_store
            .set(
                self.lock_name.clone(),
                self.lock_holder_name.clone(),
                request_timeout,
                None,
                SetOptions {
                    set_condition: SetCondition::OnlyIfEqualOrDoesNotExist,
                    expires: Some(lock_expiration),
                },
            )
            .await?;

        if state_store_response.response {
            Ok(state_store_response
                .version
                .expect("Got None for fencing token. A lock without a fencing token is of no use."))
        } else {
            Err(Error(ErrorKind::LockAlreadyHeld))
        }
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
    /// Possible panic if, for some error, the fencing token (a.k.a. `version`) of the acquired lock is None.
    pub async fn acquire_lock(
        &self,
        lock_expiration: Duration,
        request_timeout: Duration,
    ) -> Result<HybridLogicalClock, Error> {
        // Logic:
        // a. Start observing lock within this function.
        // b. Try acquiring the lock and return if acquired or got an error other than `LockAlreadyHeld`.
        // c. If got `LockAlreadyHeld`, wait until `Del` notification for the lock. If notification is None, re-observe (start from a. again).
        // d. Loop back starting from b. above.
        // e. Unobserve lock before exiting.

        let mut observe_response = self.observe_lock(request_timeout).await?;
        let mut acquire_result;

        loop {
            acquire_result = self
                .try_acquire_lock(lock_expiration, request_timeout)
                .await;

            match acquire_result {
                Ok(_) => {
                    break; /* Lock acquired */
                }
                Err(ref acquire_error) => match acquire_error.kind() {
                    ErrorKind::LockAlreadyHeld => { /* Must wait for lock to be released. */ }
                    _ => {
                        break;
                    }
                },
            };

            // Lock being held by another client. Wait for delete notification.
            loop {
                let Some((notification, _)) = observe_response.response.recv_notification().await
                else {
                    // If the state_store client gets disconnected (or shutdown), all the observation channels receive a None.
                    // In such case, as per design, we must re-observe the lock.
                    observe_response = self.observe_lock(request_timeout).await?;
                    break;
                };

                if notification.operation == state_store::Operation::Del {
                    break;
                };
            }
        }

        match self.unobserve_lock(request_timeout).await {
            Ok(_) => acquire_result,
            Err(unobserve_error) => Err(unobserve_error),
        }
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
    /// The same `request_timeout` is used for all the individual network calls within `acquire_lock_and_update_value`.
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
    pub async fn acquire_lock_and_update_value(
        &self,
        lock_expiration: Duration,
        request_timeout: Duration,
        key: Vec<u8>,
        update_value_function: impl Fn(Option<Vec<u8>>) -> AcquireAndUpdateKeyOption,
    ) -> Result<Response<bool>, Error> {
        let fencing_token = self.acquire_lock(lock_expiration, request_timeout).await?;

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

                let _ = self.release_lock(request_timeout).await;

                Ok(set_response?)
            }
            AcquireAndUpdateKeyOption::DoNotUpdate => {
                let _ = self.release_lock(request_timeout).await;
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
                        let _ = self.release_lock(request_timeout).await;
                        Ok(Response {
                            response: (delete_response.response > 0),
                            version: delete_response.version,
                        })
                    }
                    Err(delete_error) => {
                        let _ = self.release_lock(request_timeout).await;
                        Err(delete_error.into())
                    }
                }
            }
        }
    }

    /// Releases a lock if and only if requested by the lock holder (same client id).
    ///
    /// Note: `request_timeout` is rounded up to the nearest second.
    ///
    /// Returns `Ok()` if lock is no longer held by this `lock_holder`, or `Error` otherwise.
    /// # Errors
    /// [`struct@Error`] of kind [`InvalidArgument`](ErrorKind::InvalidArgument) if the `request_timeout` is zero or > `u32::max`
    ///
    /// [`struct@Error`] of kind [`ServiceError`](ErrorKind::ServiceError) if the State Store returns an Error response
    ///
    /// [`struct@Error`] of kind [`UnexpectedPayload`](ErrorKind::UnexpectedPayload) if the State Store returns a response that isn't valid for a `V Delete` request
    ///
    /// [`struct@Error`] of kind [`AIOProtocolError`](ErrorKind::AIOProtocolError) if there are any underlying errors from the command invoker
    pub async fn release_lock(&self, request_timeout: Duration) -> Result<(), Error> {
        match self
            .state_store
            .vdel(
                self.lock_name.clone(),
                self.lock_holder_name.clone(),
                None,
                request_timeout,
            )
            .await
        {
            Ok(_) => Ok(()),
            Err(state_store_error) => Err(state_store_error.into()),
        }
    }

    /// Starts observation of any changes on a lock
    ///
    /// Note: `request_timeout` is rounded up to the nearest second.
    ///
    /// Returns OK([`Response<LockObservation>`]) if the lock is now being observed.
    /// The [`LockObservation`] can be used to receive lock notifications for this lock
    ///
    /// <div class="warning">
    ///
    /// If a client disconnects, `observe_lock` must be called again by the user.
    ///
    /// </div>
    ///
    /// # Errors
    /// [`struct@Error`] of kind [`InvalidArgument`](ErrorKind::InvalidArgument) if
    /// - the `request_timeout` is zero or > `u32::max`
    ///
    /// [`struct@Error`] of kind [`ServiceError`](ErrorKind::ServiceError) if
    /// - the State Store returns an Error response
    /// - the State Store returns a response that isn't valid for an `Observe` request
    ///
    /// [`struct@Error`] of kind [`AIOProtocolError`](ErrorKind::AIOProtocolError) if
    /// - there are any underlying errors from the command invoker
    pub async fn observe_lock(
        &self,
        request_timeout: Duration,
    ) -> Result<Response<LockObservation>, Error> {
        Ok(self
            .state_store
            .observe(self.lock_name.clone(), request_timeout)
            .await?)
    }

    /// Stops observation of any changes on a lock.
    ///
    /// Note: `request_timeout` is rounded up to the nearest second.
    ///
    /// Returns `true` if the lock is no longer being observed or `false` if the lock wasn't being observed
    /// # Errors
    /// [`struct@Error`] of kind [`InvalidArgument`](ErrorKind::InvalidArgument) if
    /// - the `request_timeout` is zero or > `u32::max`
    ///
    /// [`struct@Error`] of kind [`ServiceError`](ErrorKind::ServiceError) if
    /// - the State Store returns an Error response
    /// - the State Store returns a response that isn't valid for an `Unobserve` request
    ///
    /// [`struct@Error`] of kind [`AIOProtocolError`](ErrorKind::AIOProtocolError) if
    /// - there are any underlying errors from the command invoker
    pub async fn unobserve_lock(&self, request_timeout: Duration) -> Result<Response<bool>, Error> {
        Ok(self
            .state_store
            .unobserve(self.lock_name.clone(), request_timeout)
            .await?)
    }

    /// Gets the name of the holder of a lock
    ///
    /// Note: `request_timeout` is rounded up to the nearest second.
    ///
    /// Returns `Some(<holder of the lock>)` if the lock is found or `None`
    /// if the lock was not found (i.e., was not acquired by anyone, already released or expired).
    ///
    /// # Errors
    /// [`struct@Error`] of kind [`InvalidArgument`](ErrorKind::InvalidArgument) if the `request_timeout` is zero or > `u32::max`
    ///
    /// [`struct@Error`] of kind [`ServiceError`](ErrorKind::ServiceError) if the State Store returns an Error response
    ///
    /// [`struct@Error`] of kind [`UnexpectedPayload`](ErrorKind::UnexpectedPayload) if the State Store returns a response that isn't valid for a `Get` request
    ///
    /// [`struct@Error`] of kind [`AIOProtocolError`](ErrorKind::AIOProtocolError) if there are any underlying errors from the command invoker
    pub async fn get_lock_holder(
        &self,
        request_timeout: Duration,
    ) -> Result<Response<Option<Vec<u8>>>, Error> {
        Ok(self
            .state_store
            .get(self.lock_name.clone(), request_timeout)
            .await?)
    }
}
