// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Protocol;

namespace Azure.Iot.Operations.Services.EdgeRegistry;

internal class EdgeRegistryThingDescriptionExtensionsClientStub(ApplicationContext applicationContext, IMqttPubSubClient mqttClient)
    : Generated.EdgeRegistryThingDescriptionExtensions.Client(applicationContext, mqttClient)
{
}
