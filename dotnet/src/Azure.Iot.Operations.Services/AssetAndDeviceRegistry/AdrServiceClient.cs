// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Concurrent;
using Azure.Iot.Operations.Protocol;
using Azure.Iot.Operations.Services.AssetAndDeviceRegistry.AdrBaseService;
using Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models;
using Asset = Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models.Asset;
using Device = Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models.Device;
using DeviceStatus = Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models.DeviceStatus;
using NotificationResponse = Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models.NotificationResponse;

namespace Azure.Iot.Operations.Services.AssetAndDeviceRegistry;

public class AdrServiceClient(ApplicationContext applicationContext, IMqttPubSubClient mqttClient) : IAdrServiceClient
{
    private const string _endpointNameTokenKey = "inboundEndpointName";
    private const string _deviceNameTokenKey = "deviceName";
    private const byte _dummyByte = 1;
    private static readonly TimeSpan _defaultTimeout = TimeSpan.FromSeconds(10);
    private readonly AssetServiceClientStub _assetServiceClient = new(applicationContext, mqttClient);
    private readonly DeviceServiceClientStub _deviceServiceClient = new(applicationContext, mqttClient);
    private readonly ConcurrentDictionary<string, byte> _observedAssets = new();
    private readonly ConcurrentDictionary<string, byte> _observedEndpoints = new();
    private bool _disposed;

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;

