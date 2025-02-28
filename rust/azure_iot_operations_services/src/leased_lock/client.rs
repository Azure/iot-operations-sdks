// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

//! Client for Lease Lock operations.

use std::{sync::Arc, time::Duration};

use tokio::sync::Mutex;

use crate::leased_lock::{Error, ErrorKind, LockObservation, Response};
use crate::state_store::{self, SetCondition, SetOptions};
use azure_iot_operations_mqtt::interface::ManagedClient;

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
    /// `lock_expiration` is how long the lock will remain set in the Distributed State Store after set, if not deleted.
    /// `request_timeout` is the maximum time the function will wait for receiving a response from the Distributed State Store service.
    ///
    /// Returns `true` if completed successfully, or `false` if lock is not acquired.
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
    pub async fn try_acquire_lock(
        &self,
        lock_expiration: Duration,
        request_timeout: Duration,
    ) -> Result<Response<bool>, Error> {
        let locked_state_store = self.state_store.lock().await;

        match locked_state_store
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
                    Ok(Response::from_response(state_store_response))
                } else {
                    Err(Error(ErrorKind::LockAlreadyHeld))
                }
            }
            Err(state_store_error) => Err(state_store_error.into()),
        }
    }

    /// Waits until a lock is available (if not already) and attempts to acquire it.
    ///
    /// Returns `true` if completed successfully, or `false` if lock is not acquired.
    /// # Errors
    /// [`Error`] of kind [`InvalidArgument`](ErrorKind::InvalidArgument) if the `request_timeout` is < 1 ms or > `u32::max`
    ///
    /// [`Error`] of kind [`ServiceError`](ErrorKind::ServiceError) if the State Store returns an Error response
    ///
    /// [`Error`] of kind [`UnexpectedPayload`](ErrorKind::UnexpectedPayload) if the State Store returns a response that isn't valid for a `Set` request
    ///
    /// [`Error`] of kind [`AIOProtocolError`](ErrorKind::AIOProtocolError) if there are any underlying errors from [`CommandInvoker::invoke`]
    pub async fn acquire_lock(
        &self,
        lock_expiration: Duration,
        request_timeout: Duration,
    ) -> Result<Response<bool>, Error> {
        // Logic:
        // a. Start observing lock within this function.
        // b. Try acquiring the lock and return if acquired or got an error other than `LockAlreadyHeld`.
        // c. If got `LockAlreadyHeld`, wait until `Del` notification for the lock.
        // d. Loop back starting from b. above.
        // e. Unobserve lock before exiting.

        let mut observe_response = self.observe_lock(request_timeout).await?;
        let mut acquire_result;

        loop {
            acquire_result = self
                .try_acquire_lock(lock_expiration, request_timeout)
                .await;

            match acquire_result {
                Ok(ref acquire_response) => {
                    if acquire_response.response {
                        break; /* Lock acquired */
                    }
                }
                Err(ref acquire_error) => match acquire_error.kind() {
                    ErrorKind::LockAlreadyHeld => { /* Must wait for lock to be released. */ }
                    _ => {
                        break;
                    }
                },
            };

            // Lock being held by another client. Wait for delete notification.
            while let Some((notification, _)) = observe_response.response.recv_notification().await
            {
                if notification.operation == state_store::Operation::Del {
                    break;
                }
            }
        }

        let _ = self.unobserve_lock(request_timeout).await;

        acquire_result
    }

    /// Waits until a lock is acquired, sets or updates a key in the state store and releases the lock.
    ///
    /// Returns `true` if the key is successfully set, or `false` if it is not.
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
        value: Vec<u8>,
        set_options: SetOptions,
    ) -> Result<Response<bool>, Error> {
        loop {
            let acquire_result = self.acquire_lock(lock_expiration, request_timeout).await;

            match acquire_result {
                Err(ref acquire_error) => {
                    match acquire_error.kind() {
                        ErrorKind::LockAlreadyHeld => continue, // Try to lock again.
                        _ => return acquire_result, // Some other error that cannot be handle here.
                    }
                }
                Ok(acquire_response) => {
                    /* lock acquired, let's proceed. */

                    let locked_state_store = self.state_store.lock().await;

                    let set_result = locked_state_store
                        .set(
                            key,
                            value,
                            request_timeout,
                            acquire_response.version,
                            set_options,
                        )
                        .await;

                    drop(locked_state_store);

                    let _ = self.release_lock(request_timeout).await;

                    match set_result {
                        Ok(set_response) => {
                            return Ok(Response::from_response(set_response));
                        }
                        Err(set_error) => {
                            return Err(set_error.into());
                        }
                    }
                }
            }
        }
    }

    /// Waits until a lock is acquired, deletes a key from the state store and releases the lock.
    ///
    /// Returns the number of keys deleted. Will be `0` if the key was not found, otherwise `1`
    ///
    /// # Errors
    /// [`Error`] of kind [`InvalidArgument`](ErrorKind::InvalidArgument) if the `request_timeout` is < 1 ms or > `u32::max`
    ///
    /// [`Error`] of kind [`ServiceError`](ErrorKind::ServiceError) if the State Store returns an Error response
    ///
    /// [`Error`] of kind [`UnexpectedPayload`](ErrorKind::UnexpectedPayload) if the State Store returns a response that isn't valid for a `Del` request
    ///
    /// [`Error`] of kind [`AIOProtocolError`](ErrorKind::AIOProtocolError) if there are any underlying errors from [`CommandInvoker::invoke`]
    pub async fn acquire_lock_and_delete_value(
        &self,
        lock_expiration: Duration,
        request_timeout: Duration,
        key: Vec<u8>,
    ) -> Result<Response<i64>, Error> {
        loop {
            match self.acquire_lock(lock_expiration, request_timeout).await {
                Err(acquire_error) => {
                    match acquire_error.kind() {
                        ErrorKind::LockAlreadyHeld => continue, // Try to lock again.
                        _ => return Err(acquire_error), // Some other error that cannot be handle here.
                    }
                }
                Ok(acquire_response) => {
                    /* lock acquired, let's proceed. */

                    let locked_state_store = self.state_store.lock().await;

                    let del_result = locked_state_store
                        .del(key, acquire_response.version, request_timeout)
                        .await;

                    drop(locked_state_store);

                    let _ = self.release_lock(request_timeout).await;

                    match del_result {
                        Ok(del_response) => {
                            return Ok(Response::from_response(del_response));
                        }
                        Err(del_error) => {
                            return Err(del_error.into());
                        }
                    }
                }
            }
        }
    }

    /// Releases a lock if and only if requested by the lock holder (same client id).
    ///
    /// Returns the number of locks deleted. Will be `0` if the lock was not found, `-1` if this is not the current holder, otherwise `1`
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
    pub async fn release_lock(&self, request_timeout: Duration) -> Result<Response<i64>, Error> {
        let locked_state_store = self.state_store.lock().await;

        match locked_state_store
            .vdel(
                self.lock_name.clone(),
                self.lock_holder_name.clone(),
                None,
                request_timeout,
            )
            .await
        {
            Ok(state_store_response) => Ok(Response::from_response(state_store_response)),
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
        let locked_state_store = self.state_store.lock().await;

        let observe_result = locked_state_store
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
        let locked_state_store = self.state_store.lock().await;

        match locked_state_store
            .unobserve(self.lock_name.clone(), request_timeout)
            .await
        {
            Ok(state_store_response) => Ok(Response::from_response(state_store_response)),
            Err(state_store_error) => Err(state_store_error.into()),
        }
    }

    /// Gets the name of the holder of a lock
    ///
    /// Returns `Some(<holder of the lock>)` if the lock is found or `None` if the lock was not found
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
        lock_name: Vec<u8>,
        request_timeout: Duration,
    ) -> Result<Response<Option<Vec<u8>>>, Error> {
        let locked_state_store = self.state_store.lock().await;

        match locked_state_store
            .get(lock_name.clone(), request_timeout)
            .await
        {
            Ok(state_store_response) => Ok(Response::from_response(state_store_response)),
            Err(state_store_error) => Err(state_store_error.into()),
        }
    }
}
