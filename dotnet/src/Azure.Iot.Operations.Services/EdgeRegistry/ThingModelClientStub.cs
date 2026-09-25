// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Protocol;

namespace Azure.Iot.Operations.Services.EdgeRegistry;

/// <summary>
/// Instantiable concrete subclass of the generated abstract xRegistry Thing Model extension client
/// (<c>Generated.EdgeRegistryThingModelExtensions.Client</c>). The generated client is abstract only
/// to require a named subclass; this stub adds no behavior and exists so the Thing Model extension
/// surface can be composed by the hand-written EdgeRegistry SDK client.
/// </summary>
internal class ThingModelClientStub(ApplicationContext applicationContext, IMqttPubSubClient mqttClient)
    : Generated.EdgeRegistryThingModelExtensions.Client(applicationContext, mqttClient)
{
}
