// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

//! Client for Key Lease operations.

use std::{sync::Arc, sync::Mutex, time::Duration};

use tokio::select;
use tokio_util::sync::CancellationToken;

use crate::leased_lock::{Error, ErrorKind, LeaseObservation, Response, SetCondition, SetOptions};
use crate::state_store::{self};
use azure_iot_operations_mqtt::interface::ManagedClient;
use azure_iot_operations_protocol::common::hybrid_logical_clock::HybridLogicalClock;

/// Key Lease client struct.
#[derive(Clone)]
pub struct Client<C>
where
    C: ManagedClient + Clone + Send + Sync + 'static,
    C::PubReceiver: Send + Sync,
{
    state_store: Arc<state_store::Client<C>>,
    lease_name: Vec<u8>,
    lease_holder_name: Vec<u8>,
    current_fencing_token: Arc<Mutex<Option<HybridLogicalClock>>>,
    auto_renewal_cancellation_token: CancellationToken,
}

impl<C> Drop for Client<C>
where
    C: ManagedClient + Clone + Send + Sync,
    C::PubReceiver: Send + Sync,
{
    fn drop(&mut self) {
        if !self.auto_renewal_cancellation_token.is_cancelled() {
            self.auto_renewal_cancellation_token.cancel();
        }
    }
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
    /// [`struct@Error`] of kind [`KeyLengthZero`](ErrorKind::KeyLengthZero) if the `lease_name` is empty
    ///
    /// [`struct@Error`] of kind [`LeaseHolderNameLengthZero`](ErrorKind::LeaseHolderNameLengthZero) if the `lease_holder_name` is empty
    pub fn new(
        state_store: Arc<state_store::Client<C>>,
        lease_name: Vec<u8>,
        lease_holder_name: Vec<u8>,
    ) -> Result<Self, Error> {
        if lease_name.is_empty() {
            return Err(Error(ErrorKind::KeyLengthZero));
        }

        if lease_holder_name.is_empty() {
            return Err(Error(ErrorKind::LeaseHolderNameLengthZero));
        }

        Ok(Self {
            state_store,
            lease_name,
            lease_holder_name,
            current_fencing_token: Arc::new(Mutex::new(None)),
            auto_renewal_cancellation_token: CancellationToken::new(),
        })
    }

    /// Gets the latest fencing token related to the most recent lease.
    ///
    /// Returns either None or an actual Fencing Token (`HybridLogicalClock`).
    /// None means that either a lease has not been acquired previously with this client, or
    /// if a lease renewal has failed (if lease auto-renewal is used). The presence of a `HybridLogicalClock`
    /// does not mean that it is the most recent (and thus valid) Fencing Token - in case
    /// auto-renewal has not been used and the lease has already expired.
    ///
    /// # Panics
    /// Possible panic if `std::Lock::lock()` fails.
    #[must_use]
    pub fn get_current_lease_fencing_token(&self) -> Option<HybridLogicalClock> {
        self.current_fencing_token
            .lock()
            .expect("Could not lock mutex")
            .clone()
    }

    async fn internal_acquire(
        &mut self,
        lease_expiration: Duration,
        request_timeout: Duration,
    ) -> Result<HybridLogicalClock, Error> {
        let a = self
            .state_store
            .set(
                self.lease_name.clone(),
                self.lease_holder_name.clone(),
                request_timeout,
                None,
                SetOptions {
                    set_condition: SetCondition::OnlyIfEqualOrDoesNotExist,
                    expires: Some(lease_expiration),
                },
            )
            .await;

        let state_store_response = a?;

        if state_store_response.response {
            self.current_fencing_token
                .lock()
                .expect("Could not lock mutex")
                .clone_from(&state_store_response.version);

            Ok(state_store_response.version.expect(
                "Got None for fencing token. A lease without a fencing token is of no use.",
            ))
        } else {
            *self
                .current_fencing_token
                .lock()
                .expect("Could not lock mutex") = None;

            Err(Error(ErrorKind::KeyAlreadyLeased))
        }
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
    pub async fn acquire(
        &mut self,
        lease_expiration: Duration,
        request_timeout: Duration,
        renewal_period: Option<Duration>,
    ) -> Result<HybridLogicalClock, Error> {
        // Stop auto-renewal.
        if !self.auto_renewal_cancellation_token.is_cancelled() {
            self.auto_renewal_cancellation_token.cancel();
        }

        let acquire_result = self
            .internal_acquire(lease_expiration, request_timeout)
            .await;

        if renewal_period.is_some() {
            self.auto_renewal_cancellation_token = CancellationToken::new();
            let mut self_clone = self.clone();

            tokio::task::spawn({
                async move {
                    loop {
                        select! {
                            () = self_clone.auto_renewal_cancellation_token.cancelled() => {
                                break; // Auto-renewal is cancelled.
                            }
                            () = tokio::time::sleep(renewal_period.unwrap()) => {}
                        }

                        if self_clone
                            .internal_acquire(lease_expiration, request_timeout)
                            .await
                            .is_err()
                        {
                            // Acquire failed. Stopping Auto-renewal.
                            break;
                        }
                    }
                }
            });
        }

        acquire_result
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
    ///
    /// # Panics
    /// Possible panic if `std::Lock::lock()` fails.
    pub async fn release(&mut self, request_timeout: Duration) -> Result<(), Error> {
        // Stop auto-renewal.
        if !self.auto_renewal_cancellation_token.is_cancelled() {
            self.auto_renewal_cancellation_token.cancel();
        }

        *self
            .current_fencing_token
            .lock()
            .expect("Could not lock mutex") = None;

        match self
            .state_store
            .vdel(
                self.lease_name.clone(),
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
            .observe(self.lease_name.clone(), request_timeout)
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
            .unobserve(self.lease_name.clone(), request_timeout)
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
            .get(self.lease_name.clone(), request_timeout)
            .await?)
    }
}
