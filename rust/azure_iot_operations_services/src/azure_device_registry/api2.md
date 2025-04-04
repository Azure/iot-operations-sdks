I'll create a complete markdown document that combines both the code and the descriptions for the entire Azure Device Registry Client API in a single file.

# Azure Device Registry Client API Documentation

## Introduction
The Azure Device Registry Client provides a Rust interface for interacting with the Azure Device Registry service, allowing users to manage assets and asset endpoint profiles. This client is designed to work with MQTT-based communication.

## Constructor

```rust
pub fn new(
    application_context: ApplicationContext,
    client: &C,
    notification_auto_ack: bool,
) -> Self
```

Creates a new Azure Device Registry Client.

The constructor takes an application context, MQTT client, and notification auto-acknowledgment flag. It initializes command invokers and telemetry receivers for communicating with the Azure Device Registry service.

**Panics:** If the options for the underlying command invokers and telemetry receivers cannot be built. Not possible since the options are statically generated.

## Asset Endpoint Profile Methods

### Get Asset Endpoint Profile
```rust
pub async fn get_asset_endpoint_profile(
    &self,
    aep_name: String,
    timeout: Duration,
) -> Result<AssetEndpointProfile, Error>
```

Retrieves an asset endpoint profile from a Azure Device Registry service.

This method sends a request to get information about a specific asset endpoint profile identified by its name. It waits for a response within the specified timeout period.

**Arguments:**
- `aep_name`: The name of the asset endpoint profile.
- `timeout`: The duration until the Client stops waiting for a response to the request, it is rounded up to the nearest second.

**Returns:** `AssetEndpointProfile` if the endpoint was found.

**Errors:**
- `InvalidArgument`: If the `timeout` is zero or > `u32::max`, or there is an error building the request.
- `ServiceError`: If there is an error returned by the ADR Service.
- `AIOProtocolError`: If there are any underlying errors from the AIO RPC protocol.

### Update Asset Endpoint Profile Status
```rust
pub async fn update_asset_endprofile_status(
    &self,
    source: AssetEndpointProfileStatus,
    timeout: Duration,
) -> Result<AssetEndpointProfile, Error>
```

Updates an asset endpoint profile's status in the Azure Device Registry service.

This method allows updating the status of an asset endpoint profile, particularly for reporting errors.

**Arguments:**
- `source`: The profile status containing update information.
- `timeout`: The duration until the Client stops waiting for a response to the request, it is rounded up to the nearest second.

**Returns:** The updated `AssetEndpointProfile` once updated.

**Errors:**
- `InvalidArgument`: If the `timeout` is zero or > `u32::max`, or there is an error building the request.
- `SerializationError`: If there is an error serializing the request.
- `ServiceError`: If there is an error returned by the ADR Service.
- `AIOProtocolError`: If there are any underlying errors from the AIO RPC protocol.

### Observe Asset Endpoint Profile Update
```rust
pub async fn observe_asset_endpoint_profile_update(
    &self,
    aep_name: String,
    timeout: Duration,
) -> Result<AssetEndpointProfileObservation, Error>
```

Notifies the Azure Device Registry service that client is listening for asset endpoint profile updates.

This method registers interest in receiving notifications about changes to a specific asset endpoint profile. It sets up a channel for receiving these updates.

**Arguments:**
- `aep_name`: The name of the asset endpoint profile.
- `timeout`: The duration until the Client stops waiting for a response to the request, it is rounded up to the nearest second.

**Returns:** The `AssetEndpointProfileObservation` if observation was done successfully, which contains a channel for receiving updates.

**Errors:**
- `InvalidArgument`: If the `timeout` is zero or > `u32::max`, or there is an error building the request.
- `SerializationError`: If there is an error serializing the request.
- `ServiceError`: If there is an error returned by the ADR Service.
- `AIOProtocolError`: If there are any underlying errors from the AIO RPC protocol.
- `ObservationError`: If notification response failed.
- `DuplicateObserve`: If duplicate AEPs are being observed.