        await _assetServiceClient.DisposeAsync().ConfigureAwait(false);
        await _deviceServiceClient.DisposeAsync().ConfigureAwait(false);
        GC.SuppressFinalize(this);
        _disposed = true;
    }

    /// <inheritdoc />
    public async Task<NotificationResponse> ObserveDeviceEndpointUpdatesAsync(string deviceName, string endpointName, TimeSpan? commandTimeout = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ObjectDisposedException.ThrowIf(_disposed, this);

        _observedEndpoints[$"{deviceName}_{endpointName}"] = _dummyByte;
        await _assetServiceClient.DeviceUpdateEventTelemetryReceiver.StartAsync(cancellationToken);

        var additionalTopicTokenMap = new Dictionary<string, string> { { _deviceNameTokenKey, deviceName }, { _endpointNameTokenKey, endpointName } };
        var notificationRequest = new SetNotificationPreferenceForDeviceUpdatesRequestPayload
        {
            NotificationPreferenceRequest = NotificationPreference.On
        };

        var result = await _assetServiceClient.SetNotificationPreferenceForDeviceUpdatesAsync(notificationRequest, null, additionalTopicTokenMap,
            commandTimeout ?? _defaultTimeout, cancellationToken);
        return result.NotificationPreferenceResponse.ToModel();
    }

    /// <inheritdoc />
    public async Task<NotificationResponse> UnobserveDeviceEndpointUpdatesAsync(string deviceName, string endpointName, TimeSpan? commandTimeout = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_observedEndpoints.TryRemove($"{deviceName}_{endpointName}", out _) && _observedEndpoints.IsEmpty)
            await _assetServiceClient.DeviceUpdateEventTelemetryReceiver.StopAsync(cancellationToken);

        var additionalTopicTokenMap = new Dictionary<string, string> { { _deviceNameTokenKey, deviceName }, { _endpointNameTokenKey, endpointName } };
        var notificationRequest = new SetNotificationPreferenceForDeviceUpdatesRequestPayload
        {
            NotificationPreferenceRequest = NotificationPreference.Off
        };

        var result = await _assetServiceClient.SetNotificationPreferenceForDeviceUpdatesAsync(notificationRequest, null, additionalTopicTokenMap,
            commandTimeout ?? _defaultTimeout, cancellationToken);
        return result.NotificationPreferenceResponse.ToModel();
    }

    /// <inheritdoc />
    public async Task<Device> GetDeviceAsync(string deviceName, string endpointName, TimeSpan? commandTimeout = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ObjectDisposedException.ThrowIf(_disposed, this);

        var additionalTopicTokenMap = new Dictionary<string, string> { { _deviceNameTokenKey, deviceName }, { _endpointNameTokenKey, endpointName } };

        var result = await _assetServiceClient.GetDeviceAsync(null, additionalTopicTokenMap, commandTimeout ?? _defaultTimeout,
            cancellationToken);
        return result.Device.ToModel();
    }

    /// <inheritdoc />
    public async Task<Device> UpdateDeviceStatusAsync(string deviceName, string endpointName,
        DeviceStatus status, TimeSpan? commandTimeout = null, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ObjectDisposedException.ThrowIf(_disposed, this);

        var additionalTopicTokenMap = new Dictionary<string, string> { { _deviceNameTokenKey, deviceName }, { _endpointNameTokenKey, endpointName } };

        var request = new UpdateDeviceStatusRequestPayload
        {
            DeviceStatusUpdate = status.ToProtocol()
        };

        var result = await _assetServiceClient.UpdateDeviceStatusAsync(request, null, additionalTopicTokenMap,
            commandTimeout ?? _defaultTimeout, cancellationToken);
        return result.UpdatedDevice.ToModel();
    }

    /// <inheritdoc />
    public async Task<NotificationResponse> ObserveAssetUpdatesAsync(string deviceName, string endpointName, string assetName,
        TimeSpan? commandTimeout = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ObjectDisposedException.ThrowIf(_disposed, this);

        _observedAssets[$"{deviceName}_{endpointName}_{assetName}"] = _dummyByte;
        await _assetServiceClient.AssetUpdateEventTelemetryReceiver.StartAsync(cancellationToken);

        var additionalTopicTokenMap = new Dictionary<string, string> { { _deviceNameTokenKey, deviceName }, { _endpointNameTokenKey, endpointName } };
        var notificationRequest = new SetNotificationPreferenceForAssetUpdatesRequestPayload
        {
            NotificationPreferenceRequest = new SetNotificationPreferenceForAssetUpdatesRequestSchema
            {
                AssetName = assetName,
                NotificationPreference = NotificationPreference.On
            }
        };
        var result = await _assetServiceClient.SetNotificationPreferenceForAssetUpdatesAsync(notificationRequest, null, additionalTopicTokenMap,
            commandTimeout ?? _defaultTimeout,
            cancellationToken);
        return result.NotificationPreferenceResponse.ToModel();
    }

    /// <inheritdoc />
    public async Task<NotificationResponse> UnobserveAssetUpdatesAsync(string deviceName, string endpointName, string assetName,
        TimeSpan? commandTimeout = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_observedAssets.TryRemove($"{deviceName}_{endpointName}_{assetName}", out _) && _observedAssets.IsEmpty)
            await _assetServiceClient.AssetUpdateEventTelemetryReceiver.StopAsync(cancellationToken);

        var additionalTopicTokenMap = new Dictionary<string, string> { { _deviceNameTokenKey, deviceName }, { _endpointNameTokenKey, endpointName } };
        var notificationRequest = new SetNotificationPreferenceForAssetUpdatesRequestPayload
        {
            NotificationPreferenceRequest = new SetNotificationPreferenceForAssetUpdatesRequestSchema
            {
                AssetName = assetName,
                NotificationPreference = NotificationPreference.Off
            }
        };
        var result = await _assetServiceClient.SetNotificationPreferenceForAssetUpdatesAsync(notificationRequest, null, additionalTopicTokenMap,
            commandTimeout ?? _defaultTimeout,
            cancellationToken);
        return result.NotificationPreferenceResponse.ToModel();
    }

    /// <inheritdoc />
    public async Task<Asset> GetAssetAsync(string deviceName, string endpointName, GetAssetRequest request, TimeSpan? commandTimeout = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ObjectDisposedException.ThrowIf(_disposed, this);

        var additionalTopicTokenMap = new Dictionary<string, string> { { _deviceNameTokenKey, deviceName }, { _endpointNameTokenKey, endpointName } };

        var result = await _assetServiceClient.GetAssetAsync(request.ToProtocol(), null, additionalTopicTokenMap, commandTimeout ?? _defaultTimeout,
            cancellationToken);
        return result.Asset.ToModel();
    }

    /// <inheritdoc />
    public async Task<Asset> UpdateAssetStatusAsync(string deviceName, string endpointName, UpdateAssetStatusRequest request,
        TimeSpan? commandTimeout = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ObjectDisposedException.ThrowIf(_disposed, this);

        var additionalTopicTokenMap = new Dictionary<string, string> { { _deviceNameTokenKey, deviceName }, { _endpointNameTokenKey, endpointName } };

        var result = await _assetServiceClient.UpdateAssetStatusAsync(request.ToProtocol(), null, additionalTopicTokenMap, commandTimeout ?? _defaultTimeout,
            cancellationToken);
        return result.UpdatedAsset.ToModel();
    }

    /// <inheritdoc />
    public async Task<CreateDetectedAssetResponse> CreateDetectedAssetAsync(string deviceName, string endpointName, CreateDetectedAssetRequest request,
        TimeSpan? commandTimeout = null, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ObjectDisposedException.ThrowIf(_disposed, this);

        var additionalTopicTokenMap = new Dictionary<string, string> { { _deviceNameTokenKey, deviceName }, { _endpointNameTokenKey, endpointName } };

        var result = await _assetServiceClient.CreateDetectedAssetAsync(request.ToProtocol(), null, additionalTopicTokenMap, commandTimeout ?? _defaultTimeout,
            cancellationToken);
        return result.CreateDetectedAssetResponse.ToModel();
    }

    /// <inheritdoc />
    public async Task<CreateDiscoveredAssetEndpointProfileResponse> CreateDiscoveredAssetEndpointProfileAsync(string deviceName, string endpointName,
        CreateDiscoveredAssetEndpointProfileRequest request, TimeSpan? commandTimeout = null, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ObjectDisposedException.ThrowIf(_disposed, this);

        var additionalTopicTokenMap = new Dictionary<string, string> { { _deviceNameTokenKey, deviceName }, { _endpointNameTokenKey, endpointName } };

        var result = await _deviceServiceClient.CreateDiscoveredAssetEndpointProfileAsync(request.ToProtocol(), null, additionalTopicTokenMap,
            commandTimeout ?? _defaultTimeout, cancellationToken);
        return result.CreateDiscoveredAssetEndpointProfileResponse.ToModel();
    }

    /// <inheritdoc />
    public event Func<string, Device?, Task>? OnReceiveDeviceUpdateEventTelemetry
    {
        add => _assetServiceClient.OnReceiveDeviceUpdateEventTelemetry += value;
        remove => _assetServiceClient.OnReceiveDeviceUpdateEventTelemetry -= value;
    }

    /// <inheritdoc />
    public event Func<string, Asset?, Task>? OnReceiveAssetUpdateEventTelemetry
    {
        add => _assetServiceClient.OnReceiveAssetUpdateEventTelemetry += value;
        remove => _assetServiceClient.OnReceiveAssetUpdateEventTelemetry -= value;
    }
}
