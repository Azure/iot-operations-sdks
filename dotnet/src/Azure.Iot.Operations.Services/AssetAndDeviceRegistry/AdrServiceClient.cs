using Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models;
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Protocol;
using Azure.Iot.Operations.Services.AssetAndDeviceRegistry.AdrBaseService;

namespace Azure.Iot.Operations.Services.AssetAndDeviceRegistry;

public class AdrServiceClient(ApplicationContext applicationContext, IMqttPubSubClient mqttClient) : IAdrServiceClient
{
    private readonly AssetServiceClientStub _assetServiceClient = new(applicationContext, mqttClient);
    private readonly AssetEndpointProfileServiceClientStub _assetEndpointProfileServiceClient = new(applicationContext, mqttClient);
    private bool _disposed;
    private static readonly TimeSpan _defaultTimeout = TimeSpan.FromSeconds(10);
    private bool _observingAssetEndpointProfileUpdates;
    private bool _observingAssetUpdates;
    private readonly SemaphoreSlim _syncLock = new(1, 1);

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        await _assetServiceClient.DisposeAsync().ConfigureAwait(false);
        await _assetEndpointProfileServiceClient.DisposeAsync().ConfigureAwait(false);
        GC.SuppressFinalize(this);
        _disposed = true;
    }

    public async Task<Models.NotificationResponse> ObserveAssetEndpointProfileUpdatesAsync(string aepName, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ObjectDisposedException.ThrowIf(_disposed, this);

        await _syncLock.WaitAsync(cancellationToken);
        try
        {
            await _assetServiceClient.StartAsync(cancellationToken);
            _observingAssetEndpointProfileUpdates = true;
        }
        finally
        {
            _syncLock.Release();
        }

        var additionalTopicTokenMap = new Dictionary<string, string> { { "aepName", aepName } };
        var notificationRequest = new NotifyOnAssetEndpointProfileUpdateRequestPayload
        {
            NotificationRequest = NotificationMessageType.On
        };

        return (await _assetServiceClient.NotifyOnAssetEndpointProfileUpdateAsync(notificationRequest, null, additionalTopicTokenMap,
            _defaultTimeout, cancellationToken)).NotificationResponse.ToModel();
    }

    public async Task<Models.NotificationResponse> UnobserveAssetEndpointProfileUpdatesAsync(string aepName, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ObjectDisposedException.ThrowIf(_disposed, this);

        await _syncLock.WaitAsync(cancellationToken);
        try
        {
            if (_observingAssetEndpointProfileUpdates && !_observingAssetUpdates)
            {
                await _assetServiceClient.StopAsync(cancellationToken);
            }
            _observingAssetEndpointProfileUpdates = false;
        }
        finally
        {
            _syncLock.Release();
        }

        var additionalTopicTokenMap = new Dictionary<string, string> { { "aepName", aepName } };
        var notificationRequest = new NotifyOnAssetEndpointProfileUpdateRequestPayload
        {
            NotificationRequest = NotificationMessageType.Off
        };

        return (await _assetServiceClient.NotifyOnAssetEndpointProfileUpdateAsync(notificationRequest, null, additionalTopicTokenMap,
            _defaultTimeout, cancellationToken)).NotificationResponse.ToModel();
    }

    public async Task<AssetEndpointProfileResponse> GetAssetEndpointProfileAsync(string aepName, TimeSpan? commandTimeout = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ObjectDisposedException.ThrowIf(_disposed, this);

        var additionalTopicTokenMap = new Dictionary<string, string> { { "aepName", aepName } };
        var result = await _assetServiceClient.GetAssetEndpointProfileAsync(null, additionalTopicTokenMap, commandTimeout ?? _defaultTimeout, cancellationToken);
        return result.AssetEndpointProfile.ToModel();
    }

    public async Task<AssetEndpointProfileResponse> UpdateAssetEndpointProfileStatusAsync(string aepName,
        UpdateAssetEndpointProfileStatusRequest request, TimeSpan? commandTimeout = null, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ObjectDisposedException.ThrowIf(_disposed, this);

        var additionalTopicTokenMap = new Dictionary<string, string> { { "aepName", aepName } };
        var result = await _assetServiceClient.UpdateAssetEndpointProfileStatusAsync(request.ToProtocol(), null, additionalTopicTokenMap, commandTimeout ?? _defaultTimeout, cancellationToken);
        return result.UpdatedAssetEndpointProfile.ToModel();
    }

    public async Task<Models.NotificationResponse> ObserveAssetUpdatesAsync(string aepName, string assetName, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ObjectDisposedException.ThrowIf(_disposed, this);

        await _syncLock.WaitAsync(cancellationToken);
        try
        {
            await _assetServiceClient.StartAsync(cancellationToken);
            _observingAssetUpdates = true;
        }
        finally
        {
            _syncLock.Release();
        }

        var additionalTopicTokenMap = new Dictionary<string, string> { { "aepName", aepName } };
        var notificationRequest = new NotifyOnAssetUpdateRequestPayload
        {
            NotificationRequest = new NotifyOnAssetUpdateRequestSchema()
            {
                AssetName = assetName,
                NotificationMessageType = NotificationMessageType.On
            }
        };

        return (await _assetServiceClient.NotifyOnAssetUpdateAsync(notificationRequest,
                null,
                additionalTopicTokenMap,
                _defaultTimeout,
                cancellationToken)).NotificationResponse.ToModel();
    }

    public async Task<Models.NotificationResponse> UnobserveAssetUpdatesAsync(string aepName, string assetName, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ObjectDisposedException.ThrowIf(_disposed, this);

        await _syncLock.WaitAsync(cancellationToken);
        try
        {
            if (_observingAssetUpdates && !_observingAssetEndpointProfileUpdates)
            {
                await _assetServiceClient.StopAsync(cancellationToken);
            }
            _observingAssetUpdates = false;
        }
        finally
        {
            _syncLock.Release();
        }

        var additionalTopicTokenMap = new Dictionary<string, string> { { "aepName", aepName } };
        var notificationRequest = new NotifyOnAssetUpdateRequestPayload
        {
            NotificationRequest = new NotifyOnAssetUpdateRequestSchema()
            {
                AssetName = assetName,
                NotificationMessageType = NotificationMessageType.Off
            }
        };

        return (await _assetServiceClient.NotifyOnAssetUpdateAsync(notificationRequest,
                null,
                additionalTopicTokenMap,
                _defaultTimeout,
                cancellationToken)).NotificationResponse.ToModel();

    }

    public async Task<AssetResponse> GetAssetAsync(string aepName, GetAssetRequest request, TimeSpan? commandTimeout = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ObjectDisposedException.ThrowIf(_disposed, this);

        var additionalTopicTokenMap = new Dictionary<string, string> { { "aepName", aepName } };
        var result = await _assetServiceClient.GetAssetAsync(request.ToProtocol(), null, additionalTopicTokenMap, commandTimeout ?? _defaultTimeout, cancellationToken);
        return result.Asset.ToModel();
    }

    public async Task<AssetResponse> UpdateAssetStatusAsync(string aepName, UpdateAssetStatusRequest request, TimeSpan? commandTimeout = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ObjectDisposedException.ThrowIf(_disposed, this);

        var additionalTopicTokenMap = new Dictionary<string, string> { { "aepName", aepName } };
        var result = await _assetServiceClient.UpdateAssetStatusAsync(request.ToProtocol(), null, additionalTopicTokenMap, commandTimeout ?? _defaultTimeout, cancellationToken);
        return result.UpdatedAsset.ToModel();
    }

    public async Task<CreateDetectedAssetResponse> CreateDetectedAssetAsync(string aepName, CreateDetectedAssetRequest request,
        TimeSpan? commandTimeout = null, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ObjectDisposedException.ThrowIf(_disposed, this);

        var additionalTopicTokenMap = new Dictionary<string, string> { { "aepName", aepName } };
        var result = await _assetServiceClient.CreateDetectedAssetAsync(request.ToProtocol(), null, additionalTopicTokenMap, commandTimeout ?? _defaultTimeout, cancellationToken);
        return result.CreateDetectedAssetResponse.ToModel();
    }

    public async Task<CreateDiscoveredAssetEndpointProfileResponse> CreateDiscoveredAssetEndpointProfileAsync(string aepName,
        CreateDiscoveredAssetEndpointProfileRequest request, TimeSpan? commandTimeout = null, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ObjectDisposedException.ThrowIf(_disposed, this);

        var additionalTopicTokenMap = new Dictionary<string, string> { { "aepName", aepName } };
        var result = await _assetEndpointProfileServiceClient.CreateDiscoveredAssetEndpointProfileAsync(request.ToProtocol(), null, additionalTopicTokenMap, commandTimeout ?? _defaultTimeout, cancellationToken);
        return result.CreateDiscoveredAssetEndpointProfileResponse.ToModel();
    }

    public event Func<string, Models.AssetEndpointProfile?, Task>? OnReceiveAssetEndpointProfileUpdateTelemetry
    {
        add => _assetServiceClient.OnReceiveAssetEndpointProfileUpdateTelemetry += value;
        remove => _assetServiceClient.OnReceiveAssetEndpointProfileUpdateTelemetry -= value;
    }

    public event Func<string, Models.Asset?, Task>? OnReceiveAssetUpdateEventTelemetry
    {
        add => _assetServiceClient.OnReceiveAssetUpdateEventTelemetry += value;
        remove => _assetServiceClient.OnReceiveAssetUpdateEventTelemetry -= value;
    }
}
