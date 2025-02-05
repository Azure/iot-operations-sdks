// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Mqtt.Session;

namespace SampleClient;

internal class MathClient(MqttSessionClient mqttClient) : TestEnvoys.Math.Math.Client(mqttClient)
{
}
