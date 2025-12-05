// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Mqtt.Session;
using Azure.Iot.Operations.Protocol.Connection;

namespace SimpleRpc
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
            await using MqttSessionClient invokerMqttClient = new MqttSessionClient();
            await using MqttSessionClient executorMqttClient = new MqttSessionClient();

            var connectionSettings = new MqttConnectionSettings("127.0.0.1", Guid.NewGuid().ToString())
            {
                TcpPort = 1884, //todo
                UseTls = false,
            };

            await invokerMqttClient.ConnectAsync(connectionSettings, stoppingToken);

            connectionSettings.ClientId = Guid.NewGuid().ToString();
            await executorMqttClient.ConnectAsync(connectionSettings, stoppingToken);

            TaskCompletionSource<DateTime> OnCommandReceived = new TaskCompletionSource<DateTime>();

            var payloadObject = new PayloadObject()
            {
                SomeField = "someValue",
                OtherField = "otherValue"
            };

            await using SimpleCommandInvoker commandInvoker = new(new(), invokerMqttClient, "someCommandName", new Utf8JsonSerializer());
            await using SimpleCommandExecutor commandExecutor = new(new(), invokerMqttClient, "someCommandName", new Utf8JsonSerializer())
            {
                OnCommandReceived = (request, cancellationToken) =>
                {
                    OnCommandReceived.TrySetResult(DateTime.UtcNow);

                    // Echo the payload back to the sender
                    return Task.FromResult(new Azure.Iot.Operations.Protocol.RPC.ExtendedResponse<PayloadObject>()
                    {
                        Response = payloadObject
                    });
                }
            };


            Console.WriteLine("Starting command invocation loop...");
            while (!stoppingToken.IsCancellationRequested)
            {
                OnCommandReceived = new TaskCompletionSource<DateTime>();

                DateTime timeBeforeInvoke = DateTime.UtcNow;
                await commandInvoker.InvokeCommandAsync(payloadObject);
                DateTime timeAfterInvoke = DateTime.UtcNow;
                DateTime timeWhenCommandWasReceived = await OnCommandReceived.Task;

                Console.WriteLine("Latency between invoking command and executor receiving command: " + timeBeforeInvoke.Subtract(timeWhenCommandWasReceived).TotalMilliseconds + " milliseconds");
                Console.WriteLine("Latency between invoking command and invoker receiving response: " + timeBeforeInvoke.Subtract(timeAfterInvoke).TotalMilliseconds + " milliseconds");
                Console.WriteLine();
                Console.WriteLine();
            }
        }
    }
}
