// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Protocol;

namespace Azure.Iot.Operations.Services.EdgeRegistry;

/// <summary>
/// Instantiable concrete subclass of the generated abstract core xRegistry client
/// (<c>Generated.EdgeRegistry.Client</c>). The generated client is abstract only to require a
/// named subclass; this stub adds no behavior and exists so the core client can be constructed
/// and composed by the hand-written EdgeRegistry SDK client.
/// </summary>
internal class CoreClientStub(ApplicationContext applicationContext, IMqttPubSubClient mqttClient)
    : Generated.EdgeRegistry.Client(applicationContext, mqttClient)
{
}
