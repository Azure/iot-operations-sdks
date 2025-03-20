// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Protocol;
using Azure.Iot.Operations.Protocol.Telemetry;
using Azure.Iot.Operations.Services.AssetAndDeviceRegistry.AdrBaseService;

namespace Azure.Iot.Operations.Services.AssetAndDeviceRegistry;

internal class AssetServiceClientStub(ApplicationContext applicationContext, IMqttPubSubClient mqttClient, Dictionary<string, string>? topicTokenMap = null)
    : AdrBaseService.AdrBaseService.Client(applicationContext, mqttClient, topicTokenMap)
{
    internal event Func<string, AssetEndpointProfile?, Task>? OnReceiveAssetEndpointProfileUpdateTelemetry;
    internal event Func<string, Asset?, Task>? OnReceiveAssetUpdateEventTelemetry;

    public override async Task ReceiveTelemetry(string senderId, AssetEndpointProfileUpdateEventTelemetry telemetry, IncomingTelemetryMetadata metadata)
    {
        var aepName = telemetry.AssetEndpointProfileUpdateEvent.AssetEndpointProfile?.Name ?? string.Empty;
        AssetEndpointProfile? assetEndpointProfile = telemetry.AssetEndpointProfileUpdateEvent.AssetEndpointProfile;

        if (OnReceiveAssetEndpointProfileUpdateTelemetry != null)
        {
            await OnReceiveAssetEndpointProfileUpdateTelemetry.Invoke(aepName, assetEndpointProfile);
        }
    }

    public override async Task ReceiveTelemetry(string senderId, AssetUpdateEventTelemetry telemetry, IncomingTelemetryMetadata metadata)
    {
        string assetName = telemetry.AssetUpdateEvent.AssetName!;
        Asset? asset = telemetry.AssetUpdateEvent.Asset;

        if (OnReceiveAssetUpdateEventTelemetry != null)
        {
            await OnReceiveAssetUpdateEventTelemetry.Invoke(assetName, asset);
        }
    }
}
