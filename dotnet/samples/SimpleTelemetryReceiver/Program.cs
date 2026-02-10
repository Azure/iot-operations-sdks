using MQTTnet;

internal class Program
{
    private static async Task Main()
    {
        using var mqttClient1 = new MqttClientFactory().CreateMqttClient();
        var mqttClientOptions1 = new MqttClientOptionsBuilder().WithTcpServer("localhost", 1883).WithClientId(Guid.NewGuid().ToString()).Build();
        await mqttClient1.ConnectAsync(mqttClientOptions1, CancellationToken.None);

        using var mqttClient2 = new MqttClientFactory().CreateMqttClient();
        var mqttClientOptions2 = new MqttClientOptionsBuilder().WithTcpServer("localhost", 1883).WithClientId(Guid.NewGuid().ToString()).Build();
        await mqttClient2.ConnectAsync(mqttClientOptions2, CancellationToken.None);
        Console.WriteLine("Connected to MQTT broker");


        TaskCompletionSource responseMessageReceived = new();
        mqttClient1.ApplicationMessageReceivedAsync += (args) =>
        {
            responseMessageReceived.TrySetResult();
            return Task.CompletedTask;
        };

        await mqttClient1.SubscribeAsync(
            new MqttClientSubscribeOptionsBuilder().WithTopicFilter(
                new MqttTopicFilterBuilder()
                .WithTopic("timtay/responseTopic")
                .WithAtLeastOnceQoS()).Build());

        MqttApplicationMessage msg1 =
            new MqttApplicationMessageBuilder()
                .WithTopic("timtay/requestTopic")
                .WithResponseTopic("timtay/responseTopic")
                .WithQualityOfServiceLevel(MQTTnet.Protocol.MqttQualityOfServiceLevel.AtLeastOnce)
                .Build();

        mqttClient2.ApplicationMessageReceivedAsync += async (args) =>
        {
            Console.WriteLine("Handling a request");
            Console.WriteLine();
            args.AutoAcknowledge = false;
            MqttApplicationMessage msg2 =
                new MqttApplicationMessageBuilder()
                    .WithTopic(args.ApplicationMessage.ResponseTopic)
                    .WithQualityOfServiceLevel(MQTTnet.Protocol.MqttQualityOfServiceLevel.AtLeastOnce)
                    .Build();

            await mqttClient2.PublishAsync(msg2);
            await args.AcknowledgeAsync(CancellationToken.None);
        };

        await mqttClient2.SubscribeAsync(
            new MqttClientSubscribeOptionsBuilder().WithTopicFilter(
                new MqttTopicFilterBuilder()
                .WithTopic("timtay/requestTopic")
                .WithAtLeastOnceQoS()).Build());

        while (true)
        {
            await Task.Delay(TimeSpan.FromSeconds(1));
            await mqttClient1.PublishAsync(msg1);

            DateTime beforeResponse = DateTime.UtcNow;
            await responseMessageReceived.Task;
            DateTime afterResponse = DateTime.UtcNow;
            responseMessageReceived = new();

            var diff = afterResponse.Subtract(beforeResponse);
            Console.WriteLine("DIFF! " + diff.TotalMilliseconds);
            Console.WriteLine();
        }       
    }
}
