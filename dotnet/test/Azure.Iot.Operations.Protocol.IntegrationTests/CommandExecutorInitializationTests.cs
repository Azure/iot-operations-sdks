// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using MQTTnet;
using Azure.Iot.Operations.Mqtt;

namespace Azure.Iot.Operations.Protocol.IntegrationTests;

public  class CommandExecutorInitializationTests
{
    [Fact]
    public async Task ExecutorCanBeInitializedWithoutAValidConnection()
    {
        MQTTnet.IMqttClient mqttClient = new MqttClientFactory().CreateMqttClient();
        await using var orderedAckClient = new OrderedAckMqttClient(mqttClient);
        ApplicationContext applicationContext = new ApplicationContext();
        GreeterService greeterService = new(applicationContext, orderedAckClient);
    }
}