### Unobserve Asset Endpoint Profile Update
```rust
pub async fn unobserve_asset_endpoint_profile_update(
    &self,
    aep_name: String,
    timeout: Duration,
) -> Result<(), Error>
```

Notifies the Azure Device Registry service that client is no longer listening for asset endpoint profile updates.

This method cancels a previous observation request, indicating that the client is no longer interested in receiving updates about the specified asset endpoint profile.

**Arguments:**
- `aep_name`: The name of the asset endpoint profile.
- `timeout`: The duration until the Client stops waiting for a response to the request, it is rounded up to the nearest second.

**Returns:** `()` if successful.

**Errors:**
- `InvalidArgument`: If the `timeout` is zero or > `u32::max`, or there is an error building the request.
- `SerializationError`: If there is an error serializing the request.
- `ServiceError`: If there is an error returned by the ADR Service.
- `AIOProtocolError`: If there are any underlying errors from the AIO RPC protocol.
- `ObservationError`: If notification response failed.

### Create Discovered Asset Endpoint Profile
```rust
pub async fn create_discovered_asset_endpoint_profile(
    &self,
    daep: DiscoveredAssetEndpointProfile,
    timeout: Duration,
) -> Result<DiscoveredAssetEndpointProfileResponseStatusSchema, Error>
```

Creates an asset endpoint profile inside the Azure Device Registry service.

This method registers a newly discovered asset endpoint profile with the service.

**Arguments:**
- `daep`: All relevant details needed for an asset endpoint profile creation.
- `timeout`: The duration until the Client stops waiting for a response to the request, it is rounded up to the nearest second.

**Returns:** A `DiscoveredAssetEndpointProfileResponseStatusSchema` depending on the status of the asset endpoint profile creation.

**Errors:**
- `InvalidArgument`: If the `timeout` is zero or > `u32::max`, or there is an error building the request.
- `SerializationError`: If there is an error serializing the request.
- `ServiceError`: If there is an error returned by the ADR Service.
- `AIOProtocolError`: If there are any underlying errors from the AIO RPC protocol.

## Asset Methods

### Get Asset
```rust
pub async fn get_asset(
    &self,
    aep_name: String,
    asset_name: String,
    timeout: Duration,
) -> Result<Asset, Error>
```

Retrieves an asset from a Azure Device Registry service.

This method fetches information about a specific asset identified by its name and associated with a particular asset endpoint profile.

**Arguments:**
- `aep_name`: The name of the asset endpoint profile.
- `asset_name`: The name of the asset.
- `timeout`: The duration until the Client stops waiting for a response to the request, it is rounded up to the nearest second.

**Returns:** An `Asset` if the asset was found.

**Errors:**
- `InvalidArgument`: If the `timeout` is zero or > `u32::max`, or there is an error building the request.
- `SerializationError`: If there is an error serializing the request.
- `ServiceError`: If there is an error returned by the ADR Service.
- `AIOProtocolError`: If there are any underlying errors from the AIO RPC protocol.

### Update Asset Status
```rust
pub async fn update_asset_status(
    &self,
    name: String,
    status: AssetStatus,
    timeout: Duration,
) -> Result<Asset, Error>
```

Updates an asset in the Azure Device Registry service.

This method allows updating the status of an existing asset in the service.

**Arguments:**
- `name`: The name of the asset.
- `status`: The status of an asset for the update.
- `timeout`: The duration until the Client stops waiting for a response to the request, it is rounded up to the nearest second.

**Returns:** The updated `Asset` once updated.

**Errors:**
- `InvalidArgument`: If the `timeout` is zero or > `u32::max`, or there is an error building the request.
- `SerializationError`: If there is an error serializing the request.
- `ServiceError`: If there is an error returned by the ADR Service.
- `AIOProtocolError`: If there are any underlying errors from the AIO RPC protocol.

