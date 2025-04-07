// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models;

namespace Azure.Iot.Operations.Services.AssetAndDeviceRegistry;

public interface IAdrServiceClient : IAsyncDisposable
{
    Task<NotificationResponse> ObserveAssetEndpointProfileUpdatesAsync(string aepName, CancellationToken cancellationToken);

    Task<NotificationResponse> UnobserveAssetEndpointProfileUpdatesAsync(string aepName, CancellationToken cancellationToken);

    Task<AssetEndpointProfileResponse> GetAssetEndpointProfileAsync(string aepName, TimeSpan? commandTimeout = null,
        CancellationToken cancellationToken = default);

    Task<AssetEndpointProfileResponse> UpdateAssetEndpointProfileStatusAsync(string aepName,
        UpdateAssetEndpointProfileStatusRequest request,
        TimeSpan? commandTimeout = null,
        CancellationToken cancellationToken = default);

    Task<NotificationResponse> ObserveAssetUpdatesAsync(string aepName, string assetName, CancellationToken cancellationToken);

    Task<NotificationResponse> UnobserveAssetUpdatesAsync(string aepName, string assetName, CancellationToken cancellationToken);

    Task<AssetResponse> GetAssetAsync(string aepName,
        GetAssetRequest request,
        TimeSpan? commandTimeout = null,
        CancellationToken cancellationToken = default);

    Task<AssetResponse> UpdateAssetStatusAsync(string aepName,
        UpdateAssetStatusRequest request,
        TimeSpan? commandTimeout = null,
        CancellationToken cancellationToken = default);

    Task<CreateDetectedAssetResponse> CreateDetectedAssetAsync(string aepName,
        CreateDetectedAssetRequest request,
        TimeSpan? commandTimeout = null,
        CancellationToken cancellationToken = default);

    Task<CreateDiscoveredAssetEndpointProfileResponse> CreateDiscoveredAssetEndpointProfileAsync(string aepName,
        CreateDiscoveredAssetEndpointProfileRequest request,
        TimeSpan? commandTimeout = null,
        CancellationToken cancellationToken = default);

    event Func<string, AssetEndpointProfile?, Task>? OnReceiveAssetEndpointProfileUpdateTelemetry;
    event Func<string, Asset?, Task>? OnReceiveAssetUpdateEventTelemetry;
}
