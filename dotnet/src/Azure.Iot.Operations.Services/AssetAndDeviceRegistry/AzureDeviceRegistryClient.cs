// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Concurrent;
using Azure.Iot.Operations.Protocol;
using Azure.Iot.Operations.Protocol.Retry;
using Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Generated.AdrBaseService;
using Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models;
using AkriServiceErrorException = Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Generated.AdrBaseService.AkriServiceErrorException;
using Asset = Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models.Asset;
using AssetStatus = Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models.AssetStatus;
using Device = Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models.Device;
using DeviceStatus = Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models.DeviceStatus;

namespace Azure.Iot.Operations.Services.AssetAndDeviceRegistry;

public class AzureDeviceRegistryClient : IAzureDeviceRegistryClient
{
    private const string _connectorClientIdTokenKey = "connectorClientId";
    private const string _discoveryClientIdTokenKey = "discoveryClientId";
    private const string _endpointNameTokenKey = "inboundEndpointName";
    private const string _deviceNameTokenKey = "deviceName";
    private const string _inboundEpTypeTokenKey = "inboundEndpointType";
    private const byte _dummyByte = 1;
    private static readonly TimeSpan _defaultTimeout = TimeSpan.FromSeconds(10);
    private readonly string _connectorClientId;
    private readonly ApplicationContext _applicationContext;
    private readonly IAdrBaseServiceClientStub _adrBaseServiceClient;
    private readonly IDeviceDiscoveryServiceClientStub _deviceDiscoveryServiceClient;
    private readonly ConcurrentDictionary<string, byte> _observedAssets = new();
    private readonly ConcurrentDictionary<string, byte> _observedEndpoints = new();
    private bool _disposed;
    private readonly IRetryPolicy _retryPolicy;

    /// <summary>
    /// Construct an ADR service client.
    /// </summary>
    /// <param name="applicationContext">The shared context for this application.</param>
    /// <param name="mqttClient">The MQTT client to communicate with the ADR service with.</param>
    /// <param name="retryPolicy">
    /// The retry policy to apply to all operations. By default, <see cref="ExponentialBackoffRetryPolicy"/>
    /// will be used if none is provided. <see cref="NoRetryPolicy"/> can be provided if no retry is desired.</param>
    public AzureDeviceRegistryClient(ApplicationContext applicationContext, IMqttPubSubClient mqttClient, IRetryPolicy? retryPolicy = null)
    {
        _retryPolicy = retryPolicy ?? new ExponentialBackoffRetryPolicy(10, TimeSpan.FromSeconds(5));
        _applicationContext = applicationContext;
        _connectorClientId = mqttClient.ClientId ?? throw new ArgumentException("Must provide an MQTT client Id in the IMqttPubSubClient");

        _adrBaseServiceClient = new AdrBaseServiceClientStub(_applicationContext, mqttClient);
        _deviceDiscoveryServiceClient = new DeviceDiscoveryServiceClientStub(_applicationContext, mqttClient);
    }

    // For unit test purposes only
    internal AzureDeviceRegistryClient(ApplicationContext applicationContext, string connectorClientId, IAdrBaseServiceClientStub baseServiceClient, IDeviceDiscoveryServiceClientStub deviceDiscoveryServiceClientStub, IRetryPolicy? retryPolicy = null)
    {
        _retryPolicy = retryPolicy ?? new ExponentialBackoffRetryPolicy(3, TimeSpan.FromMilliseconds(5));
        _applicationContext = applicationContext;
        _connectorClientId = connectorClientId;
        _adrBaseServiceClient = baseServiceClient;
        _deviceDiscoveryServiceClient = deviceDiscoveryServiceClientStub;
    }

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
        if (string.IsNullOrWhiteSpace(deviceName))
        {
            throw new ArgumentException("Device name cannot be null or empty", nameof(deviceName));
        }

        if (string.IsNullOrWhiteSpace(inboundEndpointName))
        {
            throw new ArgumentException("Inbound endpoint name cannot be null or empty", nameof(inboundEndpointName));
        }