### Create Detected Asset
```rust
pub async fn create_detected_asset(
    &self,
    asset: DetectedAsset,
    timeout: Duration,
) -> Result<DetectedAssetResponseStatusSchema, Error>
```

Creates an asset inside the Azure Device Registry service.

This method registers a newly detected asset with the service.

**Arguments:**
- `asset`: All relevant details needed for an asset creation.
- `timeout`: The duration until the Client stops waiting for a response to the request, it is rounded up to the nearest second.

**Returns:** A `DetectedAssetResponseStatusSchema` depending on the status of the asset creation.

**Errors:**
- `InvalidArgument`: If the `timeout` is zero or > `u32::max`, or there is an error building the request.
- `SerializationError`: If there is an error serializing the request.
- `ServiceError`: If there is an error returned by the ADR Service.
- `AIOProtocolError`: If there are any underlying errors from the AIO RPC protocol.

### Observe Asset Update
```rust
pub async fn observe_asset_update(
    &self,
    aep_name: String,
    asset_name: String,
    timeout: Duration,
) -> Result<AssetObservation, Error>
```

Notifies the Azure Device Registry service that client is listening for asset updates.

This method registers interest in receiving notifications about changes to a specific asset. It sets up a channel for receiving these updates.

**Arguments:**
- `aep_name`: The name of the asset endpoint profile.
- `asset_name`: The name of the asset.
- `timeout`: The duration until the Client stops waiting for a response to the request, it is rounded up to the nearest second.

**Returns:** `AssetObservation` if successful, which contains a channel for receiving updates.

**Errors:**
- `InvalidArgument`: If the `timeout` is zero or > `u32::max`, or there is an error building the request.
- `SerializationError`: If there is an error serializing the request.
- `ServiceError`: If there is an error returned by the ADR Service.
- `AIOProtocolError`: If there are any underlying errors from the AIO RPC protocol.
- `ObservationError`: If notification response failed.
- `DuplicateObserve`: If duplicate assets are being observed.

### Unobserve Asset Update
```rust
pub async fn unobserve_asset_update(
    &self,
    aep_name: String,
    asset_name: String,
    timeout: Duration,
) -> Result<(), Error>
```

Notifies the Azure Device Registry service that client is no longer listening for asset updates.

This method cancels a previous observation request, indicating that the client is no longer interested in receiving updates about the specified asset.

**Arguments:**
- `aep_name`: The name of the asset endpoint profile.
- `asset_name`: The name of the asset.
- `timeout`: The duration until the Client stops waiting for a response to the request, it is rounded up to the nearest second.

**Returns:** `()` if successful.

**Errors:**
- `InvalidArgument`: If the `timeout` is zero or > `u32::max`, or there is an error building the request.
- `SerializationError`: If there is an error serializing the request.
- `ServiceError`: If there is an error returned by the ADR Service.
- `AIOProtocolError`: If there are any underlying errors from the AIO RPC protocol.
- `ObservationError`: If notification response failed.

### Shutdown
```rust
pub async fn shutdown(&self) -> Result<(), Error>
```

Shutdown the `Client`. Shuts down the underlying command invokers for get and put operations.

This method properly terminates all connections and subscriptions used by the client.

**Note:** If this method is called, the `Client` should not be used again. If the method returns an error, it may be called again to re-attempt unsubscribing.

**Returns:** `Ok(())` on success.

**Errors:**
- `AIOProtocolError`: If the unsubscribe fails or if the unsuback reason code doesn't indicate success.

## Error Types

The client uses a unified error type `Error` with various kind variants:

- `InvalidArgument`: For invalid input parameters.
- `SerializationError`: For errors during request serialization.
- `ServiceError`: For errors returned by the ADR service.
- `AIOProtocolError`: For underlying protocol communication errors.
- `ObservationError`: For failed notification operations.
- `DuplicateObserve`: When attempting to observe an already observed asset or profile.