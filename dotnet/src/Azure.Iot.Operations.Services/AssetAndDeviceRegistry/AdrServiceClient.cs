// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Protocol;
using Azure.Iot.Operations.Protocol.Telemetry;
using Azure.Iot.Operations.Services.AssetAndDeviceRegistry.AdrBaseService;
using Azure.Iot.Operations.Services.AssetAndDeviceRegistry.AepTypeService;

namespace Azure.Iot.Operations.Services.AssetAndDeviceRegistry;

public class AdrServiceClient(ApplicationContext applicationContext, IMqttPubSubClient mqttClient) : IAdrServiceClient
{
    private readonly IMqttPubSubClient _mqttClient = mqttClient;
    private readonly ApplicationContext _applicationContext = applicationContext;

    private readonly AdrBaseServiceClientStub _adrClient = new(applicationContext, mqttClient);
    private readonly AepTypeServiceClientStub _aepTypeClient = new(applicationContext, mqttClient);

    private bool _disposed;
    private static readonly TimeSpan _defaultTimeout = TimeSpan.FromSeconds(10);

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        await _adrClient.DisposeAsync()
            .ConfigureAwait(false);
        await _aepTypeClient.DisposeAsync()
            .ConfigureAwait(false);

        GC.SuppressFinalize(this);
        _disposed = true;
    }

    public async Task<AssetEndpointProfile?> GetAssetEndpointProfileAsync(
        TimeSpan? commandTimeout = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ObjectDisposedException.ThrowIf(_disposed, this);

        return (await _adrClient.GetAssetEndpointProfileAsync(
            null,
            null,
            commandTimeout ?? _defaultTimeout,
            cancellationToken)).AssetEndpointProfile;
    }

    public async Task<Asset?> GetAssetAsync(
        string assetName,
        TimeSpan? commandTimeout = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ObjectDisposedException.ThrowIf(_disposed, this);

        return (await _adrClient.GetAssetAsync(
            new GetAssetRequestPayload
            {
                AssetName = assetName
            },
            null,
            null,
            commandTimeout ?? _defaultTimeout,
            cancellationToken)).Asset;
    }

    public async Task<AssetEndpointProfile?> UpdateAssetEndpointProfileStatusAsync(
        List<Error> errors,
        TimeSpan? commandTimeout = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ObjectDisposedException.ThrowIf(_disposed, this);

        return (await _adrClient.UpdateAssetEndpointProfileStatusAsync(
            new UpdateAssetEndpointProfileStatusRequestPayload
            {
                AssetEndpointProfileStatusUpdate = new AssetEndpointProfileStatus
                {
                    Errors = errors
                }
            },
            null,
            null,
            commandTimeout ?? _defaultTimeout,
            cancellationToken)).UpdatedAssetEndpointProfile;
    }

    public async Task<Asset?> UpdateAssetStatusAsync(
        string assetName,
        AssetStatus assetStatus,
        TimeSpan? commandTimeout = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ObjectDisposedException.ThrowIf(_disposed, this);

        return (await _adrClient.UpdateAssetStatusAsync(
            new UpdateAssetStatusRequestPayload
            {
                AssetStatusUpdate = new()
                {
                    AssetName = assetName,
                    AssetStatus = assetStatus
                }
            },
            null,
            null,
            commandTimeout ?? _defaultTimeout,
            cancellationToken)).UpdatedAsset;
    }

    public async Task<NotificationResponse?> NotifyOnAssetEndpointProfileUpdateAsync(
        NotificationMessageType notificationRequest,
        TimeSpan? commandTimeout = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ObjectDisposedException.ThrowIf(_disposed, this);

        return (await _adrClient.NotifyOnAssetEndpointProfileUpdateAsync(
            new NotifyOnAssetEndpointProfileUpdateRequestPayload
            {
                NotificationRequest = notificationRequest
            },
            null,
            null,
            commandTimeout ?? _defaultTimeout,
            cancellationToken)).NotificationResponse;
    }

    public async Task<NotificationResponse?> NotifyOnAssetUpdateAsync(
        string assetName,
        NotificationMessageType notificationMessageType,
        TimeSpan? commandTimeout = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ObjectDisposedException.ThrowIf(_disposed, this);

        return (await _adrClient.NotifyOnAssetUpdateAsync(
            new NotifyOnAssetUpdateRequestPayload
            {
                NotificationRequest = new()
                {
                    AssetName = assetName,
                    NotificationMessageType = notificationMessageType
                }
            },
            null,
            null,
            commandTimeout ?? _defaultTimeout,
            cancellationToken)).NotificationResponse;
    }

    public async Task<CreateDetectedAssetResponseSchema?> CreateDetectedAssetAsync(
        string assetEndpointProfileRef,
        string assetName,
        List<DetectedAssetDatasetSchemaElementSchema> datasets,
        string defaultDatasetsConfiguration,
        string defaultEventsConfiguration,
        Topic? defaultTopic,
        string documentationUri,
        List<DetectedAssetEventSchemaElementSchema> events,
        string hardwareRevision,
        string manufacturer,
        string manufacturerUri,
        string model,
        string productCode,
        string serialNumber,
        string softwareRevision,
        TimeSpan? commandTimeout = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ObjectDisposedException.ThrowIf(_disposed, this);

        return (await _adrClient.CreateDetectedAssetAsync(
            new CreateDetectedAssetRequestPayload
            {
                DetectedAsset = new()
                {
                    AssetEndpointProfileRef = assetEndpointProfileRef,
                    AssetName = assetName,
                    Datasets = datasets,
                    DefaultDatasetsConfiguration = defaultDatasetsConfiguration,
                    DefaultEventsConfiguration = defaultEventsConfiguration,
                    DefaultTopic = defaultTopic,
                    DocumentationUri = documentationUri,
                    Events = events,
                    HardwareRevision = hardwareRevision,
                    Manufacturer = manufacturer,
                    ManufacturerUri = manufacturerUri,
                    Model = model,
                    ProductCode = productCode,
                    SerialNumber = serialNumber,
                    SoftwareRevision = softwareRevision
                }
            },
            null,
            null,
            commandTimeout ?? _defaultTimeout,
            cancellationToken)).CreateDetectedAssetResponse;
    }

    public event Func<string, AssetEndpointProfileUpdateEventTelemetry, IncomingTelemetryMetadata, Task>? OnReceiveAssetEndpointProfileUpdateTelemetry
    {
        add => _adrClient.OnReceiveAssetEndpointProfileUpdateTelemetry += value;
        remove => _adrClient.OnReceiveAssetEndpointProfileUpdateTelemetry -= value;
    }

    public event Func<string, AssetUpdateEventTelemetry, IncomingTelemetryMetadata, Task>? OnReceiveAssetUpdateEventTelemetry
    {
        add => _adrClient.OnReceiveAssetUpdateEventTelemetry += value;
        remove => _adrClient.OnReceiveAssetUpdateEventTelemetry -= value;
    }

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ObjectDisposedException.ThrowIf(_disposed, this);

        await _adrClient.StartAsync(cancellationToken);
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ObjectDisposedException.ThrowIf(_disposed, this);

        await _adrClient.StopAsync(cancellationToken);
    }

    public async Task<CreateDiscoveredAssetEndpointProfileResponseSchema?> CreateDiscoveredAssetEndpointProfileAsync(
        string additionalConfiguration,
        string daepName,
        string endpointProfileType,
        List<SupportedAuthenticationMethodsSchemaElementSchema> supportedAuthenticationMethods,
        string targetAddress,
        TimeSpan? commandTimeout = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ObjectDisposedException.ThrowIf(_disposed, this);

        return (await _aepTypeClient.CreateDiscoveredAssetEndpointProfileAsync(
            new CreateDiscoveredAssetEndpointProfileRequestPayload()
            {
                DiscoveredAssetEndpointProfile = new()
                {
                    AdditionalConfiguration = additionalConfiguration,
                    DaepName = daepName,
                    EndpointProfileType = endpointProfileType,
                    SupportedAuthenticationMethods = supportedAuthenticationMethods,
                    TargetAddress = targetAddress
                }
            },
            null,
            null,
            commandTimeout ?? _defaultTimeout,
            cancellationToken)).CreateDiscoveredAssetEndpointProfileResponse;
    }
}
