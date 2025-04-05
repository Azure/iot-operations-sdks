# Azure Device Registry Client API Signatures

## Constructor

```rust
/// Create a new Azure Device Registry Client.
///
/// # Arguments
/// * `application_context` - The application context for the client.
/// * `client` - The managed MQTT client instance.
/// * `notification_auto_ack` - Whether to automatically acknowledge notifications.
///
/// # Returns
/// A new instance of the Client.
///
/// # Panics
/// Panics if the options for the underlying command invokers and telemetry receivers cannot be built.
/// Not possible since the options are statically generated.
pub fn new(
    application_context: ApplicationContext,
    client: &C,
    notification_auto_ack: bool,
) -> Self
```

## Asset Endpoint Profile Operations

```rust
/// Retrieves an asset endpoint profile from an Azure Device Registry service.
///
/// # Arguments
/// * `aep_name` - The name of the asset endpoint profile.
/// * `timeout` - The duration until the Client stops waiting for a response to the request, 
///               it is rounded up to the nearest second.
///
/// # Returns
/// A `Result` containing the `AssetEndpointProfile` if the endpoint was found.
///
/// # Errors
/// * `ErrorKind::InvalidArgument` - If the timeout is zero or > u32::max, or there is an error building the request.
/// * `ErrorKind::ServiceError` - If there is an error returned by the ADR Service.
/// * `ErrorKind::AIOProtocolError` - If there are any underlying errors from the AIO RPC protocol.
pub async fn get_asset_endpoint_profile(
    &self,
    aep_name: String,
    timeout: Duration,
) -> Result<AssetEndpointProfile, Error>

/// Updates an asset endpoint profile's status in the Azure Device Registry service.
///
/// # Arguments
/// * `source` - The asset endpoint profile status containing all information for the update.
/// * `timeout` - The duration until the Client stops waiting for a response to the request,
///               it is rounded up to the nearest second.
///
/// # Returns
/// A `Result` containing the updated `AssetEndpointProfile`.
///
/// # Errors
/// * `ErrorKind::InvalidArgument` - If the timeout is zero or > u32::max, or there is an error building the request.
/// * `ErrorKind::SerializationError` - If there is an error serializing the request.
/// * `ErrorKind::ServiceError` - If there is an error returned by the ADR Service.
/// * `ErrorKind::AIOProtocolError` - If there are any underlying errors from the AIO RPC protocol.
pub async fn update_asset_endprofile_status(
    &self,
    source: AssetEndpointProfileStatus,
    timeout: Duration,
) -> Result<AssetEndpointProfile, Error>

/// Notifies the Azure Device Registry service that client is listening for asset endpoint profile updates.
///
/// # Arguments
/// * `aep_name` - The name of the asset endpoint profile.
/// * `timeout` - The duration until the Client stops waiting for a response to the request,
///               it is rounded up to the nearest second.
///
/// # Returns
/// A `Result` containing the `AssetEndpointProfileObservation` if observation was successful.
///
/// # Errors
/// * `ErrorKind::InvalidArgument` - If the timeout is zero or > u32::max, or there is an error building the request.
/// * `ErrorKind::SerializationError` - If there is an error serializing the request.
/// * `ErrorKind::ServiceError` - If there is an error returned by the ADR Service.
/// * `ErrorKind::AIOProtocolError` - If there are any underlying errors from the AIO RPC protocol.
/// * `ErrorKind::ObservationError` - If notification response failed.
pub async fn observe_asset_endpoint_profile_update(
    &self,
    aep_name: String,
    timeout: Duration,
) -> Result<AssetEndpointProfileObservation, Error>

/// Notifies the Azure Device Registry service that client is no longer listening for asset endpoint profile updates.
///
/// # Arguments
/// * `aep_name` - The name of the asset endpoint profile.
/// * `timeout` - The duration until the Client stops waiting for a response to the request,
///               it is rounded up to the nearest second.
///
/// # Returns
/// A `Result` containing a boolean indicating success.
///
/// # Errors
/// * `ErrorKind::InvalidArgument` - If the timeout is zero or > u32::max, or there is an error building the request.
/// * `ErrorKind::SerializationError` - If there is an error serializing the request.
/// * `ErrorKind::ServiceError` - If there is an error returned by the ADR Service.
/// * `ErrorKind::AIOProtocolError` - If there are any underlying errors from the AIO RPC protocol.
/// * `ErrorKind::ObservationError` - If notification response failed.
pub async fn unobserve_asset_endpoint_profile_update(
    &self,
    aep_name: String,
    timeout: Duration,
) -> Result<bool, Error>

/// Creates an asset endpoint profile inside the Azure Device Registry service.
///
/// # Arguments
/// * `daep` - The discovered asset endpoint profile containing all relevant details needed for creation.
/// * `timeout` - The duration until the Client stops waiting for a response to the request,
///               it is rounded up to the nearest second.
///
/// # Returns
/// A `Result` containing the `DiscoveredAssetEndpointProfileResponseStatusSchema` indicating creation status.
///
/// # Errors
/// * `ErrorKind::InvalidArgument` - If the timeout is zero or > u32::max, or there is an error building the request.
/// * `ErrorKind::SerializationError` - If there is an error serializing the request.
/// * `ErrorKind::ServiceError` - If there is an error returned by the ADR Service.
/// * `ErrorKind::AIOProtocolError` - If there are any underlying errors from the AIO RPC protocol.
pub async fn create_discovered_asset_endpoint_profile(
    &self,
    daep: DiscoveredAssetEndpointProfile,
    timeout: Duration,
) -> Result<DiscoveredAssetEndpointProfileResponseStatusSchema, Error>
```

