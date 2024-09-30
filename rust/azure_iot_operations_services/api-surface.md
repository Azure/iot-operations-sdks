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

/// Create a new State Store Client
/// # Errors
/// [`StateStoreError`] of kind [`AIOProtocolError`](StateStoreErrorKind::AIOProtocolError) is possible if
///     there are any errors creating the underlying command invoker or telemetry receiver, but it should not happen
///
/// # Panics
/// Possible panics when building options for the underlying command invoker or telemetry receiver,
/// but they should be unreachable because we control the static parameters that go into these calls.
pub fn new(mqtt_provider: &mut impl MqttProvider<PS, PR>) -> Result<Self, StateStoreError>;

/// Sets a key value pair in the State Store Service
/// 
/// Note: timeout refers to the duration until the State Store Client stops
/// waiting for a `Set` response from the Service. This value is not linked
/// to the key in the State Store.
///
/// Returns `true` if the `Set` completed successfully, or `false` if the `Set` did not occur because of values specified in `SetOptions`
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
pub async fn set(
      &self,
      key: Vec<u8>,
      value: Vec<u8>,
      timeout: Duration,
      options: SetOptions,
  ) -> Result<state_store::Response<bool>, StateStoreError>;

/// Gets the value of a key in the State Store Service
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
/// Returns the number of keys deleted. Will be `0` if the key was not found, otherwise `1`
/// # Errors
/// [`StateStoreError`] of kind [`KeyLengthZero`](StateStoreErrorKind::KeyLengthZero) if the `key` is empty
///
/// [`StateStoreError`] of kind [`InvalidArgument`](StateStoreErrorKind::InvalidArgument) if the `timeout` is < 1 ms or > `u32::max`
///
/// [`StateStoreError`] of kind [`ServiceError`](StateStoreErrorKind::ServiceError) if the State Store returns an Error response
///
/// [`StateStoreError`] of kind [`UnexpectedPayload`](StateStoreErrorKind::UnexpectedPayload) if the State Store returns a response that isn't valid for a `Delete` request
///
/// [`StateStoreError`] of kind [`AIOProtocolError`](StateStoreErrorKind::AIOProtocolError) if there are any underlying errors from [`CommandInvoker::invoke`]
pub async fn del(
      &self,
      key: Vec<u8>,
      fencing_token: Option<HybridLogicalClock>,
      timeout: Duration,
  ) -> Result<state_store::Response<usize>, StateStoreError>;

/// Deletes a key from the State Store Service if and only if the value matches the one provided
/// 
/// Note: timeout refers to the duration until the State Store Client stops
/// waiting for a `V Delete` response from the Service. This value is not linked
/// to the key in the State Store.
///
/// Returns the number of keys deleted. Will be `0` if the key was not found or the value did not match, otherwise `1`
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
pub async fn vdel(
      &self,
      key: Vec<u8>,
      value: Vec<u8>,
      fencing_token: Option<HybridLogicalClock>,
      timeout: Duration,
  ) -> Result<state_store::Response<usize>, StateStoreError>;

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
/// - there are any underlying errors from [`TelemetryReceiver::stop`]
pub async fn unobserve(
      &mut self,
      key: Vec<u8>,
      timeout: Duration,
  ) -> Result<state_store::Response<bool>, StateStoreError>;

/// Recv a key notification
/// # Errors
/// TODO: Add errors if needed after `telemetry_receiver` implementation
pub async fn recv_notification(
      &mut self,
  ) -> Result<state_store::KeyNotification, StateStoreError>

// Request Relevant type(s)
/// Options for a `Set` Request
#[derive(Clone, Debug, Default)]
pub struct SetOptions {
    /// Condition for the `Set` operation. Default is [`SetCondition::Unconditional`]
    pub set_condition: SetCondition,
    /// How long the key should persist before it expires, in millisecond precision.
    pub expires: Option<Duration>,
    /// Optional fencing token for the `Set` operation
    pub fencing_token: Option<HybridLogicalClock>,
}

