// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Protocol;
using Azure.Iot.Operations.Protocol.RPC;
using Azure.Iot.Operations.Protocol.Telemetry;
using Azure.Iot.Operations.Services.AssetAndDeviceRegistry.AdrBaseService;

namespace Azure.Iot.Operations.Services.AssetAndDeviceRegistry;

public class AdrBaseServiceClient(ApplicationContext applicationContext, IMqttPubSubClient mqttClient) : IAdrBaseServiceClient
{
    private readonly AdrBaseServiceClientStub _client = new(applicationContext, mqttClient);

    private bool _disposed;
    private static readonly TimeSpan _defaultTimeout = TimeSpan.FromSeconds(10);

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        await _client.DisposeAsync()
            .ConfigureAwait(false);
        ;
        GC.SuppressFinalize(this);
        _disposed = true;
    }

    public async Task<AssetEndpointProfile?> GetAssetEndpointProfileAsync(
        TimeSpan? commandTimeout = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            ObjectDisposedException.ThrowIf(_disposed, this);

            return (await _client.GetAssetEndpointProfileAsync(
                null,
                null,
                commandTimeout ?? _defaultTimeout,
                cancellationToken)).AssetEndpointProfile;
        }
        catch (AkriMqttException ex) when (ex.Kind == AkriMqttErrorKind.PayloadInvalid)
        {
            // This is likely because the user received a "not found" response payload from the service, but the service is an
            // older version that sends an empty payload instead of the expected "{}" payload.
            return null;
        }
        catch (AkriMqttException e) when (e.Kind == AkriMqttErrorKind.UnknownError)
        {
            // ADR 15 specifies that schema registry clients should still throw a distinct error when the service returns a 422. It also specifies
            // that the protocol layer should no longer recognize 422 as an expected error kind, so assume unknown errors are just 422's
            throw new AdrBaseServiceException("Invocation error returned by ADR base service",
                e.PropertyName,
                e.PropertyValue);
        }
    }

    public async Task<Asset?> GetAssetAsync(
        string assetName,
        TimeSpan? commandTimeout = null,
        CancellationToken cancellationToken = default)
    {
        var request = new GetAssetRequestPayload
        {
            AssetName = assetName
        };

        return await ExecuteClientMethodAsync(
            _client.GetAssetAsync,
            request,
            commandTimeout,
            cancellationToken,
            result => result.Asset);
    }

    public async Task<AssetEndpointProfile?> UpdateAssetEndpointProfileStatusAsync(
        List<Error> errors,
        TimeSpan? commandTimeout = null,
        CancellationToken cancellationToken = default)
    {
        var request = new UpdateAssetEndpointProfileStatusRequestPayload
        {
            AssetEndpointProfileStatusUpdate = new AssetEndpointProfileStatus
            {
                Errors = errors
            }
        };

        return await ExecuteClientMethodAsync(
            _client.UpdateAssetEndpointProfileStatusAsync,
            request,
            commandTimeout,
            cancellationToken,
            result => result.UpdatedAssetEndpointProfile);
    }

    public async Task<Asset?> UpdateAssetStatusAsync(
        string assetName,
        AssetStatus assetStatus,
        TimeSpan? commandTimeout = null,
        CancellationToken cancellationToken = default)
    {
        var request = new UpdateAssetStatusRequestPayload
        {
            AssetStatusUpdate = new()
            {
                AssetName = assetName,
                AssetStatus = assetStatus
            }
        };

        return await ExecuteClientMethodAsync(
            _client.UpdateAssetStatusAsync,
            request,
            commandTimeout,
            cancellationToken,
            result => result.UpdatedAsset);
    }

    public async Task<NotificationResponse?> NotifyOnAssetEndpointProfileUpdateAsync(
        NotificationMessageType notificationRequest,
        TimeSpan? commandTimeout = null,
        CancellationToken cancellationToken = default)
    {
        var request = new NotifyOnAssetEndpointProfileUpdateRequestPayload
        {
            NotificationRequest = notificationRequest
        };

        return await ExecuteClientMethodAsync(
                _client.NotifyOnAssetEndpointProfileUpdateAsync,
                request,
                commandTimeout,
                cancellationToken,
                result => result.NotificationResponse);
    }

    public async Task<NotificationResponse?> NotifyOnAssetUpdateAsync(
        string assetName,
        NotificationMessageType notificationMessageType,
        TimeSpan? commandTimeout = null,
        CancellationToken cancellationToken = default)
    {
        var request = new NotifyOnAssetUpdateRequestPayload
        {
            NotificationRequest = new()
            {
                AssetName = assetName,
                NotificationMessageType = notificationMessageType
            }
        };

        return await ExecuteClientMethodAsync(
            _client.NotifyOnAssetUpdateAsync,
            request,
            commandTimeout,
            cancellationToken,
            result => result.NotificationResponse);
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
        var request = new CreateDetectedAssetRequestPayload
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
        };

        return await ExecuteClientMethodAsync(
            _client.CreateDetectedAssetAsync,
            request,
            commandTimeout,
            cancellationToken,
            result => result.CreateDetectedAssetResponse);
    }

    public event Func<string, AssetEndpointProfileUpdateEventTelemetry, IncomingTelemetryMetadata, Task>? OnReceiveAssetEndpointProfileUpdateTelemetry
    {
        add => _client.OnReceiveAssetEndpointProfileUpdateTelemetry += value;
        remove => _client.OnReceiveAssetEndpointProfileUpdateTelemetry -= value;
    }

    public event Func<string, AssetUpdateEventTelemetry, IncomingTelemetryMetadata, Task>? OnReceiveAssetUpdateEventTelemetry
    {
        add => _client.OnReceiveAssetUpdateEventTelemetry += value;
        remove => _client.OnReceiveAssetUpdateEventTelemetry -= value;
    }

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ObjectDisposedException.ThrowIf(_disposed, this);

        try
        {
            await _client.StartAsync(cancellationToken);
        }
        catch (AkriMqttException e) when (e.Kind == AkriMqttErrorKind.UnknownError)
        {
            throw new AdrBaseServiceException("Invocation error returned by ADR base service", e.PropertyName, e.PropertyValue);
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ObjectDisposedException.ThrowIf(_disposed, this);

        try
        {
            await _client.StopAsync(cancellationToken);
        }
        catch (AkriMqttException e) when (e.Kind == AkriMqttErrorKind.UnknownError)
        {
            throw new AdrBaseServiceException("Invocation error returned by ADR base service", e.PropertyName, e.PropertyValue);
        }
    }

    private async Task<TResult?> ExecuteClientMethodAsync<TRequest, TResponse, TResult>(
        Func<TRequest, CommandRequestMetadata?, Dictionary<string, string>?, TimeSpan?, CancellationToken, RpcCallAsync<TResponse>> clientMethod,
        TRequest request,
        TimeSpan? commandTimeout = null,
        CancellationToken cancellationToken = default,
        Func<TResponse, TResult>? resultSelector = null) where TResponse : class
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            ObjectDisposedException.ThrowIf(_disposed, this);

            var response = await clientMethod(
                request,
                null,
                null,
                commandTimeout ?? _defaultTimeout,
                cancellationToken);

            return resultSelector != null ? resultSelector(response) : (response is TResult r ? r : default);
        }
        catch (AkriMqttException ex) when (ex.Kind == AkriMqttErrorKind.PayloadInvalid)
        {
            // This is likely because the user received a "not found" response payload from the service, but the service is an
            // older version that sends an empty payload instead of the expected "{}" payload.
            return default;
        }
        catch (AkriMqttException e) when (e.Kind == AkriMqttErrorKind.UnknownError)
        {
            // ADR 15 specifies that schema registry clients should still throw a distinct error when the service returns a 422. It also specifies
            // that the protocol layer should no longer recognize 422 as an expected error kind, so assume unknown errors are just 422's
            throw new AdrBaseServiceException("Invocation error returned by ADR base service", e.PropertyName, e.PropertyValue);
        }
    }
}
