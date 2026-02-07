using MQTTnet;

using var mqttClient = new MqttClientFactory().CreateMqttClient();
var mqttClientOptions = new MqttClientOptionsBuilder().WithTcpServer("localhost", 1883).Build();
await mqttClient.ConnectAsync(mqttClientOptions, CancellationToken.None);
Console.WriteLine("Connected to MQTT broker");


mqttClient.ApplicationMessageReceivedAsync += async (args) =>
{
    Console.WriteLine("Handling a request");
    Console.WriteLine();
    args.AutoAcknowledge = false;
    MqttApplicationMessage msg =
        new MqttApplicationMessageBuilder()
            .WithTopic(args.ApplicationMessage.ResponseTopic)
            .Build();

    await mqttClient.PublishAsync(msg);
    await args.AcknowledgeAsync(CancellationToken.None);
};

await mqttClient.SubscribeAsync(
    new MqttClientSubscribeOptionsBuilder().WithTopicFilter(
        new MqttTopicFilterBuilder()
        .WithTopic("timtay/requestTopic")
        .WithAtLeastOnceQoS()).Build());

await Task.Delay(-1);