/// Condition for a `Set` Request
#[derive(Clone, Debug, Default)]
pub enum SetCondition {
    /// The `Set` operation will only execute if the State Store does not have this key already.
    OnlyIfDoesNotExist,
    /// The `Set` operation will only execute if the State Store does not have this key or it has this key and
    /// the value in the State Store is equal to the value provided for this `Set` operation.
    OnlyIfEqualOrDoesNotExist,
    /// The `Set` operation will execute regardless of if the key exists already and regardless of the value
    /// of this key in the State Store.
    #[default]
    Unconditional,
}

/// State Store Operation Response struct.
#[derive(Debug)]
pub struct state_store::Response<T>
where
    T: Debug,
{
    /// The version of the key as a [`HybridLogicalClock`].
    pub version: Option<HybridLogicalClock>,
    /// The response for the request. Will vary per operation.
    pub response: T,
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
/// Represents an error that occurred in the Azure IoT Operations State Store implementation.
#[derive(Debug, Error)]
#[error(transparent)]
pub struct StateStoreError(#[from] StateStoreErrorKind);

/// Represents the kinds of errors that occur in the Azure IoT Operations State Store implementation.
#[derive(Error, Debug)]
pub enum StateStoreErrorKind {
    /// An error occurred in the AIO Protocol. See [`AIOProtocolError`] for more information.
    #[error(transparent)]
    AIOProtocolError(#[from] AIOProtocolError),
    /// An error occurred from the State Store Service. See [`ServiceError`] for more information.
    #[error(transparent)]
    ServiceError(#[from] ServiceError),
    /// The key length must not be zero.
    #[error("key length must not be zero")]
    KeyLengthZero,
    /// An error occurred during serialization of a request.
    #[error("{0}")]
    SerializationError(String),
    /// An argument provided for a request was invalid.
    #[error("{0}")]
    InvalidArgument(String),
    /// The payload of the response does not match the expected type for the request.
    #[error("Unexpected response payload for the request type: {0}")]
    UnexpectedPayload(String),
}

/// Represents the errors that occur in the Azure IoT Operations State Store Service.
#[derive(Error, Debug)]
pub enum ServiceError {
    /// The requested timestamp is too far in the future; ensure that the client and broker system clocks are synchronized.
    #[error("the requested timestamp is too far in the future; ensure that the client and broker system clocks are synchronized")]
    TimestampSkew,
    /// A fencing token is required for this request. This happens if a key has been marked with a fencing token, but the client doesn't specify it
    #[error("a fencing token is required for this request")]
    MissingFencingToken,
    /// The requested fencing token timestamp is too far in the future; ensure that the client and broker system clocks are synchronized.
    #[error("the requested fencing token timestamp is too far in the future; ensure that the client and broker system clocks are synchronized")]
    FencingTokenSkew,
    /// The requested fencing token is a lower version that the fencing token protecting the resource.
    #[error("the requested fencing token is a lower version that the fencing token protecting the resource")]
    FencingTokenLowerVersion,
    /// The state store has a quota of how many keys it can store, which is based on the memory profile of the MQ broker that's specified.
    #[error("the quota has been exceeded")]
    QuotaExceeded,
    /// The payload sent does not conform to state store's definition.
    #[error("syntax error")]
    SyntaxError,
    /// The client is not authorized to perform the operation.
    #[error("not authorized")]
    NotAuthorized,
    /// The command sent is not recognized by the state store.
    #[error("unknown command")]
    UnknownCommand,
    /// The number of arguments sent in the command is incorrect.
    #[error("wrong number of arguments")]
    WrongNumberOfArguments,
    /// The timestamp is missing on the request.
    #[error("missing timestamp")]
    TimestampMissing,
    /// The timestamp or fencing token is malformed.
    #[error("malformed timestamp")]
    TimestampMalformed,
    /// The key length is zero.
    #[error("the key length is zero")]
    KeyLengthZero,
    /// An unknown error was received from the State Store Service.
    #[error("{0}")]
    Unknown(String),
}
```
