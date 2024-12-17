// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Mqtt.Session;

namespace SampleClient;

internal class MathClient(MqttSessionClient mqttClient) : TestEnvoys.dtmi_rpc_samples_math__1.Math.Client(mqttClient)
{
}
