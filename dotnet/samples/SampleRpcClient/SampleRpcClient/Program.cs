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
        Console.WriteLine("Connected to broker");

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
            long? stageTwoTicks = null;
            if (args.ApplicationMessage.UserProperties != null)
            {
                foreach (MqttUserProperty mqttUserProperty in args.ApplicationMessage.UserProperties)
                {
                    if (mqttUserProperty.Name.Equals("stage2"))
                    {
                        stageTwoTicks = long.Parse(mqttUserProperty.Value);
                    }
                }
            }

            Console.WriteLine("Recieved MQTT publish from connector. Sending mRPC back to connector");
            var crm = new CommandRequestMetadata();
            long stageThreeTicks = DateTime.UtcNow.Ticks;
            crm.UserData.Add("stage3", stageThreeTicks + "");
            var rpcResponse = await rpcInvoker!.InvokeCommandAsync(new PayloadObject(), crm);
            Console.WriteLine("mRPC to connector returned.");
            long stageFiveTicks = DateTime.UtcNow.Ticks;


            long? stageFourTicks = null;
            if (rpcResponse.ResponseMetadata != null)
            {
                foreach (string key in rpcResponse.ResponseMetadata.UserData.Keys)
                {
                    if (key.Equals("stage4"))
                    {
                        stageFourTicks = long.Parse(rpcResponse.ResponseMetadata.UserData[key]);
                    }
                }
            }

            if (stageTwoTicks == null)
            {
                Console.WriteLine("Stage 2 ticks not found");
                return;
            }

            if (stageFourTicks == null)
            {
                Console.WriteLine("Stage 4 ticks not found");
                return;
            }

            Console.WriteLine("ACK'ing the recieved MQTT publish from connector.");
            await args.AcknowledgeAsync(default);

            // Delay between TCP connector publishing message and this app receiving it from the broker
            long delayOne = (stageThreeTicks - stageTwoTicks.Value) / System.TimeSpan.TicksPerMillisecond;

            // Delay between this app sending RPC and when the connector receiving it
            long delayTwo = (stageFourTicks.Value - stageThreeTicks) / System.TimeSpan.TicksPerMillisecond;

            // Delay between the connector sending mRPC response and this app receiving it
            long delayThree = (stageFiveTicks - stageFourTicks.Value) / System.TimeSpan.TicksPerMillisecond;

            Console.WriteLine();
            Console.WriteLine();
            Console.WriteLine("MQTT pub from TCP connector -> MQTT app: " + delayOne);
            Console.WriteLine("MQTT app invoke RPC -> connector receives invocation: " + delayTwo);
            Console.WriteLine("connector sends RPC response -> MQTT app receives response: " + delayThree);
            if (delayOne > 30 || delayTwo > 30 || delayThree > 30)
            {
                Console.WriteLine("~~~~~~~~~~~~~Repro~~~~~~~~~~~~~");
            }
            Console.WriteLine();
            Console.WriteLine();
        }
    }
}
