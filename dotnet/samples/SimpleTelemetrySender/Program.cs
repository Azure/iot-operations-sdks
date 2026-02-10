using MQTTnet;

internal class Program
{
    private static async Task Main()
    {
        MqttApplicationMessage msg1 =
        new MqttApplicationMessageBuilder()
            .WithTopic("timtay/requestTopic")
            .WithResponseTopic("timtay/responseTopic")
            .WithQualityOfServiceLevel(MQTTnet.Protocol.MqttQualityOfServiceLevel.AtLeastOnce)
            .WithMessageExpiryInterval(20)
            .Build();

        MqttApplicationMessage msg2 =
            new MqttApplicationMessageBuilder()
                .WithTopic("timtay/responseTopic")
                .WithQualityOfServiceLevel(MQTTnet.Protocol.MqttQualityOfServiceLevel.AtLeastOnce)
                .WithMessageExpiryInterval(20)
                .Build();

        using var mqttClient1 = new MqttClientFactory().CreateMqttClient();
        using var mqttClient2 = new MqttClientFactory().CreateMqttClient();
        var mqttClientOptions1 = new MqttClientOptionsBuilder().WithTcpServer("localhost", 1883).WithClientId(Guid.NewGuid().ToString()).Build();
        var mqttClientOptions2 = new MqttClientOptionsBuilder().WithTcpServer("localhost", 1883).WithClientId(Guid.NewGuid().ToString()).Build();

        await mqttClient1.ConnectAsync(mqttClientOptions1);
        await mqttClient2.ConnectAsync(mqttClientOptions2);
        Console.WriteLine("Connected");

        TaskCompletionSource mqttClient1ReceivedMessage = new();
        mqttClient1.ApplicationMessageReceivedAsync += (args) =>
        {
            args.AutoAcknowledge = true;
            mqttClient1ReceivedMessage.TrySetResult();
            return Task.CompletedTask;
        };

        TaskCompletionSource mqttClient2ReceivedMessage = new();
        mqttClient2.ApplicationMessageReceivedAsync += async (args) =>
        {
            args.AutoAcknowledge = false;
            mqttClient2ReceivedMessage.TrySetResult();
            await args.AcknowledgeAsync(CancellationToken.None);
        };

        await mqttClient1.SubscribeAsync(
            new MqttClientSubscribeOptionsBuilder().WithTopicFilter(
                new MqttTopicFilterBuilder()
                .WithTopic("timtay/responseTopic")
                .WithAtLeastOnceQoS()).Build());

        await mqttClient2.SubscribeAsync(
            new MqttClientSubscribeOptionsBuilder().WithTopicFilter(
                new MqttTopicFilterBuilder()
                .WithTopic("timtay/requestTopic")
                .WithAtLeastOnceQoS()).Build());

        while (true)
        {
            await Task.Delay(TimeSpan.FromSeconds(1));

            DateTime before = DateTime.UtcNow;
            await mqttClient1.PublishAsync(msg1);
            await mqttClient2ReceivedMessage.Task;
            await mqttClient2.PublishAsync(msg2);
            await mqttClient1ReceivedMessage.Task;
            mqttClient1ReceivedMessage = new();
            mqttClient2ReceivedMessage = new();
            DateTime after = DateTime.UtcNow;
            var diff = after.Subtract(before);
            Console.WriteLine("DIFF! " + diff.TotalMilliseconds);
            Console.WriteLine();
        }
    }
}
