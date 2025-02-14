// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

//! Client for Lease Lock operations.

use std:: {
    time::Duration,
    sync::Arc
};

use tokio::sync::Mutex;

use crate::state_store::{self, KeyObservation, SetCondition, SetOptions, StateStoreError};
use azure_iot_operations_mqtt::interface::ManagedClient;

/// Leased Lock client state
pub struct Client<C>
where
    C: ManagedClient + Clone + Send + Sync + 'static,
    C::PubReceiver: Send + Sync,
{
    dss_client: Arc<Mutex<state_store::Client<C>>>,
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
    /// # Errors
    /// [`StateStoreError`] of kind [`AIOProtocolError`](StateStoreErrorKind::AIOProtocolError) is possible if
    ///     there are any errors creating the underlying command invoker or telemetry receiver, but it should not happen
    ///
    /// # Panics
    /// Possible panics when building options for the underlying command invoker or telemetry receiver,
    /// but they should be unreachable because we control the static parameters that go into these calls.
    #[allow(clippy::needless_pass_by_value)]
    pub fn new(dss_client: Arc<Mutex<state_store::Client<C>>>, lock_holder_name: Vec<u8>) -> Result<Self, StateStoreError> {
        Ok(Self {
            dss_client,
            lock_holder_name,
        })
    }

    /// Attempts to set a lock key in the State Store Service
    ///
    /// Note: timeout refers to the duration until the State Store Client stops
    /// waiting for a `Set` response from the Service. This value is not linked
    /// to the key in the State Store.
    ///
    /// Returns `true` if completed successfully, or `false` if lock key not set.
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
    pub async fn acquire_lock(
        &self,
        key: Vec<u8>,
        expiration: Duration,
        timeout: Duration,
    ) -> Result<state_store::Response<bool>, StateStoreError> {
        let cloned_dss_client = self.dss_client.clone();
        let locked_dss_client = cloned_dss_client.lock().await;

        locked_dss_client
            .set(
                key,
                self.lock_holder_name.clone(),
                timeout,
                None,
                SetOptions {
                    set_condition: SetCondition::OnlyIfEqualOrDoesNotExist,
                    expires: Some(expiration)
                },
            )
            .await
    }

    /// Deletes a lock key from the State Store Service if and only if requested by the lock holder (same client id).
    ///
    /// Note: timeout refers to the duration until the State Store Client stops
    /// waiting for the response from the Service. This value is not linked
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
    pub async fn release_lock(
        &self,
        key: Vec<u8>,
        timeout: Duration,
    ) -> Result<state_store::Response<i64>, StateStoreError> {
        let cloned_dss_client = self.dss_client.clone();
        let locked_dss_client = cloned_dss_client.lock().await;

        locked_dss_client
            .vdel(key.clone(), self.lock_holder_name.clone(), None, timeout)
            .await
    }

    /// Starts observation of any changes on a lock key from the State Store Service
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
    pub async fn observe_lock(
        &self,
        key: Vec<u8>,
        timeout: Duration,
    ) -> Result<state_store::Response<KeyObservation>, StateStoreError> {
        let cloned_dss_client = self.dss_client.clone();
        let locked_dss_client = cloned_dss_client.lock().await;

        locked_dss_client.observe(key, timeout).await
    }

    /// Stops observation of any changes on a lock key from the State Store Service
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
    pub async fn unobserve_lock(
        &self,
        key: Vec<u8>,
        timeout: Duration,
    ) -> Result<state_store::Response<bool>, StateStoreError> {
        let cloned_dss_client = self.dss_client.clone();
        let locked_dss_client = cloned_dss_client.lock().await;

        locked_dss_client.unobserve(key, timeout).await
    }

    /// Gets the holder of a lock key in the State Store Service
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
    pub async fn get_lock_holder(
        &self,
        key: Vec<u8>,
        timeout: Duration,
    ) -> Result<state_store::Response<Option<Vec<u8>>>, StateStoreError> {
        let cloned_dss_client = self.dss_client.clone();
        let locked_dss_client = cloned_dss_client.lock().await;

        locked_dss_client.get(key.clone(), timeout).await
    }

    /// Enables the auto-renewal of the lock duration.
    pub fn enable_auto_renewal(&self, _key: &[u8])
    /* -> Result<state_store::Response<bool>, StateStoreError> */
    {
        unimplemented!();
    }

    /// Disables the auto-renewal of the lock duration.
    pub fn disable_auto_renewal(&self, _key: &[u8])
    /* -> Result<state_store::Response<bool>, StateStoreError> */
    {
        unimplemented!();
    }

    /// Shutdown the [`leased_lock::Client`]. Shuts down the underlying `state_store` client.
    ///
    /// Note: If this method is called, the [`leased_lock::Client`] should not be used again.
    /// If the method returns an error, it may be called again to attempt the unsubscribe again.
    ///
    /// Returns Ok(()) on success, otherwise returns [`StateStoreError`].
    /// # Errors
    /// [`StateStoreError`] of kind [`AIOProtocolError`](StateStoreErrorKind::AIOProtocolError) if the unsubscribe fails or if the unsuback reason code doesn't indicate success.
    pub async fn shutdown(&self) -> Result<(), StateStoreError> {
        let cloned_dss_client = self.dss_client.clone();
        let locked_dss_client = cloned_dss_client.lock().await;

        locked_dss_client.shutdown().await
    }
}
