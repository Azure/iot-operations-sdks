using Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models;

namespace Azure.Iot.Operations.Services.AssetAndDeviceRegistry;

/// <summary>
/// Defines methods and events for interacting with the Asset and Device Registry (ADR) service.
/// </summary>
public interface IAdrServiceClient : IAsyncDisposable
{
    /// <summary>
    /// Starts observing updates for a specified Asset Endpoint Profile (AEP).
    /// </summary>
    /// <param name="aepName">The name of the Asset Endpoint Profile to observe.</param>
    /// <param name="commandTimeout"></param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A notification response indicating the result of the operation.</returns>
    Task<NotificationResponse> ObserveAssetEndpointProfileUpdatesAsync(string aepName,
        TimeSpan? commandTimeout = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Stops observing updates for a specified Asset Endpoint Profile (AEP).
    /// </summary>
    /// <param name="aepName">The name of the Asset Endpoint Profile to stop observing.</param>
    /// <param name="commandTimeout"></param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A notification response indicating the result of the operation.</returns>
    Task<NotificationResponse> UnobserveAssetEndpointProfileUpdatesAsync(string aepName,
        TimeSpan? commandTimeout = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves the details of a specified Asset Endpoint Profile (AEP).
    /// </summary>
    /// <param name="aepName">The name of the Asset Endpoint Profile to retrieve.</param>
    /// <param name="commandTimeout">Optional timeout for the command execution.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The requested Asset Endpoint Profile.</returns>
    Task<AssetEndpointProfile> GetAssetEndpointProfileAsync(string aepName, TimeSpan? commandTimeout = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates the status of a specified Asset Endpoint Profile (AEP).
    /// </summary>
    /// <param name="aepName">The name of the Asset Endpoint Profile to update.</param>
    /// <param name="request">The request containing the status update details.</param>
    /// <param name="commandTimeout">Optional timeout for the command execution.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The updated Asset Endpoint Profile.</returns>
    Task<AssetEndpointProfile> UpdateAssetEndpointProfileStatusAsync(string aepName,
        UpdateAssetEndpointProfileStatusRequest request,
        TimeSpan? commandTimeout = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Starts observing updates for a specified asset within an Asset Endpoint Profile (AEP).
    /// </summary>
    /// <param name="aepName">The name of the Asset Endpoint Profile.</param>
    /// <param name="assetName">The name of the asset to observe.</param>
    /// <param name="commandTimeout">The optional timeout for the command execution.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A notification response indicating the result of the operation.</returns>
    Task<NotificationResponse> ObserveAssetUpdatesAsync(string aepName, string assetName, TimeSpan? commandTimeout = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Stops observing updates for a specified asset within an Asset Endpoint Profile (AEP).
    /// </summary>
    /// <param name="aepName">The name of the Asset Endpoint Profile.</param>
    /// <param name="assetName">The name of the asset to stop observing.</param>
    /// <param name="commandTimeout">The optional timeout for the command execution.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A notification response indicating the result of the operation.</returns>
    Task<NotificationResponse> UnobserveAssetUpdatesAsync(string aepName, string assetName, TimeSpan? commandTimeout = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves details of a specified asset within an Asset Endpoint Profile (AEP).
    /// </summary>
    /// <param name="aepName">The name of the Asset Endpoint Profile.</param>
    /// <param name="request">The request containing asset retrieval details.</param>
    /// <param name="commandTimeout">Optional timeout for the command execution.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The requested asset details.</returns>
    Task<Asset> GetAssetAsync(string aepName,
        GetAssetRequest request,
        TimeSpan? commandTimeout = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates the status of a specified asset within an Asset Endpoint Profile (AEP).
    /// </summary>
    /// <param name="aepName">The name of the Asset Endpoint Profile.</param>
    /// <param name="request">The request containing the asset status update details.</param>
    /// <param name="commandTimeout">Optional timeout for the command execution.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The updated asset details.</returns>
    Task<Asset> UpdateAssetStatusAsync(string aepName,
        UpdateAssetStatusRequest request,
        TimeSpan? commandTimeout = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a detected asset within an Asset Endpoint Profile (AEP).
    /// </summary>
    /// <param name="aepName">The name of the Asset Endpoint Profile.</param>
    /// <param name="request">The request containing details of the detected asset.</param>
    /// <param name="commandTimeout">Optional timeout for the command execution.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The response containing details of the created detected asset.</returns>
    Task<CreateDetectedAssetResponse> CreateDetectedAssetAsync(string aepName,
        CreateDetectedAssetRequest request,
        TimeSpan? commandTimeout = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a discovered Asset Endpoint Profile (AEP).
    /// </summary>
    /// <param name="aepName">The name of the Asset Endpoint Profile.</param>
    /// <param name="request">The request containing details of the discovered AEP.</param>
    /// <param name="commandTimeout">Optional timeout for the command execution.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The response containing details of the created discovered AEP.</returns>
    Task<CreateDiscoveredAssetEndpointProfileResponse> CreateDiscoveredAssetEndpointProfileAsync(string aepName,
        CreateDiscoveredAssetEndpointProfileRequest request,
        TimeSpan? commandTimeout = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Event triggered when telemetry updates for an Asset Endpoint Profile (AEP) are received.
    /// NOTE: This event starts triggering when <see cref="ObserveAssetEndpointProfileUpdatesAsync"/> called.
    /// </summary>
    event Func<string, AssetEndpointProfile?, Task>? OnReceiveAssetEndpointProfileUpdateTelemetry;

    /// <summary>
    /// Event triggered when telemetry updates for an asset are received.
    /// NOTE: This event starts triggering when <see cref="ObserveAssetUpdatesAsync"/> called.
    /// </summary>
    event Func<string, Asset?, Task>? OnReceiveAssetUpdateEventTelemetry;
}
