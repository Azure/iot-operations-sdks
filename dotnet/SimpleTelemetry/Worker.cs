// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Mqtt.Session;
using Azure.Iot.Operations.Protocol.Connection;
using Azure.Iot.Operations.Protocol.Models;

namespace SimpleTelemetry
{
    public class Worker : BackgroundService
    {
        private readonly ILogger<Worker> _logger;

        public Worker(ILogger<Worker> logger)
        {
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            MqttSessionClientOptions mqttSessionClientOptions = new MqttSessionClientOptions()
            {
                RetryOnFirstConnect = false,
            };

            await using MqttSessionClient publishingClient = new MqttSessionClient(mqttSessionClientOptions);
            await using MqttSessionClient receivingClient = new MqttSessionClient(mqttSessionClientOptions);

            // use node port service name
            var connectionSettings = new MqttConnectionSettings("no-tls", Guid.NewGuid().ToString())
            {
                TcpPort = 1884, //todo
                UseTls = false,
            };

            var sendingClientId = connectionSettings.ClientId;

            await publishingClient.ConnectAsync(connectionSettings, stoppingToken);

            connectionSettings.ClientId = Guid.NewGuid().ToString();
            await receivingClient.ConnectAsync(connectionSettings, stoppingToken);
            Console.WriteLine("Both clients connected");

            TaskCompletionSource<DateTime> onMessageReceived = new TaskCompletionSource<DateTime>();
            receivingClient.ApplicationMessageReceivedAsync += (args) =>
            {
                onMessageReceived.TrySetResult(DateTime.UtcNow);
                args.AutoAcknowledge = true;
                return Task.CompletedTask;
            };

            MqttApplicationMessage sentMessage = new MqttApplicationMessage("some/topic", MqttQualityOfServiceLevel.AtLeastOnce)
            {
                Payload = new(new byte[100]), //TODO message size, TODO construct message fresh each time to mimic customer use?
                Topic = "timtay/some/topic/",
                MessageExpiryInterval = 100,
                ResponseTopic = "some/other/topic",
            };

            sentMessage.AddUserProperty("$partition", sendingClientId);

            await receivingClient.SubscribeAsync(new MqttClientSubscribeOptions("timtay/some/topic/"), stoppingToken);

            Console.WriteLine("Starting telemetry sending loop...");
            long count = 0;
            while (!stoppingToken.IsCancellationRequested)
            {
                onMessageReceived = new TaskCompletionSource<DateTime>();

                DateTime timeBeforePublishing = DateTime.UtcNow;
                await publishingClient.PublishAsync(sentMessage);
                DateTime timeAfterPuback = DateTime.UtcNow;

                DateTime timeWhenPublishIsReceived = await onMessageReceived.Task.WaitAsync(stoppingToken);

                Console.WriteLine("Latency between publishing and receiving pub ack on sender side: " + timeAfterPuback.Subtract(timeBeforePublishing).TotalMilliseconds + " milliseconds");
                Console.WriteLine("Latency between publishing and receiver receiving publish: " + timeWhenPublishIsReceived.Subtract(timeBeforePublishing).TotalMilliseconds + " milliseconds");
                Console.WriteLine("Latency between broker sending puback to sender and receiver receiving publish: " + timeAfterPuback.Subtract(timeWhenPublishIsReceived).TotalMilliseconds + " milliseconds");
                if (timeAfterPuback.Subtract(timeBeforePublishing).TotalMilliseconds > 30
                    || timeWhenPublishIsReceived.Subtract(timeBeforePublishing).TotalMilliseconds > 30)
                {
                    Console.WriteLine("!!!!!!!!!!");
                }
                Console.WriteLine();
                Console.WriteLine();

                if (count++ % 2 == 0)
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(10));
                }
            }
        }
    }
}
