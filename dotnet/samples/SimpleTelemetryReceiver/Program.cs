using MQTTnet;

internal class Program
{
    private static async Task Main()
    {
        await Task.WhenAll(RunReceiver(), RunSender());
    }

    public static async Task RunReceiver()
    {
        using var mqttClient = new MqttClientFactory().CreateMqttClient();
        var mqttClientOptions = new MqttClientOptionsBuilder().WithTcpServer("localhost", 1883).WithClientId(Guid.NewGuid().ToString()).Build();
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
                    .WithQualityOfServiceLevel(MQTTnet.Protocol.MqttQualityOfServiceLevel.AtLeastOnce)
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
    }

    public static async Task RunSender()
    {
        using var mqttClient = new MqttClientFactory().CreateMqttClient();
        var mqttClientOptions = new MqttClientOptionsBuilder().WithTcpServer("localhost", 1883).WithClientId(Guid.NewGuid().ToString()).Build();
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
                .WithQualityOfServiceLevel(MQTTnet.Protocol.MqttQualityOfServiceLevel.AtLeastOnce)
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
    }
}
