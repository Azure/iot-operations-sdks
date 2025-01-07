// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Protocol;
using Azure.Iot.Operations.Mqtt.Session;
using TestEnvoys.dtmi_com_example_Counter__1;

namespace CounterClient;

public class CounterClient(IMqttPubSubClient mqttClient) : Counter.Client(mqttClient)
{
    public static Func<IServiceProvider, CounterClient> Factory = service => new CounterClient(service.GetService<MqttSessionClient>()!);
}
