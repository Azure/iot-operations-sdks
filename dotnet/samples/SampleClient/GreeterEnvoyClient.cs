// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Mqtt.Session;
using Azure.Iot.Operations.Protocol;
using TestEnvoys.Greeter;

namespace SampleClient;

internal class GreeterEnvoyClient(ApplicationContext applicationContext, MqttSessionClient mqttClient) : GreeterEnvoy.Client(applicationContext, mqttClient)
{
}
