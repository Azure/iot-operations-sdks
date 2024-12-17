namespace Azure.Iot.Operations.Services.Akri;

using AssetEndpointProfileResponseInfo = dtmi_com_microsoft_deviceregistry_DiscoveredAssetResources__1.Object_CreateDiscoveredAssetEndpointProfile_Response;
using AssetEndpointProfileRequestAuthMethodSchema = dtmi_com_microsoft_deviceregistry_DiscoveredAssetResources__1.Enum_CreateDiscoveredAssetEndpointProfile_Request_SupportedAuthenticationMethods_ElementSchema;
using AssetResponseInfo = dtmi_com_microsoft_deviceregistry_DiscoveredAssetResources__1.Object_CreateDiscoveredAsset_Response;
using AssetRequestDatasetsElementSchema = dtmi_com_microsoft_deviceregistry_DiscoveredAssetResources__1.Object_CreateDiscoveredAsset_Request_Datasets_ElementSchema;
using AssetRequestDefaultTopic = dtmi_com_microsoft_deviceregistry_DiscoveredAssetResources__1.Object_CreateDiscoveredAsset_Request_DefaultTopic;
using AssetRequestEventsSchema = dtmi_com_microsoft_deviceregistry_DiscoveredAssetResources__1.Object_CreateDiscoveredAsset_Request_Events_ElementSchema;
using Azure.Iot.Operations.Services.Akri.dtmi_com_microsoft_deviceregistry_DiscoveredAssetResources__1;

/// <summary>
/// Interface for creating discovered assets and asset endpoint profiles.
/// </summary>
public interface IDiscoveredAssetResourcesClient : IAsyncDisposable
{
    /// <summary>
    /// Creates a discovered asset endpoint profile based on the specified request details.
    /// </summary>
    /// <param name="discoveredAssetEndpointProfileRequest">Request containing the details required to create the asset endpoint profile.</param>
    /// <param name="timeout">An optional timeout for the operation, which specifies the maximum time allowed for the request to complete.</param>
    /// <param name="cancellationToken">A token that can be used to cancel the operation before completion if needed.</param>
    /// <returns>
    /// A task that represents the asynchronous operation. The task result contains an <see cref="AssetEndpointProfileResponseInfo"/> object
    /// if the asset endpoint profile is successfully created, or <c>null</c> if the operation fails or no response is available.
    /// </returns>
    public Task<AssetEndpointProfileResponseInfo?> CreateDiscoveredAssetEndpointProfileAsync(
        CreateDiscoveredAssetEndpointProfileRequestPayload discoveredAssetEndpointProfileRequest,
        TimeSpan? timeout = default!, CancellationToken cancellationToken = default!);

    /// <summary>
    /// Creates a discovered asset based on the specified request details.
    /// </summary>
    /// <param name="discoveredAssetRequest">Request containing information about the asset to be created.</param>
    /// <param name="timeout">An optional timeout for the operation, which defines how long the method should wait before timing out.</param>
    /// <param name="cancellationToken">A token that can be used to cancel the operation before completion if needed.</param>
    /// <returns>
    /// A task that represents the asynchronous operation. The task result contains an <see cref="AssetResponseInfo"/> object
    /// if the asset creation succeeds, or <c>null</c> if the operation fails or no response is available.
    /// </returns>
    public Task<AssetResponseInfo?> CreateDiscoveredAssetAsync(
        CreateDiscoveredAssetRequestPayload discoveredAssetRequest,
        TimeSpan? timeout = default!, CancellationToken cancellationToken = default!);
}

