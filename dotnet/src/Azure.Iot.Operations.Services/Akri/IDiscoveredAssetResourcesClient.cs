// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Iot.Operations.Services.Akri;

using AssetEndpointProfileResponseInfo = DiscoveredAssetResources.CreateDiscoveredAssetEndpointProfileResponseSchema;
using AssetEndpointProfileRequestAuthMethodSchema = DiscoveredAssetResources.SupportedAuthenticationMethodsSchemaElementSchema;
using AssetResponseInfo = DiscoveredAssetResources.CreateDiscoveredAssetResponseSchema;
using AssetRequestDatasetsElementSchema = DiscoveredAssetResources.DatasetsSchemaElementSchema;
using AssetRequestDefaultTopic = DiscoveredAssetResources.DefaultTopicSchema;
using AssetRequestEventsSchema = DiscoveredAssetResources.EventsSchemaElementSchema;
using Azure.Iot.Operations.Services.Akri.DiscoveredAssetResources;

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

