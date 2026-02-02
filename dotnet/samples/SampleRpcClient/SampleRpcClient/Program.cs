// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Diagnostics;
using Azure.Iot.Operations.Mqtt.Session;
using Azure.Iot.Operations.Protocol.Connection;
using Azure.Iot.Operations.Protocol.Events;
using Azure.Iot.Operations.Protocol.Models;
using Azure.Iot.Operations.Protocol.RPC;
using SimpleRpcClient;

internal class Program
{
    private static SampleCommandInvoker? rpcInvoker;

    private static async Task Main()
    {
        await using MqttSessionClient mqttClient = new(new MqttSessionClientOptions { EnableMqttLogging = false });
        var mcs = new MqttConnectionSettings("127.0.0.1", Guid.NewGuid().ToString())
        {
            TcpPort = 1883,
            UseTls = false
        };
        await mqttClient.ConnectAsync(mcs);

        rpcInvoker = new(new(), mqttClient, "someCommandName", new Utf8JsonSerializer());

        mqttClient.ApplicationMessageReceivedAsync += MessageReceivedCallbackAsync;

        await mqttClient.SubscribeAsync(new MqttClientSubscribeOptions("timtay/topic"));

        Console.WriteLine("Waiting for MQTT publish to trigger mRPC call back to connector");

        await Task.Delay(TimeSpan.FromHours(24));
    }

    private static async Task MessageReceivedCallbackAsync(MqttApplicationMessageReceivedEventArgs args)
    {
        if (args.ApplicationMessage.Topic.Equals("timtay/topic"))
        {
            Console.WriteLine("Recieved MQTT publish from connector. Sending mRPC back to connector");
            DateTime beforeInvoke = DateTime.Now;
            await rpcInvoker!.InvokeCommandAsync(new PayloadObject());
            DateTime afterInvoke = DateTime.Now;

            Console.WriteLine("ACK'ing the recieved MQTT publish from connector.");
            await args.AcknowledgeAsync(default);

            TimeSpan delay = afterInvoke - beforeInvoke;
            if (delay.TotalMilliseconds > 30)
            {
                Console.WriteLine("DELAY");
            }
        }
    }
}
