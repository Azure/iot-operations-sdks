// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Iot.Operations.Protocol.IntegrationTests;

public class MathClient : TestEnvoys.dtmi_rpc_samples_math__1.Math.Client
{
    public MathClient(IMqttPubSubClient mqttClient) : base(mqttClient)
    {
    }
}
