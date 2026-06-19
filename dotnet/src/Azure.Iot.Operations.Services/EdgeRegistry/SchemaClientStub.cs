// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Protocol;

namespace Azure.Iot.Operations.Services.EdgeRegistry;

/// <summary>
/// Instantiable concrete subclass of the generated abstract xRegistry Schema extension client
/// (<c>Generated.EdgeRegistrySchemaExtensions.Client</c>). The generated client is abstract only to
/// require a named subclass; this stub adds no behavior and exists so the schema extension surface
/// can be composed by the hand-written EdgeRegistry SDK client.
/// </summary>
internal class SchemaClientStub(ApplicationContext applicationContext, IMqttPubSubClient mqttClient)
    : Generated.EdgeRegistrySchemaExtensions.Client(applicationContext, mqttClient)
{
}
