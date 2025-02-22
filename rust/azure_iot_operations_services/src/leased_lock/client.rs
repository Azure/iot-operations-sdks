// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

//! Client for Lease Lock operations.

use std:: {
    time::Duration,
    sync::Arc
};

use tokio::sync::Mutex;

use crate::state_store::{
    self,
    SetCondition,
    SetOptions 
};
use azure_iot_operations_mqtt::interface::ManagedClient;
use crate::leased_lock::{
    Error,
    Response,
    LockObservation
};

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
    /// # Errors
    /// [`Error`] of kind [`AIOProtocolError`](ErrorKind::AIOProtocolError) is possible if
    ///     there are any errors creating the underlying command invoker or telemetry receiver, but it should not happen.
    ///
    /// # Panics
    /// Possible panics when building options for the underlying command invoker or telemetry receiver,
    /// but they should be unreachable because we control the static parameters that go into these calls.
    #[allow(clippy::needless_pass_by_value)]
    pub fn new(state_store: Arc<Mutex<state_store::Client<C>>>, lock_holder_name: Vec<u8>) -> Result<Self, Error> {
        Ok(Self {
            state_store,
            lock_holder_name,
        })
    }

    /// Attempts to set a lock key in the State Store Service
    ///
    /// Note: timeout refers to the duration until the State Store Client stops
    /// waiting for a `Set` response from the Service. This value is not linked
    /// to the key in the State Store.
    ///
    /// Returns `true` if completed successfully, or `false` if lock key not acquired.
    /// # Errors
    /// [`Error`] of kind [`LockNameLengthZero`](ErrorKind::LockNameLengthZero) if the `lock` is empty
    ///
    /// [`Error`] of kind [`InvalidArgument`](ErrorKind::InvalidArgument) if the `timeout` is < 1 ms or > `u32::max`
    ///
    /// [`Error`] of kind [`ServiceError`](ErrorKind::ServiceError) if the State Store returns an Error response
    ///
    /// [`Error`] of kind [`UnexpectedPayload`](ErrorKind::UnexpectedPayload) if the State Store returns a response that isn't valid for a `Set` request
    ///
    /// [`Error`] of kind [`AIOProtocolError`](ErrorKind::AIOProtocolError) if there are any underlying errors from [`CommandInvoker::invoke`]
    pub async fn acquire_lock(
        &self,
        lock: Vec<u8>,
        expiration: Duration,
        timeout: Duration,
    ) -> Result<Response<bool>, Error> {
        let cloned_state_store = self.state_store.clone();
        let locked_state_store = cloned_state_store.lock().await;

        match locked_state_store
            .set(
                lock,
                self.lock_holder_name.clone(),
                timeout,
                None,
                SetOptions {
                    set_condition: SetCondition::OnlyIfEqualOrDoesNotExist,
                    expires: Some(expiration)
                },
            )
            .await {
                Ok(state_store_response) => Ok(Response::from_response(state_store_response)),
                Err(state_store_error) => Err(state_store_error.into())
            }
    }

    /// Deletes a lock key from the State Store Service if and only if requested by the lock holder (same client id).
    ///
    /// Note: timeout refers to the duration until the State Store Client stops
    /// waiting for the response from the Service. This value is not linked
    /// to the key in the State Store.
    ///
    /// Returns the number of keys deleted. Will be `0` if the key was not found, `-1` if the value did not match, otherwise `1`
    /// # Errors
    /// [`Error`] of kind [`LockNameLengthZero`](ErrorKind::LockNameLengthZero) if the `key` is empty
    ///
    /// [`Error`] of kind [`InvalidArgument`](ErrorKind::InvalidArgument) if the `timeout` is < 1 ms or > `u32::max`
    ///
    /// [`Error`] of kind [`ServiceError`](ErrorKind::ServiceError) if the State Store returns an Error response
    ///
    /// [`Error`] of kind [`UnexpectedPayload`](ErrorKind::UnexpectedPayload) if the State Store returns a response that isn't valid for a `V Delete` request
    ///
    /// [`Error`] of kind [`AIOProtocolError`](ErrorKind::AIOProtocolError) if there are any underlying errors from [`CommandInvoker::invoke`]
    pub async fn release_lock(
        &self,
        key: Vec<u8>,
        timeout: Duration,
    ) -> Result<Response<i64>, Error> { // TODO: change this to bool? Look into how other languages are doing it.
        let cloned_state_store = self.state_store.clone();
        let locked_state_store = cloned_state_store.lock().await;

        let vdel_result = locked_state_store
            .vdel(key.clone(), self.lock_holder_name.clone(), None, timeout)
            .await;

        match vdel_result  {
            Ok(state_store_response) => Ok(Response::from_response(state_store_response)),
            Err(state_store_error) => Err(state_store_error.into())
        }
    }

    /// Starts observation of any changes on a lock
    ///
    /// Returns OK([`leased_lock::Response<LockObservation>`]) if the lock is now being observed.
    /// The [`LockObservation`] can be used to receive key notifications for this key
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
    /// [`Error`] of kind [`LockNameLengthZero`](ErrorKind::LockNameLengthZero) if
    /// - the `key` is empty
    ///
    /// [`Error`] of kind [`InvalidArgument`](ErrorKind::InvalidArgument) if
    /// - the `timeout` is < 1 ms or > `u32::max`
    ///
    /// [`Error`] of kind [`ServiceError`](ErrorKind::ServiceError) if
    /// - the State Store returns an Error response
    /// - the State Store returns a response that isn't valid for an `Observe` request
    ///
    /// [`Error`] of kind [`AIOProtocolError`](ErrorKind::AIOProtocolError) if
    /// - there are any underlying errors from [`CommandInvoker::invoke`]
    pub async fn observe_lock(
        &self,
        key: Vec<u8>,
        timeout: Duration,
    ) -> Result<Response<LockObservation>, Error> {
        let cloned_state_store = self.state_store.clone();
        let locked_state_store = cloned_state_store.lock().await;

        let observe_result = locked_state_store.observe(key, timeout).await;

        match observe_result  {
            Ok(state_store_response) => Ok(state_store_response.into()),
            Err(state_store_error) => Err(state_store_error.into())
        }
    }

    /// Stops observation of any changes on a lock key from the State Store Service
    ///
    /// Returns `true` if the key is no longer being observed or `false` if the key wasn't being observed
    /// # Errors
    /// [`Error`] of kind [`LockNameLengthZero`](ErrorKind::LockNameLengthZero) if
    /// - the `key` is empty
    ///
    /// [`Error`] of kind [`InvalidArgument`](ErrorKind::InvalidArgument) if
    /// - the `timeout` is < 1 ms or > `u32::max`
    ///
    /// [`Error`] of kind [`ServiceError`](ErrorKind::ServiceError) if
    /// - the State Store returns an Error response
    /// - the State Store returns a response that isn't valid for an `Unobserve` request
    ///
    /// [`Error`] of kind [`AIOProtocolError`](ErrorKind::AIOProtocolError) if
    /// - there are any underlying errors from [`CommandInvoker::invoke`]
    pub async fn unobserve_lock(
        &self,
        key: Vec<u8>,
        timeout: Duration,
    ) -> Result<Response<bool>, Error> {
        let cloned_state_store = self.state_store.clone();
        let locked_state_store = cloned_state_store.lock().await;

        let unobserve_result = locked_state_store.unobserve(key, timeout).await;

        match unobserve_result  {
            Ok(state_store_response) => Ok(Response::from_response(state_store_response)),
            Err(state_store_error) => Err(state_store_error.into())
        }
    }

    /// Gets the holder of a lock key in the State Store Service
    ///
    /// Note: timeout refers to the duration until the State Store Client stops
    /// waiting for a `Get` response from the Service. This value is not linked
    /// to the key in the State Store.
    ///
    /// Returns `Some(<value of the key>)` if the key is found or `None` if the key was not found
    /// # Errors
    /// [`Error`] of kind [`LockNameLengthZero`](ErrorKind::LockNameLengthZero) if the `key` is empty
    ///
    /// [`Error`] of kind [`InvalidArgument`](ErrorKind::InvalidArgument) if the `timeout` is < 1 ms or > `u32::max`
    ///
    /// [`Error`] of kind [`ServiceError`](ErrorKind::ServiceError) if the State Store returns an Error response
    ///
    /// [`Error`] of kind [`UnexpectedPayload`](ErrorKind::UnexpectedPayload) if the State Store returns a response that isn't valid for a `Get` request
    ///
    /// [`Error`] of kind [`AIOProtocolError`](ErrorKind::AIOProtocolError) if there are any underlying errors from [`CommandInvoker::invoke`]
    pub async fn get_lock_holder(
        &self,
        key: Vec<u8>,
        timeout: Duration,
    ) -> Result<Response<Option<Vec<u8>>>, Error> {
        let cloned_state_store = self.state_store.clone();
        let locked_state_store = cloned_state_store.lock().await;

        let get_result = locked_state_store.get(key.clone(), timeout).await;

        match get_result  {
            Ok(state_store_response) => Ok(Response::from_response(state_store_response)),
            Err(state_store_error) => Err(state_store_error.into())
        }
    }

    /// Enables the auto-renewal of the lock duration.
    pub fn enable_auto_renewal(&self, _key: &[u8])
    /* -> Result<leased_lock::Response<bool>, Error> */
    {
        unimplemented!();
    }

    /// Disables the auto-renewal of the lock duration.
    pub fn disable_auto_renewal(&self, _key: &[u8])
    /* -> Result<leased_lock::Response<bool>, Error> */
    {
        unimplemented!();
    }

    /// Shutdown the [`leased_lock::Client`]. Shuts down the underlying `state_store` client.
    ///
    /// Note: If this method is called, the [`leased_lock::Client`] should not be used again.
    /// If the method returns an error, it may be called again to attempt the unsubscribe again.
    ///
    /// Returns Ok(()) on success, otherwise returns [`Error`].
    /// # Errors
    /// [`Error`] of kind [`AIOProtocolError`](ErrorKind::AIOProtocolError) if the unsubscribe fails or if the unsuback reason code doesn't indicate success.
    pub async fn shutdown(&self) -> Result<(), Error> {
        let cloned_state_store = self.state_store.clone();
        let locked_state_store = cloned_state_store.lock().await;

        let shutdown_result = locked_state_store.shutdown().await;

        match shutdown_result  {
            Ok(()) => Ok(()),
            Err(state_store_error) => Err(state_store_error.into())
        }
    }
}
