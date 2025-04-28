// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

//! Client for Key Lease operations.

use std::{sync::Arc, time::Duration};

use crate::leased_lock::{Error, ErrorKind, LeaseObservation, Response, SetCondition, SetOptions};
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
    key_name: Vec<u8>,
    lease_holder_name: Vec<u8>,
}

/// Key Lease client implementation
///
/// Notes:
/// Do not call any of the methods of this client after the `state_store` parameter is shutdown.
/// Calling any of the methods in this implementation after the `state_store` is shutdown results in undefined behavior.
impl<C> Client<C>
where
    C: ManagedClient + Clone + Send + Sync,
    C::PubReceiver: Send + Sync,
{
    /// Create a new Key Lease Client.
    ///
    /// Notes:
    /// - `lease_holder_name` is expected to be the client ID used in the underlying MQTT connection settings.
    /// - There must be one instance of `leased_lock::Client` per lease.
    ///
    /// # Errors
    /// [`struct@Error`] of kind [`KeyLengthZero`](ErrorKind::KeyLengthZero) if the `key_name` is empty
    ///
    /// [`struct@Error`] of kind [`LeaseHolderNameLengthZero`](ErrorKind::LeaseHolderNameLengthZero) if the `lease_holder_name` is empty
    pub fn new(
        state_store: Arc<state_store::Client<C>>,
        key_name: Vec<u8>,
        lease_holder_name: Vec<u8>,
    ) -> Result<Self, Error> {
        if key_name.is_empty() {
            return Err(Error(ErrorKind::KeyLengthZero));
        }

        if lease_holder_name.is_empty() {
            return Err(Error(ErrorKind::LeaseHolderNameLengthZero));
        }

        Ok(Self {
            state_store,
            key_name,
            lease_holder_name,
        })
    }

    /// Attempts to acquire a lease, returning if it cannot be acquired after one attempt.
    ///
    /// `lease_expiration` is how long the lease will remain held in the State Store after acquired, if not released before then.
    /// `request_timeout` is the maximum time the function will wait for receiving a response from the State Store service, it is rounded up to the nearest second.
    ///
    /// Returns Ok with a fencing token (`HybridLogicalClock`) if completed successfully, or `Error(KeyAlreadyLeased)` if lease is not acquired.
    /// # Errors
    /// [`struct@Error`] of kind [`InvalidArgument`](ErrorKind::InvalidArgument) if the `request_timeout` is zero or > `u32::max`
    ///
    /// [`struct@Error`] of kind [`ServiceError`](ErrorKind::ServiceError) if the State Store returns an Error response
    ///
    /// [`struct@Error`] of kind [`UnexpectedPayload`](ErrorKind::UnexpectedPayload) if the State Store returns a response that isn't valid for a `Set` request
    ///
    /// [`struct@Error`] of kind [`AIOProtocolError`](ErrorKind::AIOProtocolError) if there are any underlying errors from the command invoker
    ///
    /// [`struct@Error`] of kind [`KeyAlreadyLeased`](ErrorKind::KeyAlreadyLeased) if the `lease` is already in use by another holder
    /// # Panics
    /// Possible panic if, for some error, the fencing token (a.k.a. `version`) of the acquired lease is None.
    pub async fn try_acquire(
        &self,
        lease_expiration: Duration,
        request_timeout: Duration,
    ) -> Result<HybridLogicalClock, Error> {
        let state_store_response = self
            .state_store
            .set(
                self.key_name.clone(),
                self.lease_holder_name.clone(),
                request_timeout,
                None,
                SetOptions {
                    set_condition: SetCondition::OnlyIfEqualOrDoesNotExist,
                    expires: Some(lease_expiration),
                },
            )
            .await?;

        if state_store_response.response {
            Ok(state_store_response.version.expect(
                "Got None for fencing token. A lease without a fencing token is of no use.",
            ))
        } else {
            Err(Error(ErrorKind::KeyAlreadyLeased))
        }
    }

    /// Waits until a lease is available (if not already) and attempts to acquire it.
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
    pub async fn acquire(
        &self,
        lease_expiration: Duration,
        request_timeout: Duration,
    ) -> Result<HybridLogicalClock, Error> {
        // Logic:
        // a. Start observing lease within this function.
        // b. Try acquiring the lease and return if acquired or got an error other than `KeyAlreadyLeased`.
        // c. If got `KeyAlreadyLeased`, wait until `Del` notification for the lease. If notification is None, re-observe (start from a. again).
        // d. Loop back starting from b. above.
        // e. Unobserve lease before exiting.

        let mut observe_response = self.observe(request_timeout).await?;
        let mut acquire_result;

        loop {
            acquire_result = self.try_acquire(lease_expiration, request_timeout).await;

            match acquire_result {
                Ok(_) => {
                    break; /* lease acquired */
                }
                Err(ref acquire_error) => match acquire_error.kind() {
                    ErrorKind::KeyAlreadyLeased => { /* Must wait for lease to be released. */ }
                    _ => {
                        break;
                    }
                },
            };

            // Lease being held by another client. Wait for delete notification.
            loop {
                let Some((notification, _)) = observe_response.response.recv_notification().await
                else {
                    // If the state_store client gets disconnected (or shutdown), all the observation channels receive a None.
                    // In such case, as per design, we must re-observe the lease.
                    observe_response = self.observe(request_timeout).await?;
                    break;
                };

                if notification.operation == state_store::Operation::Del {
                    break;
                };
            }
        }

        match self.unobserve(request_timeout).await {
            Ok(_) => acquire_result,
            Err(unobserve_error) => Err(unobserve_error),
        }
    }

    /// Releases a lease if and only if requested by the lease holder (same client id).
    ///
    /// Note: `request_timeout` is rounded up to the nearest second.
    ///
    /// Returns `Ok()` if lease is no longer held by this `lease_holder`, or `Error` otherwise.
    /// # Errors
    /// [`struct@Error`] of kind [`InvalidArgument`](ErrorKind::InvalidArgument) if the `request_timeout` is zero or > `u32::max`
    ///
    /// [`struct@Error`] of kind [`ServiceError`](ErrorKind::ServiceError) if the State Store returns an Error response
    ///
    /// [`struct@Error`] of kind [`UnexpectedPayload`](ErrorKind::UnexpectedPayload) if the State Store returns a response that isn't valid for a `V Delete` request
    ///
    /// [`struct@Error`] of kind [`AIOProtocolError`](ErrorKind::AIOProtocolError) if there are any underlying errors from the command invoker
    pub async fn release(&self, request_timeout: Duration) -> Result<(), Error> {
        match self
            .state_store
            .vdel(
                self.key_name.clone(),
                self.lease_holder_name.clone(),
                None,
                request_timeout,
            )
            .await
        {
            Ok(_) => Ok(()),
            Err(state_store_error) => Err(state_store_error.into()),
        }
    }

    /// Starts observation of any changes on a lease
    ///
    /// Note: `request_timeout` is rounded up to the nearest second.
    ///
    /// Returns OK([`Response<LeaseObservation>`]) if the lease is now being observed.
    /// The [`LeaseObservation`] can be used to receive lease notifications for this lease
    ///
    /// <div class="warning">
    ///
    /// If a client disconnects, `observe` must be called again by the user.
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
    pub async fn observe(
        &self,
        request_timeout: Duration,
    ) -> Result<Response<LeaseObservation>, Error> {
        Ok(self
            .state_store
            .observe(self.key_name.clone(), request_timeout)
            .await?)
    }

    /// Stops observation of any changes on a lease.
    ///
    /// Note: `request_timeout` is rounded up to the nearest second.
    ///
    /// Returns `true` if the lease is no longer being observed or `false` if the lease wasn't being observed
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
    pub async fn unobserve(&self, request_timeout: Duration) -> Result<Response<bool>, Error> {
        Ok(self
            .state_store
            .unobserve(self.key_name.clone(), request_timeout)
            .await?)
    }

    /// Gets the name of the holder of a lease
    ///
    /// Note: `request_timeout` is rounded up to the nearest second.
    ///
    /// Returns `Some(<holder of the lease>)` if the lease is found or `None`
    /// if the lease was not found (i.e., was not acquired by anyone, already released or expired).
    ///
    /// # Errors
    /// [`struct@Error`] of kind [`InvalidArgument`](ErrorKind::InvalidArgument) if the `request_timeout` is zero or > `u32::max`
    ///
    /// [`struct@Error`] of kind [`ServiceError`](ErrorKind::ServiceError) if the State Store returns an Error response
    ///
    /// [`struct@Error`] of kind [`UnexpectedPayload`](ErrorKind::UnexpectedPayload) if the State Store returns a response that isn't valid for a `Get` request
    ///
    /// [`struct@Error`] of kind [`AIOProtocolError`](ErrorKind::AIOProtocolError) if there are any underlying errors from the command invoker
    pub async fn get_holder(
        &self,
        request_timeout: Duration,
    ) -> Result<Response<Option<Vec<u8>>>, Error> {
        Ok(self
            .state_store
            .get(self.key_name.clone(), request_timeout)
            .await?)
    }
}
