// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Protocol;

namespace Azure.Iot.Operations.Services.SchemaRegistry;

internal class SchemaRegistryClientStub(IMqttPubSubClient mqttClient) : dtmi_ms_adr_SchemaRegistry__1.SchemaRegistry.Client(mqttClient)
{


}


