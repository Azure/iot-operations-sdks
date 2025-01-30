// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Protocol;

namespace Azure.Iot.Operations.Services.SchemaRegistry;

internal class SchemaRegistryClientStub(IMqttPubSubClient mqttClient) : SchemaRegistry.SchemaRegistry.Client(mqttClient)
{


}


