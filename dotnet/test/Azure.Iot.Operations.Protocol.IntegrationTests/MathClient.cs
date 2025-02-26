// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Iot.Operations.Protocol.IntegrationTests;

public class MathClient : TestEnvoys.Math.Math.Client
{
    public MathClient(ApplicationContext applicationContext, IMqttPubSubClient mqttClient) : base(applicationContext, mqttClient)
    {
    }
}
