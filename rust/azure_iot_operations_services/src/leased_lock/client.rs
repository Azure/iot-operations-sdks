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
    lock_holder_name: Vec<u8>,
}

/// Leased Lock client implementation
impl<C> Client<C>
where
    C: ManagedClient + Clone + Send + Sync,
    C::PubReceiver: Send + Sync,
{
    /// Create a new Leased Lock Client
    ///
    /// Note: `lock_holder_name` is expected to be the client ID used in the underlying MQTT connection settings.
    ///
    /// # Errors
    /// [`Error`] of kind [`AIOProtocolError`](ErrorKind::AIOProtocolError) is possible if
    ///     there are any errors creating the underlying command invoker or telemetry receiver, but it should not happen.
    ///
    /// # Panics
    /// Possible panics when building options for the underlying command invoker or telemetry receiver,
    /// but they should be unreachable because we control the static parameters that go into these calls.
    #[allow(clippy::needless_pass_by_value)]
    pub fn new(
        state_store: Arc<Mutex<state_store::Client<C>>>,
        lock_holder_name: Vec<u8>,
    ) -> Result<Self, Error> {
        Ok(Self {
            state_store,
            lock_holder_name,
        })
    }

    /// Attempts to acquire a lock, returning immediately if it cannot be acquired.
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
    /// [`Error`] of kind [`LockAlreadyInUse`](ErrorKind::LockAlreadyInUse) if the `lock` is already in use by another holder
    pub async fn try_acquire_lock(
        &self,
        lock: Vec<u8>,
        lock_expiration: Duration,
        request_timeout: Duration,
    ) -> Result<Response<bool>, Error> {
        let locked_state_store = self.state_store.lock().await;

        match locked_state_store
            .set(
                lock,
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
                    Err(Error(ErrorKind::LockAlreadyInUse))
                }
            },
            Err(state_store_error) => Err(state_store_error.into()),
        }
    }

    /// Waits until a lock is available (if not already) and attempts to acquire it.
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
    /// [`Error`] of kind [`LockAlreadyInUse`](ErrorKind::LockAlreadyInUse) if the `lock` is already in use by another holder
    pub async fn acquire_lock(
        &self,
        lock: Vec<u8>,
        lock_expiration: Duration,
        request_timeout: Duration,
    ) -> Result<Response<bool>, Error> {
        if lock.is_empty() {
            return Err(Error(ErrorKind::LockNameLengthZero));
        }

        // Logic:
        // If user already observing lock, unobserve it.
        // Start observing lock within this function.
        // If succeeds, wait for observe notification until lock deleted
        // try acquire again, repeat while error is retriable (?); exit loop once acquire succeeds.
        // Unobserve lock before exiting.

        let mut observe_result = self.observe_lock(lock.clone(), request_timeout).await;

        match observe_result {
            Ok(_) => {}
            Err(observe_error) => {
                return Err(observe_error);
            }
        }

        let mut acquire_result;

        loop {
            acquire_result = self
                .try_acquire_lock(lock.clone(), lock_expiration, request_timeout)
                .await;

            match acquire_result {
                Ok(ref acquire_response) => if acquire_response.response { break /* Lock acquired */ },
                Err(_) => break,
            };

            // Lock being held by another client. Wait for delete notification.
            if let Ok(ref mut response) = observe_result {
                while let Some((notification, _)) = response.response.recv_notification().await {
                    if notification.operation == state_store::Operation::Del {
                        break;
                    }
                }
            }
        }

        let _ = self.unobserve_lock(lock.clone(), request_timeout).await;

        acquire_result
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
    pub async fn release_lock(
        &self,
        lock: Vec<u8>,
        request_timeout: Duration,
    ) -> Result<Response<i64>, Error> {
        let locked_state_store = self.state_store.lock().await;

        let vdel_result = locked_state_store
            .vdel(
                lock.clone(),
                self.lock_holder_name.clone(),
                None,
                request_timeout,
            )
            .await;

        match vdel_result {
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
        lock: Vec<u8>,
        request_timeout: Duration,
    ) -> Result<Response<LockObservation>, Error> {
        let locked_state_store = self.state_store.lock().await;

        let observe_result = locked_state_store.observe(lock, request_timeout).await;

        match observe_result {
            Ok(state_store_response) => Ok(state_store_response.into()),
            Err(state_store_error) => Err(state_store_error.into()),
        }
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
    pub async fn unobserve_lock(
        &self,
        lock: Vec<u8>,
        request_timeout: Duration,
    ) -> Result<Response<bool>, Error> {
        let locked_state_store = self.state_store.lock().await;

        let unobserve_result = locked_state_store.unobserve(lock, request_timeout).await;

        match unobserve_result {
            Ok(state_store_response) => Ok(Response::from_response(state_store_response)),
            Err(state_store_error) => Err(state_store_error.into()),
        }
    }

    /// Gets the name of the holder of a lock
    ///
    /// Returns `Some(<holder of the lock>)` if the lock is found or `None` if the lock was not found
    /// # Errors
    /// [`Error`] of kind [`LockNameLengthZero`](ErrorKind::LockNameLengthZero) if the `lock` is empty
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
        lock: Vec<u8>,
        timeout: Duration,
    ) -> Result<Response<Option<Vec<u8>>>, Error> {
        let locked_state_store = self.state_store.lock().await;

        let get_result = locked_state_store.get(lock.clone(), timeout).await;

        match get_result {
            Ok(state_store_response) => Ok(Response::from_response(state_store_response)),
            Err(state_store_error) => Err(state_store_error.into()),
        }
    }
}
