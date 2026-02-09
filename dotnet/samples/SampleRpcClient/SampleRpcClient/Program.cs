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
        var mcs = new MqttConnectionSettings("127.0.0.1", "timtay-dotnet-invoker")
        {
            TcpPort = 1883,
            UseTls = false
        };
        await mqttClient.ConnectAsync(mcs);
        Console.WriteLine("Connected to broker");

        rpcInvoker = new(new(), mqttClient, "someCommandName", new Utf8JsonSerializer());
        int rpcNumber = 0;

        while (true)
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(3));
                Console.WriteLine("Sending mRPC to connector");
                var crm = new CommandRequestMetadata();
                long stageThreeTicks = DateTime.UtcNow.Ticks;
                crm.UserData.Add("stage3", stageThreeTicks + "");
                var rpcResponse = await rpcInvoker!.InvokeCommandAsync(new PayloadObject() { Count = rpcNumber }, crm);
                Console.WriteLine("mRPC to connector returned.");
                long stageFiveTicks = DateTime.UtcNow.Ticks; //note it repros at this commit (not this line in particular)


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

                if (stageFourTicks == null)
                {
                    Console.WriteLine("Stage 4 ticks not found");
                    return;
                }


                // Delay between this app sending RPC and when the connector receiving it
                long delayTwo = (stageFourTicks.Value - stageThreeTicks) / System.TimeSpan.TicksPerMillisecond;

                // Delay between the connector sending mRPC response and this app receiving it
                long delayThree = (stageFiveTicks - stageFourTicks.Value) / System.TimeSpan.TicksPerMillisecond;

                Console.WriteLine();
                Console.WriteLine();
                Console.WriteLine("For RPC repsonse message : " + rpcNumber);
                Console.WriteLine("MQTT app invoke RPC -> connector receives invocation: " + delayTwo);
                Console.WriteLine("connector sends RPC response -> MQTT app receives response: " + delayThree);
                if (delayTwo > 30 || delayThree > 30)
                {
                    Console.WriteLine("~~~~~~~~~~~~~Repro~~~~~~~~~~~~~");
                }
                Console.WriteLine();
                Console.WriteLine();
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                //Console.WriteLine(e.StackTrace);
            }

            rpcNumber++;
        }
    }
}
