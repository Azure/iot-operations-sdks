// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

//! Client for Lock operations.

use std::{sync::Arc, time::Duration};

use crate::leased_lock::{Error, ErrorKind, lease::Client as LeaseClient};
use crate::state_store::{self};
use azure_iot_operations_mqtt::interface::ManagedClient;
use azure_iot_operations_protocol::common::hybrid_logical_clock::HybridLogicalClock;

/// Lock client struct.
pub struct Client<C>
where
    C: ManagedClient + Clone + Send + Sync + 'static,
    C::PubReceiver: Send + Sync,
{
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
    /// - There must be one instance of `lock::Client` per lock.
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

        let lease_client = LeaseClient::new(state_store, lock_name, lock_holder_name)?;

        Ok(Self { lease_client })
    }

    /// Waits until a lock is available (if not already) and attempts to acquire it.
    ///
    /// If a non-zero `Duration` is provided as `renewal_period`, the lock is automatically renewed
    /// after every consecutive elapse of `renewal_period` until the lock is released or a re-acquire failure occurs.
    /// If automatic lock renewal is used, `current_lock_fencing_token()` must be used to access the most up-to-date
    /// fencing token (see function documentation).
    ///
    /// Note: `request_timeout` is rounded up to the nearest second.
    ///
    /// Returns Ok with a fencing token (`HybridLogicalClock`) if completed successfully, or an `Error` if any failure occurs.
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
    pub async fn lock(
        &mut self,
        lock_expiration: Duration,
        request_timeout: Duration,
        renewal_period: Option<Duration>,
    ) -> Result<HybridLogicalClock, Error> {
        // Logic:
        // a. Start observing lease within this function.
        // b. Try acquiring the lease and return if acquired or got an error other than `KeyAlreadyLeased`.
        // c. If got `KeyAlreadyLeased`, wait until `Del` notification for the lease. If notification is None, re-observe (start from a. again).
        // d. Loop back starting from b. above.
        // e. Unobserve lease before exiting.

        let mut observe_response = self.lease_client.observe(request_timeout).await?;
        let mut acquire_result;

        loop {
            acquire_result = self
                .lease_client
                .acquire(lock_expiration, request_timeout, renewal_period)
                .await;

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
                let Some((notification, _)) = observe_response.recv_notification().await else {
                    // If the state_store client gets disconnected (or shutdown), all the observation channels receive a None.
                    // In such case, as per design, we must re-observe the lease.
                    observe_response = self.lease_client.observe(request_timeout).await?;
                    break;
                };

                if notification.operation == state_store::Operation::Del {
                    break;
                };
            }
        }

        _ = self.lease_client.unobserve(request_timeout).await?;

        acquire_result
    }

    /// Releases a lock.
    ///
    /// Note: `request_timeout` is rounded up to the nearest second.
    ///
    /// Returns `Ok()` if lock is no longer held by this `lock holder`, or `Error` otherwise.
    /// # Errors
    /// [`struct@Error`] of kind [`InvalidArgument`](ErrorKind::InvalidArgument) if the `request_timeout` is zero or > `u32::max`
    ///
    /// [`struct@Error`] of kind [`ServiceError`](ErrorKind::ServiceError) if the State Store returns an Error response
    ///
    /// [`struct@Error`] of kind [`UnexpectedPayload`](ErrorKind::UnexpectedPayload) if the State Store returns a response that isn't valid for a `V Delete` request
    ///
    /// [`struct@Error`] of kind [`AIOProtocolError`](ErrorKind::AIOProtocolError) if there are any underlying errors from the command invoker
    pub async fn unlock(&mut self, request_timeout: Duration) -> Result<(), Error> {
        self.lease_client.release(request_timeout).await
    }

    /// Gets the latest fencing token related to the most recent lock.
    ///
    /// Returns either None or an actual Fencing Token (`HybridLogicalClock`).
    /// None means that either a lock has not been acquired previously with this client, or
    /// a lock renewal has failed (if lock auto-renewal is used). The presence of a `HybridLogicalClock`
    /// does not mean that it is the most recent (and thus valid) Fencing Token - this can
    /// happen in the scenario where auto-renewal has not been used and the lease has already expired.
    #[must_use]
    pub fn current_lock_fencing_token(&self) -> Option<HybridLogicalClock> {
        self.lease_client.current_lease_fencing_token()
    }
}
