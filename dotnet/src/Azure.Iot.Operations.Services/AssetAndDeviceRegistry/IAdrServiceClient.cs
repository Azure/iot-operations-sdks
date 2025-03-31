// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Services.AssetAndDeviceRegistry.AdrBaseService;
using Azure.Iot.Operations.Services.AssetAndDeviceRegistry.AepTypeService;

namespace Azure.Iot.Operations.Services.AssetAndDeviceRegistry;

public interface IAdrServiceClient : IAsyncDisposable
{
    Task<NotificationResponse> ObserveAssetEndpointProfileUpdatesAsync(string aepName, CancellationToken cancellationToken);

    Task<NotificationResponse> UnobserveAssetEndpointProfileUpdatesAsync(string aepName, CancellationToken cancellationToken);

    Task<AssetEndpointProfile?> GetAssetEndpointProfileAsync(string aepName, TimeSpan? commandTimeout = null, CancellationToken cancellationToken = default);

    Task<AssetEndpointProfile?> UpdateAssetEndpointProfileStatusAsync(string aepName,
        UpdateAssetEndpointProfileStatusRequestPayload requestPayload,
        TimeSpan? commandTimeout = null,
        CancellationToken cancellationToken = default);

    Task<NotificationResponse> ObserveAssetUpdatesAsync(string aepName, string assetName, CancellationToken cancellationToken);

    Task<NotificationResponse> UnobserveAssetUpdatesAsync(string aepName, string assetName, CancellationToken cancellationToken);

    Task<Asset?> GetAssetAsync(string aepName,
        GetAssetRequestPayload requestPayload,
        TimeSpan? commandTimeout = null,
        CancellationToken cancellationToken = default);

    Task<Asset?> UpdateAssetStatusAsync(string aepName,
        UpdateAssetStatusRequestPayload requestPayload,
        TimeSpan? commandTimeout = null,
        CancellationToken cancellationToken = default);

    Task<CreateDetectedAssetResponseSchema?> CreateDetectedAssetAsync(string aepName,
        CreateDetectedAssetRequestPayload requestPayload,
        TimeSpan? commandTimeout = null,
        CancellationToken cancellationToken = default);

    Task<CreateDiscoveredAssetEndpointProfileResponseSchema?> CreateDiscoveredAssetEndpointProfileAsync(string aepName,
        CreateDiscoveredAssetEndpointProfileRequestPayload requestPayload,
        TimeSpan? commandTimeout = null,
        CancellationToken cancellationToken = default);

    event Func<string, AssetEndpointProfile?, Task>? OnReceiveAssetEndpointProfileUpdateTelemetry;
    event Func<string, Asset?, Task>? OnReceiveAssetUpdateEventTelemetry;
}
