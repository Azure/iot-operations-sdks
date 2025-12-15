// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Mqtt.Session;
using Azure.Iot.Operations.Protocol.Connection;
using Azure.Iot.Operations.Protocol.Models;
using Azure.Iot.Operations.Protocol.RPC;

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

            var connectionSettings = new MqttConnectionSettings("no-tls", Guid.NewGuid().ToString())
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

                    CommandResponseMetadata metadata = new()
                    {
                        UserData = request.RequestMetadata.UserData
                    };

                    metadata.UserData.Add("timtay_uponexecutereturned", DateTime.UtcNow.Ticks + "");


                    // Echo the payload back to the sender
                    return Task.FromResult(new Azure.Iot.Operations.Protocol.RPC.ExtendedResponse<PayloadObject>()
                    {
                        Response = payloadObject,
                        ResponseMetadata = metadata
                    });
                }
            };

            await commandExecutor.StartAsync(null, stoppingToken);

            Console.WriteLine("Starting command invocation loop...");
            while (!stoppingToken.IsCancellationRequested)
            {
                OnCommandReceived = new TaskCompletionSource<DateTime>();

                DateTime timeBeforeInvoke = DateTime.UtcNow;
                CommandRequestMetadata md = new();
                md.UserData.Add("timtay_beforeinvoke", DateTime.UtcNow.Ticks+"");
                var response = await commandInvoker.InvokeCommandAsync(payloadObject, md, null, null, stoppingToken);
                DateTime timeAfterInvokeReturn = DateTime.UtcNow;
                DateTime timeWhenCommandWasReceived = await OnCommandReceived.Task.WaitAsync(stoppingToken);

                //timeBeforeInvoke
                var timeAtInvokePublish = new DateTime(long.Parse(response.ResponseMetadata!.UserData["timtay_beforeinvokepublish"]));
                var timeAtExecutorReceivedPublish = new DateTime(long.Parse(response.ResponseMetadata!.UserData["timtay_uponinvokepublishreceived"]));
                //timeWhenCommandWasReceived
                var timeAtExecutorReturns = new DateTime(long.Parse(response.ResponseMetadata!.UserData["timtay_uponexecutereturned"]));
                var timeAtExecutorPublishes = new DateTime(long.Parse(response.ResponseMetadata!.UserData["timtay_beforeexecutepublish"]));
                var timeAtInvokerRecievesPublish = new DateTime(long.Parse(response.ResponseMetadata!.UserData["timtay_uponexecutepublishreceived"]));

                Console.WriteLine("Latency between invoke and publish: " + timeAtInvokePublish.Subtract(timeBeforeInvoke).TotalMilliseconds + " milliseconds");
                Console.WriteLine("Latency between invoker publish and executor receive publish: " + timeAtExecutorReceivedPublish.Subtract(timeAtInvokePublish).TotalMilliseconds + " milliseconds");
                Console.WriteLine("Latency between executor receive and executor user-callback: " + timeWhenCommandWasReceived.Subtract(timeAtExecutorReceivedPublish).TotalMilliseconds + " milliseconds");
                Console.WriteLine("Latency between command received and user callback returned: " + timeAtExecutorReturns.Subtract(timeWhenCommandWasReceived).TotalMilliseconds + " milliseconds");
                Console.WriteLine("Latency between executor user callback returns and executor publishing: " + timeAtExecutorPublishes.Subtract(timeAtExecutorReturns).TotalMilliseconds + " milliseconds");
                Console.WriteLine("Latency between executor publishing and invoker receives publish: " + timeAtInvokerRecievesPublish.Subtract(timeAtExecutorPublishes).TotalMilliseconds + " milliseconds");
                Console.WriteLine("Latency between invoker receives publish and invoker returning: " + timeAfterInvokeReturn.Subtract(timeAtInvokerRecievesPublish).TotalMilliseconds + " milliseconds");
                Console.WriteLine("Overall roundtrip latency: " + timeAfterInvokeReturn.Subtract(timeBeforeInvoke).TotalMilliseconds + " milliseconds");
                if (timeAfterInvokeReturn.Subtract(timeBeforeInvoke).TotalMilliseconds > 30)
                {
                    Console.WriteLine("!!!!!!!!!!!!!!!");
                }
                Console.WriteLine();
                Console.WriteLine();



                await Task.Delay(TimeSpan.FromMilliseconds(10), stoppingToken);
            }
        }
    }
}
