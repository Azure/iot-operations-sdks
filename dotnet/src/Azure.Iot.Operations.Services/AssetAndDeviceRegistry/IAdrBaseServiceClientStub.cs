// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Protocol.RPC;
using Azure.Iot.Operations.Protocol.Telemetry;
using Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Generated;
using static Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Generated.AdrBaseService;

namespace Azure.Iot.Operations.Services.AssetAndDeviceRegistry
{
    // An interface for the code gen'd AdrBaseServiceClientStub so that we can mock it in our unit tests
    internal interface IAdrBaseServiceClientStub : IAsyncDisposable
    {
        event Func<string, string, Models.Device, Task>? OnReceiveDeviceUpdateEventTelemetry;
        event Func<string, Models.Asset, Task>? OnReceiveAssetUpdateEventTelemetry;

        GetDeviceCommandInvoker GetDeviceCommandInvoker { get; }

        GetDeviceStatusCommandInvoker GetDeviceStatusCommandInvoker { get; }

        GetAssetCommandInvoker GetAssetCommandInvoker { get; }

        GetAssetStatusCommandInvoker GetAssetStatusCommandInvoker { get; }

        UpdateDeviceStatusCommandInvoker UpdateDeviceStatusCommandInvoker { get; }

        UpdateAssetStatusCommandInvoker UpdateAssetStatusCommandInvoker { get; }

        SetNotificationPreferenceForDeviceUpdatesCommandInvoker SetNotificationPreferenceForDeviceUpdatesCommandInvoker { get; }

        SetNotificationPreferenceForAssetUpdatesCommandInvoker SetNotificationPreferenceForAssetUpdatesCommandInvoker { get; }

        CreateOrUpdateDiscoveredAssetCommandInvoker CreateOrUpdateDiscoveredAssetCommandInvoker { get; }

        DeviceUpdateEventTelemetryReceiver DeviceUpdateEventTelemetryReceiver { get; }

        AssetUpdateEventTelemetryReceiver AssetUpdateEventTelemetryReceiver { get; }

        Task ReceiveTelemetry(string senderId, DeviceUpdateEventTelemetry telemetry, IncomingTelemetryMetadata metadata);

        Task ReceiveTelemetry(string senderId, AssetUpdateEventTelemetry telemetry, IncomingTelemetryMetadata metadata);

        RpcCallAsync<GetDeviceResponsePayload> GetDeviceAsync(CommandRequestMetadata? requestMetadata = null, Dictionary<string, string>? additionalTopicTokenMap = null, TimeSpan? commandTimeout = default, CancellationToken cancellationToken = default);

        RpcCallAsync<GetDeviceStatusResponsePayload> GetDeviceStatusAsync(CommandRequestMetadata? requestMetadata = null, Dictionary<string, string>? additionalTopicTokenMap = null, TimeSpan? commandTimeout = default, CancellationToken cancellationToken = default);

        RpcCallAsync<GetAssetResponsePayload> GetAssetAsync(GetAssetRequestPayload request, CommandRequestMetadata? requestMetadata = null, Dictionary<string, string>? additionalTopicTokenMap = null, TimeSpan? commandTimeout = default, CancellationToken cancellationToken = default);

        RpcCallAsync<GetAssetStatusResponsePayload> GetAssetStatusAsync(GetAssetStatusRequestPayload request, CommandRequestMetadata? requestMetadata = null, Dictionary<string, string>? additionalTopicTokenMap = null, TimeSpan? commandTimeout = default, CancellationToken cancellationToken = default);

        RpcCallAsync<UpdateDeviceStatusResponsePayload> UpdateDeviceStatusAsync(UpdateDeviceStatusRequestPayload request, CommandRequestMetadata? requestMetadata = null, Dictionary<string, string>? additionalTopicTokenMap = null, TimeSpan? commandTimeout = default, CancellationToken cancellationToken = default);

        RpcCallAsync<UpdateAssetStatusResponsePayload> UpdateAssetStatusAsync(UpdateAssetStatusRequestPayload request, CommandRequestMetadata? requestMetadata = null, Dictionary<string, string>? additionalTopicTokenMap = null, TimeSpan? commandTimeout = default, CancellationToken cancellationToken = default);

        RpcCallAsync<SetNotificationPreferenceForDeviceUpdatesResponsePayload> SetNotificationPreferenceForDeviceUpdatesAsync(SetNotificationPreferenceForDeviceUpdatesRequestPayload request, CommandRequestMetadata? requestMetadata = null, Dictionary<string, string>? additionalTopicTokenMap = null, TimeSpan? commandTimeout = default, CancellationToken cancellationToken = default);

        RpcCallAsync<SetNotificationPreferenceForAssetUpdatesResponsePayload> SetNotificationPreferenceForAssetUpdatesAsync(SetNotificationPreferenceForAssetUpdatesRequestPayload request, CommandRequestMetadata? requestMetadata = null, Dictionary<string, string>? additionalTopicTokenMap = null, TimeSpan? commandTimeout = default, CancellationToken cancellationToken = default);

        RpcCallAsync<CreateOrUpdateDiscoveredAssetResponsePayload> CreateOrUpdateDiscoveredAssetAsync(CreateOrUpdateDiscoveredAssetRequestPayload request, CommandRequestMetadata? requestMetadata = null, Dictionary<string, string>? additionalTopicTokenMap = null, TimeSpan? commandTimeout = default, CancellationToken cancellationToken = default);

        Task StartAsync(CancellationToken cancellationToken = default);

        Task StopAsync(CancellationToken cancellationToken = default);
    }
}
