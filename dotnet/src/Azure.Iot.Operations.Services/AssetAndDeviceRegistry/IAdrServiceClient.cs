// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Protocol.Telemetry;
using Azure.Iot.Operations.Services.AssetAndDeviceRegistry.AdrBaseService;
using Azure.Iot.Operations.Services.AssetAndDeviceRegistry.AepTypeService;

namespace Azure.Iot.Operations.Services.AssetAndDeviceRegistry;

public interface IAdrServiceClient : IAsyncDisposable
{
    Task<AssetEndpointProfile?> GetAssetEndpointProfileAsync(
        TimeSpan? commandTimeout = null,
        CancellationToken cancellationToken = default);

    Task<Asset?> GetAssetAsync(
        string assetName,
        TimeSpan? commandTimeout = null,
        CancellationToken cancellationToken = default);

    Task<AssetEndpointProfile?> UpdateAssetEndpointProfileStatusAsync(
        List<Error> errors,
        TimeSpan? commandTimeout = null,
        CancellationToken cancellationToken = default);

    Task<Asset?> UpdateAssetStatusAsync(
        string assetName,
        AssetStatus assetStatus,
        TimeSpan? commandTimeout = null,
        CancellationToken cancellationToken = default);

    Task<NotificationResponse?> NotifyOnAssetEndpointProfileUpdateAsync(
        NotificationMessageType notificationRequest,
        TimeSpan? commandTimeout = null,
        CancellationToken cancellationToken = default);

    Task<NotificationResponse?> NotifyOnAssetUpdateAsync(
        string assetName,
        NotificationMessageType notificationMessageType,
        TimeSpan? commandTimeout = null,
        CancellationToken cancellationToken = default);

    Task<CreateDetectedAssetResponseSchema?> CreateDetectedAssetAsync(
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
        CancellationToken cancellationToken = default);

    Task<CreateDiscoveredAssetEndpointProfileResponseSchema?> CreateDiscoveredAssetEndpointProfileAsync(
        string additionalConfiguration,
        string daepName,
        string endpointProfileType,
        List<SupportedAuthenticationMethodsSchemaElementSchema> supportedAuthenticationMethods,
        string targetAddress,
        TimeSpan? commandTimeout = null,
        CancellationToken cancellationToken = default);

    event Func<string, AssetEndpointProfileUpdateEventTelemetry, IncomingTelemetryMetadata, Task> OnReceiveAssetEndpointProfileUpdateTelemetry;

    event Func<string, AssetUpdateEventTelemetry, IncomingTelemetryMetadata, Task> OnReceiveAssetUpdateEventTelemetry;

    Task StartAsync(CancellationToken cancellationToken = default);
    Task StopAsync(CancellationToken cancellationToken = default);
}