## Asset Operations

```rust
/// Retrieves an asset from an Azure Device Registry service.
///
/// # Arguments
/// * `asset_name` - The name of the asset.
/// * `timeout` - The duration until the Client stops waiting for a response to the request,
///               it is rounded up to the nearest second.
///
/// # Returns
/// A `Result` containing the `Asset` if the asset was found.
///
/// # Errors
/// * `ErrorKind::InvalidArgument` - If the timeout is zero or > u32::max, or there is an error building the request.
/// * `ErrorKind::SerializationError` - If there is an error serializing the request.
/// * `ErrorKind::ServiceError` - If there is an error returned by the ADR Service.
/// * `ErrorKind::AIOProtocolError` - If there are any underlying errors from the AIO RPC protocol.
pub async fn get_asset(
    &self, 
    asset_name: String, 
    timeout: Duration
) -> Result<Asset, Error>

/// Updates an asset in the Azure Device Registry service.
///
/// # Arguments
/// * `name` - The name of the asset.
/// * `status` - The status of an asset for the update.
/// * `timeout` - The duration until the Client stops waiting for a response to the request,
///               it is rounded up to the nearest second.
///
/// # Returns
/// A `Result` containing the updated `Asset`.
///
/// # Errors
/// * `ErrorKind::InvalidArgument` - If the timeout is zero or > u32::max, or there is an error building the request.
/// * `ErrorKind::SerializationError` - If there is an error serializing the request.
/// * `ErrorKind::ServiceError` - If there is an error returned by the ADR Service.
/// * `ErrorKind::AIOProtocolError` - If there are any underlying errors from the AIO RPC protocol.
pub async fn update_asset_status(
    &self,
    name: String,
    status: AssetStatus,
    timeout: Duration,
) -> Result<Asset, Error>

/// Creates an asset inside the Azure Device Registry service.
///
/// # Arguments
/// * `asset` - The detected asset containing all relevant details needed for asset creation.
/// * `timeout` - The duration until the Client stops waiting for a response to the request,
///               it is rounded up to the nearest second.
///
/// # Returns
/// A `Result` containing the `DetectedAssetResponseStatusSchema` indicating creation status.
///
/// # Errors
/// * `ErrorKind::InvalidArgument` - If the timeout is zero or > u32::max, or there is an error building the request.
/// * `ErrorKind::SerializationError` - If there is an error serializing the request.
/// * `ErrorKind::ServiceError` - If there is an error returned by the ADR Service.
/// * `ErrorKind::AIOProtocolError` - If there are any underlying errors from the AIO RPC protocol.
pub async fn create_detected_asset(
    &self,
    asset: DetectedAsset,
    timeout: Duration,
) -> Result<DetectedAssetResponseStatusSchema, Error>

/// Notifies the Azure Device Registry service that client is listening for asset updates.
///
/// # Arguments
/// * `asset_name` - The name of the asset.
/// * `timeout` - The duration until the Client stops waiting for a response to the request,
///               it is rounded up to the nearest second.
///
/// # Returns
/// A `Result` containing the `AssetObservation` if observation was successful.
///
/// # Errors
/// * `ErrorKind::InvalidArgument` - If the timeout is zero or > u32::max, or there is an error building the request.
/// * `ErrorKind::SerializationError` - If there is an error serializing the request.
/// * `ErrorKind::ServiceError` - If there is an error returned by the ADR Service.
/// * `ErrorKind::AIOProtocolError` - If there are any underlying errors from the AIO RPC protocol.
pub async fn observe_asset_update(
    &self,
    asset_name: String,
    timeout: Duration,
) -> Result<AssetObservation, Error>

/// Notifies the Azure Device Registry service that client is no longer listening for asset updates.
///
/// # Arguments
/// * `asset_name` - The name of the asset.
/// * `timeout` - The duration until the Client stops waiting for a response to the request,
///               it is rounded up to the nearest second.
///
/// # Returns
/// A `Result` containing a boolean indicating success.
///
/// # Errors
/// * `ErrorKind::InvalidArgument` - If the timeout is zero or > u32::max, or there is an error building the request.
/// * `ErrorKind::SerializationError` - If there is an error serializing the request.
/// * `ErrorKind::ServiceError` - If there is an error returned by the ADR Service.
/// * `ErrorKind::AIOProtocolError` - If there are any underlying errors from the AIO RPC protocol.
/// * `ErrorKind::ObservationError` - If notification response failed.
pub async fn unobserve_asset_update(
    &self,
    asset_name: String,
    timeout: Duration,
) -> Result<bool, Error>
```

## Client Management

```rust
/// Shutdown the Client. Shuts down the underlying command invokers for get and put operations.
///
/// Note: If this method is called, the Client should not be used again.
/// If the method returns an error, it may be called again to re-attempt unsubscribing.
///
/// # Returns
/// A `Result` indicating success or failure of the shutdown operation.
///
/// # Errors
/// * `ErrorKind::AIOProtocolError` - If the unsubscribe fails or if the unsuback reason code doesn't indicate success.
pub async fn shutdown(&self) -> Result<(), Error>
```