        return await RunWithRetryAsync<Models.SetNotificationPreferenceForDeviceUpdatesResponsePayload>(async () =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            ObjectDisposedException.ThrowIf(_disposed, this);

            _observedEndpoints[$"{deviceName}_{inboundEndpointName}"] = _dummyByte;
            await _adrBaseServiceClient.DeviceUpdateEventTelemetryReceiver.StartAsync(cancellationToken);

            Dictionary<string, string> additionalTopicTokenMap = new()
            {
                { _connectorClientIdTokenKey, _connectorClientId },
                { _deviceNameTokenKey, deviceName },
                { _endpointNameTokenKey, inboundEndpointName }
            };
            var notificationRequest = new Generated.AdrBaseService.SetNotificationPreferenceForDeviceUpdatesRequestPayload
            {
                NotificationPreferenceRequest = (Generated.AdrBaseService.NotificationPreference)(int)notificationPreference
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
        }, cancellationToken);
    }

    public async Task<Models.SetNotificationPreferenceForAssetUpdatesResponsePayload> SetNotificationPreferenceForAssetUpdatesAsync(string deviceName, string inboundEndpointName, string assetName, Models.NotificationPreference notificationPreference, TimeSpan? commandTimeout = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(deviceName))
        {
            throw new ArgumentException("Device name cannot be null or empty", nameof(deviceName));
        }

        if (string.IsNullOrWhiteSpace(inboundEndpointName))
        {
            throw new ArgumentException("Inbound endpoint name cannot be null or empty", nameof(inboundEndpointName));
        }

        if (string.IsNullOrWhiteSpace(assetName))
        {
            throw new ArgumentException("Asset name cannot be null or empty", nameof(assetName));
        }

        return await RunWithRetryAsync<Models.SetNotificationPreferenceForAssetUpdatesResponsePayload>(async () =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            ObjectDisposedException.ThrowIf(_disposed, this);

            _observedAssets[$"{deviceName}_{inboundEndpointName}_{assetName}"] = _dummyByte;
            await _adrBaseServiceClient.AssetUpdateEventTelemetryReceiver.StartAsync(cancellationToken);

            Dictionary<string, string> additionalTopicTokenMap = new()
            {
                { _connectorClientIdTokenKey, _connectorClientId },
                { _deviceNameTokenKey, deviceName },
                { _endpointNameTokenKey, inboundEndpointName }
            };
            var notificationRequest = new Generated.AdrBaseService.SetNotificationPreferenceForAssetUpdatesRequestPayload
            {
                NotificationPreferenceRequest = new Generated.AdrBaseService.SetNotificationPreferenceForAssetUpdatesRequestSchema
                {
                    AssetName = assetName,
                    NotificationPreference = (Generated.AdrBaseService.NotificationPreference)(int)notificationPreference
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
        }, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<Device> GetDeviceAsync(string deviceName, string inboundEndpointName, TimeSpan? commandTimeout = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(deviceName))
        {
            throw new ArgumentException("Device name cannot be null or empty", nameof(deviceName));
        }

        if (string.IsNullOrWhiteSpace(inboundEndpointName))
        {
            throw new ArgumentException("Inbound endpoint name cannot be null or empty", nameof(inboundEndpointName));
        }

        return await RunWithRetryAsync<Device>(async () =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            ObjectDisposedException.ThrowIf(_disposed, this);

            Dictionary<string, string> additionalTopicTokenMap = new()
            {
                { _connectorClientIdTokenKey, _connectorClientId },
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
        }, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<DeviceStatus> UpdateDeviceStatusAsync(string deviceName, string inboundEndpointName,
        DeviceStatus status, TimeSpan? commandTimeout = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(deviceName))
        {
            throw new ArgumentException("Device name cannot be null or empty", nameof(deviceName));
        }

        if (string.IsNullOrWhiteSpace(inboundEndpointName))
        {
            throw new ArgumentException("Inbound endpoint name cannot be null or empty", nameof(inboundEndpointName));
        }

        return await RunWithRetryAsync<DeviceStatus>(async () =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            ObjectDisposedException.ThrowIf(_disposed, this);

            Dictionary<string, string> additionalTopicTokenMap = new()
            {
                { _connectorClientIdTokenKey, _connectorClientId },
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
        }, cancellationToken);
    }

    public async Task<DeviceStatus> GetDeviceStatusAsync(string deviceName, string inboundEndpointName, TimeSpan? commandTimeout = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(deviceName))
        {
            throw new ArgumentException("Device name cannot be null or empty", nameof(deviceName));
        }

        if (string.IsNullOrWhiteSpace(inboundEndpointName))
        {
            throw new ArgumentException("Inbound endpoint name cannot be null or empty", nameof(inboundEndpointName));
        }

        return await RunWithRetryAsync<DeviceStatus>(async () =>
        {

            cancellationToken.ThrowIfCancellationRequested();
            ObjectDisposedException.ThrowIf(_disposed, this);

            Dictionary<string, string> additionalTopicTokenMap = new()
            {
                { _connectorClientIdTokenKey, _connectorClientId },
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
        }, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<Asset> GetAssetAsync(string deviceName, string inboundEndpointName, string assetName, TimeSpan? commandTimeout = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(deviceName))
        {
            throw new ArgumentException("Device name cannot be null or empty", nameof(deviceName));
        }

        if (string.IsNullOrWhiteSpace(inboundEndpointName))
        {
            throw new ArgumentException("Inbound endpoint name cannot be null or empty", nameof(inboundEndpointName));
        }

        if (string.IsNullOrWhiteSpace(assetName))
        {
            throw new ArgumentException("Asset name cannot be null or empty", nameof(assetName));
        }

        return await RunWithRetryAsync<Asset>(async () =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            ObjectDisposedException.ThrowIf(_disposed, this);

            Dictionary<string, string> additionalTopicTokenMap = new()
            {
                { _connectorClientIdTokenKey, _connectorClientId },
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
            catch (Generated.DeviceDiscoveryService.AkriServiceErrorException exception)
            {
                var error = exception.AkriServiceError.ToModel();
                throw new Models.AkriServiceErrorException(error);
            }

        }, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<AssetStatus> UpdateAssetStatusAsync(string deviceName, string inboundEndpointName, UpdateAssetStatusRequest request,
        TimeSpan? commandTimeout = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(deviceName))
        {
            throw new ArgumentException("Device name cannot be null or empty", nameof(deviceName));
        }

        if (string.IsNullOrWhiteSpace(inboundEndpointName))
        {
            throw new ArgumentException("Inbound endpoint name cannot be null or empty", nameof(inboundEndpointName));
        }

        return await RunWithRetryAsync<AssetStatus>(async () =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            ObjectDisposedException.ThrowIf(_disposed, this);

            Dictionary<string, string> additionalTopicTokenMap = new()
            {
                { _connectorClientIdTokenKey, _connectorClientId },
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

        }, cancellationToken);

    }

    /// <inheritdoc />
    public async Task<AssetStatus> GetAssetStatusAsync(string deviceName, string inboundEndpointName, string assetName, TimeSpan? commandTimeout = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(deviceName))
        {
            throw new ArgumentException("Device name cannot be null or empty", nameof(deviceName));
        }

        if (string.IsNullOrWhiteSpace(inboundEndpointName))
        {
            throw new ArgumentException("Inbound endpoint name cannot be null or empty", nameof(inboundEndpointName));
        }

        if (string.IsNullOrWhiteSpace(assetName))
        {
            throw new ArgumentException("Asset name cannot be null or empty", nameof(assetName));
        }

        return await RunWithRetryAsync<AssetStatus>(async () =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            ObjectDisposedException.ThrowIf(_disposed, this);

            Dictionary<string, string> additionalTopicTokenMap = new()
            {
                { _connectorClientIdTokenKey, _connectorClientId },
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
        }, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<Models.CreateOrUpdateDiscoveredAssetResponsePayload> CreateOrUpdateDiscoveredAssetAsync(string deviceName, string inboundEndpointName, CreateOrUpdateDiscoveredAssetRequest request,
        TimeSpan? commandTimeout = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(deviceName))
        {
            throw new ArgumentException("Device name cannot be null or empty", nameof(deviceName));
        }

        if (string.IsNullOrWhiteSpace(inboundEndpointName))
        {
            throw new ArgumentException("Inbound endpoint name cannot be null or empty", nameof(inboundEndpointName));
        }

        return await RunWithRetryAsync<Models.CreateOrUpdateDiscoveredAssetResponsePayload>(async () =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            ObjectDisposedException.ThrowIf(_disposed, this);

            Dictionary<string, string> additionalTopicTokenMap = new()
            {
                { _connectorClientIdTokenKey, _connectorClientId },
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
        }, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<Models.CreateOrUpdateDiscoveredDeviceResponsePayload> CreateOrUpdateDiscoveredDeviceAsync(Models.CreateOrUpdateDiscoveredDeviceRequestSchema request,
        string inboundEndpointType, TimeSpan? commandTimeout = null, CancellationToken cancellationToken = default)
    {
        return await RunWithRetryAsync<Models.CreateOrUpdateDiscoveredDeviceResponsePayload>(async () =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            ObjectDisposedException.ThrowIf(_disposed, this);

            Dictionary<string, string> additionalTopicTokenMap = new()
            {
                { _discoveryClientIdTokenKey, _connectorClientId },
                { _inboundEpTypeTokenKey, inboundEndpointType },
            };

            var req = new Generated.DeviceDiscoveryService.CreateOrUpdateDiscoveredDeviceRequestPayload
            {
                DiscoveredDeviceRequest = new Generated.DeviceDiscoveryService.CreateOrUpdateDiscoveredDeviceRequestSchema
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
            catch (Generated.DeviceDiscoveryService.AkriServiceErrorException exception)
            {
                var error = exception.AkriServiceError.ToModel();
                throw new Models.AkriServiceErrorException(error);
            }
        }, cancellationToken);
    }

    /// <inheritdoc />
    public event Func<string, string, Device, Task>? OnReceiveDeviceUpdateEventTelemetry
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

    public async Task<TResult> RunWithRetryAsync<TResult>(Func<Task<TResult>> taskFunc, CancellationToken cancellationToken)
    {
        uint currentRetryCount = 0;
        Exception? lastThrownException = null;
        TimeSpan retryDelay = TimeSpan.FromSeconds(0);
        do
        {
            await Task.Delay(retryDelay, cancellationToken);

            try
            {
                return await taskFunc.Invoke();
            }
            catch (Exception e) when (e is not OperationCanceledException && e is not ObjectDisposedException) // Operation canceled exception should end retry logic immediately
            {
                lastThrownException = e;
            }
        } while (_retryPolicy.ShouldRetry(++currentRetryCount, lastThrownException, out retryDelay));

        if (currentRetryCount == 1)
        {
            // if no retry policy is configured, the above loop will exit with retryCount == 1. No need to throw a RetryExpiredException
            // around this exception since no retry was applied.
            throw lastThrownException;
        }
        else
        {
            throw new RetryExpiredException("Retry expired while attempting the operation. Last known exception is the inner exception.", lastThrownException);
        }
    }
}
