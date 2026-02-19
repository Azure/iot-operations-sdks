// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Mqtt.Session;
using Azure.Iot.Operations.Protocol;

namespace SampleClient;

internal class MathClient(ApplicationContext applicationContext, MqttSessionClient mqttClient) : TestEnvoys.Math.Math.Client(applicationContext, mqttClient)
{
}
