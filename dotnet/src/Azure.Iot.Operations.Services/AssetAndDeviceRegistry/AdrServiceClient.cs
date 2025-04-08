using System.Collections.Concurrent;
using Azure.Iot.Operations.Protocol;
using Azure.Iot.Operations.Services.AssetAndDeviceRegistry.AdrBaseService;
using Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models;
using Asset = Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models.Asset;
using AssetEndpointProfile = Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models.AssetEndpointProfile;
using NotificationResponse = Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models.NotificationResponse;

namespace Azure.Iot.Operations.Services.AssetAndDeviceRegistry;

public class AdrServiceClient(ApplicationContext applicationContext, IMqttPubSubClient mqttClient) : IAdrServiceClient
{
    private const string _aepNameTokenKey = "aepName";
    private const byte _dummyByte = 1;
    private static readonly TimeSpan _defaultTimeout = TimeSpan.FromSeconds(10);
    private readonly AssetEndpointProfileServiceClientStub _assetEndpointProfileServiceClient = new(applicationContext, mqttClient);
    private readonly AssetServiceClientStub _assetServiceClient = new(applicationContext, mqttClient);
    private bool _disposed;
    private readonly ConcurrentDictionary<string, byte> _observedObjects = new();

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        await _assetServiceClient.DisposeAsync().ConfigureAwait(false);
        await _assetEndpointProfileServiceClient.DisposeAsync().ConfigureAwait(false);
        GC.SuppressFinalize(this);
        _disposed = true;
    }

    /// <inheritdoc/>
    public async Task<NotificationResponse> ObserveAssetEndpointProfileUpdatesAsync(string aepName, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ObjectDisposedException.ThrowIf(_disposed, this);

        _observedObjects[aepName] = _dummyByte;
        await _assetServiceClient.StartAsync(cancellationToken);

        var additionalTopicTokenMap = new Dictionary<string, string> { { _aepNameTokenKey, aepName } };
        var notificationRequest = new NotifyOnAssetEndpointProfileUpdateRequestPayload
        {
            NotificationRequest = NotificationMessageType.On
        };

        var result = await _assetServiceClient.NotifyOnAssetEndpointProfileUpdateAsync(notificationRequest, null, additionalTopicTokenMap,
            _defaultTimeout, cancellationToken);
        return result.NotificationResponse.ToModel();
    }

    /// <inheritdoc/>
    public async Task<NotificationResponse> UnobserveAssetEndpointProfileUpdatesAsync(string aepName, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_observedObjects.TryRemove(aepName, out _))
        {
            await _assetServiceClient.StopAsync(cancellationToken);
        }

        var additionalTopicTokenMap = new Dictionary<string, string> { { _aepNameTokenKey, aepName } };
        var notificationRequest = new NotifyOnAssetEndpointProfileUpdateRequestPayload
        {
            NotificationRequest = NotificationMessageType.Off
        };

        var result = await _assetServiceClient.NotifyOnAssetEndpointProfileUpdateAsync(notificationRequest, null, additionalTopicTokenMap,
            _defaultTimeout, cancellationToken);
        return result.NotificationResponse.ToModel();
    }

    /// <inheritdoc/>
    public async Task<AssetEndpointProfile> GetAssetEndpointProfileAsync(string aepName, TimeSpan? commandTimeout = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ObjectDisposedException.ThrowIf(_disposed, this);

        var additionalTopicTokenMap = new Dictionary<string, string> { { _aepNameTokenKey, aepName } };

        var result = await _assetServiceClient.GetAssetEndpointProfileAsync(null, additionalTopicTokenMap, commandTimeout ?? _defaultTimeout,
            cancellationToken);
        return result.AssetEndpointProfile.ToModel();
    }

    /// <inheritdoc/>
    public async Task<AssetEndpointProfile> UpdateAssetEndpointProfileStatusAsync(string aepName,
        UpdateAssetEndpointProfileStatusRequest request, TimeSpan? commandTimeout = null, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ObjectDisposedException.ThrowIf(_disposed, this);

        var additionalTopicTokenMap = new Dictionary<string, string> { { _aepNameTokenKey, aepName } };

        var result = await _assetServiceClient.UpdateAssetEndpointProfileStatusAsync(request.ToProtocol(), null, additionalTopicTokenMap,
            commandTimeout ?? _defaultTimeout, cancellationToken);
        return result.UpdatedAssetEndpointProfile.ToModel();
    }

    /// <inheritdoc/>
    public async Task<NotificationResponse> ObserveAssetUpdatesAsync(string aepName, string assetName, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ObjectDisposedException.ThrowIf(_disposed, this);

        _observedObjects[$"{aepName}_{assetName}"] = _dummyByte;
        await _assetServiceClient.StartAsync(cancellationToken);

        var additionalTopicTokenMap = new Dictionary<string, string> { { _aepNameTokenKey, aepName } };
        var notificationRequest = new NotifyOnAssetUpdateRequestPayload
        {
            NotificationRequest = new NotifyOnAssetUpdateRequestSchema
            {
                AssetName = assetName,
                NotificationMessageType = NotificationMessageType.On
            }
        };

        var result = await _assetServiceClient.NotifyOnAssetUpdateAsync(notificationRequest,
            null,
            additionalTopicTokenMap,
            _defaultTimeout,
            cancellationToken);
        return result.NotificationResponse.ToModel();
    }

    /// <inheritdoc/>
    public async Task<NotificationResponse> UnobserveAssetUpdatesAsync(string aepName, string assetName, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_observedObjects.TryRemove($"{aepName}_{assetName}", out _))
        {
            await _assetServiceClient.StopAsync(cancellationToken);
        }

        var additionalTopicTokenMap = new Dictionary<string, string> { { _aepNameTokenKey, aepName } };
        var notificationRequest = new NotifyOnAssetUpdateRequestPayload
        {
            NotificationRequest = new NotifyOnAssetUpdateRequestSchema
            {
                AssetName = assetName,
                NotificationMessageType = NotificationMessageType.Off
            }
        };

        var result = await _assetServiceClient.NotifyOnAssetUpdateAsync(notificationRequest,
            null,
            additionalTopicTokenMap,
            _defaultTimeout,
            cancellationToken);
        return result.NotificationResponse.ToModel();
    }

    /// <inheritdoc/>
    public async Task<Asset> GetAssetAsync(string aepName, GetAssetRequest request, TimeSpan? commandTimeout = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ObjectDisposedException.ThrowIf(_disposed, this);

        var additionalTopicTokenMap = new Dictionary<string, string> { { _aepNameTokenKey, aepName } };

        var result = await _assetServiceClient.GetAssetAsync(request.ToProtocol(), null, additionalTopicTokenMap, commandTimeout ?? _defaultTimeout,
            cancellationToken);
        return result.Asset.ToModel();
    }

    /// <inheritdoc/>
    public async Task<Asset> UpdateAssetStatusAsync(string aepName, UpdateAssetStatusRequest request, TimeSpan? commandTimeout = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ObjectDisposedException.ThrowIf(_disposed, this);

        var additionalTopicTokenMap = new Dictionary<string, string> { { _aepNameTokenKey, aepName } };

        var result = await _assetServiceClient.UpdateAssetStatusAsync(request.ToProtocol(), null, additionalTopicTokenMap, commandTimeout ?? _defaultTimeout,
            cancellationToken);
        return result.UpdatedAsset.ToModel();
    }

    /// <inheritdoc/>
    public async Task<CreateDetectedAssetResponse> CreateDetectedAssetAsync(string aepName, CreateDetectedAssetRequest request,
        TimeSpan? commandTimeout = null, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ObjectDisposedException.ThrowIf(_disposed, this);

        var additionalTopicTokenMap = new Dictionary<string, string> { { _aepNameTokenKey, aepName } };

        var result = await _assetServiceClient.CreateDetectedAssetAsync(request.ToProtocol(), null, additionalTopicTokenMap, commandTimeout ?? _defaultTimeout,
            cancellationToken);
        return result.CreateDetectedAssetResponse.ToModel();
    }

    /// <inheritdoc/>
    public async Task<CreateDiscoveredAssetEndpointProfileResponse> CreateDiscoveredAssetEndpointProfileAsync(string aepName,
        CreateDiscoveredAssetEndpointProfileRequest request, TimeSpan? commandTimeout = null, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ObjectDisposedException.ThrowIf(_disposed, this);

        var additionalTopicTokenMap = new Dictionary<string, string> { { _aepNameTokenKey, aepName } };

        var result = await _assetEndpointProfileServiceClient.CreateDiscoveredAssetEndpointProfileAsync(request.ToProtocol(), null, additionalTopicTokenMap,
            commandTimeout ?? _defaultTimeout, cancellationToken);
        return result.CreateDiscoveredAssetEndpointProfileResponse.ToModel();
    }

    /// <inheritdoc/>
    public event Func<string, AssetEndpointProfile?, Task>? OnReceiveAssetEndpointProfileUpdateTelemetry
    {
        add => _assetServiceClient.OnReceiveAssetEndpointProfileUpdateTelemetry += value;
        remove => _assetServiceClient.OnReceiveAssetEndpointProfileUpdateTelemetry -= value;
    }

    /// <inheritdoc/>
    public event Func<string, Asset?, Task>? OnReceiveAssetUpdateEventTelemetry
    {
        add => _assetServiceClient.OnReceiveAssetUpdateEventTelemetry += value;
        remove => _assetServiceClient.OnReceiveAssetUpdateEventTelemetry -= value;
    }
}
