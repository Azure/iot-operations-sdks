// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Protocol;

namespace Azure.Iot.Operations.Services.EdgeRegistry;

internal class EdgeRegistryClientStub(ApplicationContext applicationContext, IMqttPubSubClient mqttClient)
    : Generated.EdgeRegistry.Client(applicationContext, mqttClient)
{
}
