// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using TestEnvoys.dtmi_com_example_Counter__1;

namespace Azure.Iot.Operations.Protocol.IntegrationTests;

public class CounterClient(IMqttPubSubClient mqttClient) : Counter.Client(mqttClient)
{
}
