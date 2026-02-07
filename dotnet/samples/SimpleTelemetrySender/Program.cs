using MQTTnet;

using var mqttClient = new MqttClientFactory().CreateMqttClient();
var mqttClientOptions = new MqttClientOptionsBuilder().WithTcpServer("localhost", 1884).WithClientId(Guid.NewGuid().ToString()).Build();
await mqttClient.ConnectAsync(mqttClientOptions, CancellationToken.None);
Console.WriteLine("Connected to MQTT broker");

TaskCompletionSource responseMessageReceived = new();
mqttClient.ApplicationMessageReceivedAsync += (args) =>
{
    responseMessageReceived.TrySetResult();
    return Task.CompletedTask;
};

await mqttClient.SubscribeAsync(
    new MqttClientSubscribeOptionsBuilder().WithTopicFilter(
        new MqttTopicFilterBuilder()
        .WithTopic("timtay/responseTopic")
        .WithAtLeastOnceQoS()).Build());

MqttApplicationMessage msg =
    new MqttApplicationMessageBuilder()
        .WithTopic("timtay/requestTopic")
        .WithResponseTopic("timtay/responseTopic")
        .Build();

while (true)
{
    //await Task.Delay(TimeSpan.FromSeconds(3));
    await mqttClient.PublishAsync(msg);

    DateTime beforeResponse = DateTime.UtcNow;
    await responseMessageReceived.Task;
    DateTime afterResponse = DateTime.UtcNow;
    responseMessageReceived = new();

    var diff = afterResponse.Subtract(beforeResponse);
    Console.WriteLine("DIFF! " + diff.TotalMilliseconds);
    Console.WriteLine();
}

