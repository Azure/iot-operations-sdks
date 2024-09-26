# State Store API Proposal
## Client
```rust
pub struct state_store::Client<PS, PR>
where
    PS: MqttPubSub + Clone + Send + Sync + 'static,
    PR: MqttPubReceiver + MqttAck + Send + Sync + 'static,
{
    command_invoker: CommandInvoker<state_store::resp3::Request, resp3::Response, PS>,
    notification_receiver: TelemetryReceiver<resp3::Operation, PS, PR>,
    observed_keys: Arc<Mutex<HashSet<Vec<u8>>>>, // This may not be needed depending on key notification implementation
}

pub fn new(mqtt_provider: &mut impl MqttProvider<PS, PR>) -> Result<Self, StateStoreError>;

/// Sets a key value pair in the State Store Service
/// 
/// Note: timeout refers to the duration until the State Store Client stops
/// waiting for a `Set` response from the Service. This value is not linked
/// to the key in the State Store.
///
/// Returns `Some(())` if the Set completed successfully, or `None` if the Set did not occur because of values specified in `SetOptions`
/// # Errors
/// [`StateStoreError`] of kind [`KeyLengthZero`](StateStoreErrorKind::KeyLengthZero) if
/// - the `key` is empty
///
/// [`StateStoreError`] of kind [`InvalidArgument`](StateStoreErrorKind::InvalidArgument) if
/// - the `timeout` is < 1 ms or > `u32::max`
///
/// [`StateStoreError`] of kind [`ServiceError`](StateStoreErrorKind::ServiceError) if
/// - the State Store returns an Error response
/// - the State Store returns a response that isn't valid for a `Set` request
///
/// [`StateStoreError`] of kind [`AIOProtocolError`](StateStoreErrorKind::AIOProtocolError) if there are any underlying errors from [`CommandInvoker::invoke`]
pub async fn set(
      &self,
      key: Vec<u8>,
      value: Vec<u8>,
      timeout: Duration,
      options: SetOptions,
  ) -> Result<state_store::Response<Option<()>>, StateStoreError>;

/// Gets the value of a key in the State Store Service
/// 
/// Note: timeout refers to the duration until the State Store Client stops
/// waiting for a `Get` response from the Service. This value is not linked
/// to the key in the State Store.
///
/// Returns `Some(<value of the key>)` if the key is found or `None` if the key was not found
/// # Errors
/// [`StateStoreError`] of kind [`KeyLengthZero`](StateStoreErrorKind::KeyLengthZero) if
/// - the `key` is empty
///
/// [`StateStoreError`] of kind [`InvalidArgument`](StateStoreErrorKind::InvalidArgument) if
/// - the `timeout` is < 1 ms or > `u32::max`
///
/// [`StateStoreError`] of kind [`ServiceError`](StateStoreErrorKind::ServiceError) if
/// - the State Store returns an Error response
/// - the State Store returns a response that isn't valid for a `Get` request
///
/// [`StateStoreError`] of kind [`AIOProtocolError`](StateStoreErrorKind::AIOProtocolError) if there are any underlying errors from [`CommandInvoker::invoke`]
pub async fn get(
      &self,
      key: Vec<u8>,
      timeout: Duration,
  ) -> Result<state_store::Response<Option<Vec<u8>>>, StateStoreError>;

/// Deletes a key from the State Store Service
/// 
/// Note: timeout refers to the duration until the State Store Client stops
/// waiting for a `Delete` response from the Service. This value is not linked
/// to the key in the State Store.
///
/// Returns `Some(())` if the key is found and deleted or `None` if the key was not found
/// # Errors
/// [`StateStoreError`] of kind [`KeyLengthZero`](StateStoreErrorKind::KeyLengthZero) if
/// - the `key` is empty
///
/// [`StateStoreError`] of kind [`InvalidArgument`](StateStoreErrorKind::InvalidArgument) if
/// - the `timeout` is < 1 ms or > `u32::max`
///
/// [`StateStoreError`] of kind [`ServiceError`](StateStoreErrorKind::ServiceError) if
/// - the State Store returns an Error response
/// - the State Store returns a response that isn't valid for a `Delete` request
///
/// [`StateStoreError`] of kind [`AIOProtocolError`](StateStoreErrorKind::AIOProtocolError) if there are any underlying errors from [`CommandInvoker::invoke`]
pub async fn del(
      &self,
      key: Vec<u8>,
      fencing_token: Option<HybridLogicalClock>,
      timeout: Duration,
  ) -> Result<state_store::Response<Option<()>>, StateStoreError>;

/// Deletes a key from the State Store Service if and only if the value matches the one provided
/// 
/// Note: timeout refers to the duration until the State Store Client stops
/// waiting for a `V Delete` response from the Service. This value is not linked
/// to the key in the State Store.
///
/// Returns `Some(())` if the key is found and deleted or `None` if the key was not found
/// # Errors
/// [`StateStoreError`] of kind [`KeyLengthZero`](StateStoreErrorKind::KeyLengthZero) if
/// - the `key` is empty
///
/// [`StateStoreError`] of kind [`InvalidArgument`](StateStoreErrorKind::InvalidArgument) if
/// - the `timeout` is < 1 ms or > `u32::max`
///
/// [`StateStoreError`] of kind [`ServiceError`](StateStoreErrorKind::ServiceError) if
/// - the State Store returns an Error response
/// - the State Store returns a response that isn't valid for a `V Delete` request
///
/// [`StateStoreError`] of kind [`AIOProtocolError`](StateStoreErrorKind::AIOProtocolError) if there are any underlying errors from [`CommandInvoker::invoke`]
pub async fn vdel(
      &self,
      key: Vec<u8>,
      value: Vec<u8>,
      fencing_token: Option<HybridLogicalClock>,
      timeout: Duration,
  ) -> Result<state_store::Response<Option<()>>, StateStoreError>;

/// Starts observation of any changes on a key from the State Store Service
/// 
/// Note: timeout refers to the duration until the State Store Client stops
/// waiting for an `Observe` response from the Service. This value is not linked
/// to the length of the observation.
///
/// Returns `OK(())` if the key is now being observed
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
/// - there are any underlying errors from [`TelemetryReceiver::start`]
pub async fn observe(
      &mut self,
      key: Vec<u8>,
      timeout: Duration,
  ) -> Result<state_store::Response<()>, StateStoreError>;

/// Stops observation of any changes on a key from the State Store Service
/// 
/// Note: timeout refers to the duration until the State Store Client stops
/// waiting for an `Unobserve` response from the Service. This value is not linked
/// to the key in the State Store.
///
/// Returns `Some(())` if the key is no longer being observed or `None` if the key wasn't being observed
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
/// - there are any underlying errors from [`TelemetryReceiver::stop`]
pub async fn unobserve(
      &mut self,
      key: Vec<u8>,
      timeout: Duration,
  ) -> Result<state_store::Response<Option<()>>, StateStoreError>;

/// Recv a key notification
/// # Errors
/// TODO: Add errors if needed after `telemetry_receiver` implementation
pub async fn recv_notification(
      &mut self,
  ) -> Result<state_store::KeyNotification, StateStoreError>

// Request Relevant type(s)
#[derive(Clone, Debug, Default)]
pub struct SetOptions {
    pub set_condition: SetCondition, // default is SetCondition::Unconditional
    pub expires_millis: Option<u64>, // default is None
    pub fencing_token: Option<HybridLogicalClock>, // default is None
}

#[derive(Clone, Debug, Default)]
pub enum SetCondition {
    OnlyIfDoesNotExist,
    OnlyIfEqualOrDoesNotExist,
    #[default]
    Unconditional,
}

// Return type
pub struct state_store::Response<T> {
  pub response: T,
  pub version: Option<HybridLogicalClock>
}

pub struct state_store::KeyNotification {
  pub key: Vec<u8>,
  pub operation: state_store::Operation,
}

pub enum Operation {
    /// Operation was a `SET`, and the argument is the new value
    Set(Vec<u8>),
    /// Operation was a `DELETE`
    Del,
}
```

## State Store Error
```rust
#[derive(Debug, Error)]
#[error(transparent)]
pub struct StateStoreError(#[from] StateStoreErrorKind);

#[derive(Error, Debug)]
pub enum StateStoreErrorKind {
    #[error(transparent)]
    AIOProtocolError(#[from] AIOProtocolError),
    #[error("{0}")]
    ServiceError(#[from] ServiceError),
    #[error("key length must not be zero")]
    KeyLengthZero,
    #[error("{0}")]
    SerializationError(String),
    #[error("{0}")]
    InvalidArgument(String),
}

#[derive(Error, Debug)]
pub enum ServiceError {
    // This is an example for now
    #[error("Malformed request")]
    BadFormat,
    // TODO: fill in these errors
}
```
