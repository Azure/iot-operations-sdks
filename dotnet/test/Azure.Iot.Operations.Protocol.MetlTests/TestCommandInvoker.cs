// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Iot.Operations.Protocol.MetlTests
{
    using Azure.Iot.Operations.Protocol;
    using Azure.Iot.Operations.Protocol.RPC;

    public class TestCommandInvoker : CommandInvoker<string, string>
    {
        internal TestCommandInvoker(IMqttPubSubClient mqttClient, string commandName, IPayloadSerializer payloadSerializer)
            : base(mqttClient, commandName, payloadSerializer)
        {
        }
    }
}
