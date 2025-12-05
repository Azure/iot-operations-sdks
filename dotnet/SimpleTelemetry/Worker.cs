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
            await using MqttSessionClient publishingClient = new MqttSessionClient();
            await using MqttSessionClient receivingClient = new MqttSessionClient();

            var connectionSettings = new MqttConnectionSettings("127.0.0.1", Guid.NewGuid().ToString())
            {
                TcpPort = 1884, //todo
                UseTls = false,
            };

            await publishingClient.ConnectAsync(connectionSettings, stoppingToken);

            connectionSettings.ClientId = Guid.NewGuid().ToString();
            await receivingClient.ConnectAsync(connectionSettings, stoppingToken);

            TaskCompletionSource<DateTime> onMessageReceived = new TaskCompletionSource<DateTime>();
            receivingClient.ApplicationMessageReceivedAsync += (args) =>
            {
                onMessageReceived.TrySetResult(DateTime.UtcNow);
                args.AutoAcknowledge = true;
                return Task.CompletedTask;
            };

            MqttApplicationMessage sentMessage = new MqttApplicationMessage("some/topic", MqttQualityOfServiceLevel.AtLeastOnce)
            {
                Payload = new(new byte[100]) //TODO message size, TODO construct message fresh each time to mimic customer use?
            };

            Console.WriteLine("Starting telemetry sending loop...");
            while (!stoppingToken.IsCancellationRequested)
            {
                onMessageReceived = new TaskCompletionSource<DateTime>();

                DateTime timeBeforePublishing = DateTime.UtcNow;
                await publishingClient.PublishAsync(sentMessage);
                DateTime timeAfterPuback = DateTime.UtcNow;

                DateTime timeWhenPublishIsReceived = await onMessageReceived.Task;

                Console.WriteLine("Latency between publishing and receiving pub ack on sender side: " + timeAfterPuback.Subtract(timeBeforePublishing).TotalMilliseconds + " milliseconds");
                Console.WriteLine("Latency between publishing and receiver receiving publish: " + timeBeforePublishing.Subtract(timeWhenPublishIsReceived).TotalMilliseconds + " milliseconds");
                Console.WriteLine("Latency between broker sending puback to sender and receiver receiving publish: " + timeAfterPuback.Subtract(timeWhenPublishIsReceived).TotalMilliseconds + " milliseconds");
                Console.WriteLine();
                Console.WriteLine();
            }
        }
    }
}
