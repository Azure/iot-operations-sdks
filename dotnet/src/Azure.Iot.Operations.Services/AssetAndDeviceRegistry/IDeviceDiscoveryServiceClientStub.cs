// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Protocol.RPC;
using Azure.Iot.Operations.Services.AssetAndDeviceRegistry.DeviceDiscoveryService;
using static Azure.Iot.Operations.Services.AssetAndDeviceRegistry.DeviceDiscoveryService.DeviceDiscoveryService;

namespace Azure.Iot.Operations.Services.AssetAndDeviceRegistry
{
    internal interface IDeviceDiscoveryServiceClientStub : IAsyncDisposable
    {
        CreateOrUpdateDiscoveredDeviceCommandInvoker CreateOrUpdateDiscoveredDeviceCommandInvoker { get; }

        RpcCallAsync<CreateOrUpdateDiscoveredDeviceResponsePayload> CreateOrUpdateDiscoveredDeviceAsync(CreateOrUpdateDiscoveredDeviceRequestPayload request, CommandRequestMetadata? requestMetadata = null, Dictionary<string, string>? additionalTopicTokenMap = null, TimeSpan? commandTimeout = default, CancellationToken cancellationToken = default);
    }
}
