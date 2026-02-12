// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Diagnostics;
using Azure.Iot.Operations.Protocol;
using Azure.Iot.Operations.Protocol.Telemetry;
using Azure.Iot.Operations.Services.AssetAndDeviceRegistry.AdrBaseService;
using Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Generated;
using Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models;

namespace Azure.Iot.Operations.Services.AssetAndDeviceRegistry;

internal class AdrBaseServiceClientStub(ApplicationContext applicationContext, IMqttPubSubClient mqttClient, Dictionary<string, string>? topicTokenMap = null)
    : Generated.AdrBaseService.Client(applicationContext, mqttClient, topicTokenMap), IAdrBaseServiceClientStub
{
    public event Func<string, string, Models.Device, Task>? OnReceiveDeviceUpdateEventTelemetry;
    public event Func<string, Models.Asset, Task>? OnReceiveAssetUpdateEventTelemetry;

    private const string deviceNameTopicToken = "ex:deviceName";
    private const string inboundEndpointNameTopicToken = "ex:inboundEndpointName";

    public override async Task ReceiveTelemetry(string senderId, DeviceUpdateEventTelemetry telemetry, IncomingTelemetryMetadata metadata)
    {
        if (!metadata.TopicTokens.TryGetValue(deviceNameTopicToken, out string? deviceName))
        {
            Trace.TraceWarning("Received a device update event that was missing the expected device name topic token. Ignoring it.");
            return;
        }

        if (!metadata.TopicTokens.TryGetValue(inboundEndpointNameTopicToken, out string? inboundEndpointName))
        {
            Trace.TraceWarning("Received a device update event that was missing the expected device name topic token. Ignoring it.");
            return;
        }

        Models.Device device = telemetry.DeviceUpdateEvent.Device.ToModel();

        if (OnReceiveDeviceUpdateEventTelemetry != null)
        {
            await OnReceiveDeviceUpdateEventTelemetry.Invoke(deviceName, inboundEndpointName, device);
        }
    }

    public override async Task ReceiveTelemetry(string senderId, AssetUpdateEventTelemetry telemetry, IncomingTelemetryMetadata metadata)
    {
        string assetName = telemetry.AssetUpdateEvent.AssetName;
        Models.Asset asset = telemetry.AssetUpdateEvent.Asset.ToModel();

        if (OnReceiveAssetUpdateEventTelemetry != null)
        {
            await OnReceiveAssetUpdateEventTelemetry.Invoke(assetName, asset);
        }
    }
}
