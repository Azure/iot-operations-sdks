// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

//! Client for Lease Lock operations.

use std::{sync::Arc, time::Duration};

use tokio::sync::Mutex;

use crate::leased_lock::{AcquireAndUpdateKeyOption, Error, ErrorKind, LockObservation, Response};
use crate::state_store::{self, SetCondition, SetOptions};
use azure_iot_operations_mqtt::interface::ManagedClient;
use azure_iot_operations_protocol::common::hybrid_logical_clock::HybridLogicalClock;

/// Leased Lock client struct.
pub struct Client<C>
where
    C: ManagedClient + Clone + Send + Sync + 'static,
    C::PubReceiver: Send + Sync,
{
    state_store: Arc<Mutex<state_store::Client<C>>>,
    lock_name: Vec<u8>,
    lock_holder_name: Vec<u8>,
}

/// Leased Lock client implementation
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
    /// [`Error`] of kind [`LockNameLengthZero`](ErrorKind::LockNameLengthZero) if the `lock_name` is empty
    ///
    /// [`Error`] of kind [`LockNameLengthZero`](ErrorKind::LockHolderNameLengthZero) if the `lock_holder_name` is empty
    pub fn new(
        state_store: Arc<Mutex<state_store::Client<C>>>,
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
    /// `request_timeout` is the maximum time the function will wait for receiving a response from the State Store service.
    ///
    /// Returns Ok with a fencing token (`HybridLogicalClock`) if completed successfully, or Error(LockAlreadyHeld) if lock is not acquired.
    /// # Errors
    /// [`Error`] of kind [`LockNameLengthZero`](ErrorKind::LockNameLengthZero) if the `lock` is empty
    ///
    /// [`Error`] of kind [`InvalidArgument`](ErrorKind::InvalidArgument) if the `request_timeout` is < 1 ms or > `u32::max`
    ///
    /// [`Error`] of kind [`ServiceError`](ErrorKind::ServiceError) if the State Store returns an Error response
    ///
    /// [`Error`] of kind [`UnexpectedPayload`](ErrorKind::UnexpectedPayload) if the State Store returns a response that isn't valid for a `Set` request
    ///
    /// [`Error`] of kind [`AIOProtocolError`](ErrorKind::AIOProtocolError) if there are any underlying errors from [`CommandInvoker::invoke`]
    ///
    /// [`Error`] of kind [`LockAlreadyHeld`](ErrorKind::LockAlreadyHeld) if the `lock` is already in use by another holder
    /// # Panics
    /// Possible panic if, for some error, the fencing token (a.k.a. `version`) of the acquired lock is None.
    pub async fn try_acquire_lock(
        &self,
        lock_expiration: Duration,
        request_timeout: Duration,
    ) -> Result<HybridLogicalClock, Error> {
        match self
            .state_store
            .lock()
            .await
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
            .await
        {
            Ok(state_store_response) => {
                if state_store_response.response {
                    Ok(state_store_response.version.expect(
                        "Got None for fencing token. A lock without a fencing token is of no use.",
                    ))
                } else {
                    Err(Error(ErrorKind::LockAlreadyHeld))
                }
            }
            Err(state_store_error) => Err(state_store_error.into()),
        }
    }

    /// Waits until a lock is available (if not already) and attempts to acquire it.
    ///
    /// Returns Ok with a fencing token (`HybridLogicalClock`) if completed successfully, or an Error if any failure occurs.
    /// # Errors
    /// [`Error`] of kind [`InvalidArgument`](ErrorKind::InvalidArgument) if the `request_timeout` is < 1 ms or > `u32::max`
    ///
    /// [`Error`] of kind [`ServiceError`](ErrorKind::ServiceError) if the State Store returns an Error response
    ///
    /// [`Error`] of kind [`UnexpectedPayload`](ErrorKind::UnexpectedPayload) if the State Store returns a response that isn't valid for a `Set` request
    ///
    /// [`Error`] of kind [`AIOProtocolError`](ErrorKind::AIOProtocolError) if there are any underlying errors from [`CommandInvoker::invoke`]
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
                    // If the state_store client disconnect, all the observation channels receive a None.
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
    /// `update_value_function` is a function with signature:
    ///     fn `should_update_key(key_current_value`: Vec<u8>) -> `AcquireAndUpdateKeyOption`
    /// Where `key_current_value` is the current value of `key` in the State Store (right after the lock is acquired).
    /// If the return is `AcquireAndUpdateKeyOption::Update(key_new_value)` it must contain the new value of the State Store key.
    ///
    /// The same `request_timeout` is used for all the individual network calls within `acquire_lock_and_update_value`.
    ///
    /// Returns `true` if the key is successfully set or deleted, or `false` if it is not.
    /// # Errors
    /// [`Error`] of kind [`InvalidArgument`](ErrorKind::InvalidArgument) if the `request_timeout` is < 1 ms or > `u32::max`
    ///
    /// [`Error`] of kind [`ServiceError`](ErrorKind::ServiceError) if the State Store returns an Error response
    ///
    /// [`Error`] of kind [`UnexpectedPayload`](ErrorKind::UnexpectedPayload) if the State Store returns a response that isn't valid for a `Set` request
    ///
    /// [`Error`] of kind [`AIOProtocolError`](ErrorKind::AIOProtocolError) if there are any underlying errors from [`CommandInvoker::invoke`]
    pub async fn acquire_lock_and_update_value(
        &self,
        lock_expiration: Duration,
        request_timeout: Duration,
        key: Vec<u8>,
        update_value_function: impl Fn(Option<Vec<u8>>) -> AcquireAndUpdateKeyOption,
    ) -> Result<Response<bool>, Error> {
        let fencing_token = self.acquire_lock(lock_expiration, request_timeout).await?;

        /* lock acquired, let's proceed. */
        let get_result = self
            .state_store
            .lock()
            .await
            .get(key.clone(), request_timeout)
            .await?;

        match update_value_function(get_result.response) {
            AcquireAndUpdateKeyOption::Update(new_value, set_options) => {
                match self
                    .state_store
                    .lock()
                    .await
                    .set(
                        key,
                        new_value,
                        request_timeout,
                        Some(fencing_token),
                        set_options,
                    )
                    .await
                {
                    Ok(set_response) => {
                        let _ = self.release_lock(request_timeout).await;
                        Ok(Response::from_response(set_response))
                    }
                    Err(set_error) => {
                        let _ = self.release_lock(request_timeout).await;
                        Err(set_error.into())
                    }
                }
            }
            AcquireAndUpdateKeyOption::DoNotUpdate => {
                let _ = self.release_lock(request_timeout).await;
                Ok(Response::new(true, None))
            }
            AcquireAndUpdateKeyOption::Delete => {
                match self
                    .state_store
                    .lock()
                    .await
                    .del(key, Some(fencing_token), request_timeout)
                    .await
                {
                    Ok(delete_response) => {
                        let _ = self.release_lock(request_timeout).await;
                        Ok(Response::new(
                            delete_response.response != 0,
                            delete_response.version,
                        ))
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
    /// Returns `Ok()` if lock is released, or `Error` otherwise.
    /// # Errors
    /// [`Error`] of kind [`LockNameLengthZero`](ErrorKind::LockNameLengthZero) if the `lock` is empty
    ///
    /// [`Error`] of kind [`InvalidArgument`](ErrorKind::InvalidArgument) if the `request_timeout` is < 1 ms or > `u32::max`
    ///
    /// [`Error`] of kind [`ServiceError`](ErrorKind::ServiceError) if the State Store returns an Error response
    ///
    /// [`Error`] of kind [`UnexpectedPayload`](ErrorKind::UnexpectedPayload) if the State Store returns a response that isn't valid for a `V Delete` request
    ///
    /// [`Error`] of kind [`AIOProtocolError`](ErrorKind::AIOProtocolError) if there are any underlying errors from [`CommandInvoker::invoke`]
    pub async fn release_lock(&self, request_timeout: Duration) -> Result<(), Error> {
        match self
            .state_store
            .lock()
            .await
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
    /// Returns OK([`leased_lock::Response<LockObservation>`]) if the lock is now being observed.
    /// The [`LockObservation`] can be used to receive lock notifications for this lock
    ///
    /// <div class="warning">
    ///
    /// If a client disconnects, `observe_lock` must be called again by the user.
    ///
    /// </div>
    ///
    /// # Errors
    /// [`Error`] of kind [`LockNameLengthZero`](ErrorKind::LockNameLengthZero) if
    /// - the `lock` is empty
    ///
    /// [`Error`] of kind [`InvalidArgument`](ErrorKind::InvalidArgument) if
    /// - the `request_timeout` is < 1 ms or > `u32::max`
    ///
    /// [`Error`] of kind [`ServiceError`](ErrorKind::ServiceError) if
    /// - the State Store returns an Error response
    /// - the State Store returns a response that isn't valid for an `Observe` request
    ///
    /// [`Error`] of kind [`AIOProtocolError`](ErrorKind::AIOProtocolError) if
    /// - there are any underlying errors from [`CommandInvoker::invoke`]
    pub async fn observe_lock(
        &self,
        request_timeout: Duration,
    ) -> Result<Response<LockObservation>, Error> {
        let observe_result = self
            .state_store
            .lock()
            .await
            .observe(self.lock_name.clone(), request_timeout)
            .await?;

        Ok(observe_result.into())
    }

    /// Stops observation of any changes on a lock.
    ///
    /// Returns `true` if the lock is no longer being observed or `false` if the lock wasn't being observed
    /// # Errors
    /// [`Error`] of kind [`LockNameLengthZero`](ErrorKind::LockNameLengthZero) if
    /// - the `lock` is empty
    ///
    /// [`Error`] of kind [`InvalidArgument`](ErrorKind::InvalidArgument) if
    /// - the `request_timeout` is < 1 ms or > `u32::max`
    ///
    /// [`Error`] of kind [`ServiceError`](ErrorKind::ServiceError) if
    /// - the State Store returns an Error response
    /// - the State Store returns a response that isn't valid for an `Unobserve` request
    ///
    /// [`Error`] of kind [`AIOProtocolError`](ErrorKind::AIOProtocolError) if
    /// - there are any underlying errors from [`CommandInvoker::invoke`]
    pub async fn unobserve_lock(&self, request_timeout: Duration) -> Result<Response<bool>, Error> {
        match self
            .state_store
            .lock()
            .await
            .unobserve(self.lock_name.clone(), request_timeout)
            .await
        {
            Ok(state_store_response) => Ok(Response::from_response(state_store_response)),
            Err(state_store_error) => Err(state_store_error.into()),
        }
    }

    /// Gets the name of the holder of a lock
    ///
    /// Returns `Some(<holder of the lock>)` if the lock is found or `None`
    /// if the lock was not found (i.e., was not acquired by anyone, already released or expired).
    ///
    /// # Errors
    /// [`Error`] of kind [`LockNameLengthZero`](ErrorKind::LockNameLengthZero) if the `lock_name` is empty
    ///
    /// [`Error`] of kind [`InvalidArgument`](ErrorKind::InvalidArgument) if the `request_timeout` is < 1 ms or > `u32::max`
    ///
    /// [`Error`] of kind [`ServiceError`](ErrorKind::ServiceError) if the State Store returns an Error response
    ///
    /// [`Error`] of kind [`UnexpectedPayload`](ErrorKind::UnexpectedPayload) if the State Store returns a response that isn't valid for a `Get` request
    ///
    /// [`Error`] of kind [`AIOProtocolError`](ErrorKind::AIOProtocolError) if there are any underlying errors from [`CommandInvoker::invoke`]
    pub async fn get_lock_holder(
        &self,
        request_timeout: Duration,
    ) -> Result<Response<Option<Vec<u8>>>, Error> {
        Ok(Response::from_response(
            self.state_store
                .lock()
                .await
                .get(self.lock_name.clone(), request_timeout)
                .await?,
        ))
    }
}
