using MQTTnet.Client;
using MQTTnet;
using Azure.Iot.Operations.Mqtt;

namespace Azure.Iot.Operations.Protocol.IntegrationTests;

public  class CommandExecutorInitializationTests
{
    [Fact]
    public async Task ExecutorCanBeInitializedWithoutAValidConnection()
    {
        MQTTnet.Client.IMqttClient mqttClient = new MqttFactory().CreateMqttClient();
        await using var orderedAckClient = new OrderedAckMqttClient(mqttClient);
        GreeterService greeterService = new(orderedAckClient);
    }
}
