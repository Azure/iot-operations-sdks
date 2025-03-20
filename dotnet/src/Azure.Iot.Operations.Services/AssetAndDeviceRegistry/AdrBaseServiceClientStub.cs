// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Protocol;
using Azure.Iot.Operations.Protocol.Telemetry;
using Azure.Iot.Operations.Services.AssetAndDeviceRegistry.AdrBaseService;

namespace Azure.Iot.Operations.Services.AssetAndDeviceRegistry;

internal class AdrBaseServiceClientStub(ApplicationContext applicationContext, IMqttPubSubClient mqttClient, Dictionary<string, string>? topicTokenMap = null)
    : AdrBaseService.AdrBaseService.Client(applicationContext, mqttClient, topicTokenMap)
{
    public override async Task ReceiveTelemetry(string senderId, AssetEndpointProfileUpdateEventTelemetry telemetry, IncomingTelemetryMetadata metadata)
    {
        throw new NotImplementedException();
    }

    public override async Task ReceiveTelemetry(string senderId, AssetUpdateEventTelemetry telemetry, IncomingTelemetryMetadata metadata)
    {
        throw new NotImplementedException();
    }
}
