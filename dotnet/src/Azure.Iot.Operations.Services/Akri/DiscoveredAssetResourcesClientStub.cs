// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Protocol;

namespace Azure.Iot.Operations.Services.Akri;

internal class DiscoveredAssetResourcesClientStub(IMqttPubSubClient mqttClient) : DiscoveredAssetResources.DiscoveredAssetResources.Client(mqttClient)
{
}