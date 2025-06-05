// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Concurrent;
using Azure.Iot.Operations.Protocol;
using Azure.Iot.Operations.Protocol.RPC;
using Azure.Iot.Operations.Services.AssetAndDeviceRegistry.AdrBaseService;
using Azure.Iot.Operations.Services.AssetAndDeviceRegistry.DeviceDiscoveryService;
using Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models;
using AkriServiceErrorException = Azure.Iot.Operations.Services.AssetAndDeviceRegistry.AdrBaseService.AkriServiceErrorException;
using Asset = Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models.Asset;
using AssetStatus = Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models.AssetStatus;
using Device = Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models.Device;
using DeviceStatus = Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models.DeviceStatus;
using NotificationResponse = Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models.NotificationResponse;

namespace Azure.Iot.Operations.Services.AssetAndDeviceRegistry;

public class AdrServiceClient(ApplicationContext applicationContext, IMqttPubSubClient mqttClient, string clientId) : IAdrServiceClient
{
    private const string _connectorClientIdTokenKey = "connectorClientId";
    private const string _discoveryClientIdTokenKey = "discoveryClientId";
    private const string _endpointNameTokenKey = "inboundEndpointName";
    private const string _deviceNameTokenKey = "deviceName";
    private const string _inboundEpTypeTokenKey = "inboundEndpointType";
    private const byte _dummyByte = 1;
    private static readonly TimeSpan _defaultTimeout = TimeSpan.FromSeconds(10);
    private readonly AdrBaseServiceClientStub _adrBaseServiceClient = new(applicationContext, mqttClient);
    private readonly DeviceDiscoveryServiceClientStub _deviceDiscoveryServiceClient = new(applicationContext, mqttClient);
    private readonly ConcurrentDictionary<string, byte> _observedAssets = new();
    private readonly ConcurrentDictionary<string, byte> _observedEndpoints = new();
    private bool _disposed;

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        await _adrBaseServiceClient.DisposeAsync().ConfigureAwait(false);
        await _deviceDiscoveryServiceClient.DisposeAsync().ConfigureAwait(false);
        GC.SuppressFinalize(this);
        _disposed = true;
    }

    /// <inheritdoc />
    public async Task<Models.SetNotificationPreferenceForDeviceUpdatesResponsePayload> SetNotificationPreferenceForDeviceUpdatesAsync(string deviceName, string inboundEndpointName, Models.NotificationPreference notificationPreference, TimeSpan? commandTimeout = null, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ObjectDisposedException.ThrowIf(_disposed, this);

        _observedEndpoints[$"{deviceName}_{inboundEndpointName}"] = _dummyByte;
        await _adrBaseServiceClient.DeviceUpdateEventTelemetryReceiver.StartAsync(cancellationToken);

        Dictionary<string, string> additionalTopicTokenMap = new()
        {
            { _connectorClientIdTokenKey, clientId },
            { _deviceNameTokenKey, deviceName },
            { _endpointNameTokenKey, inboundEndpointName }
        };
        var notificationRequest = new AdrBaseService.SetNotificationPreferenceForDeviceUpdatesRequestPayload
        {
            NotificationPreferenceRequest = (AdrBaseService.NotificationPreference)(int)notificationPreference
        };

        try
        {
            var result = await _adrBaseServiceClient.SetNotificationPreferenceForDeviceUpdatesAsync(
                notificationRequest,
                null,
                additionalTopicTokenMap,
                commandTimeout ?? _defaultTimeout,
                cancellationToken);
            return result.ToModel();
        }
        catch (AkriServiceErrorException exception)
        {
            var error = exception.AkriServiceError.ToModel();
            throw new Models.AkriServiceErrorException(error);
        }
    }

    public async Task<Models.SetNotificationPreferenceForAssetUpdatesResponsePayload> SetNotificationPreferenceForAssetUpdatesAsync(string deviceName, string inboundEndpointName, string assetName, Models.NotificationPreference notificationPreference, TimeSpan? commandTimeout = null, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ObjectDisposedException.ThrowIf(_disposed, this);

        _observedAssets[$"{deviceName}_{inboundEndpointName}_{assetName}"] = _dummyByte;
        await _adrBaseServiceClient.AssetUpdateEventTelemetryReceiver.StartAsync(cancellationToken);

        Dictionary<string, string> additionalTopicTokenMap = new()
        {
            { _connectorClientIdTokenKey, clientId },
            { _deviceNameTokenKey, deviceName },
            { _endpointNameTokenKey, inboundEndpointName }
        };
        var notificationRequest = new AdrBaseService.SetNotificationPreferenceForAssetUpdatesRequestPayload
        {
            NotificationPreferenceRequest = new AdrBaseService.SetNotificationPreferenceForAssetUpdatesRequestSchema
            {
                AssetName = assetName,
                NotificationPreference = (AdrBaseService.NotificationPreference)(int)notificationPreference
            }
        };

        try
        {
            var result = await _adrBaseServiceClient.SetNotificationPreferenceForAssetUpdatesAsync(
                notificationRequest,
                null,
                additionalTopicTokenMap,
                commandTimeout ?? _defaultTimeout,
                cancellationToken);
            return result.ToModel();
        }
        catch (AkriServiceErrorException exception)
        {
            var error = exception.AkriServiceError.ToModel();
            throw new Models.AkriServiceErrorException(error);
        }
    }

    /// <inheritdoc />
    public async Task<Device> GetDeviceAsync(string deviceName, string inboundEndpointName, TimeSpan? commandTimeout = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ObjectDisposedException.ThrowIf(_disposed, this);

        Dictionary<string, string> additionalTopicTokenMap = new()
        {
            { _connectorClientIdTokenKey, clientId },
            { _deviceNameTokenKey, deviceName },
            { _endpointNameTokenKey, inboundEndpointName }
        };

        try
        {
            var result = await _adrBaseServiceClient.GetDeviceAsync(null, additionalTopicTokenMap, commandTimeout ?? _defaultTimeout,
                cancellationToken);
            return result.Device.ToModel();
        }
        catch (AkriServiceErrorException exception)
        {
            var error = exception.AkriServiceError.ToModel();
            throw new Models.AkriServiceErrorException(error);
        }
    }

    /// <inheritdoc />
    public async Task<DeviceStatus> UpdateDeviceStatusAsync(string deviceName, string inboundEndpointName,
        DeviceStatus status, TimeSpan? commandTimeout = null, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ObjectDisposedException.ThrowIf(_disposed, this);

        Dictionary<string, string> additionalTopicTokenMap = new()
        {
            { _connectorClientIdTokenKey, clientId },
            { _deviceNameTokenKey, deviceName },
            { _endpointNameTokenKey, inboundEndpointName }
        };

        var request = new UpdateDeviceStatusRequestPayload
        {
            DeviceStatusUpdate = status.ToProtocol()
        };

        try
        {
            UpdateDeviceStatusResponsePayload result = await _adrBaseServiceClient.UpdateDeviceStatusAsync(
                request,
                null,
                additionalTopicTokenMap,
                commandTimeout ?? _defaultTimeout,
                cancellationToken);
            return result.UpdatedDeviceStatus.ToModel();
        }
        catch (AkriServiceErrorException exception)
        {
            var error = exception.AkriServiceError.ToModel();
            throw new Models.AkriServiceErrorException(error);
        }
    }

    public async Task<DeviceStatus> GetDeviceStatusAsync(string deviceName, string inboundEndpointName, TimeSpan? commandTimeout = null, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ObjectDisposedException.ThrowIf(_disposed, this);

        Dictionary<string, string> additionalTopicTokenMap = new()
        {
            { _connectorClientIdTokenKey, clientId },
            { _deviceNameTokenKey, deviceName },
            { _endpointNameTokenKey, inboundEndpointName }
        };

        try
        {
            GetDeviceStatusResponsePayload result = await _adrBaseServiceClient.GetDeviceStatusAsync(
                null,
                additionalTopicTokenMap,
                commandTimeout ?? _defaultTimeout,
                cancellationToken);
            return result.DeviceStatus.ToModel();
        }
        catch (AkriServiceErrorException exception)
        {
            var error = exception.AkriServiceError.ToModel();
            throw new Models.AkriServiceErrorException(error);
        }
    }

    /// <inheritdoc />
    public async Task<Asset> GetAssetAsync(string deviceName, string inboundEndpointName, string assetName, TimeSpan? commandTimeout = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ObjectDisposedException.ThrowIf(_disposed, this);

        Dictionary<string, string> additionalTopicTokenMap = new()
        {
            { _connectorClientIdTokenKey, clientId },
            { _deviceNameTokenKey, deviceName },
            { _endpointNameTokenKey, inboundEndpointName }
        };

        try
        {
            var result = await _adrBaseServiceClient.GetAssetAsync(
                new() { AssetName = assetName },
                null,
                additionalTopicTokenMap,
                commandTimeout ?? _defaultTimeout,
                cancellationToken);
            return result.Asset.ToModel();
        }
        catch (DeviceDiscoveryService.AkriServiceErrorException exception)
        {
            var error = exception.AkriServiceError.ToModel();
            throw new Models.AkriServiceErrorException(error);
        }
    }

    /// <inheritdoc />
    public async Task<AssetStatus> UpdateAssetStatusAsync(string deviceName, string inboundEndpointName, UpdateAssetStatusRequest request,
        TimeSpan? commandTimeout = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ObjectDisposedException.ThrowIf(_disposed, this);

        Dictionary<string, string> additionalTopicTokenMap = new()
        {
            { _connectorClientIdTokenKey, clientId },
            { _deviceNameTokenKey, deviceName },
            { _endpointNameTokenKey, inboundEndpointName }
        };

        try
        {
            var result = await _adrBaseServiceClient.UpdateAssetStatusAsync(request.ToProtocol(),
                null,
                additionalTopicTokenMap,
                commandTimeout ?? _defaultTimeout,
                cancellationToken);
            return result.UpdatedAssetStatus.ToModel();
        }
        catch (AkriServiceErrorException exception)
        {
            var error = exception.AkriServiceError.ToModel();
            throw new Models.AkriServiceErrorException(error);
        }
    }

    /// <inheritdoc />
    public async Task<AssetStatus> GetAssetStatusAsync(string deviceName, string inboundEndpointName, string assetName, TimeSpan? commandTimeout = null, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ObjectDisposedException.ThrowIf(_disposed, this);

        Dictionary<string, string> additionalTopicTokenMap = new()
        {
            { _connectorClientIdTokenKey, clientId },
            { _deviceNameTokenKey, deviceName },
            { _endpointNameTokenKey, inboundEndpointName }
        };

        var request = new GetAssetStatusRequestPayload
        {
            AssetName = assetName
        };

        try
        {
            var result = await _adrBaseServiceClient.GetAssetStatusAsync(
                request,
                null,
                additionalTopicTokenMap,
                commandTimeout ?? _defaultTimeout,
                cancellationToken);
            return result.AssetStatus.ToModel();
        }
        catch (AkriServiceErrorException exception)
        {
            var error = exception.AkriServiceError.ToModel();
            throw new Models.AkriServiceErrorException(error);
        }
    }

    /// <inheritdoc />
    public async Task<Models.CreateOrUpdateDiscoveredAssetResponsePayload> CreateOrUpdateDiscoveredAssetAsync(string deviceName, string inboundEndpointName, CreateOrUpdateDiscoveredAssetRequest request,
        TimeSpan? commandTimeout = null, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ObjectDisposedException.ThrowIf(_disposed, this);

        Dictionary<string, string> additionalTopicTokenMap = new()
        {
            { _connectorClientIdTokenKey, clientId },
            { _deviceNameTokenKey, deviceName },
            { _endpointNameTokenKey, inboundEndpointName }
        };

        try
        {
            var result = await _adrBaseServiceClient.CreateOrUpdateDiscoveredAssetAsync(
                request.ToProtocol(),
                null,
                additionalTopicTokenMap,
                commandTimeout ?? _defaultTimeout,
                cancellationToken);
            return result.ToModel();
        }
        catch (AkriServiceErrorException exception)
        {
            var error = exception.AkriServiceError.ToModel();
            throw new Models.AkriServiceErrorException(error);
        }
    }

    /// <inheritdoc />
    public async Task<Models.CreateOrUpdateDiscoveredDeviceResponsePayload> CreateOrUpdateDiscoveredDeviceAsync(Models.CreateOrUpdateDiscoveredDeviceRequestSchema request,
        string inboundEndpointType, TimeSpan? commandTimeout = null, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ObjectDisposedException.ThrowIf(_disposed, this);

        Dictionary<string, string> additionalTopicTokenMap = new()
        {
            { _discoveryClientIdTokenKey, clientId },
            { _inboundEpTypeTokenKey, inboundEndpointType },
        };

        var req = new DeviceDiscoveryService.CreateOrUpdateDiscoveredDeviceRequestPayload
        {
            DiscoveredDeviceRequest = new DeviceDiscoveryService.CreateOrUpdateDiscoveredDeviceRequestSchema
            {
                DiscoveredDevice = request.DiscoveredDevice.ToProtocol(),
                DiscoveredDeviceName = request.DiscoveredDeviceName,
            }
        };

        try
        {
            var result = await _deviceDiscoveryServiceClient.CreateOrUpdateDiscoveredDeviceAsync(
                req,
                null,
                additionalTopicTokenMap,
                commandTimeout ?? _defaultTimeout,
                cancellationToken);
            return result.ToModel();
        }
        catch (DeviceDiscoveryService.AkriServiceErrorException exception)
        {
            var error = exception.AkriServiceError.ToModel();
            throw new Models.AkriServiceErrorException(error);
        }
    }

    /// <inheritdoc />
    public event Func<string, Device, Task>? OnReceiveDeviceUpdateEventTelemetry
    {
        add => _adrBaseServiceClient.OnReceiveDeviceUpdateEventTelemetry += value;
        remove => _adrBaseServiceClient.OnReceiveDeviceUpdateEventTelemetry -= value;
    }

    /// <inheritdoc />
    public event Func<string, Asset, Task>? OnReceiveAssetUpdateEventTelemetry
    {
        add => _adrBaseServiceClient.OnReceiveAssetUpdateEventTelemetry += value;
        remove => _adrBaseServiceClient.OnReceiveAssetUpdateEventTelemetry -= value;
    }
}
