﻿// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Protocol;
using Azure.Iot.Operations.Protocol.Telemetry;
using Azure.Iot.Operations.Services.AssetAndDeviceRegistry.AdrBaseService;
using Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models;

namespace Azure.Iot.Operations.Services.AssetAndDeviceRegistry;

internal class AssetServiceClientStub(ApplicationContext applicationContext, IMqttPubSubClient mqttClient, Dictionary<string, string>? topicTokenMap = null)
    : AdrBaseService.AdrBaseService.Client(applicationContext, mqttClient, topicTokenMap)
{
    internal event Func<string, Models.Device?, Task>? OnReceiveAssetEndpointProfileUpdateTelemetry;
    internal event Func<string, Models.Asset?, Task>? OnReceiveAssetUpdateEventTelemetry;

    // public override async Task ReceiveTelemetry(string senderId, AssetEndpointProfileUpdateEventTelemetry telemetry, IncomingTelemetryMetadata metadata)
    // {
    //     var aepName = telemetry.AssetEndpointProfileUpdateEvent.AssetEndpointProfile?.Name ?? string.Empty;
    //     Models.AssetEndpointProfile? assetEndpointProfile = telemetry.AssetEndpointProfileUpdateEvent.AssetEndpointProfile?.ToModel();
    //
    //     if (OnReceiveAssetEndpointProfileUpdateTelemetry != null)
    //     {
    //         await OnReceiveAssetEndpointProfileUpdateTelemetry.Invoke(aepName, assetEndpointProfile);
    //     }
    // }

    public override async Task ReceiveTelemetry(string senderId, DeviceUpdateEventTelemetry telemetry, IncomingTelemetryMetadata metadata)
    {
        Models.Device? device = telemetry.DeviceUpdateEvent.Device?.ToModel();
    }

    public override async Task ReceiveTelemetry(string senderId, AssetUpdateEventTelemetry telemetry, IncomingTelemetryMetadata metadata)
    {
        string assetName = telemetry.AssetUpdateEvent.AssetName!;
        Models.Asset? asset = telemetry.AssetUpdateEvent.Asset?.ToModel();

        if (OnReceiveAssetUpdateEventTelemetry != null)
        {
            await OnReceiveAssetUpdateEventTelemetry.Invoke(assetName, asset);
        }
    }
}
