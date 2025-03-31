// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Protocol;
using Azure.Iot.Operations.Services.AssetAndDeviceRegistry.AdrBaseService;
using AepTypes = Azure.Iot.Operations.Services.AssetAndDeviceRegistry.AepTypeService;

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

    public async Task<NotificationResponse> ObserveAssetEndpointProfileUpdatesAsync(string aepName, CancellationToken cancellationToken)
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
            _defaultTimeout, cancellationToken)).NotificationResponse;
    }

    public async Task<NotificationResponse> UnobserveAssetEndpointProfileUpdatesAsync(string aepName, CancellationToken cancellationToken)
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
            _defaultTimeout, cancellationToken)).NotificationResponse;
    }

    public async Task<AssetEndpointProfile?> GetAssetEndpointProfileAsync(string aepName, TimeSpan? commandTimeout = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ObjectDisposedException.ThrowIf(_disposed, this);

        var additionalTopicTokenMap = new Dictionary<string, string> { { "aepName", aepName } };
        return (await _assetServiceClient.GetAssetEndpointProfileAsync(null,
                additionalTopicTokenMap,
                commandTimeout ?? _defaultTimeout,
                cancellationToken)).AssetEndpointProfile;
    }

    public async Task<AssetEndpointProfile?> UpdateAssetEndpointProfileStatusAsync(string aepName,
        UpdateAssetEndpointProfileStatusRequestPayload requestPayload, TimeSpan? commandTimeout = null, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ObjectDisposedException.ThrowIf(_disposed, this);

        var additionalTopicTokenMap = new Dictionary<string, string> { { "aepName", aepName } };
        return (await _assetServiceClient.UpdateAssetEndpointProfileStatusAsync(requestPayload, null, additionalTopicTokenMap,
            commandTimeout ?? _defaultTimeout, cancellationToken)).UpdatedAssetEndpointProfile;
    }

    public async Task<NotificationResponse> ObserveAssetUpdatesAsync(string aepName, string assetName, CancellationToken cancellationToken)
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
                cancellationToken)).NotificationResponse;
    }

    public async Task<NotificationResponse> UnobserveAssetUpdatesAsync(string aepName, string assetName, CancellationToken cancellationToken)
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
                cancellationToken)).NotificationResponse;

    }

    public async Task<Asset?> GetAssetAsync(string aepName, GetAssetRequestPayload requestPayload, TimeSpan? commandTimeout = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ObjectDisposedException.ThrowIf(_disposed, this);

        var additionalTopicTokenMap = new Dictionary<string, string> { { "aepName", aepName } };
        return (await _assetServiceClient.GetAssetAsync(requestPayload,
                null,
                additionalTopicTokenMap,
                commandTimeout ?? _defaultTimeout,
                cancellationToken)).Asset;
    }

    public async Task<Asset?> UpdateAssetStatusAsync(string aepName, UpdateAssetStatusRequestPayload requestPayload, TimeSpan? commandTimeout = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ObjectDisposedException.ThrowIf(_disposed, this);

        var additionalTopicTokenMap = new Dictionary<string, string> { { "aepName", aepName } };
        return (await _assetServiceClient.UpdateAssetStatusAsync(requestPayload,
            null,
            additionalTopicTokenMap,
            commandTimeout ?? _defaultTimeout,
            cancellationToken)).UpdatedAsset;
    }

    public async Task<CreateDetectedAssetResponseSchema?> CreateDetectedAssetAsync(string aepName, CreateDetectedAssetRequestPayload requestPayload,
        TimeSpan? commandTimeout = null, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ObjectDisposedException.ThrowIf(_disposed, this);

        var additionalTopicTokenMap = new Dictionary<string, string> { { "aepName", aepName } };
        return (await _assetServiceClient.CreateDetectedAssetAsync(requestPayload,
            null,
            additionalTopicTokenMap,
            commandTimeout ?? _defaultTimeout,
            cancellationToken)).CreateDetectedAssetResponse;
    }

    public async Task<AepTypes.CreateDiscoveredAssetEndpointProfileResponseSchema?> CreateDiscoveredAssetEndpointProfileAsync(string aepName,
        AepTypes.CreateDiscoveredAssetEndpointProfileRequestPayload requestPayload,
        TimeSpan? commandTimeout = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ObjectDisposedException.ThrowIf(_disposed, this);

        var additionalTopicTokenMap = new Dictionary<string, string> { { "aepName", aepName } };
        return (await _assetEndpointProfileServiceClient.CreateDiscoveredAssetEndpointProfileAsync(requestPayload,
            null,
            additionalTopicTokenMap,
            commandTimeout ?? _defaultTimeout,
            cancellationToken)).CreateDiscoveredAssetEndpointProfileResponse;
    }

    public event Func<string, AssetEndpointProfile?, Task>? OnReceiveAssetEndpointProfileUpdateTelemetry
    {
        add => _assetServiceClient.OnReceiveAssetEndpointProfileUpdateTelemetry += value;
        remove => _assetServiceClient.OnReceiveAssetEndpointProfileUpdateTelemetry -= value;
    }

    public event Func<string, Asset?, Task>? OnReceiveAssetUpdateEventTelemetry
    {
        add => _assetServiceClient.OnReceiveAssetUpdateEventTelemetry += value;
        remove => _assetServiceClient.OnReceiveAssetUpdateEventTelemetry -= value;
    }
}
