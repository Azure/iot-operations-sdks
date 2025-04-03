/// Create a new Azure Device Registry Client.
pub fn new(
    application_context: ApplicationContext,
    client: &C,
    notification_auto_ack: bool,
) -> Self

/// Retrieves an asset endpoint profile from a Azure Device Registry service.
pub async fn get_asset_endpoint_profile(
    &self,
    aep_name: String,
    timeout: Duration,
) -> Result<AssetEndpointProfile, Error>

/// Updates an asset endpoint profile's status in the Azure Device Registry service.
pub async fn update_asset_endprofile_status(
    &self,
    source: AssetEndpointProfileStatus,
    timeout: Duration,
) -> Result<AssetEndpointProfile, Error>

/// Notifies the Azure Device Registry service that client is listening for asset updates.
pub async fn observe_asset_endpoint_profile_update(
    &self,
    aep_name: String,
    notification_type: bool,
    timeout: Duration,
) -> Result<AssetEndpointProfileObservation, Error>

/// Retrieves an asset from a Azure Device Registry service.
pub async fn get_asset(
    &self, 
    asset_name: String, 
    timeout: Duration
) -> Result<Asset, Error>

/// Updates an asset in the Azure Device Registry service.
pub async fn update_asset_status(
    &self,
    name: String,
    status: AssetStatus,
    timeout: Duration,
) -> Result<Asset, Error>

/// Creates an asset inside the Azure Device Registry service.
pub async fn create_detected_asset(
    &self,
    asset: DetectedAsset,
    timeout: Duration,
) -> Result<DetectedAssetResponseStatusSchema, Error>

/// Notifies the Azure Device Registry service that client is listening for asset updates.
pub async fn observe_asset_update(
    &self,
    asset_name: String,
    notification_type: bool,
    timeout: Duration,
) -> Result<AssetObservation, Error>

/// Creates an asset endpoint profile inside the Azure Device Registry service.
pub async fn create_discovered_asset_endpoint_profile(
    &self,
    daep: DiscoveredAssetEndpointProfile,
    timeout: Duration,
) -> Result<DiscoveredAssetEndpointProfileResponseStatusSchema, Error>

/// Shutdown the Client. Shuts down the underlying command invokers for get and put operations.
pub async fn shutdown(&self) -> Result<(), Error>