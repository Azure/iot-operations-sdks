# State Store API Proposal
## Client
```rust
pub fn new(mqtt_provider: &mut impl MqttProvider<PS, PR>) -> Result<Self, StateStoreError>;
pub async fn set(
      &self,
      key: Vec<u8>,
      value: Vec<u8>,
      timeout: Duration,
      options: SetOptions,
  ) -> Result<state_store::Response<Option<()>>, StateStoreError>;
pub async fn get(
      &self,
      key: Vec<u8>,
      timeout: Duration,
  ) -> Result<state_store::Response<Option<Vec<u8>>>, StateStoreError>;
pub async fn del(
      &self,
      key: Vec<u8>,
      fencing_token: Option<HybridLogicalClock>,
      timeout: Duration,
  ) -> Result<state_store::Response<Option<()>>, StateStoreError>;
pub async fn vdel(
      &self,
      key: Vec<u8>,
      value: Vec<u8>,
      fencing_token: Option<HybridLogicalClock>,
      timeout: Duration,
  ) -> Result<state_store::Response<Option<()>>, StateStoreError>;
pub async fn observe(
      &mut self,
      key: Vec<u8>,
      timeout: Duration,
  ) -> Result<state_store::Response<()>, StateStoreError>;
pub async fn unobserve(
      &mut self,
      key: Vec<u8>,
      timeout: Duration,
  ) -> Result<state_store::Response<Option<()>>, StateStoreError>;


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
pub struct Response<T>
where
  T: Debug,
{
  pub response: T,
  pub version: Option<HybridLogicalClock>
}

pub struct state_store::KeyNotification {
  pub key: Vec<u8>,
  pub operation: state_store::Operation,
}

pub enum Operation {
    Set(Vec<u8>),
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
    ServerError(String),
    #[error("{0}")]
    ClientError(String),
}
```
