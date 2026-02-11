using System.Net;
using System.Net.Sockets;
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
        var mqttClientOptions1 = new MqttClientOptionsBuilder().WithEndPoint(new DnsEndPoint("localhost", 1883, AddressFamily.Unspecified)).WithTcpServer(bob).WithClientId(Guid.NewGuid().ToString()).Build();
        var mqttClientOptions2 = new MqttClientOptionsBuilder().WithEndPoint(new DnsEndPoint("localhost", 1883, AddressFamily.Unspecified)).WithTcpServer(bob).WithClientId(Guid.NewGuid().ToString()).Build();

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
            DateTime before = DateTime.UtcNow;
            await args.AcknowledgeAsync(CancellationToken.None);
            DateTime after = DateTime.UtcNow;
            Console.WriteLine("Time sending ack: " + after.Subtract(before).TotalMilliseconds);
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
            DateTime time1 = DateTime.UtcNow;
            await mqttClient2.PublishAsync(msg2);
            await mqttClient1ReceivedMessage.Task;
            mqttClient1ReceivedMessage = new();
            mqttClient2ReceivedMessage = new();
            DateTime after = DateTime.UtcNow;
            var diff = after.Subtract(before);
            Console.WriteLine("inv->ex diff: " + time1.Subtract(before).TotalMilliseconds);
            Console.WriteLine("ex->inv diff: " + after.Subtract(time1).TotalMilliseconds);
            Console.WriteLine("Total diff: " + diff.TotalMilliseconds);
            Console.WriteLine();
        }
    }

    private static void bob(MqttClientTcpOptions options)
    {
        options.RemoteEndpoint = new DnsEndPoint("localhost", 1883, AddressFamily.Unspecified);
        options.NoDelay = false;
    }
}
