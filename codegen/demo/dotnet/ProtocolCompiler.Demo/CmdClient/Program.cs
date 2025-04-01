// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Linq;
using System.Globalization;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Azure.Iot.Operations.Mqtt.Session;
using Azure.Iot.Operations.Protocol;
using Azure.Iot.Operations.Protocol.Connection;
using Azure.Iot.Operations.Protocol.RPC;
using Counters.CounterCollection;

namespace Client
{
    internal sealed class CounterCollectionClient : CounterCollection.Client
    {
        public CounterCollectionClient(ApplicationContext applicationContext, IMqttPubSubClient mqttClient)
            : base(applicationContext, mqttClient)
        {
        }
    }

    internal sealed class Program
    {
        private enum CounterCommand
        {
            Increment,
            GetLocation
        }

        const string clientId = "DotnetCounterClient";

        static async Task Main(string[] args)
        {
            if (args.Length < 2)
            {
                Console.WriteLine("Usage: CmdClient {INC|GET} counter_name");
                return;
            }

            CounterCommand command = args[0].ToLowerInvariant() switch
            {
                "inc" => CounterCommand.Increment,
                "get" => CounterCommand.GetLocation,
                _ => throw new ArgumentException("command must be INC or GET")
            };

            string counterName = args[1];

            ApplicationContext appContext = new();
            MqttSessionClient mqttSessionClient = new();

            Console.Write($"Connecting to MQTT broker as {clientId} ... ");
            await mqttSessionClient.ConnectAsync(new MqttConnectionSettings("localhost", clientId) { TcpPort = 1883, UseTls = false });
            Console.WriteLine("Connected!");

            CounterCollectionClient client = new(appContext, mqttSessionClient);

            Console.WriteLine("Sending request");

            try
            {
                switch (command)
                {
                    case CounterCommand.Increment:
                        IncrementResponsePayload incResponse = await client.IncrementAsync(new IncrementRequestPayload { CounterName = counterName });
                        Console.WriteLine($"New value = {incResponse.CounterValue}");
                        break;
                    case CounterCommand.GetLocation:
                        GetLocationResponsePayload getResponse = await client.GetLocationAsync(new GetLocationRequestPayload { CounterName = counterName });
                        Console.WriteLine(getResponse.CounterLocation == null ? "counter is not deployed in the field" :
                            $"Location = ({getResponse.CounterLocation.Latitude}, {getResponse.CounterLocation.Longitude})");
                        break;
                }
            }
            catch (CounterErrorException counterException)
            {
                Console.WriteLine($"Request failed with exception: '{counterException.Message}'");

                switch (counterException.CounterError.Condition)
                {
                    case ConditionSchema.CounterNotFound:
                        Console.WriteLine($"Counter '{counterName}' was not found");
                        break;
                    case ConditionSchema.CounterOverflow:
                        Console.WriteLine($"Counter '{counterName}' has overflowed");
                        break;
                }
            }
        }
    }
}
