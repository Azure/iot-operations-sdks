// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Protocol;
using Azure.Iot.Operations.Protocol.Telemetry;
using Azure.Iot.Operations.Services.AssetAndDeviceRegistry.AdrBaseService;

namespace Azure.Iot.Operations.Services.AssetAndDeviceRegistry;

internal class AdrBaseServiceClientStub(ApplicationContext applicationContext, IMqttPubSubClient mqttClient, Dictionary<string, string>? topicTokenMap = null)
    : AdrBaseService.AdrBaseService.Client(applicationContext, mqttClient, topicTokenMap)
{
    public event Func<string, AssetEndpointProfileUpdateEventTelemetry, IncomingTelemetryMetadata, Task>? OnReceiveAssetEndpointProfileUpdateTelemetry;
    public event Func<string, AssetUpdateEventTelemetry, IncomingTelemetryMetadata, Task>? OnReceiveAssetUpdateEventTelemetry;

    public override async Task ReceiveTelemetry(string senderId, AssetEndpointProfileUpdateEventTelemetry telemetry, IncomingTelemetryMetadata metadata)
    {
        if (OnReceiveAssetEndpointProfileUpdateTelemetry != null)
        {
            await OnReceiveAssetEndpointProfileUpdateTelemetry.Invoke(senderId, telemetry, metadata);
        }
    }

    public override async Task ReceiveTelemetry(string senderId, AssetUpdateEventTelemetry telemetry, IncomingTelemetryMetadata metadata)
    {
        if (OnReceiveAssetUpdateEventTelemetry != null)
        {
            await OnReceiveAssetUpdateEventTelemetry.Invoke(senderId, telemetry, metadata);
        }
    }
}
