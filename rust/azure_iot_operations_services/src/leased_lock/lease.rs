// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

//! Client for Lease operations.

use std::{sync::Arc, sync::Mutex, time::Duration};

use tokio::select;
use tokio_util::sync::CancellationToken;

use crate::leased_lock::{Error, ErrorKind, LeaseObservation, SetCondition, SetOptions};
use crate::state_store;
use azure_iot_operations_mqtt::interface::ManagedClient;
use azure_iot_operations_protocol::common::hybrid_logical_clock::HybridLogicalClock;

/// Lease client struct.
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
        self.auto_renewal_cancellation_token.cancel();
    }
}

/// Lease client implementation
///
/// Notes:
/// Do not call any of the methods of this client after the `state_store` parameter is shutdown.
/// Calling any of the methods in this implementation after the `state_store` is shutdown results in undefined behavior.
impl<C> Client<C>
where
    C: ManagedClient + Clone + Send + Sync,
    C::PubReceiver: Send + Sync,
{
    /// Create a new Lease Client.
    ///
    /// Notes:
    /// - `lease_holder_name` is expected to be the client ID used in the underlying MQTT connection settings.
    /// - There must be only one instance of `lease::Client` per lease.
    ///
    /// # Errors
    /// [`struct@Error`] of kind [`InvalidArgument`](ErrorKind::InvalidArgument) if the either `lease_name` or `lease_holder_name` is empty.
    pub fn new(
        state_store: Arc<state_store::Client<C>>,
        lease_name: Vec<u8>,
        lease_holder_name: Vec<u8>,
    ) -> Result<Self, Error> {
        if lease_name.is_empty() {
            return Err(Error(ErrorKind::InvalidArgument(
                "lease_name is empty".to_string(),
            )));
        }

        if lease_holder_name.is_empty() {
            return Err(Error(ErrorKind::InvalidArgument(
                "lease_holder_name is empty".to_string(),
            )));
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
    /// a lease renewal has failed (if lease auto-renewal is used). The presence of a `HybridLogicalClock`
    /// does not mean that it is the most recent (and thus valid) Fencing Token - this can
    /// happen in the scenario where auto-renewal has not been used and the lease has already expired.
    ///
    /// # Panics
    /// If the lock on the `current_fencing_token` is poisoned, which should not be possible.
    #[must_use]
    pub fn current_lease_fencing_token(&self) -> Option<HybridLogicalClock> {
        self.current_fencing_token.lock().unwrap().clone()
    }

    async fn internal_acquire(
        &self,
        lease_expiration: Duration,
        request_timeout: Duration,
    ) -> Result<HybridLogicalClock, Error> {
        let state_store_response = self
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
            .await?;

        if state_store_response.response {
            self.current_fencing_token
                .lock()
                .unwrap()
                .clone_from(&state_store_response.version);

            state_store_response
                .version
                .ok_or(Error(ErrorKind::MissingFencingToken))
        } else {
            *self.current_fencing_token.lock().unwrap() = None;

            Err(Error(ErrorKind::LeaseAlreadyHeld))
        }
    }

    /// Attempts to acquire a lease, returning if it cannot be acquired after one attempt.
    ///
    /// `lease_expiration` is how long the lease will remain held in the State Store after acquired, if not released before then.
    /// `request_timeout` is the maximum time the function will wait for receiving a response from the State Store service, it is rounded up to the nearest second.
    /// `renewal_period` is the frequency with which the lease will be auto-renewed by the lease client if acquired successfully. `None` (or zero) indicates the lease should not be auto-renewed.
    ///
    /// Returns Ok with a fencing token (`HybridLogicalClock`) if completed successfully, or `Error` if lease is not acquired.
    /// # Errors
    /// [`struct@Error`] of kind [`InvalidArgument`](ErrorKind::InvalidArgument) if the `request_timeout` is zero or > `u32::max`
    ///
    /// [`struct@Error`] of kind [`ServiceError`](ErrorKind::ServiceError) if the State Store returns an Error response
    ///
    /// [`struct@Error`] of kind [`UnexpectedPayload`](ErrorKind::UnexpectedPayload) if the State Store returns a response that isn't valid for a `Set` request
    ///
    /// [`struct@Error`] of kind [`AIOProtocolError`](ErrorKind::AIOProtocolError) if there are any underlying errors from the command invoker
    ///
    /// [`struct@Error`] of kind [`LeaseAlreadyHeld`](ErrorKind::LeaseAlreadyHeld) if the `lease` is already in use by another holder
    ///
    /// [`struct@Error`] of kind [`MissingFencingToken`](ErrorKind::MissingFencingToken) if the fencing token in the service response is empty.
    pub async fn acquire(
        &mut self,
        lease_expiration: Duration,
        request_timeout: Duration,
        renewal_period: Option<Duration>,
    ) -> Result<HybridLogicalClock, Error> {
        if renewal_period.is_some_and(|rp| rp >= lease_expiration) {
            return Err(Error(ErrorKind::InvalidArgument(
                "renewal_period must be less than lease_expiration".to_string(),
            )));
        }

        // Stop auto-renewal.
        self.auto_renewal_cancellation_token.cancel();

        let acquire_result = self
            .internal_acquire(lease_expiration, request_timeout)
            .await;

        if let Some(renewal_period) = renewal_period {
            if renewal_period > Duration::ZERO {
                self.auto_renewal_cancellation_token = CancellationToken::new();
                let self_clone = self.clone();

                tokio::task::spawn({
                    async move {
                        loop {
                            select! {
                                () = self_clone.auto_renewal_cancellation_token.cancelled() => {
                                    break; // Auto-renewal is cancelled.
                                }
                                () = tokio::time::sleep(renewal_period) => {
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
                        }
                    }
                });
            }
        }

        acquire_result
    }

    /// Releases a lease if and only if requested by the lease holder (same client id).
    ///
    /// Note: `request_timeout` is rounded up to the nearest second.
    ///
    /// Returns `Ok()` if lease is no longer held by this `lease_holder`, or `Error` otherwise.
    ///
    /// Even if this method fails the current fencing token (obtained by calling `current_lease_fencing_token()`) is cleared
    /// and the auto-renewal task is cancelled (if the lease was acquired using auto-renewal).
    ///
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
    /// If the lock on the `current_fencing_token` is poisoned, which should not be possible.
    pub async fn release(&self, request_timeout: Duration) -> Result<(), Error> {
        // Stop auto-renewal.
        self.auto_renewal_cancellation_token.cancel();

        *self.current_fencing_token.lock().unwrap() = None;

        self.state_store
            .vdel(
                self.lease_name.clone(),
                self.lease_holder_name.clone(),
                None,
                request_timeout,
            )
            .await?;

        Ok(())
    }

    /// Starts observation of any changes on a lease
    ///
    /// Note: `request_timeout` is rounded up to the nearest second.
    ///
    /// Returns OK([`LeaseObservation`]) if the lease is now being observed.
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
    pub async fn observe(&self, request_timeout: Duration) -> Result<LeaseObservation, Error> {
        Ok(self
            .state_store
            .observe(self.lease_name.clone(), request_timeout)
            .await?
            .response)
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
    pub async fn unobserve(&self, request_timeout: Duration) -> Result<bool, Error> {
        Ok(self
            .state_store
            .unobserve(self.lease_name.clone(), request_timeout)
            .await?
            .response)
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
    pub async fn get_holder(&self, request_timeout: Duration) -> Result<Option<Vec<u8>>, Error> {
        Ok(self
            .state_store
            .get(self.lease_name.clone(), request_timeout)
            .await?
            .response)
    }
}
